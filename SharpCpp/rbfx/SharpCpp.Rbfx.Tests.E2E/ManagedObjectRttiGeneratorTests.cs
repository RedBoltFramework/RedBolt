using Urho3D.Core;
using Urho3D.Engine;

using Xunit;

namespace SharpCpp.Rbfx.Tests.E2E;

public sealed class ManagedObjectRttiGeneratorTests
{
    [Fact]
    public void Managed_object_subclasses_receive_most_derived_rtti_surface()
    {
        using var context = new Context();
        using var app = new GeneratedManagedApplication(context);
        var generatedTypeInfo = GeneratedManagedApplication.GetTypeInfoStatic();

        Assert.Equal(GeneratedManagedApplication.GetTypeStatic(), app.GetType());
        Assert.Equal(new Urho3D.Math.StringHash(nameof(GeneratedManagedApplication)), GeneratedManagedApplication.GetTypeStatic());
        Assert.Same(generatedTypeInfo, app.GetTypeInfo());
        Assert.True(generatedTypeInfo.IsTypeOf(Application.GetTypeStatic()));
        Assert.True(app.IsInstanceOf(GeneratedManagedApplication.GetTypeStatic()));
        Assert.True(app.IsInstanceOf(Application.GetTypeStatic()));
    }
}

public partial class GeneratedManagedApplication : Application
{
    public GeneratedManagedApplication(Context context)
        : base(context)
    {
    }
}
