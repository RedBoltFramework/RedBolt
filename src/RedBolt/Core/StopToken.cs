using System.Runtime.CompilerServices;
using System.Threading;

namespace RedBolt;

/// <summary>
/// Stop token used to thread-safely stop asynchronous task. This object can be passed
/// by value and all copies will share same internal state..
/// TODO: For better memory management split this class into StopSource with shared_ptr
/// and StopToken with weak_ptr, or reuse corresponding classes from C++20 standard library.
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
