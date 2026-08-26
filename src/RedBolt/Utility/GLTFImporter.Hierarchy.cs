// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Tg = RedBolt.Bindings.TinyGltf;

using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;

/// <summary>Utility to process scene and node hierarchy of source GLTF asset.</summary>
/// <remarks>
/// Mirrors the scene to convert from right-handed to left-handed coordinates.
///
/// Implements simple heuristics: if no models are actually mirrored after initial mirror,
/// then GLTF exporter implemented lazy export from left-handed to right-handed coordinate system
/// by adding top-level mirroring. In this case, keep scene as is.
/// TODO: Cleanup redundant mirrors?
/// Otherwise scene is truly left-handed and deep mirroring is needed.
///
/// Converts skins to the format consumable by Urho scene.
/// </remarks>
internal sealed partial class GLTFHierarchyAnalyzer
{
    public GLTFHierarchyAnalyzer(GLTFImporterBase importerBase, GLTFBufferReader bufferReader)
    {
        _base = importerBase;
        _bufferReader = bufferReader;
        _model = _base.GetModel();
        _inlineTransform.MirrorX = importerBase.GetSettings().MirrorX;
        _inlineTransform.Scale = importerBase.GetSettings().Scale;

        ProcessMeshMorphs();
        InitializeParents();
        InitializeTrees();
        ConvertToLeftHandedCoordinates();
        PreProcessSkins();
        InitializeSkeletons();
        InitializeSkins();
        AssignSkinnedModelsToNodes();
        EnumerateUniqueMeshSkinPairs();
        AssignNamesToSkeletonRoots();
        ImportAnimations();
    }

    public InlineTransform GetInlineTransform() => _inlineTransform;
    public GLTFNode GetNode(int nodeIndex) { _base.CheckNode(nodeIndex); return _nodeByIndex[nodeIndex]; }
    public List<GLTFNode> GetRootNodes() => _rootNodes;
    public List<GLTFMeshSkinPair> GetUniqueMeshSkinPairs() => _uniqueMeshSkinPairs;
    public int GetNumMorphsInMesh(int meshIndex) { _base.CheckMesh(meshIndex); return _numMorphsInMesh[meshIndex]; }

    public int GetUniqueMeshSkin(int meshIndex, int skinIndex)
    {
        if (!_meshSkinPairs.TryGetValue((meshIndex, skinIndex), out var result))
            throw new InvalidOperationException($"Cannot find mesh #{meshIndex} with skin #{skinIndex}");
        return result;
    }

    public List<BoneView> GetSkinBones(int? skinIndex)
    {
        if (skinIndex is null)
            return EmptyBones;
        _base.CheckSkin(skinIndex.Value);
        return _skins[skinIndex.Value].CookedBones;
    }

    public GLTFSkeleton GetSkeleton(int skeletonIndex)
    {
        if (skeletonIndex >= _skeletons.Count)
            throw new InvalidOperationException($"Invalid skeleton #{skeletonIndex} is referenced");
        return _skeletons[skeletonIndex];
    }

    public GLTFAnimation GetAnimation(int animationIndex)
    {
        _base.CheckAnimation(animationIndex);
        return _animations[animationIndex];
    }

    public static bool IsChildOf(GLTFNode child, GLTFNode parent) =>
        child.Parent == parent || child.Parent is not null && IsChildOf(child.Parent, parent);

    public static List<GLTFNode> GetPathIncludingSelf(GLTFNode node)
    {
        var path = new List<GLTFNode> { node };
        var currentParent = node.Parent;
        while (currentParent is not null)
        {
            path.Add(currentParent);
            currentParent = currentParent.Parent;
        }
        path.Reverse();
        return path;
    }

    public static GLTFNode? GetCommonParent(GLTFNode lhs, GLTFNode rhs)
    {
        if (lhs.Root != rhs.Root)
            return null;
        var lhsPath = GetPathIncludingSelf(lhs);
        var rhsPath = GetPathIncludingSelf(rhs);
        for (var i = Math.Min(lhsPath.Count, rhsPath.Count) - 1; i >= 0; --i)
        {
            if (lhsPath[i] == rhsPath[i])
                return lhsPath[i];
        }
        Debug.Assert(false);
        return null;
    }

