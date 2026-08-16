// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

/// <summary>Automatically ends a profiling block when disposed.</summary>
public readonly struct AutoProfileBlock : IDisposable
{
    private readonly Profiler? _profiler;
    private readonly ProfilerBlock? _block;

    internal AutoProfileBlock(Profiler profiler, string name)
    {
        _block = profiler.BeginBlockInternal(name);
        _profiler = _block is not null ? profiler : null;
    }

    public void Dispose()
    {
        if (_profiler is not null && _block is not null)
            _profiler.EndBlock(_block);
    }
}
