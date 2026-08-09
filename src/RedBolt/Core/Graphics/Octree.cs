namespace RedBolt.Graphics;

using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Octree octant.</summary>
public class Octant : IDisposable
{
    private BoundingBox _worldBoundingBox = new();
    private BoundingBox _cullingBox = new();
    private readonly List<Drawable> _drawables = [];
    private readonly Octant?[] _children = new Octant?[8];
    private Vector3f _center;
    private Vector3f _halfSize;
    private readonly uint _level;
    private uint _numDrawables;
    private readonly Octant? _parent;
    private Octree? _octree;
    private readonly uint _index;

    /// <summary>Construct.</summary>
    public Octant(in BoundingBox box, uint level, Octant? parent, Octree? octree, uint index = uint.MaxValue)
    {
        _level = level;
        _parent = parent;
        _octree = octree;
        _index = index;
        Initialize(box);
    }

    /// <summary>Destruct. Move drawables to root if available (detach if not) and free child octants.</summary>
    public virtual void Dispose()
    {
        if (_octree is not null)
        {
            var rootOctant = _octree.GetRootOctant();

            // Remove the drawables (if any) from this octant to the root octant
            foreach (var drawable in _drawables)
            {
                drawable.SetOctant(rootOctant);
                rootOctant._drawables.Add(drawable);
                _octree.QueueUpdate(drawable);
            }
            _drawables.Clear();
            _numDrawables = 0;
        }

        for (var i = 0u; i < _children.Length; ++i)
            DeleteChild(i);

        _drawables.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>Return or create a child octant.</summary>
    public Octant GetOrCreateChild(uint index)
    {
        if (_children[index] is not null)
            return _children[index]!;

        var newMin = _worldBoundingBox.Min;
        var newMax = _worldBoundingBox.Max;
        var oldCenter = _worldBoundingBox.Center;

        if ((index & 1u) != 0)
            newMin.X = oldCenter.X;
        else
            newMax.X = oldCenter.X;

        if ((index & 2u) != 0)
            newMin.Y = oldCenter.Y;
        else
            newMax.Y = oldCenter.Y;

        if ((index & 4u) != 0)
            newMin.Z = oldCenter.Z;
        else
            newMax.Z = oldCenter.Z;

        _children[index] = new Octant(new BoundingBox(newMin, newMax), _level + 1, this, _octree, index);
        return _children[index]!;
    }

    /// <summary>Delete child octant.</summary>
    public void DeleteChild(uint index)
    {
        System.Diagnostics.Debug.Assert(index < _children.Length);
        _children[index]?.Dispose();
        _children[index] = null;
    }

    /// <summary>Insert a drawable object by checking for fit recursively.</summary>
    public void InsertDrawable(Drawable drawable)
    {
        var box = drawable.GetWorldBoundingBox();

        // If root octant, insert all non-occludees here, so that octant occlusion does not hide the drawable.
        // Also if drawable is outside the root octant bounds, insert to root
        bool insertHere;
        if (ReferenceEquals(this, _octree!.GetRootOctant()))
            insertHere = !drawable.IsOccludee() || _cullingBox.IsInside(box) != Intersection.Inside || CheckDrawableFit(box);
        else
            insertHere = CheckDrawableFit(box);

        if (insertHere)
        {
            var oldOctant = drawable.GetOctant();
            if (!ReferenceEquals(oldOctant, this))
            {
                // Add first, then remove, because drawable count going to zero deletes the octree branch in question
                AddDrawable(drawable);
                oldOctant?.RemoveDrawable(drawable, false);
            }
        }
        else
        {
            var boxCenter = box.Center;
            var x = boxCenter.X < _center.X ? 0u : 1u;
            var y = boxCenter.Y < _center.Y ? 0u : 2u;
            var z = boxCenter.Z < _center.Z ? 0u : 4u;

            GetOrCreateChild(x + y + z).InsertDrawable(drawable);
        }
    }

    /// <summary>Check if a drawable object fits.</summary>
    public bool CheckDrawableFit(in BoundingBox box)
    {
        var boxSize = box.Size;

        // If max split level, size always OK, otherwise check that box is at least half size of octant
        if (_level >= _octree!.GetNumLevels() || boxSize.X >= _halfSize.X || boxSize.Y >= _halfSize.Y ||
            boxSize.Z >= _halfSize.Z)
            return true;
        // Also check if the box can not fit a child octant's culling box, in that case size OK (must insert here)
        else
        {
            if (box.Min.X <= _worldBoundingBox.Min.X - 0.5f * _halfSize.X ||
                box.Max.X >= _worldBoundingBox.Max.X + 0.5f * _halfSize.X ||
                box.Min.Y <= _worldBoundingBox.Min.Y - 0.5f * _halfSize.Y ||
                box.Max.Y >= _worldBoundingBox.Max.Y + 0.5f * _halfSize.Y ||
                box.Min.Z <= _worldBoundingBox.Min.Z - 0.5f * _halfSize.Z ||
                box.Max.Z >= _worldBoundingBox.Max.Z + 0.5f * _halfSize.Z)
                return true;
        }

        // Bounding box too small, should create a child octant
        return false;
    }

    /// <summary>Add a drawable object to this octant.</summary>
    public void AddDrawable(Drawable drawable)
    {
        drawable.SetOctant(this);
        _drawables.Add(drawable);
        IncDrawableCount();
    }

    /// <summary>Remove a drawable object from this octant.</summary>
    public void RemoveDrawable(Drawable drawable, bool resetOctant = true)
    {
        var index = _drawables.IndexOf(drawable);
        if (index != -1)
        {
            _drawables.RemoveAt(index);
            if (resetOctant)
                drawable.SetOctant(null);
            DecDrawableCount();
        }
    }

    /// <summary>Return world-space bounding box.</summary>
    public ref readonly BoundingBox GetWorldBoundingBox() => ref _worldBoundingBox;

    /// <summary>Return bounding box used for fitting drawable objects.</summary>
    public ref readonly BoundingBox GetCullingBox() => ref _cullingBox;

    /// <summary>Return subdivision level.</summary>
    public uint GetLevel() => _level;

    /// <summary>Return parent octant.</summary>
    public Octant? GetParent() => _parent;

    /// <summary>Return octree.</summary>
    public Octree? GetOctree() => _octree;

    /// <summary>Return number of drawables.</summary>
    public uint GetNumDrawables() => _numDrawables;

    /// <summary>Return true if there are no drawable objects in this octant and child octants.</summary>
    public bool IsEmpty() => _numDrawables == 0;

    /// <summary>Set size for the root octant. If octree is not empty, drawable objects will be temporarily moved to the root.</summary>
    public void SetRootSize(in BoundingBox box)
    {
        // If drawables exist, they are temporarily moved to the root
        for (var i = 0u; i < _children.Length; ++i)
            DeleteChild(i);

        Initialize(box);
        _numDrawables = (uint)_drawables.Count;
    }

    /// <summary>Reset octree pointer recursively. Called when the whole octree is being destroyed.</summary>
    public void ResetOctree()
    {
        _octree = null;

        // The whole octree is being destroyed, just detach the drawables
        foreach (var drawable in _drawables)
        {
            drawable.SetOctant(null);
            drawable.SetDrawableIndex(uint.MaxValue);
        }

        foreach (var child in _children)
            child?.ResetOctree();
    }

    /// <summary>Draw bounds to the debug graphics recursively.</summary>
    public void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && debug.IsInside(_worldBoundingBox))
        {
            debug.AddBoundingBox(_worldBoundingBox, new Color(0.25f, 0.25f, 0.25f), depthTest);

            foreach (var child in _children)
                child?.DrawDebugGeometry(debug, depthTest);
        }
    }

    /// <summary>Return drawable objects by a ray query, called internally.</summary>
    internal void GetDrawablesInternal(OctreeQuery query, bool inside)
    {
        if (!ReferenceEquals(this, _octree!.GetRootOctant()))
        {
            var result = query.TestOctant(_cullingBox, inside);
            if (result == Intersection.Inside)
                inside = true;
            else if (result == Intersection.Outside)
            {
                // Fully outside, so cull this octant, its children & drawables
                return;
            }
        }

        if (_drawables.Count != 0)
            query.TestDrawables(_drawables, inside);

        foreach (var child in _children)
            child?.GetDrawablesInternal(query, inside);
    }

    /// <summary>Return drawable objects by a ray query, called internally.</summary>
    internal void GetDrawablesInternal(RayOctreeQuery query)
    {
        var octantDistance = query.Ray.HitDistance(_cullingBox);
        if (octantDistance >= query.MaxDistance)
            return;

        foreach (var drawable in _drawables)
        {
            if ((drawable.GetDrawableFlags() & query.DrawableFlags) != 0 &&
                (drawable.GetViewMask() & query.ViewMask) != 0)
                drawable.ProcessRayQuery(query, query.Result);
        }

        foreach (var child in _children)
            child?.GetDrawablesInternal(query);
    }

    /// <summary>Return drawable objects only for a threaded ray query, called internally.</summary>
    internal void GetDrawablesOnlyInternal(RayOctreeQuery query, List<Drawable> drawables)
    {
        var octantDistance = query.Ray.HitDistance(_cullingBox);
        if (octantDistance >= query.MaxDistance)
            return;

        foreach (var drawable in _drawables)
        {
            if ((drawable.GetDrawableFlags() & query.DrawableFlags) != 0 &&
                (drawable.GetViewMask() & query.ViewMask) != 0)
                drawables.Add(drawable);
        }

        foreach (var child in _children)
            child?.GetDrawablesOnlyInternal(query, drawables);
    }

    /// <summary>Initialize bounding box.</summary>
    protected void Initialize(in BoundingBox box)
    {
        _worldBoundingBox = box;
        _center = box.Center;
        _halfSize = 0.5f * box.Size;
        _cullingBox = new BoundingBox(_worldBoundingBox.Min - _halfSize, _worldBoundingBox.Max + _halfSize);
    }

    /// <summary>Increase drawable object count recursively.</summary>
    protected void IncDrawableCount()
    {
        ++_numDrawables;
        _parent?.IncDrawableCount();
    }

    /// <summary>Decrease drawable object count recursively and remove octant if it becomes empty.</summary>
    protected void DecDrawableCount()
    {
        var parent = _parent;

        --_numDrawables;
        if (_numDrawables == 0)
        {
            if (parent is not null)
                parent.DeleteChild(_index);
        }

        parent?.DecDrawableCount();
    }
}

