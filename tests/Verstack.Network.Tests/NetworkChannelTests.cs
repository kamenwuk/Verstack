namespace Verstack.Network.Tests;

public class NetworkChannelTests
{
    [Fact]
    public void EnqueueOutbound_AddsToQueue()
    {
        var channel = new FakeNetworkChannel();
        byte[] data = new byte[] { 1, 2, 3 };
        channel.EnqueueOutbound(data);
        Assert.True(channel.OutboundQueue.TryDequeue(out var chunk));
        Assert.Equal(3, chunk.Length);
        Assert.Equal(1, chunk.Buffer[0]);
        channel.Disconnect(); // очистка
    }

    [Fact]
    public void Disconnect_CleansUp()
    {
        var channel = new FakeNetworkChannel();
        channel.Disconnect();
        // нет исключений — успех
        Assert.True(true);
    }
}