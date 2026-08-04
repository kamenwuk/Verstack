using BenchmarkDotNet.Attributes;
using System.Net.Sockets;

namespace Verstack.Engine.Network.Benchmark;

[ShortRunJob]
[MemoryDiagnoser]
public class NetworkChannelBenchmarks
{
    private NetworkChannel _channel;
    private byte[] _payload;

    [GlobalSetup]
    public void Setup()
    {
        _payload = new byte[200];
        new Random(42).NextBytes(_payload);

        // создаём рабочий канал с dummy-сокетом
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        listener.Listen(1);
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(listener.LocalEndPoint);
        var server = listener.Accept();
        listener.Close();
        _channel = new NetworkChannel(server);
    }

    [Benchmark]
    public void EnqueueOutbound()
    {
        _channel.EnqueueOutbound(_payload);
        // очищаем очередь, чтобы не накапливать
        while (_channel.OutboundQueue.TryDequeue(out _)) { }
    }

    [GlobalCleanup]
    public void Cleanup() => _channel?.Disconnect();
}