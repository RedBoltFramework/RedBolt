namespace RedBolt.PhysicsJolt;

using RedBolt.Bindings.Jolt;
using RedBolt.Math;

internal sealed class JoltRaycastVehicle : IDisposable
{
    private readonly PhysicsSystem _physicsSystem;
    private readonly uint _bodyId;
    private readonly uint _objectLayer;
    private readonly Vector3f _up;
    private readonly Vector3f _forward;
    private readonly VehicleConstraintSettings _settings;
    private readonly WheeledVehicleControllerSettings _controllerSettings;
    private readonly List<WheelSettingsWV> _wheels = [];
    private readonly List<float> _engineTorqueFactors = [];
    private readonly List<float> _suspensionStiffness = [];
    private readonly List<float> _suspensionDamping = [];
    private VehicleConstraint? _vehicle;
    private VehicleCollisionTesterCastSphere? _collisionTester;
    private NativePhysicsStepListener? _stepListener;
    private float _engineTorqueScale;
    private bool _addedConstraint;
    private bool _addedStepListener;
    private bool _disposed;

    internal JoltRaycastVehicle(PhysicsSystem physicsSystem, in uint bodyId, Vector3f up,
        Vector3f forward, uint objectLayer, float engineTorque)
    {
        _physicsSystem = physicsSystem;
        _bodyId = bodyId;
        _objectLayer = objectLayer;
        _up = up;
        _forward = forward;
        _settings = new VehicleConstraintSettings { MUp = up, MForward = forward };
        _controllerSettings = new WheeledVehicleControllerSettings();
        using (var engine = _controllerSettings.MEngine)
        using (var torqueCurve = engine.MNormalizedTorque)
        {
            engine.MMaxTorque = engineTorque;
            torqueCurve.Clear();
            torqueCurve.AddPoint(0.0f, 1.0f);
            torqueCurve.AddPoint(1.0f, 1.0f);
        }
        using (var transmission = _controllerSettings.MTransmission)
        using (var forwardGears = new global::JPH.Array<float>())
        using (var reverseGears = new global::JPH.Array<float>())
        {
            forwardGears.Add(1.0f);
            reverseGears.Add(-1.0f);
            transmission.MGearRatios = forwardGears;
            transmission.MReverseGearRatios = reverseGears;
        }
        _settings.MController = _controllerSettings;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Remove();
        _stepListener?.Dispose();
        _collisionTester?.Dispose();
        _vehicle?.Dispose();
        _settings.Dispose();
        _controllerSettings.Dispose();
        for (var i = 0; i < _wheels.Count; ++i)
            _wheels[i].Dispose();
    }

