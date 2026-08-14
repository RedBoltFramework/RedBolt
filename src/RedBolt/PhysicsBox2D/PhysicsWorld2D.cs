// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Bindings.Box2D;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>2D physics raycast hit.</summary>
public struct PhysicsRaycastResult2D : IEquatable<PhysicsRaycastResult2D>
{
    /// <summary>Test for equality.</summary>
    public static bool operator ==(PhysicsRaycastResult2D left, PhysicsRaycastResult2D right) => !(left != right);
    /// <summary>Test for inequality.</summary>
    public static bool operator !=(PhysicsRaycastResult2D left, PhysicsRaycastResult2D right) =>
        left.Position != right.Position || left.Normal != right.Normal || left.Distance != right.Distance ||
        !ReferenceEquals(left.Body, right.Body);

    public readonly bool Equals(PhysicsRaycastResult2D other) => this == other;
    public override readonly bool Equals(object? obj) => obj is PhysicsRaycastResult2D other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Position, Normal, Distance, Body);

    /// <summary>Hit world-space position.</summary>
    public Vector2f Position;
    /// <summary>Hit world-space normal.</summary>
    public Vector2f Normal;
    /// <summary>Hit distance from ray origin.</summary>
    public float Distance;
    /// <summary>Rigid body that was hit.</summary>
    public RigidBody2D? Body;
}

/// <summary>Delayed world transform assignment for a parented 2D rigid body.</summary>
public struct DelayedWorldTransform2D
{
    public RigidBody2D RigidBody;
    public RigidBody2D ParentRigidBody;
    public Vector3f WorldPosition;
    public Quaternion WorldRotation;
}

