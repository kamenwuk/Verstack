using Verstack.Minecraft.Status;
using Verstack.Network;

namespace Verstack.Minecraft.Session;

/// <summary>
/// Creates one <see cref="PacketDispatcher"/> per accepted connection. Holds
/// the shared, immutable server status; each <see cref="Create"/> call yields
/// an independent dispatcher with its own <see cref="SessionPhase"/>.
/// </summary>
public sealed class PacketDispatcherFactory : IPacketHandlerFactory
{
    private readonly ServerStatusResponse _status;

    /// <param name="status">Server status data shared across all connections.</param>
    public PacketDispatcherFactory(ServerStatusResponse status)
    {
        _status = status;
    }

    /// <inheritdoc/>
    public IPacketHandler Create() => new PacketDispatcher(_status);
}