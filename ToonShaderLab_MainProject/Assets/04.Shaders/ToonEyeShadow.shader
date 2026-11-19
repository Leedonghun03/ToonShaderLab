Shader "ToonShaderLab/Eye Layer Overlay"
{
    Properties
    {
        [MainTexture] _BaseMap("Eye Shadow Texture", 2D) = "white" {}
        [NoScaleOffset] _EyeColorMap("Eye Palette (16 x 16)", 2D) = "white" {}
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1
        [HideInInspector] _ReflectionLayer("Reflection Layer", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 2
        [HideInInspector] _DstBlend("Destination Blend", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "EyeShadowOverlay"
            Tags { "LightMode"="UniversalForwardOnly" }
            // RGB는 아래에 이미 그려진 눈 색에 곱할 필터다. 깊이는 읽고 쓰지 않는다.
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EyeColorMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _ShadowStrength;
                float _ReflectionLayer;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 palette : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                // 이 모델의 R 바이트는 16x16 팔레트 인덱스다. 정점에서 읽고 경계에서 보간한다.
                uint index=(uint)round(saturate(input.color.r)*255.0h);
                int2 location=int2(index&15u,15u-(index>>4u));
                output.palette=LOAD_TEXTURE2D(_EyeColorMap,location);
                output.fogFactor=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 texel=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv);
                half3 tint=texel.rgb*input.palette.rgb*2.0h;
                if (_ReflectionLayer > 0.5h)
                    return half4(MixFog(tint,input.fogFactor),saturate(texel.a*input.palette.a));
                // DstColor / OneMinusSrcAlpha 그림자 합성을 밝아지지 않는 곱셈 계수로 변환한다.
                half3 filter=saturate(1.0h-texel.a*input.palette.a+tint);
                filter=lerp(1.0h,filter,saturate(_ShadowStrength));
                // 안개가 짙을 때 배경의 안개색까지 어두워지지 않도록 한다.
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    filter=lerp(1.0h,filter,ComputeFogIntensity(input.fogFactor));
                #endif
                return half4(filter,1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
