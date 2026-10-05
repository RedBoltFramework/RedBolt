// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Resource;

/// <summary>Image mipmap generation algorithm.</summary>
public enum ImageMipMapAlgorithm
{
    /// <summary>Component-wise average. Color channels and alpha channel are treated in the same way.</summary>
    Average,

    /// <summary>Alpha-weighted linear composition of colors. Preserves perceived color for transparent images.</summary>
    AlphaWeightedLinear,
}

/// <summary>Image mipmap generation parameters.</summary>
public struct ImageMipMapParams
{
    public ImageMipMapParams()
    {
    }

    public ImageMipMapAlgorithm Algorithm { get; set; } = ImageMipMapAlgorithm.Average;

    /// <summary>
    /// Alpha is scaled by this factor after color composition.
    /// Adjust this parameter to tweak perceived cutout texture transparency.
    /// </summary>
    public float AlphaScaleFactor { get; set; } = 1.0f;
}
