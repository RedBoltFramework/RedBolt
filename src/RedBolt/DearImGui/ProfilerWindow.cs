// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.DearImGui;

using System.Diagnostics;
using System.Globalization;
using SystemMath = global::System.Math;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;

/// <summary>Reusable Dear ImGui presentation of the hierarchical profiler.</summary>
public sealed class ProfilerWindow : IDisposable
{
    public const int HistoryCapacity = 240;

    private sealed class Accumulator
    {
        public ulong Count;
        public long Time;
        public long MaxTime;
        public long AllocatedBytes;
        public long MaxAllocatedBytes;
    }

    private sealed record Row(
        string Name,
        int Depth,
        ulong Count,
        long Time,
        long MaxTime,
        long AllocatedBytes,
        long MaxAllocatedBytes);

    private readonly Profiler _profiler;
    private readonly Dictionary<ProfilerBlock, Accumulator> _accumulators = [];
    private readonly List<Row> _rows = [];
    private readonly float[] _frameTimeHistory = new float[HistoryCapacity];
    private readonly float[] _allocationHistory = new float[HistoryCapacity];
    private readonly CString _text = new();
    private ulong _lastFrameNumber;
    private long _intervalStart = Stopwatch.GetTimestamp();
    private uint _intervalFrames;
    private uint _displayFrames;
    private int _historyCount;
    private int _historyOffset;
    private bool _paused;

    public ProfilerWindow(Profiler? profiler = null)
    {
        _profiler = profiler ?? Context.Instance.GetSubsystem<Profiler>()
            ?? throw new InvalidOperationException("Profiler subsystem is not registered.");
    }

    public bool Paused => _paused;

