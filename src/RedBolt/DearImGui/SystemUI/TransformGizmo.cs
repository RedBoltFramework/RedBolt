// Copyright (c) 2017-2023 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System.Runtime.InteropServices;

using ImGuizmo;
using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Gizmo manipulation mode.</summary>
public enum TransformGizmoOperation
{
    None,
    Translate,
    Rotate,
    Scale,
}

/// <summary>Gizmo manipulation axes.</summary>
[Flags]
public enum TransformGizmoAxis
{
    None = 0,
    X = 1 << 0,
    Y = 1 << 1,
    Z = 1 << 2,
    Screen = 1 << 3,
    Universal = 1 << 4,
}

/// <summary>Utility class for gizmo manipulation. It's okay to recreate this class on every frame.</summary>
public class TransformGizmo
{
    /// <summary>Setup gizmo for main viewport.</summary>
    public TransformGizmo(Camera camera)
        : this(camera, true, GetMainViewportRect())
    {
    }

    /// <summary>Setup gizmo for current window rectangle.</summary>
    public TransformGizmo(Camera camera, Rect viewportRect)
        : this(camera, false, viewportRect)
    {
    }

    private TransformGizmo(Camera camera, bool isMainViewport, Rect viewportRect)
    {
        _camera = camera;
        _internalViewMatrix = camera.GetView().Matrix4.Transpose;
        _internalProjMatrix = camera.GetProjection().Transpose;
        _isMainViewport = isMainViewport;
        _viewportRect = viewportRect;
    }

    /// <summary>Manipulate transform. Returns delta matrix in world space.</summary>
    public Matrix4? ManipulateTransform(ref Matrix4 transform, TransformGizmoOperation operation,
        TransformGizmoAxis axes, bool local, Vector3f snap)
    {
        if (operation == TransformGizmoOperation.None)
            return null;

        PrepareToManipulate();

        if (!ImGui.IsUsing())
            InternalTransformMatrix = transform.Transpose;

        var internalOperation = GetInternalOperation(operation, axes);
        var mode = local ? Mode.Local : Mode.World;
        var snapVector = snap * Vector3f.One;
        ReadOnlySpan<float> snapData = snap != Vector3f.Zero ? snapVector.Data() : [];

        var delta = new Matrix4();
        ImGui.Manipulate(
            _internalViewMatrix.Data,
            _internalProjMatrix.Data,
            internalOperation,
            mode,
            MemoryMarshal.CreateSpan(ref InternalTransformMatrix.M00, 16),
            MemoryMarshal.CreateSpan(ref delta.M00, 16),
            snapData,
            ReadOnlySpan<float>.Empty,
            ReadOnlySpan<float>.Empty);
        transform = InternalTransformMatrix.Transpose;

        if (!ImGui.IsUsing())
            return null;

        return delta.Transpose;
    }

    /// <summary>Manipulate position. Returns delta position in world space.</summary>
    public Vector3f? ManipulatePosition(Matrix4 transform, TransformGizmoAxis axes, bool local, Vector3f snap)
    {
        var delta = ManipulateTransform(ref transform, TransformGizmoOperation.Translate, axes, local, snap);
        return delta is not null ? new Matrix3x4(delta.Value).Translation : null;
    }

    /// <summary>Manipulate rotation. Returns delta rotation in world space.</summary>
    public Quaternion? ManipulateRotation(Matrix4 transform, TransformGizmoAxis axes, bool local, float snap)
    {
        var snapVector = snap * Vector3f.One;
        var delta = ManipulateTransform(ref transform, TransformGizmoOperation.Rotate, axes, local, snapVector);
        return delta is not null ? new Matrix3x4(delta.Value).Rotation : null;
    }

