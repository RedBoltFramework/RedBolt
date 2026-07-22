namespace RedBolt;

/// <summary>Object animation class, an object animation include one or more attribute animations and theirs wrap mode and speed for an Animatable object.</summary>
public class ObjectAnimation : Resource
{
    private readonly Dictionary<string, ValueAnimationInfo> _attributeAnimationInfos = new(StringComparer.Ordinal);

    /// <summary>Construct.</summary>
    public ObjectAnimation()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        ClearAttributeAnimationInfos();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<ObjectAnimation>();
    }

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var xml = new XMLFile();
        return xml.Load(source) && LoadXML(xml.GetRoot());
    }

    /// <summary>Save resource. Return true if successful.</summary>
    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var xml = new XMLFile();
        var root = xml.CreateRoot("objectanimation");
        return SaveXML(root) && xml.Save(destination);
    }

    /// <summary>Load from XML data. Return true if successful.</summary>
    public bool LoadXML(XMLElement source)
    {
        ClearAttributeAnimationInfos();
        for (var animationElement = source.GetChild("attributeanimation"); animationElement.NotNull; animationElement = animationElement.GetNext("attributeanimation"))
        {
            var name = animationElement.GetString("name");
            var animation = new ValueAnimation();
            if (!animation.LoadXML(animationElement))
            {
                animation.Dispose();
                return false;
            }

            AddAttributeAnimation(
                name,
                animation,
                ParseWrapMode(animationElement.GetString("wrapmode")),
                animationElement.GetFloat("speed"));
        }

        return true;
    }

    /// <summary>Save as XML data. Return true if successful.</summary>
    public bool SaveXML(XMLElement dest)
    {
        foreach (var pair in _attributeAnimationInfos)
        {
            var element = dest.CreateChild("attributeanimation");
            element.SetString("name", pair.Key);
            if (!pair.Value.Animation!.SaveXML(element))
                return false;
            element.SetString("wrapmode", pair.Value.WrapMode.ToString());
            element.SetFloat("speed", pair.Value.Speed);
        }

        return true;
    }

    /// <summary>Load from JSON data. Return true if successful.</summary>
    public bool LoadJSON(JSONValue source)
    {
        ClearAttributeAnimationInfos();
        var attributeAnimations = source.Get("attributeanimations");
        if (attributeAnimations.IsNull || !attributeAnimations.IsObject)
            return true;

        foreach (var pair in attributeAnimations.GetObject())
        {
            var animation = new ValueAnimation();
            if (!animation.LoadJSON(pair.Value))
            {
                animation.Dispose();
                return false;
            }

            AddAttributeAnimation(
                pair.Key,
                animation,
                ParseWrapMode(pair.Value.Get("wrapmode").GetString()),
                pair.Value.Get("speed").GetFloat());
        }

        return true;
    }

    /// <summary>Save as JSON data. Return true if successful.</summary>
    public bool SaveJSON(JSONValue dest)
    {
        var animationsValue = new JSONValue(JSONValueType.Object);
        foreach (var pair in _attributeAnimationInfos)
        {
            var animationValue = new JSONValue(JSONValueType.Object);
            animationValue.Set("name", new JSONValue(pair.Key));
            if (!pair.Value.Animation!.SaveJSON(animationValue))
                return false;
            animationValue.Set("wrapmode", new JSONValue(pair.Value.WrapMode.ToString()));
            animationValue.Set("speed", new JSONValue(pair.Value.Speed));
            animationsValue.Set(pair.Key, animationValue);
        }

        dest.Set("attributeanimations", animationsValue);
        return true;
    }

    /// <summary>Add attribute animation, attribute name can in following format: "attribute" or "#0/#1/attribute" or ""#0/#1/@component#1/attribute.</summary>
    public void AddAttributeAnimation(string name, ValueAnimation? attributeAnimation, WrapMode wrapMode = WrapMode.Loop, float speed = 1.0f)
    {
        if (attributeAnimation is null)
            return;

        attributeAnimation.Owner = this;
        var animationInfo = new ValueAnimationInfo(attributeAnimation, wrapMode, speed);
        if (_attributeAnimationInfos.TryGetValue(name, out var previousInfo))
        {
            _attributeAnimationInfos[name] = animationInfo;
            previousInfo.Dispose();
        }
        else
            _attributeAnimationInfos[name] = animationInfo;
        SendAttributeAnimationAddedEvent(name);
    }

    /// <summary>Remove attribute animation, attribute name can in following format: "attribute" or "#0/#1/attribute" or ""#0/#1/@component#1/attribute.</summary>
    public void RemoveAttributeAnimation(string name)
    {
        if (_attributeAnimationInfos.Remove(name, out var info))
        {
            SendAttributeAnimationRemovedEvent(name);
            info.Animation!.Owner = null;
            info.Dispose();
        }
    }

    /// <summary>Remove attribute animation.</summary>
    public void RemoveAttributeAnimation(ValueAnimation? attributeAnimation)
    {
        if (attributeAnimation is null)
            return;
        foreach (var pair in _attributeAnimationInfos)
        {
            if (!ReferenceEquals(pair.Value.Animation, attributeAnimation))
                continue;

            SendAttributeAnimationRemovedEvent(pair.Key);

            attributeAnimation.Owner = null;
            _attributeAnimationInfos.Remove(pair.Key);
            pair.Value.Dispose();
            return;
        }
    }

    /// <summary>Return attribute animation by name.</summary>
    public ValueAnimation? GetAttributeAnimation(string name)
    {
        return GetAttributeAnimationInfo(name)?.Animation;
    }

    /// <summary>Return attribute animation wrap mode by name.</summary>
    public WrapMode GetAttributeAnimationWrapMode(string name)
    {
        return GetAttributeAnimationInfo(name)?.WrapMode ?? WrapMode.Loop;
    }

    /// <summary>Return attribute animation speed by name.</summary>
    public float GetAttributeAnimationSpeed(string name)
    {
        return GetAttributeAnimationInfo(name)?.Speed ?? 1.0f;
    }

    /// <summary>Return all attribute animations infos.</summary>
    public IReadOnlyDictionary<string, ValueAnimationInfo> AttributeAnimationInfos => _attributeAnimationInfos;

    /// <summary>Return attribute animation info by name.</summary>
    public ValueAnimationInfo? GetAttributeAnimationInfo(string name)
    {
        return _attributeAnimationInfos.GetValueOrDefault(name);
    }

    /// <summary>Send attribute animation added event.</summary>
    private void SendAttributeAnimationAddedEvent(string name)
    {
        var eventData = new SceneEvents.AttributeAnimationAddedEvent
        {
            ObjectAnimation = this,
            AttributeAnimationName = name
        };
        SendEvent(ref eventData);
    }

    /// <summary>Send attribute animation remove event.</summary>
    private void SendAttributeAnimationRemovedEvent(string name)
    {
        var eventData = new SceneEvents.AttributeAnimationRemovedEvent
        {
            ObjectAnimation = this,
            AttributeAnimationName = name
        };
        SendEvent(ref eventData);
    }

    private static WrapMode ParseWrapMode(string value)
    {
        return Enum.TryParse<WrapMode>(value, ignoreCase: true, out var wrapMode) ? wrapMode : WrapMode.Loop;
    }

    private void ClearAttributeAnimationInfos()
    {
        foreach (var info in _attributeAnimationInfos.Values)
            info.Dispose();
        _attributeAnimationInfos.Clear();
    }
}
