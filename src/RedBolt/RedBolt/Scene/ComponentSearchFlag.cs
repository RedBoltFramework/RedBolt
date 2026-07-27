namespace RedBolt.Scene;

[Flags]
public enum ComponentSearchFlag
{
    None = 0,
    Self = 1 << 0,
    Parent = 1 << 1,
    ParentRecursive = Parent | 1 << 2,
    Children = 1 << 3,
    ChildrenRecursive = Children | 1 << 4,
    Derived = 1 << 8,
    EnabledOnly = 1 << 9,

    SelfOrParentRecursive = Self | ParentRecursive,
    SelfOrChildrenRecursive = Self | ChildrenRecursive,

    /// <summary>Default search option - to find components in the given node and all children recursively.</summary>
    Default = SelfOrChildrenRecursive
}
