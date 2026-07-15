namespace RedBolt;

// C++: KeyFrameSet is a struct template with keyFrames_ as public field:
// template <class T> struct KeyFrameSet { ea::vector<KeyFrame> keyFrames_; ... };
// C++ uses free function template GetKeyFrameTime(T) for ordering;
// C# uses a delegate Func<T,float> passed to the constructor.
// The C++ struct is just a data+algorithms template; the C++ T can be any type.
public class KeyFrameSet<T>
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
        KeyFrames.Add(keyFrame);
        if (needSort)
            SortKeyFrames();
    }

    public void RemoveKeyFrame(uint index)
    {
        KeyFrames.RemoveAt(checked((int)index));
    }

    public void RemoveAllKeyFrames()
    {
        KeyFrames.Clear();
    }

    public T? GetKeyFrame(uint index)
    {
        return index < KeyFrames.Count ? KeyFrames[checked((int)index)] : default;
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
            : Math.Min(frameIndex + 1, numFrames - 1);

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
}
