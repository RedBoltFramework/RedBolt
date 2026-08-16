// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using Tg = RedBolt.Bindings.TinyGltf;

using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;

/// <summary>Utility to import textures on-demand.</summary>
/// <remarks>Textures cannot be imported after cooking.</remarks>
internal sealed class GLTFTextureImporter
{
    public GLTFTextureImporter(GLTFImporterBase importerBase)
    {
        _base = importerBase;
        _model = _base.GetModel();
        for (var i = 0; i < _model.Textures.Count; ++i)
            _texturesAsIs.Add(ImportTexture(i, _model.Textures[i]));
    }

    public void CookTextures()
    {
        if (_texturesCooked)
            throw new InvalidOperationException("Textures are already cooking");
        _texturesCooked = true;
        foreach (var (indices, texture) in _texturesMRO)
        {
            var (metallicRoughnessTextureIndex, occlusionTextureIndex) = indices;
            texture.RepackedImage = ImportRMOTexture(metallicRoughnessTextureIndex, occlusionTextureIndex,
                texture.FakeTexture.GetName());
        }
        if (_base.GetSettings().GpuResources)
        {
            foreach (var texture in _texturesAsIs)
                LoadGPUTexture(texture.FakeTexture, texture.Image);
            foreach (var texture in _texturesMRO.Values)
                LoadGPUTexture(texture.FakeTexture, texture.RepackedImage!);
        }
    }

    public void SaveResources()
    {
        foreach (var texture in _texturesAsIs)
        {
            if (!texture.IsReferenced)
                continue;
            _base.SaveResource(texture.Image);
            texture.CookedSamplerParams?.SaveFile(new FileIdentifier(texture.CookedSamplerParams.GetAbsoluteFileName()));
        }
        foreach (var texture in _texturesMRO.Values)
        {
            _base.SaveResource(texture.RepackedImage!);
            texture.CookedSamplerParams?.SaveFile(new FileIdentifier(texture.CookedSamplerParams.GetAbsoluteFileName()));
        }
    }

    public Texture2D ReferenceTextureAsIs(int textureIndex)
    {
        if (_texturesCooked)
            throw new InvalidOperationException("Cannot reference textures after cooking");
        if (textureIndex >= _texturesAsIs.Count)
            throw new InvalidOperationException($"Invalid texture #{textureIndex} is referenced");
        var texture = _texturesAsIs[textureIndex];
        texture.IsReferenced = true;
        return texture.FakeTexture;
    }

    public Texture2D ReferenceRoughnessMetallicOcclusionTexture(
        int metallicRoughnessTextureIndex, int occlusionTextureIndex)
    {
        if (_texturesCooked)
            throw new InvalidOperationException("Cannot reference textures after cooking");
        if (metallicRoughnessTextureIndex < 0 && occlusionTextureIndex < 0)
            throw new InvalidOperationException("At least one texture should be referenced");
        if (metallicRoughnessTextureIndex >= _texturesAsIs.Count)
            throw new InvalidOperationException($"Invalid metallic-roughness texture #{metallicRoughnessTextureIndex} is referenced");
        if (occlusionTextureIndex >= _texturesAsIs.Count)
            throw new InvalidOperationException($"Invalid occlusion texture #{occlusionTextureIndex} is referenced");

        var key = (metallicRoughnessTextureIndex, occlusionTextureIndex);
        var partialKeyA = (metallicRoughnessTextureIndex, -1);
        var partialKeyB = (-1, occlusionTextureIndex);
        // Try to find exact match
        if (_texturesMRO.TryGetValue(key, out var result))
            return result.FakeTexture;
        // Try to re-purpose partial match A
        if (_texturesMRO.Remove(partialKeyA, out result))
        {
            Debug.Assert(occlusionTextureIndex != -1);
            _texturesMRO.Add(key, result);
            return result.FakeTexture;
        }
        // Try to re-purpose partial match B
        if (_texturesMRO.Remove(partialKeyB, out result))
        {
            Debug.Assert(metallicRoughnessTextureIndex != -1);
            _texturesMRO.Add(key, result);
            return result.FakeTexture;
        }

        // Create new texture
        var referenceTexture = metallicRoughnessTextureIndex >= 0
            ? _texturesAsIs[metallicRoughnessTextureIndex]
            : _texturesAsIs[occlusionTextureIndex];
        var imageName = _base.GetResourceName(referenceTexture.NameHint, "Textures/", "Texture", ".png");
        result = new ImportedRMOTexture { FakeTexture = new Texture2D() };
        result.FakeTexture.SetName(imageName);
        result.CookedSamplerParams = CookSamplerParams(result.FakeTexture, referenceTexture.SamplerParams);
        _texturesMRO.Add(key, result);
        return result.FakeTexture;
    }

