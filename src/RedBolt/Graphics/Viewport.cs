namespace RedBolt;

public sealed class Viewport
{
    public Viewport(Scene scene, Camera camera)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
    }

    public Camera Camera { get; }

    public Scene Scene { get; }
}
