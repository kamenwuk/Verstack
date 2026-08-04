using System.Runtime.CompilerServices;
using System.Text;

namespace Verstack.Engine.Network.Packet
{
    /// <summary>
    /// Безаллокационное представление прочитанной строки (окно в буфер пакета).
    /// Является ref-структурой и не может быть сохранена в поля классов.
    /// </summary>
    public readonly ref struct ReadOnlyUtf8Span
    {
        private readonly ReadOnlySpan<byte> _bytes;
        private readonly bool _isValid;

        internal ReadOnlyUtf8Span(ReadOnlySpan<byte> bytes, bool isValid)
        {
            _bytes = bytes;
            _isValid = isValid;
        }

        /// <summary>
        /// Указывает, успешно ли была прочитана строка. 
        /// Использовать данные можно только если это свойство равно true.
        /// </summary>
        public bool IsValid => _isValid;
        
        public int Length => _isValid ? _bytes.Length : 0;

        /// <summary>
        /// Побайтово сравнивает строку с ожидаемой. 0 аллокаций.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ReadOnlySpan<byte> utf8Value)
        {
            if (!_isValid) return false;
            return _bytes.SequenceEqual(utf8Value);
        }

        /// <summary>
        /// Возвращает байт по индексу.
        /// </summary>
        public byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!_isValid) return 0;
                return _bytes[index];
            }
        }

        /// <summary>
        /// Возвращает срез сырых байт.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> AsSpan() => _isValid ? _bytes : ReadOnlySpan<byte>.Empty;

        /// <summary>
        /// Осознанно создаёт объект string в куче. 
        /// Вызывай ТОЛЬКО если строку нужно сохранить надолго.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            if (!_isValid) return null;
            return Encoding.UTF8.GetString(_bytes);
        }
    }
}