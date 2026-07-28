// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2023-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using RedBolt.Core;
using RedBolt.Scene;

namespace RedBolt.Navigation;

/// <summary>
/// Component which tags geometry for inclusion in the navigation mesh. Optionally auto-includes geometry from child
/// nodes.
/// </summary>
[Reflected(ObjectCategory.Category_Navigation, InheritAttributes = false)]
public partial class Navigable : Component
{
    private bool _recursive = true;
    private bool _walkable = true;
    private byte _areaId = NavigationDefs.DeduceAreaId;

    /// <summary>Construct.</summary>
    public Navigable()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Set whether geometry is automatically collected from child nodes. Default true.</summary>
    /// <summary>Return whether geometry is automatically collected from child nodes.</summary>
    [Serialized(
        "Recursive",
        Order = 1000
    )]
    public bool Recursive
    {
        get => _recursive;
        set => _recursive = value;
    }

    /// <summary>Set whether geometry is walkable. Default true.</summary>
    /// <summary>Return whether geometry is walkable.</summary>
    [Serialized(
        "Walkable",
        Order = 1001
    )]
    public bool Walkable
    {
        get => _walkable;
        set => _walkable = value;
    }

    /// <summary>Set area ID of geometry. Deduced by default.</summary>
    /// <summary>Return area ID of geometry.</summary>
    [Serialized("Area ID", DefaultSource = nameof(GetAreaIdDefault))]
    public uint AreaId
    {
        get => _areaId;
        set => _areaId = unchecked((byte)value);
    }

    /// <summary>Return effective area ID for navigation mesh builder.</summary>
    public byte EffectiveAreaId => _areaId != NavigationDefs.DeduceAreaId || _walkable ? _areaId : (byte)0;

    private static bool GetEnabledDefault() => true;
    private static bool GetRecursiveDefault() => true;
    private static bool GetWalkableDefault() => true;
    private static uint GetAreaIdDefault() => NavigationDefs.DeduceAreaId;
    [Serialized(
        "Is Enabled",
        DefaultSource = nameof(GetEnabledDefault),
        Setter = nameof(SetEnabled),
        Order = 0
    )]
    private bool GetEnabled() => Enabled;
}
