namespace RedBolt;

public enum InterpolationMode
{
    BezierCurve = 0,
    CatmullRomCurve,
    LinearCurve,
    CatmullRomFullCurve
}

public sealed class Spline
{
    private readonly List<object?> knots = [];

    public Spline()
        : this(InterpolationMode.BezierCurve)
    {
    }

    public Spline(InterpolationMode mode)
    {
        InterpolationMode = mode;
    }

    public Spline(IEnumerable<object?> knots, InterpolationMode mode = InterpolationMode.BezierCurve)
        : this(mode)
    {
        SetKnots(knots);
    }

    public InterpolationMode InterpolationMode { get; private set; }

    public IReadOnlyList<object?> GetKnots() => knots;

    public object? GetKnot(uint index) => index < knots.Count ? knots[(int)index] : null;

    public object? GetPoint(float factor)
    {
        if (knots.Count == 0)
            return null;
        if (knots.Count == 1)
            return knots[0];

        var t = MathDefs.Clamp(factor, 0.0f, 1.0f);
        return InterpolationMode switch
        {
            InterpolationMode.LinearCurve => LinearInterpolation(knots, t),
            InterpolationMode.CatmullRomCurve => CatmullRomInterpolation(knots, t, fullPath: false),
            InterpolationMode.CatmullRomFullCurve => CatmullRomInterpolation(knots, t, fullPath: true),
            _ => BezierInterpolation(knots, t)
        };
    }

    public void SetInterpolationMode(InterpolationMode interpolationMode)
    {
        InterpolationMode = interpolationMode;
    }

    public void SetKnots(IEnumerable<object?> knots)
    {
        ArgumentNullException.ThrowIfNull(knots);
        this.knots.Clear();
        this.knots.AddRange(knots);
    }

    public void SetKnot(object? knot, uint index)
    {
        if (index < this.knots.Count)
            this.knots[(int)index] = knot;
    }

    public void AddKnot(object? knot)
    {
        knots.Add(knot);
    }

    public void AddKnot(object? knot, uint index)
    {
        knots.Insert((int)Math.Min(index, (uint)knots.Count), knot);
    }

    public void RemoveKnot()
    {
        if (knots.Count > 0)
            knots.RemoveAt(knots.Count - 1);
    }

    public void RemoveKnot(uint index)
    {
        if (index < knots.Count)
            knots.RemoveAt((int)index);
    }

    public void Clear()
    {
        knots.Clear();
    }

    private static object? BezierInterpolation(IReadOnlyList<object?> knots, float t)
    {
        var points = knots.ToArray();
        for (var count = points.Length - 1; count > 0; --count)
        {
            for (var i = 0; i < count; ++i)
                points[i] = Interpolate(points[i], points[i + 1], t);
        }
        return points[0];
    }

    private static object? CatmullRomInterpolation(IReadOnlyList<object?> originalKnots, float t, bool fullPath)
    {
        if (originalKnots.Count < 2)
            return originalKnots.Count == 1 ? originalKnots[0] : null;

        if (!fullPath && originalKnots.Count < 4)
            return LinearInterpolation(originalKnots, t);

        var segmentCount = fullPath ? originalKnots.Count - 1 : originalKnots.Count - 3;
        if (segmentCount <= 0)
            return LinearInterpolation(originalKnots, t);

        var scaled = t * segmentCount;
        var segment = Math.Min((int)MathF.Floor(scaled), segmentCount - 1);
        var localT = scaled - segment;

        object? p0;
        object? p1;
        object? p2;
        object? p3;
        if (fullPath)
        {
            p0 = originalKnots[Math.Max(segment - 1, 0)];
            p1 = originalKnots[segment];
            p2 = originalKnots[Math.Min(segment + 1, originalKnots.Count - 1)];
            p3 = originalKnots[Math.Min(segment + 2, originalKnots.Count - 1)];
        }
        else
        {
            p0 = originalKnots[segment];
            p1 = originalKnots[segment + 1];
            p2 = originalKnots[segment + 2];
            p3 = originalKnots[segment + 3];
        }

        return CatmullRom(p0, p1, p2, p3, localT);
    }

    private static object? LinearInterpolation(IReadOnlyList<object?> knots, float t)
    {
        var scaled = t * (knots.Count - 1);
        var index = Math.Min((int)MathF.Floor(scaled), knots.Count - 2);
        var localT = scaled - index;
        return Interpolate(knots[index], knots[index + 1], localT);
    }

    private static object? CatmullRom(object? p0, object? p1, object? p2, object? p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return (p0, p1, p2, p3) switch
        {
            (float a, float b, float c, float d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (Vector2 a, Vector2 b, Vector2 c, Vector2 d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (Vector3 a, Vector3 b, Vector3 c, Vector3 d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (Vector4 a, Vector4 b, Vector4 c, Vector4 d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (Color a, Color b, Color c, Color d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            _ => Interpolate(p1, p2, t)
        };
    }

    private static object? Interpolate(object? lhs, object? rhs, float t)
    {
        return (lhs, rhs) switch
        {
            (float left, float right) => MathDefs.Lerp(left, right, t),
            (Vector2 left, Vector2 right) => left.Lerp(right, t),
            (Vector3 left, Vector3 right) => left.Lerp(right, t),
            (Vector4 left, Vector4 right) => left.Lerp(right, t),
            (Quaternion left, Quaternion right) => left.Nlerp(right, t, shortestPath: true),
            (Color left, Color right) => left.Lerp(right, t),
            _ => lhs
        };
    }
}
