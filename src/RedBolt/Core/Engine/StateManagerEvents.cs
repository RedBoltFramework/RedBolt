// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Engine;

using RedBolt.Container;

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
