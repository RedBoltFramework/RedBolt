using System.Collections.Concurrent;

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

        public bool IsRemoved { get; set; }
    }

    private sealed class EventSubscriptionBucket
    {
        public List<EventSubscription> Subscriptions { get; } = [];

        public int DispatchDepth { get; set; }

        public bool RequiresCompaction { get; set; }
    }

    private static Context? instance;

    private readonly ConcurrentDictionary<Type, object> subsystems = new();
    private readonly Dictionary<StringHash, EventSubscriptionBucket> eventSubscriptions = new();
    private readonly VariantMap globalVars = [];
    private ulong nextSubscriptionId = 1;

    public static Context Instance
    {
        get => instance ?? throw new InvalidOperationException("Context.Instance has not been set.");
    }

    public static Context? TryGetInstance() => instance;

    public static void SetInstance(Context? context) => instance = context;

    public void Reset()
    {
        subsystems.Clear();
        eventSubscriptions.Clear();
        globalVars.Clear();
        nextSubscriptionId = 1;
    }

    public void InitializeEngineSubsystems(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        RegisterSubsystem(engine);
        RegisterSubsystem(new EngineTime());
        RegisterSubsystem(new ResourceCache());
        RegisterSubsystem(new Input());
        RegisterSubsystem(new Graphics());
        RegisterSubsystem(new Renderer());
    }

    public void RegisterSubsystem(object subsystem)
    {
        ArgumentNullException.ThrowIfNull(subsystem);
        subsystems[subsystem.GetType()] = subsystem;
    }

    public void RegisterSubsystem<T>(T subsystem)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(subsystem);
        subsystems[typeof(T)] = subsystem;
    }

    public bool RemoveSubsystem<T>()
        where T : class
        => subsystems.TryRemove(typeof(T), out _);

    public T? GetSubsystem<T>()
        where T : class
    {
        if (subsystems.TryGetValue(typeof(T), out var subsystem))
            return (T)subsystem;

        return subsystems.Values.OfType<T>().FirstOrDefault();
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

        foreach (var bucket in eventSubscriptions.Values)
            RemoveMatchingSubscriptions(bucket, subscription => ReferenceEquals(subscription.Receiver, receiver));
        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe(object receiver, StringHash eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        if (eventSubscriptions.TryGetValue(eventType, out var bucket))
            RemoveMatchingSubscriptions(bucket, subscription => ReferenceEquals(subscription.Receiver, receiver));

        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe(object receiver, string eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        Unsubscribe(receiver, new StringHash(eventType));
    }

    public void Unsubscribe(object receiver, object sender, StringHash eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);

        if (eventSubscriptions.TryGetValue(eventType, out var bucket))
        {
            RemoveMatchingSubscriptions(bucket, subscription =>
                ReferenceEquals(subscription.Receiver, receiver) && ReferenceEquals(subscription.SenderFilter, sender));
        }

        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe(object receiver, object sender, string eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        Unsubscribe(receiver, sender, new StringHash(eventType));
    }

    public void UnsubscribeFromEvents(object receiver, object sender)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);

        foreach (var bucket in eventSubscriptions.Values)
        {
            RemoveMatchingSubscriptions(bucket, subscription =>
                ReferenceEquals(subscription.Receiver, receiver) && ReferenceEquals(subscription.SenderFilter, sender));
        }

        RemoveEmptyEventBuckets();
    }

    public void UnsubscribeAllExcept(object receiver, IReadOnlyCollection<StringHash> eventTypes)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(eventTypes);

        foreach (var (eventType, bucket) in eventSubscriptions)
        {
            if (eventTypes.Contains(eventType))
                continue;

            RemoveMatchingSubscriptions(bucket, subscription => ReferenceEquals(subscription.Receiver, receiver));
        }

        RemoveEmptyEventBuckets();
    }

    public void UnsubscribeAllExcept(object receiver, IReadOnlyCollection<object> senders)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(senders);

        foreach (var bucket in eventSubscriptions.Values)
        {
            RemoveMatchingSubscriptions(bucket, subscription =>
                ReferenceEquals(subscription.Receiver, receiver)
                && (subscription.SenderFilter is null || !senders.Contains(subscription.SenderFilter)));
        }

        RemoveEmptyEventBuckets();
    }

    public bool HasSubscribedToEvent(object receiver, StringHash eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return eventSubscriptions.TryGetValue(eventType, out var bucket)
            && bucket.Subscriptions.Any(subscription =>
                !subscription.IsRemoved
                && ReferenceEquals(subscription.Receiver, receiver)
                && subscription.SenderFilter is null);
    }

    public bool HasSubscribedToEvent(object receiver, object sender, StringHash eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);
        return eventSubscriptions.TryGetValue(eventType, out var bucket)
            && bucket.Subscriptions.Any(subscription =>
                !subscription.IsRemoved
                && ReferenceEquals(subscription.Receiver, receiver)
                && ReferenceEquals(subscription.SenderFilter, sender));
    }

    public bool HasEventHandlers(object receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return eventSubscriptions.Values.Any(bucket =>
            bucket.Subscriptions.Any(subscription => !subscription.IsRemoved && ReferenceEquals(subscription.Receiver, receiver)));
    }

    public Variant GetGlobalVar(StringHash key)
    {
        return globalVars.TryGetValue(key, out var value) ? value : default;
    }

    public VariantMap GetGlobalVars() => globalVars;

    public void SetGlobalVar(StringHash key, Variant value)
    {
        globalVars[key] = value;
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
        if (sender is Object { BlockEvents: true })
            return;

        if (!eventSubscriptions.TryGetValue(eventType, out var bucket) || bucket.Subscriptions.Count == 0)
            return;

        var effectiveEventData = eventData ?? new VariantMap();
        ++bucket.DispatchDepth;
        try
        {
            var numSubscriptions = bucket.Subscriptions.Count;
            for (var i = 0; i < numSubscriptions; ++i)
            {
                var subscription = bucket.Subscriptions[i];
                if (subscription.IsRemoved)
                    continue;

                if (subscription.SenderFilter is not null && !ReferenceEquals(subscription.SenderFilter, sender))
                    continue;

                if (subscription.Receiver is Object { BlockEvents: true })
                    continue;

                subscription.Handler(sender, eventType, effectiveEventData);
            }
        }
        finally
        {
            EndDispatch(eventType, bucket);
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

        if (!eventSubscriptions.TryGetValue(eventType, out var bucket))
            eventSubscriptions[eventType] = bucket = new EventSubscriptionBucket();

        var subscriptionId = nextSubscriptionId++;
        bucket.Subscriptions.Add(new EventSubscription
        {
            Id = subscriptionId,
            Receiver = receiver,
            SenderFilter = senderFilter,
            Handler = handler
        });

        return new ContextEventSubscription(this, subscriptionId);
    }

    internal void Unsubscribe(ulong subscriptionId)
    {
        foreach (var bucket in eventSubscriptions.Values)
        {
            if (RemoveMatchingSubscriptions(bucket, subscription => subscription.Id == subscriptionId) != 0)
                break;
        }

        RemoveEmptyEventBuckets();
    }

    private static int RemoveMatchingSubscriptions(EventSubscriptionBucket bucket, Predicate<EventSubscription> predicate)
    {
        if (bucket.DispatchDepth > 0)
        {
            var removed = 0;
            foreach (var subscription in bucket.Subscriptions)
            {
                if (subscription.IsRemoved || !predicate(subscription))
                    continue;

                subscription.IsRemoved = true;
                bucket.RequiresCompaction = true;
                ++removed;
            }

            return removed;
        }

        return bucket.Subscriptions.RemoveAll(predicate);
    }

    private void EndDispatch(StringHash eventType, EventSubscriptionBucket bucket)
    {
        --bucket.DispatchDepth;
        if (bucket.DispatchDepth == 0 && bucket.RequiresCompaction)
        {
            bucket.Subscriptions.RemoveAll(static subscription => subscription.IsRemoved);
            bucket.RequiresCompaction = false;
        }

        if (bucket.DispatchDepth == 0 && bucket.Subscriptions.Count == 0)
            eventSubscriptions.Remove(eventType);
    }

    private void RemoveEmptyEventBuckets()
    {
        var emptyKeys = eventSubscriptions
            .Where(static pair => pair.Value.DispatchDepth == 0 && pair.Value.Subscriptions.Count == 0)
            .Select(static pair => pair.Key)
            .ToArray();
        foreach (var key in emptyKeys)
            eventSubscriptions.Remove(key);
    }
}
