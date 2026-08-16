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
public class SceneDragAndDropMaterial : SceneViewAddon
{
    private const string MaterialAttribute = "Material";

    private sealed class MaterialAssignment : IDisposable
    {
        public void Dispose()
        {
            NewMaterial.Dispose();
            OldMaterial.Dispose();
            Drawable = null;
        }

        public void Reset()
        {
            Dispose();
            MaterialIndex = 0;
        }

        public WeakReference<Drawable>? Drawable;
        public uint MaterialIndex;
        public Variant OldMaterial;
        public Variant NewMaterial;
    }

    public SceneDragAndDropMaterial(SceneViewTab owner)
        : base(owner)
    {
    }

    public override void Dispose()
    {
        _temporaryAssignment.Dispose();
        _material = null;
        _currentPage = null;
        base.Dispose();
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "DragAndDropMaterial";

    public override bool IsDragDropPayloadSupported(
        SceneViewPage page, DragDropPayload? payload)
    {
        return payload is ResourceDragDropPayload { Resources.Count: > 0 } resourcePayload
            && resourcePayload.Resources[0].HasObjectType<Material>();
    }

    public override void BeginDragDrop(SceneViewPage page, DragDropPayload? payload)
    {
        var resourcePayload = payload as ResourceDragDropPayload;
        Debug.Assert(resourcePayload is not null);

        var descriptor = resourcePayload!.Resources[0];

        var cache = GetSubsystem<ResourceCache>();
        _material = cache.GetResource<Material>(descriptor.ResourceName);

        _currentPage = new WeakReference<SceneViewPage>(page);
    }

    public override void UpdateDragDrop(DragDropPayload? payload)
    {
        if (_currentPage is null
            || !_currentPage.TryGetTarget(out var currentPage)
            || _material is null)
        {
            return;
        }

        var (drawable, materialIndex) =
            QueryHoveredGeometry(currentPage.Scene, currentPage.CameraRay);
        Drawable? assignedDrawable = null;
        _temporaryAssignment.Drawable?.TryGetTarget(out assignedDrawable);
        if (ReferenceEquals(assignedDrawable, drawable)
            && _temporaryAssignment.MaterialIndex == materialIndex)
        {
            return;
        }

        if (assignedDrawable is not null)
            ClearAssignment();
        if (drawable is not null)
            CreateAssignment(drawable, materialIndex);
    }

    public override void CompleteDragDrop(DragDropPayload? payload)
    {
        if (_currentPage is null
            || !_currentPage.TryGetTarget(out _)
            || _material is null)
        {
            return;
        }

        if (_temporaryAssignment.Drawable is not null
            && _temporaryAssignment.Drawable.TryGetTarget(out var drawable)
            && _owner.TryGetTarget(out var owner))
        {
            var scene = drawable.Scene!;
            Component[] components = [drawable];
            using var oldValues = new VariantVector { _temporaryAssignment.OldMaterial };
            using var newValues = new VariantVector { _temporaryAssignment.NewMaterial };
            owner.PushAction(new ChangeComponentAttributesAction(
                scene, MaterialAttribute, components, oldValues, newValues));
        }
        _temporaryAssignment.Reset();
    }

    public override void CancelDragDrop()
    {
        ClearAssignment();
        _material = null;
    }

    private void ClearAssignment()
    {
        if (_temporaryAssignment.Drawable is not null
            && _temporaryAssignment.Drawable.TryGetTarget(out var drawable))
        {
            drawable.SetAttribute(MaterialAttribute, _temporaryAssignment.OldMaterial);
        }
        _temporaryAssignment.Drawable = null;
    }

    private void CreateAssignment(Drawable drawable, uint materialIndex)
    {
        var materialRef = new ResourceRef(
            ObjectReflection<Material>.TypeId, _material!.Name);

        _temporaryAssignment.Drawable = new WeakReference<Drawable>(drawable);
        _temporaryAssignment.MaterialIndex = materialIndex;
        _temporaryAssignment.OldMaterial.Dispose();
        _temporaryAssignment.OldMaterial = drawable.GetAttribute(MaterialAttribute);
        _temporaryAssignment.NewMaterial.Dispose();

        if (_temporaryAssignment.OldMaterial.Type == VariantType.ResourceRefList)
        {
            var list = _temporaryAssignment.OldMaterial.GetResourceRefList().Clone();
            if (materialIndex < list.Names.Count)
                list.Names[checked((int)materialIndex)] = materialRef.Name;
            _temporaryAssignment.NewMaterial = new Variant(list);
        }
        else
        {
            _temporaryAssignment.NewMaterial = new Variant(materialRef);
        }

        drawable.SetAttribute(MaterialAttribute, _temporaryAssignment.NewMaterial);
    }

    private (Drawable? Drawable, uint MaterialIndex) QueryHoveredGeometry(
        Scene scene, Ray cameraRay)
    {
        var results = SceneViewFunctions.QueryGeometriesFromScene(scene, cameraRay);

        if (results.Count != 0 && results[0].Drawable is { } drawable)
        {
            using var material = drawable.GetAttribute(MaterialAttribute);

            if (material.Type == VariantType.ResourceRef)
                return (drawable, 0u);
            if (material.Type == VariantType.ResourceRefList)
            {
                // TODO: This is a hack
                var numMaterials = unchecked((uint)material.GetResourceRefList().Names.Count);
                var materialIndex = Math.Min(
                    results[0].SubObject, Math.Max(numMaterials, 1u) - 1u);
                return (drawable, materialIndex);
            }
        }
        return (null, 0u);
    }

    private WeakReference<SceneViewPage>? _currentPage;

    private Material? _material;
    private readonly MaterialAssignment _temporaryAssignment = new();
}
