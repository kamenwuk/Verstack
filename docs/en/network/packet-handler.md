# Packet handler

The packet handler is the seam between Network and the layer above. Network defines the contracts; Minecraft (or any other application layer) provides implementations; App wires them together.

```
public interface IPacketHandler
{
    PacketVerdict OnPacket(ReadOnlySequence<byte> payload, PipeWriter output);
}

public interface IPacketHandlerFactory
{
    IPacketHandler Create();
}
```

`IPacketHandler.OnPacket` is the reaction to one frame. `payload` is a complete frame's body, already stripped of the length prefix by the scanner. `output` is the write side of the connection; the handler writes framed responses here via `PacketFraming`. The method is synchronous — the handler only buffers; flushing is `SessionLifetime`'s job. The return value `PacketVerdict` is the verdict on the connection's fate: `Keep` (default) keeps reading, `Disconnect` tears it down. The verdict is honored by `SessionLifetime` **after** the flush, so a response written for this frame still goes out before the disconnect. What makes a handler return `Disconnect` — see [the dispatcher](../minecraft/dispatcher.md); how `SessionLifetime` acts on it — see [the drop point](server-lifetime.md).

`IPacketHandlerFactory.Create` is the birth point of a handler for each connection. It exists because a handler carries per-connection state (the current protocol phase), and that state cannot live in a singleton object shared across all connections. `TcpServer` calls `Create()` in the accept loop and passes the fresh handler to `SessionLifetime`. The details of what the handler stores belong to the Minecraft layer; here only the contract is fixed: "each connection gets its own handler."

These two interfaces are the entire contact surface between Network and Minecraft. Adding a new protocol state or packet type never touches Network — only a new `IPacketHandler` implementation (and the factory that produces it) plus the wiring in `App`.

→ [Server lifetime](server-lifetime.md)
