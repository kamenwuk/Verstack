using System.IO.Compression;
using System.Diagnostics;
using System.Text.Json;

namespace Verstack.DataCompiler;

public static class DataExtractor
{
    private static readonly string[] RegistryNames = {
        "minecraft:attribute", "minecraft:block", "minecraft:block_entity_type",
        "minecraft:consume_effect_type", "minecraft:custom_stat", "minecraft:data_component_type",
        "minecraft:entity_type", "minecraft:fluid", "minecraft:game_event", "minecraft:item",
        "minecraft:menu", "minecraft:mob_effect", "minecraft:particle_type", "minecraft:potion",
        "minecraft:recipe_book_category", "minecraft:recipe_display", "minecraft:slot_display",
        "minecraft:sound_event", "minecraft:stat_type", "minecraft:villager_type"
    };

    public static void Run(string jarPath, string reportsDir, string inputRegistriesDir, string inputTagsDir)
    {
        Console.WriteLine("\n--- Запуск извлечения данных из JAR ---");
        ExtractRegistries(jarPath, reportsDir, inputRegistriesDir);
        ExtractTags(jarPath, inputTagsDir);
        Console.WriteLine("--- Извлечение завершено ---\n");
    }

    private static void ExtractRegistries(string jarPath, string reportsDir, string outputDir)
    {
        // Список всех возможных путей, куда новая Java может сгенерировать файл
        string[] possiblePaths = new[]
        {
            Path.Combine(reportsDir, "generated", "reports", "registries.json"),
            Path.Combine(reportsDir, "reports", "registries.json"),
            Path.Combine(reportsDir, "registries.json")
        };

        string? regPath = possiblePaths.FirstOrDefault(File.Exists);

        if (regPath == null)
        {
            Console.WriteLine("registries.json не найден. Запускаю Data Generator (Bundler)...");
            
            var psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-DbundlerMainClass=\"net.minecraft.data.Main\" -jar \"{Path.GetFileName(jarPath)}\" --reports",
                WorkingDirectory = reportsDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.WriteLine("ОШИБКА: Не удалось запустить Java.");
                return;
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // Ищем файл заново после генерации
            regPath = possiblePaths.FirstOrDefault(File.Exists);

            if (regPath == null)
            {
                Console.WriteLine("ОШИБКА: registries.json так и не был создан.");
                Console.WriteLine("Вывод Java:\n" + output);
                Console.WriteLine("Ошибки Java:\n" + error);
                Console.WriteLine("\n-> Решение: создай файл eula.txt в папке Reports и напиши внутри eula=true");
                return;
            }
        }

        Console.WriteLine($"Найден реестр: {regPath}");
        Console.WriteLine("Извлечение реестров...");
        Directory.CreateDirectory(outputDir);

        string json = File.ReadAllText(regPath);
        using var doc = JsonDocument.Parse(json);

        foreach (var regName in RegistryNames)
        {
            if (!doc.RootElement.TryGetProperty(regName, out var regData)) continue;
            if (!regData.TryGetProperty("entries", out var entries)) continue;

            var sortedEntries = entries.EnumerateObject()
                .OrderBy(e => e.Value.TryGetProperty("protocol_id", out var idProp) ? idProp.GetInt32() : 0)
                .Select(e => e.Name)
                .ToList();

            // Заменяем слеш на подчеркивание, чтобы файлы лежали плоско (worldgen_biome.registry.json)
            string fileName = regName.Replace("minecraft:", "") + ".registry.json";
            string filePath = Path.Combine(outputDir, fileName);

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
            
            writer.WriteStartArray();
            foreach (var name in sortedEntries)
                writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.Flush();

            File.WriteAllBytes(filePath, ms.ToArray());
            Console.WriteLine($"  -> Реестр: {fileName}");
        }
    }

