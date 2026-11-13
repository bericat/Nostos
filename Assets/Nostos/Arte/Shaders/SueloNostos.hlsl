// Funciones comunes del suelo del valle (SueloValle, TierraRemovida, Camino, HierbaViento).
// El terreno trae un color de paleta por cara. Lo que no es roca se pinta con las capas de terreno
// de CliffStonePackM (la referencia de la biblia artística): hierba, barro con hierba y barro.
// Dónde va el barro (caminos y orillas del valle) lo dice _MapaSuelo, un mapa visto desde arriba
// hecho con esas caras y difuminado, con ruido: el borde no sigue los triángulos.
#ifndef SUELO_NOSTOS_INCLUDED
#define SUELO_NOSTOS_INCLUDED

TEXTURE2D(_Cesped);
SAMPLER(sampler_Cesped);
TEXTURE2D(_CespedN);
TEXTURE2D(_Barro);
TEXTURE2D(_BarroN);
TEXTURE2D(_BarroHierba);
TEXTURE2D(_BarroHierbaN);
TEXTURE2D(_MapaSuelo);
SAMPLER(sampler_MapaSuelo);

// Metros por repetición de cada capa y lado del mapa (el valle va de -200 a 200).
#define TAM_HIERBA 3.2
#define TAM_BARRO 2.8
#define TAM_BARRO_HIERBA 3.4
#define LADO_MAPA_SUELO 400.0

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

