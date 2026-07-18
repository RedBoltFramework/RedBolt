using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Hardware vertex buffer.</summary>
public sealed class VertexBuffer : PipelineStateTracker
{
    private sealed class VertexRawBuffer : RawBuffer
    {
        public VertexRawBuffer(DeviceObjectFlag flags)
            : base(flags)
        {
        }

        public bool CreateBuffer(RawBufferParams parameters) => Create(parameters);
    }

    private static readonly uint[] TypeSizes =
    [
        sizeof(int),
        sizeof(float),
        2u * sizeof(float),
        3u * sizeof(float),
        4u * sizeof(float),
        sizeof(uint),
        sizeof(uint)
    ];

    private byte[] _data = [];
    private VertexRawBuffer? _rawBuffer;
    private List<VertexElement> _elements = [];
    private uint _elementMask;
    private string? _layoutSignature;
    private bool _shadowedPending;
    private string _debugName = string.Empty;
    private readonly DeviceObjectFlag _flags;

    public VertexBuffer(DeviceObjectFlag flags = DeviceObjectFlag.None)
    {
        _flags = flags;
    }

    /// <summary>Whether is dynamic.</summary>
    public bool IsDynamic { get; private set; }

    /// <summary>Whether the buffer has CPU shadow data.</summary>
    public bool Shadowed { get; private set; }

    /// <summary>CPU-side buffer data.</summary>
    public IReadOnlyList<byte> Data => _data;

    /// <summary>Vertex elements.</summary>
    public IReadOnlyList<VertexElement> Elements => _elements;

    /// <summary>Legacy vertex element mask.</summary>
    public uint ElementMask => _elementMask;

    /// <summary>Number of vertices.</summary>
    public uint VertexCount { get; private set; }

    /// <summary>Vertex size.</summary>
    public uint VertexSize { get; private set; }

    /// <summary>Return buffer data.</summary>
    public IReadOnlyList<byte> GetData() => _data;

    /// <summary>Return vertex elements.</summary>
    public IReadOnlyList<VertexElement> GetElements() => _elements;

    /// <summary>Return legacy vertex element mask.</summary>
    public uint GetElementMask() => _elementMask;

    /// <summary>Return number of vertices.</summary>
    public uint GetVertexCount() => VertexCount;

    /// <summary>Return vertex size in bytes.</summary>
    public uint GetVertexSize() => VertexSize;

    /// <summary>Return vertex element, or null if it does not exist.</summary>
    public VertexElement? GetElement(VertexElementSemantic semantic, byte index = 0)
    {
        foreach (var element in _elements)
        {
            if (element.Semantic == semantic && element.Index == index)
                return element;
        }

        return null;
    }

    /// <summary>Return vertex element with specific type, or null if it does not exist.</summary>
    public VertexElement? GetElement(VertexElementType type, VertexElementSemantic semantic, byte index = 0)
    {
        foreach (var element in _elements)
        {
            if (element.Type == type && element.Semantic == semantic && element.Index == index)
                return element;
        }

        return null;
    }

    /// <summary>Return whether the buffer has the specified element semantic.</summary>
    public bool HasElement(VertexElementSemantic semantic, byte index = 0) => GetElement(semantic, index) is not null;

    /// <summary>Return whether the buffer has the specified element type and semantic.</summary>
    public bool HasElement(VertexElementType type, VertexElementSemantic semantic, byte index = 0) =>
        GetElement(type, semantic, index) is not null;

    /// <summary>Return offset of an element, or uint.MaxValue if it does not exist.</summary>
    public uint GetElementOffset(VertexElementSemantic semantic, byte index = 0) =>
        GetElement(semantic, index)?.Offset ?? uint.MaxValue;

    /// <summary>Return offset of an element with a specific type, or uint.MaxValue if it does not exist.</summary>
    public uint GetElementOffset(VertexElementType type, VertexElementSemantic semantic, byte index = 0) =>
        GetElement(type, semantic, index)?.Offset ?? uint.MaxValue;

