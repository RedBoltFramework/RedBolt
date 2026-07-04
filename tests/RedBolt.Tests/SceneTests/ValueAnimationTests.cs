using RedBolt;

namespace RedBolt.Tests;

public sealed class ValueAnimationTests
{
    [Fact]
    public void Linear_float_interpolation_works()
    {
        var animation = new ValueAnimation("Animations/Test");
        Assert.True(animation.SetKeyFrame(0.0f, 0.0f));
        Assert.True(animation.SetKeyFrame(1.0f, 10.0f));

        Assert.Equal(VariantType.Float, animation.GetValueType());
        Assert.True(animation.IsValid());
        Assert.True(MathDefs.ApproximatelyEquivalent((float)animation.GetAnimationValue(0.5f)!, 5.0f));
    }

    [Fact]
    public void Duplicate_time_and_type_mismatch_are_rejected()
    {
        var animation = new ValueAnimation("Animations/Test");
        Assert.True(animation.SetKeyFrame(0.0f, 0.0f));
        Assert.False(animation.SetKeyFrame(0.0f, 1.0f));
        Assert.False(animation.SetKeyFrame(1.0f, new Vector3(1.0f, 2.0f, 3.0f)));
    }

    [Fact]
    public void Spline_vector_interpolation_works()
    {
        var animation = new ValueAnimation("Animations/Test");
        animation.SetInterpolationMethod(InterpMethod.Spline);
        Assert.True(animation.SetKeyFrame(0.0f, new Vector3(0.0f, 0.0f, 0.0f)));
        Assert.True(animation.SetKeyFrame(1.0f, new Vector3(10.0f, 0.0f, 0.0f)));
        Assert.True(animation.SetKeyFrame(2.0f, new Vector3(20.0f, 0.0f, 0.0f)));
        Assert.True(animation.SetKeyFrame(3.0f, new Vector3(30.0f, 0.0f, 0.0f)));

        var value = (Vector3)animation.GetAnimationValue(1.5f)!;
        Assert.True(value.ApproximatelyEquivalent(new Vector3(15.0f, 0.0f, 0.0f), 0.001f));
    }

    [Fact]
    public void Xml_and_json_roundtrip_work()
    {
        var animation = new ValueAnimation("Animations/Test");
        animation.SetInterpolationMethod(InterpMethod.Spline);
        animation.SetSplineTension(0.25f);
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);
        animation.SetKeyFrame(2.0f, 20.0f);
        animation.SetEventFrame(0.5f, new StringHash(123u), new VariantMap { [new StringHash("flag")] = true });

        using var stream = new MemoryStream();
        Assert.True(animation.Save(stream));
        stream.Position = 0;

        var loadedXml = new ValueAnimation("Animations/LoadedXml");
        Assert.True(loadedXml.Load(stream));
        Assert.Equal(InterpMethod.Spline, loadedXml.GetInterpolationMethod());
        Assert.True(MathDefs.ApproximatelyEquivalent(loadedXml.GetSplineTension(), 0.25f));
        Assert.Single(loadedXml.GetEventFrames(0.0f, 1.0f));

        var json = new JSONValue(JSONValueType.Object);
        Assert.True(animation.SaveJSON(json));
        var loadedJson = new ValueAnimation("Animations/LoadedJson");
        Assert.True(loadedJson.LoadJSON(json));
        Assert.Equal(3, loadedJson.GetKeyFrames().Count);
        Assert.Single(loadedJson.GetEventFrames(0.0f, 1.0f));
    }

    [Fact]
    public void ValueAnimationInfo_wrap_modes_and_events_work()
    {
        var animation = new ValueAnimation("Animations/Test");
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);
        animation.SetEventFrame(0.5f, new StringHash(77u));

        var info = new TestValueAnimationInfo(animation, WrapMode.Once, 1.0f);
        VAnimEventFrame? lastEvent = null;
        info.EventTriggered += frame => lastEvent = frame;

        Assert.False(info.Update(0.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent((float)info.LastValue!, 5.0f));
        Assert.Equal(77u, lastEvent?.EventType.Value);
        Assert.True(info.Update(0.6f));
        Assert.True(MathDefs.ApproximatelyEquivalent((float)info.LastValue!, 10.0f));

        var looping = new TestValueAnimationInfo(animation, WrapMode.Loop, 1.0f);
        looping.SetTime(1.25f);
        Assert.True(MathDefs.ApproximatelyEquivalent((float)looping.LastValue!, 2.5f));
    }

    private sealed class TestValueAnimationInfo : ValueAnimationInfo
    {
        public TestValueAnimationInfo(ValueAnimation animation, WrapMode wrapMode, float speed)
            : base(animation, wrapMode, speed)
        {
        }

        public object? LastValue { get; private set; }

        protected override void ApplyValue(object? newValue)
        {
            LastValue = newValue;
        }
    }
}