    public void Dispose()
    {
        _text.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Render(ref bool open)
    {
        if (ImGui.Begin("Profiler"u8, ref open))
            RenderContent();
        ImGui.End();
    }

    public void RenderContent()
    {
        if (!_profiler.Enabled)
        {
            ImGui.TextUnformatted("Profiling is disabled. Start the application with --profile."u8);
            return;
        }

        UpdateData();

        if (ImGui.Button(_paused ? "Resume"u8 : "Pause"u8))
            _paused = !_paused;
        ImGui.SameLine();
        if (ImGui.Button("Reset history"u8))
            ResetHistory();

        RenderGraphs();
        RenderTable();
    }

    public void ResetHistory()
    {
        Array.Clear(_frameTimeHistory);
        Array.Clear(_allocationHistory);
        _historyCount = 0;
        _historyOffset = 0;
    }

    private void UpdateData()
    {
        if (_paused)
            return;

        var frame = _profiler.LastFrame;
        if (frame.Number == 0 || frame.Number == _lastFrameNumber)
            return;
        _lastFrameNumber = frame.Number;

        AddHistory((float)(frame.ActiveTime / 1000.0), frame.AllocatedBytes / 1024.0f);
        Accumulate(_profiler.RootBlock);
        ++_intervalFrames;

        if (Stopwatch.GetElapsedTime(_intervalStart).TotalSeconds < 1.0)
            return;

        _rows.Clear();
        BuildRows(_profiler.RootBlock, 0);
        _displayFrames = _intervalFrames;
        _accumulators.Clear();
        _intervalFrames = 0;
        _intervalStart = Stopwatch.GetTimestamp();
    }

    private void Accumulate(ProfilerBlock block)
    {
        if (!_accumulators.TryGetValue(block, out var value))
        {
            value = new Accumulator();
            _accumulators.Add(block, value);
        }

        value.Count += block.FrameCount;
        value.Time += block.FrameTime;
        value.MaxTime = SystemMath.Max(value.MaxTime, block.FrameMaxTime);
        value.AllocatedBytes += block.FrameAllocatedBytes;
        value.MaxAllocatedBytes = SystemMath.Max(value.MaxAllocatedBytes, block.FrameMaxAllocatedBytes);

        foreach (var child in block.Children)
            Accumulate(child);
    }

    private void BuildRows(ProfilerBlock block, int depth)
    {
        if (_accumulators.TryGetValue(block, out var value) && value.Count != 0)
        {
            _rows.Add(new Row(block.Name, depth, value.Count, value.Time, value.MaxTime,
                value.AllocatedBytes, value.MaxAllocatedBytes));
        }

        foreach (var child in block.Children)
            BuildRows(child, depth + 1);
    }

    private void AddHistory(float frameTime, float allocatedKiB)
    {
        _frameTimeHistory[_historyOffset] = frameTime;
        _allocationHistory[_historyOffset] = allocatedKiB;
        _historyOffset = (_historyOffset + 1) % HistoryCapacity;
        _historyCount = SystemMath.Min(_historyCount + 1, HistoryCapacity);
    }

    private void RenderGraphs()
    {
        RenderGraph("Active frame time"u8, _frameTimeHistory, "ms");
        RenderGraph("Frame allocations"u8, _allocationHistory, "KiB");
    }

    private void RenderGraph(ReadOnlySpan<byte> label, float[] values, string unit)
    {
        var current = _historyCount == 0 ? 0.0f : values[(_historyOffset + HistoryCapacity - 1) % HistoryCapacity];
        var total = 0.0;
        var maximum = 0.0f;
        for (var i = 0; i < _historyCount; ++i)
        {
            var value = values[i];
            total += value;
            maximum = SystemMath.Max(maximum, value);
        }
        var average = _historyCount == 0 ? 0.0 : total / _historyCount;
        _text.Set(string.Create(CultureInfo.InvariantCulture,
            $"Current {current:F2} {unit}    Average {average:F2} {unit}    Maximum {maximum:F2} {unit}"));
        ImGui.TextUnformatted(_text.ContentBytes);
        ImGui.PlotLines(label, values, _historyCount, _historyCount == HistoryCapacity ? _historyOffset : 0);
    }

    private void RenderTable()
    {
        var flags = ImGuiTableFlags.ImGuiTableFlagsResizable
            | ImGuiTableFlags.ImGuiTableFlagsRowBg
            | ImGuiTableFlags.ImGuiTableFlagsBorders
            | ImGuiTableFlags.ImGuiTableFlagsScrollY;
        if (!ImGui.BeginTable("ProfilerData"u8, 10, flags))
            return;

        ImGui.TableSetupScrollFreeze(1, 1);
        ImGui.TableSetupColumn("Block"u8);
        ImGui.TableSetupColumn("Calls"u8);
        ImGui.TableSetupColumn("Avg ms"u8);
        ImGui.TableSetupColumn("Max ms"u8);
        ImGui.TableSetupColumn("Frame ms"u8);
        ImGui.TableSetupColumn("Total ms"u8);
        ImGui.TableSetupColumn("Avg alloc"u8);
        ImGui.TableSetupColumn("Max alloc"u8);
        ImGui.TableSetupColumn("Frame alloc"u8);
        ImGui.TableSetupColumn("Total alloc"u8);
        ImGui.TableHeadersRow();

        foreach (var row in _rows)
        {
            ImGui.TableNextRow();
            Cell(new string(' ', row.Depth * 2) + row.Name);
            Cell(row.Count.ToString(CultureInfo.InvariantCulture));
            Cell(FormatMilliseconds(row.Count == 0 ? 0 : (double)row.Time / row.Count));
            Cell(FormatMilliseconds(row.MaxTime));
            Cell(FormatMilliseconds(_displayFrames == 0 ? 0 : (double)row.Time / _displayFrames));
            Cell(FormatMilliseconds(row.Time));
            Cell(FormatBytes(row.Count == 0 ? 0 : row.AllocatedBytes / (double)row.Count));
            Cell(FormatBytes(row.MaxAllocatedBytes));
            Cell(FormatBytes(_displayFrames == 0 ? 0 : row.AllocatedBytes / (double)_displayFrames));
            Cell(FormatBytes(row.AllocatedBytes));
        }
        ImGui.EndTable();
    }

    private void Cell(string value)
    {
        ImGui.TableNextColumn();
        _text.Set(value);
        ImGui.TextUnformatted(_text.ContentBytes);
    }

    private static string FormatMilliseconds(double microseconds) =>
        (microseconds / 1000.0).ToString("F3", CultureInfo.InvariantCulture);

    private static string FormatBytes(double bytes)
    {
        if (bytes >= 1024.0 * 1024.0)
            return (bytes / (1024.0 * 1024.0)).ToString("F2", CultureInfo.InvariantCulture) + " MiB";
        if (bytes >= 1024.0)
            return (bytes / 1024.0).ToString("F2", CultureInfo.InvariantCulture) + " KiB";
        return bytes.ToString("F0", CultureInfo.InvariantCulture) + " B";
    }
}
