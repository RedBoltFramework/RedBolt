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
    private readonly List<VAnimKeyFrame> _keyFrames = [];
    private readonly List<VAnimEventFrame> _eventFrames = [];
    private object? _owner;
    private bool _interpolatable;
    private VariantType _valueType = VariantType.None;

    public ValueAnimation(string name)
        : base(name)
    {
    }

    public InterpMethod InterpolationMethod { get; private set; } = InterpMethod.Linear;

    public float SplineTension { get; private set; } = 0.5f;

    public VariantType ValueType => _valueType;

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
                eventData[new StringHash(dataElement.GetUInt("key"))] = VariantTypeUtility.DeserializeVariant(dataElement.GetValue(), type);
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

        foreach (var keyFrame in _keyFrames)
        {
            var element = dest.CreateChild("keyframe");
            element.SetFloat("time", keyFrame.Time);
            var type = VariantTypeUtility.GetVariantType(keyFrame.Value);
            element.SetAttribute("type", type.ToString());
            element.SetValue(VariantTypeUtility.Serialize(keyFrame.Value, type));
        }

        foreach (var eventFrame in _eventFrames)
        {
            var element = dest.CreateChild("eventframe");
            element.SetFloat("time", eventFrame.Time);
            element.SetUInt("eventtype", eventFrame.EventType.Value);
            var eventDataElement = element.CreateChild("eventdata");
            foreach (var pair in eventFrame.EventData)
            {
                var dataElement = eventDataElement.CreateChild("item");
                dataElement.SetUInt("key", pair.Key.Value);
                var type = pair.Value.Type;
                dataElement.SetAttribute("type", type.ToString());
                dataElement.SetValue(VariantTypeUtility.Serialize(pair.Value));
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
                eventData[new StringHash(item.Get("key").GetUInt())] = VariantTypeUtility.DeserializeVariant(item.Get("json").GetString(), type);
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
        foreach (var keyFrame in _keyFrames)
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
        foreach (var eventFrame in _eventFrames)
        {
            var frame = new JSONValue(JSONValueType.Object);
            frame.Set("time", new JSONValue(eventFrame.Time));
            frame.Set("eventtype", new JSONValue(eventFrame.EventType.Value));
            var eventDataArray = new JSONValue(JSONValueType.Array);
            foreach (var pair in eventFrame.EventData)
            {
                var type = pair.Value.Type;
                var item = new JSONValue(JSONValueType.Object);
                item.Set("key", new JSONValue(pair.Key.Value));
                item.Set("type", new JSONValue(type.ToString()));
                item.Set("json", new JSONValue(VariantTypeUtility.Serialize(pair.Value)));
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
        this._owner = owner;
    }

    public object? GetOwner() => _owner;

    public InterpMethod GetInterpolationMethod() => InterpolationMethod;

    public float GetSplineTension() => SplineTension;

    public VariantType GetValueType() => _valueType;

    public void SetInterpolationMethod(InterpMethod method)
    {
        if (method == InterpMethod.Spline && _valueType is VariantType.IntRect or VariantType.Vector2i or VariantType.Vector3i)
            method = InterpMethod.Linear;

        InterpolationMethod = method;
    }

    public void SetSplineTension(float tension)
    {
        SplineTension = tension;
    }

    public void SetValueType(VariantType valueType)
    {
        if (this._valueType == valueType)
            return;

        this._valueType = valueType;
        _interpolatable = VariantTypeUtility.IsInterpolatable(valueType);
        if (valueType is VariantType.IntRect or VariantType.Vector2i or VariantType.Vector3i && InterpolationMethod == InterpMethod.Spline)
            InterpolationMethod = InterpMethod.Linear;
        _keyFrames.Clear();
        _eventFrames.Clear();
        BeginTime = MathDefs.Infinity;
        EndTime = -MathDefs.Infinity;
    }

    public bool SetKeyFrame(float time, object? value)
    {
        var type = VariantTypeUtility.GetVariantType(value);
        if (type == VariantType.None)
            return false;

        if (_valueType == VariantType.None)
            SetValueType(type);
        else if (type != _valueType)
            return false;

        if (_keyFrames.Any(frame => frame.Time == time))
            return false;

        _keyFrames.Add(new VAnimKeyFrame { Time = time, Value = value });
        _keyFrames.Sort(static (left, right) => left.Time.CompareTo(right.Time));
        BeginTime = Math.Min(BeginTime, time);
        EndTime = Math.Max(EndTime, time);
        return true;
    }

    public void SetEventFrame(float time, StringHash eventType, VariantMap? eventData = null)
    {
        _eventFrames.Add(new VAnimEventFrame
        {
            Time = time,
            EventType = eventType,
            EventData = eventData is null ? new VariantMap() : new VariantMap(eventData)
        });
        _eventFrames.Sort(static (left, right) => left.Time.CompareTo(right.Time));
        BeginTime = Math.Min(BeginTime, time);
        EndTime = Math.Max(EndTime, time);
    }

    public bool IsValid()
    {
        return InterpolationMethod switch
        {
            InterpMethod.None => true,
            InterpMethod.Linear => _keyFrames.Count > 1,
            InterpMethod.Spline => _keyFrames.Count > 2,
            _ => false
        };
    }

    public object? GetAnimationValue(float scaledTime)
    {
        if (_keyFrames.Count == 0)
            return null;
        if (_keyFrames.Count == 1)
            return _keyFrames[0].Value;

        var index = 1;
        while (index < _keyFrames.Count && scaledTime >= _keyFrames[index].Time)
            ++index;

        if (index >= _keyFrames.Count)
            return _keyFrames[^1].Value;
        if (!_interpolatable || InterpolationMethod == InterpMethod.None)
            return _keyFrames[index - 1].Value;

        return InterpolationMethod == InterpMethod.Spline
            ? SplineInterpolation(index - 1, index, scaledTime)
            : LinearInterpolation(index - 1, index, scaledTime);
    }

    public IReadOnlyList<VAnimKeyFrame> GetKeyFrames() => _keyFrames;

    public bool HasEventFrames() => _eventFrames.Count != 0;

    public IReadOnlyList<VAnimEventFrame> GetEventFrames(float beginTime, float endTime)
    {
        var result = new List<VAnimEventFrame>();
        foreach (var frame in _eventFrames)
        {
            if (frame.Time >= beginTime && frame.Time <= endTime)
                result.Add(frame);
        }

        return result;
    }

    private object? LinearInterpolation(int index1, int index2, float scaledTime)
    {
        var lhs = _keyFrames[index1].Value;
        var rhs = _keyFrames[index2].Value;
        var denominator = _keyFrames[index2].Time - _keyFrames[index1].Time;
        var t = denominator == 0.0f ? 0.0f : (scaledTime - _keyFrames[index1].Time) / denominator;
        return Interpolate(lhs, rhs, t);
    }

    private object? SplineInterpolation(int index1, int index2, float scaledTime)
    {
        var denominator = _keyFrames[index2].Time - _keyFrames[index1].Time;
        var t = denominator == 0.0f ? 0.0f : (scaledTime - _keyFrames[index1].Time) / denominator;
        var tangent1 = index1 == 0
            ? Equals(_keyFrames[0].Value, _keyFrames[^1].Value)
                ? SubtractAndMultiply(_keyFrames[1].Value, _keyFrames[^2].Value, SplineTension)
                : SubtractAndMultiply(_keyFrames[0].Value, _keyFrames[0].Value, SplineTension)
            : SubtractAndMultiply(_keyFrames[index1 + 1].Value, _keyFrames[index1 - 1].Value, SplineTension);
        var tangent2 = index2 == _keyFrames.Count - 1
            ? Equals(_keyFrames[0].Value, _keyFrames[^1].Value)
                ? SubtractAndMultiply(_keyFrames[1].Value, _keyFrames[^2].Value, SplineTension)
                : SubtractAndMultiply(_keyFrames[^1].Value, _keyFrames[^1].Value, SplineTension)
            : SubtractAndMultiply(_keyFrames[index2 + 1].Value, _keyFrames[index2 - 1].Value, SplineTension);
        return Hermite(_keyFrames[index1].Value, tangent1, _keyFrames[index2].Value, tangent2, t);
    }

    private static object? Interpolate(object? lhs, object? rhs, float t)
    {
        return (lhs, rhs) switch
        {
            (float left, float right) => MathDefs.Lerp(left, right, t),
            (Vector2f left, Vector2f right) => left.Lerp(right, t),
            (Vector3f left, Vector3f right) => left.Lerp(right, t),
            (Vector4f left, Vector4f right) => left.Lerp(right, t),
            (Quaternion left, Quaternion right) => left.Slerp(right, t),
            (Color left, Color right) => left.Lerp(right, t),
            (Vector2i left, Vector2i right) => new Vector2i((int)(left.X * (1.0f - t) + right.X * t), (int)(left.Y * (1.0f - t) + right.Y * t)),
            (Vector3i left, Vector3i right) => new Vector3i((int)(left.X * (1.0f - t) + right.X * t), (int)(left.Y * (1.0f - t) + right.Y * t), (int)(left.Z * (1.0f - t) + right.Z * t)),
            (IntRect left, IntRect right) => new IntRect(
                (int)(left.Left * (1.0f - t) + right.Left * t),
                (int)(left.Top * (1.0f - t) + right.Top * t),
                (int)(left.Right * (1.0f - t) + right.Right * t),
                (int)(left.Bottom * (1.0f - t) + right.Bottom * t)),
            _ => lhs
        };
    }

    private static object? SubtractAndMultiply(object? value1, object? value2, float t)
    {
        return (value1, value2) switch
        {
            (float left, float right) => (left - right) * t,
            (Vector2f left, Vector2f right) => (left - right) * t,
            (Vector3f left, Vector3f right) => (left - right) * t,
            (Vector4f left, Vector4f right) => (left - right) * t,
            (Quaternion left, Quaternion right) => (left - right) * t,
            (Color left, Color right) => (left - right) * t,
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
            (Vector2f v1, Vector2f m1, Vector2f v2, Vector2f m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (Vector3f v1, Vector3f m1, Vector3f v2, Vector3f m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (Vector4f v1, Vector4f m1, Vector4f v2, Vector4f m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (Quaternion v1, Quaternion m1, Quaternion v2, Quaternion m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            (Color v1, Color m1, Color v2, Color m2) => v1 * h1 + v2 * h2 + m1 * h3 + m2 * h4,
            _ => Interpolate(value1, value2, t)
        };
    }

    private static InterpMethod ParseInterpolationMethod(string value)
    {
        return Enum.TryParse<InterpMethod>(value, ignoreCase: true, out var method) ? method : InterpMethod.Linear;
    }

    private static VariantType ParseVariantType(string value)
    {
        if (Enum.TryParse<VariantType>(value, ignoreCase: true, out var type))
            return type;

        return Variant.GetTypeFromName(value);
    }

    private void Reset()
    {
        _keyFrames.Clear();
        _eventFrames.Clear();
        _valueType = VariantType.None;
        _interpolatable = false;
        InterpolationMethod = InterpMethod.Linear;
        SplineTension = 0.5f;
        BeginTime = MathDefs.Infinity;
        EndTime = -MathDefs.Infinity;
    }
}
