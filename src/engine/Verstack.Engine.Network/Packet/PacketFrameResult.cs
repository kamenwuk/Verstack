namespace Verstack.Engine.Network.Packet;

/// <summary>
/// Результат попытки разобрать один кадр из буфера. Управляет поведением read-цикла:
/// <list type="bullet">
///   <item><see cref="Complete"/> — кадр разобран, можно забирать пакет и сдвигать буфер.</item>
///   <item><see cref="Partial"/> — данных мало, буфер НЕ трогать, ждать следующего ReadAsync.</item>
///   <item><see cref="Malformed"/> — кадр битый (некорректная длина, мусорный zlib-поток),
///     буфер НЕ трогать, но соединение отключить: дальнейший парсинг бессмысленен.</item>
/// </list>
/// </summary>
public enum PacketFrameResult
{
    Complete,
    Partial,
    Malformed
}