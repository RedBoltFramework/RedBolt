// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Diagnostics;

using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

/// <summary>Add-on to update materials via drag&amp;drop.</summary>
public class SceneDragAndDropAnimation : SceneViewAddon
{
    public SceneDragAndDropAnimation(SceneViewTab owner)
        : base(owner)
    {
    }

    public override void Dispose()
    {
        _hoveredDrawable = null;
        _hoveredController = null;
        _animation = null;
        _currentPage = null;
        base.Dispose();
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "DragAndDropAnimation";

    public override bool IsDragDropPayloadSupported(
        SceneViewPage page, DragDropPayload? payload)
    {
        return payload is ResourceDragDropPayload { Resources.Count: > 0 } resourcePayload
            && resourcePayload.Resources[0].HasObjectType<Animation>();
    }

    public override void BeginDragDrop(SceneViewPage page, DragDropPayload? payload)
    {
        var resourcePayload = payload as ResourceDragDropPayload;
        Debug.Assert(resourcePayload is not null);

        var descriptor = resourcePayload!.Resources[0];

        var cache = GetSubsystem<ResourceCache>();
        _animation = cache.GetResource<Animation>(descriptor.ResourceName);

        _currentPage = new WeakReference<SceneViewPage>(page);
    }

    public override void UpdateDragDrop(DragDropPayload? payload)
    {
        if (_currentPage is null
            || !_currentPage.TryGetTarget(out var currentPage)
            || _animation is null)
        {
            return;
        }

        var (hoveredController, hoveredDrawable) =
            QueryHoveredController(currentPage.Scene, currentPage.CameraRay);
        _hoveredController = hoveredController is not null
            ? new WeakReference<AnimationController>(hoveredController)
            : null;
        _hoveredDrawable = hoveredDrawable is not null
            ? new WeakReference<Drawable>(hoveredDrawable)
            : null;

        if (hoveredDrawable?.Scene?.GetComponent<DebugRenderer>() is { } debugRenderer)
            hoveredDrawable.DrawDebugGeometry(debugRenderer, false);
    }

    public override void CompleteDragDrop(DragDropPayload? payload)
    {
        if (_currentPage is null
            || !_currentPage.TryGetTarget(out _)
            || _animation is null
            || _hoveredController is null
            || !_hoveredController.TryGetTarget(out var hoveredController))
        {
            return;
        }

        using var oldValue = hoveredController.GetAnimationsAttr();
        using (var emptyValue = new VariantVector())
            hoveredController.SetAnimationsAttr(emptyValue);
        hoveredController.AddAnimation(new AnimationParameters(_animation).Looped());
        using var newValue = hoveredController.GetAnimationsAttr();

        var scene = hoveredController.Scene!;
        Component[] components = [hoveredController];
        using var oldValues = new VariantVector { new(oldValue) };
        using var newValues = new VariantVector { new(newValue) };
        if (_owner.TryGetTarget(out var owner))
        {
            owner.PushAction(new ChangeComponentAttributesAction(
                scene, "Animations", components, oldValues, newValues));
        }
    }

    public override void CancelDragDrop()
    {
        _animation = null;
    }

    private (AnimationController? Controller, Drawable? Drawable) QueryHoveredController(
        Scene scene, Ray cameraRay)
    {
        var results = SceneViewFunctions.QueryGeometriesFromScene(scene, cameraRay);
        var drawable = results.Count != 0 ? results[0].Drawable : null;
        var node = drawable?.TryGetNode();
        var controller = node?.GetComponent<AnimationController>();
        return controller is not null && drawable is not null
            ? (controller, drawable)
            : (null, null);
    }

    private WeakReference<SceneViewPage>? _currentPage;

    private Animation? _animation;
    private WeakReference<AnimationController>? _hoveredController;
    private WeakReference<Drawable>? _hoveredDrawable;
}
