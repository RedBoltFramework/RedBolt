// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Bindings.Box2D;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>2D collision shape component.</summary>
[Reflected(ObjectCategory.Category_Physics2D, Factory = false, InheritAttributes = false)]
public abstract partial class CollisionShape2D : Component
{
    private readonly List<B2ShapeId> _shapeIds = [];
    private WeakReference<RigidBody2D>? _rigidBody;
    private Vector3f _cachedWorldScale = Vector3f.One;
    private bool _trigger;
    private int _categoryBits = 1;
    private int _maskBits = ushort.MaxValue;
    private int _groupIndex;
    private float _density;
    private float _friction = 0.2f;
    private float _restitution;

    /// <summary>Destruct. Free native shapes.</summary>
    public override void Dispose()
    {
        ReleaseFixture();
        if (_rigidBody is not null && _rigidBody.TryGetTarget(out var rigidBody))
            rigidBody.RemoveCollisionShape2D(this);
        _node?.RemoveListener(this);
        base.Dispose();
    }

    public override void OnSetEnabled()
    {
        if (IsEnabledEffective)
            CreateFixture();
        else
            ReleaseFixture();
    }

    [Serialized("Trigger")]
    public bool Trigger
    {
        get => _trigger;
        set
        {
            if (_trigger == value)
                return;
            _trigger = value;
            RecreateFixture();
        }
    }

    [Serialized("Category Bits")]
    public int CategoryBits
    {
        get => _categoryBits;
        set
        {
            _categoryBits = value;
            UpdateFilter();
        }
    }

    [Serialized("Mask Bits")]
    public int MaskBits
    {
        get => _maskBits;
        set
        {
            _maskBits = value;
            UpdateFilter();
        }
    }

    [Serialized("Group Index")]
    public int GroupIndex
    {
        get => _groupIndex;
        set
        {
            _groupIndex = value;
            UpdateFilter();
        }
    }

    [Serialized("Density")]
    public float Density
    {
        get => _density;
        set
        {
            _density = MathF.Max(value, 0.0f);
            foreach (var id in _shapeIds)
                Box2D.B2ShapeSetDensity(id, _density, RigidBody?.UseFixtureMass ?? true);
            RigidBody?.ApplyFixtureMass();
        }
    }

    [Serialized("Friction")]
    public float Friction
    {
        get => _friction;
        set
        {
            _friction = MathF.Max(value, 0.0f);
            foreach (var id in _shapeIds)
                Box2D.B2ShapeSetFriction(id, _friction);
        }
    }

    [Serialized("Restitution")]
    public float Restitution
    {
        get => _restitution;
        set
        {
            _restitution = MathF.Max(value, 0.0f);
            foreach (var id in _shapeIds)
                Box2D.B2ShapeSetRestitution(id, _restitution);
        }
    }

    /// <summary>Return mass of all native shapes.</summary>
    public float Mass => GetMassData(static data => data.Mass);
    /// <summary>Return rotational inertia of all native shapes.</summary>
    public float Inertia => GetMassData(static data => data.RotationalInertia);
    /// <summary>Return mass center.</summary>
    public Vector2f MassCenter
    {
        get
        {
            if (_shapeIds.Count == 0)
                return default;
            using var data = Box2D.B2ShapeGetMassData(_shapeIds[0]);
            return new Vector2f(data.CenterX, data.CenterY);
        }
    }

    /// <summary>Create native shape fixture.</summary>
    public void CreateFixture()
    {
        if (_shapeIds.Count != 0 || !IsEnabledEffective)
            return;
        var rigidBody = RigidBody ?? GetComponent<RigidBody2D>();
        if (rigidBody?.BodyId is null)
            return;
        _rigidBody = new WeakReference<RigidBody2D>(rigidBody);
        rigidBody.AddCollisionShape2D(this);

        using var def = Box2D.B2DefaultShapeDef();
        def.IsSensor = _trigger;
        def.EnableContactEvents = true;
        def.EnablePreSolveEvents = true;
        def.EnableSensorEvents = true;
        def.FilterCategoryBits = unchecked((uint)_categoryBits);
        def.FilterMaskBits = unchecked((uint)_maskBits);
        def.FilterGroupIndex = _groupIndex;
        def.Density = _density;
        def.MaterialFriction = _friction;
        def.MaterialRestitution = _restitution;
        foreach (var id in CreateNativeShapes(rigidBody.BodyId, def))
        {
            _shapeIds.Add(id);
            rigidBody.PhysicsWorld?.RegisterShape(this, id);
        }
        if (rigidBody.UseFixtureMass)
            rigidBody.ApplyFixtureMass();
    }

