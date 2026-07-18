namespace SharpCpp.Model.Ast;

public sealed class BindingAliasTargetKey : IEquatable<BindingAliasTargetKey>
{
    private readonly BindingAliasTargetKey[] _genericArguments;
    private readonly BindingAliasTargetKey[] _callbackParameterTypes;

    private BindingAliasTargetKey(
        BindingTypeKind kind,
        BindingPrimitiveKind primitiveKind,
        string nativeIdentity,
        string? genericParameterName,
        IEnumerable<BindingAliasTargetKey> genericArguments,
        bool isCallback,
        BindingAliasTargetKey? callbackReturnType,
        IEnumerable<BindingAliasTargetKey> callbackParameterTypes)
    {
        Kind = kind;
        PrimitiveKind = primitiveKind;
        NativeIdentity = nativeIdentity;
        GenericParameterName = genericParameterName;
        _genericArguments = genericArguments.ToArray();
        IsCallback = isCallback;
        CallbackReturnType = callbackReturnType;
        _callbackParameterTypes = callbackParameterTypes.ToArray();
    }

    public BindingTypeKind Kind { get; }

    public BindingPrimitiveKind PrimitiveKind { get; }

    public string NativeIdentity { get; }

    public string? GenericParameterName { get; }

    public IReadOnlyList<BindingAliasTargetKey> GenericArguments => _genericArguments;

    public bool IsCallback { get; }

    public BindingAliasTargetKey? CallbackReturnType { get; }

    public IReadOnlyList<BindingAliasTargetKey> CallbackParameterTypes => _callbackParameterTypes;

    public static BindingAliasTargetKey From(BindingType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return new BindingAliasTargetKey(
            type.Kind,
            type.PrimitiveKind,
            GetNativeIdentity(type),
            type.GenericParameterName,
            type.IsCallback ? Array.Empty<BindingAliasTargetKey>() : type.GenericArguments.Select(From),
            type.IsCallback,
            type.CallbackReturnType is null ? null : From(type.CallbackReturnType),
            type.CallbackParameterTypes.Select(From));
    }

    private static string GetNativeIdentity(BindingType type)
    {
        if (!string.IsNullOrWhiteSpace(type.RecordId))
        {
            return type.RecordId!;
        }

        if (!string.IsNullOrWhiteSpace(type.EnumId))
        {
            return type.EnumId!;
        }

        if (type.Kind == BindingTypeKind.GenericParameter)
        {
            return type.GenericParameterName ?? type.CppTypeName;
        }

        return NormalizeCppTypeName(type.CppTypeName);
    }

    private static string NormalizeCppTypeName(string cppTypeName)
    {
        var result = cppTypeName.Trim();
        while (result.StartsWith("const ", StringComparison.Ordinal) || result.StartsWith("volatile ", StringComparison.Ordinal))
        {
            result = result.StartsWith("const ", StringComparison.Ordinal)
                ? result[6..].TrimStart()
                : result[9..].TrimStart();
        }

        if (result.EndsWith("&&*", StringComparison.Ordinal))
        {
            result = result[..^3] + "*";
        }
        else if (result.EndsWith("&*", StringComparison.Ordinal))
        {
            result = result[..^2] + "*";
        }

        return result.TrimEnd('&').Trim();
    }

    public bool Equals(BindingAliasTargetKey? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Kind == other.Kind &&
            PrimitiveKind == other.PrimitiveKind &&
            string.Equals(NativeIdentity, other.NativeIdentity, StringComparison.Ordinal) &&
            string.Equals(GenericParameterName, other.GenericParameterName, StringComparison.Ordinal) &&
            IsCallback == other.IsCallback &&
            Equals(CallbackReturnType, other.CallbackReturnType) &&
            _genericArguments.SequenceEqual(other._genericArguments) &&
            _callbackParameterTypes.SequenceEqual(other._callbackParameterTypes);
    }

    public override bool Equals(object? obj) => obj is BindingAliasTargetKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(PrimitiveKind);
        hash.Add(NativeIdentity, StringComparer.Ordinal);
        hash.Add(GenericParameterName, StringComparer.Ordinal);
        hash.Add(IsCallback);
        hash.Add(CallbackReturnType);
        foreach (var argument in _genericArguments)
        {
            hash.Add(argument);
        }

        foreach (var argument in _callbackParameterTypes)
        {
            hash.Add(argument);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(BindingAliasTargetKey? left, BindingAliasTargetKey? right) => Equals(left, right);

    public static bool operator !=(BindingAliasTargetKey? left, BindingAliasTargetKey? right) => !Equals(left, right);
}
