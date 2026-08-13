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

using SixDofAxis = JPH.SixDOFConstraintSettings.EAxis;

using Color = RedBolt.Math.Color;
using JoltConstraint = RedBolt.Bindings.Jolt.Constraint;
using RedBolt.Bindings.Jolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Supported constraint types.</summary>
public enum ConstraintType
{
    Point = 0,
    Hinge,
    Slider,
    ConeTwist
}

/// <summary>Physics constraint component. Connects two rigid bodies together, or one rigid body to a static point.</summary>
[Reflected(ObjectCategory.Category_Physics, InheritAttributes = false)]
public partial class Constraint : Component
{
    private WeakReference<PhysicsWorld>? _physicsWorld;
    private RigidBody? _ownBody;
    private RigidBody? _otherBody;
    private JoltConstraint? _constraint;
    [Serialized("Constraint Type", EnumNames = ["Point", "Hinge", "Slider", "ConeTwist"],
        Setter = nameof(SetConstraintTypeAttribute))]
    private ConstraintType _constraintType;
    private Vector3f _position;
    private Quaternion _rotation = Quaternion.IDENTITY;
    private Vector3f _otherPosition;
    private Quaternion _otherRotation = Quaternion.IDENTITY;
    private Vector3f _cachedWorldScale;
    private Vector2f _highLimit;
    private Vector2f _lowLimit;
    private float _erp;
    private float _cfm;
    [Serialized("Other Body NodeID", Mode = AttributeMode.Default | AttributeMode.NodeId,
        Setter = nameof(SetOtherBodyNodeId))]
    private uint _otherBodyNodeId;
    private bool _disableCollision;
    private bool _recreateConstraint = true;
    private bool _framesDirty;
    private bool _retryCreation = true;
    private (uint Body1, uint Body2)? _disabledCollisionPair;

