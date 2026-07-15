using Container = System.Collections.Generic.Dictionary<RedBolt.StringHash, object>;

namespace RedBolt;

/// <summary>
/// List of cached subsystems that don't require hash map lookup.
/// </summary>
public sealed class SubsystemCache
{
    // C++: CachedSubsystemList includes FileSystem, VirtualFileSystem, Log (if URHO3D_LOGGING),
    // Network (if URHO3D_NETWORK), Audio, UI, SystemUI (if URHO3D_SYSTEMUI),
    // ParticleGraphSystem (if URHO3D_PARTICLE_GRAPH), PluginManager, StateManager
    private static readonly Type[] CachedSubsystemTypes =
    [
        typeof(Engine),
        typeof(Time),
        typeof(WorkQueue),
        typeof(ResourceCache),
        typeof(Localization),
        typeof(Input),
        typeof(Graphics),
        typeof(Renderer),
        typeof(RenderDevice)
    ];

    private static readonly int NumCachedSubsystems = CachedSubsystemTypes.Length;

    private readonly object[] _cachedSubsystems = new object[NumCachedSubsystems];
    private readonly StringHash[] _cachedSubsystemTypes = new StringHash[NumCachedSubsystems];
    private readonly Container _subsystems = [];

    /// <summary>
    /// Construct.
    /// </summary>
    public SubsystemCache()
    {
        for (var i = 0; i < _cachedSubsystemTypes.Length; ++i)
            _cachedSubsystemTypes[i] = GetTypeId(CachedSubsystemTypes[i]);
    }

    /// <summary>
    /// Add subsystem.
    /// </summary>
    public void Add(StringHash type, object subsystem)
    {
        var index = GetCacheIndex(type);
        if (index < NumCachedSubsystems)
            _cachedSubsystems[index] = subsystem;
        _subsystems[type] = subsystem;
    }

    /// <summary>
    /// Remove subsystem.
    /// </summary>
    public void Remove(StringHash type)
    {
        var index = GetCacheIndex(type);
        if (index < NumCachedSubsystems)
            _cachedSubsystems[index] = null!;
        _subsystems.Remove(type);
    }

    // C++: uses ea::move (not Clone) — moves arrays to temps to avoid modifying during destruction,
    // then fills cached with nullptr and clears subsystems map.
    /// <summary>
    /// Remove all subsystems.
    /// </summary>
    public void Clear()
    {
        var tempCachedSubsystems = _cachedSubsystems.Clone();
        var tempSubsystems = new Container(_subsystems);
        _ = tempCachedSubsystems;
        _ = tempSubsystems;

        Array.Clear(_cachedSubsystems, 0, _cachedSubsystems.Length);
        _subsystems.Clear();
    }

    /// <summary>
    /// Return subsystem by dynamic type.
    /// </summary>
    public object Get(StringHash type)
    {
        return _subsystems.TryGetValue(type, out var subsystem) ? subsystem : null!;
    }

    /// <summary>
    /// Return subsystem by static type.
    /// </summary>
    public T Get<T>() where T : class
    {
        var type = typeof(T);
        for (var i = 0; i < CachedSubsystemTypes.Length; ++i)
        {
            if (CachedSubsystemTypes[i] == type)
                return (T)_cachedSubsystems[i];
        }

        return (T)Get(ObjectReflection<T>.TypeId)!;
    }

    /// <summary>
    /// Return all subsystems.
    /// </summary>
    public Container GetContainer() => _subsystems;

    private int GetCacheIndex(StringHash type)
    {
        for (var i = 0; i < _cachedSubsystemTypes.Length; ++i)
        {
            if (_cachedSubsystemTypes[i] == type)
                return i;
        }

        return NumCachedSubsystems;
    }

    private static StringHash GetTypeId(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type.GetTypeId();
    }
}
