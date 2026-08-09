namespace RedBolt.Engine;

using System.Globalization;
using System.Reflection;

internal static class EngineParameterBinding
{
    private static readonly Dictionary<string, PropertyInfo> Properties = typeof(EngineParameters)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(static property => property.CanRead && property.CanWrite)
        .ToDictionary(static property => property.Name, StringComparer.Ordinal);

    public static bool IsKnown(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Properties.ContainsKey(name);
    }

    public static bool TryGetValue(EngineParameters parameters, string name, out object? value)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Properties.TryGetValue(name, out var property))
        {
            value = null;
            return false;
        }

        value = property.GetValue(parameters);
        return true;
    }

    public static bool TrySetValue(EngineParameters parameters, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Properties.TryGetValue(name, out var property))
            return false;

        property.SetValue(parameters, CoerceValue(property.PropertyType, value));
        return true;
    }

    private static object? CoerceValue(Type targetType, object? value)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        if (value is null)
        {
            if (!targetType.IsValueType || nullableType is not null)
                return null;
            throw new InvalidCastException($"Cannot assign null to '{targetType.Name}'.");
        }

        var effectiveType = nullableType ?? targetType;
        if (effectiveType.IsInstanceOfType(value))
            return value;

        if (effectiveType.IsEnum)
        {
            if (value is string stringValue)
                return Enum.Parse(effectiveType, stringValue, ignoreCase: true);

            var enumUnderlyingType = Enum.GetUnderlyingType(effectiveType);
            var convertedEnumValue = Convert.ChangeType(value, enumUnderlyingType, CultureInfo.InvariantCulture)
                ?? throw new InvalidCastException($"Unable to convert '{value}' to '{effectiveType.Name}'.");
            return Enum.ToObject(effectiveType, convertedEnumValue);
        }

        if (effectiveType == typeof(nint))
            return (nint)Convert.ToInt64(value, CultureInfo.InvariantCulture);
        if (effectiveType == typeof(uint))
            return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        if (effectiveType == typeof(int))
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        if (effectiveType == typeof(bool))
            return value is string stringValue
                ? bool.Parse(stringValue)
                : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        if (effectiveType == typeof(string))
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }
}
