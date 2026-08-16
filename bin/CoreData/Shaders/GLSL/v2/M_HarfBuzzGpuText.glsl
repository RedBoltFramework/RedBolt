// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#ifndef UNLIT
    #define UNLIT
#endif
#define URHO3D_PIXEL_NEED_TEXCOORD
#define URHO3D_CUSTOM_MATERIAL_UNIFORMS

#include "_Config.glsl"
#include "_Uniforms.glsl"
#include "_DefaultSamplers.glsl"

UNIFORM_BUFFER_BEGIN(4, Material)
    DEFAULT_MATERIAL_UNIFORMS
UNIFORM_BUFFER_END(4, Material)

#include "_Material.glsl"

#ifdef URHO3D_VERTEX_SHADER
void main()
{
    VertexTransform vertexTransform = GetVertexTransform();
    Vertex_SetAll(vertexTransform, cNormalScale, cUOffset, cVOffset, cLMOffset);
}
#endif

#ifdef URHO3D_PIXEL_SHADER
#include "_HarfBuzzGpu.glsl"

void main()
{
    half4 finalColor = vColor;
    finalColor.a *= HarfBuzzGpuCoverage(vTexCoord);
    gl_FragColor = GammaToLightSpaceAlpha(finalColor);
}
#endif
