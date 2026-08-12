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

using SystemMath = global::System.Math;

using RedBolt.Bindings.Jolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Physics simulation world component.</summary>
[Reflected(ObjectCategory.Category_Subsystem, InheritAttributes = false)]
public partial class PhysicsWorld : Component, IFixedUpdateSource
{
    private const int DefaultFps = 60;
    private const uint MaxBodies = 65_536;
    private const uint MaxBodyPairs = 65_536;
    private const uint MaxContactConstraints = 10_240;
    private const uint MaxPhysicsJobs = 2_048;
    private const uint MaxPhysicsBarriers = 8;
    private const nuint TempAllocatorSize = 10 * 1024 * 1024;

    private static readonly object _joltSync = new();
    private static Factory? _joltFactory;
    private static int _joltReferenceCount;

    private readonly object _contactLock = new();
    private readonly List<WeakReference<KinematicCharacterController>> _kinematicCharacters = [];
    private readonly List<WeakReference<RigidBody>> _rigidBodies = [];
    private readonly List<WeakReference<CollisionShape>> _collisionShapes = [];
    private readonly List<WeakReference<Constraint>> _constraints = [];
    private readonly Dictionary<uint, WeakReference<RigidBody>> _bodiesById = [];
    private readonly Dictionary<BodyPair, int> _disabledCollisionPairs = [];
    private readonly Dictionary<BodyPair, ContactData> _currentContacts = [];
    private readonly Dictionary<BodyPair, ContactData> _previousContacts = [];
    private readonly BroadPhaseLayerInterfaceMask _broadPhaseLayerInterface;
    private readonly PhysicsObjectLayerPairFilter _objectLayerPairFilter;
    private readonly ObjectVsBroadPhaseLayerFilterMask _objectVsBroadPhaseLayerFilter;
    private readonly TempAllocatorImpl _tempAllocator;
    private readonly JobSystemThreadPool _jobSystem;
    private readonly PhysicsContactListener _contactListener;
    private readonly BroadPhaseLayerFilter _raycastBroadPhaseLayerFilter;
    private readonly BodyInterface _bodyInterface;
    private readonly NarrowPhaseQuery _narrowPhaseQuery;
    private Vector3f _gravity = new(0.0f, -9.81f, 0.0f);
    private int _fps = DefaultFps;
    private int _maxSubSteps;
    private int _numIterations = 10;
    private bool _interpolation = true;
    private bool _manualUpdate;
    private bool _disposed;
    private float _timeAccumulator;
    private SynchronizedStep? _synchronizedStep;

    /// <summary>Construct.</summary>
    public PhysicsWorld()
    {
        AcquireJolt();
        try
        {
            World = new PhysicsSystem();
            _broadPhaseLayerInterface = new BroadPhaseLayerInterfaceMask(1);
            _broadPhaseLayerInterface.ConfigureLayer(0, 0xffff, 0);
            _objectLayerPairFilter = new PhysicsObjectLayerPairFilter();
            _objectVsBroadPhaseLayerFilter = new ObjectVsBroadPhaseLayerFilterMask(_broadPhaseLayerInterface);
            _tempAllocator = new TempAllocatorImpl(TempAllocatorSize);
            _jobSystem = new JobSystemThreadPool(
                MaxPhysicsJobs, MaxPhysicsBarriers, SystemMath.Max(Environment.ProcessorCount - 1, 0));
            _contactListener = new PhysicsContactListener(this);
            _raycastBroadPhaseLayerFilter = new BroadPhaseLayerFilter();

            World.Init(MaxBodies, 0, MaxBodyPairs, MaxContactConstraints,
                _broadPhaseLayerInterface, _objectVsBroadPhaseLayerFilter, _objectLayerPairFilter);
            World.SetGravity(_gravity);
            World.SetContactListener(_contactListener);
            _bodyInterface = World.GetBodyInterface();
            _narrowPhaseQuery = World.GetNarrowPhaseQuery();
            NumIterations = _numIterations;
        }
        catch
        {
            ReleaseJolt();
            throw;
        }
    }

