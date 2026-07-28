namespace Verstack.Nbt;

/// <summary>
/// Тип тега NBT (Named Binary Tag). Wire-представление — один байт: ID тега.
///
/// IDs фиксированы спецификацией NBT (minecraft.wiki/w/NBT_format), одинаковы для disk- и networked-формата.
/// Значения проставлены явно: это binary-протокол, и переименование/перестановка членов не должна менять
/// wire-формат. <c>End</c> — маркер конца compound (без имени и значения, один байт); в List не пишется.
/// </summary>
public enum NbtTagType : byte
{
    /// <summary>Маркер конца compound. Тело пустое (только сам байт типа). В List не пишется.</summary>
    End = 0,

    /// <summary>TAG_Byte: 1 знаковый байт.</summary>
    Byte = 1,

    /// <summary>TAG_Short: 2 байта big-endian, знаковый.</summary>
    Short = 2,

    /// <summary>TAG_Int: 4 байта big-endian, знаковый.</summary>
    Int = 3,

    /// <summary>TAG_Long: 8 байт big-endian, знаковый.</summary>
    Long = 4,

    /// <summary>TAG_Float: 4 байта big-endian, IEEE 754 single.</summary>
    Float = 5,

    /// <summary>TAG_Double: 8 байт big-endian, IEEE 754 double.</summary>
    Double = 6,

    /// <summary>TAG_Byte_Array: Int (BE, длина) + N байт.</summary>
    ByteArray = 7,

    /// <summary>TAG_String: Short (BE, длина, max 32767) + modified-UTF-8 байты (не null-terminated).</summary>
    String = 8,

    /// <summary>TAG_List: Byte (тип элементов) + Int (BE, кол-во) + N элементов без имён и без байтов типа.</summary>
    List = 9,

    /// <summary>TAG_Compound: последовательность именованных тегов до TAG_End.</summary>
    Compound = 10,

    /// <summary>TAG_Int_Array: Int (BE, длина) + N×4 байта big-endian.</summary>
    IntArray = 11,

    /// <summary>TAG_Long_Array: Int (BE, длина) + N×8 байт big-endian.</summary>
    LongArray = 12
}