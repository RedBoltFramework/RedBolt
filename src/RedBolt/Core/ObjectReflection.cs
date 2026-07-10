namespace RedBolt;

public static class ObjectReflection<T>
{
    public static string ClassName { get; } = typeof(T).GetClassName();

    public static StringHash TypeId { get; } = ClassName;
}