    /// <summary>Return unpacked buffer data as count times element-count values, grouped by vertices.</summary>
    public IReadOnlyList<Vector4f> GetUnpackedData(uint start = 0, uint count = uint.MaxValue)
    {
        if (start >= VertexCount || count == 0 || !Shadowed)
            return Array.Empty<Vector4f>();

        // Clamp count to vertex buffer size.
        if (count == uint.MaxValue || unchecked(start + count) > VertexCount)
            count = VertexCount - start;

        var elementCount = _elements.Count;
        var result = new Vector4f[checked((int)(count * (uint)elementCount))];
        var destinationStride = checked((uint)(elementCount * Marshal.SizeOf<Vector4f>()));
        for (var i = 0; i < elementCount; ++i)
        {
            UnpackVertexData(_data, VertexSize, _elements[i], start, count, result.AsSpan(i), destinationStride);
        }

        return result;
    }

    /// <summary>Set data in the buffer from unpacked data.</summary>
    public void SetUnpackedData(ReadOnlySpan<Vector4f> data, uint start = 0, uint count = uint.MaxValue)
    {
        if (start >= VertexCount || count == 0)
            return;

        // Clamp count to vertex buffer size.
        if (count == uint.MaxValue || unchecked(start + count) > VertexCount)
            count = VertexCount - start;

        var elementCount = _elements.Count;
        var sourceStride = checked((uint)(elementCount * Marshal.SizeOf<Vector4f>()));
        var buffer = new byte[checked((int)(count * VertexSize))];
        for (var i = 0; i < elementCount; ++i)
            PackVertexData(data[i..], sourceStride, buffer, VertexSize, _elements[i], 0, count);

        buffer.AsSpan().CopyTo(_data.AsSpan(checked((int)(start * VertexSize)), checked((int)(count * VertexSize))));
        _rawBuffer?.UpdateRange(buffer, start * VertexSize, count * VertexSize);
    }

    /// <summary>Set data in the buffer from unpacked data.</summary>
    public void SetUnpackedData(IEnumerable<Vector4f> data, uint start = 0, uint count = uint.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(data);
        SetUnpackedData(data.ToArray().AsSpan(), start, count);
    }

    /// <summary>Return element with specified type and semantic from a vertex element list, or null if it does not exist.</summary>
    public static VertexElement? GetElement(IEnumerable<VertexElement> elements, VertexElementType type,
        VertexElementSemantic semantic, byte index = 0)
    {
        ArgumentNullException.ThrowIfNull(elements);
        foreach (var element in elements)
        {
            if (element.Type == type && element.Semantic == semantic && element.Index == index)
                return element;
        }

        return null;
    }

    /// <summary>Return whether an element list has a specified element type and semantic.</summary>
    public static bool HasElement(IEnumerable<VertexElement> elements, VertexElementType type,
        VertexElementSemantic semantic, byte index = 0) => GetElement(elements, type, semantic, index) is not null;

    /// <summary>Return element offset for specified type and semantic from a vertex element list.</summary>
    public static uint GetElementOffset(IEnumerable<VertexElement> elements, VertexElementType type,
        VertexElementSemantic semantic, byte index = 0) => GetElement(elements, type, semantic, index)?.Offset ?? uint.MaxValue;

    /// <summary>Return a vertex element list from a legacy element bitmask.</summary>
    public static List<VertexElement> GetElements(uint elementMask)
    {
        var result = new List<VertexElement>();
        for (var i = 0; i < GraphicsDefs.LegacyVertexElements.Length; ++i)
        {
            if ((elementMask & (1u << i)) != 0)
                result.Add(GraphicsDefs.LegacyVertexElements[i]);
        }

        return result;
    }

    /// <summary>Return vertex size from an element list.</summary>
    public static uint GetVertexSize(IEnumerable<VertexElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        var size = 0u;
        foreach (var element in elements)
            size += TypeSizes[(int)element.Type];
        return size;
    }

    /// <summary>Return vertex size for a legacy vertex element bitmask.</summary>
    public static uint GetVertexSize(VertexMask elementMask) => GetVertexSize(GetElements((uint)elementMask));

