using System.Net.Sockets;
using System.Net;

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

    private class FakeNetworkChannel() : NetworkChannel(CreateDummySocket())
    {
        private static Socket CreateDummySocket()
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
            EndPoint? remoteEp = listener.LocalEndPoint;
            if (remoteEp is null)
                throw new InvalidOperationException("LocalEndPoint was null after Bind");

            client.Connect(remoteEp);
            var server = listener.Accept();
            listener.Close();
            return server;
        }
    }
}