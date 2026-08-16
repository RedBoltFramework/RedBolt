// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Audio;

using RedBolt.Scene;

public static class AudioEvents
{
    /// <summary>Sound playback finished. Sent through the SoundSource's Node.</summary>
    public struct SoundFinishedEvent { public Node? Node; public SoundSource? SoundSource; public Sound? Sound; }

    /// <summary>Recording updated.</summary>
    public struct RecordingUpdatedEvent { public Microphone? Microphone; public int DataLength; public bool ClearData; }

    /// <summary>Recording started.</summary>
    public struct RecordingStartedEvent { public Microphone? Microphone; }

    /// <summary>Recording ended.</summary>
    public struct RecordingEndedEvent { public Microphone? Microphone; public int DataLength; public bool ClearData; }
}
