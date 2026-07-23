using RedBolt;

namespace RedBolt.Tests;

public sealed class ObjectAnimationTests
{
    [Fact]
    public void Add_remove_and_lookup_work()
    {
        var animation = new ValueAnimation { Name = "Animations/Fov" };
        animation.SetKeyFrame(0.0f, 45.0f);
        animation.SetKeyFrame(1.0f, 90.0f);

        var objectAnimation = new ObjectAnimation { Name = "Animations/Object" };
        objectAnimation.AddAttributeAnimation("FOV", animation, WrapMode.Clamp, 2.0f);

        Assert.Same(animation, objectAnimation.GetAttributeAnimation("FOV"));
        Assert.Equal(WrapMode.Clamp, objectAnimation.GetAttributeAnimationWrapMode("FOV"));
        Assert.True(MathDefs.Equals(objectAnimation.GetAttributeAnimationSpeed("FOV"), 2.0f));

        objectAnimation.RemoveAttributeAnimation(animation);
        Assert.Null(objectAnimation.GetAttributeAnimation("FOV"));
    }

    [Fact]
    public void Xml_and_json_roundtrip_work()
    {
        var animation = new ValueAnimation { Name = "Animations/Position" };
        animation.SetKeyFrame(0.0f, new Vector3f(0.0f, 0.0f, 0.0f));
        animation.SetKeyFrame(1.0f, new Vector3f(10.0f, 0.0f, 0.0f));
        var objectAnimation = new ObjectAnimation { Name = "Animations/Object" };
        objectAnimation.AddAttributeAnimation("Position", animation, WrapMode.Once, 3.0f);

        using var xmlStream = new VectorBuffer();
        Assert.True(objectAnimation.Save(xmlStream));
        xmlStream.Seek(0);
        var loadedXml = new ObjectAnimation { Name = "Animations/LoadedXml" };
        Assert.True(loadedXml.Load(xmlStream));
        Assert.Equal(WrapMode.Once, loadedXml.GetAttributeAnimationWrapMode("Position"));
        Assert.True(MathDefs.Equals(loadedXml.GetAttributeAnimationSpeed("Position"), 3.0f));

        var json = new JSONValue(JSONValueType.Object);
        Assert.True(objectAnimation.SaveJSON(json));
        var loadedJson = new ObjectAnimation { Name = "Animations/LoadedJson" };
        Assert.True(loadedJson.LoadJSON(json));
        Assert.NotNull(loadedJson.GetAttributeAnimation("Position"));
    }

    [Fact]
    public void Animatable_applies_object_animation()
    {
        var animatable = new TestAnimatable();
        var animation = new ValueAnimation { Name = "Animations/Value" };
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);
        var objectAnimation = new ObjectAnimation { Name = "Animations/Object" };
        objectAnimation.AddAttributeAnimation("Value", animation);

        animatable.SetObjectAnimation(objectAnimation);
        animatable.SetAnimationTime(0.5f);

        Assert.True(MathDefs.Equals(animatable.Value, 5.0f));
        Assert.Same(objectAnimation, animatable.GetObjectAnimation());

        animatable.RemoveObjectAnimation();
        Assert.Null(animatable.GetObjectAnimation());
        Assert.Null(animatable.GetAttributeAnimation("Value"));
    }

    [Fact]
    public void Add_remove_dispatch_attribute_animation_events()
    {
        var objectAnimation = new ObjectAnimation { Name = "Animations/Object" };
        var animation = new ValueAnimation { Name = "Animations/Fov" };
        animation.SetKeyFrame(0.0f, 45.0f);
        animation.SetKeyFrame(1.0f, 90.0f);
        var context = new Context();
        Context.SetInstance(context);
        try
        {
            using var receiver = new TestEventReceiver();
            var added = 0;
            var removed = 0;
            receiver.SubscribeToEvent<SceneEvents.AttributeAnimationAddedEvent>(
                objectAnimation, (ref SceneEvents.AttributeAnimationAddedEvent _) => ++added);
            receiver.SubscribeToEvent<SceneEvents.AttributeAnimationRemovedEvent>(
                objectAnimation, (ref SceneEvents.AttributeAnimationRemovedEvent _) => ++removed);

            objectAnimation.AddAttributeAnimation("FOV", animation);
            objectAnimation.RemoveAttributeAnimation("FOV");

            Assert.Equal(1, added);
            Assert.Equal(1, removed);
        }
        finally
        {
            Context.SetInstance(new Context());
        }
    }
}
