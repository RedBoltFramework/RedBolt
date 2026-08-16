// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System;
using System.Runtime.CompilerServices;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;

public static partial class Widgets
{
    /// <summary>Return best size of small square button with one icon.</summary>
    public static float GetSmallButtonSize()
    {
        using var style = ImGui.GetStyle();
        return ImGui.GetFontSize() + style.FramePadding.Y * 2.0f;
    }

    /// <summary>Render toolbar button with optional tooltip. May be toggled on.</summary>
    public static bool ToolbarButton(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip = default, bool active = false)
    {
        using var style = ImGui.GetStyle();
        using var colors = style.Colors;
        var dimension = GetSmallButtonSize();
        using var colorGuard = new ColorScopeGuard(
            ImGuiCol.ImGuiColButton,
            new Vector4f(colors[(int)ImGuiCol.ImGuiColButtonActive]),
            active);

        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarFramePadding, new Vector2f());

        var result = ImGui.ButtonEx(
            label,
            new Vector2f(dimension, dimension),
            (ImGuiButtonFlags)ImGuiButtonFlagsPrivate.ImGuiButtonFlagsPressedOnClick);

        ImGui.PopStyleVar();

        ImGui.SameLine(0, 0);

        if (ImGui.IsItemHovered() && tooltip.Length != 0)
            ImGui.SetTooltip(tooltip);

