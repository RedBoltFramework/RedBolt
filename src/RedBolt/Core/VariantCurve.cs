namespace RedBolt;

public enum KeyFrameInterpolation
{
    None,
    Linear,
    TensionSpline,
    TangentSpline
}

public struct VariantCurvePoint : IEquatable<VariantCurvePoint>
{
    public float Time;
    public Variant Value;

    public readonly uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(Time);
        hash.Add(Value);
        return unchecked((uint)hash.ToHashCode());
    }

    public readonly bool Equals(VariantCurvePoint other) => Time == other.Time && Value == other.Value;

    public override readonly bool Equals(object? obj) => obj is VariantCurvePoint other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Time, Value);

    public static bool operator ==(VariantCurvePoint lhs, VariantCurvePoint rhs) => lhs.Equals(rhs);
    public static bool operator !=(VariantCurvePoint lhs, VariantCurvePoint rhs) => !lhs.Equals(rhs);
}

public class VariantCurve : KeyFrameSet<VariantCurvePoint>, IEquatable<VariantCurve>
{
    public static readonly VariantCurve Empty = new();

    public VariantCurve()
        : base(static keyFrame => keyFrame.Time)
    {
    }

    public string Name = string.Empty;
    public StringHash NameHash;
    public KeyFrameInterpolation Interpolation = KeyFrameInterpolation.Linear;
    public float SplineTension = 0.5f;
    public List<Variant> InTangents { get; } = [];
    public List<Variant> OutTangents { get; } = [];
    public VariantType Type { get; private set; }

    public void Commit()
    {
        NameHash = new StringHash(Name);
        Type = GetType();

        switch (Type)
        {
        case VariantType.Float:
        case VariantType.Vector2:
        case VariantType.Vector3:
        case VariantType.Vector4:
        case VariantType.Quaternion:
        case VariantType.Color:
        case VariantType.Double:
            if (Interpolation == KeyFrameInterpolation.TensionSpline)
            {
                var numKeyFrames = KeyFrames.Count;
                InTangents.Clear();
                for (var i = 0; i < numKeyFrames; ++i)
                    InTangents.Add(default);

                for (var i = 1; i + 1 < numKeyFrames; ++i)
                    InTangents[i] = SubtractAndMultiply(Type, KeyFrames[i + 1].Value, KeyFrames[i - 1].Value, SplineTension);

                if (numKeyFrames <= 2 || KeyFrames[0].Value != KeyFrames[^1].Value)
                {
                    InTangents[0] = new Variant(Type);
                    if (numKeyFrames >= 2)
                        InTangents[^1] = new Variant(Type);
                }
                else
                {
                    var tangent = SubtractAndMultiply(Type, KeyFrames[1].Value, KeyFrames[^2].Value, SplineTension);
                    InTangents[0] = tangent;
                    InTangents[^1] = tangent;
                }

                OutTangents.Clear();
                OutTangents.AddRange(InTangents);
            }
            break;

        case VariantType.Int:
        case VariantType.Int64:
        case VariantType.IntRect:
        case VariantType.Vector2i:
        case VariantType.Vector3i:
            if (Interpolation is KeyFrameInterpolation.TensionSpline or KeyFrameInterpolation.TangentSpline)
                Interpolation = KeyFrameInterpolation.Linear;
            break;

        default:
            Interpolation = KeyFrameInterpolation.None;
            break;
        }
    }

    public Variant Sample(float time, float duration, bool isLooped, ref uint frameIndex)
    {
        GetKeyFrames(time, duration, isLooped, ref frameIndex, out var nextFrameIndex, out var blendFactor);

        var keyFrame = KeyFrames[checked((int)frameIndex)];
        var nextKeyFrame = KeyFrames[checked((int)nextFrameIndex)];
        if (blendFactor >= MathDefs.Epsilon)
        {
            var isSplineInterpolation = Interpolation is KeyFrameInterpolation.TensionSpline or KeyFrameInterpolation.TangentSpline;
            if (isSplineInterpolation && InTangents.Count == KeyFrames.Count && OutTangents.Count == KeyFrames.Count)
            {
                return InterpolateSpline(Type, keyFrame.Value, nextKeyFrame.Value,
                    OutTangents[checked((int)frameIndex)], InTangents[checked((int)nextFrameIndex)], blendFactor);
            }

            if (Interpolation == KeyFrameInterpolation.Linear)
                return keyFrame.Value.Lerp(nextKeyFrame.Value, blendFactor);
        }

        return keyFrame.Value;
    }

    public Variant Sample(float time)
    {
        uint frameIndex = 0;
        return Sample(time, 0.0f, false, ref frameIndex);
    }

    public new VariantType GetType() => IsEmpty ? VariantType.None : KeyFrames[0].Value.Type;

    public void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        ArchiveSerialization.SerializeValue(archive, "name", ref Name);
        var type = Type;
        ArchiveSerialization.SerializeEnum(archive, "type", ref type, Variant.GetTypeNameList());
        ArchiveSerialization.SerializeEnum(archive, "interpolation", ref Interpolation, InterpolationNames);
        ArchiveSerialization.SerializeValue(archive, "splineTension", ref SplineTension);

        var size = checked((uint)KeyFrames.Count);
        using var block = archive.OpenArrayBlock("keyframes", size);
        if (archive.IsInput)
        {
            KeyFrames.Clear();
            InTangents.Clear();
            OutTangents.Clear();
        }

