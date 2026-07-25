using System.IO.Pipelines;

namespace Verstack.Network.Tests;

/// <summary>
/// Integration tests for <see cref="SessionLifetime"/>: drives the read loop
/// through a pair of <see cref="Pipe"/>s — no socket, deterministic. Covers
/// the two exit paths: handler-returned Disconnect, and peer completing the
/// input pipe.
/// </summary>
public class SessionLifetimeTests
{
    // Минимальный таймаут, чтобы зависший RunAsync упал детерминированно,
    // а не вешал набор. RunAsync гоняется под этим токеном.
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(5);

    // ─── Disconnect по вердикту handler'а ───────────────────────────

    [Fact]
    public async Task RunAsync_HandlerReturnsDisconnect_TerminatesLoop()
    {
        var handler = new FakePacketHandler(PacketVerdict.Disconnect);
        var session = new SessionLifetime(handler);
        var (feed, connection) = CreateConnection();

        // Один валидный кадр: handler проигнорирует содержимое, скажет Disconnect.
        WriteFrame(feed, [0x00]);
        await feed.FlushAsync();

        using var cts = new CancellationTokenSource(TIMEOUT);
        await session.RunAsync(connection, cts.Token);

        // RunAsync завершился без отмены по таймауту → Disconnect сработал.
        // Handler вызван ровно один раз: после Disconnect цикл должен прерваться.
        Assert.Equal(1, handler.CallCount);
    }

    // ─── Graceful: peer закрыл input, handler говорил Keep ─────────

    [Fact]
    public async Task RunAsync_PeerCompletesInput_TerminatesLoop()
    {
        var handler = new FakePacketHandler(PacketVerdict.Keep);
        var session = new SessionLifetime(handler);
        var (feed, connection) = CreateConnection();

        WriteFrame(feed, stackalloc byte[] { 0x00 });
        await feed.FlushAsync();
        // Peer закрыл отправляющую сторону → result.IsCompleted → выход из цикла.
        await feed.CompleteAsync();

        using var cts = new CancellationTokenSource(TIMEOUT);
        await session.RunAsync(connection, cts.Token);

        Assert.Equal(1, handler.CallCount);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Пишет один обрамлённый кадр в Writer: [VarInt length][payload].
    /// Для тестовых payloads (≤ 127 байт) length-prefix — один байт.
    /// </summary>
    private static void WriteFrame(PipeWriter writer, ReadOnlySpan<byte> payload)
    {
        Span<byte> span = writer.GetSpan(1 + payload.Length);
        span[0] = (byte)payload.Length;
        payload.CopyTo(span[1..]);
        writer.Advance(1 + payload.Length);
    }

    /// <summary>
    /// Создаёт тестовое соединение из двух Pipe: тест пишет кадры в
    /// <paramref name="feed"/> (writer входного pipe), SessionLifetime читает
    /// их через <c>connection.Input</c>; ответы handler'а уходят в отдельный
    /// выходной pipe и тестом не инспектируются.
    /// </summary>
    private static (PipeWriter feed, IDuplexPipe connection) CreateConnection()
    {
        var input = new Pipe();
        var output = new Pipe();
        return (input.Writer, new DuplexPipe(input.Reader, output.Writer));
    }

    /// <summary>
    /// Простейший IDuplexPipe из заданных reader/writer.
    /// </summary>
    private sealed class DuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;
        public PipeWriter Output { get; } = output;
    }
}