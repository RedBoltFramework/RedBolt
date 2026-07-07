namespace RedBolt;

public class RenderPool : Object
{
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

    private readonly RenderDevice renderDevice;
    private readonly Dictionary<ulong, RawBuffer> uniformBuffers = [];
    private readonly Dictionary<RawTextureParams, TextureCacheEntryGroup> transientTextures = [];
    private readonly Dictionary<(RawTextureParams Params, nint PersistenceKey), TextureCacheEntry> persistentTextures = [];
    private uint numAddedTextures;
    private uint numRemovedTextures;
    private FrameIndex lastLogFrame;

    public RenderPool(RenderDevice renderDevice)
    {
        this.renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
    }

    public RenderPoolSettings Settings { get; } = new();

    public void Invalidate()
    {
        uniformBuffers.Clear();
        transientTextures.Clear();
        persistentTextures.Clear();
        numAddedTextures = 0u;
        numRemovedTextures = 0u;
        lastLogFrame = FrameIndex.None;
    }

    public void Restore()
    {
    }

    public void OnFrameEnd()
    {
        RecycleTextures();
    }

    public RenderDevice GetRenderDevice() => renderDevice;

    public RawBuffer GetUniformBuffer(uint id, uint size)
    {
        var quantizedSize = (size + 511u) / 512u * 512u;
        var key = ((ulong)id << 32) | quantizedSize;
        if (!uniformBuffers.TryGetValue(key, out var uniformBuffer))
        {
            uniformBuffer = new RawBuffer(
                new RawBufferParams(BufferType.Uniform, quantizedSize, 0u, BufferFlag.Dynamic | BufferFlag.Discard));
            uniformBuffers[key] = uniformBuffer;
        }

        return uniformBuffer;
    }

    public RawTexture GetTexture(RawTextureParams parameters, nint persistenceKey = default)
    {
        return persistenceKey != default
            ? GetPersistentTexture(parameters, persistenceKey)
            : GetTransientTexture(parameters);
    }

    public void RecycleTextures()
    {
        CleanupOldTextures();
        CleanupTexturesExceedingQuota();

        var currentFrame = renderDevice.GetFrameIndex();
        var framesSinceLastLog = (long)currentFrame - (long)lastLogFrame;
        if ((numAddedTextures > 0u || numRemovedTextures > 0u) && framesSinceLastLog >= Settings.LogPeriod)
        {
            var totalTextures = transientTextures.Values.Sum(static group => group.Entries.Count) + persistentTextures.Count;
            lastLogFrame = currentFrame;
            numAddedTextures = 0u;
            numRemovedTextures = 0u;
            _ = totalTextures;
        }
    }

    private long GetAge(TextureCacheEntry entry)
    {
        var currentFrame = renderDevice.GetFrameIndex();
        return (long)currentFrame - (long)entry.LastUsedFrame;
    }

    private RawTexture GetTransientTexture(RawTextureParams parameters)
    {
        if (!transientTextures.TryGetValue(parameters, out var allocations))
        {
            allocations = new TextureCacheEntryGroup();
            transientTextures[parameters] = allocations;
        }

        if (allocations.NumUsed < allocations.Entries.Count)
        {
            var entry = allocations.Entries[(int)allocations.NumUsed++];
            entry.LastUsedFrame = renderDevice.GetFrameIndex();
            return entry.Texture;
        }

        var texture = new RawTexture(parameters);
        var newEntry = new TextureCacheEntry
        {
            Texture = texture,
            LastUsedFrame = renderDevice.GetFrameIndex()
        };
        allocations.Entries.Add(newEntry);
        ++allocations.NumUsed;
        ++numAddedTextures;
        return texture;
    }

    private RawTexture GetPersistentTexture(RawTextureParams parameters, nint persistenceKey)
    {
        var key = (parameters, persistenceKey);
        if (!persistentTextures.TryGetValue(key, out var entry))
        {
            entry = new TextureCacheEntry
            {
                Texture = new RawTexture(parameters)
            };
            persistentTextures[key] = entry;
            ++numAddedTextures;
        }

        entry.LastUsedFrame = renderDevice.GetFrameIndex();
        return entry.Texture;
    }

    private void CleanupOldTextures()
    {
        foreach (var allocations in transientTextures.Values)
        {
            allocations.NumUsed = 0u;
            var removed = allocations.Entries.RemoveAll(entry => GetAge(entry) >= Settings.TextureCacheMaxFrames);
            numRemovedTextures += (uint)Math.Max(removed, 0);
        }

        foreach (var emptyGroup in transientTextures.Where(static pair => pair.Value.Entries.Count == 0).Select(static pair => pair.Key).ToArray())
            transientTextures.Remove(emptyGroup);

        foreach (var oldTexture in persistentTextures.Where(pair => GetAge(pair.Value) >= Settings.TextureCacheMaxFrames).Select(static pair => pair.Key).ToArray())
        {
            persistentTextures.Remove(oldTexture);
            ++numRemovedTextures;
        }
    }

    private void CleanupTexturesExceedingQuota()
    {
        var totalTextures = 0;
        var totalOldTextures = 0;

        foreach (var allocations in transientTextures.Values)
        {
            foreach (var entry in allocations.Entries)
            {
                ++totalTextures;
                if (GetAge(entry) >= Settings.TextureCacheMinFrames)
                    ++totalOldTextures;
            }
        }

        foreach (var entry in persistentTextures.Values)
        {
            ++totalTextures;
            if (GetAge(entry) >= Settings.TextureCacheMinFrames)
                ++totalOldTextures;
        }

        var maxOldTextures = (int)Math.Ceiling(totalTextures * (1.0f - Settings.TextureCacheMinLoadFactor));
        if (totalOldTextures < maxOldTextures)
            return;

        foreach (var allocations in transientTextures.Values)
        {
            var removed = allocations.Entries.RemoveAll(entry => GetAge(entry) >= Settings.TextureCacheMinFrames);
            numRemovedTextures += (uint)Math.Max(removed, 0);
        }

        foreach (var emptyGroup in transientTextures.Where(static pair => pair.Value.Entries.Count == 0).Select(static pair => pair.Key).ToArray())
            transientTextures.Remove(emptyGroup);

        foreach (var oldTexture in persistentTextures.Where(pair => GetAge(pair.Value) >= Settings.TextureCacheMinFrames).Select(static pair => pair.Key).ToArray())
        {
            persistentTextures.Remove(oldTexture);
            ++numRemovedTextures;
        }
    }
}
