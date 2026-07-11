namespace RedBolt;

/// <summary>Base class for sound streams.</summary>
public abstract class SoundStream
{
    /// <summary>Default frequency.</summary>
    protected uint Frequency;
    /// <summary>Stop when no more data flag.</summary>
    protected bool StopAtEnd;
    /// <summary>Sixteen bit flag.</summary>
    protected bool SixteenBit;
    /// <summary>Stereo flag.</summary>
    protected bool Stereo;

    /// <summary>Seek to sample number. Return true on success.</summary>
    public virtual bool Seek(uint sampleNumber) => false;

    /// <summary>Produce sound data into destination. Return number of bytes produced.</summary>
    public abstract uint GetData(sbyte[] dest, uint numBytes);

    /// <summary>Set sound data format.</summary>
    public void SetFormat(uint frequency, bool sixteenBit, bool stereo)
    {
        Frequency = frequency;
        SixteenBit = sixteenBit;
        Stereo = stereo;
    }

    /// <summary>Set whether playback should stop when no more data.</summary>
    public void SetStopAtEnd(bool enable) => StopAtEnd = enable;

    /// <summary>Return sample size.</summary>
    public uint GetSampleSize()
    {
        uint size = 1;
        if (SixteenBit) size <<= 1;
        if (Stereo) size <<= 1;
        return size;
    }

    /// <summary>Return default frequency as a float.</summary>
    public float GetFrequency() => Frequency;

    /// <summary>Return default frequency as an integer.</summary>
    public uint GetIntFrequency() => Frequency;

    /// <summary>Return whether playback should stop when no more data.</summary>
    public bool GetStopAtEnd() => StopAtEnd;

    /// <summary>Return whether data is sixteen bit.</summary>
    public bool IsSixteenBit() => SixteenBit;

    /// <summary>Return whether data is stereo.</summary>
    public bool IsStereo() => Stereo;
}
