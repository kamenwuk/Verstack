using System.Runtime.CompilerServices;
using Verstack.Shared.Nbt.Writer;

namespace Verstack.Shared.Nbt.Reader;

/// <summary>
/// GC-free чтение NBT из <c>ReadOnlySpan&lt;byte&gt;</c>. Stateful <c>ref struct</c>, зеркало
/// <see cref="NbtStreamWriter"/>. Ядро держит только состояние; логика чтения и контекста вынесена в
/// extensions (как у <c>PacketStreamReader</c>).
/// </summary>
/// <remarks>
/// <para>Поля <c>internal</c> в PascalCase — extensions обращаются к ним напрямую, без мостовых методов.</para>
/// <para>
/// При выходе за буфер reader переходит в faulted-состояние (<see cref="Faulted"/>), последующие чтения
/// возвращают <c>default</c>. Проверяйте <see cref="IsValid"/> после чтения. Структурные нарушения (баги
/// caller'а) ловятся в DEBUG через исключения.
/// </para>
/// </remarks>
public ref struct NbtStreamReader
{
    internal readonly ReadOnlySpan<byte> Buffer;
    internal readonly Span<NbtFrame> Frames;
    internal readonly bool Networked;
    internal int Offset;
    internal int Depth;
    internal bool Faulted;

    /// <summary>Инициализирует reader поверх указанного буфера и стека кадров.</summary>
    /// <param name="buffer">Буфер NBT-данных для чтения.</param>
    /// <param name="frames">Стек кадров вложенности (буфер caller'а, обычно <c>stackalloc</c>).</param>
    /// <param name="networked">
    /// <c>true</c> (по умолчанию) — networked-формат корня (поле имени пропускается);
    /// <c>false</c> — disk-формат (читается <c>Short=0</c>).
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NbtStreamReader(ReadOnlySpan<byte> buffer, Span<NbtFrame> frames, bool networked = true)
    {
        Buffer = buffer;
        Frames = frames;
        Networked = networked;
        Offset = 0;
        Depth = 0;
        Faulted = false;
    }

    /// <summary>Сколько байт прочитано из буфера.</summary>
    public int Read => Offset;

    /// <summary>Сколько непрочитанных байт осталось в буфере.</summary>
    public int Remaining => Buffer.Length - Offset;

    /// <summary>Оставшаяся непрочитанная часть буфера.</summary>
    public ReadOnlySpan<byte> RemainingSpan => Buffer[Offset..];

    /// <summary>Произошла ли ошибка (выход за буфер) во время чтения.</summary>
    public bool IsFaulted => Faulted;

    /// <summary>Все ли операции чтения завершились успешно (отрицание <see cref="IsFaulted"/>).</summary>
    public bool IsValid => !Faulted;
}