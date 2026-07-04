namespace RedBolt;

public static class ObjectExtensionMethods
{
    public static string GetFormattedTypeName(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        var tickIndex = name.IndexOf('`', StringComparison.Ordinal);
        var genericName = tickIndex >= 0 ? name[..tickIndex] : name;
        var genericArguments = string.Join(", ", type.GetGenericArguments().Select(GetFormattedTypeName));
        return $"{genericName}<{genericArguments}>";
    }
}
