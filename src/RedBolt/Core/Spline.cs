using Microsoft.Extensions.Logging;

namespace RedBolt;

public enum InterpolationMode
{
    /// <summary>Bezier interpolation.</summary>
    BezierCurve = 0,
    /// <summary>Catmull-Rom interpolation. The first and last knots control velocity and are not included on the path.</summary>
    CatmullRomCurve,
    /// <summary>Linear interpolation.</summary>
    LinearCurve,
    /// <summary>Catmull-Rom full path interpolation. Start and end knots are duplicated or looped as necessary to move through the full path.</summary>
    CatmullRomFullCurve
}

/// <summary>Spline class to get a point on it based off the interpolation mode.</summary>
public sealed class Spline : ICloneable, IEquatable<Spline>
{
    private InterpolationMode _interpolationMode;
    private VariantVector _knots = new();
    private readonly VariantVector _fullKnotsScratch = new();
    private readonly VariantVector _bezierScratchA = new();
    private readonly VariantVector _bezierScratchB = new();

    /// <summary>Default constructor.</summary>
    public Spline()
        : this(InterpolationMode.BezierCurve)
    {
    }

    /// <summary>Constructor setting interpolation mode.</summary>
    public Spline(InterpolationMode mode)
    {
        _interpolationMode = mode;
    }

    /// <summary>Constructor setting knots and interpolation mode.</summary>
    public Spline(VariantVector knots, InterpolationMode mode = InterpolationMode.BezierCurve)
    {
        _interpolationMode = mode;
        _knots = knots is not null ? new VariantVector(knots) : new VariantVector();
    }

    /// <summary>Return the interpolation mode.</summary>
    public InterpolationMode GetInterpolationMode() => _interpolationMode;

    /// <summary>Return the knots of the spline.</summary>
    public IReadOnlyList<Variant> GetKnots() => _knots;

    /// <summary>Return the knot at the specific index.</summary>
    public Variant GetKnot(uint index) => _knots[(int)index];

    /// <summary>Return the T of the point of the spline at f from 0.f - 1.f.</summary>
    public Variant GetPoint(float f)
    {
        if (_knots.Count < 2)
            return _knots.Count == 1 ? _knots[0] : default;

        if (f > 1.0f)
            f = 1.0f;
        else if (f < 0.0f)
            f = 0.0f;

        switch (_interpolationMode)
        {
        case InterpolationMode.BezierCurve:
            return BezierInterpolation(_knots, f);
        case InterpolationMode.CatmullRomCurve:
            return CatmullRomInterpolation(_knots, f);
        case InterpolationMode.LinearCurve:
            return LinearInterpolation(_knots, f);
        case InterpolationMode.CatmullRomFullCurve:
        {
            _fullKnotsScratch.Clear();
            if (_knots.Count > 1)
            {
                // Non-cyclic case: duplicate start and end
                if (!_knots[0].Equals(_knots[^1]))
                {
                    _fullKnotsScratch.Add(_knots[0]);
                    _fullKnotsScratch.AddRange(_knots);
                    _fullKnotsScratch.Add(_knots[^1]);
                }
                // Cyclic case: smooth the tangents
                else
                {
                    _fullKnotsScratch.Add(_knots[^2]);
                    _fullKnotsScratch.AddRange(_knots);
                    _fullKnotsScratch.Add(_knots[1]);
                }
            }
            return CatmullRomInterpolation(_fullKnotsScratch, f);
        }
        default:
            Log.Logger.LogError("Unsupported interpolation mode");
            return default;
        }
    }

    /// <summary>Set the interpolation mode.</summary>
    public void SetInterpolationMode(InterpolationMode interpolationMode)
    {
        _interpolationMode = interpolationMode;
    }

    /// <summary>Set the knots of the spline.</summary>
    public void SetKnots(VariantVector knots)
    {
        _knots = knots is not null ? new VariantVector(knots) : new VariantVector();
    }

    /// <summary>Set the value of an existing knot.</summary>
    public void SetKnot(Variant knot, uint index)
    {
        if (index < _knots.Count)
        {
            if (_knots.Count > 0 && _knots[0].Type == knot.Type)
                _knots[(int)index] = knot;
            else if (_knots.Count == 0)
                _knots.Add(knot);
            else
                Log.Logger.LogError(
                    "Attempted to set a Spline's Knot value of type {KnotType} where elements are already using {ExistingType}",
                    knot.Type,
                    _knots[0].Type);
        }
    }