/// <summary>Acceleration structure for zone search.</summary>
public sealed class ZoneLookupIndex : IDisposable
{
    private struct ZoneData
    {
        public BoundingBox BoundingBox;
        public Matrix3x4 InverseWorldTransform;
        public uint ZoneMask;
    }

    private readonly Zone? _defaultZone;
    private readonly List<Zone> _zones = [];
    private readonly List<ZoneData> _zonesData = [];
    private bool _zonesDirty;

    public ZoneLookupIndex()
    {
        var context = Context.Instance;
        var renderer = context.GetSubsystem<Renderer>();
        _defaultZone = renderer?.GetDefaultZone();
    }

    public void Dispose()
    {
        _zonesData.Clear();
        _zones.Clear();
        GC.SuppressFinalize(this);
    }

    public void AddZone(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        System.Diagnostics.Debug.Assert(!_zones.Contains(zone));
        _zones.Add(zone);
        _zonesDirty = true;
    }

    public void UpdateZone(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        System.Diagnostics.Debug.Assert(_zones.Contains(zone));
        _zonesDirty = true;
    }

    public void RemoveZone(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var index = _zones.IndexOf(zone);
        System.Diagnostics.Debug.Assert(index >= 0);
        _zones.RemoveAt(index);
    }

    public void Commit()
    {
        if (_zonesDirty)
        {
            _zonesDirty = false;

            // Sort zones by priority from high to low
            _zones.Sort(static (lhs, rhs) => rhs.GetPriority().CompareTo(lhs.GetPriority()));

            // Update cached data
            _zonesData.Clear();
            foreach (var zone in _zones)
            {
                _zonesData.Add(new ZoneData
                {
                    ZoneMask = zone.GetZoneMask(),
                    BoundingBox = zone.GetBoundingBox(),
                    InverseWorldTransform = zone.GetInverseWorldTransform()
                });
            }
        }

        foreach (var zone in _zones)
            zone.UpdateCachedData();
        _defaultZone?.UpdateCachedData();
    }

