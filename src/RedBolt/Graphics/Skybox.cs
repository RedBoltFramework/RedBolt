namespace RedBolt;

public sealed class Skybox : StaticModel
{
    public Skybox()
    {
        SetReflectionMode(ReflectionMode.Zone);
    }

    public object? GetImage() => null;

    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(new BoundingBox(-MathDefs.LargeValue, MathDefs.LargeValue));
    }
}