    public static bool LoadImageData(Tg.Image image, int imageIndex, Std.String error, Std.String warning,
        int requestedWidth, int requestedHeight, IntPtr bytes, int size, IntPtr userData)
    {
        var imageName = image.Name.ToString();
        image.Name = Path.GetFileName(imageName.Length == 0 ? image.Uri.ToString() : imageName);
        image.AsIs = true;
        var imageBytes = new byte[size];
        Marshal.Copy(bytes, imageBytes, 0, size);
        image.ImageValue.Clear();
        foreach (var value in imageBytes)
            image.ImageValue.Add(value);
        return true;
    }

    private sealed class SamplerParams
    {
        public TextureFilterMode FilterMode = TextureFilterMode.Default;
        public bool Mipmaps = true;
        public TextureAddressMode WrapU = TextureAddressMode.Wrap;
        public TextureAddressMode WrapV = TextureAddressMode.Wrap;
    }

    private sealed class ImportedTexture
    {
        public bool IsReferenced;
        public string NameHint = string.Empty;
        public BinaryFile Image = null!;
        public Texture2D FakeTexture = null!;
        public SamplerParams SamplerParams = new();
        public XMLFile? CookedSamplerParams;
    }

    private sealed class ImportedRMOTexture
    {
        public Texture2D FakeTexture = null!;
        public XMLFile? CookedSamplerParams;
        public Image? RepackedImage;
    }

    private static TextureFilterMode GetFilterMode(Tg.Sampler sampler)
    {
        if (sampler.MinFilter == -1 || sampler.MagFilter == -1)
            return TextureFilterMode.Default;
        if (sampler.MagFilter == GLTFConstants.TextureFilterNearest)
        {
            if (sampler.MinFilter == GLTFConstants.TextureFilterNearest ||
                sampler.MinFilter == GLTFConstants.TextureFilterNearestMipmapNearest)
                return TextureFilterMode.Nearest;
            return TextureFilterMode.NearestAnisotropic;
        }
        if (sampler.MinFilter == GLTFConstants.TextureFilterLinearMipmapNearest)
            return TextureFilterMode.Bilinear;
        return TextureFilterMode.Default;
    }

    private static bool HasMipmaps(Tg.Sampler sampler) =>
        sampler.MinFilter == -1 || sampler.MagFilter == -1 ||
        sampler.MinFilter == GLTFConstants.TextureFilterNearestMipmapNearest ||
        sampler.MinFilter == GLTFConstants.TextureFilterLinearMipmapNearest ||
        sampler.MinFilter == GLTFConstants.TextureFilterNearestMipmapLinear ||
        sampler.MinFilter == GLTFConstants.TextureFilterLinearMipmapLinear;

    private static TextureAddressMode GetAddressMode(int sourceMode) => sourceMode switch
    {
        GLTFConstants.TextureWrapClampToEdge => TextureAddressMode.Clamp,
        GLTFConstants.TextureWrapMirroredRepeat => TextureAddressMode.Mirror,
        _ => TextureAddressMode.Wrap
    };