    /// <summary>Update offsets of vertex elements.</summary>
    public static void UpdateOffsets(IList<VertexElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var offset = 0u;
        for (var i = 0; i < elements.Count; ++i)
        {
            var element = elements[i];
            element.Offset = offset;
            elements[i] = element;
            offset += TypeSizes[(int)element.Type];
        }
    }

    /// <summary>Unpack given element data from vertex buffer into Vector4f. Stride is measured in bytes.</summary>
    public static void UnpackVertexData(ReadOnlySpan<byte> source, uint sourceStride, VertexElement element,
        uint start, uint count, Span<Vector4f> dest, uint destStride)
    {
        var sourceBytes = source.Slice(checked((int)(element.Offset + start * sourceStride)));
        var destBytes = MemoryMarshal.Cast<Vector4f, byte>(dest);
        var vector4Size = (uint)Marshal.SizeOf<Vector4f>();

        for (var i = 0u; i < count; ++i)
        {
            var sourceOffset = checked((int)(i * sourceStride));
            var destinationOffset = checked((int)(i * destStride));
            dest[destinationOffset / (int)vector4Size] = ReadElement(sourceBytes, sourceOffset, element);
        }
    }

    /// <summary>Pack given element data from Vector4f into vertex buffer. Stride is measured in bytes.</summary>
    public static void PackVertexData(ReadOnlySpan<Vector4f> source, uint sourceStride, Span<byte> dest,
        uint destStride, VertexElement element, uint start, uint count)
    {
        var sourceBytes = MemoryMarshal.Cast<Vector4f, byte>(source);
        var destBytes = dest.Slice(checked((int)(element.Offset + start * destStride)));
        var vector4Size = (uint)Marshal.SizeOf<Vector4f>();

        for (var i = 0u; i < count; ++i)
        {
            var sourceOffset = checked((int)(i * sourceStride));
            var destinationOffset = checked((int)(i * destStride));
            WriteElement(destBytes, destinationOffset, element, source[sourceOffset / (int)vector4Size]);
        }
    }

    /// <summary>Shuffle unpacked vertex data according to another vertex format.</summary>
    public static void ShuffleUnpackedVertexData(uint vertexCount, ReadOnlySpan<Vector4f> source,
        IReadOnlyList<VertexElement> sourceElements, Span<Vector4f> dest, IReadOnlyList<VertexElement> destElements,
        bool setMissingElementsToZero = true)
    {
        var sourceElementCount = sourceElements.Count;
        var destElementCount = destElements.Count;

        if (setMissingElementsToZero)
            dest[..checked((int)(vertexCount * (uint)destElementCount))].Fill(Vector4f.ZERO);

        for (var sourceElementIndex = 0; sourceElementIndex < sourceElementCount; ++sourceElementIndex)
        {
            var sourceElement = sourceElements[sourceElementIndex];
            var destElementIndex = -1;
            for (var i = 0; i < destElementCount; ++i)
            {
                var destElement = destElements[i];
                if (sourceElement.Semantic == destElement.Semantic && sourceElement.Index == destElement.Index)
                {
                    destElementIndex = i;
                    break;
                }
            }

            if (destElementIndex < 0)
                continue;

            for (var i = 0u; i < vertexCount; ++i)
            {
                dest[checked((int)(i * (uint)destElementCount + (uint)destElementIndex))] =
                    source[checked((int)(i * (uint)sourceElementCount + (uint)sourceElementIndex))];
            }
        }
    }

    /// <summary>Enable shadowing in CPU memory.</summary>
    public void SetShadowed(bool enable)
    {
        _shadowedPending = enable;
        Shadowed = enable;
    }

    /// <summary>Set size and vertex elements and dynamic mode. Previous data will be lost.</summary>
    public bool SetSize(uint vertexCount, IEnumerable<VertexElement> elements, bool dynamic = false)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var elementList = elements.ToList();
        UpdateOffsets(elementList);
        VertexCount = vertexCount;
        _elements = elementList;
        _elementMask = CalculateElementMask(elementList);
        _layoutSignature = null;
        VertexSize = GetVertexSize(elementList);
        IsDynamic = dynamic;
        var size = checked((int)(vertexCount * VertexSize));
        if (_data.Length != size)
            _data = new byte[size];
        MarkPipelineStateHashDirty();

