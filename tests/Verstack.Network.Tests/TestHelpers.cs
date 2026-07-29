using Verstack.Network.Compression;
using Verstack.Network.Packet;
using System.Net.Sockets;
using Leopotam.EcsProto;
using System.Buffers;
using System.Net;

namespace Verstack.Network.Tests;

internal class IdentityCompressor : IPacketCompressor
{
    public int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        source.CopyTo(destination);
        return source.Length;
    }
    public int GetMaxCompressedSize(int sourceLength) => sourceLength;
}

internal class IdentityDecompressor : IPacketDecompressor
{
    public void Decompress(ReadOnlySequence<byte> source, Span<byte> destination)
    {
        foreach (var seg in source)
        {
            seg.Span.CopyTo(destination);
            destination = destination[seg.Length..];
        }
    }
}

internal class FakeNetworkChannel : NetworkChannel
{
    public FakeNetworkChannel() : base(CreateDummySocket()) { }

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

internal class DummyBundle : PacketBundle
{
    public override int StepCount => 1;
    public override PacketHandleResult TryProcess(int stepIndex, ProtoEntity entity, in RawPacket packet, ref PacketOutbound outbound)
        => PacketHandleResult.Accepted;
}