// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Bindings.Box2D;
using RedBolt.Core;

/// <summary>2D distance constraint.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintDistance2D : Constraint2D
{
    private Vector2f _ownerBodyAnchor;
    private Vector2f _otherBodyAnchor;
    private float _frequencyHz;
    private float _dampingRatio;
    private float _length = 1.0f;

    [Serialized("Owner Body Anchor")]
    public Vector2f OwnerBodyAnchor { get => _ownerBodyAnchor; set { _ownerBodyAnchor = value; RecreateJoint(); } }
    [Serialized("Other Body Anchor")]
    public Vector2f OtherBodyAnchor { get => _otherBodyAnchor; set { _otherBodyAnchor = value; RecreateJoint(); } }
    [Serialized("Frequency Hz")]
    public float FrequencyHz { get => _frequencyHz; set { _frequencyHz = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Damping Ratio")]
    public float DampingRatio { get => _dampingRatio; set { _dampingRatio = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Length")]
    public float Length { get => _length; set { _length = MathF.Max(value, 0.0f); RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultDistanceJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        var anchorA = ToLocalPoint(ownerBody, _ownerBodyAnchor);
        var anchorB = ToLocalPoint(otherBody, _otherBodyAnchor);
        def.LocalAnchorAX = anchorA.X;
        def.LocalAnchorAY = anchorA.Y;
        def.LocalAnchorBX = anchorB.X;
        def.LocalAnchorBY = anchorB.Y;
        def.Length = _length;
        def.MinLength = _length;
        def.MaxLength = _length;
        def.EnableSpring = _frequencyHz > 0.0f;
        def.Hertz = _frequencyHz;
        def.DampingRatio = _dampingRatio;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreateDistanceJoint(worldId, def);
    }
}

/// <summary>2D friction constraint.</summary>
/// <remarks>Box2D 3 removed friction joints. This uses a zero-correction motor joint.</remarks>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintFriction2D : Constraint2D
{
    private Vector2f _anchor;
    private float _maxForce;
    private float _maxTorque;

    [Serialized("Anchor")]
    public Vector2f Anchor { get => _anchor; set { _anchor = value; RecreateJoint(); } }
    [Serialized("Max Force")]
    public float MaxForce { get => _maxForce; set { _maxForce = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Max Torque")]
    public float MaxTorque { get => _maxTorque; set { _maxTorque = MathF.Max(value, 0.0f); RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultMotorJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        def.MaxForce = _maxForce;
        def.MaxTorque = _maxTorque;
        def.CorrectionFactor = 0.0f;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreateMotorJoint(worldId, def);
    }
}

/// <summary>2D motor constraint.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintMotor2D : Constraint2D
{
    private Vector2f _linearOffset;
    private float _angularOffset;
    private float _maxForce = 1.0f;
    private float _maxTorque = 1.0f;
    private float _correctionFactor = 0.3f;

    [Serialized("Linear Offset")]
    public Vector2f LinearOffset { get => _linearOffset; set { _linearOffset = value; RecreateJoint(); } }
    [Serialized("Angular Offset")]
    public float AngularOffset { get => _angularOffset; set { _angularOffset = value; RecreateJoint(); } }
    [Serialized("Max Force")]
    public float MaxForce { get => _maxForce; set { _maxForce = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Max Torque")]
    public float MaxTorque { get => _maxTorque; set { _maxTorque = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Correction Factor")]
    public float CorrectionFactor { get => _correctionFactor; set { _correctionFactor = System.Math.Clamp(value, 0.0f, 1.0f); RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultMotorJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        def.LinearOffsetX = _linearOffset.X;
        def.LinearOffsetY = _linearOffset.Y;
        def.AngularOffset = _angularOffset;
        def.MaxForce = _maxForce;
        def.MaxTorque = _maxTorque;
        def.CorrectionFactor = _correctionFactor;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreateMotorJoint(worldId, def);
    }
}

/// <summary>2D mouse constraint.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintMouse2D : Constraint2D
{
    private Vector2f _target;
    private float _maxForce;
    private float _frequencyHz = 5.0f;
    private float _dampingRatio = 0.7f;

    [Serialized("Target")]
    public Vector2f Target { get => _target; set { _target = value; RecreateJoint(); } }
    [Serialized("Max Force")]
    public float MaxForce { get => _maxForce; set { _maxForce = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Frequency Hz")]
    public float FrequencyHz { get => _frequencyHz; set { _frequencyHz = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Damping Ratio")]
    public float DampingRatio { get => _dampingRatio; set { _dampingRatio = MathF.Max(value, 0.0f); RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultMouseJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        def.TargetX = _target.X;
        def.TargetY = _target.Y;
        def.MaxForce = _maxForce;
        def.Hertz = _frequencyHz;
        def.DampingRatio = _dampingRatio;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreateMouseJoint(worldId, def);
    }
}

/// <summary>2D prismatic constraint.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintPrismatic2D : Constraint2D
{
    private Vector2f _anchor;
    private Vector2f _axis = new(1.0f, 0.0f);
    private bool _enableLimit;
    private float _lowerTranslation;
    private float _upperTranslation;
    private bool _enableMotor;
    private float _maxMotorForce = 2.0f;
    private float _motorSpeed = 0.7f;

    [Serialized("Anchor")]
    public Vector2f Anchor { get => _anchor; set { _anchor = value; RecreateJoint(); } }
    [Serialized("Axis")]
    public Vector2f Axis { get => _axis; set { _axis = value; RecreateJoint(); } }
    [Serialized("Enable Limit")]
    public bool EnableLimit { get => _enableLimit; set { _enableLimit = value; RecreateJoint(); } }
    [Serialized("Lower translation")]
    public float LowerTranslation { get => _lowerTranslation; set { _lowerTranslation = value; RecreateJoint(); } }
    [Serialized("Upper translation")]
    public float UpperTranslation { get => _upperTranslation; set { _upperTranslation = value; RecreateJoint(); } }
    [Serialized("Enable Motor")]
    public bool EnableMotor { get => _enableMotor; set { _enableMotor = value; RecreateJoint(); } }
    [Serialized("Max Motor Force")]
    public float MaxMotorForce { get => _maxMotorForce; set { _maxMotorForce = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Motor Speed")]
    public float MotorSpeed { get => _motorSpeed; set { _motorSpeed = value; RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultPrismaticJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        var anchorA = ToLocalPoint(ownerBody, _anchor);
        var anchorB = ToLocalPoint(otherBody, _anchor);
        var axis = ToLocalVector(ownerBody, _axis).NormalizedOrDefault(new Vector2f(1.0f, 0.0f));
        def.LocalAnchorAX = anchorA.X;
        def.LocalAnchorAY = anchorA.Y;
        def.LocalAnchorBX = anchorB.X;
        def.LocalAnchorBY = anchorB.Y;
        def.LocalAxisAX = axis.X;
        def.LocalAxisAY = axis.Y;
        def.ReferenceAngle = GetReferenceAngle(ownerBody, otherBody);
        def.EnableLimit = _enableLimit;
        def.LowerTranslation = _lowerTranslation;
        def.UpperTranslation = _upperTranslation;
        def.EnableMotor = _enableMotor;
        def.MaxMotorForce = _maxMotorForce;
        def.MotorSpeed = _motorSpeed;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreatePrismaticJoint(worldId, def);
    }
}

/// <summary>2D revolute constraint.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintRevolute2D : Constraint2D
{
    private Vector2f _anchor;
    private bool _enableLimit;
    private float _lowerAngle;
    private float _upperAngle;
    private bool _enableMotor;
    private float _motorSpeed;
    private float _maxMotorTorque;

    [Serialized("Anchor")]
    public Vector2f Anchor { get => _anchor; set { _anchor = value; RecreateJoint(); } }
    [Serialized("Enable Limit")]
    public bool EnableLimit { get => _enableLimit; set { _enableLimit = value; RecreateJoint(); } }
    [Serialized("Lower Angle")]
    public float LowerAngle { get => _lowerAngle; set { _lowerAngle = value; RecreateJoint(); } }
    [Serialized("Upper Angle")]
    public float UpperAngle { get => _upperAngle; set { _upperAngle = value; RecreateJoint(); } }
    [Serialized("Enable Motor")]
    public bool EnableMotor { get => _enableMotor; set { _enableMotor = value; RecreateJoint(); } }
    [Serialized("Motor Speed")]
    public float MotorSpeed { get => _motorSpeed; set { _motorSpeed = value; RecreateJoint(); } }
    [Serialized("Max Motor Torque")]
    public float MaxMotorTorque { get => _maxMotorTorque; set { _maxMotorTorque = MathF.Max(value, 0.0f); RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultRevoluteJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        var anchorA = ToLocalPoint(ownerBody, _anchor);
        var anchorB = ToLocalPoint(otherBody, _anchor);
        def.LocalAnchorAX = anchorA.X;
        def.LocalAnchorAY = anchorA.Y;
        def.LocalAnchorBX = anchorB.X;
        def.LocalAnchorBY = anchorB.Y;
        def.ReferenceAngle = GetReferenceAngle(ownerBody, otherBody);
        def.EnableLimit = _enableLimit;
        def.LowerAngle = _lowerAngle;
        def.UpperAngle = _upperAngle;
        def.EnableMotor = _enableMotor;
        def.MotorSpeed = _motorSpeed;
        def.MaxMotorTorque = _maxMotorTorque;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreateRevoluteJoint(worldId, def);
    }
}

/// <summary>2D rope constraint.</summary>
/// <remarks>Box2D 3 implements this as a distance joint with only an upper limit.</remarks>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintRope2D : Constraint2D
{
    private Vector2f _ownerBodyAnchor;
    private Vector2f _otherBodyAnchor;
    private float _maxLength;

    [Serialized("Owner Body Anchor")]
    public Vector2f OwnerBodyAnchor { get => _ownerBodyAnchor; set { _ownerBodyAnchor = value; RecreateJoint(); } }
    [Serialized("Other Body Anchor")]
    public Vector2f OtherBodyAnchor { get => _otherBodyAnchor; set { _otherBodyAnchor = value; RecreateJoint(); } }
    [Serialized("Max Length")]
    public float MaxLength { get => _maxLength; set { _maxLength = MathF.Max(value, 0.0f); RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultDistanceJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        var anchorA = ToLocalPoint(ownerBody, _ownerBodyAnchor);
        var anchorB = ToLocalPoint(otherBody, _otherBodyAnchor);
        def.LocalAnchorAX = anchorA.X;
        def.LocalAnchorAY = anchorA.Y;
        def.LocalAnchorBX = anchorB.X;
        def.LocalAnchorBY = anchorB.Y;
        def.Length = MathF.Max(_maxLength, 0.005f);
        def.MinLength = 0.005f;
        def.MaxLength = MathF.Max(_maxLength, 0.005f);
        def.EnableLimit = true;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreateDistanceJoint(worldId, def);
    }
}

/// <summary>2D weld constraint.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintWeld2D : Constraint2D
{
    private Vector2f _anchor;
    private float _frequencyHz;
    private float _dampingRatio;

    [Serialized("Anchor")]
    public Vector2f Anchor { get => _anchor; set { _anchor = value; RecreateJoint(); } }
    [Serialized("Frequency Hz")]
    public float FrequencyHz { get => _frequencyHz; set { _frequencyHz = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Damping Ratio")]
    public float DampingRatio { get => _dampingRatio; set { _dampingRatio = MathF.Max(value, 0.0f); RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultWeldJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        var anchorA = ToLocalPoint(ownerBody, _anchor);
        var anchorB = ToLocalPoint(otherBody, _anchor);
        def.LocalAnchorAX = anchorA.X;
        def.LocalAnchorAY = anchorA.Y;
        def.LocalAnchorBX = anchorB.X;
        def.LocalAnchorBY = anchorB.Y;
        def.ReferenceAngle = GetReferenceAngle(ownerBody, otherBody);
        def.LinearHertz = _frequencyHz;
        def.AngularHertz = _frequencyHz;
        def.LinearDampingRatio = _dampingRatio;
        def.AngularDampingRatio = _dampingRatio;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreateWeldJoint(worldId, def);
    }
}

/// <summary>2D wheel constraint.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class ConstraintWheel2D : Constraint2D
{
    private Vector2f _anchor;
    private Vector2f _axis = new(1.0f, 0.0f);
    private bool _enableMotor;
    private float _maxMotorTorque;
    private float _motorSpeed;
    private float _frequencyHz = 2.0f;
    private float _dampingRatio = 0.7f;

    [Serialized("Anchor")]
    public Vector2f Anchor { get => _anchor; set { _anchor = value; RecreateJoint(); } }
    [Serialized("Axis")]
    public Vector2f Axis { get => _axis; set { _axis = value; RecreateJoint(); } }
    [Serialized("Enable Motor")]
    public bool EnableMotor { get => _enableMotor; set { _enableMotor = value; RecreateJoint(); } }
    [Serialized("Max Motor Torque")]
    public float MaxMotorTorque { get => _maxMotorTorque; set { _maxMotorTorque = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Motor Speed")]
    public float MotorSpeed { get => _motorSpeed; set { _motorSpeed = value; RecreateJoint(); } }
    [Serialized("Frequency Hz")]
    public float FrequencyHz { get => _frequencyHz; set { _frequencyHz = MathF.Max(value, 0.0f); RecreateJoint(); } }
    [Serialized("Damping Ratio")]
    public float DampingRatio { get => _dampingRatio; set { _dampingRatio = MathF.Max(value, 0.0f); RecreateJoint(); } }

    protected override B2JointId CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var def = Box2DNativeBindings.B2DefaultWheelJointDef();
        PhysicsUtils2D.SetBodyA(def, ownerBody);
        PhysicsUtils2D.SetBodyB(def, otherBody);
        var anchorA = ToLocalPoint(ownerBody, _anchor);
        var anchorB = ToLocalPoint(otherBody, _anchor);
        var axis = ToLocalVector(ownerBody, _axis).NormalizedOrDefault(new Vector2f(1.0f, 0.0f));
        def.LocalAnchorAX = anchorA.X;
        def.LocalAnchorAY = anchorA.Y;
        def.LocalAnchorBX = anchorB.X;
        def.LocalAnchorBY = anchorB.Y;
        def.LocalAxisAX = axis.X;
        def.LocalAxisAY = axis.Y;
        def.EnableMotor = _enableMotor;
        def.MaxMotorTorque = _maxMotorTorque;
        def.MotorSpeed = _motorSpeed;
        def.EnableSpring = _frequencyHz > 0.0f;
        def.Hertz = _frequencyHz;
        def.DampingRatio = _dampingRatio;
        def.CollideConnected = ShouldCollideConnected;
        return Box2DNativeBindings.B2CreateWheelJoint(worldId, def);
    }
}
