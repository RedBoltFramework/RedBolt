//
// Copyright (c) 2024-2024 the rbfx project.
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

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Graphics;

/// <summary>
/// Helper component that tracks points in world space and updates camera position.
/// </summary>
[Reflected(ObjectCategory.Category_Scene)]
public partial class CameraOperator : Component
{
    private static readonly StringVector InstanceNodesStructureElementNames = new()
    {
        "Instance Count",
        "   NodeID"
    };

    private readonly List<WeakReference<Node>> _trackedNodes = [];
    private readonly VariantVector _nodeIdsAttr = [];
    private bool _nodesDirty;
    private bool _nodeIdsDirty;
    [Serialized(
        "Track Bounding Box",
        Order = 2
    )]
    private bool _boundingBoxEnabled;
    private BoundingBox _boundingBox = new();
    [Serialized(
        "Padding",
        Order = 1
    )]
    private Rect _padding = Rect.Zero;
    private readonly List<Vector3f> _points = [];

    /// <summary>
    /// Construct.
    /// </summary>
    public CameraOperator()
    {
        _boundingBox = new BoundingBox(-Vector3f.One, Vector3f.One);
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        _points.Clear();
        _nodeIdsAttr.Dispose();
        _trackedNodes.Clear();
        base.Dispose();
    }

    /// <summary>
    /// Apply attribute changes that can not be applied immediately. Called after scene load or a network update.
    /// </summary>
    public override void ApplyAttributes()
    {
        if (!_nodesDirty)
            return;

        // Remove all old nodes before searching for new
        for (var i = 0; i < _trackedNodes.Count; ++i)
        {
            if (_trackedNodes[i].TryGetTarget(out var trackedNode))
                trackedNode.RemoveListener(this);
        }

        _trackedNodes.Clear();

        var scene = Scene;
        if (scene is not null)
        {
            // The first index stores the number of IDs redundantly. This is for editing
            for (var i = 1; i < _nodeIdsAttr.Count; ++i)
            {
                var node = scene.GetNode(_nodeIdsAttr[i].GetUInt());
                if (node is not null)
                {
                    node.AddListener(this);
                    _trackedNodes.Add(new WeakReference<Node>(node));
                }
            }
        }

        _nodesDirty = false;
    }

    /// <summary>
    /// Set node IDs attribute.
    /// </summary>
    public void SetNodeIDsAttr(VariantVector value)
    {
        // Just remember the node IDs. They need to go through the SceneResolver, and we actually find the nodes during
        // ApplyAttributes()
        if (value.Count != 0)
        {
            _nodeIdsAttr.Clear();

            var index = 0;
            var numInstances = value[index++].GetUInt();
            // Prevent crash on entering negative value in the editor
            if (numInstances > (uint)MathDefs.MaxInt)
                numInstances = 0;

            _nodeIdsAttr.Add(numInstances);
            while (numInstances-- != 0)
            {
                // If vector contains less IDs than should, fill the rest with zeroes
                if (index < value.Count)
                    _nodeIdsAttr.Add(value[index++].GetUInt());
                else
                    _nodeIdsAttr.Add(0);
            }
        }
        else
        {
            _nodeIdsAttr.Clear();
            _nodeIdsAttr.Add(0);
        }

        _nodesDirty = true;
        _nodeIdsDirty = false;
    }

    /// <summary>
    /// Return node IDs attribute.
    /// </summary>
    [Serialized(
        "Tracked Nodes",
        Configure = nameof(ConfigureTrackedNodesAttribute),
        DefaultSource = nameof(GetTrackedNodesAttributeDefault),
        Mode = AttributeMode.Default | AttributeMode.NodeIdVector,
        Setter = nameof(SetNodeIDsAttr),
        Order = 5
    )]
    public VariantVector GetNodeIDsAttr()
    {
        if (_nodeIdsDirty)
            UpdateNodeIDs();

        return _nodeIdsAttr;
    }

    /// <summary>
    /// Get padding in world space units.
    /// </summary>
    public Rect GetPadding() => _padding;

    /// <summary>
    /// Set padding in world space units.
    /// </summary>
    public void SetPadding(Rect padding)
    {
        _padding = padding;
    }