    public CachedDrawableZone QueryZone(Vector3f position, uint zoneMask)
    {
        var minDistanceToOtherZone = MathDefs.LargeValue;
        var distanceToBestZoneBorder = MathDefs.LargeValue;
        Zone? bestZone = null;

        for (var i = 0; i < _zonesData.Count; ++i)
        {
            var data = _zonesData[i];
            if ((data.ZoneMask & zoneMask) == 0)
                continue;

            var localPosition = data.InverseWorldTransform * position;
            var signedDistance = data.BoundingBox.SignedDistanceToPoint(localPosition);

            if (signedDistance > 0.0f)
            {
                // Zone cannot affect point, keep distance for cache
                minDistanceToOtherZone = MathF.Min(minDistanceToOtherZone, signedDistance);
            }
            else if (bestZone is null)
            {
                // Zone may affect point
                bestZone = _zones[i];
                distanceToBestZoneBorder = -signedDistance;
            }
        }

        var cacheInvalidationDistance = MathF.Min(minDistanceToOtherZone, distanceToBestZoneBorder);
        return new CachedDrawableZone(bestZone ?? _defaultZone, position, cacheInvalidationDistance * cacheInvalidationDistance);
    }

    public Zone? GetBackgroundZone()
    {
        return _zones.Count > 0 && _zones[^1].GetPriority() <= 0 ? _zones[^1] : _defaultZone;
    }
}

