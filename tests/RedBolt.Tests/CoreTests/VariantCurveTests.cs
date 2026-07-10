using RedBolt;

namespace RedBolt.Tests;

public sealed class VariantCurveTests
{
    [Fact]
    public void Key_frame_set_sorts_and_uses_index_hint()
    {
        var curve = new VariantCurve();
        curve.AddKeyFrame(new VariantCurvePoint { Time = 2.0f, Value = 2.0f });
        curve.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 0.0f });
        curve.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = 1.0f });

        Assert.Equal([0.0f, 1.0f, 2.0f], curve.KeyFrames.Select(static keyFrame => keyFrame.Time));

        uint index = 2;
        Assert.True(curve.GetKeyFrameIndex(0.5f, ref index));
        Assert.Equal(0u, index);
        Assert.True(curve.GetKeyFrameIndex(1.5f, ref index));
        Assert.Equal(1u, index);
    }

    [Fact]
    public void Variant_curve_commits_and_samples_supported_interpolation_modes()
    {
        var curve = new VariantCurve();
        curve.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 0.0f });
        curve.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = 10.0f });
        curve.Commit();

        Assert.Equal(VariantType.Float, curve.Type);
        Assert.Equal(5.0f, curve.Sample(0.5f).GetFloat());

        curve.Interpolation = KeyFrameInterpolation.None;
        Assert.Equal(0.0f, curve.Sample(0.5f).GetFloat());

        curve.Interpolation = KeyFrameInterpolation.TensionSpline;
        curve.Commit();
        Assert.Equal(5.0f, curve.Sample(0.5f).GetFloat());
    }

    [Fact]
    public void Variant_curve_falls_back_to_supported_integer_interpolation()
    {
        var curve = new VariantCurve { Interpolation = KeyFrameInterpolation.TangentSpline };
        curve.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 0 });
        curve.AddKeyFrame(new VariantCurvePoint { Time = 1.0f, Value = 10 });
        curve.Commit();

        Assert.Equal(KeyFrameInterpolation.Linear, curve.Interpolation);
        Assert.Equal(5, curve.Sample(0.5f).GetInt());
    }

    [Fact]
    public void Variant_stores_variant_curve()
    {
        var curve = new VariantCurve { Name = "Curve" };
        curve.AddKeyFrame(new VariantCurvePoint { Time = 0.0f, Value = 1.0f });
        curve.Commit();

        var variant = new Variant(curve);

        Assert.Equal(VariantType.VariantCurve, variant.Type);
        Assert.Same(curve, variant.GetVariantCurve());
        Assert.Same(curve, variant.Get<VariantCurve>());
        Assert.Same(curve, variant.GetObject());
        Assert.Equal(variant, Variant.FromObject(curve));
    }
}