        if (_rawBuffer is not null)
        {
            _rawBuffer.Destroy();
            _rawBuffer.Dispose();
        }
        _rawBuffer = new VertexRawBuffer(_flags);
        var flags = (_shadowedPending ? BufferFlag.Shadowed : BufferFlag.None)
            | (dynamic ? BufferFlag.Dynamic : BufferFlag.None);
        if (elementList.Count != 0 && elementList[0].StepRate != 0)
            flags |= BufferFlag.PerInstanceData;
        return _rawBuffer.CreateBuffer(new RawBufferParams(
            BufferType.Vertex,
            checked((uint)size),
            VertexSize,
            flags));
    }

    /// <summary>Set size and vertex elements and dynamic mode using a legacy element bitmask.</summary>
    public bool SetSize(uint vertexCount, uint elementMask, bool dynamic = false) =>
        SetSize(vertexCount, GetElements(elementMask), dynamic);

    /// <summary>Set size using a typed legacy element bitmask.</summary>
    public bool SetSize(uint vertexCount, VertexMask elementMask, bool dynamic = false) =>
        SetSize(vertexCount, (uint)elementMask, dynamic);

    /// <summary>Return whether is dynamic.</summary>
    public bool IsDynamicBuffer() => IsDynamic;

    /// <summary>Return the underlying raw buffer.</summary>
    public RawBuffer? GetRawBuffer() => _rawBuffer;

    /// <summary>Map the buffer into CPU memory.</summary>
    public Span<byte> Map() => Shadowed ? _data : Span<byte>.Empty;

    /// <summary>Unmap the buffer and upload modified data.</summary>
    public void Unmap() => _rawBuffer?.Update(_data);

    /// <summary>Update buffer data range without allocating.</summary>
    public void UpdateRange(ReadOnlySpan<byte> data, uint offset, uint size)
    {
        data[..checked((int)size)].CopyTo(_data.AsSpan(checked((int)offset), checked((int)size)));
        _rawBuffer?.UpdateRange(data, offset, size);
    }

    /// <summary>Set raw buffer data.</summary>
    public void SetData(IEnumerable<byte> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        SetData(data.ToArray().AsSpan());
    }

    /// <summary>Set raw buffer data.</summary>
    public void SetData(ReadOnlySpan<byte> data)
    {
        _data = data.ToArray();
        _rawBuffer?.Update(_data);
    }

    /// <summary>Set raw buffer data.</summary>
    public void SetData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        SetData(data.AsSpan());
    }

    /// <summary>Return the managed input-layout signature.</summary>
    internal string GetLayoutSignature()
    {
        if (_layoutSignature is not null)
            return _layoutSignature;
        if (_elements.Count == 0)
            return _layoutSignature = string.Empty;

        var builder = new System.Text.StringBuilder(_elements.Count * 20);
        for (var i = 0; i < _elements.Count; ++i)
        {
            if (i > 0)
                builder.Append('|');

            var element = _elements[i];
            builder.Append((int)element.Semantic);
            builder.Append(':');
            builder.Append(element.Index);
            builder.Append(':');
            builder.Append((int)element.Type);
            builder.Append(':');
            builder.Append(element.Offset);
            builder.Append(':');
            builder.Append(element.StepRate);
        }

        _layoutSignature = builder.ToString();
        return _layoutSignature;
    }

    private void UpdateOffsets()
    {
        UpdateOffsets(_elements);
        VertexSize = GetVertexSize(_elements);
        _elementMask = CalculateElementMask(_elements);
    }

    private static uint CalculateElementMask(IEnumerable<VertexElement> elements)
    {
        var mask = 0u;
        foreach (var element in elements)
        {
            for (var i = 0; i < GraphicsDefs.LegacyVertexElements.Length; ++i)
            {
                var legacy = GraphicsDefs.LegacyVertexElements[i];
                if (legacy.Type == element.Type && legacy.Semantic == element.Semantic && legacy.Index == element.Index)
                    mask |= 1u << i;
            }
        }

        return mask;
    }

    private static Vector4f ReadElement(ReadOnlySpan<byte> source, int offset, VertexElement element)
    {
        var bytes = source.Slice(offset);
        return element.Type switch
        {
            VertexElementType.Int => new Vector4f(MemoryMarshal.Read<int>(bytes), 0, 0, 0),
            VertexElementType.Float => new Vector4f(MemoryMarshal.Read<float>(bytes), 0, 0, 0),
            VertexElementType.Vector2 => ReadVector2(bytes, element.Semantic == VertexElementSemantic.Position),
            VertexElementType.Vector3 => element.Semantic == VertexElementSemantic.Position
                ? new Vector4f(MemoryMarshal.Read<Vector3f>(bytes), 1)
                : new Vector4f(MemoryMarshal.Read<Vector3f>(bytes), 0),
            VertexElementType.Vector4 => MemoryMarshal.Read<Vector4f>(bytes),
            VertexElementType.UByte4 => new Vector4f(bytes[0], bytes[1], bytes[2], bytes[3]),
            VertexElementType.UByte4Norm => new Vector4f(bytes[0] / 255.0f, bytes[1] / 255.0f, bytes[2] / 255.0f, bytes[3] / 255.0f),
            _ => throw new ArgumentOutOfRangeException(nameof(element))
        };
    }

    private static void WriteElement(Span<byte> dest, int offset, VertexElement element, Vector4f value)
    {
        var bytes = dest.Slice(offset);
        switch (element.Type)
        {
            case VertexElementType.Int:
                MemoryMarshal.Write(bytes, (int)value.X);
                break;
            case VertexElementType.Float:
                MemoryMarshal.Write(bytes, value.X);
                break;
            case VertexElementType.Vector2:
                MemoryMarshal.Write(bytes, new Vector2f(value.X, value.Y));
                break;
            case VertexElementType.Vector3:
                MemoryMarshal.Write(bytes, new Vector3f(value.X, value.Y, value.Z));
                break;
            case VertexElementType.Vector4:
                MemoryMarshal.Write(bytes, value);
                break;
            case VertexElementType.UByte4:
                bytes[0] = FloatToUByte(value.X);
                bytes[1] = FloatToUByte(value.Y);
                bytes[2] = FloatToUByte(value.Z);
                bytes[3] = FloatToUByte(value.W);
                break;
            case VertexElementType.UByte4Norm:
                if (element.Semantic == VertexElementSemantic.BlendWeights)
                {
                    var weightSum = value.DotProduct(Vector4f.ONE);
                    if (weightSum < MathDefs.Epsilon)
                    {
                        bytes[0] = 255;
                        bytes[1] = 0;
                        bytes[2] = 0;
                        bytes[3] = 0;
                    }
                    else
                    {
                        Span<byte> packed = stackalloc byte[4];
                        packed[0] = FloatToUByte(value.X * 255.0f / weightSum);
                        packed[1] = FloatToUByte(value.Y * 255.0f / weightSum);
                        packed[2] = FloatToUByte(value.Z * 255.0f / weightSum);
                        packed[3] = FloatToUByte(value.W * 255.0f / weightSum);
                        var underflowError = 255 - packed[0] - packed[1] - packed[2] - packed[3];
                        var patchIndex = 0;
                        for (var i = 1; i < 4; ++i)
                        {
                            if (packed[i] > packed[patchIndex])
                                patchIndex = i;
                        }

                        packed[patchIndex] = unchecked((byte)(packed[patchIndex] + underflowError));
                        packed.CopyTo(bytes);
                    }
                }
                else
                {
                    bytes[0] = FloatToUByte(value.X * 255.0f);
                    bytes[1] = FloatToUByte(value.Y * 255.0f);
                    bytes[2] = FloatToUByte(value.Z * 255.0f);
                    bytes[3] = FloatToUByte(value.W * 255.0f);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(element));
        }
    }

    private static byte FloatToUByte(float value) => unchecked((byte)(value + 0.5f));

    private static Vector4f ReadVector2(ReadOnlySpan<byte> source, bool position)
    {
        var value = MemoryMarshal.Read<Vector2f>(source);
        return new Vector4f(value.X, value.Y, 0, position ? 1 : 0);
    }

    protected override uint RecalculatePipelineStateHash()
    {
        var hash = 0u;
        foreach (var element in _elements)
            CombineHash(ref hash, element.ToHash());
        return Math.Max(1u, hash);
    }

    /// <summary>Set the debug name.</summary>
    public void SetDebugName(string debugName)
    {
        _debugName = debugName;
        _rawBuffer?.SetDebugName(debugName);
    }

    public string GetDebugName() => _debugName;

    public override void Dispose()
    {
        if (_rawBuffer is not null)
        {
            _rawBuffer.Destroy();
            _rawBuffer.Dispose();
            _rawBuffer = null;
        }
        base.Dispose();
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}