[Reflected(ObjectCategory.Category_Subsystem)]
public sealed partial class Octree : Component
{
    private const float DefaultOctreeSize = 1000.0f;
    private const uint DefaultOctreeLevels = 8;

    private readonly Octant _rootOctant;
    private readonly List<Drawable?> _drawableUpdates;
    private readonly List<Drawable?> _threadedDrawableUpdates;
    private readonly Action<int, Drawable?> _updateDrawableCallback;
    private FrameInfo _updateFrame;
    private readonly WorkQueueVector<KeyValuePair<Node, Transform>> _pendingNodeTransforms;
    private readonly List<Drawable> _drawables;
    private readonly object _octreeMutex;
    private readonly List<Drawable> _rayQueryDrawables;
    [Serialized("Number of Levels", DefaultSource = nameof(GetNumberOfLevelsAttributeDefault))]
    private uint _numLevels;
    private BoundingBox _worldBoundingBox = new();
    private readonly ZoneLookupIndex _zones;
    private bool _locked;

    public Octree()
    {
        var rootBox = new BoundingBox(-DefaultOctreeSize, DefaultOctreeSize);
        _rootOctant = new Octant(rootBox, 0, null, this);
        _drawableUpdates = [];
        _threadedDrawableUpdates = [];
        _updateDrawableCallback = UpdateDrawable;
        _pendingNodeTransforms = new WorkQueueVector<KeyValuePair<Node, Transform>>();
        _drawables = [];
        _octreeMutex = new object();
        _rayQueryDrawables = [];
        _numLevels = DefaultOctreeLevels;
        _worldBoundingBox = _rootOctant.GetWorldBoundingBox();
        _zones = new ZoneLookupIndex();

        // If the engine is running headless, subscribe to RenderUpdate events for manually updating the octree
        // to allow raycasts and animation update
        if (GetSubsystem<Graphics>() is null)
            SubscribeToEvent<CoreEvents.RenderUpdateEvent>(HandleRenderUpdate);

    }


    public override void Dispose()
    {
        // Reset root pointer from all child octants now so that they do not move their drawables to root
        _drawableUpdates.Clear();
        _rootOctant.ResetOctree();

        _zones.Dispose();
        _rayQueryDrawables.Clear();
        _drawables.Clear();
        _pendingNodeTransforms.Clear();
        _threadedDrawableUpdates.Clear();
        _drawableUpdates.Clear();
        _rootOctant.Dispose();
        base.Dispose();
    }

    public Octant GetRootOctant() => _rootOctant;

    public uint GetNumLevels() => _numLevels;

