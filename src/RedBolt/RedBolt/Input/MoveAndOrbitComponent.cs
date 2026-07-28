using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.Input;

[Reflected(ObjectCategory.Category_Logic)]
public partial class MoveAndOrbitComponent : LogicComponent
{
    private Vector3f _velocity = Vector3f.Zero;
    private float _yaw;
    private float _pitch;
    private float _minDistance = 0.5f;
    private float _maxDistance = 100.0f;

    /// <summary>Construct.</summary>
    public MoveAndOrbitComponent()
    {
    }


    /// <summary>Handle scene node being assigned at creation.</summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);
        if (currentNode is not null)
        {
            var currentRotation = currentNode.Rotation;
            SetYaw(currentRotation.YawAngle);
            SetPitch(currentRotation.PitchAngle);
        }
    }

    /// <summary>Set movement velocity in node's local space.</summary>
    public virtual void SetVelocity(Vector3f velocity)
    {
        this._velocity = velocity;
    }

    /// <summary>Set yaw angle in degrees.</summary>
    public virtual void SetYaw(float yaw)
    {
        this._yaw = yaw;
    }

    /// <summary>Set pitch angle in degrees.</summary>
    public virtual void SetPitch(float pitch)
    {
        this._pitch = MathDefs.Clamp(pitch, -90.0f, 90.0f);
    }

    /// <summary>Set distance limits.</summary>
    public void SetDistanceLimits(float minDistance, float maxDistance)
    {
        _minDistance = minDistance;
        _maxDistance = maxDistance;
    }

    /// <summary>Get movement velocity in node's local space.</summary>
    public Vector3f GetVelocity() => _velocity;

    /// <summary>Get yaw angle in degrees.</summary>
    public float GetYaw() => _yaw;

    /// <summary>Get pitch angle in degrees.</summary>
    public float GetPitch() => _pitch;

    /// <summary>Get min distance.</summary>
    public float GetMinDistance() => _minDistance;

    /// <summary>Get max distance.</summary>
    public float GetMaxDistance() => _maxDistance;

    /// <summary>Get yaw and pitch rotation.</summary>
    public Quaternion GetYawPitchRotation() => new(_pitch, _yaw, 0.0f);
}
