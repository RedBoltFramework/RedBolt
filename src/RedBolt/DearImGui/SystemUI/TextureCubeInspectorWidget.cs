// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using Microsoft.Extensions.Logging;
using System.Collections.Generic;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.RenderAPI;
using RedBolt.Resource;

/// <summary>SystemUI widget used to edit 2D texture.</summary>
public class TextureCubeInspectorWidget : ResourceInspectorWidget
{
    public TextureCubeInspectorWidget(IReadOnlyList<Resource> resources)
        : base(resources, _properties)
    {
        SubscribeToEvent<GraphicsEvents.BeginRenderingEvent>(
            (ref GraphicsEvents.BeginRenderingEvent _) => GeneratePendingSH());
    }

    public override void Dispose()
    {
        _generator.Dispose();
        _texturesToGenerateSH.Clear();
        base.Dispose();
    }

    public override void RenderContent()
    {
        base.RenderContent();

        if (ImGui.Button("Generate Spherical Harmonics"u8))
        {
            foreach (var resource in GetResources())
                _texturesToGenerateSH.Add(resource.GetName());
        }
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
            Getter = static resource => new Variant(((TextureCube)resource).GetSRGB()),
            Setter = static (Resource resource, in Variant value) =>
                ((TextureCube)resource).SetSRGB(value.GetBool()),
            Hint = "SRGB",
        },
        new()
        {
            Name = "Linear",
            DefaultValue = new Variant(false),
            Getter = static resource => new Variant(((TextureCube)resource).GetLinear()),
            Setter = static (Resource resource, in Variant value) =>
                ((TextureCube)resource).SetLinear(value.GetBool()),
            Hint = "Linear color space",
        },
        new()
        {
            Name = "Filter Mode",
            DefaultValue = new Variant((int)TextureFilterMode.Default),
            Getter = static resource => new Variant((int)((TextureCube)resource).GetFilterMode()),
            Setter = static (Resource resource, in Variant value) =>
                ((TextureCube)resource).SetFilterMode((TextureFilterMode)value.GetInt()),
            Hint = "Texture Filter Mode",
            Options = new Widgets.EditVariantOptions().Enum(_textureFilterModes),
        },
        new()
        {
            Name = "U Address Mode",
            DefaultValue = new Variant((int)TextureAddressMode.Wrap),
            Getter = static resource =>
                new Variant((int)((TextureCube)resource).GetAddressMode(TextureCoordinate.U)),
            Setter = static (Resource resource, in Variant value) =>
                ((TextureCube)resource).SetAddressMode(
                    TextureCoordinate.U, (TextureAddressMode)value.GetInt()),
            Hint = "U texture coordinate address mode",
            Options = new Widgets.EditVariantOptions().Enum(_textureAddressMode),
        },
        new()
        {
            Name = "V Address Mode",
            DefaultValue = new Variant((int)TextureAddressMode.Wrap),
            Getter = static resource =>
                new Variant((int)((TextureCube)resource).GetAddressMode(TextureCoordinate.V)),
            Setter = static (Resource resource, in Variant value) =>
                ((TextureCube)resource).SetAddressMode(
                    TextureCoordinate.V, (TextureAddressMode)value.GetInt()),
            Hint = "V texture coordinate address mode",
            Options = new Widgets.EditVariantOptions().Enum(_textureAddressMode),
        },
        new()
        {
            Name = "W Address Mode",
            DefaultValue = new Variant((int)TextureAddressMode.Wrap),
            Getter = static resource =>
                new Variant((int)((TextureCube)resource).GetAddressMode(TextureCoordinate.W)),
            Setter = static (Resource resource, in Variant value) =>
                ((TextureCube)resource).SetAddressMode(
                    TextureCoordinate.W, (TextureAddressMode)value.GetInt()),
            Hint = "W texture coordinate address mode",
            Options = new Widgets.EditVariantOptions().Enum(_textureAddressMode),
        },
    ];

    private void GeneratePendingSH()
    {
        var cache = GetSubsystem<ResourceCache>();
        foreach (var textureName in _texturesToGenerateSH)
        {
            var texture = cache.GetResourceOrNull<TextureCube>(textureName);
            var imageXml = cache.GetResourceOrNull<XMLFile>(textureName);
            if (texture is null || imageXml is null)
            {
                Log.GetLogger().LogError(
                    "Cannot generate Spherical Harmonics for texture {TextureName}: must be valid XML file",
                    textureName);
                continue;
            }
            _generator.Generate(texture, imageXml);
        }
        _texturesToGenerateSH.Clear();
    }

    private readonly List<string> _texturesToGenerateSH = [];
    private readonly SphericalHarmonicsGenerator _generator = new();
}
