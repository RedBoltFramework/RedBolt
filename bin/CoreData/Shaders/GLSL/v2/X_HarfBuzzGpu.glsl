#include "_Config.glsl"

UNIFORM_BUFFER_BEGIN(1, Camera)
    UNIFORM_HIGHP(mat4 cViewProj)
UNIFORM_BUFFER_END(1, Camera)

UNIFORM_BUFFER_BEGIN(5, Object)
    UNIFORM_HIGHP(mat4 cModel)
UNIFORM_BUFFER_END(5, Object)

VERTEX_OUTPUT_HIGHP(vec2 vTexCoord)
VERTEX_OUTPUT(half4 vColor)

#ifdef URHO3D_VERTEX_SHADER
VERTEX_INPUT(vec4 iPos)
VERTEX_INPUT(vec2 iTexCoord)
VERTEX_INPUT(vec4 iColor)

void main()
{
    vec3 worldPos = (iPos * cModel).xyz;
    gl_Position = vec4(worldPos, 1.0) * cViewProj;
    vTexCoord = iTexCoord;
    vColor = iColor;
}
#endif

#ifdef URHO3D_PIXEL_SHADER
#include "_HarfBuzzGpu.glsl"

void main()
{
    vec4 finalColor = vColor;
    finalColor.a *= HarfBuzzGpuCoverage(vTexCoord);
#ifdef URHO3D_LINEAR_OUTPUT
    finalColor.rgb *= finalColor.rgb * (finalColor.rgb * 0.305306011 + 0.682171111) + 0.012522878;
#endif
    gl_FragColor = finalColor;
}
#endif
