//
// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2023-2023 the rbfx project.
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
using MathColor = RedBolt.Math.Color;

/// <summary>Raycast vehicle component.</summary>
[Reflected(ObjectCategory.Category_Physics, InheritAttributes = false)]
public partial class RaycastVehicle : LogicComponent
{
    public const float DefaultBrakingForce = 50.0f;
    public const float DefaultEngineForce = 2500.0f;

    public static readonly Vector3i RIGHT_UP_FORWARD = new(0, 1, 2);
    public static readonly Vector3i RIGHT_FORWARD_UP = new(0, 2, 1);
    public static readonly Vector3i UP_FORWARD_RIGHT = new(1, 2, 0);
    public static readonly Vector3i UP_RIGHT_FORWARD = new(1, 0, 2);
    public static readonly Vector3i FORWARD_RIGHT_UP = new(2, 0, 1);
    public static readonly Vector3i FORWARD_UP_RIGHT = new(2, 1, 0);

    private readonly List<RaycastVehicleWheel> _wheelComponents = [];
    private RigidBody? _hullBody;
    private PhysicsWorld? _physicsWorld;
    private JoltRaycastVehicle? _vehicle;
    private Vector3i _coordinateSystem = RIGHT_UP_FORWARD;
    private float _inAirRpm;
    private float _maxSideSlipSpeed = 4.0f;
    private float _brakingForce = DefaultBrakingForce;
    private float _engineForce = DefaultEngineForce;
    private bool _activate;
    private bool _initialized;
    private bool _staticParametersDirty = true;
    private bool _hasSimulated;
    private bool _physicsEventsSubscribed;

    /// <summary>Construct.</summary>
    public RaycastVehicle()
    {
        SetUpdateEventMask(UpdateEvent.UseFixedUpdate | UpdateEvent.UseFixedPostUpdate | UpdateEvent.UsePostUpdate);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        UnsubscribeFromPhysicsEvents();
        ReleaseVehicle();
        _hullBody?.RemoveVehicle(this);
        _hullBody = null;
        _node?.RemoveListener(this);
        base.Dispose();
    }

    /// <summary>Visualize wheel as debug geometry.</summary>
    public void DrawWheelDebugGeometry(int wheelIndex, DebugRenderer? debug, bool depthTest)
    {
        var wheel = GetWheel(wheelIndex);
        if (wheel is null || debug is null || !EnsureVehicle())
            return;

        var wheelColor = wheel.InContact ? MathColor.Blue : MathColor.Magenta;
        var wheelPosition = GetWheelPosition(wheelIndex);
        var axle = (GetWheelRotation(wheelIndex) * Vector3f.Right).NormalizedOrDefault(Vector3f.Right);
        debug.AddCircle(wheelPosition, axle, wheel.Radius, wheelColor, 64, depthTest);
        debug.AddLine(wheelPosition, wheelPosition + axle, wheelColor, depthTest);
        if (wheel.InContact)
        {
            debug.AddCircle(wheel.ContactPosition, wheel.ContactNormal,
                MathF.Max(0.01f, wheel.Radius * 0.2f), wheelColor, 64, depthTest);
        }
    }

