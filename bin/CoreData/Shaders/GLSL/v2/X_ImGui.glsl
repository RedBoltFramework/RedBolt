#include "_Config.glsl"
#include "_GammaCorrection.glsl"

VERTEX_OUTPUT_HIGHP(vec2 vTexCoord)
VERTEX_OUTPUT(half4 vColor)

#ifdef URHO3D_VERTEX_SHADER

UNIFORM_BUFFER_BEGIN(0, Camera)
    UNIFORM_HIGHP(mat4 cProjectionMatrix)
UNIFORM_BUFFER_END(0, Camera)

VERTEX_INPUT(vec2 iPos)
VERTEX_INPUT(vec2 iTexCoord)
VERTEX_INPUT(vec4 iColor)

void main()
{
    gl_Position = cProjectionMatrix * vec4(iPos, 0.0, 1.0);
    vTexCoord  = iTexCoord;
    vColor = iColor;
}

#endif

#ifdef URHO3D_PIXEL_SHADER

SAMPLER(0, sampler2D sTexture)
UNIFORM_BUFFER_BEGIN(6, Custom)
    UNIFORM(int cHarfBuzzFontTexture)
UNIFORM_BUFFER_END(6, Custom)

#include "_HarfBuzzGpu.glsl"

vec4 SampleImGuiTexture(vec2 uv)
{
#ifdef REDBOLT_ANDROID
    // Some Android GLES translators expose sampler objects but do not apply their
    // state. ImGui uses a mip-0 bilinear wrap sampler, which is cheap to reproduce.
    ivec2 size = textureSize(sTexture, 0);
    vec2 texel = fract(uv) * vec2(size) - vec2(0.5);
    ivec2 base = ivec2(floor(texel));
    vec2 weight = fract(texel);
    ivec2 p0 = (base % size + size) % size;
    ivec2 p1 = (p0 + ivec2(1)) % size;
    return mix(
        mix(texelFetch(sTexture, ivec2(p0.x, p0.y), 0), texelFetch(sTexture, ivec2(p1.x, p0.y), 0), weight.x),
        mix(texelFetch(sTexture, ivec2(p0.x, p1.y), 0), texelFetch(sTexture, ivec2(p1.x, p1.y), 0), weight.x),
        weight.y);
#else
    return texture(sTexture, uv);
#endif
}

void main()
{
    uint glyphLoc;
    vec2 renderCoord;
    vec4 color = vColor;
    if (cHarfBuzzFontTexture != 0 && HarfBuzzGpuDecode(vTexCoord, glyphLoc, renderCoord))
        color.a *= HarfBuzzGpuCoverageDecoded(vTexCoord, glyphLoc, renderCoord);
    else
        color *= SampleImGuiTexture(vTexCoord);
#ifdef URHO3D_LINEAR_OUTPUT
    color.rgb = GammaToLinearSpace(color.rgb);
#endif
    gl_FragColor = color;
}

#endif
