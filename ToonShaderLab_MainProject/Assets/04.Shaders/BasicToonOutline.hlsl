#ifndef TOON_SHADER_LAB_OUTLINE_INCLUDED
#define TOON_SHADER_LAB_OUTLINE_INCLUDED

#include "BasicToonInput.hlsl"

struct ToonOutlineVaryings
{
    float4 positionCS : SV_POSITION;
    half fogFactor : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

ToonOutlineVaryings ToonOutlineVertex(ToonAttributes input)
{
    ToonOutlineVaryings output = (ToonOutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // 비활성 / 두께 0 / 분석용 표시 모드에서는 삼각형을 한 점으로 모아 그리지 않는다.
    // 드로우 호출 자체는 Renderer Feature에 남으므로 비용 측정 시에는 구분한다.
    if (_OutlineEnabled < 0.5 || _OutlineWidth <= 0.0 || _DisplayMode > 0.5)
    {
        output.positionCS = float4(0, 0, 0, 1);
        return output;
    }

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float4 positionCS = TransformWorldToHClip(positionWS);
    float4 normalCS = mul(GetWorldToHClipMatrix(), float4(normalWS, 0));

    // 표면 법선 방향을 화면에 투영한다. w의 변화도 포함해 원근 / 직교 투영을 처리한다.
    float2 direction = normalCS.xy * positionCS.w - positionCS.xy * normalCS.w;
    float2 screenSize = max(GetScaledScreenParams().xy, float2(1, 1));
    float2 pixelDirection = direction * screenSize;
    pixelDirection *= rsqrt(max(dot(pixelDirection, pixelDirection), 1e-8));

    // 화면 종횡비와 렌더 해상도를 반영해 설정한 픽셀 수만큼 확장한다.
    // 깊이는 원래 메시 기준으로 두어 앞의 표면에 가려지도록 한다.
    float2 offsetNDC = pixelDirection * (2.0 * max(_OutlineWidth, 0.0) / screenSize);
    positionCS.xy += offsetNDC * positionCS.w;
    output.positionCS = positionCS;
    output.fogFactor = ComputeFogFactor(positionCS.z);
    return output;
}

half4 ToonOutlineFragment(ToonOutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1);
}

#endif
