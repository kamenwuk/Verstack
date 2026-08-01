using Verstack.Layer.Global.User;
using Verstack.Shared.Bridge;

namespace Verstack.Layer.Global.Bridge.Contracts;

/// <summary>
/// Данные для входа в слой Realm, передаваемые через мост.
/// </summary>
public sealed record EnterRealmHandoffData(UserProfile Profile, NetworkSession Session) : BridgeHandoffData;