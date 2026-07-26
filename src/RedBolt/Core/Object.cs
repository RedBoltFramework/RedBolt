namespace RedBolt;

public abstract class Object : IArchiveSerializable, IDisposable
{
    public Context Context => Context.Instance;

    public virtual void Dispose()
    {
        var context = Context.Instance;
        context.Unsubscribe(this);
        context.RemoveEventSender(this);
        GC.SuppressFinalize(this);
    }

    public virtual void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        throw new InvalidOperationException($"Serialization is not supported for type '{GetTypeName()}'.");
    }

    public bool BlockEvents { get; set; }

    public string GetTypeName() => GetType().GetClassName();

    public StringHash GetTypeNameHash() => GetTypeName();

    public bool IsInstanceOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.IsAssignableFrom(GetType());
    }

    public bool IsInstanceOf<T>() where T : Object => this is T;

    public string GetCategory() => Context.GetReflection(GetTypeNameHash())?.Category ?? string.Empty;

    public T GetSubsystem<T>()
        where T : class
    {
        return Context.GetSubsystem<T>()!;
    }

    /// <summary>
    /// Return subsystem by type.
    /// </summary>
    public object? GetSubsystem(StringHash type)
    {
        return Context.GetSubsystem(type);
    }

    /// <summary>
    /// Return all subsystems from Context.
    /// </summary>
    public SubsystemCache GetSubsystems()
    {
        return Context.GetSubsystems();
    }

    public Object? GetEventSender()
    {
        return Context.GetEventSender();
    }

    public EventHandler? GetEventHandler()
    {
        return Context.GetEventHandler();
    }

    public void SubscribeToEvent<T>(RefEventHandler<T> handler) where T : struct
    {
        Context.SubscribeToEvent(this, handler);
    }

    public void SubscribeToEvent<T>(Object sender, RefEventHandler<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(sender);
        Context.SubscribeToEvent(this, sender, handler);
    }

    public void UnsubscribeFromEvent<T>() where T : struct => Context.Unsubscribe<T>(this);

    public void UnsubscribeFromEvent<T>(Object sender) where T : struct
    {
        ArgumentNullException.ThrowIfNull(sender);
        Context.Unsubscribe<T>(this, sender);
    }

    public void UnsubscribeFromEvents(Object sender)
    {
        ArgumentNullException.ThrowIfNull(sender);
        Context.UnsubscribeFromEvents(this, sender);
    }

    public void UnsubscribeFromAllEvents()
    {
        Context.Unsubscribe(this);
    }

    public void UnsubscribeFromAllEventsExcept(IReadOnlyCollection<Object> senders)
    {
        ArgumentNullException.ThrowIfNull(senders);
        Context.UnsubscribeAllExcept(this, senders);
    }

    public void UnsubscribeFromAllEventsExcept(IReadOnlyCollection<Type> eventTypes)
    {
        ArgumentNullException.ThrowIfNull(eventTypes);
        Context.UnsubscribeAllExcept(this, eventTypes);
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

    public ref readonly Variant GetGlobalVar(StringHash key)
    {
        return ref Context.GetGlobalVar(key);
    }

    public IReadOnlyDictionary<StringHash, Variant> GetGlobalVars() => Context.GetGlobalVars();

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

    public bool HasSubscribedToEvent<T>(Object sender) where T : struct
    {
        ArgumentNullException.ThrowIfNull(sender);
        return Context.HasSubscribedToEvent<T>(this, sender);
    }

    public bool HasEventHandlers() => Context.HasEventHandlers(this);
}
