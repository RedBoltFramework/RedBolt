namespace RedBolt.Scene;

using RedBolt.Graphics;

/// <summary>Animation wrap mode.</summary>
public enum WrapMode
{
    /// <summary>Loop mode.</summary>
    Loop = 0,
    /// <summary>Play once, when animation finished it will be removed.</summary>
    Once,
    /// <summary>Clamp mode.</summary>
    Clamp
}
