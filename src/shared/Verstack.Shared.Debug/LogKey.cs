namespace Verstack.Shared.Debug;

public enum LogKey
{
    ServerStart,
    ServerStarted,
    ServerStop,
    ServerStopped,
    ServerTickFailed,
    
    NetworkNewConnection,
    NetworkChannelDisconnected,
    NetworkAcceptFailed,
    NetworkSendLoopStarted,
    NetworkMalformedFrame,
    
    ComposerRealmSkipped,

    GatewayNewChannel,
    GatewayStatusState,
    GatewayLoginState,
    GatewayHandshakeRejected,
    GatewayStatusInvalidPacket,
    GatewayPacketRejected,

    PacketStatusExchange,
    PacketPingPong,
    PacketLoginStart,
    PacketLoginAcknowledged,
    
    PacketClientInformation,
    PacketKnownPacks,
    PacketUpdateTags,
    PacketConfigurationFinish,
    
    PacketPlayDisconnect,
    PacketRealmTransfer,
    PacketPlayLogin,
    PacketPlayWorldBorder,
    PacketPlayAbilities,
    PacketPlayInfoUpdate,
    PacketPlayPosition,
    PacketPlaySpawnPosition,
    PacketPlayCommands,
    PacketPlayTeleportConfirm,
    PacketPlayMove
}