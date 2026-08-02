// using Verstack.Nbt.Assets;
// using Verstack.Nbt.Writer;
// using System.Text.Json;
// using Verstack.Nbt;
//
// namespace Verstack.NBT.Tests;
//
// public class NbtAssetTests : IDisposable
// {
//     private readonly string _testDir;
//
//     public NbtAssetTests()
//     {
//         // Создаем временную папку для тестовых файлов
//         _testDir = Path.Combine(Path.GetTempPath(), "VerstackNbtTests_" + Guid.NewGuid().ToString("N"));
//         Directory.CreateDirectory(_testDir);
//     }
//
//     public void Dispose()
//     {
//         // Очищаем папку после тестов
//         if (Directory.Exists(_testDir))
//             Directory.Delete(_testDir, true);
//     }
//
//     // ───────────────  Тесты конвертера JSON -> NBT  ───────────────
//
//     [Fact]
//     public void JsonToNbt_ConvertsSimpleObject_Correctly()
//     {
//         // 1. Подготовка JSON
//         string json = @"{ ""name"": ""overworld"", ""height"": 384, ""has_skylight"": true }";
//         using JsonDocument doc = JsonDocument.Parse(json);
//
//         // 2. Запись в NBT
//         Span<NbtFrame> frames = stackalloc NbtFrame[8];
//         Span<byte> buffer = stackalloc byte[256];
//         var writer = new NbtWriter(buffer, frames, networked: true);
//         
//         writer.WriteJsonRoot(doc.RootElement);
//         ReadOnlySpan<byte> nbtBytes = writer.Finish();
//
//         // 3. Проверки (сверяемся со спецификацией NBT)
//         // 10 = TAG_Compound (Корень)
//         Assert.Equal(10, nbtBytes[0]); 
//         
//         // 8 = TAG_String, 0 4 = длина имени (4 байта)
//         Assert.Equal(8, nbtBytes[1]);
//         Assert.Equal(0, nbtBytes[2]);
//         Assert.Equal(4, nbtBytes[3]);
//         
//         // Проверяем, что имя это "name"
//         Assert.Equal((byte)'n', nbtBytes[4]);
//         Assert.Equal((byte)'a', nbtBytes[5]);
//         Assert.Equal((byte)'m', nbtBytes[6]);
//         Assert.Equal((byte)'e', nbtBytes[7]);
//     }
//
//     // ───────────────  Тесты ScopedNbtBuffer (Временный)  ───────────────
//
//     [Fact]
//     public void ScopedNbtBuffer_Load_ReadsFileDataCorrectly()
//     {
//         // 1. Создаем фейковый .nbt файл
//         string filePath = Path.Combine(_testDir, "test_scoped.nbt");
//         byte[] expectedData = { 10, 0, 11, 109, 105, 110, 101, 99, 114, 97, 102, 116 };
//         File.WriteAllBytes(filePath, expectedData);
//
//         // 2. Читаем через ScopedNbtBuffer
//         using var scopedBuffer = ScopedNbtBuffer.Load(filePath);
//
//         // 3. Проверяем данные
//         Assert.Equal(expectedData.Length, scopedBuffer.Data.Length);
//         for (int i = 0; i < expectedData.Length; i++)
//         {
//             Assert.Equal(expectedData[i], scopedBuffer.Data[i]);
//         }
//     }
//
//     [Fact]
//     public void ScopedNbtBuffer_Load_ThrowsIfFileNotFound()
//     {
//         string filePath = Path.Combine(_testDir, "non_existent_file.nbt");
//         
//         Assert.Throws<FileNotFoundException>(() => 
//         { 
//             using var scopedBuffer = ScopedNbtBuffer.Load(filePath); 
//         });
//     }
//
//     // ───────────────  Тесты CachedNbtBuffer (Удерживаемый)  ───────────────
//
//     [Fact]
//     public void CachedNbtBuffer_LoadAndUnload_WorksCorrectly()
//     {
//         // 1. Создаем фейковый файл
//         string filePath = Path.Combine(_testDir, "test_cached.nbt");
//         byte[] expectedData = { 1, 2, 3, 4, 5 };
//         File.WriteAllBytes(filePath, expectedData);
//
//         // 2. Загружаем в кэш
//         var cachedBuffer = new CachedNbtBuffer();
//         Assert.False(cachedBuffer.IsLoaded); // До загрузки должен быть false
//
//         cachedBuffer.Load(filePath);
//         
//         // 3. Проверяем данные
//         Assert.True(cachedBuffer.IsLoaded);
//         Assert.Equal(5, cachedBuffer.Data.Length);
//         Assert.Equal(3, cachedBuffer.Data.Span[2]);
//
//         // 4. Выгружаем
//         cachedBuffer.Unload();
//         Assert.False(cachedBuffer.IsLoaded);
//         Assert.True(cachedBuffer.Data.IsEmpty);
//     }
// }