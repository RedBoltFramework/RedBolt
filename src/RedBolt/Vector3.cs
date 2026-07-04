namespace RedBolt;

public readonly record struct Vector3(float X, float Y, float Z)
{
    public static Vector3 Zero { get; } = new(0.0f, 0.0f, 0.0f);
}
