namespace RedBolt;

public sealed class MoveAndOrbitComponent : LogicComponent
{
    private Vector3 velocity = Vector3.Zero;
    private float yaw;
    private float pitch;

    public MoveAndOrbitComponent()
    {
        RegisterAttribute(
            "Velocity",
            typeof(Vector3),
            self => ((MoveAndOrbitComponent)self).GetVelocity(),
            (self, value) => ((MoveAndOrbitComponent)self).SetVelocity(value is Vector3 velocityValue ? velocityValue : Vector3.Zero),
            Vector3.Zero);
        RegisterAttribute(
            "Yaw",
            typeof(float),
            self => ((MoveAndOrbitComponent)self).GetYaw(),
            (self, value) => ((MoveAndOrbitComponent)self).SetYaw(value is float yawValue ? yawValue : 0.0f),
            0.0f);
        RegisterAttribute(
            "Pitch",
            typeof(float),
            self => ((MoveAndOrbitComponent)self).GetPitch(),
            (self, value) => ((MoveAndOrbitComponent)self).SetPitch(value is float pitchValue ? pitchValue : 0.0f),
            0.0f);
    }

    public void SetVelocity(Vector3 velocity)
    {
        this.velocity = velocity;
    }

    public void SetYaw(float yaw)
    {
        this.yaw = yaw;
    }

    public void SetPitch(float pitch)
    {
        this.pitch = MathDefs.Clamp(pitch, -90.0f, 90.0f);
    }

    public Vector3 GetVelocity() => velocity;

    public float GetYaw() => yaw;

    public float GetPitch() => pitch;

    public Quaternion GetYawPitchRotation() => new(pitch, yaw, 0.0f);
}
