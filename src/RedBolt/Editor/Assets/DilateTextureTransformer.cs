// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Assets;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Utility;
using ImageResource = RedBolt.Resource.Image;

/// <summary>Asset transformer that dilates opaque regions of texture. Only PNG textures are supported.</summary>
[Reflected(ObjectCategory.Category_Transformer)]
public partial class DilateTextureTransformer : AssetTransformer
{
    public DilateTextureTransformer()
    {
        _fileNamePatterns = [.. DefaultPatterns];
    }

    /// <summary>Implement AssetTransformer.</summary>
    public override bool IsApplicable(AssetTransformerInput input)
    {
        EnsureInitialized();

        var resourceName = input.ResourceName;
        foreach (var regex in _fileNamePatternsRegex)
        {
            if (regex.IsMatch(resourceName))
                return true;
        }

        return false;
    }

    /// <summary>Implement AssetTransformer.</summary>
    public override bool Execute(AssetTransformerInput input, AssetTransformerOutput output,
        IReadOnlyList<AssetTransformer> transformers)
    {
        using var image = new ImageResource();
        if (!image.LoadFile(new FileIdentifier(input.InputFileName)))
            return false;

        if (image.IsCompressed() || image.Depth > 1)
        {
            Log.GetLogger().LogError("Dilation is not supported for image of this type");
            return false;
        }

        DilateImage(image, _alphaThreshold);

        if (!image.SavePNG(input.InputFileName))
            return false;

        output.SourceModified = true;
        return true;
    }

    private static readonly StringVector DefaultPatterns = [@".+\.png"];

    private static readonly Vector2i[] Offsets = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];

    private static void DilateImage(ImageResource image, float alphaThreshold)
    {
        var width = image.Width;
        var height = image.Height;
        var numPixels = width * height;
        var pixels = new Color[numPixels];
        for (var y = 0; y < height; ++y)
        {
            for (var x = 0; x < width; ++x)
                pixels[y * width + x] = image.GetPixel(x, y);
        }

        // Queue all opaque pixels.
        var opaqueIndex = new int[numPixels];
        System.Array.Fill(opaqueIndex, -1);
        var queue = new List<int>();
        for (var i = 0; i < numPixels; ++i)
        {
            if (pixels[i].A > alphaThreshold)
            {
                opaqueIndex[i] = i;
                queue.Add(i);
            }
        }

        // Multi-source BFS.
        for (var i = 0; i < queue.Count; ++i)
        {
            var index = queue[i];
            var x = index % width;
            var y = index / width;
            var color = pixels[opaqueIndex[index]];

            foreach (var offset in Offsets)
            {
                var neighborX = x + offset.X;
                var neighborY = y + offset.Y;
                if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height)
                    continue;

                var neighbor = neighborY * width + neighborX;
                if (opaqueIndex[neighbor] == -1)
                {
                    opaqueIndex[neighbor] = opaqueIndex[index];
                    queue.Add(neighbor);

                    var neighborColor = color;
                    neighborColor.A = pixels[neighbor].A;
                    image.SetPixel(neighborX, neighborY, neighborColor);
                }
            }
        }
    }

    private void MarkRegexesDirty()
    {
        _fileNamePatternsRegex.Clear();
    }

    private void EnsureInitialized()
    {
        if (_fileNamePatternsRegex.Count != 0)
            return;

        foreach (var pattern in _fileNamePatterns)
        {
            try
            {
                // Match the whole resource name, like std::regex_match.
                var regex = new Regex(
                    $@"\A(?:{pattern})\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                _fileNamePatternsRegex.Add(regex);
            }
            catch (ArgumentException e)
            {
                Log.GetLogger().LogError("Invalid file name regex: {Message}", e.Message);
            }
        }
    }

    private static object? GetFileNamePatternsDefault() => new StringVector(DefaultPatterns);

    [Serialized("File Name Regex", typeof(StringVector), DefaultSource = nameof(GetFileNamePatternsDefault),
        Setter = nameof(SetFileNamePatterns))]
    private object? GetFileNamePatterns() => new StringVector(_fileNamePatterns);

    private void SetFileNamePatterns(object? value)
    {
        _fileNamePatterns = value is StringVector patterns ? [.. patterns] : [];
        MarkRegexesDirty();
    }

    [Serialized("Alpha Threshold")]
    private float _alphaThreshold;

    private List<string> _fileNamePatterns;
    private readonly List<Regex> _fileNamePatternsRegex = [];
}
