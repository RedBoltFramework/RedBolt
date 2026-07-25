using RedBolt.Bindings.Sdl;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

public delegate void RefEventHandler<T>(ref T args) where T : struct;

public class Context : ObjectReflectionRegistry
{
    private sealed class StructEventSubscriptionBucket
    {
        public List<EventHandler> Subscriptions { get; } = [];

        public int DispatchDepth { get; set; }

        public bool RequiresCompaction { get; set; }
    }

    private readonly struct ActiveEventInvocation
    {
        public ActiveEventInvocation(Object? sender, EventHandler handler)
        {
            Sender = sender;
            Handler = handler;
        }

        public Object? Sender { get; }

        public EventHandler Handler { get; }
    }

    private static Context _instance = new();

    static Context()
    {
        GeneratedReflectionRegistration.SetCurrentContext(_instance);
    }

    private static int _sdlInitCounter;

    private readonly SubsystemCache _subsystems = new();
    private readonly Dictionary<Type, object> _subsystemsByType = [];
    private readonly Dictionary<Type, StructEventSubscriptionBucket> _structEventSubscriptions = [];
    private readonly List<ActiveEventInvocation> _activeEventInvocations = [];
    private readonly VariantMap _globalVars = [];

    public Context()
        : this(RuntimeServices.Default)
    {
    }

    public Context(RuntimeServices runtimeServices)
    {
        ArgumentNullException.ThrowIfNull(runtimeServices);
        RuntimeServices = runtimeServices;
    }

    public static Context Instance => _instance;

    public RuntimeServices RuntimeServices { get; }

    public static void SetInstance(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _instance = context;
        GeneratedReflectionRegistration.SetCurrentContext(context);
    }

    public IReadOnlyList<AttributeInfo>? GetAttributes(StringHash type)
    {
        return GetReflection(type)?.Attributes;
    }

    public void RegisterAssembly(Assembly assembly)
    {
        GeneratedReflectionRegistration.RegisterAssembly(this, assembly);
    }

    public void RegisterAttributedType(Type type)
    {
        GeneratedReflectionRegistration.RegisterType(this, type);
    }

    public void RegisterAttributedType<T>() where T : Object
    {
        RegisterAttributedType(typeof(T));
    }

    public void Reset()
    {
        GetSubsystem<ResourceCache>()?.Clear();

        var pluginManager = GetSubsystem("PluginManager");

        var virtualReality = GetSubsystem("VirtualReality");
        RemoveSubsystem("VirtualReality");
        (virtualReality as IDisposable)?.Dispose();
        RemoveSubsystem("PluginManager");
        var audio = GetSubsystem("Audio");
        RemoveSubsystem("Audio");
        (audio as IDisposable)?.Dispose();
        var dearImGui = GetSubsystem("DearImGui");
        RemoveSubsystem("DearImGui");
        (dearImGui as IDisposable)?.Dispose();
        var resourceCache = GetSubsystem("ResourceCache");
        RemoveSubsystem("ResourceCache");
        (resourceCache as IDisposable)?.Dispose();
        var input = GetSubsystem("Input");
        RemoveSubsystem("Input");
        (input as IDisposable)?.Dispose();
        var renderer = GetSubsystem("Renderer");
        RemoveSubsystem("Renderer");
        (renderer as IDisposable)?.Dispose();
        var renderDevice = GetSubsystem("RenderDevice");
        RemoveSubsystem("RenderDevice");
        (renderDevice as IDisposable)?.Dispose();
        var graphics = GetSubsystem("Graphics");
        RemoveSubsystem("Graphics");
        (graphics as IDisposable)?.Dispose();
        var stateManager = GetSubsystem("StateManager");
        RemoveSubsystem("StateManager");
        (stateManager as IDisposable)?.Dispose();

        var remainingSubsystems = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var subsystem in _subsystems.GetContainer().Values)
        {
            if (subsystem is not Engine)
                remainingSubsystems.Add(subsystem);
        }

        _subsystemsByType.Clear();
        _subsystems.Clear();
        foreach (var subsystem in remainingSubsystems)
            (subsystem as IDisposable)?.Dispose();
        (pluginManager as IDisposable)?.Dispose();

