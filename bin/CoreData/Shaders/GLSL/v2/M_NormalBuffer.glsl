// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#define URHO3D_PIXEL_NEED_TEXCOORD
#define URHO3D_SURFACE_NEED_NORMAL

#define URHO3D_MATERIAL_ALBEDO URHO3D_TEXTURE_ALBEDO
#define URHO3D_MATERIAL_NORMAL URHO3D_TEXTURE_NORMAL

#include "_Material.glsl"
#include "_DefaultSamplers.glsl"

#ifdef URHO3D_VERTEX_SHADER
void main()
{
    VertexTransform vertexTransform = GetVertexTransform();
    Vertex_SetAll(vertexTransform, cNormalScale, cUOffset, cVOffset, cLMOffset);
}
#endif

#ifdef URHO3D_PIXEL_SHADER
void main()
{
#ifdef URHO3D_DEPTH_ONLY_PASS
    Pixel_DepthOnly(sAlbedo, vTexCoord);
#else
    SurfaceData surfaceData;
    Surface_SetCommon(surfaceData);
    Surface_SetNormal(surfaceData, vNormal, sNormal, vTexCoord, vTangent, vBitangentXY);
    Surface_SetBaseAlbedo(surfaceData, cMatDiffColor, cAlphaCutoff, vColor, sAlbedo, vTexCoord, URHO3D_MATERIAL_ALBEDO);
    gl_FragColor = vec4(surfaceData.normal * 0.5 + 0.5, 0.0);
#endif
}
#endif
