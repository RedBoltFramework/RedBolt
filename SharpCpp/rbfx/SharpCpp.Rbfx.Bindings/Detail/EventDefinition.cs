namespace Urho3D.Detail;

public abstract class EventDefinition
{
    private readonly global::Urho3D.Math.StringHash _eventType;

    protected EventDefinition(string eventName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        _eventType = eventName;
    }

    public static implicit operator global::Urho3D.Math.StringHash(EventDefinition value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value._eventType;
    }
}
