// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderAPI;

using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;

/// <summary>
/// Pool for different resources used by renderer.
/// </summary>
public class RenderPool : RedObject
{
    private const int InitialScratchBufferSize = 64 * 1024;

    private sealed class TextureCacheEntry
    {
        public RawTexture Texture { get; init; } = null!;

        public FrameIndex LastUsedFrame { get; set; }
    }

    private sealed class TextureCacheEntryGroup
    {
        public List<TextureCacheEntry> Entries { get; } = [];

        public int NumUsed { get; set; }
    }

    private readonly RenderDevice _renderDevice;
    private RenderPoolSettings _settings = new();
    private readonly Dictionary<ulong, RawBuffer> _uniformBuffers = [];
    private readonly Dictionary<RawTextureParams, TextureCacheEntryGroup> _transientTextures = [];
    private readonly Dictionary<(RawTextureParams Params, nint PersistenceKey), TextureCacheEntry> _persistentTextures = [];
    private int _numAddedTextures;
    private int _numRemovedTextures;
    private FrameIndex _lastLogFrame;
    private nint _scratchBuffer;
    private int _scratchBufferCapacity = InitialScratchBufferSize;
    private int _scratchBufferOffset;
    private int _numScratchBufferAllocations;
    private nint _temporaryScratchBufferHead;
    private int _temporaryScratchBufferAllocationsSize;
    private readonly List<RawTextureParams> _transientKeysToRemove = [];
    private readonly List<(RawTextureParams Params, nint PersistenceKey)> _persistentKeysToRemove = [];

    public RenderPool(RenderDevice renderDevice)
    {
        _renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
        _scratchBuffer = Marshal.AllocHGlobal((nint)_scratchBufferCapacity);
    }

    public void Invalidate()
    {
        foreach (var uniformBuffer in _uniformBuffers.Values)
            uniformBuffer.Dispose();
        _uniformBuffers.Clear();
    }

    public void Restore()
    {
    }

    public void OnFrameEnd()
    {
        RecycleTextures();
    }

