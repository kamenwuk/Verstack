using System.Collections.Concurrent;
using Verstack.Network.DataTypes;
using Verstack.Network.Packet;
using System.IO.Pipelines;
using System.Net.Sockets;
using Verstack.Debug;
using System.Buffers;
using System.Net;

namespace Verstack.Network
{
    public class TcpNetworkService
    {
        private Socket _listener;
        private CancellationTokenSource _cts;

        // Очередь новых подключений
        public ConcurrentQueue<NetworkChannel> PendingConnections { get; } = new();

        // Очередь ОТКЛЮЧЕННЫХ каналов (События смерти)
        public ConcurrentQueue<NetworkChannel> DisconnectedChannels { get; } = new();

        public void Start(int port)
        {
            _cts = new CancellationTokenSource();
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
            _listener.Listen(100);

            _ = AcceptLoopAsync(_cts.Token);
        }

        public void Stop()
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
                    PendingConnections.Enqueue(channel);

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
        /// Не трогает ECS-мир — только очередь. Leopotam не потокобезопасен.
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
                        break; // Клиент отключился

                    // Бесконечно режем байты на пакеты и кидаем в очередь
                    while (TryReadPacket(ref buffer, out int packetId, out byte[] data))
                    {
                        channel.IncomingPackets.Enqueue(new RawPacket(packetId, data));
                    }

                    channel.Reader.AdvanceTo(buffer.Start, buffer.End);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error(LogKey.NetworkChannelDisconnected, ex);
            }
            finally
            {
                Logger.Warn(LogKey.NetworkChannelDisconnected, channel.RemoteAddress);
                channel.Disconnect();
                DisconnectedChannels.Enqueue(channel);
            }
        }

        /// <summary>
        /// Send-цикл: единственный владелец PipeWriter (single-writer контракт Pipes).
        /// Ждёт сигнала от ECS, вычитывает OutboundQueue, пишет в Writer и флашит.
        /// Ошибка flush → канал помечается мёртвым (читающая сторона закроет его).
        /// </summary>
        private async Task SendLoopAsync(NetworkChannel channel, CancellationToken cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await channel.WaitOutboundAsync(cts);

                    // Вычитываем всё накопленное за один будильник — один flush на пачку.
                    while (channel.OutboundQueue.TryDequeue(out var chunk))
                    {
                        try
                        {
                            channel.Writer.Write(new ReadOnlySpan<byte>(chunk.Buffer, 0, chunk.Length));
                            await channel.Writer.FlushAsync(cts);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(LogKey.NetworkChannelDisconnected, ex);
                            channel.Disconnect();
                            // DisconnectedChannels заполняется в finally read-цикла — не дублируем.
                            return;
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(chunk.Buffer);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            // Прочие исключения логируем, но не дублируем Disconnect — read-цикл всё равно заметит обрыв.
            catch (Exception ex)
            {
                Logger.Error(LogKey.NetworkChannelDisconnected, ex);
            }
        }

        private bool TryReadPacket(ref ReadOnlySequence<byte> buffer, out int packetId, out byte[] data)
        {
            packetId = 0;
            data = null;

            var reader = new SequenceReader<byte>(buffer);

            // 1. Читаем длину пакета (VarInt)
            if (!VarInt.TryRead(ref reader, out int length))
                return false;

            // 2. Проверяем, есть ли само тело пакета
            if (reader.Remaining < length)
                return false;

            // 3. Запоминаем позицию, где начинается пакет (ID + Данные)
            var packetStart = reader.Position;

            // 4. Читаем ID пакета (VarInt)
            if (!VarInt.TryRead(ref reader, out packetId))
                return false;

            // 5. Запоминаем позицию, где начинаются данные (после ID)
            var payloadStart = reader.Position;

            // 6. Вычисляем точный размер данных
            long idSize = buffer.Slice(packetStart, payloadStart).Length;
            int dataLength = length - (int)idSize;

            // 7. Копируем данные
            data = new byte[dataLength];
            var payloadSequence = buffer.Slice(payloadStart, dataLength);
            payloadSequence.CopyTo(data);

            // 8. Сдвигаем буфер: отрезаем длину пакета и сам пакет
            // payloadSequence.End указывает на конец текущего пакета
            buffer = buffer.Slice(payloadSequence.End);

            return true;
        }
    }
}
