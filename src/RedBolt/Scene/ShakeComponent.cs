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
    private Vector3f _shiftRange = Vector3f.Zero;
    private Vector3f _rotationRange = Vector3f.Zero;
    private bool _hasOriginalPosition;
    private Vector3f _originalPosition = Vector3f.Zero;
    private Quaternion _originalRotation = Quaternion.IDENTITY;
    private Vector3f _lastKnownPosition = Vector3f.Zero;
    private Quaternion _lastKnownRotation = Quaternion.IDENTITY;

    public ShakeComponent()
    {
        SetUpdateEventMask(UpdateEvent.UseNoEvent);
        RegisterAttribute("Trauma", typeof(float), self => ((ShakeComponent)self).GetTrauma(), (self, value) => ((ShakeComponent)self).SetTrauma(value is float typed ? typed : 0.0f), 0.0f);
        RegisterAttribute("Trauma Power", typeof(float), self => ((ShakeComponent)self).GetTraumaPower(), (self, value) => ((ShakeComponent)self).SetTraumaPower(value is float typed ? typed : 2.0f), 2.0f);
        RegisterAttribute("Trauma Falloff", typeof(float), self => ((ShakeComponent)self).GetTraumaFalloff(), (self, value) => ((ShakeComponent)self).SetTraumaFalloff(value is float typed ? typed : 1.0f), 1.0f);
        RegisterAttribute("Time Scale", typeof(float), self => ((ShakeComponent)self).GetTimeScale(), (self, value) => ((ShakeComponent)self).SetTimeScale(value is float typed ? typed : DefaultTimeScale), DefaultTimeScale);
        RegisterAttribute("Shift Range", typeof(Vector3f), self => ((ShakeComponent)self).GetShiftRange(), (self, value) => ((ShakeComponent)self).SetShiftRange(value is Vector3f typed ? typed : Vector3f.Zero), Vector3f.Zero);
        RegisterAttribute("Rotation Range", typeof(Vector3f), self => ((ShakeComponent)self).GetRotationRange(), (self, value) => ((ShakeComponent)self).SetRotationRange(value is Vector3f typed ? typed : Vector3f.Zero), Vector3f.Zero);
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

    public float GetTraumaFalloff() => _traumaPower;

    public void SetShiftRange(Vector3f value)
    {
        _shiftRange = value;
    }

    public Vector3f GetShiftRange() => _shiftRange;

    public void SetRotationRange(Vector3f value)
    {
        _rotationRange = value;
    }

    public Vector3f GetRotationRange() => _rotationRange;

    public override void Update(float timeStep)
    {
        if (!IsAttached)
            return;

        var currentPosition = Node.Position;
        var currentRotation = Node.Rotation;
        if (!_hasOriginalPosition)
        {
            _lastKnownPosition = _originalPosition = currentPosition;
            _lastKnownRotation = _originalRotation = currentRotation;
            _hasOriginalPosition = true;
        }
        else
        {
            if (_lastKnownPosition != currentPosition)
                _originalPosition += currentPosition - _lastKnownPosition;
            if (_lastKnownRotation != currentRotation)
                _originalRotation *= _lastKnownRotation.Inverse * currentRotation;
        }

        _time += timeStep * _timeScale;
        _trauma = Math.Max(0.0f, _trauma - _traumaFalloff * timeStep);
        if (MathDefs.Equals(_trauma, 0.0f))
        {
            Node.SetPosition(_originalPosition);
            Node.SetRotation(_originalRotation);
            SetUpdateEventMask(UpdateEvent.UseNoEvent);
            _hasOriginalPosition = false;
            return;
        }

        var scale = MathDefs.Pow(_trauma, _traumaPower);
        var offset = Vector3f.Zero;
        if (_shiftRange != Vector3f.Zero)
        {
            offset = (new Vector3f(_perlinNoise.Get(_time), _perlinNoise.Get(_time + 1.0f), _perlinNoise.Get(_time + 2.0f)) * 2.0f - Vector3f.One)
                * _shiftRange * scale;
        }
        Node.SetPosition(_lastKnownPosition = _originalPosition + offset);

        var rotation = Vector3f.Zero;
        if (_rotationRange != Vector3f.Zero)
        {
            rotation = (new Vector3f(_perlinNoise.Get(_time + 3.0f), _perlinNoise.Get(_time + 4.0f), _perlinNoise.Get(_time + 5.0f)) * 2.0f - Vector3f.One)
                * _rotationRange * scale;
        }
        Node.SetRotation(_lastKnownRotation = _originalRotation * new Quaternion(rotation));
    }
}
