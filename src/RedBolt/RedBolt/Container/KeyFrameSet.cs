using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Container;

/// <summary>Sorted array of keyframes.</summary>
/// <remarks>T: GetKeyFrameTime(T) must return <see cref="float"/> time to be used for ordering.</remarks>
public class KeyFrameSet<T> : ICloneable, IDisposable
{
    private readonly Func<T, float> _getKeyFrameTime;

    protected KeyFrameSet(Func<T, float> getKeyFrameTime)
    {
        _getKeyFrameTime = getKeyFrameTime;
    }

    public List<T> KeyFrames { get; } = [];

    public void SortKeyFrames()
    {
        KeyFrames.Sort((lhs, rhs) => _getKeyFrameTime(lhs).CompareTo(_getKeyFrameTime(rhs)));
    }

    public void AddKeyFrame(T keyFrame)
    {
        var needSort = KeyFrames.Count > 0 && _getKeyFrameTime(KeyFrames[^1]) > _getKeyFrameTime(keyFrame);
        KeyFrames.Add(CopyValue(keyFrame));
        if (needSort)
            SortKeyFrames();
    }

    public void RemoveKeyFrame(uint index)
    {
        var intIndex = checked((int)index);
        var value = KeyFrames[intIndex];
        KeyFrames.RemoveAt(intIndex);
        DisposeValue(value);
    }

    public void RemoveAllKeyFrames()
    {
        for (var i = KeyFrames.Count - 1; i >= 0; --i)
            DisposeValue(KeyFrames[i]);
        KeyFrames.Clear();
    }

    public ref T GetKeyFrame(uint index)
    {
        if (index < KeyFrames.Count)
            return ref CollectionsMarshal.AsSpan(KeyFrames)[checked((int)index)];
        return ref Unsafe.NullRef<T>();
    }

    public uint NumKeyFrames => checked((uint)KeyFrames.Count);

    public bool IsEmpty => KeyFrames.Count == 0;

    public void GetKeyFrames(float time, float duration, bool isLooped, ref uint frameIndex, out uint nextFrameIndex,
        out float blendFactor)
    {
        GetKeyFrameIndex(time, ref frameIndex);

        var numFrames = checked((uint)KeyFrames.Count);
        nextFrameIndex = isLooped
            ? (frameIndex + 1) % numFrames
            : SystemMath.Min(frameIndex + 1, numFrames - 1);

        if (frameIndex != nextFrameIndex)
        {
            var frameTime = _getKeyFrameTime(KeyFrames[checked((int)frameIndex)]);
            var nextFrameTime = _getKeyFrameTime(KeyFrames[checked((int)nextFrameIndex)]);

            var timeInterval = nextFrameTime - frameTime;
            if (timeInterval < 0.0f)
                timeInterval += duration;
            blendFactor = timeInterval > 0.0f ? (time - frameTime) / timeInterval : 1.0f;
        }
        else
            blendFactor = 0.0f;
    }

    public bool GetKeyFrameIndex(float time, ref uint index)
    {
        if (KeyFrames.Count == 0)
            return false;

        if (time < 0.0f)
            time = 0.0f;

        if (index >= KeyFrames.Count)
            index = checked((uint)(KeyFrames.Count - 1));

        while (index != 0 && time < _getKeyFrameTime(KeyFrames[checked((int)index)]))
            --index;

        while (index < KeyFrames.Count - 1 && time >= _getKeyFrameTime(KeyFrames[checked((int)(index + 1))]))
            ++index;

        return true;
    }

    public virtual KeyFrameSet<T> Clone()
    {
        var clone = new KeyFrameSet<T>(_getKeyFrameTime);
        foreach (var keyFrame in KeyFrames)
            clone.KeyFrames.Add(CopyValue(keyFrame));
        return clone;
    }

    object ICloneable.Clone() => Clone();

    public virtual void Dispose()
    {
        RemoveAllKeyFrames();
        GC.SuppressFinalize(this);
    }

    private static T CopyValue(T value) => value is ICloneable cloneable ? (T)cloneable.Clone() : value;

    private static void DisposeValue(T value)
    {
        if (value is ICloneable && value is IDisposable disposable)
            disposable.Dispose();
    }
}
