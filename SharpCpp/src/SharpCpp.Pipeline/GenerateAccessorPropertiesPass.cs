using SharpCpp.Model.Ast;

namespace SharpCpp.Pipeline;

public sealed class AccessorPropertyPattern
{
    public AccessorPropertyPattern(
        string getterPrefix,
        string setterPrefix,
        string? propertyNamePrefix = null,
        bool allowGetterOnly = true)
    {
        GetterPrefix = ValidatePrefix(getterPrefix, nameof(getterPrefix));
        SetterPrefix = ValidatePrefix(setterPrefix, nameof(setterPrefix));
        PropertyNamePrefix = propertyNamePrefix ?? string.Empty;
        AllowGetterOnly = allowGetterOnly;
    }

    public string GetterPrefix { get; }

    public string SetterPrefix { get; }

    public string PropertyNamePrefix { get; }

    public bool AllowGetterOnly { get; }

    public string GetPropertyName(string stem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stem);
        return string.IsNullOrEmpty(PropertyNamePrefix) ? stem : $"{PropertyNamePrefix}{stem}";
    }

    internal bool TryGetGetterStem(FunctionBinding function, out string stem)
    {
        ArgumentNullException.ThrowIfNull(function);
        return TryGetStem(function.ManagedName, GetterPrefix, out stem);
    }

    internal bool TryGetSetterStem(FunctionBinding function, out string stem)
    {
        ArgumentNullException.ThrowIfNull(function);
        return TryGetStem(function.ManagedName, SetterPrefix, out stem);
    }

    private static string ValidatePrefix(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Accessor prefixes must be non-empty.", parameterName)
            : value;
    }

    private static bool TryGetStem(string managedName, string prefix, out string stem)
    {
        stem = string.Empty;

        if (string.IsNullOrWhiteSpace(managedName) ||
            !managedName.StartsWith(prefix, StringComparison.Ordinal) ||
            managedName.Length == prefix.Length)
        {
            return false;
        }

        stem = managedName[prefix.Length..];
        return !string.IsNullOrWhiteSpace(stem);
    }
}

public static class GenerateAccessorPropertiesPass
{
    private static readonly AccessorPropertyPattern[] DefaultPatterns =
    [
        new("Get", "Set")
    ];

    public static int Apply(
        BindingCompilation compilation,
        IEnumerable<AccessorPropertyPattern>? patterns = null,
        Predicate<RecordBinding>? recordFilter = null,
        Predicate<FunctionBinding>? getterFilter = null,
        Predicate<FunctionBinding>? setterFilter = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var resolvedPatterns = (patterns ?? DefaultPatterns).ToArray();
        if (resolvedPatterns.Length == 0)
        {
            throw new ArgumentException("At least one accessor property pattern is required.", nameof(patterns));
        }

        var promotedCount = 0;
        foreach (var record in compilation.Records)
        {
            if (recordFilter is not null && !recordFilter(record))
            {
                continue;
            }

            promotedCount += PromoteRecordAccessors(record, resolvedPatterns, getterFilter, setterFilter);
        }

        return promotedCount;
    }

    private static int PromoteRecordAccessors(
        RecordBinding record,
        IReadOnlyList<AccessorPropertyPattern> patterns,
        Predicate<FunctionBinding>? getterFilter,
        Predicate<FunctionBinding>? setterFilter)
    {
        var promotedCount = 0;
        var functions = record.Members.OfType<FunctionBinding>().ToArray();
        var claimedFunctions = new HashSet<FunctionBinding>();

        foreach (var pattern in patterns)
        {
            var gettersByStem = GroupGetterCandidates(functions, claimedFunctions, pattern, getterFilter);
            var settersByStem = GroupSetterCandidates(functions, claimedFunctions, pattern, setterFilter);
            var visitedStems = new HashSet<string>(StringComparer.Ordinal);

            foreach (var function in functions)
            {
                if (claimedFunctions.Contains(function) || !TryGetGetterCandidateStem(function, pattern, getterFilter, out var stem))
                {
                    continue;
                }

                if (!visitedStems.Add(stem) || !gettersByStem.TryGetValue(stem, out var getters))
                {
                    continue;
                }

                var propertyName = pattern.GetPropertyName(stem);
                if (getters.Count != 1)
                {
                    continue;
                }

                var getter = getters[0];
                settersByStem.TryGetValue(stem, out var setters);
                setters ??= [];
                var compatibleSetters = setters.Where(setter => CanPromote(propertyName, getter, setter)).ToArray();
                if (compatibleSetters.Length > 1 || (compatibleSetters.Length == 0 && setters.Count > 0))
                {
                    continue;
                }

                var setter = compatibleSetters.Length == 1 ? compatibleSetters[0] : null;
                if (setter is null && !pattern.AllowGetterOnly)
                {
                    continue;
                }

                if (!CanPromote(propertyName, getter, setter) || HasManagedNameCollision(record, propertyName, getter, setter))
                {
                    continue;
                }

                getter.SetMetadata(MetadataKeys.AccessorProperty, new AccessorPropertyAnnotation(propertyName, AccessorPropertyKind.Getter));
                claimedFunctions.Add(getter);

                if (setter is not null)
                {
                    setter.SetMetadata(MetadataKeys.AccessorProperty, new AccessorPropertyAnnotation(propertyName, AccessorPropertyKind.Setter));
                    claimedFunctions.Add(setter);
                }

                promotedCount++;
            }
        }

        return promotedCount;
    }

