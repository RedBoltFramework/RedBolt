using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Base class for a value animation instance, which includes animation runtime information and updates the target object's value automatically.</summary>
public class ValueAnimationInfo : ICloneable, IDisposable
{
    /// <summary>Target object.</summary>
    protected WeakReference<RedBolt.Object>? _target;
    /// <summary>Attribute animation.</summary>
    protected ValueAnimation? _animation;
    /// <summary>Wrap mode.</summary>
    protected WrapMode _wrapMode;
    /// <summary>Animation speed.</summary>
    protected float _speed;
    /// <summary>Current time.</summary>
    protected float _currentTime;
    /// <summary>Last scaled time.</summary>
    protected float _lastScaledTime;

    /// <summary>Construct without target object.</summary>
    public ValueAnimationInfo(ValueAnimation? animation, WrapMode wrapMode, float speed)
    {
        _animation = animation;
        _wrapMode = wrapMode;
        _speed = speed;
        _currentTime = 0.0f;
        _lastScaledTime = 0.0f;
        _speed = Math.Max(0.0f, _speed);
    }

    /// <summary>Construct with target object.</summary>
    public ValueAnimationInfo(RedBolt.Object? target, ValueAnimation? animation, WrapMode wrapMode, float speed)
    {
        _target = target is null ? null : new WeakReference<RedBolt.Object>(target);
        _animation = animation;
        _wrapMode = wrapMode;
        _speed = speed;
        _currentTime = 0.0f;
        _lastScaledTime = 0.0f;
        _speed = Math.Max(0.0f, _speed);
    }

    /// <summary>Copy construct.</summary>
    public ValueAnimationInfo(ValueAnimationInfo other)
    {
        var target = other.Target;
        _target = target is null ? null : new WeakReference<RedBolt.Object>(target);
        _animation = other._animation;
        _wrapMode = other._wrapMode;
        _speed = other._speed;
        _currentTime = 0.0f;
        _lastScaledTime = 0.0f;
    }

    public event Action<VAnimEventFrame>? EventTriggered;

    public virtual object Clone() => new ValueAnimationInfo(this);

    /// <summary>Destruct.</summary>
    public virtual void Dispose()
    {
        EventTriggered = null;
        _animation = null;
        _target = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>Advance time position and apply. Return true when the animation is finished. No-op when the target object is not defined.</summary>
    public bool Update(float timeStep)
    {
        if (_animation is null || Target is null)
            return true;

        return SetTime(_currentTime + timeStep * _speed);
    }

    /// <summary>Set time position and apply. Return true when the animation is finished. No-op when the target object is not defined.</summary>
    public bool SetTime(float time)
    {
        var animation = _animation;
        var target = Target;
        if (animation is null || target is null)
            return true;

        _currentTime = time;

        if (!animation.IsValid())
            return true;

        var finished = false;
        var scaledTime = CalculateScaledTime(_currentTime, ref finished);

        using (var newValue = animation.GetAnimationValue(scaledTime))
            ApplyValue(newValue);

        if (animation.HasEventFrames)
        {
            var eventFrames = new List<VAnimEventFrame>();
            GetEventFrames(_lastScaledTime, scaledTime, eventFrames);

            if (eventFrames.Count != 0)
            {
                var targetWeak = new WeakReference<RedBolt.Object>(target);

                for (var i = 0; i < eventFrames.Count; ++i)
                    EventTriggered?.Invoke(eventFrames[i]);

                if (!targetWeak.TryGetTarget(out _))
                    return true;
            }
        }

        _lastScaledTime = scaledTime;

        return finished;
    }

    /// <summary>Set wrap mode.</summary>
    public WrapMode WrapMode
    {
        get => _wrapMode;
        set => _wrapMode = value;
    }

    /// <summary>Set speed.</summary>
    public float Speed
    {
        get => _speed;
        set => _speed = value;
    }

    /// <summary>Return target object.</summary>
    public RedBolt.Object? Target => _target is not null && _target.TryGetTarget(out var target) ? target : null;

    /// <summary>Return animation.</summary>
    public ValueAnimation? Animation => _animation;

    /// <summary>Return time position.</summary>
    public float Time => _currentTime;

    /// <summary>Apply new animation value to the target object. Called by Update().</summary>
    protected virtual void ApplyValue(in Variant newValue)
    {
    }

    /// <summary>Calculate scaled time.</summary>
    protected float CalculateScaledTime(float currentTime, ref bool finished)
    {
        var animation = _animation!;
        var beginTime = animation.BeginTime;
        var endTime = animation.EndTime;

        switch (_wrapMode)
        {
        case WrapMode.Loop:
        {
            var span = endTime - beginTime;
            var time = (currentTime - beginTime) % span;
            if (time < 0.0f)
                time += span;
            return beginTime + time;
        }
        case WrapMode.Once:
            finished = currentTime >= endTime;
            goto case WrapMode.Clamp;
        case WrapMode.Clamp:
            return MathDefs.Clamp(currentTime, beginTime, endTime);
        default:
            Log.GetLogger().LogError("Unsupported attribute animation wrap mode");
            return beginTime;
        }
    }

    /// <summary>Return event frames.</summary>
    protected void GetEventFrames(float beginTime, float endTime, List<VAnimEventFrame> eventFrames)
    {
        var animation = _animation!;
        switch (_wrapMode)
        {
        case WrapMode.Loop:
            // TODO: This can miss an event if the deltatime is exactly the animation's length
            if (beginTime <= endTime)
                animation.GetEventFrames(beginTime, endTime, eventFrames);
            else
            {
                animation.GetEventFrames(beginTime, animation.EndTime, eventFrames);
                animation.GetEventFrames(animation.BeginTime, endTime, eventFrames);
            }
            break;
        case WrapMode.Once:
        case WrapMode.Clamp:
            animation.GetEventFrames(beginTime, endTime, eventFrames);
            break;
        }
    }
}