    public static void ForEachInPathExceptParent(GLTFNode child, GLTFNode parent, Action<GLTFNode> callback)
    {
        if (child == parent)
            return;
        if (!IsChildOf(child, parent))
            throw new InvalidOperationException("Invalid ForEachInPath call");
        var node = child;
        while (node != parent)
        {
            callback(node);
            node = node.Parent!;
        }
    }

    public static void ForEachChild(GLTFNode parent, Action<GLTFNode> callback)
    {
        foreach (var child in parent.Children)
        {
            callback(child);
            ForEachChild(child, callback);
        }
    }

    public static void ForEach(IEnumerable<GLTFNode> nodes, Action<GLTFNode> callback)
    {
        foreach (var node in nodes)
        {
            callback(node);
            ForEachChild(node, callback);
        }
    }

    public static void ForEachSkeletonNode(GLTFNode skeletonRoot, int skeletonIndex, Action<GLTFNode> callback)
    {
        if (skeletonRoot.SkeletonIndex != skeletonIndex)
            throw new InvalidOperationException("Invalid call to ForEachSkeletonNode");
        callback(skeletonRoot);
        foreach (var child in skeletonRoot.Children)
        {
            if (child.SkeletonIndex == skeletonIndex)
                ForEachSkeletonNode(child, skeletonIndex, callback);
        }
    }

    private void ProcessMeshMorphs()
    {
        _numMorphsInMesh.AddRange(Enumerable.Repeat(0, _model.Meshes.Count));
        for (var meshIndex = 0; meshIndex < _model.Meshes.Count; ++meshIndex)
        {
            var mesh = _model.Meshes[meshIndex];
            _numMorphsInMesh[meshIndex] = mesh.Primitives.Count == 0 ? 0 : mesh.Primitives[0].Targets.Count;
        }
    }

    private void InitializeParents()
    {
        _nodeToParent.AddRange(Enumerable.Repeat<int?>(null, _model.Nodes.Count));
        for (var nodeIndex = 0; nodeIndex < _model.Nodes.Count; ++nodeIndex)
        {
            foreach (var childIndex in _model.Nodes[nodeIndex].Children)
            {
                _base.CheckNode(childIndex);
                if (_nodeToParent[childIndex].HasValue)
                    throw new InvalidOperationException($"Node #{childIndex} has multiple parents: #{nodeIndex} and #{_nodeToParent[childIndex]}");
                _nodeToParent[childIndex] = nodeIndex;
            }
        }
    }

    private void InitializeTrees()
    {
        _nodeByIndex.AddRange(Enumerable.Repeat<GLTFNode>(null!, _model.Nodes.Count));
        for (var nodeIndex = 0; nodeIndex < _model.Nodes.Count; ++nodeIndex)
        {
            if (!_nodeToParent[nodeIndex].HasValue)
                _rootNodes.Add(ImportTree(nodeIndex, null, null));
        }
        foreach (var node in _rootNodes)
            ReadNodeProperties(node);
    }

    private GLTFNode ImportTree(int nodeIndex, GLTFNode? parent, GLTFNode? root)
    {
        _base.CheckNode(nodeIndex);
        var sourceNode = _model.Nodes[nodeIndex];
        var node = new GLTFNode();
        root ??= node;
        node.Index = nodeIndex;
        node.Root = root;
        node.Parent = parent;
        foreach (var childIndex in sourceNode.Children)
            node.Children.Add(ImportTree(childIndex, node, root));
        _nodeByIndex[nodeIndex] = node;
        return node;
    }

