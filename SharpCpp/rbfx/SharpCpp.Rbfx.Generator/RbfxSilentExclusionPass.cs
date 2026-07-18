using SharpCpp.Model.Ast;

namespace SharpCpp.Rbfx.Generator;

public static class RbfxSilentExclusionPass
{
    private static readonly HashSet<string> BlockedHeaderIncludePaths = new(StringComparer.Ordinal)
    {
        "Container/Ptr.h",
        "Container/Str.h",
        "Container/Hash.h",
        "Container/FlagSet.h",
        "Container/IndexAllocator.h",
        "Container/Functors.h",
        "Container/Allocator.h",
        "Core/Assert.h",
        "Core/Format.h",
        "Core/IteratorRange.h",
        "Core/Macros.h",
        "Core/Main.h",
        "Core/NonCopyable.h",
        "Core/TypeTrait.h",
        "Core/StringUtils.h",
    };

    public static int Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var count = 0;
        foreach (var record in compilation.Records)
        {
            if (record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
            {
                continue;
            }

            if (TryGetBlockedHeaderSilentExclusionReason(record, inheritFromAncestor: false, out var reason))
            {
                if (SetSilentExclusionReasonIfChanged(record, reason))
                {
                    count++;
                }
            }
            else if (TryGetSilentExclusionReason(record.CppQualifiedName, out reason))
            {
                if (IsExplicitlyConfiguredRecord(record) && IsOutsideUrho3DNamespace(record.CppQualifiedName))
                {
                    continue;
                }

                if (SetSilentExclusionReasonIfChanged(record, reason))
                {
                    count++;
                }
            }
        }

        foreach (var enumBinding in compilation.Enums)
        {
            if (TryGetBlockedHeaderSilentExclusionReason(enumBinding, inheritFromAncestor: false, out var reason) ||
                TryGetSilentExclusionReason(enumBinding.CppQualifiedName, out reason))
            {
                if (SetSilentExclusionReasonIfChanged(enumBinding, reason))
                {
                    count++;
                }
            }
        }

        var headerBlockedTypes = CreateHeaderBlockedTypeSet(compilation);

        foreach (var function in compilation.Functions)
        {
            if (TryGetBlockedHeaderSilentExclusionReason(function, inheritFromAncestor: true, out var reason))
            {
                if (SetSilentExclusionReasonIfChanged(function, reason))
                {
                    count++;
                }
            }
            else if (ShouldSilentlyExcludeFunction(function, out reason))
            {
                if (SetSilentExclusionReasonIfChanged(function, reason))
                {
                    count++;
                }
            }
            else if (TryGetDependentFunctionIgnoreReason(function, headerBlockedTypes, out reason))
            {
                SetIgnoreReasonIfMissing(function, reason);
            }
        }

        foreach (var field in compilation.Descendants<FieldBinding>())
        {
            if (TryGetBlockedHeaderSilentExclusionReason(field, inheritFromAncestor: true, out var reason))
            {
                if (SetSilentExclusionReasonIfChanged(field, reason))
                {
                    count++;
                }
            }
            else if (TryGetDependentFieldIgnoreReason(field, headerBlockedTypes, out reason))
            {
                SetIgnoreReasonIfMissing(field, reason);
            }
        }

        DropIgnoredBases(compilation);
        PruneAliasesTargetingIgnoredTypes(compilation, headerBlockedTypes);

        return count;
    }

    private static bool SetSilentExclusionReasonIfChanged(BindingNode node, string reason)
    {
        if (node.IsSilentlyExcluded() && string.Equals(node.GetIgnoreReason(), reason, StringComparison.Ordinal))
        {
            return false;
        }

        node.SetSilentExclusionReason(reason, overwriteExisting: true);
        return true;
    }

    private static bool SetIgnoreReasonIfMissing(BindingNode node, string reason)
    {
        if (node.IsIgnored())
        {
            return false;
        }

        node.SetIgnoreReason(reason, overwriteExisting: false);
        return true;
    }

    private sealed class HeaderBlockedTypeSet
    {
        public HashSet<string> RecordIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> EnumIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> CppTypeNames { get; } = new(StringComparer.Ordinal);
    }

    private static HeaderBlockedTypeSet CreateHeaderBlockedTypeSet(BindingCompilation compilation)
    {
        var result = new HeaderBlockedTypeSet();
        foreach (var record in compilation.Records.Where(static record => IsDefinedInBlockedHeader(record, inheritFromAncestor: false)))
        {
            result.RecordIds.Add(record.SymbolId);
            AddBlockedCppTypeName(result, record.CppQualifiedName);
            AddBlockedCppTypeName(result, record.NativeName);
        }

        foreach (var enumBinding in compilation.Enums.Where(static enumBinding => IsDefinedInBlockedHeader(enumBinding, inheritFromAncestor: false)))
        {
            result.EnumIds.Add(enumBinding.SymbolId);
            AddBlockedCppTypeName(result, enumBinding.CppQualifiedName);
            AddBlockedCppTypeName(result, enumBinding.NativeName);
        }

        return result;
    }