    private static Dictionary<string, List<FunctionBinding>> GroupGetterCandidates(
        IEnumerable<FunctionBinding> functions,
        ISet<FunctionBinding> claimedFunctions,
        AccessorPropertyPattern pattern,
        Predicate<FunctionBinding>? getterFilter)
    {
        var groups = new Dictionary<string, List<FunctionBinding>>(StringComparer.Ordinal);

        foreach (var function in functions)
        {
            if (claimedFunctions.Contains(function) || !TryGetGetterCandidateStem(function, pattern, getterFilter, out var stem))
            {
                continue;
            }

            if (!groups.TryGetValue(stem, out var group))
            {
                group = [];
                groups.Add(stem, group);
            }

            group.Add(function);
        }

        return groups;
    }

    private static Dictionary<string, List<FunctionBinding>> GroupSetterCandidates(
        IEnumerable<FunctionBinding> functions,
        ISet<FunctionBinding> claimedFunctions,
        AccessorPropertyPattern pattern,
        Predicate<FunctionBinding>? setterFilter)
    {
        var groups = new Dictionary<string, List<FunctionBinding>>(StringComparer.Ordinal);

        foreach (var function in functions)
        {
            if (claimedFunctions.Contains(function) || !TryGetSetterCandidateStem(function, pattern, setterFilter, out var stem))
            {
                continue;
            }

            if (!groups.TryGetValue(stem, out var group))
            {
                group = [];
                groups.Add(stem, group);
            }

            group.Add(function);
        }

        return groups;
    }

    private static bool TryGetGetterCandidateStem(
        FunctionBinding function,
        AccessorPropertyPattern pattern,
        Predicate<FunctionBinding>? getterFilter,
        out string stem)
    {
        stem = string.Empty;
        return IsGetterCandidate(function) &&
            (getterFilter?.Invoke(function) ?? true) &&
            pattern.TryGetGetterStem(function, out stem);
    }

    private static bool TryGetSetterCandidateStem(
        FunctionBinding function,
        AccessorPropertyPattern pattern,
        Predicate<FunctionBinding>? setterFilter,
        out string stem)
    {
        stem = string.Empty;
        return IsSetterCandidate(function) &&
            (setterFilter?.Invoke(function) ?? true) &&
            pattern.TryGetSetterStem(function, out stem);
    }

    private static bool IsGetterCandidate(FunctionBinding function)
    {
        return !function.IsIgnored() &&
            !function.IsConstructor &&
            !function.IsStatic &&
            !function.IsVirtual &&
            !function.IsPureVirtual &&
            !function.IsGeneric &&
            string.IsNullOrWhiteSpace(function.NativeDispatchRecordCppTypeName) &&
            string.IsNullOrWhiteSpace(function.OperatorToken) &&
            !function.IsConversionOperator &&
            function.Parameters.Count == 0 &&
            function.ReturnType.PrimitiveKind != BindingPrimitiveKind.Void;
    }

    private static bool IsSetterCandidate(FunctionBinding function)
    {
        return !function.IsIgnored() &&
            !function.IsConstructor &&
            !function.IsStatic &&
            !function.IsVirtual &&
            !function.IsPureVirtual &&
            !function.IsGeneric &&
            string.IsNullOrWhiteSpace(function.NativeDispatchRecordCppTypeName) &&
            string.IsNullOrWhiteSpace(function.OperatorToken) &&
            !function.IsConversionOperator &&
            function.Parameters.Count == 1 &&
            function.Parameters[0].Direction == BindingParameterDirection.In &&
            function.ReturnType.PrimitiveKind == BindingPrimitiveKind.Void;
    }

    private static bool CanPromote(string propertyName, FunctionBinding getter, FunctionBinding? setter)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        if (!IsGetterCandidate(getter))
        {
            return false;
        }

        if (setter is null)
        {
            return true;
        }

        return IsSetterCandidate(setter) &&
            string.Equals(getter.ReturnType.UnaliasedManagedTypeName, setter.Parameters[0].Type.UnaliasedManagedTypeName, StringComparison.Ordinal);
    }

    private static bool HasManagedNameCollision(
        RecordBinding record,
        string propertyName,
        FunctionBinding getter,
        FunctionBinding? setter)
    {
        foreach (var member in record.Members)
        {
            if (ReferenceEquals(member, getter) || ReferenceEquals(member, setter))
            {
                continue;
            }

            var managedName = member switch
            {
                BindingNamedNode namedNode => namedNode.ManagedName,
                BindingNamedContainerNode namedContainerNode => namedContainerNode.ManagedName,
                _ => null
            };
            if (string.Equals(managedName, propertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
