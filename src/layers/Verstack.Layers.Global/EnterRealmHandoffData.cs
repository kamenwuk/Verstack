using Verstack.Layers.Global.User;
using Verstack.Engine.Bridge;

namespace Verstack.Layers.Global;

/// <summary>
/// Данные для входа в слой Realm, передаваемые через мост.
/// </summary>
public sealed record EnterRealmHandoffData(UserProfile Profile, NetworkSession Session) : BridgeHandoffData;