        _globalVars.Dispose();
        _activeEventInvocations.Clear();
        _structEventSubscriptions.Clear();
    }

    public override void Dispose()
    {
        Reset();
        base.Dispose();
    }

    public void InitializeEngineSubsystems(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        RegisterSubsystem(engine);
        RegisterSubsystem(new Time());
        RegisterSubsystem(new WorkQueue());
        RegisterSubsystem(new VirtualFileSystem());
        RegisterSubsystem(new Log());
        RegisterSubsystem(new ResourceCache());
        RegisterSubsystem(new Input());
        RegisterSubsystem(new Graphics());
        RegisterSubsystem(new Renderer());
        RegisterSubsystem(new Audio());
        RegisterSubsystem(new StateManager());
    }

    public void RegisterSubsystem(object subsystem)
    {
        ArgumentNullException.ThrowIfNull(subsystem);
        RegisterSubsystem(subsystem, GetSubsystemTypeId(subsystem.GetType()));
    }

    /// <summary>
    /// Register a subsystem by explicitly using a type.
    /// </summary>
    public void RegisterSubsystem(object subsystem, StringHash type)
    {
        ArgumentNullException.ThrowIfNull(subsystem);
        _subsystemsByType[subsystem.GetType()] = subsystem;
        _subsystems.Add(type, subsystem);
    }

    public void RegisterSubsystem<T>(T subsystem)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(subsystem);
        _subsystemsByType[typeof(T)] = subsystem;
        _subsystems.Add(GetSubsystemTypeId(typeof(T)), subsystem);
    }

    /// <summary>
    /// Remove a subsystem.
    /// </summary>
    public void RemoveSubsystem(StringHash objectType)
    {
        var subsystem = _subsystems.Get(objectType);
        _subsystems.Remove(objectType);

        if (subsystem is null)
            return;

        Type typeToRemove = null!;
        foreach (var (type, registeredSubsystem) in _subsystemsByType)
        {
            if (ReferenceEquals(registeredSubsystem, subsystem) && GetSubsystemTypeId(type) == objectType)
            {
                typeToRemove = type;
                break;
            }
        }

        if (typeToRemove is not null)
            _subsystemsByType.Remove(typeToRemove);
    }

    public bool RequireSdl(uint sdlFlags)
    {
        ++_sdlInitCounter;

        if (_sdlInitCounter == 1 && SdlNativeBindings.SdlInit(0) != 0)
        {
            Log.GetLogger().LogError("Failed to initialise SDL: {SdlError}", DecodeSdlError());
            return false;
        }

        var remainingFlags = sdlFlags & ~SdlNativeBindings.SdlWasInit(0);
        if (remainingFlags == 0 || SdlNativeBindings.SdlInitSubSystem(remainingFlags) == 0)
            return true;

        Log.GetLogger().LogError("Failed to initialise SDL subsystem: {SdlError}", DecodeSdlError());
        return false;
    }

    public void ReleaseSdl()
    {
        --_sdlInitCounter;

        if (_sdlInitCounter == 0)
        {
            SdlNativeBindings.SdlQuitSubSystem(uint.MaxValue);
            SdlNativeBindings.SdlQuit();
        }

        if (_sdlInitCounter < 0)
            throw new InvalidOperationException("Too many calls to Context.ReleaseSdl().");
    }

    private static string DecodeSdlError()
    {
        var error = SdlNativeBindings.SdlGetError();
        var length = error.IndexOf((byte)0);
        return System.Text.Encoding.UTF8.GetString(length < 0 ? error : error[..length]);
    }

    public bool RemoveSubsystem<T>()
        where T : class
    {
        if (!_subsystemsByType.Remove(typeof(T), out var subsystem))
            return false;

        _subsystems.Remove(GetSubsystemTypeId(subsystem.GetType()));
        return true;
    }

    public T? GetSubsystem<T>()
        where T : class
    {
        if (_subsystemsByType.TryGetValue(typeof(T), out var subsystem))
            return (T)subsystem;

        var cachedSubsystem = _subsystems.Get<T>();
        if (cachedSubsystem is not null)
            return cachedSubsystem;

        foreach (var registeredSubsystem in _subsystemsByType.Values)
        {
            if (registeredSubsystem is T typedSubsystem)
                return typedSubsystem;
        }

        return null;
    }

    /// <summary>
    /// Return subsystem by type.
    /// </summary>
    public object? GetSubsystem(StringHash type)
    {
        return _subsystems.Get(type);
    }

    /// <summary>
    /// Return all subsystems.
    /// </summary>
    public SubsystemCache GetSubsystems()
    {
        return _subsystems;
    }

    public string GetTypeName(StringHash objectType)
    {
        return GetReflection(objectType)?.ClassName ?? string.Empty;
    }

    public AttributeInfo GetAttribute(StringHash objectType, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetReflection(objectType)?.GetAttribute(name) ?? null!;
    }

    public AttributeInfo GetAttribute<T>(string name) where T : Object
    {
        return GetAttribute(ObjectReflection<T>.TypeId, name);
    }

    private static StringHash GetSubsystemTypeId(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.GetTypeId();
    }

    public void SubscribeToEvent<T>(Object receiver, RefEventHandler<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);
        SubscribeToEventCore(receiver, senderFilter: null, handler);
    }

    public void SubscribeToEvent<T>(Object receiver, Object sender, RefEventHandler<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);
        SubscribeToEventCore(receiver, sender, handler);
    }

    public void Unsubscribe(Object receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        foreach (var bucket in _structEventSubscriptions.Values)
            RemoveMatchingSubscriptions(bucket, subscription => IsReceiverMatch(subscription, receiver));

        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe(Object receiver, Object sender)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);

        foreach (var bucket in _structEventSubscriptions.Values)
        {
            RemoveMatchingSubscriptions(
                bucket,
                subscription => IsReceiverMatch(subscription, receiver) && ReferenceEquals(subscription.Sender, sender));
        }

        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe<T>(Object receiver) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);

        if (_structEventSubscriptions.TryGetValue(typeof(T), out var bucket))
            RemoveMatchingSubscriptions(bucket, subscription => IsReceiverMatch(subscription, receiver));

        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe<T>(Object receiver, Object sender) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);

        if (_structEventSubscriptions.TryGetValue(typeof(T), out var bucket))
        {
            RemoveMatchingSubscriptions(
                bucket,
                subscription => IsReceiverMatch(subscription, receiver) && ReferenceEquals(subscription.Sender, sender));
        }

        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe(Object receiver, Type eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(eventType);

        if (_structEventSubscriptions.TryGetValue(eventType, out var bucket))
            RemoveMatchingSubscriptions(bucket, subscription => IsReceiverMatch(subscription, receiver));

        RemoveEmptyEventBuckets();
    }

    public void Unsubscribe(Object receiver, Object sender, Type eventType)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(eventType);

        if (_structEventSubscriptions.TryGetValue(eventType, out var bucket))
        {
            RemoveMatchingSubscriptions(
                bucket,
                subscription => IsReceiverMatch(subscription, receiver) && ReferenceEquals(subscription.Sender, sender));
        }

        RemoveEmptyEventBuckets();
    }

    public void UnsubscribeFromEvents(Object receiver, Object sender)
    {
        Unsubscribe(receiver, sender);
    }

    /// <summary>
    /// Remove an event sender from all receivers.
    /// </summary>
    public void RemoveEventSender(Object sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        foreach (var bucket in _structEventSubscriptions.Values)
            RemoveMatchingSubscriptions(bucket, subscription => ReferenceEquals(subscription.Sender, sender));

        RemoveEmptyEventBuckets();
    }

    public void UnsubscribeAllExcept(Object receiver, IReadOnlyCollection<Object> senders)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(senders);

        foreach (var bucket in _structEventSubscriptions.Values)
        {
            RemoveMatchingSubscriptions(
                bucket,
                subscription =>
                    IsReceiverMatch(subscription, receiver) && (subscription.Sender is null || !senders.Contains(subscription.Sender)));
        }

        RemoveEmptyEventBuckets();
    }

    public void UnsubscribeAllExcept(Object receiver, IReadOnlyCollection<Type> eventTypes)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(eventTypes);

        foreach (var (eventType, bucket) in _structEventSubscriptions)
        {
            if (!eventTypes.Contains(eventType))
                RemoveMatchingSubscriptions(bucket, subscription => IsReceiverMatch(subscription, receiver));
        }

        RemoveEmptyEventBuckets();
    }

    public bool HasSubscribedToEvent(Object receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        foreach (var bucket in _structEventSubscriptions.Values)
        {
            if (HasMatchingSubscription(bucket, subscription => IsReceiverMatch(subscription, receiver)))
                return true;
        }

        return false;
    }

    public bool HasSubscribedToEvent<T>(Object receiver) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);

        if (!_structEventSubscriptions.TryGetValue(typeof(T), out var bucket))
            return false;

        return HasMatchingSubscription(bucket, subscription => IsReceiverMatch(subscription, receiver));
    }

    public bool HasSubscribedToEvent<T>(Object receiver, Object sender) where T : struct
    {
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(sender);

        if (!_structEventSubscriptions.TryGetValue(typeof(T), out var bucket))
            return false;

        return HasMatchingSubscription(bucket,
            subscription => IsReceiverMatch(subscription, receiver) && ReferenceEquals(subscription.Sender, sender));
    }

    public bool HasEventHandlers(Object receiver)
    {
        return HasSubscribedToEvent(receiver);
    }

    public Object? GetEventSender()
    {
        return _activeEventInvocations.Count > 0 ? _activeEventInvocations[^1].Sender : null;
    }

    public EventHandler? GetEventHandler()
    {
        return _activeEventInvocations.Count > 0 ? _activeEventInvocations[^1].Handler : null;
    }

    public ref readonly Variant GetGlobalVar(StringHash key)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(_globalVars, key);
        if (!Unsafe.IsNullRef(ref value))
            return ref value;
        return ref Variant.Empty;
    }

    public IReadOnlyDictionary<StringHash, Variant> GetGlobalVars() => _globalVars;

    public void SetGlobalVar(StringHash key, Variant value)
    {
        _globalVars[key] = value;
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

    public void SendEvent<T>(Object? sender, ref T args) where T : struct
    {
        if (sender is Object { BlockEvents: true })
            return;

        if (!_structEventSubscriptions.TryGetValue(typeof(T), out var bucket) || bucket.Subscriptions.Count == 0)
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

                if (!subscription.TryGetReceiver(out var receiver))
                {
                    subscription.IsRemoved = true;
                    bucket.RequiresCompaction = true;
                    continue;
                }

                if (subscription.Sender is not null && !ReferenceEquals(subscription.Sender, sender))
                    continue;

                if (subscription.Sender is null && sender is not null && HasSpecificSubscription(bucket, receiver, sender))
                    continue;

                if (receiver is Object { BlockEvents: true })
                    continue;

                _activeEventInvocations.Add(new ActiveEventInvocation(sender, subscription));
                try
                {
                    ((RefEventHandler<T>)subscription.Handler)(ref args);
                }
                finally
                {
                    _activeEventInvocations.RemoveAt(_activeEventInvocations.Count - 1);
                }
            }
        }
        finally
        {
            EndDispatch(typeof(T), bucket);
        }
    }

    private static bool IsReceiverMatch(EventHandler subscription, Object receiver)
    {
        if (!subscription.TryGetReceiver(out var targetReceiver))
            return false;

        return ReferenceEquals(targetReceiver, receiver);
    }

    private static bool HasSpecificSubscription(StructEventSubscriptionBucket bucket, Object? receiver, Object sender)
    {
        foreach (var subscription in bucket.Subscriptions)
        {
            if (subscription.IsRemoved || !ReferenceEquals(subscription.Sender, sender))
                continue;

            if (subscription.TryGetReceiver(out var targetReceiver) && ReferenceEquals(targetReceiver, receiver))
                return true;
        }

        return false;
    }

    private void SubscribeToEventCore<T>(Object receiver, Object? senderFilter, RefEventHandler<T> handler) where T : struct
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(T);
        if (!_structEventSubscriptions.TryGetValue(eventType, out var bucket))
            _structEventSubscriptions[eventType] = bucket = new StructEventSubscriptionBucket();

        for (var i = 0; i < bucket.Subscriptions.Count; ++i)
        {
            var subscription = bucket.Subscriptions[i];
            if (!subscription.IsRemoved
                && IsReceiverMatch(subscription, receiver)
                && ReferenceEquals(subscription.Sender, senderFilter))
            {
                bucket.Subscriptions[i] = new EventHandler(receiver, senderFilter, eventType, handler);
                return;
            }
        }

        bucket.Subscriptions.Add(new EventHandler(receiver, senderFilter, eventType, handler));
    }

    private static int RemoveMatchingSubscriptions(
        StructEventSubscriptionBucket bucket,
        Predicate<EventHandler> predicate)
    {
        if (bucket.DispatchDepth > 0)
        {
            var removed = 0;
            foreach (var subscription in bucket.Subscriptions)
            {
                if (subscription.IsRemoved)
                    continue;

                if (!subscription.TryGetReceiver(out _) && subscription.HasReceiver)
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
            if (!subscription.TryGetReceiver(out _) && subscription.HasReceiver)
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

    private bool HasMatchingSubscription(StructEventSubscriptionBucket bucket, Func<EventHandler, bool> predicate)
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
            _structEventSubscriptions.Remove(eventType);
    }

    private void RemoveEmptyEventBuckets()
    {
        var emptyEventTypeKeys = _structEventSubscriptions
            .Where(static pair => pair.Value.DispatchDepth == 0 && pair.Value.Subscriptions.Count == 0)
            .Select(static pair => pair.Key)
            .ToArray();
        foreach (var key in emptyEventTypeKeys)
            _structEventSubscriptions.Remove(key);
    }
}
