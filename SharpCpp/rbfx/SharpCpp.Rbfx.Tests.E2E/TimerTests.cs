using SharpCpp.Support;

using Urho3D.Graphics;

using Xunit;

namespace SharpCpp.Rbfx.Tests.E2E;

public sealed class BiasParametersTests
{
    [Fact]
    public void Generated_bias_parameters_wrapper_exposes_supported_surface()
    {
        var type = typeof(BiasParameters);

        Assert.True(type.IsSubclassOf(typeof(NativeObject)));
        Assert.NotNull(type.GetConstructor(global::System.Type.EmptyTypes));

        Assert.Equal(typeof(float), type.GetProperty(nameof(BiasParameters.ConstantBias))!.PropertyType);
        Assert.Equal(typeof(float), type.GetProperty(nameof(BiasParameters.SlopeScaledBias))!.PropertyType);
        Assert.Equal(typeof(float), type.GetProperty(nameof(BiasParameters.NormalOffset))!.PropertyType);
        Assert.NotNull(type.GetMethod(nameof(BiasParameters.Validate), global::System.Type.EmptyTypes));
    }
}
