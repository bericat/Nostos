// Versión URP (Universal Render Pipeline) del shader "Mobile_Vtx_Cutout" del paquete "Pine Forest - LowPoly".
// El original era de Shader Forge para el render antiguo y en URP salía rosa. Este hace lo mismo:
// color base con un poco de ruido por UV, multiplicado por el color de vértice del modelo, recorte
// por el alfa del vértice con tramado 3x3 (bordes de la hierba y la tierra).
// Mismas propiedades, así los materiales del paquete no cambian. Lo escribió el proyecto Nostos.
Shader "Shader Forge/Mobile_Vtx_Cutout" {
    Properties {
        _ColorBase ("ColorBase", Color) = (0.6470588,0.6470588,0.6470588,1)
        _Noise_Color ("Noise_Color", Color) = (0.9338235,0.9338235,0.9338235,1)
        _Base_Noise_Amnt ("Base_Noise_Amnt", Float ) = 0
        _Color_Noise_Steps ("Color_Noise_Steps", Float ) = 50
        _Opacity_Noise_Steps ("Opacity_Noise_Steps", Float ) = 50
        _Opacity_NoiseAmnt ("Opacity_NoiseAmnt", Float ) = 0.5
        _Emissiv_Amnt ("Emissiv_Amnt", Float ) = 0
        _Vtx_Color_Amnt ("Vtx_Color_Amnt", Float ) = 1
        _Vtx_Opacity ("Vtx_Opacity", Float ) = 1
        _All_Opacity ("All_Opacity", Float ) = 1
        _NoiseAmnt ("NoiseAmnt", Float ) = 0.4
        _NoiseSpeed ("NoiseSpeed", Float ) = 2
        _WindNoiseFrequency ("WindNoiseFrequency", Float ) = 50
        _Spec_Color ("Spec_Color", Color) = (0.5,0.5,0.5,1)
        _GlossAmnt ("GlossAmnt", Range(0.1, 1)) = 0.1
        [HideInInspector]_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        

        CBUFFER_START(UnityPerMaterial)
            float4 _ColorBase;
            float4 _Noise_Color;
            float _Base_Noise_Amnt;
            float _Color_Noise_Steps;
            float _Opacity_Noise_Steps;
            float _Opacity_NoiseAmnt;
            float _Emissiv_Amnt;
            float _Vtx_Color_Amnt;
            float _Vtx_Opacity;
            float _All_Opacity;
            float _NoiseAmnt;
            float _NoiseSpeed;
            float _WindNoiseFrequency;
            float4 _Spec_Color;
            float _GlossAmnt;
            float _Cutoff;
        CBUFFER_END

        // El "Noise" de Shader Forge sobre las UV posterizadas.
        float RuidoSF(float2 uv, float pasos)
        {
            float2 p = floor(uv * pasos) / (pasos - 1);
            float2 skew = p + 0.2127 + p.x * 0.3713 * p.y;
            float2 rnd = 4.789 * sin(489.123 * skew);
            return frac(rnd.x * rnd.y * (1 + skew.x));
        }

        float3 Viento(float3 posOS, float3 normalOS, float2 uv, float alfa)
        {
            #ifdef PINO_VIENTO
                float n = RuidoSF(uv, _WindNoiseFrequency);
                posOS += sin(n * (_Time.y * _NoiseSpeed)) * normalOS * _NoiseAmnt * (1.0 - alfa);
            #endif
            return posOS;
        }

        // clip(BinaryDither3x3(...)) del original, con la misma matriz de Bayer.
        void Recortar(float2 uv, float alfa, float4 posCS)
        {
            float n = RuidoSF(uv, _Opacity_Noise_Steps);
            float valor = lerp(alfa * _Vtx_Opacity * n * _Opacity_NoiseAmnt, _All_Opacity, alfa) - 1.5;
            int x = (int)fmod(floor(posCS.x), 3.0);
            int y = (int)fmod(floor(posCS.y), 3.0);
            float3x3 m = float3x3(float3(3, 7, 4) / 10.0, float3(6, 1, 9) / 10.0, float3(2, 8, 5) / 10.0);
            clip(round(valor + m[x][y]));
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

            struct Entrada {
                float4 posOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Salida {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 color : COLOR;
                float niebla : TEXCOORD3;
            };

            Salida vert(Entrada v)
            {
                Salida o;
                float3 pos = Viento(v.posOS.xyz, v.normalOS, v.uv, v.color.a);
                o.posWS = TransformObjectToWorld(pos);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.color = v.color;
                o.niebla = ComputeFogFactor(o.posCS.z);
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                Recortar(i.uv, i.color.a, i.posCS);
                float3 n = normalize(i.normalWS);
                float ruido = RuidoSF(i.uv, _Color_Noise_Steps);
                float3 baseColor = ruido * _Noise_Color.rgb * _Base_Noise_Amnt + _ColorBase.rgb;
                float3 albedo = baseColor * i.color.rgb * _Vtx_Color_Amnt;

                Light sol = GetMainLight(TransformWorldToShadowCoord(i.posWS));
                float3 luz = saturate(dot(n, sol.direction)) * sol.color * sol.distanceAttenuation * sol.shadowAttenuation;
                float3 brillo = 0;
                #ifdef PINO_VIENTO
                    float3 vista = normalize(GetWorldSpaceViewDir(i.posWS));
                    float3 medio = normalize(sol.direction + vista);
                    brillo = pow(saturate(dot(medio, n)), exp2(_GlossAmnt * 10.0 + 1.0)) * _Spec_Color.rgb * sol.color * sol.shadowAttenuation;
                #endif

                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.posWS;
                    inputData.normalWS = n;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.posCS);
                    uint cuantas = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(cuantas)
                        Light otra = GetAdditionalLight(lightIndex, i.posWS, half4(1, 1, 1, 1));
                        luz += saturate(dot(n, otra.direction)) * otra.color * otra.distanceAttenuation * otra.shadowAttenuation;
                    LIGHT_LOOP_END
                #endif

                float3 color = (luz + SampleSH(n)) * albedo + brillo + baseColor * i.color.rgb * _Emissiv_Amnt;
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
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Salida {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Salida vert(Entrada v)
            {
                Salida o;
                float3 pos = Viento(v.posOS.xyz, v.normalOS, v.uv, v.color.a);
                float3 posWS = TransformObjectToWorld(pos);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 dirLuz = normalize(_LightPosition - posWS);
                #else
                    float3 dirLuz = _LightDirection;
                #endif
                o.posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, dirLuz));
                #if UNITY_REVERSED_Z
                    o.posCS.z = min(o.posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.posCS.z = max(o.posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                Recortar(i.uv, i.color.a, i.posCS);
                return 0;
            }
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

            struct Entrada {
                float4 posOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Salida {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Salida vert(Entrada v)
            {
                Salida o;
                float3 pos = Viento(v.posOS.xyz, v.normalOS, v.uv, v.color.a);
                o.posCS = TransformObjectToHClip(pos);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                Recortar(i.uv, i.color.a, i.posCS);
                return 0;
            }
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

            struct Entrada {
                float4 posOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Salida {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
            };

            Salida vert(Entrada v)
            {
                Salida o;
                float3 pos = Viento(v.posOS.xyz, v.normalOS, v.uv, v.color.a);
                o.posCS = TransformObjectToHClip(pos);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                Recortar(i.uv, i.color.a, i.posCS);
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
