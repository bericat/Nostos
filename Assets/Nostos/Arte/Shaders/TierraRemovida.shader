// Tierra removida: el surco que dejó la nave de Nicias al estrellarse.
// Usa la textura de tierra pintada a mano del paquete "Lowpoly Textures Pack" (Summer_Dirt_A).
// Color de vértice: rgb = color del terreno debajo (en sRGB, de la paleta del valle), a = cuánta
// tierra hay (0 en el borde, donde se funde con la hierba a terrones, 1 en el surco).
// UV1.x = quemado (oscurece la tierra cerca de la nave y en el primer impacto); UV1.y = fondo del
// surco (apisonado y en sombra, algo más oscuro: así se leen los cordones de tierra de los lados).
Shader "Nostos/TierraRemovida" {
    Properties {
        [MainTexture] _BaseMap ("Tierra", 2D) = "white" {}
        [MainColor] _BaseColor ("Tono", Color) = (1,1,1,1)
        _ColorQuemado ("Quemado", Color) = (0.22,0.17,0.14,1)
        _Terrones ("Terrones en el borde", Range(1, 12)) = 5
        _Fondo ("Oscuro del fondo", Range(0, 1)) = 0.38
        _Cesped ("Césped del suelo (Grass_texture_1)", 2D) = "white" {}
        _EscalaCesped ("Repeticiones por metro", Float) = 0.22
        _FuerzaCesped ("Cuánto césped", Range(0, 1)) = 1
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _ColorQuemado;
            float _Terrones;
            float _Fondo;
            float _EscalaCesped;
            float _FuerzaCesped;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        float3 ALineal(float3 c)
        {
            return lerp(c / 12.92, pow((c + 0.055) / 1.055, 2.4), step(0.04045, c));
        }
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
                float2 uv1 : TEXCOORD1;
                float4 color : COLOR;
            };
            struct Salida {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 color : COLOR;
                float2 quemado : TEXCOORD3;
                float niebla : TEXCOORD4;
            };

            Salida vert(Entrada v)
            {
                Salida o;
                o.posWS = TransformObjectToWorld(v.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.quemado = v.uv1;
                o.niebla = ComputeFogFactor(o.posCS.z);
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 tierra = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float brillo = dot(tierra, float3(0.3, 0.55, 0.15));
                float claro = saturate(brillo * 2.2);
                // Quemado: las vetas claras de la tierra aguantan algo más el color.
                tierra = lerp(tierra, _ColorQuemado.rgb * (0.6 + 0.8 * claro), saturate(i.quemado.x) * (0.85 - 0.35 * claro));
                tierra *= 1.0 - _Fondo * saturate(i.quemado.y);
                // En el borde solo quedan los terrones (las partes claras de la textura) sobre la hierba.
                float hayTierra = saturate((claro * 0.9 + i.color.a * 1.6 - 1.0) * _Terrones);
                float3 albedo = lerp(AlbedoSueloN(i.color.rgb, i.posWS.xz, _EscalaCesped, _FuerzaCesped), tierra, hayTierra);

                float3 color = IluminarN(albedo, n, i.posWS, i.posCS);
                color = MixFog(color, i.niebla);
                return half4(color, 1);
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

            struct Entrada {
                float4 posOS : POSITION;
                float3 normalOS : NORMAL;
            };

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
