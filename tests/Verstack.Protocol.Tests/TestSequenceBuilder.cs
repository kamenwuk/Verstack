using System.Buffers;

namespace Verstack.Protocol.Tests;

/// <summary>
/// Builds multi-segment <see cref="ReadOnlySequence{T}"/> for testing reads
/// that cross segment boundaries.
/// </summary>
internal static class TestSequenceBuilder
{
    /// <summary>Собирает sequence из нескольких массивов-сегментов.</summary>
    public static ReadOnlySequence<byte> BuildSegmented(params byte[][] segments)
    {
        if (segments.Length == 0)
            return ReadOnlySequence<byte>.Empty;
        if (segments.Length == 1)
            return new ReadOnlySequence<byte>(segments[0]);

        var first = new ByteSegment(segments[0]);
        ByteSegment current = first;
        for (int i = 1; i < segments.Length; i++)
        {
            current = current.Append(segments[i]);
        }
        return new ReadOnlySequence<byte>(first, 0, current, current.Memory.Length);
    }

    private sealed class ByteSegment : ReadOnlySequenceSegment<byte>
    {
        public ByteSegment(byte[] data) => Memory = data;

        public ByteSegment Append(byte[] data)
        {
            var next = new ByteSegment(data)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = next;
            return next;
        }
    }
}