    public IReadOnlyList<Drawable> GetAllDrawables() => _drawables;

    public void SetSize(BoundingBox box, uint numLevels)
    {
        _worldBoundingBox = box;
        _rootOctant.SetRootSize(box);
        _numLevels = SystemMath.Max(numLevels, 1u);
    }

    public void SetLocked(bool locked)
    {
        this._locked = locked;
    }

    public void AddManualDrawable(Drawable? drawable)
    {
        if (drawable is null || drawable.GetOctant() is not null)
            return;

        AddDrawable(drawable);
    }

    public void RemoveManualDrawable(Drawable? drawable)
    {
        if (drawable is null)
            return;

        var octant = drawable.GetOctant();
        if (octant is not null && ReferenceEquals(octant.GetOctree(), this))
        {
            CancelUpdate(drawable);
            RemoveDrawable(drawable, octant);
        }
    }

    public void AddDrawable(Drawable drawable)
    {
        if (_locked)
            return;

        if (drawable.GetDrawableIndex() != uint.MaxValue)
            return;

        var index = (uint)_drawables.Count;
        _drawables.Add(drawable);
        drawable.SetDrawableIndex(index);
        _rootOctant.InsertDrawable(drawable);

        if ((drawable.GetDrawableFlags() & DrawableFlags.Zone) != 0)
        {
            var zone = (Zone)drawable;
            _zones.AddZone(zone);
            zone.ClearDrawablesZone();
        }
    }

    public void QueueUpdate(Drawable drawable)
    {
        var scene = Scene;
        if (scene is not null && scene.IsThreadedUpdate())
        {
            lock (_octreeMutex)
                _threadedDrawableUpdates.Add(drawable);
        }
        else
            _drawableUpdates.Add(drawable);

        drawable._updateQueued = true;
    }

    public void QueueNodeTransformUpdate(Node node, in Transform transform)
    {
        _pendingNodeTransforms.Insert(new KeyValuePair<Node, Transform>(node, transform));
    }

    public void CancelUpdate(Drawable drawable)
    {
        // This doesn't have to take into account scene being in threaded update, because it is called only
        // when removing a drawable from octree, which should only ever happen from the main thread.
        var lastIndex = _drawableUpdates.Count - 1;
        if (lastIndex >= 0 && ReferenceEquals(_drawableUpdates[lastIndex], drawable))
            _drawableUpdates.RemoveAt(lastIndex);
        else
            _drawableUpdates.Remove(drawable);
        drawable._updateQueued = false;
    }

    public void MarkZoneDirty(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        _zones.UpdateZone(zone);
    }

    public void Update(FrameInfo frame)
    {
        // Let drawables update themselves before reinsertion. This can be used for animation
        if (_drawableUpdates.Count != 0)
        {
            // Perform updates in worker threads. Notify the scene that a threaded update is going on and components
            // (for example physics objects) should not perform non-threadsafe work when marked dirty
            var scene = Scene!;
            var queue = GetSubsystem<WorkQueue>()!;
            scene.BeginThreadedUpdate();

            _pendingNodeTransforms.Clear();

            _updateFrame = frame;
            WorkQueue.ForEachParallel(queue, _drawableUpdates, _updateDrawableCallback);

            scene.EndThreadedUpdate();
        }

        // If any drawables were inserted during threaded update, update them now from the main thread
        if (_threadedDrawableUpdates.Count != 0)
        {
            foreach (var drawable in _threadedDrawableUpdates)
            {
                if (drawable is not null)
                {
                    drawable.Update(frame);
                    _drawableUpdates.Add(drawable);
                }
            }

            _threadedDrawableUpdates.Clear();
        }

        // Commit delayed Node transforms
        if (_drawableUpdates.Count != 0)
        {
            var buckets = _pendingNodeTransforms.GetUnderlyingCollection();
            for (var bucketIndex = 0; bucketIndex < buckets.Count; ++bucketIndex)
            {
                var bucket = buckets[bucketIndex];
                for (var i = 0; i < bucket.Count; ++i)
                {
                    var pair = bucket[i];
                    var transform = pair.Value;
                    pair.Key.SetTransform(transform.Position, transform.Rotation, transform.Scale);
                }
            }
        }

        // Notify drawable update being finished. Custom animation (eg. IK) can be done at this point
        var currentScene = Scene;
        if (currentScene is not null)
        {
            var eventData = new SceneEvents.SceneDrawableUpdateFinishedEvent
            {
                Scene = currentScene,
                TimeStep = frame.TimeStep
            };
            currentScene.SendEvent(ref eventData);
        }

        // Reinsert drawables that have been moved or resized, or that have been newly added to the octree and do not sit inside
        // the proper octant yet
        if (_drawableUpdates.Count != 0)
        {
            foreach (var queuedDrawable in _drawableUpdates)
            {
                var drawable = queuedDrawable!;
                drawable._updateQueued = false;
                var octant = drawable.GetOctant();
                var box = drawable.GetWorldBoundingBox();

                // Skip if no octant or does not belong to this octree anymore
                if (octant is null || !ReferenceEquals(octant.GetOctree(), this))
                    continue;
                // Skip if still fits the current octant
                if (drawable.IsOccludee() && octant.GetCullingBox().IsInside(box) == Intersection.Inside &&
                    octant.CheckDrawableFit(box))
                    continue;

                _rootOctant.InsertDrawable(drawable);
            }
        }

        _drawableUpdates.Clear();

        // Update other singletons.
        // TODO: Refactor it, maybe split Octree?
        _zones.Commit();

        if (currentScene is not null)
            currentScene.GetComponent<ReflectionProbeManager>()?.Update();
    }

