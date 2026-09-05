// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.SkeletalAnimation;

using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Custom logic component for moving the animated model and rotating at area edges.</summary>
[Reflected]
public sealed partial class Mover3D : LogicComponent
{
    private const string OriginalRootTrackMetadata = "OriginalRootTrack";
    private const string RootLinearVelocityMetadata = "RootLinearVelocity";
    private AnimationTrack? _rootMotionTrack;
    private float _animationLength;
    private float _animationTime;
    private float _animationSpeed;
    private float _moveSpeed;
    private float _rotationSpeed;
    private BoundingBox _bounds;

    /// <summary>Construct.</summary>
    public Mover3D()
    {
        // Only the scene update event is needed: unsubscribe from the rest for optimization
        SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    /// <summary>Set motion parameters: source animation, initial time, movement speed, rotation speed, and movement boundaries.</summary>
    public void SetParameters(Animation animation, float startTime, float moveSpeed, float rotationSpeed, BoundingBox bounds)
    {
        var rootTrackName = animation.GetMetadata(OriginalRootTrackMetadata).GetString();
        _rootMotionTrack = animation.GetTrack(rootTrackName);
        _animationLength = animation.Length;
        _animationTime = startTime;
        _moveSpeed = moveSpeed;
        _animationSpeed = GetAnimationSpeed(animation, moveSpeed);
        Node!.GetComponent<AnimationController>()!.SetSpeed(animation.Name, _animationSpeed);
        _rotationSpeed = rotationSpeed;
        _bounds = bounds;
    }

    /// <summary>Handle scene update. Called by LogicComponent base class.</summary>
    public override void Update(float timeStep)
    {
        ApplyRootMotion(timeStep);

        // If in risk of going outside the plane, rotate the model right
        var pos = Node!.Position;
        if (pos.X < _bounds.Min.X || pos.X > _bounds.Max.X || pos.Z < _bounds.Min.Z || pos.Z > _bounds.Max.Z)
            Node!.Yaw(_rotationSpeed * timeStep);
    }

    /// <summary>Return forward movement speed.</summary>
    public float MoveSpeed => _moveSpeed;
    /// <summary>Return rotation speed.</summary>
    public float RotationSpeed => _rotationSpeed;
    /// <summary>Return movement boundaries.</summary>
    public BoundingBox Bounds => _bounds;

    private void ApplyRootMotion(float timeStep)
    {
        if (_rootMotionTrack is null || _animationLength <= 0.0f)
            return;

        var nextTime = _animationTime + timeStep * _animationSpeed;
        var rootMotion = Vector3f.Zero;
        while (nextTime >= _animationLength)
        {
            rootMotion += SampleRootPosition(_animationLength) - SampleRootPosition(_animationTime);
            nextTime -= _animationLength;
            _animationTime = 0.0f;
        }
        rootMotion += SampleRootPosition(nextTime) - SampleRootPosition(_animationTime);

        Node!.Translate(rootMotion);
        _animationTime = nextTime;
    }

    private Vector3f SampleRootPosition(float time)
    {
        var frameIndex = 0u;
        var transform = new Transform();
        _rootMotionTrack!.Sample(time, _animationLength, false, ref frameIndex, ref transform);
        return transform.Position;
    }

    private static float GetAnimationSpeed(Animation animation, float moveSpeed)
    {
        var rootVelocity = animation.GetMetadata(RootLinearVelocityMetadata).GetVector3();
        var rootPlanarSpeed = new Vector2f(rootVelocity.X, rootVelocity.Z).Length;
        return rootPlanarSpeed > MathDefs.Epsilon ? moveSpeed / rootPlanarSpeed : 1.0f;
    }
}
