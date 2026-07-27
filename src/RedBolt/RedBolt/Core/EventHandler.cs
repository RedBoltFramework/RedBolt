namespace RedBolt;

/// <summary>
/// Internal helper class for invoking event handler functions.
/// </summary>
public sealed class EventHandler
{
    private readonly WeakReference<RedBolt.Object> _receiver;

    internal EventHandler(RedBolt.Object receiver, RedBolt.Object? sender, Type eventType, Delegate handler)
    {
        _receiver = new WeakReference<RedBolt.Object>(receiver);
        Sender = sender;
        EventType = eventType;
        Handler = handler;
    }

    internal Delegate Handler { get; }

    internal bool IsRemoved { get; set; }

    internal bool HasReceiver => true;

    /// <summary>
    /// Return event receiver.
    /// </summary>
    public object? Receiver => TryGetReceiver(out var receiver) ? receiver : null;

    /// <summary>
    /// Return event sender. Null if the handler is non-specific.
    /// </summary>
    public RedBolt.Object? Sender { get; }

    /// <summary>
    /// Return event type.
    /// </summary>
    public Type EventType { get; }

    internal bool TryGetReceiver(out RedBolt.Object? receiver)
    {
        return _receiver.TryGetTarget(out receiver);
    }
}
