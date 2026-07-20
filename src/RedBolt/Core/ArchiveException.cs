namespace RedBolt;

/// <summary>Exception thrown on I/O error on Archive serialization/deserialization.</summary>
/// <remarks>
/// Try to catch this exception outside of serialization code and don't leak it to user code.
/// Archive is generally not safe to use if ArchiveException has been thrown.
/// </remarks>
public class ArchiveException : InvalidOperationException
{
    public ArchiveException(string message)
        : base(message)
    {
    }
}
