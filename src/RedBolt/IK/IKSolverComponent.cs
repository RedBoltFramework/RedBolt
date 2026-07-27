// Copyright (c) 2022-2023 the RedBolt project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Scene;

namespace RedBolt.IK;

/// <summary>Base component for all IK solvers.</summary>
[Reflected(ObjectCategory.Category_IK, InheritAttributes = false)]
public abstract partial class IKSolverComponent : Component
{

    public IKSolverComponent()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public bool Initialize(IKNodeCache nodeCache)
    {
        _solverNodes.Clear();
        return InitializeNodes(nodeCache);
    }

    public void NotifyPositionsReady()
    {
        var localFrameOfReference = GetLocalFrameOfReferenceTransform();
        UpdateChainLengths(localFrameOfReference.Inverse());
    }

    public void Solve(IKSettings settings, float timeStep)
    {
        for (var i = 0; i < _solverNodes.Count; ++i)
        {
            var (node, solverNode) = _solverNodes[i];
            solverNode.StorePreviousTransform();
            solverNode.Position = node.WorldPosition;
            solverNode.Rotation = node.WorldRotation;
        }

        var frameOfReference = GetFrameOfReferenceTransform();
        SolveInternal(frameOfReference, settings, timeStep);

        for (var i = 0; i < _solverNodes.Count; ++i)
        {
            var (node, solverNode) = _solverNodes[i];
            if (solverNode.PositionDirty)
                node.SetWorldPosition(solverNode.Position);
            if (solverNode.RotationDirty)
                node.SetWorldRotation(solverNode.Rotation);
        }
    }

