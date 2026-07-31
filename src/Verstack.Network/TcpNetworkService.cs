using Verstack.Network.Compression;
using Verstack.Network.Lifecycle;
using Verstack.Network.Packet;
using Leopotam.EcsProto.QoL;
using System.IO.Pipelines;
using System.Net.Sockets;
using Leopotam.EcsProto;
using Verstack.Debug;
using System.Buffers;
using System.Net;

namespace Verstack.Network
{
    internal sealed class TcpNetworkService : IProtoInitService, IProtoDestroyService
    {
        [DI] private readonly NetworkHandoffRouter _handoffRouter = null!;
        [DI] private readonly ZLibPacketDecompressor _decompressor = null!;
        
        private readonly int _port;
        
        private CancellationTokenSource _cts;
        private Socket _listener;
        
        internal TcpNetworkService(int port)
        {
            _port = port;
        }
        
        public void Init(IProtoSystems systems)
        {
            _cts = new CancellationTokenSource();
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, _port));
            _listener.Listen(100);

            _ = AcceptLoopAsync(_cts.Token);
        }

        public void Destroy()
        {
            _cts?.Cancel();
            _listener?.Close();
        }

        private async Task AcceptLoopAsync(CancellationToken cts)
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    Socket client = await _listener.AcceptAsync(cts);
                    Logger.Info(LogKey.NetworkNewConnection, client.RemoteEndPoint);

                    var channel = new NetworkChannel(client);
                    // Просто кидаем новый канал в очередь. Никаких Handshake!
                    _handoffRouter.HandleConnect(channel);

                    // Запускаем read-цикл и send-цикл параллельно: каждый живёт до отключения канала.
                    _ = ProcessClientAsync(channel, cts);
                    _ = SendLoopAsync(channel, cts);
#if DEBUG
                    Logger.Debug(LogKey.NetworkSendLoopStarted, channel.RemoteAddress);
#endif
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    // Accept упал (например, слишком много открытых дескрипторов) — логируем и крутимся дальше.
                    Logger.Error(LogKey.NetworkAcceptFailed, ex);
                }
            }
        }

        /// <summary>
        /// Read-цикл: режет входящий поток на RawPacket и складывает в IncomingPackets.
        /// </summary>
        private async Task ProcessClientAsync(NetworkChannel channel, CancellationToken cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    ReadResult result = await channel.Reader.ReadAsync(cts);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    if (result.IsCompleted && buffer.Length == 0)
                        break; 

                    while (true)
                    {
                        var frameResult = TryReadPacket(channel, ref buffer, out int packetId, out byte[] data);
                        if (frameResult == PacketFrameResult.Malformed)
                        {
                            Logger.Warn(LogKey.NetworkMalformedFrame, channel.RemoteAddress);
                            channel.Disconnect();
                            break;
                        }
                        if (frameResult != PacketFrameResult.Complete)
                            break; 

                        channel.IncomingPackets.Enqueue(new RawPacket(packetId, data));
                    }

                    channel.Reader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                // Соединение разорвано клиентом или сервером во время чтения
                Logger.Warn(LogKey.NetworkChannelDisconnected, channel.RemoteAddress);
            }
            catch (Exception ex)
            {
                Logger.Error(LogKey.NetworkChannelDisconnected, channel.RemoteAddress, ex.Message);
            }
            finally
            {
                channel.Disconnect();
                _handoffRouter.HandleDisconnect(channel);
            }
        }

        /// <summary>
        /// Send-цикл: единственный владелец PipeWriter (single-writer контракт Pipes).
        /// Ждёт сигнала от ECS, вычитывает OutboundQueue, пишет в Writer и флашит.
        /// </summary>
        private async Task SendLoopAsync(NetworkChannel channel, CancellationToken cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await channel.WaitOutboundAsync(cts);

                    // Если канал был отключен (проснулись от Release в Disconnect)
                    if (channel.IsDisconnected)
                        return;

                    while (channel.OutboundQueue.TryDequeue(out var chunk))
                    {
                        try
                        {
                            channel.Writer.Write(new ReadOnlySpan<byte>(chunk.Buffer, 0, chunk.Length));
                            await channel.Writer.FlushAsync(cts);
                        }
                        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
                        {
                            Logger.Warn(LogKey.NetworkChannelDisconnected, channel.RemoteAddress);
                            channel.Disconnect();
                            return; 
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(chunk.Buffer);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error(LogKey.NetworkChannelDisconnected, channel.RemoteAddress, ex.Message);
            }
        }

        /// <summary>
        /// Вырезает один пакет из буфера через <see cref="PacketFrame.TryRead"/>.
        /// При Complete сдвигает буфер до consumed; при Partial/Malformed буфер не трогает.
        /// </summary>
        private PacketFrameResult TryReadPacket(NetworkChannel channel, ref ReadOnlySequence<byte> buffer, out int packetId, out byte[] data)
        {
            var result = PacketFrame.TryRead(buffer, channel.CompressionThreshold, _decompressor,
                out packetId, out data, out var consumed);
            if (result == PacketFrameResult.Complete)
                buffer = buffer.Slice(consumed);
            return result;
        }
    }
}
