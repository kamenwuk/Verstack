namespace Verstack.Minecraft.Status;

/// <summary>
/// Player slot entry of <see cref="ServerStatusResponse"/>: total capacity
/// and current fill.
/// </summary>
public readonly struct ServerCapacity(int max, int online)
{
    /// <summary>Maximum number of concurrent players.</summary>
    public readonly int Max = max;

    /// <summary>Players currently connected.</summary>
    public readonly int Online = online;
}