    private void UpdateDrawable(int index, Drawable? drawable)
    {
        if (drawable is not null)
            drawable.Update(_updateFrame);
    }

    public void RemoveDrawable(Drawable drawable, Octant octant)
    {
        if (_locked)
            return;

        var index = drawable.GetDrawableIndex();
        if (index >= _drawables.Count || !ReferenceEquals(_drawables[(int)index], drawable))
            return;

        // Remove drawable from Octree
        octant.RemoveDrawable(drawable);

        // Remove drawable from Zone index
        if ((drawable.GetDrawableFlags() & DrawableFlags.Zone) != 0)
            _zones.RemoveZone((Zone)drawable);

        // Remove drawable from index
        if (_drawables.Count > 1)
        {
            var replacement = _drawables[^1];
            _drawables[(int)index] = replacement;
            replacement.SetDrawableIndex(index);
        }
        _drawables.RemoveAt(_drawables.Count - 1);

        drawable.SetDrawableIndex(uint.MaxValue);
        drawable._updateQueued = false;
        drawable.GetMutableCachedZone() = new CachedDrawableZone();
        drawable.GetMutableCachedReflection() = new CachedDrawableReflection();
    }

    public void GetDrawables(OctreeQuery query)
    {
        query.Result.Clear();
        _rootOctant.GetDrawablesInternal(query, false);
    }

    public void Raycast(RayOctreeQuery query)
    {
        query.Result.Clear();
        _rootOctant.GetDrawablesInternal(query);
        query.Result.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
    }

    public void RaycastSingle(RayOctreeQuery query)
    {
        query.Result.Clear();
        _rayQueryDrawables.Clear();
        _rootOctant.GetDrawablesOnlyInternal(query, _rayQueryDrawables);

        // Sort by increasing hit distance to AABB
        foreach (var drawable in _rayQueryDrawables)
            drawable.SetSortValue(query.Ray.HitDistance(drawable.GetWorldBoundingBox()));

        _rayQueryDrawables.Sort(static (left, right) => left.GetSortValue().CompareTo(right.GetSortValue()));

        // Then do the actual test according to the query, and early-out as possible
        var closestHit = float.PositiveInfinity;
        foreach (var drawable in _rayQueryDrawables)
        {
            if (drawable.GetSortValue() < MathF.Min(closestHit, query.MaxDistance))
            {
                var oldSize = query.Result.Count;
                drawable.ProcessRayQuery(query, query.Result);
                if (query.Result.Count > oldSize)
                    closestHit = MathF.Min(closestHit, query.Result[^1].Distance);
            }
            else
                break;
        }

        if (query.Result.Count > 1)
        {
            query.Result.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
            query.Result.RemoveRange(1, query.Result.Count - 1);
        }
    }

