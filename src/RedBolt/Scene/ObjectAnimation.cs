namespace RedBolt;

public sealed class ObjectAnimation : Resource
{
    private readonly Dictionary<string, ValueAnimationInfo> _attributeAnimationInfos = new(StringComparer.Ordinal);

    public ObjectAnimation(string name)
        : base(name)
    {
    }

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
        var root = xml.CreateRoot("objectanimation");
        return SaveXML(root) && xml.Save(destination);
    }

    public bool LoadXML(XMLElement source)
    {
        _attributeAnimationInfos.Clear();
        for (var animationElement = source.GetChild("attributeanimation"); animationElement.NotNull; animationElement = animationElement.GetNext("attributeanimation"))
        {
            var name = animationElement.GetString("name");
            var animation = new ValueAnimation(Name + ":" + name);
            if (!animation.LoadXML(animationElement))
                return false;

            AddAttributeAnimation(
                name,
                animation,
                ParseWrapMode(animationElement.GetString("wrapmode")),
                animationElement.GetFloat("speed"));
        }

        return true;
    }

    public bool SaveXML(XMLElement dest)
    {
        foreach (var pair in _attributeAnimationInfos)
        {
            var element = dest.CreateChild("attributeanimation");
            element.SetString("name", pair.Key);
            element.SetString("wrapmode", pair.Value.GetWrapMode().ToString());
            element.SetFloat("speed", pair.Value.GetSpeed());
            if (!pair.Value.GetAnimation().SaveXML(element))
                return false;
        }

        return true;
    }

    public bool LoadJSON(JSONValue source)
    {
        _attributeAnimationInfos.Clear();
        var attributeAnimations = source.Get("attributeanimations");
        if (attributeAnimations.IsNull || !attributeAnimations.IsObject)
            return true;

        foreach (var pair in attributeAnimations.GetObject())
        {
            var animation = new ValueAnimation(Name + ":" + pair.Key);
            if (!animation.LoadJSON(pair.Value))
                return false;

            AddAttributeAnimation(
                pair.Key,
                animation,
                ParseWrapMode(pair.Value.Get("wrapmode").GetString()),
                pair.Value.Get("speed").GetFloat());
        }

        return true;
    }

    public bool SaveJSON(JSONValue dest)
    {
        var animationsValue = new JSONValue(JSONValueType.Object);
        foreach (var pair in _attributeAnimationInfos)
        {
            var animationValue = new JSONValue(JSONValueType.Object);
            if (!pair.Value.GetAnimation().SaveJSON(animationValue))
                return false;
            animationValue.Set("wrapmode", new JSONValue(pair.Value.GetWrapMode().ToString()));
            animationValue.Set("speed", new JSONValue(pair.Value.GetSpeed()));
            animationsValue.Set(pair.Key, animationValue);
        }

        dest.Set("attributeanimations", animationsValue);
        return true;
    }

    public void AddAttributeAnimation(string name, ValueAnimation attributeAnimation, WrapMode wrapMode = WrapMode.Loop, float speed = 1.0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(attributeAnimation);

        attributeAnimation.SetOwner(this);
        _attributeAnimationInfos[name] = new ValueAnimationInfo(attributeAnimation, wrapMode, speed);
        var eventData = new SceneEvents.AttributeAnimationAddedEvent
        {
            ObjectAnimation = this,
            AttributeAnimationName = name
        };
        SendEvent(ref eventData);
    }

    public void RemoveAttributeAnimation(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_attributeAnimationInfos.Remove(name, out var info))
        {
            var eventData = new SceneEvents.AttributeAnimationRemovedEvent
            {
                ObjectAnimation = this,
                AttributeAnimationName = name
            };
            SendEvent(ref eventData);
            info.GetAnimation().SetOwner(null);
        }
    }

    public void RemoveAttributeAnimation(ValueAnimation attributeAnimation)
    {
        ArgumentNullException.ThrowIfNull(attributeAnimation);
        foreach (var pair in _attributeAnimationInfos.ToArray())
        {
            if (!ReferenceEquals(pair.Value.GetAnimation(), attributeAnimation))
                continue;

            RemoveAttributeAnimation(pair.Key);
            return;
        }
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
        return GetAttributeAnimationInfo(name)?.GetSpeed() ?? 1.0f;
    }

    public IReadOnlyDictionary<string, ValueAnimationInfo> GetAttributeAnimationInfos() => _attributeAnimationInfos;

    public ValueAnimationInfo? GetAttributeAnimationInfo(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _attributeAnimationInfos.GetValueOrDefault(name);
    }

    private static WrapMode ParseWrapMode(string value)
    {
        return Enum.TryParse<WrapMode>(value, ignoreCase: true, out var wrapMode) ? wrapMode : WrapMode.Loop;
    }
}
