using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Decal vertex.</summary>
public struct DecalVertex
{
    public DecalVertex() { }

    public DecalVertex(Vector3f position, Vector3f normal)
    {
        Position = position;
        Normal = normal;
    }

    public DecalVertex(Vector3f position, Vector3f normal, ReadOnlySpan<float> blendWeights, ReadOnlySpan<byte> blendIndices)
    {
        Position = position;
        Normal = normal;
        for (var i = 0; i < 4; ++i)
        {
            SetBlendWeight(i, blendWeights[i]);
            SetBlendIndex(i, blendIndices[i]);
        }
    }

    public Vector3f Position;
    public Vector3f Normal;
    public Vector2f TexCoord;
    public Vector4f Tangent;
    public float BlendWeight0;
    public float BlendWeight1;
    public float BlendWeight2;
    public float BlendWeight3;
    public byte BlendIndex0;
    public byte BlendIndex1;
    public byte BlendIndex2;
    public byte BlendIndex3;

    public float GetBlendWeight(int i) => i switch { 0 => BlendWeight0, 1 => BlendWeight1, 2 => BlendWeight2, _ => BlendWeight3 };
    public void SetBlendWeight(int i, float v) { switch (i) { case 0: BlendWeight0 = v; break; case 1: BlendWeight1 = v; break; case 2: BlendWeight2 = v; break; default: BlendWeight3 = v; break; } }
    public byte GetBlendIndex(int i) => i switch { 0 => BlendIndex0, 1 => BlendIndex1, 2 => BlendIndex2, _ => BlendIndex3 };
    public void SetBlendIndex(int i, byte v) { switch (i) { case 0: BlendIndex0 = v; break; case 1: BlendIndex1 = v; break; case 2: BlendIndex2 = v; break; default: BlendIndex3 = v; break; } }

    public uint BlendIndicesPacked => (uint)(BlendIndex0 | (BlendIndex1 << 8) | (BlendIndex2 << 16) | (BlendIndex3 << 24));
}

/// <summary>One decal in a decal set.</summary>
public sealed class Decal
{
    public float Timer;
    public float TimeToLive;
    public BoundingBox BoundingBox;
    public readonly List<DecalVertex> Vertices = [];
    public readonly List<ushort> Indices = [];

    public void AddVertex(DecalVertex vertex)
    {
        for (var i = 0; i < Vertices.Count; ++i)
        {
            if (vertex.Position.Equals(Vertices[i].Position) && vertex.Normal.Equals(Vertices[i].Normal))
            {
                Indices.Add((ushort)i);
                return;
            }
        }

        var newIndex = (ushort)Vertices.Count;
        Vertices.Add(vertex);
        Indices.Add(newIndex);
    }

    public void CalculateBoundingBox()
    {
        BoundingBox = default;
        BoundingBox.Clear();
        foreach (var vertex in Vertices)
            BoundingBox.Merge(vertex.Position);
    }
}

/// <summary>Decal renderer component.</summary>
public class DecalSet : Drawable
{
    private const uint MinVertices = 4;
    private const uint MinIndices = 6;
    private const uint MaxVerticesLimit = 65536;
    private const uint DefaultMaxVertices = 512;
    private const uint DefaultMaxIndices = 1024;
    private static readonly VertexMask StaticElementMask = VertexMask.Position | VertexMask.Normal | VertexMask.TexCoord1 | VertexMask.Tangent;
    private static readonly VertexMask SkinnedElementMask = StaticElementMask | VertexMask.BlendWeights | VertexMask.BlendIndices;

    private readonly Geometry _geometry = new();
    private readonly VertexBuffer _vertexBuffer = new();
    private readonly IndexBuffer _indexBuffer = new();
    private readonly List<Decal> _decals = [];
    private readonly List<Bone> _bones = [];
    private Matrix3x4[] _skinMatrices = [];
    private readonly Matrix3x4[] _nodeWorldTransform = [Matrix3x4.Identity];
    private uint _numVertices;
    private uint _numIndices;
    private uint _maxVertices = DefaultMaxVertices;
    private uint _maxIndices = DefaultMaxIndices;
    private bool _optimizeBufferSize;
    private bool _skinned;
    private bool _bufferDirty = true;
    private bool _boundingBoxDirty = true;
    private bool _skinningDirty;
    private bool _assignBonesPending;
    private bool _subscribed;

    public DecalSet()
        : base(DrawableFlags.Geometry)
    {
        _vertexBuffer.SetShadowed(true);
        _indexBuffer.SetShadowed(true);
        _geometry.SetIndexBuffer(_indexBuffer);
        _batches.Add(new SourceBatch
        {
            Geometry = _geometry,
            GeometryType = GeometryType.StaticNoInstancing
        });
        _vertexBuffer.SetDebugName("DecalSet Geometry");
        _indexBuffer.SetDebugName("DecalSet Geometry");
    }

