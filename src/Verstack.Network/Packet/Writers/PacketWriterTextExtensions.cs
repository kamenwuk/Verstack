using System.Runtime.CompilerServices;
using System.Text;

namespace Verstack.Network.Packet.Writers;

/// <summary>
/// Запись строковых типов данных (UTF-8 с VarInt-префиксом длины).
/// </summary>
public static class PacketWriterTextExtensions
{
    extension(ref PacketStreamWriter streamWriter)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteString(string value)
        {
            int byteCount = Encoding.UTF8.GetByteCount(value);
            streamWriter.WriteVarInt(byteCount); // VarInt сам вызовет EnsureCapacity
            streamWriter.EnsureCapacity(byteCount);
            Encoding.UTF8.GetBytes(value, streamWriter.FreeSpan);
            streamWriter.Advance(byteCount);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteString(scoped ReadOnlySpan<byte> utf8Value)
        {
            streamWriter.WriteVarInt(utf8Value.Length);
            streamWriter.WriteSpan(utf8Value);
            return ref streamWriter;
        }

        /// <summary>
        /// Записывает строку из безаллокационного представления <see cref="ReadOnlyUtf8Span"/>.
        /// Если представление невалидно (IsValid = false), будет записана пустая строка (VarInt(0)).
        /// </summary>
        /// <param name="utf8Value">Представление строки, полученное из ридера.</param>
        /// <returns>Ссылка на писателя для цепочечного вызова.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteString(in ReadOnlyUtf8Span utf8Value)
        {
            // AsSpan() безопасно вернет Empty, если utf8Value невалидна
            return ref streamWriter.WriteString(utf8Value.AsSpan());
        }
    }
}