        private static void ExtractTags(string jarPath, string outputDir)
    {
        Console.WriteLine("\nИзвлечение тегов из JAR...");
        Directory.CreateDirectory(outputDir);

        if (!File.Exists(jarPath)) return;

        using var jar = ZipFile.OpenRead(jarPath);
        var groupedTags = new Dictionary<string, Dictionary<string, List<string>>>();

        // Белый список реестров, которые реально существуют на клиенте и поддерживают теги
        var validRegistries = new HashSet<string> {
            "banner_pattern", "block", "damage_type", "dialog", "enchantment", "entity_type",
            "fluid", "game_event", "instrument", "item", "painting_variant", "point_of_interest_type",
            "potion", "timeline", "worldgen/biome"
        };

        void ProcessEntry(ZipArchiveEntry entry)
        {
            int tagsIdx = entry.FullName.IndexOf("tags/", StringComparison.OrdinalIgnoreCase);
            if (tagsIdx == -1) return;

            string relativePath = entry.FullName.Substring(tagsIdx + "tags/".Length);
            if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return;

            // ПЕРВЫЙ слеш отделяет реестр от имени тега
            int firstSlash = relativePath.IndexOf('/');
            if (firstSlash == -1) return;

            string registryName = relativePath.Substring(0, firstSlash); // например, "block"
            
            // Если это мусорный реестр (villager_trade и т.д.), просто пропускаем
            if (!validRegistries.Contains(registryName)) return;

            // Остальной путь — это имя тега (например, "mineable/axe.json")
            string tagPath = relativePath.Substring(firstSlash + 1);
            string tagName = tagPath.Substring(0, tagPath.Length - 5); // Убираем ".json"
            string fullTagName = "minecraft:" + tagName;

            string fileName = registryName + ".tags.json";
            
            if (!groupedTags.ContainsKey(fileName))
                groupedTags[fileName] = new Dictionary<string, List<string>>();

            var values = new List<string>();
            using var entryStream = entry.Open();
            using var doc = JsonDocument.Parse(entryStream);

            if (doc.RootElement.TryGetProperty("values", out var valuesProp))
            {
                foreach (var v in valuesProp.EnumerateArray())
                {
                    string? id = null;
                    if (v.ValueKind == JsonValueKind.String)
                        id = v.GetString();
                    else if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("id", out var idProp))
                        id = idProp.GetString();

                    if (!string.IsNullOrEmpty(id) && !id.StartsWith("#"))
                        values.Add(id);
                }
            }
            
            groupedTags[fileName][fullTagName] = values;
        }

        // Ищем теги прямо в JAR
        foreach (var entry in jar.Entries)
        {
            if (entry.FullName.Contains("tags/", StringComparison.OrdinalIgnoreCase) 
                && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                ProcessEntry(entry);
            }
        }

        // Если не нашли, ищем вложенные архивы (.zip ИЛИ .jar)
        if (groupedTags.Count == 0)
        {
            var innerArchives = jar.Entries.Where(e => 
                e.FullName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                e.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)).ToList();
            
            Console.WriteLine($"Найдено вложенных архивов: {innerArchives.Count}");

            foreach (var innerEntry in innerArchives)
            {
                using var ms = new MemoryStream();
                using (var zipStream = innerEntry.Open())
                {
                    zipStream.CopyTo(ms);
                }
                ms.Position = 0;
                
                try 
                {
                    using var innerZip = new ZipArchive(ms, ZipArchiveMode.Read);
                    foreach (var entry in innerZip.Entries)
                    {
                        if (entry.FullName.Contains("tags/", StringComparison.OrdinalIgnoreCase) 
                            && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            ProcessEntry(entry);
                        }
                    }
                }
                catch { /* Пропускаем не-архивы */ }
            }
        }

        int totalTags = groupedTags.Values.Sum(d => d.Count);
        Console.WriteLine($"Всего найдено файлов тегов: {totalTags}");
        if (totalTags == 0) return;

        // Сохраняем склеенные файлы
        foreach (var kvp in groupedTags)
        {
            string filePath = Path.Combine(outputDir, kvp.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
            
            writer.WriteStartObject();
            foreach (var tag in kvp.Value)
            {
                writer.WriteStartArray(tag.Key);
                foreach (var val in tag.Value)
                    writer.WriteStringValue(val);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
            writer.Flush();
            
            File.WriteAllBytes(filePath, ms.ToArray());
            Console.WriteLine($"  -> Теги: {kvp.Key} ({kvp.Value.Count} шт.)");
        }
    }
}