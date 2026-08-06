using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Compression;
using BenchmarkDotNet.Attributes;
using System.Net.Sockets;
using System.Buffers;
using System.Net;

namespace Verstack.Engine.Network.Benchmark;

[ShortRunJob]
[MemoryDiagnoser]
public class PacketOutboundBenchmarks
{
    private byte[] _testPayload;
    private FakeNetworkChannel _channel;
    private IPacketCompressor _compressor;

    [GlobalSetup]
    public void Setup()
    {
        _testPayload = new byte[200];
        _channel = new FakeNetworkChannel();
        _compressor = new IdentityCompressor();
    }

    [Benchmark]
    public int SendSingle()
    {
        var outbound = new PacketOutbound(_channel, _compressor);
        try
        {
            var writer = outbound.Begin();
            writer.WriteVarInt(42)
                 .WriteSpan(_testPayload);
            outbound.Commit(ref writer);
            
            outbound.Flush();
        }
        finally
        {
            outbound.Dispose();
        }
        
        _channel.ClearOutboundQueue();
        
        return _testPayload.Length;
    }

    [Benchmark]
    public int SendThree()
    {
        var outbound = new PacketOutbound(_channel, _compressor);
        try
        {
            var writer = outbound.Begin();
            writer.WriteVarInt(42).WriteSpan(_testPayload);
            outbound.Commit(ref writer);

            writer = outbound.Begin();
            writer.WriteVarInt(42).WriteSpan(_testPayload);
            outbound.Commit(ref writer);

            writer = outbound.Begin();
            writer.WriteVarInt(42).WriteSpan(_testPayload);
            outbound.Commit(ref writer);

            outbound.Flush();
        }
        finally
        {
            outbound.Dispose();
        }
        
        _channel.ClearOutboundQueue();
        
        return _testPayload.Length;
    }

    [Benchmark]
    public int SendWithCompressionEnable()
    {
        var outbound = new PacketOutbound(_channel, _compressor);
        try
        {
            var writer = outbound.Begin();
            writer.WriteVarInt(42).WriteSpan(_testPayload);
            outbound.Commit(ref writer);

            outbound.EnableCompression(256);

            writer = outbound.Begin();
            writer.WriteVarInt(42).WriteSpan(_testPayload);
            outbound.Commit(ref writer);

            outbound.Flush();
        }
        finally
        {
            outbound.Dispose();
        }
        
        _channel.ClearOutboundQueue();
        
        return _testPayload.Length;
    }

    private class FakeNetworkChannel() : NetworkChannel(CreateDummySocket())
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