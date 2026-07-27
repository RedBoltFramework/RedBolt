using RedBolt;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class CameraOperatorTests
{
    [Fact]
    public void Camera_operator_stores_tracked_nodes_weakly_and_destroys_owned_ids()
    {
        var scene = new SceneType();
        var cameraOperator = new CameraOperator();
        var target = scene.CreateChild("Target");

        cameraOperator.TrackNode(target);
        cameraOperator.SetNodeIDsAttr(new VariantVector { new Variant(1u), new Variant(target.Id) });
        var nodeIds = cameraOperator.GetNodeIDsAttr();

        var trackedNodes = GetPrivateField<List<WeakReference<Node>>>(cameraOperator, "_trackedNodes");
        Assert.Single(trackedNodes);
        Assert.True(trackedNodes[0].TryGetTarget(out var resolvedTarget));
        Assert.Same(target, resolvedTarget);

        cameraOperator.Dispose();
        Assert.Empty(nodeIds);
    }

    [Fact]
    public void Camera_operator_tracks_nodes_and_refreshes_node_id_attribute()
    {
        var scene = new SceneType();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.CreateComponent<Camera>();
        var cameraOperator = cameraNode.CreateComponent<CameraOperator>();
        var target = scene.CreateChild("Target");

        cameraOperator.TrackNode(target);
        cameraOperator.TrackNode(target);

        Assert.Equal(1u, cameraOperator.GetNumTrackedNodes());
        Assert.Same(target, cameraOperator.GetTrackedNode(0));

        cameraOperator.RemoveTrackedNode(target);
        Assert.Equal(0u, cameraOperator.GetNumTrackedNodes());

        cameraOperator.RemoveAllTrackedNodes();
        Assert.Equal(0u, cameraOperator.GetNodeIDsAttr()[0].GetUInt());
    }

    [Fact]
    public void Camera_operator_resolves_node_ids_during_attribute_application()
    {
        var scene = new SceneType();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.CreateComponent<Camera>();
        var cameraOperator = cameraNode.CreateComponent<CameraOperator>();
        var target = scene.CreateChild("Target");

        cameraOperator.SetNodeIDsAttr(new VariantVector { new Variant(1u), new Variant(target.Id) });
        cameraOperator.ApplyAttributes();

        Assert.Equal(1u, cameraOperator.GetNumTrackedNodes());
        Assert.Same(target, cameraOperator.GetTrackedNode(0));
    }

    [Fact]
    public void Camera_operator_padding_and_bounding_box_state_roundtrip()
    {
        var cameraOperator = new CameraOperator();
        var padding = new Rect(1.0f, 2.0f, 3.0f, 4.0f);
        var boundingBox = new BoundingBox(new Vector3f(-2.0f, -3.0f, -4.0f), new Vector3f(2.0f, 3.0f, 4.0f));

        cameraOperator.SetPadding(padding);
        cameraOperator.SetBoundingBox(boundingBox);
        cameraOperator.SetBoundingBoxTrackingEnabled(true);

        Assert.Equal(padding, cameraOperator.GetPadding());
        Assert.Equal(boundingBox, cameraOperator.GetBoundingBox());
        Assert.True(cameraOperator.IsBoundingBoxTrackingEnabled());
    }

    [Fact]
    public void Camera_operator_registers_scene_factory_and_action_metadata()
    {
        var context = new Context();
        CameraOperator.RegisterObject(context);

        using var cameraOperator = Assert.IsType<CameraOperator>(
            context.CreateObject(ObjectReflection<CameraOperator>.TypeId));
        Assert.Equal(ObjectCategory.Category_Scene, context.GetReflection<CameraOperator>().Category);

        var trackedNodes = cameraOperator.Attributes.Single(attribute => attribute.Name == "Tracked Nodes");
        Assert.Equal(
            new StringVector
            {
                "Instance Count",
                "   NodeID"
            },
            trackedNodes.GetMetadata<StringVector>(AttributeMetadata.VectorStructElements));

        var updateCamera = cameraOperator.Attributes.Single(attribute => attribute.Name == "Update Camera");
        Assert.True(updateCamera.GetMetadata<bool>(AttributeMetadata.IsAction));
        Assert.Equal(
            "Move camera to keep tracked nodes and/or bounding box in frustum",
            updateCamera.Accessor!.GetObject(cameraOperator));
        updateCamera.Accessor!.SetObject(cameraOperator, true);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(instance));
    }
}
