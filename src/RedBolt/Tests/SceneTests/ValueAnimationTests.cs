using RedBolt;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
namespace RedBolt.Tests;

public sealed class ValueAnimationTests
{
    [Fact]
    public void WrapMode_matches_rbfx_value_enum()
    {
        Assert.True(typeof(WrapMode).IsEnum);
        Assert.True(typeof(WrapMode).IsValueType);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(WrapMode)));
        Assert.Equal(0, (int)WrapMode.Loop);
        Assert.Equal(1, (int)WrapMode.Once);
        Assert.Equal(2, (int)WrapMode.Clamp);
    }

    [Fact]
    public void Linear_float_interpolation_works()
    {
        var animation = new ValueAnimation { Name = "Animations/Test" };
        Assert.True(animation.SetKeyFrame(0.0f, 0.0f));
        Assert.True(animation.SetKeyFrame(1.0f, 10.0f));

        Assert.Equal(VariantType.Float, animation.ValueType);
        Assert.True(animation.IsValid());
        using var value = animation.GetAnimationValue(0.5f);
        Assert.True(MathDefs.Equals(value.GetFloat(), 5.0f));
    }

    [Fact]
    public void Duplicate_time_and_type_mismatch_are_rejected()
    {
        var animation = new ValueAnimation { Name = "Animations/Test" };
        Assert.True(animation.SetKeyFrame(0.0f, 0.0f));
        Assert.False(animation.SetKeyFrame(0.0f, 1.0f));
        Assert.False(animation.SetKeyFrame(1.0f, new Vector3f(1.0f, 2.0f, 3.0f)));
    }

    [Fact]
    public void Key_and_event_frames_own_independent_variant_values()
    {
        using var animation = new ValueAnimation { Name = "Animations/Test" };
        using var sourceValue = new Variant(new byte[] { 1, 2, 3 });
        Assert.True(animation.SetKeyFrame(0.0f, sourceValue));

        sourceValue.GetBuffer()[0] = 9;
        using (var firstResult = animation.GetAnimationValue(0.0f))
        {
            Assert.Equal(new byte[] { 1, 2, 3 }, firstResult.GetBuffer());
            firstResult.GetBuffer()[1] = 8;
        }

        using (var secondResult = animation.GetAnimationValue(0.0f))
            Assert.Equal(new byte[] { 1, 2, 3 }, secondResult.GetBuffer());

        var key = new StringHash("buffer");
        using (var eventData = new VariantMap())
        {
            using var eventValue = new Variant(new byte[] { 4, 5, 6 });
            eventData[key] = eventValue;
            animation.SetEventFrame(0.5f, new StringHash(42u), eventData);
            eventData[key].GetBuffer()[0] = 7;
        }

        var eventFrames = new List<VAnimEventFrame>();
        animation.GetEventFrames(0.0f, 1.0f, eventFrames);
        Assert.Equal(new byte[] { 4, 5, 6 }, Assert.Single(eventFrames).EventData[key].GetBuffer());
    }

    [Fact]
    public void Spline_vector_interpolation_works()
    {
        var animation = new ValueAnimation { Name = "Animations/Test" };
        animation.InterpolationMethod = InterpMethod.Spline;
        Assert.True(animation.SetKeyFrame(0.0f, new Vector3f(0.0f, 0.0f, 0.0f)));
        Assert.True(animation.SetKeyFrame(1.0f, new Vector3f(10.0f, 0.0f, 0.0f)));
        Assert.True(animation.SetKeyFrame(2.0f, new Vector3f(20.0f, 0.0f, 0.0f)));
        Assert.True(animation.SetKeyFrame(3.0f, new Vector3f(30.0f, 0.0f, 0.0f)));

        using var value = animation.GetAnimationValue(1.5f);
        Assert.True(value.GetVector3().ApproximatelyEquivalent(new Vector3f(15.0f, 0.0f, 0.0f), 0.001f));
    }

    [Fact]
    public void Xml_and_json_roundtrip_work()
    {
        var animation = new ValueAnimation { Name = "Animations/Test" };
        animation.InterpolationMethod = InterpMethod.Spline;
        animation.SplineTension = 0.25f;
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);
        animation.SetKeyFrame(2.0f, 20.0f);
        animation.SetEventFrame(0.5f, new StringHash(123u), new VariantMap { [new StringHash("flag")] = true });

        using var stream = new VectorBuffer();
        Assert.True(animation.Save(stream));
        stream.Seek(0);

        var loadedXml = new ValueAnimation { Name = "Animations/LoadedXml" };
        Assert.True(loadedXml.Load(stream));
        Assert.Equal(InterpMethod.Spline, loadedXml.InterpolationMethod);
        Assert.True(MathDefs.Equals(loadedXml.SplineTension, 0.25f));
        var xmlEventFrames = new List<VAnimEventFrame>();
        loadedXml.GetEventFrames(0.0f, 1.0f, xmlEventFrames);
        var xmlEventFrame = Assert.Single(xmlEventFrames);
        Assert.True(xmlEventFrame.EventData[new StringHash("flag")].GetBool());

        var json = new JSONValue(JSONValueType.Object);
        Assert.True(animation.SaveJSON(json));
        var loadedJson = new ValueAnimation { Name = "Animations/LoadedJson" };
        Assert.True(loadedJson.LoadJSON(json));
        Assert.Equal(3, loadedJson.KeyFrames.Count);
        var jsonEventFrames = new List<VAnimEventFrame>();
        loadedJson.GetEventFrames(0.0f, 1.0f, jsonEventFrames);
        var jsonEventFrame = Assert.Single(jsonEventFrames);
        Assert.True(jsonEventFrame.EventData[new StringHash("flag")].GetBool());
    }

    [Fact]
    public void ValueAnimationInfo_wrap_modes_and_events_work()
    {
        var animation = new ValueAnimation { Name = "Animations/Test" };
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);
        animation.SetEventFrame(0.5f, new StringHash(77u));

        var target = new TestTarget();
        var info = new TestValueAnimationInfo(target, animation, WrapMode.Once, 1.0f);
        VAnimEventFrame? lastEvent = null;
        info.EventTriggered += frame => lastEvent = frame;

        Assert.False(info.Update(0.5f));
        Assert.True(MathDefs.Equals((float)info.LastValue!, 5.0f));
        Assert.Equal(77u, lastEvent?.EventType.Value);
        Assert.True(info.Update(0.6f));
        Assert.True(MathDefs.Equals((float)info.LastValue!, 10.0f));

        var loopingTarget = new TestTarget();
        var looping = new TestValueAnimationInfo(loopingTarget, animation, WrapMode.Loop, 1.0f);
        looping.SetTime(1.25f);
        Assert.True(MathDefs.Equals((float)looping.LastValue!, 2.5f));
    }

    [Fact]
    public void Event_frame_begin_boundary_and_negative_speed_follow_rbfx_rules()
    {
        var animation = new ValueAnimation { Name = "Animations/Test" };
        animation.SetKeyFrame(0.0f, 0.0f);
        animation.SetKeyFrame(1.0f, 10.0f);
        animation.SetEventFrame(0.25f, new StringHash(11u));

        var eventFrames = new List<VAnimEventFrame>();
        animation.GetEventFrames(0.25f, 0.5f, eventFrames);
        Assert.Single(eventFrames);

        var target = new TestTarget();
        var info = new TestValueAnimationInfo(target, animation, WrapMode.Clamp, -1.0f);
        Assert.Equal(0.0f, info.Speed);
        info.Speed = -1.0f;
        Assert.Equal(-1.0f, info.Speed);
        info.Speed = 0.0f;
        info.SetTime(1.0f);
        info.Update(1.0f);
        Assert.True(MathDefs.Equals((float)info.LastValue!, 10.0f));

        var targetless = new TestValueAnimationInfo(animation, WrapMode.Clamp, 1.0f);
        Assert.True(targetless.Update(0.5f));
        Assert.Null(targetless.LastValue);

        var clone = Assert.IsType<TestValueAnimationInfo>(info.Clone());
        Assert.Same(animation, clone.Animation);
        Assert.Equal(WrapMode.Clamp, clone.WrapMode);
        Assert.Equal(0.0f, clone.Time);
    }

    private sealed class TestValueAnimationInfo : ValueAnimationInfo
    {
        public TestValueAnimationInfo(ValueAnimation animation, WrapMode wrapMode, float speed)
            : base(animation, wrapMode, speed)
        {
        }

        public TestValueAnimationInfo(RedObject target, ValueAnimation animation, WrapMode wrapMode, float speed)
            : base(target, animation, wrapMode, speed)
        {
        }

        public object? LastValue { get; private set; }

        private TestValueAnimationInfo(TestValueAnimationInfo rhs)
            : base(rhs)
        {
        }

        public override object Clone() => new TestValueAnimationInfo(this);

        protected override void ApplyValue(in Variant newValue)
        {
            LastValue = newValue.GetObject();
        }
    }

    private sealed class TestTarget : RedObject
    {
    }
}
