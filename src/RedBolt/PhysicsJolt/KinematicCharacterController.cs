// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.PhysicsJolt;

using Color = RedBolt.Math.Color;
using RedBolt.Bindings.Jolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using Sphere = RedBolt.Math.Sphere;

/// <summary>Kinematic capsule character driven by Jolt's native Character controller.</summary>
[Reflected(ObjectCategory.Category_Physics, InheritAttributes = false)]
public partial class KinematicCharacterController : Component
{
    private readonly HashSet<uint> _activeTriggerBodies = [];
    private WeakReference<PhysicsWorld>? _physicsWorld;
    private Character? _character;
    private uint _collisionLayer = 1;
    private uint _collisionMask = 0xffff;
    private Vector3f _gravity = new(0.0f, -14.0f, 0.0f);
    private float _linearDamping = 0.2f;
    private float _angularDamping = 0.2f;
    private float _height = 1.8f;
    private float _diameter = 0.7f;
    private Vector3f _offset = new(0.0f, 0.9f, 0.0f);
    private float _stepHeight = 0.4f;
    private float _maxJumpHeight = 2.0f;
    private float _fallSpeed = 55.0f;
    private float _jumpSpeed = 9.0f;
    private float _maxSlope = 45.0f;
    private bool _trigger = true;
    private bool _activateTriggers = true;
    private Vector3f _walkIncrement;
    private Vector3f _lastWalkVelocity;
    private Vector3f _angularVelocity;
    private Vector3f _positionOffset;
    private float _smoothingConstant;
    private Vector3f _previousPosition;
    private Vector3f _nextPosition;
    private Vector3f _latestPosition;
    private bool _readdToWorld;

    /// <summary>Construct.</summary>
    public KinematicCharacterController()
    {
    }

