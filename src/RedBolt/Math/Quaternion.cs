namespace RedBolt;

public readonly record struct Quaternion(float X, float Y, float Z, float W)
{
    public static Quaternion Identity { get; } = new(0.0f, 0.0f, 0.0f, 1.0f);
}
