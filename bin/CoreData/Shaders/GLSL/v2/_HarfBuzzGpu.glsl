// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

// RedBolt adapter for HarfBuzz 14.2.1's monochrome GPU draw shader.
SAMPLER_HIGHP(11, isampler2D sHarfBuzzAtlas)
#define HB_GPU_ATLAS_DECLARED
#define HB_GPU_ATLAS_2D
#define HB_GPU_FIXED_ATLAS_WIDTH 512
#define hb_gpu_atlas sHarfBuzzAtlas
#include "_HarfBuzzGpuFragment.glsl"
#include "_HarfBuzzGpuDrawFragment.glsl"
#undef hb_gpu_atlas
#undef HB_GPU_ATLAS_DECLARED

bool HarfBuzzGpuDecode(vec2 packedUv, out uint glyphLoc, out vec2 renderCoord)
{
    ivec2 packedOffset = ivec2(floor(packedUv)) - ivec2(2);
    if (any(lessThan(packedOffset, ivec2(0))) || any(greaterThan(packedOffset, ivec2(511))))
        return false;

    glyphLoc = uint(packedOffset.x + packedOffset.y * 512);
    if (glyphLoc == 0u)
        return false;

    vec2 local = (fract(packedUv) - vec2(0.25)) * 2.0;
    vec4 extents = vec4(hb_gpu_fetch(int(glyphLoc))) * 0.25;
    renderCoord = vec2(
        mix(extents.x, extents.z, local.x),
        mix(extents.w, extents.y, local.y));
    return true;
}

float HarfBuzzGpuCoverageDecoded(vec2 packedUv, uint glyphLoc, vec2 renderCoord)
{
    vec4 extents = vec4(hb_gpu_fetch(int(glyphLoc))) * 0.25;
    // Keep derivatives on the continuous interpolant. Taking them after fract()
    // lets helper lanes cross packed integer boundaries and joins narrow contours.
    vec2 emsPerPixel = fwidth(packedUv) * 2.0 * abs(extents.zw - extents.xy);
    vec2 pixelsPerEm = 1.0 / max(emsPerPixel, vec2(1.0 / 65536.0));
    float coverage = _hb_gpu_slug_single(renderCoord, pixelsPerEm, glyphLoc);

#ifndef HB_GPU_NO_MSAA
    ivec4 header1 = hb_gpu_fetch(int(glyphLoc) + 1);
    float ppem = min(float(header1.b), float(header1.a))
        / max(max(emsPerPixel.x, emsPerPixel.y), 1.0 / 65536.0);
    if (ppem < 16.0)
    {
        vec2 delta = emsPerPixel * (1.0 / 3.0);
        float multisampledCoverage = 0.25 *
            (_hb_gpu_slug_single(renderCoord + vec2(-delta.x, -delta.y), pixelsPerEm, glyphLoc) +
             _hb_gpu_slug_single(renderCoord + vec2( delta.x, -delta.y), pixelsPerEm, glyphLoc) +
             _hb_gpu_slug_single(renderCoord + vec2(-delta.x,  delta.y), pixelsPerEm, glyphLoc) +
             _hb_gpu_slug_single(renderCoord + vec2( delta.x,  delta.y), pixelsPerEm, glyphLoc));
        coverage = mix(coverage, multisampledCoverage, smoothstep(16.0, 8.0, ppem));
    }
#endif

    return coverage;
}

float HarfBuzzGpuCoverage(vec2 packedUv)
{
    uint glyphLoc;
    vec2 renderCoord;
    if (!HarfBuzzGpuDecode(packedUv, glyphLoc, renderCoord))
        return 0.0;
    return HarfBuzzGpuCoverageDecoded(packedUv, glyphLoc, renderCoord);
}