    /// <summary>
    /// Return uniform buffer.
    /// Buffers are recycled immediately, pass different ids to get different buffers.
    /// Don't store the pointer between frames.
    /// </summary>
    public RawBuffer GetUniformBuffer(int id, int size)
    {
        var quantizedSize = (size + 511) / 512 * 512;
        var key = ((ulong)(uint)id << 32) | (uint)quantizedSize;
        if (!_uniformBuffers.TryGetValue(key, out var uniformBuffer))
        {
            uniformBuffer = new RawBuffer(
                new RawBufferParams(BufferType.Uniform, quantizedSize, 0, BufferFlag.Dynamic | BufferFlag.Discard));
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
    public nint AllocateScratchBuffer(int size)
    {
        var alignedSize = AlignScratchSize(size);
        if (_scratchBufferOffset + alignedSize > _scratchBufferCapacity)
        {
            var temporaryAllocation = Marshal.AllocHGlobal((nint)alignedSize + IntPtr.Size);

            Marshal.WriteIntPtr(temporaryAllocation, _temporaryScratchBufferHead);
            _temporaryScratchBufferHead = temporaryAllocation;
            _temporaryScratchBufferAllocationsSize += alignedSize;
            ++_numScratchBufferAllocations;
            return temporaryAllocation + IntPtr.Size;
        }

        var buffer = _scratchBuffer + _scratchBufferOffset;
        _scratchBufferOffset += alignedSize;
        ++_numScratchBufferAllocations;
        return buffer;
    }

    /// <summary>
    /// Release scratch buffer.
    /// </summary>
    public void ReleaseScratchBuffer(nint buffer)
    {
        System.Diagnostics.Debug.Assert(_numScratchBufferAllocations != 0 && buffer != nint.Zero);

        var scratchBufferEnd = _scratchBuffer + _scratchBufferOffset;
        var isStandard = buffer >= _scratchBuffer && buffer < scratchBufferEnd;
        var isTemporary = !isStandard && ReleaseTemporaryScratchBuffer(buffer);
        System.Diagnostics.Debug.Assert(isTemporary != isStandard);

        if (--_numScratchBufferAllocations == 0)
        {
            if (_temporaryScratchBufferAllocationsSize != 0)
                ResizeScratchBuffer(((_scratchBufferCapacity + _temporaryScratchBufferAllocationsSize) * 3) / 2);

            _scratchBufferOffset = 0;
            _temporaryScratchBufferAllocationsSize = 0;
        }
    }

    private bool ReleaseTemporaryScratchBuffer(nint buffer)
    {
        var allocation = buffer - IntPtr.Size;
        var previous = nint.Zero;
        var current = _temporaryScratchBufferHead;
        while (current != nint.Zero)
        {
            var next = Marshal.ReadIntPtr(current);
            if (current == allocation)
            {
                if (previous == nint.Zero)
                    _temporaryScratchBufferHead = next;
                else
                    Marshal.WriteIntPtr(previous, next);
                Marshal.FreeHGlobal(current);
                return true;
            }

            previous = current;
            current = next;
        }
        return false;
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
        if ((_numAddedTextures > 0 || _numRemovedTextures > 0) && framesSinceLastLog >= _settings.LogPeriod)
        {
            var totalTextures = _persistentTextures.Count;
            foreach (var group in _transientTextures.Values)
                totalTextures += group.Entries.Count;
            Log.GetLogger().Debug("RenderPool: {0} uniform buffers, {1} textures (+{2} -{3})",
                _uniformBuffers.Count, totalTextures, _numAddedTextures, _numRemovedTextures);
            _lastLogFrame = currentFrame;
            _numAddedTextures = 0;
            _numRemovedTextures = 0;
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
            var entry = allocations.Entries[allocations.NumUsed++];
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
            allocations.NumUsed = 0;
            _numRemovedTextures += RemoveOldEntries(allocations.Entries, _settings.TextureCacheMaxFrames);
        }

        RemoveEmptyTransientGroups();

        _persistentKeysToRemove.Clear();
        foreach (var pair in _persistentTextures)
        {
            if (GetAge(pair.Value) >= _settings.TextureCacheMaxFrames)
                _persistentKeysToRemove.Add(pair.Key);
        }
        foreach (var key in _persistentKeysToRemove)
        {
            _persistentTextures[key].Texture.Dispose();
            _persistentTextures.Remove(key);
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
                if (GetAge(entry) >= _settings.TextureCacheMinFrames)
                    ++totalOldTextures;
            }
        }

        foreach (var entry in _persistentTextures.Values)
        {
            ++totalTextures;
            if (GetAge(entry) >= _settings.TextureCacheMinFrames)
                ++totalOldTextures;
        }

        var maxOldTextures = (int)SystemMath.Ceiling(totalTextures * (1.0f - _settings.TextureCacheMinLoadFactor));
        if (totalOldTextures < maxOldTextures)
            return;

        foreach (var allocations in _transientTextures.Values)
            _numRemovedTextures += RemoveOldEntries(allocations.Entries, _settings.TextureCacheMinFrames);

        RemoveEmptyTransientGroups();

        _persistentKeysToRemove.Clear();
        foreach (var pair in _persistentTextures)
        {
            if (GetAge(pair.Value) >= _settings.TextureCacheMinFrames)
                _persistentKeysToRemove.Add(pair.Key);
        }
        foreach (var key in _persistentKeysToRemove)
        {
            _persistentTextures[key].Texture.Dispose();
            _persistentTextures.Remove(key);
            ++_numRemovedTextures;
        }
    }

    private void RemoveEmptyTransientGroups()
    {
        _transientKeysToRemove.Clear();
        foreach (var pair in _transientTextures)
        {
            if (pair.Value.Entries.Count == 0)
                _transientKeysToRemove.Add(pair.Key);
        }
        foreach (var key in _transientKeysToRemove)
            _transientTextures.Remove(key);
    }

    private int RemoveOldEntries(List<TextureCacheEntry> entries, int minimumAge)
    {
        var removed = 0;
        for (var i = entries.Count - 1; i >= 0; --i)
        {
            if (GetAge(entries[i]) < minimumAge)
                continue;
            entries[i].Texture.Dispose();
            entries.RemoveAt(i);
            ++removed;
        }
        return removed;
    }

    private static int AlignScratchSize(int size)
    {
        return (size + 15) & ~15;
    }

    private void ResizeScratchBuffer(int newCapacity)
    {
        newCapacity = SystemMath.Max(newCapacity, InitialScratchBufferSize);
        Marshal.FreeHGlobal(_scratchBuffer);
        _scratchBuffer = Marshal.AllocHGlobal((nint)newCapacity);
        _scratchBufferCapacity = newCapacity;
    }

    public override void Dispose()
    {
        if (_scratchBuffer != nint.Zero)
            Marshal.FreeHGlobal(_scratchBuffer);

        foreach (var entry in _persistentTextures.Values)
            entry.Texture.Dispose();
        _persistentTextures.Clear();
        foreach (var allocations in _transientTextures.Values)
        {
            foreach (var entry in allocations.Entries)
                entry.Texture.Dispose();
        }
        _transientTextures.Clear();
        Invalidate();

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
