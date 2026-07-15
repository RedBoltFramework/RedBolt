// C++: no direct C++ counterpart file.
// C++ equivalent functionality lives in Object.h (TypeName, GetTypeNameStatic, URHO3D_OBJECT reflection).
// The C# version provides a reflection-based facade for the C++ URHO3D_OBJECT metadata system.
namespace RedBolt;

public static class ObjectExtensionMethods
{
    public static string GetFormattedTypeName(this Type type)
    {
        return GetFormattedTypeName(type, fullyQualified: false);
    }

    public static string GetClassName(this Type type)
    {
        return GetFormattedTypeName(type, fullyQualified: false);
    }

    public static StringHash GetTypeId(this Type type)
    {
        return GetClassName(type);
    }

    private static string GetFormattedTypeName(this Type type, bool fullyQualified)
    {
        ArgumentNullException.ThrowIfNull(type);

        var typeName = GetTypeDisplayName(type, fullyQualified);
        if (!type.IsGenericType)
            return typeName;

        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(argument => GetFormattedTypeName(argument, fullyQualified)));
        return $"{typeName}<{genericArguments}>";
    }

    private static string GetTypeDisplayName(Type type, bool fullyQualified)
    {
        var name = type.IsGenericType ? type.GetGenericTypeDefinition().Name : type.Name;
        var tickIndex = name.IndexOf('`', StringComparison.Ordinal);
        var baseName = tickIndex >= 0 ? name[..tickIndex] : name;
        if (!fullyQualified)
            return baseName;

        if (type.DeclaringType is not null)
            return $"{GetTypeDisplayName(type.DeclaringType, fullyQualified: true)}.{baseName}";

        return string.IsNullOrEmpty(type.Namespace) ? baseName : $"{type.Namespace}.{baseName}";
    }
}