    private BinaryFile ImportImageAsIs(int imageIndex, Tg.Image sourceImage)
    {
        var image = new BinaryFile();
        var imageUri = sourceImage.Uri.ToString();
        var mimeType = sourceImage.MimeType.ToString();
        var sourceName = sourceImage.Name.ToString();
        if (mimeType == "image/jpeg" || imageUri.EndsWith(".jpg", StringComparison.Ordinal) ||
            imageUri.EndsWith(".jpeg", StringComparison.Ordinal))
            image.SetName(_base.GetResourceName(sourceName, "Textures/", "Texture", ".jpg"));
        else if (mimeType == "image/png" || imageUri.EndsWith(".png", StringComparison.Ordinal))
            image.SetName(_base.GetResourceName(sourceName, "Textures/", "Texture", ".png"));
        else
            throw new InvalidOperationException($"Image #{imageIndex} '{sourceImage.Name}' has unknown type '{sourceImage.MimeType}'");
        var imageBytes = new byte[sourceImage.ImageValue.Count];
        for (var i = 0; i < imageBytes.Length; ++i)
            imageBytes[i] = sourceImage.ImageValue[i];
        image.SetData(imageBytes);
        return image;
    }

    private static void LoadGPUTexture(Texture2D texture, BinaryFile image)
    {
        image.GetMutableBuffer().SetName(texture.GetName());
        var deserializer = image.AsDeserializer();
        deserializer.Seek(0);
        texture.Load(deserializer);
    }

    private static void LoadGPUTexture(Texture2D texture, Image image) => texture.SetData(image);

    private static Image DecodeImage(BinaryFile imageAsIs)
    {
        var deserializer = imageAsIs.AsDeserializer();
        deserializer.Seek(0);
        var decodedImage = new Image();
        decodedImage.SetName(imageAsIs.GetName());
        decodedImage.Load(deserializer);
        return decodedImage;
    }

    private ImportedTexture ImportTexture(int textureIndex, Tg.Texture sourceTexture)
    {
        _base.CheckImage(sourceTexture.Source);
        var sourceImage = _model.Images[sourceTexture.Source];
        var result = new ImportedTexture
        {
            NameHint = sourceImage.Name.ToString(),
            Image = ImportImageAsIs(sourceTexture.Source, sourceImage),
            FakeTexture = new Texture2D()
        };
        result.FakeTexture.SetName(result.Image.GetName());
        if (sourceTexture.Sampler >= 0)
        {
            _base.CheckSampler(sourceTexture.Sampler);
            var sourceSampler = _model.Samplers[sourceTexture.Sampler];
            result.SamplerParams.FilterMode = GetFilterMode(sourceSampler);
            result.SamplerParams.Mipmaps = HasMipmaps(sourceSampler);
            result.SamplerParams.WrapU = GetAddressMode(sourceSampler.WrapS);
            result.SamplerParams.WrapV = GetAddressMode(sourceSampler.WrapT);
        }
        result.CookedSamplerParams = CookSamplerParams(result.Image, result.SamplerParams);
        return result;
    }

