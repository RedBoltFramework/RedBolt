namespace RedBolt;

public sealed class AxisAdapter
{
    public const float DefaultDeadZone = 0.1f;
    public const float Epsilon = 1.0f / 32767.0f;

    private float deadZone = DefaultDeadZone;
    private float positiveSensitivity;
    private float negativeSensitivity;
    private float neutral;
    private bool inverted;

    public void SetDeadZone(float deadZone)
    {
        this.deadZone = Math.Max(deadZone, 0.0f);
    }

    public float GetDeadZone() => deadZone;

    public void SetSensitivity(float value)
    {
        positiveSensitivity = value;
        negativeSensitivity = value;
    }

    public void SetPositiveSensitivity(float value)
    {
        positiveSensitivity = value;
    }

    public void SetNegativeSensitivity(float value)
    {
        negativeSensitivity = value;
    }

    public float GetPositiveSensitivity() => positiveSensitivity;

    public float GetNegativeSensitivity() => negativeSensitivity;

    public void SetNeutralValue(float value)
    {
        neutral = value;
    }

    public float GetNeutralValue() => neutral;

    public void SetInverted(bool inverted)
    {
        this.inverted = inverted;
    }

    public bool IsInverted() => inverted;

    public float Transform(float value)
    {
        if (inverted)
            value = -value;

        if (value >= neutral - deadZone - Epsilon && value <= neutral + deadZone + Epsilon)
            return 0.0f;

        if (value >= 1.0f - Epsilon)
            return 1.0f;
        if (value <= -1.0f + Epsilon)
            return -1.0f;

        if (value > neutral)
        {
            var rangeMin = neutral + deadZone;
            var sourceRange = 1.0f - rangeMin;
            var normalized = (value - rangeMin) / sourceRange;
            return MathF.Pow(normalized, GetExponent(positiveSensitivity));
        }

        var rangeMax = neutral - deadZone;
        var negativeRange = rangeMax + 1.0f;
        var negativeNormalized = (rangeMax - value) / negativeRange;
        return -MathF.Pow(negativeNormalized, GetExponent(negativeSensitivity));
    }

    private float GetExponent(float sensitivity)
    {
        return sensitivity > 0.0f
            ? 1.0f + sensitivity
            : 1.0f / (1.0f - sensitivity);
    }
}
