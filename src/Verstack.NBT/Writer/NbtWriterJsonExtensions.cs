using System.Text.Json;
using System.Text;

namespace Verstack.Nbt.Writer;

public static class NbtWriterJsonExtensions
{
    public static void WriteJsonRoot(this ref NbtWriter writer, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Корневой элемент NBT должен быть JSON-объектом (Compound).");

        writer.BeginRootCompound();
        WriteJsonObjectContents(ref writer, root);
        writer.EndCompound();
    }

    private static void WriteJsonObjectContents(ref NbtWriter writer, JsonElement obj)
    {
        foreach (var property in obj.EnumerateObject())
        {
            byte[] nameUtf8 = Encoding.UTF8.GetBytes(property.Name);
            WriteJsonValue(ref writer, nameUtf8, property.Value);
        }
    }

    private static void WriteJsonValue(ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                byte[] strUtf8 = Encoding.UTF8.GetBytes(value.GetString()!);
                writer.WriteString(nameUtf8, strUtf8);
                break;

            case JsonValueKind.Number:
                if (value.TryGetInt32(out int intVal))
                    writer.WriteInt(nameUtf8, intVal);
                else if (value.TryGetDouble(out double doubleVal))
                    writer.WriteDouble(nameUtf8, doubleVal);
                break;

            case JsonValueKind.True:
                writer.WriteBool(nameUtf8, true);
                break;

            case JsonValueKind.False:
                writer.WriteBool(nameUtf8, false);
                break;

            case JsonValueKind.Object:
                writer.BeginCompound(nameUtf8);
                WriteJsonObjectContents(ref writer, value);
                writer.EndCompound();
                break;

            case JsonValueKind.Array:
                WriteJsonArray(ref writer, nameUtf8, value);
                break;

            case JsonValueKind.Null:
                throw new JsonException("JSON null не поддерживается в NBT.");

            default:
                throw new JsonException($"Неподдерживаемый тип JSON: {value.ValueKind}");
        }
    }

    private static void WriteJsonArray(ref NbtWriter writer, ReadOnlySpan<byte> nameUtf8, JsonElement array)
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
            writer.BeginList(nameUtf8, NbtTagType.Compound, 0);
            writer.EndList();
            return;
        }

        writer.BeginList(nameUtf8, listType, count);

        // Второй проход: пишем все элементы
        foreach (var item in array.EnumerateArray())
        {
            WriteJsonListItem(ref writer, item, listType);
        }

        writer.EndList();
    }

    private static void WriteJsonListItem(ref NbtWriter writer, JsonElement item, NbtTagType expectedType)
    {
        switch (expectedType)
        {
            case NbtTagType.String:
                byte[] strUtf8 = Encoding.UTF8.GetBytes(item.GetString()!);
                writer.WriteListItemString(strUtf8);
                break;

            case NbtTagType.Int:
                writer.WriteListItemInt(item.GetInt32());
                break;

            case NbtTagType.Double:
                writer.WriteListItemDouble(item.GetDouble());
                break;

            case NbtTagType.Byte:
                writer.WriteListItemBool(item.GetBoolean());
                break;

            case NbtTagType.Compound:
                writer.BeginCompound();
                WriteJsonObjectContents(ref writer, item);
                writer.EndCompound();
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