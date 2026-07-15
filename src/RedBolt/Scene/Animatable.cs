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

    public override bool LoadXML(XMLElement source)
    {
        if (!base.LoadXML(source))
            return false;

        SetObjectAnimation(null);
        _attributeAnimationInfos.Clear();

        var objectAnimationElement = source.GetChild("objectanimation");
        if (objectAnimationElement.NotNull)
        {
            var objectAnimation = new ObjectAnimation(string.Empty);
            if (!objectAnimation.LoadXML(objectAnimationElement))
                return false;
            SetObjectAnimation(objectAnimation);
        }

        for (var element = source.GetChild("attributeanimation"); element.NotNull; element = element.GetNext("attributeanimation"))
        {
            var name = element.GetString("name");
            var animation = new ValueAnimation(name);
            if (!animation.LoadXML(element))
                return false;
            SetAttributeAnimation(name, animation, ParseWrapMode(element.GetString("wrapmode")), element.GetFloat("speed"));
        }

        return true;
    }

    public override bool SaveXML(XMLElement destination)
    {
        if (!base.SaveXML(destination))
            return false;

        if (_objectAnimation is not null)
        {
            var objectAnimationElement = destination.CreateChild("objectanimation");
            if (!_objectAnimation.SaveXML(objectAnimationElement))
                return false;
        }

        foreach (var pair in _attributeAnimationInfos)
        {
            if (pair.Value.GetAnimation().GetOwner() is not null)
                continue;

            var element = destination.CreateChild("attributeanimation");
            element.SetString("name", pair.Key);
            element.SetString("wrapmode", pair.Value.GetWrapMode().ToString());
            element.SetFloat("speed", pair.Value.GetSpeed());
            if (!pair.Value.GetAnimation().SaveXML(element))
                return false;
        }

        return true;
    }

    public override bool LoadJSON(JSONValue source)
    {
        if (!base.LoadJSON(source))
            return false;

        SetObjectAnimation(null);
        _attributeAnimationInfos.Clear();

        var objectAnimationValue = source.Get("objectanimation");
        if (!objectAnimationValue.IsNull)
        {
            var objectAnimation = new ObjectAnimation(string.Empty);
            if (!objectAnimation.LoadJSON(objectAnimationValue))
                return false;
            SetObjectAnimation(objectAnimation);
        }

        var attributeAnimations = source.Get("attributeanimation");
        if (!attributeAnimations.IsNull && attributeAnimations.IsObject)
        {
            foreach (var pair in attributeAnimations.GetObject())
            {
                var animation = new ValueAnimation(pair.Key);
                if (!animation.LoadJSON(pair.Value))
                    return false;
                SetAttributeAnimation(pair.Key, animation, ParseWrapMode(pair.Value.Get("wrapmode").GetString()), pair.Value.Get("speed").GetFloat());
            }
        }

        return true;
    }

    public override bool SaveJSON(JSONValue destination)
    {
        if (!base.SaveJSON(destination))
            return false;

        if (_objectAnimation is not null)
        {
            var objectAnimationValue = new JSONValue(JSONValueType.Object);
            if (!_objectAnimation.SaveJSON(objectAnimationValue))
                return false;
            destination.Set("objectanimation", objectAnimationValue);
        }

        var attributeAnimations = new JSONValue(JSONValueType.Object);
        foreach (var pair in _attributeAnimationInfos)
        {
            if (pair.Value.GetAnimation().GetOwner() is not null)
                continue;

            var animationValue = new JSONValue(JSONValueType.Object);
            if (!pair.Value.GetAnimation().SaveJSON(animationValue))
                return false;
            animationValue.Set("wrapmode", new JSONValue(pair.Value.GetWrapMode().ToString()));
            animationValue.Set("speed", new JSONValue(pair.Value.GetSpeed()));
            attributeAnimations.Set(pair.Key, animationValue);
        }
        destination.Set("attributeanimation", attributeAnimations);
        return true;
    }

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

    private static WrapMode ParseWrapMode(string value)
    {
        return Enum.TryParse<WrapMode>(value, ignoreCase: true, out var wrapMode) ? wrapMode : WrapMode.Loop;
    }
}