    /// <summary>
    /// Set uniform padding in every direction in world space units.
    /// </summary>
    public void SetUniformPadding(float padding)
    {
        var vec4 = new Rect(padding, padding, padding, padding);
        _padding = vec4;
    }

    /// <summary>
    /// Get tracked bounding box.
    /// </summary>
    public BoundingBox GetBoundingBox() => _boundingBox;

    /// <summary>
    /// Set bounding box.
    /// </summary>
    public void SetBoundingBox(BoundingBox box)
    {
        _boundingBox = box;
    }

    /// <summary>
    /// Return whether bounding box tracking is enabled.
    /// </summary>
    public bool IsBoundingBoxTrackingEnabled() => _boundingBoxEnabled;

    /// <summary>
    /// Enable or disable bounding box tracking. Disabled by default.
    /// </summary>
    public void SetBoundingBoxTrackingEnabled(bool enable)
    {
        _boundingBoxEnabled = enable;
    }

    /// <summary>
    /// Add scene node to track.
    /// </summary>
    public void TrackNode(Node? node)
    {
        if (node is null)
            return;

        for (var i = 0; i < _trackedNodes.Count; ++i)
        {
            if (_trackedNodes[i].TryGetTarget(out var trackedNode) && ReferenceEquals(trackedNode, node))
                return;
        }

        // Add as a listener for the node, so that we know to dirty the transforms when the node moves or is
        // enabled/disabled
        node.AddListener(this);
        _trackedNodes.Add(new WeakReference<Node>(node));
    }