        for (var index = 0u; index < block.SizeHint; ++index)
        {
            var keyFrame = archive.IsInput ? default : KeyFrames[checked((int)index)];
            var time = keyFrame.Time;
            var value = archive.IsInput ? new Variant(type) : keyFrame.Value;
            var inTangent = archive.IsInput ? new Variant(type) : index < InTangents.Count ? InTangents[checked((int)index)] : new Variant(type);
            var outTangent = archive.IsInput ? new Variant(type) : index < OutTangents.Count ? OutTangents[checked((int)index)] : new Variant(type);

            using var keyFrameBlock = archive.OpenUnorderedBlock("keyframe");
            ArchiveSerialization.SerializeValue(archive, "time", ref time);
            ArchiveSerialization.SerializeVariantAsType(archive, "value", ref value, type);

            if (Interpolation == KeyFrameInterpolation.TangentSpline)
            {
                ArchiveSerialization.SerializeVariantAsType(archive, "in", ref inTangent, type);
                ArchiveSerialization.SerializeVariantAsType(archive, "out", ref outTangent, type);
            }

            if (archive.IsInput)
            {
                KeyFrames.Add(new VariantCurvePoint { Time = time, Value = value });
                if (Interpolation == KeyFrameInterpolation.TangentSpline)
                {
                    InTangents.Add(inTangent);
                    OutTangents.Add(outTangent);
                }
            }
        }

        if (archive.IsInput)
            Commit();
    }

    public uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Interpolation);
        hash.Add(SplineTension);
        foreach (var tangent in InTangents) hash.Add(tangent);
        foreach (var tangent in OutTangents) hash.Add(tangent);
        foreach (var keyFrame in KeyFrames) hash.Add(keyFrame);
        return unchecked((uint)hash.ToHashCode());
    }

    public bool Equals(VariantCurve? other)
    {
        return other is not null
            && Name == other.Name
            && Interpolation == other.Interpolation
            && SplineTension == other.SplineTension
            && ManagedSequence.Equal(InTangents, other.InTangents)
            && ManagedSequence.Equal(OutTangents, other.OutTangents)
            && ManagedSequence.Equal(KeyFrames, other.KeyFrames);
    }

    public override bool Equals(object? obj) => Equals(obj as VariantCurve);

    public override int GetHashCode() => unchecked((int)ToHash());

    public static bool operator ==(VariantCurve? lhs, VariantCurve? rhs) => ReferenceEquals(lhs, rhs) || lhs?.Equals(rhs) == true;
    public static bool operator !=(VariantCurve? lhs, VariantCurve? rhs) => !(lhs == rhs);

    private static Variant InterpolateSpline(VariantType type, Variant v1, Variant v2, Variant t1, Variant t2, float t)
    {
        var tt = t * t;
        var ttt = t * tt;
        var h1 = 2.0f * ttt - 3.0f * tt + 1.0f;
        var h2 = -2.0f * ttt + 3.0f * tt;
        var h3 = ttt - 2.0f * tt + t;
        var h4 = ttt - tt;

        return type switch
        {
            VariantType.Float => v1.GetFloat() * h1 + v2.GetFloat() * h2 + t1.GetFloat() * h3 + t2.GetFloat() * h4,
            VariantType.Vector2 => v1.GetVector2() * h1 + v2.GetVector2() * h2 + t1.GetVector2() * h3 + t2.GetVector2() * h4,
            VariantType.Vector3 => v1.GetVector3() * h1 + v2.GetVector3() * h2 + t1.GetVector3() * h3 + t2.GetVector3() * h4,
            VariantType.Vector4 => v1.GetVector4() * h1 + v2.GetVector4() * h2 + t1.GetVector4() * h3 + t2.GetVector4() * h4,
            VariantType.Quaternion => (v1.GetQuaternion() * h1 + v2.GetQuaternion() * h2 + t1.GetQuaternion() * h3 + t2.GetQuaternion() * h4).Normalized,
            VariantType.Color => v1.GetColor() * h1 + v2.GetColor() * h2 + t1.GetColor() * h3 + t2.GetColor() * h4,
            VariantType.Double => v1.GetDouble() * h1 + v2.GetDouble() * h2 + t1.GetDouble() * h3 + t2.GetDouble() * h4,
            _ => v1
        };
    }

    private static Variant SubtractAndMultiply(VariantType type, Variant v1, Variant v2, float t)
    {
        return type switch
        {
            VariantType.Float => (v1.GetFloat() - v2.GetFloat()) * t,
            VariantType.Vector2 => (v1.GetVector2() - v2.GetVector2()) * t,
            VariantType.Vector3 => (v1.GetVector3() - v2.GetVector3()) * t,
            VariantType.Vector4 => (v1.GetVector4() - v2.GetVector4()) * t,
            VariantType.Quaternion => (v1.GetQuaternion() - v2.GetQuaternion()) * t,
            VariantType.Color => (v1.GetColor() - v2.GetColor()) * t,
            VariantType.Double => (v1.GetDouble() - v2.GetDouble()) * t,
            _ => new Variant(type)
        };
    }

    private static readonly string[] InterpolationNames = ["None", "Linear", "Spline", "CubicSpline"];
}
