//
// Copyright (c) 2008-2017 the Urho3D project.
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.SystemUI;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;

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
        case VariantType.Vector2i:
            return EditVariantIntVector2(ref value, in options);
        case VariantType.Vector3i:
            return EditVariantIntVector3(ref value, in options);
        case VariantType.Rect:
            return EditVariantRect(ref value, in options);
        case VariantType.Int64:
            return EditVariantInt64(ref value, in options);
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

    private static readonly Dictionary<uint, QuaternionCachedInfo> QuaternionCache = [];
    private static readonly CString LabelBuffer = new();
    private static readonly CString TooltipBuffer = new();
}
