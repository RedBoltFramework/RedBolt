namespace RedBolt.Engine;

public static class EngineEvents
{
    /// <summary>A command has been entered on the console.</summary>
    public struct ConsoleCommandEvent
    {
        public string Command = string.Empty;
        public string Id = string.Empty;

        public ConsoleCommandEvent() { }
    }

    /// <summary>A console URI has been clicked.</summary>
    public struct ConsoleUriClickEvent
    {
        public string Address = string.Empty;
        public string Protocol = string.Empty;

        public ConsoleUriClickEvent() { }
    }

    /// <summary>Engine finished initialization, but Application.Start() was not called yet.</summary>
    public struct EngineInitializedEvent { }

    /// <summary>Application started, but first frame was not rendered yet.</summary>
    public struct ApplicationStartedEvent { }

    /// <summary>Application stopped and no frames will be rendered any more.</summary>
    public struct ApplicationStoppedEvent { }

    /// <summary>Begin plugin reloading.</summary>
    public struct BeginPluginReloadEvent { }

    /// <summary>End plugin reloading.</summary>
    public struct EndPluginReloadEvent { }
}
