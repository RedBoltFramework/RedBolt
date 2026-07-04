namespace RedBolt;

public static class EaseMath
{
    public static float BackIn(float time)
    {
        const float overshoot = 1.70158f;
        return time * time * ((overshoot + 1.0f) * time - overshoot);
    }

    public static float BackOut(float time)
    {
        const float overshoot = 1.70158f;
        time -= 1.0f;
        return time * time * ((overshoot + 1.0f) * time + overshoot) + 1.0f;
    }

    public static float BackInOut(float time)
    {
        const float overshoot = 1.70158f * 1.525f;
        time *= 2.0f;
        if (time < 1.0f)
            return time * time * ((overshoot + 1.0f) * time - overshoot) * 0.5f;

        time -= 2.0f;
        return time * time * ((overshoot + 1.0f) * time + overshoot) * 0.5f + 1.0f;
    }

    public static float BounceOut(float time)
    {
        if (time < 1.0f / 2.75f)
            return 7.5625f * time * time;
        if (time < 2.0f / 2.75f)
        {
            time -= 1.5f / 2.75f;
            return 7.5625f * time * time + 0.75f;
        }
        if (time < 2.5f / 2.75f)
        {
            time -= 2.25f / 2.75f;
            return 7.5625f * time * time + 0.9375f;
        }

        time -= 2.625f / 2.75f;
        return 7.5625f * time * time + 0.984375f;
    }

    public static float BounceIn(float time) => 1.0f - BounceOut(1.0f - time);

    public static float BounceInOut(float time)
    {
        if (time < 0.5f)
        {
            time *= 2.0f;
            return (1.0f - BounceOut(1.0f - time)) * 0.5f;
        }

        return BounceOut(time * 2.0f - 1.0f) * 0.5f + 0.5f;
    }

    public static float SineOut(float time) => MathF.Sin(time * MathDefs.PiOver2);
    public static float SineIn(float time) => -MathF.Cos(time * MathDefs.PiOver2) + 1.0f;
    public static float SineInOut(float time) => -0.5f * (MathF.Cos(MathDefs.Pi * time) - 1.0f);

    public static float ExponentialOut(float time) => time == 1.0f ? 1.0f : -MathF.Pow(2.0f, -10.0f * time) + 1.0f;
    public static float ExponentialIn(float time) => time == 0.0f ? 0.0f : MathF.Pow(2.0f, 10.0f * (time - 1.0f)) - 0.001f;

    public static float ExponentialInOut(float time)
    {
        time /= 0.5f;
        if (time < 1.0f)
            return 0.5f * MathF.Pow(2.0f, 10.0f * (time - 1.0f));

        return 0.5f * (-MathF.Pow(2.0f, -10.0f * (time - 1.0f)) + 2.0f);
    }

    public static float ElasticIn(float time, float period)
    {
        if (time == 0.0f || time == 1.0f)
            return time;

        var s = period / 4.0f;
        time -= 1.0f;
        return -(MathF.Pow(2.0f, 10.0f * time) * MathF.Sin((time - s) * MathDefs.TwoPi / period));
    }

    public static float ElasticOut(float time, float period)
    {
        if (time == 0.0f || time == 1.0f)
            return time;

        var s = period / 4.0f;
        return MathF.Pow(2.0f, -10.0f * time) * MathF.Sin((time - s) * MathDefs.TwoPi / period) + 1.0f;
    }

    public static float ElasticInOut(float time, float period)
    {
        if (time == 0.0f || time == 1.0f)
            return time;

        time *= 2.0f;
        if (period == 0.0f)
            period = 0.3f * 1.5f;

        var s = period / 4.0f;
        time -= 1.0f;
        if (time < 0.0f)
            return -0.5f * MathF.Pow(2.0f, 10.0f * time) * MathF.Sin((time - s) * MathDefs.TwoPi / period);

        return MathF.Pow(2.0f, -10.0f * time) * MathF.Sin((time - s) * MathDefs.TwoPi / period) * 0.5f + 1.0f;
    }
}
