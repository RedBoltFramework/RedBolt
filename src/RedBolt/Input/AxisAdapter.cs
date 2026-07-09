namespace RedBolt;

public sealed class AxisAdapter
{
    public const float DefaultDeadZone = 0.1f;
    public const float Epsilon = 1.0f / 32767.0f;

    private float _deadZone = DefaultDeadZone;
    private float _positiveSensitivity;
    private float _negativeSensitivity;
    private float _neutral;
    private bool _inverted;

    public void SetDeadZone(float deadZone)
    {
        this._deadZone = Math.Max(deadZone, 0.0f);
    }

    public float GetDeadZone() => _deadZone;

    public void SetSensitivity(float value)
    {
        _positiveSensitivity = value;
        _negativeSensitivity = value;
    }

    public void SetPositiveSensitivity(float value)
    {
        _positiveSensitivity = value;
    }

    public void SetNegativeSensitivity(float value)
    {
        _negativeSensitivity = value;
    }

    public float GetPositiveSensitivity() => _positiveSensitivity;

    public float GetNegativeSensitivity() => _negativeSensitivity;

    public void SetNeutralValue(float value)
    {
        _neutral = value;
    }

    public float GetNeutralValue() => _neutral;

    public void SetInverted(bool inverted)
    {
        this._inverted = inverted;
    }

    public bool IsInverted() => _inverted;

    public float Transform(float value)
    {
        if (_inverted)
            value = -value;

        if (value >= _neutral - _deadZone - Epsilon && value <= _neutral + _deadZone + Epsilon)
            return 0.0f;

        if (value >= 1.0f - Epsilon)
            return 1.0f;
        if (value <= -1.0f + Epsilon)
            return -1.0f;

        if (value > _neutral)
        {
            var rangeMin = _neutral + _deadZone;
            var sourceRange = 1.0f - rangeMin;
            var normalized = (value - rangeMin) / sourceRange;
            return MathF.Pow(normalized, GetExponent(_positiveSensitivity));
        }

        var rangeMax = _neutral - _deadZone;
        var negativeRange = rangeMax + 1.0f;
        var negativeNormalized = (rangeMax - value) / negativeRange;
        return -MathF.Pow(negativeNormalized, GetExponent(_negativeSensitivity));
    }

    private float GetExponent(float sensitivity)
    {
        return sensitivity > 0.0f
            ? 1.0f + sensitivity
            : 1.0f / (1.0f - sensitivity);
    }
}
