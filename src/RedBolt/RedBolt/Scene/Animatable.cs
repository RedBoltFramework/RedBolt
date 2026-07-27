using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

namespace RedBolt.Scene;

/// <summary>Attribute animation instance.</summary>
public class AttributeAnimationInfo : ValueAnimationInfo
{
    /// <summary>Construct.</summary>
    public AttributeAnimationInfo(Animatable animatable, AttributeInfo attributeInfo,
        ValueAnimation attributeAnimation, WrapMode wrapMode, float speed)
        : base(animatable, attributeAnimation, wrapMode, speed)
    {
        AttributeInfo = attributeInfo;
    }

    /// <summary>Copy construct.</summary>
    public AttributeAnimationInfo(AttributeAnimationInfo other)
        : base(other)
    {
        AttributeInfo = other.AttributeInfo;
    }

    /// <summary>Return attribute information.</summary>
    public AttributeInfo AttributeInfo { get; }

    /// <inheritdoc />
    public override object Clone() => new AttributeAnimationInfo(this);

    /// <summary>Apply new animation value to the target object.</summary>
    protected override void ApplyValue(in Variant newValue)
    {
        if (Target is Animatable animatable)
        {
        animatable.OnSetAttribute(AttributeInfo, newValue);
            animatable.ApplyAttributes();
        }
    }
}

/// <summary>Base class for animatable object, an animatable object can be set animation on it's attributes, or can be set an object animation to it.</summary>
public abstract class Animatable : Serializable
{
    /// <summary>Animation enabled.</summary>
    private bool _animationEnabled = true;
    /// <summary>Animation.</summary>
    private ObjectAnimation? _objectAnimation;
    /// <summary>Attribute animation infos.</summary>
    private readonly Dictionary<string, AttributeAnimationInfo> _attributeAnimationInfos = new(StringComparer.Ordinal);

    /// <summary>Construct.</summary>
    protected Animatable()
    {
        RegisterAttribute(
            "Object Animation",
            typeof(ResourceRef),
            self => ((Animatable)self).GetObjectAnimationAttr(),
            (self, value) =>
            {
                if (value is ResourceRef resourceRef)
                    ((Animatable)self).SetObjectAnimationAttr(resourceRef);
            },
            new ResourceRef(ObjectReflection<ObjectAnimation>.TypeId));
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        ClearAttributeAnimationInfos();
        _objectAnimation = null;
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddAbstractReflection<Animatable>();
    }

    /// <summary>Load from XML data. Return true if successful.</summary>
    public override bool LoadXML(XMLElement source)
    {
        if (!base.LoadXML(source))
            return false;

        SetObjectAnimation(null);
        ClearAttributeAnimationInfos();

        var objectAnimationElement = source.GetChild("objectanimation");
        if (objectAnimationElement.NotNull)
        {
            var objectAnimation = new ObjectAnimation();
            if (!objectAnimation.LoadXML(objectAnimationElement))
            {
                objectAnimation.Dispose();
                return false;
            }
            SetObjectAnimation(objectAnimation);
        }

        for (var element = source.GetChild("attributeanimation"); element.NotNull;
             element = element.GetNext("attributeanimation"))
        {
            var name = element.GetAttribute("name");
            var attributeAnimation = new ValueAnimation();
            if (!attributeAnimation.LoadXML(element))
            {
                attributeAnimation.Dispose();
                return false;
            }

            var wrapMode = WrapMode.Loop;
            var wrapModeString = source.GetAttribute("wrapmode");
            for (var i = 0; i <= (int)WrapMode.Clamp; ++i)
            {
                if (string.Equals(wrapModeString, ((WrapMode)i).ToString(), StringComparison.Ordinal))
                {
                    wrapMode = (WrapMode)i;
                    break;
                }
            }

            var speed = element.GetFloat("speed");
            SetAttributeAnimation(name, attributeAnimation, wrapMode, speed);
        }

        return true;
    }

    /// <summary>Save as XML data. Return true if successful.</summary>
    public override bool SaveXML(XMLElement destination)
    {
        if (!base.SaveXML(destination))
            return false;

        if (_objectAnimation is not null && string.IsNullOrEmpty(_objectAnimation.Name))
        {
            var objectAnimationElement = destination.CreateChild("objectanimation");
            if (!_objectAnimation.SaveXML(objectAnimationElement))
                return false;
        }

        foreach (var info in _attributeAnimationInfos.Values)
        {
            if (info.Animation!.Owner is not null)
                continue;

            var attributeInfo = info.AttributeInfo;
            var element = destination.CreateChild("attributeanimation");
            element.SetString("name", attributeInfo.Name);
            if (!info.Animation!.SaveXML(element))
                return false;
            element.SetString("wrapmode", info.WrapMode.ToString());
            element.SetFloat("speed", info.Speed);
        }

        return true;
    }