    /// <summary>Manipulate scale. Returns multiplicative delta scale in local space.</summary>
    public Vector3f? ManipulateScale(Matrix4 transform, TransformGizmoAxis axes, bool local, float snap)
    {
        var snapVector = snap * Vector3f.One;
        var delta = ManipulateTransform(ref transform, TransformGizmoOperation.Scale, axes, local, snapVector);
        return delta is not null ? new Matrix3x4(delta.Value).SignedScale(new Matrix3()) : null;
    }

    private static Rect GetMainViewportRect()
    {
        var viewport = ImGui.GetMainViewport();
        return new Rect(viewport.Pos, viewport.Pos + viewport.Size);
    }

    private static Operation GetInternalOperation(TransformGizmoOperation operation, TransformGizmoAxis axes)
    {
        var result = (Operation)0;
        switch (operation)
        {
        case TransformGizmoOperation.Translate:
            if ((axes & TransformGizmoAxis.X) != 0)
                result |= Operation.TranslateX;
            if ((axes & TransformGizmoAxis.Y) != 0)
                result |= Operation.TranslateY;
            if ((axes & TransformGizmoAxis.Z) != 0)
                result |= Operation.TranslateZ;
            break;

        case TransformGizmoOperation.Rotate:
            if ((axes & TransformGizmoAxis.X) != 0)
                result |= Operation.RotateX;
            if ((axes & TransformGizmoAxis.Y) != 0)
                result |= Operation.RotateY;
            if ((axes & TransformGizmoAxis.Z) != 0)
                result |= Operation.RotateZ;
            if ((axes & TransformGizmoAxis.Screen) != 0)
                result |= Operation.RotateScreen;
            break;

        case TransformGizmoOperation.Scale:
            if ((axes & TransformGizmoAxis.Universal) != 0)
                result |= Operation.ScaleXu | Operation.ScaleYu | Operation.ScaleZu;
            else
            {
                if ((axes & TransformGizmoAxis.X) != 0)
                    result |= Operation.ScaleX;
                if ((axes & TransformGizmoAxis.Y) != 0)
                    result |= Operation.ScaleY;
                if ((axes & TransformGizmoAxis.Z) != 0)
                    result |= Operation.ScaleZ;
            }
            break;

        default:
            System.Diagnostics.Debug.Fail("Unexpected transform gizmo operation");
            result = Operation.Translate;
            break;
        }

        if (result == 0)
        {
            System.Diagnostics.Debug.Fail("Transform gizmo operation has no axes");
            result = Operation.Translate;
        }

        return result;
    }

    private void PrepareToManipulate()
    {
        var position = _viewportRect.Min;
        var size = _viewportRect.Size;
        ImGui.SetRect(position.X, position.Y, size.X, size.Y);

        if (_isMainViewport)
            ImGui.SetDrawlist(ImGui.GetBackgroundDrawList());
        else
            ImGui.SetDrawlist();

        ImGui.SetOrthographic(_camera.IsOrthographic());
    }

    private readonly Camera _camera;
    private readonly Matrix4 _internalViewMatrix;
    private readonly Matrix4 _internalProjMatrix;

    private readonly bool _isMainViewport;
    private readonly Rect _viewportRect;

    private static Matrix4 InternalTransformMatrix;
}

/// <summary>Gizmo for manipulating a set of scene nodes.</summary>
public class TransformNodesGizmo : IDisposable
{
    public event Action<Node, Transform>? OnNodeTransformChanged;

    public TransformNodesGizmo()
    {
    }

    public TransformNodesGizmo(Node? activeNode, IEnumerable<WeakReference<Node>> nodes)
    {
        _activeNode = activeNode is not null ? new WeakReference<Node>(activeNode) : null;
        _nodes.AddRange(nodes);
    }

    public TransformNodesGizmo(Node activeNode)
    {
        _activeNode = new WeakReference<Node>(activeNode);
        _nodes.Add(new WeakReference<Node>(activeNode));
    }

