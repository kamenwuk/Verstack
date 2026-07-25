using Pipelines.Sockets.Unofficial;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Net;
using Verstack.Protocol;

namespace Verstack.Network;

/// <summary>
/// TCP server: listening socket + accept loop.
/// Knows nothing about Verstack.Minecraft — transport only.
/// </summary>
public sealed class TcpServer : IDisposable
{
    private const int BACKLOG = 100;
    
    private readonly IPEndPoint _endPoint;
    private readonly IPacketHandlerFactory _factory;
    private readonly IPacketDecompressor? _decompressor;
    private readonly List<Task> _sessionTasks = new();
    private readonly Lock _sessionTasksLock = new();
    private Socket? _listenSocket;
    
    /// <summary>
    /// Creates a server bound to <paramref name="endPoint"/>.
    /// </summary>
    /// <param name="endPoint">The endpoint to listen on.</param>
    /// <param name="factory">Factory for creating packet handlers.</param>
    /// <param name="decompressor">Decompressor instance. If null, compression is disabled.</param>
    public TcpServer(IPEndPoint endPoint, IPacketHandlerFactory factory, IPacketDecompressor? decompressor = null)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        ArgumentNullException.ThrowIfNull(factory);
        _endPoint = endPoint;
        _factory = factory;
        _decompressor = decompressor;
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
    public async Task AcceptConnectionsAsync(CancellationToken token)
    {
        if (_listenSocket is null)
            throw new InvalidOperationException(
                $"[{nameof(TcpServer)}] Start() must be called before {nameof(AcceptConnectionsAsync)}().");

        while (!token.IsCancellationRequested)
        {
            Socket client;
            try
            {
                // ValueTask<Socket>: на синхронном завершении не аллокирует.
                // Для Verstack.Minecraft (десятки подключений при старте) — более чем достаточно;
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
            var connection = SocketConnection.Create(client);
            Console.WriteLine($"[{nameof(TcpServer)}] Accepted from {client.RemoteEndPoint}.");
            
            var sessionTask = HandleConnectionAsync(connection, token);
            lock (_sessionTasksLock)
            {
                _sessionTasks.Add(sessionTask);
            }
            
            // Когда сессия завершится — уберём её из списка, чтобы не копить
            // завершённые Task'и (утечка памяти при долгой жизни сервера).
            // ExecuteSynchronously: continuation выполнится на потоке, завершившем
            // задачу, без постановки в threadpool — дёшево на accept-пути (не горячий путь).
            _ = sessionTask.ContinueWith(
                completedTask =>
                {
                    lock (_sessionTasksLock)
                    {
                        _sessionTasks.Remove(completedTask);
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
        }
        
        // После выхода из accept-цикла — ждём все ещё живые сессии. Каждая из них
        // крутится по тому же token, поэтому завершится по Cancel без вечного ожидания.
        // Программа (Program.cs) выходит только когда все соединения закрыты — graceful shutdown.
        Task[] pending;
        lock (_sessionTasksLock)
        {
            pending = _sessionTasks.ToArray();
        }

        if (pending.Length > 0)
        {
            Console.WriteLine($"[{nameof(TcpServer)}] Awaiting {pending.Length} active session(s) to close...");
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
    }
    
    /// <summary>
    /// Жизнь одного соединения, запущенная в фоне. Точка ownership для
    /// <see cref="IDuplexPipe"/>: вся обработка и финализация здесь.
    /// try/catch обязателен — задача fire-and-forget, никто не await'ит её
    /// напрямую, без перехвата исключение стало бы UnobservedTaskException.
    /// </summary>
    private async Task HandleConnectionAsync(IDuplexPipe connection, CancellationToken token)
    {
        try
        {
            // Передаём декомпрессор в SessionLifetime
            var session = new SessionLifetime(_factory.Create(), _decompressor);
            await session.RunAsync(connection, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка по токену — тишина.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{nameof(TcpServer)}] Session error: {ex.GetType().Name}: {ex.Message}.");
        }
        finally
        {
            // Dispose здесь, а не в accept-цикле: это единственный реальный владелец connection.
            // Поле connection пришло как IDuplexPipe, но SocketConnection — IDisposable.
            if (connection is IDisposable disposable)
                disposable.Dispose();
        }
    }
    
    /// <inheritdoc/>
    public void Dispose()
    {
        _listenSocket?.Dispose();
        _listenSocket = null;
    }
}