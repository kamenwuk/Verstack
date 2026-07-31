// using System;
// using System.Buffers;
// using BenchmarkDotNet.Attributes;
// using Verstack.Network.Compression;
// using Verstack.Network.DataTypes;
// using Verstack.Network.Packet;
//
// [ShortRunJob]
// [MemoryDiagnoser]
// public class PacketFrameBenchmarks
// {
//     private byte[] _testPayload;
//     private byte[] _frameScratch;
//     private IdentityCompressor _compressor;
//     private IdentityDecompressor _decompressor;
//     private const int MaxFrameSize = 65536;
//
//     [GlobalSetup]
//     public void Setup()
//     {
//         var payloadWriter = new ArrayBufferWriter<byte>();
//         VarInt.Write(payloadWriter, 42);
//         payloadWriter.Write(new byte[200]);
//         _testPayload = payloadWriter.WrittenSpan.ToArray();
//
//         _frameScratch = new byte[MaxFrameSize];
//         _compressor = new IdentityCompressor();
//         _decompressor = new IdentityDecompressor();
//     }
//
//     [Benchmark]
//     public int Write_Uncompressed()
//     {
//         var writer = new SpanWriter(_frameScratch);
//         PacketFrame.Write(ref writer, _testPayload, _compressor, -1);
//         return writer.Written;
//     }
//
//     [Benchmark]
//     public int Write_Compressed_BelowThreshold()
//     {
//         var writer = new SpanWriter(_frameScratch);
//         PacketFrame.Write(ref writer, _testPayload, _compressor, 1000);
//         return writer.Written;
//     }
//
//     [Benchmark]
//     public int Write_Compressed_AboveThreshold()
//     {
//         var writer = new SpanWriter(_frameScratch);
//         PacketFrame.Write(ref writer, _testPayload, _compressor, 50);
//         return writer.Written;
//     }
//
//     private ReadOnlySequence<byte> PrepareFrame(ReadOnlySpan<byte> payload, int threshold)
//     {
//         var buf = new ArrayBufferWriter<byte>();
//         var sw = new SpanWriter(buf.GetSpan(1024));
//         PacketFrame.Write(ref sw, payload, _compressor, threshold);
//         buf.Advance(sw.Written);
//         return new ReadOnlySequence<byte>(buf.WrittenMemory);
//     }
//
//     [Benchmark]
//     public PacketFrameResult TryRead_Uncompressed()
//     {
//         var seq = PrepareFrame(_testPayload, -1);
//         return PacketFrame.TryRead(seq, -1, _decompressor, out _, out _, out _);
//     }
//
//     [Benchmark]
//     public PacketFrameResult TryRead_Compressed_BelowThreshold()
//     {
//         var seq = PrepareFrame(_testPayload, 1000);
//         return PacketFrame.TryRead(seq, 1000, _decompressor, out _, out _, out _);
//     }
//
//     [Benchmark]
//     public PacketFrameResult TryRead_Compressed_AboveThreshold()
//     {
//         var seq = PrepareFrame(_testPayload, 50);
//         return PacketFrame.TryRead(seq, 50, _decompressor, out _, out _, out _);
//     }
// }
//
// // Заглушки без сжатия
// internal class IdentityCompressor : IPacketCompressor
// {
//     public int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
//     {
//         source.CopyTo(destination);
//         return source.Length;
//     }
//     public int GetMaxCompressedSize(int sourceLength) => sourceLength;
// }
//
// internal class IdentityDecompressor : IPacketDecompressor
// {
//     public void Decompress(ReadOnlySequence<byte> source, Span<byte> destination)
//     {
//         foreach (var seg in source) { seg.Span.CopyTo(destination); destination = destination[seg.Length..]; }
//     }
// }