    public void UpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
        UpdateWorldOriginInternal(delta.ToVector3());
    }

    /// <summary>Internal. Marks chain tree as dirty.</summary>
    public void OnTreeDirty()
    {
        GetComponent<IKSolver>()?.MarkSolversDirty();
    }

    /// <summary>Utility to calculate Node transform relative to parent Node without relying on world transforms.</summary>
    public static Matrix3x4 GetParentTransform(Node node, Node parent)
    {
        var result = node.GetTransformMatrix();
        while (node.Parent is not null && node.Parent != parent)
        {
            node = node.Parent;
            result = node.GetTransformMatrix() * result;
        }
        return result;
    }

    /// <summary>Implement IKSolverComponent.</summary>
    protected abstract bool InitializeNodes(IKNodeCache nodeCache);
    protected abstract void UpdateChainLengths(Transform inverseLocalFrameOfReference);
    protected abstract void SolveInternal(Transform frameOfReference, IKSettings settings, float timeStep);

    protected virtual void UpdateWorldOriginInternal(Vector3f delta)
    {
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (previousNode is not null)
            previousNode.GetComponent<IKSolver>()?.MarkSolversDirty();
        if (currentNode is not null)
            currentNode.GetComponent<IKSolver>()?.MarkSolversDirty();
    }

    /// <summary>Add node to cache by name. Return null if the node is not found.</summary>
    protected IKNode? AddSolverNode(IKNodeCache nodeCache, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var boneNode = Node.GetChild(name, true);
        if (boneNode is null)
        {
            Log.GetLogger().Error("IKSolverComponent: Bone node '{Name}' is not found", name);
            return null;
        }

        var solverNode = nodeCache.GetOrAdd(boneNode);
        _solverNodes.Add((boneNode, solverNode));
        return solverNode;
    }

    /// <summary>Add node that should be checked for existence before solving.</summary>
    protected Node? AddCheckedNode(IKNodeCache nodeCache, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var boneNode = Node.GetChild(name, true);
        if (boneNode is null)
        {
            Log.GetLogger().Error("IKSolverComponent: Bone node '{Name}' is not found", name);
            return null;
        }

        nodeCache.GetOrAdd(boneNode);
        return boneNode;
    }

    /// <summary>Find Node corresponding to solver IKNode. Suboptimal, prefer to call it on initialization only!</summary>
    protected Node? FindNode(IKNode node)
    {
        for (var i = 0; i < _solverNodes.Count; ++i)
        {
            var (sceneNode, ikNode) = _solverNodes[i];
            if (ReferenceEquals(ikNode, node))
                return sceneNode;
        }
        return null;
    }

    /// <summary>Set frame of reference Node used for calculations.</summary>
    protected void SetFrameOfReference(Node? node)
    {
        if (node is null || !(node == Node || node.IsChildOf(Node)))
        {
            Log.GetLogger().Error("IKSolverComponent has invalid frame of reference");
            return;
        }

        _frameOfReferenceNode = new WeakReference<Node>(node);
    }

    protected void SetFrameOfReference(IKNode node)
    {
        SetFrameOfReference(FindNode(node));
    }

    /// <summary>Same as SetFrameOfReference, except it accepts first child of the node.</summary>
    protected void SetParentAsFrameOfReference(IKNode childNode)
    {
        var node = FindNode(childNode);
        var parent = node?.Parent;
        SetFrameOfReference(parent);
    }

    /// <summary>Draw IK node in DebugRenderer.</summary>
    protected void DrawIKNode(DebugRenderer debug, IKNode node, bool oriented)
    {
        const float radius = 0.02f;
        var color = Color.Yellow;

        DrawNode(debug, oriented, node.Position, node.Rotation, color, radius);
    }

    /// <summary>Draw IK segment line in DebugRenderer.</summary>
    protected void DrawIKSegment(DebugRenderer debug, IKNode beginNode, IKNode endNode)
    {
        var color = Color.Yellow;

        debug.AddLine(beginNode.Position, endNode.Position, color, false);
    }

    /// <summary>Draw IK target in DebugRenderer.</summary>
    protected void DrawIKTarget(DebugRenderer debug, Vector3f position, Quaternion rotation, bool oriented)
    {
        const float radius = 0.05f;
        var color = Color.Green;

        DrawNode(debug, oriented, position, rotation, color, radius);
    }

    protected void DrawIKTarget(DebugRenderer debug, Node node, bool oriented)
    {
        DrawIKTarget(debug, node.WorldPosition, node.WorldRotation, oriented);
    }

    /// <summary>Draw direction in DebugRenderer.</summary>
    protected void DrawDirection(
        DebugRenderer debug, Vector3f position, Vector3f direction, bool markBegin = false, bool markEnd = true)
    {
        const float radius = 0.02f;
        const float length = 0.1f;
        var color = Color.Green;

        var endPosition = position + direction * length;
        if (markBegin)
            debug.AddSphere(new Sphere(position, radius), color, false);
        debug.AddLine(position, endPosition, color, false);
        if (markEnd)
            debug.AddSphere(new Sphere(endPosition, radius), color, false);
    }

    protected struct BendCalculationParams
    {
        public Quaternion ParentNodeRotation;
        public Vector3f StartPosition;
        public Vector3f TargetPosition;

        public Vector3f TargetDirectionInLocalSpace;
        public Vector3f BendDirectionInNodeSpace;
        public Vector3f BendDirectionInLocalSpace;

        public Node? BendTarget;
        public float BendTargetWeight;
    }

    protected static (Vector3f OriginalDirection, Vector3f CurrentDirection) CalculateBendDirectionsInternal(
        Transform frameOfReference, BendCalculationParams parameters)
    {
        var bendTargetWeight = parameters.BendTarget is not null ? parameters.BendTargetWeight : 0.0f;
        var bendTargetPosition = parameters.BendTarget is not null
            ? parameters.BendTarget.WorldPosition
            : Vector3f.Zero;
        var bendTargetDirection = bendTargetPosition
            - parameters.StartPosition.Lerp(parameters.TargetPosition, 0.5f);

        var chainRotation = new Quaternion(
            frameOfReference.Rotation * parameters.TargetDirectionInLocalSpace,
            parameters.TargetPosition - parameters.StartPosition);
        var originalDirection = parameters.ParentNodeRotation * parameters.BendDirectionInNodeSpace;
        var currentDirection0 = chainRotation * frameOfReference.Rotation * parameters.BendDirectionInLocalSpace;
        var currentDirection1 = bendTargetDirection.Normalized;
        var currentDirection = currentDirection0.Lerp(currentDirection1, bendTargetWeight);

        return (originalDirection, currentDirection);
    }

    protected static float GetMaxDistance(IKTrigonometricChain chain, float maxAngle)
    {
        var a = chain.GetFirstLength();
        var b = chain.GetSecondLength();
        return MathDefs.Sqrt(a * a + b * b - 2 * a * b * MathDefs.Cos(maxAngle));
    }

    private static readonly BoundingBox NodeBox = new(-1.0f, 1.0f);

    private static void DrawNode(
        DebugRenderer debug, bool oriented, Vector3f position, Quaternion rotation, Color color, float radius)
    {
        if (!oriented)
            debug.AddSphere(new Sphere(position, radius), color, false);
        else
        {
            var transform = new Matrix3x4(position, rotation, Vector3f.One * radius);
            debug.AddBoundingBox(NodeBox, transform, color, false);
        }
    }

    private Transform GetFrameOfReferenceTransform()
    {
        if (_frameOfReferenceNode is not null && _frameOfReferenceNode.TryGetTarget(out var node))
            return new Transform(node.WorldPosition, node.WorldRotation);
        return new Transform();
    }

    private Transform GetLocalFrameOfReferenceTransform()
    {
        if (_frameOfReferenceNode is null || !_frameOfReferenceNode.TryGetTarget(out var node))
            return new Transform();

        var localTransform = GetParentTransform(node, Node);
        return Transform.FromMatrix3x4(localTransform);
    }

    private readonly List<(Node Node, IKNode SolverNode)> _solverNodes = [];
    private WeakReference<Node>? _frameOfReferenceNode;
}
