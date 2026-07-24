namespace Verstack.Minecraft.Status;

/// <summary>
/// Server status response — the data shown in the Minecraft server list (the
/// "MOTD"). Serialized by <see cref="ServerStatusSerializer"/>.
/// </summary>
public readonly struct ServerStatusResponse(ServerVersion version, ServerCapacity capacity, string description)
{
    /// <summary>Reported server version (display name + protocol number).</summary>
    public readonly ServerVersion Version = version;

    /// <summary>Player slot capacity shown in the server list.</summary>
    public readonly ServerCapacity Capacity = capacity;

    /// <summary>Plain-text MOTD, rendered as <c>{"text": "..."}</c>.</summary>
    public readonly string Description = description;
}