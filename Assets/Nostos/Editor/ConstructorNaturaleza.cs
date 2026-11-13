using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nostos.EditorTools
{
    /// <summary>
    /// La naturaleza del paquete "A piece of nature" (Assets/A_piece_of_nature): pinos y rocas tal
    /// cual, el césped pintado en el suelo del valle y hierba densa por todo el valle (matas de su
    /// textura de hierba, pintadas con GPU instancing por HierbaDelValle).
    /// </summary>
    public static partial class ConstructorEscena
    {
        const string CarpetaPieza = "Assets/A_piece_of_nature/";
        const string TexturaMata = CarpetaPieza + "Textures/Grass.tif";
        const string TexturaCesped = CarpetaPieza + "Textures/Grass_texture_1.tif";

        /// <summary>Matas por metro cuadrado de hierba.</summary>
        const float DensidadHierba = 3.2f;
        /// <summary>Hasta dónde llega la hierba desde el centro del valle (m).</summary>
        const float RadioHierba = 118f;

        /// <summary>Prefab del paquete por nombre (Pine_tree, Rock_3, Rock_4, Chest), o null.</summary>
        static GameObject Pieza(string nombre)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaPieza + "Prefabs/" + nombre + ".prefab");
        }

        static Texture2D BuscarTextura(string ruta)
        {
            var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
            if (textura != null) return textura;
            // Por si las texturas no están en Textures/: se busca por nombre en todo el paquete.
            if (!AssetDatabase.IsValidFolder(CarpetaPieza.TrimEnd('/'))) return null;
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            foreach (string guid in AssetDatabase.FindAssets(nombre + " t:Texture2D", new[] { CarpetaPieza.TrimEnd('/') }))
            {
                string otra = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(otra) == nombre) return AssetDatabase.LoadAssetAtPath<Texture2D>(otra);
            }
            return null;
        }

        /// <summary>
        /// La mata de hierba tiene alfa: con mipmaps normales se "come" a lo lejos. Se conserva la
        /// cobertura (ajuste de importación; la imagen no cambia).
        /// </summary>
        static void PrepararTexturaMata(Texture2D mata)
        {
            string ruta = AssetDatabase.GetAssetPath(mata);
            var importador = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importador == null || importador.mipMapsPreserveCoverage) return;
            importador.mipMapsPreserveCoverage = true;
            importador.alphaTestReferenceValue = 0.5f;
            importador.alphaIsTransparency = true;
            importador.SaveAndReimport();
        }

        // ------------------------------------------------------------------ suelo

        /// <summary>Material del suelo del valle: la paleta de siempre y el césped del paquete donde hay hierba.</summary>
        static Material MaterialSueloValle()
        {
            Shader shader = Shader.Find("Nostos/SueloValle");
            Texture2D cesped = BuscarTextura(TexturaCesped);
            var paleta = AssetDatabase.LoadAssetAtPath<Texture2D>(ImportadorKit.RutaTextura);
            if (shader == null || cesped == null || paleta == null) return null;
            Material material = CargarOCrear("SueloValle", "Nostos/SueloValle");
            material.shader = shader;
            material.SetTexture("_BaseMap", paleta);
            material.SetTexture("_Cesped", cesped);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>El césped también en el borde del surco, para que se funda igual que con el suelo.</summary>
        static void CespedEn(Material material)
        {
            Texture2D cesped = BuscarTextura(TexturaCesped);
            if (material == null || cesped == null || !material.HasProperty("_Cesped")) return;
            material.SetTexture("_Cesped", cesped);
            EditorUtility.SetDirty(material);
        }

        // ------------------------------------------------------------------ hierba

        static void SembrarHierba()
        {
            Texture2D textura = BuscarTextura(TexturaMata);
            Shader shader = Shader.Find("Nostos/HierbaViento");
            if (textura == null || shader == null)
            {
                Debug.LogWarning("Nostos: falta Grass.tif del paquete \"A piece of nature\" o el shader HierbaViento; no hay hierba.");
                return;
            }
            PrepararTexturaMata(textura);

            var suelo = new SueloIndexado();
            if (!suelo.Valido)
            {
                Debug.LogWarning("Nostos: no pude leer la malla del valle; no hay hierba.");
                return;
            }

            // Matas repartidas en una rejilla con desorden; solo donde la cara del valle es de hierba,
            // casi llana y fuera de la plaza, la panadería y el surco.
            var azar = new System.Random(23);
            float paso = 1f / Mathf.Sqrt(DensidadHierba);
            var datos = new List<byte>(200000 * 16);
            int cuantas = 0;
            for (float z = -RadioHierba; z <= RadioHierba; z += paso)
            {
                for (float x = -RadioHierba; x <= RadioHierba; x += paso)
                {
                    float px = x + ((float)azar.NextDouble() - 0.5f) * paso * 0.95f;
                    float pz = z + ((float)azar.NextDouble() - 0.5f) * paso * 0.95f;
                    float giro = (float)azar.NextDouble() * 360f;
                    float escala = Mathf.Lerp(0.65f, 1.45f, Mathf.Pow((float)azar.NextDouble(), 1.4f));
                    if (px * px + pz * pz > RadioHierba * RadioHierba) continue;
                    if (!suelo.Muestra(px, pz, out float py, out float hierba, out float normalY)) continue;
                    if (hierba < 0.5f || normalY < 0.82f) continue;
                    if (Ocupado(px, pz)) continue;
                    // Manchas más ralas aquí y allá, para que no parezca una alfombra.
                    float claros = Mathf.PerlinNoise(px * 0.07f + 13f, pz * 0.07f + 5f);
                    if (claros < 0.28f && azar.NextDouble() > claros * 2.5f) continue;
                    datos.AddRange(System.BitConverter.GetBytes(px));
                    datos.AddRange(System.BitConverter.GetBytes(py - 0.03f));
                    datos.AddRange(System.BitConverter.GetBytes(pz));
                    datos.AddRange(System.BitConverter.GetBytes(Mathf.Floor(giro * 10f) + Mathf.Clamp(escala - 0.6f, 0f, 0.999f)));
                    cuantas++;
                }
            }

            string rutaDatos = CarpetaPrefabsPaquetes + "/hierba_valle.bytes";
            File.WriteAllBytes(rutaDatos, datos.ToArray());
            AssetDatabase.ImportAsset(rutaDatos);

            Mesh mata = MallaMata();
            AssetDatabase.CreateAsset(mata, CarpetaPrefabsPaquetes + "/mata_hierba.asset");

            Material material = CargarOCrear("HierbaValle", "Nostos/HierbaViento");
            material.shader = shader;
            material.SetTexture("_BaseMap", textura);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var go = new GameObject("Hierba del valle");
            var hierbaValle = go.AddComponent<HierbaDelValle>();
            hierbaValle.datos = AssetDatabase.LoadAssetAtPath<TextAsset>(rutaDatos);
            hierbaValle.mata = mata;
            hierbaValle.material = material;
            hierbaValle.distancia = material.GetFloat("_Distancia");
            Debug.Log("Nostos: " + cuantas + " matas de hierba en el valle (" + (datos.Count / 1024) + " KB).");
        }

        /// <summary>Una mata: tres planos cruzados con la textura entera, de 0,7 m de ancho y 0,5 de alto.</summary>
        static Mesh MallaMata()
        {
            const float Ancho = 0.7f, Alto = 0.5f;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI / 3f;
                var lado = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (Ancho * 0.5f);
                int b = v.Count;
                v.Add(-lado); v.Add(lado); v.Add(lado + Vector3.up * Alto); v.Add(-lado + Vector3.up * Alto);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                t.Add(b); t.Add(b + 2); t.Add(b + 1);
                t.Add(b); t.Add(b + 3); t.Add(b + 2);
            }
            var malla = new Mesh { name = "Mata de hierba" };
            malla.SetVertices(v);
            malla.SetUVs(0, uv);
            malla.SetTriangles(t, 0);
            var normales = new Vector3[v.Count];
            for (int i = 0; i < normales.Length; i++) normales[i] = Vector3.up;
            malla.normals = normales;
            malla.bounds = new Bounds(new Vector3(0f, Alto * 0.5f, 0f), new Vector3(Ancho + 0.3f, Alto + 0.3f, Ancho + 0.3f));
            return malla;
        }

        /// <summary>
        /// La malla del valle con un índice por casillas, para saber rápido la altura exacta, si la
        /// cara es de hierba (por su color de paleta) y su inclinación en cualquier punto.
        /// </summary>
        class SueloIndexado
        {
            const float Casilla = 4f;
            Vector3[] puntos;
            float[] hierba;
            Vector3[] normal;
            readonly Dictionary<long, List<int>> casillas = new Dictionary<long, List<int>>();
            public bool Valido { get; private set; }

            static readonly Color32[] ColoresHierba =
            {
                new Color32(150, 160, 74, 255), new Color32(184, 172, 92, 255), new Color32(106, 132, 66, 255),
                new Color32(120, 150, 72, 255), new Color32(150, 170, 84, 255),
            };

            public SueloIndexado()
            {
                try
                {
                    GameObject valle = GameObject.Find("Valle");
                    Transform suelo = valle != null ? BuscarHijo(valle.transform, "Suelo") : null;
                    MeshFilter filtro = suelo != null ? suelo.GetComponent<MeshFilter>() : null;
                    if (filtro == null || filtro.sharedMesh == null || !File.Exists(ImportadorKit.RutaTextura)) return;
                    var paleta = new Texture2D(2, 2);
                    paleta.LoadImage(File.ReadAllBytes(ImportadorKit.RutaTextura));
                    Color32[] pixeles = paleta.GetPixels32();
                    int ancho = paleta.width, alto = paleta.height;
                    Object.DestroyImmediate(paleta);

                    Mesh malla = filtro.sharedMesh;
                    Vector3[] v = malla.vertices;
                    Vector2[] uvs = malla.uv;
                    int[] t = malla.triangles;
                    Matrix4x4 m = filtro.transform.localToWorldMatrix;
                    int caras = t.Length / 3;
                    puntos = new Vector3[t.Length];
                    hierba = new float[caras];
                    normal = new Vector3[caras];
                    for (int c = 0; c < caras; c++)
                    {
                        Vector3 p0 = m.MultiplyPoint3x4(v[t[c * 3]]);
                        Vector3 p1 = m.MultiplyPoint3x4(v[t[c * 3 + 1]]);
                        Vector3 p2 = m.MultiplyPoint3x4(v[t[c * 3 + 2]]);
                        puntos[c * 3] = p0;
                        puntos[c * 3 + 1] = p1;
                        puntos[c * 3 + 2] = p2;
                        Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                        if (n.y < 0f) n = -n;
                        normal[c] = n;
                        Vector2 q = uvs[t[c * 3]];
                        Color32 color = pixeles[Mathf.Clamp((int)(q.y * alto), 0, alto - 1) * ancho + Mathf.Clamp((int)(q.x * ancho), 0, ancho - 1)];
                        hierba[c] = EsHierba(color);

                        int x0 = Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x) / Casilla), x1 = Mathf.FloorToInt(Mathf.Max(p0.x, p1.x, p2.x) / Casilla);
                        int z0 = Mathf.FloorToInt(Mathf.Min(p0.z, p1.z, p2.z) / Casilla), z1 = Mathf.FloorToInt(Mathf.Max(p0.z, p1.z, p2.z) / Casilla);
                        for (int cx = x0; cx <= x1; cx++)
                        {
                            for (int cz = z0; cz <= z1; cz++)
                            {
                                long clave = Clave(cx, cz);
                                if (!casillas.TryGetValue(clave, out List<int> lista)) casillas.Add(clave, lista = new List<int>());
                                lista.Add(c);
                            }
                        }
                    }
                    Valido = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Nostos: no pude indexar el valle para la hierba (" + ex.Message + ").");
                    Valido = false;
                }
            }

            static long Clave(int x, int z) => ((long)x << 32) ^ (uint)z;

            static float EsHierba(Color32 c)
            {
                float mejor = 0f;
                foreach (Color32 h in ColoresHierba)
                {
                    float d = Mathf.Sqrt(Sq(c.r - h.r) + Sq(c.g - h.g) + Sq(c.b - h.b)) / 255f;
                    mejor = Mathf.Max(mejor, Mathf.Clamp01(1f - d * 10f));
                }
                return mejor;
            }

            static float Sq(float a) => a * a;

            /// <summary>Altura exacta, cuánto de hierba es la cara y su normal Y en (x, z).</summary>
            public bool Muestra(float x, float z, out float y, out float esHierba, out float normalY)
            {
                y = 0f;
                esHierba = 0f;
                normalY = 0f;
                if (!casillas.TryGetValue(Clave(Mathf.FloorToInt(x / Casilla), Mathf.FloorToInt(z / Casilla)), out List<int> lista)) return false;
                foreach (int c in lista)
                {
                    Vector3 a = puntos[c * 3], b = puntos[c * 3 + 1], d = puntos[c * 3 + 2];
                    // Coordenadas baricéntricas en planta.
                    float det = (b.z - d.z) * (a.x - d.x) + (d.x - b.x) * (a.z - d.z);
                    if (Mathf.Abs(det) < 1e-6f) continue;
                    float l1 = ((b.z - d.z) * (x - d.x) + (d.x - b.x) * (z - d.z)) / det;
                    float l2 = ((d.z - a.z) * (x - d.x) + (a.x - d.x) * (z - d.z)) / det;
                    float l3 = 1f - l1 - l2;
                    if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                    y = l1 * a.y + l2 * b.y + l3 * d.y;
                    esHierba = hierba[c];
                    normalY = normal[c].y;
                    return true;
                }
                return false;
            }
        }
    }
}
