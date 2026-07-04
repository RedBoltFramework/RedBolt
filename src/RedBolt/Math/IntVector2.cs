namespace RedBolt;

public readonly record struct IntVector2(int X, int Y)
{
    public static IntVector2 Zero { get; } = new(0, 0);
}
