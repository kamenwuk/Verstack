using Verstack.Engine.Network.Packet.Pipeline;
using Verstack.Engine.Network.Packet;
using System.Net.Sockets;
using Leopotam.EcsProto;
using System.Net;

namespace Verstack.Engine.Network.Tests;

public sealed class SequentialPacketPipelineTests
{
    private static NetworkChannel CreateFakeChannel()
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(listener.LocalEndPoint!);
        
        var server = listener.Accept();
        client.Close();

        return new NetworkChannel(server);
    }

    // Бандл, который проверяет логику Continue (тот же пакет идет на следующий шаг)
    private sealed class ContinueBundle : PacketBundle
    {
        public override int StepCount => 2;

        public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
        {
            if (packet.Id != 0x01) return PacketHandleResult.Kick;

            if (stepIndex == 0) return PacketHandleResult.Continue;
            if (stepIndex == 1) return PacketHandleResult.Accepted;

            return PacketHandleResult.Kick;
        }
    }

    // Бандл, который ждет конкретный пакет, игнорируя остальные
    private sealed class IgnoreBundle : PacketBundle
    {
        public override int StepCount => 1;

        public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
        {
            if (packet.Id == 0x05) return PacketHandleResult.Accepted;
            return PacketHandleResult.Ignored;
        }
    }

    [Fact]
    public void ProcessSession_WithContinue_FeedsSamePacket_ToNextStep()
    {
        var channel = CreateFakeChannel();
        // Кладем только один пакет. Continue должен прогнать его через оба шага.
        channel.IncomingPackets.Enqueue(new RawPacket(0x01, []));
        
        var pipeline = new SequentialPacketPipeline(null!, null!, [new ContinueBundle()]);
        var state = new PacketFlowState { BundleIndex = 0, StepIndex = 0 };

        var status = pipeline.ProcessSession(default, channel, ref state);

        // Пакет прошел step 0 (Continue) и step 1 (Accepted). Бандл завершен.
        // Так как это был единственный бандл, конвейер завершен -> статус Transfer.
        Assert.Equal(PipelineSessionStatus.Transfer, status);
        Assert.Equal(1, state.BundleIndex); // Перешли на несуществующий бандл
        Assert.Equal(0, state.StepIndex);
    }

    [Fact]
    public void ProcessSession_WithIgnored_ConsumesPacket_And_KeepsStep()
    {
        var channel = CreateFakeChannel();
        channel.IncomingPackets.Enqueue(new RawPacket(0x99, [])); // Мусор
        channel.IncomingPackets.Enqueue(new RawPacket(0x05, [])); // Нужный пакет
        
        var pipeline = new SequentialPacketPipeline(null!, null!, [new IgnoreBundle()]);
        var state = new PacketFlowState { BundleIndex = 0, StepIndex = 0 };

        var status = pipeline.ProcessSession(default, channel, ref state);

        // Мусорный пакет был поглощен (Ignored), нужный пакет завершает бандл (Accepted)
        Assert.Equal(PipelineSessionStatus.Transfer, status);
        Assert.Equal(1, state.BundleIndex); 
        Assert.Equal(0, state.StepIndex);
    }

    [Fact]
    public void ProcessSession_WhenKick_ReturnsKick_And_DoesNotAdvance()
    {
        var channel = CreateFakeChannel();
        channel.IncomingPackets.Enqueue(new RawPacket(0xFF, [])); // Неверный пакет
        
        var pipeline = new SequentialPacketPipeline(null!, null!, [new ContinueBundle()]);
        var state = new PacketFlowState { BundleIndex = 0, StepIndex = 0 };

        var status = pipeline.ProcessSession(default, channel, ref state);

        Assert.Equal(PipelineSessionStatus.Kick, status);
        Assert.Equal(0, state.BundleIndex); // Остались на месте
        Assert.Equal(0, state.StepIndex);   // Остались на месте
    }

    [Fact]
    public void ProcessSession_WhenBundleIndexExceedsCount_ReturnsTransfer()
    {
        var channel = CreateFakeChannel();
        channel.IncomingPackets.Enqueue(new RawPacket(0x01, []));
        
        var pipeline = new SequentialPacketPipeline(null!, null!, [new IgnoreBundle()]);
        // Устанавливаем состояние, будто бандлы уже пройдены
        var state = new PacketFlowState { BundleIndex = 1, StepIndex = 0 };

        var status = pipeline.ProcessSession(default, channel, ref state);

        Assert.Equal(PipelineSessionStatus.Transfer, status);
    }
}