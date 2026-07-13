namespace RedBolt;

/// <summary>
/// Reflection probe data. Reused by actual reflection probes and zones.
/// </summary>
public struct ReflectionProbeData
{
    /// <summary>
    /// Reflection map, should never be null.
    /// </summary>
    public TextureCube? ReflectionMap { get; set; }

    /// <summary>
    /// Roughness to LOD factor. Should be equal to log2(NumLODs - 1).
    /// </summary>
    public float RoughnessToLodFactor { get; set; }

    /// <summary>
    /// Position of cubemap center. W component indicates whether it is initialized.
    /// </summary>
    public Vector4f CubemapCenter { get; set; }

    /// <summary>
    /// World-space bounding box used for cubemap box projection.
    /// </summary>
    public BoundingBox ProjectionBox { get; set; }
}
