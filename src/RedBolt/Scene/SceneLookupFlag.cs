namespace RedBolt;

[Flags]
public enum SceneLookupFlag
{
    None = 0x0,

    /// <summary>Whether to do recursive search in the scene subtree.</summary>
    Recursive = 0x1,

    //IgnoreTemporary = 0x2,

    /// <summary>Used for lazy node lookup. Whether to validate the existing node name.</summary>
    ValidateName = 0x4,

    /// <summary>Used for lazy node lookup. Whether to validate that the existing node is a child of the queried node.</summary>
    ValidateRelation = 0x8,
}
