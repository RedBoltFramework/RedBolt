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

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Wheel component used by <see cref="RaycastVehicle"/>.</summary>
[Reflected(ObjectCategory.Category_Physics, InheritAttributes = false)]
public partial class RaycastVehicleWheel : Component
{
    public const float DefaultWheelRadius = 1.0f;
    public const float DefaultSuspensionRestLength = 0.2f;
    public const float DefaultMaxSuspensionTravel = 0.5f;
    public const float DefaultSuspensionStiffness = 5.88f;
    public const float DefaultSuspensionCompression = 0.83f;
    public const float DefaultSuspensionDamping = 0.88f;
    public const float DefaultFrictionSlip = 10.5f;
    public const float DefaultMaxSuspensionForce = 6000.0f;
    public const float DefaultSkidInfoCumulative = 1.0f;
    public const float DefaultSideSlipSpeed = 0.0f;
    public const float DefaultRollInfluence = 0.1f;
    public const float DefaultSteeringFactor = 0.0f;
    public const float DefaultBrakeFactor = 1.0f;
    public const float DefaultEngineFactor = 1.0f;
    public const float DefaultSlidingFactor = 1.0f;

    public static readonly Vector3f DefaultWheelDirection = new(0.0f, -1.0f, 0.0f);
    public static readonly Vector3f DefaultWheelAxle = new(1.0f, 0.0f, 0.0f);

    private Vector3f _connectionPoint;
    private Vector3f _direction = DefaultWheelDirection;
    private Vector3f _axle = DefaultWheelAxle;
    private Vector3f _offset;
    private Quaternion _rotation = Quaternion.IDENTITY;
    private float _steeringValue;
    private float _brake;
    private float _engineForce;
    private float _rollInfluence = DefaultRollInfluence;
    private float _radius = DefaultWheelRadius;
    private float _suspensionRestLength = DefaultSuspensionRestLength;
    private float _maxSuspensionTravel = DefaultMaxSuspensionTravel;
    private float _suspensionStiffness = DefaultSuspensionStiffness;
    private float _dampingCompression = DefaultSuspensionCompression;
    private float _dampingRelaxation = DefaultSuspensionDamping;
    private float _frictionSlip = DefaultFrictionSlip;
    private float _maxSuspensionForce = DefaultMaxSuspensionForce;
    private float _steeringFactor = DefaultSteeringFactor;
    private float _engineFactor = DefaultEngineFactor;
    private float _brakeFactor = DefaultBrakeFactor;
    private Vector3f _contactPosition;
    private Vector3f _contactNormal;
    private float _slidingFactor = DefaultSlidingFactor;
    private float _skidInfoCumulative = DefaultSkidInfoCumulative;
    private float _sideSlipSpeed = DefaultSideSlipSpeed;
    private bool _isInContact;
    private RaycastVehicle? _vehicle;

    /// <summary>Wheel index in the vehicle, or -1 if not attached.</summary>
    public int WheelIndex { get; set; } = -1;

    /// <summary>Wheel connection point in vehicle space.</summary>
    [Serialized("Connection Point", DefaultSource = nameof(GetConnectionPointAttributeDefault))]
    public Vector3f ConnectionPoint
    {
        get => _connectionPoint;
        set
        {
            if (_connectionPoint == value)
                return;
            _connectionPoint = value;
            InvalidateStaticParameters();
        }
    }

    /// <summary>Wheel node position relative to connection point.</summary>
    [Serialized("Offset")]
    public Vector3f Offset
    {
        get => _offset;
        set
        {
            if (_offset == value)
                return;
            _offset = value;
            InvalidateStaticParameters();
        }
    }

