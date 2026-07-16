using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Container for vertex buffer animation data.</summary>
public sealed class VertexBufferAnimationData
{
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
}

/// <summary>Class for software model animation (morphing and skinning).</summary>
public sealed class SoftwareModelAnimator : Object
{
    private struct BoneWeight
    {
        public float Weight;
        public byte Index;
    }

    /// <summary>Max number of bones.</summary>
    public const uint MaxBones = 4;

    private Model? _originalModel;
    private readonly List<VertexBuffer?> _vertexBuffers = [];
    private readonly List<List<Geometry?>> _geometries = [];
    private bool _skinned;
    private uint _numBones;
    private readonly List<VertexBufferAnimationData> _vertexBuffersData = [];

    /// <summary>Construct.</summary>
    public SoftwareModelAnimator()
    {
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

    /// <summary>Reset morph and/or skeletal animation. Safe to call from worker thread.</summary>
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
            CopyMorphVertices(clonedBuffer.Map(), originalBuffer.Data, vertexStart, vertexCount, clonedBuffer, originalBuffer);
        }
    }

    /// <summary>Apply morphs. Safe to call from worker thread.</summary>
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
            var animationData = _vertexBuffersData[bufferIndex];
            if (clonedBuffer is null || !animationData.HasSkeletalAnimation)
                continue;

            ApplyVertexBufferSkinning(clonedBuffer, animationData, worldTransforms,
                animationData.SkinNormals, animationData.SkinTangents);
        }
    }

    /// <summary>Commit data to GPU.</summary>
    public void Commit()
    {
        foreach (var clonedVertexBuffer in _vertexBuffers)
        {
            if (clonedVertexBuffer is not null)
                clonedVertexBuffer.Unmap();
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
        ResizeList(_vertexBuffers, originalVertexBuffers.Count);

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
                Log.Logger.LogError("Position must be Vector3 or Vector4 for software skinning and morphing");
                continue;
            }
            if (needNormal && !hasNormalVector3)
            {
                Log.Logger.LogError("Normal must be Vector3 for software skinning and morphing");
                continue;
            }
            if (needTangent && !hasTangentVector4)
            {
                Log.Logger.LogError("Tangent must be Vector4 for software skinning and morphing");
                continue;
            }
            if (clonedBufferMask == VertexMask.None)
                continue;
            if (needPosition && originalVertexBuffer.GetElementOffset(VertexElementSemantic.Position) != 0)
            {
                Log.Logger.LogError("Position must be at offest 0 for software skinning and morphing");
                continue;
            }
            if (originalVertexBuffer.GetVertexSize() % sizeof(float) != 0)
            {
                Log.Logger.LogError("Vertex size must be aligned to 4 for software skinning and morphing");
                continue;
            }
            if (needNormal && originalVertexBuffer.GetElementOffset(VertexElementSemantic.Normal) % sizeof(float) != 0)
            {
                Log.Logger.LogError("Normal offset within vertex must be aligned to 4 for software skinning and morphing");
                continue;
            }
            if (needTangent && originalVertexBuffer.GetElementOffset(VertexElementSemantic.Tangent) % sizeof(float) != 0)
            {
                Log.Logger.LogError("Tangent offset within vertex must be aligned to 4 for software skinning and morphing");
                continue;
            }

            var clonedVertexBuffer = new VertexBuffer();
            clonedVertexBuffer.SetDebugName($"{originalVertexBuffer.GetDebugName()}: Animated Copy");
            clonedVertexBuffer.SetShadowed(true);
            clonedVertexBuffer.SetSize(originalVertexBuffer.GetVertexCount(), clonedBufferMask, true);
            CopyMorphVertices(clonedVertexBuffer.Map(), originalVertexBuffer.Data, 0,
                originalVertexBuffer.GetVertexCount(), clonedVertexBuffer, originalVertexBuffer);
            clonedVertexBuffer.Unmap();
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
        ResizeList(_vertexBuffersData, _vertexBuffers.Count);
        foreach (var animationData in _vertexBuffersData)
        {
            animationData.HasSkeletalAnimation = false;
            animationData.BlendIndices = [];
            animationData.BlendWeights = [];
        }

        if (!_skinned)
            return;

        var originalVertexBuffers = _originalModel!.GetVertexBuffers();
        Span<BoneWeight> bones = stackalloc BoneWeight[(int)MaxBones];
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
            var animationData = _vertexBuffersData[bufferIndex];
            animationData.HasSkeletalAnimation = true;
            animationData.SkinNormals = clonedBuffer!.HasElement(VertexElementSemantic.Normal);
            animationData.SkinTangents = clonedBuffer.HasElement(VertexElementSemantic.Tangent);
            animationData.BlendIndices = new byte[checked((int)(numVertices * _numBones))];
            animationData.BlendWeights = new float[checked((int)(numVertices * _numBones))];
            var originalBufferData = originalBuffer.Data;

            for (var vertexIndex = 0u; vertexIndex < numVertices; ++vertexIndex)
            {
                var vertexOffset = checked((int)(vertexIndex * originalVertexSize));
                for (var boneIndex = 0; boneIndex < MaxBones; ++boneIndex)
                {
                    bones[boneIndex].Index = originalBufferData[vertexOffset + checked((int)indicesOffset) + boneIndex];
                    bones[boneIndex].Weight = vector4WeightsOffset != uint.MaxValue
                        ? ReadFloat(originalBufferData, vertexOffset + checked((int)weightsOffset) + boneIndex * sizeof(float))
                        : originalBufferData[vertexOffset + checked((int)weightsOffset) + boneIndex] / 255.0f;
                }

                if (_numBones != MaxBones)
                {
                    for (var i = 0; i < MaxBones - 1; ++i)
                    {
                        for (var j = i + 1; j < MaxBones; ++j)
                        {
                            if (bones[j].Weight > bones[i].Weight)
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
                CopyBytes(srcVertexData, sourceOffset, destVertexData, destOffset, 3 * sizeof(float));
                destOffset += 3 * sizeof(float);
            }
            if ((mask & VertexMask.Normal) != 0)
            {
                CopyBytes(srcVertexData, sourceOffset + checked((int)normalOffset), destVertexData, destOffset, 3 * sizeof(float));
                destOffset += 3 * sizeof(float);
            }
            if ((mask & VertexMask.Tangent) != 0)
                CopyBytes(srcVertexData, sourceOffset + checked((int)tangentOffset), destVertexData, destOffset, 4 * sizeof(float));
        }
    }

    private static void ApplyMorph(VertexBuffer buffer, VertexBufferMorph morph, float weight)
    {
        var elementMask = morph.ElementMask & (VertexMask)buffer.GetElementMask();
        var normalOffset = buffer.GetElementOffset(VertexElementSemantic.Normal);
        var tangentOffset = buffer.GetElementOffset(VertexElementSemantic.Tangent);
        var vertexSize = buffer.GetVertexSize();
        var sourceData = morph.MorphData.AsSpan();
        var destData = buffer.Map();
        var sourceOffset = 0;

        for (var i = 0u; i < morph.VertexCount; ++i)
        {
            var vertexIndex = MemoryMarshal.Read<uint>(sourceData[sourceOffset..]);
            sourceOffset += sizeof(uint);
            if ((elementMask & VertexMask.Position) != 0)
                AddVector3(destData, checked((int)(vertexIndex * vertexSize)), sourceData, ref sourceOffset, weight);
            if ((elementMask & VertexMask.Normal) != 0)
                AddVector3(destData, checked((int)(vertexIndex * vertexSize + normalOffset)), sourceData, ref sourceOffset, weight);
            if ((elementMask & VertexMask.Tangent) != 0)
                AddVector3(destData, checked((int)(vertexIndex * vertexSize + tangentOffset)), sourceData, ref sourceOffset, weight);
        }
    }

    private void ApplyVertexBufferSkinning(VertexBuffer clonedBuffer, VertexBufferAnimationData animationData,
        ReadOnlySpan<Matrix3x4> worldTransforms, bool skinNormals, bool skinTangents)
    {
        var vertexSize = clonedBuffer.GetVertexSize();
        var normalOffset = clonedBuffer.GetElementOffset(VertexElementType.Vector3, VertexElementSemantic.Normal);
        var tangentOffset = clonedBuffer.GetElementOffset(VertexElementType.Vector4, VertexElementSemantic.Tangent);
        var data = clonedBuffer.Map();

        for (var vertexIndex = 0u; vertexIndex < clonedBuffer.GetVertexCount(); ++vertexIndex)
        {
            var influenceOffset = checked((int)(vertexIndex * _numBones));
            var matrix = worldTransforms[animationData.BlendIndices[influenceOffset]] * animationData.BlendWeights[influenceOffset];
            for (var boneIndex = 1u; boneIndex < _numBones; ++boneIndex)
                matrix += worldTransforms[animationData.BlendIndices[influenceOffset + (int)boneIndex]] * animationData.BlendWeights[influenceOffset + (int)boneIndex];

            var baseOffset = checked((int)(vertexIndex * vertexSize));
            var position = ReadVector3(data, baseOffset);
            WriteVector3(data, baseOffset, matrix * position);
            if (skinNormals)
                WriteVector3(data, baseOffset + checked((int)normalOffset), TransformNormal(matrix, ReadVector3(data, baseOffset + checked((int)normalOffset))));
            if (skinTangents)
                WriteVector3(data, baseOffset + checked((int)tangentOffset), TransformNormal(matrix, ReadVector3(data, baseOffset + checked((int)tangentOffset))));
        }
    }

    private static Vector3f TransformNormal(Matrix3x4 matrix, Vector3f value) => new(
        matrix.M00 * value.X + matrix.M01 * value.Y + matrix.M02 * value.Z,
        matrix.M10 * value.X + matrix.M11 * value.Y + matrix.M12 * value.Z,
        matrix.M20 * value.X + matrix.M21 * value.Y + matrix.M22 * value.Z);

    private static void AddVector3(Span<byte> destination, int destinationOffset, ReadOnlySpan<byte> source,
        ref int sourceOffset, float weight)
    {
        var dest = ReadVector3(destination, destinationOffset);
        var delta = ReadVector3(source, sourceOffset);
        WriteVector3(destination, destinationOffset, dest + delta * weight);
        sourceOffset += 3 * sizeof(float);
    }

    private static Vector3f ReadVector3(ReadOnlySpan<byte> data, int offset) => MemoryMarshal.Read<Vector3f>(data[offset..]);
    private static void WriteVector3(Span<byte> data, int offset, Vector3f value) => MemoryMarshal.Write(data[offset..], in value);

    private static float ReadFloat(IReadOnlyList<byte> data, int offset)
    {
        Span<byte> bytes = stackalloc byte[sizeof(float)];
        for (var i = 0; i < bytes.Length; ++i)
            bytes[i] = data[offset + i];
        return MemoryMarshal.Read<float>(bytes);
    }

    private static void CopyBytes(IReadOnlyList<byte> source, int sourceOffset, Span<byte> destination,
        int destinationOffset, int count)
    {
        for (var i = 0; i < count; ++i)
            destination[destinationOffset + i] = source[sourceOffset + i];
    }

    private static void ResizeList<T>(List<T?> values, int size) where T : class
    {
        while (values.Count < size)
            values.Add(null);
        if (values.Count > size)
            values.RemoveRange(size, values.Count - size);
    }

    private static void ResizeList(List<VertexBufferAnimationData> values, int size)
    {
        while (values.Count < size)
            values.Add(new VertexBufferAnimationData());
        if (values.Count > size)
            values.RemoveRange(size, values.Count - size);
    }
}