    private void ReadNodeProperties(GLTFNode node)
    {
        var sourceNode = _model.Nodes[node.Index];
        node.Name = sourceNode.Name.ToString();
        if (sourceNode.Mesh >= 0)
        {
            _base.CheckMesh(sourceNode.Mesh);
            node.Mesh = sourceNode.Mesh;
            var numMorphs = _numMorphsInMesh[sourceNode.Mesh];
            if (numMorphs > 0)
            {
                var morphWeights = sourceNode.Weights.Count != 0 ? sourceNode.Weights : _model.Meshes[sourceNode.Mesh].Weights;
                node.MorphWeights.AddRange(Enumerable.Repeat(0.0f, numMorphs));
                for (var i = 0; i < morphWeights.Count; ++i)
                    node.MorphWeights[i] = (float)morphWeights[i];
            }
        }
        if (sourceNode.Skin >= 0)
        {
            _base.CheckSkin(sourceNode.Skin);
            node.Skin = sourceNode.Skin;
        }
        if (sourceNode.Matrix.Count != 0)
        {
            var matrix = ReadMatrix3x4(sourceNode.Matrix);
            matrix.Decompose(out node.Position, out node.Rotation, out node.Scale);
        }
        else
        {
            if (sourceNode.Translation.Count != 0) node.Position = ReadVector3(sourceNode.Translation);
            if (sourceNode.Rotation.Count != 0) node.Rotation = ReadQuaternion(sourceNode.Rotation);
            if (sourceNode.Scale.Count != 0)
            {
                node.Scale = ReadVector3(sourceNode.Scale);
                node.Scale.X = MathDefs.SnapTo(node.Scale.X, 1.0f, MathDefs.Epsilon);
                node.Scale.Y = MathDefs.SnapTo(node.Scale.Y, 1.0f, MathDefs.Epsilon);
                node.Scale.Z = MathDefs.SnapTo(node.Scale.Z, 1.0f, MathDefs.Epsilon);
            }
        }
        foreach (var child in node.Children)
            ReadNodeProperties(child);
    }

    private void ConvertToLeftHandedCoordinates()
    {
        // If most of geometry is mirrored, we need to undo that by mirroring the scene root nodes
        var isMostlyMirrored = HasMostlyMirroredGeometry();
        var needDeepMirror = isMostlyMirrored == _inlineTransform.MirrorX;
        var needShallowMirror = isMostlyMirrored;
        _inlineTransform.MirrorX = needDeepMirror;
        if (needShallowMirror)
        {
            // If the scene already had negative scale and we reverted it, no need to do deep mirroring.
            // Mirror only root nodes.
            foreach (var node in _rootNodes)
            {
                node.Position = GLTFImporterFunctions.MirrorX(node.Position);
                node.Rotation = GLTFImporterFunctions.MirrorX(node.Rotation);
                node.Scale = GLTFImporterFunctions.MirrorX(node.Scale);
            }
        }
        // Apply inline transforms on the lowest level
        foreach (var node in _rootNodes)
            ApplyInlineTransform(node);
    }

    /// <summary>Return true if source model is has a lot of mirrored meshes.</summary>
    private bool HasMostlyMirroredGeometry()
    {
        var totalMeshes = 0;
        var mirroredMeshes = 0;
        CountMirroredMeshes(_rootNodes, false, ref totalMeshes, ref mirroredMeshes);
        return mirroredMeshes > 0 && mirroredMeshes > totalMeshes / 2;
    }

    private void CountMirroredMeshes(IEnumerable<GLTFNode> nodes, bool isParentMirrored,
        ref int totalMeshes, ref int mirroredMeshes)
    {
        foreach (var node in nodes)
            CountMirroredMeshes(node, isParentMirrored, ref totalMeshes, ref mirroredMeshes);
    }

    private void CountMirroredMeshes(GLTFNode node, bool isParentMirrored,
        ref int totalMeshes, ref int mirroredMeshes)
    {
        var hasMesh = _model.Nodes[node.Index].Mesh >= 0;
        var isMirroredWorld = isParentMirrored != GLTFImporterFunctions.IsNegativeScale(node.Scale);
        if (hasMesh)
        {
            ++totalMeshes;
            if (isMirroredWorld) ++mirroredMeshes;
        }
        CountMirroredMeshes(node.Children, isMirroredWorld, ref totalMeshes, ref mirroredMeshes);
    }

