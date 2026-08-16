// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Threading;
using SystemMath = global::System.Math;

using RedBolt.Logging;

/// <summary>Hierarchical main-thread performance profiler subsystem.</summary>
public sealed class Profiler : RedObject
{
    public const uint ColorEvents = 0xb26d19;
    public const uint ColorResources = 0x006b82;

    private readonly int _mainThreadId = Environment.CurrentManagedThreadId;
    private readonly ProfilerBlock _root = new(null, "RunFrame");
    private ProfilerBlock _current;
    private bool _frameActive;

    public Profiler()
    {
        _current = _root;
    }

    public bool Enabled { get; set; }

    public ProfilerBlock CurrentBlock => _current;

    public ProfilerBlock RootBlock => _root;

    public uint IntervalFrames { get; private set; }

    public ProfilerFrameSample LastFrame { get; private set; }

    public AutoProfileBlock Profile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AutoProfileBlock(this, name);
    }

    public void BeginBlock(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        BeginBlockInternal(name);
    }

    internal ProfilerBlock? BeginBlockInternal(string name)
    {
        if (!Enabled || Environment.CurrentManagedThreadId != _mainThreadId)
            return null;

        var parent = _current;
        var allocatedBytesAtBegin = GC.GetAllocatedBytesForCurrentThread();
        _current = parent.GetChild(name);
        parent.ExcludeCurrentAllocation(GC.GetAllocatedBytesForCurrentThread() - allocatedBytesAtBegin);
        _current.Begin();
        return _current;
    }

    public void EndBlock()
    {
        if (!Enabled || Environment.CurrentManagedThreadId != _mainThreadId)
            return;

        _current.End();
        _current = _current.Parent ?? _root;
    }

    internal void EndBlock(ProfilerBlock block)
    {
        if (ReferenceEquals(_current, block))
            EndBlock();
    }

    public void BeginFrame()
    {
        if (!Enabled || Environment.CurrentManagedThreadId != _mainThreadId)
            return;

        if (_frameActive)
            EndFrame();

        _current = _root;
        _root.Begin();
        _frameActive = true;
    }

    public void EndFrame()
    {
        if (!Enabled || !_frameActive || Environment.CurrentManagedThreadId != _mainThreadId)
            return;

        while (_current.Parent is not null)
            EndBlock();
        _root.End();
        _root.EndFrame();
        _current = _root;
        _frameActive = false;
        ++IntervalFrames;

        var limiterTime = _root.FindChild("ApplyFrameLimit")?.FrameTime ?? 0;
        LastFrame = new ProfilerFrameSample(
            LastFrame.Number + 1,
            _root.FrameTime,
            SystemMath.Max(0, _root.FrameTime - limiterTime),
            _root.FrameAllocatedBytes);
    }

    public void BeginInterval()
    {
        if (Environment.CurrentManagedThreadId != _mainThreadId)
            return;
        _root.BeginInterval();
        IntervalFrames = 0;
    }

    public string PrintData(bool showUnused = false, bool showTotal = false, uint maxDepth = uint.MaxValue)
    {
        var output = new StringBuilder();
        if (showTotal)
            output.AppendLine("Block                            Cnt   Avg ms   Max ms  Total ms   Avg B   Max B   Total B");
        else
            output.AppendLine("Block                            Cnt   Avg ms   Max ms  Frame ms  Total ms   Avg B   Max B  Frame B  Total B");
        output.AppendLine();
        PrintData(_root, output, 0, maxDepth == 0 ? 1u : maxDepth, showUnused, showTotal);
        return output.ToString();
    }

    public void DumpData(bool showUnused = false, bool showTotal = false, uint maxDepth = uint.MaxValue)
    {
        Log.GetLogger().LogInformation("{ProfilerData}", PrintData(showUnused, showTotal, maxDepth));
    }

    /// <summary>Set profiler-visible name for the current thread.</summary>
    public static void SetProfilerThreadName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Thread.CurrentThread.Name is null)
            Thread.CurrentThread.Name = name;
    }

    private void PrintData(ProfilerBlock block, StringBuilder output, uint depth, uint maxDepth,
        bool showUnused, bool showTotal)
    {
        if (depth >= maxDepth)
            return;

        var count = showTotal ? block.TotalCount : block.IntervalCount;
        if (showUnused || count != 0)
        {
            var time = showTotal ? block.TotalTime : block.IntervalTime;
            var maxTime = showTotal ? block.TotalMaxTime : block.IntervalMaxTime;
            var allocated = showTotal ? block.TotalAllocatedBytes : block.IntervalAllocatedBytes;
            var maxAllocated = showTotal ? block.TotalMaxAllocatedBytes : block.IntervalMaxAllocatedBytes;
            var averageTime = count == 0 ? 0.0 : (double)time / count / 1000.0;
            var averageAllocated = count == 0 ? 0.0 : (double)allocated / count;
            var name = new string(' ', checked((int)depth)) + block.Name;

            if (showTotal)
            {
                output.Append(CultureInfo.InvariantCulture,
                    $"{name,-32} {SystemMath.Min(count, 99999),5} {averageTime,8:F3} {maxTime / 1000.0,8:F3} {time / 1000.0,9:F3} {averageAllocated,7:F0} {maxAllocated,7} {allocated,9}\n");
            }
            else
            {
                var frames = SystemMath.Max(IntervalFrames, 1u);
                output.Append(CultureInfo.InvariantCulture,
                    $"{name,-32} {SystemMath.Min(count, 99999),5} {averageTime,8:F3} {maxTime / 1000.0,8:F3} {(double)time / frames / 1000.0,9:F3} {time / 1000.0,9:F3} {averageAllocated,7:F0} {maxAllocated,7} {allocated / frames,8} {allocated,8}\n");
            }
        }

        foreach (var child in block.Children)
            PrintData(child, output, depth + 1, maxDepth, showUnused, showTotal);
    }
}