    /// <summary>Load from JSON data. Return true if successful.</summary>
    public override bool LoadJSON(JSONValue source)
    {
        if (!base.LoadJSON(source))
            return false;

        SetObjectAnimation(null);
        ClearAttributeAnimationInfos();

        var objectAnimationValue = source.Get("objectanimation");
        if (!objectAnimationValue.IsNull)
        {
            var objectAnimation = new ObjectAnimation();
            if (!objectAnimation.LoadJSON(objectAnimationValue))
            {
                objectAnimation.Dispose();
                return false;
            }
            SetObjectAnimation(objectAnimation);
        }

        var attributeAnimationsValue = source.Get("attributeanimation");
        if (attributeAnimationsValue.IsNull)
            return true;

        if (!attributeAnimationsValue.IsObject)
        {
            Log.GetLogger().Warning("JSON attribute animation value is not object");
            return true;
        }

        foreach (var pair in attributeAnimationsValue.GetObject())
        {
            var attributeAnimation = new ValueAnimation();
            if (!attributeAnimation.LoadJSON(pair.Value))
            {
                attributeAnimation.Dispose();
                return false;
            }

            var wrapMode = WrapMode.Loop;
            var wrapModeString = pair.Value.Get("wrapmode").GetString();
            for (var i = 0; i <= (int)WrapMode.Clamp; ++i)
            {
                if (string.Equals(wrapModeString, ((WrapMode)i).ToString(), StringComparison.Ordinal))
                {
                    wrapMode = (WrapMode)i;
                    break;
                }
            }

            var speed = pair.Value.Get("speed").GetFloat();
            SetAttributeAnimation(pair.Key, attributeAnimation, wrapMode, speed);
        }

        return true;
    }

    /// <summary>Save as JSON data. Return true if successful.</summary>
    public override bool SaveJSON(JSONValue destination)
    {
        if (!base.SaveJSON(destination))
            return false;

        if (_objectAnimation is not null && string.IsNullOrEmpty(_objectAnimation.Name))
        {
            var objectAnimationValue = new JSONValue(JSONValueType.Object);
            if (!_objectAnimation.SaveJSON(objectAnimationValue))
                return false;
            destination.Set("objectanimation", objectAnimationValue);
        }

        var attributeAnimationsValue = new JSONValue(JSONValueType.Object);
        foreach (var info in _attributeAnimationInfos.Values)
        {
            if (info.Animation!.Owner is not null)
                continue;

            var attributeInfo = info.AttributeInfo;
            var animationValue = new JSONValue(JSONValueType.Object);
            animationValue.Set("name", new JSONValue(attributeInfo.Name));
            if (!info.Animation!.SaveJSON(animationValue))
                return false;
            animationValue.Set("wrapmode", new JSONValue(info.WrapMode.ToString()));
            animationValue.Set("speed", new JSONValue(info.Speed));
            attributeAnimationsValue.Set(attributeInfo.Name, animationValue);
        }

        if (!attributeAnimationsValue.IsNull)
            destination.Set("attributeanimation", attributeAnimationsValue);

        return true;
    }

    /// <summary>Set automatic update of animation, default true.</summary>
    public void SetAnimationEnabled(bool enable)
    {
        if (_objectAnimation is not null)
        {
            // In object animation there may be targets in hierarchy. Set same enable/disable state in all
            var targets = new HashSet<Animatable>();
            foreach (var pair in _objectAnimation.AttributeAnimationInfos)
            {
                var target = FindAttributeAnimationTarget(pair.Key, out _);
                if (target is not null && !ReferenceEquals(target, this))
                    targets.Add(target);
            }

            foreach (var target in targets)
                target._animationEnabled = enable;
        }

        _animationEnabled = enable;
    }

    /// <summary>Set time position of all attribute animations or an object animation manually. Automatic update should be disabled in this case.</summary>
    public void SetAnimationTime(float time)
    {
        if (_objectAnimation is not null)
        {
            // In object animation there may be targets in hierarchy. Set same time in all
            foreach (var pair in _objectAnimation.AttributeAnimationInfos)
            {
                var target = FindAttributeAnimationTarget(pair.Key, out var outName);
                target?.SetAttributeAnimationTime(outName, time);
            }
        }
        else
        {
            foreach (var info in _attributeAnimationInfos.Values)
                info.SetTime(time);
        }
    }