    internal void AddWheel(Vector3f position, Vector3f direction, Vector3f axle, float rollInfluence,
        float radius, float suspensionRestLength, float maxSuspensionTravel, float suspensionStiffness,
        float dampingCompression, float dampingRelaxation, float frictionSlip, float maxSuspensionForce,
        float maxBrakeTorque, float steeringFactor, float engineFactor)
    {
        var suspensionDirection = direction.NormalizedOrDefault(Vector3f.Down);
        var wheelUp = -suspensionDirection;
        var normalizedAxle = axle.NormalizedOrDefault(_forward.CrossProduct(_up));
        var wheelForward = wheelUp.CrossProduct(normalizedAxle).NormalizedOrDefault(_forward);
        var wheelRadius = MathF.Max(0.001f, radius);
        var suspensionMinLength = MathF.Max(0.0f, suspensionRestLength - maxSuspensionTravel);
        var suspensionMaxLength = MathF.Max(suspensionMinLength, suspensionRestLength);
        var damping = MathF.Max(0.0f, 0.5f * (dampingCompression + dampingRelaxation));
        var neutralSuspensionPoint = position + suspensionDirection * suspensionRestLength;
        var upOffset = neutralSuspensionPoint.DotProduct(_up);
        var suspensionForcePoint = neutralSuspensionPoint
            + _up * upOffset * (global::System.Math.Clamp(rollInfluence, 0.0f, 1.0f) - 1.0f);
        var brakeTorque = MathF.Max(0.0f, maxBrakeTorque * wheelRadius * 60.0f);
        var stiffness = MathF.Max(0.0f, suspensionStiffness);

        var wheel = new WheelSettingsWV
        {
            MPosition = position,
            MSuspensionDirection = suspensionDirection,
            MSteeringAxis = -suspensionDirection,
            MWheelUp = wheelUp,
            MWheelForward = wheelForward,
            MSuspensionMinLength = suspensionMinLength,
            MSuspensionMaxLength = suspensionMaxLength,
            MRadius = wheelRadius,
            MWidth = MathF.Max(0.01f, radius * 0.8f),
            MSuspensionForcePoint = suspensionForcePoint,
            MEnableSuspensionForcePoint = rollInfluence < 1.0f,
            MMaxSteerAngle = global::System.Math.Clamp(MathF.Abs(steeringFactor), 0.0f, 0.5f * MathF.PI),
            MMaxBrakeTorque = brakeTorque,
            MMaxHandBrakeTorque = brakeTorque
        };
        using (var suspensionSpring = wheel.MSuspensionSpring)
        {
            suspensionSpring.MMode = ESpringMode.MassNormalizedStiffnessAndDamping;
            suspensionSpring.MStiffness = stiffness;
            suspensionSpring.MDamping = damping;
        }

        _ = maxSuspensionForce; // Jolt has no per-wheel suspension force cap.
        var frictionScale = global::System.Math.Clamp(frictionSlip / 10.5f, 0.0f, 2.0f);
        using (var longitudinalFriction = wheel.MLongitudinalFriction)
        using (var lateralFriction = wheel.MLateralFriction)
        {
            longitudinalFriction.Clear();
            longitudinalFriction.AddPoint(0.0f, 0.0f);
            longitudinalFriction.AddPoint(0.06f, 1.2f * frictionScale);
            longitudinalFriction.AddPoint(0.2f, frictionScale);
            lateralFriction.Clear();
            lateralFriction.AddPoint(0.0f, 0.0f);
            lateralFriction.AddPoint(3.0f, 1.2f * frictionScale);
            lateralFriction.AddPoint(20.0f, frictionScale);
        }
        using (var wheels = _settings.MWheels)
            wheels.Add(wheel);
        _wheels.Add(wheel);
        _engineTorqueFactors.Add(MathF.Max(0.0f, MathF.Abs(engineFactor) * wheelRadius));
        _suspensionStiffness.Add(stiffness);
        _suspensionDamping.Add(damping);
    }

    internal bool Initialize(bool enabled)
    {
        if (_vehicle is not null)
            return true;

        ConfigureSuspension();
        ConfigureDifferentials();
        using var bodyLock = _physicsSystem.GetBodyLockInterface();
        var mutex = bodyLock.LockRead(in _bodyId);
        try
        {
            using var body = bodyLock.TryGetBody(in _bodyId);
            if (body.IsNull)
                return false;
            _vehicle = new VehicleConstraint(body, _settings);
        }
        finally
        {
            bodyLock.UnlockRead(mutex);
        }
        _stepListener = new NativePhysicsStepListener(_vehicle.GetPhysicsStepListenerHandle());
        _collisionTester = new VehicleCollisionTesterCastSphere(_objectLayer, GetCollisionRadius());
        _vehicle.SetVehicleCollisionTester(_collisionTester);
        _physicsSystem.AddConstraint(_vehicle);
        _addedConstraint = true;
        SetEnabled(enabled);
        return true;
    }

    internal void Remove()
    {
        if (_vehicle is null)
            return;
        if (_addedStepListener)
        {
            _physicsSystem.RemoveStepListener(_stepListener!);
            _addedStepListener = false;
        }
        if (_addedConstraint)
        {
            _physicsSystem.RemoveConstraint(_vehicle);
            _addedConstraint = false;
        }
    }

    internal void SetEnabled(bool enabled)
    {
        if (_vehicle is null)
            return;
        _vehicle.SetEnabled(enabled);
        if (enabled == _addedStepListener)
            return;
        if (enabled)
            _physicsSystem.AddStepListener(_stepListener!);
        else
            _physicsSystem.RemoveStepListener(_stepListener!);
        _addedStepListener = enabled;
    }

    internal void SetDriverInput(float forward, float right, float brake, float engineTorque)
    {
        if (GetController() is not { } controller)
            return;
        using var engine = controller.GetEngine();
        engine.MMaxTorque = MathF.Max(0.0f, engineTorque * _engineTorqueScale);
        controller.SetDriverInput(forward, -right, brake, 0.0f);
    }

