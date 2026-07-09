using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>
/// Pool for different resources used by renderer.
/// </summary>
public class RenderPool : Object
{
    private const uint InitialScratchBufferSize = 64u * 1024u;

    private sealed class TextureCacheEntry
    {
        public RawTexture Texture { get; init; } = null!;

        public FrameIndex LastUsedFrame { get; set; }
    }

    private sealed class TextureCacheEntryGroup
    {
        public List<TextureCacheEntry> Entries { get; } = [];

        public uint NumUsed { get; set; }
    }

    private readonly RenderDevice _renderDevice;
    private readonly Dictionary<ulong, RawBuffer> _uniformBuffers = [];
    private readonly Dictionary<RawTextureParams, TextureCacheEntryGroup> _transientTextures = [];
    private readonly Dictionary<(RawTextureParams Params, nint PersistenceKey), TextureCacheEntry> _persistentTextures = [];
    private readonly List<nint> _scratchBufferAllocations = [];
    private readonly List<nint> _temporaryScratchBufferAllocations = [];
    private nint _scratchBuffer;
    private uint _scratchBufferCapacity = InitialScratchBufferSize;
    private uint _scratchBufferOffset;
    private uint _temporaryScratchBufferAllocationsSize;
    private uint _numAddedTextures;
    private uint _numRemovedTextures;
    private FrameIndex _lastLogFrame;

    public RenderPool(RenderDevice renderDevice)
    {
        this._renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
        _scratchBuffer = Marshal.AllocHGlobal((nint)_scratchBufferCapacity);
        if (_scratchBuffer == nint.Zero)
            throw new OutOfMemoryException("Failed to allocate RenderPool scratch buffer.");
    }

    public RenderPoolSettings Settings { get; } = new();

    public void Invalidate()
    {
        _uniformBuffers.Clear();
        _transientTextures.Clear();
        _persistentTextures.Clear();
        _numAddedTextures = 0u;
        _numRemovedTextures = 0u;
        _lastLogFrame = FrameIndex.None;
    }

    public void Restore()
    {
    }

    public void OnFrameEnd()
    {
        RecycleTextures();
    }

    public RenderDevice GetRenderDevice() => _renderDevice;

    /// <summary>
    /// Return uniform buffer.
    /// Buffers are recycled immediately, pass different ids to get different buffers.
    /// Don't store the pointer between frames.
    /// </summary>
    public RawBuffer GetUniformBuffer(uint id, uint size)
    {
        var quantizedSize = (size + 511u) / 512u * 512u;
        var key = ((ulong)id << 32) | quantizedSize;
        if (!_uniformBuffers.TryGetValue(key, out var uniformBuffer))
        {
            uniformBuffer = new RawBuffer(
                new RawBufferParams(BufferType.Uniform, quantizedSize, 0u, BufferFlag.Dynamic | BufferFlag.Discard));
            _uniformBuffers[key] = uniformBuffer;
        }

        return uniformBuffer;
    }

    /// <summary>
    /// Return texture.
    /// Transient textures are recycled on demand, same parameters will return different textures between recycles.
    /// Persistent textures are not recycled, same parameters will return same texture.
    /// </summary>
    public RawTexture GetTexture(RawTextureParams parameters, nint persistenceKey = default)
    {
        return persistenceKey != default
            ? GetPersistentTexture(parameters, persistenceKey)
            : GetTransientTexture(parameters);
    }

    /// <summary>
    /// Allocate scratch buffer. Don't store the pointer between frames.
    /// </summary>
    public nint AllocateScratchBuffer(uint size)
    {
        var alignedSize = AlignScratchSize(size);
        if (_scratchBufferOffset + alignedSize > _scratchBufferCapacity)
        {
            var temporaryBuffer = Marshal.AllocHGlobal((nint)alignedSize);
            if (temporaryBuffer == nint.Zero)
                throw new OutOfMemoryException("Failed to allocate temporary RenderPool scratch buffer.");

            _temporaryScratchBufferAllocations.Add(temporaryBuffer);
            _temporaryScratchBufferAllocationsSize += alignedSize;
            return temporaryBuffer;
        }

        var buffer = _scratchBuffer + (int)_scratchBufferOffset;
        _scratchBufferAllocations.Add(buffer);
        _scratchBufferOffset += alignedSize;
        return buffer;
    }

    /// <summary>
    /// Release scratch buffer.
    /// </summary>
    public void ReleaseScratchBuffer(nint buffer)
    {
        var isTemporary = _temporaryScratchBufferAllocations.Remove(buffer);
        var isStandard = _scratchBufferAllocations.Remove(buffer);
        if (isTemporary == isStandard)
            throw new InvalidOperationException("Unknown scratch buffer allocation.");

        if (isTemporary)
            Marshal.FreeHGlobal(buffer);

        if (_scratchBufferAllocations.Count == 0 && _temporaryScratchBufferAllocations.Count == 0)
        {
            if (_temporaryScratchBufferAllocationsSize != 0u)
                ResizeScratchBuffer(((_scratchBufferCapacity + _temporaryScratchBufferAllocationsSize) * 3u) / 2u);

            _scratchBufferOffset = 0u;
            _temporaryScratchBufferAllocationsSize = 0u;
        }
    }

