// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

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
            "DearImGui",
            "ImGuiValueMarshallers.hpp");
    }

    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        ConfigureFunctions(compilation);
        ConfigureRemainingTextPointers(compilation);
        ExcludeUnconstrainedImVectorHelpers(compilation);
        foreach (var record in compilation.Records.Where(static record =>
                     record.NativeName is "ImFontAtlasBuilder" or "ImGuiContext" or "ImGuiDockContext"))
            record.HasCopyConstructor = false;
        foreach (var field in compilation.Records
                     .Where(static record => record.NativeName is "ImDrawDataBuilder" or "ImGuiViewportP")
                     .SelectMany(static record => record.Members.OfType<FieldBinding>())
                     .Where(static field => field.NativeName is "Layers" or "DrawDataBuilder_Layers"))
            field.SetSilentExclusionReason("ImVector pointer arrays have no managed container projection");
    }

    // Optional `const char** remaining` outputs point into the input text; bind them as a raw
    // pointer slot defaulting to null instead of SharpCpp's copied `out string?`.
    // TODO: project an offset (`out int consumed` or a ReadOnlySpan<byte> slice).
    private static void ConfigureRemainingTextPointers(BindingCompilation compilation)
    {
        foreach (var function in compilation.Functions)
        {
            var name = function.NativeName switch
            {
                "CalcTextSizeA" => "remaining",
                "ImFontCalcTextSizeEx" => "out_remaining",
                "ImTextStrFromUtf8" => "in_remaining",
                _ => null
            };
            if (name is null)
                continue;

            foreach (var parameter in function.Parameters)
            {
                if (parameter.NativeName != name || parameter.PointerPointer != PointerPointerKind.Chars)
                    continue;

                // The resolver degrades a shape tag left on a non pointer-pointer parameter.
                parameter.RemoveMetadata(MetadataKeys.PointerPointerShape);
                parameter.RemoveMetadata(MetadataKeys.PointerPointerDefaultShape);
                parameter.PointerPointer = PointerPointerKind.None;
                parameter.Type = BindingType.IntPtr;
                parameter.Direction = BindingParameterDirection.In;
                parameter.AbiKind = BindingParameterAbiKind.Value;
                parameter.ManagedDefaultValueExpression = "default";
            }
        }
    }

    private static void ExcludeUnconstrainedImVectorHelpers(BindingCompilation compilation)
    {
        foreach (var function in compilation.Records
                     .Where(static record => record.NativeName == "ImVector")
                     .SelectMany(static record => record.Members.OfType<FunctionBinding>())
                     .Where(static function => function.NativeName is
                         "clear_delete" or "contains" or "find" or "find_index" or
                         "find_erase" or "find_erase_unsorted"))
        {
            // ImVector declares these for every T, but their bodies require either a
            // pointer element or operator==. The declarations carry no C++ constraint
            // that SharpCpp can use to select valid specializations.
            function.SetSilentExclusionReason("unconstrained ImVector helper is not valid for every element type");
        }
    }

    protected override void AfterPruneCompilation(BindingCompilation compilation) =>
        ApplyEnumAliasSurface(compilation);

    private static void ConfigureFunctions(BindingCompilation compilation)
    {
        var overloadsByName = compilation.Functions.ToLookup(static function => function.CppQualifiedName);
        foreach (var function in compilation.Functions)
        {
            if (function.CppQualifiedName is "ImGuizmo::PushID" or "ImGuizmo::PopID" or "ImGuizmo::GetID")
            {
                function.SetSilentExclusionReason("ImGuizmo ID helpers duplicate the existing ImGui managed API");
                continue;
            }

            if (function.NativeName == "RenderTextEllipsis")
            {
                var textSize = function.Parameters.First(static parameter => parameter.NativeName == "text_size_if_known");
                textSize.AbiKind = BindingParameterAbiKind.Pointer;
            }

            if (function.NativeName == "ItemAdd")
            {
                var navigationRectangle = function.Parameters.First(static parameter => parameter.NativeName == "nav_bb");
                navigationRectangle.AbiKind = BindingParameterAbiKind.Pointer;
                navigationRectangle.SetMetadata(MetadataKeys.UnexpressibleDefault, true);
            }

            if (function.NativeName == "ImGui_ImplSDL2_KeyEventToImGuiKey")
            {
                foreach (var parameter in function.Parameters)
                    parameter.Type = BindingType.Int32;
            }

            if (function.NativeName is "AddFontFromMemoryTTF" or "AddFontFromMemoryCompressedTTF")
            {
                var glyphRanges = function.Parameters.First(static parameter => parameter.NativeName == "glyph_ranges");
                glyphRanges.Type = BindingType.UInt16;
                glyphRanges.Direction = BindingParameterDirection.In;
                glyphRanges.AbiKind = BindingParameterAbiKind.Pointer;
                glyphRanges.SetMetadata(MetadataKeys.InputPointerArray, true);
            }

            if (function.CppQualifiedName == "ImGuizmo::Manipulate")
            {
                foreach (var parameter in function.Parameters.Where(static parameter =>
                             parameter.NativeName is "view" or "projection" or "matrix"
                                 or "deltaMatrix" or "snap" or "localBounds" or "boundsSnap"))
                {
                    parameter.Type = BindingType.Single;
                    parameter.Direction = BindingParameterDirection.In;
                    parameter.AbiKind = BindingParameterAbiKind.Pointer;
                    parameter.SetMetadata(MetadataKeys.InputPointerArray, true);
                    if (parameter.NativeName is "matrix" or "deltaMatrix")
                        parameter.SetMetadata(MetadataKeys.WritablePointerArray, true);
                }
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

            foreach (var parameter in function.Parameters)
            {
                if (parameter.NativeName is not ("callback" or "user_data"))
                    continue;

                parameter.Type = BindingType.IntPtr;
                parameter.Direction = BindingParameterDirection.In;
                parameter.AbiKind = BindingParameterAbiKind.Value;
                parameter.ManagedDefaultValueExpression = "default";
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
            if (function.CppQualifiedName == "ImGui::BeginPopupModal")
            {
                var open = function.Parameters.First(static parameter => parameter.NativeName == "p_open");
                open.Type = BindingType.IntPtr;
                open.Direction = BindingParameterDirection.In;
                open.AbiKind = BindingParameterAbiKind.Value;
                open.CppTypeName = "bool*";
            }
            if (function.NativeName == "Checkbox")
            {
                function.Parameters.First(static parameter => parameter.NativeName == "v").Direction =
                    BindingParameterDirection.Ref;
            }
            if (function.NativeName == "Combo")
            {
                function.Parameters.First(static parameter => parameter.NativeName == "current_item").Direction =
                    BindingParameterDirection.Ref;
            }
            if (function.NativeName == "RadioButton"
                && function.Parameters.FirstOrDefault(static parameter => parameter.NativeName == "v") is { } radioValue)
            {
                radioValue.Direction = BindingParameterDirection.Ref;
            }
            if (function.NativeName is "DragFloat" or "DragInt" or "InputFloat" or "InputInt" or "SliderFloat" or "SliderInt")
            {
                function.Parameters.First(static parameter => parameter.NativeName == "v").Direction =
                    BindingParameterDirection.Ref;
            }
            if (function.NativeName is "ColorEdit3" or "ColorEdit4"
                or "DragFloat2" or "DragFloat3" or "DragFloat4"
                or "DragInt2" or "DragInt3")
            {
                var value = function.Parameters.First(static parameter => parameter.NativeName is "col" or "v");
                value.Type = function.NativeName.StartsWith("DragInt", StringComparison.Ordinal)
                    ? BindingType.Int32
                    : BindingType.Single;
                value.Direction = BindingParameterDirection.In;
                value.AbiKind = BindingParameterAbiKind.Pointer;
                value.CppTypeName = $"{value.Type.CppTypeName}*";
                value.SetMetadata(MetadataKeys.InputPointerArray, true);
                value.SetMetadata(MetadataKeys.WritablePointerArray, true);
                if (function.NativeName is not ("ColorEdit3" or "ColorEdit4"))
                {
                    var format = function.Parameters.First(static parameter => parameter.NativeName == "format");
                    format.ManagedDefaultValueExpression = null;
                    format.SetMetadata(MetadataKeys.UndefaultableParam, true);
                }
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
