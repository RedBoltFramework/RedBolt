namespace RedBolt.IO;

/// <summary>
/// Vector encoding for serialization/deserialization.
/// Encoding parameter may be provided.
/// </summary>
public enum VectorBinaryEncoding
{
    /// <summary>
    /// 32-bit float components.
    /// </summary>
    Float,

    /// <summary>
    /// 64-bit float components.
    /// </summary>
    Double,

    /// <summary>
    /// 32-bit integer components, range is normalized to [-param; param].
    /// </summary>
    Int32,

    /// <summary>
    /// 16-bit integer components, range is normalized to [-param; param].
    /// </summary>
    Int16,

    Count
}