    /// <summary>Construct.</summary>
    public Constraint()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        ReleaseConstraint();
        if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var physicsWorld))
            physicsWorld.RemoveConstraint(this);
        _node?.RemoveListener(this);
        base.Dispose();
    }

    /// <summary>Apply attribute changes that cannot be applied immediately.</summary>
    public override void ApplyAttributes()
    {
        if (_recreateConstraint)
        {
            _otherBody?.RemoveConstraint(this);
            _otherBody = null;

            var scene = Scene;
            if (scene is not null && _otherBodyNodeId != 0)
                _otherBody = scene.GetNode(_otherBodyNodeId)?.GetComponent<RigidBody>();

            CreateConstraint();
        }
        else if (_framesDirty)
        {
            ApplyFrames();
            _framesDirty = false;
        }
    }

    /// <summary>Handle enabled or disabled state change.</summary>
    public override void OnSetEnabled()
    {
        _constraint?.SetEnabled(IsEnabledEffective);
    }

    /// <summary>Return the depended on nodes to order network updates.</summary>
    public override void GetDependencyNodes(ICollection<Node> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (_otherBody?.TryGetNode() is { } otherNode)
            destination.Add(otherNode);
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && _constraint is not null)
            debug.AddCross(WorldPosition, 0.1f, Color.White, depthTest);
    }

    /// <summary>Return or set constraint type and recreate the constraint.</summary>
    public ConstraintType ConstraintType
    {
        get => _constraintType;
        set
        {
            if (value == _constraintType && _constraint is not null)
                return;
            _constraintType = value;
            CreateConstraint();
        }
    }

    /// <summary>Return or set other body to connect to. Null connects to the static world.</summary>
    public RigidBody? OtherBody
    {
        get => _otherBody;
        set
        {
            if (ReferenceEquals(value, _otherBody))
                return;

            _otherBody?.RemoveConstraint(this);
            _otherBody = value;
            _otherBodyNodeId = value?.TryGetNode()?.Id ?? 0;
            CreateConstraint();
        }
    }

    /// <summary>Return or set constraint position relative to own body.</summary>
    [Serialized("Position", Setter = nameof(SetPositionAttribute))]
    public Vector3f Position
    {
        get => _position;
        set
        {
            if (value == _position)
                return;
            _position = value;
            ApplyFrames();
        }
    }

    /// <summary>Return or set constraint rotation relative to own body.</summary>
    [Serialized("Rotation")]
    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            if (value == _rotation)
                return;
            _rotation = value;
            ApplyFrames();
        }
    }

    /// <summary>Set constraint rotation relative to own body by specifying the axis.</summary>
    public void SetAxis(Vector3f axis)
    {
        switch (_constraintType)
        {
            case ConstraintType.Point:
            case ConstraintType.Hinge:
                _rotation = new Quaternion(Vector3f.Forward, axis);
                break;

            case ConstraintType.Slider:
            case ConstraintType.ConeTwist:
                _rotation = new Quaternion(Vector3f.Right, axis);
                break;
        }

        ApplyFrames();
    }

    /// <summary>Return or set position relative to the other body, or in world space when connected to the static world.</summary>
    [Serialized("Other Body Position")]
    public Vector3f OtherPosition
    {
        get => _otherPosition;
        set
        {
            if (value == _otherPosition)
                return;
            _otherPosition = value;
            ApplyFrames();
        }
    }

    /// <summary>Return or set rotation relative to the other body, or in world space when connected to the static world.</summary>
    [Serialized("Other Body Rotation")]
    public Quaternion OtherRotation
    {
        get => _otherRotation;
        set
        {
            if (value == _otherRotation)
                return;
            _otherRotation = value;
            ApplyFrames();
        }
    }

    /// <summary>Set constraint rotation relative to the other body by specifying the axis.</summary>
    public void SetOtherAxis(Vector3f axis)
    {
        switch (_constraintType)
        {
            case ConstraintType.Point:
            case ConstraintType.Hinge:
                _otherRotation = new Quaternion(Vector3f.Forward, axis);
                break;

            case ConstraintType.Slider:
            case ConstraintType.ConeTwist:
                _otherRotation = new Quaternion(Vector3f.Right, axis);
                break;
        }

        ApplyFrames();
    }

    /// <summary>Return or set constraint world-space position and zero the constraint error.</summary>
    public Vector3f WorldPosition
    {
        get => _ownBody is not null
            ? _ownBody.Position + _ownBody.Rotation * (_position * _cachedWorldScale)
            : Vector3f.Zero;
        set
        {
            if (_constraint is null || _ownBody is null)
            {
                Log.GetLogger().Warning("Constraint not created, world position could not be stored");
                return;
            }

            _position = (_ownBody.Rotation.Inverse * (value - _ownBody.Position)) / _cachedWorldScale;
            _otherPosition = _otherBody is not null
                ? (_otherBody.Rotation.Inverse * (value - _otherBody.Position)) / _otherBody.Node.WorldScale
                : value;
            ApplyFrames();
        }
    }

    /// <summary>Return or set high limit. Interpretation is constraint-type specific.</summary>
    [Serialized("High Limit")]
    public Vector2f HighLimit
    {
        get => _highLimit;
        set
        {
            if (value == _highLimit)
                return;
            _highLimit = value;
            ApplyLimits();
        }
    }

    /// <summary>Return or set low limit. Interpretation is constraint-type specific.</summary>
    [Serialized("Low Limit")]
    public Vector2f LowLimit
    {
        get => _lowLimit;
        set
        {
            if (value == _lowLimit)
                return;
            _lowLimit = value;
            ApplyLimits();
        }
    }

    /// <summary>Return or set constraint error reduction parameter. Zero leaves the Jolt default.</summary>
    [Serialized("ERP Parameter")]
    public float ERP
    {
        get => _erp;
        set
        {
            value = MathF.Max(value, 0.0f);
            if (value == _erp)
                return;
            _erp = value;
            ApplyLimits();
        }
    }

    /// <summary>Return or set constraint force mixing parameter. Zero leaves the Jolt default.</summary>
    [Serialized("CFM Parameter")]
    public float CFM
    {
        get => _cfm;
        set
        {
            value = MathF.Max(value, 0.0f);
            if (value == _cfm)
                return;
            _cfm = value;
            ApplyLimits();
        }
    }

    /// <summary>Return or set whether collisions between connected bodies are disabled.</summary>
    [Serialized("Disable Collision")]
    public bool DisableCollision
    {
        get => _disableCollision;
        set
        {
            if (value == _disableCollision)
                return;
            _disableCollision = value;
            CreateConstraint();
        }
    }

    /// <summary>Return physics world.</summary>
    public PhysicsWorld? PhysicsWorld =>
        _physicsWorld is not null && _physicsWorld.TryGetTarget(out var physicsWorld) ? physicsWorld : null;

    /// <summary>Return Jolt constraint.</summary>
    public JoltConstraint? JoltConstraint => _constraint;

    /// <summary>Return rigid body in own scene node.</summary>
    public RigidBody? OwnBody => _ownBody;

    /// <summary>Release the constraint.</summary>
    public void ReleaseConstraint()
    {
        if (_disabledCollisionPair is { } pair && PhysicsWorld is { } collisionWorld)
        {
            collisionWorld.EnableCollision(pair.Body1, pair.Body2);
            _disabledCollisionPair = null;
        }

        if (_constraint is null)
            return;

        _ownBody?.RemoveConstraint(this);
        _otherBody?.RemoveConstraint(this);

        if (PhysicsWorld is { } physicsWorld)
            physicsWorld.World.RemoveConstraint(_constraint);

        _constraint.Dispose();
        _constraint = null;
    }

    /// <summary>Apply constraint frames.</summary>
    public void ApplyFrames()
    {
        if (_constraint is null || _node is null || (_otherBody is not null && _otherBody.TryGetNode() is null))
            return;

        _cachedWorldScale = _node.WorldScale;
        CreateConstraint();
    }

    internal void ReleaseConstraintForBodyRecreation()
    {
        ReleaseConstraint();
        _recreateConstraint = true;
        _retryCreation = true;
    }

    internal void NotifyBodyCreated(RigidBody body)
    {
        if (_recreateConstraint &&
            (ReferenceEquals(_ownBody, body) || ReferenceEquals(_otherBody, body) ||
                ReferenceEquals(GetComponent<RigidBody>(), body)))
            ApplyAttributes();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        previousNode?.RemoveListener(this);
        if (currentNode is not null)
        {
            currentNode.AddListener(this);
            _cachedWorldScale = currentNode.WorldScale;
        }
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (scene is not null)
        {
            if (ReferenceEquals(scene, _node))
                Log.GetLogger().Warning("{0} should not be created on the root scene node", GetTypeName());

            var physicsWorld = scene.GetOrCreateComponent<PhysicsWorld>();
            _physicsWorld = new WeakReference<PhysicsWorld>(physicsWorld);
            physicsWorld.AddConstraint(this);

            if (_retryCreation)
                CreateConstraint();
        }
        else
        {
            ReleaseConstraint();
            if (_physicsWorld is not null && _physicsWorld.TryGetTarget(out var physicsWorld))
                physicsWorld.RemoveConstraint(this);
            _physicsWorld = null;
            _retryCreation = true;
        }
    }

    protected override void OnMarkedDirty(Node node)
    {
        if (PhysicsUtils.HasWorldScaleChanged(_cachedWorldScale, node.WorldScale))
            ApplyFrames();
    }

    private void CreateConstraint()
    {
        if (_node is null)
        {
            _retryCreation = true;
            _recreateConstraint = true;
            return;
        }

        _cachedWorldScale = _node.WorldScale;
        ReleaseConstraint();

        _ownBody = GetComponent<RigidBody>();
        var physicsWorld = PhysicsWorld;
        var ownBodyId = _ownBody?.JoltBodyId;
        var otherBodyId = _otherBody?.JoltBodyId;
        if (physicsWorld is null || ownBodyId is null || (_otherBody is not null && otherBodyId is null))
        {
            _retryCreation = true;
            _recreateConstraint = true;
            return;
        }

        var ownPoint = _ownBody!.Position + _ownBody.Rotation * (_position * _cachedWorldScale);
        var ownFrame = _ownBody.Rotation * _rotation;
        var otherPoint = _otherBody is not null
            ? _otherBody.Position + _otherBody.Rotation * (_otherPosition * _otherBody.Node.WorldScale)
            : _otherPosition;
        var otherFrame = _otherBody is not null ? _otherBody.Rotation * _otherRotation : _otherRotation;

        using TwoBodyConstraintSettings settings = CreateSettings(otherPoint, otherFrame, ownPoint, ownFrame);
        settings.MEnabled = IsEnabledEffective;
        var body1 = otherBodyId ?? uint.MaxValue;
        var body2 = ownBodyId.Value;
        _constraint = physicsWorld.BodyInterface.CreateConstraint(settings, in body1, in body2);
        physicsWorld.World.AddConstraint(_constraint);
        _ownBody.AddConstraint(this);
        _otherBody?.AddConstraint(this);

        if (_disableCollision && otherBodyId is not null)
        {
            physicsWorld.DisableCollision(ownBodyId.Value, otherBodyId.Value);
            _disabledCollisionPair = (ownBodyId.Value, otherBodyId.Value);
        }

        _recreateConstraint = false;
        _framesDirty = false;
        _retryCreation = false;
    }

    private TwoBodyConstraintSettings CreateSettings(
        Vector3f point1, Quaternion frame1, Vector3f point2, Quaternion frame2)
    {
        switch (_constraintType)
        {
            case ConstraintType.Hinge:
                var hinge = new HingeConstraintSettings
                {
                    MSpace = EConstraintSpace.WorldSpace,
                    MPoint1 = point1,
                    MHingeAxis1 = frame1 * Vector3f.Forward,
                    MNormalAxis1 = frame1 * Vector3f.Right,
                    MPoint2 = point2,
                    MHingeAxis2 = frame2 * Vector3f.Forward,
                    MNormalAxis2 = frame2 * Vector3f.Right
                };
                // Jolt rejects equal hinge limits. Keep its unrestricted defaults while attributes are loading.
                if (_lowLimit.X != _highLimit.X)
                {
                    hinge.MLimitsMin = _lowLimit.X * MathDefs.DegToRad;
                    hinge.MLimitsMax = _highLimit.X * MathDefs.DegToRad;
                }
                return hinge;

            case ConstraintType.Slider:
                var slider = new SixDOFConstraintSettings
                {
                    MSpace = EConstraintSpace.WorldSpace,
                    MPosition1 = point1,
                    MAxisX1 = frame1 * Vector3f.Right,
                    MAxisY1 = frame1 * Vector3f.Up,
                    MPosition2 = point2,
                    MAxisX2 = frame2 * Vector3f.Right,
                    MAxisY2 = frame2 * Vector3f.Up
                };
                slider.SetLimitedAxis(SixDofAxis.TranslationX, _lowLimit.X, _highLimit.X);
                slider.MakeFixedAxis(SixDofAxis.TranslationY);
                slider.MakeFixedAxis(SixDofAxis.TranslationZ);
                slider.SetLimitedAxis(SixDofAxis.RotationX,
                    _lowLimit.Y * MathDefs.DegToRad, _highLimit.Y * MathDefs.DegToRad);
                slider.MakeFixedAxis(SixDofAxis.RotationY);
                slider.MakeFixedAxis(SixDofAxis.RotationZ);
                return slider;

            case ConstraintType.ConeTwist:
                return new SwingTwistConstraintSettings
                {
                    MSpace = EConstraintSpace.WorldSpace,
                    MPosition1 = point1,
                    MTwistAxis1 = frame1 * Vector3f.Right,
                    MPlaneAxis1 = frame1 * Vector3f.Up,
                    MPosition2 = point2,
                    MTwistAxis2 = frame2 * Vector3f.Right,
                    MPlaneAxis2 = frame2 * Vector3f.Up,
                    MTwistMinAngle = -_highLimit.X * MathDefs.DegToRad,
                    MTwistMaxAngle = _highLimit.X * MathDefs.DegToRad,
                    MNormalHalfConeAngle = _highLimit.Y * MathDefs.DegToRad,
                    MPlaneHalfConeAngle = _highLimit.Y * MathDefs.DegToRad
                };

            default:
                return new PointConstraintSettings
                {
                    MSpace = EConstraintSpace.WorldSpace,
                    MPoint1 = point1,
                    MPoint2 = point2
                };
        }
    }

    private void ApplyLimits()
    {
        if (_constraint is not null)
            CreateConstraint();
    }

    private void AdjustOtherBodyPosition()
    {
        if (_constraint is not null && _otherBody is null)
            _otherPosition = WorldPosition;
        MarkFramesDirty();
    }

    private void MarkConstraintDirty()
    {
        _recreateConstraint = true;
    }

    private void MarkFramesDirty()
    {
        _framesDirty = true;
    }

    private void SetOtherBodyNodeId(uint value)
    {
        _otherBodyNodeId = value;
        MarkConstraintDirty();
    }

    private void SetConstraintTypeAttribute(ConstraintType value)
    {
        _constraintType = value;
        MarkConstraintDirty();
    }

    private void SetPositionAttribute(Vector3f value)
    {
        _position = value;
        AdjustOtherBodyPosition();
    }
}