    public static void RegisterObject(Context context) =>
        context.AddFactoryReflection<DecalSet>(ObjectCategory.Category_Geometry);

    public override void ApplyAttributes()
    {
        if (_assignBonesPending)
            AssignBoneNodes();
    }

    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
        UpdateEventSubscription(true);
    }

    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        // Do not return raycast hits
    }

    public override void UpdateBatches(CommonFrameInfo frame)
    {
        var worldBoundingBox = GetWorldBoundingBox();
        _nodeWorldTransform[0] = Node.GetWorldTransform();
        SetDistance(frame.Camera!.GetDistance(worldBoundingBox.Center));

        var scale = worldBoundingBox.Size.DotProduct(GraphicsDefs.DotScale);
        SetLodDistance(frame.Camera.GetLodDistance(GetDistance(), scale, LodBias));

        var batch = _batches[0];
        batch.Distance = GetDistance();
        if (!_skinned)
            batch.WorldTransform = _nodeWorldTransform;
        _batches[0] = batch;

        if (_bufferDirty || (_vertexBuffer.GetRawBuffer()?.IsDataLost() ?? false) ||
            (_indexBuffer.GetRawBuffer()?.IsDataLost() ?? false))
            RequestUpdateBatchesDelayed(frame);
    }

    public override void UpdateBatchesDelayed(CommonFrameInfo frame) => UpdateBuffers();

    public override void UpdateGeometry(CommonFrameInfo frame)
    {
        if (_skinningDirty)
            UpdateSkinning();
    }

    public override UpdateGeometryType GetUpdateGeometryType() =>
        _skinningDirty ? UpdateGeometryType.WorkerThread : UpdateGeometryType.None;

    public void SetMaterial(Material? material)
    {
        var batch = _batches[0];
        batch.Material = material;
        _batches[0] = batch;
    }

    public void SetMaxVertices(uint num)
    {
        num = (uint)Math.Clamp((int)num, (int)MinVertices, (int)MaxVerticesLimit);
        if (num != _maxVertices)
        {
            if (!_optimizeBufferSize)
                _bufferDirty = true;
            _maxVertices = num;
            while (_decals.Count != 0 && _numVertices > _maxVertices)
                RemoveDecals(1);
        }
    }

    public void SetMaxIndices(uint num)
    {
        if (num < MinIndices)
            num = MinIndices;
        if (num != _maxIndices)
        {
            if (!_optimizeBufferSize)
                _bufferDirty = true;
            _maxIndices = num;
            while (_decals.Count != 0 && _numIndices > _maxIndices)
                RemoveDecals(1);
        }
    }

    public void SetOptimizeBufferSize(bool enable)
    {
        if (enable != _optimizeBufferSize)
        {
            _optimizeBufferSize = enable;
            _bufferDirty = true;
        }
    }

    public Material? GetMaterial() => _batches[0].Material;
    public uint GetNumDecals() => (uint)_decals.Count;
    public uint GetNumVertices() => _numVertices;
    public uint GetNumIndices() => _numIndices;
    public uint GetMaxVertices() => _maxVertices;
    public uint GetMaxIndices() => _maxIndices;
    public bool GetOptimizeBufferSize() => _optimizeBufferSize;

    public void SetMaterialAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>();
        SetMaterial(cache?.GetResourceOrNull<Material>(value.Name));
    }

    public ResourceRef GetMaterialAttr() =>
        Resource.GetResourceRef(_batches[0].Material, new StringHash(nameof(Material)));

    public void RemoveDecals(uint num)
    {
        while (num-- != 0 && _decals.Count != 0)
            RemoveDecal(0);
    }

    public void RemoveAllDecals()
    {
        if (_decals.Count != 0)
        {
            _decals.Clear();
            _numVertices = 0;
            _numIndices = 0;
            MarkDecalsDirty();
        }

        foreach (var bone in _bones)
        {
            if (bone.Node is not null)
                bone.Node.RemoveListener(this);
        }
        _bones.Clear();
        _skinMatrices = [];
        UpdateBatch();
    }

    private void RemoveDecal(int index)
    {
        var decal = _decals[index];
        _numVertices -= (uint)decal.Vertices.Count;
        _numIndices -= (uint)decal.Indices.Count;
        _decals.RemoveAt(index);
        MarkDecalsDirty();
    }

    public void MarkDecalsDirty()
    {
        if (!_boundingBoxDirty)
        {
            _boundingBoxDirty = true;
            if (IsAttached)
                OnMarkedDirty(Node);
        }
        _bufferDirty = true;
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        base.OnMarkedDirty(node);
        if (_skinned)
            _skinningDirty = true;
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        if (!_skinned)
        {
            if (_boundingBoxDirty)
                CalculateBoundingBox();
            SetWorldBoundingBox(BoundingBox.Transformed(Node.GetWorldTransform()));
        }
        else
        {
            var worldBox = default(BoundingBox);
            foreach (var bone in _bones)
            {
                var boneNode = bone.Node;
                if (boneNode is null)
                    continue;
                if ((bone.CollisionMask & BoneCollisionShape.Box) != 0)
                    worldBox.Merge(bone.BoundingBox.Transformed(boneNode.GetWorldTransform()));
                else if ((bone.CollisionMask & BoneCollisionShape.Sphere) != 0)
                    worldBox.Merge(new Sphere(boneNode.GetWorldPosition(), bone.Radius * 0.5f));
            }
            SetWorldBoundingBox(worldBox);
        }
    }

    private void CalculateBoundingBox()
    {
        var box = default(BoundingBox);
        box.Clear();
        foreach (var decal in _decals)
            box.Merge(decal.BoundingBox);
        SetLocalBoundingBox(box);
        _boundingBoxDirty = false;
    }

    public bool AddDecal(Drawable? target, Vector3f worldPosition, Quaternion worldRotation, float size,
        float aspectRatio, float depth, Vector2f topLeftUV, Vector2f bottomRightUV, float timeToLive = 0.0f,
        float normalCutoff = 0.1f, uint subGeometry = uint.MaxValue)
    {
        // Do not add decals in headless mode
        if (!IsAttached || GetSubsystem<Graphics>() is null)
            return false;

        if (target is null || !target.IsAttached)
        {
            Log.Logger.LogError("Null target drawable for decal");
            return false;
        }

        var animatedModel = target as AnimatedModel;
        if ((animatedModel is not null && !_skinned) || (animatedModel is null && _skinned))
        {
            RemoveAllDecals();
            _skinned = animatedModel is not null;
            _bufferDirty = true;
        }

        var adjustedWorldPosition = worldPosition - 0.5f * depth * (worldRotation * Vector3f.Forward);
        /// \todo target transform is not right if adding a decal to StaticModelGroup
        var targetTransform = target.Node.GetWorldTransform().Inverse;

        if (animatedModel is not null)
        {
            var skeleton = animatedModel.GetSkeleton();
            var numBones = skeleton.GetNumBones();
            Bone? bestBone = null;
            var bestSize = 0.0f;

            for (var i = 0u; i < numBones; ++i)
            {
                var bone = skeleton.GetBone(i);
                if (bone?.Node is null || bone.CollisionMask == BoneCollisionShape.None)
                    continue;

                var decalSphere = new Sphere(
                    bone.Node.GetWorldTransform().Inverse * worldPosition,
                    0.5f * size / bone.Node.GetWorldScale().Length);

                if ((bone.CollisionMask & BoneCollisionShape.Box) != 0)
                {
                    var sz = bone.BoundingBox.HalfSize.Length;
                    if (bone.BoundingBox.IsInside(decalSphere) != Intersection.Outside && sz > bestSize)
                    {
                        bestBone = bone;
                        bestSize = sz;
                    }
                }
                else if ((bone.CollisionMask & BoneCollisionShape.Sphere) != 0)
                {
                    var boneSphere = new Sphere(Vector3f.Zero, bone.Radius);
                    var sz = bone.Radius;
                    if (boneSphere.IsInside(decalSphere) != Intersection.Outside && sz > bestSize)
                    {
                        bestBone = bone;
                        bestSize = sz;
                    }
                }
            }

            if (bestBone is not null)
                targetTransform = (bestBone.Node!.GetWorldTransform() * bestBone.OffsetMatrix).Inverse;
        }

        var decalFrustum = new Frustum();
        var frustumTransform = targetTransform * new Matrix3x4(adjustedWorldPosition, worldRotation, 1.0f);
        decalFrustum.DefineOrtho(size, aspectRatio, 1.0f, 0.0f, depth, frustumTransform);

        var decalNormal = (targetTransform * (worldRotation * Vector3f.Back).ToVector4(0.0f)).Normalized;

        var newDecal = new Decal { TimeToLive = timeToLive };
        _decals.Add(newDecal);

        var faces = new List<List<DecalVertex>>();
        var numBatches = (uint)target.GetBatches().Count;
        if (subGeometry < numBatches)
            GetFaces(faces, target, subGeometry, decalFrustum, decalNormal, normalCutoff);
        else
        {
            for (var i = 0u; i < numBatches; ++i)
                GetFaces(faces, target, i, decalFrustum, decalNormal, normalCutoff);
        }

        var tempFace = new List<DecalVertex>();
        foreach (var plane in decalFrustum.Planes)
        {
            for (var j = 0; j < faces.Count; ++j)
            {
                var face = faces[j];
                if (face.Count == 0)
                    continue;
                ClipPolygon(tempFace, face, plane, _skinned);
                faces[j] = [.. tempFace];
            }
        }

        foreach (var face in faces)
        {
            if (face.Count < 3)
                continue;
            for (var j = 2; j < face.Count; ++j)
            {
                newDecal.AddVertex(face[0]);
                newDecal.AddVertex(face[j - 1]);
                newDecal.AddVertex(face[j]);
            }
        }

        if (newDecal.Vertices.Count == 0)
        {
            _decals.RemoveAt(_decals.Count - 1);
            return true;
        }

        if (newDecal.Vertices.Count > _maxVertices)
        {
            Log.Logger.LogWarning("Can not add decal, vertex count {Count} exceeds maximum {Max}", newDecal.Vertices.Count, _maxVertices);
            _decals.RemoveAt(_decals.Count - 1);
            return false;
        }
        if (newDecal.Indices.Count > _maxIndices)
        {
            Log.Logger.LogWarning("Can not add decal, index count {Count} exceeds maximum {Max}", newDecal.Indices.Count, _maxIndices);
            _decals.RemoveAt(_decals.Count - 1);
            return false;
        }

        var projection = new Matrix4(
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0);
        // Set diagonal elements used by C++
        // C++: projection.m11_ = (1.0f / (size * 0.5f)); projection.m00_ = projection.m11_ / aspectRatio; projection.m22_ = 1/depth; projection.m33_=1
        var m11 = 1.0f / (size * 0.5f);
        var m00 = m11 / aspectRatio;
        projection = new Matrix4(
            m00, 0, 0, 0,
            0, m11, 0, 0,
            0, 0, 1.0f / depth, 0,
            0, 0, 0, 1.0f);

        CalculateUVs(newDecal, frustumTransform.Inverse, projection, topLeftUV, bottomRightUV);

        var decalTransform = Node.GetWorldTransform().Inverse * target.Node.GetWorldTransform();
        TransformVertices(newDecal, _skinned ? Matrix3x4.Identity : decalTransform);

        // Tangents
        var vertexBytes = new byte[newDecal.Vertices.Count * (uint)Marshal.SizeOf<DecalVertexLayout>()];
        // Use simplified tangent generation via float layout helper
        GenerateDecalTangents(newDecal);

        newDecal.CalculateBoundingBox();
        _numVertices += (uint)newDecal.Vertices.Count;
        _numIndices += (uint)newDecal.Indices.Count;

        while (_decals.Count != 0 && (_numVertices > _maxVertices || _numIndices > _maxIndices))
            RemoveDecals(1);

        Log.Logger.LogDebug("Added decal with {Count} vertices", newDecal.Vertices.Count);

        if (newDecal.TimeToLive > 0.0f && !_subscribed)
            UpdateEventSubscription(false);

        MarkDecalsDirty();
        return true;
    }

    private struct DecalVertexLayout { }

    private static void GenerateDecalTangents(Decal decal)
    {
        // Port of GenerateTangents call: compute simple tangents from triangles if none set.
        for (var i = 0; i + 2 < decal.Indices.Count; i += 3)
        {
            var i0 = decal.Indices[i];
            var i1 = decal.Indices[i + 1];
            var i2 = decal.Indices[i + 2];
            var v0 = decal.Vertices[i0];
            var v1 = decal.Vertices[i1];
            var v2 = decal.Vertices[i2];
            var edge1 = v1.Position - v0.Position;
            var edge2 = v2.Position - v0.Position;
            var deltaUV1 = v1.TexCoord - v0.TexCoord;
            var deltaUV2 = v2.TexCoord - v0.TexCoord;
            var f = deltaUV1.X * deltaUV2.Y - deltaUV2.X * deltaUV1.Y;
            if (MathF.Abs(f) < MathDefs.Epsilon)
                continue;
            f = 1.0f / f;
            var tangent = new Vector3f(
                f * (deltaUV2.Y * edge1.X - deltaUV1.Y * edge2.X),
                f * (deltaUV2.Y * edge1.Y - deltaUV1.Y * edge2.Y),
                f * (deltaUV2.Y * edge1.Z - deltaUV1.Y * edge2.Z)).Normalized;
            var t0 = v0; t0.Tangent = new Vector4f(tangent, 1.0f); decal.Vertices[i0] = t0;
            var t1 = v1; t1.Tangent = new Vector4f(tangent, 1.0f); decal.Vertices[i1] = t1;
            var t2 = v2; t2.Tangent = new Vector4f(tangent, 1.0f); decal.Vertices[i2] = t2;
        }
    }

    private static DecalVertex ClipEdge(DecalVertex v0, DecalVertex v1, float d0, float d1, bool skinned)
    {
        var ret = new DecalVertex();
        var t = d0 / (d0 - d1);
        ret.Position = v0.Position + t * (v1.Position - v0.Position);
        ret.Normal = v0.Normal + t * (v1.Normal - v0.Normal);
        if (skinned)
        {
            if (v0.BlendIndicesPacked != v1.BlendIndicesPacked)
            {
                var src = MathF.Abs(d0) < MathF.Abs(d1) ? v0 : v1;
                for (var i = 0; i < 4; ++i)
                {
                    ret.SetBlendWeight(i, src.GetBlendWeight(i));
                    ret.SetBlendIndex(i, src.GetBlendIndex(i));
                }
            }
            else
            {
                for (var i = 0; i < 4; ++i)
                {
                    ret.SetBlendWeight(i, v0.GetBlendWeight(i) + t * (v1.GetBlendWeight(i) - v0.GetBlendWeight(i)));
                    ret.SetBlendIndex(i, v0.GetBlendIndex(i));
                }
            }
        }
        return ret;
    }

    private static void ClipPolygon(List<DecalVertex> dest, List<DecalVertex> src, Plane plane, bool skinned)
    {
        var last = 0;
        var lastDistance = 0.0f;
        dest.Clear();
        if (src.Count == 0)
            return;

        for (var i = 0; i < src.Count; ++i)
        {
            var distance = plane.Distance(src[i].Position);
            if (distance >= 0.0f)
            {
                if (lastDistance < 0.0f)
                    dest.Add(ClipEdge(src[last], src[i], lastDistance, distance, skinned));
                dest.Add(src[i]);
            }
            else
            {
                if (lastDistance >= 0.0f && i != 0)
                    dest.Add(ClipEdge(src[last], src[i], lastDistance, distance, skinned));
            }
            last = i;
            lastDistance = distance;
        }

        var distance0 = plane.Distance(src[0].Position);
        if ((lastDistance < 0.0f && distance0 >= 0.0f) || (lastDistance >= 0.0f && distance0 < 0.0f))
            dest.Add(ClipEdge(src[last], src[0], lastDistance, distance0, skinned));
    }

    private void GetFaces(List<List<DecalVertex>> faces, Drawable target, uint batchIndex, Frustum frustum,
        Vector3f decalNormal, float normalCutoff)
    {
        var geometry = target.GetLodGeometry(batchIndex, 0);
        if (geometry is null || geometry.GetPrimitiveType() != PrimitiveType.TriangleList)
            return;

        geometry.GetRawDataShared(out var vertexData, out var vertexSize, out var indexData, out var indexSize, out _);
        if (vertexData is null || vertexSize == 0)
        {
            Log.Logger.LogWarning("Can not add decal, target drawable has no CPU-side geometry data");
            return;
        }

        // Position is assumed at offset 0 for raw geometry (C++ also falls back this way).
        var positionData = vertexData;
        var positionStride = vertexSize;
        byte[]? normalData = null;
        uint normalStride = 0;

        if (indexData is not null && indexSize != 0)
        {
            var indexStart = geometry.GetIndexStart();
            var indexCount = geometry.GetIndexCount();
            if (indexSize == 2)
            {
                for (var i = 0; i + 2 < indexCount; i += 3)
                {
                    var i0 = BitConverter.ToUInt16(indexData, (int)((indexStart + i) * 2));
                    var i1 = BitConverter.ToUInt16(indexData, (int)((indexStart + i + 1) * 2));
                    var i2 = BitConverter.ToUInt16(indexData, (int)((indexStart + i + 2) * 2));
                    GetFace(faces, target, batchIndex, i0, i1, i2, positionData, positionStride, normalData, normalStride,
                        frustum, decalNormal, normalCutoff);
                }
            }
            else
            {
                for (var i = 0; i + 2 < indexCount; i += 3)
                {
                    var i0 = BitConverter.ToUInt32(indexData, (int)((indexStart + i) * 4));
                    var i1 = BitConverter.ToUInt32(indexData, (int)((indexStart + i + 1) * 4));
                    var i2 = BitConverter.ToUInt32(indexData, (int)((indexStart + i + 2) * 4));
                    GetFace(faces, target, batchIndex, i0, i1, i2, positionData, positionStride, normalData, normalStride,
                        frustum, decalNormal, normalCutoff);
                }
            }
        }
        else
        {
            var indices = geometry.GetVertexStart();
            var indicesEnd = indices + geometry.GetVertexCount();
            while (indices + 2 < indicesEnd)
            {
                GetFace(faces, target, batchIndex, indices, indices + 1, indices + 2, positionData, positionStride,
                    normalData, normalStride, frustum, decalNormal, normalCutoff);
                indices += 3;
            }
        }
    }

    private void GetFace(List<List<DecalVertex>> faces, Drawable target, uint batchIndex, uint i0, uint i1, uint i2,
        byte[] positionData, uint positionStride, byte[]? normalData, uint normalStride, Frustum frustum,
        Vector3f decalNormal, float normalCutoff)
    {
        var v0 = MemoryMarshal.Read<Vector3f>(positionData.AsSpan(checked((int)(i0 * positionStride))));
        var v1 = MemoryMarshal.Read<Vector3f>(positionData.AsSpan(checked((int)(i1 * positionStride))));
        var v2 = MemoryMarshal.Read<Vector3f>(positionData.AsSpan(checked((int)(i2 * positionStride))));

        Vector3f faceNormal = default;
        var hasNormals = normalData is not null && normalStride != 0;
        if (!hasNormals)
        {
            var dist1 = v1 - v0;
            var dist2 = v2 - v0;
            faceNormal = dist1.CrossProduct(dist2).Normalized;
        }

        var n0 = hasNormals
            ? MemoryMarshal.Read<Vector3f>(normalData!.AsSpan(checked((int)(i0 * normalStride))))
            : faceNormal;
        var n1 = hasNormals
            ? MemoryMarshal.Read<Vector3f>(normalData!.AsSpan(checked((int)(i1 * normalStride))))
            : faceNormal;
        var n2 = hasNormals
            ? MemoryMarshal.Read<Vector3f>(normalData!.AsSpan(checked((int)(i2 * normalStride))))
            : faceNormal;

        if (decalNormal.DotProduct((n0 + n1 + n2) / 3.0f) < normalCutoff)
            return;

        for (var i = (int)FrustumPlane.Far; i >= 0; --i)
        {
            var plane = frustum.Planes[i];
            if (plane.Distance(v0) < 0.0f && plane.Distance(v1) < 0.0f && plane.Distance(v2) < 0.0f)
                return;
        }

        faces.Add([new DecalVertex(v0, n0), new DecalVertex(v1, n1), new DecalVertex(v2, n2)]);
    }

    private static void CalculateUVs(Decal decal, Matrix3x4 view, Matrix4 projection, Vector2f topLeftUV, Vector2f bottomRightUV)
    {
        var viewProj = projection * view;
        for (var i = 0; i < decal.Vertices.Count; ++i)
        {
            var vertex = decal.Vertices[i];
            var projected = viewProj * vertex.Position;
            vertex.TexCoord = new Vector2f(
                MathDefs.Lerp(topLeftUV.X, bottomRightUV.X, projected.X * 0.5f + 0.5f),
                MathDefs.Lerp(bottomRightUV.Y, topLeftUV.Y, projected.Y * 0.5f + 0.5f));
            decal.Vertices[i] = vertex;
        }
    }

    private static void TransformVertices(Decal decal, Matrix3x4 transform)
    {
        for (var i = 0; i < decal.Vertices.Count; ++i)
        {
            var vertex = decal.Vertices[i];
            vertex.Position = transform * vertex.Position;
            vertex.Normal = (transform * vertex.Normal.ToVector4(0.0f)).Normalized;
            decal.Vertices[i] = vertex;
        }
    }

    private void UpdateBuffers()
    {
        var newElementMask = _skinned ? SkinnedElementMask : StaticElementMask;
        var newVbSize = _optimizeBufferSize ? _numVertices : _maxVertices;
        var newIbSize = _optimizeBufferSize ? _numIndices : _maxIndices;

        if (_vertexBuffer.GetElementMask() != (uint)newElementMask || _vertexBuffer.GetVertexCount() != newVbSize)
            _vertexBuffer.SetSize(newVbSize, newElementMask);
        if (_indexBuffer.GetIndexCount() != newIbSize)
            _indexBuffer.SetSize(newIbSize, false);
        _geometry.SetVertexBuffer(0, _vertexBuffer);
        _geometry.SetDrawRange(PrimitiveType.TriangleList, 0, _numIndices, 0, _numVertices);

        if (_numVertices != 0 && _numIndices != 0)
        {
            var mapped = _vertexBuffer.Map();
            if (!mapped.IsEmpty)
            {
                var floats = MemoryMarshal.Cast<byte, float>(mapped);
                var fi = 0;
                var indices = new uint[_numIndices];
                uint indexStart = 0;
                uint indexWrite = 0;

                foreach (var decal in _decals)
                {
                    foreach (var vertex in decal.Vertices)
                    {
                        floats[fi++] = vertex.Position.X;
                        floats[fi++] = vertex.Position.Y;
                        floats[fi++] = vertex.Position.Z;
                        floats[fi++] = vertex.Normal.X;
                        floats[fi++] = vertex.Normal.Y;
                        floats[fi++] = vertex.Normal.Z;
                        floats[fi++] = vertex.TexCoord.X;
                        floats[fi++] = vertex.TexCoord.Y;
                        floats[fi++] = vertex.Tangent.X;
                        floats[fi++] = vertex.Tangent.Y;
                        floats[fi++] = vertex.Tangent.Z;
                        floats[fi++] = vertex.Tangent.W;
                        if (_skinned)
                        {
                            floats[fi++] = vertex.BlendWeight0;
                            floats[fi++] = vertex.BlendWeight1;
                            floats[fi++] = vertex.BlendWeight2;
                            floats[fi++] = vertex.BlendWeight3;
                            MemoryMarshal.Write(MemoryMarshal.AsBytes(floats.Slice(fi, 1)), in vertex.BlendIndex0);
                            // write 4 blend indices as one float-sized slot (C++ reinterprets)
                            var packed = vertex.BlendIndicesPacked;
                            MemoryMarshal.Write(MemoryMarshal.AsBytes(floats.Slice(fi, 1)), in packed);
                            fi++;
                        }
                    }

                    foreach (var index in decal.Indices)
                        indices[indexWrite++] = index + indexStart;
                    indexStart += (uint)decal.Vertices.Count;
                }

                _vertexBuffer.Unmap();
                _indexBuffer.SetUnpackedData(indices.AsSpan(0, (int)_numIndices));
            }
        }

        _vertexBuffer.GetRawBuffer()?.ClearDataLost();
        _indexBuffer.GetRawBuffer()?.ClearDataLost();
        _bufferDirty = false;
    }

    private void UpdateSkinning()
    {
        var worldTransform = Node.GetWorldTransform();
        if (_skinMatrices.Length != _bones.Count)
            _skinMatrices = new Matrix3x4[_bones.Count];
        for (var i = 0; i < _bones.Count; ++i)
        {
            var bone = _bones[i];
            _skinMatrices[i] = bone.Node is not null
                ? bone.Node.GetWorldTransform() * bone.OffsetMatrix
                : worldTransform;
        }
        _skinningDirty = false;
    }

    private void UpdateBatch()
    {
        var batch = _batches[0];
        if (_skinMatrices.Length != 0)
        {
            batch.GeometryType = GeometryType.Skinned;
            batch.WorldTransform = _skinMatrices;
            batch.NumWorldTransforms = (uint)_skinMatrices.Length;
        }
        else
        {
            batch.GeometryType = GeometryType.Static;
            _nodeWorldTransform[0] = IsAttached ? Node.GetWorldTransform() : Matrix3x4.Identity;
            batch.WorldTransform = _nodeWorldTransform;
            batch.NumWorldTransforms = 1;
        }
        _batches[0] = batch;
    }

    private void AssignBoneNodes()
    {
        _assignBonesPending = false;
        if (!IsAttached)
            return;
        foreach (var bone in _bones)
        {
            var boneNode = Node.GetChild(bone.Name, true);
            if (boneNode is not null)
                boneNode.AddListener(this);
            bone.Node = boneNode;
        }
    }

    private void UpdateEventSubscription(bool checkAllDecals)
    {
        var scene = Scene;
        if (scene is null)
            return;

        var enabled = IsEnabledEffective;
        if (enabled && checkAllDecals)
        {
            var hasTimeLimitedDecals = false;
            foreach (var decal in _decals)
            {
                if (decal.TimeToLive > 0.0f)
                {
                    hasTimeLimitedDecals = true;
                    break;
                }
            }
            enabled = hasTimeLimitedDecals;
        }

        if (enabled && !_subscribed)
        {
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
            _subscribed = true;
        }
        else if (!enabled && _subscribed)
        {
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>(scene);
            _subscribed = false;
        }
    }

    private void HandleScenePostUpdate(ref SceneEvents.ScenePostUpdateEvent eventData)
    {
        var timeStep = eventData.TimeStep;
        for (var i = 0; i < _decals.Count;)
        {
            _decals[i].Timer += timeStep;
            if (_decals[i].TimeToLive > 0.0f && _decals[i].Timer > _decals[i].TimeToLive)
                RemoveDecal(i);
            else
                ++i;
        }
    }

    public void SetDecalsAttr(byte[] value)
    {
        RemoveAllDecals();
        if (value.Length == 0)
            return;

        var buffer = new MemoryBuffer(value);
        _skinned = buffer.ReadBool();
        var numDecals = buffer.ReadVLE();
        while (numDecals-- != 0)
        {
            var newDecal = new Decal
            {
                Timer = buffer.ReadFloat(),
                TimeToLive = buffer.ReadFloat()
            };
            var numVerts = buffer.ReadVLE();
            var numInds = buffer.ReadVLE();
            for (var i = 0; i < numVerts; ++i)
            {
                var vertex = new DecalVertex
                {
                    Position = buffer.ReadVector3(),
                    Normal = buffer.ReadVector3(),
                    TexCoord = buffer.ReadVector2(),
                    Tangent = buffer.ReadVector4()
                };
                if (_skinned)
                {
                    for (var w = 0; w < 4; ++w)
                        vertex.SetBlendWeight(w, buffer.ReadFloat());
                    for (var b = 0; b < 4; ++b)
                        vertex.SetBlendIndex(b, buffer.ReadUByte());
                }
                newDecal.Vertices.Add(vertex);
            }
            for (var i = 0; i < numInds; ++i)
                newDecal.Indices.Add(buffer.ReadUShort());
            newDecal.CalculateBoundingBox();
            _numVertices += (uint)newDecal.Vertices.Count;
            _numIndices += (uint)newDecal.Indices.Count;
            _decals.Add(newDecal);
        }

        if (_skinned)
        {
            var numBones = buffer.ReadVLE();
            _skinMatrices = new Matrix3x4[numBones];
            _bones.Clear();
            for (var i = 0; i < numBones; ++i)
            {
                var newBone = new Bone
                {
                    Name = buffer.ReadString(),
                    CollisionMask = (BoneCollisionShape)buffer.ReadUByte()
                };
                if ((newBone.CollisionMask & BoneCollisionShape.Sphere) != 0)
                    newBone.Radius = buffer.ReadFloat();
                if ((newBone.CollisionMask & BoneCollisionShape.Box) != 0)
                    newBone.BoundingBox = buffer.ReadBoundingBox();
                newBone.OffsetMatrix = buffer.ReadMatrix3x4();
                _bones.Add(newBone);
            }
            _assignBonesPending = true;
            _skinningDirty = true;
        }

        UpdateEventSubscription(true);
        UpdateBatch();
        MarkDecalsDirty();
    }

    public byte[] GetDecalsAttr()
    {
        var ret = new VectorBuffer();
        ret.WriteBool(_skinned);
        ret.WriteVLE((uint)_decals.Count);
        foreach (var decal in _decals)
        {
            ret.WriteFloat(decal.Timer);
            ret.WriteFloat(decal.TimeToLive);
            ret.WriteVLE((uint)decal.Vertices.Count);
            ret.WriteVLE((uint)decal.Indices.Count);
            foreach (var vertex in decal.Vertices)
            {
                ret.WriteVector3(vertex.Position);
                ret.WriteVector3(vertex.Normal);
                ret.WriteVector2(vertex.TexCoord);
                ret.WriteVector4(vertex.Tangent);
                if (_skinned)
                {
                    for (var w = 0; w < 4; ++w)
                        ret.WriteFloat(vertex.GetBlendWeight(w));
                    for (var b = 0; b < 4; ++b)
                        ret.WriteUByte(vertex.GetBlendIndex(b));
                }
            }
            foreach (var index in decal.Indices)
                ret.WriteUShort(index);
        }

        if (_skinned)
        {
            ret.WriteVLE((uint)_bones.Count);
            foreach (var bone in _bones)
            {
                ret.WriteString(bone.Name);
                ret.WriteUByte((byte)bone.CollisionMask);
                if ((bone.CollisionMask & BoneCollisionShape.Sphere) != 0)
                    ret.WriteFloat(bone.Radius);
                if ((bone.CollisionMask & BoneCollisionShape.Box) != 0)
                    ret.WriteBoundingBox(bone.BoundingBox);
                ret.WriteMatrix3x4(bone.OffsetMatrix);
            }
        }

        return ret.GetBuffer();
    }
}
