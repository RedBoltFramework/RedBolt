// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using RedBolt.Resource;

/// <summary>SystemUI widget used to edit 2D texture.</summary>
public class Texture2DInspectorWidget : ResourceInspectorWidget
{
    public Texture2DInspectorWidget(IReadOnlyList<Resource> resources)
        : base(resources, _properties)
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    public override bool CanSave() => false;

    private static readonly StringVector _textureFilterModes =
    [
        "NEAREST",
        "BILINEAR",
        "TRILINEAR",
        "ANISOTROPIC",
        "NEAREST_ANISOTROPIC",
        "DEFAULT",
    ];

    private static readonly StringVector _textureAddressMode =
    [
        "WRAP",
        "MIRROR",
        "CLAMP",
        "BORDER",
    ];

    private static readonly PropertyDesc[] _properties =
    [
        new()
        {
            Name = "SRGB",
            DefaultValue = new Variant(false),
            Getter = static resource => new Variant(((Texture2D)resource).GetSRGB()),
            Setter = static (Resource resource, in Variant value) =>
                ((Texture2D)resource).SetSRGB(value.GetBool()),
            Hint = "SRGB",
        },
        new()
        {
            Name = "Linear",
            DefaultValue = new Variant(false),
            Getter = static resource => new Variant(((Texture2D)resource).GetLinear()),
            Setter = static (Resource resource, in Variant value) =>
                ((Texture2D)resource).SetLinear(value.GetBool()),
            Hint = "Linear color space",
        },
        new()
        {
            Name = "Filter Mode",
            DefaultValue = new Variant((int)TextureFilterMode.Default),
            Getter = static resource => new Variant((int)((Texture2D)resource).GetFilterMode()),
            Setter = static (Resource resource, in Variant value) =>
                ((Texture2D)resource).SetFilterMode((TextureFilterMode)value.GetInt()),
            Hint = "Texture Filter Mode",
            Options = new Widgets.EditVariantOptions().Enum(_textureFilterModes),
        },
        new()
        {
            Name = "U Address Mode",
            DefaultValue = new Variant((int)TextureAddressMode.Wrap),
            Getter = static resource =>
                new Variant((int)((Texture2D)resource).GetAddressMode(TextureCoordinate.U)),
            Setter = static (Resource resource, in Variant value) =>
                ((Texture2D)resource).SetAddressMode(
                    TextureCoordinate.U, (TextureAddressMode)value.GetInt()),
            Hint = "U texture coordinate address mode",
            Options = new Widgets.EditVariantOptions().Enum(_textureAddressMode),
        },
        new()
        {
            Name = "V Address Mode",
            DefaultValue = new Variant((int)TextureAddressMode.Wrap),
            Getter = static resource =>
                new Variant((int)((Texture2D)resource).GetAddressMode(TextureCoordinate.V)),
            Setter = static (Resource resource, in Variant value) =>
                ((Texture2D)resource).SetAddressMode(
                    TextureCoordinate.V, (TextureAddressMode)value.GetInt()),
            Hint = "V texture coordinate address mode",
            Options = new Widgets.EditVariantOptions().Enum(_textureAddressMode),
        },
    ];
}