    /// <summary>
    /// Remove scene node from trackable nodes.
    /// </summary>
    public void RemoveTrackedNode(Node? node)
    {
        if (node is null)
            return;

        for (var i = 0; i < _trackedNodes.Count; ++i)
        {
            if (_trackedNodes[i].TryGetTarget(out var trackedNode) && ReferenceEquals(trackedNode, node))
            {
                node.RemoveListener(this);
                _trackedNodes.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>
    /// Remove all instance scene nodes.
    /// </summary>
    public void RemoveAllTrackedNodes()
    {
        for (var i = 0; i < _trackedNodes.Count; ++i)
        {
            if (_trackedNodes[i].TryGetTarget(out var trackedNode))
                trackedNode.RemoveListener(this);
        }

        _trackedNodes.Clear();
        _nodeIdsDirty = true;
    }

    /// <summary>
    /// Return number of tracked nodes.
    /// </summary>
    public uint GetNumTrackedNodes() => checked((uint)_trackedNodes.Count);

    /// <summary>
    /// Return tracked node by index.
    /// </summary>
    public Node? GetTrackedNode(uint index)
    {
        return index < _trackedNodes.Count && _trackedNodes[(int)index].TryGetTarget(out var node) ? node : null;
    }

    /// <summary>
    /// Move camera.
    /// </summary>
    public void MoveCamera()
    {
        if (TryGetNode() is null)
            return;

        var camera = Node.GetComponent<Camera>();
        if (camera is null)
            return;

        _points.Clear();
        if (_boundingBoxEnabled)
        {
            _points.Add(new Vector3f(_boundingBox.Min.X, _boundingBox.Min.Y, _boundingBox.Min.Z));
            _points.Add(new Vector3f(_boundingBox.Max.X, _boundingBox.Min.Y, _boundingBox.Min.Z));
            _points.Add(new Vector3f(_boundingBox.Min.X, _boundingBox.Max.Y, _boundingBox.Min.Z));
            _points.Add(new Vector3f(_boundingBox.Max.X, _boundingBox.Max.Y, _boundingBox.Min.Z));
            _points.Add(new Vector3f(_boundingBox.Min.X, _boundingBox.Min.Y, _boundingBox.Max.Z));
            _points.Add(new Vector3f(_boundingBox.Max.X, _boundingBox.Min.Y, _boundingBox.Max.Z));
            _points.Add(new Vector3f(_boundingBox.Min.X, _boundingBox.Max.Y, _boundingBox.Max.Z));
            _points.Add(new Vector3f(_boundingBox.Max.X, _boundingBox.Max.Y, _boundingBox.Max.Z));
        }

        if (_nodesDirty)
            ApplyAttributes();

        for (var i = 0; i < _trackedNodes.Count; ++i)
        {
            if (_trackedNodes[i].TryGetTarget(out var trackedNode))
                _points.Add(trackedNode.WorldPosition);
        }

        if (_points.Count != 0)
            FocusOn(_points, camera);
    }

    /// <summary>
    /// Update node IDs attribute from the actual nodes.
    /// </summary>
    protected void UpdateNodeIDs()
    {
        var numInstances = checked((uint)_trackedNodes.Count);

        _nodeIdsAttr.Clear();
        _nodeIdsAttr.Add(numInstances);

        for (var i = 0u; i < numInstances; ++i)
            _nodeIdsAttr.Add(_trackedNodes[(int)i].TryGetTarget(out var node) ? node.Id : 0u);

        _nodeIdsDirty = false;
    }

    /// <summary>
    /// Handle enabled/disabled state change.
    /// </summary>
    public override void OnSetEnabled()
    {
        if (IsEnabledEffective)
            MoveCamera();
    }

    /// <summary>
    /// Handle scene being assigned.
    /// </summary>
    protected internal override void OnSceneSet(SceneType? previousScene, SceneType? scene)
    {
        UnsubscribeFromEvent<SceneEvents.SceneDrawableUpdateFinishedEvent>();
        if (scene is not null)
            SubscribeToEvent<SceneEvents.SceneDrawableUpdateFinishedEvent>(scene, HandleSceneDrawableUpdateFinished);
    }

    private void HandleSceneDrawableUpdateFinished(ref SceneEvents.SceneDrawableUpdateFinishedEvent eventData)
    {
        if (!IsEnabledEffective)
            return;

        MoveCamera();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (previousNode is not null)
            previousNode.RemoveListener(this);
        if (currentNode is not null)
            currentNode.AddListener(this);
    }

    private void FocusOn(IReadOnlyList<Vector3f> points, Camera camera)
    {
        if (points.Count == 0)
        {
            Log.GetLogger().LogError("Can't focus on empty array of vertices");
            return;
        }

        if (camera is null)
        {
            Log.GetLogger().LogError("No Camera found");
            return;
        }

        if (TryGetNode() is null)
        {
            Log.GetLogger().LogError("No Node to move");
            return;
        }

        var frustum = camera.GetFrustum();
        Span<Plane> planes = stackalloc Plane[Frustum.NumFrustumPlanes];
        for (var i = 0; i < Frustum.NumFrustumPlanes; ++i)
            planes[i] = frustum.Planes[i];

        for (var i = 0; i < planes.Length; ++i)
        {
            planes[i].D = -float.MaxValue;
            for (var point = 0; point < points.Count; ++point)
                planes[i].D = MathF.Max(planes[i].D, -planes[i].Normal.DotProduct(points[point]));
        }

        planes[(int)FrustumPlane.Left].D += _padding.Left;
        planes[(int)FrustumPlane.Up].D += _padding.Top;
        planes[(int)FrustumPlane.Right].D += _padding.Right;
        planes[(int)FrustumPlane.Down].D += _padding.Bottom;

        var oldPosition = Node.WorldPosition;

        if (camera.IsOrthographic())
        {
            // Evaluate new central point.
            var left = Node.WorldToLocal(planes[(int)FrustumPlane.Left].GetPoint()).X;
            var right = Node.WorldToLocal(planes[(int)FrustumPlane.Right].GetPoint()).X;
            var up = Node.WorldToLocal(planes[(int)FrustumPlane.Up].GetPoint()).Y;
            var down = Node.WorldToLocal(planes[(int)FrustumPlane.Down].GetPoint()).Y;
            var offset = new Vector3f(left + right, up + down, 0.0f) * 0.5f;

            // Move camera back if it is too close to the closest point.
            var n = Node.WorldToLocal(planes[(int)FrustumPlane.Near].GetPoint()).Z;
            if (n < 0.0f)
                offset.Z += n;

            // Move camera node.
            var newPosition = Node.LocalToWorld(offset);
            if (!oldPosition.Equals(newPosition))
                Node.SetWorldPosition(newPosition);

            // Adjust orthoSize_ to avoid any extra padding.
            var autoAspectRatio = camera.GetAutoAspectRatio();
            var zoom = camera.GetZoom();
            var aspectRatio = camera.GetAspectRatio();
            var orthoSizeY = (up - down) * zoom;
            var orthoSizeX = (right - left) * zoom;
            var verticalOrthoSize = MathF.Max(orthoSizeX / aspectRatio, orthoSizeY);

            if (autoAspectRatio)
            {
                camera.SetOrthoSize(verticalOrthoSize);
                camera.SetAspectRatio(aspectRatio);
                camera.SetAutoAspectRatio(true);
            }
            else
                camera.SetOrthoSize(new Vector2f(verticalOrthoSize * aspectRatio, verticalOrthoSize));
        }
        else
        {
            // Evaluate focal point.
            var ray0 = planes[(int)FrustumPlane.Left].Intersect(planes[(int)FrustumPlane.Right]);
            var ray1 = planes[(int)FrustumPlane.Up].Intersect(planes[(int)FrustumPlane.Down]);
            var distance0 = frustum.Planes[(int)FrustumPlane.Near].Distance(ray0.Origin);
            var distance1 = frustum.Planes[(int)FrustumPlane.Near].Distance(ray1.Origin);
            var focalPoint = distance0 < distance1 ? ray0.ClosestPoint(ray1) : ray1.ClosestPoint(ray0);

            var n = planes[(int)FrustumPlane.Near].Distance(focalPoint) + camera.GetNearClip();
            if (n > 0.0f)
                focalPoint -= planes[(int)FrustumPlane.Near].Normal * n;

            // Move camera node.
            if (!oldPosition.Equals(focalPoint))
                Node.SetWorldPosition(focalPoint);
        }
    }

    private static object? GetIsEnabledAttributeDefault() => true;

    private static object? GetBoundingBoxMinAttributeDefault() => -Vector3f.One;

    [Serialized(
        "Bounding Box Min",
        typeof(Vector3f),
        DefaultSource = nameof(GetBoundingBoxMinAttributeDefault),
        Setter = nameof(SetSerializedBoundingBoxMin),
        Order = 3
    )]
    private object? GetSerializedBoundingBoxMin()
    {
        Serializable self = this;
        return ((CameraOperator)self)._boundingBox.Min;
    }

    private void SetSerializedBoundingBoxMin(object? value)
    {
        Serializable self = this;
        var target = (CameraOperator)self;
        var box = target._boundingBox;
        box.Min = (Vector3f)value!;
        target._boundingBox = box;
    }

    private static object? GetBoundingBoxMaxAttributeDefault() => Vector3f.One;

    [Serialized(
        "Bounding Box Max",
        typeof(Vector3f),
        DefaultSource = nameof(GetBoundingBoxMaxAttributeDefault),
        Setter = nameof(SetSerializedBoundingBoxMax),
        Order = 4
    )]
    private object? GetSerializedBoundingBoxMax()
    {
        Serializable self = this;
        return ((CameraOperator)self)._boundingBox.Max;
    }

    private void SetSerializedBoundingBoxMax(object? value)
    {
        Serializable self = this;
        var target = (CameraOperator)self;
        var box = target._boundingBox;
        box.Max = (Vector3f)value!;
        target._boundingBox = box;
    }

    private static object? GetTrackedNodesAttributeDefault() => new VariantVector();

    private static void ConfigureTrackedNodesAttribute(AttributeHandle attribute)
    {
        using var metadata = new Variant(InstanceNodesStructureElementNames);
        attribute.SetMetadata(AttributeMetadata.VectorStructElements, metadata);
    }

    private static object? GetUpdateCameraAttributeDefault() => false;

    private static void ConfigureUpdateCameraAttribute(AttributeHandle attribute)
    {
        attribute.SetMetadata(AttributeMetadata.IsAction, true);
    }

    [Serialized(
        "Update Camera",
        typeof(bool),
        Configure = nameof(ConfigureUpdateCameraAttribute),
        DefaultSource = nameof(GetUpdateCameraAttributeDefault),
        Mode = AttributeMode.Edit,
        Setter = nameof(SetSerializedUpdateCamera),
        Order = 6
    )]
    private object? GetSerializedUpdateCamera() => "Move camera to keep tracked nodes and/or bounding box in frustum";

    private void SetSerializedUpdateCamera(object? value)
    {
        Serializable self = this;
        if (value is true)
            ((CameraOperator)self).MoveCamera();
    }


    [Serialized(
        "Is Enabled",
        DefaultSource = nameof(GetIsEnabledAttributeDefault),
        Order = 0
    )]
    private bool SerializedIsEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }
}
