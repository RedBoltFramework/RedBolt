namespace RedBolt;

/// <summary>Instancing buffer compositor.</summary>
public sealed class InstancingBuffer : Object
{
    /// <summary>Stride of one element in bytes.</summary>
    public const uint ElementStride = 4 * sizeof(float);

    private InstancingBufferSettings _settings = new();
    private DynamicVertexBuffer? _vertexBuffer;
    private Memory<byte> _currentInstanceData;

    public void SetSettings(InstancingBufferSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!_settings.Equals(settings))
        {
            _settings = new InstancingBufferSettings
            {
                EnableInstancing = settings.EnableInstancing,
                FirstInstancingTexCoord = settings.FirstInstancingTexCoord,
                NumInstancingTexCoords = settings.NumInstancingTexCoords,
                StepRate = settings.StepRate
            };
            Initialize();
        }
    }

    /// <summary>Begin buffer composition.</summary>
    public void Begin()
    {
        _vertexBuffer?.Discard();
    }

    /// <summary>End buffer composition and commit added instances to GPU.</summary>
    public void End()
    {
        _vertexBuffer?.Commit();
    }

    /// <summary>Return index of next added instance.</summary>
    public uint GetNextInstanceIndex() => _vertexBuffer!.GetVertexCount();

    /// <summary>Add instance to buffer. Use SetElements to fill it after.</summary>
    public uint AddInstance()
    {
        var indexAndData = _vertexBuffer!.AddVertices(1);
        _currentInstanceData = indexAndData.Data;
        return indexAndData.StartVertex;
    }

    /// <summary>Set one or more 4-float elements in current instance.</summary>
    public void SetElements(ReadOnlySpan<byte> data, uint index, uint count)
    {
        var offset = checked((int)(index * ElementStride));
        var size = checked((int)(count * ElementStride));
        data[..size].CopyTo(_currentInstanceData.Span.Slice(offset, size));
    }

    /// <summary>Set one or more 4-float elements from an unmanaged value (matches C++ SetElements(const void*, ...)).</summary>
    public void SetElements<T>(in T value, uint index, uint count) where T : unmanaged
    {
        SetElements(System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref System.Runtime.CompilerServices.Unsafe.AsRef(in value), 1)),
            index, count);
    }

    /// <summary>Return settings.</summary>
    public InstancingBufferSettings GetSettings() => _settings;

    /// <summary>Return vertex buffer.</summary>
    public VertexBuffer? GetVertexBuffer() => _vertexBuffer?.GetVertexBuffer();

    /// <summary>Return whether instancing is enabled.</summary>
    public bool IsEnabled() => _settings.EnableInstancing;

    private void Initialize()
    {
        _vertexBuffer = null;

        if (_settings.EnableInstancing)
        {
            var vertexElements = new List<VertexElement>();
            for (uint i = 0; i < _settings.NumInstancingTexCoords; ++i)
            {
                var index = _settings.FirstInstancingTexCoord + i;
                vertexElements.Add(new VertexElement(VertexElementType.Vector4, VertexElementSemantic.TexCoord,
                    checked((byte)index), _settings.StepRate));
            }

            _vertexBuffer = new DynamicVertexBuffer();
            _vertexBuffer.SetDebugName("InstancingBuffer");
            _vertexBuffer.Initialize(128, vertexElements);
        }
    }
}
