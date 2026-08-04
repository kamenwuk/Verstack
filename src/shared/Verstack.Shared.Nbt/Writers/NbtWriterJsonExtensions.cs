using System.Text.Json;
using System.Text;

namespace Verstack.Shared.Nbt.Writer;

public static class NbtWriterJsonExtensions
{
    public static void WriteJsonRoot(this ref NbtStreamWriter streamWriter, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Корневой элемент NBT должен быть JSON-объектом (Compound).");

        streamWriter.BeginRootCompound();
        WriteJsonObjectContents(ref streamWriter, root);
        streamWriter.EndCompound();
    }

    private static void WriteJsonObjectContents(ref NbtStreamWriter streamWriter, JsonElement obj)
    {
        foreach (var property in obj.EnumerateObject())
        {
            byte[] nameUtf8 = Encoding.UTF8.GetBytes(property.Name);
            WriteJsonValue(ref streamWriter, nameUtf8, property.Value);
        }
    }

    private static void WriteJsonValue(ref NbtStreamWriter streamWriter, ReadOnlySpan<byte> nameUtf8, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                byte[] strUtf8 = Encoding.UTF8.GetBytes(value.GetString()!);
                streamWriter.WriteString(nameUtf8, strUtf8);
                break;

            case JsonValueKind.Number:
                if (value.TryGetInt32(out int intVal))
                    streamWriter.WriteInt(nameUtf8, intVal);
                else if (value.TryGetDouble(out double doubleVal))
                    streamWriter.WriteDouble(nameUtf8, doubleVal);
                break;

            case JsonValueKind.True:
                streamWriter.WriteBool(nameUtf8, true);
                break;

            case JsonValueKind.False:
                streamWriter.WriteBool(nameUtf8, false);
                break;

            case JsonValueKind.Object:
                streamWriter.BeginCompound(nameUtf8);
                WriteJsonObjectContents(ref streamWriter, value);
                streamWriter.EndCompound();
                break;

            case JsonValueKind.Array:
                WriteJsonArray(ref streamWriter, nameUtf8, value);
                break;

            case JsonValueKind.Null:
                throw new JsonException("JSON null не поддерживается в NBT.");

            default:
                throw new JsonException($"Неподдерживаемый тип JSON: {value.ValueKind}");
        }
    }

    private static void WriteJsonArray(ref NbtStreamWriter streamWriter, ReadOnlySpan<byte> nameUtf8, JsonElement array)
    {
        int count = 0;
        NbtTagType listType = NbtTagType.End;

        // Первый проход: считаем количество и определяем тип по первому элементу
        foreach (var item in array.EnumerateArray())
        {
            if (count == 0)
            {
                listType = GetNbtTypeFromJson(item);
            }
            count++;
        }

        if (count == 0)
        {
            // Пустой массив
            streamWriter.BeginList(nameUtf8, NbtTagType.Compound, 0);
            streamWriter.EndList();
            return;
        }

        streamWriter.BeginList(nameUtf8, listType, count);

        // Второй проход: пишем все элементы
        foreach (var item in array.EnumerateArray())
        {
            WriteJsonListItem(ref streamWriter, item, listType);
        }

        streamWriter.EndList();
    }

    private static void WriteJsonListItem(ref NbtStreamWriter streamWriter, JsonElement item, NbtTagType expectedType)
    {
        switch (expectedType)
        {
            case NbtTagType.String:
                byte[] strUtf8 = Encoding.UTF8.GetBytes(item.GetString()!);
                streamWriter.WriteListItemString(strUtf8);
                break;

            case NbtTagType.Int:
                streamWriter.WriteListItemInt(item.GetInt32());
                break;

            case NbtTagType.Double:
                streamWriter.WriteListItemDouble(item.GetDouble());
                break;

            case NbtTagType.Byte:
                streamWriter.WriteListItemBool(item.GetBoolean());
                break;

            case NbtTagType.Compound:
                streamWriter.BeginCompound();
                WriteJsonObjectContents(ref streamWriter, item);
                streamWriter.EndCompound();
                break;
        }
    }

    private static NbtTagType GetNbtTypeFromJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => NbtTagType.Compound,
            JsonValueKind.String => NbtTagType.String,
            JsonValueKind.Number => element.TryGetInt32(out _) ? NbtTagType.Int : NbtTagType.Double,
            JsonValueKind.True or JsonValueKind.False => NbtTagType.Byte,
            _ => throw new JsonException("Невозможно определить NBT тип для массива.")
        };
    }
}