    private XMLFile? CookSamplerParams(Resource image, SamplerParams samplerParams)
    {
        string[] addressModeNames = ["wrap", "mirror", "clamp", ""];
        string[] filterModeNames = ["nearest", "bilinear", "trilinear", "anisotropic", "nearestanisotropic", "default"];
        var xmlFile = new XMLFile();
        var rootElement = xmlFile.CreateRoot("texture");
        if (samplerParams.WrapU != TextureAddressMode.Wrap)
        {
            var childElement = rootElement.CreateChild("address");
            childElement.SetAttribute("coord", "u");
            childElement.SetAttribute("mode", addressModeNames[(int)samplerParams.WrapU]);
        }
        if (samplerParams.WrapV != TextureAddressMode.Wrap)
        {
            var childElement = rootElement.CreateChild("address");
            childElement.SetAttribute("coord", "v");
            childElement.SetAttribute("mode", addressModeNames[(int)samplerParams.WrapV]);
        }
        if (samplerParams.FilterMode != TextureFilterMode.Default)
        {
            var childElement = rootElement.CreateChild("filter");
            childElement.SetAttribute("mode", filterModeNames[(int)samplerParams.FilterMode]);
        }
        if (!samplerParams.Mipmaps)
        {
            var childElement = rootElement.CreateChild("mipmap");
            childElement.SetBool("enable", false);
        }
        // Don't create XML if all parameters are default
        if (!rootElement.GetChild().NotNull)
            return null;
        var imageName = image.GetName();
        xmlFile.SetName(Path.ChangeExtension(imageName, ".xml"));
        xmlFile.SetAbsoluteFileName(Path.ChangeExtension(_base.GetAbsoluteFileName(imageName), ".xml"));
        return xmlFile;
    }

    private Image ImportRMOTexture(int metallicRoughnessTextureIndex, int occlusionTextureIndex, string name)
    {
        // Unpack input images
        var metallicRoughnessImage = metallicRoughnessTextureIndex >= 0
            ? DecodeImage(_texturesAsIs[metallicRoughnessTextureIndex].Image) : null;
        var occlusionImage = occlusionTextureIndex >= 0
            ? DecodeImage(_texturesAsIs[occlusionTextureIndex].Image) : null;
        if (metallicRoughnessImage is null && occlusionImage is null)
            throw new InvalidOperationException($"Neither metallic-roughness texture #{metallicRoughnessTextureIndex} nor occlusion texture #{occlusionTextureIndex} can be loaded");
        var metallicRoughnessImageSize = metallicRoughnessImage?.GetSize() ?? Vector3i.Zero;
        var occlusionImageSize = occlusionImage?.GetSize() ?? Vector3i.Zero;
        var repackedImageSize = new Vector2i(Math.Max(metallicRoughnessImageSize.X, occlusionImageSize.X),
            Math.Max(metallicRoughnessImageSize.Y, occlusionImageSize.Y));
        if (repackedImageSize.X <= 0 || repackedImageSize.Y <= 0)
            throw new InvalidOperationException("Repacked metallic-roughness-occlusion texture has invalid size");
        if (metallicRoughnessImage is not null && new Vector2i(metallicRoughnessImageSize.X, metallicRoughnessImageSize.Y) != repackedImageSize)
            metallicRoughnessImage.Resize(repackedImageSize.X, repackedImageSize.Y);
        if (occlusionImage is not null && new Vector2i(occlusionImageSize.X, occlusionImageSize.Y) != repackedImageSize)
            occlusionImage.Resize(repackedImageSize.X, repackedImageSize.Y);
        var finalImage = new Image();
        finalImage.SetName(name);
        finalImage.SetSize(repackedImageSize.X, repackedImageSize.Y, 1, occlusionImage is not null ? 4 : 3);
        for (var y = 0; y < repackedImageSize.Y; ++y)
        for (var x = 0; x < repackedImageSize.X; ++x)
        {
            // 0xOO__MMRR
            uint color;
            if (metallicRoughnessImage is not null)
            {
                // 0x__MMRR__
                var value = metallicRoughnessImage.GetPixelInt(x, y);
                color = (value >> 8) & 0xffff;
            }
            else color = 0x0000ffff;
            if (occlusionImage is not null)
            {
                // 0x______OO
                var value = occlusionImage.GetPixelInt(x, y);
                color |= (value & 0xff) << 24;
            }
            else color |= 0xff000000;
            finalImage.SetPixelInt(x, y, color);
        }
        return finalImage;
    }

    private readonly GLTFImporterBase _base;
    private readonly Tg.Model _model;
    private readonly List<ImportedTexture> _texturesAsIs = [];
    private readonly Dictionary<(int, int), ImportedRMOTexture> _texturesMRO = [];
    private bool _texturesCooked;
}
