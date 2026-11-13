using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nostos.EditorTools
{
    /// <summary>
    /// El suelo del valle con las capas de terreno de CliffStonePackM (hierba, barro con hierba y
    /// barro), según la biblia artística. Los caminos y las orillas del valle salían como caras planas
    /// de color arena con el borde en zigzag; ahora se pintan con un mapa visto desde arriba
    /// (mapa_suelo.png, 1 texel = 0,39 m) hecho con esas caras y difuminado, más ruido en el shader:
    /// el borde es irregular y pasa de barro a barro con hierba y a hierba sin seguir los triángulos.
    /// </summary>
    public static partial class ConstructorEscena
    {
        const string RutaMapaSuelo = CarpetaPrefabsPaquetes + "/mapa_suelo.png";
        const int LadoMapa = 1024;
        const float MetrosMapa = 400f;   // el valle va de -200 a 200 (generar_terreno.py, TAMANO)

        static float[] mapaTierra;

        /// <summary>Las tres capas de terreno del paquete (null si falta alguna).</summary>
        static TerrainLayer CapaCliff(string fin)
        {
            if (!HayCliff) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { CarpetaCliff }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(ruta).EndsWith(fin)) return AssetDatabase.LoadAssetAtPath<TerrainLayer>(ruta);
            }
            return null;
        }

        /// <summary>Pone en un material del suelo (valle, caminos, surco) las texturas del paquete y el mapa.</summary>
        static void TexturasSuelo(Material material)
        {
            if (material == null) return;
            TerrainLayer hierba = CapaCliff("_Grass"), barro = CapaCliff("_Mud"), barroHierba = CapaCliff("_GrassyMud");
            if (hierba != null && hierba.diffuseTexture != null)
            {
                material.SetTexture("_Cesped", hierba.diffuseTexture);
                if (hierba.normalMapTexture != null && material.HasProperty("_CespedN")) material.SetTexture("_CespedN", hierba.normalMapTexture);
            }
            if (barro != null && material.HasProperty("_Barro"))
            {
                material.SetTexture("_Barro", barro.diffuseTexture);
                if (barro.normalMapTexture != null) material.SetTexture("_BarroN", barro.normalMapTexture);
            }
            if (barroHierba != null && material.HasProperty("_BarroHierba"))
            {
                material.SetTexture("_BarroHierba", barroHierba.diffuseTexture);
                if (barroHierba.normalMapTexture != null) material.SetTexture("_BarroHierbaN", barroHierba.normalMapTexture);
            }
            if (material.HasProperty("_MapaSuelo"))
            {
                Texture2D mapa = MapaSuelo();
                if (mapa != null) material.SetTexture("_MapaSuelo", mapa);
            }
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Mapa del valle visto desde arriba: R = tierra (caminos y orillas), G = roca. Se rehace una
        /// vez por carga del editor.
        /// </summary>
        static Texture2D MapaSuelo()
        {
            if (mapaTierra != null && File.Exists(RutaMapaSuelo)) return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaMapaSuelo);
            GameObject valle = GameObject.Find("Valle");
            Transform sueloT = valle != null ? BuscarHijo(valle.transform, "Suelo") : null;
            MeshFilter filtro = sueloT != null ? sueloT.GetComponent<MeshFilter>() : null;
            if (filtro == null || filtro.sharedMesh == null || !File.Exists(ImportadorKit.RutaTextura))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaMapaSuelo);

            var paleta = new Texture2D(2, 2);
            paleta.LoadImage(File.ReadAllBytes(ImportadorKit.RutaTextura));
            Color32[] pixeles = paleta.GetPixels32();
            int anchoP = paleta.width, altoP = paleta.height;
            Object.DestroyImmediate(paleta);

            var tierra = new float[LadoMapa * LadoMapa];
            var roca = new float[LadoMapa * LadoMapa];
            Mesh malla = filtro.sharedMesh;
            Vector3[] v = malla.vertices;
            Vector2[] uvs = malla.uv;
            int[] t = malla.triangles;
            Matrix4x4 m = filtro.transform.localToWorldMatrix;
            float porTexel = LadoMapa / MetrosMapa;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector2 q = uvs[t[i]];
                Color32 c = pixeles[Mathf.Clamp((int)(q.y * altoP), 0, altoP - 1) * anchoP + Mathf.Clamp((int)(q.x * anchoP), 0, anchoP - 1)];
                bool esTierra = Parecido(c, 200, 168, 114) || Parecido(c, 132, 104, 78);
                bool esRoca = Parecido(c, 160, 142, 150) || Parecido(c, 116, 102, 122);
                if (!esTierra && !esRoca) continue;
                float[] destino = esTierra ? tierra : roca;
                Vector3 p0 = m.MultiplyPoint3x4(v[t[i]]), p1 = m.MultiplyPoint3x4(v[t[i + 1]]), p2 = m.MultiplyPoint3x4(v[t[i + 2]]);
                Vector2 a = (new Vector2(p0.x, p0.z) + Vector2.one * MetrosMapa * 0.5f) * porTexel;
                Vector2 b = (new Vector2(p1.x, p1.z) + Vector2.one * MetrosMapa * 0.5f) * porTexel;
                Vector2 d = (new Vector2(p2.x, p2.z) + Vector2.one * MetrosMapa * 0.5f) * porTexel;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x, d.x))), x1 = Mathf.Min(LadoMapa - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x, d.x)));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y, d.y))), y1 = Mathf.Min(LadoMapa - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y, d.y)));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (EnTriangulo(new Vector2(x + 0.5f, y + 0.5f), a, b, d)) destino[y * LadoMapa + x] = 1f;
                    }
            }
            // Difuminado (tres pasadas de caja de 7 texels, casi gaussiano): borde de unos 2 m.
            for (int pasada = 0; pasada < 3; pasada++)
            {
                Caja(tierra, 3);
                Caja(roca, 3);
            }
            mapaTierra = tierra;

            var textura = new Texture2D(LadoMapa, LadoMapa, TextureFormat.RGBA32, false, true);
            var salida = new Color32[LadoMapa * LadoMapa];
            for (int i = 0; i < salida.Length; i++)
                salida[i] = new Color32((byte)Mathf.RoundToInt(tierra[i] * 255f), (byte)Mathf.RoundToInt(roca[i] * 255f), 0, 255);
            textura.SetPixels32(salida);
            textura.Apply();
            File.WriteAllBytes(RutaMapaSuelo, textura.EncodeToPNG());
            Object.DestroyImmediate(textura);
            AssetDatabase.ImportAsset(RutaMapaSuelo);
            var importador = AssetImporter.GetAtPath(RutaMapaSuelo) as TextureImporter;
            if (importador != null)
            {
                importador.sRGBTexture = false;
                importador.mipmapEnabled = false;
                importador.wrapMode = TextureWrapMode.Clamp;
                importador.filterMode = FilterMode.Bilinear;
                importador.textureCompression = TextureImporterCompression.Uncompressed;
                importador.SaveAndReimport();
            }
            Debug.Log("Nostos: mapa del suelo del valle hecho (" + LadoMapa + " x " + LadoMapa + ").");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaMapaSuelo);
        }

        /// <summary>
        /// Cuánta tierra hay en (x, z), con el mismo ruido que pone el shader en el borde (0 hierba,
        /// 1 barro). La hierba en matas solo crece donde es menos de 0,2.
        /// </summary>
        static float TierraEn(float x, float z)
        {
            if (mapaTierra == null) return 0f;
            float u = (x / MetrosMapa + 0.5f) * LadoMapa - 0.5f, w = (z / MetrosMapa + 0.5f) * LadoMapa - 0.5f;
            int ix = Mathf.Clamp(Mathf.FloorToInt(u), 0, LadoMapa - 2), iz = Mathf.Clamp(Mathf.FloorToInt(w), 0, LadoMapa - 2);
            float fx = Mathf.Clamp01(u - ix), fz = Mathf.Clamp01(w - iz);
            float a = mapaTierra[iz * LadoMapa + ix], b = mapaTierra[iz * LadoMapa + ix + 1];
            float c = mapaTierra[(iz + 1) * LadoMapa + ix], d = mapaTierra[(iz + 1) * LadoMapa + ix + 1];
            float mapa = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
            float n = RuidoN(x * 0.55f, z * 0.55f) * 0.6f + RuidoN(x * 1.7f + 13f, z * 1.7f + 13f) * 0.3f + RuidoN(x * 4.3f + 31f, z * 4.3f + 31f) * 0.1f;
            return mapa + (n - 0.5f) * 0.45f;
        }

        // El mismo ruido de valor que SueloNostos.hlsl (AzarN / RuidoN).
        static float AzarN(float x, float y)
        {
            x = Frac(x * 123.34f);
            y = Frac(y * 456.21f);
            float d = x * (x + 45.32f) + y * (y + 45.32f);
            x += d;
            y += d;
            return Frac(x * y);
        }

        static float RuidoN(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            float a = AzarN(ix, iy), b = AzarN(ix + 1f, iy), c = AzarN(ix, iy + 1f), d = AzarN(ix + 1f, iy + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        static bool Parecido(Color32 c, int r, int g, int b) => Mathf.Abs(c.r - r) + Mathf.Abs(c.g - g) + Mathf.Abs(c.b - b) < 12;

        static bool EnTriangulo(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool negativo = d1 < 0 || d2 < 0 || d3 < 0;
            bool positivo = d1 > 0 || d2 > 0 || d3 > 0;
            return !(negativo && positivo);
        }

        /// <summary>Media en caja de lado 2·r+1, por filas y luego por columnas.</summary>
        static void Caja(float[] datos, int r)
        {
            int n = LadoMapa;
            var tmp = new float[datos.Length];
            float lado = 2 * r + 1;
            for (int y = 0; y < n; y++)
            {
                float suma = 0f;
                for (int k = -r; k <= r; k++) suma += datos[y * n + Mathf.Clamp(k, 0, n - 1)];
                for (int x = 0; x < n; x++)
                {
                    tmp[y * n + x] = suma / lado;
                    suma += datos[y * n + Mathf.Min(x + r + 1, n - 1)] - datos[y * n + Mathf.Max(x - r, 0)];
                }
            }
            for (int x = 0; x < n; x++)
            {
                float suma = 0f;
                for (int k = -r; k <= r; k++) suma += tmp[Mathf.Clamp(k, 0, n - 1) * n + x];
                for (int y = 0; y < n; y++)
                {
                    datos[y * n + x] = suma / lado;
                    suma += tmp[Mathf.Min(y + r + 1, n - 1) * n + x] - tmp[Mathf.Max(y - r, 0) * n + x];
                }
            }
        }
    }
}