    private void ApplyInlineTransform(GLTFNode node)
    {
        node.Position = _inlineTransform.ApplyToPosition(node.Position);
        node.Rotation = _inlineTransform.ApplyToRotation(node.Rotation);
        foreach (var child in node.Children)
            ApplyInlineTransform(child);
    }

    private void PreProcessSkins()
    {
        _skinToRootNode.AddRange(Enumerable.Repeat<GLTFNode>(null!, _model.Skins.Count));
        for (var skinIndex = 0; skinIndex < _model.Skins.Count; ++skinIndex)
        {
            var sourceSkin = _model.Skins[skinIndex];
            var rootNode = _nodeByIndex[GetSkinRoot(sourceSkin).Index];
            MarkInSkin(rootNode, skinIndex);
            foreach (var jointNodeIndex in sourceSkin.Joints)
            {
                _base.CheckNode(jointNodeIndex);
                ForEachInPathExceptParent(_nodeByIndex[jointNodeIndex], rootNode, node => MarkInSkin(node, skinIndex));
            }
            _skinToRootNode[skinIndex] = rootNode;
        }
    }

    private GLTFNode GetSkinRoot(Tg.Skin sourceSkin)
    {
        if (sourceSkin.Skeleton >= 0)
        {
            _base.CheckNode(sourceSkin.Skeleton);
            var skeletonNode = _nodeByIndex[sourceSkin.Skeleton];
            if (IsValidSkeletonRootNode(skeletonNode, sourceSkin))
                return skeletonNode;
        }
        GLTFNode? rootNode = null;
        foreach (var nodeIndex in sourceSkin.Joints)
        {
            _base.CheckNode(nodeIndex);
            rootNode = rootNode is null ? _nodeByIndex[nodeIndex] : GetCommonParent(rootNode, _nodeByIndex[nodeIndex]);
            if (rootNode is null)
                throw new InvalidOperationException("Skin doesn't have common root node");
        }
        return rootNode ?? throw new InvalidOperationException("Skin doesn't have joints");
    }

    private bool IsValidSkeletonRootNode(GLTFNode skeletonNode, Tg.Skin sourceSkin)
    {
        foreach (var nodeIndex in sourceSkin.Joints)
        {
            _base.CheckNode(nodeIndex);
            var node = _nodeByIndex[nodeIndex];
            if (!IsChildOf(node, skeletonNode) && node != skeletonNode)
            {
                Log.GetLogger().Warning("Skeleton node #{} is not a parent of joint node #{}", sourceSkin.Skeleton, nodeIndex);
                return false;
            }
        }
        return true;
    }

    private void InitializeSkeletons()
    {
        var skinToGroup = Enumerable.Range(0, _model.Skins.Count).ToList();
        ForEach(_rootNodes, child =>
        {
            if (child.ContainedInSkins.Count <= 1)
                return;
            var newGroup = skinToGroup[child.ContainedInSkins[0]];
            for (var i = 1; i < child.ContainedInSkins.Count; ++i)
            {
                var oldGroup = skinToGroup[child.ContainedInSkins[i]];
                if (oldGroup == newGroup)
                    continue;
                for (var j = 0; j < skinToGroup.Count; ++j)
                    if (skinToGroup[j] == oldGroup) skinToGroup[j] = newGroup;
            }
        });
        var uniqueGroups = skinToGroup.Distinct().Order().ToList();
        _skinToSkeleton.AddRange(Enumerable.Repeat(0, _model.Skins.Count));
        for (var skeletonIndex = 0; skeletonIndex < uniqueGroups.Count; ++skeletonIndex)
        {
            var skeleton = new GLTFSkeleton();
            _skeletons.Add(skeleton);
            for (var skinIndex = 0; skinIndex < _model.Skins.Count; ++skinIndex)
            {
                if (skinToGroup[skinIndex] != uniqueGroups[skeletonIndex])
                    continue;
                skeleton.Skins.Add(skinIndex);
                _skinToSkeleton[skinIndex] = skeletonIndex;
            }
            if (skeleton.Skins.Count == 0)
                throw new InvalidOperationException("Skeleton must contain at least one skin");
        }
        AssignNodesToSkeletons();
        for (var skeletonIndex = 0; skeletonIndex < _skeletons.Count; ++skeletonIndex)
        {
            var skeleton = _skeletons[skeletonIndex];
            skeleton.Index = skeletonIndex;
            InitializeSkeletonRootNode(skeleton);
            AppendEmptyNodesToSkeleton(skeleton);
            AssignSkeletonBoneNames(skeleton);
            if (_base.GetSettings().CleanupBoneNames)
                CleanupSkeletonBoneNames(skeleton);
        }
    }

