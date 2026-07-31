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
