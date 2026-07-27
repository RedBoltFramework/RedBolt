namespace RedBolt;

public static class AudioDefs
{
    /// <summary>Sound type: Master.</summary>
    public const string SoundMaster = "Master";
    /// <summary>Sound type: Effect.</summary>
    public const string SoundEffect = "Effect";
    /// <summary>Sound type: Ambient.</summary>
    public const string SoundAmbient = "Ambient";
    /// <summary>Sound type: Voice.</summary>
    public const string SoundVoice = "Voice";
    /// <summary>Sound type: Music.</summary>
    public const string SoundMusic = "Music";
}

/// <summary>Audio channel configuration, WAV ordered.</summary>
public enum SpeakerMode
{
    /// <summary>Auto-detect.</summary>
    Auto,
    /// <summary>Single channel.</summary>
    Mono,
    /// <summary>Stereo, L-R.</summary>
    Stereo,
    /// <summary>Surround 4, FL-FR-RL-RR.</summary>
    Quadrophonic,
    /// <summary>5.1 Surround, FL-FR-RL-RR-C-S (again WAV order).</summary>
    Surround51,
}
