namespace RedBolt;

public enum InterpMethod
{
    None = 0,
    Linear,
    Spline
}

public struct VAnimKeyFrame
{
    public float Time;
    public object? Value;
}

public struct VAnimEventFrame
{
    public float Time;
    public StringHash EventType;
    public VariantMap EventData;
}

public sealed class ValueAnimation : Resource
{
    private readonly List<VAnimKeyFrame> keyFrames = [];
    private readonly List<VAnimEventFrame> eventFrames = [];
    private object? owner;
    private bool interpolatable;
    private VariantType valueType = VariantType.None;

    public ValueAnimation(string name)
        : base(name)
    {
    }

    public InterpMethod InterpolationMethod { get; private set; } = InterpMethod.Linear;

    public float SplineTension { get; private set; } = 0.5f;

    public VariantType ValueType => valueType;

    public float BeginTime { get; private set; } = MathDefs.Infinity;

    public float EndTime { get; private set; } = -MathDefs.Infinity;

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var xml = new XMLFile(Name);
        return xml.Load(source) && LoadXML(xml.GetRoot());
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var xml = new XMLFile(Name);
        var root = xml.CreateRoot("valueanimation");
        return SaveXML(root) && xml.Save(destination);
    }

    public bool LoadXML(XMLElement source)
    {
        Reset();
        SetInterpolationMethod(ParseInterpolationMethod(source.GetAttribute("interpolationmethod")));
        if (InterpolationMethod == InterpMethod.Spline && source.HasAttribute("splinetension"))
            SetSplineTension(source.GetFloat("splinetension"));

        for (var keyFrameElement = source.GetChild("keyframe"); keyFrameElement.NotNull; keyFrameElement = keyFrameElement.GetNext("keyframe"))
        {
            var type = ParseVariantType(keyFrameElement.GetAttribute("type"));
            var value = VariantTypeUtility.Deserialize(keyFrameElement.GetValue(), type);
            if (!SetKeyFrame(keyFrameElement.GetFloat("time"), value))
                return false;
        }

        for (var eventFrameElement = source.GetChild("eventframe"); eventFrameElement.NotNull; eventFrameElement = eventFrameElement.GetNext("eventframe"))
        {
            var eventData = new VariantMap();
            for (var dataElement = eventFrameElement.GetChild("eventdata").GetChild("item"); dataElement.NotNull; dataElement = dataElement.GetNext("item"))
            {
                var type = ParseVariantType(dataElement.GetAttribute("type"));
                eventData[new StringHash(dataElement.GetUInt("key"))] = VariantTypeUtility.Deserialize(dataElement.GetValue(), type);
            }

            SetEventFrame(eventFrameElement.GetFloat("time"), new StringHash(eventFrameElement.GetUInt("eventtype")), eventData);
        }

        return true;
    }

    public bool SaveXML(XMLElement dest)
    {
        dest.SetAttribute("interpolationmethod", InterpolationMethod.ToString());
        if (InterpolationMethod == InterpMethod.Spline)
            dest.SetFloat("splinetension", SplineTension);

        foreach (var keyFrame in keyFrames)
        {
            var element = dest.CreateChild("keyframe");
            element.SetFloat("time", keyFrame.Time);
            var type = VariantTypeUtility.GetVariantType(keyFrame.Value);
            element.SetAttribute("type", type.ToString());
            element.SetValue(VariantTypeUtility.Serialize(keyFrame.Value, type));
        }

        foreach (var eventFrame in eventFrames)
        {
            var element = dest.CreateChild("eventframe");
            element.SetFloat("time", eventFrame.Time);
            element.SetUInt("eventtype", eventFrame.EventType.Value);
            var eventDataElement = element.CreateChild("eventdata");
            foreach (var pair in eventFrame.EventData)
            {
                var dataElement = eventDataElement.CreateChild("item");
                dataElement.SetUInt("key", pair.Key.Value);
                var type = VariantTypeUtility.GetVariantType(pair.Value);
                dataElement.SetAttribute("type", type.ToString());
                dataElement.SetValue(VariantTypeUtility.Serialize(pair.Value, type));
            }
        }

        return true;
    }

    public bool LoadJSON(JSONValue source)
    {
        Reset();
        SetInterpolationMethod(ParseInterpolationMethod(source.Get("interpolationmethod").GetString()));
        if (InterpolationMethod == InterpMethod.Spline)
            SetSplineTension(source.Get("splinetension").GetFloat());

        foreach (var frameValue in source.Get("keyframes").GetArray())
        {
            var type = ParseVariantType(frameValue.Get("type").GetString());
            var value = VariantTypeUtility.Deserialize(frameValue.Get("json").GetString(), type);
            if (!SetKeyFrame(frameValue.Get("time").GetFloat(), value))
                return false;
        }

        foreach (var eventFrameValue in source.Get("eventframes").GetArray())
        {
            var eventData = new VariantMap();
            foreach (var item in eventFrameValue.Get("eventdata").GetArray())
            {
                var type = ParseVariantType(item.Get("type").GetString());
                eventData[new StringHash(item.Get("key").GetUInt())] = VariantTypeUtility.Deserialize(item.Get("json").GetString(), type);
            }

            SetEventFrame(eventFrameValue.Get("time").GetFloat(), new StringHash(eventFrameValue.Get("eventtype").GetUInt()), eventData);
        }

        return true;
    }

    public bool SaveJSON(JSONValue dest)
    {
        dest.Set("interpolationmethod", new JSONValue(InterpolationMethod.ToString()));
        if (InterpolationMethod == InterpMethod.Spline)
            dest.Set("splinetension", new JSONValue(SplineTension));

        var keyFrameArray = new JSONValue(JSONValueType.Array);
        foreach (var keyFrame in keyFrames)
        {
            var type = VariantTypeUtility.GetVariantType(keyFrame.Value);
            var frame = new JSONValue(JSONValueType.Object);
            frame.Set("time", new JSONValue(keyFrame.Time));
            frame.Set("type", new JSONValue(type.ToString()));
            frame.Set("json", new JSONValue(VariantTypeUtility.Serialize(keyFrame.Value, type)));
            keyFrameArray.Push(frame);
        }
        dest.Set("keyframes", keyFrameArray);

        var eventFrameArray = new JSONValue(JSONValueType.Array);
        foreach (var eventFrame in eventFrames)
        {
            var frame = new JSONValue(JSONValueType.Object);
            frame.Set("time", new JSONValue(eventFrame.Time));
            frame.Set("eventtype", new JSONValue(eventFrame.EventType.Value));
            var eventDataArray = new JSONValue(JSONValueType.Array);
            foreach (var pair in eventFrame.EventData)
            {
                var type = VariantTypeUtility.GetVariantType(pair.Value);
                var item = new JSONValue(JSONValueType.Object);
                item.Set("key", new JSONValue(pair.Key.Value));
                item.Set("type", new JSONValue(type.ToString()));
                item.Set("json", new JSONValue(VariantTypeUtility.Serialize(pair.Value, type)));
                eventDataArray.Push(item);
            }
            frame.Set("eventdata", eventDataArray);
            eventFrameArray.Push(frame);
        }
        dest.Set("eventframes", eventFrameArray);

        return true;
    }

    public void SetOwner(object? owner)
    {
        this.owner = owner;
    }

    public object? GetOwner() => owner;

    public InterpMethod GetInterpolationMethod() => InterpolationMethod;

    public float GetSplineTension() => SplineTension;

    public VariantType GetValueType() => valueType;

    public void SetInterpolationMethod(InterpMethod method)
    {
        if (method == InterpMethod.Spline && valueType is VariantType.IntRect or VariantType.IntVector2 or VariantType.IntVector3)
            method = InterpMethod.Linear;

        InterpolationMethod = method;
    }

    public void SetSplineTension(float tension)
    {
        SplineTension = tension;
    }

    public void SetValueType(VariantType valueType)
    {
        if (this.valueType == valueType)
            return;

        this.valueType = valueType;
        interpolatable = VariantTypeUtility.IsInterpolatable(valueType);
        if (valueType is VariantType.IntRect or VariantType.IntVector2 or VariantType.IntVector3 && InterpolationMethod == InterpMethod.Spline)
            InterpolationMethod = InterpMethod.Linear;
        keyFrames.Clear();
        eventFrames.Clear();
        BeginTime = MathDefs.Infinity;
        EndTime = -MathDefs.Infinity;
    }

    public bool SetKeyFrame(float time, object? value)
    {
        var type = VariantTypeUtility.GetVariantType(value);
        if (type == VariantType.None)
            return false;

        if (valueType == VariantType.None)
            SetValueType(type);
        else if (type != valueType)
            return false;

        if (keyFrames.Any(frame => MathDefs.ApproximatelyEquivalent(frame.Time, time)))
            return false;

        keyFrames.Add(new VAnimKeyFrame { Time = time, Value = value });
        keyFrames.Sort(static (left, right) => left.Time.CompareTo(right.Time));
        BeginTime = Math.Min(BeginTime, time);
        EndTime = Math.Max(EndTime, time);
        return true;
    }

    public void SetEventFrame(float time, StringHash eventType, VariantMap? eventData = null)
    {
        eventFrames.Add(new VAnimEventFrame
        {
            Time = time,
            EventType = eventType,
            EventData = eventData is null ? new VariantMap() : new VariantMap(eventData)
        });
        eventFrames.Sort(static (left, right) => left.Time.CompareTo(right.Time));
        BeginTime = Math.Min(BeginTime, time);
        EndTime = Math.Max(EndTime, time);
    }

    public bool IsValid()
    {
        return InterpolationMethod switch
        {
            InterpMethod.None => keyFrames.Count > 0,
            InterpMethod.Linear => keyFrames.Count > 1,
            InterpMethod.Spline => keyFrames.Count > 2,
            _ => false
        };
    }

    public object? GetAnimationValue(float scaledTime)
    {
        if (keyFrames.Count == 0)
            return null;
        if (keyFrames.Count == 1)
            return keyFrames[0].Value;

        var index = 1;
        while (index < keyFrames.Count && scaledTime >= keyFrames[index].Time)
            ++index;

        if (index >= keyFrames.Count)
            return keyFrames[^1].Value;
        if (!interpolatable || InterpolationMethod == InterpMethod.None)
            return keyFrames[index - 1].Value;

        return InterpolationMethod == InterpMethod.Spline
            ? SplineInterpolation(index - 1, index, scaledTime)
            : LinearInterpolation(index - 1, index, scaledTime);
    }

    public IReadOnlyList<VAnimKeyFrame> GetKeyFrames() => keyFrames;

    public bool HasEventFrames() => eventFrames.Count != 0;

    public IReadOnlyList<VAnimEventFrame> GetEventFrames(float beginTime, float endTime)
    {
        var result = new List<VAnimEventFrame>();
        foreach (var frame in eventFrames)
        {
            if (frame.Time > beginTime && frame.Time <= endTime)
                result.Add(frame);
        }

        return result;
    }

    private object? LinearInterpolation(int index1, int index2, float scaledTime)
    {
        var lhs = keyFrames[index1].Value;
        var rhs = keyFrames[index2].Value;
        var denominator = keyFrames[index2].Time - keyFrames[index1].Time;
        var t = denominator == 0.0f ? 0.0f : (scaledTime - keyFrames[index1].Time) / denominator;
        return Interpolate(lhs, rhs, t);
    }

    private object? SplineInterpolation(int index1, int index2, float scaledTime)
    {
        var denominator = keyFrames[index2].Time - keyFrames[index1].Time;
        var t = denominator == 0.0f ? 0.0f : (scaledTime - keyFrames[index1].Time) / denominator;
        var previous = index1 > 0 ? keyFrames[index1 - 1].Value : keyFrames[index1].Value;
        var next = index2 + 1 < keyFrames.Count ? keyFrames[index2 + 1].Value : keyFrames[index2].Value;
        var scale = (1.0f - SplineTension) * 0.5f;
        var tangent1 = SubtractAndMultiply(keyFrames[index2].Value, previous, scale);
        var tangent2 = SubtractAndMultiply(next, keyFrames[index1].Value, scale);
        return Hermite(keyFrames[index1].Value, tangent1, keyFrames[index2].Value, tangent2, t);
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
            (IntVector2 left, IntVector2 right) => new IntVector2(MathDefs.RoundToInt(MathDefs.Lerp(left.X, right.X, t)), MathDefs.RoundToInt(MathDefs.Lerp(left.Y, right.Y, t))),
            (IntVector3 left, IntVector3 right) => new IntVector3(MathDefs.RoundToInt(MathDefs.Lerp(left.X, right.X, t)), MathDefs.RoundToInt(MathDefs.Lerp(left.Y, right.Y, t)), MathDefs.RoundToInt(MathDefs.Lerp(left.Z, right.Z, t))),
            (IntRect left, IntRect right) => new IntRect(
                MathDefs.RoundToInt(MathDefs.Lerp(left.Left, right.Left, t)),
                MathDefs.RoundToInt(MathDefs.Lerp(left.Top, right.Top, t)),
                MathDefs.RoundToInt(MathDefs.Lerp(left.Right, right.Right, t)),
                MathDefs.RoundToInt(MathDefs.Lerp(left.Bottom, right.Bottom, t))),
            _ => lhs
        };
    }

    private static object? SubtractAndMultiply(object? value1, object? value2, float t)
    {
        return (value1, value2) switch
        {
            (float left, float right) => (left - right) * t,
            (Vector2 left, Vector2 right) => (left - right) * t,
            (Vector3 left, Vector3 right) => (left - right) * t,
            (Vector4 left, Vector4 right) => (left - right) * t,
            (Color left, Color right) => (left - right) * t,
            (IntVector2 left, IntVector2 right) => (left - right).ToVector2() * t,
            (IntVector3 left, IntVector3 right) => (left - right).ToVector3() * t,
            (IntRect left, IntRect right) => new Vector4(left.Left - right.Left, left.Top - right.Top, left.Right - right.Right, left.Bottom - right.Bottom) * t,
            _ => null
        };
    }

    private static object? Hermite(object? value1, object? tangent1, object? value2, object? tangent2, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        var h1 = 2.0f * t3 - 3.0f * t2 + 1.0f;
        var h2 = -2.0f * t3 + 3.0f * t2;
        var h3 = t3 - 2.0f * t2 + t;
        var h4 = t3 - t2;

        return (value1, tangent1, value2, tangent2) switch
        {
            (float v1, float m1, float v2, float m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (Vector2 v1, Vector2 m1, Vector2 v2, Vector2 m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (Vector3 v1, Vector3 m1, Vector3 v2, Vector3 m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (Vector4 v1, Vector4 m1, Vector4 v2, Vector4 m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (Color v1, Color m1, Color v2, Color m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (IntVector2 v1, Vector2 m1, IntVector2 v2, Vector2 m2) => new IntVector2(
                MathDefs.RoundToInt(v1.X * h1 + v2.X * h2 + m1.X * h3 + m2.X * h4),
                MathDefs.RoundToInt(v1.Y * h1 + v2.Y * h2 + m1.Y * h3 + m2.Y * h4)),
            (IntVector3 v1, Vector3 m1, IntVector3 v2, Vector3 m2) => new IntVector3(
                MathDefs.RoundToInt(v1.X * h1 + v2.X * h2 + m1.X * h3 + m2.X * h4),
                MathDefs.RoundToInt(v1.Y * h1 + v2.Y * h2 + m1.Y * h3 + m2.Y * h4),
                MathDefs.RoundToInt(v1.Z * h1 + v2.Z * h2 + m1.Z * h3 + m2.Z * h4)),
            (IntRect v1, Vector4 m1, IntRect v2, Vector4 m2) => new IntRect(
                MathDefs.RoundToInt(v1.Left * h1 + v2.Left * h2 + m1.X * h3 + m2.X * h4),
                MathDefs.RoundToInt(v1.Top * h1 + v2.Top * h2 + m1.Y * h3 + m2.Y * h4),
                MathDefs.RoundToInt(v1.Right * h1 + v2.Right * h2 + m1.Z * h3 + m2.Z * h4),
                MathDefs.RoundToInt(v1.Bottom * h1 + v2.Bottom * h2 + m1.W * h3 + m2.W * h4)),
            _ => Interpolate(value1, value2, t)
        };
    }

    private static InterpMethod ParseInterpolationMethod(string value)
    {
        return Enum.TryParse<InterpMethod>(value, ignoreCase: true, out var method) ? method : InterpMethod.Linear;
    }

    private static VariantType ParseVariantType(string value)
    {
        return Enum.TryParse<VariantType>(value, ignoreCase: true, out var type) ? type : VariantType.None;
    }

    private void Reset()
    {
        keyFrames.Clear();
        eventFrames.Clear();
        valueType = VariantType.None;
        interpolatable = false;
        InterpolationMethod = InterpMethod.Linear;
        SplineTension = 0.5f;
        BeginTime = MathDefs.Infinity;
        EndTime = -MathDefs.Infinity;
    }
}
