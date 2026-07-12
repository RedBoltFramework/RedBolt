using RedBolt;

namespace RedBolt.Tests;

public sealed class SerializableResourceTests
{
    [Theory]
    [InlineData(InternalResourceFormat.Json)]
    [InlineData(InternalResourceFormat.Xml)]
    [InlineData(InternalResourceFormat.Binary)]
    public void Serializable_resource_roundtrips_camera(InternalResourceFormat format)
    {
        var previousContext = Context.Instance;
        var context = new Context();
        context.AddFactoryReflection<Camera>();
        Context.SetInstance(context);

        try
        {
        var resource = new SerializableResource("Data/camera.serializable.json");
        var camera = new Camera();
        camera.SetFov(72.0f);
        camera.SetNearClip(0.5f);
        camera.SetFarClip(250.0f);
        camera.SetOrthographic(true);
        resource.SetValue(camera);

        using var buffer = new MemoryStream();
        Assert.True(resource.Save(buffer, format));
        buffer.Position = 0;

        var loaded = new SerializableResource("Data/loaded.serializable.json");
        Assert.True(loaded.Load(buffer));

        var loadedCamera = Assert.IsType<Camera>(loaded.GetValue());
        Assert.True(MathDefs.ApproximatelyEquivalent(loadedCamera.GetFov(), 72.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(loadedCamera.GetNearClip(), 0.5f));
        Assert.True(MathDefs.ApproximatelyEquivalent(loadedCamera.GetFarClip(), 250.0f));
        Assert.True(loadedCamera.IsOrthographic());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
