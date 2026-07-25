# Слой Protocol

Чистая логика работы с байтами протокола Minecraft. Нет зависимостей от сети или ввода-вывода — только `Span<byte>` и `ReadOnlySequence<byte>`. Можно тестировать без сокета.

Три группы инструментов:

- **VarInt** — кодирование целых переменной длины (LEB128). Используется для длин пакетов, идентификаторов и числовых полей.
- **Фрейминг** — `PacketFrameReader` читает кадры из потока байт, `PacketFrameWriter` записывает кадры в буфер. Оба опираются на `VarInt` и общий лимит размера `DefaultMaxPacketSize`.
- **Чтение полей** — `PacketPayloadReader` принимает payload готового кадра и последовательно читает VarInt, big-endian числа, строки и UUID. UUID представлен собственным типом `Uuid`, сохраняющим wire-порядок байт.

→ [VarInt](varint.md)  
→ [PacketFrameReader](packet-frame-reader.md)  
→ [PacketFrameWriter](packet-frame-writer.md)  
→ [PacketPayloadReader](packet-payload-reader.md)  
→ [Uuid](uuid.md)