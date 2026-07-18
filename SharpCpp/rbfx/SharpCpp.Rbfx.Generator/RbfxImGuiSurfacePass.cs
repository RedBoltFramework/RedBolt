using SharpCpp.Model.Ast;

namespace SharpCpp.Rbfx.Generator;

public static class RbfxImGuiSurfacePass
{
    private static readonly HashSet<string> PublicGlobalRecordNames = new(StringComparer.Ordinal)
    {
        "ImDrawCmd",
        "ImDrawData",
        "ImDrawList",
        "ImFont",
        "ImFontAtlas",
        "ImGuiIO",
        "ImGuiStyle",
        "ImGuiViewport"
    };

    private static readonly HashSet<string> ConfiguredValueRecordNames = new(StringComparer.Ordinal)
    {
        "ImVec2",
        "ImVec4"
    };

    private static readonly HashSet<string> PublicFreeFunctionNames = new(StringComparer.Ordinal)
    {
        "Begin",
        "BeginItemTooltip",
        "Button",
        "End",
        "EndTooltip",
        "PopStyleColor",
        "PopTextWrapPos",
        "PushStyleColor",
        "PushTextWrapPos",
        "Separator",
        "SeparatorText",
        "SetNextWindowPos",
        "SetNextWindowSize",
        "TextUnformatted"
    };

    public static int Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        var changed = 0;

        foreach (var record in compilation.Records)
        {
            if (IsDearImGuiHeader(record.HeaderIncludePath) && PublicGlobalRecordNames.Contains(record.CppQualifiedName))
            {
                record.ManagedNamespace = "ImGuiNet";
                record.ClearIgnoreReason();
                changed++;
            }
            else if (IsDearImGuiHeader(record.HeaderIncludePath))
            {
                record.SetSilentExclusionReason("Dear ImGui declaration is outside the conservative bootstrap surface", overwriteExisting: false);
            }
        }

        foreach (var enumBinding in compilation.Enums)
        {
            if (!IsDearImGuiHeader(enumBinding.HeaderIncludePath))
            {
                continue;
            }

            enumBinding.ManagedNamespace = "ImGuiNet";
            enumBinding.ManagedName = enumBinding.NativeName.TrimEnd('_');
            enumBinding.IsFlags = enumBinding.ManagedName.EndsWith("Flags", StringComparison.Ordinal);
            NormalizeEnumValues(enumBinding);
            enumBinding.ClearIgnoreReason();
            changed++;
        }

        foreach (var function in compilation.Functions.Where(static function => IsDearImGuiHeader(function.HeaderIncludePath)))
        {
            function.ManagedNamespace = "ImGuiNet";
            function.ManagedClassName = "ImGui";
            if (IsPublicImGuiHeader(function.HeaderIncludePath) &&
                function.CppQualifiedName.StartsWith("ImGui::", StringComparison.Ordinal) &&
                PublicFreeFunctionNames.Contains(function.NativeName))
                function.ClearIgnoreReason();
            else
                function.SetSilentExclusionReason("Dear ImGui callable is outside the conservative public imgui.h surface", overwriteExisting: true);
            changed++;
        }

        var recordsBySymbolId = compilation.Records
            .GroupBy(static record => record.SymbolId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.Ordinal);
        foreach (var function in compilation.Functions.Where(static function => !function.IsIgnored()))
        {
            if (FunctionUsesUnsupportedImGuiType(function, recordsBySymbolId))
            {
                function.SetSilentExclusionReason("Dear ImGui callable references a type outside the conservative bootstrap surface");
                changed++;
            }
        }

        foreach (var field in compilation.Records.SelectMany(static record => record.Members.OfType<FieldBinding>()).Where(static field => !field.IsIgnored()))
        {
            var parentIsDearImGui = field.Parent is RecordBinding parentRecord && IsDearImGuiRecord(parentRecord);
            if (TypeUsesUnsupportedImGuiType(field.Type, field.CppTypeName, recordsBySymbolId) ||
                (parentIsDearImGui && IsUnsupportedImGuiFieldType(field.CppTypeName, field.Type.CppTypeName)))
            {
                field.SetSilentExclusionReason("Dear ImGui field references a type outside the conservative bootstrap surface");
                changed++;
            }
        }

        foreach (var record in compilation.Records)
        {
            changed += RemoveUnsupportedGenericInstantiations(record.SupportedGenericInstantiations, recordsBySymbolId);
        }

        foreach (var function in compilation.Functions)
        {
            changed += RemoveUnsupportedGenericInstantiations(function.SupportedGenericInstantiations, recordsBySymbolId);
        }

