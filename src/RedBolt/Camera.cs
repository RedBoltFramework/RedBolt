namespace RedBolt;

public sealed class Camera : Component
{
    public float FarClip { get; set; } = 1000.0f;

    public float FieldOfView { get; set; } = 45.0f;

    public float NearClip { get; set; } = 0.1f;
}
