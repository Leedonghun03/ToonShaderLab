Shader "ToonShaderLab/Basic Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Tint", Color) = (1,1,1,1)
        [ToggleUI] _DarkDetailEnabled("Dark Detail Color Correction", Float) = 0
        _DarkDetailColor("Dark Detail Color", Color) = (0.27,0.18,0.17,1)
        [ToggleUI] _RegionColorEnabled("Region Color Correction", Float) = 0
        [NoScaleOffset] _ColorRegionMask("Color Regions (R / G)", 2D) = "black" {}
        [HDR] _RegionTintR("Region R Tint", Color) = (1,1,1,1)
        [HDR] _RegionTintG("Region G Tint", Color) = (1,1,1,1)
        _RegionSaturationR("Region R Saturation", Range(0,2)) = 1
        _RegionSaturationG("Region G Saturation", Range(0,2)) = 1
        [ToggleUI] _LocalToneEnabled("Local Tone Refinement", Float) = 0
        [NoScaleOffset] _LocalToneMask("Local Tone (R Cheek / G Hair Depth / B Hair)", 2D) = "black" {}
        _CheekTint("Cheek Tint", Color) = (1,0.55,0.68,1)
        _CheekStrength("Cheek Strength", Range(0,1)) = 0
        [HDR] _HairDepthTint("Hair Depth Tint", Color) = (0.4,1.15,1.1,1)
        _HairDepthStrength("Hair Depth Strength", Range(0,1)) = 0
        _LitColor("Lit Tint", Color) = (1,1,1,1)
        _ShadowColor("Shadow Tint", Color) = (0.78,0.70,0.76,1)
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1
        _ShadeThreshold("Shade Threshold", Range(-1,1)) = 0
        _ShadeSoftness("Shade Softness", Range(0.001,0.5)) = 0.05
        [ToggleUI] _OutlineEnabled("Outline Enabled", Float) = 0
        _OutlineWidth("Outline Width (Pixels)", Range(0,5)) = 1
        _OutlineColor("Outline Color", Color) = (0.18,0.12,0.15,1)
        [ToggleUI] _HighlightEnabled("Highlight Enabled", Float) = 0
        [NoScaleOffset] _HighlightMask("Highlight Region (R)", 2D) = "black" {}
        [NoScaleOffset] _MaterialMask("Material Data (G Metallic / B Specular)", 2D) = "black" {}
        [NoScaleOffset] _SurfaceMask("Surface Data (G Smoothness)", 2D) = "black" {}
        _HighlightColor("Highlight Color", Color) = (1,1,1,1)
        _HighlightIntensity("Highlight Intensity", Range(0,8)) = 2
        _HighlightSmoothness("Highlight Smoothness Scale", Range(0,2)) = 1
        [ToggleUI] _EmissionEnabled("Surface Emission Enabled", Float) = 0
        [NoScaleOffset] _EmissionMask("Emission Region (R)", 2D) = "black" {}
        _SurfaceEmissionColor("Emission Tint", Color) = (1,1,1,1)
        _EmissionIntensity("Emission Intensity", Range(0,8)) = 1
        // 표시 모드 번호 0~7은 기존 머티리얼과의 호환성을 위해 유지한다.
        _DisplayMode("Display Mode", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Cull [_Cull]
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "BasicToonForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "BasicToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BasicToonHighlight.hlsl"

            half4 ToonFragment(ToonVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. UV로 기본 색을 읽는다. 원본 UV의 데이터 마스크는 별도로 읽는다.
                half3 baseColor = ReadBaseColor(input.uv, input.maskUV);
                if (_DisplayMode > 0.5 && _DisplayMode < 1.5)
                    return half4(baseColor, 1);

                // 2. 월드 공간의 표면 방향과 주 방향광 방향을 비교한다.
                Light mainLight = GetMainLight();
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float3 lightDirectionWS = SafeNormalize(mainLight.direction);
                half lightBand = EvaluateLightBand(normalWS, lightDirectionWS);
                if (_DisplayMode > 1.5 && _DisplayMode < 2.5)
                    return half4(lightBand.xxx, 1);

                // 눈과 의상을 제외한 전용 마스크로 볼 색과 머리카락 음영 깊이만 조정한다.
                // lightBand 자체와 금속 하이라이트/발광 계산은 변경하지 않는다.
                if (_LocalToneEnabled > 0.5)
                {
                    half3 toneMask = saturate(SAMPLE_TEXTURE2D(_LocalToneMask, sampler_LocalToneMask, input.maskUV).rgb);
                    baseColor *= lerp(1.0h, _CheekTint.rgb, toneMask.r * saturate(_CheekStrength));
                    half hairDepth = max(toneMask.g, toneMask.b * (1.0h - lightBand));
                    baseColor *= lerp(1.0h, _HairDepthTint.rgb, hairDepth * saturate(_HairDepthStrength));
                }

                half3 highlight = 0;
                if (_HighlightEnabled > 0.5 || (_DisplayMode > 2.5 && _DisplayMode < 7.5))
                {
                    ToonHighlightData data = ReadHighlightData(input.maskUV);
                    if (_DisplayMode > 2.5 && _DisplayMode < 3.5)
                        return half4(data.region.xxx, 1);
                    if (_DisplayMode > 3.5 && _DisplayMode < 4.5)
                        return half4(data.metallic.xxx, 1);
                    if (_DisplayMode > 4.5 && _DisplayMode < 5.5)
                        return half4(data.specularMask.xxx, 1);
                    if (_DisplayMode > 5.5 && _DisplayMode < 6.5)
                        return half4(data.smoothness.xxx, 1);
                    float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    highlight = EvaluateToonHighlight(data, normalWS, lightDirectionWS, viewDirectionWS, lightBand)
                        * mainLight.color * mainLight.distanceAttenuation;
                }
                if (_DisplayMode > 6.5 && _DisplayMode < 7.5)
                    return half4(highlight, 1);

                // 원본 UV0의 전용 마스크와 기본색으로 표면 발광을 계산한다.
                // 조명·금속성·툰 명암과 독립적인 가산 항이며 주변에 빛을 투사하지 않는다.
                half3 emission = 0;
                if (_EmissionEnabled > 0.5 || _DisplayMode > 7.5)
                {
                    half region = saturate(SAMPLE_TEXTURE2D(_EmissionMask, sampler_EmissionMask, input.maskUV).r);
                    if (_DisplayMode > 7.5 && _DisplayMode < 8.5)
                        return half4(region.xxx, 1);
                    if (_EmissionEnabled > 0.5)
                    {
                        half3 emissionBase = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.maskUV).rgb * _BaseColor.rgb;
                        emission = emissionBase * _SurfaceEmissionColor.rgb * (region * max(_EmissionIntensity, 0.0));
                    }
                }
                if (_DisplayMode > 8.5)
                    return half4(emission, 1);

                // 3. 경계 위치는 유지하고 머티리얼별로 그림자색의 반영량을 조절한다.
                // 강도 0은 밝은 영역 색, 강도 1은 기존 툰 명암과 같다.
                // 실시간 그림자 수신, 추가 광원, 환경 반사, GI는 아직 계산하지 않는다.
                half shadowAmount = (1.0h - lightBand) * saturate(_ShadowStrength);
                half3 toonTint = lerp(_LitColor.rgb, _ShadowColor.rgb, shadowAmount);
                half3 color = baseColor * toonTint * mainLight.color * mainLight.distanceAttenuation;
                color += highlight;
                color += emission;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Render Objects가 ToonOutline 태그를 선택하여 불투명 표면 다음에 그린다.
        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode"="ToonOutline" }
            Cull Front
            // 뒤에 그리는 Skybox가 외곽선을 덮지 않도록 깊이도 기록한다.
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonOutlineVertex
            #pragma fragment ToonOutlineFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "BasicToonOutline.hlsl"
            ENDHLSL
        }

        // URP가 깊이와 표면 방향을 요청할 때도 같은 메시가 기록되도록 한다.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonDepthFragment
            #pragma multi_compile_instancing
            #include "BasicToonInput.hlsl"
            half ToonDepthFragment(ToonVaryings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ToonVertex
            #pragma fragment ToonNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "BasicToonInput.hlsl"
            half4 ToonNormalsFragment(ToonVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
        // 바닥 등 다른 물체에 드리우는 그림자는 URP의 표준 패스를 사용한다.
        // 캐릭터가 그림자를 받는 계산과는 별개다. 이 셰이더는 현재 불투명 표면 전용이다.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "BasicToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "ToonShaderLab.Editor.BasicToonShaderGUI"
    FallBack Off
}
