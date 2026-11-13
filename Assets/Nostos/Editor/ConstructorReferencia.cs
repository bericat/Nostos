using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Fase 0 de la biblia artística: CliffStonePackM (Dzeruza) pasado a URP y capturas para comparar.
    /// Los materiales del paquete son de HDRP (en URP salen rosas): se hacen copias URP/Lit con sus
    /// mismas texturas (la MaskMap de HDRP lleva el metal en R, la oclusión en G y la suavidad en A,
    /// que es justo lo que URP lee de _MetallicGlossMap y _OcclusionMap) y copias de sus prefabs con
    /// esos materiales. Lo del paquete no se toca.
    /// Las capturas se hacen en una copia de la escena del juego (Comparacion.unity) con rocas del
    /// paquete junto a la nave y junto a la plaza, jugando, para que se vea igual que al jugar.
    /// </summary>
    public static partial class ConstructorEscena
    {
        const string CarpetaCliff = "Assets/Dzeruza/CliffStonePackM";
        const string CarpetaCliffURP = "Assets/Nostos/Prefabs/Paquetes/CliffStone";
        const string RutaComparacion = "Assets/Nostos/Escenas/Comparacion.unity";
        const string VersionCapturas = "R5";
        const string MarcaCapturas = "Library/NostosCapturas.txt";
        const string ClaveVolver = "Nostos.VolverAEscena";
        const string ClaveInicioCapturas = "Nostos.InicioCapturas";

        static bool HayCliff => AssetDatabase.IsValidFolder(CarpetaCliff);

        // ------------------------------------------------------------------ materiales y prefabs URP

        [MenuItem("Nostos/Pasar CliffStonePackM a URP")]
        public static void MenuCliffURP()
        {
            int n = CliffStoneURP();
            Debug.Log("Nostos: CliffStonePackM en URP, " + n + " prefabs en " + CarpetaCliffURP + ".");
        }

        /// <summary>Materiales y prefabs URP del paquete. Devuelve cuántos prefabs hay.</summary>
        static int CliffStoneURP()
        {
            if (!HayCliff) return 0;
            CarpetasNostos.Asegurar(CarpetaCliffURP + "/Materiales");
            var mapa = new Dictionary<Material, Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { CarpetaCliff }))
            {
                var original = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (original == null) continue;
                Material urp = MaterialURPDe(original);
                if (urp != null) mapa[original] = urp;
            }

            int prefabs = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CarpetaCliff }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (original == null) continue;
                var copia = (GameObject)PrefabUtility.InstantiatePrefab(original);
                if (PrefabUtility.IsPartOfPrefabInstance(copia))
                    PrefabUtility.UnpackPrefabInstance(copia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (Renderer r in copia.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    for (int j = 0; j < mats.Length; j++)
                    {
                        if (mats[j] != null && mapa.TryGetValue(mats[j], out Material urp)) mats[j] = urp;
                    }
                    r.sharedMaterials = mats;
                }
                string nombre = Path.GetFileNameWithoutExtension(ruta).Replace(" Variant", "");
                copia.name = nombre;
                PrefabUtility.SaveAsPrefabAsset(copia, CarpetaCliffURP + "/" + nombre + ".prefab");
                Object.DestroyImmediate(copia);
                prefabs++;
            }
            AssetDatabase.SaveAssets();
            return prefabs;
        }

        /// <summary>Copia URP/Lit de un material HDRP/Lit, leyendo lo guardado en el material (su shader no está).</summary>
        static Material MaterialURPDe(Material hdrp)
        {
            var texturas = new Dictionary<string, (Texture t, Vector2 escala, Vector2 desplaza)>();
            var numeros = new Dictionary<string, float>();
            var colores = new Dictionary<string, Color>();
            var so = new SerializedObject(hdrp);
            SerializedProperty guardadas = so.FindProperty("m_SavedProperties");
            if (guardadas == null) return null;
            SerializedProperty envs = guardadas.FindPropertyRelative("m_TexEnvs");
            for (int i = 0; envs != null && i < envs.arraySize; i++)
            {
                SerializedProperty e = envs.GetArrayElementAtIndex(i);
                string nombre = e.FindPropertyRelative("first").stringValue;
                SerializedProperty s = e.FindPropertyRelative("second");
                var t = s.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
                if (t != null) texturas[nombre] = (t, s.FindPropertyRelative("m_Scale").vector2Value, s.FindPropertyRelative("m_Offset").vector2Value);
            }
            SerializedProperty flotantes = guardadas.FindPropertyRelative("m_Floats");
            for (int i = 0; flotantes != null && i < flotantes.arraySize; i++)
            {
                SerializedProperty e = flotantes.GetArrayElementAtIndex(i);
                numeros[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").floatValue;
            }
            SerializedProperty tonos = guardadas.FindPropertyRelative("m_Colors");
            for (int i = 0; tonos != null && i < tonos.arraySize; i++)
            {
                SerializedProperty e = tonos.GetArrayElementAtIndex(i);
                colores[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null;
            string ruta = CarpetaCliffURP + "/Materiales/" + hdrp.name + "_URP.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null)
            {
                m = new Material(lit) { name = hdrp.name + "_URP" };
                AssetDatabase.CreateAsset(m, ruta);
            }
            else
            {
                m.shader = lit;
            }
            m.shaderKeywords = new string[0];
            float Num(string clave, float defecto) => numeros.TryGetValue(clave, out float v) ? v : defecto;

            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_Surface", 0f);
            m.SetColor("_BaseColor", colores.TryGetValue("_BaseColor", out Color c) ? c : Color.white);
            bool cortar = Num("_AlphaCutoffEnable", 0f) > 0.5f;
            if (texturas.TryGetValue("_BaseColorMap", out var b))
            {
                Texture baseMap = b.t;
                if (cortar && !TieneAlfa(baseMap)) baseMap = ConAlfa(baseMap) ?? baseMap;
                m.SetTexture("_BaseMap", baseMap);
                m.SetTextureScale("_BaseMap", b.escala);
                m.SetTextureOffset("_BaseMap", b.desplaza);
                m.mainTexture = baseMap;
            }
            if (texturas.TryGetValue("_NormalMap", out var n))
            {
                m.SetTexture("_BumpMap", n.t);
                m.SetFloat("_BumpScale", Num("_NormalScale", 1f));
                m.EnableKeyword("_NORMALMAP");
            }
            if (texturas.TryGetValue("_MaskMap", out var mascara))
            {
                m.SetTexture("_MetallicGlossMap", mascara.t);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_SmoothnessTextureChannel", 0f);
                m.SetFloat("_Smoothness", Num("_SmoothnessRemapMax", 1f));
                m.SetTexture("_OcclusionMap", mascara.t);
                m.SetFloat("_OcclusionStrength", 1f);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            else
            {
                m.SetFloat("_Metallic", Num("_Metallic", 0f));
                m.SetFloat("_Smoothness", Num("_Smoothness", 0.4f));
            }
            m.SetFloat("_AlphaClip", cortar ? 1f : 0f);
            m.SetFloat("_Cutoff", Num("_AlphaCutoff", 0.5f));
            if (cortar)
            {
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = -1;
            }
            m.SetFloat("_Cull", Num("_DoubleSidedEnable", 0f) > 0.5f ? 0f : 2f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static bool TieneAlfa(Texture t)
        {
            var importador = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
            return importador == null || importador.DoesSourceTextureHaveAlpha();
        }

        /// <summary>El color con la transparencia de la textura "Alpha" hermana (la hierba del paquete las trae separadas).</summary>
        static Texture ConAlfa(Texture color)
        {
            string ruta = AssetDatabase.GetAssetPath(color);
            string carpeta = Path.GetDirectoryName(ruta).Replace('\\', '/');
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            int corte = nombre.LastIndexOf('_');
            string prefijo = corte > 0 ? nombre.Substring(0, corte + 1) : nombre;
            string rutaAlfa = null;
            foreach (string guid in AssetDatabase.FindAssets(prefijo + "Alpha t:Texture2D", new[] { carpeta }))
            {
                rutaAlfa = AssetDatabase.GUIDToAssetPath(guid);
                break;
            }
            if (rutaAlfa == null) return null;
            var a = new Texture2D(2, 2);
            var col = new Texture2D(2, 2);
            try
            {
                if (!a.LoadImage(File.ReadAllBytes(rutaAlfa)) || !col.LoadImage(File.ReadAllBytes(ruta))) return null;
                var salida = new Texture2D(col.width, col.height, TextureFormat.RGBA32, false);
                for (int y = 0; y < col.height; y++)
                    for (int x = 0; x < col.width; x++)
                    {
                        Color p = col.GetPixel(x, y);
                        p.a = a.GetPixelBilinear((x + 0.5f) / col.width, (y + 0.5f) / col.height).r;
                        salida.SetPixel(x, y, p);
                    }
                CarpetasNostos.Asegurar(CarpetaCliffURP + "/Texturas");
                string destino = CarpetaCliffURP + "/Texturas/" + nombre + "_ConAlfa.png";
                File.WriteAllBytes(destino, salida.EncodeToPNG());
                Object.DestroyImmediate(salida);
                AssetDatabase.ImportAsset(destino);
                var importador = AssetImporter.GetAtPath(destino) as TextureImporter;
                if (importador != null)
                {
                    importador.alphaIsTransparency = true;
                    importador.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(destino);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(col);
            }
        }

        // ------------------------------------------------------------------ capturas de comparación

        /// <summary>Una vez por versión de las capturas, solas, con la escena del juego al día.</summary>
        static void RevisarCapturas()
        {
            if (!HayCliff || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(MarcaCapturas) && File.ReadAllText(MarcaCapturas).Trim() == VersionCapturas) return;
            if (SessionState.GetBool("Nostos.Capturas." + VersionCapturas, false)) return;
            if (SceneManager.GetActiveScene().isDirty) return;
            SessionState.SetBool("Nostos.Capturas." + VersionCapturas, true);
            Capturas();
        }

        [MenuItem("Nostos/Capturas de comparación")]
        public static void Capturas()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Nostos: sal del modo Play antes de hacer las capturas.");
                return;
            }
            if (!HayCliff)
            {
                Debug.LogWarning("Nostos: falta " + CarpetaCliff + ".");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int prefabs = CliffStoneURP();
            Debug.Log("Nostos: CliffStonePackM en URP, " + prefabs + " prefabs.");

            Scene escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
            Physics.SyncTransforms();
            GameObject valleGo = GameObject.Find("Valle");
            Transform sueloT = valleGo != null ? BuscarHijo(valleGo.transform, "Suelo") : null;
            MeshCollider suelo = sueloT != null ? sueloT.GetComponent<MeshCollider>() : null;

            var raiz = new GameObject("Comparación con CliffStonePackM");
            var vistas = new List<CapturasReferencia.Vista>();
            const float Ojo = 1.7f;

            // Junto a la nave: al otro lado del surco, con la nave detrás.
            GameObject nave = GameObject.Find("Nave de Nicias");
            Vector3 s = nave != null ? nave.transform.position : new Vector3((NaveCeldaX + NaveAncho * 0.5f) * TamCelda, 0f, (NaveCeldaZ + NaveFondo * 0.5f) * TamCelda);
            Vector3 lejosSurco = Vector3.forward;
            GameObject surco = GameObject.Find("Surco");
            if (surco != null && surco.GetComponent<Renderer>() != null)
            {
                Vector3 d = s - surco.GetComponent<Renderer>().bounds.center;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) lejosSurco = d.normalized;
            }
            Vector3 lado = Vector3.Cross(Vector3.up, lejosSurco);
            Vector3 g = SitioLibre(s + lejosSurco * 17f, 5f, s, 12f);
            g.y = Altura(suelo, g);
            GameObject m1 = PonerRoca("SM_Cliff_M1_SS_Brown", raiz.transform, g - lado * 2.6f, 25f, 0.25f, suelo);
            PonerRoca("SM_Cliff_M3_SS_Grey", raiz.transform, g + lado * 2.4f + lejosSurco * 1.2f, 140f, 0.2f, suelo);
            PonerRoca("SM_Cliff_M5_SS_Orange", raiz.transform, g - lejosSurco * 2.4f + lado * 0.4f, 260f, 0.2f, suelo);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 61f;
                Vector3 p = g + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * (3.6f + (i % 3) * 0.7f);
                PonerRoca("SM_Grass" + (1 + i % 3), raiz.transform, p, ang * 3f, 0f, suelo);
            }
            // Una veta nuestra al lado, con la misma luz.
            GameObject veta = null;
            foreach (NodoRecurso nodo in Object.FindObjectsByType<NodoRecurso>(FindObjectsSortMode.None))
            {
                if (nodo.recurso != Nostos.Sim.Recurso.Mineral || nodo.GetComponentInChildren<Renderer>() == null) continue;
                veta = Object.Instantiate(nodo.gameObject, raiz.transform);
                veta.name = "Veta nuestra (comparación)";
                Vector3 p = g + lado * 7f;
                Asentar(veta, new Vector3(p.x, Altura(suelo, p), p.z));
                break;
            }

            Vector3 haciaCamara = (lejosSurco * 0.85f + lado * 0.5f).normalized;
            Vector3 ojo = g + haciaCamara * 10f;
            ojo.y = Altura(suelo, ojo) + Ojo;
            vistas.Add(Vista("1_fps_rocas_y_nave", ojo, Vector3.Lerp(g, s, 0.35f) + Vector3.up * 1.4f, 70f));
            if (m1 != null) vistas.Add(VistaCerca("2_fps_cerca_roca_paquete", m1, haciaCamara, 3.4f, suelo, Ojo));
            if (veta != null) vistas.Add(VistaCerca("3_fps_cerca_veta_nuestra", veta, haciaCamara, 3.4f, suelo, Ojo));
            Vector3 ojoNave = g - lejosSurco * 3f + lado * 4.5f;
            ojoNave.y = Altura(suelo, ojoNave) + Ojo;
            vistas.Add(Vista("4_fps_nave_enterrada", ojoNave, s + Vector3.up * 0.8f, 70f));
            vistas.Add(Iso("5_iso_rocas_y_nave", Vector3.Lerp(g, s, 0.4f), 34f));
            vistas.Add(Iso("6_iso_cerca_rocas", g, 16f));

            // Junto a la plaza: rocas delante y el pueblo detrás.
            GameObject plazaGo = GameObject.Find("Plaza de Nova Lavinia");
            if (plazaGo != null)
            {
                Vector3 pl = plazaGo.transform.position;
                Vector3 r = SitioLibre(pl + new Vector3(-9f, 0f, 7f), 3.5f, pl, 7f);
                r.y = Altura(suelo, r);
                Vector3 haciaPlaza = pl - r;
                haciaPlaza.y = 0f;
                haciaPlaza.Normalize();
                Vector3 ladoP = Vector3.Cross(Vector3.up, haciaPlaza);
                PonerRoca("SM_Cliff_M2_SS_Grey", raiz.transform, r - ladoP * 1.6f, 70f, 0.2f, suelo);
                PonerRoca("SM_Cliff_M4_SS_Brown", raiz.transform, r + ladoP * 1.8f - haciaPlaza * 0.8f, 200f, 0.2f, suelo);
                PonerRoca("SM_Grass2", raiz.transform, r + haciaPlaza * 1.8f, 10f, 0f, suelo);
                PonerRoca("SM_Grass3", raiz.transform, r - haciaPlaza * 2.2f + ladoP * 0.5f, 100f, 0f, suelo);
                Vector3 ojoP = r - haciaPlaza * 7f + ladoP * 1.5f;
                ojoP.y = Altura(suelo, ojoP) + Ojo;
                vistas.Add(Vista("7_fps_pueblo", ojoP, pl + Vector3.up * 2f, 70f));
                vistas.Add(Iso("8_iso_pueblo", Vector3.Lerp(r, pl, 0.5f), 42f));
            }

            // Las montañas: dos paredes desde el borde del llano y el valle entero desde arriba.
            var montanas = new[]
            {
                ("9_fps_montana_oeste", new Vector3(-38f, 0f, 6f), new Vector3(-95f, 16f, 10f)),
                ("10_fps_montana_este", new Vector3(38f, 0f, -4f), new Vector3(95f, 16f, -8f)),
            };
            foreach (var (nombre, desde, hacia) in montanas)
            {
                Vector3 ojoM = desde;
                ojoM.y = Altura(suelo, desde) + Ojo;
                vistas.Add(Vista(nombre, ojoM, hacia, 70f));
            }
            vistas.Add(Iso("11_iso_valle", Vector3.zero, 230f));

            var capturas = raiz.AddComponent<CapturasReferencia>();
            capturas.vistas = vistas.ToArray();

            EditorSceneManager.SaveScene(escena, RutaComparacion, true);
            EditorSceneManager.OpenScene(RutaComparacion, OpenSceneMode.Single);
            SessionState.SetBool(CapturasReferencia.ClavePedidas, true);
            SessionState.SetBool(CapturasReferencia.ClaveHechas, false);
            SessionState.SetBool(ClaveVolver, true);
            SessionState.SetFloat(ClaveInicioCapturas, (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= VigilarCapturas;
            EditorApplication.update += VigilarCapturas;
            Debug.Log("Nostos: hago " + vistas.Count + " capturas de comparación jugando la escena " + RutaComparacion + "; vuelve sola a la del juego.");
            EditorApplication.isPlaying = true;
        }

        /// <summary>Mientras se juega para las capturas: al terminar (o si tarda demasiado) sale de Play.</summary>
        static void VigilarCapturas()
        {
            if (!SessionState.GetBool(CapturasReferencia.ClavePedidas, false))
            {
                EditorApplication.update -= VigilarCapturas;
                return;
            }
            if (!EditorApplication.isPlaying) return;
            bool hechas = SessionState.GetBool(CapturasReferencia.ClaveHechas, false);
            bool tarde = EditorApplication.timeSinceStartup - SessionState.GetFloat(ClaveInicioCapturas, 0f) > 150f;
            if (!hechas && !tarde) return;
            if (hechas) File.WriteAllText(MarcaCapturas, VersionCapturas);
            else Debug.LogWarning("Nostos: las capturas de comparación no terminaron a tiempo.");
            SessionState.SetBool(CapturasReferencia.ClavePedidas, false);
            EditorApplication.update -= VigilarCapturas;
            EditorApplication.isPlaying = false;
        }

        /// <summary>Al salir de Play tras las capturas, vuelve a la escena del juego.</summary>
        static bool VolverDeCapturas()
        {
            if (!SessionState.GetBool(ClaveVolver, false)) return false;
            if (SessionState.GetBool(CapturasReferencia.ClavePedidas, false)) return false;
            SessionState.SetBool(ClaveVolver, false);
            if (SceneManager.GetActiveScene().path == RutaComparacion) EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
            return true;
        }

        static GameObject PonerRoca(string nombre, Transform padre, Vector3 p, float giro, float hundir, MeshCollider suelo)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaCliffURP + "/" + nombre + ".prefab");
            if (modelo == null)
            {
                Debug.LogWarning("Nostos: no está " + CarpetaCliffURP + "/" + nombre + ".prefab.");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
            go.transform.rotation = Quaternion.Euler(0f, giro, 0f);
            Asentar(go, new Vector3(p.x, Altura(suelo, p), p.z));
            go.transform.position += Vector3.down * hundir;
            return go;
        }

        static float Altura(MeshCollider suelo, Vector3 p)
        {
            if (suelo != null && suelo.Raycast(new Ray(new Vector3(p.x, 300f, p.z), Vector3.down), out RaycastHit golpe, 600f))
                return golpe.point.y;
            return 0f;
        }

        /// <summary>El sitio más cercano a `preferido` sin nada en un cuadrado de lado 2·radio (salvo el suelo).</summary>
        static Vector3 SitioLibre(Vector3 preferido, float radio, Vector3 evitar, float distanciaMinima)
        {
            for (int anillo = 0; anillo < 14; anillo++)
            {
                int pasos = anillo == 0 ? 1 : 8 * anillo;
                for (int k = 0; k < pasos; k++)
                {
                    float ang = k * Mathf.PI * 2f / pasos;
                    Vector3 c = preferido + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (anillo * 2.5f);
                    if (new Vector2(c.x - evitar.x, c.z - evitar.z).magnitude < distanciaMinima) continue;
                    if (new Vector2(c.x, c.z).magnitude > 40f) continue;   // en el llano
                    bool libre = true;
                    foreach (Collider col in Physics.OverlapBox(c + Vector3.up * 3f, new Vector3(radio, 2.8f, radio)))
                    {
                        string n = col.gameObject.name;
                        if (n == "Suelo" || n == "Surco" || n.StartsWith("Tierra encima")) continue;
                        libre = false;
                        break;
                    }
                    if (libre) return c;
                }
            }
            return preferido;
        }

        static CapturasReferencia.Vista Vista(string nombre, Vector3 posicion, Vector3 mira, float campo)
        {
            return new CapturasReferencia.Vista { nombre = nombre, posicion = posicion, mira = mira, campo = campo };
        }

        static CapturasReferencia.Vista VistaCerca(string nombre, GameObject objeto, Vector3 desde, float distancia, MeshCollider suelo, float ojo)
        {
            Bounds b = Medir(objeto);
            Vector3 p = b.center + desde * (Mathf.Max(b.extents.x, b.extents.z) + distancia);
            p.y = Altura(suelo, p) + ojo;
            return Vista(nombre, p, b.center, 70f);
        }

        /// <summary>Como la cámara del constructor: 40° de inclinación, rumbo 45°, campo de 25°.</summary>
        static CapturasReferencia.Vista Iso(string nombre, Vector3 centro, float distancia)
        {
            Vector3 mira = Quaternion.Euler(40f, 45f, 0f) * Vector3.forward;
            return Vista(nombre, centro - mira * distancia, centro, 25f);
        }
    }
}