    /// <summary>Set object animation.</summary>
    public void SetObjectAnimation(ObjectAnimation? objectAnimation)
    {
        if (ReferenceEquals(objectAnimation, _objectAnimation))
            return;

        if (_objectAnimation is not null)
        {
            OnObjectAnimationRemoved(_objectAnimation);
            UnsubscribeFromEvent<SceneEvents.AttributeAnimationAddedEvent>(_objectAnimation);
            UnsubscribeFromEvent<SceneEvents.AttributeAnimationRemovedEvent>(_objectAnimation);
        }

        _objectAnimation = objectAnimation;

        if (_objectAnimation is not null)
        {
            OnObjectAnimationAdded(_objectAnimation);
            SubscribeToEvent<SceneEvents.AttributeAnimationAddedEvent>(_objectAnimation, HandleAttributeAnimationAdded);
            SubscribeToEvent<SceneEvents.AttributeAnimationRemovedEvent>(_objectAnimation, HandleAttributeAnimationRemoved);
        }
    }

    /// <summary>Set attribute animation.</summary>
    public void SetAttributeAnimation(string name, ValueAnimation? attributeAnimation,
        WrapMode wrapMode = WrapMode.Loop, float speed = 1.0f)
    {
        var info = GetAttributeAnimationInfo(name);

        if (attributeAnimation is not null)
        {
            if (info is not null && ReferenceEquals(attributeAnimation, info.Animation))
            {
                info.WrapMode = wrapMode;
                info.Speed = speed;
                return;
            }

            AttributeInfo? attributeInfo = null;
            if (info is not null)
                attributeInfo = info.AttributeInfo;
            else
            {
                foreach (var attribute in GetAttributes())
                {
                    if (string.Equals(name, attribute.Name, StringComparison.Ordinal))
                    {
                        attributeInfo = attribute;
                        break;
                    }
                }
            }

            if (attributeInfo is null)
            {
                Log.GetLogger().Error("Invalid name: {Name}", name);
                return;
            }

            if (attributeAnimation.ValueType != attributeInfo.DefaultValue.Type)
            {
                Log.GetLogger().Error("Invalid value type");
                return;
            }

            var animationInfo = new AttributeAnimationInfo(this, attributeInfo, attributeAnimation, wrapMode, speed);
            _attributeAnimationInfos[name] = animationInfo;
            info?.Dispose();

            if (info is null)
                OnAttributeAnimationAdded();
        }
        else
        {
            if (info is null)
                return;

            _attributeAnimationInfos.Remove(name);
            info.Dispose();
            OnAttributeAnimationRemoved();
        }
    }

    /// <summary>Set attribute animation wrap mode.</summary>
    public void SetAttributeAnimationWrapMode(string name, WrapMode wrapMode)
    {
        var info = GetAttributeAnimationInfo(name);
        if (info is not null)
            info.WrapMode = wrapMode;
    }

    /// <summary>Set attribute animation speed.</summary>
    public void SetAttributeAnimationSpeed(string name, float speed)
    {
        var info = GetAttributeAnimationInfo(name);
        if (info is not null)
            info.Speed = speed;
    }

    /// <summary>Set attribute animation time position manually. Automatic update should be disabled in this case.</summary>
    public void SetAttributeAnimationTime(string name, float time)
    {
        var info = GetAttributeAnimationInfo(name);
        info?.SetTime(time);
    }

    /// <summary>Remove object animation. Same as calling SetObjectAnimation with a null pointer.</summary>
    public void RemoveObjectAnimation()
    {
        SetObjectAnimation(null);
    }

    /// <summary>Remove attribute animation. Same as calling SetAttributeAnimation with a null pointer.</summary>
    public void RemoveAttributeAnimation(string name)
    {
        SetAttributeAnimation(name, null);
    }

    /// <summary>Return animation enabled.</summary>
    public bool GetAnimationEnabled() => _animationEnabled;

    /// <summary>Return object animation.</summary>
    public ObjectAnimation? GetObjectAnimation()
    {
        return _objectAnimation;
    }

    /// <summary>Return attribute animation.</summary>
    public ValueAnimation? GetAttributeAnimation(string name)
    {
        var info = GetAttributeAnimationInfo(name);
        return info?.Animation;
    }

    /// <summary>Return attribute animation wrap mode.</summary>
    public WrapMode GetAttributeAnimationWrapMode(string name)
    {
        var info = GetAttributeAnimationInfo(name);
        return info?.WrapMode ?? WrapMode.Loop;
    }

