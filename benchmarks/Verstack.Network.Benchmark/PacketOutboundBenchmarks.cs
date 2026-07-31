using Verstack.Network.Packet.Writers;
using Verstack.Network.Compression;
using BenchmarkDotNet.Attributes;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using Verstack.Network;
using System.Buffers;

[ShortRunJob]
[MemoryDiagnoser]
public sealed class PacketOutboundBenchmarks
{
    private byte[] _frameScratch;
    private byte[] _payloadBuffer;
    private byte[] _testPayload;
    private FakeNetworkChannel _channel;
    private IPacketCompressor _compressor;
    private const int MaxFrameSize = 65536;
    private const int MaxPayloadSize = 32768;

    [GlobalSetup]
    public void Setup()
    {
        var payloadWriter = new ArrayBufferWriter<byte>();
        VarInt.Write(payloadWriter, 42);
        payloadWriter.Write(new byte[200]);
        _testPayload = payloadWriter.WrittenSpan.ToArray();

        _frameScratch = new byte[MaxFrameSize];
        _payloadBuffer = new byte[MaxPayloadSize];
        _channel = new FakeNetworkChannel { CompressionThreshold = -1 };
        _compressor = new IdentityCompressor();
    }

    [Benchmark]
    public int SendSingle()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
        
        var writer = outbound.Begin();
        writer.WriteVarInt(42)
             .WriteSpanRaw(_testPayload);
        outbound.Commit(ref writer);
        
        outbound.Flush();
        
        // Очищаем очередь, чтобы не словить OOM на миллионах итераций BDN
        _channel.ClearOutboundQueue();
        
        return _testPayload.Length; // Возвращаем значение, чтобы компилятор не вырезал код
    }

    [Benchmark]
    public int SendThree()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);

        // Пакет 1
        var writer = outbound.Begin();
        writer.WriteVarInt(42).WriteSpanRaw(_testPayload);
        outbound.Commit(ref writer);

        // Пакет 2
        writer = outbound.Begin();
        writer.WriteVarInt(42).WriteSpanRaw(_testPayload);
        outbound.Commit(ref writer);

        // Пакет 3
        writer = outbound.Begin();
        writer.WriteVarInt(42).WriteSpanRaw(_testPayload);
        outbound.Commit(ref writer);

        outbound.Flush();
        _channel.ClearOutboundQueue();
        
        return _testPayload.Length;
    }

    [Benchmark]
    public int SendWithCompressionEnable()
    {
        var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);

        // Пакет 1 (несжатый)
        var writer = outbound.Begin();
        writer.WriteVarInt(42).WriteSpanRaw(_testPayload);
        outbound.Commit(ref writer);

        outbound.EnableCompression(256);

        // Пакет 2 (сжатый)
        writer = outbound.Begin();
        writer.WriteVarInt(42).WriteSpanRaw(_testPayload);
        outbound.Commit(ref writer);

        outbound.Flush();
        _channel.ClearOutboundQueue();
        
        return _testPayload.Length;
    }

    private class FakeNetworkChannel() : NetworkChannel(CreateDummySocket())
    {
        private static System.Net.Sockets.Socket CreateDummySocket()
        {
            var s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            s.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
            s.Listen(0);
            var client = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            client.Connect(s.LocalEndPoint);
            var server = s.Accept();
            s.Close();
            return server;
        }

        /// <summary>
        /// Очищает очередь отправки, возвращая массивы в пул.
        /// </summary>
        public void ClearOutboundQueue()
        {
            while (OutboundQueue.TryDequeue(out var chunk))
            {
                // ВАЖНО: возвращаем массив в пул, иначе ArrayPool опустошится 
                // и бенчмарк будет аллоцировать память на каждой итерации!
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