#ifndef UNIVERSAL_SIMPLE_LIT_INPUT_INCLUDED
#define UNIVERSAL_SIMPLE_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
float _SkinMatricesOffset;
CBUFFER_END

uniform StructuredBuffer<float3x4> _SkinMatrices;

TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);

half4 SampleSpecularSmoothness(half2 uv, half alpha, half4 specColor, TEXTURE2D_PARAM(specMap, sampler_specMap))
{
    half4 specularSmoothness = half4(0.0h, 0.0h, 0.0h, 1.0h);
#ifdef _SPECGLOSSMAP
    specularSmoothness = SAMPLE_TEXTURE2D(specMap, sampler_specMap, uv) * specColor;
#elif defined(_SPECULAR_COLOR)
    specularSmoothness = specColor;
#endif

#ifdef _GLOSSINESS_FROM_BASE_ALPHA
    specularSmoothness.a = exp2(10 * alpha + 1);
#else
    specularSmoothness.a = exp2(10 * specularSmoothness.a + 1);
#endif

    return specularSmoothness;
}

// void Unity_LinearBlendSkinning_float(uint4 indices, int indexOffset, float4 weights, float3 positionIn, float3 normalIn, float3 tangentIn, out float3 positionOut, out float3 normalOut, out float3 tangentOut)
// {
//     for (int i = 0; i < 4; i++)
//     {
//         float3x4 skinMatrix = _SkinMatrices[indices[i] + indexOffset];
//         float3 vtransformed = mul(skinMatrix, float4(positionIn, 1));
//         float3 ntransformed = mul(skinMatrix, float4(normalIn, 0));
//         float3 ttransformed = mul(skinMatrix, float4(tangentIn, 0));

//         positionOut += vtransformed * weights[i];
//         normalOut += ntransformed * weights[i];
//         tangentOut += ttransformed * weights[i];
//     }
// }

#endif
