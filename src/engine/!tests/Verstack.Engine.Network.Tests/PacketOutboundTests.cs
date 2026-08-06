using Verstack.Engine.Network.Packet.Outbound;
using Verstack.Engine.Network.Compression;
using System.Net.Sockets;
using System.Buffers;
using System.Net;

namespace Verstack.Engine.Network.Tests;

public sealed class PacketOutboundTests
{
    // Фейковый компрессор, который просто копирует данные (сжимает в 1:1)
    private sealed class IdentityCompressor : IPacketCompressor
    {
        public int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            source.CopyTo(destination);
            return source.Length;
        }

        public int GetMaxCompressedSize(int sourceSize) => sourceSize;
    }

    // Создаем канал без реального подключения. Socket создаётся, но не коннектится.
    private static NetworkChannel CreateFakeChannel()
    {
        // Создаем локальный слушатель, чтобы получить два подключенных друг к другу сокета
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(listener.LocalEndPoint!);
        
        var server = listener.Accept();
        client.Close(); // Нам нужен только серверный сокет для NetworkChannel

        return new NetworkChannel(server);
    }

    [Fact]
    public void Commit_OnePacket_SendsDataOnFlush()
    {
        var channel = CreateFakeChannel();
        var outbound = new PacketOutbound(channel, new IdentityCompressor());
        
        var writer = outbound.Begin();
        writer.WriteVarInt(42);
        outbound.Commit(ref writer);
        
        outbound.Flush();

        Assert.False(channel.OutboundQueue.IsEmpty);
        
        // Очистка ресурсов после теста
        while (channel.OutboundQueue.TryDequeue(out var chunk))
            ArrayPool<byte>.Shared.Return(chunk.Buffer);
    }

    [Fact]
    public void Buffer_Grows_Automatically_When_Payload_Exceeds_Initial_Size()
    {
        var channel = CreateFakeChannel();
        var outbound = new PacketOutbound(channel, new IdentityCompressor());
        
        var writer = outbound.Begin();
        
        // Пишем 10 000 интов (40 000 байт). Изначальный буфер 2048 байт.
        // Он должен увеличиться внутри EnsureCapacity без ошибок Overflow.
        for (int i = 0; i < 10000; i++)
        {
            writer.WriteInt(i);
        }
        
        outbound.Commit(ref writer);
        outbound.Flush();

        Assert.True(channel.OutboundQueue.TryDequeue(out var chunk));
        // 40000 байт payload + ~3 байта на VarInt длины
        Assert.True(chunk.Length > 40000);
        
        ArrayPool<byte>.Shared.Return(chunk.Buffer);
    }

    [Fact]
    public void EnableCompression_ChangesThreshold()
    {
        var channel = CreateFakeChannel();
        var outbound = new PacketOutbound(channel, new IdentityCompressor());
        
        outbound.EnableCompression(256);
        
        Assert.Equal(256, channel.CompressionThreshold);
    }

//     [Fact]
//     public void Flush_WithoutCommit_ThrowsInDebug()
//     {
//         var channel = CreateFakeChannel();
//         var outbound = new PacketOutbound(channel, new IdentityCompressor());
//         
//         var writer = outbound.Begin();
//         writer.WriteVarInt(42);
//         
//         // Намеренно НЕ вызываем outbound.Commit(ref writer);
//         
// #if DEBUG
//         bool threw = false;
//         try
//         {
//             outbound.Flush();
//         }
//         catch (InvalidOperationException)
//         {
//             threw = true;
//         }
//         Assert.True(threw, "Ожидалось исключение InvalidOperationException в DEBUG-режиме.");
// #endif
//     }

    [Fact]
    public void Begin_TwiceWithoutCommit_ThrowsInDebug()
    {
        var channel = CreateFakeChannel();
        var outbound = new PacketOutbound(channel, new IdentityCompressor());
        
        var writer = outbound.Begin();
        writer.WriteVarInt(42);
        
        // Намеренно НЕ вызываем outbound.Commit(ref writer);
        
#if DEBUG
        bool threw = false;
        try
        {
            outbound.Begin();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Assert.True(threw, "Ожидалось исключение InvalidOperationException в DEBUG-режиме.");
#endif
    }
}