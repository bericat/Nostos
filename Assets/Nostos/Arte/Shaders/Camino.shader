// Caminos que traza el jugador: el empedrado de la plaza (Summer_Rocks del "Lowpoly Textures
// Pack"), con el borde roto y la tierra del borde con el barro de CliffStonePackM. Cada vértice lleva en uv.x cuánto está dentro del camino (metros:
// positivo dentro, negativo fuera); con ruido en el mundo, el borde se come la hierba en unos
// sitios y la hierba se mete en el camino en otros. Junto al borde las piedras se hunden en
// tierra (Summer_Dirt_A) y la tierra se pierde poco a poco en el césped, que se pinta igual que el
// suelo del valle (uv1 trae el color de paleta de la cara del suelo que hay debajo): al final la
// malla del camino es indistinguible del suelo y el corte no se ve.
Shader "Nostos/Camino" {
    Properties {
        [MainTexture] _BaseMap ("Empedrado (Summer_Rocks)", 2D) = "white" {}
        _Tierra ("Tierra del borde (Summer_Dirt_A)", 2D) = "white" {}
        _MetrosPiedra ("Metros por dibujo del empedrado", Float) = 2.5
        _MetrosTierra ("Metros por dibujo de la tierra", Float) = 4
        _Mordisco ("Cuánto entra y sale el borde (m)", Range(0, 1)) = 0.5
        _AnchoTierra ("Franja de tierra junto al borde (m)", Range(0, 1)) = 0.35
        _FundidoHierba ("Fundido de la tierra en la hierba (m)", Range(0.1, 1.2)) = 0.6
        _Paleta ("Paleta del suelo", 2D) = "white" {}
        _Cesped ("Hierba del suelo (CSPM_Grass)", 2D) = "white" {}
        [Normal] _CespedN ("Hierba, normal", 2D) = "bump" {}
        _Barro ("Barro (CSPM_Mud)", 2D) = "white" {}
        [Normal] _BarroN ("Barro, normal", 2D) = "bump" {}
        _BarroHierba ("Barro con hierba (CSPM_GrassyMud)", 2D) = "white" {}
        [Normal] _BarroHierbaN ("Barro con hierba, normal", 2D) = "bump" {}
        [NoScaleOffset] _MapaSuelo ("Mapa del suelo", 2D) = "black" {}
        _EscalaCesped ("Repeticiones del césped por metro", Float) = 0.22
        _FuerzaCesped ("Cuánto césped", Range(0, 1)) = 1
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _Tierra_ST;
            float _MetrosPiedra;
            float _MetrosTierra;
            float _Mordisco;
            float _AnchoTierra;
            float _FundidoHierba;
            float4 _Paleta_ST;
            float _EscalaCesped;
            float _FuerzaCesped;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_Tierra);
        SAMPLER(sampler_Tierra);

        float AzarC(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float RuidoC(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            float a = AzarC(i), b = AzarC(i + float2(1, 0)), c = AzarC(i + float2(0, 1)), d = AzarC(i + float2(1, 1));
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        /// Cuánto dentro del camino está el punto, ya con el borde roto (m; < 0 es hierba).
        float BordeC(float dentro, float2 xz)
        {
            float n = RuidoC(xz * 0.9) * 0.6 + RuidoC(xz * 2.3 + 17.0) * 0.3 + RuidoC(xz * 6.1 + 41.0) * 0.1;
            return dentro + (n - 0.5) * 2.0 * _Mordisco;
        }

        /// Más allá de esto (hacia fuera) la malla ya es puro suelo y se puede cortar sin que se note.
        float CorteC() { return -(_FundidoHierba + 0.3); }
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

            TEXTURE2D(_Paleta);
            SAMPLER(sampler_Paleta);

            struct Entrada {
                float4 posOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uvPaleta : TEXCOORD1;
            };
            struct Salida {
                float4 posCS : SV_POSITION;
                float dentro : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float niebla : TEXCOORD3;
                float2 uvPaleta : TEXCOORD4;
            };

            Salida vert(Entrada v)
            {
                Salida o;
                o.posWS = TransformObjectToWorld(v.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.dentro = v.uv.x;
                o.uvPaleta = v.uvPaleta;
                o.niebla = ComputeFogFactor(o.posCS.z);
                return o;
            }

            half4 frag(Salida i) : SV_Target
            {
                float2 xz = i.posWS.xz;
                float borde = BordeC(i.dentro, xz);
                clip(borde - CorteC());

                float3 piedra = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, xz / _MetrosPiedra).rgb;
                float2 girado = float2(xz.x * 0.8 - xz.y * 0.6, xz.x * 0.6 + xz.y * 0.8);
                float3 tierra = SAMPLE_TEXTURE2D(_Tierra, sampler_Tierra, girado / _MetrosTierra).rgb;
                // El suelo del valle tal cual: paleta de la cara de debajo y césped donde es hierba.
                float3 paleta = ASRGBN(SAMPLE_TEXTURE2D(_Paleta, sampler_Paleta, i.uvPaleta).rgb);
                float3 detalle;
                float3 hierba = SueloN(paleta, xz, detalle);

                // Dentro: piedras. Hacia el borde se hunden en tierra a trozos (las sueltas primero).
                float suelta = RuidoC(xz * 3.7 + 7.0);
                float aTierra = 1.0 - smoothstep(0.0, _AnchoTierra, borde - (suelta - 0.5) * 0.3);
                aTierra = max(aTierra, smoothstep(0.78, 0.92, RuidoC(xz * 0.35 + 3.0)) * 0.55);
                float3 albedo = lerp(piedra, tierra, saturate(aTierra));
                // Fuera: la tierra se pierde en el césped, con matas que entran y calvas que salen.
                float matas = RuidoC(xz * 2.9 + 23.0) * 0.65 + RuidoC(xz * 7.3 + 5.0) * 0.35;
                float aHierba = smoothstep(0.05, -_FundidoHierba, borde + (matas - 0.5) * 0.45);
                albedo = lerp(albedo, hierba, saturate(aHierba));

                float3 cara = normalize(i.normalWS);
                float3 n = normalize(lerp(cara, NormalSueloN(cara, detalle), saturate(aHierba)));
                float3 color = IluminarN(albedo, n, i.posWS, i.posCS);
                return half4(MixFog(color, i.niebla), 1);
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
            struct Entrada { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct Salida { float4 posCS : SV_POSITION; float dentro : TEXCOORD0; float3 posWS : TEXCOORD1; };
            Salida vert(Entrada v)
            {
                Salida o;
                o.posWS = TransformObjectToWorld(v.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.dentro = v.uv.x;
                return o;
            }
            half4 frag(Salida i) : SV_Target
            {
                clip(BordeC(i.dentro, i.posWS.xz) - CorteC());
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
            struct Entrada { float4 posOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Salida { float4 posCS : SV_POSITION; float3 normalWS : TEXCOORD0; float dentro : TEXCOORD1; float3 posWS : TEXCOORD2; };
            Salida vert(Entrada v)
            {
                Salida o;
                o.posWS = TransformObjectToWorld(v.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.dentro = v.uv.x;
                return o;
            }
            half4 frag(Salida i) : SV_Target
            {
                clip(BordeC(i.dentro, i.posWS.xz) - CorteC());
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
