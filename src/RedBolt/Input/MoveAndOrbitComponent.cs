// C++: no direct C++ counterpart - adapter class
namespace RedBolt;

public sealed class MoveAndOrbitComponent : LogicComponent
{
    private Vector3f _velocity = Vector3f.Zero;
    private float _yaw;
    private float _pitch;
    private float _minDistance = 0.5f;
    private float _maxDistance = 100.0f;

    public MoveAndOrbitComponent()
    {
        RegisterAttribute(
            "Velocity",
            typeof(Vector3f),
            self => ((MoveAndOrbitComponent)self).GetVelocity(),
            (self, value) => ((MoveAndOrbitComponent)self).SetVelocity(value is Vector3f velocityValue ? velocityValue : Vector3f.Zero),
            Vector3f.Zero);
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
        RegisterAttribute(
            "Min Distance",
            typeof(float),
            self => ((MoveAndOrbitComponent)self).GetMinDistance(),
            (self, value) => ((MoveAndOrbitComponent)self).SetDistanceLimits(value is float min ? min : 0.5f, ((MoveAndOrbitComponent)self).GetMaxDistance()),
            0.5f);
        RegisterAttribute(
            "Max Distance",
            typeof(float),
            self => ((MoveAndOrbitComponent)self).GetMaxDistance(),
            (self, value) => ((MoveAndOrbitComponent)self).SetDistanceLimits(((MoveAndOrbitComponent)self).GetMinDistance(), value is float max ? max : 100.0f),
            100.0f);
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);
        if (currentNode is not null)
        {
            SetYaw(currentNode.Rotation.YawAngle);
            SetPitch(currentNode.Rotation.PitchAngle);
        }
    }

    public void SetVelocity(Vector3f velocity)
    {
        this._velocity = velocity;
    }

    public void SetYaw(float yaw)
    {
        this._yaw = yaw;
    }

    public void SetPitch(float pitch)
    {
        this._pitch = MathDefs.Clamp(pitch, -90.0f, 90.0f);
    }

    public void SetDistanceLimits(float minDistance, float maxDistance)
    {
        _minDistance = minDistance;
        _maxDistance = maxDistance;
    }

    public Vector3f GetVelocity() => _velocity;

    public float GetYaw() => _yaw;

    public float GetPitch() => _pitch;

    public float GetMinDistance() => _minDistance;

    public float GetMaxDistance() => _maxDistance;

    public Quaternion GetYawPitchRotation() => new(_pitch, _yaw, 0.0f);
}
