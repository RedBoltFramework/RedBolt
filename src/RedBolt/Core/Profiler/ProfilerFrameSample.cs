// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

/// <summary>Coarse totals for one completed engine frame.</summary>
public readonly record struct ProfilerFrameSample(
    ulong Number,
    long Time,
    long ActiveTime,
    long AllocatedBytes);