    /// <summary>Add a knot to the end of the spline.</summary>
    public void AddKnot(Variant knot)
    {
        if (_knots.Count > 0 && _knots[0].Type == knot.Type)
            _knots.Add(knot);
        else if (_knots.Count == 0)
            _knots.Add(knot);
        else
            Log.Logger.LogError(
                "Attempted to add Knot to Spline of type {KnotType} where elements are already using {ExistingType}",
                knot.Type,
                _knots[0].Type);
    }

    /// <summary>Add a knot to the spline at a specific index.</summary>
    public void AddKnot(Variant knot, uint index)
    {
        if (index > _knots.Count)
            index = (uint)_knots.Count;

        if (_knots.Count > 0 && _knots[0].Type == knot.Type)
            _knots.Insert((int)index, knot);
        else if (_knots.Count == 0)
            _knots.Add(knot);
        else
            Log.Logger.LogError(
                "Attempted to add Knot to Spline of type {KnotType} where elements are already using {ExistingType}",
                knot.Type,
                _knots[0].Type);
    }

    /// <summary>Remove the last knot on the spline.</summary>
    public void RemoveKnot()
    {
        _knots.RemoveAt(_knots.Count - 1);
    }

    /// <summary>Remove the knot at the specific index.</summary>
    public void RemoveKnot(uint index)
    {
        _knots.RemoveAt((int)index);
    }

    /// <summary>Clear the spline.</summary>
    public void Clear()
    {
        _knots.Clear();
    }

    public object Clone()
    {
        return new Spline(_knots, _interpolationMode);
    }

    public bool Equals(Spline? other)
    {
        return other is not null && _interpolationMode == other._interpolationMode && ManagedSequence.Equal(_knots, other._knots);
    }

