using System.IO.Pipelines;
using System.Buffers;

namespace Verstack.Network.Tests;

/// <summary>
/// Test-only <see cref="IPacketHandler"/> stub: returns a configured verdict
/// on every call and records invocations, so <c>SessionLifetime</c> can be
/// driven through a pair of <c>Pipe</c>s without any real packet logic.
/// </summary>
internal sealed class FakePacketHandler : IPacketHandler
{
    private readonly PacketVerdict _verdict;
    private readonly Action<PipeWriter>? _onWrite;
    private int _callCount;

    /// <param name="verdict">Verdict returned on every <see cref="OnPacket"/> call.</param>
    /// <param name="onWrite">Optional action invoked with <c>output</c> before
    /// returning, to simulate a handler that writes a response.</param>
    public FakePacketHandler(PacketVerdict verdict, Action<PipeWriter>? onWrite = null)
    {
        _verdict = verdict;
        _onWrite = onWrite;
    }

    /// <summary>Number of times <see cref="OnPacket"/> was called.</summary>
    public int CallCount => _callCount;

    /// <inheritdoc/>
    public PacketVerdict OnPacket(ReadOnlySequence<byte> payload, PipeWriter output)
    {
        _callCount++;
        _onWrite?.Invoke(output);
        return _verdict;
    }
}