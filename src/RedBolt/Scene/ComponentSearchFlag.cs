namespace RedBolt;

[Flags]
public enum ComponentSearchFlag
{
    Self = 1 << 0,
    Parent = 1 << 1,
    ParentRecursive = 1 << 2,
    Children = 1 << 3,
    ChildrenRecursive = 1 << 4,
    Derived = 1 << 5,
    EnabledOnly = 1 << 9,
    Default = Self
}
