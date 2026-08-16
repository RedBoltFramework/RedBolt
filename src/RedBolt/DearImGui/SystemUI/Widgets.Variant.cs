// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;

public static partial class Widgets
{
    private struct QuaternionCachedInfo
    {
        public uint Time;
        public Quaternion Value;
        public Vector3f Angles;
    }

    private static int GetFloatNumberOfDigits(ReadOnlySpan<float> values, in EditVariantOptions options)
    {
        if (options.StepValue >= 1.0 || options.StepValue <= 0.0)
            return 0;

        var result = MathDefs.RoundToInt((float)-Math.Log10(options.StepValue));
        foreach (var value in values)
        {
            var absoluteValue = MathF.Abs(value);
            var numberOfDigits = absoluteValue != 0.0f
                ? MathDefs.RoundToInt(-MathF.Log10(absoluteValue)) + 1
                : 0;
            result = Math.Max(result, numberOfDigits);
        }
        return Math.Clamp(result, 1, 8);
    }

    private static ReadOnlySpan<byte> GetFloatFormatString(
        Span<byte> format, ReadOnlySpan<float> values, in EditVariantOptions options)
    {
        format[0] = (byte)'%';
        format[1] = (byte)'.';
        format[2] = (byte)('0' + GetFloatNumberOfDigits(values, options));
        format[3] = (byte)'f';
        return format[..4];
    }

    private static StringHash? GetMatchingType(
        ResourceFileDescriptor descriptor, StringHash currentType, StringVector? allowedTypes)
    {
        if (allowedTypes is null)
            return descriptor.HasObjectType(currentType) ? currentType : (StringHash?)null;

        if (allowedTypes.Count == 0)
            return new StringHash(descriptor.MostDerivedType);

        foreach (var type in allowedTypes)
        {
            if (descriptor.HasObjectType(type))
                return new StringHash(type);
        }
        return null;
    }

    private static int GetWrappedIndex(int index, int numberOfLabels)
    {
        if (index == 0)
            return 0;
        return (index - 1) % (numberOfLabels - 1) + 1;
    }

    private static ReadOnlySpan<char> StripSpaces(string value)
    {
        var result = value.AsSpan();
        while (!result.IsEmpty && result[0] == ' ')
            result = result[1..];
        return result;
    }

