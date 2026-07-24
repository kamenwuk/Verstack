using Pipelines.Sockets.Unofficial;
using System.Net.Sockets;
using System.Net;

namespace Verstack.Network;

/// <summary>
/// TCP server: listening socket + accept loop.
/// Knows nothing about Minecraft — transport only.
/// </summary>
public sealed class TcpServer : IDisposable
{
    private const int BACKLOG = 100;
    
    private readonly IPEndPoint _endPoint;
    private Socket? _listenSocket;
    
    /// <summary>
    /// Creates a server bound to <paramref name="endPoint"/>.
    /// </summary>
    public TcpServer(IPEndPoint endPoint)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        _endPoint = endPoint;
    }
    
    /// <summary>
    /// Bind + Listen. Brings up the listening socket but does not start accepting connections.
    /// </summary>
    public void Start()
    {
        if (_listenSocket is not null)
            throw new InvalidOperationException($"[{nameof(TcpServer)}] Already started.");

        _listenSocket = new Socket(_endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        // Рекомендуемые серверные настройки от Marc Gravell (NoDelay и т.п.).
        // Замечание: настройки слушающего сокета НЕ наследуются принятыми —
        // для production стоит применять их и к каждому client-сокету.
        SocketConnection.SetRecommendedServerOptions(_listenSocket);
        _listenSocket.Bind(_endPoint);
        _listenSocket.Listen(BACKLOG);

        Console.WriteLine($"[{nameof(TcpServer)}] Listening on {_endPoint}.");
    }

    /// <summary>
    /// Accept loop. For each incoming connection, creates a
    /// <see cref="SocketConnection"/> (wraps the Socket in a Pipe) and delegates
    /// the rest of the connection's lifetime to <see cref="SessionLifetime"/>.
    /// </summary>
    public async Task RunAsync(CancellationToken token)
    {
        if (_listenSocket is null)
            throw new InvalidOperationException(
                $"[{nameof(TcpServer)}] Start() must be called before {nameof(RunAsync)}().");

        while (!token.IsCancellationRequested)
        {
            Socket client;
            try
            {
                // ValueTask<Socket>: на синхронном завершении не аллокирует.
                // Для Minecraft (десятки подключений при старте) — более чем достаточно;
                // для «тысячи/сек» нужен пул SocketAsyncEventArgs — но не сейчас.
                client = await _listenSocket.AcceptAsync(token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break; // штатная остановка по токену
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[{nameof(TcpServer)}] Accept socket error: {ex.SocketErrorCode} ({ex.Message}).");
                continue;
            }
            catch (ObjectDisposedException)
            {
                break; // слушающий сокет закрыт через Dispose
            }

            // Натягиваем Pipe на Socket: библиотека запускает фоновый приём в pipe.
            using var connection = SocketConnection.Create(client);
            Console.WriteLine($"[{nameof(TcpServer)}] Accepted from {client.RemoteEndPoint}.");
            
            // Вся жизнь соединения — в SessionLifetime (SRP: TcpServer только accept).
            // Оговорка: await блокирует accept-цикл, поэтому сервер держит одно
            // соединение за раз. Конкурентность — отдельный шаг (Task.Run / fire-and-forget).
            var session = new SessionLifetime();
            await session.RunAsync(connection, token).ConfigureAwait(false);
            
            Console.WriteLine($"[{nameof(TcpServer)}] Connection from {client.RemoteEndPoint} closed.");
        }
    }
    
    /// <inheritdoc/>
    public void Dispose()
    {
        _listenSocket?.Dispose();
        _listenSocket = null;
    }
}