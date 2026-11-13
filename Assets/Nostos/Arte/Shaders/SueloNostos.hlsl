// Funciones comunes del suelo del valle (SueloValle, TierraRemovida, HierbaViento).
// El terreno trae un color de paleta por cara; donde ese color es de hierba se pinta con el
// césped del paquete "A piece of nature" (Grass_texture_1), tal cual, y el tono de la paleta
// solo lo matiza (hierba seca más amarilla, hierba oscura más oscura).
#ifndef SUELO_NOSTOS_INCLUDED
#define SUELO_NOSTOS_INCLUDED

TEXTURE2D(_Cesped);
SAMPLER(sampler_Cesped);

float3 ALinealN(float3 c)
{
    return lerp(c / 12.92, pow((c + 0.055) / 1.055, 2.4), step(0.04045, c));
}

float3 ASRGBN(float3 c)
{
    return lerp(c * 12.92, 1.055 * pow(max(c, 1e-6), 1.0 / 2.4) - 0.055, step(0.0031308, c));
}

float ParecidoN(float3 c, float3 r)
{
    return saturate(1.0 - length(c - r) * 10.0);
}

/// Cuánto de hierba es un color de la paleta (en sRGB, 0..1).
float EsHierbaN(float3 paleta)
{
    float h = ParecidoN(paleta, float3(150, 160, 74) / 255.0);          // hierba
    h = max(h, ParecidoN(paleta, float3(184, 172, 92) / 255.0));        // hierba_seca
    h = max(h, ParecidoN(paleta, float3(106, 132, 66) / 255.0));        // hierba_oscura
    h = max(h, ParecidoN(paleta, float3(120, 150, 72) / 255.0));        // hoja
    h = max(h, ParecidoN(paleta, float3(150, 170, 84) / 255.0));        // hoja_clara
    return h;
}

/// Césped del paquete en el punto xz del mundo, con una segunda muestra girada para que no se
/// note la repetición.
float3 CespedN(float2 xz, float escala)
{
    float3 a = SAMPLE_TEXTURE2D(_Cesped, sampler_Cesped, xz * escala).rgb;
    float2 girado = float2(xz.x * 0.8 - xz.y * 0.6, xz.x * 0.6 + xz.y * 0.8);
    float3 b = SAMPLE_TEXTURE2D(_Cesped, sampler_Cesped, girado * escala * 0.37 + 0.31).rgb;
    return lerp(a, b, 0.35);
}

/// Albedo lineal del suelo: la paleta, o el césped donde la paleta es hierba.
float3 AlbedoSueloN(float3 paleta, float2 xz, float escala, float fuerza)
{
    float3 lin = ALinealN(paleta);
    float3 tinte = lin / ALinealN(float3(150, 160, 74) / 255.0);
    return lerp(lin, CespedN(xz, escala) * tinte, EsHierbaN(paleta) * fuerza);
}

/// Luz difusa como la del terreno (URP Lit no metálico y mate): sol con sombras, luces
/// adicionales y ambiente.
float3 IluminarN(float3 albedo, float3 n, float3 posWS, float4 posCS)
{
    Light sol = GetMainLight(TransformWorldToShadowCoord(posWS));
    float3 luz = saturate(dot(n, sol.direction)) * sol.color * sol.distanceAttenuation * sol.shadowAttenuation;
    #if defined(_ADDITIONAL_LIGHTS)
        InputData inputData = (InputData)0;
        inputData.positionWS = posWS;
        inputData.normalWS = n;
        inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(posCS);
        uint cuantas = GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(cuantas)
            Light otra = GetAdditionalLight(lightIndex, posWS, half4(1, 1, 1, 1));
            luz += saturate(dot(n, otra.direction)) * otra.color * otra.distanceAttenuation * otra.shadowAttenuation;
        LIGHT_LOOP_END
    #endif
    return (luz + SampleSH(n)) * albedo * 0.96;
}

#endif
