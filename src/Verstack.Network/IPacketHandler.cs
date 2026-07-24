using System.IO.Pipelines;
using System.Buffers;

namespace Verstack.Network;

/// <summary>
/// Reacts to framed packets received over a connection. Implemented by the
/// layer above Network (e.g. Verstack.Minecraft); SessionLifetime calls it
/// for every complete frame read off the wire.
/// </summary>
/// <remarks>
/// Inversion of dependency: Network defines the contract, the application
/// layer implements it. This keeps Network decoupled from Minecraft packet
/// specifics — it never references <c>ServerStatusSerializer</c> or
/// any packet type.
/// </remarks>
public interface IPacketHandler
{
    /// <summary>
    /// Reacts to one complete frame's payload, optionally writing a response
    /// to <paramref name="output"/>.
    /// </summary>
    /// <param name="payload">The frame's payload bytes (already stripped of the
    /// length prefix by <c>PacketFrameScanner</c>).</param>
    /// <param name="output">Write side of the connection — write framed
    /// responses here; SessionLifetime flushes after the call returns.</param>
    /// <remarks>
    /// Synchronous: write to the buffer only. Flushing to the socket is
    /// SessionLifetime's job, so it controls the flush point (and future
    /// batching).
    /// </remarks>
    void OnPacket(ReadOnlySequence<byte> payload, PipeWriter output);
}