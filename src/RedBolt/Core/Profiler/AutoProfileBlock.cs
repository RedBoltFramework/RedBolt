// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

/// <summary>Automatically ends a profiling block when disposed.</summary>
public readonly struct AutoProfileBlock : IDisposable
{
    private readonly ProfilerBlock? _block;

    internal AutoProfileBlock(string name)
    {
        _block = Profiler.BeginProfileBlock(name);
    }

    public void Dispose()
    {
        if (_block is not null)
            Profiler.EndBlock(_block);
    }
}
