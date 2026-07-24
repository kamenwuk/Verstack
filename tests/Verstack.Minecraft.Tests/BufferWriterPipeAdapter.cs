using System.Buffers;
using System.IO.Pipelines;

namespace Verstack.Minecraft.Tests;

/// <summary>
/// Test-only adapter: exposes an <see cref="ArrayBufferWriter{T}"/> as a
/// <see cref="PipeWriter"/> so handlers can be driven synchronously in tests
/// without a real pipe or socket.
/// </summary>
/// <remarks>
/// <see cref="PipeWriter.Create(System.IO.Stream)"/> doesn't accept an
/// <see cref="IBufferWriter{T}"/>; this adapter fills the gap.
/// <see cref="FlushAsync"/> and <see cref="Complete"/> are no-ops — the test
/// reads the underlying buffer directly.
/// </remarks>
internal sealed class BufferWriterPipeAdapter : PipeWriter
{
    private readonly ArrayBufferWriter<byte> _buffer;

    public BufferWriterPipeAdapter(ArrayBufferWriter<byte> buffer)
    {
        _buffer = buffer;
    }

    /// <summary>Underlying buffer, for the test to inspect after writes.</summary>
    public ArrayBufferWriter<byte> Buffer => _buffer;

    public override void Advance(int bytes) => _buffer.Advance(bytes);

    public override Memory<byte> GetMemory(int sizeHint = 0) => _buffer.GetMemory(sizeHint);

    public override Span<byte> GetSpan(int sizeHint = 0) => _buffer.GetSpan(sizeHint);

    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        => default;

    public override void Complete(Exception? exception = null) { }

    public override void CancelPendingFlush() { }
}