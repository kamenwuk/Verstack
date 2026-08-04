namespace Verstack.Engine.Network;

public abstract class ClientLifecycleHandler
{
    protected internal abstract void HandleConnect(NetworkChannel channel);
    protected internal abstract void HandleDisconnect(NetworkChannel channel);
}