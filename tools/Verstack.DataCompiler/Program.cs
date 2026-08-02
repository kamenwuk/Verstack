using Verstack.DataCompiler;
using Verstack.Nbt;

string baseDir = AppContext.BaseDirectory;
string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
string toolsDir = Path.Combine(solutionRoot, "tools", "Verstack.DataCompiler");
string inputDir = Path.Combine(toolsDir, "Input");
string outputDir = Path.Combine(solutionRoot, "src", "Verstack.App", "assets");
string reportsDir = Path.Combine(toolsDir, "Reports");
string jarPath = Path.Combine(reportsDir, "server.jar");

string inputRegistriesDir = Path.Combine(inputDir, "Registries");
string inputTagsDir = Path.Combine(inputDir, "Tags");

Console.WriteLine("=== Verstack Data Compiler ===");

// --- 1. ОЧИСТКА СТАРЫХ ДАННЫХ ---
Console.WriteLine("Очистка старых данных...");
if (Directory.Exists(inputRegistriesDir)) Directory.Delete(inputRegistriesDir, true);
if (Directory.Exists(inputTagsDir)) Directory.Delete(inputTagsDir, true);
if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);

Directory.CreateDirectory(inputRegistriesDir);
Directory.CreateDirectory(inputTagsDir);
Directory.CreateDirectory(outputDir);

// --- 2. ИЗВЛЕЧЕНИЕ ДАННЫХ ИЗ JAR ---
if (File.Exists(jarPath))
{
    DataExtractor.Run(jarPath, reportsDir, inputRegistriesDir, inputTagsDir);
}
else
{
    Console.WriteLine("server.jar не найден. Пропуск извлечения данных.");
}

// --- 3. КОМПИЛЯЦИЯ ДАННЫХ ---
Console.WriteLine($"Компиляция данных из {inputDir} в {outputDir}...");
string[] jsonFiles = Directory.GetFiles(inputDir, "*.json", SearchOption.AllDirectories);

Span<NbtFrame> frames = stackalloc NbtFrame[32];
Span<byte> buffer = stackalloc byte[8192];

foreach (string jsonPath in jsonFiles)
{
    string json = File.ReadAllText(jsonPath);
    string relativePath = Path.GetRelativePath(inputDir, jsonPath);

    try
    {
        if (relativePath.EndsWith(".registry.json"))
        {
            RegistryCompiler.Compile(json, outputDir, relativePath);
        }
        else if (relativePath.EndsWith(".tags.json"))
        {
            TagCompiler.Compile(json, outputDir, relativePath, inputDir);
        }
        else if (relativePath.EndsWith(".nbt.json") || relativePath.EndsWith(".json"))
        {
            NbtCompiler.Compile(json, outputDir, relativePath, buffer, frames);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ОШИБКА] Файл {relativePath}: {ex.Message}");
    }
}

Console.WriteLine("\nГотово!");