// Matas de hierba del valle con la textura del paquete "A piece of nature" (Grass.tif, una mata
// de briznas con alfa). Se pintan a miles con GPU instancing (HierbaDelValle.cs): se mecen con el
// viento, se encogen al alejarse (sin saltos) y se iluminan como el suelo (normal hacia arriba),
// para que no salgan caras oscuras por detrás.
Shader "Nostos/HierbaViento" {
    Properties {
        [MainTexture] _BaseMap ("Mata (Grass.tif)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tono", Color) = (1,1,1,1)
        _Cutoff ("Recorte", Range(0, 1)) = 0.5
        _Viento ("Viento (m)", Float) = 0.08
        _VelocidadViento ("Velocidad del viento", Float) = 1.6
        _Variacion ("Variación de tono entre matas", Range(0, 0.5)) = 0.18
        _Desvanecer ("Empieza a encoger (m)", Float) = 38
        _Distancia ("Desaparece (m)", Float) = 55
        _Sombra ("Oscuro en la base", Range(0, 1)) = 0.45
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float _Viento;
            float _VelocidadViento;
            float _Variacion;
            float _Desvanecer;
            float _Distancia;
            float _Sombra;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        // Lo pone HierbaDelValle: la cámara a pie, o el punto del suelo que mira la cámara de constructor.
        float4 _CentroHierba;

        float AzarH(float2 p)
        {
            return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        // Posición en el mundo de un vértice de la mata: viento y encogido con la distancia.
        float3 MoverMata(float3 posOS, float alto, out float azar)
        {
            float3 base = TransformObjectToWorld(float3(0, 0, 0));
            float3 p = TransformObjectToWorld(posOS);
            azar = AzarH(base.xz);
            float fase = _Time.y * _VelocidadViento + base.x * 0.35 + base.z * 0.27 + azar * 6.28;
            float rafaga = 0.6 + 0.4 * sin(_Time.y * 0.37 + base.x * 0.05);
            float2 empuje = float2(sin(fase), sin(fase * 0.73 + 1.7)) * _Viento * rafaga;
            p.xz += empuje * alto * alto;
            float lejos = distance(base, _CentroHierba.xyz);
            float queda = 1.0 - smoothstep(_Desvanecer, _Distancia, lejos);
            return base + (p - base) * queda;
        }
        ENDHLSL

        Pass {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
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
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Salida {
                float4 posCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float azar : TEXCOORD2;
                float niebla : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Salida vert(Entrada v)
            {
                Salida o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float azar;
                o.posWS = MoverMata(v.posOS.xyz, v.uv.y, azar);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.azar = azar;
                o.niebla = ComputeFogFactor(o.posCS.z);
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 mata = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(mata.a - _Cutoff);
                float3 albedo = mata.rgb * _BaseColor.rgb;
                albedo *= 1.0 + (i.azar - 0.5) * 2.0 * _Variacion;
                albedo *= lerp(1.0 - _Sombra, 1.0, saturate(i.uv.y * 1.4));
                float3 color = IluminarN(albedo, float3(0, 1, 0), i.posWS, i.posCS);
                return half4(MixFog(color, i.niebla), 1);
            }
            ENDHLSL
        }

        Pass {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Entrada { float4 posOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Salida vert(Entrada v)
            {
                Salida o;
                UNITY_SETUP_INSTANCE_ID(v);
                float azar;
                o.posCS = TransformWorldToHClip(MoverMata(v.posOS.xyz, v.uv.y, azar));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 frag(Salida i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Entrada { float4 posOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Salida { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Salida vert(Entrada v)
            {
                Salida o;
                UNITY_SETUP_INSTANCE_ID(v);
                float azar;
                o.posCS = TransformWorldToHClip(MoverMata(v.posOS.xyz, v.uv.y, azar));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 frag(Salida i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return half4(0, 1, 0, 0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