    /// <summary>Handle enabled/disabled state change.</summary>
    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
        _vehicle?.SetEnabled(IsEnabledEffective);
    }

    /// <summary>Perform post-load after deserialization.</summary>
    public override void ApplyAttributes()
    {
        Init();
        base.ApplyAttributes();
    }

    /// <summary>Immediately apply wheel attributes to physics.</summary>
    public void ApplyWheelAttributes(int index)
    {
        if (index >= 0 && index < _wheelComponents.Count)
            _staticParametersDirty = true;
    }

    /// <summary>Add a wheel.</summary>
    public void AddWheel(RaycastVehicleWheel? wheel)
    {
        if (wheel is null || _wheelComponents.Contains(wheel))
            return;
        _wheelComponents.Add(wheel);
        wheel.WheelIndex = _wheelComponents.Count - 1;
        _staticParametersDirty = true;
    }

    /// <summary>Remove a wheel.</summary>
    public void RemoveWheel(RaycastVehicleWheel? wheel)
    {
        if (wheel is null)
            return;
        var index = wheel.WheelIndex;
        if (index < 0 || index >= _wheelComponents.Count || !ReferenceEquals(_wheelComponents[index], wheel))
            return;

        _wheelComponents.RemoveAt(index);
        wheel.WheelIndex = -1;
        for (var i = index; i < _wheelComponents.Count; ++i)
            _wheelComponents[i].WheelIndex = i;
        _staticParametersDirty = true;
    }

    /// <summary>Get wheel.</summary>
    public RaycastVehicleWheel? GetWheel(int index) =>
        index >= 0 && index < _wheelComponents.Count ? _wheelComponents[index] : null;

    /// <summary>Maximum linear momentum supplied by engine to rigid body.</summary>
    [Serialized("Engine Force")]
    public float EngineForce
    {
        get => _engineForce;
        set => _engineForce = value;
    }

    /// <summary>Rotational momentum preventing wheel rotation.</summary>
    [Serialized("Braking Force")]
    public float BrakingForce
    {
        get => _brakingForce;
        set
        {
            if (_brakingForce == value)
                return;
            _brakingForce = value;
            _staticParametersDirty = true;
        }
    }

    /// <summary>Side speed which is considered sliding.</summary>
    [Serialized("Maximum side slip threshold")]
    public float MaxSideSlipSpeed
    {
        get => _maxSideSlipSpeed;
        set => _maxSideSlipSpeed = value;
    }

    /// <summary>Revolutions per minute for motor wheels that do not touch the ground.</summary>
    [Serialized("RPM for wheel motors in air (0=calculate)")]
    public float InAirRPM
    {
        get => _inAirRpm;
        set => _inAirRpm = value;
    }

    /// <summary>Vehicle coordinate system.</summary>
    [Serialized("Coordinate system")]
    public Vector3i CoordinateSystem
    {
        get => _coordinateSystem;
        set
        {
            if (_coordinateSystem == value)
                return;
            _coordinateSystem = value;
            _staticParametersDirty = true;
        }
    }

    /// <summary>Number of attached wheels in the initialized vehicle.</summary>
    public int NumWheels => _vehicle is not null ? _wheelComponents.Count : 0;

    /// <summary>Update input values for wheels.</summary>
    public void UpdateInput(float steering, float engineForceFactor, float brakingForceFactor)
    {
        for (var i = 0; i < _wheelComponents.Count; ++i)
        {
            var wheel = _wheelComponents[i];
            wheel.SteeringValue = wheel.SteeringFactor * steering;
            wheel.EngineForce = wheel.EngineFactor * engineForceFactor * _engineForce;
            wheel.BrakeValue = wheel.BrakeFactor * brakingForceFactor * _brakingForce;
        }
    }

    /// <summary>Reset all suspension.</summary>
    public void ResetSuspension()
    {
        _staticParametersDirty = true;
        EnsureVehicle();
    }

    /// <summary>Set wheel nodes to their initial positions.</summary>
    public void ResetWheels()
    {
        ResetSuspension();
        for (var i = 0; i < NumWheels; ++i)
        {
            if (_wheelComponents[i].TryGetNode() is { } wheelNode)
                wheelNode.SetWorldPosition(GetWheelPosition(i));
        }
    }

    /// <summary>Mark wheel static data as dirty and update it before simulation.</summary>
    public void InvalidateStaticWheelParameters(int wheelIndex)
    {
        if (wheelIndex >= 0 && wheelIndex < _wheelComponents.Count)
            _staticParametersDirty = true;
    }

    /// <summary>Mark wheel dynamic data as dirty and update it before simulation.</summary>
    public void InvalidateDynamicWheelParameters(int wheelIndex)
    {
        if (wheelIndex >= 0 && wheelIndex < _wheelComponents.Count)
            _activate = true;
    }

    /// <summary>Update transform for a particular wheel.</summary>
    public void UpdateWheelTransform(int wheel, bool interpolated)
    {
        if (wheel >= 0 && wheel < NumWheels)
            _hasSimulated |= interpolated;
    }

    /// <summary>Initialize the vehicle component after creation.</summary>
    public void Init()
    {
        if (TryGetNode() is not { } node || Scene is null)
            return;

        var hullBody = node.GetOrCreateComponent<RigidBody>();
        if (!ReferenceEquals(_hullBody, hullBody))
        {
            ReleaseVehicle();
            _hullBody?.RemoveVehicle(this);
            _hullBody = hullBody;
            _hullBody.AddVehicle(this);
        }
        _physicsWorld = Scene.GetOrCreateComponent<PhysicsWorld>();
        SubscribeToPhysicsEvents();
        _initialized = true;
        _staticParametersDirty = true;
        EnsureVehicle();
    }

    /// <summary>Perform fixed step pre-update.</summary>
    public override void FixedUpdate(float timeStep)
    {
        _hasSimulated = true;
        if (!EnsureVehicle() || _vehicle is null)
            return;

        var steering = 0.0f;
        var engine = 0.0f;
        var brake = 0.0f;
        for (var i = 0; i < _wheelComponents.Count; ++i)
        {
            var wheel = _wheelComponents[i];
            if (wheel.SteeringFactor != 0.0f)
                steering = SelectLargestMagnitude(steering, wheel.SteeringValue / wheel.SteeringFactor);
            if (wheel.EngineFactor != 0.0f && _engineForce != 0.0f)
                engine = SelectLargestMagnitude(engine, wheel.EngineForce / (wheel.EngineFactor * _engineForce));
            if (wheel.BrakeFactor != 0.0f && _brakingForce != 0.0f)
                brake = MathF.Max(brake, MathF.Abs(wheel.BrakeValue / (wheel.BrakeFactor * _brakingForce)));
            if (wheel.EngineForce != 0.0f || wheel.SteeringValue != 0.0f)
                _activate = true;
        }

        _vehicle.SetDriverInput(
            SystemMath.Clamp(engine, -1.0f, 1.0f),
            SystemMath.Clamp(steering, -1.0f, 1.0f),
            SystemMath.Clamp(brake, 0.0f, 1.0f),
            MathF.Abs(_engineForce));
        if (_activate)
        {
            _hullBody?.Activate();
            _activate = false;
        }
    }

    /// <summary>Perform fixed step post-update.</summary>
    public override void FixedPostUpdate(float timeStep)
    {
        if (_vehicle is null || _hullBody is null)
            return;

        var velocity = _hullBody.LinearVelocity;
        for (var i = 0; i < NumWheels; ++i)
        {
            var wheel = _wheelComponents[i];
            var skidInfoCumulative = wheel.SkidInfoCumulative;
            var inContact = _vehicle.IsWheelInContact((uint)i);
            wheel.InContact = inContact;
            if (!inContact && wheel.EngineForce != 0.0f)
            {
                var angularVelocity = _inAirRpm != 0.0f
                    ? _inAirRpm * (2.0f * MathF.PI / 60.0f)
                    : _hullBody.Mass > 0.0f && wheel.Radius > 0.0f
                        ? 8.0f * wheel.EngineForce / (_hullBody.Mass * wheel.Radius)
                        : 0.0f;
                if (MathF.Abs(_vehicle.GetWheelAngularVelocity((uint)i)) < MathF.Abs(angularVelocity))
                    _vehicle.SetWheelAngularVelocity((uint)i, angularVelocity);
                if (skidInfoCumulative > 0.05f)
                    skidInfoCumulative -= 0.002f;
            }
            else
            {
                if (inContact)
                {
                    wheel.ContactPosition = _vehicle.GetWheelContactPosition((uint)i);
                    wheel.ContactNormal = _vehicle.GetWheelContactNormal((uint)i);
                }
                skidInfoCumulative = wheel.SlidingFactor;
            }

            var axle = GetWheelRotation(i) * Vector3f.Right;
            var sideSlipSpeed = MathF.Abs(axle.DotProduct(velocity));
            if (sideSlipSpeed > _maxSideSlipSpeed)
                skidInfoCumulative = SystemMath.Clamp(skidInfoCumulative, 0.0f, 0.89f);
            wheel.SideSlipSpeed = sideSlipSpeed;
            wheel.SkidInfoCumulative = skidInfoCumulative;
        }
    }

    /// <summary>Perform variable step post-update.</summary>
    public override void PostUpdate(float timeStep)
    {
        if (_vehicle is null || TryGetNode() is not { } vehicleNode)
            return;
        for (var i = 0; i < NumWheels; ++i)
        {
            var wheel = _wheelComponents[i];
            if (wheel.TryGetNode() is not { } wheelNode)
                continue;
            // Compose the fixed-step wheel pose with the interpolated scene-node pose. Using Jolt's world-space
            // wheel transform here puts the wheels one physics step ahead of the chassis while it is moving.
            var localPosition = _vehicle.GetWheelLocalPosition((uint)i) + wheel.Offset;
            var localRotation = _vehicle.GetWheelLocalRotation((uint)i);
            wheelNode.SetWorldPosition(vehicleNode.LocalToWorld(localPosition));
            wheelNode.SetWorldRotation(vehicleNode.WorldRotation * localRotation * wheel.Rotation);
        }
    }

    /// <summary>Get wheel position in world space.</summary>
    public Vector3f GetWheelPosition(int wheel) =>
        _vehicle is not null && wheel >= 0 && wheel < NumWheels
            ? _vehicle.GetWheelPosition((uint)wheel)
            : Vector3f.Zero;

    /// <summary>Get wheel rotation in world space.</summary>
    public Quaternion GetWheelRotation(int wheel) =>
        _vehicle is not null && wheel >= 0 && wheel < NumWheels
            ? _vehicle.GetWheelRotation((uint)wheel)
            : Quaternion.IDENTITY;

    /// <summary>Get wheel connection point relative to rigid body.</summary>
    public Vector3f GetWheelConnectionPoint(int wheel) => GetWheel(wheel)?.ConnectionPoint ?? Vector3f.Zero;

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        previousNode?.RemoveListener(this);
        if (currentNode is null)
        {
            UnsubscribeFromPhysicsEvents();
            ReleaseVehicle();
            _hullBody?.RemoveVehicle(this);
            _hullBody = null;
            _physicsWorld = null;
            _initialized = false;
        }
        currentNode?.AddListener(this);
        base.OnNodeSet(previousNode, currentNode);
        OnSetEnabled();
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (scene is null)
        {
            UnsubscribeFromPhysicsEvents();
            ReleaseVehicle();
            _physicsWorld = null;
        }
        base.OnSceneSet(previousScene, scene);
        if (scene is not null && _initialized)
        {
            _physicsWorld = scene.GetOrCreateComponent<PhysicsWorld>();
            SubscribeToPhysicsEvents();
            _staticParametersDirty = true;
            EnsureVehicle();
        }
    }

    protected override void OnMarkedDirty(Node node)
    {
        if (!_hasSimulated && _vehicle is not null && _physicsWorld is { IsApplyingTransforms: false })
            PostUpdate(0.0f);
    }

    internal void ReleaseVehicleForBodyRecreation()
    {
        ReleaseVehicle();
        _staticParametersDirty = true;
    }

    internal void NotifyBodyCreated(RigidBody body)
    {
        if (!ReferenceEquals(body, _hullBody))
            return;
        _staticParametersDirty = true;
        EnsureVehicle();
    }

    private bool EnsureVehicle()
    {
        if (!_initialized || _hullBody?.JoltBodyId is not { } bodyId || _physicsWorld is null ||
            _wheelComponents.Count == 0)
            return false;
        if (_vehicle is not null && !_staticParametersDirty)
            return true;

        ReleaseVehicle();
        var up = GetCoordinateAxis(_coordinateSystem.Y, Vector3f.Up);
        var forward = GetCoordinateAxis(_coordinateSystem.Z, Vector3f.Forward);
        var objectLayer = ObjectLayerPairFilterMask.SGetObjectLayer(
            _hullBody.CollisionLayer & 0xffff, _hullBody.CollisionMask & 0xffff);
        var vehicle = new JoltRaycastVehicle(
            _physicsWorld.World, in bodyId, up, forward, objectLayer, MathF.Abs(_engineForce));
        for (var i = 0; i < _wheelComponents.Count; ++i)
        {
            var wheel = _wheelComponents[i];
            vehicle.AddWheel(
                wheel.ConnectionPoint, wheel.Direction, wheel.Axle, wheel.RollInfluence, wheel.Radius,
                wheel.SuspensionRestLength, wheel.MaxSuspensionTravel, wheel.SuspensionStiffness,
                wheel.DampingCompression, wheel.DampingRelaxation, wheel.FrictionSlip,
                wheel.MaxSuspensionForce, MathF.Abs(_brakingForce * wheel.BrakeFactor),
                wheel.SteeringFactor, wheel.EngineFactor);
        }
        if (!vehicle.Initialize(IsEnabledEffective))
        {
            vehicle.Dispose();
            return false;
        }
        _vehicle = vehicle;
        _staticParametersDirty = false;
        return true;
    }

    private void ReleaseVehicle()
    {
        if (_vehicle is null)
            return;
        _vehicle.Remove();
        _vehicle.Dispose();
        _vehicle = null;
    }

    private void SubscribeToPhysicsEvents()
    {
        if (_physicsEventsSubscribed || _physicsWorld is null)
            return;
        SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(_physicsWorld, HandlePhysicsPreStep);
        SubscribeToEvent<PhysicsEvents.PhysicsPostStepEvent>(_physicsWorld, HandlePhysicsPostStep);
        _physicsEventsSubscribed = true;
    }

    private void UnsubscribeFromPhysicsEvents()
    {
        if (!_physicsEventsSubscribed)
            return;
        if (_physicsWorld is not null)
        {
            UnsubscribeFromEvent<PhysicsEvents.PhysicsPreStepEvent>(_physicsWorld);
            UnsubscribeFromEvent<PhysicsEvents.PhysicsPostStepEvent>(_physicsWorld);
        }
        _physicsEventsSubscribed = false;
    }

    private void HandlePhysicsPreStep(ref PhysicsEvents.PhysicsPreStepEvent eventData)
    {
        if (IsEnabledEffective)
            FixedUpdate(eventData.TimeStep);
    }

    private void HandlePhysicsPostStep(ref PhysicsEvents.PhysicsPostStepEvent eventData)
    {
        if (IsEnabledEffective)
            FixedPostUpdate(eventData.TimeStep);
    }

    private static float SelectLargestMagnitude(float current, float candidate) =>
        MathF.Abs(candidate) > MathF.Abs(current) ? candidate : current;

    private static Vector3f GetCoordinateAxis(int axis, Vector3f fallback) => axis switch
    {
        0 => Vector3f.Right,
        1 => Vector3f.Up,
        2 => Vector3f.Forward,
        _ => fallback
    };
}
