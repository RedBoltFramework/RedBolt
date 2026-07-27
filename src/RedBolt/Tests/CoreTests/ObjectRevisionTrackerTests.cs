using System.Reflection;

using RedBolt.Core;
namespace RedBolt.Tests;

public class ObjectRevisionTrackerTests
{
    [Fact]
    public void RevisionStartsAtOneAndAdvances()
    {
        var tracker = new TestRevisionTracker();

        Assert.Equal(1u, tracker.GetRevision());

        tracker.Advance();
        tracker.Advance();

        Assert.Equal(3u, tracker.GetRevision());
    }

    [Fact]
    public void RevisionSkipsZeroOnWraparound()
    {
        var tracker = new TestRevisionTracker();
        var revisionField = typeof(ObjectRevisionTracker).GetField("_revision", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ObjectRevisionTracker revision field was not found.");

        revisionField.SetValue(tracker, uint.MaxValue);

        tracker.Advance();

        Assert.Equal(1u, tracker.GetRevision());
    }

    private sealed class TestRevisionTracker : ObjectRevisionTracker
    {
        public void Advance() => MarkRevisionUpdated();
    }
}