    /// <summary>Destruct. Release all native simulation resources.</summary>
    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        for (var i = 0; i < _constraints.Count; ++i)
        {
            if (_constraints[i].TryGetTarget(out var constraint))
                constraint.ReleaseConstraint();
        }
        for (var i = 0; i < _rigidBodies.Count; ++i)
        {
            if (_rigidBodies[i].TryGetTarget(out var rigidBody))
                rigidBody.ReleaseBody();
        }
        for (var i = 0; i < _kinematicCharacters.Count; ++i)
        {
            if (_kinematicCharacters[i].TryGetTarget(out var character))
                character.ReleaseCharacter();
        }
        _bodiesById.Clear();
        World.SetContactListener(null!);
        _raycastBroadPhaseLayerFilter.Dispose();
        _narrowPhaseQuery.Dispose();
        _bodyInterface.Dispose();
        World.Dispose();
        _contactListener.Dispose();
        _jobSystem.Dispose();
        _tempAllocator.Dispose();
        _objectVsBroadPhaseLayerFilter.Dispose();
        _objectLayerPairFilter.Dispose();
        _broadPhaseLayerInterface.Dispose();
        ReleaseJolt();
        base.Dispose();
    }

    /// <summary>Visualize all collision shapes as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null)
            return;
        for (var i = _collisionShapes.Count - 1; i >= 0; --i)
        {
            if (_collisionShapes[i].TryGetTarget(out var shape))
                shape.DrawDebugGeometry(debug, depthTest);
            else
                _collisionShapes.RemoveAt(i);
        }
    }

    /// <summary>Return or set gravity.</summary>
    [Serialized("Gravity")]
    public Vector3f Gravity
    {
        get => _gravity;
        set
        {
            _gravity = value;
            World.SetGravity(value);
        }
    }

    /// <summary>Return or set physics simulation frequency.</summary>
    [Serialized("Physics FPS")]
    public int Fps
    {
        get => _fps;
        set => _fps = SystemMath.Clamp(value, 1, 1000);
    }

    /// <summary>Return or set maximum simulation substeps. A negative value uses one variable step.</summary>
    [Serialized("Max Substeps")]
    public int MaxSubSteps
    {
        get => _maxSubSteps;
        set => _maxSubSteps = value;
    }

    /// <summary>Return or set solver velocity iterations.</summary>
    [Serialized("Solver Iterations")]
    public int NumIterations
    {
        get => _numIterations;
        set
        {
            _numIterations = SystemMath.Clamp(value, 1, 256);
            using var settings = World.GetPhysicsSettings();
            settings.MNumVelocitySteps = checked((uint)_numIterations);
        }
    }

    /// <summary>Return or set transform interpolation.</summary>
    [Serialized("Interpolation")]
    public bool Interpolation
    {
        get => _interpolation;
        set => _interpolation = value;
    }

    /// <summary>Return or set manual update mode.</summary>
    public bool ManualUpdate
    {
        get => _manualUpdate;
        set => _manualUpdate = value;
    }

    /// <summary>Return whether the world is simulating.</summary>
    public bool IsSimulating { get; private set; }

    /// <summary>Return whether scene transforms are being applied from simulation.</summary>
    public bool IsApplyingTransforms { get; internal set; }

    /// <summary>Return the last Jolt update status.</summary>
    public EPhysicsUpdateError LastUpdateError { get; private set; }

    /// <summary>Return the native Jolt physics system.</summary>
    public PhysicsSystem World { get; }

    /// <summary>Return the native Jolt body interface.</summary>
    public BodyInterface BodyInterface => _bodyInterface;

    /// <summary>Return the native Jolt narrow-phase query interface.</summary>
    public NarrowPhaseQuery NarrowPhaseQuery => _narrowPhaseQuery;

    /// <summary>Return the native Jolt temporary allocator.</summary>
    public TempAllocatorImpl TempAllocator => _tempAllocator;

    /// <summary>Return the native Jolt job system.</summary>
    public JobSystemThreadPool JobSystem => _jobSystem;

    /// <summary>Update physics simulation.</summary>
    public void Update(float timeStep)
    {
        timeStep = MathF.Max(timeStep, 0.0f);
        var preUpdate = new PhysicsEvents.PhysicsPreUpdateEvent { World = this, TimeStep = timeStep };
        SendEvent(ref preUpdate);
        IsSimulating = true;
        try
        {
            if (_maxSubSteps < 0)
            {
                if (timeStep > 0.0f)
                    SimulateStep(timeStep);
                ApplyWorldTransforms(1.0f);
            }
            else
            {
                var fixedTimeStep = 1.0f / _fps;
                _timeAccumulator += timeStep;
                var maxSteps = _maxSubSteps > 0 ? _maxSubSteps : (int)(_timeAccumulator / fixedTimeStep) + 1;
                var numSteps = 0;
                while (_timeAccumulator >= fixedTimeStep && numSteps < maxSteps)
                {
                    SimulateStep(fixedTimeStep);
                    _timeAccumulator -= fixedTimeStep;
                    ++numSteps;
                }
                if (numSteps == maxSteps && _timeAccumulator >= fixedTimeStep)
                    _timeAccumulator %= fixedTimeStep;
                ApplyWorldTransforms(_interpolation ? _timeAccumulator / fixedTimeStep : 1.0f);
            }
        }
        finally
        {
            IsSimulating = false;
        }
        var postUpdate = new PhysicsEvents.PhysicsPostUpdateEvent { World = this, TimeStep = timeStep };
        SendEvent(ref postUpdate);
    }

    /// <summary>Update physics with an explicit number of fixed steps and overtime.</summary>
    public void CustomUpdate(uint numSteps, float fixedTimeStep, float overtime,
        long? networkFrame = null, uint synchronizedStepOffset = 0)
    {
        var timeStep = numSteps * fixedTimeStep + overtime;
        var preUpdate = new PhysicsEvents.PhysicsPreUpdateEvent { World = this, TimeStep = timeStep };
        SendEvent(ref preUpdate);
        IsSimulating = true;
        try
        {
            _synchronizedStep = networkFrame is not null
                ? new SynchronizedStep(networkFrame.Value, synchronizedStepOffset) : null;
            for (var i = 0u; i < numSteps; ++i)
                SimulateStep(fixedTimeStep);
            _timeAccumulator = MathF.Max(overtime, 0.0f);
            ApplyWorldTransforms(_interpolation && fixedTimeStep > 0.0f ? SystemMath.Clamp(overtime / fixedTimeStep, 0.0f, 1.0f) : 1.0f);
        }
        finally
        {
            IsSimulating = false;
        }
        var postUpdate = new PhysicsEvents.PhysicsPostUpdateEvent { World = this, TimeStep = timeStep };
        SendEvent(ref postUpdate);
    }

    /// <summary>Raycast against the world and return the closest hit.</summary>
    public bool TryRaycastSingle(Ray ray, float maxDistance, out PhysicsRaycastResult result, uint collisionMask = uint.MaxValue)
    {
        result = default;
        if (maxDistance <= 0.0f)
            return false;

        var direction = ray.Direction * maxDistance;
        var nativeRay = ray;
        nativeRay.Direction = direction;
        var hit = new JoltRayCastResult { BodyId = uint.MaxValue, Fraction = 1.0f };
        using var filter = new PhysicsObjectLayerFilter(collisionMask);
        if (!_narrowPhaseQuery.CastRay(in nativeRay, ref hit, _raycastBroadPhaseLayerFilter, filter))
            return false;

        var position = ray.Origin + direction * hit.Fraction;
        var normal = Vector3f.Zero;
        using (var transformedShape = _bodyInterface.GetTransformedShape(in hit.BodyId))
            normal = transformedShape.GetWorldSpaceSurfaceNormal(in hit.SubShapeId, position);

        result = new PhysicsRaycastResult
        {
            Body = GetBody(hit.BodyId),
            Position = position,
            Normal = normal,
            Distance = maxDistance * hit.Fraction,
            HitFraction = hit.Fraction
        };
        return true;
    }

    /// <summary>Raycast against the world and write the closest hit.</summary>
    public void RaycastSingle(ref PhysicsRaycastResult result, Ray ray, float maxDistance, uint collisionMask = uint.MaxValue)
    {
        TryRaycastSingle(ray, maxDistance, out result, collisionMask);
    }

    /// <summary>Return colliding bodies from the last simulation step.</summary>
    public void GetCollidingBodies(ICollection<RigidBody> result, RigidBody body)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(body);
        result.Clear();
        var bodyId = body.JoltBodyId;
        if (bodyId is null)
            return;

        foreach (var pair in _previousContacts.Keys)
        {
            if (pair.Body1 != bodyId && pair.Body2 != bodyId)
                continue;
            var other = GetBody(pair.Body1 == bodyId ? pair.Body2 : pair.Body1);
            if (other is not null && ShouldSignal(body, other))
                result.Add(other);
        }
    }

    internal void AddRigidBody(RigidBody body)
    {
        for (var i = 0; i < _rigidBodies.Count; ++i)
        {
            if (_rigidBodies[i].TryGetTarget(out var existing) && ReferenceEquals(existing, body))
                return;
        }
        _rigidBodies.Add(new WeakReference<RigidBody>(body));
    }

    internal void AddKinematicCharacter(KinematicCharacterController character)
    {
        for (var i = 0; i < _kinematicCharacters.Count; ++i)
        {
            if (_kinematicCharacters[i].TryGetTarget(out var existing) && ReferenceEquals(existing, character))
                return;
        }
        _kinematicCharacters.Add(new WeakReference<KinematicCharacterController>(character));
    }

    internal void RemoveKinematicCharacter(KinematicCharacterController character)
    {
        for (var i = _kinematicCharacters.Count - 1; i >= 0; --i)
        {
            if (!_kinematicCharacters[i].TryGetTarget(out var existing) || ReferenceEquals(existing, character))
                _kinematicCharacters.RemoveAt(i);
        }
    }

    internal void RemoveRigidBody(RigidBody body)
    {
        for (var i = _rigidBodies.Count - 1; i >= 0; --i)
        {
            if (!_rigidBodies[i].TryGetTarget(out var existing) || ReferenceEquals(existing, body))
                _rigidBodies.RemoveAt(i);
        }
    }

    internal void AddCollisionShape(CollisionShape shape)
    {
        for (var i = 0; i < _collisionShapes.Count; ++i)
        {
            if (_collisionShapes[i].TryGetTarget(out var existing) && ReferenceEquals(existing, shape))
                return;
        }
        _collisionShapes.Add(new WeakReference<CollisionShape>(shape));
    }

    internal void RemoveCollisionShape(CollisionShape shape)
    {
        for (var i = _collisionShapes.Count - 1; i >= 0; --i)
        {
            if (!_collisionShapes[i].TryGetTarget(out var existing) || ReferenceEquals(existing, shape))
                _collisionShapes.RemoveAt(i);
        }
    }

    internal void AddConstraint(Constraint constraint)
    {
        for (var i = 0; i < _constraints.Count; ++i)
        {
            if (_constraints[i].TryGetTarget(out var existing) && ReferenceEquals(existing, constraint))
                return;
        }
        _constraints.Add(new WeakReference<Constraint>(constraint));
    }

    internal void RemoveConstraint(Constraint constraint)
    {
        for (var i = _constraints.Count - 1; i >= 0; --i)
        {
            if (!_constraints[i].TryGetTarget(out var existing) || ReferenceEquals(existing, constraint))
                _constraints.RemoveAt(i);
        }
    }

    internal void NotifyBodyCreated(RigidBody body)
    {
        for (var i = _constraints.Count - 1; i >= 0; --i)
        {
            if (_constraints[i].TryGetTarget(out var constraint))
                constraint.NotifyBodyCreated(body);
            else
                _constraints.RemoveAt(i);
        }
    }

    internal void DisableCollision(uint body1, uint body2)
    {
        lock (_contactLock)
        {
            var pair = new BodyPair(body1, body2);
            _disabledCollisionPairs[pair] = _disabledCollisionPairs.GetValueOrDefault(pair) + 1;
        }
    }

    internal void EnableCollision(uint body1, uint body2)
    {
        lock (_contactLock)
        {
            var pair = new BodyPair(body1, body2);
            if (!_disabledCollisionPairs.TryGetValue(pair, out var count) || count <= 1)
                _disabledCollisionPairs.Remove(pair);
            else
                _disabledCollisionPairs[pair] = count - 1;
        }
    }

    internal void RegisterBody(uint bodyId, RigidBody body)
    {
        _bodiesById[bodyId] = new WeakReference<RigidBody>(body);
    }

    internal void UnregisterBody(uint bodyId)
    {
        _bodiesById.Remove(bodyId);
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (previousScene is not null)
        {
            UnsubscribeFromEvent<SceneEvents.SceneSubsystemUpdateEvent>();
            UnsubscribeFromEvent<SceneEvents.WorldOriginUpdateEvent>();
        }
        if (scene is not null)
        {
            SubscribeToEvent<SceneEvents.SceneSubsystemUpdateEvent>(scene, HandleSceneSubsystemUpdate);
            SubscribeToEvent<SceneEvents.WorldOriginUpdateEvent>(scene, HandleWorldOriginUpdate);
        }
    }

    private void HandleSceneSubsystemUpdate(ref SceneEvents.SceneSubsystemUpdateEvent eventData)
    {
        if (!_manualUpdate && IsEnabledEffective)
            Update(eventData.TimeStep);
    }

    private void HandleWorldOriginUpdate(ref SceneEvents.WorldOriginUpdateEvent eventData)
    {
        var delta = new Vector3f(eventData.Delta.X, eventData.Delta.Y, eventData.Delta.Z);
        for (var i = _rigidBodies.Count - 1; i >= 0; --i)
        {
            if (_rigidBodies[i].TryGetTarget(out var body))
                body.OffsetWorldPosition(delta);
            else
                _rigidBodies.RemoveAt(i);
        }
    }

    private void SimulateStep(float timeStep)
    {
        lock (_contactLock)
            _currentContacts.Clear();

        for (var i = _rigidBodies.Count - 1; i >= 0; --i)
        {
            if (_rigidBodies[i].TryGetTarget(out var body))
                body.PrepareStep(timeStep);
            else
                _rigidBodies.RemoveAt(i);
        }

        var preStep = new PhysicsEvents.PhysicsPreStepEvent
        {
            World = this,
            TimeStep = timeStep,
            NetworkFrame = _synchronizedStep is { Offset: 0 } synchronized ? synchronized.NetworkFrame : null
        };
        SendEvent(ref preStep);
        if (_synchronizedStep is { } synchronizedStep)
        {
            _synchronizedStep = synchronizedStep.Offset == 0
                ? null
                : synchronizedStep with { Offset = synchronizedStep.Offset - 1 };
        }
        for (var i = _kinematicCharacters.Count - 1; i >= 0; --i)
        {
            if (_kinematicCharacters[i].TryGetTarget(out var character))
                character.PrepareStep(timeStep);
            else
                _kinematicCharacters.RemoveAt(i);
        }
        LastUpdateError = World.Update(timeStep, 1, _tempAllocator, _jobSystem);
        if (LastUpdateError != EPhysicsUpdateError.None)
            throw new InvalidOperationException($"Jolt physics update failed: {LastUpdateError}.");

        for (var i = 0; i < _rigidBodies.Count; ++i)
        {
            if (_rigidBodies[i].TryGetTarget(out var body))
                body.CaptureSimulationTransform();
        }
        for (var i = 0; i < _kinematicCharacters.Count; ++i)
        {
            if (_kinematicCharacters[i].TryGetTarget(out var character))
                character.CaptureSimulationTransform();
        }
        SendCollisionEvents();
        var postStep = new PhysicsEvents.PhysicsPostStepEvent { World = this, TimeStep = timeStep };
        SendEvent(ref postStep);
    }

    private void ApplyWorldTransforms(float interpolation)
    {
        interpolation = SystemMath.Clamp(interpolation, 0.0f, 1.0f);
        for (var i = 0; i < _rigidBodies.Count; ++i)
        {
            if (_rigidBodies[i].TryGetTarget(out var body))
                body.ApplyWorldTransform(interpolation);
        }
    }

    private void RecordContact(Body body1, Body body2, ContactManifold manifold, ContactSettings settings)
    {
        var id1 = body1.GetID();
        var id2 = body2.GetID();
        var pair = new BodyPair(id1, id2);
        var point = manifold.GetWorldSpaceContactPointOn1(0);
        var normal = manifold.MWorldSpaceNormal;
        if (pair.Body1 != id1)
            normal = -normal;
        var contact = new ContactData(
            pair,
            settings.MIsSensor || body1.IsSensor() || body2.IsSensor(),
            new PhysicsContact
            {
                Position = point,
                Normal = normal,
                Distance = -manifold.MPenetrationDepth,
                Impulse = 0.0f
            });
        lock (_contactLock)
            _currentContacts[pair] = contact;
    }

    private void SendCollisionEvents()
    {
        lock (_contactLock)
        {
            foreach (var (pair, contact) in _currentContacts)
            {
                var bodyA = GetBody(pair.Body1);
                var bodyB = GetBody(pair.Body2);
                if (bodyA is null || bodyB is null || !ShouldSignal(bodyA, bodyB))
                    continue;
                var nodeA = bodyA.TryGetNode();
                var nodeB = bodyB.TryGetNode();
                if (nodeA is null || nodeB is null)
                    continue;
                var isNew = !_previousContacts.ContainsKey(pair);
                if (isNew)
                {
                    var start = new PhysicsEvents.PhysicsCollisionStartEvent
                    {
                        World = this, NodeA = nodeA, NodeB = nodeB, BodyA = bodyA, BodyB = bodyB,
                        Trigger = contact.Trigger, Contact = contact.Contact
                    };
                    SendEvent(ref start);
                }

                if (bodyA.TryGetNode() is null || bodyB.TryGetNode() is null)
                    continue;
                var ongoing = new PhysicsEvents.PhysicsCollisionEvent
                {
                    World = this, NodeA = nodeA, NodeB = nodeB, BodyA = bodyA, BodyB = bodyB,
                    Trigger = contact.Trigger, Contact = contact.Contact
                };
                SendEvent(ref ongoing);
                SendNodeCollisionEvents(bodyA, nodeA, bodyB, nodeB, contact, isNew);
                if (bodyA.TryGetNode() is not null && bodyB.TryGetNode() is not null)
                {
                    var flippedContact = contact.Contact;
                    flippedContact.Normal = -flippedContact.Normal;
                    var flipped = contact with { Contact = flippedContact };
                    SendNodeCollisionEvents(bodyB, nodeB, bodyA, nodeA, flipped, isNew);
                }
            }

            foreach (var (pair, contact) in _previousContacts)
            {
                if (_currentContacts.ContainsKey(pair))
                    continue;
                var bodyA = GetBody(pair.Body1);
                var bodyB = GetBody(pair.Body2);
                if (bodyA is null || bodyB is null || !ShouldSignal(bodyA, bodyB))
                    continue;
                var nodeA = bodyA.TryGetNode();
                var nodeB = bodyB.TryGetNode();
                if (nodeA is null || nodeB is null)
                    continue;
                var end = new PhysicsEvents.PhysicsCollisionEndEvent
                {
                    World = this, NodeA = nodeA, NodeB = nodeB, BodyA = bodyA, BodyB = bodyB, Trigger = contact.Trigger
                };
                SendEvent(ref end);
                var nodeEndA = new PhysicsEvents.NodeCollisionEndEvent
                {
                    Body = bodyA, OtherNode = nodeB, OtherBody = bodyB, Trigger = contact.Trigger
                };
                nodeA.SendEvent(ref nodeEndA);
                if (bodyA.TryGetNode() is null || bodyB.TryGetNode() is null)
                    continue;
                var nodeEndB = new PhysicsEvents.NodeCollisionEndEvent
                {
                    Body = bodyB, OtherNode = nodeA, OtherBody = bodyA, Trigger = contact.Trigger
                };
                nodeB.SendEvent(ref nodeEndB);
            }

            _previousContacts.Clear();
            foreach (var pair in _currentContacts)
                _previousContacts.Add(pair.Key, pair.Value);
        }
    }

    private static void SendNodeCollisionEvents(
        RigidBody body, Node node, RigidBody otherBody, Node otherNode, ContactData contact, bool isNew)
    {
        if (isNew)
        {
            var start = new PhysicsEvents.NodeCollisionStartEvent
            {
                Body = body, OtherNode = otherNode, OtherBody = otherBody,
                Trigger = contact.Trigger, Contact = contact.Contact
            };
            node.SendEvent(ref start);
        }
        if (body.TryGetNode() is null || otherBody.TryGetNode() is null)
            return;
        var ongoing = new PhysicsEvents.NodeCollisionEvent
        {
            Body = body, OtherNode = otherNode, OtherBody = otherBody,
            Trigger = contact.Trigger, Contact = contact.Contact
        };
        node.SendEvent(ref ongoing);
    }

    private RigidBody? GetBody(uint bodyId)
    {
        if (!_bodiesById.TryGetValue(bodyId, out var weakBody))
            return null;
        if (weakBody.TryGetTarget(out var body))
            return body;
        _bodiesById.Remove(bodyId);
        return null;
    }

    private static bool ShouldSignal(RigidBody bodyA, RigidBody bodyB)
    {
        if (bodyA.Mass == 0.0f && bodyB.Mass == 0.0f)
            return false;
        if (bodyA.CollisionEventMode == CollisionEventMode.Never || bodyB.CollisionEventMode == CollisionEventMode.Never)
            return false;
        return bodyA.CollisionEventMode != CollisionEventMode.Active || bodyB.CollisionEventMode != CollisionEventMode.Active ||
            bodyA.IsActive || bodyB.IsActive;
    }

    private bool IsCollisionDisabled(uint body1, uint body2)
    {
        lock (_contactLock)
            return _disabledCollisionPairs.ContainsKey(new BodyPair(body1, body2));
    }

    private static void AcquireJolt()
    {
        lock (_joltSync)
        {
            if (_joltReferenceCount++ != 0)
                return;

            JoltNativeBindings.RegisterDefaultAllocator();
            _joltFactory = new Factory();
            Factory.SInstance = _joltFactory;
            JoltNativeBindings.RegisterTypes();
        }
    }

    private static void ReleaseJolt()
    {
        lock (_joltSync)
        {
            if (_joltReferenceCount == 0 || --_joltReferenceCount != 0)
                return;

            JoltNativeBindings.UnregisterTypes();
            Factory.SInstance = null!;
            _joltFactory!.Dispose();
            _joltFactory = null;
        }
    }

    private readonly record struct BodyPair
    {
        public BodyPair(uint body1, uint body2)
        {
            Body1 = SystemMath.Min(body1, body2);
            Body2 = SystemMath.Max(body1, body2);
        }

        public uint Body1 { get; }
        public uint Body2 { get; }
    }

    private readonly record struct ContactData(BodyPair Pair, bool Trigger, PhysicsContact Contact);
    private readonly record struct SynchronizedStep(long NetworkFrame, uint Offset);

    private sealed class PhysicsContactListener : ContactListener
    {
        private readonly WeakReference<PhysicsWorld> _world;

        public PhysicsContactListener(PhysicsWorld world)
        {
            _world = new WeakReference<PhysicsWorld>(world);
        }

        public override ValidateResult OnContactValidate(Body inBody1, Body inBody2,
            Vector3f inBaseOffset, CollideShapeResult inCollisionResult)
        {
            return _world.TryGetTarget(out var world) && world.IsCollisionDisabled(inBody1.GetID(), inBody2.GetID())
                ? ValidateResult.RejectAllContactsForThisBodyPair
                : ValidateResult.AcceptAllContactsForThisBodyPair;
        }

        public override void OnContactAdded(Body inBody1, Body inBody2, ContactManifold inManifold, ContactSettings ioSettings)
        {
            if (_world.TryGetTarget(out var world))
                world.RecordContact(inBody1, inBody2, inManifold, ioSettings);
        }

        public override void OnContactPersisted(Body inBody1, Body inBody2, ContactManifold inManifold, ContactSettings ioSettings)
        {
            if (_world.TryGetTarget(out var world))
                world.RecordContact(inBody1, inBody2, inManifold, ioSettings);
        }
    }

    private sealed class PhysicsObjectLayerPairFilter : ObjectLayerPairFilter
    {
        public override bool ShouldCollide(uint inLayer1, uint inLayer2) =>
            (ObjectLayerPairFilterMask.SGetGroup(inLayer1) & ObjectLayerPairFilterMask.SGetMask(inLayer2)) != 0 &&
            (ObjectLayerPairFilterMask.SGetGroup(inLayer2) & ObjectLayerPairFilterMask.SGetMask(inLayer1)) != 0;
    }

    private sealed class PhysicsObjectLayerFilter(uint collisionMask) : ObjectLayerFilter
    {
        public override bool ShouldCollide(uint inLayer) =>
            (ObjectLayerPairFilterMask.SGetGroup(inLayer) & collisionMask & 0xffff) != 0;
    }
}