    public override void Dispose()
    {
        ReleaseCharacter();
        if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var physicsWorld))
            physicsWorld.RemoveKinematicCharacter(this);
        _node?.RemoveListener(this);
        base.Dispose();
    }

    public override void OnSetAttribute(AttributeInfo attribute, Variant value)
    {
        base.OnSetAttribute(attribute, value);
        _readdToWorld = true;
    }

    public override void ApplyAttributes()
    {
        if (_readdToWorld || IsEnabledEffective && _character is null)
            RecreateCharacter();
        _readdToWorld = false;
    }

    public override void OnSetEnabled()
    {
        if (IsEnabledEffective)
            RecreateCharacter();
        else
            ReleaseCharacter();
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || _node is null || !IsEnabledEffective)
            return;
        var center = RawPosition + _offset;
        var radius = _diameter * 0.5f;
        var cylinderHeight = MathF.Max(_height - _diameter, 0.0f);
        debug.AddCylinder(center, radius, cylinderHeight, Color.Green, depthTest);
        debug.AddSphere(new Sphere(center + Vector3f.Up * (cylinderHeight * 0.5f), radius), Color.Green, depthTest);
        debug.AddSphere(new Sphere(center - Vector3f.Up * (cylinderHeight * 0.5f), radius), Color.Green, depthTest);
    }

    /// <summary>Draw debug geometry using the scene debug renderer.</summary>
    public virtual void DrawDebugGeometry() =>
        DrawDebugGeometry(Scene?.GetComponent<DebugRenderer>(), true);

    [Serialized("Gravity")]
    public Vector3f Gravity
    {
        get => _gravity;
        set => _gravity = value;
    }

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

    [Serialized("Linear Damping")]
    public float LinearDamping
    {
        get => _linearDamping;
        set => _linearDamping = System.Math.Clamp(value, 0.0f, 1.0f);
    }

    [Serialized("Angular Damping")]
    public float AngularDamping
    {
        get => _angularDamping;
        set => _angularDamping = System.Math.Clamp(value, 0.0f, 1.0f);
    }

    [Serialized("Height")]
    public float Height
    {
        get => _height;
        set
        {
            value = MathF.Max(value, 0.001f);
            if (_height == value)
                return;
            _height = value;
            RecreateCharacter();
        }
    }

    [Serialized("Diameter")]
    public float Diameter
    {
        get => _diameter;
        set
        {
            value = MathF.Max(value, 0.001f);
            if (_diameter == value)
                return;
            _diameter = value;
            RecreateCharacter();
        }
    }

    [Serialized("Offset")]
    public Vector3f Offset
    {
        get => _offset;
        set
        {
            if (_offset == value)
                return;
            _offset = value;
            RecreateCharacter();
        }
    }

    [Serialized("Step Height")]
    public float StepHeight
    {
        get => _stepHeight;
        set => _stepHeight = MathF.Max(value, 0.0f);
    }

    [Serialized("Max Jump Height")]
    public float MaxJumpHeight
    {
        get => _maxJumpHeight;
        set => _maxJumpHeight = MathF.Max(value, 0.0f);
    }

    [Serialized("Fall Speed")]
    public float FallSpeed
    {
        get => _fallSpeed;
        set => _fallSpeed = MathF.Max(value, 0.0f);
    }

    [Serialized("Jump Speed")]
    public float JumpSpeed
    {
        get => _jumpSpeed;
        set => _jumpSpeed = MathF.Max(value, 0.0f);
    }

    [Serialized("Max Slope")]
    public float MaxSlope
    {
        get => _maxSlope;
        set
        {
            _maxSlope = value;
            _character?.SetMaxSlopeAngle(value * MathDefs.DegToRad);
        }
    }

    [Serialized("Is Trigger")]
    public bool IsTrigger
    {
        get => _trigger;
        set
        {
            if (_trigger == value)
                return;
            _trigger = value;
        }
    }

    [Serialized("Activate Triggers")]
    public bool ActivateTriggers
    {
        get => _activateTriggers;
        set
        {
            _activateTriggers = value;
            if (!value)
                _activeTriggerBodies.Clear();
        }
    }

    public Vector3f RawPosition => _character?.GetPosition() ?? _node?.WorldPosition ?? _latestPosition;
    public Vector3f LinearVelocity
    {
        get => _character?.GetLinearVelocity() ?? Vector3f.Zero;
        set => _character?.SetLinearVelocity(value);
    }
    public Vector3f AngularVelocity
    {
        get => _angularVelocity;
        set => _angularVelocity = value;
    }
    public bool OnGround => _character?.GetGroundState() == JPH.CharacterBase.EGroundState.OnGround;
    public bool CanJump => OnGround;

    public void SetCollisionLayerAndMask(uint layer, uint mask)
    {
        _collisionLayer = layer;
        _collisionMask = mask;
        UpdateObjectLayer();
    }

    public void SetWalkIncrement(Vector3f walkIncrement) => _walkIncrement = walkIncrement;

    public void Jump(Vector3f jump = default)
    {
        if (_character is null || !CanJump)
            return;
        var impulse = jump == Vector3f.Zero ? Vector3f.Up * _jumpSpeed : jump;
        _character.SetLinearVelocity(_character.GetLinearVelocity() + impulse);
    }

    public void ApplyImpulse(Vector3f impulse) => _character?.AddImpulse(impulse);

    public void AdjustRawPosition(Vector3f offset, float smoothConstant)
    {
        _smoothingConstant = smoothConstant;
        _positionOffset -= offset;
        WarpKinematic(RawPosition + offset);
        _node?.SetWorldPosition(_latestPosition);
    }

    /// <summary>Instantly reset character position to a new value.</summary>
    protected void WarpKinematic(Vector3f position)
    {
        _latestPosition = position + _positionOffset;
        _previousPosition = position;
        _nextPosition = position;
        _character?.SetPosition(position);
    }

    internal void PrepareStep(float timeStep)
    {
        if (_character is null || timeStep <= 0.0f)
            return;

        var velocity = _character.GetLinearVelocity();
        velocity -= _lastWalkVelocity;
        var damping = MathF.Pow(1.0f - _linearDamping, timeStep);
        velocity *= damping;
        var verticalSpeed = velocity.DotProduct(Vector3f.Up);
        if (OnGround && verticalSpeed < 0.0f)
            verticalSpeed = 0.0f;
        var walkVelocity = _walkIncrement / timeStep;
        walkVelocity -= Vector3f.Up * walkVelocity.DotProduct(Vector3f.Up);
        velocity -= Vector3f.Up * velocity.DotProduct(Vector3f.Up);
        if (_walkIncrement != Vector3f.Zero || _lastWalkVelocity != Vector3f.Zero)
            velocity = Vector3f.Zero;
        velocity += walkVelocity + Vector3f.Up * verticalSpeed + _gravity * timeStep;
        var fallSpeed = -velocity.DotProduct(Vector3f.Up);
        if (fallSpeed > _fallSpeed)
            velocity += Vector3f.Up * (fallSpeed - _fallSpeed);
        _lastWalkVelocity = walkVelocity;
        _angularVelocity *= MathF.Pow(1.0f - _angularDamping, timeStep);
        _character.SetLinearAndAngularVelocity(velocity, _angularVelocity);
    }

    internal void CaptureSimulationTransform()
    {
        if (_character is null)
            return;
        _character.PostSimulation(0.05f);
        _previousPosition = _nextPosition;
        _nextPosition = RawPosition;
    }

    internal void ReleaseCharacter()
    {
        if (_character is null)
            return;
        var bodyId = _character.GetBodyID();
        if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var world))
            world.UnregisterCharacter(bodyId);
        _character.RemoveFromPhysicsSystem();
        _character.Dispose();
        _character = null;
        _activeTriggerBodies.Clear();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        previousNode?.RemoveListener(this);
        currentNode?.AddListener(this);
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (previousScene is not null)
        {
            UnsubscribeFromEvent<PhysicsEvents.PhysicsPreUpdateEvent>();
            UnsubscribeFromEvent<PhysicsEvents.PhysicsPostUpdateEvent>();
            ReleaseCharacter();
            if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var previousWorld))
                previousWorld.RemoveKinematicCharacter(this);
        }

        if (scene is null)
        {
            _physicsWorld = null;
            return;
        }

        var physicsWorld = scene.GetOrCreateComponent<PhysicsWorld>();
        _physicsWorld = new WeakReference<PhysicsWorld>(physicsWorld);
        physicsWorld.AddKinematicCharacter(this);
        SubscribeToEvent<PhysicsEvents.PhysicsPreUpdateEvent>(physicsWorld, HandlePhysicsPreUpdate);
        SubscribeToEvent<PhysicsEvents.PhysicsPostUpdateEvent>(physicsWorld, HandlePhysicsPostUpdate);
        RecreateCharacter();
    }

    private void RecreateCharacter()
    {
        if (!IsEnabledEffective || _node is null || _physicsWorld is null || !_physicsWorld.TryGetTarget(out var world))
            return;

        var position = _character?.GetPosition() ?? _node.WorldPosition;
        var velocity = _character?.GetLinearVelocity() ?? Vector3f.Zero;
        ReleaseCharacter();

        var radius = MathF.Max(_diameter * 0.5f, 0.001f);
        var halfCylinder = MathF.Max(_height * 0.5f - radius, 0.001f);
        using var capsule = new CapsuleShape(halfCylinder, radius);
        using var shape = new RotatedTranslatedShape(_offset, Quaternion.IDENTITY, capsule);
        using var settings = new CharacterSettings
        {
            MAllowedDOFs = EAllowedDOFs.TranslationX | EAllowedDOFs.TranslationY | EAllowedDOFs.TranslationZ,
            MFriction = 0.0f,
            MGravityFactor = 0.0f,
            MLayer = ObjectLayerPairFilterMask.SGetObjectLayer(_collisionLayer & 0xffff, _collisionMask & 0xffff),
            MMaxSlopeAngle = _maxSlope * MathDefs.DegToRad,
            MUp = Vector3f.Up
        };
        settings.MShape = shape;
        _character = new Character(settings, position, Quaternion.IDENTITY, 0, world.World);
        _character.AddToPhysicsSystem();
        world.RegisterCharacter(_character.GetBodyID(), this);
        _character.SetLinearVelocity(velocity);
        _lastWalkVelocity = Vector3f.Zero;
        _previousPosition = _nextPosition = _latestPosition = position;
    }

    private void UpdateObjectLayer()
    {
        _character?.SetLayer(ObjectLayerPairFilterMask.SGetObjectLayer(_collisionLayer & 0xffff, _collisionMask & 0xffff));
    }

    private void HandlePhysicsPreUpdate(ref PhysicsEvents.PhysicsPreUpdateEvent eventData)
    {
        if (_node is not null && !_node.WorldPosition.ApproximatelyEquivalent(_latestPosition, MathDefs.LargeEpsilon))
            WarpKinematic(_node.WorldPosition);
    }

    private void HandlePhysicsPostUpdate(ref PhysicsEvents.PhysicsPostUpdateEvent eventData)
    {
        if (_node is null || _physicsWorld is null || !_physicsWorld.TryGetTarget(out var world))
            return;
        if (world.Interpolation)
        {
            var factor = MathDefs.InverseExponentialDecay(_smoothingConstant * eventData.TimeStep);
            _positionOffset = _positionOffset.Lerp(Vector3f.Zero, factor);
            var interpolation = System.Math.Clamp(eventData.Overtime * world.Fps, 0.0f, 1.0f);
            _latestPosition = _previousPosition.Lerp(_nextPosition, interpolation) + _positionOffset;
        }
        else
            _latestPosition = _nextPosition;
        world.IsApplyingTransforms = true;
        _node.SetWorldPosition(_latestPosition);
        world.IsApplyingTransforms = false;
    }

    internal void SendTriggerCollisionEvent(
        PhysicsWorld world, RigidBody otherBody, bool isNew, bool isEnd)
    {
        var node = TryGetNode();
        var otherNode = otherBody.TryGetNode();
        if (node is null || otherNode is null)
            return;

        if (isEnd)
        {
            var physicsEnd = new PhysicsEvents.PhysicsCollisionEndEvent
            {
                World = world, NodeA = node, NodeB = otherNode, BodyA = null, BodyB = otherBody, Trigger = true
            };
            SendEvent(ref physicsEnd);
            var nodeEnd = new PhysicsEvents.NodeCollisionEndEvent
            {
                Body = null, OtherNode = otherNode, OtherBody = otherBody, Trigger = true
            };
            node.SendEvent(ref nodeEnd);
            var otherNodeEnd = new PhysicsEvents.NodeCollisionEndEvent
            {
                Body = otherBody, OtherNode = node, OtherBody = null, Trigger = true
            };
            otherNode.SendEvent(ref otherNodeEnd);
            return;
        }

        var contacts = Array.Empty<PhysicsContact>();
        if (isNew)
        {
            var physicsStart = new PhysicsEvents.PhysicsCollisionStartEvent
            {
                World = world, NodeA = node, NodeB = otherNode, BodyA = null, BodyB = otherBody,
                Trigger = true, Contacts = contacts
            };
            SendEvent(ref physicsStart);
            var nodeStart = new PhysicsEvents.NodeCollisionStartEvent
            {
                Body = null, OtherNode = otherNode, OtherBody = otherBody, Trigger = true, Contacts = contacts
            };
            node.SendEvent(ref nodeStart);
            var otherNodeStart = new PhysicsEvents.NodeCollisionStartEvent
            {
                Body = otherBody, OtherNode = node, OtherBody = null, Trigger = true, Contacts = contacts
            };
            otherNode.SendEvent(ref otherNodeStart);
            return;
        }

        var physicsOngoing = new PhysicsEvents.PhysicsCollisionEvent
        {
            World = world, NodeA = node, NodeB = otherNode, BodyA = null, BodyB = otherBody,
            Trigger = true, Contacts = contacts
        };
        SendEvent(ref physicsOngoing);
        var nodeOngoing = new PhysicsEvents.NodeCollisionEvent
        {
            Body = null, OtherNode = otherNode, OtherBody = otherBody, Trigger = true, Contacts = contacts
        };
        node.SendEvent(ref nodeOngoing);
        var otherNodeOngoing = new PhysicsEvents.NodeCollisionEvent
        {
            Body = otherBody, OtherNode = node, OtherBody = null, Trigger = true, Contacts = contacts
        };
        otherNode.SendEvent(ref otherNodeOngoing);
    }

    internal bool BeginTriggerContact(uint bodyId) => _activeTriggerBodies.Add(bodyId);

    internal bool EndTriggerContact(uint bodyId) => _activeTriggerBodies.Remove(bodyId);
}
