namespace RedBolt;

/// <summary>Instancing buffer compositor.</summary>
public class InstancingBuffer : Object
{
    /// <summary>Stride of one element in bytes.</summary>
    public const int ElementStride = 4 * sizeof(float);

    private InstancingBufferSettings _settings = new();
    private DynamicVertexBuffer? _vertexBuffer;
    private Memory<byte> _currentInstanceData;

    public void SetSettings(InstancingBufferSettings settings)
    {
        if (_settings != settings)
        {
            _settings = settings;
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
    public int NextInstanceIndex => checked((int)_vertexBuffer!.GetVertexCount());

    /// <summary>Add instance to buffer. Use SetElements to fill it after.</summary>
    public int AddInstance()
    {
        var indexAndData = _vertexBuffer!.AddVertices(1);
        _currentInstanceData = indexAndData.Data;
        return checked((int)indexAndData.StartVertex);
    }

    /// <summary>Set one or more 4-float elements in current instance.</summary>
    public void SetElements(ReadOnlySpan<byte> data, int index, int count)
    {
        var offset = checked(index * ElementStride);
        var size = checked(count * ElementStride);
        data[..size].CopyTo(_currentInstanceData.Span.Slice(offset, size));
    }

    /// <summary>Set one or more 4-float elements from an unmanaged value (matches C++ SetElements(const void*, ...)).</summary>
    public void SetElements<T>(in T value, int index, int count) where T : unmanaged
    {
        SetElements(System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref System.Runtime.CompilerServices.Unsafe.AsRef(in value), 1)),
            index, count);
    }

    /// <summary>Return settings.</summary>
    public ref readonly InstancingBufferSettings Settings => ref _settings;

    /// <summary>Return vertex buffer.</summary>
    public VertexBuffer? VertexBuffer => _vertexBuffer?.GetVertexBuffer();

    /// <summary>Return whether instancing is enabled.</summary>
    public bool IsEnabled => _settings.EnableInstancing;

    public override void Dispose()
    {
        _currentInstanceData = default;
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        base.Dispose();
    }

    private void Initialize()
    {
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;

        if (_settings.EnableInstancing)
        {
            var vertexElements = new List<VertexElement>();
            for (var i = 0; i < _settings.NumInstancingTexCoords; ++i)
            {
                var index = _settings.FirstInstancingTexCoord + i;
                vertexElements.Add(new VertexElement(VertexElementType.Vector4, VertexElementSemantic.TexCoord,
                    checked((byte)index), checked((uint)_settings.StepRate)));
            }

            _vertexBuffer = new DynamicVertexBuffer();
            _vertexBuffer.SetDebugName("InstancingBuffer");
            _vertexBuffer.Initialize(128, vertexElements);
        }
    }
}
