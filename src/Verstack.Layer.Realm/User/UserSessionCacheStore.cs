using Verstack.Layer.Global.User;
using Leopotam.EcsProto.QoL;
using Leopotam.EcsProto;

namespace Verstack.Layer.Realm.User;

internal sealed class UserSessionCacheStore : ProtoAspectInject
{
    internal readonly ProtoPool<NetworkSession> Sessions = null!;
    internal readonly ProtoPool<UserProfile> UserProfiles = null!;
}