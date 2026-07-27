using System.Runtime.CompilerServices;
using RedBolt;

using RedBolt.Container;
using RedBolt.Core;
namespace RedBolt.Tests;

public sealed class KeyFrameSetTests
{
    [Fact]
    public void GetKeyFrame_returns_nullable_reference_to_stored_value()
    {
        var keyFrames = new TestKeyFrameSet();
        keyFrames.AddKeyFrame(new TestKeyFrame { Time = 1.0f, Value = 2 });

        ref var keyFrame = ref keyFrames.GetKeyFrame(0);
        keyFrame.Value = 3;

        Assert.Equal(3, keyFrames.KeyFrames[0].Value);
        Assert.True(Unsafe.IsNullRef(ref keyFrames.GetKeyFrame(1)));
    }

    [Fact]
    public void Non_pod_keyframes_are_copied_and_destroyed_like_cpp_values()
    {
        var source = new OwnedKeyFrame { Time = 1.0f, Value = 2 };
        using var keyFrames = new OwnedKeyFrameSet();
        keyFrames.AddKeyFrame(source);
        var stored = keyFrames.KeyFrames[0];

        Assert.NotSame(source, stored);
        Assert.Equal(2, stored.Value);

        using var clone = keyFrames.Clone();
        Assert.NotSame(stored, clone.KeyFrames[0]);

        keyFrames.RemoveKeyFrame(0);
        Assert.True(stored.IsDisposed);
        Assert.False(source.IsDisposed);
    }

    private sealed class TestKeyFrameSet : KeyFrameSet<TestKeyFrame>
    {
        public TestKeyFrameSet()
            : base(static keyFrame => keyFrame.Time)
        {
        }
    }

    private struct TestKeyFrame
    {
        public float Time;
        public int Value;
    }

    private sealed class OwnedKeyFrameSet : KeyFrameSet<OwnedKeyFrame>
    {
        public OwnedKeyFrameSet()
            : base(static keyFrame => keyFrame.Time)
        {
        }

        public override OwnedKeyFrameSet Clone()
        {
            var clone = new OwnedKeyFrameSet();
            foreach (var keyFrame in KeyFrames)
                clone.AddKeyFrame(keyFrame);
            return clone;
        }
    }

    private sealed class OwnedKeyFrame : ICloneable, IDisposable
    {
        public float Time;
        public int Value;
        public bool IsDisposed;

        public object Clone() => new OwnedKeyFrame { Time = Time, Value = Value };

        public void Dispose() => IsDisposed = true;
    }
}
