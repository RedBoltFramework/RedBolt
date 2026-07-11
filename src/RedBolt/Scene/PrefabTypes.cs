namespace RedBolt;

/// <summary>Strongly typed attribute ID.</summary>
public enum AttributeId : uint
{
    None
}

/// <summary>Strongly typed serializable ID.</summary>
public enum SerializableId : uint
{
    None
}

/// <summary>
/// Prefab archive format flags.
/// Flags must be the same on loading and saving.
/// Mismatch will cause serialization error.
/// </summary>
[Flags]
public enum PrefabArchiveFlag
{
    None = 0,

    /// <summary>Whether to ignore ID of serializable object.</summary>
    IgnoreSerializableId = 1 << 0,
    /// <summary>Whether to ignore type of serializable object.</summary>
    IgnoreSerializableType = 1 << 1,
    /// <summary>Whether to compact type names to hashes.</summary>
    /// <remarks>Useful for large structures not intended for readability.</remarks>
    CompactTypeNames = 1 << 2,
    /// <summary>Whether to serialize temporary objects.</summary>
    /// <remarks>Useful if the exact serialization is required.</remarks>
    SerializeTemporary = 1 << 3,
}

/// <summary>Flags that control how prefab is saved.</summary>
[Flags]
public enum PrefabSaveFlag
{
    None = 0,

    /// <summary>Whether to compact attribute names to hashes.</summary>
    /// <remarks>Useful for large structures not intended for readability.</remarks>
    CompactAttributeNames = 1 << 0,
    /// <summary>Whether to treat enums as strings.</summary>
    /// <remarks>Improves readability and portability of text formats.</remarks>
    EnumsAsStrings = 1 << 1,
    /// <summary>Whether to save default attribute values.</summary>
    SaveDefaultValues = 1 << 2,
    /// <summary>Whether the prefab is saved. Attributes without AM_PREFAB flag will be ignored.</summary>
    Prefab = 1 << 3,
    /// <summary>Whether to save temporary objects and attributes.</summary>
    SaveTemporary = 1 << 4,
}

/// <summary>Flags that control how prefab is loaded.</summary>
[Flags]
public enum PrefabLoadFlag
{
    None = 0,

    /// <summary>Whether to check that serializable object type matches type in prefab.</summary>
    CheckSerializableType = 1 << 0,
    /// <summary>Whether to keep existing components of serializable object.</summary>
    KeepExistingComponents = 1 << 1,
    /// <summary>Whether to keep existing children of serializable object.</summary>
    KeepExistingChildren = 1 << 2,
    /// <summary>Whether to create temporary nodes and components instead of persistent ones.</summary>
    /// <remarks>Useful for instantiating prefabs.</remarks>
    LoadAsTemporary = 1 << 3,
    /// <summary>Whether to discard and reassign IDs.</summary>
    DiscardIds = 1 << 4,
    /// <summary>Whether to ignore attributes of the root node.</summary>
    IgnoreRootAttributes = 1 << 5,
    /// <summary>Keep "temporary" state as is.</summary>
    KeepTemporaryState = 1 << 6,
    /// <summary>Skip "ApplyAttributes" callback.</summary>
    SkipApplyAttributes = 1 << 7,
}

public static class PrefabTypes
{
    /// <summary>Return standard archive flags for node. Node doesn't need type and must have ID.</summary>
    public static PrefabArchiveFlag ToNodeFlags(PrefabArchiveFlag flags)
    {
        return (flags | PrefabArchiveFlag.IgnoreSerializableType) & ~PrefabArchiveFlag.IgnoreSerializableId;
    }

    /// <summary>Return standard archive flags for component. Component must have type and ID.</summary>
    public static PrefabArchiveFlag ToComponentFlags(PrefabArchiveFlag flags)
    {
        return flags & ~PrefabArchiveFlag.IgnoreSerializableType & ~PrefabArchiveFlag.IgnoreSerializableType;
    }
}
