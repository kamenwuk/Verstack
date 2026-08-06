using System.Runtime.CompilerServices;

namespace Verstack.Engine.Network.Packet.Inbound;

/// <summary>
/// Предоставляет быстрый, безаллокационный последовательный доступ к буферу байт только для чтения.
/// </summary>
/// <remarks>
/// <para>
/// Структура реализует паттерн "отложенного состояния ошибки" (Deferred Fault State) для минимизации 
/// накладных расходов при обработке битых пакетов.
/// </para>
/// <para>
/// Если при чтении данных происходит ошибка (например, попытка прочитать больше байт, чем доступно в буфере), 
/// ридер не выбрасывает исключение немедленно. Вместо этого он переходит в состояние ошибки 
/// (<see cref="IsFaulted"/> = <c>true</c>).
/// </para>
/// <para>
/// Все последующие вызовы методов чтения будут мгновенно возвращать значения по умолчанию (<c>0</c>, <c>null</c>), 
/// не выполняя реальных операций с памятью. Вызывающий код должен проверять свойство <see cref="IsValid"/> 
/// после завершения всех операций чтения, чтобы определить, успешно ли был разобран пакет.
/// </para>
/// </remarks>
public ref struct PacketStreamReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _offset;
    private bool _isFaulted;

    /// <summary>
    /// Текущая позиция чтения (смещение от начала буфера в байтах).
    /// </summary>
    public int Offset => _offset;

    /// <summary>
    /// Количество непрочитанных байт, оставшихся в буфере.
    /// </summary>
    public int Remaining => _buffer.Length - _offset;

    /// <summary>
    ///Gets a span representing the remaining unread portion of the buffer.
    /// </summary>
    public ReadOnlySpan<byte> RemainingSpan => _buffer[_offset..];

    /// <summary>
    /// Указывает, произошла ли ошибка во время предыдущих операций чтения.
    /// </summary>
    /// <value><c>true</c>, если ридер находится в состоянии ошибки; иначе <c>false</c>.</value>
    public bool IsFaulted => _isFaulted;

    /// <summary>
    /// Указывает, что все предыдущие операции чтения завершились успешно (отрицание <see cref="IsFaulted"/>).
    /// </summary>
    /// <value><c>true</c>, если ридер валиден; иначе <c>false</c>.</value>
    public bool IsValid => !_isFaulted;

    /// <summary>
    /// Инициализирует новый экземпляр читателя поверх указанного массива байт.
    /// </summary>
    /// <param name="buffer">Массив байт с данными для чтения.</param>
    /// <param name="length">Количество валидных байт в массиве, доступных для чтения.</param>
    internal PacketStreamReader(byte[] buffer, int length)
    {
        _buffer = new ReadOnlySpan<byte>(buffer, 0, length);
        _offset = 0;
        _isFaulted = false;
    }

    /// <summary>
    /// Инициализирует новый экземпляр читателя поверх указанного диапазона памяти.
    /// </summary>
    /// <param name="buffer">Диапазон только для чтения с данными.</param>
    internal PacketStreamReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _offset = 0;
        _isFaulted = false;
    }

    /// <summary>
    /// Сдвигает внутренний указатель чтения вперёд на заданное количество байт.
    /// </summary>
    /// <remarks>
    /// Если запрошенный сдвиг превышает размер оставшихся данных, ридер переходит в состояние ошибки.
    /// </remarks>
    /// <param name="count">Количество байт для пропуска.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Advance(int count)
    {
        if (_offset + count > _buffer.Length)
        {
            SetFaulted();
            return;
        }
        
        _offset += count;
    }

    /// <summary>
    /// Принудительно переводит читатель в состояние ошибки.
    /// </summary>
    /// <remarks>
    /// Используется методами расширения для чтения (например, VarInt), когда данные не соответствуют ожидаемому формату.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetFaulted()
    {
        _isFaulted = true;
    }
}