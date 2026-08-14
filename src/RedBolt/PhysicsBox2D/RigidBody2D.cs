// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Bindings.Box2D;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Rigid body type.</summary>
public enum BodyType2D
{
    Static,
    Kinematic,
    Dynamic,
}

/// <summary>2D rigid body component.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = false)]
public partial class RigidBody2D : Component
{
    private readonly List<CollisionShape2D> _collisionShapes = [];
    private readonly List<Constraint2D> _constraints = [];
    private WeakReference<PhysicsWorld2D>? _physicsWorld;
    private B2BodyId? _bodyId;
    private BodyType2D _bodyType;
    private float _mass;
    private float _inertia;
    private Vector2f _massCenter;
    private bool _useFixtureMass = true;
    private float _linearDamping;
    private float _angularDamping;
    private bool _allowSleep = true;
    private bool _fixedRotation;
    private bool _bullet;
    private float _gravityScale = 1.0f;
    private bool _awake = true;
    private Vector2f _linearVelocity;
    private float _angularVelocity;

    /// <summary>Construct.</summary>
    public RigidBody2D()
    {
    }

    /// <summary>Destruct. Free the body.</summary>
    public override void Dispose()
    {
        ReleaseBody();
        if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var world))
            world.RemoveRigidBody(this);
        _node?.RemoveListener(this);
        base.Dispose();
    }

    /// <summary>Handle enabled/disabled state change.</summary>
    public override void OnSetEnabled()
    {
        if (!IsBodyValid)
            return;
        if (IsEnabledEffective)
            Box2DNativeBindings.B2BodyEnable(_bodyId!);
        else
            Box2DNativeBindings.B2BodyDisable(_bodyId!);
    }

    /// <summary>Visualize collision geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || !IsEnabledEffective)
            return;
        foreach (var shape in _collisionShapes)
            shape.DrawDebugGeometry(debug, depthTest);
    }

    /// <summary>Return or set body type.</summary>
    [Serialized("Body Type")]
    public BodyType2D BodyType
    {
        get => IsBodyValid ? (BodyType2D)Box2DNativeBindings.B2BodyGetType(_bodyId!) : _bodyType;
        set
        {
            _bodyType = value;
            if (IsBodyValid)
            {
                Box2DNativeBindings.B2BodySetType(_bodyId!, (B2BodyType)value);
                if (!_useFixtureMass)
                    ApplyCustomMass();
            }
        }
    }

    /// <summary>Return or set mass.</summary>
    [Serialized("Mass")]
    public float Mass
    {
        get => _useFixtureMass && IsBodyValid ? Box2DNativeBindings.B2BodyGetMass(_bodyId!) : _mass;
        set
        {
            _mass = MathF.Max(value, 0.0f);
            ApplyCustomMass();
        }
    }

    /// <summary>Return or set rotational inertia.</summary>
    [Serialized("Inertia")]
    public float Inertia
    {
        get => _useFixtureMass && IsBodyValid ? Box2DNativeBindings.B2BodyGetRotationalInertia(_bodyId!) : _inertia;
        set
        {
            _inertia = MathF.Max(value, 0.0f);
            ApplyCustomMass();
        }
    }

    /// <summary>Return or set mass center.</summary>
    [Serialized("Mass Center")]
    public Vector2f MassCenter
    {
        get
        {
            if (!_useFixtureMass || !IsBodyValid)
                return _massCenter;
            using var center = Box2DNativeBindings.B2BodyGetLocalCenterOfMass(_bodyId!);
            return PhysicsUtils2D.ToVector2(center);
        }
        set
        {
            _massCenter = value;
            ApplyCustomMass();
        }
    }

    /// <summary>Return or set whether mass and inertia are calculated from collision shapes.</summary>
    [Serialized("Use Fixture Mass")]
    public bool UseFixtureMass
    {
        get => _useFixtureMass;
        set
        {
            if (_useFixtureMass == value)
                return;
            _useFixtureMass = value;
            if (!IsBodyValid)
                return;
            if (value)
                Box2DNativeBindings.B2BodyApplyMassFromShapes(_bodyId!);
            else
                ApplyCustomMass();
        }
    }

    [Serialized("Linear Damping")]
    public float LinearDamping
    {
        get => IsBodyValid ? Box2DNativeBindings.B2BodyGetLinearDamping(_bodyId!) : _linearDamping;
        set
        {
            _linearDamping = value;
            if (IsBodyValid)
                Box2DNativeBindings.B2BodySetLinearDamping(_bodyId!, value);
        }
    }

    [Serialized("Angular Damping")]
    public float AngularDamping
    {
        get => IsBodyValid ? Box2DNativeBindings.B2BodyGetAngularDamping(_bodyId!) : _angularDamping;
        set
        {
            _angularDamping = value;
            if (IsBodyValid)
                Box2DNativeBindings.B2BodySetAngularDamping(_bodyId!, value);
        }
    }

    [Serialized("Allow Sleep")]
    public bool AllowSleep
    {
        get => IsBodyValid ? Box2DNativeBindings.B2BodyIsSleepEnabled(_bodyId!) : _allowSleep;
        set
        {
            _allowSleep = value;
            if (IsBodyValid)
                Box2DNativeBindings.B2BodyEnableSleep(_bodyId!, value);
        }
    }

    [Serialized("Fixed Rotation")]
    public bool FixedRotation
    {
        get => IsBodyValid ? Box2DNativeBindings.B2BodyIsFixedRotation(_bodyId!) : _fixedRotation;
        set
        {
            _fixedRotation = value;
            if (IsBodyValid)
                Box2DNativeBindings.B2BodySetFixedRotation(_bodyId!, value);
        }
    }

    [Serialized("Bullet")]
    public bool Bullet
    {
        get => IsBodyValid ? Box2DNativeBindings.B2BodyIsBullet(_bodyId!) : _bullet;
        set
        {
            _bullet = value;
            if (IsBodyValid)
                Box2DNativeBindings.B2BodySetBullet(_bodyId!, value);
        }
    }

    [Serialized("Gravity Scale")]
    public float GravityScale
    {
        get => IsBodyValid ? Box2DNativeBindings.B2BodyGetGravityScale(_bodyId!) : _gravityScale;
        set
        {
            _gravityScale = value;
            if (IsBodyValid)
                Box2DNativeBindings.B2BodySetGravityScale(_bodyId!, value);
        }
    }

    [Serialized("Awake")]
    public bool Awake
    {
        get => IsBodyValid ? Box2DNativeBindings.B2BodyIsAwake(_bodyId!) : _awake;
        set
        {
            _awake = value;
            if (IsBodyValid)
                Box2DNativeBindings.B2BodySetAwake(_bodyId!, value);
        }
    }

    [Serialized("Linear Velocity")]
    public Vector2f LinearVelocity
    {
        get
        {
            if (!IsBodyValid)
                return _linearVelocity;
            using var value = Box2DNativeBindings.B2BodyGetLinearVelocity(_bodyId!);
            return PhysicsUtils2D.ToVector2(value);
        }
        set
        {
            _linearVelocity = value;
            if (!IsBodyValid)
                return;
            using var native = PhysicsUtils2D.ToB2(value);
            Box2DNativeBindings.B2BodySetLinearVelocity(_bodyId!, native);
        }
    }

    [Serialized("Angular Velocity")]
    public float AngularVelocity
    {
        get => IsBodyValid ? Box2DNativeBindings.B2BodyGetAngularVelocity(_bodyId!) : _angularVelocity;
        set
        {
            _angularVelocity = value;
            if (IsBodyValid)
                Box2DNativeBindings.B2BodySetAngularVelocity(_bodyId!, value);
        }
    }

    public bool IsAwake => Awake;

    /// <summary>Apply force at world point.</summary>
    public void ApplyForce(Vector2f force, Vector2f point, bool wake = true)
    {
        if (!IsBodyValid)
            return;
        using var nativeForce = PhysicsUtils2D.ToB2(force);
        using var nativePoint = PhysicsUtils2D.ToB2(point);
        Box2DNativeBindings.B2BodyApplyForce(_bodyId!, nativeForce, nativePoint, wake);
    }

    public void ApplyForceToCenter(Vector2f force, bool wake = true)
    {
        if (!IsBodyValid)
            return;
        using var nativeForce = PhysicsUtils2D.ToB2(force);
        Box2DNativeBindings.B2BodyApplyForceToCenter(_bodyId!, nativeForce, wake);
    }

    public void ApplyTorque(float torque, bool wake = true)
    {
        if (IsBodyValid)
            Box2DNativeBindings.B2BodyApplyTorque(_bodyId!, torque, wake);
    }

    public void ApplyLinearImpulse(Vector2f impulse, Vector2f point, bool wake = true)
    {
        if (!IsBodyValid)
            return;
        using var nativeImpulse = PhysicsUtils2D.ToB2(impulse);
        using var nativePoint = PhysicsUtils2D.ToB2(point);
        Box2DNativeBindings.B2BodyApplyLinearImpulse(_bodyId!, nativeImpulse, nativePoint, wake);
    }

    public void ApplyLinearImpulseToCenter(Vector2f impulse, bool wake = true)
    {
        if (!IsBodyValid)
            return;
        using var nativeImpulse = PhysicsUtils2D.ToB2(impulse);
        Box2DNativeBindings.B2BodyApplyLinearImpulseToCenter(_bodyId!, nativeImpulse, wake);
    }

    public void ApplyAngularImpulse(float impulse, bool wake = true)
    {
        if (IsBodyValid)
            Box2DNativeBindings.B2BodyApplyAngularImpulse(_bodyId!, impulse, wake);
    }

    /// <summary>Create native body.</summary>
    public void CreateBody()
    {
        if (IsBodyValid || _physicsWorld is null || !_physicsWorld.TryGetTarget(out var world) || _node is null)
            return;

        using var def = Box2DNativeBindings.B2DefaultBodyDef();
        def.Type = (B2BodyType)_bodyType;
        def.PositionX = _node.WorldPosition.X;
        def.PositionY = _node.WorldPosition.Y;
        var radians = _node.WorldRotation.RollAngle * PhysicsUtils2D.DegreesToRadians;
        def.RotationC = MathF.Cos(radians);
        def.RotationS = MathF.Sin(radians);
        def.LinearVelocityX = _linearVelocity.X;
        def.LinearVelocityY = _linearVelocity.Y;
        def.AngularVelocity = _angularVelocity;
        def.LinearDamping = _linearDamping;
        def.AngularDamping = _angularDamping;
        def.GravityScale = _gravityScale;
        def.EnableSleep = _allowSleep;
        def.IsAwake = _awake;
        def.FixedRotation = _fixedRotation;
        def.IsBullet = _bullet;
        def.IsEnabled = IsEnabledEffective;
        _bodyId = Box2DNativeBindings.B2CreateBody(world.WorldId, def);

        foreach (var shape in _collisionShapes)
            shape.CreateFixture();
        if (!_useFixtureMass)
            ApplyCustomMass();
        foreach (var constraint in _constraints.ToArray())
            constraint.CreateJoint();
    }

    /// <summary>Release native body.</summary>
    public void ReleaseBody()
    {
        if (!IsBodyValid)
            return;
        foreach (var constraint in _constraints.ToArray())
            constraint.ReleaseJoint();
        foreach (var shape in _collisionShapes.ToArray())
            shape.ReleaseFixture();
        Box2DNativeBindings.B2DestroyBody(_bodyId!);
        _bodyId!.Dispose();
        _bodyId = null;
    }

    /// <summary>Apply world transform from Box2D.</summary>
    public void ApplyWorldTransform()
    {
        if (!IsBodyValid || _node is null)
            return;
        var parentRigidBody = _node.Parent != Scene ? _node.Parent?.GetComponent<RigidBody2D>() : null;
        if (parentRigidBody is null &&
            (!Box2DNativeBindings.B2BodyIsEnabled(_bodyId!) || _bodyType == BodyType2D.Static || !Awake))
            return;
        using var position = Box2DNativeBindings.B2BodyGetPosition(_bodyId!);
        using var rotation = Box2DNativeBindings.B2BodyGetRotation(_bodyId!);
        var worldPosition = _node.WorldPosition;
        worldPosition.X = position.X;
        worldPosition.Y = position.Y;
        var worldRotation = new Quaternion(Box2DNativeBindings.B2RotGetAngle(rotation) * PhysicsUtils2D.RadiansToDegrees);
        if (parentRigidBody is not null && PhysicsWorld is { } world)
        {
            world.AddDelayedWorldTransform(new DelayedWorldTransform2D
            {
                RigidBody = this,
                ParentRigidBody = parentRigidBody,
                WorldPosition = worldPosition,
                WorldRotation = worldRotation,
            });
        }
        else
            ApplyWorldTransform(worldPosition, worldRotation);
    }

    /// <summary>Apply the specified world position and rotation.</summary>
    public void ApplyWorldTransform(Vector3f newWorldPosition, Quaternion newWorldRotation)
    {
        if (_node is null || (_node.WorldPosition == newWorldPosition && _node.WorldRotation == newWorldRotation))
            return;
        var world = PhysicsWorld;
        var wasApplying = world?.ApplyingTransforms ?? false;
        if (world is not null)
            world.ApplyingTransforms = true;
        _node.SetWorldPosition(newWorldPosition);
        _node.SetWorldRotation(newWorldRotation);
        if (world is not null)
            world.ApplyingTransforms = wasApplying;
    }

    /// <summary>Return the native Box2D body ID.</summary>
    public B2BodyId? Body => BodyId;
    /// <summary>Return the physics world.</summary>
    public PhysicsWorld2D? PhysicsWorld =>
        _physicsWorld is not null && _physicsWorld.TryGetTarget(out var world) ? world : null;

    internal B2BodyId? BodyId => IsBodyValid ? _bodyId : null;
    internal IReadOnlyList<CollisionShape2D> CollisionShapes => _collisionShapes;
    internal IReadOnlyList<Constraint2D> Constraints => _constraints;
    private bool IsBodyValid => _bodyId is not null && Box2DNativeBindings.B2BodyIsValid(_bodyId);

    public void AddCollisionShape2D(CollisionShape2D shape)
    {
        if (!_collisionShapes.Contains(shape))
            _collisionShapes.Add(shape);
    }

    public void RemoveCollisionShape2D(CollisionShape2D shape) => _collisionShapes.Remove(shape);
    public void AddConstraint2D(Constraint2D constraint)
    {
        if (!_constraints.Contains(constraint))
            _constraints.Add(constraint);
    }
    public void RemoveConstraint2D(Constraint2D constraint) => _constraints.Remove(constraint);

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        previousNode?.RemoveListener(this);
        currentNode?.AddListener(this);
        if (currentNode is null)
            return;
        foreach (var shape in currentNode.GetComponents<CollisionShape2D>())
            AddCollisionShape2D(shape);
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (scene is not null)
        {
            var world = scene.GetOrCreateComponent<PhysicsWorld2D>();
            _physicsWorld = new WeakReference<PhysicsWorld2D>(world);
            world.AddRigidBody(this);
            CreateBody();
        }
        else
        {
            ReleaseBody();
            if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var world))
                world.RemoveRigidBody(this);
            _physicsWorld = null;
        }
    }

    protected override void OnMarkedDirty(Node node)
    {
        if (!IsBodyValid || PhysicsWorld?.ApplyingTransforms == true)
            return;
        var scene = Scene;
        if (scene is not null && scene.IsThreadedUpdate())
        {
            scene.DelayedMarkedDirty(this);
            return;
        }
        using var position = PhysicsUtils2D.ToB2(new Vector2f(node.WorldPosition.X, node.WorldPosition.Y));
        using var rotation = Box2DNativeBindings.B2MakeRot(node.WorldRotation.RollAngle * PhysicsUtils2D.DegreesToRadians);
        Box2DNativeBindings.B2BodySetTransform(_bodyId!, position, rotation);
    }

    private void ApplyCustomMass()
    {
        if (_useFixtureMass || !IsBodyValid)
            return;
        using var data = new B2MassData
        {
            Mass = _mass,
            RotationalInertia = _inertia,
            CenterX = _massCenter.X,
            CenterY = _massCenter.Y,
        };
        Box2DNativeBindings.B2BodySetMassData(_bodyId!, data);
    }
}