/// <summary>Vertex Buffer of dynamic size. Resize policy is similar to standard vector.</summary>
public sealed class DynamicVertexBuffer
{
    private readonly VertexBuffer _vertexBuffer = new();
    private byte[] _shadowData = [];
    private bool _vertexBufferNeedResize;

    private uint _vertexSize;
    private uint _numVertices;
    private uint _maxNumVertices;

    /// <summary>Initialize dynamic vertex buffer.</summary>
    public bool Initialize(uint vertexCount, IEnumerable<VertexElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        _numVertices = 0;
        _maxNumVertices = vertexCount;

        if (!_vertexBuffer.SetSize(vertexCount, elements, true))
        {
            Log.Logger.LogError("Failed to create DynamicVertexBuffer");
            return false;
        }

        _vertexSize = _vertexBuffer.GetVertexSize();
        _shadowData = new byte[checked((int)(_vertexSize * _maxNumVertices))];
        return true;
    }

    /// <summary>Discard existing content of the buffer.</summary>
    public void Discard()
    {
        _numVertices = 0;
    }

    /// <summary>Commit all added data to GPU.</summary>
    public void Commit()
    {
        if (_numVertices == 0)
            return;

        if (_vertexBufferNeedResize)
        {
            _vertexBufferNeedResize = false;
            if (!_vertexBuffer.SetSize(_maxNumVertices, _vertexBuffer.GetElements(), true))
            {
                Log.Logger.LogError(
                    "Failed to grow DynamicVertexBuffer to {MaxNumVertices} vertices with stride {VertexSize}",
                    _maxNumVertices,
                    _vertexSize);
                return;
            }
        }

        _vertexBuffer.UpdateRange(_shadowData.AsSpan(0, checked((int)(_numVertices * _vertexSize))), 0,
            _numVertices * _vertexSize);
    }

