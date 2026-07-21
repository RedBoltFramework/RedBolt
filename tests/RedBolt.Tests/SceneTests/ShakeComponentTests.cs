using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShakeComponentTests
{
    [Fact]
    public void ShakeComponent_can_be_applied_outside_origin()
    {
        using var scene = new Scene();
        var node = scene.CreateChild();
        node.SetPosition(new Vector3f(1.0f, 2.0f, 3.0f));
        node.SetRotation(new Quaternion(new Vector3f(10.0f, 20.0f, 30.0f)));
        var shake = node.CreateComponent<ShakeComponent>();
        shake.RotationRange = new Vector3f(20.0f, 20.0f, 20.0f);
        shake.ShiftRange = new Vector3f(20.0f, 20.0f, 20.0f);
        shake.TraumaFalloff = 1.0f;
        shake.AddTrauma(0.25f);

        scene.Update(0.1f);
        Assert.False(new Vector3f(1.0f, 2.0f, 3.0f).ApproximatelyEquivalent(node.Position));
        Assert.False(new Quaternion(new Vector3f(10.0f, 20.0f, 30.0f)).ApproximatelyEquivalent(node.Rotation));

        node.Translate(new Vector3f(11.0f, 22.0f, 33.0f), TransformSpace.Parent);
        node.Rotate(new Quaternion(new Vector3f(11.0f, 22.0f, 32.0f)), TransformSpace.Parent);

        scene.Update(0.1f);
        scene.Update(0.1f);

        Assert.True(new Vector3f(12.0f, 24.0f, 36.0f).ApproximatelyEquivalent(node.Position));
        var expected = new Quaternion(new Vector3f(11.0f, 22.0f, 32.0f))
            * new Quaternion(new Vector3f(10.0f, 20.0f, 30.0f));
        Assert.True(expected.ApproximatelyEquivalent(node.Rotation, 1e-2f));
    }

    [Fact]
    public void Configuration_helpers_work()
    {
        using var shake = new ShakeComponent();
        shake.TimeScale = 10.0f;
        shake.TraumaPower = 3.0f;
        shake.TraumaFalloff = 2.0f;
        shake.AddTrauma(0.5f);

        Assert.True(MathDefs.Equals(shake.TimeScale, 10.0f));
        Assert.True(MathDefs.Equals(shake.TraumaPower, 3.0f));
        Assert.True(MathDefs.Equals(shake.TraumaFalloff, 3.0f));
        Assert.True(MathDefs.Equals(shake.Trauma, 0.5f));
    }

    [Fact]
    public void Class_shape_registration_and_attributes_match_native()
    {
        Assert.False(typeof(ShakeComponent).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(ShakeComponent)));

        using var context = new Context();
        ShakeComponent.RegisterObject(context);

        Assert.True(context.IsReflected<ShakeComponent>());
        using var created = Assert.IsType<ShakeComponent>(context.CreateObject(ObjectReflection<ShakeComponent>.TypeId));
        Assert.Contains(ObjectReflection<ShakeComponent>.TypeId,
            context.GetObjectCategories()[ObjectCategory.Category_Scene]);
        Assert.Equal(6u, created.NumAttributes);
        created.TraumaPower = 3.0f;
        created.TraumaFalloff = 2.0f;
        Assert.Equal(2.0f, created.GetAttribute("Trauma Falloff"));
        Assert.Equal(3.0f, created.TraumaFalloff);
    }
}
