using SharpCpp.Model.Ast;

namespace SharpCpp.Pipeline;

public static class DegradeUnsupportedSurfacePass
{
    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var changed = true;
        while (changed)
        {
            changed = false;
            var recordsById = compilation.Records
                .GroupBy(static record => record.SymbolId, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.Ordinal);
            var enumsById = compilation.Enums
                .GroupBy(static @enum => @enum.SymbolId, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.Ordinal);
            var allowedNamespaceRoots = compilation.NamespaceMappings.Keys.ToHashSet(StringComparer.Ordinal);

            foreach (var duplicateGroup in compilation.Functions
                         .Where(static function => function.Parent is not RecordBinding && !function.IsIgnored() && function.Parent?.IsIgnored() != true)
                         .GroupBy(GetCallableSignatureKey, StringComparer.Ordinal)
                         .Where(static group => group.Count() > 1))
            {
                foreach (var function in duplicateGroup)
                {
                    function.SetIgnoreReason($"callable signature '{duplicateGroup.Key}' is declared multiple times in this bootstrap surface");
                    changed = true;
                }
            }

            foreach (var duplicateGroup in compilation.Functions
                          .Where(static function => function.Parent is not RecordBinding && !function.IsIgnored() && function.Parent?.IsIgnored() != true)
                          .GroupBy(GetManagedCallableSignatureKey, StringComparer.Ordinal)
                          .Where(static group => group.Count() > 1))
            {
                foreach (var function in duplicateGroup)
                {
                    function.SetIgnoreReason($"managed callable signature '{duplicateGroup.Key}' is declared multiple times in this bootstrap surface");
                    changed = true;
                }
            }

            foreach (var duplicateDelegateGroup in EnumerateCallbackDelegateParameters(compilation)
                         .GroupBy(GetCallbackDelegatePolicyTypeName, StringComparer.Ordinal)
                         .Where(static group => group.Select(parameter => GetCallbackSignatureKey(parameter.Type)).Distinct(StringComparer.Ordinal).Count() > 1))
            {
                foreach (var parameter in duplicateDelegateGroup)
                {
                    if (parameter.Parent is FunctionBinding function && !function.IsIgnored())
                    {
                        function.SetIgnoreReason($"callback delegate type '{duplicateDelegateGroup.Key}' is requested with incompatible callback signatures");
                        changed = true;
                    }
                }
            }

            foreach (var record in compilation.Records)
            {
                var duplicateGroups = record.Members.OfType<FunctionBinding>()
                    .Where(static function => !function.IsIgnored())
                    .GroupBy(GetManagedPublicCallableSignatureKey, StringComparer.Ordinal)
                    .Where(static group => group.Count() > 1)
                    .ToArray();

                foreach (var duplicateGroup in duplicateGroups)
                {
                    var kept = duplicateGroup
                        .OrderBy(static function => function.IsConstMethod)
                        .ThenBy(static function => function.SymbolId, StringComparer.Ordinal)
                        .First();

                    foreach (var function in duplicateGroup.Where(function => !ReferenceEquals(function, kept)))
                    {
                        function.SetIgnoreReason($"managed callable signature '{duplicateGroup.Key}' is declared multiple times in this bootstrap surface");
                        changed = true;
                    }
                }
            }

            foreach (var record in compilation.Records)
            {
                if (record.IsIgnored())
                {
                    continue;
                }

                if (record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
                {
                    continue;
                }

                if (!IsMarshallerBackedWrapperSurface(record) &&
                    TryGetNamespaceExclusionReason(record.CppQualifiedName, allowedNamespaceRoots, out var namespaceReason))
                {
                    record.SetIgnoreReason(namespaceReason);
                    changed = true;
                    continue;
                }

                if (record.GetMetadata<bool>(MetadataKeys.ForwardDeclaration) &&
                    !record.GetMetadata<bool>(MetadataKeys.OpaqueNativeHandle))
                {
                    record.SetIgnoreReason("record is only forward-declared; wrapper materialization requires a complete definition");
                    changed = true;
                    continue;
                }

                if (TryGetUnsupportedRecordReason(record, compilation, recordsById, out var recordReason))
                {
                    record.SetIgnoreReason(recordReason);
                    changed = true;
                }
            }

            foreach (var @enum in compilation.Enums)
            {
                if (@enum.IsIgnored())
                {
                    continue;
                }

                if (@enum.GetMetadata<bool>(MetadataKeys.SuppressEmission))
                {
                    continue;
                }

                if (TryGetNamespaceExclusionReason(@enum.CppQualifiedName, allowedNamespaceRoots, out var namespaceReason))
                {
                    @enum.SetIgnoreReason(namespaceReason);
                    changed = true;
                }
            }

            foreach (var function in compilation.Functions)
            {
                if (function.IsIgnored() || function.Parent?.IsIgnored() == true)
                {
                    continue;
                }

                if (!function.HasMetadata(MetadataKeys.MarshallerExposedMethod) &&
                    TryGetNamespaceExclusionReason(function.CppQualifiedName, allowedNamespaceRoots, out var namespaceReason))
                {
                    function.SetIgnoreReason(namespaceReason);
                    changed = true;
                    continue;
                }

                if (TryGetUnsupportedFunctionReason(function, recordsById, enumsById, compilation, out var reason))
                {
                    function.SetIgnoreReason(reason);
                    changed = true;
                }
            }

            foreach (var field in compilation.Descendants<FieldBinding>())
            {
                if (field.IsIgnored() || field.Parent?.IsIgnored() == true)
                {
                    continue;
                }

                if (TryGetUnsupportedFieldReason(field, recordsById, enumsById, out var reason))
                {
                    field.SetIgnoreReason(reason);
                    changed = true;
                }
            }
        }
    }

