#ifndef TOON_SHADER_LAB_BASIC_INPUT_INCLUDED
#define TOON_SHADER_LAB_BASIC_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

// 모든 패스가 같은 배치를 사용하여 SRP Batcher와 호환되도록 한다.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _LitColor;
    half4 _ShadowColor;
    float _ShadeThreshold;
    float _ShadeSoftness;
    float _DisplayMode;
    float _Cull;
CBUFFER_END

struct ToonAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct ToonVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    half fogFactor : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ToonVaryings ToonVertex(ToonAttributes input)
{
    ToonVaryings output = (ToonVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

half3 ReadBaseColor(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
}

half EvaluateLightBand(float3 normalWS, float3 lightDirectionWS)
{
    // 내적 범위는 -1~1이다. 기준을 올리면 어두운 영역이 넓어진다.
    float ndotl = dot(normalWS, lightDirectionWS);
    // 경계 폭이 0일 때 smoothstep의 두 끝값이 같아지는 상황을 방지한다.
    float width = max(_ShadeSoftness, 0.001);
    return smoothstep(_ShadeThreshold - width, _ShadeThreshold + width, ndotl);
}
#endif
