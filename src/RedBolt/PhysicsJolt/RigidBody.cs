//
// Copyright (c) 2008-2022 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.PhysicsJolt;

using RedBolt.Bindings.Jolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Physics rigid body component.</summary>
[Reflected(ObjectCategory.Category_Physics, InheritAttributes = false)]
public partial class RigidBody : Component
{
    private const float DefaultMass = 0.0f;
    private const float DefaultFriction = 0.5f;
    private const float DefaultRestitution = 0.0f;
    private const uint DefaultCollisionLayer = 0x1;
    private const uint DefaultCollisionMask = uint.MaxValue;
    private const uint InvalidBodyId = uint.MaxValue;

    private readonly List<Constraint> _constraints = [];
    private readonly List<RaycastVehicle> _vehicles = [];
    private WeakReference<PhysicsWorld>? _physicsWorld;
    private Shape? _compoundShape;
    private uint _bodyId = InvalidBodyId;
    private float _mass = DefaultMass;
    private float _friction = DefaultFriction;
    private Vector3f _anisotropicFriction = Vector3f.One;
    private float _rollingFriction;
    private float _restitution = DefaultRestitution;
    private Vector3f _linearVelocity;
    private Vector3f _angularVelocity;
    private Vector3f _linearFactor = Vector3f.One;
    private Vector3f _angularFactor = Vector3f.One;
    private float _linearDamping;
    private float _angularDamping;
    private float _linearRestThreshold = 0.8f;
    private float _angularRestThreshold = 1.0f;
    private uint _collisionLayer = DefaultCollisionLayer;
    private uint _collisionMask = DefaultCollisionMask;
    private CollisionEventMode _collisionEventMode = CollisionEventMode.Active;
    private float _contactProcessingThreshold = 1.0e18f;
    private float _ccdRadius;
    private float _ccdMotionThreshold;
    private bool _useGravity = true;
    private Vector3f _gravityOverride;
    private bool _kinematic;
    private bool _trigger;
    private Vector3f _previousPosition;
    private Vector3f _currentPosition;
    private Quaternion _previousRotation = Quaternion.IDENTITY;
    private Quaternion _currentRotation = Quaternion.IDENTITY;
    private Vector3f _lastPosition;
    private Quaternion _lastRotation = Quaternion.IDENTITY;
    private Vector3f _kinematicTargetPosition;
    private Quaternion _kinematicTargetRotation = Quaternion.IDENTITY;
    private bool _kinematicTargetDirty;
    private bool _hasSimulated;
    private bool _enableMassUpdate = true;
    private Vector3f _centerOfMass;

    /// <summary>Construct.</summary>
    public RigidBody()
    {
    }

