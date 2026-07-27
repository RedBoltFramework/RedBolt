using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

namespace RedBolt.Scene;

/// <summary>Interpolation method.</summary>
public enum InterpMethod
{
    /// <summary>No interpolation.</summary>
    None = 0,
    /// <summary>Linear interpolation (default).</summary>
    Linear,
    /// <summary>Cardinal spline interpolation, default tension value is 0.5f. For more information please refer to http://cubic.org/docs/hermite.htm.</summary>
    Spline
}

/// <summary>Value animation key frame.</summary>
public struct VAnimKeyFrame : ICloneable, IDisposable
{
    /// <summary>Time.</summary>
    public float Time;
    /// <summary>Value.</summary>
    public Variant Value;

    public readonly VAnimKeyFrame Clone() => new() { Time = Time, Value = Value.Clone() };

    readonly object ICloneable.Clone() => Clone();

    public void Dispose() => Value.Dispose();
}

/// <summary>Value animation event frame.</summary>
public struct VAnimEventFrame : ICloneable, IDisposable
{
    /// <summary>Time.</summary>
    public float Time;
    /// <summary>Event type.</summary>
    public StringHash EventType;
    /// <summary>Event data.</summary>
    public VariantMap EventData;

    public VAnimEventFrame()
    {
        EventData = new VariantMap();
    }

    public readonly VAnimEventFrame Clone()
    {
        var result = new VAnimEventFrame
        {
            Time = Time,
            EventType = EventType
        };
        result.EventData.Dispose();
        result.EventData = new VariantMap(EventData);
        return result;
    }

    readonly object ICloneable.Clone() => Clone();

    public void Dispose() => EventData?.Dispose();
}

/// <summary>Value animation class.</summary>
public class ValueAnimation : ResourceType
{
    /// <summary>Owner.</summary>
    private object? _owner;
    /// <summary>Interpolation method.</summary>
    private InterpMethod _interpolationMethod = InterpMethod.Linear;
    /// <summary>Spline tension.</summary>
    private float _splineTension = 0.5f;
    /// <summary>Value type.</summary>
    private VariantType _valueType;
    /// <summary>Interpolatable flag.</summary>
    private bool _interpolatable;
    /// <summary>Begin time.</summary>
    private float _beginTime = MathDefs.Infinity;
    /// <summary>End time.</summary>
    private float _endTime = -MathDefs.Infinity;
    /// <summary>Key frames.</summary>
    private readonly List<VAnimKeyFrame> _keyFrames = [];
    /// <summary>Spline tangents.</summary>
    private readonly List<Variant> _splineTangents = [];
    /// <summary>Spline tangents dirty.</summary>
    private bool _splineTangentsDirty;
    /// <summary>Event frames.</summary>
    private readonly List<VAnimEventFrame> _eventFrames = [];

