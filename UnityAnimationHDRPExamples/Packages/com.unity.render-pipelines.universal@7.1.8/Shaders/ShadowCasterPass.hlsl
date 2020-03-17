#ifndef UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED
#define UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 texcoord     : TEXCOORD0;
#ifdef _VERTEX_SKINNING
    float4 weights : BLENDWEIGHTS;
    uint4 indices : BLENDINDICES;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv           : TEXCOORD0;
    float4 positionCS   : SV_POSITION;
};

float4 GetShadowPositionHClip(Attributes input)
{
#ifdef _VERTEX_SKINNING
    float3 skinnedPositionOS = float3(0, 0, 0);
    float3 skinnedNormalOS = float3(0, 0, 0);
    for (int i = 0; i < 4; ++i)
    {
        float3x4 skinMatrix = _SkinMatrices[input.indices[i] + (int)_SkinMatricesOffset];
        float3 vtransformed = mul(skinMatrix, input.positionOS);
        float3 ntransformed = mul(skinMatrix, float4(input.normalOS, 0));

        skinnedPositionOS += vtransformed * input.weights[i];
        skinnedNormalOS += ntransformed * input.weights[i];
    }
    float3 positionWS = TransformObjectToWorld(skinnedPositionOS);
    float3 normalWS = TransformObjectToWorldNormal(skinnedNormalOS);
#else
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    return positionCS;
}

Varyings ShadowPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = GetShadowPositionHClip(input);
    return output;
}

half4 ShadowPassFragment(Varyings input) : SV_TARGET
{
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
    return 0;
}

#endif
