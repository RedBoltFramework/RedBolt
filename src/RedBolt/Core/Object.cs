namespace RedBolt;

public abstract class Object
{
    public static string GetTypeNameStatic() => nameof(Object);

    public static StringHash GetTypeStatic() => nameof(Object);

    public Context Context => Context.Instance;

    public T? GetSubsystem<T>()
        where T : class
    {
        return Context.GetSubsystem<T>();
    }

    public ContextEventSubscription SubscribeToEvent(StringHash eventType, NamedEventHandler handler)
    {
        return Context.SubscribeToEvent(this, eventType, handler);
    }

    public ContextEventSubscription SubscribeToEvent(string eventType, NamedEventHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return Context.SubscribeToEvent(this, eventType, handler);
    }

    public ContextEventSubscription SubscribeToEvent(object sender, StringHash eventType, NamedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return Context.SubscribeToEvent(this, sender, eventType, handler);
    }

    public ContextEventSubscription SubscribeToEvent(object sender, string eventType, NamedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return Context.SubscribeToEvent(this, sender, eventType, handler);
    }

    public void UnsubscribeFromAllEvents()
    {
        Context.Unsubscribe(this);
    }

    public void UnsubscribeFromEvent(StringHash eventType)
    {
        Context.Unsubscribe(this, eventType);
    }

    public void UnsubscribeFromEvent(string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        Context.Unsubscribe(this, eventType);
    }

    public void SendEvent(StringHash eventType, VariantMap? eventData = null)
    {
        Context.SendEvent(this, eventType, eventData);
    }

    public void SendEvent(string eventType, VariantMap? eventData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        Context.SendEvent(this, eventType, eventData);
    }
}
