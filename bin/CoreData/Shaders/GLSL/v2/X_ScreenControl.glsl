// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#include "_Config.glsl"
#include "_GammaCorrection.glsl"

SAMPLER(0, sampler2D sAlbedo)

UNIFORM_BUFFER_BEGIN(6, Custom)
    UNIFORM_HIGHP(vec4 cRect)
    UNIFORM_HIGHP(vec4 cUVRect)
    UNIFORM(half4 cColor)
    UNIFORM_HIGHP(int cSdf)
UNIFORM_BUFFER_END(6, Custom)

VERTEX_OUTPUT_HIGHP(vec2 vTexCoord)

#ifdef URHO3D_VERTEX_SHADER
VERTEX_INPUT(vec4 iPos)

void main()
{
    gl_Position = vec4(cRect.xy + iPos.xy * cRect.zw, 0.0, 1.0);
    vec2 localCoord = vec2(iPos.x * 0.5 + 0.5, 0.5 - iPos.y * 0.5);
    vTexCoord = mix(cUVRect.xy, cUVRect.zw, localCoord);
}
#endif

#ifdef URHO3D_PIXEL_SHADER
void main()
{
    vec4 sampled = texture(sAlbedo, vTexCoord);
    vec4 color;
    if (cSdf != 0)
    {
        half width = max(fwidth(sampled.a), 0.001);
        color = cColor;
        color.a *= smoothstep(0.5 - width, 0.5 + width, sampled.a);
    }
    else
        color = sampled * cColor;

#ifdef URHO3D_LINEAR_OUTPUT
    color.rgb = GammaToLinearSpace(color.rgb);
#endif
    gl_FragColor = color;
}
#endif
