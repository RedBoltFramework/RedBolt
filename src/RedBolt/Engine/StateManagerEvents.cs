namespace RedBolt;

public static class StateManagerEvents
{
    /// <summary>State transition started.</summary>
    public struct StateTransitionStartedEvent { public StringHash From; public StringHash To; }

    /// <summary>Leaving application state.</summary>
    public struct LeavingApplicationStateEvent { public StringHash From; public StringHash To; }

    /// <summary>Entering application state.</summary>
    public struct EnteringApplicationStateEvent { public StringHash From; public StringHash To; }

    /// <summary>State transition complete.</summary>
    public struct StateTransitionCompleteEvent { public StringHash From; public StringHash To; }
}