    /// <summary>Wheel node rotation relative to connection point.</summary>
    [Serialized("Rotation")]
    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value)
                return;
            _rotation = value;
            InvalidateStaticParameters();
        }
    }

    [Callable("Set Connection", Label = "Update connection point and rotation")]
    private void SetConnectionAction()
    {
        ConnectionPointFromTransform();
    }

    /// <summary>Wheel direction vector.</summary>
    [Serialized("Direction")]
    public Vector3f Direction
    {
        get => _direction;
        set
        {
            if (_direction == value)
                return;
            _direction = value;
            InvalidateStaticParameters();
        }
    }

    /// <summary>Wheel axle vector.</summary>
    [Serialized("Axle")]
    public Vector3f Axle
    {
        get => _axle;
        set
        {
            if (_axle == value)
                return;
            _axle = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Roll Influence")]
    public float RollInfluence
    {
        get => _rollInfluence;
        set
        {
            if (_rollInfluence == value)
                return;
            _rollInfluence = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Radius")]
    public float Radius
    {
        get => _radius;
        set
        {
            if (_radius == value)
                return;
            _radius = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Steering Factor")]
    public float SteeringFactor
    {
        get => _steeringFactor;
        set
        {
            if (_steeringFactor == value)
                return;
            _steeringFactor = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Engine Factor")]
    public float EngineFactor
    {
        get => _engineFactor;
        set
        {
            if (_engineFactor == value)
                return;
            _engineFactor = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Brake Factor")]
    public float BrakeFactor
    {
        get => _brakeFactor;
        set
        {
            if (_brakeFactor == value)
                return;
            _brakeFactor = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Suspension Rest Length")]
    public float SuspensionRestLength
    {
        get => _suspensionRestLength;
        set
        {
            if (_suspensionRestLength == value)
                return;
            _suspensionRestLength = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Max Suspension Travel")]
    public float MaxSuspensionTravel
    {
        get => _maxSuspensionTravel;
        set
        {
            if (_maxSuspensionTravel == value)
                return;
            _maxSuspensionTravel = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Suspension Stiffness")]
    public float SuspensionStiffness
    {
        get => _suspensionStiffness;
        set
        {
            if (_suspensionStiffness == value)
                return;
            _suspensionStiffness = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Damping Compression")]
    public float DampingCompression
    {
        get => _dampingCompression;
        set
        {
            if (_dampingCompression == value)
                return;
            _dampingCompression = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Damping Relaxation")]
    public float DampingRelaxation
    {
        get => _dampingRelaxation;
        set
        {
            if (_dampingRelaxation == value)
                return;
            _dampingRelaxation = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Friction Slip")]
    public float FrictionSlip
    {
        get => _frictionSlip;
        set
        {
            if (_frictionSlip == value)
                return;
            _frictionSlip = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Max Suspension Force")]
    public float MaxSuspensionForce
    {
        get => _maxSuspensionForce;
        set
        {
            if (_maxSuspensionForce == value)
                return;
            _maxSuspensionForce = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Sliding Factor")]
    public float SlidingFactor
    {
        get => _slidingFactor;
        set
        {
            if (_slidingFactor == value)
                return;
            _slidingFactor = value;
            InvalidateStaticParameters();
        }
    }

    [Serialized("Skid Info")]
    public float SkidInfoCumulative
    {
        get => _skidInfoCumulative;
        set => _skidInfoCumulative = value;
    }

    [Serialized("Steering")]
    public float SteeringValue
    {
        get => _steeringValue;
        set
        {
            if (_steeringValue == value)
                return;
            _steeringValue = value;
            InvalidateDynamicParameters();
        }
    }

    [Serialized("Brake")]
    public float BrakeValue
    {
        get => _brake;
        set
        {
            if (_brake == value)
                return;
            _brake = value;
            InvalidateDynamicParameters();
        }
    }

    [Serialized("Engine Force")]
    public float EngineForce
    {
        get => _engineForce;
        set
        {
            if (_engineForce == value)
                return;
            _engineForce = value;
            InvalidateDynamicParameters();
        }
    }

    [Serialized("Side Slip Speed")]
    public float SideSlipSpeed
    {
        get => _sideSlipSpeed;
        set => _sideSlipSpeed = value;
    }

    [Serialized("Is In Contact")]
    public bool InContact
    {
        get => _isInContact;
        set => _isInContact = value;
    }

    [Serialized("Contact Position")]
    public Vector3f ContactPosition
    {
        get => _contactPosition;
        set => _contactPosition = value;
    }

    [Serialized("Contact Normal")]
    public Vector3f ContactNormal
    {
        get => _contactNormal;
        set => _contactNormal = value;
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        _vehicle?.DrawWheelDebugGeometry(WheelIndex, debug, false);
    }

    public override void ApplyAttributes()
    {
        UpdateWheelAtVehicle();
        _vehicle?.ApplyWheelAttributes(WheelIndex);
        base.ApplyAttributes();
    }

    /// <summary>Update connection point and rotation from the wheel node transform.</summary>
    public void ConnectionPointFromTransform()
    {
        if (TryGetNode() is not { } node || _vehicle?.TryGetNode() is not { } vehicleNode ||
            vehicleNode.GetComponent<RigidBody>() is null)
            return;

        var centerOfMassTransform = new Matrix3x4(vehicleNode.WorldPosition, vehicleNode.WorldRotation, 1.0f);
        var worldToCarBody = centerOfMassTransform.Inverse;
        var wheelToVehicle = worldToCarBody * node.GetWorldTransform();
        var worldSpaceOffset = node.GetWorldTransform() * _offset;
        var worldSpaceConnection = worldSpaceOffset - _direction * _suspensionRestLength;
        ConnectionPoint = worldToCarBody * worldSpaceConnection;
        Rotation = wheelToVehicle.Rotation;
        UpdateWheelAtVehicle();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        UpdateWheelAtVehicle();
        base.OnNodeSet(previousNode, currentNode);
    }

    private void UpdateWheelAtVehicle()
    {
        var vehicle = TryGetNode()?.FindComponent<RaycastVehicle>(ComponentSearchFlag.ParentRecursive);
        if (ReferenceEquals(vehicle, _vehicle))
            return;
        _vehicle?.RemoveWheel(this);
        _vehicle = vehicle;
        _vehicle?.AddWheel(this);
    }

    private void InvalidateStaticParameters()
    {
        _vehicle?.InvalidateStaticWheelParameters(WheelIndex);
    }

    private void InvalidateDynamicParameters()
    {
        _vehicle?.InvalidateDynamicWheelParameters(WheelIndex);
    }

    private static object? GetConnectionPointAttributeDefault() => DefaultWheelDirection;
}
