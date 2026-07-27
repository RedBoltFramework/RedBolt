namespace RedBolt;

/// <summary>
/// Utility to keep track of object revisions.
/// Revision is never zero, so it can be used as sentinel value to save space.
/// </summary>
public class ObjectRevisionTracker
{
    public const uint InvalidRevision = 0;

    private uint _revision = 1;

    /// <summary>
    /// Return object revision.
    /// </summary>
    public uint GetRevision() => _revision;

    /// <summary>
    /// Mark object as changed.
    /// </summary>
    protected void MarkRevisionUpdated()
    {
        unchecked
        {
            ++_revision;
            if (_revision == InvalidRevision)
                ++_revision;
        }
    }
}