        return result;
    }

    /// <summary>Render a bit of space in toolbar between buttons.</summary>
    public static void ToolbarSeparator()
    {
        using var style = ImGui.GetStyle();
        ImGui.Dummy(new Vector2f(style.FramePadding.X, 0.0f));
        ImGui.SameLine(0.0f, 0.0f);
    }

    /// <summary>Render a label for the next item, on either the left or the right.</summary>
    public static void ItemLabel(ReadOnlySpan<byte> title, Color? color = null, bool isLeft = true)
    {
        using var window = ImGui.GetCurrentWindow();
        using var style = ImGui.GetStyle();

        var lineStart = ImGui.GetCursorScreenPos();
        var fullWidth = ImGui.GetContentRegionAvail().X;
        var itemWidth = ImGui.CalcItemWidth() + style.ItemSpacing.X;
        var textSize = ImGui.CalcTextSize(title);

        var textRect = new Rect(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos());
        if (!isLeft)
            textRect.Min.X += itemWidth;
        textRect.Max = textRect.Min;
        textRect.Max.X += fullWidth - itemWidth;
        textRect.Max.Y += textSize.Y;

        ImGui.SetCursorScreenPos(textRect.Min);

        ImGui.AlignTextToFramePadding();
        var textBaseOffset = window.DcCurrLineTextBaseOffset;
        textRect.Min.Y += textBaseOffset;
        textRect.Max.Y += textBaseOffset;

        ImGui.ItemSize(in textRect);
        if (ImGui.ItemAdd(in textRect, window.GetID(title)))
        {
            using var colorGuard = new ColorScopeGuard(
                ImGuiCol.ImGuiColText,
                color.GetValueOrDefault(Color.Black).ToUInt(),
                color.HasValue);
            using var drawList = ImGui.GetWindowDrawList();
            ImGui.RenderTextEllipsis(
                drawList,
                textRect.Min,
                textRect.Max,
                textRect.Max.X,
                title,
                textSize);

            if (textRect.Size.X < textSize.X && ImGui.IsItemHovered())
                ImGui.SetTooltip(title);
        }

        if (isLeft)
        {
            ImGui.SetCursorScreenPos(textRect.Max - new Vector2f(0.0f, textSize.Y + textBaseOffset));
            ImGui.SameLine();
        }
        else
            ImGui.SetCursorScreenPos(lineStart);
    }

    /// <summary>Render an item label inside an ID scope of the same name.</summary>
    public readonly struct ItemLabelScopeGuard : IDisposable
    {
        public ItemLabelScopeGuard(ReadOnlySpan<byte> title, Color? color = null, bool isLeft = true)
        {
            _idScope = new IdScopeGuard(title);
            ItemLabel(title, color, isLeft);
        }

        public void Dispose() => _idScope.Dispose();

        private readonly IdScopeGuard _idScope;
    }

    /// <summary>Return recommended color for item label.</summary>
    public static Color GetItemLabelColor(bool isUndefined, bool defaultValue)
    {
        using var style = ImGui.GetStyle();
        using var colors = style.Colors;
        if (isUndefined)
            return new Color(colors[(int)ImGuiCol.ImGuiColTextDisabled]);
        else if (defaultValue)
            return new Color(0.85f, 0.85f, 0.85f, 1.0f);
        else
            return new Color(1.0f, 1.0f, 0.75f, 1.0f);
    }

    /// <summary>Return recommended color for item background.</summary>
    public static Color GetItemBackgroundColor(bool isUndefined)
    {
        using var style = ImGui.GetStyle();
        using var colors = style.Colors;
        if (isUndefined)
            return new Color(0.09f, 0.09f, 0.09f, 1.0f);
        else
            return new Color(colors[(int)ImGuiCol.ImGuiColFrameBg]);
    }

    /// <summary>Underline previously rendered item (usually text).</summary>
    public static void Underline(Color color)
    {
        var minimum = ImGui.GetItemRectMin();
        var maximum = ImGui.GetItemRectMax();
        using var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(new Vector2f(minimum.X, maximum.Y), maximum, color.ToUInt(), 1.0f);
    }

    /// <summary>Render clickable URL text.</summary>
    public static void TextURL(ReadOnlySpan<byte> label, ReadOnlySpan<byte> url)
    {
        using var style = ImGui.GetStyle();
        using var colors = style.Colors;

        ImGui.TextUnformatted(label);
        Underline(new Color(colors[(int)ImGuiCol.ImGuiColText]));

        var isHovered = ImGui.IsItemHovered();
        var isOpened = isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonLeft);

        if (isHovered)
        {
            ImGui.SetTooltip(url);
            ImGui.SetMouseCursor(ImGuiMouseCursor.ImGuiMouseCursorHand);
        }

        if (isOpened)
            ProcessUtils.OpenURL(Encoding.UTF8.GetString(url));
    }

    /// <summary>Fit content into area.</summary>
    public static Vector2f FitContent(Vector2f contentArea, Vector2f originalSize)
    {
        const float epsilon = 1.1920929e-7f;
        if (contentArea.X <= epsilon || contentArea.Y <= epsilon
            || originalSize.X <= epsilon || originalSize.Y <= epsilon)
        {
            return Vector2f.Zero;
        }
        var contentAspect = contentArea.X / contentArea.Y;
        var imageAspect = originalSize.X / originalSize.Y;
        if (contentAspect > imageAspect)
            return new Vector2f(contentArea.Y * imageAspect, contentArea.Y);
        else
            return new Vector2f(contentArea.X, contentArea.X / imageAspect);
    }

    /// <summary>Render Texture2D as ImGui item.</summary>
    public static void Image(Texture2D texture, ImTextureRef textureReference, Vector2f size)
    {
        var systemUI = Context.Instance.GetSubsystem<global::RedBolt.DearImGui.DearImGui>()!;
        systemUI.ReferenceTexture(texture);

        var textureView = texture.GetRawTexture().GetHandles().ShaderResourceView!;
        textureReference.TexID = (ulong)(nuint)textureView.Handle;
        ImGui.Image(textureReference, size);
    }

    /// <summary>Render Texture2D as ImGui item.</summary>
    public static void ImageItem(Texture2D texture, ImTextureRef textureReference, Vector2f size)
    {
        using var window = ImGui.GetCurrentWindow();
        var id = window.GetID(RuntimeHelpers.GetHashCode(texture));
        var cursorPosition = ImGui.GetCursorScreenPos();
        var boundingBox = new Rect(cursorPosition, cursorPosition + size);
        Image(texture, textureReference, size);
        ImGui.ItemAdd(in boundingBox, id);
    }

    /// <summary>Render Texture2D as an ImGui button.</summary>
    public static bool ImageButton(Texture2D texture, ImTextureRef textureReference, Vector2f size)
    {
        return ImageButton(texture, textureReference, size, Vector2f.Zero, Vector2f.One, -1, Vector4f.Zero, Vector4f.One);
    }

    /// <summary>Render Texture2D as an ImGui button.</summary>
    public static bool ImageButton(Texture2D texture, ImTextureRef textureReference, Vector2f size,
        Vector2f uv0, Vector2f uv1, int framePadding, Vector4f backgroundColor, Vector4f tintColor)
    {
        var systemUI = Context.Instance.GetSubsystem<global::RedBolt.DearImGui.DearImGui>()!;
        systemUI.ReferenceTexture(texture);

        var textureView = texture.GetRawTexture().GetHandles().ShaderResourceView!;
        textureReference.TexID = (ulong)(nuint)textureView.Handle;

        using var window = ImGui.GetCurrentWindow();
        _ = framePadding;

        ImGui.PushID(RuntimeHelpers.GetHashCode(texture));
        var id = window.GetID("#image"u8);
        ImGui.PopID();

        return ImGui.ImageButtonEx(id, textureReference, in size, in uv0, in uv1, in backgroundColor, in tintColor);
    }

    /// <summary>Parameters for rendering arbitrary variant value.</summary>
    public struct EditVariantOptions
    {
        /// <summary>Increment per pixel for scalar scrolls.</summary>
        public double StepValue = 0.01;
        /// <summary>Minimum value (for component).</summary>
        public double MinimumValue;
        /// <summary>Maximum value (for component).</summary>
        public double MaximumValue;
        /// <summary>Whether to treat Vector3 and Vector4 as color values.</summary>
        public bool IsColor;
        /// <summary>Whether to allow resize for dynamically sized containers.</summary>
        public bool IsResizeAllowed;
        /// <summary>Whether to allow element type changes for containers.</summary>
        public bool IsTypeChangeAllowed;
        /// <summary>Whether to treat integer as bitmask.</summary>
        public bool IsBitmask;
        /// <summary>Whether to extract elements metadata dynamically from the inspected StringVariantMap itself.</summary>
        public bool UsesDynamicMetadata;
        /// <summary>Enum values used to convert integer to string.</summary>
        public IReadOnlyList<string>? IntToString;
        /// <summary>Allowed resource types.</summary>
        public StringVector? ResourceTypeNames;
        /// <summary>Structure array element names.</summary>
        public StringVector? SizedStructVectorElementNames;

        public EditVariantOptions()
        {
        }

        public EditVariantOptions AsColor()
        {
            IsColor = true;
            return this;
        }

        public EditVariantOptions AsBitmask()
        {
            IsBitmask = true;
            return this;
        }

        public EditVariantOptions Range(double min, double max)
        {
            MinimumValue = min;
            MaximumValue = max;
            return this;
        }

        public EditVariantOptions Step(double step)
        {
            StepValue = step;
            return this;
        }

        public EditVariantOptions Enum(IReadOnlyList<string> values)
        {
            IntToString = values;
            return this;
        }

        public EditVariantOptions ResourceTypes(StringVector types)
        {
            ResourceTypeNames = types;
            return this;
        }

        public EditVariantOptions SizedStructVector(StringVector names)
        {
            SizedStructVectorElementNames = names;
            return this;
        }

        public EditVariantOptions AllowResize()
        {
            IsResizeAllowed = true;
            return this;
        }

        public EditVariantOptions AllowTypeChange()
        {
            IsTypeChangeAllowed = true;
            return this;
        }

        public EditVariantOptions DynamicMetadata()
        {
            UsesDynamicMetadata = true;
            return this;
        }
    }
}