    public override bool Equals(object? obj)
    {
        return obj is Spline other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_interpolationMode);
        foreach (var knot in _knots)
            hash.Add(knot);
        return hash.ToHashCode();
    }

    public static bool operator ==(Spline lhs, Spline rhs) => lhs.Equals(rhs);

    public static bool operator !=(Spline lhs, Spline rhs) => !lhs.Equals(rhs);

    /// <summary>Perform Bezier interpolation on the spline.</summary>
    private Variant BezierInterpolation(VariantVector knots, float t)
    {
        if (knots.Count == 2)
        {
            switch (knots[0].Type)
            {
            case VariantType.Float:
            case VariantType.Vector2:
            case VariantType.Vector3:
            case VariantType.Vector4:
            case VariantType.Color:
            case VariantType.Double:
                return LinearInterpolation(knots[0], knots[1], t);
            default:
                return default;
            }
        }
        else
        {
            var source = knots;
            var destination = _bezierScratchA;
            while (source.Count > 2)
            {
                destination.Clear();
                switch (knots[0].Type)
                {
                case VariantType.Float:
                case VariantType.Vector2:
                case VariantType.Vector3:
                case VariantType.Vector4:
                case VariantType.Color:
                case VariantType.Double:
                    for (var i = 1; i < source.Count; ++i)
                        destination.Add(LinearInterpolation(source[i - 1], source[i], t));
                    break;
                default:
                    return default;
                }
                var previousSource = source;
                source = destination;
                destination = ReferenceEquals(previousSource, _bezierScratchA) ? _bezierScratchB : _bezierScratchA;
            }
            return LinearInterpolation(source[0], source[1], t);
        }
    }

    /// <summary>Perform Spline interpolation on the spline.</summary>
    private Variant CatmullRomInterpolation(VariantVector knots, float t)
    {
        if (knots.Count < 4)
            return default;

        if (t >= 1.0f)
            return knots[^2];

        var originIndex = (int)(t * (knots.Count - 3));
        t %= 1.0f / (knots.Count - 3);
        t *= knots.Count - 3;
        var t2 = t * t;
        var t3 = t2 * t;

        switch (knots[originIndex].Type)
        {
        case VariantType.Float:
            return CalculateCatmullRom(
                knots[originIndex].GetFloat(),
                knots[originIndex + 1].GetFloat(),
                knots[originIndex + 2].GetFloat(),
                knots[originIndex + 3].GetFloat(),
                t,
                t2,
                t3);
        case VariantType.Vector2:
            return CalculateCatmullRom(
                knots[originIndex].GetVector2(),
                knots[originIndex + 1].GetVector2(),
                knots[originIndex + 2].GetVector2(),
                knots[originIndex + 3].GetVector2(),
                t,
                t2,
                t3);
        case VariantType.Vector3:
            return CalculateCatmullRom(
                knots[originIndex].GetVector3(),
                knots[originIndex + 1].GetVector3(),
                knots[originIndex + 2].GetVector3(),
                knots[originIndex + 3].GetVector3(),
                t,
                t2,
                t3);
        case VariantType.Vector4:
            return CalculateCatmullRom(
                knots[originIndex].GetVector4(),
                knots[originIndex + 1].GetVector4(),
                knots[originIndex + 2].GetVector4(),
                knots[originIndex + 3].GetVector4(),
                t,
                t2,
                t3);
        case VariantType.Color:
            return CalculateCatmullRom(
                knots[originIndex].GetColor(),
                knots[originIndex + 1].GetColor(),
                knots[originIndex + 2].GetColor(),
                knots[originIndex + 3].GetColor(),
                t,
                t2,
                t3);
        case VariantType.Double:
            return CalculateCatmullRom(
                knots[originIndex].GetDouble(),
                knots[originIndex + 1].GetDouble(),
                knots[originIndex + 2].GetDouble(),
                knots[originIndex + 3].GetDouble(),
                t,
                t2,
                t3);
        default:
            return default;
        }
    }

    /// <summary>Perform linear interpolation on the spline.</summary>
    private Variant LinearInterpolation(VariantVector knots, float t)
    {
        if (knots.Count < 2)
            return default;

        if (t >= 1.0f)
            return knots[^1];

        var originIndex = Math.Clamp((int)(t * (knots.Count - 1)), 0, knots.Count - 2);
        t %= 1.0f / (knots.Count - 1);
        t *= knots.Count - 1;
        return LinearInterpolation(knots[originIndex], knots[originIndex + 1], t);
    }

    /// <summary>Linear interpolation between two Variants based on underlying type.</summary>
    private Variant LinearInterpolation(Variant lhs, Variant rhs, float t)
    {
        switch (lhs.Type)
        {
        case VariantType.Float:
            return MathDefs.Lerp(lhs.GetFloat(), rhs.GetFloat(), t);
        case VariantType.Vector2:
            return lhs.GetVector2().Lerp(rhs.GetVector2(), t);
        case VariantType.Vector3:
            return lhs.GetVector3().Lerp(rhs.GetVector3(), t);
        case VariantType.Vector4:
            return lhs.GetVector4().Lerp(rhs.GetVector4(), t);
        case VariantType.Color:
            return lhs.GetColor().Lerp(rhs.GetColor(), t);
        case VariantType.Double:
            return lhs.GetDouble() * (1.0 - t) + rhs.GetDouble() * t;
        default:
            return default;
        }
    }

    private static Variant CalculateCatmullRom<T>(T p0, T p1, T p2, T p3, float t, float t2, float t3)
    {
        return (p0, p1, p2, p3) switch
        {
            (float a, float b, float c, float d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (Vector2f a, Vector2f b, Vector2f c, Vector2f d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (Vector3f a, Vector3f b, Vector3f c, Vector3f d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (Vector4f a, Vector4f b, Vector4f c, Vector4f d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (Color a, Color b, Color c, Color d) => 0.5f * ((2.0f * b) + (-a + c) * t + (2.0f * a - 5.0f * b + 4.0f * c - d) * t2 + (-a + 3.0f * b - 3.0f * c + d) * t3),
            (double a, double b, double c, double d) => 0.5 * ((2.0 * b) + (-a + c) * t + (2.0 * a - 5.0 * b + 4.0 * c - d) * t2 + (-a + 3.0 * b - 3.0 * c + d) * t3),
            _ => default
        };
    }
}
