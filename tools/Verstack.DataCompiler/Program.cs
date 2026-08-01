using Verstack.Nbt.Writer;
using System.Text.Json;
using Verstack.Nbt;

// 1. Получаем папку, где лежит сам .exe компилятора (bin/Debug/net10.0)
string baseDir = AppContext.BaseDirectory;

// 2. Поднимаемся на 5 уровней вверх до корня солюшена (Verstack)
string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));

// 3. Формируем абсолютные пути
string inputDir = Path.Combine(solutionRoot, "tools", "Verstack.DataCompiler", "Input");
string outputDir = Path.Combine(solutionRoot, "src", "Verstack.App", "assets", "nbt");

Console.WriteLine($"Компиляция данных из {inputDir} в {outputDir}...");

// --- ОЧИСТКА ПАПКИ OUTPUT ---
// Если папка существует, полностью удаляем её со всеми старыми файлами
if (Directory.Exists(outputDir))
{
    Console.WriteLine("Очистка старых скомпилированных данных...");
    Directory.Delete(outputDir, recursive: true);
}
// Создаем пустую папку заново
Directory.CreateDirectory(outputDir);
// ---------------------------

string[] jsonFiles = Directory.GetFiles(inputDir, "*.json", SearchOption.AllDirectories);

Span<NbtFrame> frames = stackalloc NbtFrame[32];
Span<byte> buffer = stackalloc byte[8192];

foreach (string jsonPath in jsonFiles)
{
    string json = File.ReadAllText(jsonPath);
    using JsonDocument doc = JsonDocument.Parse(json);

    var writer = new NbtWriter(buffer, frames, networked: true);
    writer.WriteJsonRoot(doc.RootElement);
    ReadOnlySpan<byte> nbtBytes = writer.Finish();

    string relativePath = Path.GetRelativePath(inputDir, jsonPath);
    string outputPath = Path.ChangeExtension(Path.Combine(outputDir, relativePath), ".nbt");
    
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllBytes(outputPath, nbtBytes.ToArray());

    Console.WriteLine($"  -> Скомпилирован {relativePath} ({nbtBytes.Length} байт)");
}

Console.WriteLine("Готово!");