        changed += RemoveUnsupportedTypeAliases(compilation.TypeAliases, recordsBySymbolId);

        return changed;
    }

    private static bool IsDearImGuiGlobal(string cppQualifiedName)
        => cppQualifiedName.StartsWith("Im", StringComparison.Ordinal) && !cppQualifiedName.Contains("::", StringComparison.Ordinal);

    private static bool IsDearImGuiHeader(string? headerIncludePath)
    {
        if (string.IsNullOrWhiteSpace(headerIncludePath))
            return false;

        var normalized = headerIncludePath.Replace('\\', '/');
        return string.Equals(normalized, "imgui.h", StringComparison.Ordinal) ||
            string.Equals(normalized, "ImGui/imgui.h", StringComparison.Ordinal) ||
            string.Equals(normalized, "imgui_internal.h", StringComparison.Ordinal) ||
            string.Equals(normalized, "ImGui/imgui_internal.h", StringComparison.Ordinal) ||
            normalized.EndsWith("/ImGui/imgui.h", StringComparison.Ordinal) ||
            normalized.EndsWith("/ImGui/imgui_internal.h", StringComparison.Ordinal);
    }

    private static bool IsPublicImGuiHeader(string? headerIncludePath)
    {
        if (string.IsNullOrWhiteSpace(headerIncludePath))
            return false;

        var normalized = headerIncludePath.Replace('\\', '/');
        return string.Equals(normalized, "imgui.h", StringComparison.Ordinal) ||
            string.Equals(normalized, "ImGui/imgui.h", StringComparison.Ordinal) ||
            normalized.EndsWith("/ImGui/imgui.h", StringComparison.Ordinal);
    }

    private static bool FunctionUsesUnsupportedImGuiType(
        FunctionBinding function,
        IReadOnlyDictionary<string, RecordBinding> recordsBySymbolId)
    {
        return (IsDearImGuiCallable(function) && IsUnsupportedImGuiFunction(function)) ||
            TypeUsesUnsupportedImGuiType(function.ReturnType, function.CppReturnTypeName, recordsBySymbolId) ||
            function.Parameters.Any(parameter => TypeUsesUnsupportedImGuiType(parameter.Type, parameter.CppTypeName, recordsBySymbolId));
    }

    private static bool IsDearImGuiCallable(FunctionBinding function)
    {
        return function.CppQualifiedName.StartsWith("ImGui::", StringComparison.Ordinal) ||
            (function.Parent is RecordBinding parentRecord && IsDearImGuiRecord(parentRecord));
    }

    private static bool IsUnsupportedImGuiFunction(FunctionBinding function)
    {
        var declarationText = function.GetMetadata<string>(MetadataKeys.DeclarationDisplayText) ?? string.Empty;
        return declarationText.Contains("...", StringComparison.Ordinal) ||
            function.CppQualifiedName.Contains("operator\"\"", StringComparison.Ordinal) ||
            function.NativeName.Contains("operator\"\"", StringComparison.Ordinal);
    }

    private static bool TypeUsesUnsupportedImGuiType(
        BindingType type,
        string cppTypeName,
        IReadOnlyDictionary<string, RecordBinding> recordsBySymbolId)
    {
        if (type.IsFixedArray || ContainsUnsupportedImGuiType(cppTypeName) || ContainsUnsupportedImGuiType(type.CppTypeName))
        {
            return true;
        }

        if (type.RecordId is not null &&
            recordsBySymbolId.TryGetValue(type.RecordId, out var record) &&
            IsDearImGuiRecord(record) &&
            !PublicGlobalRecordNames.Contains(record.CppQualifiedName) &&
            !ConfiguredValueRecordNames.Contains(record.CppQualifiedName))
        {
            return true;
        }

        return type.GenericArguments.Any(argument => TypeUsesUnsupportedImGuiType(argument, argument.CppTypeName, recordsBySymbolId)) ||
            (type.FixedArrayElementType is not null && TypeUsesUnsupportedImGuiType(type.FixedArrayElementType, type.FixedArrayElementType.CppTypeName, recordsBySymbolId)) ||
            (type.DynamicArrayElementType is not null && TypeUsesUnsupportedImGuiType(type.DynamicArrayElementType, type.DynamicArrayElementType.CppTypeName, recordsBySymbolId)) ||
            (type.CallbackReturnType is not null && TypeUsesUnsupportedImGuiType(type.CallbackReturnType, type.CallbackReturnType.CppTypeName, recordsBySymbolId)) ||
            type.CallbackParameterTypes.Any(parameter => TypeUsesUnsupportedImGuiType(parameter, parameter.CppTypeName, recordsBySymbolId));
    }

    private static int RemoveUnsupportedGenericInstantiations(
        IList<RecordGenericInstantiationBinding> instantiations,
        IReadOnlyDictionary<string, RecordBinding> recordsBySymbolId)
    {
        var removed = 0;
        for (var index = instantiations.Count - 1; index >= 0; --index)
        {
            if (!IsConfiguredValueFixedArrayInstantiation(instantiations[index]) &&
                !instantiations[index].TypeArguments.Any(type => TypeUsesUnsupportedImGuiType(type, type.CppTypeName, recordsBySymbolId)))
            {
                continue;
            }

            instantiations.RemoveAt(index);
            removed++;
        }

        return removed;
    }

    private static int RemoveUnsupportedTypeAliases(
        IList<BindingTypeAlias> aliases,
        IReadOnlyDictionary<string, RecordBinding> recordsBySymbolId)
    {
        var removed = 0;
        for (var index = aliases.Count - 1; index >= 0; --index)
        {
            if (!TypeUsesUnsupportedImGuiType(aliases[index].TargetType, aliases[index].TargetType.CppTypeName, recordsBySymbolId))
            {
                continue;
            }

            aliases.RemoveAt(index);
            removed++;
        }

        return removed;
    }

    private static bool ContainsUnsupportedImGuiType(string cppTypeName)
        => cppTypeName.Contains("ImTextureID", StringComparison.Ordinal) ||
            cppTypeName.Contains("ImVector<void", StringComparison.Ordinal) ||
            cppTypeName.Contains("const ImVec4*", StringComparison.Ordinal) ||
            cppTypeName.Contains("__va_list_tag", StringComparison.Ordinal) ||
            cppTypeName.Contains("va_list", StringComparison.Ordinal) ||
            cppTypeName.Contains("ImGuiInputTextCallback", StringComparison.Ordinal) ||
            cppTypeName.Contains("ImGuiSizeCallback", StringComparison.Ordinal) ||
            cppTypeName.Contains("ImGuiErrorLogCallback", StringComparison.Ordinal) ||
            cppTypeName.Contains("ImGuiMemAllocFunc", StringComparison.Ordinal) ||
            cppTypeName.Contains("ImGuiMemFreeFunc", StringComparison.Ordinal) ||
            cppTypeName.Contains("[]", StringComparison.Ordinal) ||
            cppTypeName.Contains("**", StringComparison.Ordinal) ||
            IsConfiguredValueFixedArrayType(cppTypeName);

    private static bool IsDearImGuiRecord(RecordBinding record)
        => IsDearImGuiHeader(record.HeaderIncludePath);

    private static bool IsUnsupportedImGuiFieldType(string cppTypeName, string modelCppTypeName)
        => IsCharPointer(cppTypeName) || IsCharPointer(modelCppTypeName);

    private static bool IsCharPointer(string cppTypeName)
    {
        var normalized = cppTypeName.Replace(" ", string.Empty, StringComparison.Ordinal);
        return normalized.Contains("char*", StringComparison.Ordinal);
    }

    private static bool IsConfiguredValueFixedArrayInstantiation(RecordGenericInstantiationBinding instantiation)
    {
        return instantiation.TypeArguments.Count >= 2 &&
            int.TryParse(instantiation.TypeArguments[1].CppTypeName, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _) &&
            (ConfiguredValueRecordNames.Contains(instantiation.TypeArguments[0].CppTypeName) ||
             instantiation.TypeArguments[0].PrimitiveKind == BindingPrimitiveKind.Boolean);
    }

    private static bool IsConfiguredValueFixedArrayType(string cppTypeName)
        => cppTypeName.Contains("ImVec2[", StringComparison.Ordinal) ||
            cppTypeName.Contains("ImVec4[", StringComparison.Ordinal) ||
            cppTypeName.Contains("<ImVec2,", StringComparison.Ordinal) ||
            cppTypeName.Contains("<ImVec4,", StringComparison.Ordinal);

    private static void NormalizeEnumValues(EnumBinding enumBinding)
    {
        var prefix = enumBinding.ManagedName + "_";
        foreach (var value in enumBinding.Values)
        {
            var name = value.NativeName.StartsWith(prefix, StringComparison.Ordinal)
                ? value.NativeName[prefix.Length..]
                : value.NativeName;
            value.ManagedName = NormalizeEnumValueName(ToPascalCase(name));
        }
    }

    private static string NormalizeEnumValueName(string name)
        => string.IsNullOrWhiteSpace(name) ? "Value" : char.IsDigit(name[0]) ? $"Value{name}" : name;

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var parts = value.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return value;

        return string.Concat(parts.Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
