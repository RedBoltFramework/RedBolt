using RedBolt;

/// <summary>
/// Custom logic component for rotating a scene node.
/// </summary>
public sealed class Rotator : LogicComponent
{
    private Vector3f _rotationSpeed;

    public Rotator()
    {
        // Only the scene update event is needed: unsubscribe from the rest for optimization
        SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    /// <summary>
    /// Set rotation speed about the Euler axes. Will be scaled with scene update time step.
    /// </summary>
    public void SetRotationSpeed(Vector3f speed)
    {
        _rotationSpeed = speed;
    }

    /// <summary>
    /// Handle scene update. Called by LogicComponent base class.
    /// </summary>
    public override void Update(float timeStep)
    {
        // Components have their scene node as a member variable for convenient access. Rotate the scene node now: construct a
        // rotation quaternion from Euler angles, scale rotation speed with the scene update time step
        Node.Rotate(new Quaternion(
            _rotationSpeed.X * timeStep,
            _rotationSpeed.Y * timeStep,
            _rotationSpeed.Z * timeStep));
    }

    /// <summary>
    /// Return rotation speed.
    /// </summary>
    public Vector3f GetRotationSpeed() => _rotationSpeed;
}
