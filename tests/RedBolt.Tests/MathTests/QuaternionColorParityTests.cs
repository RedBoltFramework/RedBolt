namespace RedBolt.Tests.MathTests;

public class QuaternionColorParityTests
{
    [Fact]
    public void QuaternionAngularVelocityRoundTripsAndIdentityAxisMatchesRbfx()
    {
        var velocity = new Vector3f(0.1f, -0.2f, 0.3f);
        var rotation = Quaternion.FromAngularVelocity(velocity);

        Assert.True(rotation.AngularVelocity.ApproximatelyEquivalent(velocity, 0.0001f));
        Assert.Equal(Vector3f.Up, Quaternion.Identity.Axis);
        Assert.True(rotation.Equivalent(-rotation));
    }

    [Fact]
    public void QuaternionSwingTwistRecomposesOriginalRotation()
    {
        var rotation = Quaternion.FromEulerAngles(20, 30, 40);
        var parts = rotation.ToSwingTwist(Vector3f.Up);

        Assert.True((parts.Swing * parts.Twist).Equivalent(rotation, 0.0001f));
    }

    [Fact]
    public void ColorPackedMasksAndColorSpacesRoundTrip()
    {
        var color = new Color(0.2f, 0.4f, 0.8f, 0.6f);
        var packed = color.ToUIntMask(Color.ARGB);
        var unpacked = new Color(packed, Color.ARGB);

        Assert.True(color.ApproximatelyEquivalent(unpacked, 1.0f / 255.0f + 0.0001f));

        var hsv = color.ToHSV();
        var fromHsv = new Color();
        fromHsv.FromHSV(hsv.X, hsv.Y, hsv.Z, color.A);
        Assert.True(color.ApproximatelyEquivalent(fromHsv, 0.0001f));
    }

    [Fact]
    public void ColorExactEqualityDoesNotUseApproximation()
    {
        var lhs = new Color(0.1f, 0.2f, 0.3f, 1.0f);
        var rhs = new Color(0.1f + MathDefs.Epsilon * 0.5f, 0.2f, 0.3f, 1.0f);

        Assert.NotEqual(lhs, rhs);
        Assert.True(lhs.ApproximatelyEquivalent(rhs));
        Assert.True(colorRoundTrip(lhs));

        static bool colorRoundTrip(Color color) => color.GammaToLinear().LinearToGamma().ApproximatelyEquivalent(color, 0.0001f);
    }
}
