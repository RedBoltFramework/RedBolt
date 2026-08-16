// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#ifndef _DEFAULT_SAMPLERS_GLSL_
#define _DEFAULT_SAMPLERS_GLSL_

#ifndef _CONFIG_GLSL_
    #error Include _Config.glsl before _DefaultSamplers.glsl
#endif

#ifdef URHO3D_PIXEL_SHADER

SAMPLER(3, sampler2D sAlbedo)
SAMPLER(4, sampler2D sNormal)
SAMPLER(5, sampler2D sProperties)
SAMPLER(6, sampler2D sEmission)
#ifdef URHO3D_MATERIAL_HAS_PLANAR_ENVIRONMENT
    SAMPLER(7, sampler2D sReflection0)
#else
    SAMPLER(8, samplerCube sReflection0)
#endif
SAMPLER_HIGHP(9, sampler2D sDepthBuffer)
SAMPLER(10, samplerCube sReflection1)

#endif // URHO3D_PIXEL_SHADER
#endif // _DEFAULT_SAMPLERS_GLSL_
