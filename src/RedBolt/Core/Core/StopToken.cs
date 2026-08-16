// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using System.Runtime.CompilerServices;
using System.Threading;

/// <summary>
/// Stop token used to thread-safely stop asynchronous task. This object can be passed
/// by value and all copies will share same internal state..
/// </summary>
public readonly struct StopToken
{
    /// <summary>
    /// Construct default.
    /// </summary>
    public StopToken()
    {
        _stopped = new StrongBox<int>(0);
    }

    /// <summary>
    /// Signal stop.
    /// </summary>
    public void Stop()
    {
        Volatile.Write(ref _stopped.Value, 1);
    }

    /// <summary>
    /// Check whether is stopped.
    /// </summary>
    public bool IsStopped()
    {
        return Volatile.Read(ref _stopped.Value) != 0;
    }

    /// <summary>
    /// Whether the token is stopped.
    /// </summary>
    private readonly StrongBox<int> _stopped;
}