float AzarN(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float RuidoN(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = AzarN(i), b = AzarN(i + float2(1, 0)), c = AzarN(i + float2(0, 1)), d = AzarN(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

/// Cuánto de tierra (camino, orilla) es un color de la paleta (sRGB).
float EsTierraN(float3 paleta)
{
    return max(ParecidoN(paleta, float3(200, 168, 114) / 255.0), ParecidoN(paleta, float3(132, 104, 78) / 255.0));
}

/// Tierra en el punto (0 hierba, 1 barro), con el borde roto por ruido (igual que TierraEn en C#).
float TierraEnN(float2 xz)
{
    float m = SAMPLE_TEXTURE2D_LOD(_MapaSuelo, sampler_MapaSuelo, xz / LADO_MAPA_SUELO + 0.5, 0).r;
    float n = RuidoN(xz * 0.55) * 0.6 + RuidoN(xz * 1.7 + 13.0) * 0.3 + RuidoN(xz * 4.3 + 31.0) * 0.1;
    return m + (n - 0.5) * 0.45;
}

/// Dos muestras (una girada y a otra escala) mezcladas con ruido, para que no se vea la repetición.
/// Devuelve el color y deja la normal (en los ejes x/z del mundo) en `normal`.
float3 CapaN(TEXTURE2D_PARAM(tex, smp), TEXTURE2D_PARAM(texN, smpN), float2 xz, float tam, float mezcla, out float3 normal)
{
    float2 uvA = xz / tam;
    float2 girado = float2(xz.x * 0.8 - xz.y * 0.6, xz.x * 0.6 + xz.y * 0.8);
    float2 uvB = girado / (tam * 1.37) + 0.37;
    float3 a = SAMPLE_TEXTURE2D(tex, smp, uvA).rgb;
    float3 b = SAMPLE_TEXTURE2D(tex, smp, uvB).rgb;
    float3 na = UnpackNormal(SAMPLE_TEXTURE2D(texN, smpN, uvA));
    float3 nb = UnpackNormal(SAMPLE_TEXTURE2D(texN, smpN, uvB));
    // La muestra girada trae la normal en sus ejes: se devuelve a los del mundo.
    nb.xy = float2(nb.x * 0.8 + nb.y * 0.6, -nb.x * 0.6 + nb.y * 0.8);
    normal = normalize(lerp(na, nb, mezcla));
    return lerp(a, b, mezcla);
}

/// Verde natural: menos saturado y menos amarillo (lineal).
float3 TonoHierbaN(float3 c)
{
    float luz = dot(c, float3(0.2126, 0.7152, 0.0722));
    return lerp(luz.xxx, c, 0.75) * float3(0.55, 0.68, 0.55);
}

/// El suelo en un punto: albedo lineal y normal de detalle (x y z del mundo en .xy, arriba en .z).
/// Las caras de roca siguen con su color de paleta.
float3 SueloN(float3 paleta, float2 xz, out float3 normalDetalle)
{
    float suelo = saturate(EsHierbaN(paleta) + EsTierraN(paleta));
    float t = TierraEnN(xz);
    float aBarroHierba = smoothstep(0.16, 0.42, t);
    float aBarro = smoothstep(0.46, 0.7, t);
    float mezcla = smoothstep(0.25, 0.75, RuidoN(xz * 0.13 + 7.0));
    float3 nH, nBH, nB;
    float3 hierba = CapaN(TEXTURE2D_ARGS(_Cesped, sampler_Cesped), TEXTURE2D_ARGS(_CespedN, sampler_Cesped), xz, TAM_HIERBA, mezcla, nH);
    float3 barroHierba = CapaN(TEXTURE2D_ARGS(_BarroHierba, sampler_Cesped), TEXTURE2D_ARGS(_BarroHierbaN, sampler_Cesped), xz, TAM_BARRO_HIERBA, mezcla, nBH);
    float3 barro = CapaN(TEXTURE2D_ARGS(_Barro, sampler_Cesped), TEXTURE2D_ARGS(_BarroN, sampler_Cesped), xz, TAM_BARRO, mezcla, nB);
    // La hierba del paquete sale verde lima con nuestra luz: a un verde natural (biblia, #76A946),
    // y con manchas grandes más secas o más verdes, sin seguir las caras.
    hierba = TonoHierbaN(hierba);
    // El barro con hierba también trae la hierba lima: el mismo verde, a medias.
    barroHierba = lerp(barroHierba, TonoHierbaN(barroHierba) * 1.25, 0.7);
    float tono = RuidoN(xz * 0.035 + 3.0) * 0.7 + RuidoN(xz * 0.11 + 17.0) * 0.3;
    hierba *= lerp(float3(0.9, 1.0, 0.95), float3(1.08, 1.0, 0.85), smoothstep(0.3, 0.75, tono));
    float3 color = lerp(lerp(hierba, barroHierba, aBarroHierba), barro, aBarro);
    float3 n = normalize(lerp(lerp(nH, nBH, aBarroHierba), nB, aBarro));
    normalDetalle = normalize(lerp(float3(0, 0, 1), n, suelo));
    return lerp(ALinealN(paleta), color, suelo);
}

/// Cuánto de roca de montaña es un color de la paleta (sRGB).
float EsRocaN(float3 paleta)
{
    return max(ParecidoN(paleta, float3(160, 142, 150) / 255.0), ParecidoN(paleta, float3(116, 102, 122) / 255.0));
}

#define TAM_ROCA 5.0

/// La piedra de las paredes entre las rocas del paquete: triplanar con el barro del paquete
/// pasado a piedra gris oliva (#777969 / #95917E de la biblia), con estratos horizontales.
/// Devuelve el albedo lineal y deja en `normal` la normal del mundo con el detalle.
float3 RocaN(float3 p, float3 cara, out float3 normal)
{
    float3 w = pow(abs(cara), 4.0);
    w /= (w.x + w.y + w.z);
    float2 uvX = p.zy / TAM_ROCA, uvY = p.xz / TAM_ROCA, uvZ = p.xy / TAM_ROCA;
    float3 c = SAMPLE_TEXTURE2D(_Barro, sampler_Cesped, uvX).rgb * w.x
             + SAMPLE_TEXTURE2D(_Barro, sampler_Cesped, uvY).rgb * w.y
             + SAMPLE_TEXTURE2D(_Barro, sampler_Cesped, uvZ).rgb * w.z;
    float3 tX = UnpackNormal(SAMPLE_TEXTURE2D(_BarroN, sampler_Cesped, uvX));
    float3 tY = UnpackNormal(SAMPLE_TEXTURE2D(_BarroN, sampler_Cesped, uvY));
    float3 tZ = UnpackNormal(SAMPLE_TEXTURE2D(_BarroN, sampler_Cesped, uvZ));
    // Mezcla "whiteout" de las tres normales.
    tX = float3(tX.xy + cara.zy, abs(tX.z) * cara.x);
    tY = float3(tY.xy + cara.xz, abs(tY.z) * cara.y);
    tZ = float3(tZ.xy + cara.xy, abs(tZ.z) * cara.z);
    normal = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
    // De barro a piedra: casi sin color y hacia el gris oliva, con dos tonos a manchas grandes.
    float luz = dot(c, float3(0.2126, 0.7152, 0.0722));
    float mancha = smoothstep(0.3, 0.7, RuidoN(p.xz * 0.02 + p.y * 0.01));
    float3 piedra = lerp(ALinealN(float3(119, 121, 105) / 255.0), ALinealN(float3(149, 145, 126) / 255.0), mancha);
    float3 color = piedra * (0.55 + 1.6 * luz) ;
    // Estratos: bandas horizontales que ondulan un poco.
    float estrato = RuidoN(float2(p.y * 0.55 + RuidoN(p.xz * 0.04) * 2.0, (p.x + p.z) * 0.03));
    color *= lerp(0.82, 1.12, estrato);
    return color;
}

/// Albedo lineal del suelo (para lo que se funde con él: caminos, surco).
float3 AlbedoSueloN(float3 paleta, float2 xz, float escala, float fuerza)
{
    float3 nada;
    return lerp(ALinealN(paleta), SueloN(paleta, xz, nada), fuerza);
}

/// La normal de la cara con el detalle de la textura del suelo encima.
float3 NormalSueloN(float3 cara, float3 detalle)
{
    float3 t = normalize(float3(1, 0, 0) - cara * cara.x);
    float3 b = normalize(float3(0, 0, 1) - cara * cara.z);
    return normalize(t * detalle.x + b * detalle.y + cara * detalle.z);
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
