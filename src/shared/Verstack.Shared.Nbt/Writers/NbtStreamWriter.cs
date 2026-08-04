using System.Runtime.CompilerServices;

namespace Verstack.Shared.Nbt.Writer;

/// <summary>
/// GC-free запись NBT прямо в <c>Span&lt;byte&gt;</c>. Stateful <c>ref struct</c>, зеркало
/// <see cref="Verstack.Shared.Nbt.Reader.NbtStreamReader"/>. Ядро держит только состояние; логика записи и
/// контекста вынесена в extensions (как у <c>PacketStreamWriter</c>).
/// </summary>
/// <remarks>
/// Поля <c>internal</c> в PascalCase — extensions обращаются к ним напрямую, без мостовых методов. Writer
/// пишет в собственный буфер, поэтому переполнение — это баг caller'а; fault-state отсутствует, проверки
/// контекста идут через <c>#if DEBUG</c>.
/// </remarks>
public ref struct NbtStreamWriter
{
    internal readonly Span<byte> Buffer;
    internal readonly Span<NbtFrame> Frames;
    internal readonly bool Networked;
    internal int Offset;
    internal int Depth;

    /// <summary>Инициализирует writer поверх указанного буфера и стека кадров.</summary>
    /// <param name="buffer">Буфер для записи NBT-данных.</param>
    /// <param name="frames">Стек кадров вложенности (буфер caller'а, обычно <c>stackalloc</c>).</param>
    /// <param name="networked">
    /// <c>true</c> (по умолчанию) — networked-формат корня (имя корневого compound не пишется);
    /// <c>false</c> — disk-формат (пишется <c>Short=0</c>).
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NbtStreamWriter(Span<byte> buffer, Span<NbtFrame> frames, bool networked = true)
    {
        Buffer = buffer;
        Frames = frames;
        Networked = networked;
        Offset = 0;
        Depth = 0;
    }

    /// <summary>Количество байт, записанных в буфер на данный момент.</summary>
    public int Written => Offset;

    /// <summary>Диапазон, представляющий записанные данные.</summary>
    public ReadOnlySpan<byte> WrittenSpan => Buffer[..Offset];
}