namespace RedBolt.Sample.SkeletalAnimation;

using RedBolt;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Custom logic component for moving the animated model and rotating at area edges.</summary>
[Reflected]
public sealed partial class Mover3D : LogicComponent
{
    private float _moveSpeed;
    private float _rotationSpeed;
    private BoundingBox _bounds;

    /// <summary>Construct.</summary>
    public Mover3D()
    {
        // Only the scene update event is needed: unsubscribe from the rest for optimization
        SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    /// <summary>Set motion parameters: forward movement speed, rotation speed, and movement boundaries.</summary>
    public void SetParameters(float moveSpeed, float rotationSpeed, BoundingBox bounds)
    {
        _moveSpeed = moveSpeed;
        _rotationSpeed = rotationSpeed;
        _bounds = bounds;
    }

    /// <summary>Handle scene update. Called by LogicComponent base class.</summary>
    public override void Update(float timeStep)
    {
        Node!.Translate(Vector3f.Forward * _moveSpeed * timeStep);

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
}
