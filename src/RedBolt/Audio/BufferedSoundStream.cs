namespace RedBolt.Audio;

using SystemMath = global::System.Math;

/// <summary>Sound stream that supports manual buffering of data from the main thread.</summary>
public sealed class BufferedSoundStream : SoundStream
{
    private readonly List<Array> _buffers = [];
    private readonly List<uint> _bufferSizes = [];
    private uint _position;
    private readonly object _bufferLock = new();

    /// <summary>Produce sound data into destination. Return number of bytes produced.</summary>
    public override uint GetData(sbyte[] dest, uint numBytes)
    {
        lock (_bufferLock)
        {
            uint totalBytes = 0;
            while (totalBytes < numBytes && _buffers.Count > 0)
            {
                var buffer = _buffers[0];
                var bufferSize = _bufferSizes[0];
                var remaining = bufferSize - _position;
                var bytesToCopy = SystemMath.Min(numBytes - totalBytes, remaining);
                Buffer.BlockCopy(buffer, (int)_position, dest, (int)totalBytes, (int)bytesToCopy);
                totalBytes += bytesToCopy;
                _position += bytesToCopy;
                if (_position >= bufferSize)
                {
                    _buffers.RemoveAt(0);
                    _bufferSizes.RemoveAt(0);
                    _position = 0;
                }
            }
            return totalBytes;
        }
    }

    /// <summary>Buffer sound data.</summary>
    public void AddData(byte[] data, uint numBytes)
    {
        if (data is null || numBytes == 0) return;
        lock (_bufferLock)
        {
            _buffers.Add(data);
            _bufferSizes.Add(numBytes);
        }
    }

    public void AddData(short[] data, uint numBytes)
    {
        if (data is null || numBytes == 0) return;
        lock (_bufferLock)
        {
            _buffers.Add(data);
            _bufferSizes.Add(numBytes);
        }
    }

    /// <summary>Remove all buffered audio data.</summary>
    public void Clear()
    {
        lock (_bufferLock)
        {
            _buffers.Clear();
            _bufferSizes.Clear();
            _position = 0;
        }
    }

    /// <summary>Return amount of buffered (unplayed) sound data in bytes.</summary>
    public uint GetBufferNumBytes()
    {
        lock (_bufferLock)
        {
            uint total = 0;
            for (var i = 0; i < _bufferSizes.Count; i++)
                total += _bufferSizes[i];
            return total - _position;
        }
    }

    /// <summary>Return length of buffered (unplayed) sound data in seconds.</summary>
    public float GetBufferLength() => (float)GetBufferNumBytes() / GetSampleSize() / Frequency;
}