    private void AssignNodesToSkeletons()
    {
        // Every skeleton is a subtree.
        // A set of overlapping subtrees is a subtree as well.
        ForEach(_rootNodes, child =>
        {
            if (child.ContainedInSkins.Count == 0)
                return;
            var skeletonIndex = _skinToSkeleton[child.ContainedInSkins[0]];
            for (var i = 1; i < child.ContainedInSkins.Count; ++i)
                if (skeletonIndex != _skinToSkeleton[child.ContainedInSkins[i]])
                    throw new InvalidOperationException("Incorrect skeleton merge");
            child.SkeletonIndex = skeletonIndex;
        });
    }

    private void InitializeSkeletonRootNode(GLTFSkeleton skeleton)
    {
        foreach (var skinIndex in skeleton.Skins)
        {
            if (skeleton.RootNode is null)
                skeleton.RootNode = _skinToRootNode[skinIndex];
            else if (GetCommonParent(skeleton.RootNode, _skinToRootNode[skinIndex]) is { } skeletonRoot)
                skeleton.RootNode = _nodeByIndex[skeletonRoot.Index];
            if (skeleton.RootNode is null || skeleton.RootNode.SkeletonIndex != skeleton.Index)
                throw new InvalidOperationException($"Cannot find root of the skeleton when processing skin #{skinIndex}");
        }
    }

    private void AppendEmptyNodesToSkeleton(GLTFSkeleton skeleton)
    {
        if (!_base.GetSettings().AddEmptyNodesToSkeleton)
            return;
        ForEachSkeletonNode(skeleton.RootNode, skeleton.Index, boneNode =>
        {
            foreach (var child in boneNode.Children)
            {
                if (child.SkeletonIndex == skeleton.Index || !child.IsRecursivelyEmpty())
                    continue;
                // For each direct child of the bone node that is not a part of the skeleton,
                // check if it is empty and if so, add it to the skeleton.
                ForEach([child], emptyNode => emptyNode.SkeletonIndex = skeleton.Index);
            }
        });
    }

    private static void AssignSkeletonBoneNames(GLTFSkeleton skeleton)
    {
        ForEachSkeletonNode(skeleton.RootNode, skeleton.Index, boneNode =>
        {
            var nameHint = boneNode.Name.Length != 0 ? boneNode.Name : "Bone";
            for (var i = 0; i < GLTFImporterFunctions.MaxNameAssignTries; ++i)
            {
                var name = i != 0 ? $"{nameHint}_{i}" : nameHint;
                if (skeleton.BoneNameToNode.ContainsKey(name))
                    continue;
                boneNode.UniqueBoneName = name;
                skeleton.BoneNameToNode.Add(name, boneNode);
                return;
            }
            throw new InvalidOperationException("Failed to assign name to bone");
        });
    }

    private static void CleanupSkeletonBoneNames(GLTFSkeleton skeleton)
    {
        var commonPrefix = GLTFImporterFunctions.FindCommonPrefixWord(skeleton.BoneNameToNode.Keys.ToList());
        var newBoneNameToNode = new Dictionary<string, GLTFNode>();
        foreach (var (oldBoneName, boneNode) in skeleton.BoneNameToNode)
        {
            var newBoneName = oldBoneName[commonPrefix..];
            Debug.Assert(newBoneName.Length != 0);
            newBoneNameToNode.Add(newBoneName, boneNode);
            boneNode.UniqueBoneName = newBoneName;
        }
        skeleton.BoneNameToNode = newBoneNameToNode;
    }

