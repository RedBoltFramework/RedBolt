namespace RedBolt;

/// <summary>
/// Internal helper class for invoking event handler functions.
/// </summary>
public sealed class EventHandler
{
    private readonly WeakReference<object>? _receiver;

    internal EventHandler(ulong id, object? receiver, Object? sender, Type eventType, Delegate handler)
    {
        Id = id;
        _receiver = receiver is null ? null : new WeakReference<object>(receiver);
        Sender = sender;
        EventType = eventType;
        Handler = handler;
    }

    internal ulong Id { get; }

    internal Delegate Handler { get; }

    internal bool IsRemoved { get; set; }

    internal bool HasReceiver => _receiver is not null;

    /// <summary>
    /// Return event receiver.
    /// </summary>
    public object? Receiver => TryGetReceiver(out var receiver) ? receiver : null;

    /// <summary>
    /// Return event sender. Null if the handler is non-specific.
    /// </summary>
    public Object? Sender { get; }

    /// <summary>
    /// Return event type.
    /// </summary>
    public Type EventType { get; }

    internal bool TryGetReceiver(out object? receiver)
    {
        if (_receiver is null)
        {
            receiver = null;
            return true;
        }

        return _receiver.TryGetTarget(out receiver);
    }
}
