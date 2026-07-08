namespace RedBolt;

public abstract class Object
{
    public static string GetTypeNameStatic() => nameof(Object);

    public static StringHash GetTypeStatic() => nameof(Object);

    public Context Context => Context.Instance;

    public bool BlockEvents { get; set; }

    public T? GetSubsystem<T>()
        where T : class
    {
        return Context.GetSubsystem<T>();
    }

    public ContextEventSubscription SubscribeToEvent<T>(RefEventHandler<T> handler) where T : struct
    {
        return Context.SubscribeToEvent(this, handler);
    }

    public ContextEventSubscription SubscribeToEvent<T>(object sender, RefEventHandler<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(sender);
        return Context.SubscribeToEvent(this, sender, handler);
    }

    public void UnsubscribeFromEvent(object sender) => Context.Unsubscribe(this, sender);

    public void UnsubscribeFromEvents(object sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        Context.UnsubscribeFromEvents(this, sender);
    }

    public void UnsubscribeFromAllEvents()
    {
        Context.Unsubscribe(this);
    }

    public void UnsubscribeFromAllEventsExcept(IReadOnlyCollection<object> senders)
    {
        ArgumentNullException.ThrowIfNull(senders);
        Context.UnsubscribeAllExcept(this, senders);
    }

    public void SendEvent<T>() where T : struct
    {
        var args = new T();
        Context.SendEvent(this, ref args);
    }

    public void SendEvent<T>(ref T args) where T : struct
    {
        Context.SendEvent(this, ref args);
    }

    public Variant GetGlobalVar(StringHash key)
    {
        return Context.GetGlobalVar(key);
    }

    public VariantMap GetGlobalVars() => Context.GetGlobalVars();

    public void SetGlobalVar(StringHash key, Variant value)
    {
        Context.SetGlobalVar(key, value);
    }

    public void SetBlockEvents(bool block)
    {
        BlockEvents = block;
    }

    public bool GetBlockEvents() => BlockEvents;

    public bool HasSubscribedToEvent()
    {
        return Context.HasSubscribedToEvent(this);
    }

    public bool HasSubscribedToEvent<T>() where T : struct
    {
        return Context.HasSubscribedToEvent<T>(this);
    }

    public bool HasSubscribedToEvent<T>(object sender) where T : struct
    {
        ArgumentNullException.ThrowIfNull(sender);
        return Context.HasSubscribedToEvent<T>(this, sender);
    }

    public bool HasEventHandlers() => Context.HasEventHandlers(this);
}
