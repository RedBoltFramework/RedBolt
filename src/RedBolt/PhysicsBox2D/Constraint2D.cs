// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Bindings.Box2D;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>2D physics constraint component.</summary>
[Reflected(ObjectCategory.Category_Physics2D, Factory = false, InheritAttributes = false)]
public abstract partial class Constraint2D : Component
{
    private WeakReference<PhysicsWorld2D>? _physicsWorld;
    private RigidBody2D? _ownerBody;
    private RigidBody2D? _otherBody;
    private B2JointId? _jointId;
    private bool _collideConnected;
    private Constraint2D? _attachedConstraint;

    [Serialized("Other Body NodeID", Mode = AttributeMode.Default | AttributeMode.NodeId,
        Setter = nameof(SetOtherBodyNodeId))]
    private uint _otherBodyNodeId;

    /// <summary>Destruct. Free native joint.</summary>
    public override void Dispose()
    {
        ReleaseJoint();
        _ownerBody?.RemoveConstraint2D(this);
        _otherBody?.RemoveConstraint2D(this);
        base.Dispose();
    }

    public override void ApplyAttributes()
    {
        if (_otherBodyNodeId != 0 && Scene?.GetNode(_otherBodyNodeId)?.GetComponent<RigidBody2D>() is { } body)
            SetOtherBody(body);
        CreateJoint();
    }

    public override void OnSetEnabled()
    {
        if (IsEnabledEffective)
            CreateJoint();
        else
            ReleaseJoint();
    }

    public override void GetDependencyNodes(ICollection<Node> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (_otherBody?.TryGetNode() is { } node)
            destination.Add(node);
    }

    /// <summary>Create native joint.</summary>
    public void CreateJoint()
    {
        if (IsJointValid || !IsEnabledEffective || _ownerBody?.BodyId is null || _otherBody?.BodyId is null ||
            _physicsWorld is null || !_physicsWorld.TryGetTarget(out var world))
            return;
        _jointId = CreateNativeJoint(world.WorldId, _ownerBody.BodyId, _otherBody.BodyId);
        if (!IsJointValid)
        {
            _jointId?.Dispose();
            _jointId = null;
            return;
        }
        _ownerBody.AddConstraint2D(this);
        _otherBody.AddConstraint2D(this);
    }

    /// <summary>Release native joint.</summary>
    public void ReleaseJoint()
    {
        if (!IsJointValid)
            return;
        Box2DNativeBindings.B2DestroyJoint(_jointId!);
        _jointId!.Dispose();
        _jointId = null;
    }

    /// <summary>Set other rigid body.</summary>
    public void SetOtherBody(RigidBody2D? body)
    {
        if (ReferenceEquals(body, _otherBody))
            return;
        _otherBody?.RemoveConstraint2D(this);
        _otherBody = body;
        _otherBodyNodeId = body?.TryGetNode()?.Id ?? 0;
        _otherBody?.AddConstraint2D(this);
        RecreateJoint();
    }

    /// <summary>Return owner body.</summary>
    public RigidBody2D? OwnerBody => _ownerBody;
    /// <summary>Return or set other body.</summary>
    public RigidBody2D? OtherBody { get => _otherBody; set => SetOtherBody(value); }

    [Serialized("Collide Connected")]
    public bool CollideConnected
    {
        get => _collideConnected;
        set
        {
            if (_collideConnected == value)
                return;
            _collideConnected = value;
            RecreateJoint();
        }
    }

    /// <summary>Set attached constraint. Used by legacy gear constraint data.</summary>
    public void SetAttachedConstraint(Constraint2D? constraint) => _attachedConstraint = constraint;
    /// <summary>Return attached constraint.</summary>
    public Constraint2D? AttachedConstraint => _attachedConstraint;
    /// <summary>Return the physics world.</summary>
    public PhysicsWorld2D? PhysicsWorld =>
        _physicsWorld is not null && _physicsWorld.TryGetTarget(out var world) ? world : null;
    /// <summary>Return the native Box2D joint ID.</summary>
    public B2JointId? Joint => IsJointValid ? _jointId : null;

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || !IsJointValid || _ownerBody?.TryGetNode() is not { } ownerNode ||
            _otherBody?.TryGetNode() is not { } otherNode)
            return;
        debug.AddLine(ownerNode.WorldPosition, otherNode.WorldPosition, Color.Cyan, depthTest);
    }

    protected bool ShouldCollideConnected => _collideConnected;
    protected abstract B2JointId? CreateNativeJoint(B2WorldId worldId, B2BodyId ownerBody, B2BodyId otherBody);

    protected void RecreateJoint()
    {
        _attachedConstraint?.ReleaseJoint();
        ReleaseJoint();
        CreateJoint();
        _attachedConstraint?.CreateJoint();
    }

    protected static Vector2f ToLocalPoint(B2BodyId body, Vector2f point)
    {
        using var nativePoint = PhysicsUtils2D.ToB2(point);
        using var localPoint = Box2DNativeBindings.B2BodyGetLocalPoint(body, nativePoint);
        return PhysicsUtils2D.ToVector2(localPoint);
    }

    protected static Vector2f ToLocalVector(B2BodyId body, Vector2f vector)
    {
        using var nativeVector = PhysicsUtils2D.ToB2(vector);
        using var localVector = Box2DNativeBindings.B2BodyGetLocalVector(body, nativeVector);
        return PhysicsUtils2D.ToVector2(localVector);
    }

    protected static float GetReferenceAngle(B2BodyId ownerBody, B2BodyId otherBody)
    {
        using var ownerRotation = Box2DNativeBindings.B2BodyGetRotation(ownerBody);
        using var otherRotation = Box2DNativeBindings.B2BodyGetRotation(otherBody);
        return Box2DNativeBindings.B2RotGetAngle(otherRotation) - Box2DNativeBindings.B2RotGetAngle(ownerRotation);
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        _ownerBody?.RemoveConstraint2D(this);
        _ownerBody = currentNode?.GetComponent<RigidBody2D>();
        _ownerBody?.AddConstraint2D(this);
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);
        if (scene is not null)
        {
            var world = scene.GetOrCreateComponent<PhysicsWorld2D>();
            _physicsWorld = new WeakReference<PhysicsWorld2D>(world);
            CreateJoint();
        }
        else
        {
            ReleaseJoint();
            _physicsWorld = null;
        }
    }

    private bool IsJointValid => _jointId is not null && Box2DNativeBindings.B2JointIsValid(_jointId);
    private void SetOtherBodyNodeId(uint value)
    {
        _otherBodyNodeId = value;
        _otherBody = value == 0 ? null : Scene?.GetNode(value)?.GetComponent<RigidBody2D>();
        RecreateJoint();
    }
}
