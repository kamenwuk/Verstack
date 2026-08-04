using System.Runtime.CompilerServices;

namespace Verstack.Engine.Network.Packet.Readers
{
    public static class PacketReaderTextExtensions
    {
        private const int MAX_STRING_LENGTH = 32767 * 4;

        extension(ref PacketStreamReader streamReader)
        {
            /// <summary>
            /// Пытается прочитать строку. Возвращает false, если данных не хватает или они битые.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryReadString(out ReadOnlyUtf8Span value)
            {
                value = ReadStringInternal(ref streamReader);
                return value.IsValid;
            }

            /// <summary>
            /// Читает строку как ReadOnlyUtf8Span. Проверяйте IsValid перед использованием!
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlyUtf8Span ReadString()
            {
                return ReadStringInternal(ref streamReader);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlyUtf8Span ReadStringInternal(ref PacketStreamReader streamReader)
        {
            if (streamReader.IsFaulted)
                return new ReadOnlyUtf8Span(ReadOnlySpan<byte>.Empty, false);

            int length = streamReader.ReadVarInt();
            if (streamReader.IsFaulted)
                return new ReadOnlyUtf8Span(ReadOnlySpan<byte>.Empty, false);

            if (length < 0 || length > MAX_STRING_LENGTH)
            {
                streamReader.SetFaulted();
                return new ReadOnlyUtf8Span(ReadOnlySpan<byte>.Empty, false);
            }

            if (streamReader.Remaining < length)
            {
                streamReader.SetFaulted();
                return new ReadOnlyUtf8Span(ReadOnlySpan<byte>.Empty, false);
            }

            ReadOnlySpan<byte> bytes = streamReader.ReadSpanRaw(length);
            return new ReadOnlyUtf8Span(bytes, true);
        }
    }
}