    private static unsafe bool InputText(ReadOnlySpan<byte> label, string value, ImGuiInputTextFlags flags)
    {
        InputTextBuffer.Set(value);
        var callback = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, int>)&InputTextCallback;
        return ImGui.InputText(
            label,
            InputTextBuffer.Pointer,
            (nuint)InputTextBuffer.Capacity,
            flags | ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackResize,
            callback,
            GCHandle.ToIntPtr(InputTextBufferHandle));
    }

    private static string GetInputTextValue(string value)
    {
        return InputTextBuffer.Equals(value) ? value : InputTextBuffer.ConvertToString();
    }

    public static bool InputText(
        ReadOnlySpan<byte> label, ref string value, ImGuiInputTextFlags flags = default)
    {
        var result = InputText(label, value, flags);
        value = GetInputTextValue(value);
        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int InputTextCallback(IntPtr dataPointer)
    {
        using var data = ImGuiInputTextCallbackData.CreateManagedWrapper(dataPointer, ownsHandle: false);
        if (data.EventFlag == ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackResize)
        {
            var value = (CString)GCHandle.FromIntPtr(data.UserData).Target!;
            Debug.Assert(data.Buf == value.Pointer);
            value.Resize(data.BufTextLen);
            data.Buf = value.Pointer;
        }
        return 0;
    }

    private static void PruneQuaternionCache()
    {
        const uint expireTimeMs = 1000;
        var currentTime = Time.GetSystemTime();
        foreach (var (id, info) in QuaternionCache)
        {
            if (currentTime - info.Time > expireTimeMs)
                QuaternionCache.Remove(id);
        }
    }

    private static Vector3f GetQuaternionAngles(uint id, in Quaternion quaternion)
    {
        ref var info = ref CollectionsMarshal.GetValueRefOrAddDefault(QuaternionCache, id, out var exists);
        if (!exists)
        {
            info.Value = Quaternion.IDENTITY;
            info.Angles = Quaternion.IDENTITY.EulerAngles;
        }

        info.Time = Time.GetSystemTime();
        if (info.Value == quaternion)
            return info.Angles;

        info.Value = quaternion;
        info.Angles = quaternion.EulerAngles;
        return info.Angles;
    }

    private static void UpdateQuaternionAngles(uint id, in Quaternion quaternion, in Vector3f angles)
    {
        ref var info = ref CollectionsMarshal.GetValueRefOrAddDefault(QuaternionCache, id, out _);
        info.Value = quaternion;
        info.Angles = angles;
    }

    public static bool EditResourceRef(ref StringHash type, ref string name, StringVector? allowedTypes)
    {
        var modified = false;

        if (allowedTypes is { Count: > 0 })
        {
            if (ImGui.Button("\uf03a"u8))
                ImGui.OpenPopup("##SelectType"u8);
            if (ImGui.IsItemHovered())
            {
                TooltipBuffer.FormatInPlace($"Select resource type ({allowedTypes.Count} allowed)");
                ImGui.SetTooltip(TooltipBuffer.ContentBytes);
            }
            ImGui.SameLine();

            if (ImGui.BeginPopup("##SelectType"u8))
            {
                foreach (var allowedType in allowedTypes)
                {
                    LabelBuffer.Set(allowedType);
                    if (ImGui.Selectable(LabelBuffer.ContentBytes, type == new StringHash(allowedType)))
                    {
                        type = new StringHash(allowedType);
                        modified = true;
                    }
                }
                ImGui.EndPopup();
            }
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (InputText(
                "##Name"u8,
                name,
                ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue
                    | ImGuiInputTextFlags.ImGuiInputTextFlagsNoUndoRedo))
        {
            modified = true;
        }
        name = GetInputTextValue(name);

        if (allowedTypes is not null && ImGui.IsItemHovered())
        {
            if (allowedTypes.Count == 0)
                ImGui.SetTooltip("Resource: any type"u8);
            else
            {
                string? typeName = null;
                foreach (var allowedType in allowedTypes)
                {
                    if (type != new StringHash(allowedType))
                        continue;
                    typeName = allowedType;
                    break;
                }
                TooltipBuffer.FormatInPlace($"Resource: {typeName ?? "Unknown"}");
                ImGui.SetTooltip(TooltipBuffer.ContentBytes);
            }
        }

        if (ImGui.BeginDragDropTarget())
        {
            var payload = DragDropPayload.Get() as ResourceDragDropPayload;
            if (payload is { Resources.Count: 1 } && !payload.Resources[0].IsDirectory)
            {
                var descriptor = payload.Resources[0];
                var matchingType = GetMatchingType(descriptor, type, allowedTypes);
                if (matchingType.HasValue)
                {
                    using var acceptedPayload = ImGui.AcceptDragDropPayload("DragDropPayload"u8);
                    if (!acceptedPayload.IsNull)
                    {
                        name = descriptor.ResourceName;
                        type = matchingType.Value;
                        modified = true;
                    }
                }
            }
            ImGui.EndDragDropTarget();
        }

        return modified;
    }

    public static bool EditResourceRefList(
        ref StringHash type,
        StringVector names,
        StringVector? allowedTypes,
        bool resizable,
        StringVector? elementNames = null)
    {
        return EditResourceRefList(ref type, (IList<string>)names, allowedTypes, resizable, elementNames);
    }

    private static bool EditResourceRefList(
        ref StringHash type,
        IList<string> names,
        StringVector? allowedTypes,
        bool resizable,
        StringVector? elementNames)
    {
        var modified = false;
        int? pendingRemove = null;

        for (var index = 0; index < names.Count; ++index)
        {
            using var idGuard = new IdScopeGuard(index);
            if (resizable)
            {
                if (ImGui.Button("\uf2ed"u8))
                    pendingRemove = index;
                ImGui.SameLine();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Remove item"u8);
            }
            else if (elementNames is { Count: > 0 })
            {
                var wrappedIndex = index % elementNames.Count;
                if (wrappedIndex == 0)
                    ImGui.Separator();
                LabelBuffer.Set(StripSpaces(elementNames[wrappedIndex]));
                ItemLabel(LabelBuffer.ContentBytes);
            }

            var name = names[index];
            if (EditResourceRef(ref type, ref name, allowedTypes))
                modified = true;
            names[index] = name;
        }

        if (pendingRemove.HasValue && pendingRemove.Value < names.Count)
        {
            names.RemoveAt(pendingRemove.Value);
            modified = true;
        }

        if (resizable)
        {
            if (ImGui.Button("\uf0fe Add item"u8))
            {
                names.Add(string.Empty);
                modified = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add item"u8);
        }
        else if (names.Count == 0)
            ImGui.NewLine();

        return modified;
    }

    public static bool EditBitmask(ref uint value)
    {
        const int groupWidth = 8;
        const int groupHeight = 2;
        const int numberOfGroups = 2;

        var modified = false;
        using var style = ImGui.GetStyle();
        using var colors = style.Colors;
        var availableWidth = ImGui.CalcItemWidth();
        var buttonSize = new Vector2f(
            MathDefs.Round(availableWidth / ((groupWidth + 2) * numberOfGroups)),
            MathDefs.Round(ImGui.GetFontSize() * 0.5f + style.ItemSpacing.Y));
        var pointSize = ImGui.GetWindowDpiScale();

        if (ImGui.Button("\uf142"u8))
            ImGui.OpenPopup("##Action"u8);
        ImGui.SameLine();

        if (ImGui.BeginPopup("##Action"u8))
        {
            if (ImGui.Selectable("Reset all to 0"u8))
            {
                modified = value != 0;
                value = 0;
            }
            if (ImGui.Selectable("Set all to 1"u8))
            {
                modified = value != uint.MaxValue;
                value = uint.MaxValue;
            }
            if (ImGui.Selectable("Invert all"u8))
            {
                modified = true;
                value = ~value;
            }
            ImGui.EndPopup();
        }

        var baseCursorPosition = ImGui.GetCursorPos();
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarFrameRounding, 3.0f);
        for (var row = 0; row < groupHeight; ++row)
        {
            for (var column = 0; column < groupWidth * numberOfGroups; ++column)
            {
                var groupIndex = column / groupWidth;
                var columnInGroup = column % groupWidth;
                var bitIndex = columnInGroup + groupWidth * (row + groupIndex * groupHeight);
                var bitMask = 1u << bitIndex;
                var selected = (value & bitMask) != 0;
                var currentColor = new Vector4f(colors[(int)(selected
                    ? ImGuiCol.ImGuiColButtonActive
                    : ImGuiCol.ImGuiColButton)]);
                Span<(ImGuiCol Id, Vector4f Color)> buttonColors =
                [
                    (ImGuiCol.ImGuiColButton, currentColor),
                    (ImGuiCol.ImGuiColButtonActive, currentColor),
                ];
                using var colorGuard = new ColorScopeGuard(buttonColors);
                using var idGuard = new IdScopeGuard(bitIndex);

                if (ImGui.Button(""u8, buttonSize))
                {
                    modified = true;
                    value ^= bitMask;
                }
                if (ImGui.IsItemHovered())
                {
                    TooltipBuffer.FormatInPlace($"Bit {bitIndex}");
                    ImGui.SetTooltip(TooltipBuffer.ContentBytes);
                }
                ImGui.SameLine(0.0f, pointSize + (columnInGroup == groupWidth - 1 ? buttonSize.X : 0.0f));
            }
            ImGui.NewLine();
            if (row != groupHeight - 1)
            {
                ImGui.SetCursorPos(new Vector2f(
                    baseCursorPosition.X,
                    baseCursorPosition.Y + buttonSize.Y + pointSize));
            }
        }
        ImGui.PopStyleVar();
        return modified;
    }

    public static bool EditVariantType(ref VariantType value)
    {
        return EditVariantType(ref value, "\uf03a"u8);
    }

    public static bool EditVariantType(ref VariantType value, ReadOnlySpan<byte> button)
    {
        var modified = false;
        if (ImGui.Button(button))
            ImGui.OpenPopup("##SelectType"u8);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Select variant type"u8);

        if (ImGui.BeginPopup("##SelectType"u8))
        {
            foreach (var allowedType in AllowedVariantTypes)
            {
                LabelBuffer.Set(Variant.GetTypeName(allowedType));
                if (!ImGui.Selectable(LabelBuffer.ContentBytes, value == allowedType))
                    continue;
                value = allowedType;
                modified = true;
            }
            ImGui.EndPopup();
        }
        return modified;
    }

    public static bool EditVariantVector(
        VariantVector value, bool resizable, bool dynamicTypes, StringVector? elementNames = null)
    {
        var modified = false;
        int? pendingRemove = null;

        for (var index = 0; index < value.Count; ++index)
        {
            using var idGuard = new IdScopeGuard(index);

            if (elementNames is { Count: > 1 })
            {
                var wrappedIndex = GetWrappedIndex(index, elementNames.Count);
                if (wrappedIndex == 1)
                    ImGui.Separator();
                LabelBuffer.Set(StripSpaces(elementNames[wrappedIndex]));
                ItemLabel(LabelBuffer.ContentBytes);
            }

            if (resizable)
            {
                if (ImGui.Button("\uf2ed"u8))
                    pendingRemove = index;
                ImGui.SameLine();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Remove item"u8);
            }

            ref var element = ref CollectionsMarshal.AsSpan(value)[index];
            var elementType = element.Type;
            if (dynamicTypes)
            {
                if (EditVariantType(ref elementType))
                {
                    SetVariant(ref element, new Variant(elementType));
                    modified = true;
                }
                ImGui.SameLine();
            }

            if (EditVariantValue(ref element))
                modified = true;
        }

        if (pendingRemove.HasValue && pendingRemove.Value < value.Count)
        {
            value.RemoveAt(pendingRemove.Value);
            modified = true;
        }

        if (resizable)
        {
            using var idGuard = new IdScopeGuard("##AddElement"u8);
            var newElementType = VariantType.None;
            if (EditVariantType(ref newElementType, "\uf0fe Add item"u8))
            {
                using var newElement = new Variant(newElementType);
                value.Add(newElement);
                modified = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add item"u8);
        }

        return modified;
    }

    public static bool EditStringVector(StringVector value, bool resizable)
    {
        var modified = false;
        int? pendingRemove = null;

        for (var index = 0; index < value.Count; ++index)
        {
            using var idGuard = new IdScopeGuard(index);
            if (resizable)
            {
                if (ImGui.Button("\uf2ed"u8))
                    pendingRemove = index;
                ImGui.SameLine();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Remove item"u8);
            }

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var committed = InputText(
                ""u8,
                value[index],
                ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue
                    | ImGuiInputTextFlags.ImGuiInputTextFlagsNoUndoRedo);
            value[index] = GetInputTextValue(value[index]);
            if (committed)
                return true;
        }

        if (pendingRemove.HasValue && pendingRemove.Value < value.Count)
        {
            value.RemoveAt(pendingRemove.Value);
            modified = true;
        }

        if (resizable)
        {
            using var idGuard = new IdScopeGuard("##AddElement"u8);
            var isButtonClicked = ImGui.Button("\uf0fe Add item"u8);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var isTextCommitted = InputText(
                ""u8,
                NewStringElement,
                ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue
                    | ImGuiInputTextFlags.ImGuiInputTextFlagsNoUndoRedo);
            NewStringElement = GetInputTextValue(NewStringElement);

            if (isButtonClicked || isTextCommitted)
            {
                value.Add(NewStringElement);
                modified = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add item"u8);
        }

        return modified;
    }

    public static bool EditStringVariantMap(
        StringVariantMap value, bool resizable, bool dynamicTypes, bool dynamicMetadata)
    {
        var modified = false;
        string? pendingRemove = null;
        var sortedKeys = new List<string>(value.Keys);
        sortedKeys.Sort(StringComparer.Ordinal);

        foreach (var key in sortedKeys)
        {
            if (dynamicMetadata && key.EndsWith('@'))
                continue;

            LabelBuffer.Set(key);
            using var idGuard = new IdScopeGuard(LabelBuffer.ContentBytes);
            ItemLabel(LabelBuffer.ContentBytes);

            if (resizable)
            {
                if (ImGui.Button("\uf2ed"u8))
                    pendingRemove = key;
                ImGui.SameLine();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Remove item"u8);
            }

            ref var element = ref CollectionsMarshal.GetValueRefOrNullRef(value, key);
            Debug.Assert(!Unsafe.IsNullRef(ref element));
            var elementType = element.Type;
            if (dynamicTypes)
            {
                if (EditVariantType(ref elementType))
                {
                    SetVariant(ref element, new Variant(elementType));
                    modified = true;
                }
                ImGui.SameLine();
            }

            if (dynamicMetadata)
            {
                var metadataKey = key + "@";
                if (elementType == VariantType.Int
                    && value.TryGetValue(metadataKey, out var metadata)
                    && metadata.Type == VariantType.StringVector)
                {
                    var options = new EditVariantOptions().Enum(metadata.GetStringVector());
                    if (EditVariant(ref element, in options))
                        modified = true;
                    continue;
                }
            }

            if (EditVariantValue(ref element))
                modified = true;
        }

        if (pendingRemove is not null && value.ContainsKey(pendingRemove))
        {
            value.Remove(pendingRemove);
            modified = true;
        }

        if (resizable)
        {
            using var idGuard = new IdScopeGuard("##AddElement"u8);
            TooltipBuffer.FormatInPlace($"\uf0fe Add new {Variant.GetTypeName(NewMapElementType)}");
            var isButtonClicked = ImGui.Button(TooltipBuffer.ContentBytes);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Add new item to the map"u8);
            ImGui.SameLine();

            EditVariantType(ref NewMapElementType);
            ImGui.SameLine();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            var isTextCommitted = InputText(
                ""u8,
                NewMapKey,
                ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue);
            NewMapKey = GetInputTextValue(NewMapKey);

            if (isButtonClicked || isTextCommitted)
            {
                using var newValue = new Variant(NewMapElementType);
                value[NewMapKey] = newValue;
                modified = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Item name"u8);
        }

        return modified;
    }

    public static bool EditVariantValue(ref Variant value)
    {
        var options = new EditVariantOptions().AllowResize().AllowTypeChange();
        return EditVariant(ref value, in options);
    }

    public static bool EditVariant(ref Variant value)
    {
        var options = new EditVariantOptions();
        return EditVariant(ref value, in options);
    }

    public static bool EditVariant(ref Variant value, in EditVariantOptions options)
    {
        switch (value.Type)
        {
        case VariantType.None:
            ImGui.Text("None"u8);
            return false;
        case VariantType.Ptr:
        case VariantType.VoidPtr:
            ImGui.Text("Unsupported: raw pointer"u8);
            return false;
        case VariantType.Custom:
            ImGui.Text("Unsupported: custom object"u8);
            return false;
        case VariantType.Int:
            if (options.IsBitmask)
                return EditVariantBitmask(ref value, in options);
            if (options.IntToString is { Count: > 0 })
                return EditVariantEnum(ref value, in options);
            return EditVariantInt(ref value, in options);
        case VariantType.Bool:
            return EditVariantBool(ref value, in options);
        case VariantType.Float:
            return EditVariantFloat(ref value, in options);
        case VariantType.Vector2:
            return EditVariantVector2(ref value, in options);
        case VariantType.DoubleVector2:
            return EditVariantDoubleVector2(ref value, in options);
        case VariantType.Vector3:
            return options.IsColor
                ? EditVariantColor(ref value, in options)
                : EditVariantVector3(ref value, in options);
        case VariantType.DoubleVector3:
            return EditVariantDoubleVector3(ref value, in options);
        case VariantType.Vector4:
            return options.IsColor
                ? EditVariantColor(ref value, in options)
                : EditVariantVector4(ref value, in options);
        case VariantType.Quaternion:
            return EditVariantQuaternion(ref value, in options);
        case VariantType.Color:
            return EditVariantColor(ref value, in options);
        case VariantType.String:
            return EditVariantString(ref value, in options);
        case VariantType.ResourceRef:
            return EditVariantResourceRef(ref value, in options);
        case VariantType.ResourceRefList:
            return EditVariantResourceRefList(ref value, in options);
        case VariantType.VariantVector:
            return EditVariantVector(ref value, in options);
        case VariantType.Vector2i:
            return EditVariantIntVector2(ref value, in options);
        case VariantType.Vector3i:
            return EditVariantIntVector3(ref value, in options);
        case VariantType.StringVector:
            return EditVariantStringVector(ref value, in options);
        case VariantType.Rect:
            return EditVariantRect(ref value, in options);
        case VariantType.Int64:
            return EditVariantInt64(ref value, in options);
        case VariantType.StringVariantMap:
            return EditVariantStringVariantMap(ref value, in options);
        default:
            ImGui.Button("TODO: Implement"u8);
            return false;
        }
    }

    private static bool EditVariantColor(ref Variant value, in EditVariantOptions options)
    {
        var isColor = value.Type == VariantType.Color;
        var hasAlpha = isColor || value.Type == VariantType.Vector4;
        var flags = hasAlpha
            ? ImGuiColorEditFlags.ImGuiColorEditFlagsNone
            : ImGuiColorEditFlags.ImGuiColorEditFlagsNoAlpha;
        var color = isColor
            ? value.GetColor()
            : hasAlpha
                ? new Color(value.GetVector4())
                : new Color(value.GetVector3());
        Span<float> components = [color.R, color.G, color.B, color.A];
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.ColorEdit4(""u8, components, flags))
            return false;

        color = new Color(components[0], components[1], components[2], components[3]);
        SetVariant(ref value, isColor
            ? new Variant(color)
            : hasAlpha
                ? new Variant(color.ToVector4())
                : new Variant(color.ToVector3()));
        return true;
    }

    private static unsafe bool EditVariantDoubleVector2(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetDoubleVector2();
        var minimum = options.MinimumValue;
        var maximum = options.MaximumValue;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragScalarN(
                ""u8,
                ImGuiDataType.ImGuiDataTypeDouble,
                (IntPtr)(&current.X),
                2,
                (float)options.StepValue,
                (IntPtr)(&minimum),
                (IntPtr)(&maximum),
                "%.6f"u8))
        {
            return false;
        }
        SetVariant(ref value, new Variant(current));
        return true;
    }

    private static unsafe bool EditVariantDoubleVector3(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetDoubleVector3();
        var minimum = options.MinimumValue;
        var maximum = options.MaximumValue;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragScalarN(
                ""u8,
                ImGuiDataType.ImGuiDataTypeDouble,
                (IntPtr)(&current.X),
                3,
                (float)options.StepValue,
                (IntPtr)(&minimum),
                (IntPtr)(&maximum),
                "%.6f"u8))
        {
            return false;
        }
        SetVariant(ref value, new Variant(current));
        return true;
    }

    private static bool EditVariantFloat(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetFloat();
        Span<float> values = [current];
        Span<byte> format = stackalloc byte[4];
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragFloat(
                ""u8,
                ref current,
                (float)options.StepValue,
                (float)options.MinimumValue,
                (float)options.MaximumValue,
                GetFloatFormatString(format, values, in options)))
        {
            return false;
        }
        SetVariant(ref value, new Variant(current));
        return true;
    }

    private static bool EditVariantVector2(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetVector2();
        Span<float> components = [current.X, current.Y];
        Span<byte> format = stackalloc byte[4];
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragFloat2(
                ""u8,
                components,
                (float)options.StepValue,
                (float)options.MinimumValue,
                (float)options.MaximumValue,
                GetFloatFormatString(format, components, in options)))
        {
            return false;
        }
        SetVariant(ref value, new Variant(new Vector2f(components[0], components[1])));
        return true;
    }

    private static bool EditVariantIntVector2(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetIntVector2();
        Span<int> components = [current.X, current.Y];
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragInt2(
                ""u8,
                components,
                (float)options.StepValue,
                (int)options.MinimumValue,
                (int)options.MaximumValue))
        {
            return false;
        }
        SetVariant(ref value, new Variant(new Vector2i(components[0], components[1])));
        return true;
    }

    private static bool EditVariantVector3(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetVector3();
        Span<float> components = [current.X, current.Y, current.Z];
        Span<byte> format = stackalloc byte[4];
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragFloat3(
                ""u8,
                components,
                (float)options.StepValue,
                (float)options.MinimumValue,
                (float)options.MaximumValue,
                GetFloatFormatString(format, components, in options)))
        {
            return false;
        }
        SetVariant(ref value, new Variant(new Vector3f(components[0], components[1], components[2])));
        return true;
    }

    private static bool EditVariantIntVector3(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetIntVector3();
        Span<int> components = [current.X, current.Y, current.Z];
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragInt3(
                ""u8,
                components,
                (float)options.StepValue,
                (int)options.MinimumValue,
                (int)options.MaximumValue))
        {
            return false;
        }
        SetVariant(ref value, new Variant(new Vector3i(components[0], components[1], components[2])));
        return true;
    }

    private static bool EditVariantVector4(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetVector4();
        Span<float> components = [current.X, current.Y, current.Z, current.W];
        Span<byte> format = stackalloc byte[4];
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragFloat4(
                ""u8,
                components,
                (float)options.StepValue,
                (float)options.MinimumValue,
                (float)options.MaximumValue,
                GetFloatFormatString(format, components, in options)))
        {
            return false;
        }
        SetVariant(ref value, new Variant(new Vector4f(components[0], components[1], components[2], components[3])));
        return true;
    }

    private static bool EditVariantRect(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetRect();
        Span<float> components = [current.Min.X, current.Min.Y, current.Max.X, current.Max.Y];
        Span<byte> format = stackalloc byte[4];
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragFloat4(
                ""u8,
                components,
                (float)options.StepValue,
                (float)options.MinimumValue,
                (float)options.MaximumValue,
                GetFloatFormatString(format, components, in options)))
        {
            return false;
        }
        SetVariant(ref value, new Variant(new Rect(components)));
        return true;
    }

    private static bool EditVariantQuaternion(ref Variant value, in EditVariantOptions options)
    {
        var id = ImGui.GetID("Quaternion"u8);
        PruneQuaternionCache();
        var current = value.GetQuaternion();
        var angles = GetQuaternionAngles(id, in current);
        Span<float> components = [angles.X, angles.Y, angles.Z];

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        const float maximumValue = 360.0f;
        if (!ImGui.DragFloat3(
                ""u8,
                components,
                1.0f,
                -maximumValue * 100.0f,
                maximumValue * 100.0f,
                "%.2f"u8))
        {
            return false;
        }

        angles = new Vector3f(components[0], components[1], components[2]);
        var updated = new Quaternion(angles);
        UpdateQuaternionAngles(id, in updated, in angles);
        SetVariant(ref value, new Variant(updated));
        return true;
    }

    private static bool EditVariantBool(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetBool();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.Checkbox(""u8, ref current))
            return false;
        SetVariant(ref value, new Variant(current));
        return true;
    }

    private static bool EditVariantInt(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetInt();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.DragInt(
                ""u8,
                ref current,
                (float)Math.Max(1.0, options.StepValue),
                (int)options.MinimumValue,
                (int)options.MaximumValue))
        {
            return false;
        }
        SetVariant(ref value, new Variant(current));
        return true;
    }

    private static unsafe bool EditVariantInt64(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetInt64();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.InputScalar(""u8, ImGuiDataType.ImGuiDataTypeS64, (IntPtr)(&current)))
            return false;
        SetVariant(ref value, new Variant(current));
        return true;
    }

    private static bool EditVariantString(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetString();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var isCommitted = InputText(
            ""u8,
            current,
            ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue
                | ImGuiInputTextFlags.ImGuiInputTextFlagsNoUndoRedo
                | ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackAlways);
        var isDeactivated = ImGui.IsItemDeactivatedAfterEdit();
        if (!isCommitted && !isDeactivated)
            return false;

        SetVariant(ref value, new Variant(GetInputTextValue(current)));
        return true;
    }

    private static bool EditVariantEnum(ref Variant value, in EditVariantOptions options)
    {
        var items = options.IntToString!;
        var maximumEnumValue = items.Count - 1;
        var current = Math.Clamp(value.GetInt(), 0, maximumEnumValue);
        LabelBuffer.Set(items[current]);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (!ImGui.BeginCombo(""u8, LabelBuffer.ContentBytes))
            return false;

        var changed = false;
        for (var index = 0; index <= maximumEnumValue; ++index)
        {
            LabelBuffer.Set(items[index]);
            if (!ImGui.Selectable(LabelBuffer.ContentBytes, current == index))
                continue;
            SetVariant(ref value, new Variant(index));
            changed = true;
            break;
        }
        ImGui.EndCombo();
        return changed;
    }

    private static bool EditVariantResourceRef(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetResourceRef();
        var allowedTypes = current.Type == TextureType ? LightShapeTypes : options.ResourceTypeNames;
        if (!EditResourceRef(ref current.Type, ref current.Name, allowedTypes))
            return false;
        SetVariant(ref value, new Variant(current));
        return true;
    }

    private static bool EditVariantResourceRefList(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetResourceRefList();
        var effectiveLines = current.Names.Count + (options.IsResizeAllowed ? 1 : 0);
        if (effectiveLines > 1)
        {
            ImGui.NewLine();
            ImGui.Indent();
        }
        if (EditResourceRefList(
                ref current.Type,
                current.Names,
                options.ResourceTypeNames,
                options.IsResizeAllowed,
                options.SizedStructVectorElementNames))
        {
            SetVariant(ref value, new Variant(current));
            return true;
        }
        if (effectiveLines > 1)
            ImGui.Unindent();
        return false;
    }

    private static bool EditVariantVector(ref Variant value, in EditVariantOptions options)
    {
        if (!ImGui.CollapsingHeader("##Elements"u8))
            return false;

        var current = value.GetVariantVector();
        var modified = false;
        ImGui.Indent();
        if (EditVariantVector(
                current,
                options.IsResizeAllowed,
                options.IsTypeChangeAllowed,
                options.SizedStructVectorElementNames))
        {
            modified = true;
        }
        ImGui.Unindent();
        return modified;
    }

    private static bool EditVariantStringVector(ref Variant value, in EditVariantOptions options)
    {
        if (!ImGui.CollapsingHeader("##Elements"u8))
            return false;

        var current = value.GetStringVector();
        var modified = false;
        ImGui.Indent();
        if (EditStringVector(current, options.IsResizeAllowed))
            modified = true;
        ImGui.Unindent();
        return modified;
    }

    private static bool EditVariantStringVariantMap(ref Variant value, in EditVariantOptions options)
    {
        if (!ImGui.CollapsingHeader("##Elements"u8))
            return false;

        var current = value.GetStringVariantMap();
        var modified = false;
        ImGui.Indent();
        if (EditStringVariantMap(
                current,
                options.IsResizeAllowed,
                options.IsTypeChangeAllowed,
                options.UsesDynamicMetadata))
        {
            modified = true;
        }
        ImGui.Unindent();
        return modified;
    }

    private static bool EditVariantBitmask(ref Variant value, in EditVariantOptions options)
    {
        var current = value.GetUInt();
        if (!EditBitmask(ref current))
            return false;
        SetVariant(ref value, new Variant(current));
        return true;
    }

    private static void SetVariant(ref Variant value, Variant replacement)
    {
        value.Dispose();
        value = replacement;
    }

    private static readonly VariantType[] AllowedVariantTypes =
    [
        VariantType.Int,
        VariantType.Bool,
        VariantType.Float,
        VariantType.Vector2,
        VariantType.DoubleVector2,
        VariantType.Vector3,
        VariantType.Vector4,
        VariantType.DoubleVector3,
        VariantType.Quaternion,
        VariantType.Color,
        VariantType.String,
        VariantType.Buffer,
        VariantType.ResourceRef,
        VariantType.ResourceRefList,
        VariantType.VariantVector,
        VariantType.VariantMap,
        VariantType.IntRect,
        VariantType.Vector2i,
        VariantType.Matrix3,
        VariantType.Matrix3x4,
        VariantType.Matrix4,
        VariantType.Double,
        VariantType.StringVector,
        VariantType.Rect,
        VariantType.Vector3i,
        VariantType.Int64,
        VariantType.VariantCurve,
        VariantType.StringVariantMap,
    ];
    private static readonly StringHash TextureType = typeof(Texture).GetTypeId();
    private static readonly StringVector LightShapeTypes = new(
        [typeof(Texture2D).GetClassName(), typeof(TextureCube).GetClassName()]);
    private static readonly Dictionary<uint, QuaternionCachedInfo> QuaternionCache = [];
    private static readonly CString LabelBuffer = new();
    private static readonly CString TooltipBuffer = new();
    private static readonly CString InputTextBuffer = new();
    private static readonly GCHandle InputTextBufferHandle = GCHandle.Alloc(InputTextBuffer);
    private static string NewStringElement = string.Empty;
    private static string NewMapKey = string.Empty;
    private static VariantType NewMapElementType = VariantType.String;
}
