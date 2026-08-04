using System.Text.Json;
using System.Text;

namespace Verstack.Tools.DataCompiler;

internal static class RegistryCompiler
{
    public static void Compile(string json, string outputDir, string relativePath)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int entryCount = doc.RootElement.GetArrayLength();
        BinaryUtils.WriteVarInt(bw, entryCount);

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            string name = entry.GetString()!;
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);

            BinaryUtils.WriteVarInt(bw, nameBytes.Length);
            bw.Write(nameBytes);
            
            // Обязательно: флаг отсутствия NBT (0 = false)
            bw.Write((byte)0);

            //Console.WriteLine($"  -> Entry {name} получил ID: {assignedId}");
        }

        // Просто отрезаем ".json" (5 символов)
        string outputPath = Path.Combine(outputDir, relativePath);
        outputPath = outputPath.Remove(outputPath.Length - 5, 5);
        
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, ms.ToArray());

        Console.WriteLine($"Скомпилирован реестр: {relativePath} ({ms.Length} байт)");
    }
}