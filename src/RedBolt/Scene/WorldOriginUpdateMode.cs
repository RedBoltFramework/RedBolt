namespace RedBolt;

/// <summary>Reaction to world origin updated.</summary>
public enum WorldOriginUpdateMode
{
    /// <summary>This node is moved automatically. Children nodes are ignored.</summary>
    Move,

    /// <summary>This node and its children are ignored.</summary>
    Ignore,

    /// <summary>This node is ignored. Children nodes are checked instead.</summary>
    Recurse
}
