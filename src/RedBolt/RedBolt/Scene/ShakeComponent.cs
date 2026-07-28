using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Math;

namespace RedBolt.Scene;

/// <summary>
/// Component that randomly displaces the node around (0,0,0) coordinate.
/// Perfect for camera shake effect.
///
/// Math for Game Programmers: Juicing Your Cameras With Math by Squirrel Eiserloh
/// https://www.gdcvault.com/play/1033548/Math-for-Game-Programmers-Juicing
/// </summary>
[Reflected(ObjectCategory.Category_Scene)]
[Serialized(
    "Trauma",
    typeof(float),
    DefaultSource = nameof(GetTraumaAttributeDefault),
    Getter = nameof(Trauma),
    Setter = nameof(Trauma)
)]
[Serialized(
    "Trauma Power",
    typeof(float),
    DefaultSource = nameof(GetTraumaPowerAttributeDefault),
    Getter = nameof(TraumaPower),
    Setter = nameof(TraumaPower)
)]
[Serialized(
    "Trauma Falloff",
    typeof(float),
    DefaultSource = nameof(GetTraumaFalloffAttributeDefault),
    Getter = nameof(_traumaFalloff),
    Setter = nameof(_traumaFalloff)
)]
[Serialized(
    "Time Scale",
    typeof(float),
    DefaultSource = nameof(GetTimeScaleAttributeDefault),
    Getter = nameof(TimeScale),
    Setter = nameof(TimeScale)
)]
[Serialized(
    "Shift Range",
    typeof(Vector3f),
    DefaultSource = nameof(GetShiftRangeAttributeDefault),
    Getter = nameof(ShiftRange),
    Setter = nameof(ShiftRange)
)]
[Serialized(
    "Rotation Range",
    typeof(Vector3f),
    DefaultSource = nameof(GetRotationRangeAttributeDefault),
    Getter = nameof(RotationRange),
    Setter = nameof(RotationRange)
)]
public partial class ShakeComponent : LogicComponent
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

    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
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
            _trauma = SystemMath.Max(value, 0.0f);

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
        _trauma = SystemMath.Max(0.0f, _trauma - _traumaFalloff * timeStep);
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

    private static object? GetTraumaAttributeDefault() => 0.0f;

    private static object? GetTraumaPowerAttributeDefault() => 2.0f;

    private static object? GetTraumaFalloffAttributeDefault() => 1.0f;

    private static object? GetTimeScaleAttributeDefault() => DefaultTimeScale;

    private static object? GetShiftRangeAttributeDefault() => Vector3f.Zero;

    private static object? GetRotationRangeAttributeDefault() => Vector3f.Zero;
}
