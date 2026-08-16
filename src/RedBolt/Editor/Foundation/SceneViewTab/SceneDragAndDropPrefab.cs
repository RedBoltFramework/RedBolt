// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Diagnostics;
using System.IO;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

/// <summary>Add-on to create new nodes via drag&amp;drop.</summary>
public class SceneDragAndDropPrefab : SceneViewAddon
{
    private const float DefaultDistance = 10.0f; // TODO: Make configurable

    private struct HitResult
    {
        public bool Hit;
        public float Distance;
        public Vector3f Position;
        public Vector3f Normal;
    }

    public SceneDragAndDropPrefab(SceneViewTab owner)
        : base(owner)
    {
    }

    public override void Dispose()
    {
        _temporaryNode = null;
        _currentPage = null;
        _nodeActionBuilder = null;
        base.Dispose();
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "DragAndDropPrefab";

    public override bool IsDragDropPayloadSupported(
        SceneViewPage page, DragDropPayload? payload)
    {
        return payload is ResourceDragDropPayload { Resources.Count: > 0 } resourcePayload
            && (resourcePayload.Resources[0].HasObjectType<PrefabResource>()
                || resourcePayload.Resources[0].HasObjectType<Model>());
    }

    public override void BeginDragDrop(SceneViewPage page, DragDropPayload? payload)
    {
        var resourcePayload = payload as ResourceDragDropPayload;
        Debug.Assert(resourcePayload is not null);

        var descriptor = resourcePayload!.Resources[0];

        if (descriptor.HasObjectType<PrefabResource>())
            CreateNodeFromPrefab(page.Scene, descriptor);
        else if (descriptor.HasObjectType<Model>())
            CreateNodeFromModel(page.Scene, descriptor);
        else
            Debug.Fail("Unsupported drag-and-drop payload");

        _currentPage = new WeakReference<SceneViewPage>(page);
    }

    public override void UpdateDragDrop(DragDropPayload? payload)
    {
        if (_currentPage is null
            || !_currentPage.TryGetTarget(out var currentPage)
            || _temporaryNode is null)
        {
            return;
        }

        var hit = QueryHoveredGeometry(currentPage.Scene, currentPage.CameraRay);

        _temporaryNode.SetPosition(hit.Position);

        if (IsKeyDown(Key.Alt))
            _temporaryNode.SetRotation(new Quaternion(Vector3f.Up, hit.Normal));
        else
            _temporaryNode.SetRotation(Quaternion.IDENTITY);
    }

    public override void CompleteDragDrop(DragDropPayload? payload)
    {
        if (_temporaryNode is null)
            return;

        if (_currentPage is not null && _currentPage.TryGetTarget(out var currentPage))
        {
            currentPage.Selection.Clear();
            currentPage.Selection.SetSelected(_temporaryNode, true);
        }

        if (_owner.TryGetTarget(out var owner))
            owner.PushAction(_nodeActionBuilder!.Build(_temporaryNode)!);

        _nodeActionBuilder = null;
        _temporaryNode = null;
    }

    public override void CancelDragDrop()
    {
        _temporaryNode?.Remove();
        _nodeActionBuilder = null;
        _temporaryNode = null;
    }

    private void CreateNodeFromPrefab(Scene scene, ResourceFileDescriptor descriptor)
    {
        var cache = GetSubsystem<ResourceCache>();
        var prefabFile = cache.GetResource<PrefabResource>(descriptor.ResourceName);
        if (prefabFile is null)
            return;

        var nodePrefab = prefabFile.GetNodePrefab();

        _nodeActionBuilder = new CreateNodeActionBuilder(
            scene, nodePrefab.GetEffectiveScopeHint());
        _temporaryNode = scene.CreateChild(Path.GetFileNameWithoutExtension(descriptor.LocalName));

        var prefabReference = _temporaryNode.CreateComponent<PrefabReference>();
        prefabReference.SetPrefab(prefabFile);

        if (!nodePrefab.IsEmpty())
            nodePrefab.Node.Export(_temporaryNode);
    }

    private void CreateNodeFromModel(Scene scene, ResourceFileDescriptor descriptor)
    {
        var cache = GetSubsystem<ResourceCache>();
        var model = cache.GetResource<Model>(descriptor.ResourceName);
        var defaultMaterial = cache.GetResource<Material>("Materials/DefaultWhite.xml");
        if (model is null)
            return;

        _nodeActionBuilder = new CreateNodeActionBuilder(
            scene, AttributeScopeHint.Attribute);
        _temporaryNode = scene.CreateChild(Path.GetFileNameWithoutExtension(descriptor.LocalName));

        StaticModel staticModel;
        if (model.GetSkeleton().GetNumBones() == 0)
        {
            staticModel = _temporaryNode.CreateComponent<StaticModel>();

            // TODO: Revisit this place, physical components may be harmful for bug models.
            // Also, ConvexHull vs TriangleMesh?
            /*var rigidBody = _temporaryNode.CreateComponent<RigidBody>();
            var collisionShape = _temporaryNode.CreateComponent<CollisionShape>();
            collisionShape.SetTriangleMesh(model);*/
        }
        else
        {
            staticModel = _temporaryNode.CreateComponent<AnimatedModel>();
        }

        staticModel.SetModel(model);
        staticModel.SetCastShadows(true);
        staticModel.ApplyMaterialList();

        if (defaultMaterial is not null)
        {
            for (var i = 0u; i < staticModel.GetNumGeometries(); ++i)
            {
                if (staticModel.GetMaterial(i) is null)
                    staticModel.SetMaterial(i, defaultMaterial);
            }
        }
    }

    private HitResult QueryHoveredGeometry(Scene scene, Ray cameraRay)
    {
        var results = SceneViewFunctions.QueryGeometriesFromScene(scene, cameraRay);

        RayQueryResult? queryResult = null;
        foreach (var result in results)
        {
            var node = result.Node;
            if (node is not null
                && node.Scene is not null
                && !ReferenceEquals(node, _temporaryNode)
                && !node.IsChildOf(_temporaryNode))
            {
                queryResult = result;
                break;
            }
        }

        var distance = queryResult?.Distance ?? DefaultDistance;
        return new HitResult
        {
            Hit = queryResult is not null,
            Distance = distance,
            Position = cameraRay.Origin + cameraRay.Direction * distance,
            Normal = queryResult?.Normal ?? Vector3f.Up,
        };
    }

    private static bool IsKeyDown(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyDown(imguiKey);
    }

    private Node? _temporaryNode;
    private WeakReference<SceneViewPage>? _currentPage;
    private CreateNodeActionBuilder? _nodeActionBuilder;

#if false
    private void AssignMaterialToDrawable(
        SceneViewPage scenePage, ResourceFileDescriptor descriptor, RayQueryResult result)
    {
        var cache = GetSubsystem<ResourceCache>();
        if (cache.GetResource<Material>(descriptor.ResourceName) is { } material
            && result.Drawable is StaticModel model)
        {
            var names = new List<string>();
            for (var i = 0u; i < model.GetNumGeometries(); ++i)
                names.Add(descriptor.ResourceName);

            model.SetMaterialsAttr(new ResourceRefList(ObjectReflection<Material>.TypeId, names));
        }
    }
#endif
}