    private void InitializeSkins()
    {
        for (var skinIndex = 0; skinIndex < _model.Skins.Count; ++skinIndex)
        {
            var skin = new GLTFSkin { Index = skinIndex };
            _skins.Add(skin);
            InitializeSkin(skin);
        }
    }

    private void InitializeSkin(GLTFSkin skin)
    {
        var sourceSkin = _model.Skins[skin.Index];
        var skeleton = _skeletons[_skinToSkeleton[skin.Index]];
        skin.Skeleton = skeleton;
        // Fill joints first
        var jointNodes = new HashSet<int>();
        foreach (var jointNodeIndex in sourceSkin.Joints)
        {
            var jointNode = GetNode(jointNodeIndex);
            if (jointNode.UniqueBoneName is null)
                throw new InvalidOperationException($"Cannot use node #{jointNodeIndex} in skin #{skin.Index}");
            skin.BoneNodes.Add(jointNode);
            jointNodes.Add(jointNodeIndex);
        }
        // Fill other nodes
        ForEachSkeletonNode(skeleton.RootNode, skeleton.Index, boneNode =>
        {
            if (jointNodes.Contains(boneNode.Index))
                return;
            if (boneNode.UniqueBoneName is null)
                throw new InvalidOperationException($"Cannot use node #{boneNode.Index} in skin #{skin.Index}");
            skin.BoneNodes.Add(boneNode);
        });
        // Fill bind matrices
        var numBones = skin.BoneNodes.Count;
        skin.InverseBindMatrices.AddRange(Enumerable.Repeat(new Matrix3x4(), numBones));
        if (sourceSkin.InverseBindMatrices >= 0)
        {
            _base.CheckAccessor(sourceSkin.InverseBindMatrices);
            var sourceBindMatrices = _bufferReader.ReadMatrix4Accessor(_model.Accessors[sourceSkin.InverseBindMatrices]);
            if (sourceSkin.Joints.Count > sourceBindMatrices.Count)
                throw new InvalidOperationException("Unexpected size of bind matrices array");
            for (var i = 0; i < sourceSkin.Joints.Count; ++i)
                skin.InverseBindMatrices[i] = new Matrix3x4(sourceBindMatrices[i].Transpose);
            ApplyInlineTransformToInverseBindMatrix(skin.InverseBindMatrices);
        }
        // Generate skeleton bones
        for (var boneIndex = 0; boneIndex < numBones; ++boneIndex)
        {
            var bone = new BoneView();
            skin.CookedBones.Add(bone);
            var boneNode = skin.BoneNodes[boneIndex];
            if (boneNode != skeleton.RootNode)
            {
                if (boneNode.Parent is null)
                    throw new InvalidOperationException("Bone parent must be present for child node");
                bone.ParentIndex = skin.BoneNodes.IndexOf(boneNode.Parent);
                if (bone.ParentIndex >= numBones)
                    throw new InvalidOperationException("Bone parent must be within the skeleton");
            }
            bone.Name = boneNode.UniqueBoneName!;
            bone.SetInitialTransform(boneNode.Position, boneNode.Rotation, boneNode.Scale);
            if (boneIndex < skin.InverseBindMatrices.Count)
                bone.OffsetMatrix = skin.InverseBindMatrices[boneIndex];
            skin.CookedBones[boneIndex] = bone;
        }
    }

    private void AssignSkinnedModelsToNodes()
    {
        ForEach(_rootNodes, node =>
        {
            if (node.Mesh is null || node.Skin is null)
                return;
            var skeletonRoot = _skeletons[_skinToSkeleton[node.Skin.Value]].RootNode;
            skeletonRoot.SkinnedMeshNodes.Add(node.Index);
            _skinnedMeshNodeRemapping[node.Index] = skeletonRoot.Index;
        });
    }

