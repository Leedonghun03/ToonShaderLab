#ifndef TOON_SHADER_LAB_HIGHLIGHT_INCLUDED
#define TOON_SHADER_LAB_HIGHLIGHT_INCLUDED

// 패킹된 데이터 텍스처는 sRGB를 끄고 읽는다. A.B는 이 하이라이트에 사용하지 않는다.
struct ToonHighlightData
{
    half region;
    half metallic;
    half specularMask;
    half smoothness;
};

ToonHighlightData ReadHighlightData(float2 uv)
{
    ToonHighlightData data;
    half4 material = SAMPLE_TEXTURE2D(_MaterialMask, sampler_MaterialMask, uv);
    half4 surface = SAMPLE_TEXTURE2D(_SurfaceMask, sampler_SurfaceMask, uv);
    data.region = saturate(SAMPLE_TEXTURE2D(_HighlightMask, sampler_HighlightMask, uv).r);
    data.metallic = saturate(material.g);
    data.specularMask = saturate(material.b);
    data.smoothness = saturate(surface.g);
    return data;
}

half3 EvaluateToonHighlight(ToonHighlightData data, float3 normalWS,
    float3 lightDirectionWS, float3 viewDirectionWS, half lightBand)
{
    if (_HighlightEnabled < 0.5)
        return 0;

    // 1차 구현은 선택한 금속 부위의 주광원 Blinn-Phong 하이라이트다.
    // 밝은 툰 영역에만 더하므로 기존 명암 경계/그림자색 계산은 바꾸지 않는다.
    float3 halfDirectionWS = SafeNormalize(lightDirectionWS + viewDirectionWS);
    float smoothness = saturate(data.smoothness * _HighlightSmoothness);
    float exponent = exp2(3.0 + 7.0 * smoothness);
    float lobe = pow(saturate(dot(normalWS, halfDirectionWS)), exponent);
    float litVisibility = saturate(dot(normalWS, lightDirectionWS)) * lightBand;
    float mask = data.region * data.metallic * data.specularMask;
    return _HighlightColor.rgb * (lobe * litVisibility * mask * max(_HighlightIntensity, 0.0));
}
#endif
