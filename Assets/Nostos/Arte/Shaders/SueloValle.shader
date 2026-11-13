// El suelo del valle: las capas de terreno de CliffStonePackM (hierba, barro con hierba, barro)
// donde no es roca, con los caminos y orillas sacados de _MapaSuelo; la roca, con su color de
// paleta por cara. Lo usa el objeto "Suelo" de valle.fbx.
Shader "Nostos/SueloValle" {
    Properties {
        [MainTexture] _BaseMap ("Paleta", 2D) = "white" {}
        _Cesped ("Hierba (CSPM_Grass)", 2D) = "white" {}
        [Normal] _CespedN ("Hierba, normal", 2D) = "bump" {}
        _Barro ("Barro (CSPM_Mud)", 2D) = "white" {}
        [Normal] _BarroN ("Barro, normal", 2D) = "bump" {}
        _BarroHierba ("Barro con hierba (CSPM_GrassyMud)", 2D) = "white" {}
        [Normal] _BarroHierbaN ("Barro con hierba, normal", 2D) = "bump" {}
        [NoScaleOffset] _MapaSuelo ("Mapa del suelo (R tierra, G roca)", 2D) = "black" {}
        _EscalaCesped ("Repeticiones por metro", Float) = 0.22
        _FuerzaCesped ("Cuánto césped", Range(0, 1)) = 1
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _Cesped_ST;
            float _EscalaCesped;
            float _FuerzaCesped;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SueloNostos.hlsl"

            struct Entrada {
                float4 posOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Salida {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float niebla : TEXCOORD3;
            };

            Salida vert(Entrada v)
            {
                Salida o;
                o.posWS = TransformObjectToWorld(v.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.niebla = ComputeFogFactor(o.posCS.z);
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                // La paleta es sRGB: se lee en lineal y se vuelve a sRGB para comparar colores.
                float3 paleta = ASRGBN(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb);
                float3 detalle;
                float3 albedo = SueloN(paleta, i.posWS.xz, detalle);
                float3 n = NormalSueloN(normalize(i.normalWS), detalle);
                float3 color = IluminarN(albedo, n, i.posWS, i.posCS);
                return half4(MixFog(color, i.niebla), 1);
            }
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Entrada { float4 posOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert(Entrada v) : SV_POSITION
            {
                float3 posWS = TransformObjectToWorld(v.posOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 dirLuz = normalize(_LightPosition - posWS);
                #else
                    float3 dirLuz = _LightDirection;
                #endif
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, dirLuz));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return posCS;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 posOS : POSITION) : SV_POSITION { return TransformObjectToHClip(posOS.xyz); }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Entrada { float4 posOS : POSITION; float3 normalOS : NORMAL; };
            struct Salida { float4 posCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Salida vert(Entrada v)
            {
                Salida o;
                o.posCS = TransformObjectToHClip(v.posOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Salida i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