    private void EnumerateUniqueMeshSkinPairs()
    {
        ForEach(_rootNodes, node =>
        {
            if (node.Mesh is not null && node.Skin is not null)
                _meshSkinPairs[(node.Mesh.Value, node.Skin.Value)] = GetOrCreateMatchingMeshSkinPair(node.Mesh.Value, node.Skin);
        });
        ForEach(_rootNodes, node =>
        {
            if (node.Mesh is not null && node.Skin is null)
                _meshSkinPairs[(node.Mesh.Value, -1)] = GetOrCreateMatchingMeshSkinPair(node.Mesh.Value, null);
        });
    }

    private int GetOrCreateMatchingMeshSkinPair(int meshIndex, int? skinIndex)
    {
        for (var pairIndex = 0; pairIndex < _uniqueMeshSkinPairs.Count; ++pairIndex)
        {
            var existingMeshSkin = _uniqueMeshSkinPairs[pairIndex];
            if (existingMeshSkin.Skin is null && skinIndex is not null)
                throw new InvalidOperationException("Skinned meshes should be processed before non-skinned");
            // Always skip other meshes
            if (existingMeshSkin.Mesh != meshIndex)
                continue;
            // Match non-skinned model to the first mesh
            if (skinIndex is null || skinIndex == existingMeshSkin.Skin)
                return pairIndex;
            var existingBones = _skins[existingMeshSkin.Skin!.Value].CookedBones;
            var newBones = _skins[skinIndex.Value].CookedBones;
            var areBonesMatching = existingBones.Count == newBones.Count;
            for (var i = 0; areBonesMatching && i < existingBones.Count; ++i)
            {
                var lhs = existingBones[i];
                var rhs = newBones[i];
                areBonesMatching = lhs.Name == rhs.Name && lhs.ParentIndex == rhs.ParentIndex &&
                    lhs.OffsetMatrix.ApproximatelyEquivalent(rhs.OffsetMatrix, _base.GetSettings().OffsetMatrixError);
                // Don't compare initial transforms and bounding shapes
            }
            if (areBonesMatching)
                return pairIndex;
        }
        var result = _uniqueMeshSkinPairs.Count;
        _uniqueMeshSkinPairs.Add(new GLTFMeshSkinPair { Mesh = meshIndex, Skin = skinIndex });
        return result;
    }

    private void AssignNamesToSkeletonRoots()
    {
        ForEach(_rootNodes, node =>
        {
            if (node.SkinnedMeshNodes.Count == 0)
                return;
            var names = node.SkinnedMeshNodes.Select(index =>
            {
                _base.CheckNode(index);
                return _nodeByIndex[index].Name;
            }).Where(name => name.Length != 0);
            var name = string.Join('_', names);
            if (name.Length != 0) node.Name = name;
            else if (node.Name.Length == 0) node.Name = "SkinnedMesh";
        });
    }

    private static readonly List<BoneView> EmptyBones = [];

    private readonly GLTFImporterBase _base;
    private readonly GLTFBufferReader _bufferReader;
    private readonly Tg.Model _model;
    private readonly List<int> _numMorphsInMesh = [];
    private readonly List<int?> _nodeToParent = [];
    private readonly List<GLTFNode> _rootNodes = [];
    private readonly List<GLTFNode> _nodeByIndex = [];
    private readonly InlineTransform _inlineTransform = new();
    private readonly List<GLTFNode> _skinToRootNode = [];
    private readonly List<int> _skinToSkeleton = [];
    private readonly Dictionary<int, int> _skinnedMeshNodeRemapping = [];
    private readonly List<GLTFSkeleton> _skeletons = [];
    private readonly List<GLTFSkin> _skins = [];
    private readonly Dictionary<(int, int), int> _meshSkinPairs = [];
    private readonly List<GLTFMeshSkinPair> _uniqueMeshSkinPairs = [];
    private readonly List<GLTFAnimation> _animations = [];
}