/// <summary>2D physics simulation world component. Should be added only to the root scene node.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = false)]
public partial class PhysicsWorld2D : Component
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PreSolveData
    {
        public int ShapeAIndex;
        public ushort ShapeAWorld;
        public ushort ShapeAGeneration;
        public int ShapeBIndex;
        public ushort ShapeBWorld;
        public ushort ShapeBGeneration;
        public float NormalX;
        public float NormalY;
        public int PointCount;
        public float Point0X;
        public float Point0Y;
        public float Separation0;
        public float Point1X;
        public float Point1Y;
        public float Separation1;
    }

    private readonly List<WeakReference<RigidBody2D>> _rigidBodies = [];
    private readonly Dictionary<long, WeakReference<CollisionShape2D>> _shapes = [];
    private readonly Dictionary<ContactKey, ContactState> _contacts = [];
    private readonly Dictionary<RigidBody2D, DelayedWorldTransform2D> _delayedWorldTransforms = [];
    private B2WorldId? _worldId;
    private GCHandle _preSolveGcHandle;
    private nint _preSolveCallbackContext;
    private Vector2f _gravity = new(0.0f, -9.81f);
    private bool _updateEnabled = true;
    private bool _drawShape = true;
    private bool _drawJoint;
    private bool _drawAabb;
    private bool _drawPair;
    private bool _drawCenterOfMass;
    private bool _allowSleeping = true;
    private bool _warmStarting = true;
    private bool _continuousPhysics = true;
    private bool _subStepping;
    private bool _autoClearForces = true;
    private int _velocityIterations = 8;
    private int _positionIterations = 3;

    /// <summary>Construct.</summary>
    public PhysicsWorld2D()
    {
        using var def = Box2DNativeBindings.B2DefaultWorldDef();
        def.GravityX = _gravity.X;
        def.GravityY = _gravity.Y;
        def.EnableSleep = _allowSleeping;
        def.EnableContinuous = _continuousPhysics;
        _worldId = Box2DNativeBindings.B2CreateWorld(def);
        _preSolveGcHandle = GCHandle.Alloc(this);
        unsafe
        {
            System.Diagnostics.Debug.Assert(sizeof(PreSolveData) == 52);
            var callback = (nint)(delegate* unmanaged[Cdecl]<PreSolveData*, nint, int>)&HandlePreSolve;
            _preSolveCallbackContext = Box2DNativeBindings.RedboltB2WorldSetManagedPreSolveCallback(
                _worldId, callback, GCHandle.ToIntPtr(_preSolveGcHandle));
        }
        if (_preSolveCallbackContext == 0)
        {
            _preSolveGcHandle.Free();
            throw new InvalidOperationException("Could not create the Box2D pre-solve callback.");
        }
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        if (IsWorldValid && _preSolveCallbackContext != 0)
            Box2DNativeBindings.RedboltB2WorldClearManagedPreSolveCallback(_worldId!, _preSolveCallbackContext);
        _preSolveCallbackContext = 0;
        if (_preSolveGcHandle.IsAllocated)
            _preSolveGcHandle.Free();
        if (IsWorldValid)
            Box2DNativeBindings.B2DestroyWorld(_worldId!);
        _worldId?.Dispose();
        _worldId = null;
        base.Dispose();
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || !IsEnabledEffective)
            return;
        var drawnConstraints = new HashSet<Constraint2D>();
        foreach (var weakBody in _rigidBodies)
        {
            if (!weakBody.TryGetTarget(out var body))
                continue;
            foreach (var shape in body.CollisionShapes)
            {
                if (_drawShape)
                    shape.DrawDebugGeometry(debug, depthTest);
                if (_drawAabb)
                    shape.DrawDebugAabb(debug, depthTest);
            }
            if (_drawJoint)
            {
                foreach (var constraint in body.Constraints)
                    if (drawnConstraints.Add(constraint))
                        constraint.DrawDebugGeometry(debug, depthTest);
            }
            if (_drawCenterOfMass)
            {
                var center = body.TryGetNode()?.WorldPosition ?? Vector3f.Zero;
                center.X += body.MassCenter.X;
                center.Y += body.MassCenter.Y;
                debug.AddCross(center, 0.1f, Color.Yellow, depthTest);
            }
        }
    }

    /// <summary>Visualize physics debug geometry using the scene debug renderer.</summary>
    public void DrawDebugGeometry()
    {
        var debug = Scene?.GetComponent<DebugRenderer>();
        if (debug is not null)
            DrawDebugGeometry(debug, false);
    }

    /// <summary>Step the simulation forward.</summary>
    public void Update(float timeStep)
    {
        if (!IsWorldValid || timeStep <= 0.0f)
            return;

        var preStep = new PhysicsEvents2D.PhysicsPreStep2DEvent { World = this, TimeStep = timeStep };
        SendEvent(ref preStep);
        Box2DNativeBindings.B2WorldStep(_worldId!, timeStep, _subStepping ? System.Math.Max(_positionIterations, 1) : 1);
        ProcessContactEvents();

        ApplyingTransforms = true;
        for (var i = _rigidBodies.Count - 1; i >= 0; --i)
        {
            if (_rigidBodies[i].TryGetTarget(out var body))
                body.ApplyWorldTransform();
            else
                _rigidBodies.RemoveAt(i);
        }

        while (_delayedWorldTransforms.Count != 0)
        {
            var applied = false;
            foreach (var (body, transform) in _delayedWorldTransforms.ToArray())
            {
                if (_delayedWorldTransforms.ContainsKey(transform.ParentRigidBody))
                    continue;
                body.ApplyWorldTransform(transform.WorldPosition, transform.WorldRotation);
                _delayedWorldTransforms.Remove(body);
                applied = true;
            }
            if (!applied)
            {
                _delayedWorldTransforms.Clear();
                break;
            }
        }
        ApplyingTransforms = false;

        var postStep = new PhysicsEvents2D.PhysicsPostStep2DEvent { World = this, TimeStep = timeStep };
        SendEvent(ref postStep);
    }

    /// <summary>Perform a physics world raycast and return the closest hit.</summary>
    public void RaycastSingle(ref PhysicsRaycastResult2D result, Vector2f startPoint, Vector2f endPoint,
        uint collisionMask = uint.MaxValue)
    {
        result = default;
        if (!IsWorldValid)
            return;

        var translation = endPoint - startPoint;
        if (translation.LengthSquared <= MathDefs.Epsilon * MathDefs.Epsilon)
            return;

        using var origin = PhysicsUtils2D.ToB2(startPoint);
        using var delta = PhysicsUtils2D.ToB2(translation);
        using var filter = Box2DNativeBindings.B2DefaultQueryFilter();
        filter.MaskBits = collisionMask;
        using var hit = Box2DNativeBindings.B2WorldCastRayClosest(_worldId!, origin, delta, filter);
        if (!hit.Hit)
            return;

        var key = ((long)hit.ShapeIdWorld0 << 48) | ((long)hit.ShapeIdGeneration << 32) | (uint)hit.ShapeIdIndex1;
        CollisionShape2D? shape = null;
        if (_shapes.TryGetValue(key, out var weakShape))
            weakShape.TryGetTarget(out shape);
        result.Position = new Vector2f(hit.PointX, hit.PointY);
        result.Normal = new Vector2f(hit.NormalX, hit.NormalY);
        result.Distance = translation.Length * hit.Fraction;
        result.Body = shape?.RigidBody;
    }

    /// <summary>Perform a physics world raycast and return all hits.</summary>
    public void Raycast(ICollection<PhysicsRaycastResult2D> results, Vector2f startPoint, Vector2f endPoint,
        uint collisionMask = uint.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();
        if (!IsWorldValid)
            return;
        var translation = endPoint - startPoint;
        if (translation.LengthSquared <= MathDefs.Epsilon * MathDefs.Epsilon)
            return;
        using var origin = PhysicsUtils2D.ToB2(startPoint);
        using var delta = PhysicsUtils2D.ToB2(translation);
        using var filter = Box2DNativeBindings.B2DefaultQueryFilter();
        filter.MaskBits = collisionMask;
        var count = Box2DNativeBindings.RedboltB2WorldCastRayCount(_worldId!, origin, delta, filter);
        for (var index = 0; index < count; ++index)
        {
            using var hit = Box2DNativeBindings.RedboltB2WorldCastRayAt(_worldId!, origin, delta, filter, index);
            if (!hit.Hit || !TryGetShape(hit.ShapeIdWorld0, hit.ShapeIdGeneration, hit.ShapeIdIndex1, out var shape))
                continue;
            results.Add(new PhysicsRaycastResult2D
            {
                Position = new Vector2f(hit.PointX, hit.PointY),
                Normal = new Vector2f(hit.NormalX, hit.NormalY),
                Distance = translation.Length * hit.Fraction,
                Body = shape.RigidBody,
            });
        }
    }

    /// <summary>Return rigid body at point.</summary>
    public RigidBody2D? GetRigidBody(Vector2f point, uint collisionMask = uint.MaxValue)
    {
        foreach (var weakBody in _rigidBodies)
        {
            if (!weakBody.TryGetTarget(out var body))
                continue;
            foreach (var shape in body.CollisionShapes)
            {
                if ((shape.CategoryBits & collisionMask) == 0 || shape.ShapeId is null)
                    continue;
                using var nativePoint = PhysicsUtils2D.ToB2(point);
                if (Box2DNativeBindings.B2ShapeTestPoint(shape.ShapeId, nativePoint))
                    return body;
            }
        }
        return null;
    }

    /// <summary>Return rigid body at screen coordinates in the first viewport rendering this scene.</summary>
    public RigidBody2D? GetRigidBody(int screenX, int screenY, uint collisionMask = uint.MaxValue)
    {
        var viewport = GetSubsystem<Renderer>()?.GetViewportForScene(Scene);
        var worldPoint = viewport?.ScreenToWorldPoint(screenX, screenY, 0.0f);
        return worldPoint is { } point ? GetRigidBody(new Vector2f(point.X, point.Y), collisionMask) : null;
    }

    /// <summary>Return rigid bodies by a box query.</summary>
    public void GetRigidBodies(ICollection<RigidBody2D> results, Rect aabb, uint collisionMask = uint.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();
        foreach (var weakBody in _rigidBodies)
        {
            if (!weakBody.TryGetTarget(out var body))
                continue;
            if (body.CollisionShapes.Any(shape => (shape.CategoryBits & collisionMask) != 0 && shape.Overlaps(aabb)))
                results.Add(body);
        }
    }

    /// <summary>Enable or disable automatic physics simulation during scene update.</summary>
    [Serialized("Is Enabled")]
    public bool UpdateEnabled { get => _updateEnabled; set => _updateEnabled = value; }

    [Serialized("Draw Shape")]
    public bool DrawShape { get => _drawShape; set => _drawShape = value; }
    [Serialized("Draw Joint")]
    public bool DrawJoint { get => _drawJoint; set => _drawJoint = value; }
    [Serialized("Draw Aabb")]
    public bool DrawAabb { get => _drawAabb; set => _drawAabb = value; }
    [Serialized("Draw Pair")]
    public bool DrawPair { get => _drawPair; set => _drawPair = value; }
    [Serialized("Draw Center Of Mass")]
    public bool DrawCenterOfMass { get => _drawCenterOfMass; set => _drawCenterOfMass = value; }

    [Serialized("Allow Sleeping")]
    public bool AllowSleeping
    {
        get => _allowSleeping;
        set
        {
            _allowSleeping = value;
            if (IsWorldValid)
                Box2DNativeBindings.B2WorldEnableSleeping(_worldId!, value);
        }
    }

    [Serialized("Warm Starting")]
    public bool WarmStarting
    {
        get => _warmStarting;
        set
        {
            _warmStarting = value;
            if (IsWorldValid)
                Box2DNativeBindings.B2WorldEnableWarmStarting(_worldId!, value);
        }
    }

    [Serialized("Continuous Physics")]
    public bool ContinuousPhysics
    {
        get => _continuousPhysics;
        set
        {
            _continuousPhysics = value;
            if (IsWorldValid)
                Box2DNativeBindings.B2WorldEnableContinuous(_worldId!, value);
        }
    }

    [Serialized("Sub Stepping")]
    public bool SubStepping { get => _subStepping; set => _subStepping = value; }

    [Serialized("Gravity")]
    public Vector2f Gravity
    {
        get => _gravity;
        set
        {
            _gravity = value;
            if (!IsWorldValid)
                return;
            using var gravity = PhysicsUtils2D.ToB2(value);
            Box2DNativeBindings.B2WorldSetGravity(_worldId!, gravity);
        }
    }

    [Serialized("Auto Clear Forces")]
    public bool AutoClearForces { get => _autoClearForces; set => _autoClearForces = value; }
    [Serialized("Velocity Iterations")]
    public int VelocityIterations { get => _velocityIterations; set => _velocityIterations = System.Math.Max(value, 1); }
    [Serialized("Position Iterations")]
    public int PositionIterations { get => _positionIterations; set => _positionIterations = System.Math.Max(value, 1); }

    /// <summary>Return the native Box2D world ID.</summary>
    public B2WorldId? World => IsWorldValid ? _worldId : null;

    internal B2WorldId WorldId => _worldId!;
    public bool ApplyingTransforms { get; set; }
    private bool IsWorldValid => _worldId is not null && Box2DNativeBindings.B2WorldIsValid(_worldId);

    public void AddRigidBody(RigidBody2D body)
    {
        if (_rigidBodies.Any(item => item.TryGetTarget(out var value) && ReferenceEquals(value, body)))
            return;
        _rigidBodies.Add(new WeakReference<RigidBody2D>(body));
    }

    public void RemoveRigidBody(RigidBody2D body)
    {
        _rigidBodies.RemoveAll(item => !item.TryGetTarget(out var value) || ReferenceEquals(value, body));
    }

    /// <summary>Add a delayed world transform assignment for a parented rigid body.</summary>
    public void AddDelayedWorldTransform(DelayedWorldTransform2D transform) =>
        _delayedWorldTransforms[transform.RigidBody] = transform;

    internal void RegisterShape(CollisionShape2D shape, B2ShapeId id) =>
        _shapes[PhysicsUtils2D.GetKey(id)] = new WeakReference<CollisionShape2D>(shape);

    internal void UnregisterShape(B2ShapeId id) => _shapes.Remove(PhysicsUtils2D.GetKey(id));

    private bool TryGetShape(ushort world, ushort generation, int index, out CollisionShape2D shape)
    {
        var key = ((long)world << 48) | ((long)generation << 32) | (uint)index;
        if (_shapes.TryGetValue(key, out var weakShape) && weakShape.TryGetTarget(out var value))
        {
            shape = value;
            return true;
        }
        shape = null!;
        return false;
    }

    private void ProcessContactEvents()
    {
        using var contactEvents = Box2DNativeBindings.B2WorldGetContactEvents(_worldId!);
        for (var index = 0; index < contactEvents.BeginCount; ++index)
        {
            using var nativeEvent = Box2DNativeBindings.RedboltB2ContactBeginEventAt(_worldId!, index);
            if (!TryGetShape(nativeEvent.ShapeIdAWorld0, nativeEvent.ShapeIdAGeneration, nativeEvent.ShapeIdAIndex1, out var shapeA)
                || !TryGetShape(nativeEvent.ShapeIdBWorld0, nativeEvent.ShapeIdBGeneration, nativeEvent.ShapeIdBIndex1, out var shapeB))
                continue;
            var points = new List<PhysicsContact2D>(nativeEvent.ManifoldPointCount);
            for (var pointIndex = 0; pointIndex < nativeEvent.ManifoldPointCount; ++pointIndex)
            {
                using var point = nativeEvent.ManifoldPoints[pointIndex];
                points.Add(new PhysicsContact2D
                {
                    Position = new Vector2f(point.PointX, point.PointY),
                    Normal = new Vector2f(nativeEvent.ManifoldNormalX, nativeEvent.ManifoldNormalY),
                    Distance = point.Separation,
                });
            }
            var key = new ContactKey(ShapeKey(nativeEvent.ShapeIdAWorld0, nativeEvent.ShapeIdAGeneration, nativeEvent.ShapeIdAIndex1),
                ShapeKey(nativeEvent.ShapeIdBWorld0, nativeEvent.ShapeIdBGeneration, nativeEvent.ShapeIdBIndex1));
            var state = new ContactState(shapeA, shapeB, points);
            if (!_contacts.ContainsKey(key))
                SendBeginContact(state);
            _contacts[key] = state;
        }

        using var sensorEvents = Box2DNativeBindings.B2WorldGetSensorEvents(_worldId!);
        for (var index = 0; index < sensorEvents.BeginCount; ++index)
        {
            using var nativeEvent = Box2DNativeBindings.RedboltB2SensorBeginEventAt(_worldId!, index);
            if (!TryGetShape(nativeEvent.SensorShapeIdWorld0, nativeEvent.SensorShapeIdGeneration, nativeEvent.SensorShapeIdIndex1, out var shapeA)
                || !TryGetShape(nativeEvent.VisitorShapeIdWorld0, nativeEvent.VisitorShapeIdGeneration, nativeEvent.VisitorShapeIdIndex1, out var shapeB))
                continue;
            var key = new ContactKey(ShapeKey(nativeEvent.SensorShapeIdWorld0, nativeEvent.SensorShapeIdGeneration, nativeEvent.SensorShapeIdIndex1),
                ShapeKey(nativeEvent.VisitorShapeIdWorld0, nativeEvent.VisitorShapeIdGeneration, nativeEvent.VisitorShapeIdIndex1));
            var state = new ContactState(shapeA, shapeB, Array.Empty<PhysicsContact2D>());
            _contacts[key] = state;
            SendBeginContact(state);
        }

        for (var index = 0; index < contactEvents.EndCount; ++index)
        {
            using var nativeEvent = Box2DNativeBindings.RedboltB2ContactEndEventAt(_worldId!, index);
            EndContact(new ContactKey(ShapeKey(nativeEvent.ShapeIdAWorld0, nativeEvent.ShapeIdAGeneration, nativeEvent.ShapeIdAIndex1),
                ShapeKey(nativeEvent.ShapeIdBWorld0, nativeEvent.ShapeIdBGeneration, nativeEvent.ShapeIdBIndex1)));
        }
        for (var index = 0; index < sensorEvents.EndCount; ++index)
        {
            using var nativeEvent = Box2DNativeBindings.RedboltB2SensorEndEventAt(_worldId!, index);
            EndContact(new ContactKey(ShapeKey(nativeEvent.SensorShapeIdWorld0, nativeEvent.SensorShapeIdGeneration, nativeEvent.SensorShapeIdIndex1),
                ShapeKey(nativeEvent.VisitorShapeIdWorld0, nativeEvent.VisitorShapeIdGeneration, nativeEvent.VisitorShapeIdIndex1)));
        }
    }

    private void SendBeginContact(ContactState state)
    {
        if (!state.TryGetBodies(out var bodyA, out var bodyB))
            return;
        var global = new PhysicsEvents2D.PhysicsBeginContact2DEvent
        {
            World = this, BodyA = bodyA, BodyB = bodyB, NodeA = bodyA.Node, NodeB = bodyB.Node,
            Contacts = state.Points, ShapeA = state.ShapeA, ShapeB = state.ShapeB,
        };
        SendEvent(ref global);
        var nodeA = new PhysicsEvents2D.NodeBeginContact2DEvent
        {
            Body = bodyA, OtherBody = bodyB, OtherNode = bodyB.Node, Contacts = state.Points,
            Shape = state.ShapeA, OtherShape = state.ShapeB,
        };
        bodyA.Node.SendEvent(ref nodeA);
        var nodeB = new PhysicsEvents2D.NodeBeginContact2DEvent
        {
            Body = bodyB, OtherBody = bodyA, OtherNode = bodyA.Node, Contacts = state.Points,
            Shape = state.ShapeB, OtherShape = state.ShapeA,
        };
        bodyB.Node.SendEvent(ref nodeB);
    }

    private bool SendUpdateContact(ContactState state)
    {
        if (!state.TryGetBodies(out var bodyA, out var bodyB))
            return true;
        var global = new PhysicsEvents2D.PhysicsUpdateContact2DEvent
        {
            World = this, BodyA = bodyA, BodyB = bodyB, NodeA = bodyA.Node, NodeB = bodyB.Node,
            Contacts = state.Points, ShapeA = state.ShapeA, ShapeB = state.ShapeB, Enabled = true,
        };
        SendEvent(ref global);
        var nodeA = new PhysicsEvents2D.NodeUpdateContact2DEvent
        {
            Body = bodyA, OtherBody = bodyB, OtherNode = bodyB.Node, Contacts = state.Points,
            Shape = state.ShapeA, OtherShape = state.ShapeB, Enabled = global.Enabled,
        };
        bodyA.Node.SendEvent(ref nodeA);
        var nodeB = new PhysicsEvents2D.NodeUpdateContact2DEvent
        {
            Body = bodyB, OtherBody = bodyA, OtherNode = bodyA.Node, Contacts = state.Points,
            Shape = state.ShapeB, OtherShape = state.ShapeA, Enabled = nodeA.Enabled,
        };
        bodyB.Node.SendEvent(ref nodeB);
        return nodeB.Enabled;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int HandlePreSolve(PreSolveData* data, nint context)
    {
        try
        {
            return GCHandle.FromIntPtr(context).Target is PhysicsWorld2D world && world.PreSolve(
                data->ShapeAIndex, data->ShapeAWorld, data->ShapeAGeneration,
                data->ShapeBIndex, data->ShapeBWorld, data->ShapeBGeneration,
                data->NormalX, data->NormalY, data->PointCount,
                data->Point0X, data->Point0Y, data->Separation0,
                data->Point1X, data->Point1Y, data->Separation1) ? 1 : 0;
        }
        catch
        {
            return 1;
        }
    }

    private bool PreSolve(
        int shapeAIndex, ushort shapeAWorld, ushort shapeAGeneration,
        int shapeBIndex, ushort shapeBWorld, ushort shapeBGeneration,
        float normalX, float normalY, int pointCount,
        float point0X, float point0Y, float separation0,
        float point1X, float point1Y, float separation1)
    {
        if (!TryGetShape(shapeAWorld, shapeAGeneration, shapeAIndex, out var shapeA) ||
            !TryGetShape(shapeBWorld, shapeBGeneration, shapeBIndex, out var shapeB))
            return true;

        var points = new List<PhysicsContact2D>(System.Math.Min(pointCount, 2));
        var normal = new Vector2f(normalX, normalY);
        if (pointCount > 0)
            points.Add(new PhysicsContact2D { Position = new Vector2f(point0X, point0Y), Normal = normal, Distance = separation0 });
        if (pointCount > 1)
            points.Add(new PhysicsContact2D { Position = new Vector2f(point1X, point1Y), Normal = normal, Distance = separation1 });
        var key = new ContactKey(ShapeKey(shapeAWorld, shapeAGeneration, shapeAIndex),
            ShapeKey(shapeBWorld, shapeBGeneration, shapeBIndex));
        var state = new ContactState(shapeA, shapeB, points);
        if (!_contacts.ContainsKey(key))
            SendBeginContact(state);
        _contacts[key] = state;
        return SendUpdateContact(state);
    }

    private void EndContact(ContactKey key)
    {
        if (!_contacts.Remove(key, out var state) || !state.TryGetBodies(out var bodyA, out var bodyB))
            return;
        var global = new PhysicsEvents2D.PhysicsEndContact2DEvent
        {
            World = this, BodyA = bodyA, BodyB = bodyB, NodeA = bodyA.Node, NodeB = bodyB.Node,
            Contacts = state.Points, ShapeA = state.ShapeA, ShapeB = state.ShapeB,
        };
        SendEvent(ref global);
        var nodeA = new PhysicsEvents2D.NodeEndContact2DEvent
        {
            Body = bodyA, OtherBody = bodyB, OtherNode = bodyB.Node, Contacts = state.Points,
            Shape = state.ShapeA, OtherShape = state.ShapeB,
        };
        bodyA.Node.SendEvent(ref nodeA);
        var nodeB = new PhysicsEvents2D.NodeEndContact2DEvent
        {
            Body = bodyB, OtherBody = bodyA, OtherNode = bodyA.Node, Contacts = state.Points,
            Shape = state.ShapeB, OtherShape = state.ShapeA,
        };
        bodyB.Node.SendEvent(ref nodeB);
    }

    private static long ShapeKey(ushort world, ushort generation, int index) =>
        ((long)world << 48) | ((long)generation << 32) | (uint)index;
    private readonly record struct ContactKey(long ShapeA, long ShapeB);
    private sealed record ContactState(CollisionShape2D ShapeA, CollisionShape2D ShapeB, IReadOnlyList<PhysicsContact2D> Points)
    {
        public bool TryGetBodies(out RigidBody2D bodyA, out RigidBody2D bodyB)
        {
            bodyA = ShapeA.RigidBody!;
            bodyB = ShapeB.RigidBody!;
            return bodyA is not null && bodyB is not null && bodyA.Node is not null && bodyB.Node is not null;
        }
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (previousScene is not null)
            UnsubscribeFromEvent<SceneEvents.SceneSubsystemUpdateEvent>();
        if (scene is not null)
            SubscribeToEvent<SceneEvents.SceneSubsystemUpdateEvent>(scene, HandleSceneSubsystemUpdate);
    }

    private void HandleSceneSubsystemUpdate(ref SceneEvents.SceneSubsystemUpdateEvent eventData)
    {
        if (_updateEnabled && IsEnabledEffective)
            Update(eventData.TimeStep);
    }
}
