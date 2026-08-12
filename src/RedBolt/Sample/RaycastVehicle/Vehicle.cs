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

namespace RedBolt.Sample.RaycastVehicle;

using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Resource;
using RedBolt.Scene;
using ResourceType = RedBolt.Resource.Resource;

/// <summary>Vehicle logic component used by the raycast vehicle sample.</summary>
[Reflected]
public sealed partial class Vehicle2 : MoveAndOrbitComponent
{
    private const float ChassisWidth = 2.6f;

    private readonly List<Node> _particleEmitterNodes = [];
    private readonly Vector3f[] _connectionPoints = new Vector3f[4];
    private PhysicsWorld? _physicsWorld;
    private InputMap? _inputMap;
    private Vector3f _previousVelocity;
    private float _steering;
    private readonly float _wheelRadius = 0.5f;
    private readonly float _suspensionRestLength = 0.6f;
    private readonly float _wheelWidth = 0.4f;
    private readonly float _suspensionStiffness = 14.0f;
    private readonly float _suspensionDamping = 2.0f;
    private readonly float _suspensionCompression = 4.0f;
    private readonly float _wheelFriction = 1000.0f;
    private readonly float _rollInfluence = 0.12f;
    private bool _emittersCreated;

    public Vehicle2()
    {
        SetUpdateEventMask(UpdateEvent.UseFixedUpdate | UpdateEvent.UsePostUpdate);
    }

    [Serialized("Steering")]
    public float Steering
    {
        get => _steering;
        set => _steering = value;
    }

    [Serialized("Controls Yaw")]
    public float ControlsYaw
    {
        get => GetYaw();
        set => SetYaw(value);
    }

    [Serialized("Controls Pitch")]
    public float ControlsPitch
    {
        get => GetPitch();
        set => SetPitch(value);
    }

    public float WheelRadius => _wheelRadius;
    public float WheelWidth => _wheelWidth;

    public void SetInputMap(InputMap? inputMap) => _inputMap = inputMap;
    public InputMap? GetInputMap() => _inputMap;

    [Serialized("Input Map", DefaultSource = nameof(GetInputMapAttributeDefault), Setter = nameof(SetInputMapAttr))]
    public ResourceRef GetInputMapAttr() =>
        ResourceType.GetResourceRef(_inputMap, ObjectReflection<InputMap>.TypeId);

    public void SetInputMapAttr(ResourceRef value) => SetInputMap(InputMap.Load(value.Name));

    public override void Start()
    {
        SubscribeToPhysicsWorld();
    }

    public override void DelayedStart()
    {
        SubscribeToPhysicsWorld();
    }

    public override void Stop()
    {
        if (_physicsWorld is not null)
            UnsubscribeFromEvent<PhysicsEvents.PhysicsPreStepEvent>(_physicsWorld);
        _physicsWorld = null;
    }

    public void Init()
    {
        var node = Node!;
        var vehicle = node.CreateComponent<RedBolt.PhysicsJolt.RaycastVehicle>();
        vehicle.Init();
        var hullBody = node.GetComponent<RigidBody>()!;
        hullBody.Mass = 800.0f;
        hullBody.LinearDamping = 0.2f;
        hullBody.AngularDamping = 0.5f;
        hullBody.CollisionLayer = 1;

        var boxExtents = new Vector3f(2.3f, 1.0f, 4.0f);
        node.CreateComponent<CollisionShape>().SetBox(boxExtents);
        var box = node.CreateChild();
        box.SetScale(boxExtents);
        var hullObject = box.CreateComponent<StaticModel>();
        hullObject.SetModel(GetSubsystem<ResourceCache>()!.Get<Model>("Models/Box.mdl"));
        hullObject.SetMaterial(GetSubsystem<ResourceCache>()!.Get<Material>("Materials/Stone.xml"));
        hullObject.SetCastShadows(true);

        const float connectionHeight = -0.4f;
        var wheelDirection = new Vector3f(0.0f, -1.0f, 0.0f);
        var wheelAxle = new Vector3f(-1.0f, 0.0f, 0.0f);
        var wheelX = ChassisWidth / 2.0f - _wheelWidth;
        _connectionPoints[0] = new Vector3f(-wheelX, connectionHeight, 2.5f - _wheelRadius * 2.0f);
        _connectionPoints[1] = new Vector3f(wheelX, connectionHeight, 2.5f - _wheelRadius * 2.0f);
        _connectionPoints[2] = new Vector3f(-wheelX, connectionHeight, -2.5f + _wheelRadius * 2.0f);
        _connectionPoints[3] = new Vector3f(wheelX, connectionHeight, -2.5f + _wheelRadius * 2.0f);

        foreach (var connectionPoint in _connectionPoints)
        {
            var wheelNode = node.CreateChild();
            var isFrontWheel = connectionPoint.Z > 0.0f;
            var rotation = connectionPoint.X >= 0.0f
                ? new Quaternion(0.0f, 0.0f, -90.0f)
                : new Quaternion(0.0f, 0.0f, 90.0f);
            wheelNode.SetRotation(rotation);
            wheelNode.SetWorldPosition(node.WorldPosition + node.WorldRotation * connectionPoint);

            var wheel = wheelNode.GetOrCreateComponent<RaycastVehicleWheel>();
            wheel.ConnectionPoint = connectionPoint;
            wheel.Direction = wheelDirection;
            wheel.Rotation = rotation;
            wheel.Axle = wheelAxle;
            wheel.SuspensionRestLength = _suspensionRestLength;
            wheel.Radius = _wheelRadius;
            wheel.SteeringFactor = isFrontWheel ? 1.0f : 0.0f;
            wheel.EngineFactor = isFrontWheel ? 0.0f : 1.0f;
            wheel.SuspensionStiffness = _suspensionStiffness;
            wheel.DampingRelaxation = _suspensionDamping;
            wheel.DampingCompression = _suspensionCompression;
            wheel.FrictionSlip = _wheelFriction;
            wheel.RollInfluence = _rollInfluence;

            wheelNode.SetScale(new Vector3f(1.0f, 0.65f, 1.0f));
            var wheelObject = wheelNode.CreateComponent<StaticModel>();
            wheelObject.SetModel(GetSubsystem<ResourceCache>()!.Get<Model>("Models/Cylinder.mdl"));
            wheelObject.SetMaterial(GetSubsystem<ResourceCache>()!.Get<Material>("Materials/Stone.xml"));
            wheelObject.SetCastShadows(true);
            CreateEmitter(connectionPoint);
        }

        _emittersCreated = true;
        vehicle.ResetWheels();
    }

