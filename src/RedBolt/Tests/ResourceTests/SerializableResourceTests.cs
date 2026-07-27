using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;

namespace RedBolt.Tests;

public sealed class SerializableResourceTests
{
    [Fact]
    public void Serializable_resource_preserves_cpp_class_kind_and_missing_value_load()
    {
        var previousContext = Context.Instance;
        var context = new Context();
        Context.SetInstance(context);

        try
        {
            using var resource = new SerializableResource();
            using var empty = new SerializableResource();
            var camera = new Camera();
            resource.SetValue(camera);

            using var buffer = new VectorBuffer();
            Assert.True(empty.Save(buffer, InternalResourceFormat.Json));
            buffer.Seek(0);
            Assert.True(resource.Load(buffer));

            Assert.True(typeof(SerializableResource).IsClass);
            Assert.False(typeof(SerializableResource).IsSealed);
            Assert.DoesNotContain(typeof(ICloneable), typeof(SerializableResource).GetInterfaces());
            Assert.Same(camera, resource.GetValue());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }

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
        using var resource = new SerializableResource { Name = "Data/camera.serializable.json" };
        var camera = new Camera();
        camera.SetFov(72.0f);
        camera.SetNearClip(0.5f);
        camera.SetFarClip(250.0f);
        camera.SetOrthographic(true);
        resource.SetValue(camera);

        using var buffer = new VectorBuffer();
        Assert.True(resource.Save(buffer, format));
        buffer.Seek(0);

        using var loaded = new SerializableResource { Name = "Data/loaded.serializable.json" };
        Assert.True(loaded.Load(buffer));

        var loadedCamera = Assert.IsType<Camera>(loaded.GetValue());
        Assert.True(MathDefs.Equals(loadedCamera.GetFov(), 72.0f));
        Assert.True(MathDefs.Equals(loadedCamera.GetNearClip(), 0.0f));
        Assert.True(MathDefs.Equals(loadedCamera.GetFarClip(), 250.0f));
        Assert.True(loadedCamera.IsOrthographic());
        }
        finally
        {
            Context.SetInstance(previousContext);
        }
    }
}
