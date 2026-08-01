using Verstack.Layer.Global.Bridge.Contracts;
using Verstack.Layer.Realm.User;
using Verstack.Shared.Bridge;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layer.Realm.Join;

public sealed class UserEnterSystem : IProtoRunSystem
{
    [DI] private readonly BridgeStateCacheStore _bridgeCache = null!;
    [DI] private readonly UserSessionCacheStore _realmCache = null!;
    
    public void Run()
    {
        while (_bridgeCache.TryDequeueHandoff(out var payload))
        {
            if (payload.Data is EnterRealmHandoffData realmData)
            {
                var entity = payload.Entity;
                
                _realmCache.UserProfiles.Add(entity) = realmData.Profile;
                _realmCache.Sessions.Add(entity) = realmData.Session;
                
                Console.WriteLine("GOOD " + realmData.Profile.Username);
            }
        }
    }
}