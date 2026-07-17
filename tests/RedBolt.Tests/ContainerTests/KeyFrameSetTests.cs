using System.Runtime.CompilerServices;
using RedBolt;

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
}
