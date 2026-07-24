# The Network layer

Network is the byte pump: it accepts TCP connections, turns the incoming byte stream into framed Minecraft payloads, hands each payload to the layer above, and writes framed responses back out. It is built on `Pipelines.Sockets.Unofficial` (raw sockets + `System.IO.Pipelines`).

Three types do the work. `TcpServer` owns the listening socket and the accept loop. `SessionLifetime` drives one connection through the read loop, framing, dispatch, and flush. `IPacketHandler` is the contract that carries a parsed frame up to the application layer — defined here, implemented in Minecraft, wired in App.

For where this layer sits in the dependency graph, see [Architecture](../architecture.md).

→ [Server lifetime](server-lifetime.md) — TcpServer accept loop, SessionLifetime read loop, backpressure, flush point.
→ [Packet handler](packet-handler.md) — the IPacketHandler seam.