    private static void AddBlockedCppTypeName(HeaderBlockedTypeSet set, string cppTypeName)
    {
        var normalized = NormalizeReferencedCppTypeName(cppTypeName);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            set.CppTypeNames.Add(normalized);
        }
    }

    private static bool TryGetBlockedHeaderSilentExclusionReason(BindingNode node, bool inheritFromAncestor, out string reason)
    {
        var headerIncludePath = ResolveEffectiveHeaderIncludePath(node, inheritFromAncestor);
        var canonicalHeader = CanonicalizeIncludePath(headerIncludePath);
        if (canonicalHeader is not null && BlockedHeaderIncludePaths.Contains(canonicalHeader))
        {
            reason = $"declaration is defined in rbfx SDK header '{canonicalHeader}' that is provided by custom marshaller/runtime support";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool IsDefinedInBlockedHeader(BindingNode node, bool inheritFromAncestor)
    {
        var canonicalHeader = CanonicalizeIncludePath(ResolveEffectiveHeaderIncludePath(node, inheritFromAncestor));
        return canonicalHeader is not null && BlockedHeaderIncludePaths.Contains(canonicalHeader);
    }

    private static string? ResolveEffectiveHeaderIncludePath(BindingNode node, bool inheritFromAncestor)
    {
        if (!string.IsNullOrWhiteSpace(node.HeaderIncludePath))
        {
            return node.HeaderIncludePath;
        }

        if (!inheritFromAncestor)
        {
            return null;
        }

        foreach (var ancestor in node.Ancestors())
        {
            if (!string.IsNullOrWhiteSpace(ancestor.HeaderIncludePath))
            {
                return ancestor.HeaderIncludePath;
            }
        }

        return null;
    }

    private static string? CanonicalizeIncludePath(string? includePath)
    {
        if (string.IsNullOrWhiteSpace(includePath))
        {
            return null;
        }

        var path = includePath.Replace('\\', '/').Trim();
        while (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        var urho3DSegmentIndex = path.LastIndexOf("/Urho3D/", StringComparison.Ordinal);
        if (urho3DSegmentIndex >= 0)
        {
            path = path[(urho3DSegmentIndex + "/Urho3D/".Length)..];
        }

        const string urho3DPrefix = "Urho3D/";
        if (path.StartsWith(urho3DPrefix, StringComparison.Ordinal))
        {
            path = path[urho3DPrefix.Length..];
        }

        return path;
    }

    private static void DropIgnoredBases(BindingCompilation compilation)
    {
        var recordsById = compilation.Records
            .GroupBy(static record => record.SymbolId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.Ordinal);
        foreach (var record in compilation.Records)
        {
            if (record.IsIgnored())
            {
                continue;
            }

            foreach (var baseBinding in record.Bases.ToArray())
            {
                if (!recordsById.TryGetValue(baseBinding.RecordId, out var baseRecord) ||
                    !baseRecord.IsIgnored())
                {
                    continue;
                }

                record.SetWarning($"dropping base '{baseBinding.CppQualifiedName}' from record '{record.CppQualifiedName}' because the base record is not available for a coherent managed hierarchy; the derived record will still be generated without that base");
                record.Bases.Remove(baseBinding);

                if (string.Equals(record.ManagedBaseTypeName, baseBinding.ManagedName, StringComparison.Ordinal))
                {
                    record.ManagedBaseTypeName = PickRemainingManagedBaseTypeName(record, recordsById);
                }

                foreach (var projectedMethod in record.Members.OfType<FunctionBinding>())
                {
                    if (projectedMethod.SymbolId.StartsWith("projected-secondary:", StringComparison.Ordinal) &&
                        string.Equals(projectedMethod.NativeDispatchRecordCppTypeName, baseBinding.CppQualifiedName, StringComparison.Ordinal))
                    {
                        var reason = $"projected inherited member '{projectedMethod.CppQualifiedName}' is sourced from ignored rbfx implementation base '{baseRecord.CppQualifiedName}'";
                        SetIgnoreReasonIfMissing(projectedMethod, reason);
                    }
                }
            }
        }
    }

    private static string? PickRemainingManagedBaseTypeName(RecordBinding record, IReadOnlyDictionary<string, RecordBinding> recordsById)
    {
        foreach (var remainingBase in record.Bases)
        {
            if (!remainingBase.IsVirtual &&
                recordsById.TryGetValue(remainingBase.RecordId, out var remainingBaseRecord) &&
                !remainingBaseRecord.IsIgnored())
            {
                return remainingBase.ManagedName;
            }
        }

        return null;
    }

    private static void PruneAliasesTargetingIgnoredTypes(BindingCompilation compilation, HeaderBlockedTypeSet headerBlockedTypes)
    {
        foreach (var alias in compilation.TypeAliases.ToArray())
        {
            if (ReferencesIgnoredType(alias.TargetType, headerBlockedTypes))
            {
                compilation.TypeAliases.Remove(alias);
            }
        }
    }

    private static bool ReferencesIgnoredType(BindingType type, HeaderBlockedTypeSet headerBlockedTypes)
    {
        return (type.RecordId is not null && headerBlockedTypes.RecordIds.Contains(type.RecordId)) ||
            (type.EnumId is not null && headerBlockedTypes.EnumIds.Contains(type.EnumId)) ||
            headerBlockedTypes.CppTypeNames.Contains(NormalizeReferencedCppTypeName(type.CppTypeName)) ||
            type.GenericArguments.Any(argument => ReferencesIgnoredType(argument, headerBlockedTypes));
    }

    private static string NormalizeReferencedCppTypeName(string cppTypeName)
    {
        cppTypeName = NormalizeCppTypeName(cppTypeName);
        foreach (var prefix in new[] { "class ", "struct ", "enum " })
        {
            while (cppTypeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                cppTypeName = cppTypeName[prefix.Length..].TrimStart();
            }
        }

        return cppTypeName;
    }

    private static bool IsExplicitlyConfiguredRecord(RecordBinding record)
    {
        return !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) ||
            !string.IsNullOrWhiteSpace(record.ManagedTypeFormat) ||
            !string.IsNullOrWhiteSpace(record.HandleMarshallerTypeName) ||
            record.Members.OfType<FunctionBinding>().Any(static member => member.HasMetadata(MetadataKeys.MarshallerExposedMethod));
    }

    private static bool IsOutsideUrho3DNamespace(string cppQualifiedName)
    {
        var parts = cppQualifiedName.Split("::", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length < 2 || !string.Equals(parts[0], "Urho3D", StringComparison.Ordinal);
    }

    private static bool ShouldSilentlyExcludeFunction(FunctionBinding function, out string reason)
    {
        if (TryGetFlagSetOperatorSilentExclusionReason(function, out reason))
        {
            return true;
        }

        if (function.Parent is RecordBinding parentRecord)
        {
            return TryGetSmartPointerSilentExclusionReason(parentRecord.NativeName, function.CppQualifiedName, out reason) ||
                TryGetSmartPointerSilentExclusionReason(parentRecord.CppQualifiedName, function.CppQualifiedName, out reason) ||
                TryGetSmartPointerDeductionGuideSilentExclusionReason(function, out reason);
        }

        if (TryGetSilentExclusionReason(function.CppQualifiedName, out reason) ||
            TryGetSmartPointerSilentExclusionReason(function.NativeName, function.CppQualifiedName, out reason) ||
            TryGetSmartPointerDeductionGuideSilentExclusionReason(function, out reason))
        {
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetDependentFunctionIgnoreReason(FunctionBinding function, HeaderBlockedTypeSet headerBlockedTypes, out string reason)
    {
        if (ReferencesIgnoredType(function.ReturnType, headerBlockedTypes) ||
            function.Parameters.Any(parameter => ReferencesIgnoredType(parameter.Type, headerBlockedTypes)))
        {
            reason = $"function signature references ignored rbfx implementation type: '{function.CppQualifiedName}'";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetDependentFieldIgnoreReason(FieldBinding field, HeaderBlockedTypeSet headerBlockedTypes, out string reason)
    {
        if (ReferencesIgnoredType(field.Type, headerBlockedTypes))
        {
            reason = $"field type references ignored rbfx implementation type: '{field.ManagedName}'";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetSmartPointerDeductionGuideSilentExclusionReason(FunctionBinding function, out string reason)
    {
        var declarationText = function.GetMetadata<string>(MetadataKeys.DeclarationDisplayText) ?? string.Empty;
        if ((declarationText.Contains(" SharedPtr(", StringComparison.Ordinal) && declarationText.Contains(" -> SharedPtr<", StringComparison.Ordinal)) ||
            (declarationText.Contains(" WeakPtr(", StringComparison.Ordinal) && declarationText.Contains(" -> WeakPtr<", StringComparison.Ordinal)))
        {
            reason = $"rbfx smart pointer deduction guide '{function.CppQualifiedName}' is provided by custom marshaller support";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetSilentExclusionReason(string cppQualifiedName, out string reason)
    {
        if (string.IsNullOrWhiteSpace(cppQualifiedName))
        {
            reason = string.Empty;
            return false;
        }

        var parts = cppQualifiedName.Split("::", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            reason = $"declaration '{cppQualifiedName}' is outside the Urho3D namespace";
            return true;
        }

        if (!string.Equals(parts[0], "Urho3D", StringComparison.Ordinal))
        {
            reason = $"declaration namespace root '{parts[0]}' is outside the Urho3D namespace";
            return true;
        }

        if (TryGetSmartPointerSilentExclusionReason(parts[^1], cppQualifiedName, out reason))
        {
            return true;
        }

        if (parts.Skip(1).Take(parts.Length - 2).Any(static part =>
                string.Equals(part, "detail", StringComparison.Ordinal) ||
                string.Equals(part, "Detail", StringComparison.Ordinal)))
        {
            reason = $"declaration is in an internal rbfx detail namespace: '{cppQualifiedName}'";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetSmartPointerSilentExclusionReason(string name, string cppQualifiedName, out string reason)
    {
        if (IsSmartPointerWrapperName(name))
        {
            reason = $"rbfx smart pointer wrapper '{cppQualifiedName}' is provided by custom marshaller support";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool IsSmartPointerWrapperName(string name)
    {
        var namespaceDelimiterIndex = name.LastIndexOf("::", StringComparison.Ordinal);
        if (namespaceDelimiterIndex >= 0)
        {
            name = name[(namespaceDelimiterIndex + 2)..];
        }

        var genericDelimiterIndex = name.IndexOf('<', StringComparison.Ordinal);
        if (genericDelimiterIndex >= 0)
        {
            name = name[..genericDelimiterIndex];
        }

        return string.Equals(name, "SharedPtr", StringComparison.Ordinal) ||
            string.Equals(name, "WeakPtr", StringComparison.Ordinal);
    }

    private static bool TryGetFlagSetOperatorSilentExclusionReason(FunctionBinding function, out string reason)
    {
        var declarationText = function.GetMetadata<string>(MetadataKeys.DeclarationDisplayText) ?? string.Empty;
        if (!IsFlagSetOperator(function, declarationText))
        {
            reason = string.Empty;
            return false;
        }

        if (TryGetFlagSetArgument(declarationText, out _))
        {
            reason = $"rbfx FlagSet operator '{function.CppQualifiedName}' is lowered to the enum managed surface";
            return true;
        }

        if (!TryGetFlagSetArgument(function.ReturnType.CppTypeName, out var enumType))
        {
            reason = string.Empty;
            return false;
        }

        foreach (var parameter in function.Parameters)
        {
            if (!IsSameFlagSetFamilyType(parameter.CppTypeName, enumType) &&
                !IsSameFlagSetFamilyType(parameter.Type.CppTypeName, enumType))
            {
                reason = string.Empty;
                return false;
            }
        }

        reason = $"rbfx FlagSet operator '{function.CppQualifiedName}' is lowered to the enum managed surface";
        return true;
    }

    private static bool IsFlagSetOperator(FunctionBinding function, string declarationText)
    {
        var token = function.OperatorToken;
        if (string.IsNullOrWhiteSpace(token) && function.NativeName.StartsWith("operator", StringComparison.Ordinal))
        {
            token = function.NativeName["operator".Length..];
        }

        return token is "&" or "^" or "|" or "~" ||
            declarationText.Contains(" operator&(", StringComparison.Ordinal) ||
            declarationText.Contains(" operator^(", StringComparison.Ordinal) ||
            declarationText.Contains(" operator|(", StringComparison.Ordinal) ||
            declarationText.Contains(" operator~(", StringComparison.Ordinal);
    }

    private static bool IsSameFlagSetFamilyType(string cppTypeName, string enumType)
    {
        return TryGetFlagSetArgument(cppTypeName, out var flagSetArgument)
            ? string.Equals(flagSetArgument, enumType, StringComparison.Ordinal)
            : string.Equals(NormalizeCppTypeName(cppTypeName), enumType, StringComparison.Ordinal);
    }

    private static bool TryGetFlagSetArgument(string cppTypeName, out string enumType)
    {
        cppTypeName = NormalizeCppTypeName(cppTypeName);
        var prefixIndex = cppTypeName.IndexOf("FlagSet<", StringComparison.Ordinal);
        if (prefixIndex < 0)
        {
            enumType = string.Empty;
            return false;
        }

        var argumentStart = prefixIndex + "FlagSet<".Length;
        var argumentEnd = cppTypeName.IndexOf('>', argumentStart);
        if (argumentEnd < 0)
        {
            enumType = string.Empty;
            return false;
        }

        enumType = NormalizeCppTypeName(cppTypeName[argumentStart..argumentEnd]);
        return enumType.Length > 0;
    }

    private static string NormalizeCppTypeName(string cppTypeName)
    {
        cppTypeName = cppTypeName.Trim();
        while (cppTypeName.StartsWith("const ", StringComparison.Ordinal))
        {
            cppTypeName = cppTypeName["const ".Length..].TrimStart();
        }

        return cppTypeName.TrimEnd('&', '*').Trim();
    }
}