    private static bool TryGetUnsupportedFunctionReason(
        FunctionBinding function,
        IReadOnlyDictionary<string, RecordBinding> recordsById,
        IReadOnlyDictionary<string, EnumBinding> enumsById,
        BindingCompilation compilation,
        out string reason)
    {
        var functionAllowsOpenGenericTypes = function.IsGeneric || (function.Parent as RecordBinding)?.IsGeneric == true;

        if (!functionAllowsOpenGenericTypes && ContainsOpenGenericParameter(function.ReturnType))
        {
            reason = $"return type is unsupported in this bootstrap surface: open generic parameter usage is not renderer-ready in a non-generic callable";
            return true;
        }

        if (function.ReturnType.IsCallback)
        {
            reason = "callback return types are not yet supported in this bootstrap surface";
            return true;
        }

        if (TryGetUnsupportedTypeReason(function.ReturnType, function.CppReturnTypeName, recordsById, enumsById, disallowNonPointerHandles: false, out reason))
        {
            reason = $"return type is unsupported in this bootstrap surface: {reason}";
            return true;
        }

        foreach (var parameter in function.Parameters)
        {
            if (!functionAllowsOpenGenericTypes && parameter.Type.ContainsGenericParameters)
            {
                reason = $"parameter '{parameter.ManagedName}' is unsupported in this bootstrap surface: open generic parameter usage is not renderer-ready in a non-generic callable";
                return true;
            }

            if (parameter.Type.IsCallback &&
                (parameter.Direction is not BindingParameterDirection.In || parameter.AbiKind is not BindingParameterAbiKind.Value))
            {
                reason = $"parameter '{parameter.ManagedName}' is unsupported in this bootstrap surface: callback parameters must be passed as in-value parameters";
                return true;
            }

            if (parameter.UseCallbackDelegate && !parameter.Type.IsCallback)
            {
                reason = $"parameter '{parameter.ManagedName}' is unsupported in this bootstrap surface: [[sharpcpp::delegate]] can only be used on callback parameters";
                return true;
            }

            if (parameter.UseCallbackDelegate && !IsValidCallbackDelegatePolicy(parameter, out var delegatePolicyReason))
            {
                reason = $"parameter '{parameter.ManagedName}' is unsupported in this bootstrap surface: {delegatePolicyReason}";
                return true;
            }

            if (TryGetUnsupportedTypeReason(parameter.Type, parameter.CppTypeName, recordsById, enumsById, disallowNonPointerHandles: false, out reason))
            {
                reason = $"parameter '{parameter.ManagedName}' is unsupported in this bootstrap surface: {reason}";
                return true;
            }
        }

        if (function.IsGeneric &&
            !function.ReturnType.ContainsGenericParameters &&
            function.Parameters.All(static parameter => !parameter.Type.ContainsGenericParameters))
        {
            reason = $"generic function '{function.ManagedName}' does not expose its template parameters through supported ABI positions";
            return true;
        }

        if (!function.IsConstructor &&
            function.NativeName.StartsWith("operator", StringComparison.Ordinal) &&
            function.OperatorToken is null &&
            !function.IsConversionOperator)
        {
            reason = $"operator '{function.NativeName}' is not supported in this bootstrap surface";
            return true;
        }

        if (function.OperatorToken is not null && !IsSupportedOperatorShape(function))
        {
            reason = $"operator shape '{function.ManagedName}' is not supported in this bootstrap surface";
            return true;
        }

        if (function.Parent is RecordBinding owningRecord &&
            function.OperatorToken == "!=" &&
            !HasCompanionOperator(owningRecord, function, "=="))
        {
            reason = $"operator '{function.ManagedName}' does not have the companion comparison operator required by C#";
            return true;
        }

        if (function.Parent is RecordBinding owningRecord2 &&
            function.OperatorToken == "==" &&
            !IsSelfComparisonOperator(owningRecord2, function) &&
            !HasCompanionOperator(owningRecord2, function, "!="))
        {
            reason = $"operator '{function.ManagedName}' does not have the companion comparison operator required by C#";
            return true;
        }

        if (function.Parent is RecordBinding directorRecord &&
            directorRecord.IsDirector &&
            function.IsVirtual &&
            TryGetUnsupportedDirectorCallbackReason(function, out var callbackReason))
        {
            reason = $"director callback '{function.ManagedName}' is unsupported in this bootstrap surface: {callbackReason}";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetUnsupportedDirectorCallbackReason(FunctionBinding function, out string reason)
    {
        foreach (var parameter in function.Parameters)
        {
            if (parameter.Direction is not BindingParameterDirection.In || parameter.AbiKind is not BindingParameterAbiKind.Value)
            {
                reason = $"parameter '{parameter.ManagedName}' must be an in-value parameter";
                return true;
            }
        }

        if (function.ReturnType.Kind == BindingTypeKind.Handle &&
            (function.ReturnsOwnedHandle || function.ReturnType.Marshaller.CppIsRecordValue))
        {
            reason = "owned-handle returns are not yet supported";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetUnsupportedRecordReason(
        RecordBinding record,
        BindingCompilation compilation,
        IReadOnlyDictionary<string, RecordBinding> recordsById,
        out string reason)
    {
        if (record.IsDirector)
        {
            foreach (var method in record.Members.OfType<FunctionBinding>().Where(static method => method.IsPureVirtual && !method.IsIgnored()))
            {
                if (TryGetUnsupportedDirectorCallbackReason(method, out var callbackReason))
                {
                    reason = $"pure virtual director callback '{method.ManagedName}' is unsupported in this bootstrap surface: {callbackReason}";
                    return true;
                }
            }
        }

        // Records with no user-defined members are template utility types
        // (metafunctions like IndexInTuple) that shouldn't be wrapped.
        // Their only members would be the synthetic wrapper boilerplate (Handle,
        // constructors, P/Invoke) which is never useful without API methods.
        // Exception: records with a managed base type or type format are
        // configured by marshallers (e.g. std::string → StringBase).
        if (string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) &&
            (record.ManagedTypeFormat is null || record.ManagedTypeFormat == record.ManagedName) &&
            !record.GetMetadata<bool>(MetadataKeys.OpaqueNativeHandle) &&
            !record.Members.OfType<FunctionBinding>().Any(static m => !m.IsConstructor && !m.IsStatic && !m.HasMetadata(MetadataKeys.IgnoreReason)) &&
            !record.Members.OfType<FieldBinding>().Any() &&
            !record.Members.OfType<FunctionBinding>().Any(static m => m.IsConstructor && !m.HasMetadata(MetadataKeys.IgnoreReason)))
        {
            reason = $"record '{record.CppQualifiedName}' has no user-defined API surface (it is likely a template utility type)";
            return true;
        }

        var unavailableBases = new List<RecordBaseBinding>();

        foreach (var baseBinding in record.Bases)
        {
            if (!recordsById.TryGetValue(baseBinding.RecordId, out var baseRecord) ||
                baseRecord.IsIgnored() ||
                (baseRecord.GetMetadata<bool>(MetadataKeys.SuppressEmission) && !IsMarshallerBackedWrapperSurface(baseRecord)))
            {
                unavailableBases.Add(baseBinding);
            }
        }

        foreach (var baseBinding in unavailableBases)
        {
            record.SetWarning($"dropping base '{baseBinding.CppQualifiedName}' from record '{record.CppQualifiedName}' because the base record is not available for a coherent managed hierarchy; the derived record will still be generated without that base");
            record.Bases.Remove(baseBinding);

            if (string.Equals(record.ManagedBaseTypeName, baseBinding.ManagedName, StringComparison.Ordinal))
            {
                record.ManagedBaseTypeName = null;
                // Re-pick managed base from remaining non-virtual, non-ignored bases
                foreach (var remainingBase in record.Bases)
                {
                    if (!remainingBase.IsVirtual &&
                        recordsById.TryGetValue(remainingBase.RecordId, out var remainingBaseRecord) &&
                        !remainingBaseRecord.IsIgnored())
                    {
                        record.ManagedBaseTypeName = remainingBase.ManagedName;
                        break;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) &&
            !record.ManagedBaseTypeName.Contains('.', StringComparison.Ordinal) &&
            !compilation.Records.Any(candidate => !candidate.IsIgnored() && string.Equals(candidate.ManagedName, record.ManagedBaseTypeName, StringComparison.Ordinal)))
        {
            record.SetWarning($"dropping managed base type '{record.ManagedBaseTypeName}' from record '{record.ManagedName}' because the managed base type is not available for a coherent managed hierarchy; the derived record will still be generated without that base");
            record.ManagedBaseTypeName = null;
        }

        if (record.GetSecondaryBaseRecords(compilation).Any(static baseRecord =>
                baseRecord.IsIgnored() ||
                baseRecord.Members.OfType<FunctionBinding>().Any(static function =>
                    function.OperatorToken is not null ||
                    function.IsConversionOperator)))
        {
            reason = "secondary-base interfaces with comparison operators are not yet supported coherently";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetUnsupportedFieldReason(
        FieldBinding field,
        IReadOnlyDictionary<string, RecordBinding> recordsById,
        IReadOnlyDictionary<string, EnumBinding> enumsById,
        out string reason)
    {
        if ((field.Parent as RecordBinding)?.IsGeneric != true && field.Type.ContainsGenericParameters)
        {
            reason = "field type is unsupported in this bootstrap surface: open generic parameter usage is not renderer-ready in a non-generic record";
            return true;
        }

        if (field.IsReference)
        {
            reason = "reference fields are not yet supported coherently";
            return true;
        }

        if (field.Type.IsCallback)
        {
            reason = "callback fields are not yet supported in this bootstrap surface";
            return true;
        }

        if (TryGetUnsupportedTypeReason(field.Type, field.CppTypeName, recordsById, enumsById, disallowNonPointerHandles: true, out reason))
        {
            reason = $"field type is unsupported in this bootstrap surface: {reason}";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetUnsupportedTypeReason(
        BindingType type,
        string rawCppTypeName,
        IReadOnlyDictionary<string, RecordBinding> recordsById,
        IReadOnlyDictionary<string, EnumBinding> enumsById,
        bool disallowNonPointerHandles,
        out string reason)
    {
        if (type.IsCallback)
        {
            if (type.CallbackReturnType is null)
            {
                reason = $"callback type '{rawCppTypeName}' is missing return-type metadata";
                return true;
            }

            if (type.ManagedMarshaller is null && !IsGeneratedCallbackValueSupported(type.CallbackReturnType, recordsById))
            {
                reason = $"generated callback marshaller cannot convert callback return type '{type.CallbackReturnType.ManagedTypeName}'";
                return true;
            }

            if (TryGetUnsupportedTypeReason(type.CallbackReturnType, type.CallbackReturnType.CppTypeName, recordsById, enumsById, disallowNonPointerHandles: true, out var callbackReturnReason))
            {
                reason = $"callback return type is unsupported: {callbackReturnReason}";
                return true;
            }

            foreach (var parameterType in type.CallbackParameterTypes)
            {
                if (type.ManagedMarshaller is null && !IsGeneratedCallbackValueSupported(parameterType, recordsById))
                {
                    reason = $"generated callback marshaller cannot convert callback parameter type '{parameterType.ManagedTypeName}'";
                    return true;
                }

                if (TryGetUnsupportedTypeReason(parameterType, parameterType.CppTypeName, recordsById, enumsById, disallowNonPointerHandles: false, out var callbackParameterReason))
                {
                    reason = $"callback parameter type '{parameterType.ManagedTypeName}' is unsupported: {callbackParameterReason}";
                    return true;
                }
            }

            reason = string.Empty;
            return false;
        }

        if (type.Kind == BindingTypeKind.Handle &&
            !string.IsNullOrWhiteSpace(type.RecordId) &&
            recordsById.TryGetValue(type.RecordId, out var record) &&
            record.IsIgnored())
        {
            reason = $"record '{record.CppQualifiedName}' is ignored: {record.GetIgnoreReason() ?? "unsupported record surface"}";
            return true;
        }

        if (type.Kind == BindingTypeKind.Enum &&
            !string.IsNullOrWhiteSpace(type.EnumId) &&
            enumsById.TryGetValue(type.EnumId, out var enumBinding) &&
            enumBinding.IsIgnored())
        {
            reason = $"enum '{enumBinding.CppQualifiedName}' is ignored: {enumBinding.GetIgnoreReason() ?? "unsupported enum surface"}";
            return true;
        }

        if (type.Kind == BindingTypeKind.Enum &&
            (string.IsNullOrWhiteSpace(type.EnumId) || !enumsById.ContainsKey(type.EnumId)))
        {
            reason = $"external enum type '{rawCppTypeName}' is not owned by this compilation";
            return true;
        }

        if (type.Kind == BindingTypeKind.Handle &&
            !string.IsNullOrWhiteSpace(type.RecordId) &&
            !recordsById.ContainsKey(type.RecordId))
        {
            reason = $"external handle type '{rawCppTypeName}' is not owned by this compilation";
            return true;
        }

        if (type.Kind == BindingTypeKind.Primitive && type.PrimitiveKind == BindingPrimitiveKind.Unsupported)
        {
            if (string.Equals(type.ManagedTypeName, rawCppTypeName, StringComparison.Ordinal))
            {
                reason = $"configured marshaller-backed type '{rawCppTypeName}' is not yet supported";
                return true;
            }
        }

        foreach (var argument in GetGenericArgumentsForValidation(type))
        {
            if (TryGetUnsupportedTypeReason(argument, argument.CppTypeName, recordsById, enumsById, disallowNonPointerHandles: false, out var nestedReason))
            {
                if (argument.Kind == BindingTypeKind.Handle &&
                    argument.RecordId is not null &&
                    recordsById.TryGetValue(argument.RecordId, out var nestedRecord) &&
                    nestedRecord.IsIgnored() &&
                    !IsFatalIgnoredGenericArgumentRecord(nestedRecord))
                {
                    continue;
                }

                reason = $"generic argument '{argument.ManagedTypeName}' is unsupported: {nestedReason}";
                return true;
            }
        }

        if (type.HasUnresolvedManagedNamespaceSeparators)
        {
            reason = $"managed type name '{type.ManagedTypeName}' is not yet renderer-ready C#";
            return true;
        }

        if (HasRefStructGenericArgument(type))
        {
            reason = $"managed type name '{type.ManagedTypeName}' contains a ref struct generic argument which is not yet supported in this bootstrap surface";
            return true;
        }

        if (disallowNonPointerHandles && type.Kind == BindingTypeKind.Handle &&
            type.Marshaller.CppIsRecordValue)
        {
            if (!recordsById.TryGetValue(type.RecordId ?? string.Empty, out var wrapperRecord) ||
                !IsMarshallerBackedWrapperSurface(wrapperRecord))
            {
                reason = $"record value type '{rawCppTypeName}' is not yet supported";
                return true;
            }
        }

        if (type.GenericArguments.Count > 0 && !IsSupportedGenericHandleType(type, recordsById))
        {
            reason = $"template/container type '{rawCppTypeName}' is not yet supported";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static IEnumerable<ParameterBinding> EnumerateCallbackDelegateParameters(BindingCompilation compilation)
    {
        return compilation.Functions
            .Where(static function => !function.IsIgnored() && function.Parent?.IsIgnored() != true)
            .SelectMany(static function => function.Parameters)
            .Where(static parameter => parameter.UseCallbackDelegate && parameter.Type.IsCallback);
    }

    private static string GetCallbackDelegatePolicyTypeName(ParameterBinding parameter)
    {
        if (!string.IsNullOrWhiteSpace(parameter.CallbackDelegateTypeName))
        {
            return IsExternalCallbackDelegateTypeName(parameter.CallbackDelegateTypeName!)
                ? NormalizeExternalCallbackDelegateTypeName(parameter.CallbackDelegateTypeName!)
                : parameter.CallbackDelegateTypeName!;
        }

        return GetGeneratedCallbackDelegateName(parameter);
    }

    private static string GetGeneratedCallbackDelegateName(ParameterBinding parameter)
    {
        var function = parameter.Parent as FunctionBinding;
        var ownerName = function?.FindOwningRecord()?.ManagedName ?? "Global";
        var functionName = function?.ImportMethodName ?? function?.ManagedName ?? "Callback";
        return $"{SanitizeManagedToken(ownerName)}_{SanitizeManagedToken(functionName)}_{SanitizeManagedToken(parameter.ManagedName)}Callback";
    }

    private static string GetCallbackSignatureKey(BindingType type)
    {
        var returnType = type.CallbackReturnType?.UnaliasedManagedTypeName ?? "void";
        return string.Join("|", returnType, string.Join(";", type.CallbackParameterTypes.Select(static parameter => parameter.UnaliasedManagedTypeName)));
    }

    private static bool IsValidCallbackDelegatePolicy(ParameterBinding parameter, out string reason)
    {
        if (string.IsNullOrWhiteSpace(parameter.CallbackDelegateTypeName))
        {
            reason = string.Empty;
            return true;
        }

        if (IsExternalCallbackDelegateTypeName(parameter.CallbackDelegateTypeName!))
        {
            if (IsValidExternalCallbackDelegateTypeName(parameter.CallbackDelegateTypeName!))
            {
                reason = string.Empty;
                return true;
            }

            reason = $"external callback delegate type name '{parameter.CallbackDelegateTypeName}' is not a valid managed type name";
            return false;
        }

        if (IsValidCSharpIdentifier(parameter.CallbackDelegateTypeName!))
        {
            reason = string.Empty;
            return true;
        }

        reason = $"generated callback delegate name '{parameter.CallbackDelegateTypeName}' is not a valid C# identifier";
        return false;
    }

    private static bool IsGeneratedCallbackValueSupported(BindingType type, IReadOnlyDictionary<string, RecordBinding> recordsById)
    {
        if (type.PrimitiveKind == BindingPrimitiveKind.Void)
        {
            return true;
        }

        return type.Kind switch
        {
            BindingTypeKind.Primitive => type.PrimitiveKind != BindingPrimitiveKind.Unsupported ||
                IsGeneratedCallbackConfiguredValueSupported(type),
            BindingTypeKind.Handle => !recordsById.TryGetValue(type.RecordId ?? string.Empty, out var record) || record.IsHandleType,
            BindingTypeKind.Enum => true,
            _ => false
        };
    }

    private static IEnumerable<BindingType> GetGenericArgumentsForValidation(BindingType type)
    {
        return type.IsFixedArray && type.GenericArguments.Count >= 2
            ? type.GenericArguments.Take(1)
            : type.GenericArguments;
    }

    private static bool IsGeneratedCallbackConfiguredValueSupported(BindingType type)
    {
        return !string.IsNullOrWhiteSpace(type.MarshallerTypeName) &&
            string.Equals(type.MarshallerTypeName, type.UnaliasedManagedTypeName, StringComparison.Ordinal);
    }

    private static bool IsExternalCallbackDelegateTypeName(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal) ||
            value.Contains('.', StringComparison.Ordinal);
    }

    private static string NormalizeExternalCallbackDelegateTypeName(string value)
    {
        return value.StartsWith("global::", StringComparison.Ordinal)
            ? value
            : $"global::{value}";
    }

    private static bool IsValidExternalCallbackDelegateTypeName(string value)
    {
        var normalized = value.StartsWith("global::", StringComparison.Ordinal)
            ? value[8..]
            : value;
        var segments = normalized.Split('.', StringSplitOptions.TrimEntries);
        return segments.Length > 0 && segments.All(IsValidCSharpIdentifier);
    }

    private static bool IsValidCSharpIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value[0] != '_' && !char.IsLetter(value[0]))
        {
            return false;
        }

        return value.Skip(1).All(static ch => ch == '_' || char.IsLetterOrDigit(ch));
    }

    private static string SanitizeManagedToken(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        var token = builder.ToString().Trim('_');
        return token.Length == 0 ? "Callback" : token;
    }

    private static bool IsSupportedGenericHandleType(BindingType type, IReadOnlyDictionary<string, RecordBinding> recordsById)
    {
        if (type.Kind != BindingTypeKind.Handle ||
            string.IsNullOrWhiteSpace(type.RecordId) ||
            !recordsById.TryGetValue(type.RecordId, out var record))
        {
            return false;
        }

        // The fallback path handles template specializations of non-generic
        // records (e.g. std::vector<int> where the vector record isn't marked
        // generic).  A non-generic record with generic arguments whose C++ type
        // name doesn't match the record's qualified name is a template
        // instantiation and is accepted.
        return record.IsGeneric ||
            (type.GenericArguments.Count > 0 &&
             !record.IsGeneric &&
             !type.CppTypeName.StartsWith(record.CppQualifiedName, StringComparison.Ordinal));
    }

    private static bool IsSupportedOperatorShape(FunctionBinding function)
    {
        return function switch
        {
            { OperatorToken: "+", Parameters.Count: 0 } => true,
            { OperatorToken: "+", Parameters.Count: 1 } => true,
            { OperatorToken: "-", Parameters.Count: 0 } => true,
            { OperatorToken: "!", Parameters.Count: 0 } => true,
            { OperatorToken: "==" or "!=" or "<" or ">", Parameters.Count: 1 } => true,
            { IsConversionOperator: true, Parameters.Count: 0 } => true,
            _ => false
        };
    }

    private static bool HasCompanionOperator(RecordBinding record, FunctionBinding function, string companionToken)
    {
        return record.Members.OfType<FunctionBinding>().Any(candidate =>
            candidate.OperatorToken == companionToken &&
            candidate.Parameters.Count == function.Parameters.Count &&
            candidate.Parameters.Select(static parameter => parameter.CppTypeName)
                .SequenceEqual(function.Parameters.Select(static parameter => parameter.CppTypeName), StringComparer.Ordinal));
    }

    private static bool IsSelfComparisonOperator(RecordBinding record, FunctionBinding function)
    {
        if (function.Parameters.Count != 1)
        {
            return false;
        }

        var normalizedParameterType = NormalizeComparisonType(function.Parameters[0].CppTypeName);
        var normalizedRecordType = NormalizeComparisonType(record.CppQualifiedName);
        return string.Equals(normalizedParameterType, normalizedRecordType, StringComparison.Ordinal);
    }

    private static string NormalizeComparisonType(string value)
    {
        return value
            .Replace("const ", string.Empty, StringComparison.Ordinal)
            .Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static bool IsMarshallerBackedWrapperSurface(RecordBinding record)
    {
        return record.GetMetadata<bool>(MetadataKeys.MarshallerConfiguredRecord) ||
            !string.IsNullOrWhiteSpace(record.ManagedBaseTypeName) ||
            record.Members.OfType<FunctionBinding>().Any(static member => member.HasMetadata(MetadataKeys.MarshallerExposedMethod));
    }

    private static bool IsFatalIgnoredGenericArgumentRecord(RecordBinding record)
    {
        if (record.GetMetadata<bool>(MetadataKeys.ForwardDeclaration) &&
            !record.GetMetadata<bool>(MetadataKeys.OpaqueNativeHandle))
        {
            return true;
        }

        if (record.GetMetadata<bool>(MetadataKeys.PlaceholderRecord) &&
            !record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
        {
            return true;
        }

        var ignoreReason = record.GetIgnoreReason() ?? string.Empty;
        return ignoreReason.Contains("outside the configured namespace mappings", StringComparison.Ordinal) ||
            ignoreReason.Contains("internal detail namespace", StringComparison.Ordinal) ||
            ignoreReason.Contains("wrapper materialization requires a complete definition", StringComparison.Ordinal);
    }

    private static bool TryGetNamespaceExclusionReason(
        string cppQualifiedName,
        IReadOnlySet<string> allowedNamespaceRoots,
        out string reason)
    {
        if (allowedNamespaceRoots.Count == 0)
        {
            reason = string.Empty;
            return false;
        }

        var separatorIndex = cppQualifiedName.IndexOf("::", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            reason = $"global declaration '{cppQualifiedName}' is outside the configured namespace mappings for this compilation";
            return true;
        }

        var namespaceRoot = cppQualifiedName[..separatorIndex];
        if (allowedNamespaceRoots.Contains(namespaceRoot))
        {
            // Check for internal detail namespaces — a universal C++ convention
            // indicating implementation details that should not be wrapped.
            if (cppQualifiedName.Contains("::detail::", StringComparison.Ordinal) ||
                cppQualifiedName.Contains("::Detail::", StringComparison.Ordinal) ||
                cppQualifiedName.EndsWith("::detail", StringComparison.Ordinal) ||
                cppQualifiedName.EndsWith("::Detail", StringComparison.Ordinal))
            {
                reason = $"declaration is in an internal detail namespace: '{cppQualifiedName}'";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        reason = $"declaration namespace root '{namespaceRoot}' is outside the configured namespace mappings for this compilation";
        return true;
    }

    private static string GetCallableSignatureKey(FunctionBinding function)
    {
        return string.Join(
            "|",
            function.CppQualifiedName,
            function.IsStatic,
            function.IsConstructor,
            string.Join(";", function.Parameters.Select(static parameter => parameter.CppTypeName)));
    }

    private static string GetManagedCallableSignatureKey(FunctionBinding function)
    {
        return string.Join(
            "|",
            function.ManagedName,
            function.IsStatic,
            function.IsConstructor,
            GetGenericParameterSignature(function),
            string.Join(";", function.Parameters.Select(static parameter => parameter.CppTypeName)));
    }

    private static string GetManagedPublicCallableSignatureKey(FunctionBinding function)
    {
        return string.Join(
            "|",
            function.ManagedName,
            function.IsStatic,
            function.IsConstructor,
            GetGenericParameterSignature(function),
            string.Join(
                ";",
                function.Parameters.Select(static parameter =>
                    string.Join(
                        ":",
                        parameter.Direction,
                        parameter.AbiKind,
                        parameter.Type.Kind,
                        parameter.Type.UnaliasedManagedTypeName))));
    }

    private static string GetGenericParameterSignature(FunctionBinding function)
    {
        return string.Join(
            ",",
            function.GenericParameters.Select(static parameter => $"{parameter.Position}:{parameter.Name}"));
    }

    /// <summary>
    /// Returns <c>true</c> when the type itself or any element in its
    /// generic-argument tree is an open generic parameter (i.e.
    /// <see cref="BindingTypeKind.GenericParameter"/>).  This is more precise
    /// than <see cref="BindingType.ContainsGenericParameters"/> because it
    /// directly inspects the <see cref="BindingType.Kind"/> rather than
    /// relying on the cached property, which can produce false positives
    /// when generic arguments are inherited from a template record's default
    /// <c>HandleType</c>.
    /// </summary>
    private static bool ContainsOpenGenericParameter(BindingType type)
    {
        if (type.Kind == BindingTypeKind.GenericParameter)
        {
            return true;
        }

        foreach (var argument in type.GenericArguments)
        {
            if (ContainsOpenGenericParameter(argument))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether a type (or any of its generic arguments) uses a ref struct
    /// as a generic type argument.  C# does not allow ref structs (e.g.
    /// <see cref="Span{T}"/>, <see cref="ReadOnlySpan{T}"/>) to be used as type
    /// parameters, so any <c>Container&lt;ReadOnlySpan&lt;byte&gt;&gt;</c>
    /// instantiation would produce invalid C#.
    /// </summary>
    private static bool HasRefStructGenericArgument(BindingType type)
    {
        foreach (var argument in type.GenericArguments)
        {
            // The only ref struct currently mapped through the marshaller
            // system is ReadOnlySpan<byte> (from const char*).
            // Uses the IsRefStruct marshaller flag when available,
            // with a fallback exact ManagedTypeName match for bootstrap.
            if (IsArgumentRefStruct(argument))
            {
                return true;
            }

            if (HasRefStructGenericArgument(argument))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsArgumentRefStruct(BindingType argument)
    {
        return argument.Marshaller.IsRefStruct;
    }

}