    public void Dispose()
    {
        OnNodeTransformChanged = null;
        _nodes.Clear();
        _activeNode = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>Manipulate nodes.</summary>
    public bool Manipulate(TransformGizmo gizmo, TransformGizmoOperation operation, TransformGizmoAxis axes,
        bool local, bool pivoted, Vector3f snap)
    {
        return operation switch
        {
            TransformGizmoOperation.Translate => ManipulatePosition(gizmo, axes, local, pivoted, snap),
            TransformGizmoOperation.Rotate => ManipulateRotation(gizmo, axes, local, pivoted, snap),
            TransformGizmoOperation.Scale => ManipulateScale(gizmo, axes, local, pivoted, snap),
            _ => false,
        };
    }

    private Matrix4 GetGizmoTransform(bool pivoted)
    {
        if (pivoted && _activeNode is not null && _activeNode.TryGetTarget(out var activeNode))
            return activeNode.GetWorldTransform().Matrix4;
        if (_nodes.Count == 1)
        {
            _nodes[0].TryGetTarget(out var node);
            return node!.GetWorldTransform().Matrix4;
        }

        var centerPosition = Vector3f.Zero;
        foreach (var nodeReference in _nodes)
        {
            nodeReference.TryGetTarget(out var node);
            centerPosition += node!.GetWorldPosition();
        }
        centerPosition /= _nodes.Count;
        return new Matrix3x4(centerPosition, Quaternion.IDENTITY, Vector3f.One).Matrix4;
    }

    private bool ManipulatePosition(
        TransformGizmo gizmo, TransformGizmoAxis axes, bool local, bool pivoted, Vector3f snap)
    {
        var delta = gizmo.ManipulatePosition(GetGizmoTransform(pivoted), axes, local, snap);
        if (delta is null)
            return false;

        if (delta == Vector3f.Zero)
            return true;

        foreach (var nodeReference in _nodes)
        {
            if (nodeReference.TryGetTarget(out var node))
            {
                var oldTransform = node.GetTransform();
                node.Translate(delta.Value, TransformSpace.World);
                OnNodeTransformChanged?.Invoke(node, oldTransform);
            }
        }

        return true;
    }

    private bool ManipulateRotation(
        TransformGizmo gizmo, TransformGizmoAxis axes, bool local, bool pivoted, Vector3f snap)
    {
        var anchorTransform = GetGizmoTransform(pivoted);
        var delta = gizmo.ManipulateRotation(anchorTransform, axes, local, snap.X);
        if (delta is null)
            return false;

        if (delta == Quaternion.IDENTITY)
            return true;

        foreach (var nodeReference in _nodes)
        {
            if (nodeReference.TryGetTarget(out var node))
            {
                var oldTransform = node.GetTransform();
                if (pivoted)
                    node.Rotate(delta.Value, TransformSpace.World);
                else
                    node.RotateAround(anchorTransform.Translation, delta.Value, TransformSpace.World);
                OnNodeTransformChanged?.Invoke(node, oldTransform);
            }
        }

        return true;
    }

    private bool ManipulateScale(
        TransformGizmo gizmo, TransformGizmoAxis axes, bool local, bool pivoted, Vector3f snap)
    {
        var anchorTransform = GetGizmoTransform(pivoted);
        var delta = gizmo.ManipulateScale(anchorTransform, axes, local, snap.X);
        if (delta is null)
            return false;

        if (delta == Vector3f.One)
            return true;

        foreach (var nodeReference in _nodes)
        {
            if (nodeReference.TryGetTarget(out var node))
            {
                var oldTransform = node.GetTransform();
                if (pivoted)
                    node.ScaleBy(delta.Value);
                else
                    node.ScaleAround(anchorTransform.Translation, delta.Value, TransformSpace.World);
                OnNodeTransformChanged?.Invoke(node, oldTransform);
            }
        }

        return true;
    }

    private WeakReference<Node>? _activeNode;
    private readonly List<WeakReference<Node>> _nodes = [];
}
