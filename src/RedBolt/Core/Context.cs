namespace RedBolt;

public delegate void NamedEventHandler(object? sender, StringHash eventType, VariantMap eventData);

public readonly struct ContextEventSubscription : IDisposable
{
    private readonly Context? context;
    private readonly ulong subscriptionId;

    internal ContextEventSubscription(Context context, ulong subscriptionId)
    {
        this.context = context;
        this.subscriptionId = subscriptionId;
    }

    public void Dispose()
    {
        context?.Unsubscribe(subscriptionId);
    }
}

public sealed class Context
{
    private sealed class EventSubscription
    {
        public required ulong Id { get; init; }

        public object? Receiver { get; init; }

        public object? SenderFilter { get; init; }

        public required NamedEventHandler Handler { get; init; }
    }

    private static readonly object InstanceLock = new();
    private static Context? instance;

    private readonly object syncRoot = new();
    private readonly Dictionary<Type, object> subsystems = new();
    private readonly Dictionary<StringHash, List<EventSubscription>> eventSubscriptions = new();
    private ulong nextSubscriptionId = 1;

    public static Context Instance
    {
        get
        {
            lock (InstanceLock)
            {
                return instance ?? throw new InvalidOperationException("Context.Instance has not been set.");
            }
        }
    }

    public static Context? TryGetInstance()
    {
        lock (InstanceLock)
        {
            return instance;
        }
    }

    public static void SetInstance(Context? context)
    {
        lock (InstanceLock)
        {
            instance = context;
        }
    }

    public void Reset()
    {
        lock (syncRoot)
        {
            subsystems.Clear();
            eventSubscriptions.Clear();
            nextSubscriptionId = 1;
        }
    }

    public void RegisterSubsystem<T>(T subsystem)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(subsystem);

        lock (syncRoot)
        {
            subsystems[typeof(T)] = subsystem;
        }
    }

    public bool RemoveSubsystem<T>()
        where T : class
    {
        lock (syncRoot)
        {
            return subsystems.Remove(typeof(T));
        }
    }

    public T? GetSubsystem<T>()
        where T : class
    {
        lock (syncRoot)
        {
            if (subsystems.TryGetValue(typeof(T), out var subsystem))
                return (T)subsystem;

            return subsystems.Values.OfType<T>().FirstOrDefault();
        }
    }

    public ContextEventSubscription SubscribeToEvent(StringHash eventType, NamedEventHandler handler)
    {
        return SubscribeCore(receiver: null, senderFilter: null, eventType, handler);
    }

    public ContextEventSubscription SubscribeToEvent(string eventType, NamedEventHandler handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return SubscribeToEvent(new StringHash(eventType), handler);
    }

    public ContextEventSubscription SubscribeToEvent(object receiver, StringHash eventType, NamedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return SubscribeCore(receiver, senderFilter: null, eventType, handler);
    }

    public ContextEventSubscription SubscribeToEvent(object receiver, string eventType, NamedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return SubscribeToEvent(receiver, new StringHash(eventType), handler);
    }

    public ContextEventSubscription SubscribeToEvent(object receiver, object sender, StringHash eventType, NamedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);
        return SubscribeCore(receiver, sender, eventType, handler);
    }

    public ContextEventSubscription SubscribeToEvent(object receiver, object sender, string eventType, NamedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        return SubscribeToEvent(receiver, sender, new StringHash(eventType), handler);
    }

    public void Unsubscribe(object receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        lock (syncRoot)
        {
            foreach (var pair in eventSubscriptions)
                pair.Value.RemoveAll(subscription => ReferenceEquals(subscription.Receiver, receiver));
            RemoveEmptyEventBuckets();
        }
    }

    public void Unsubscribe(object receiver, StringHash eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        lock (syncRoot)
        {
            if (eventSubscriptions.TryGetValue(eventType, out var subscriptions))
            {
                subscriptions.RemoveAll(subscription => ReferenceEquals(subscription.Receiver, receiver));
                if (subscriptions.Count == 0)
                    eventSubscriptions.Remove(eventType);
            }
        }
    }

    public void Unsubscribe(object receiver, string eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        Unsubscribe(receiver, new StringHash(eventType));
    }

    public void SendEvent(StringHash eventType, VariantMap? eventData = null)
    {
        SendEvent(sender: null, eventType, eventData);
    }

    public void SendEvent(string eventType, VariantMap? eventData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        SendEvent(new StringHash(eventType), eventData);
    }

    public void SendEvent(object? sender, StringHash eventType, VariantMap? eventData = null)
    {
        EventSubscription[] subscriptions;
        lock (syncRoot)
        {
            if (!eventSubscriptions.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
                return;

            subscriptions = handlers.ToArray();
        }

        var effectiveEventData = eventData ?? new VariantMap();
        foreach (var subscription in subscriptions)
        {
            if (subscription.SenderFilter is not null && !ReferenceEquals(subscription.SenderFilter, sender))
                continue;

            subscription.Handler(sender, eventType, effectiveEventData);
        }
    }

    public void SendEvent(object? sender, string eventType, VariantMap? eventData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        SendEvent(sender, new StringHash(eventType), eventData);
    }

    private ContextEventSubscription SubscribeCore(object? receiver, object? senderFilter, StringHash eventType, NamedEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (syncRoot)
        {
            if (!eventSubscriptions.TryGetValue(eventType, out var subscriptions))
            {
                subscriptions = [];
                eventSubscriptions[eventType] = subscriptions;
            }

            var subscriptionId = nextSubscriptionId++;
            subscriptions.Add(new EventSubscription
            {
                Id = subscriptionId,
                Receiver = receiver,
                SenderFilter = senderFilter,
                Handler = handler
            });

            return new ContextEventSubscription(this, subscriptionId);
        }
    }

    private void Unsubscribe(ulong subscriptionId)
    {
        lock (syncRoot)
        {
            foreach (var pair in eventSubscriptions)
            {
                var removed = pair.Value.RemoveAll(subscription => subscription.Id == subscriptionId);
                if (removed != 0)
                    break;
            }

            RemoveEmptyEventBuckets();
        }
    }

    private void RemoveEmptyEventBuckets()
    {
        var emptyKeys = eventSubscriptions
            .Where(static pair => pair.Value.Count == 0)
            .Select(static pair => pair.Key)
            .ToArray();
        foreach (var key in emptyKeys)
            eventSubscriptions.Remove(key);
    }
}