    public override void ApplyAttributes()
    {
        Node?.GetOrCreateComponent<RedBolt.PhysicsJolt.RaycastVehicle>();
        if (_emittersCreated)
            return;
        foreach (var connectionPoint in _connectionPoints)
            CreateEmitter(connectionPoint);
        _emittersCreated = true;
    }

    public override void FixedUpdate(float timeStep)
    {
        if (Node?.GetComponent<RedBolt.PhysicsJolt.RaycastVehicle>() is not { } vehicle || _inputMap is null)
            return;

        var velocity = GetVelocity();
        var newSteering = velocity.X;
        var accelerator = velocity.Z;
        if (accelerator < 0.0f)
            accelerator *= 0.5f;
        var brakingForce = _inputMap.Evaluate("Brake") != 0.0f ? 1.0f : 0.0f;

        _steering = newSteering != 0.0f
            ? _steering * 0.95f + newSteering * 0.05f
            : _steering * 0.8f + newSteering * 0.2f;
        vehicle.UpdateInput(_steering, accelerator, brakingForce);
    }

    public override void PostUpdate(float timeStep)
    {
        if (Node?.GetComponent<RedBolt.PhysicsJolt.RaycastVehicle>() is not { } vehicle ||
            Node.GetComponent<RigidBody>() is not { } vehicleBody || timeStep <= 0.0f)
        {
            return;
        }

        var velocity = vehicleBody.LinearVelocity;
        var acceleration = (velocity - _previousVelocity) / timeStep;
        var planeAcceleration = new Vector3f(acceleration.X, 0.0f, acceleration.Z).Length;
        for (var i = 0; i < vehicle.NumWheels && i < _particleEmitterNodes.Count; ++i)
        {
            var emitterNode = _particleEmitterNodes[i];
            var wheel = vehicle.GetWheel(i)!;
            var emitter = emitterNode.GetComponent<ParticleEmitter>()!;
            if (wheel.InContact &&
                (wheel.SkidInfoCumulative < 0.9f || wheel.BrakeValue > 2.0f || planeAcceleration > 15.0f))
            {
                emitterNode.SetWorldPosition(wheel.ContactPosition);
                if (!emitter.IsEmitting())
                    emitter.SetEmitting(true);
                Log.GetLogger().LogDebug(
                    "GetWheelSkidInfoCumulative() = {SkidInfo} {MaxSideSlipSpeed}",
                    wheel.SkidInfoCumulative, vehicle.MaxSideSlipSpeed);
            }
            else if (emitter.IsEmitting())
                emitter.SetEmitting(false);
        }
        _previousVelocity = velocity;
    }

    public override void SetPitch(float pitch)
    {
        base.SetPitch(MathDefs.Clamp(pitch, 0.0f, 80.0f));
    }

    private void CreateEmitter(Vector3f place)
    {
        if (Scene is null || Node is null)
            return;
        var emitterNode = Scene.CreateChild();
        emitterNode.SetWorldPosition(
            Node.WorldPosition + Node.WorldRotation * place + new Vector3f(0.0f, -_wheelRadius, 0.0f));
        var emitter = emitterNode.CreateComponent<ParticleEmitter>();
        emitter.SetEffect(GetSubsystem<ResourceCache>()!.Get<ParticleEffect>("Particle/Dust.xml"));
        emitter.SetEmitting(false);
        _particleEmitterNodes.Add(emitterNode);
        emitterNode.SetTemporary(true);
    }

    private void SubscribeToPhysicsWorld()
    {
        var physicsWorld = Scene?.GetComponent<PhysicsWorld>();
        if (ReferenceEquals(physicsWorld, _physicsWorld) || physicsWorld is null)
            return;
        if (_physicsWorld is not null)
            UnsubscribeFromEvent<PhysicsEvents.PhysicsPreStepEvent>(_physicsWorld);
        _physicsWorld = physicsWorld;
        SubscribeToEvent<PhysicsEvents.PhysicsPreStepEvent>(_physicsWorld, HandlePhysicsPreStep);
    }

    private void HandlePhysicsPreStep(ref PhysicsEvents.PhysicsPreStepEvent eventData)
    {
        if (IsEnabledEffective)
            FixedUpdate(eventData.TimeStep);
    }

    private static object GetInputMapAttributeDefault() =>
        new ResourceRef(ObjectReflection<InputMap>.TypeId);
}
