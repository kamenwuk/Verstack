using System.Text.Json;
using System.Text;

namespace Verstack.Tools.DataCompiler;

internal static class TagCompiler
{
    public static void Compile(string json, string outputDir, string relativePath, string inputDir)
    {
        string tagFileName = Path.GetFileName(relativePath);
        string registryFileName = tagFileName.Replace(".tags.json", ".registry.json");
        string registryFullPath = Path.Combine(inputDir, "Registries", registryFileName);

        // СТРОГОЕ ПРАВИЛО: Реестр dimension_type мы НИКОГДА не компилируем как теги (он отправляется через NBT)
        if (tagFileName == "dimension_type.tags.json")
        {
            Console.WriteLine($"  -> Пропуск тегов для {tagFileName} (это NBT реестр)");
            return;
        }

        var idMap = new Dictionary<string, int>();
        bool registryFound = File.Exists(registryFullPath);

        if (registryFound)
        {
            string registryJson = File.ReadAllText(registryFullPath);
            using (JsonDocument regDoc = JsonDocument.Parse(registryJson))
            {
                int id = 0;
                foreach (var entry in regDoc.RootElement.EnumerateArray())
                {
                    idMap[entry.GetString()!] = id++;
                }
            }
        }
        else
        {
            Console.WriteLine($"  -> ВНИМАНИЕ: Реестр {registryFileName} не найден. Теги будут скомпилированы пустыми.");
        }

        using JsonDocument doc = JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int tagCount = doc.RootElement.EnumerateObject().Count();
        BinaryUtils.WriteVarInt(bw, tagCount);

        foreach (var tagProp in doc.RootElement.EnumerateObject())
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(tagProp.Name);
            BinaryUtils.WriteVarInt(bw, nameBytes.Length);
            bw.Write(nameBytes);

            var entries = tagProp.Value.EnumerateArray().ToArray();
            List<int> resolvedIds = new(entries.Length);

            // Если реестр найден, ищем ID. Если нет - список останется пустым!
            if (registryFound)
            {
                foreach (var entry in entries)
                {
                    string entryName = entry.GetString()!;
                    if (idMap.TryGetValue(entryName, out int numericId))
                    {
                        resolvedIds.Add(numericId);
                    }
                }
            }

            BinaryUtils.WriteVarInt(bw, resolvedIds.Count);
            foreach (int id in resolvedIds)
            {
                BinaryUtils.WriteVarInt(bw, id);
            }
        }

        string outputPath = Path.Combine(outputDir, relativePath);
        outputPath = outputPath.Remove(outputPath.Length - 5, 5);
        
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, ms.ToArray());

        Console.WriteLine($"  -> Скомпилированы теги {relativePath} ({ms.Length} байт)");
    }
}