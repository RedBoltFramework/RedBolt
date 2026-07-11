namespace RedBolt;

public static class IOEvents
{
    /// <summary>Log message event.</summary>
    public struct LogMessage
    {
        public string Message;
        public string Logger;
        public int Level;
        public uint Time;
    }

    /// <summary>Async system command execution finished.</summary>
    public struct AsyncExecFinished
    {
        public uint RequestId;
        public int ExitCode;
    }
}