    internal Vector3f GetWheelPosition(uint index)
    {
        if (!HasWheel(index))
            return Vector3f.Zero;
        using var transform = GetWheelWorldTransform(index);
        return transform.GetTranslation();
    }

    internal Quaternion GetWheelRotation(uint index)
    {
        if (!HasWheel(index))
            return Quaternion.IDENTITY;
        using var transform = GetWheelWorldTransform(index);
        return transform.GetQuaternion();
    }

    internal Vector3f GetWheelLocalPosition(uint index)
    {
        if (!HasWheel(index))
            return Vector3f.Zero;
        using var transform = GetWheelLocalTransform(index);
        return transform.GetTranslation();
    }

    internal Quaternion GetWheelLocalRotation(uint index)
    {
        if (!HasWheel(index))
            return Quaternion.IDENTITY;
        using var transform = GetWheelLocalTransform(index);
        return transform.GetQuaternion();
    }

    internal bool IsWheelInContact(uint index) => HasWheel(index) && _vehicle!.GetWheel(index).HasContact();

    internal Vector3f GetWheelContactPosition(uint index) =>
        IsWheelInContact(index) ? _vehicle!.GetWheel(index).GetContactPosition() : Vector3f.Zero;

    internal Vector3f GetWheelContactNormal(uint index) =>
        IsWheelInContact(index) ? _vehicle!.GetWheel(index).GetContactNormal() : Vector3f.Zero;

    internal float GetWheelSuspensionLength(uint index) =>
        HasWheel(index) ? _vehicle!.GetWheel(index).GetSuspensionLength() : 0.0f;

    internal float GetWheelAngularVelocity(uint index) =>
        HasWheel(index) ? _vehicle!.GetWheel(index).GetAngularVelocity() : 0.0f;

    internal void SetWheelAngularVelocity(uint index, float angularVelocity)
    {
        if (HasWheel(index))
            _vehicle!.GetWheel(index).SetAngularVelocity(angularVelocity);
    }

    private void ConfigureSuspension()
    {
        var wheelCount = _wheels.Count;
        for (var i = 0; i < wheelCount; ++i)
        {
            using var suspensionSpring = _wheels[i].MSuspensionSpring;
            suspensionSpring.MStiffness = _suspensionStiffness[i] * wheelCount;
            suspensionSpring.MDamping = _suspensionDamping[i] * wheelCount;
        }
    }

    private void ConfigureDifferentials()
    {
        using var differentials = _controllerSettings.MDifferentials;
        differentials.Clear();
        var totalFactor = 0.0f;
        for (var i = 0; i < _engineTorqueFactors.Count; ++i)
            totalFactor += _engineTorqueFactors[i];
        _engineTorqueScale = totalFactor;
        using (var engine = _controllerSettings.MEngine)
            engine.MMaxTorque *= totalFactor;
        if (totalFactor <= 0.0f)
            return;

        for (var i = 0; i < _engineTorqueFactors.Count; ++i)
        {
            var factor = _engineTorqueFactors[i];
            if (factor <= 0.0f)
                continue;
            using var differential = new VehicleDifferentialSettings
            {
                MLeftWheel = i,
                MRightWheel = -1,
                MDifferentialRatio = 1.0f,
                MEngineTorqueRatio = factor / totalFactor
            };
            differentials.Add(differential);
        }
    }

    private WheeledVehicleController? GetController() =>
        _vehicle?.GetController() as WheeledVehicleController;

    private bool HasWheel(uint index) => _vehicle is not null && index < _wheels.Count;

    private float GetCollisionRadius()
    {
        var radius = float.MaxValue;
        for (var i = 0; i < _wheels.Count; ++i)
            radius = MathF.Min(radius, 0.5f * _wheels[i].MWidth);
        return radius != float.MaxValue ? MathF.Max(0.01f, radius) : 0.01f;
    }

    private Mat44 GetWheelWorldTransform(uint index) =>
        _vehicle!.GetWheelWorldTransform(index, -Vector3f.Right, Vector3f.Up);

    private Mat44 GetWheelLocalTransform(uint index) =>
        _vehicle!.GetWheelLocalTransform(index, -Vector3f.Right, Vector3f.Up);

    private sealed class NativePhysicsStepListener(nint handle) : PhysicsStepListener(handle)
    {
        public override void OnStep(PhysicsStepListenerContext inContext) { }
    }
}
