using Verstack.Shared.Nbt.Writer;
using Verstack.Shared.Nbt;
using System.Text.Json;

namespace Verstack.Tools.DataCompiler;

internal static class NbtCompiler
{
    public static void Compile(string json, string outputDir, string relativePath, Span<byte> buffer, Span<NbtFrame> frames)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        var writer = new NbtWriter(buffer, frames, networked: true);
        writer.WriteJsonRoot(doc.RootElement);
        ReadOnlySpan<byte> nbtBytes = writer.Finish();

        // Просто отрезаем ".json" (5 символов)
        string outputPath = Path.Combine(outputDir, relativePath);
        outputPath = outputPath.Remove(outputPath.Length - 5, 5);
        
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, nbtBytes.ToArray());

        Console.WriteLine($"  -> Скомпилирован NBT {relativePath} ({nbtBytes.Length} байт)");
    }
}