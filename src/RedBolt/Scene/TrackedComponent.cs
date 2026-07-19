using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Base class for simple tracked component.
/// It maintains up-to-date 0-based index in the registry.
/// Index may change during the lifetime of the component!
/// </summary>
public abstract class TrackedComponentBase : Component
{
    public TrackedComponentBase()
    {
    }

    /// <summary>
    /// Manage index of the component in the registry.
    /// </summary>
    public void SetIndexInArray(uint index) => _indexInArray = index;

    public uint GetIndexInArray() => _indexInArray;

    public bool IsTrackedInRegistry() => _indexInArray != uint.MaxValue;

    /// <summary>
    /// Returns whether the component should be tracked by the registry.
    /// </summary>
    public virtual bool ShouldBeTrackedInRegistry() => false;

    /// <summary>
    /// Reconnect component to registry of the current scene.
    /// </summary>
    public abstract void ReconnectToRegistry();

    private uint _indexInArray = uint.MaxValue;
}

/// <summary>
/// Base class for component registry that keeps components derived from TrackedComponentBase.
/// </summary>
public class TrackedComponentRegistryBase : Component
{
    public virtual bool IsOnlyEnabledTracked => false;

    public TrackedComponentRegistryBase(StringHash componentType)
    {
        _componentType = componentType;
    }

    public TrackedComponentBase? GetTrackedComponentByIndex(uint index)
    {
        return index < _componentsArray.Count ? _componentsArray[(int)index] : null;
    }

    public uint GetNumTrackedComponents() => (uint)_componentsArray.Count;

    public IReadOnlyList<TrackedComponentBase> GetTrackedComponents() => _componentsArray;

    /// <summary>
    /// Internal. Manage tracked components.
    /// </summary>
    public void AddTrackedComponent(TrackedComponentBase component)
    {
        var oldIndex = component.GetIndexInArray();
        if (oldIndex != uint.MaxValue)
        {
            Debug.Assert(GetTrackedComponentByIndex(oldIndex) == component, "Component array is corrupted");
            Debug.Assert(false, $"Component is already tracked at #{oldIndex}");
            return;
        }

        var index = (uint)_componentsArray.Count;
        _componentsArray.Add(component);
        component.SetIndexInArray(index);
        OnComponentAdded(component);
    }

    public void RemoveTrackedComponent(TrackedComponentBase component)
    {
        var index = component.GetIndexInArray();
        if (index == uint.MaxValue)
        {
            Debug.Assert(false, "Component is not tracked");
            return;
        }
        if (GetTrackedComponentByIndex(index) != component)
        {
            Debug.Assert(false, "Component array is corrupted");
            return;
        }

        OnComponentRemoved(component);

        // Do swap-erase trick if possible
        var numComponents = (uint)_componentsArray.Count;
        if (numComponents > 1 && index + 1 != numComponents)
        {
            var replacement = _componentsArray[^1];
            _componentsArray[(int)index] = replacement;
            replacement.SetIndexInArray(index);
            OnComponentMoved(replacement, numComponents - 1);
        }

        _componentsArray.RemoveAt(_componentsArray.Count - 1);
        component.SetIndexInArray(uint.MaxValue);
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (scene is not null)
        {
            InitializeTrackedComponents();
            OnAddedToScene(scene);
        }
        else
        {
            OnRemovedFromScene();
            DeinitializeTrackedComponents();
        }
    }

    protected internal virtual void OnComponentAdded(TrackedComponentBase baseComponent)
    {
    }

    protected internal virtual void OnComponentMoved(TrackedComponentBase baseComponent, uint oldIndex)
    {
    }

    protected internal virtual void OnComponentRemoved(TrackedComponentBase baseComponent)
    {
    }

    protected internal virtual void OnAddedToScene(Scene scene)
    {
    }

    protected internal virtual void OnRemovedFromScene()
    {
    }

    private void InitializeTrackedComponents()
    {
        var scene = Scene;
        if (scene is null)
            return;

        if (_componentsArray.Count != 0)
        {
            Debug.Assert(false, "Invalid call to InitializeTrackedComponents()");
            _componentsArray.Clear();
        }

        var components = new List<TrackedComponentBase>();
        scene.GetComponents(components);
        foreach (var component in components)
        {
            if (!IsInstanceOfTrackedComponent(component))
                continue;

            if (component.ShouldBeTrackedInRegistry())
            {
                component.ReconnectToRegistry();
                AddTrackedComponent(component);
            }
        }
    }

