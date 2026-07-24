using Verstack.Minecraft.Status;

/// <summary>
/// Version entry of <see cref="ServerStatusResponse"/>: the game version name
/// and the protocol number the server speaks.
/// </summary>
public readonly struct ServerVersion(string name, int protocol)
{
    /// <summary>Display name, e.g. <c>"1.21.6"</c>.</summary>
    public readonly string Name = name;

    /// <summary>Protocol version, e.g. <c>774</c> for 1.21.6.</summary>
    public readonly int Protocol = protocol;
}