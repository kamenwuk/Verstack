# Packet handler

The packet handler is the seam between Network and the layer above. Network defines the contract; Minecraft (or any other application layer) provides an implementation; App wires them together.

```
public interface IPacketHandler
{
    void OnPacket(ReadOnlySequence<byte> payload, PipeWriter output);
}
```

`payload` is a complete frame's body, already stripped of the length prefix by the scanner. `output` is the write side of the connection; the handler writes framed responses here via `PacketFraming`. The method is synchronous — the handler only buffers; flushing is `SessionLifetime`'s job.

This interface is the entire contact surface between Network and Minecraft. Adding a new protocol state or packet type never touches Network — only a new `IPacketHandler` implementation and the wiring in `App`.

→ [Server lifetime](server-lifetime.md)