    private void DeinitializeTrackedComponents()
    {
        foreach (var component in _componentsArray)
        {
            OnComponentRemoved(component);
            component.SetIndexInArray(uint.MaxValue);
        }

        _componentsArray.Clear();
    }

    private bool IsInstanceOfTrackedComponent(TrackedComponentBase component)
    {
        for (var type = component.GetType(); type is not null; type = type.BaseType)
        {
            if (new StringHash(type.GetClassName(), new StringHash.NoReverse()) == _componentType)
                return true;
        }

        return false;
    }

    private readonly StringHash _componentType;
    private readonly List<TrackedComponentBase> _componentsArray = [];
}

/// <summary>
/// Strongly typed component ID. Default value is considered invalid.
/// </summary>
public enum ComponentReference : uint
{
    None
}

public static class TrackedComponent
{
    public static ComponentReference ConstructComponentReference(uint index, uint version)
    {
        const uint maxIndex = 0xffffff;
        const uint maxVersion = 0xff;
        const uint indexOffset = 0;
        const uint versionOffset = 24;

        uint result = 0;
        result |= (index & maxIndex) << (int)indexOffset;
        result |= (version & maxVersion) << (int)versionOffset;
        return (ComponentReference)result;
    }

    public static (uint Index, uint Version) DeconstructComponentReference(ComponentReference componentId)
    {
        const uint indexOffset = 0;
        const uint versionOffset = 24;
        const uint maxIndex = 0xffffff;
        const uint maxVersion = 0xff;

        var value = (uint)componentId;
        return ((value >> (int)indexOffset) & maxIndex, (value >> (int)versionOffset) & maxVersion);
    }

    public static string ToString(ComponentReference value)
    {
        var (index, version) = DeconstructComponentReference(value);
        return value == ComponentReference.None ? "(null)" : $"{index}:{version}";
    }
}

/// <summary>
/// Base class for tracked component with reference that is stable during object lifetime.
/// </summary>
public abstract class ReferencedComponentBase : TrackedComponentBase
{
    public ReferencedComponentBase()
    {
    }

    /// <summary>
    /// Manage reference to this component.
    /// </summary>
    public void SetReference(ComponentReference reference) => _componentReference = reference;

    public ComponentReference GetReference() => _componentReference;

    private ComponentReference _componentReference;
}

/// <summary>
/// Base class for component registry that keeps components derived from ReferencedComponentBase.
/// </summary>
public class ReferencedComponentRegistryBase : TrackedComponentRegistryBase
{
    public ReferencedComponentRegistryBase(StringHash componentType)
        : base(componentType)
    {
    }

    public ReferencedComponentBase? GetTrackedComponentByReference(ComponentReference id, bool checkVersion = true)
    {
        var (index, version) = TrackedComponent.DeconstructComponentReference(id);
        if (index >= _referenceIndexToEntry.Count)
            return null;

        var entry = _referenceIndexToEntry[(int)index];
        return !checkVersion || entry.Version == version ? entry.Component : null;
    }

    public ReferencedComponentBase? GetTrackedComponentByReferenceIndex(uint index)
    {
        return index < _referenceIndexToEntry.Count ? _referenceIndexToEntry[(int)index].Component : null;
    }

    public uint GetReferenceIndexUpperBound() => (uint)_referenceIndexToEntry.Count;

    private sealed class RegistryEntry
    {
        public ReferencedComponentBase? Component;
        public uint Version;
    }