    /// <summary>Return attribute animation speed.</summary>
    public float GetAttributeAnimationSpeed(string name)
    {
        var info = GetAttributeAnimationInfo(name);
        return info?.Speed ?? 1.0f;
    }

    /// <summary>Return attribute animation time position.</summary>
    public float GetAttributeAnimationTime(string name)
    {
        var info = GetAttributeAnimationInfo(name);
        return info?.Time ?? 0.0f;
    }

    /// <summary>Set object animation attribute.</summary>
    public void SetObjectAnimationAttr(ResourceRef value)
    {
        if (!string.IsNullOrEmpty(value.Name))
        {
            var cache = GetSubsystem<ResourceCache>()!;
            SetObjectAnimation(cache.GetResourceOrNull<ObjectAnimation>(value.Name));
        }
    }

    /// <summary>Return object animation attribute.</summary>
    public ResourceRef GetObjectAnimationAttr()
    {
        return ResourceType.GetResourceRef(_objectAnimation, ObjectReflection<ObjectAnimation>.TypeId);
    }

    /// <summary>Handle attribute animation added.</summary>
    protected abstract void OnAttributeAnimationAdded();

    /// <summary>Handle attribute animation removed.</summary>
    protected abstract void OnAttributeAnimationRemoved();

    /// <summary>Find target of an attribute animation from object hierarchy by name.</summary>
    protected virtual Animatable? FindAttributeAnimationTarget(string name, out string outName)
    {
        // Base implementation only handles self
        outName = name;
        return this;
    }

    /// <summary>Set object attribute animation internal.</summary>
    protected void SetObjectAttributeAnimation(string name, ValueAnimation? attributeAnimation,
        WrapMode wrapMode, float speed)
    {
        var target = FindAttributeAnimationTarget(name, out var outName);
        target?.SetAttributeAnimation(outName, attributeAnimation, wrapMode, speed);
    }

    /// <summary>Handle object animation added.</summary>
    protected void OnObjectAnimationAdded(ObjectAnimation? objectAnimation)
    {
        if (objectAnimation is null)
            return;

        // Set all attribute animations from the object animation
        foreach (var pair in objectAnimation.AttributeAnimationInfos)
        {
            var info = pair.Value;
            SetObjectAttributeAnimation(pair.Key, info.Animation, info.WrapMode, info.Speed);
        }
    }

    /// <summary>Handle object animation removed.</summary>
    protected void OnObjectAnimationRemoved(ObjectAnimation? objectAnimation)
    {
        if (objectAnimation is null)
            return;

        // Just remove all attribute animations listed by the object animation
        foreach (var pair in objectAnimation.AttributeAnimationInfos)
            SetObjectAttributeAnimation(pair.Key, null, WrapMode.Loop, 1.0f);
    }

    /// <summary>Update attribute animations.</summary>
    protected void UpdateAttributeAnimations(float timeStep)
    {
        if (!_animationEnabled)
            return;

        var finishedNames = new List<string>();
        foreach (var info in _attributeAnimationInfos.Values)
        {
            var finished = info.Update(timeStep);
            if (finished)
                finishedNames.Add(info.AttributeInfo.Name);
        }

        for (var i = 0; i < finishedNames.Count; ++i)
            SetAttributeAnimation(finishedNames[i], null);
    }

    /// <summary>Return attribute animation info.</summary>
    protected AttributeAnimationInfo? GetAttributeAnimationInfo(string name)
    {
        return _attributeAnimationInfos.GetValueOrDefault(name);
    }

    /// <summary>Handle attribute animation added.</summary>
    private void HandleAttributeAnimationAdded(ref SceneEvents.AttributeAnimationAddedEvent eventData)
    {
        if (_objectAnimation is null)
            return;

        var info = _objectAnimation.GetAttributeAnimationInfo(eventData.AttributeAnimationName);
        if (info is null)
            return;

        SetObjectAttributeAnimation(eventData.AttributeAnimationName, info.Animation, info.WrapMode, info.Speed);
    }

    /// <summary>Handle attribute animation removed.</summary>
    private void HandleAttributeAnimationRemoved(ref SceneEvents.AttributeAnimationRemovedEvent eventData)
    {
        if (_objectAnimation is null)
            return;

        SetObjectAttributeAnimation(eventData.AttributeAnimationName, null, WrapMode.Loop, 1.0f);
    }

    private void ClearAttributeAnimationInfos()
    {
        foreach (var info in _attributeAnimationInfos.Values)
            info.Dispose();
        _attributeAnimationInfos.Clear();
    }
}
