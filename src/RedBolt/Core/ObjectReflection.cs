using System.Reflection;

namespace RedBolt;

public static class ObjectReflection<T>
{
    public static string ClassName { get; }

    public static StringHash TypeId { get; }

    static ObjectReflection()
    {
        var type = typeof(T);
        var getTypeNameStatic = type.GetMethod("GetTypeNameStatic", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        ClassName = getTypeNameStatic?.Invoke(null, Array.Empty<object>()) as string ?? type.Name;

        var getTypeStatic = type.GetMethod("GetTypeStatic", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var typeId = getTypeStatic?.Invoke(null, Array.Empty<object>());
        TypeId = typeId switch
        {
            StringHash hash => hash,
            uint value => new StringHash(value),
            int value => new StringHash((uint)value),
            string value => value,
            _ => ClassName
        };
    }
}
