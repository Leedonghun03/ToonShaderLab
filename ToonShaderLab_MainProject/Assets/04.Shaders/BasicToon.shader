Shader "ToonShaderLab/Basic Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Tint", Color) = (1,1,1,1)
        _LitColor("Lit Tint", Color) = (1,1,1,1)
        _ShadowColor("Shadow Tint", Color) = (0.78,0.70,0.76,1)
        _ShadeThreshold("Shade Threshold", Range(-1,1)) = 0
        _ShadeSoftness("Shade Softness", Range(0.001,0.5)) = 0.05
        [Enum(Composite,0,Texture Only,1,Shading Only,2)] _DisplayMode("Display Mode", Float) = 0
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

            half4 ToonFragment(ToonVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. UV로 기본 색을 읽는다. 데이터 마스크는 다음 단계에서 별도로 추가한다.
                half3 baseColor = ReadBaseColor(input.uv);
                if (_DisplayMode > 0.5 && _DisplayMode < 1.5)
                    return half4(baseColor, 1);

                // 2. 월드 공간의 표면 방향과 주 방향광 방향을 비교한다.
                Light mainLight = GetMainLight();
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float3 lightDirectionWS = SafeNormalize(mainLight.direction);
                half lightBand = EvaluateLightBand(normalWS, lightDirectionWS);
                if (_DisplayMode > 1.5)
                    return half4(lightBand.xxx, 1);

                // 3. 어두운 색과 밝은 색을 섞어 텍스처에 곱한다.
                // 실시간 그림자 수신, 추가 광원, 반사, GI는 아직 계산하지 않는다.
                half3 toonTint = lerp(_ShadowColor.rgb, _LitColor.rgb, lightBand);
                half3 color = baseColor * toonTint * mainLight.color * mainLight.distanceAttenuation;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
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
