// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using SystemMath = global::System.Math;

/// <summary>Profiling data for one block in the profiling tree.</summary>
public sealed class ProfilerBlock
{
    private readonly HiresTimer _timer = new();
    private long _allocatedBytesAtBegin;
    private long _time;
    private long _maxTime;
    private long _allocatedBytes;
    private long _maxAllocatedBytes;
    private uint _count;
    private bool _active;

    internal ProfilerBlock(ProfilerBlock? parent, string name)
    {
        Parent = parent;
        Name = name;
    }

    public string Name { get; }

    public ProfilerBlock? Parent { get; }

    public IReadOnlyList<ProfilerBlock> Children => _children;

    public long FrameTime { get; private set; }

    public long FrameMaxTime { get; private set; }

    public long FrameAllocatedBytes { get; private set; }

    public long FrameMaxAllocatedBytes { get; private set; }

    public uint FrameCount { get; private set; }

    public long IntervalTime { get; private set; }

    public long IntervalMaxTime { get; private set; }

    public long IntervalAllocatedBytes { get; private set; }

    public long IntervalMaxAllocatedBytes { get; private set; }

    public ulong IntervalCount { get; private set; }

    public long TotalTime { get; private set; }

    public long TotalMaxTime { get; private set; }

    public long TotalAllocatedBytes { get; private set; }

    public long TotalMaxAllocatedBytes { get; private set; }

    public ulong TotalCount { get; private set; }

    private readonly List<ProfilerBlock> _children = [];

    internal void Begin()
    {
        _timer.Reset();
        _allocatedBytesAtBegin = GC.GetAllocatedBytesForCurrentThread();
        _active = true;
        ++_count;
    }

    internal void End()
    {
        var elapsed = _timer.GetUSec();
        var allocatedBytes = SystemMath.Max(0, GC.GetAllocatedBytesForCurrentThread() - _allocatedBytesAtBegin);
        _time += elapsed;
        _allocatedBytes += allocatedBytes;
        _maxTime = SystemMath.Max(_maxTime, elapsed);
        _maxAllocatedBytes = SystemMath.Max(_maxAllocatedBytes, allocatedBytes);
        _active = false;
    }

    internal void ExcludeCurrentAllocation(long allocatedBytes)
    {
        if (_active)
            _allocatedBytesAtBegin += allocatedBytes;
    }

    internal void EndFrame()
    {
        FrameTime = _time;
        FrameMaxTime = _maxTime;
        FrameAllocatedBytes = _allocatedBytes;
        FrameMaxAllocatedBytes = _maxAllocatedBytes;
        FrameCount = _count;

        IntervalTime += _time;
        IntervalMaxTime = SystemMath.Max(IntervalMaxTime, _maxTime);
        IntervalAllocatedBytes += _allocatedBytes;
        IntervalMaxAllocatedBytes = SystemMath.Max(IntervalMaxAllocatedBytes, _maxAllocatedBytes);
        IntervalCount += _count;

        TotalTime += _time;
        TotalMaxTime = SystemMath.Max(TotalMaxTime, _maxTime);
        TotalAllocatedBytes += _allocatedBytes;
        TotalMaxAllocatedBytes = SystemMath.Max(TotalMaxAllocatedBytes, _maxAllocatedBytes);
        TotalCount += _count;

        _time = 0;
        _maxTime = 0;
        _allocatedBytes = 0;
        _maxAllocatedBytes = 0;
        _count = 0;

        foreach (var child in _children)
            child.EndFrame();
    }

    internal void BeginInterval()
    {
        IntervalTime = 0;
        IntervalMaxTime = 0;
        IntervalAllocatedBytes = 0;
        IntervalMaxAllocatedBytes = 0;
        IntervalCount = 0;

        foreach (var child in _children)
            child.BeginInterval();
    }

    internal ProfilerBlock GetChild(string name)
    {
        foreach (var child in _children)
        {
            if (string.Equals(child.Name, name, StringComparison.Ordinal))
                return child;
        }

        var result = new ProfilerBlock(this, name);
        _children.Add(result);
        return result;
    }

    internal ProfilerBlock? FindChild(string name)
    {
        foreach (var child in _children)
        {
            if (string.Equals(child.Name, name, StringComparison.Ordinal))
                return child;
        }
        return null;
    }
}
