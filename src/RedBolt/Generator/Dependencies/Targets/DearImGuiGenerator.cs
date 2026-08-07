namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCpp.Model.Ast;

internal sealed class DearImGuiGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "dearimgui",
        "RedBolt.Bindings.DearImGui",
        "RedBolt.Bindings.DearImGui",
        "ImGui",
        nativeLibraryName,
        "redbolt_dearimgui",
        "-xc++",
        "-std=c++17",
        true)
{
    protected override string MarshallerLanguageStandardArgument => "-std=c++20";

    protected override IEnumerable<string> GetAdditionalMarshallerHeaders(GeneratorOptions options)
    {
        yield return Path.Combine(
            options.RedBoltRoot,
            "src",
            "RedBolt",
            "Native",
            "include",
            "RedBolt",
            "ImGuiValueMarshallers.hpp");
    }

    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        ConfigureFunctions(compilation);
    }

    protected override void AfterPruneCompilation(BindingCompilation compilation) =>
        ApplyEnumAliasSurface(compilation);

    private static void ConfigureFunctions(BindingCompilation compilation)
    {
        var overloadsByName = compilation.Functions.ToLookup(static function => function.CppQualifiedName);
        foreach (var function in compilation.Functions)
        {
            if (function.NativeName == "ImGui_ImplSDL2_KeyEventToImGuiKey")
            {
                foreach (var parameter in function.Parameters)
                    parameter.Type = BindingType.Int32;
            }

            if (function.Parameters.Any(static parameter =>
                    IsVaList(parameter.CppTypeName) || IsVaList(parameter.Type.CppTypeName)))
            {
                function.SetSilentExclusionReason("callables with va_list arguments are not supported");
                continue;
            }

            if (overloadsByName[function.CppQualifiedName]
                .Any(other => !ReferenceEquals(function, other) && IsConstCharCompatibilityOverload(function, other)))
                function.SetSilentExclusionReason("const char* compatibility shim duplicates the ImStrv binding");
        }

        foreach (var function in compilation.Functions)
        {
            if (function.NativeName is not ("InputText" or "InputTextMultiline" or "InputTextWithHint"))
                continue;

            for (var index = function.Parameters.Count - 1; index >= 0; --index)
            {
                if (function.Parameters[index].NativeName is "callback" or "user_data")
                    function.Parameters.RemoveAt(index);
            }

            var buffer = function.Parameters.First(static parameter => parameter.NativeName == "buf");
            buffer.Type = BindingType.IntPtr;
            buffer.Direction = BindingParameterDirection.In;
            buffer.AbiKind = BindingParameterAbiKind.Value;
            buffer.CppTypeName = "char*";
            function.SetMetadata(MetadataKeys.NativeDefaultArgumentAdapter, true);
        }

        foreach (var function in compilation.Functions)
        {
            if (function.CppQualifiedName == "ImGui::Begin")
            {
                function.Parameters.First(static parameter => parameter.NativeName == "p_open").Direction =
                    BindingParameterDirection.Ref;
            }
            if (function.NativeName == "Checkbox")
            {
                function.Parameters.First(static parameter => parameter.NativeName == "v").Direction =
                    BindingParameterDirection.Ref;
            }
        }
    }

    private static bool IsConstCharCompatibilityOverload(FunctionBinding candidate, FunctionBinding other)
    {
        if (candidate.Parameters.Count != other.Parameters.Count)
            return false;

        var replacedImStrv = false;
        for (var index = 0; index < candidate.Parameters.Count; ++index)
        {
            var candidateType = candidate.Parameters[index].Type.CppTypeName;
            var otherType = other.Parameters[index].Type.CppTypeName;
            if (candidateType == otherType)
                continue;
            if (candidateType == "const char*" && otherType == "ImStrv")
            {
                replacedImStrv = true;
                continue;
            }
            return false;
        }
        return replacedImStrv;
    }

    private static bool IsVaList(string cppTypeName) =>
        cppTypeName.Contains("va_list", StringComparison.Ordinal);

    private static void ApplyEnumAliasSurface(BindingCompilation compilation)
    {
        var enumsByNativeName = compilation.Enums.ToDictionary(static @enum => @enum.NativeName, StringComparer.Ordinal);
        foreach (var @enum in compilation.Enums)
        {
            if (@enum.NativeName.EndsWith("_", StringComparison.Ordinal))
                @enum.ManagedName = @enum.NativeName.TrimEnd('_');
            if (@enum.NativeName.EndsWith("Flags_", StringComparison.Ordinal))
                @enum.IsFlags = true;
        }

        var enumsByAliasName = new Dictionary<string, EnumBinding>(StringComparer.Ordinal);
        for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.TargetType.Kind != BindingTypeKind.Primitive ||
                alias.TargetType.PrimitiveKind != BindingPrimitiveKind.Int32 ||
                !enumsByNativeName.TryGetValue(alias.NativeName + "_", out var @enum))
            {
                continue;
            }

            enumsByAliasName[alias.NativeName] = @enum;
            compilation.TypeAliases.RemoveAt(index);
        }

        foreach (var function in compilation.Functions)
        {
            function.ReturnType = RemapEnumAliases(function.ReturnType, enumsByAliasName);
            foreach (var parameter in function.Parameters)
                parameter.Type = RemapEnumAliases(parameter.Type, enumsByAliasName);
        }

        foreach (var field in compilation.Records.SelectMany(static record => record.Members.OfType<FieldBinding>()))
            field.Type = RemapEnumAliases(field.Type, enumsByAliasName);

        compilation.InvalidateCaches();
    }

    private static BindingType RemapEnumAliases(
        BindingType type,
        IReadOnlyDictionary<string, EnumBinding> enumsByAliasName)
    {
        if (!string.IsNullOrWhiteSpace(type.AliasNativeName) &&
            enumsByAliasName.TryGetValue(type.AliasNativeName, out var @enum) &&
            type.Kind == BindingTypeKind.Primitive &&
            type.PrimitiveKind == BindingPrimitiveKind.Int32)
        {
            var nativeCppTypeName = type.AliasCppQualifiedName ?? type.AliasNativeName ?? type.CppTypeName;
            var remappedType = BindingType.CreateEnum(
                    nativeCppTypeName,
                    @enum.ManagedName,
                    enumId: @enum.SymbolId)
                .WithCppIsReference(type.Marshaller.CppIsReference);
            if (type.FixedArrayElementType is not null && type.FixedArrayLength is not null)
                remappedType = remappedType.WithFixedArray(type.FixedArrayElementType, type.FixedArrayLength.Value);
            if (type.DynamicArrayElementType is not null)
                remappedType = remappedType.WithDynamicArray(type.DynamicArrayElementType);
            return remappedType;
        }

        if (type.IsCallback)
        {
            var callbackReturnType = type.CallbackReturnType is null
                ? null
                : RemapEnumAliases(type.CallbackReturnType, enumsByAliasName);
            var callbackParameterTypes = type.CallbackParameterTypes
                .Select(parameter => RemapEnumAliases(parameter, enumsByAliasName))
                .ToArray();
            if (callbackReturnType is not null)
                type = type.WithCallbackSignature(callbackReturnType, callbackParameterTypes);
        }

        if (type.GenericArguments.Count > 0)
        {
            type = type.WithGenericArguments(
                type.GenericArguments.Select(argument => RemapEnumAliases(argument, enumsByAliasName)).ToArray());
        }
        if (type.FixedArrayElementType is not null && type.FixedArrayLength is not null)
        {
            type = type.WithFixedArray(
                RemapEnumAliases(type.FixedArrayElementType, enumsByAliasName),
                type.FixedArrayLength.Value);
        }
        if (type.DynamicArrayElementType is not null)
            type = type.WithDynamicArray(RemapEnumAliases(type.DynamicArrayElementType, enumsByAliasName));

        return type;
    }

}