    /// <summary>Release native shape fixture.</summary>
    public void ReleaseFixture()
    {
        var rigidBody = RigidBody;
        foreach (var id in _shapeIds)
        {
            rigidBody?.PhysicsWorld?.UnregisterShape(id);
            if (Box2D.B2ShapeIsValid(id))
                Box2D.B2DestroyShape(id, rigidBody?.UseFixtureMass ?? true);
            id.Dispose();
        }
        _shapeIds.Clear();
        rigidBody?.ApplyFixtureMass();
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || !IsEnabledEffective)
            return;
        foreach (var id in _shapeIds)
        {
            using var body = Box2D.B2ShapeGetBody(id);
            var z = Node.WorldPosition.Z;
            switch (Box2D.B2ShapeGetType(id))
            {
                case B2ShapeType.B2CircleShape:
                    using (var circle = Box2D.B2ShapeGetCircle(id))
                        debug.AddCircle(GetWorldPoint(body, circle.CenterX, circle.CenterY, z), Vector3f.Forward,
                            circle.Radius, Color.Green, 32, depthTest, false);
                    break;
                case B2ShapeType.B2PolygonShape:
                    using (var polygon = Box2D.B2ShapeGetPolygon(id))
                    {
                        for (var index = 0; index < polygon.Count; ++index)
                        {
                            using var a = polygon.Vertices[index];
                            using var b = polygon.Vertices[(index + 1) % polygon.Count];
                            debug.AddLine(GetWorldPoint(body, a.X, a.Y, z), GetWorldPoint(body, b.X, b.Y, z),
                                Color.Green, depthTest);
                        }
                    }
                    break;
                case B2ShapeType.B2SegmentShape:
                    using (var segment = Box2D.B2ShapeGetSegment(id))
                        debug.AddLine(GetWorldPoint(body, segment.Point1X, segment.Point1Y, z),
                            GetWorldPoint(body, segment.Point2X, segment.Point2Y, z), Color.Green, depthTest);
                    break;
                case B2ShapeType.B2ChainSegmentShape:
                    using (var segment = Box2D.B2ShapeGetChainSegment(id))
                        debug.AddLine(GetWorldPoint(body, segment.SegmentPoint1X, segment.SegmentPoint1Y, z),
                            GetWorldPoint(body, segment.SegmentPoint2X, segment.SegmentPoint2Y, z), Color.Green, depthTest);
                    break;
            }
        }
    }

    internal void DrawDebugAabb(DebugRenderer debug, bool depthTest)
    {
        foreach (var id in _shapeIds)
        {
            using var bounds = Box2D.B2ShapeGetAABB(id);
            debug.AddBoundingBox(new BoundingBox(
                new Vector3f(bounds.LowerBoundX, bounds.LowerBoundY, Node.WorldPosition.Z),
                new Vector3f(bounds.UpperBoundX, bounds.UpperBoundY, Node.WorldPosition.Z)), Color.Green, depthTest);
        }
    }

    private static Vector3f GetWorldPoint(B2BodyId body, float x, float y, float z)
    {
        using var local = new B2Vec2 { X = x, Y = y };
        using var world = Box2D.B2BodyGetWorldPoint(body, local);
        return new Vector3f(world.X, world.Y, z);
    }

    /// <summary>Return the first native Box2D shape ID that represents this fixture.</summary>
    public B2ShapeId? Fixture => ShapeId;

    internal B2ShapeId? ShapeId => _shapeIds.Count != 0 ? _shapeIds[0] : null;
    internal RigidBody2D? RigidBody => _rigidBody is not null && _rigidBody.TryGetTarget(out var value) ? value : null;

    internal bool Overlaps(Rect rect)
    {
        foreach (var id in _shapeIds)
        {
            using var bounds = Box2D.B2ShapeGetAABB(id);
            if (bounds.LowerBoundX <= rect.Max.X && bounds.UpperBoundX >= rect.Min.X &&
                bounds.LowerBoundY <= rect.Max.Y && bounds.UpperBoundY >= rect.Min.Y)
                return true;
        }
        return false;
    }

    protected abstract IEnumerable<B2ShapeId> CreateNativeShapes(B2BodyId bodyId, B2ShapeDef def);

    protected void RecreateFixture()
    {
        ReleaseFixture();
        CreateFixture();
    }

    protected Vector2f WorldScale2D => _node is null
        ? Vector2f.One
        : new Vector2f(MathF.Abs(_node.WorldScale.X), MathF.Abs(_node.WorldScale.Y));

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        previousNode?.RemoveListener(this);
        currentNode?.AddListener(this);
        if (currentNode is not null)
        {
            _cachedWorldScale = currentNode.WorldScale;
            var rigidBody = currentNode.GetComponent<RigidBody2D>();
            if (rigidBody is not null)
            {
                _rigidBody = new WeakReference<RigidBody2D>(rigidBody);
                rigidBody.AddCollisionShape2D(this);
                CreateFixture();
            }
        }
    }

    protected override void OnMarkedDirty(Node node)
    {
        if (_cachedWorldScale.ApproximatelyEquivalent(node.WorldScale))
            return;
        _cachedWorldScale = node.WorldScale;
        RecreateFixture();
    }

    private void UpdateFilter()
    {
        if (_shapeIds.Count == 0)
            return;
        using var filter = Box2D.B2DefaultFilter();
        filter.CategoryBits = unchecked((uint)_categoryBits);
        filter.MaskBits = unchecked((uint)_maskBits);
        filter.GroupIndex = _groupIndex;
        foreach (var id in _shapeIds)
            Box2D.B2ShapeSetFilter(id, filter);
    }

    private float GetMassData(Func<B2MassData, float> selector)
    {
        var result = 0.0f;
        foreach (var id in _shapeIds)
        {
            using var data = Box2D.B2ShapeGetMassData(id);
            result += selector(data);
        }
        return result;
    }
}
