using Verstack.Network.Packet;

namespace Verstack.Network.Tests;

public sealed class PacketPipelineTests
{
    [Fact]
    public void ProcessAccepted_MovesToNextBundle()
    {
        var bundles = new PacketBundle[] { new DummyBundle() };
        var pipeline = new PacketPipeline(null, bundles);
        var state = new PacketFlowState(0, 0);
        var rawPacket = new RawPacket(0, new byte[1]);
        var outbound = new PacketOutbound(
            new FakeNetworkChannel(),
            new IdentityCompressor(),
            new byte[1024],
            new byte[512]);

        var result = pipeline.TryProcessPacket(default, rawPacket, ref outbound, ref state);
        Assert.True(result == PacketHandleResult.Accepted);
        Assert.Equal(1, state.BundleIndex);   // перешли на следующий бандл
        Assert.Equal(0, state.StepIndex);     // сброшен
    }
}