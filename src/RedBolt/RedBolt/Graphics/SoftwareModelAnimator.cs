using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Container for vertex buffer animation data.</summary>
public struct VertexBufferAnimationData : ICloneable
{
    public VertexBufferAnimationData()
    {
        BlendWeights = [];
        BlendIndices = [];
    }

    /// <summary>Whether the buffer is animated by skeleton.</summary>
    public bool HasSkeletalAnimation;
    /// <summary>Whether the buffer has normals affected by skeletal animation.</summary>
    public bool SkinNormals;
    /// <summary>Whether the buffer has tangents affected by skeletal animation.</summary>
    public bool SkinTangents;
    /// <summary>Blend weights. Size is number of bones used times number of vertices.</summary>
    public float[] BlendWeights = [];
    /// <summary>Blend indices.</summary>
    public byte[] BlendIndices = [];

    public readonly object Clone()
    {
        var clone = this;
        clone.BlendWeights = (float[])BlendWeights.Clone();
        clone.BlendIndices = (byte[])BlendIndices.Clone();
        return clone;
    }
}

/// <summary>Class for software model animation (morphing and skinning).</summary>
public class SoftwareModelAnimator : RedObject
{
    /// <summary>Max number of bones.</summary>
    public const uint MaxBones = 4;

    /// <summary>Construct.</summary>
    public SoftwareModelAnimator()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _vertexBuffersData.Clear();
        for (var i = _geometries.Count - 1; i >= 0; --i)
        {
            var geometryLods = _geometries[i];
            for (var j = geometryLods.Count - 1; j >= 0; --j)
                geometryLods[j]?.Dispose();
            geometryLods.Clear();
        }
        _geometries.Clear();
        for (var i = _vertexBuffers.Count - 1; i >= 0; --i)
            _vertexBuffers[i]?.Dispose();
        _vertexBuffers.Clear();
        _originalModel = null;
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<SoftwareModelAnimator>();
    }

    /// <summary>Initialize with model. Shall be manually called on model reload.</summary>
    public void Initialize(Model model, bool skinned, uint numBones)
    {
        _originalModel = model;
        _skinned = skinned;
        _numBones = numBones;
        CloneModelGeometries();
        InitializeAnimationData();
    }

    /// <summary>Reset morph and/or skeletal animation.</summary>
    public void ResetAnimation()
    {
        var originalModel = _originalModel!;
        var originalVertexBuffers = originalModel.GetVertexBuffers();
        for (var i = 0; i < _vertexBuffers.Count; ++i)
        {
            var clonedBuffer = _vertexBuffers[i];
            if (clonedBuffer is null)
                continue;

            var originalBuffer = originalVertexBuffers[i];
            var vertexStart = _skinned ? 0u : originalModel.GetMorphRangeStart((uint)i);
            var vertexCount = _skinned ? originalBuffer.GetVertexCount() : originalModel.GetMorphRangeCount((uint)i);
            CopyMorphVertices(clonedBuffer.GetShadowData(), originalBuffer.GetShadowData(), vertexStart, vertexCount,
                clonedBuffer, originalBuffer);
        }
    }

    /// <summary>Apply morphs.</summary>
    public void ApplyMorphs(ReadOnlySpan<ModelMorph> morphs)
    {
        foreach (var morph in morphs)
        {
            if (morph.Weight == 0.0f)
                continue;

            foreach (var bufferMorph in morph.Buffers)
            {
                var clonedBuffer = _vertexBuffers[(int)bufferMorph.Key];
                if (clonedBuffer is null)
                    continue;

                ApplyMorph(clonedBuffer, bufferMorph.Value, morph.Weight);
            }
        }
    }

    /// <summary>Apply skinning.</summary>
    public void ApplySkinning(ReadOnlySpan<Matrix3x4> worldTransforms)
    {
        if (!_skinned)
            return;

        for (var bufferIndex = 0; bufferIndex < _vertexBuffers.Count; ++bufferIndex)
        {
            var clonedBuffer = _vertexBuffers[bufferIndex];
            ref readonly var animationData = ref CollectionsMarshal.AsSpan(_vertexBuffersData)[bufferIndex];
            if (clonedBuffer is null || !animationData.HasSkeletalAnimation)
                continue;

            ApplyVertexBufferSkinning(clonedBuffer, in animationData, worldTransforms,
                animationData.SkinNormals, animationData.SkinTangents);
        }
    }

    /// <summary>Commit data to GPU.</summary>
    public void Commit()
    {
        foreach (var clonedVertexBuffer in _vertexBuffers)
        {
            if (clonedVertexBuffer is not null)
                clonedVertexBuffer.Update(clonedVertexBuffer.GetShadowData());
        }
    }

    /// <summary>Return animated geometries.</summary>
    public IReadOnlyList<IReadOnlyList<Geometry?>> GetGeometries() => _geometries;

    /// <summary>Return all cloned vertex buffers.</summary>
    public IReadOnlyList<VertexBuffer?> GetVertexBuffers() => _vertexBuffers;

    private VertexMask GetMorphElementMask()
    {
        if (_skinned)
            return VertexMask.Position | VertexMask.Normal | VertexMask.Tangent;

        var morphElementMask = VertexMask.None;
        foreach (var morph in _originalModel!.GetMorphs())
        {
            foreach (var morphedBuffer in morph.Buffers)
                morphElementMask |= morphedBuffer.Value.ElementMask;
        }
        return morphElementMask;
    }

    private void CloneModelGeometries()
    {
        var originalModel = _originalModel!;
        var originalToClonedMapping = new Dictionary<VertexBuffer, VertexBuffer>();
        var morphElementMask = GetMorphElementMask();
        var originalVertexBuffers = originalModel.GetVertexBuffers();
        while (_vertexBuffers.Count < originalVertexBuffers.Count)
            _vertexBuffers.Add(null);
        if (_vertexBuffers.Count > originalVertexBuffers.Count)
            _vertexBuffers.RemoveRange(originalVertexBuffers.Count, _vertexBuffers.Count - originalVertexBuffers.Count);

        for (var i = 0; i < originalVertexBuffers.Count; ++i)
        {
            if (!_skinned && originalModel.GetMorphRangeCount((uint)i) == 0)
                continue;

            var originalVertexBuffer = originalVertexBuffers[i];
            var clonedBufferMask = morphElementMask & (VertexMask)originalVertexBuffer.GetElementMask();
            var needPosition = (clonedBufferMask & VertexMask.Position) != 0;
            var needNormal = (clonedBufferMask & VertexMask.Normal) != 0;
            var needTangent = (clonedBufferMask & VertexMask.Tangent) != 0;
            var hasPositionVector3 = originalVertexBuffer.HasElement(VertexElementType.Vector3, VertexElementSemantic.Position);
            var hasPositionVector4 = originalVertexBuffer.HasElement(VertexElementType.Vector4, VertexElementSemantic.Position);
            var hasNormalVector3 = originalVertexBuffer.HasElement(VertexElementType.Vector3, VertexElementSemantic.Normal);
            var hasTangentVector4 = originalVertexBuffer.HasElement(VertexElementType.Vector4, VertexElementSemantic.Tangent);

            if (needPosition && !(hasPositionVector3 || hasPositionVector4))
            {
                Log.GetLogger().LogError("Position must be Vector3 or Vector4 for software skinning and morphing");
                continue;
            }
            if (needNormal && !hasNormalVector3)
            {
                Log.GetLogger().LogError("Normal must be Vector3 for software skinning and morphing");
                continue;
            }
            if (needTangent && !hasTangentVector4)
            {
                Log.GetLogger().LogError("Tangent must be Vector4 for software skinning and morphing");
                continue;
            }
            if (clonedBufferMask == VertexMask.None)
                continue;
            if (needPosition && originalVertexBuffer.GetElementOffset(VertexElementSemantic.Position) != 0)
            {
                Log.GetLogger().LogError("Position must be at offest 0 for software skinning and morphing");
                continue;
            }
            if (originalVertexBuffer.GetVertexSize() % sizeof(float) != 0)
            {
                Log.GetLogger().LogError("Vertex size must be aligned to 4 for software skinning and morphing");
                continue;
            }
            if (needNormal && originalVertexBuffer.GetElementOffset(VertexElementSemantic.Normal) % sizeof(float) != 0)
            {
                Log.GetLogger().LogError("Normal offset within vertex must be aligned to 4 for software skinning and morphing");
                continue;
            }
            if (needTangent && originalVertexBuffer.GetElementOffset(VertexElementSemantic.Tangent) % sizeof(float) != 0)
            {
                Log.GetLogger().LogError("Tangent offset within vertex must be aligned to 4 for software skinning and morphing");
                continue;
            }

            var clonedVertexBuffer = new VertexBuffer();
            clonedVertexBuffer.SetDebugName($"{originalVertexBuffer.GetDebugName()}: Animated Copy");
            clonedVertexBuffer.SetShadowed(true);
            clonedVertexBuffer.SetSize(originalVertexBuffer.GetVertexCount(), clonedBufferMask, true);
            CopyMorphVertices(clonedVertexBuffer.GetShadowData(), originalVertexBuffer.GetShadowData(), 0,
                originalVertexBuffer.GetVertexCount(), clonedVertexBuffer, originalVertexBuffer);
            clonedVertexBuffer.Update(clonedVertexBuffer.GetShadowData());
            originalToClonedMapping[originalVertexBuffer] = clonedVertexBuffer;
            _vertexBuffers[i] = clonedVertexBuffer;
        }

        _geometries.Clear();
        foreach (var geometryLods in originalModel.GetGeometries())
        {
            var clonedLods = new List<Geometry?>();
            foreach (var geometry in geometryLods)
            {
                var originalGeometry = geometry!;
                var cloneGeometry = new Geometry();
                var vertexBuffers = new List<VertexBuffer?>(originalGeometry.GetVertexBuffers());
                var numVertexBuffers = vertexBuffers.Count;
                for (var i = 0; i < numVertexBuffers; ++i)
                {
                    var originalBuffer = vertexBuffers[i];
                    if (originalBuffer is not null && originalToClonedMapping.TryGetValue(originalBuffer, out var clonedBuffer))
                        vertexBuffers.Add(clonedBuffer);
                }

                cloneGeometry.SetIndexBuffer(originalGeometry.GetIndexBuffer());
                cloneGeometry.SetVertexBuffers(vertexBuffers);
                cloneGeometry.SetDrawRange(originalGeometry.GetPrimitiveType(), originalGeometry.GetIndexStart(),
                    originalGeometry.GetIndexCount());
                cloneGeometry.SetLodDistance(originalGeometry.GetLodDistance());
                clonedLods.Add(cloneGeometry);
            }
            _geometries.Add(clonedLods);
        }
    }

    private void InitializeAnimationData()
    {
        while (_vertexBuffersData.Count < _vertexBuffers.Count)
            _vertexBuffersData.Add(new VertexBufferAnimationData());
        if (_vertexBuffersData.Count > _vertexBuffers.Count)
            _vertexBuffersData.RemoveRange(_vertexBuffers.Count, _vertexBuffersData.Count - _vertexBuffers.Count);
        for (var i = 0; i < _vertexBuffersData.Count; ++i)
        {
            ref var animationData = ref CollectionsMarshal.AsSpan(_vertexBuffersData)[i];
            animationData.HasSkeletalAnimation = false;
            animationData.BlendIndices = [];
            animationData.BlendWeights = [];
        }

        if (!_skinned)
            return;

        var originalVertexBuffers = _originalModel!.GetVertexBuffers();
        Span<(float Weight, byte Index)> bones = stackalloc (float Weight, byte Index)[(int)MaxBones];
        Span<byte> weightBytes = stackalloc byte[sizeof(float)];
        for (var bufferIndex = 0; bufferIndex < _vertexBuffers.Count; ++bufferIndex)
        {
            var originalBuffer = originalVertexBuffers[bufferIndex];
            var clonedBuffer = _vertexBuffers[bufferIndex];
            var originalVertexSize = originalBuffer.GetVertexSize();
            var indicesOffset = originalBuffer.GetElementOffset(VertexElementType.UByte4, VertexElementSemantic.BlendIndices);
            var vector4WeightsOffset = originalBuffer.GetElementOffset(VertexElementType.Vector4, VertexElementSemantic.BlendWeights);
            var ubyte4WeightsOffset = originalBuffer.GetElementOffset(VertexElementType.UByte4Norm, VertexElementSemantic.BlendWeights);
            var weightsOffset = vector4WeightsOffset != uint.MaxValue ? vector4WeightsOffset : ubyte4WeightsOffset;
            if (indicesOffset == uint.MaxValue || weightsOffset == uint.MaxValue)
                continue;

            var numVertices = originalBuffer.GetVertexCount();
            ref var animationData = ref CollectionsMarshal.AsSpan(_vertexBuffersData)[bufferIndex];
            animationData.HasSkeletalAnimation = true;
            animationData.SkinNormals = clonedBuffer!.HasElement(VertexElementSemantic.Normal);
            animationData.SkinTangents = clonedBuffer.HasElement(VertexElementSemantic.Tangent);
            animationData.BlendIndices = new byte[checked((int)(numVertices * _numBones))];
            animationData.BlendWeights = new float[checked((int)(numVertices * _numBones))];
            var originalBufferData = originalBuffer.GetShadowData();

            for (var vertexIndex = 0u; vertexIndex < numVertices; ++vertexIndex)
            {
                var vertexOffset = checked((int)(vertexIndex * originalVertexSize));
                for (var boneIndex = 0; boneIndex < MaxBones; ++boneIndex)
                {
                    bones[boneIndex].Index = originalBufferData[vertexOffset + checked((int)indicesOffset) + boneIndex];
                    if (vector4WeightsOffset != uint.MaxValue)
                    {
                        var weightOffset = vertexOffset + checked((int)weightsOffset) + boneIndex * sizeof(float);
                        for (var i = 0; i < weightBytes.Length; ++i)
                            weightBytes[i] = originalBufferData[weightOffset + i];
                        bones[boneIndex].Weight = MemoryMarshal.Read<float>(weightBytes);
                    }
                    else
                        bones[boneIndex].Weight = originalBufferData[vertexOffset + checked((int)weightsOffset) + boneIndex] / 255.0f;
                }

                if (_numBones != MaxBones)
                {
                    for (var i = 0; i < MaxBones - 1; ++i)
                    {
                        for (var j = i + 1; j < MaxBones; ++j)
                        {
                            if (bones[j].Weight > bones[i].Weight
                                || bones[j].Weight == bones[i].Weight && bones[j].Index > bones[i].Index)
                                (bones[i], bones[j]) = (bones[j], bones[i]);
                        }
                    }
                }

                var totalWeight = 1.0f;
                if (_numBones != MaxBones)
                {
                    totalWeight = 0.0f;
                    for (var boneIndex = 0u; boneIndex < _numBones; ++boneIndex)
                        totalWeight += bones[(int)boneIndex].Weight;
                }

                for (var boneIndex = 0u; boneIndex < _numBones; ++boneIndex)
                {
                    var outputIndex = checked((int)(vertexIndex * _numBones + boneIndex));
                    animationData.BlendIndices[outputIndex] = bones[(int)boneIndex].Index;
                    animationData.BlendWeights[outputIndex] = bones[(int)boneIndex].Weight / totalWeight;
                }
            }
        }
    }

    private void CopyMorphVertices(Span<byte> destVertexData, IReadOnlyList<byte> srcVertexData, uint vertexStart,
        uint vertexCount, VertexBuffer destBuffer, VertexBuffer srcBuffer)
    {
        var mask = (VertexMask)(destBuffer.GetElementMask() & srcBuffer.GetElementMask());
        var normalOffset = srcBuffer.GetElementOffset(VertexElementSemantic.Normal);
        var tangentOffset = srcBuffer.GetElementOffset(VertexElementSemantic.Tangent);
        var sourceVertexSize = srcBuffer.GetVertexSize();
        var destVertexSize = destBuffer.GetVertexSize();

        for (var i = 0u; i < vertexCount; ++i)
        {
            var sourceOffset = checked((int)((vertexStart + i) * sourceVertexSize));
            var destOffset = checked((int)((vertexStart + i) * destVertexSize));
            if ((mask & VertexMask.Position) != 0)
            {
                for (var j = 0; j < 3 * sizeof(float); ++j)
                    destVertexData[destOffset + j] = srcVertexData[sourceOffset + j];
                destOffset += 3 * sizeof(float);
            }
            if ((mask & VertexMask.Normal) != 0)
            {
                for (var j = 0; j < 3 * sizeof(float); ++j)
                    destVertexData[destOffset + j] = srcVertexData[sourceOffset + checked((int)normalOffset) + j];
                destOffset += 3 * sizeof(float);
            }
            if ((mask & VertexMask.Tangent) != 0)
            {
                for (var j = 0; j < 4 * sizeof(float); ++j)
                    destVertexData[destOffset + j] = srcVertexData[sourceOffset + checked((int)tangentOffset) + j];
            }
        }
    }

    private static void ApplyMorph(VertexBuffer buffer, VertexBufferMorph morph, float weight)
    {
        var elementMask = morph.ElementMask & (VertexMask)buffer.GetElementMask();
        var normalOffset = buffer.GetElementOffset(VertexElementSemantic.Normal);
        var tangentOffset = buffer.GetElementOffset(VertexElementSemantic.Tangent);
        var vertexSize = buffer.GetVertexSize();
        var sourceData = morph.MorphData.AsSpan();
        var destData = buffer.GetShadowData().AsSpan();
        var sourceOffset = 0;

        for (var i = 0u; i < morph.VertexCount; ++i)
        {
            var vertexIndex = MemoryMarshal.Read<uint>(sourceData[sourceOffset..]);
            sourceOffset += sizeof(uint);
            if ((elementMask & VertexMask.Position) != 0)
            {
                var destinationOffset = checked((int)(vertexIndex * vertexSize));
                var destination = MemoryMarshal.Read<Vector3f>(destData[destinationOffset..]);
                var source = MemoryMarshal.Read<Vector3f>(sourceData[sourceOffset..]);
                var result = destination + source * weight;
                MemoryMarshal.Write(destData[destinationOffset..], in result);
                sourceOffset += 3 * sizeof(float);
            }
            if ((elementMask & VertexMask.Normal) != 0)
            {
                var destinationOffset = checked((int)(vertexIndex * vertexSize + normalOffset));
                var destination = MemoryMarshal.Read<Vector3f>(destData[destinationOffset..]);
                var source = MemoryMarshal.Read<Vector3f>(sourceData[sourceOffset..]);
                var result = destination + source * weight;
                MemoryMarshal.Write(destData[destinationOffset..], in result);
                sourceOffset += 3 * sizeof(float);
            }
            if ((elementMask & VertexMask.Tangent) != 0)
            {
                var destinationOffset = checked((int)(vertexIndex * vertexSize + tangentOffset));
                var destination = MemoryMarshal.Read<Vector3f>(destData[destinationOffset..]);
                var source = MemoryMarshal.Read<Vector3f>(sourceData[sourceOffset..]);
                var result = destination + source * weight;
                MemoryMarshal.Write(destData[destinationOffset..], in result);
                sourceOffset += 3 * sizeof(float);
            }
        }
    }

    private void ApplyVertexBufferSkinning(VertexBuffer clonedBuffer, in VertexBufferAnimationData animationData,
        ReadOnlySpan<Matrix3x4> worldTransforms, bool skinNormals, bool skinTangents)
    {
        var vertexSize = clonedBuffer.GetVertexSize();
        var normalOffset = clonedBuffer.GetElementOffset(VertexElementType.Vector3, VertexElementSemantic.Normal);
        var tangentOffset = clonedBuffer.GetElementOffset(VertexElementType.Vector4, VertexElementSemantic.Tangent);
        var data = clonedBuffer.GetShadowData().AsSpan();

        for (var vertexIndex = 0u; vertexIndex < clonedBuffer.GetVertexCount(); ++vertexIndex)
        {
            var influenceOffset = checked((int)(vertexIndex * _numBones));
            var matrix = worldTransforms[animationData.BlendIndices[influenceOffset]] * animationData.BlendWeights[influenceOffset];
            for (var boneIndex = 1u; boneIndex < _numBones; ++boneIndex)
                matrix += worldTransforms[animationData.BlendIndices[influenceOffset + (int)boneIndex]] * animationData.BlendWeights[influenceOffset + (int)boneIndex];

            var baseOffset = checked((int)(vertexIndex * vertexSize));
            var position = MemoryMarshal.Read<Vector3f>(data[baseOffset..]);
            var transformedPosition = matrix * position;
            MemoryMarshal.Write(data[baseOffset..], in transformedPosition);
            if (skinNormals)
            {
                var destinationOffset = baseOffset + checked((int)normalOffset);
                var normal = MemoryMarshal.Read<Vector3f>(data[destinationOffset..]);
                var transformedNormal = TransformNormal(matrix, normal);
                MemoryMarshal.Write(data[destinationOffset..], in transformedNormal);
            }
            if (skinTangents)
            {
                var destinationOffset = baseOffset + checked((int)tangentOffset);
                var tangent = MemoryMarshal.Read<Vector3f>(data[destinationOffset..]);
                var transformedTangent = TransformNormal(matrix, tangent);
                MemoryMarshal.Write(data[destinationOffset..], in transformedTangent);
            }
        }
    }

    private static Vector3f TransformNormal(Matrix3x4 matrix, Vector3f value) => new(
        matrix.M00 * value.X + matrix.M01 * value.Y + matrix.M02 * value.Z,
        matrix.M10 * value.X + matrix.M11 * value.Y + matrix.M12 * value.Z,
        matrix.M20 * value.X + matrix.M21 * value.Y + matrix.M22 * value.Z);

    /// <summary>Original model.</summary>
    private Model? _originalModel;
    /// <summary>Animated model vertex buffers.</summary>
    private readonly List<VertexBuffer?> _vertexBuffers = [];
    /// <summary>Animated model geometries.</summary>
    private readonly List<List<Geometry?>> _geometries = [];
    /// <summary>Whether CPU skinning is applied.</summary>
    private bool _skinned;
    /// <summary>Number of bones used for skeletal animation.</summary>
    private uint _numBones;
    /// <summary>Animation data for vertex buffers.</summary>
    private readonly List<VertexBufferAnimationData> _vertexBuffersData = [];
}