    /// <summary>Destruct. Free the rigid body.</summary>
    public override void Dispose()
    {
        ReleaseBody();
        if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var physicsWorld))
            physicsWorld.RemoveRigidBody(this);
        _node?.RemoveListener(this);
        base.Dispose();
    }

    /// <summary>Apply attribute changes that cannot be applied immediately.</summary>
    public override void ApplyAttributes()
    {
        AddBodyToWorld();
    }

    /// <summary>Handle enabled or disabled state change.</summary>
    public override void OnSetEnabled()
    {
        if (IsEnabledEffective)
            AddBodyToWorld();
        else
            ReleaseBody();
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || !IsEnabledEffective)
            return;
        var shapes = Node.GetComponents<CollisionShape>();
        for (var i = 0; i < shapes.Count; ++i)
            shapes[i].DrawDebugGeometry(debug, depthTest);
    }

    /// <summary>Return or set mass. Zero mass makes the body static.</summary>
    [Serialized("Mass")]
    public float Mass
    {
        get => _mass;
        set
        {
            value = MathF.Max(value, 0.0f);
            if (_mass == value)
                return;
            _mass = value;
            AddBodyToWorld();
        }
    }

    /// <summary>Return or set rigid body position in world space.</summary>
    [Serialized("Physics Position", Mode = AttributeMode.File | AttributeMode.NoEdit)]
    public Vector3f Position
    {
        get
        {
            if (TryGetBodyInterface(out var bodyInterface))
                return bodyInterface.GetPosition(in _bodyId);
            return _node?.WorldPosition ?? _currentPosition;
        }
        set => SetTransform(value, Rotation);
    }

    /// <summary>Return or set rigid body rotation in world space.</summary>
    [Serialized("Physics Rotation", Mode = AttributeMode.File | AttributeMode.NoEdit)]
    public Quaternion Rotation
    {
        get
        {
            if (TryGetBodyInterface(out var bodyInterface))
                return bodyInterface.GetRotation(in _bodyId);
            return _node?.WorldRotation ?? _currentRotation;
        }
        set => SetTransform(Position, value);
    }

    /// <summary>Set rigid body position and rotation in world space as an atomic operation.</summary>
    public void SetTransform(Vector3f position, Quaternion rotation)
    {
        _currentPosition = position;
        _currentRotation = rotation;
        _previousPosition = position;
        _previousRotation = rotation;
        if (_kinematic && _hasSimulated)
        {
            _kinematicTargetPosition = position;
            _kinematicTargetRotation = rotation;
            _kinematicTargetDirty = true;
        }
        else if (TryGetBodyInterface(out var bodyInterface))
        {
            bodyInterface.SetPositionAndRotation(in _bodyId, position, rotation, EActivation.Activate);
        }
    }

    /// <summary>Return or set linear velocity.</summary>
    [Serialized("Linear Velocity")]
    public Vector3f LinearVelocity
    {
        get => TryGetBodyInterface(out var bodyInterface) ? bodyInterface.GetLinearVelocity(in _bodyId) : _linearVelocity;
        set
        {
            _linearVelocity = value;
            if (TryGetBodyInterface(out var bodyInterface))
                bodyInterface.SetLinearVelocity(in _bodyId, value);
        }
    }

    /// <summary>Return or set linear degrees of freedom.</summary>
    [Serialized("Linear Factor")]
    public Vector3f LinearFactor
    {
        get => _linearFactor;
        set
        {
            if (_linearFactor == value)
                return;
            _linearFactor = value;
            AddBodyToWorld();
        }
    }

    /// <summary>Return linear velocity at local point.</summary>
    public Vector3f GetVelocityAtPoint(Vector3f position)
    {
        if (!TryGetBodyInterface(out var bodyInterface))
            return Vector3f.Zero;
        var worldPoint = Position + Rotation * position;
        return bodyInterface.GetPointVelocity(in _bodyId, worldPoint);
    }

    /// <summary>Return or set linear velocity deactivation threshold.</summary>
    [Serialized("Linear Rest Threshold")]
    public float LinearRestThreshold
    {
        get => _linearRestThreshold;
        set => _linearRestThreshold = MathF.Max(value, 0.0f);
    }

    /// <summary>Return or set linear velocity damping factor.</summary>
    [Serialized("Linear Damping")]
    public float LinearDamping
    {
        get => _linearDamping;
        set
        {
            value = MathF.Max(value, 0.0f);
            if (_linearDamping == value)
                return;
            _linearDamping = value;
            AddBodyToWorld();
        }
    }

    /// <summary>Return or set angular velocity.</summary>
    [Serialized("Angular Velocity")]
    public Vector3f AngularVelocity
    {
        get => TryGetBodyInterface(out var bodyInterface) ? bodyInterface.GetAngularVelocity(in _bodyId) : _angularVelocity;
        set
        {
            _angularVelocity = value;
            if (TryGetBodyInterface(out var bodyInterface))
                bodyInterface.SetAngularVelocity(in _bodyId, value);
        }
    }

    /// <summary>Return or set angular degrees of freedom.</summary>
    [Serialized("Angular Factor")]
    public Vector3f AngularFactor
    {
        get => _angularFactor;
        set
        {
            if (_angularFactor == value)
                return;
            _angularFactor = value;
            AddBodyToWorld();
        }
    }

    /// <summary>Return or set angular velocity deactivation threshold.</summary>
    [Serialized("Angular Rest Threshold")]
    public float AngularRestThreshold
    {
        get => _angularRestThreshold;
        set => _angularRestThreshold = MathF.Max(value, 0.0f);
    }

    /// <summary>Return or set angular velocity damping factor.</summary>
    [Serialized("Angular Damping")]
    public float AngularDamping
    {
        get => _angularDamping;
        set
        {
            value = MathF.Max(value, 0.0f);
            if (_angularDamping == value)
                return;
            _angularDamping = value;
            AddBodyToWorld();
        }
    }

    /// <summary>Return or set friction coefficient.</summary>
    [Serialized("Friction")]
    public float Friction
    {
        get => TryGetBodyInterface(out var bodyInterface) ? bodyInterface.GetFriction(in _bodyId) : _friction;
        set
        {
            _friction = value;
            if (TryGetBodyInterface(out var bodyInterface))
                bodyInterface.SetFriction(in _bodyId, value);
        }
    }

    /// <summary>Return or set directional friction. Jolt keeps this value for rbfx serialization compatibility.</summary>
    [Serialized("Anisotropic Friction")]
    public Vector3f AnisotropicFriction
    {
        get => _anisotropicFriction;
        set => _anisotropicFriction = value;
    }

    /// <summary>Return or set rolling friction. Jolt keeps this value for rbfx serialization compatibility.</summary>
    [Serialized("Rolling Friction")]
    public float RollingFriction
    {
        get => _rollingFriction;
        set => _rollingFriction = MathF.Max(value, 0.0f);
    }

    /// <summary>Return or set restitution coefficient.</summary>
    [Serialized("Restitution")]
    public float Restitution
    {
        get => TryGetBodyInterface(out var bodyInterface) ? bodyInterface.GetRestitution(in _bodyId) : _restitution;
        set
        {
            _restitution = value;
            if (TryGetBodyInterface(out var bodyInterface))
                bodyInterface.SetRestitution(in _bodyId, value);
        }
    }

    /// <summary>Return or set continuous collision detection swept sphere radius.</summary>
    [Serialized("CCD Radius")]
    public float CcdRadius
    {
        get => _ccdRadius;
        set => _ccdRadius = MathF.Max(value, 0.0f);
    }

    /// <summary>Return or set continuous collision detection motion threshold. A positive value enables linear-cast motion quality.</summary>
    [Serialized("CCD Motion Threshold")]
    public float CcdMotionThreshold
    {
        get => _ccdMotionThreshold;
        set
        {
            value = MathF.Max(value, 0.0f);
            if (_ccdMotionThreshold == value)
                return;
            _ccdMotionThreshold = value;
            AddBodyToWorld();
        }
    }

    /// <summary>Return or set whether gravity is applied to the rigid body.</summary>
    [Serialized("Use Gravity")]
    public bool UseGravity
    {
        get => _useGravity;
        set
        {
            if (_useGravity == value)
                return;
            _useGravity = value;
            UpdateGravity();
        }
    }

    /// <summary>Return or set gravity override. If zero, use physics world gravity.</summary>
    [Serialized("Gravity Override")]
    public Vector3f GravityOverride
    {
        get => _gravityOverride;
        set
        {
            if (_gravityOverride == value)
                return;
            _gravityOverride = value;
            UpdateGravity();
        }
    }

    /// <summary>Return or set kinematic mode.</summary>
    [Serialized("Is Kinematic")]
    public bool IsKinematic
    {
        get => _kinematic;
        set
        {
            if (_kinematic == value)
                return;
            _kinematic = value;
            AddBodyToWorld();
        }
    }

    /// <summary>Return or set trigger mode.</summary>
    [Serialized("Is Trigger")]
    public bool IsTrigger
    {
        get => _trigger;
        set
        {
            if (_trigger == value)
                return;
            _trigger = value;
            if (TryGetBodyInterface(out var bodyInterface))
                bodyInterface.SetIsSensor(in _bodyId, value);
        }
    }

    /// <summary>Return or set collision layer.</summary>
    [Serialized("Collision Layer")]
    public uint CollisionLayer
    {
        get => _collisionLayer;
        set
        {
            if (_collisionLayer == value)
                return;
            _collisionLayer = value;
            UpdateObjectLayer();
        }
    }

    /// <summary>Return or set collision mask.</summary>
    [Serialized("Collision Mask")]
    public uint CollisionMask
    {
        get => _collisionMask;
        set
        {
            if (_collisionMask == value)
                return;
            _collisionMask = value;
            UpdateObjectLayer();
        }
    }

    /// <summary>Return or set the contact processing distance threshold.</summary>
    [Serialized("Contact Threshold")]
    public float ContactProcessingThreshold
    {
        get => _contactProcessingThreshold;
        set => _contactProcessingThreshold = value;
    }

    /// <summary>Set collision layer and mask.</summary>
    public void SetCollisionLayerAndMask(uint layer, uint mask)
    {
        if (_collisionLayer == layer && _collisionMask == mask)
            return;
        _collisionLayer = layer;
        _collisionMask = mask;
        UpdateObjectLayer();
    }

    /// <summary>Return or set collision event signaling mode.</summary>
    [Serialized("Collision Event Mode")]
    public CollisionEventMode CollisionEventMode
    {
        get => _collisionEventMode;
        set => _collisionEventMode = value;
    }

    /// <summary>Return whether the rigid body is active.</summary>
    public bool IsActive => TryGetBodyInterface(out var bodyInterface) && bodyInterface.IsActive(in _bodyId);

    /// <summary>Return center-of-mass offset reported by the active collision shape.</summary>
    public Vector3f CenterOfMass => _centerOfMass;

    /// <summary>Return physics world.</summary>
    public PhysicsWorld? PhysicsWorld => _physicsWorld is not null && _physicsWorld.TryGetTarget(out var world) ? world : null;

    /// <summary>Return native Jolt body ID, or null if the body has not been created.</summary>
    public uint? JoltBodyId => _bodyId == InvalidBodyId ? null : _bodyId;

    /// <summary>Return the native Jolt shape currently assigned to the body.</summary>
    public Shape? JoltShape => _compoundShape ?? GetActiveCollisionShape()?.JoltShape;

    /// <summary>Apply force to center of mass.</summary>
    public void ApplyForce(Vector3f force)
    {
        if (force != Vector3f.Zero && TryGetBodyInterface(out var bodyInterface))
            bodyInterface.AddForce(in _bodyId, force);
    }

    /// <summary>Apply force at local position.</summary>
    public void ApplyForce(Vector3f force, Vector3f position)
    {
        if (force == Vector3f.Zero || !TryGetBodyInterface(out var bodyInterface))
            return;
        bodyInterface.AddForce(in _bodyId, force, Position + Rotation * position);
    }

    /// <summary>Apply torque.</summary>
    public void ApplyTorque(Vector3f torque)
    {
        if (torque != Vector3f.Zero && TryGetBodyInterface(out var bodyInterface))
            bodyInterface.AddTorque(in _bodyId, torque);
    }

    /// <summary>Apply impulse to center of mass.</summary>
    public void ApplyImpulse(Vector3f impulse)
    {
        if (impulse != Vector3f.Zero && TryGetBodyInterface(out var bodyInterface))
            bodyInterface.AddImpulse(in _bodyId, impulse);
    }

    /// <summary>Apply impulse at local position.</summary>
    public void ApplyImpulse(Vector3f impulse, Vector3f position)
    {
        if (impulse == Vector3f.Zero || !TryGetBodyInterface(out var bodyInterface))
            return;
        bodyInterface.AddImpulse(in _bodyId, impulse, Position + Rotation * position);
    }

    /// <summary>Apply torque impulse.</summary>
    public void ApplyTorqueImpulse(Vector3f torque)
    {
        if (torque != Vector3f.Zero && TryGetBodyInterface(out var bodyInterface))
            bodyInterface.AddAngularImpulse(in _bodyId, torque);
    }

    /// <summary>Reset accumulated force and torque.</summary>
    public void ResetForces()
    {
        if (_bodyId != InvalidBodyId && _physicsWorld is not null && _physicsWorld.TryGetTarget(out var world))
        {
            using var bodyLock = world.World.GetBodyLockInterface();
            var mutex = bodyLock.LockWrite(in _bodyId);
            try
            {
                using var body = bodyLock.TryGetBody(in _bodyId);
                if (!body.IsNull)
                {
                    body.ResetForce();
                    body.ResetTorque();
                }
            }
            finally
            {
                bodyLock.UnlockWrite(mutex);
            }
        }
    }

    /// <summary>Activate rigid body if it was resting.</summary>
    public void Activate()
    {
        if (_mass > 0.0f && TryGetBodyInterface(out var bodyInterface))
            bodyInterface.ActivateBody(in _bodyId);
    }

    /// <summary>Readd rigid body to the physics world to reset internal contact state.</summary>
    public void ReAddBodyToWorld()
    {
        AddBodyToWorld();
    }

    /// <summary>Temporarily suppress body recreation when collision shapes are edited.</summary>
    public void DisableMassUpdate()
    {
        _enableMassUpdate = false;
    }

    /// <summary>Re-enable mass updates and recreate the body with the current shape.</summary>
    public void EnableMassUpdate()
    {
        if (!_enableMassUpdate)
        {
            _enableMassUpdate = true;
            AddBodyToWorld();
        }
    }

    /// <summary>Prepare interpolation state before the scene origin changes.</summary>
    public void PrepareToWorldOriginUpdate()
    {
        _hasSimulated = false;
    }

    /// <summary>Return colliding rigid bodies from the last simulation step.</summary>
    public void GetCollidingBodies(ICollection<RigidBody> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.Clear();
        PhysicsWorld?.GetCollidingBodies(result, this);
    }

    /// <summary>Release the rigid body.</summary>
    public void ReleaseBody()
    {
        if (_bodyId == InvalidBodyId || _physicsWorld is null || !_physicsWorld.TryGetTarget(out var world))
        {
            ReleaseCompoundShape();
            return;
        }

        var vehicles = _vehicles.ToArray();
        for (var i = 0; i < vehicles.Length; ++i)
            vehicles[i].ReleaseVehicleForBodyRecreation();

        var constraints = _constraints.ToArray();
        for (var i = 0; i < constraints.Length; ++i)
            constraints[i].ReleaseConstraintForBodyRecreation();

        var bodyInterface = world.BodyInterface;
        if (bodyInterface.IsAdded(in _bodyId))
            bodyInterface.RemoveBody(in _bodyId);
        bodyInterface.DestroyBody(in _bodyId);
        world.UnregisterBody(_bodyId);
        _bodyId = InvalidBodyId;
        ReleaseCompoundShape();
        _hasSimulated = false;
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        previousNode?.RemoveListener(this);
        currentNode?.AddListener(this);
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (scene is not null)
        {
            var physicsWorld = scene.GetOrCreateComponent<PhysicsWorld>();
            _physicsWorld = new WeakReference<PhysicsWorld>(physicsWorld);
            physicsWorld.AddRigidBody(this);
            AddBodyToWorld();
        }
        else
        {
            ReleaseBody();
            if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var physicsWorld))
                physicsWorld.RemoveRigidBody(this);
            _physicsWorld = null;
        }
    }

    protected override void OnMarkedDirty(Node node)
    {
        if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var world) && world.IsApplyingTransforms)
            return;
        var scene = Scene;
        if (scene is not null && scene.IsThreadedUpdate())
        {
            scene.DelayedMarkedDirty(this);
            return;
        }

        var newPosition = node.WorldPosition;
        var newRotation = node.WorldRotation;
        if (!newRotation.ApproximatelyEquivalent(_lastRotation))
        {
            _lastRotation = newRotation;
            SetTransform(Position, newRotation);
        }
        if (!newPosition.ApproximatelyEquivalent(_lastPosition))
        {
            _lastPosition = newPosition;
            SetTransform(newPosition, Rotation);
        }
    }

    internal void NotifyShapeChanged(CollisionShape shape)
    {
        if (_enableMassUpdate && ReferenceEquals(shape.TryGetNode(), _node))
            AddBodyToWorld();
    }

    public void AddConstraint(Constraint constraint)
    {
        if (!_constraints.Contains(constraint))
            _constraints.Add(constraint);
    }

    public void RemoveConstraint(Constraint constraint)
    {
        _constraints.Remove(constraint);
        Activate();
    }

    internal void AddVehicle(RaycastVehicle vehicle)
    {
        if (!_vehicles.Contains(vehicle))
            _vehicles.Add(vehicle);
    }

    internal void RemoveVehicle(RaycastVehicle vehicle)
    {
        _vehicles.Remove(vehicle);
    }

    internal void PrepareStep(float timeStep)
    {
        if (_kinematic && _kinematicTargetDirty && TryGetBodyInterface(out var bodyInterface))
        {
            bodyInterface.MoveKinematic(in _bodyId, _kinematicTargetPosition, _kinematicTargetRotation, timeStep);
            _kinematicTargetDirty = false;
        }

        if (_mass > 0.0f && _useGravity && _gravityOverride != Vector3f.Zero && TryGetBodyInterface(out bodyInterface) &&
            _physicsWorld is not null && _physicsWorld.TryGetTarget(out var world))
        {
            bodyInterface.AddForce(in _bodyId, (_gravityOverride - world.Gravity) * _mass);
        }

        _previousPosition = _currentPosition;
        _previousRotation = _currentRotation;
    }

    internal void CaptureSimulationTransform()
    {
        if (!TryGetBodyInterface(out var bodyInterface))
            return;
        _currentPosition = bodyInterface.GetPosition(in _bodyId);
        _currentRotation = bodyInterface.GetRotation(in _bodyId);
        _linearVelocity = bodyInterface.GetLinearVelocity(in _bodyId);
        _angularVelocity = bodyInterface.GetAngularVelocity(in _bodyId);
        _hasSimulated = true;
    }

    internal void ApplyWorldTransform(float interpolation)
    {
        if (_node is null || _mass == 0.0f || _kinematic)
            return;
        var position = _previousPosition.Lerp(_currentPosition, interpolation);
        var rotation = _previousRotation.Slerp(_currentRotation, interpolation);
        if (_physicsWorld is null || !_physicsWorld.TryGetTarget(out var world))
            return;
        var parent = _node.Parent;
        var parentRigidBody = parent is not null && parent != Scene ? parent.GetComponent<RigidBody>() : null;
        if (parentRigidBody is null)
            ApplyWorldTransform(position, rotation);
        else
        {
            var delayed = new DelayedWorldTransform
            {
                RigidBody = this,
                ParentRigidBody = parentRigidBody,
                WorldPosition = position,
                WorldRotation = rotation
            };
            world.AddDelayedWorldTransform(in delayed);
        }
    }

    /// <summary>Apply a new world transform after a simulation step.</summary>
    public void ApplyWorldTransform(Vector3f position, Quaternion rotation)
    {
        if (_node is null)
            return;
        if (_physicsWorld is null || !_physicsWorld.TryGetTarget(out var world))
            return;
        world.IsApplyingTransforms = true;
        _node.SetWorldPosition(position);
        _node.SetWorldRotation(rotation);
        _lastPosition = _node.WorldPosition;
        _lastRotation = _node.WorldRotation;
        world.IsApplyingTransforms = false;
    }

    /// <summary>Recreate the body to update mass and inertia from the current collision shape.</summary>
    public void UpdateMass()
    {
        if (_enableMassUpdate)
            AddBodyToWorld();
    }

    internal void OffsetWorldPosition(Vector3f delta)
    {
        if (!TryGetBodyInterface(out var bodyInterface))
            return;
        var position = bodyInterface.GetPosition(in _bodyId) - delta;
        bodyInterface.SetPosition(in _bodyId, position, EActivation.DontActivate);
        _previousPosition -= delta;
        _currentPosition -= delta;
        _kinematicTargetPosition -= delta;
        _hasSimulated = false;
    }

    private void AddBodyToWorld()
    {
        if (_physicsWorld is null || !_physicsWorld.TryGetTarget(out var world) || _node is null)
            return;

        var position = _bodyId != InvalidBodyId ? Position : _node.WorldPosition;
        var rotation = _bodyId != InvalidBodyId ? Rotation : _node.WorldRotation;
        var linearVelocity = _bodyId != InvalidBodyId ? LinearVelocity : _linearVelocity;
        var angularVelocity = _bodyId != InvalidBodyId ? AngularVelocity : _angularVelocity;
        ReleaseBody();

        var shape = CreateBodyShape();
        if (!IsEnabledEffective || shape is null)
        {
            _centerOfMass = Vector3f.Zero;
            return;
        }

        _centerOfMass = shape.GetCenterOfMass();

        using var settings = new BodyCreationSettings(shape, position, rotation, GetMotionType(), GetObjectLayer());
        settings.MLinearVelocity = linearVelocity;
        settings.MAngularVelocity = angularVelocity;
        settings.MAllowedDOFs = GetAllowedDofs();
        settings.MIsSensor = _trigger;
        settings.MFriction = _friction;
        settings.MRestitution = _restitution;
        settings.MLinearDamping = _linearDamping;
        settings.MAngularDamping = _angularDamping;
        settings.MGravityFactor = _useGravity && _gravityOverride == Vector3f.Zero ? 1.0f : 0.0f;
        settings.MMotionQuality = _ccdMotionThreshold > 0.0f ? EMotionQuality.LinearCast : EMotionQuality.Discrete;
        settings.MAllowDynamicOrKinematic = _mass > 0.0f || _kinematic;
        if (_mass > 0.0f)
        {
            settings.MOverrideMassProperties = EOverrideMassProperties.CalculateInertia;
            using var massProperties = settings.MMassPropertiesOverride;
            massProperties.MMass = _mass;
        }

        _bodyId = world.BodyInterface.CreateAndAddBody(settings, _mass > 0.0f ? EActivation.Activate : EActivation.DontActivate);
        if (_bodyId == InvalidBodyId)
            return;
        world.RegisterBody(_bodyId, this);
        _previousPosition = _currentPosition = position;
        _previousRotation = _currentRotation = rotation;
        _lastPosition = position;
        _lastRotation = rotation;
        _kinematicTargetPosition = position;
        _kinematicTargetRotation = rotation;
        _kinematicTargetDirty = false;
        _hasSimulated = false;
        world.NotifyBodyCreated(this);
        var vehicles = _vehicles.ToArray();
        for (var i = 0; i < vehicles.Length; ++i)
            vehicles[i].NotifyBodyCreated(this);
    }

    private CollisionShape? GetActiveCollisionShape()
    {
        if (_node is null)
            return null;
        var shapes = _node.GetComponents<CollisionShape>();
        for (var i = 0; i < shapes.Count; ++i)
        {
            if (shapes[i].IsEnabledEffective && shapes[i].JoltShape is not null)
                return shapes[i];
        }
        return null;
    }

    private Shape? CreateBodyShape()
    {
        if (_node is null)
            return null;
        var components = _node.GetComponents<CollisionShape>();
        var shapes = new List<Shape>(components.Count);
        for (var i = 0; i < components.Count; ++i)
        {
            if (components[i].IsEnabledEffective && components[i].JoltShape is { } shape)
                shapes.Add(shape);
        }
        if (shapes.Count == 0)
            return null;
        if (shapes.Count == 1)
            return shapes[0];

        _compoundShape = JoltShapeFactory.CreateCompound(shapes);
        return _compoundShape;
    }

    private void ReleaseCompoundShape()
    {
        _compoundShape?.Dispose();
        _compoundShape = null;
    }

    private EMotionType GetMotionType() => _kinematic ? EMotionType.Kinematic : _mass > 0.0f ? EMotionType.Dynamic : EMotionType.Static;

    private EAllowedDOFs GetAllowedDofs()
    {
        var result = EAllowedDOFs.None;
        if (_linearFactor.X != 0.0f)
            result |= EAllowedDOFs.TranslationX;
        if (_linearFactor.Y != 0.0f)
            result |= EAllowedDOFs.TranslationY;
        if (_linearFactor.Z != 0.0f)
            result |= EAllowedDOFs.TranslationZ;
        if (_angularFactor.X != 0.0f)
            result |= EAllowedDOFs.RotationX;
        if (_angularFactor.Y != 0.0f)
            result |= EAllowedDOFs.RotationY;
        if (_angularFactor.Z != 0.0f)
            result |= EAllowedDOFs.RotationZ;
        return result;
    }

    private uint GetObjectLayer() => ObjectLayerPairFilterMask.SGetObjectLayer(_collisionLayer & 0xffff, _collisionMask & 0xffff);

    private void UpdateObjectLayer()
    {
        if (TryGetBodyInterface(out var bodyInterface))
            bodyInterface.SetObjectLayer(in _bodyId, GetObjectLayer());
    }

    public void UpdateGravity()
    {
        if (TryGetBodyInterface(out var bodyInterface))
            bodyInterface.SetGravityFactor(in _bodyId, _useGravity && _gravityOverride == Vector3f.Zero ? 1.0f : 0.0f);
    }

    private bool TryGetBodyInterface(out BodyInterface bodyInterface)
    {
        if (_bodyId != InvalidBodyId && _physicsWorld is not null && _physicsWorld.TryGetTarget(out var world))
        {
            bodyInterface = world.BodyInterface;
            return true;
        }
        bodyInterface = null!;
        return false;
    }
}
