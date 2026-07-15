namespace RedBolt;

public class AttributeAnimationInfo : ValueAnimationInfo
{
    private readonly Animatable _animatable;
    private readonly AttributeInfo _attributeInfo;

    public AttributeAnimationInfo(Animatable animatable, AttributeInfo attributeInfo, ValueAnimation attributeAnimation, WrapMode wrapMode, float speed)
        : base(animatable, attributeAnimation, wrapMode, speed)
    {
        this._animatable = animatable;
        this._attributeInfo = attributeInfo;
    }

    public AttributeAnimationInfo(AttributeAnimationInfo other)
        : this(other._animatable, other._attributeInfo, other.Animation, other.GetWrapMode(), other.GetSpeed())
    {
    }

    public AttributeInfo GetAttributeInfo() => _attributeInfo;

    protected override void ApplyValue(object? newValue)
    {
        _animatable.SetAttribute(_attributeInfo.Name, newValue);
    }
}

public class Animatable : Serializable
{
    private readonly Dictionary<string, AttributeAnimationInfo> _attributeAnimationInfos = new(StringComparer.Ordinal);
    private readonly HashSet<string> _objectAnimationAttributeNames = new(StringComparer.Ordinal);
    private bool _animationEnabled = true;
    private ObjectAnimation? _objectAnimation;

    public Animatable()
    {
        RegisterAttribute(
            "Object Animation",
            typeof(ResourceRef),
            self => Resource.GetResourceRef(
                ((Animatable)self)._objectAnimation,
                ObjectReflection<ObjectAnimation>.TypeId),
            (self, value) =>
            {
                if (value is not ResourceRef resourceRef || string.IsNullOrEmpty(resourceRef.Name))
                    return;

                var cache = ((Animatable)self).GetSubsystem<ResourceCache>();
                ((Animatable)self).SetObjectAnimation(cache?.GetResourceOrNull<ObjectAnimation>(resourceRef.Name));
            },
            new ResourceRef(ObjectReflection<ObjectAnimation>.TypeId));
    }

    // TODO: port - C++ Animatable overrides LoadXML/LoadJSON/SaveXML/SaveJSON to serialize
    // animation data (object animation and attribute animations) as children of the node/component.
    // C++ Animatable::LoadXML loads <objectanimation> and <attributeanimation> elements.
    // C++ Animatable::LoadJSON loads "objectanimation" and "attributeanimation" JSON objects.
    // C++ Animatable::SaveXML writes <objectanimation> and <attributeanimation> child elements.
    // C++ Animatable::SaveJSON writes "objectanimation" and "attributeanimation" JSON values.

    public void SetAnimationEnabled(bool enable)
    {
        _animationEnabled = enable;
    }

    public bool GetAnimationEnabled() => _animationEnabled;

    public void SetAnimationTime(float time)
    {
        foreach (var info in _attributeAnimationInfos.Values)
            info.SetTime(time);
    }

    public void SetObjectAnimation(ObjectAnimation? objectAnimation)
    {
        foreach (var name in _objectAnimationAttributeNames.ToArray())
            RemoveAttributeAnimation(name);
        _objectAnimationAttributeNames.Clear();

        this._objectAnimation = objectAnimation;
        if (objectAnimation is null)
            return;

        foreach (var pair in objectAnimation.GetAttributeAnimationInfos())
        {
            SetAttributeAnimation(pair.Key, pair.Value.GetAnimation(), pair.Value.GetWrapMode(), pair.Value.GetSpeed());
            _objectAnimationAttributeNames.Add(pair.Key);
        }
    }

    public ObjectAnimation? GetObjectAnimation() => _objectAnimation;

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
        if (_attributeAnimationInfos.Remove(name))
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

    // C++: OnAttributeAnimationAdded/OnAttributeAnimationRemoved are pure virtual (= 0).
    // C# makes them virtual (non-abstract), deviating from C++.
    protected virtual void OnAttributeAnimationAdded()
    {
    }

    protected virtual void OnAttributeAnimationRemoved()
    {
    }

    // TODO: port - C++ code:
    // void HandleAttributeAnimationAdded(StringHash eventType, VariantMap& eventData);
    // void HandleAttributeAnimationRemoved(StringHash eventType, VariantMap& eventData);

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

        AttributeInfo? attributeInfo = null;
        foreach (var attribute in Attributes)
        {
            if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
            {
                attributeInfo = attribute;
                break;
            }
        }

        if (attributeInfo is null)
            return;

        attributeAnimation.SetOwner(this);
        _attributeAnimationInfos[name] = new AttributeAnimationInfo(this, attributeInfo, attributeAnimation, wrapMode, speed);
        OnAttributeAnimationAdded();
    }

    protected void UpdateAttributeAnimations(float timeStep)
    {
        if (!_animationEnabled)
            return;

        foreach (var info in _attributeAnimationInfos.Values)
            info.Update(timeStep);
    }

    internal void ApplyAttributeAnimations(float timeStep)
    {
        UpdateAttributeAnimations(timeStep);
    }

    protected AttributeAnimationInfo? GetAttributeAnimationInfo(string name)
    {
        return _attributeAnimationInfos.GetValueOrDefault(name);
    }
}
