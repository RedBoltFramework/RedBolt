//
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

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;
using RedBolt.Utility;

public static class StandardSerializableHooks
{
    public static void Register(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _state ??= new State();
        _state.SetContext(context);

        SerializableInspectorWidget.RegisterAttributeHook<AnimatedModel>(
            "Morphs", _state.RenderMorphs);
        SerializableInspectorWidget.RegisterAttributeHook<RenderPipeline>(
            "Render Passes", _state.RenderRenderPasses);
        SerializableInspectorWidget.RegisterObjectHook<Camera>(
            ObjectHookType.Append, _state.RenderCameraPreview);
    }

    private sealed class State
    {
        private static readonly StringHash CameraHookWidgetVariable = "Camera_Hook_Widget";

        public void SetContext(Context context)
        {
            if (_context.TryGetTarget(out var previousContext)
                && previousContext is not null
                && !ReferenceEquals(previousContext, context))
            {
                if (_widget.TryGetTarget(out var previousWidget) && previousWidget is not null)
                    previousWidget.Dispose();
                previousContext.SetGlobalVar(CameraHookWidgetVariable, default);
                _widget.SetTarget(null!);
            }
            _context.SetTarget(context);
        }

        public bool RenderMorphs(in AttributeHookContext context, ref Variant boxedValue)
        {
            if (boxedValue.Type != VariantType.Buffer)
                return false;

            var value = boxedValue.GetBuffer();

            _attributeName.Set(context.Info!.Name);
            Widgets.ItemLabel(
                _attributeName.ContentBytes,
                Widgets.GetItemLabelColor(context.IsUndefined, context.IsDefaultValue));
            using var idScope = new IdScopeGuard((int)VariantType.Buffer);

            if (value.Length != 0)
                ImGui.NewLine();

            var modified = false;
            for (var morphIndex = 0; morphIndex < value.Length; ++morphIndex)
            {
                using var elementIdScope = new IdScopeGuard(morphIndex);
                _elementLabel.FormatInPlace($"> Morph #{morphIndex}");
                Widgets.ItemLabel(_elementLabel.ContentBytes);

                var weight = value[morphIndex] / 255.0f;
                modified |= ImGui.DragFloat(
                    ""u8, ref weight, 1.0f / 255.0f, 0.0f, 1.0f, "%.3f"u8);
                value[morphIndex] = (byte)MathF.Round(
                    weight * 255.0f, MidpointRounding.AwayFromZero);
            }

            if (value.Length == 0)
                ImGui.NewLine();

            return modified;
        }

        public bool RenderRenderPasses(in AttributeHookContext context, ref Variant boxedValue)
        {
            if (boxedValue.Type != VariantType.VariantVector)
                return false;

            var value = boxedValue.GetVariantVector();

            _attributeName.Set(context.Info!.Name);
            Widgets.ItemLabel(
                _attributeName.ContentBytes,
                Widgets.GetItemLabelColor(context.IsUndefined, context.IsDefaultValue));
            using var idScope = new IdScopeGuard((int)VariantType.Buffer);

            if (value.Count != 0)
                ImGui.NewLine();

            ImGui.Indent();
            var modified = false;
            for (var passIndex = 0; passIndex < value.Count / 2; ++passIndex)
            {
                using var elementIdScope = new IdScopeGuard(passIndex);

                _passName.Set(value[passIndex * 2].GetString());
                var enabled = value[passIndex * 2 + 1].GetBool();
                modified |= ImGui.Checkbox(_passName.ContentBytes, ref enabled);
                using var replacement = new Variant(enabled);
                value[passIndex * 2 + 1] = replacement;
            }
            ImGui.Unindent();

            if (value.Count == 0)
                ImGui.NewLine();

            return modified;
        }

        public void RenderCameraPreview(
            IReadOnlyList<WeakReference<Serializable>?> objects)
        {
            if (objects.Count != 1)
                return;

            if (objects[0] is null
                || !objects[0]!.TryGetTarget(out var serializable)
                || serializable is not Camera camera
                || camera.Scene is not { } scene
                || !_context.TryGetTarget(out var context)
                || context is null)
            {
                return;
            }

            var hasWidget = _widget.TryGetTarget(out var widget) && widget is not null;
            if (!hasWidget || !ReferenceEquals(widget!.Scene, scene))
            {
                widget?.Dispose();
                widget = new SceneRendererToTexture(scene);
                context.SetGlobalVar(
                    CameraHookWidgetVariable, Variant.MakeCustomValue(widget));
                _widget.SetTarget(widget);
            }

            widget!.SetActive(true);

            widget.Camera!.CopyAttributes(camera);
            widget.Camera.SetDrawDebugGeometry(false);
            var reviewNode = widget.CameraNode!;
            var cameraNode = camera.Node;
            reviewNode.CopyAttributes(cameraNode);
            reviewNode.SetWorldTransform(cameraNode.GetWorldTransform());

            context.GetSubsystem<WorkQueue>()!
                .PostDelayedTaskForMainThread(() => widget.SetActive(false));

            var availableWidth = ImGui.GetContentRegionAvail().X;
            const float defaultAspectRatio = 16.0f / 9.0f;
            var aspectRatio = camera.GetAutoAspectRatio()
                ? defaultAspectRatio
                : camera.GetAspectRatio();
            var textureSize = new Vector2f(
                availableWidth, availableWidth / aspectRatio).ToIntVector2();

            widget.SetTextureSize(textureSize);
            widget.Update();

            var sceneTexture = widget.Texture;
            Widgets.ImageItem(
                sceneTexture,
                _textureReference,
                new Vector2f(sceneTexture.Width, sceneTexture.Height));
        }

        private readonly WeakReference<Context> _context = new(null!);
        private readonly WeakReference<SceneRendererToTexture> _widget = new(null!);
        private readonly CString _attributeName = new();
        private readonly CString _elementLabel = new();
        private readonly CString _passName = new();
        private readonly ImTextureRef _textureReference = new();
    }

    private static State? _state;
}