    /// <summary>Construct.</summary>
    public ValueAnimation()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        ClearEventFrames();
        ClearSplineTangents();
        ClearKeyFrames();
        _owner = null;
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<ValueAnimation>();
    }

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        using var xmlFile = new XMLFile();
        if (!xmlFile.Load(source))
            return false;

        return LoadXML(xmlFile.GetRoot());
    }

    /// <summary>Save resource. Return true if successful.</summary>
    public override bool Save(Serializer destination)
    {
        using var xmlFile = new XMLFile();

        var rootElement = xmlFile.CreateRoot("valueanimation");
        if (!SaveXML(rootElement))
            return false;

        return xmlFile.Save(destination);
    }

    /// <summary>Load from XML data. Return true if successful.</summary>
    public bool LoadXML(XMLElement source)
    {
        _valueType = VariantType.None;
        ClearEventFrames();

        var method = ParseInterpolationMethod(source.GetAttribute("interpolationmethod"));

        InterpolationMethod = method;
        if (_interpolationMethod == InterpMethod.Spline)
            _splineTension = source.GetFloat("splinetension");

        for (var keyFrameElement = source.GetChild("keyframe"); keyFrameElement.NotNull; keyFrameElement = keyFrameElement.GetNext("keyframe"))
        {
            var time = keyFrameElement.GetFloat("time");
            using var value = keyFrameElement.GetVariant();
            SetKeyFrame(time, value);
        }

        for (var eventFrameElement = source.GetChild("eventframe"); eventFrameElement.NotNull; eventFrameElement = eventFrameElement.GetNext("eventframe"))
        {
            var time = eventFrameElement.GetFloat("time");
            var eventType = eventFrameElement.GetUInt("eventtype");
            using var eventData = eventFrameElement.GetChild("eventdata").GetVariantMap();

            SetEventFrame(time, new StringHash(eventType), eventData);
        }

        return true;
    }

    /// <summary>Save as XML data. Return true if successful.</summary>
    public bool SaveXML(XMLElement dest)
    {
        dest.SetAttribute("interpolationmethod", _interpolationMethod.ToString());
        if (_interpolationMethod == InterpMethod.Spline)
            dest.SetFloat("splinetension", _splineTension);

        for (var i = 0; i < _keyFrames.Count; ++i)
        {
            var keyFrame = _keyFrames[i];
            var keyFrameElement = dest.CreateChild("keyframe");
            keyFrameElement.SetFloat("time", keyFrame.Time);
            keyFrameElement.SetVariant(keyFrame.Value);
        }

        for (var i = 0; i < _eventFrames.Count; ++i)
        {
            var eventFrame = _eventFrames[i];
            var eventFrameElement = dest.CreateChild("eventframe");
            eventFrameElement.SetFloat("time", eventFrame.Time);
            eventFrameElement.SetUInt("eventtype", eventFrame.EventType.Value);
            eventFrameElement.CreateChild("eventdata").SetVariantMap(eventFrame.EventData);
        }

        return true;
    }

    /// <summary>Load from JSON data. Return true if successful.</summary>
    public bool LoadJSON(JSONValue source)
    {
        _valueType = VariantType.None;
        ClearEventFrames();

        var method = ParseInterpolationMethod(source.Get("interpolationmethod").GetString());

        InterpolationMethod = method;
        if (_interpolationMethod == InterpMethod.Spline)
            _splineTension = source.Get("splinetension").GetFloat();

        var keyFramesArray = source.Get("keyframes").GetArray();
        for (var i = 0; i < keyFramesArray.Count; ++i)
        {
            var valueElement = keyFramesArray[i];
            var time = valueElement.Get("time").GetFloat();
            using var value = valueElement.Get("value").GetVariant();
            SetKeyFrame(time, value);
        }

        var eventFramesArray = source.Get("eventframes").GetArray();
        for (var i = 0; i < eventFramesArray.Count; ++i)
        {
            var eventFrameValue = eventFramesArray[i];
            var time = eventFrameValue.Get("time").GetFloat();
            var eventType = eventFrameValue.Get("eventtype").GetUInt();
            using var eventData = eventFrameValue.Get("eventdata").GetVariantMap();
            SetEventFrame(time, new StringHash(eventType), eventData);
        }

        return true;
    }

    /// <summary>Save as XML data. Return true if successful.</summary>
    public bool SaveJSON(JSONValue dest)
    {
        dest.Set("interpolationmethod", new JSONValue(_interpolationMethod.ToString()));
        if (_interpolationMethod == InterpMethod.Spline)
            dest.Set("splinetension", new JSONValue(_splineTension));

        var keyFramesArray = new JSONValue(JSONValueType.Array);
        for (var i = 0; i < _keyFrames.Count; ++i)
        {
            var keyFrame = _keyFrames[i];
            var keyFrameValue = new JSONValue(JSONValueType.Object);
            keyFrameValue.Set("time", new JSONValue(keyFrame.Time));
            var valueValue = new JSONValue();
            valueValue.SetVariant(keyFrame.Value);
            keyFrameValue.Set("value", valueValue);
            keyFramesArray.Push(keyFrameValue);
        }
        dest.Set("keyframes", keyFramesArray);

        var eventFramesArray = new JSONValue(JSONValueType.Array);
        for (var i = 0; i < _eventFrames.Count; ++i)
        {
            var eventFrame = _eventFrames[i];
            var eventFrameValue = new JSONValue(JSONValueType.Object);
            eventFrameValue.Set("time", new JSONValue(eventFrame.Time));
            eventFrameValue.Set("eventtype", new JSONValue(eventFrame.EventType.Value));
            var eventDataValue = new JSONValue();
            eventDataValue.SetVariantMap(eventFrame.EventData);
            eventFrameValue.Set("eventdata", eventDataValue);
            eventFramesArray.Push(eventFrameValue);
        }
        dest.Set("eventframes", eventFramesArray);

        return true;
    }

    /// <summary>Set owner.</summary>
    public object? Owner
    {
        get => _owner;
        set => _owner = value;
    }

    /// <summary>Set interpolation method.</summary>
    public InterpMethod InterpolationMethod
    {
        get => _interpolationMethod;
        set
        {
            var method = value;
            if (method == _interpolationMethod)
                return;

            if (method == InterpMethod.Spline && _valueType is VariantType.IntRect or VariantType.Vector2i or VariantType.Vector3i)
                method = InterpMethod.Linear;

            _interpolationMethod = method;
            _splineTangentsDirty = true;
        }
    }

    /// <summary>Set spline tension, should be between 0.0f and 1.0f, but this is not a must.</summary>
    public float SplineTension
    {
        get => _splineTension;
        set
        {
            _splineTension = value;
            _splineTangentsDirty = true;
        }
    }

    /// <summary>Set value type.</summary>
    public VariantType ValueType
    {
        get => _valueType;
        set
        {
            if (value == _valueType)
                return;

            _valueType = value;
            _interpolatable = _valueType is VariantType.Float or VariantType.Vector2 or VariantType.Vector3 or VariantType.Vector4
                or VariantType.Quaternion or VariantType.Color;

            if (_valueType is VariantType.IntRect or VariantType.Vector2i or VariantType.Vector3i)
            {
                _interpolatable = true;
                if (_interpolationMethod == InterpMethod.Spline)
                    _interpolationMethod = InterpMethod.Linear;
            }

            ClearKeyFrames();
            ClearEventFrames();
            _beginTime = MathDefs.Infinity;
            _endTime = -MathDefs.Infinity;
        }
    }

    /// <summary>Set key frame.</summary>
    public bool SetKeyFrame(float time, in Variant value)
    {
        if (_valueType == VariantType.None)
            ValueType = value.Type;
        else if (value.Type != _valueType)
            return false;

        var keyFrame = new VAnimKeyFrame { Time = time, Value = value.Clone() };

        if (_keyFrames.Count == 0 || time > _keyFrames[^1].Time)
            _keyFrames.Add(keyFrame);
        else
        {
            for (var i = 0; i < _keyFrames.Count; ++i)
            {
                if (time == _keyFrames[i].Time)
                {
                    keyFrame.Dispose();
                    return false;
                }
                if (time < _keyFrames[i].Time)
                {
                    _keyFrames.Insert(i, keyFrame);
                    break;
                }
            }
        }

        _beginTime = SystemMath.Min(time, _beginTime);
        _endTime = SystemMath.Max(time, _endTime);
        _splineTangentsDirty = true;

        return true;
    }

    /// <summary>Set event frame.</summary>
    public void SetEventFrame(float time, StringHash eventType, VariantMap? eventData = null)
    {
        var eventFrame = new VAnimEventFrame
        {
            Time = time,
            EventType = eventType
        };
        eventFrame.EventData.Dispose();
        eventFrame.EventData = eventData is null ? new VariantMap() : new VariantMap(eventData);

        if (_eventFrames.Count == 0 || time >= _eventFrames[^1].Time)
            _eventFrames.Add(eventFrame);
        else
        {
            for (var i = 0; i < _eventFrames.Count; ++i)
            {
                if (time < _eventFrames[i].Time)
                {
                    _eventFrames.Insert(i, eventFrame);
                    break;
                }
            }
        }

        _beginTime = SystemMath.Min(time, _beginTime);
        _endTime = SystemMath.Max(time, _endTime);
    }

    /// <summary>Return animation is valid.</summary>
    public bool IsValid()
    {
        return _interpolationMethod == InterpMethod.None
            || _interpolationMethod == InterpMethod.Linear && _keyFrames.Count > 1
            || _interpolationMethod == InterpMethod.Spline && _keyFrames.Count > 2;
    }

    /// <summary>Return begin time.</summary>
    public float BeginTime => _beginTime;

    /// <summary>Return end time.</summary>
    public float EndTime => _endTime;

    /// <summary>Return animation value.</summary>
    public Variant GetAnimationValue(float scaledTime)
    {
        var index = 1;
        for (; index < _keyFrames.Count; ++index)
        {
            if (scaledTime < _keyFrames[index].Time)
                break;
        }

        if (index >= _keyFrames.Count || !_interpolatable || _interpolationMethod == InterpMethod.None)
            return _keyFrames[index - 1].Value.Clone();

        return _interpolationMethod == InterpMethod.Linear
            ? LinearInterpolation(index - 1, index, scaledTime)
            : SplineInterpolation(index - 1, index, scaledTime);
    }

    /// <summary>Return all key frames.</summary>
    public IReadOnlyList<VAnimKeyFrame> KeyFrames => _keyFrames;

    /// <summary>Has event frames.</summary>
    public bool HasEventFrames => _eventFrames.Count != 0;

    /// <summary>Return all event frames between time.</summary>
    public void GetEventFrames(float beginTime, float endTime, List<VAnimEventFrame> eventFrames)
    {
        for (var i = 0; i < _eventFrames.Count; ++i)
        {
            var eventFrame = _eventFrames[i];
            if (eventFrame.Time > endTime)
                break;

            if (eventFrame.Time >= beginTime)
                eventFrames.Add(eventFrame);
        }
    }

    /// <summary>Linear interpolation.</summary>
    private Variant LinearInterpolation(int index1, int index2, float scaledTime)
    {
        var keyFrame1 = _keyFrames[index1];
        var keyFrame2 = _keyFrames[index2];

        var t = (scaledTime - keyFrame1.Time) / (keyFrame2.Time - keyFrame1.Time);
        var value1 = keyFrame1.Value;
        var value2 = keyFrame2.Value;

        switch (_valueType)
        {
        case VariantType.Float:
            return new Variant(MathDefs.Lerp(value1.GetFloat(), value2.GetFloat(), t));
        case VariantType.Vector2:
            return new Variant(value1.GetVector2().Lerp(value2.GetVector2(), t));
        case VariantType.Vector3:
            return new Variant(value1.GetVector3().Lerp(value2.GetVector3(), t));
        case VariantType.Vector4:
            return new Variant(value1.GetVector4().Lerp(value2.GetVector4(), t));
        case VariantType.Quaternion:
            return new Variant(value1.GetQuaternion().Slerp(value2.GetQuaternion(), t));
        case VariantType.Color:
            return new Variant(value1.GetColor().Lerp(value2.GetColor(), t));
        case VariantType.IntRect:
        {
            var s = 1.0f - t;
            var r1 = value1.GetIntRect();
            var r2 = value2.GetIntRect();
            return new Variant(new IntRect((int)(r1.Left * s + r2.Left * t), (int)(r1.Top * s + r2.Top * t),
                (int)(r1.Right * s + r2.Right * t), (int)(r1.Bottom * s + r2.Bottom * t)));
        }
        case VariantType.Vector2i:
        {
            var s = 1.0f - t;
            var v1 = value1.GetIntVector2();
            var v2 = value2.GetIntVector2();
            return new Variant(new Vector2i((int)(v1.X * s + v2.X * t), (int)(v1.Y * s + v2.Y * t)));
        }
        case VariantType.Vector3i:
        {
            var s = 1.0f - t;
            var v1 = value1.GetIntVector3();
            var v2 = value2.GetIntVector3();
            return new Variant(new Vector3i((int)(v1.X * s + v2.X * t), (int)(v1.Y * s + v2.Y * t),
                (int)(v1.Z * s + v2.Z * t)));
        }
        case VariantType.Double:
            return new Variant(value1.GetDouble() * (1.0f - t) + value2.GetDouble() * t);
        default:
            Log.GetLogger().LogError("Invalid value type for linear interpolation");
            return default;
        }
    }

    /// <summary>Spline interpolation.</summary>
    private Variant SplineInterpolation(int index1, int index2, float scaledTime)
    {
        if (_splineTangentsDirty)
            UpdateSplineTangents();

        var keyFrame1 = _keyFrames[index1];
        var keyFrame2 = _keyFrames[index2];
        var t = (scaledTime - keyFrame1.Time) / (keyFrame2.Time - keyFrame1.Time);
        var tt = t * t;
        var ttt = t * tt;
        var h1 = 2.0f * ttt - 3.0f * tt + 1.0f;
        var h2 = -2.0f * ttt + 3.0f * tt;
        var h3 = ttt - 2.0f * tt + t;
        var h4 = ttt - tt;
        var v1 = keyFrame1.Value;
        var v2 = keyFrame2.Value;
        var tangent1 = _splineTangents[index1];
        var tangent2 = _splineTangents[index2];

        return _valueType switch
        {
            VariantType.Float => new Variant(v1.GetFloat() * h1 + v2.GetFloat() * h2 + tangent1.GetFloat() * h3 + tangent2.GetFloat() * h4),
            VariantType.Vector2 => new Variant(v1.GetVector2() * h1 + v2.GetVector2() * h2 + tangent1.GetVector2() * h3 + tangent2.GetVector2() * h4),
            VariantType.Vector3 => new Variant(v1.GetVector3() * h1 + v2.GetVector3() * h2 + tangent1.GetVector3() * h3 + tangent2.GetVector3() * h4),
            VariantType.Vector4 => new Variant(v1.GetVector4() * h1 + v2.GetVector4() * h2 + tangent1.GetVector4() * h3 + tangent2.GetVector4() * h4),
            VariantType.Quaternion => new Variant(v1.GetQuaternion() * h1 + v2.GetQuaternion() * h2 + tangent1.GetQuaternion() * h3 + tangent2.GetQuaternion() * h4),
            VariantType.Color => new Variant(v1.GetColor() * h1 + v2.GetColor() * h2 + tangent1.GetColor() * h3 + tangent2.GetColor() * h4),
            VariantType.Double => new Variant(v1.GetDouble() * h1 + v2.GetDouble() * h2 + tangent1.GetDouble() * h3 + tangent2.GetDouble() * h4),
            _ => InvalidSplineInterpolation()
        };
    }

    /// <summary>Update spline tangents.</summary>
    private void UpdateSplineTangents()
    {
        ClearSplineTangents();

        if (!IsValid())
            return;

        var size = _keyFrames.Count;
        for (var i = 0; i < size; ++i)
            _splineTangents.Add(default);

        for (var i = 1; i < size - 1; ++i)
        {
            using var tangent = SubstractAndMultiply(_keyFrames[i + 1].Value, _keyFrames[i - 1].Value, _splineTension);
            SetSplineTangent(i, tangent);
        }

        if (_keyFrames[0].Value != _keyFrames[size - 1].Value)
        {
            using var tangent = SubstractAndMultiply(_keyFrames[0].Value, _keyFrames[0].Value, _splineTension);
            SetSplineTangent(size - 1, tangent);
            SetSplineTangent(0, tangent);
        }
        else
        {
            using var tangent = SubstractAndMultiply(_keyFrames[1].Value, _keyFrames[size - 2].Value, _splineTension);
            SetSplineTangent(size - 1, tangent);
            SetSplineTangent(0, tangent);
        }

        _splineTangentsDirty = false;
    }

    /// <summary>Return (value1 - value2) * t.</summary>
    private Variant SubstractAndMultiply(in Variant value1, in Variant value2, float t)
    {
        return _valueType switch
        {
            VariantType.Float => new Variant((value1.GetFloat() - value2.GetFloat()) * t),
            VariantType.Vector2 => new Variant((value1.GetVector2() - value2.GetVector2()) * t),
            VariantType.Vector3 => new Variant((value1.GetVector3() - value2.GetVector3()) * t),
            VariantType.Vector4 => new Variant((value1.GetVector4() - value2.GetVector4()) * t),
            VariantType.Quaternion => new Variant((value1.GetQuaternion() - value2.GetQuaternion()) * t),
            VariantType.Color => new Variant((value1.GetColor() - value2.GetColor()) * t),
            VariantType.Double => new Variant((value1.GetDouble() - value2.GetDouble()) * t),
            _ => InvalidSplineSubtraction()
        };
    }

    private static InterpMethod ParseInterpolationMethod(string value)
    {
        return Enum.TryParse<InterpMethod>(value, true, out var method) ? method : InterpMethod.Linear;
    }

    private Variant InvalidSplineInterpolation()
    {
        Log.GetLogger().LogError("Invalid value type for spline interpolation");
        return default;
    }

    private Variant InvalidSplineSubtraction()
    {
        Log.GetLogger().LogError("Invalid value type for spline interpolation's subtract and multiply operation");
        return default;
    }

    private void SetSplineTangent(int index, in Variant value)
    {
        var previous = _splineTangents[index];
        _splineTangents[index] = value.Clone();
        previous.Dispose();
    }

    private void ClearKeyFrames()
    {
        for (var i = _keyFrames.Count - 1; i >= 0; --i)
        {
            var keyFrame = _keyFrames[i];
            keyFrame.Dispose();
        }
        _keyFrames.Clear();
    }

    private void ClearSplineTangents()
    {
        for (var i = _splineTangents.Count - 1; i >= 0; --i)
        {
            var tangent = _splineTangents[i];
            tangent.Dispose();
        }
        _splineTangents.Clear();
    }

    private void ClearEventFrames()
    {
        for (var i = _eventFrames.Count - 1; i >= 0; --i)
        {
            var eventFrame = _eventFrames[i];
            eventFrame.Dispose();
        }
        _eventFrames.Clear();
    }
}