    /// <summary>Allocate vertices. Returns index of first vertex and writable buffer of sufficient size.</summary>
    public (uint StartVertex, Memory<byte> Data) AddVertices(uint count)
    {
        var startVertex = _numVertices;
        var newMaxNumVertices = unchecked(startVertex + count);
        if (newMaxNumVertices > _maxNumVertices)
            GrowBuffer(newMaxNumVertices);

        _numVertices += count;
        var start = checked((int)(startVertex * _vertexSize));
        var size = checked((int)(count * _vertexSize));
        return (startVertex, _shadowData.AsMemory(start, size));
    }

    /// <summary>Store vertices. Returns index of first vertex.</summary>
    public uint AddVertices(uint count, ReadOnlySpan<byte> data)
    {
        var indexAndData = AddVertices(count);
        data[..checked((int)(count * _vertexSize))].CopyTo(indexAndData.Data.Span);
        return indexAndData.StartVertex;
    }

    /// <summary>Return the vertex buffer.</summary>
    public VertexBuffer GetVertexBuffer() => _vertexBuffer;

    /// <summary>Return number of vertices.</summary>
    public uint GetVertexCount() => _numVertices;

    /// <summary>Set the debug name.</summary>
    public void SetDebugName(string debugName)
    {
        _vertexBuffer.SetDebugName(debugName);
    }

    private void GrowBuffer(uint newMaxNumVertices)
    {
        _maxNumVertices = Math.Max(newMaxNumVertices, _maxNumVertices > 0 ? 2 * _maxNumVertices : 128);
        Array.Resize(ref _shadowData, checked((int)(_maxNumVertices * _vertexSize)));
        _vertexBufferNeedResize = true;
    }
}
