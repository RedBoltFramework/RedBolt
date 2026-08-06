// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

/// <summary>Constants declared as preprocessor macros by tinygltf.</summary>
internal static class GLTFConstants
{
    public const int ModePoints = 0;
    public const int ModeLine = 1;
    public const int ModeLineLoop = 2;
    public const int ModeLineStrip = 3;
    public const int ModeTriangles = 4;
    public const int ModeTriangleStrip = 5;
    public const int ModeTriangleFan = 6;

    public const int ComponentTypeByte = 5120;
    public const int ComponentTypeUnsignedByte = 5121;
    public const int ComponentTypeShort = 5122;
    public const int ComponentTypeUnsignedShort = 5123;
    public const int ComponentTypeInt = 5124;
    public const int ComponentTypeUnsignedInt = 5125;
    public const int ComponentTypeFloat = 5126;
    public const int ComponentTypeDouble = 5130;

    public const int TextureFilterNearest = 9728;
    public const int TextureFilterLinear = 9729;
    public const int TextureFilterNearestMipmapNearest = 9984;
    public const int TextureFilterLinearMipmapNearest = 9985;
    public const int TextureFilterNearestMipmapLinear = 9986;
    public const int TextureFilterLinearMipmapLinear = 9987;

    public const int TextureWrapRepeat = 10497;
    public const int TextureWrapClampToEdge = 33071;
    public const int TextureWrapMirroredRepeat = 33648;

    public const int TypeVec2 = 2;
    public const int TypeVec3 = 3;
    public const int TypeVec4 = 4;
    public const int TypeMat4 = 36;
    public const int TypeScalar = 65;
}
