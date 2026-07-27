namespace RedBolt;

/// <summary>
/// Component that randomly displaces the node around (0,0,0) coordinate.
/// Perfect for camera shake effect.
///
/// Math for Game Programmers: Juicing Your Cameras With Math by Squirrel Eiserloh
/// https://www.gdcvault.com/play/1033548/Math-for-Game-Programmers-Juicing
/// </summary>
public class ShakeComponent : LogicComponent
{
    public const float DefaultTimeScale = 16.0f;

    /// <summary>Construct.</summary>
    public ShakeComponent()
    {
        TraumaPower = 2.0f;
        _traumaFalloff = 1.0f;
        TimeScale = DefaultTimeScale;
        ShiftRange = Vector3f.Zero;
        RotationRange = Vector3f.Zero;

        SetUpdateEventMask(UpdateEvent.UseNoEvent);

        RegisterAttribute("Trauma", typeof(float), self => ((ShakeComponent)self).Trauma, (self, value) => ((ShakeComponent)self).Trauma = value is float typed ? typed : 0.0f, 0.0f);
        RegisterAttribute("Trauma Power", typeof(float), self => ((ShakeComponent)self).TraumaPower, (self, value) => ((ShakeComponent)self).TraumaPower = value is float typed ? typed : 2.0f, 2.0f);
        RegisterAttribute("Trauma Falloff", typeof(float), self => ((ShakeComponent)self)._traumaFalloff, (self, value) => ((ShakeComponent)self)._traumaFalloff = value is float typed ? typed : 1.0f, 1.0f);
        RegisterAttribute("Time Scale", typeof(float), self => ((ShakeComponent)self).TimeScale, (self, value) => ((ShakeComponent)self).TimeScale = value is float typed ? typed : DefaultTimeScale, DefaultTimeScale);
        RegisterAttribute("Shift Range", typeof(Vector3f), self => ((ShakeComponent)self).ShiftRange, (self, value) => ((ShakeComponent)self).ShiftRange = value is Vector3f typed ? typed : Vector3f.Zero, Vector3f.Zero);
        RegisterAttribute("Rotation Range", typeof(Vector3f), self => ((ShakeComponent)self).RotationRange, (self, value) => ((ShakeComponent)self).RotationRange = value is Vector3f typed ? typed : Vector3f.Zero, Vector3f.Zero);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<ShakeComponent>(ObjectCategory.Category_Scene);
    }

    /// <summary>Set or get time scale. This is a multiplier for Perlin Noise argument.</summary>
    public float TimeScale { get; set; }

    /// <summary>Increase trauma value.</summary>
    public void AddTrauma(float value)
    {
        Trauma += value;
    }

    /// <summary>Set or get trauma value.</summary>
    public float Trauma
    {
        get => _trauma;
        set
        {
            _trauma = Math.Max(value, 0.0f);

            if (_trauma > 0.0f)
            {
                if ((UpdateEvent)0 == (GetUpdateEventMask() & UpdateEvent.UseUpdate))
                    SetUpdateEventMask(UpdateEvent.UseUpdate);
            }
        }
    }

    /// <summary>Set or get trauma power value.</summary>
    public float TraumaPower { get; set; }

    /// <summary>Set or get trauma falloff (how many units to loose per second).</summary>
    public float TraumaFalloff
    {
        get => TraumaPower;
        set => _traumaFalloff = value;
    }

    /// <summary>Set or get shift range.</summary>
    public Vector3f ShiftRange { get; set; }

    /// <summary>Set or get rotation range.</summary>
    public Vector3f RotationRange { get; set; }

    /// <summary>Update node position.</summary>
    public override void Update(float timeStep)
    {
        if (TryGetNode() is null)
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
            {
                var diff = currentPosition - _lastKnownPosition;
                _originalPosition = _originalPosition + diff;
            }
            if (_lastKnownRotation != currentRotation)
            {
                var diff = _lastKnownRotation.Inverse * currentRotation;
                _originalRotation = _originalRotation * diff;
            }
        }

        _time += timeStep * TimeScale;
        _trauma = Math.Max(0.0f, _trauma - _traumaFalloff * timeStep);
        if (_trauma == 0.0f)
        {
            Node.SetPosition(_originalPosition);
            Node.SetRotation(_originalRotation);
            SetUpdateEventMask(UpdateEvent.UseNoEvent);
            _hasOriginalPosition = false;
            return;
        }

        var scale = MathDefs.Pow(_trauma, TraumaPower);
        var offset = Vector3f.Zero;
        if (ShiftRange != Vector3f.Zero)
        {
            offset = (new Vector3f(_perlinNoise.Get(_time), _perlinNoise.Get(_time + 1), _perlinNoise.Get(_time + 2)) * 2.0f
                    - Vector3f.One)
                * ShiftRange * scale;
        }
        Node.SetPosition(_lastKnownPosition = _originalPosition + offset);

        var rotation = Vector3f.Zero;
        if (RotationRange != Vector3f.Zero)
        {
            rotation =
                (new Vector3f(_perlinNoise.Get(_time + 3), _perlinNoise.Get(_time + 4), _perlinNoise.Get(_time + 5)) * 2.0f
                    - Vector3f.One)
                * RotationRange * scale;
        }
        Node.SetRotation(_lastKnownRotation = _originalRotation * new Quaternion(rotation));
    }

    /// <summary>Perlin noise generator.</summary>
    private readonly PerlinNoise _perlinNoise = new(RandomEngine.GetDefaultEngine());

    /// <summary>Current time value.</summary>
    private float _time;
    /// <summary>Current trauma value.</summary>
    private float _trauma;
    /// <summary>Current trauma falloff.</summary>
    private float _traumaFalloff;

    /// <summary>Flag indicating that original position and rotation are captured.</summary>
    private bool _hasOriginalPosition;
    /// <summary>Original position of the node.</summary>
    private Vector3f _originalPosition = Vector3f.Zero;
    /// <summary>Original rotation of the node.</summary>
    private Quaternion _originalRotation = Quaternion.IDENTITY;
    /// <summary>Last known position of the node.</summary>
    private Vector3f _lastKnownPosition = Vector3f.Zero;
    /// <summary>Last known rotation of the node.</summary>
    private Quaternion _lastKnownRotation = Quaternion.IDENTITY;
}
