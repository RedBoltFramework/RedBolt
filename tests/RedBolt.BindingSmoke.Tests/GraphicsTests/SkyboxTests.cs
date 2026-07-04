using RedBolt;

namespace RedBolt.BindingSmoke.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SkyboxTests
{
    private readonly HeadlessEngineFixture fixture;

    public SkyboxTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private ResourceCache Resources => fixture.Resources;

    [Fact]
    public void Defaults_and_bounds_work()
    {
        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        var skybox = new Scene().CreateChild("Skybox").CreateComponent<Skybox>();
        skybox.SetModel(model);
        skybox.SetMaterial(material);

        Assert.Equal(ReflectionMode.Zone, skybox.GetReflectionMode());
        Assert.Equal(DrawableFlags.Geometry, skybox.GetDrawableFlags());
        Assert.Null(skybox.GetImage());
        Assert.Equal(Intersection.Inside,
            skybox.GetWorldBoundingBox().IsInside(new Vector3(50_000.0f, 0.0f, 0.0f)));
    }
}
