using Leopotam.EcsProto.QoL;
using System.Text.Json;

namespace Verstack.Layers.Global;

public sealed class ServerInfoCacheStore : ProtoAspectInject
{
    public string Motd { get; private set; }
    public int MaxPlayers { get; private set; }
    public string VersionName { get; private set; }
    public int ProtocolVersion { get; private set; }
    public int OnlinePlayers { get; private set; }
    
    private byte[] _cachedStatusJson;
    private bool _isDirty = true;
    
    internal ServerInfoCacheStore(string motd, int maxPlayers, string versionName, int protocolVersion)
    {
        Motd = motd;
        MaxPlayers = maxPlayers;
        VersionName = versionName;
        ProtocolVersion = protocolVersion;
    }
    
    public byte[] GetStatusJson() 
    {
        // На случай, если пинг прилетит до первого тика сервера
        if (_cachedStatusJson == null || _isDirty) 
            RebuildIfDirty();
                
        return _cachedStatusJson; 
    }
    
    /// <summary>
    /// Вызывается системой, когда игрок заходит или выходит.
    /// </summary>
    internal void SetOnlinePlayers(int count)
    {
        if (OnlinePlayers == count) return;
        OnlinePlayers = count;
        _isDirty = true; // Просто ставим флаг, ничего не аллоцируем!
    }
    
    internal void RebuildIfDirty()
    {
        if (!_isDirty) return;

        var response = new
        {
            version = new { name = VersionName, protocol = ProtocolVersion },
            players = new { max = MaxPlayers, online = OnlinePlayers },
            description = Motd
        };

        _cachedStatusJson = JsonSerializer.SerializeToUtf8Bytes(response);
        _isDirty = false;
    }
}