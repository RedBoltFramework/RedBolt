namespace RedBolt.Input;

using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;

/// <summary>Helper class to transform axis value.</summary>
public class AxisAdapter : ICloneable
{
    public const float DefaultDeadZone = 0.1f;
    public const float Epsilon = 1.0f / 32767.0f;

    public object Clone()
    {
        var result = new AxisAdapter();
        result._deadZone = _deadZone;
        result._positiveSensitivity = _positiveSensitivity;
        result._negativeSensitivity = _negativeSensitivity;
        result._neutral = _neutral;
        result._inverted = _inverted;
        return result;
    }

    /// <summary>Set dead zone half-width to mitigate axis drift.</summary>
    public void SetDeadZone(float deadZone)
    {
        _deadZone = SystemMath.Max(deadZone, 0.0f);
    }

    /// <summary>Get dead zone half-width.</summary>
    public float GetDeadZone() => _deadZone;

    /// <summary>Set both sensitivity values. 0.0 represent linear input mapping.</summary>
    public void SetSensitivity(float value)
    {
        _positiveSensitivity = _negativeSensitivity = value;
    }

    /// <summary>Set positive sensitivity value. 0.0 represent linear input mapping.</summary>
    public void SetPositiveSensitivity(float value)
    {
        _positiveSensitivity = value;
    }

    /// <summary>Set negative sensitivity value. 0.0 represent linear input mapping.</summary>
    public void SetNegativeSensitivity(float value)
    {
        _negativeSensitivity = value;
    }

    /// <summary>Get positive sensitivity value.</summary>
    public float GetPositiveSensitivity() => _positiveSensitivity;

    /// <summary>Get negative sensitivity value.</summary>
    public float GetNegativeSensitivity() => _negativeSensitivity;

    /// <summary>Set neutral value. Neutral value is transformed into 0.0f.</summary>
    public void SetNeutralValue(float value)
    {
        _neutral = value;
    }

    /// <summary>Get neutral value. Neutral value is transformed into 0.0f.</summary>
    public float GetNeutralValue() => _neutral;

    /// <summary>Set inverted flag.</summary>
    public void SetInverted(bool inverted)
    {
        _inverted = inverted;
    }

    /// <summary>Is axis inverted.</summary>
    public bool IsInverted() => _inverted;

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeOptionalValue(archive, "deadZone", ref _deadZone, DefaultDeadZone,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "inverted", ref _inverted, false,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "neutral", ref _neutral, 0.0f,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "posSensitivity", ref _positiveSensitivity, 0.0f,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "negSensitivity", ref _negativeSensitivity, 0.0f,
            ArchiveSerialization.SerializeValue);
    }

    /// <summary>Transform axis value. The output is normalized around neutral position into range -1..1.</summary>
    public float Transform(float value)
    {
        if (_inverted)
            value = -value;

        // Apply dead zone. Neutral position is mapped to 0.0.
        if (value >= _neutral - _deadZone - Epsilon && value <= _neutral + _deadZone + Epsilon)
            return 0.0f;

        // Clamp the result.
        if (value >= 1.0f - Epsilon)
            return 1.0f;
        if (value <= -1.0f + Epsilon)
            return -1.0f;

        if (value > _neutral)
        {
            var rangeMin = _neutral + _deadZone;
            var srcRange = 1.0f - rangeMin;
            var normalized = (value - rangeMin) / srcRange;
            var exponent = GetExponent(_positiveSensitivity);
            var transformed = MathF.Pow(normalized, exponent);
            return transformed;
        }
        var rangeMax = _neutral - _deadZone;
        var srcRangeNegative = rangeMax + 1.0f;
        var normalizedNegative = (rangeMax - value) / srcRangeNegative;
        var exponentNegative = GetExponent(_negativeSensitivity);
        var transformedNegative = MathF.Pow(normalizedNegative, exponentNegative);
        return -transformedNegative;
    }

    /// <summary>Convert sensitivity to exponent.</summary>
    private float GetExponent(float sensitivity)
    {
        if (sensitivity > 0)
            return 1.0f + sensitivity;
        return 1.0f / (1.0f - sensitivity);
    }

    /// <summary>Joystick dead zone half-width.</summary>
    private float _deadZone = DefaultDeadZone;
    /// <summary>Positive sensitivity value.</summary>
    private float _positiveSensitivity;
    /// <summary>Negative sensitivity value.</summary>
    private float _negativeSensitivity;
    /// <summary>Neutral value.</summary>
    private float _neutral;
    /// <summary>Is axis inverted.</summary>
    private bool _inverted;
}