    /// <summary>
    /// Recycle all transient textures.
    /// </summary>
    public void RecycleTextures()
    {
        CleanupOldTextures();
        CleanupTexturesExceedingQuota();

        var currentFrame = _renderDevice.GetFrameIndex();
        var framesSinceLastLog = (long)currentFrame - (long)_lastLogFrame;
        if ((_numAddedTextures > 0u || _numRemovedTextures > 0u) && framesSinceLastLog >= Settings.LogPeriod)
        {
            var totalTextures = _transientTextures.Values.Sum(static group => group.Entries.Count) + _persistentTextures.Count;
            _lastLogFrame = currentFrame;
            _numAddedTextures = 0u;
            _numRemovedTextures = 0u;
            _ = totalTextures;
        }
    }

    private long GetAge(TextureCacheEntry entry)
    {
        var currentFrame = _renderDevice.GetFrameIndex();
        return (long)currentFrame - (long)entry.LastUsedFrame;
    }

    private RawTexture GetTransientTexture(RawTextureParams parameters)
    {
        if (!_transientTextures.TryGetValue(parameters, out var allocations))
        {
            allocations = new TextureCacheEntryGroup();
            _transientTextures[parameters] = allocations;
        }

        if (allocations.NumUsed < allocations.Entries.Count)
        {
            var entry = allocations.Entries[(int)allocations.NumUsed++];
            entry.LastUsedFrame = _renderDevice.GetFrameIndex();
            return entry.Texture;
        }

        var texture = new RawTexture(parameters);
        var newEntry = new TextureCacheEntry
        {
            Texture = texture,
            LastUsedFrame = _renderDevice.GetFrameIndex()
        };
        allocations.Entries.Add(newEntry);
        ++allocations.NumUsed;
        ++_numAddedTextures;
        return texture;
    }

    private RawTexture GetPersistentTexture(RawTextureParams parameters, nint persistenceKey)
    {
        var key = (parameters, persistenceKey);
        if (!_persistentTextures.TryGetValue(key, out var entry))
        {
            entry = new TextureCacheEntry
            {
                Texture = new RawTexture(parameters)
            };
            _persistentTextures[key] = entry;
            ++_numAddedTextures;
        }

        entry.LastUsedFrame = _renderDevice.GetFrameIndex();
        return entry.Texture;
    }

    private void CleanupOldTextures()
    {
        foreach (var allocations in _transientTextures.Values)
        {
            allocations.NumUsed = 0u;
            var removed = allocations.Entries.RemoveAll(entry => GetAge(entry) >= Settings.TextureCacheMaxFrames);
            _numRemovedTextures += (uint)Math.Max(removed, 0);
        }

        foreach (var emptyGroup in _transientTextures.Where(static pair => pair.Value.Entries.Count == 0).Select(static pair => pair.Key).ToArray())
            _transientTextures.Remove(emptyGroup);

        foreach (var oldTexture in _persistentTextures.Where(pair => GetAge(pair.Value) >= Settings.TextureCacheMaxFrames).Select(static pair => pair.Key).ToArray())
        {
            _persistentTextures.Remove(oldTexture);
            ++_numRemovedTextures;
        }
    }

    private void CleanupTexturesExceedingQuota()
    {
        var totalTextures = 0;
        var totalOldTextures = 0;

        foreach (var allocations in _transientTextures.Values)
        {
            foreach (var entry in allocations.Entries)
            {
                ++totalTextures;
                if (GetAge(entry) >= Settings.TextureCacheMinFrames)
                    ++totalOldTextures;
            }
        }

        foreach (var entry in _persistentTextures.Values)
        {
            ++totalTextures;
            if (GetAge(entry) >= Settings.TextureCacheMinFrames)
                ++totalOldTextures;
        }

        var maxOldTextures = (int)Math.Ceiling(totalTextures * (1.0f - Settings.TextureCacheMinLoadFactor));
        if (totalOldTextures < maxOldTextures)
            return;

        foreach (var allocations in _transientTextures.Values)
        {
            var removed = allocations.Entries.RemoveAll(entry => GetAge(entry) >= Settings.TextureCacheMinFrames);
            _numRemovedTextures += (uint)Math.Max(removed, 0);
        }

        foreach (var emptyGroup in _transientTextures.Where(static pair => pair.Value.Entries.Count == 0).Select(static pair => pair.Key).ToArray())
            _transientTextures.Remove(emptyGroup);

        foreach (var oldTexture in _persistentTextures.Where(pair => GetAge(pair.Value) >= Settings.TextureCacheMinFrames).Select(static pair => pair.Key).ToArray())
        {
            _persistentTextures.Remove(oldTexture);
            ++_numRemovedTextures;
        }
    }

    private static uint AlignScratchSize(uint size)
    {
        return (size + 15u) & ~15u;
    }

    private void ResizeScratchBuffer(uint newCapacity)
    {
        newCapacity = Math.Max(newCapacity, InitialScratchBufferSize);
        Marshal.FreeHGlobal(_scratchBuffer);
        _scratchBuffer = Marshal.AllocHGlobal((nint)newCapacity);
        if (_scratchBuffer == nint.Zero)
            throw new OutOfMemoryException("Failed to resize RenderPool scratch buffer.");

        _scratchBufferCapacity = newCapacity;
    }

    ~RenderPool()
    {
        foreach (var temporaryBuffer in _temporaryScratchBufferAllocations)
            Marshal.FreeHGlobal(temporaryBuffer);

        if (_scratchBuffer != nint.Zero)
            Marshal.FreeHGlobal(_scratchBuffer);
    }
}
