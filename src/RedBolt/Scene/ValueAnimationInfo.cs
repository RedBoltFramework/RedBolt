namespace RedBolt;

public class ValueAnimationInfo
{
    private readonly WeakReference<object?>? target;
    private float lastScaledTime;

    public ValueAnimationInfo(ValueAnimation animation, WrapMode wrapMode, float speed)
        : this(target: null, animation, wrapMode, speed)
    {
    }

    public ValueAnimationInfo(object? target, ValueAnimation animation, WrapMode wrapMode, float speed)
    {
        this.target = target is null ? null : new WeakReference<object?>(target);
        Animation = animation ?? throw new ArgumentNullException(nameof(animation));
        WrapMode = wrapMode;
        Speed = Math.Max(speed, 0.0f);
    }

    public event Action<VAnimEventFrame>? EventTriggered;

    public ValueAnimation Animation { get; }

    public WrapMode WrapMode { get; private set; }

    public float Speed { get; private set; }

    public float Time { get; private set; }

    public object? GetTarget()
    {
        return target is not null && target.TryGetTarget(out var value) ? value : null;
    }

    public ValueAnimation GetAnimation() => Animation;

    public void SetWrapMode(WrapMode wrapMode)
    {
        WrapMode = wrapMode;
    }

    public void SetSpeed(float speed)
    {
        Speed = Math.Max(speed, 0.0f);
    }

    public bool Update(float timeStep)
    {
        return SetTime(Time + timeStep * Speed);
    }

    public bool SetTime(float time)
    {
        Time = time;
        if (!Animation.IsValid())
            return true;

        var finished = false;
        var scaledTime = CalculateScaledTime(Time, ref finished);
        ApplyValue(Animation.GetAnimationValue(scaledTime));

        if (Animation.HasEventFrames())
        {
            foreach (var frame in GetEventFrames(lastScaledTime, scaledTime))
                EventTriggered?.Invoke(frame);
        }

        lastScaledTime = scaledTime;
        return finished;
    }

    protected virtual void ApplyValue(object? newValue)
    {
    }

    protected float CalculateScaledTime(float currentTime, ref bool finished)
    {
        var beginTime = Animation.BeginTime;
        var endTime = Animation.EndTime;
        return WrapMode switch
        {
            WrapMode.Loop => beginTime + MathDefs.AbsMod(currentTime - beginTime, endTime - beginTime),
            WrapMode.Once => ClampAndSetFinished(currentTime, beginTime, endTime, ref finished),
            WrapMode.Clamp => MathDefs.Clamp(currentTime, beginTime, endTime),
            _ => beginTime
        };
    }

    protected IReadOnlyList<VAnimEventFrame> GetEventFrames(float beginTime, float endTime)
    {
        if (WrapMode == WrapMode.Loop && beginTime > endTime)
        {
            var result = new List<VAnimEventFrame>();
            result.AddRange(Animation.GetEventFrames(beginTime, Animation.EndTime));
            result.AddRange(Animation.GetEventFrames(Animation.BeginTime, endTime));
            return result;
        }

        return Animation.GetEventFrames(beginTime, endTime);
    }

    private static float ClampAndSetFinished(float currentTime, float beginTime, float endTime, ref bool finished)
    {
        finished = currentTime >= endTime;
        return MathDefs.Clamp(currentTime, beginTime, endTime);
    }
}