    protected internal override void OnComponentAdded(TrackedComponentBase baseComponent)
    {
        var component = (ReferencedComponentBase)baseComponent;
        var reference = component.GetReference();

        // Try to reuse existing ID if possible
        if (reference != ComponentReference.None)
        {
            if (GetTrackedComponentByReference(reference) == component)
            {
                Debug.Assert(false, $"Component is already tracked as {TrackedComponent.ToString(reference)}");
                return;
            }

            var (index, version) = TrackedComponent.DeconstructComponentReference(reference);
            var otherComponent = GetTrackedComponentByReferenceIndex(index);
            if (otherComponent is not null)
            {
                Log.GetLogger().LogWarning("Another component is already tracked as {Reference}", TrackedComponent.ToString(reference));
                reference = ComponentReference.None;
            }
            else
            {
                EnsureIndex(index);
                _referenceIndexToEntry[(int)index].Version = version;
            }
        }

        // Allocate new ID if needed
        if (reference == ComponentReference.None)
        {
            var index = AllocateReferenceIndex();
            reference = TrackedComponent.ConstructComponentReference(index, _referenceIndexToEntry[(int)index].Version);
        }

        var (entryIndex, _) = TrackedComponent.DeconstructComponentReference(reference);

        var entry = _referenceIndexToEntry[(int)entryIndex];
        entry.Component = component;

        component.SetReference(reference);
    }

    protected internal override void OnComponentRemoved(TrackedComponentBase baseComponent)
    {
        var component = (ReferencedComponentBase)baseComponent;
        var reference = component.GetReference();

        if (reference == ComponentReference.None)
        {
            Debug.Assert(false, "Component is not tracked");
            return;
        }
        if (GetTrackedComponentByReference(reference) != component)
        {
            Debug.Assert(false, $"Component array is corrupted at {TrackedComponent.ToString(reference)}");
            return;
        }

        component.SetReference(ComponentReference.None);

        var (index, _) = TrackedComponent.DeconstructComponentReference(reference);

        var entry = _referenceIndexToEntry[(int)index];
        entry.Component = null;
        entry.Version = (entry.Version + 1) % 0xff;

        _referenceIndexAllocator.Release(index);
    }

    private uint AllocateReferenceIndex()
    {
        // May need more than one attept if some indices are taken bypassing IndexAllocator
        for (uint i = 0; i <= 0xffffff; ++i)
        {
            var index = _referenceIndexAllocator.Allocate();
            EnsureIndex(index);
            if (_referenceIndexToEntry[(int)index].Component is null)
                return index;
        }

        Log.GetLogger().LogError("Failed to allocate reference for component");
        Debug.Assert(false);
        return 0;
    }

    private void EnsureIndex(uint index)
    {
        if (index >= _referenceIndexToEntry.Count)
        {
            while (_referenceIndexToEntry.Count <= index)
                _referenceIndexToEntry.Add(new RegistryEntry());
        }
    }

    private readonly IndexAllocator _referenceIndexAllocator = new();
    private readonly List<RegistryEntry> _referenceIndexToEntry = [];
}

/// <summary>
/// Template base of any TrackedComponent that automatically registers itself in registry.
/// </summary>
public class TrackedComponent<ComponentType, RegistryComponentType> : TrackedComponentBase
    where ComponentType : Component
    where RegistryComponentType : TrackedComponentRegistryBase
{
    public TrackedComponent()
    {
    }

    public RegistryComponentType? GetRegistry() => _registry;

    public override bool ShouldBeTrackedInRegistry()
    {
        if (_registry?.IsOnlyEnabledTracked == true)
            return IsEnabledEffective;
        else
            return true;
    }

    public override void ReconnectToRegistry()
    {
        var scene = Scene;
        _registry = scene?.Root.GetDerivedComponent<RegistryComponentType>();
    }

    public override void OnSetEnabled()
    {
        if (_registry?.IsOnlyEnabledTracked == true)
        {
            var wasEnabled = IsTrackedInRegistry();
            var isEnabled = ShouldBeTrackedInRegistry();
            if (_registry is not null && wasEnabled != isEnabled)
            {
                if (isEnabled)
                    _registry.AddTrackedComponent(this);
                else
                    _registry.RemoveTrackedComponent(this);
            }
        }
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        var newRegistry = scene?.Root.GetDerivedComponent<RegistryComponentType>();
        if (ReferenceEquals(newRegistry, _registry))
            return;

        if (_registry is not null && IsTrackedInRegistry())
            _registry.RemoveTrackedComponent(this);

        _registry = newRegistry;

        if (_registry is not null && ShouldBeTrackedInRegistry())
            _registry.AddTrackedComponent(this);
    }

    private RegistryComponentType? _registry;
}
