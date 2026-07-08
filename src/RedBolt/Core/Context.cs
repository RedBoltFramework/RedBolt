namespace RedBolt;

public delegate void RefEventHandler<T>(ref T args) where T : struct;

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
    private sealed class StructEventSubscription
    {
        public required ulong Id { get; init; }

        public required WeakReference<object>? Receiver { get; init; }

        public object? SenderFilter { get; init; }

        public required Delegate Handler { get; init; }

        public bool IsRemoved { get; set; }
    }

    private sealed class StructEventSubscriptionBucket
    {
        public List<StructEventSubscription> Subscriptions { get; } = [];

        public int DispatchDepth { get; set; }

        public bool RequiresCompaction { get; set; }
    }

    private static Context? instance;

    private readonly Dictionary<Type, object> subsystems = [];
    private readonly Dictionary<Type, StructEventSubscriptionBucket> structEventSubscriptions = [];
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
        structEventSubscriptions.Clear();
        globalVars.Clear();
        nextSubscriptionId = 1;
    }

    public void InitializeEngineSubsystems(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        RegisterSubsystem(engine);
        RegisterSubsystem(new EngineTime());
        RegisterSubsystem(new WorkQueue());
        RegisterSubsystem(new ResourceCache());
        RegisterSubsystem(new Input());
        RegisterSubsystem(new Graphics());
        RegisterSubsystem(new RenderDevice());
        RegisterSubsystem(new PipelineStateCache());
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
        => subsystems.Remove(typeof(T));

    public T? GetSubsystem<T>()
        where T : class
    {
        if (subsystems.TryGetValue(typeof(T), out var subsystem))
            return (T)subsystem;

        return subsystems.Values.OfType<T>().FirstOrDefault();
    }

    public ContextEventSubscription SubscribeToEvent<T>(RefEventHandler<T> handler) where T : struct
    {
        return SubscribeToEventCore<T>(receiver: null, senderFilter: null, handler);
    }

    public ContextEventSubscription SubscribeToEvent<T>(object receiver, RefEventHandler<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return SubscribeToEventCore(receiver, senderFilter: null, handler);
    }

    public ContextEventSubscription SubscribeToEvent<T>(object receiver, object sender, RefEventHandler<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);
        return SubscribeToEventCore(receiver, sender, handler);
    }

    public void Unsubscribe(object receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        foreach (var bucket in structEventSubscriptions.Values)
            RemoveMatchingSubscriptions(bucket, subscription => IsReceiverMatch(subscription, receiver));

        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe(object receiver, object sender)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);

        foreach (var bucket in structEventSubscriptions.Values)
        {
            RemoveMatchingSubscriptions(
                bucket,
                subscription => IsReceiverMatch(subscription, receiver) && ReferenceEquals(subscription.SenderFilter, sender));
        }

        RemoveEmptyEventBuckets();
    }

    public void UnsubscribeFromEvents(object receiver, object sender)
    {
        Unsubscribe(receiver, sender);
    }

    public void UnsubscribeAllExcept(object receiver, IReadOnlyCollection<object> senders)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(senders);

        foreach (var bucket in structEventSubscriptions.Values)
        {
            RemoveMatchingSubscriptions(
                bucket,
                subscription =>
                    IsReceiverMatch(subscription, receiver) && (subscription.SenderFilter is null || !senders.Contains(subscription.SenderFilter)));
        }

        RemoveEmptyEventBuckets();
    }

    public bool HasSubscribedToEvent(object receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        foreach (var bucket in structEventSubscriptions.Values)
        {
            if (HasMatchingSubscription(bucket, subscription => IsReceiverMatch(subscription, receiver)))
                return true;
        }

        return false;
    }

    public bool HasSubscribedToEvent<T>(object receiver) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);

        if (!structEventSubscriptions.TryGetValue(typeof(T), out var bucket))
            return false;

        return HasMatchingSubscription(bucket, subscription => IsReceiverMatch(subscription, receiver));
    }

    public bool HasSubscribedToEvent<T>(object receiver, object sender) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);

        if (!structEventSubscriptions.TryGetValue(typeof(T), out var bucket))
            return false;

        return HasMatchingSubscription(bucket,
            subscription => IsReceiverMatch(subscription, receiver) && ReferenceEquals(subscription.SenderFilter, sender));
    }

    public bool HasEventHandlers(object receiver)
    {
        return HasSubscribedToEvent(receiver);
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

    public void SendEvent<T>() where T : struct
    {
        var args = new T();
        SendEvent(sender: null, ref args);
    }

    public void SendEvent<T>(ref T args) where T : struct
    {
        SendEvent(sender: null, ref args);
    }

    public void SendEvent<T>(object? sender, ref T args) where T : struct
    {
        if (sender is Object { BlockEvents: true })
            return;

        if (!structEventSubscriptions.TryGetValue(typeof(T), out var bucket) || bucket.Subscriptions.Count == 0)
            return;

        ++bucket.DispatchDepth;
        try
        {
            var numSubscriptions = bucket.Subscriptions.Count;
            for (var i = 0; i < numSubscriptions; ++i)
            {
                var subscription = bucket.Subscriptions[i];
                if (subscription.IsRemoved)
                    continue;

                if (!TryGetReceiver(subscription, out var receiver))
                {
                    subscription.IsRemoved = true;
                    bucket.RequiresCompaction = true;
                    continue;
                }

                if (subscription.SenderFilter is not null && !ReferenceEquals(subscription.SenderFilter, sender))
                    continue;

                if (receiver is Object { BlockEvents: true })
                    continue;

                ((RefEventHandler<T>)subscription.Handler)(ref args);
            }
        }
        finally
        {
            EndDispatch(typeof(T), bucket);
        }
    }

    private static bool IsReceiverMatch(StructEventSubscription subscription, object receiver)
    {
        if (!TryGetReceiver(subscription, out var targetReceiver))
            return false;

        return ReferenceEquals(targetReceiver, receiver);
    }

    private static bool TryGetReceiver(StructEventSubscription subscription, out object? receiver)
    {
        if (subscription.Receiver is null)
        {
            receiver = null;
            return true;
        }

        return subscription.Receiver.TryGetTarget(out receiver);
    }

    private ContextEventSubscription SubscribeToEventCore<T>(object? receiver, object? senderFilter, RefEventHandler<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(T);
        if (!structEventSubscriptions.TryGetValue(eventType, out var bucket))
            structEventSubscriptions[eventType] = bucket = new StructEventSubscriptionBucket();

        var subscriptionId = nextSubscriptionId++;
        bucket.Subscriptions.Add(new StructEventSubscription
        {
            Id = subscriptionId,
            Receiver = receiver is null ? null : new WeakReference<object>(receiver),
            SenderFilter = senderFilter,
            Handler = handler
        });

        return new ContextEventSubscription(this, subscriptionId);
    }

    internal void Unsubscribe(ulong subscriptionId)
    {
        foreach (var bucket in structEventSubscriptions.Values)
        {
            if (RemoveMatchingSubscriptions(bucket, subscription => subscription.Id == subscriptionId) != 0)
                break;
        }

        RemoveEmptyEventBuckets();
    }

    private static int RemoveMatchingSubscriptions(
        StructEventSubscriptionBucket bucket,
        Predicate<StructEventSubscription> predicate)
    {
        if (bucket.DispatchDepth > 0)
        {
            var removed = 0;
            foreach (var subscription in bucket.Subscriptions)
            {
                if (subscription.IsRemoved)
                    continue;

                if (subscription.Receiver is not null && !subscription.Receiver.TryGetTarget(out _))
                {
                    subscription.IsRemoved = true;
                    bucket.RequiresCompaction = true;
                    ++removed;
                    continue;
                }

                if (predicate(subscription))
                {
                    subscription.IsRemoved = true;
                    bucket.RequiresCompaction = true;
                    ++removed;
                }
            }

            return removed;
        }

        var removedCount = 0;
        for (var i = bucket.Subscriptions.Count - 1; i >= 0; --i)
        {
            var subscription = bucket.Subscriptions[i];
            if (subscription.Receiver is not null && !subscription.Receiver.TryGetTarget(out _))
            {
                bucket.Subscriptions.RemoveAt(i);
                ++removedCount;
                continue;
            }

            if (subscription.IsRemoved || predicate(subscription))
            {
                bucket.Subscriptions.RemoveAt(i);
                ++removedCount;
            }
        }

        return removedCount;
    }

    private bool HasMatchingSubscription(StructEventSubscriptionBucket bucket, Func<StructEventSubscription, bool> predicate)
    {
        RemoveMatchingSubscriptions(bucket, static subscription => false);

        foreach (var subscription in bucket.Subscriptions)
        {
            if (subscription.IsRemoved)
                continue;

            if (predicate(subscription))
                return true;
        }

        if (bucket.DispatchDepth == 0 && bucket.RequiresCompaction)
        {
            bucket.Subscriptions.RemoveAll(static subscription => subscription.IsRemoved);
            bucket.RequiresCompaction = false;
        }

        if (bucket.DispatchDepth == 0 && bucket.Subscriptions.Count == 0)
            RemoveEmptyEventBuckets();

        return false;
    }

    private void EndDispatch(Type eventType, StructEventSubscriptionBucket bucket)
    {
        --bucket.DispatchDepth;
        if (bucket.DispatchDepth == 0 && bucket.RequiresCompaction)
        {
            bucket.Subscriptions.RemoveAll(static subscription => subscription.IsRemoved);
            bucket.RequiresCompaction = false;
        }

        if (bucket.DispatchDepth == 0 && bucket.Subscriptions.Count == 0)
            structEventSubscriptions.Remove(eventType);
    }

    private void RemoveEmptyEventBuckets()
    {
        var emptyEventTypeKeys = structEventSubscriptions
            .Where(static pair => pair.Value.DispatchDepth == 0 && pair.Value.Subscriptions.Count == 0)
            .Select(static pair => pair.Key)
            .ToArray();
        foreach (var key in emptyEventTypeKeys)
            structEventSubscriptions.Remove(key);
    }
}
