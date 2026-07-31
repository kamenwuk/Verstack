using Verstack.Network.Packet.Writers;
using Verstack.Network.Compression;
using BenchmarkDotNet.Attributes;
using Verstack.Network.Packet;
using System.Net.Sockets;
using Leopotam.EcsProto;
using System.Buffers;
using System.Net;

namespace Verstack.Network.Benchmarks;

[MemoryDiagnoser]
public class PacketPipelineBenchmarks
{
    // 50 000 итераций дадут ~100 мс на один вызов, что идеально для BDN
    private const int BATCH_SIZE = 50000; 
    
    private PacketPipeline _pipeline;
    private FakeNetworkChannel _channel;
    private ProtoEntity _dummyEntity;
    private RawPacket _rawPacket;

    [GlobalSetup]
    public void Setup()
    {
        var bundles = new PacketBundle[] { new DummyBundle() };
        _pipeline = new PacketPipeline(null!, new IdentityCompressor(), bundles);
        _channel = new FakeNetworkChannel();
        _dummyEntity = default;
        
        _rawPacket = new RawPacket(42, new byte[100]);

        // Предзаполняем очередь входящих пакетов один раз.
        // Аллокации в GlobalSetup НЕ учитываются MemoryDiagnoser.
        // 10 миллионов пакетов хватит на любую прогонку BDN.
        for (int i = 0; i < 10_000_000; i++)
        {
            _channel.IncomingPackets.Enqueue(_rawPacket);
        }
    }

    [Benchmark]
    public void ProcessSession()
    {
        for (int i = 0; i < BATCH_SIZE; i++)
        {
            // Сбрасываем состояние для каждого пакета
            var state = new PacketFlowState { BundleIndex = 0, StepIndex = 0 };
            _pipeline.ProcessSession(_dummyEntity, _channel, ref state);
            
            // КРИТИЧЕСКИ ВАЖНО: Возвращаем массивы в ArrayPool сразу после обработки,
            // иначе пул переполнится и начнет аллоцировать память в куче (managed heap)!
            _channel.ClearOutboundQueue();
        }
    }

    private class DummyBundle : PacketBundle
    {
        public override int StepCount => 1;

        public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
        {
            var writer = outbound.Begin();
            writer.WriteVarInt(0x00);
            outbound.Commit(ref writer);
            return PacketHandleResult.Accepted;
        }
    }

    private sealed class FakeNetworkChannel() : NetworkChannel(CreateDummySocket())
    {
        private static Socket CreateDummySocket()
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(listener.LocalEndPoint!);
            
            var server = listener.Accept();
            listener.Close();
            client.Close();
            return server;
        }

        public void ClearOutboundQueue()
        {
            while (OutboundQueue.TryDequeue(out var chunk))
            {
                if (chunk.Buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(chunk.Buffer);
                }
            }
        }
    }

    private sealed class IdentityCompressor : IPacketCompressor
    {
        public int GetMaxCompressedSize(int sourceLength) => sourceLength;

        public int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            source.CopyTo(destination);
            return source.Length;
        }
    }
}