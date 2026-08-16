// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#ifndef _INTERNAL_SAMPLERS_GLSL_
#define _INTERNAL_SAMPLERS_GLSL_

#ifndef _CONFIG_GLSL_
    #error Include _Config.glsl before _InternalSamplers.glsl
#endif

#ifdef URHO3D_PIXEL_SHADER

SAMPLER(0, sampler2D sLightRamp)
#ifndef URHO3D_LIGHT_POINT
    SAMPLER(1, sampler2D sLightShape)
#else
    SAMPLER(1, samplerCube sLightShape)
#endif
#if defined(URHO3D_VARIANCE_SHADOW_MAP)
    SAMPLER_HIGHP(2, sampler2D sShadowMap)
#else
    SAMPLER_HIGHP(2, sampler2DShadow sShadowMap)
#endif

#endif // URHO3D_PIXEL_SHADER
#endif // _INTERNAL_SAMPLERS_GLSL_