    public CachedDrawableZone QueryZone(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return QueryZone(drawable.GetWorldBoundingBox().Center, drawable.GetZoneMask());
    }

    public CachedDrawableZone QueryZone(Vector3f drawablePosition, uint zoneMask)
    {
        return _zones.QueryZone(drawablePosition, zoneMask);
    }

    public Zone? GetBackgroundZone() => _zones.GetBackgroundZone();

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null)
            _rootOctant.DrawDebugGeometry(debug, depthTest);
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public void DrawDebugGeometry(bool depthTest)
    {
        var debug = GetComponent<DebugRenderer>();
        DrawDebugGeometry(debug, depthTest);
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (previousScene is not null)
        {
            UnsubscribeFromEvent<SceneEvents.WorldOriginUpdateEvent>(previousScene);
            UnsubscribeFromEvent<SceneEvents.WorldOriginPostUpdateEvent>(previousScene);
        }

        if (scene is not null)
        {
            SubscribeToEvent<SceneEvents.WorldOriginUpdateEvent>(scene, HandleWorldOriginUpdate);
            SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>(scene, HandleWorldOriginPostUpdate);
        }

    }

    private void HandleWorldOriginUpdate(ref SceneEvents.WorldOriginUpdateEvent eventData)
    {
        foreach (var drawable in _drawables)
            drawable.UpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
    }

    private void HandleWorldOriginPostUpdate(ref SceneEvents.WorldOriginPostUpdateEvent eventData)
    {
        foreach (var drawable in _drawables)
            drawable.PostUpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
    }

    private void HandleRenderUpdate(ref CoreEvents.RenderUpdateEvent eventData)
    {
        var scene = Scene;
        if (scene is null || !scene.IsUpdateEnabled())
            return;

        var frame = new FrameInfo
        {
            FrameNumber = GetSubsystem<Time>()!.GetFrameNumber(),
            TimeStep = eventData.TimeStep,
            Camera = null
        };

        Update(frame);
    }

    /// <summary>Update octree size.</summary>
    private void UpdateOctreeSize()
    {
        SetSize(_worldBoundingBox, _numLevels);
    }


    private static object? GetBoundingBoxMinAttributeDefault() => -Vector3f.One * DefaultOctreeSize;

    [Serialized("Bounding Box Min", typeof(Vector3f), DefaultSource = nameof(GetBoundingBoxMinAttributeDefault), Setter = nameof(SetSerializedBoundingBoxMin))]
    private object? GetSerializedBoundingBoxMin()
    {
        Serializable self = this;
        return ((Octree)self)._worldBoundingBox.Min;
    }

    private void SetSerializedBoundingBoxMin(object? value)
    {
        Serializable self = this;
        ((Octree)self).SetSize(
            new BoundingBox(
                value is Vector3f min ? min : -Vector3f.One * DefaultOctreeSize,
                ((Octree)self)._worldBoundingBox.Max),
            ((Octree)self)._numLevels);
    }

    private static object? GetBoundingBoxMaxAttributeDefault() => Vector3f.One * DefaultOctreeSize;

    [Serialized("Bounding Box Max", typeof(Vector3f), DefaultSource = nameof(GetBoundingBoxMaxAttributeDefault), Setter = nameof(SetSerializedBoundingBoxMax))]
    private object? GetSerializedBoundingBoxMax()
    {
        Serializable self = this;
        return ((Octree)self)._worldBoundingBox.Max;
    }

    private void SetSerializedBoundingBoxMax(object? value)
    {
        Serializable self = this;
        ((Octree)self).SetSize(
            new BoundingBox(
                ((Octree)self)._worldBoundingBox.Min,
                value is Vector3f max ? max : Vector3f.One * DefaultOctreeSize),
            ((Octree)self)._numLevels);
    }

    private static object? GetNumberOfLevelsAttributeDefault() => DefaultOctreeLevels;

    private void SetSerializedNumberOfLevels(object? value)
    {
        Serializable self = this;
        ((Octree)self).SetSize(
            ((Octree)self)._worldBoundingBox,
            value is uint levels ? levels : DefaultOctreeLevels);
    }
}
