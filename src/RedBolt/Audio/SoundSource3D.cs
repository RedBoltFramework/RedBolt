namespace RedBolt;

/// <summary>Sound source component with 3D position attenuation.</summary>
public sealed class SoundSource3D : SoundSource
{
    private float _nearDistance = 1.0f;
    private float _farDistance = 100.0f;
    private float _innerAngle = 360.0f;
    private float _outerAngle = 360.0f;
    private float _rolloffFactor = 1.0f;

    public override void Update(float timeStep)
    {
        CalculateAttenuation();
    }

    public void SetDistanceAttenuation(float nearDistance, float farDistance, float rolloffFactor)
    {
        _nearDistance = Math.Max(nearDistance, 0.0f);
        _farDistance = Math.Max(farDistance, 0.0f);
        _rolloffFactor = MathDefs.Clamp(rolloffFactor, 0.0f, float.MaxValue);
    }

    public void SetAngleAttenuation(float innerAngle, float outerAngle)
    {
        _innerAngle = MathDefs.Clamp(innerAngle, 0.0f, 360.0f);
        _outerAngle = MathDefs.Clamp(outerAngle, 0.0f, 360.0f);
    }

    public void SetNearDistance(float distance) { _nearDistance = Math.Max(distance, 0.0f); }
    public void SetFarDistance(float distance) { _farDistance = Math.Max(distance, 0.0f); }
    public void SetInnerAngle(float angle) { _innerAngle = MathDefs.Clamp(angle, 0.0f, 360.0f); }
    public void SetOuterAngle(float angle) { _outerAngle = MathDefs.Clamp(angle, 0.0f, 360.0f); }
    public void SetRolloffFactor(float factor) { _rolloffFactor = MathDefs.Clamp(factor, 0.0f, float.MaxValue); }

    public float GetNearDistance() => _nearDistance;
    public float GetFarDistance() => _farDistance;
    public float GetInnerAngle() => _innerAngle;
    public float GetOuterAngle() => _outerAngle;
    public float GetRolloffFactor() => _rolloffFactor;

    private void CalculateAttenuation()
    {
        /* TODO: port 3D distance attenuation using node world position and Audio listener */
        Attenuation = 1.0f;
    }
}
