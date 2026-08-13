// Copyright (c) 2025-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.CSG;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Base class for CSG components with common model and material handling.</summary>
/// <remarks>This is an abstract base class and should not be instantiated directly.</remarks>
[Reflected(Factory = false)]
public abstract partial class CsgComponent : Component
{
    private static object GetEnabledAttributeDefault() => true;
    private static object GetModelAttributeDefault() => new ResourceRef(ObjectReflection<Model>.TypeId);
    private static object GetMaterialsAttributeDefault() => new ResourceRefList(ObjectReflection<Material>.TypeId);

    public CsgComponent()
    {
    }

    [Serialized("Is Enabled", DefaultSource = nameof(GetEnabledAttributeDefault), Setter = nameof(SetEnabled))]
    private bool EnabledAttribute => Enabled;

    [Serialized("Model", DefaultSource = nameof(GetModelAttributeDefault), Setter = nameof(SetModel))]
    public ResourceRef GetModelAttr() => _modelAttr;

    public virtual void SetModel(ResourceRef value)
    {
        if (_modelAttr == value)
            return;

        _modelAttr = value;
        OnModelChanged();
    }

    [Serialized("Materials", DefaultSource = nameof(GetMaterialsAttributeDefault), Setter = nameof(SetMaterials))]
    public virtual ResourceRefList GetMaterialsAttr()
    {
        return _materialsAttr;
    }

    public virtual void SetMaterials(ResourceRefList value)
    {
        if (_materialsAttr == value)
            return;

        _materialsAttr = value.Clone();
        OnMaterialsChanged();
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        var wireColor = new Color(1.0f, 0.85f, 0.0f, 0.2f);
        DrawDebugGeometryInternal(debug, depthTest, GetDebugModelAttr(), wireColor);
    }

    protected void DrawDebugGeometryInternal(DebugRenderer? debug, bool depthTest, ResourceRef modelAttr,
        Color wireColor)
    {
        var node = TryGetNode();
        if (debug is null || node is null)
            return;

        var modelName = modelAttr.Name;
        if (string.IsNullOrEmpty(modelName))
            return;

        var cache = GetSubsystem<ResourceCache>();
        var model = cache.GetResourceOrNull<Model>(modelName);
        if (model is null)
            return;

        var numGeometries = model.GetNumGeometries();
        for (var geometryIndex = 0u; geometryIndex < numGeometries; ++geometryIndex)
        {
            var geometry = model.GetGeometry(geometryIndex, 0);
            if (geometry is null)
                continue;

            var indexBuffer = geometry.IndexBuffer;
            if (indexBuffer is null || indexBuffer.GetShadowData().Length == 0)
                continue;

            var vertexBuffers = geometry.GetVertexBuffers();
            foreach (var vertexBuffer in vertexBuffers)
            {
                if (vertexBuffer is null || vertexBuffer.GetShadowData().Length == 0)
                    continue;

                debug.AddTriangleMesh(vertexBuffer.GetShadowData(), vertexBuffer.GetVertexSize(),
                    geometry.GetVertexStart(), indexBuffer.GetShadowData(), indexBuffer.GetIndexSize(),
                    geometry.GetIndexStart(), geometry.GetIndexCount(), node.GetWorldTransform(), wireColor, depthTest);
            }
        }
    }

    protected abstract ResourceRef GetDebugModelAttr();

    protected virtual void OnModelChanged()
    {
        var numGeometries = 0;

        var modelName = _modelAttr.Name;
        if (!string.IsNullOrEmpty(modelName))
        {
            var cache = GetSubsystem<ResourceCache>();
            var model = cache.GetResourceOrNull<Model>(modelName);
            if (model is not null)
                numGeometries = checked((int)model.GetNumGeometries());
            else
                Log.GetLogger().LogWarning("CsgComponent: Could not find Model resource '{ModelName}'", modelName);
        }

        while (_materialsAttr.Names.Count < numGeometries)
            _materialsAttr.Names.Add(string.Empty);
        while (_materialsAttr.Names.Count > numGeometries)
            _materialsAttr.Names.RemoveAt(_materialsAttr.Names.Count - 1);
    }

    protected virtual void OnMaterialsChanged()
    {
    }

    private void AddBrush()
    {
        var node = TryGetNode();
        if (node is null)
            return;

        var child = node.CreateChild("Brush");
        child.CreateComponent<CsgBrush>();
    }

    private ResourceRef _modelAttr = new(ObjectReflection<Model>.TypeId);
    private ResourceRefList _materialsAttr = new(ObjectReflection<Material>.TypeId);
}
