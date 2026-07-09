namespace RedBolt;

public sealed class ShakeComponent : LogicComponent
{
    public const float DefaultTimeScale = 16.0f;

    private readonly PerlinNoise _perlinNoise = new(RandomEngine.GetDefaultEngine());
    private float _time;
    private float _trauma;
    private float _traumaPower = 2.0f;
    private float _traumaFalloff = 1.0f;
    private float _timeScale = DefaultTimeScale;
    private Vector3 _shiftRange = Vector3.Zero;
    private Vector3 _rotationRange = Vector3.Zero;

    public ShakeComponent()
    {
        SetUpdateEventMask(UpdateEvent.UseNoEvent);
        RegisterAttribute("Trauma", typeof(float), self => ((ShakeComponent)self).GetTrauma(), (self, value) => ((ShakeComponent)self).SetTrauma(value is float typed ? typed : 0.0f), 0.0f);
        RegisterAttribute("Trauma Power", typeof(float), self => ((ShakeComponent)self).GetTraumaPower(), (self, value) => ((ShakeComponent)self).SetTraumaPower(value is float typed ? typed : 2.0f), 2.0f);
        RegisterAttribute("Trauma Falloff", typeof(float), self => ((ShakeComponent)self).GetTraumaFalloff(), (self, value) => ((ShakeComponent)self).SetTraumaFalloff(value is float typed ? typed : 1.0f), 1.0f);
        RegisterAttribute("Time Scale", typeof(float), self => ((ShakeComponent)self).GetTimeScale(), (self, value) => ((ShakeComponent)self).SetTimeScale(value is float typed ? typed : DefaultTimeScale), DefaultTimeScale);
        RegisterAttribute("Shift Range", typeof(Vector3), self => ((ShakeComponent)self).GetShiftRange(), (self, value) => ((ShakeComponent)self).SetShiftRange(value is Vector3 typed ? typed : Vector3.Zero), Vector3.Zero);
        RegisterAttribute("Rotation Range", typeof(Vector3), self => ((ShakeComponent)self).GetRotationRange(), (self, value) => ((ShakeComponent)self).SetRotationRange(value is Vector3 typed ? typed : Vector3.Zero), Vector3.Zero);
    }

    public void SetTimeScale(float value)
    {
        _timeScale = value;
    }

    public float GetTimeScale() => _timeScale;

    public void AddTrauma(float value)
    {
        SetTrauma(_trauma + value);
    }

    public void SetTrauma(float value)
    {
        _trauma = Math.Max(value, 0.0f);
        if (_trauma > 0.0f && (GetUpdateEventMask() & UpdateEvent.UseUpdate) == 0)
            SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    public float GetTrauma() => _trauma;

    public void SetTraumaPower(float value)
    {
        _traumaPower = value;
    }

    public float GetTraumaPower() => _traumaPower;

    public void SetTraumaFalloff(float value)
    {
        _traumaFalloff = value;
    }

    public float GetTraumaFalloff() => _traumaFalloff;

    public void SetShiftRange(Vector3 value)
    {
        _shiftRange = value;
    }

    public Vector3 GetShiftRange() => _shiftRange;

    public void SetRotationRange(Vector3 value)
    {
        _rotationRange = value;
    }

    public Vector3 GetRotationRange() => _rotationRange;

    public override void Update(float timeStep)
    {
        if (!IsAttached)
            return;

        _time += timeStep * _timeScale;
        _trauma = Math.Max(0.0f, _trauma - _traumaFalloff * timeStep);
        if (MathDefs.ApproximatelyEquivalent(_trauma, 0.0f))
        {
            Node.SetPosition(Vector3.Zero);
            Node.SetRotation(Quaternion.Identity);
            SetUpdateEventMask(UpdateEvent.UseNoEvent);
            return;
        }

        var scale = MathDefs.Pow(_trauma, _traumaPower);
        var offset = Vector3.Zero;
        if (_shiftRange != Vector3.Zero)
        {
            offset = (new Vector3(_perlinNoise.Get(_time), _perlinNoise.Get(_time + 1.0f), _perlinNoise.Get(_time + 2.0f)) * 2.0f - Vector3.One)
                * _shiftRange * scale;
        }
        Node.SetPosition(offset);

        var rotation = Vector3.Zero;
        if (_rotationRange != Vector3.Zero)
        {
            rotation = (new Vector3(_perlinNoise.Get(_time + 3.0f), _perlinNoise.Get(_time + 4.0f), _perlinNoise.Get(_time + 5.0f)) * 2.0f - Vector3.One)
                * _rotationRange * scale;
        }
        Node.SetRotation(new Quaternion(rotation));
    }
}
