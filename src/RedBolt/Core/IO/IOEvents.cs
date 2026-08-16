// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.IO;

using RedBolt.Core;
using RedBolt.Logging;

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
