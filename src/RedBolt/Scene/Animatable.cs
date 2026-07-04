namespace RedBolt;

public class AttributeAnimationInfo : ValueAnimationInfo
{
    private readonly Animatable animatable;
    private readonly AttributeInfo attributeInfo;

    public AttributeAnimationInfo(Animatable animatable, AttributeInfo attributeInfo, ValueAnimation attributeAnimation, WrapMode wrapMode, float speed)
        : base(animatable, attributeAnimation, wrapMode, speed)
    {
        this.animatable = animatable;
        this.attributeInfo = attributeInfo;
    }

    public AttributeAnimationInfo(AttributeAnimationInfo other)
        : this(other.animatable, other.attributeInfo, other.Animation, other.GetWrapMode(), other.GetSpeed())
    {
    }

    public AttributeInfo GetAttributeInfo() => attributeInfo;

    protected override void ApplyValue(object? newValue)
    {
        animatable.SetAttribute(attributeInfo.Name, newValue);
    }
}

public class Animatable : Serializable
{
    private readonly Dictionary<string, AttributeAnimationInfo> attributeAnimationInfos = new(StringComparer.Ordinal);
    private readonly HashSet<string> objectAnimationAttributeNames = new(StringComparer.Ordinal);
    private bool animationEnabled = true;
    private ObjectAnimation? objectAnimation;

    public void SetAnimationEnabled(bool enable)
    {
        animationEnabled = enable;
    }

    public bool GetAnimationEnabled() => animationEnabled;

    public void SetAnimationTime(float time)
    {
        foreach (var info in attributeAnimationInfos.Values)
            info.SetTime(time);
    }

    public void SetObjectAnimation(ObjectAnimation? objectAnimation)
    {
        foreach (var name in objectAnimationAttributeNames.ToArray())
            RemoveAttributeAnimation(name);
        objectAnimationAttributeNames.Clear();

        this.objectAnimation = objectAnimation;
        if (objectAnimation is null)
            return;

        foreach (var pair in objectAnimation.GetAttributeAnimationInfos())
        {
            SetAttributeAnimation(pair.Key, pair.Value.GetAnimation(), pair.Value.GetWrapMode(), pair.Value.GetSpeed());
            objectAnimationAttributeNames.Add(pair.Key);
        }
    }

    public ObjectAnimation? GetObjectAnimation() => objectAnimation;

    public void RemoveObjectAnimation()
    {
        SetObjectAnimation(null);
    }

    public void SetAttributeAnimation(string name, ValueAnimation? attributeAnimation, WrapMode wrapMode = WrapMode.Loop, float speed = 1.0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var target = FindAttributeAnimationTarget(name, out var resolvedName);
        if (target is null)
            return;

        target.SetObjectAttributeAnimation(resolvedName, attributeAnimation, wrapMode, speed);
    }

    public void SetAttributeAnimationWrapMode(string name, WrapMode wrapMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (GetAttributeAnimationInfo(name) is { } info)
            info.SetWrapMode(wrapMode);
    }

    public void SetAttributeAnimationSpeed(string name, float speed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (GetAttributeAnimationInfo(name) is { } info)
            info.SetSpeed(speed);
    }

    public void SetAttributeAnimationTime(string name, float time)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (GetAttributeAnimationInfo(name) is { } info)
            info.SetTime(time);
    }

    public void RemoveAttributeAnimation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (attributeAnimationInfos.Remove(name))
            OnAttributeAnimationRemoved();
    }

    public ValueAnimation? GetAttributeAnimation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetAttributeAnimationInfo(name)?.GetAnimation();
    }

    public WrapMode GetAttributeAnimationWrapMode(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetAttributeAnimationInfo(name)?.GetWrapMode() ?? WrapMode.Loop;
    }

    public float GetAttributeAnimationSpeed(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetAttributeAnimationInfo(name)?.GetSpeed() ?? 0.0f;
    }

    public float GetAttributeAnimationTime(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return GetAttributeAnimationInfo(name)?.GetTime() ?? 0.0f;
    }

    protected virtual void OnAttributeAnimationAdded()
    {
    }

    protected virtual void OnAttributeAnimationRemoved()
    {
    }

    protected virtual Animatable? FindAttributeAnimationTarget(string name, out string outName)
    {
        outName = name;
        return this;
    }

    protected void SetObjectAttributeAnimation(string name, ValueAnimation? attributeAnimation, WrapMode wrapMode, float speed)
    {
        if (attributeAnimation is null)
        {
            RemoveAttributeAnimation(name);
            return;
        }

        var attributeInfo = Attributes.FirstOrDefault(attribute => string.Equals(attribute.Name, name, StringComparison.Ordinal));
        if (attributeInfo is null)
            return;

        attributeAnimation.SetOwner(this);
        attributeAnimationInfos[name] = new AttributeAnimationInfo(this, attributeInfo, attributeAnimation, wrapMode, speed);
        OnAttributeAnimationAdded();
    }

    protected void UpdateAttributeAnimations(float timeStep)
    {
        if (!animationEnabled)
            return;

        foreach (var info in attributeAnimationInfos.Values)
            info.Update(timeStep);
    }

    protected AttributeAnimationInfo? GetAttributeAnimationInfo(string name)
    {
        return attributeAnimationInfos.GetValueOrDefault(name);
    }
}
