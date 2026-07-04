using RedBolt;

namespace RedBolt.Tests;

public sealed class ObjectAnimationTests
{
    [Fact]
    public void Add_remove_and_lookup_work()
    {
        var animation = new ValueAnimation("Animations/Fov");
        animation.SetKeyFrame(0.0f, 45.0f);
        animation.SetKeyFrame(1.0f, 90.0f);

        var objectAnimation = new ObjectAnimation("Animations/Object");
        objectAnimation.AddAttributeAnimation("FOV", animation, WrapMode.Clamp, 2.0f);

        Assert.Same(animation, objectAnimation.GetAttributeAnimation("FOV"));
        Assert.Equal(WrapMode.Clamp, objectAnimation.GetAttributeAnimationWrapMode("FOV"));
        Assert.True(MathDefs.ApproximatelyEquivalent(objectAnimation.GetAttributeAnimationSpeed("FOV"), 2.0f));

        objectAnimation.RemoveAttributeAnimation(animation);
        Assert.Null(objectAnimation.GetAttributeAnimation("FOV"));
    }

    [Fact]
    public void Xml_and_json_roundtrip_work()
    {
        var animation = new ValueAnimation("Animations/Position");
        animation.SetKeyFrame(0.0f, new Vector3(0.0f, 0.0f, 0.0f));
        animation.SetKeyFrame(1.0f, new Vector3(10.0f, 0.0f, 0.0f));
        var objectAnimation = new ObjectAnimation("Animations/Object");
        objectAnimation.AddAttributeAnimation("Position", animation, WrapMode.Once, 3.0f);

        using var xmlStream = new MemoryStream();
        Assert.True(objectAnimation.Save(xmlStream));
        xmlStream.Position = 0;
        var loadedXml = new ObjectAnimation("Animations/LoadedXml");
        Assert.True(loadedXml.Load(xmlStream));
        Assert.Equal(WrapMode.Once, loadedXml.GetAttributeAnimationWrapMode("Position"));
        Assert.True(MathDefs.ApproximatelyEquivalent(loadedXml.GetAttributeAnimationSpeed("Position"), 3.0f));

        var json = new JSONValue(JSONValueType.Object);
        Assert.True(objectAnimation.SaveJSON(json));
        var loadedJson = new ObjectAnimation("Animations/LoadedJson");
        Assert.True(loadedJson.LoadJSON(json));
        Assert.NotNull(loadedJson.GetAttributeAnimation("Position"));
    }

    [Fact]
    public void Animatable_applies_object_animation()
    {
        var node = new Scene().CreateChild("Animated");
        var animation = new ValueAnimation("Animations/Position");
        animation.SetKeyFrame(0.0f, new Vector3(0.0f, 0.0f, 0.0f));
        animation.SetKeyFrame(1.0f, new Vector3(10.0f, 0.0f, 0.0f));
        var objectAnimation = new ObjectAnimation("Animations/Object");
        objectAnimation.AddAttributeAnimation("Position", animation);

        node.SetObjectAnimation(objectAnimation);
        node.SetAnimationTime(0.5f);

        Assert.True(node.Position.ApproximatelyEquivalent(new Vector3(5.0f, 0.0f, 0.0f)));
        Assert.Same(objectAnimation, node.GetObjectAnimation());

        node.RemoveObjectAnimation();
        Assert.Null(node.GetObjectAnimation());
        Assert.Null(node.GetAttributeAnimation("Position"));
    }
}
