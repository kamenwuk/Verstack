namespace Verstack.Network;

/// <summary>
/// Creates a fresh <see cref="IPacketHandler"/> for each accepted connection.
/// Implemented by the layer above Network (e.g. Verstack.Minecraft); called
/// once per connection in <see cref="TcpServer"/>'s accept loop.
/// </summary>
/// <remarks>
/// Inversion of dependency, mirroring <see cref="IPacketHandler"/>: Network
/// defines the factory contract, the application layer implements it. This
/// keeps Network decoupled from Minecraft packet specifics while letting each
/// connection own its per-connection state (e.g. protocol phase).
/// </summary>
public interface IPacketHandlerFactory
{
    /// <summary>
    /// Creates a new, independent handler for one connection.
    /// </summary>
    /// <remarks>
    /// Called once per accepted connection. The returned handler is owned by
    /// that connection's <see cref="SessionLifetime"/> and disposed alongside it.
    /// </remarks>
    IPacketHandler Create();
}