namespace Verstack.Layer.Global.User;

public readonly struct UserProfile(Guid uuid, string username, string locale)
{
    public readonly Guid Uuid = uuid;
    public readonly string Username = username;
    public readonly string Locale = locale;
}