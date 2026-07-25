using Verstack.Minecraft.Status;
using Verstack.Network;
using Verstack.Protocol;

namespace Verstack.Minecraft.Session;

/// <summary>
/// Creates one <see cref="PacketDispatcher"/> per accepted connection. Holds
/// the shared, immutable server status; each <see cref="Create"/> call yields
/// an independent dispatcher with its own <see cref="SessionPhase"/>.
/// </summary>
public sealed class PacketDispatcherFactory : IPacketHandlerFactory
{
    private readonly ServerStatusResponse _status;
    private readonly IPacketCompressor? _compressor;
    private readonly int _compressionThreshold;

    /// <param name="status">Server status data shared across all connections.</param>
    /// <param name="compressor">Compressor instance. If null, compression is disabled.</param>
    /// <param name="compressionThreshold">Minimum payload size to compress.</param>
    public PacketDispatcherFactory(ServerStatusResponse status, IPacketCompressor? compressor = null, int compressionThreshold = 256)
    {
        _status = status;
        _compressor = compressor;
        _compressionThreshold = compressionThreshold;
    }

    /// <inheritdoc/>
    public IPacketHandler Create() => new PacketDispatcher(_status, _compressor, _compressionThreshold);
}