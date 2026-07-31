// using System;
// using System.Buffers;
// using BenchmarkDotNet.Attributes;
// using Verstack.Network;
// using Verstack.Network.Compression;
// using Verstack.Network.DataTypes;
// using Verstack.Network.Packet;
//
// [ShortRunJob]
// [MemoryDiagnoser]
// public class PacketOutboundBenchmarks
// {
//     private byte[] _frameScratch;
//     private byte[] _payloadBuffer;
//     private byte[] _testPayload;
//     private FakeNetworkChannel _channel;
//     private IPacketCompressor _compressor;
//     private const int MaxFrameSize = 65536;
//     private const int MaxPayloadSize = 32768;
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
//         _payloadBuffer = new byte[MaxPayloadSize];
//         _channel = new FakeNetworkChannel { CompressionThreshold = -1 };
//         _compressor = new IdentityCompressor();
//     }
//
//     [Benchmark]
//     public int SendSingle()
//     {
//         var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
//         outbound.Send(_testPayload);
//         return outbound.Written;
//     }
//
//     [Benchmark]
//     public int SendThree()
//     {
//         var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
//         outbound.Send(_testPayload);
//         outbound.Send(_testPayload);
//         outbound.Send(_testPayload);
//         return outbound.Written;
//     }
//
//     [Benchmark]
//     public int SendWithCompressionEnable()
//     {
//         var outbound = new PacketOutbound(_channel, _compressor, _frameScratch, _payloadBuffer);
//         outbound.Send(_testPayload);
//         outbound.EnableCompression(256);
//         outbound.Send(_testPayload);
//         return outbound.Written;
//     }
// }
//
// // Заглушка NetworkChannel
// internal class FakeNetworkChannel : NetworkChannel
// {
//     public FakeNetworkChannel() : base(CreateDummySocket()) { }
//     private static System.Net.Sockets.Socket CreateDummySocket()
//     {
//         var s = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
//             System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
//         // привязываем к loopback, чтобы избежать ошибок
//         s.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
//         s.Listen(0);
//         var client = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,
//             System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
//         client.Connect(s.LocalEndPoint);
//         var server = s.Accept();
//         s.Close();
//         return server;
//     }
//     // CompressionThreshold уже определён в базовом классе, просто оставляем как есть.
// }