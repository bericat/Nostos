using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos.EditorTools
{
    /// <summary>
    /// La herrería: la casa del herrero del paquete "3D The Blacksmith's House", tal cual, como
    /// edificio que se construye. La obra son la misma casa cortada a distintas alturas (la base,
    /// media pared, casi entera) y al terminarla queda completa, con el yunque como sitio de uso.
    /// </summary>
    public static partial class ConstructorEscena
    {
        const string RutaHerreria = "Assets/Holy Assets Games/3D The Blacksmith's House/Prefab/Smithy.prefab";
        public const int AnchoHerreria = 7, FondoHerreria = 5;
        static readonly float[] CortesHerreria = { 0.1f, 0.42f, 0.74f };

        static bool HayHerreria => AssetDatabase.LoadAssetAtPath<GameObject>(RutaHerreria) != null;

        /// <summary>Los cuatro estados de la obra, o null si falta el paquete.</summary>
        static GameObject[] ModelosHerreria()
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaHerreria);
            if (modelo == null) return null;
            HacerLegible(modelo);
            modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaHerreria);
            CarpetasNostos.Asegurar(CarpetaPrefabsPaquetes + "/Mallas");

            var modelos = new GameObject[4];
            for (int estado = 0; estado < 4; estado++)
            {
                GameObject raiz = CasaDelHerrero(modelo, estado == 3, out Transform yunque);
                if (estado < 3)
                {
                    Cortar(raiz, CortesHerreria[estado], "herreria_" + (estado + 1));
                }
                else if (yunque != null)
                {
                    // El yunque es el sitio de uso: ahí se forja.
                    Bounds b = Medir(yunque.gameObject);
                    var maquina = new GameObject("Maquina");
                    maquina.transform.SetParent(raiz.transform, false);
                    maquina.transform.position = b.center + Vector3.up * 0.2f;
                    var caja = maquina.AddComponent<BoxCollider>();
                    caja.size = b.size + new Vector3(0.5f, 0.6f, 0.5f);
                }
                foreach (MeshFilter f in raiz.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null || f.sharedMesh.triangles.Length == 0) continue;
                    f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
                }
                modelos[estado] = PrefabUtility.SaveAsPrefabAsset(raiz, CarpetaPrefabsPaquetes + "/herreria_" + (estado + 1) + ".prefab");
                if (estado == 3) MiniaturaHerreria(raiz);
                Object.DestroyImmediate(raiz);
            }
            return modelos;
        }

        /// <summary>
        /// La casa en su solar de 7 x 5 casillas: centrada, apoyada en el suelo y con el lado largo
        /// a lo ancho. Sin yunque, rueda ni cofre si no está terminada.
        /// </summary>
        static GameObject CasaDelHerrero(GameObject modelo, bool terminada, out Transform yunque)
        {
            var raiz = new GameObject("herreria");
            var casa = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            if (PrefabUtility.IsPartOfPrefabInstance(casa)) PrefabUtility.UnpackPrefabInstance(casa, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            casa.transform.SetParent(raiz.transform, false);
            foreach (Collider c in casa.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            yunque = BuscarHijo(casa.transform, "Anvil");
            if (!terminada)
            {
                foreach (string pieza in new[] { "Anvil", "Wheel", "Casket" })
                {
                    Transform t = BuscarHijo(casa.transform, pieza);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }
                yunque = null;
            }

            Bounds b = Medir(casa);
            if (b.size.z > b.size.x) casa.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * casa.transform.rotation;
            b = Medir(casa);
            float escala = Mathf.Min(AnchoHerreria * TamCelda / Mathf.Max(b.size.x, 0.1f), FondoHerreria * TamCelda / Mathf.Max(b.size.z, 0.1f)) * 0.98f;
            casa.transform.localScale *= escala;
            b = Medir(casa);
            casa.transform.position += new Vector3(-b.center.x, -b.min.y - 0.05f, -b.center.z);
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
            return raiz;
        }

        /// <summary>Quita los triángulos por encima de `parte` de la altura: la casa a medio levantar.</summary>
        static void Cortar(GameObject raiz, float parte, string nombre)
        {
            Bounds total = Medir(raiz);
            float corte = total.min.y + parte * total.size.y;
            int numero = 0;
            foreach (MeshFilter f in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh m = f.sharedMesh;
                if (m == null) continue;
                Vector3[] v = m.vertices;
                var mundo = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++) mundo[i] = f.transform.TransformPoint(v[i]);
                var cortada = Object.Instantiate(m);
                cortada.name = nombre + "_" + numero;
                for (int sub = 0; sub < m.subMeshCount; sub++)
                {
                    int[] t = m.GetTriangles(sub);
                    var quedan = new List<int>(t.Length);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        float y = (mundo[t[i]].y + mundo[t[i + 1]].y + mundo[t[i + 2]].y) / 3f;
                        if (y > corte) continue;
                        quedan.Add(t[i]);
                        quedan.Add(t[i + 1]);
                        quedan.Add(t[i + 2]);
                    }
                    cortada.SetTriangles(quedan, sub);
                }
                cortada.RecalculateBounds();
                string ruta = CarpetaPrefabsPaquetes + "/Mallas/" + nombre + "_" + (numero++) + ".asset";
                AssetDatabase.DeleteAsset(ruta);
                AssetDatabase.CreateAsset(cortada, ruta);
                f.sharedMesh = cortada;
            }
        }

        /// <summary>Miniatura del menú de construcción (256 x 256, fondo transparente), como las demás.</summary>
        static void MiniaturaHerreria(GameObject casa)
        {
            const string ruta = "Assets/Nostos/Resources/UI/Edificios/herreria.png";
            var copia = Object.Instantiate(casa);
            copia.transform.position = new Vector3(0f, -2000f, 0f);
            Bounds b = Medir(copia);

            var luzGo = new GameObject("Luz miniatura");
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.3f;
            luzGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            var camaraGo = new GameObject("Cámara miniatura");
            var camara = camaraGo.AddComponent<Camera>();
            camara.enabled = false;
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camara.fieldOfView = 26f;
            camara.nearClipPlane = 0.5f;
            camara.farClipPlane = 400f;
            Vector3 mira = Quaternion.Euler(30f, -38f, 0f) * Vector3.forward;
            float distancia = b.extents.magnitude / Mathf.Sin(camara.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            camaraGo.transform.SetPositionAndRotation(b.center - mira * distancia, Quaternion.LookRotation(mira));

            var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            camara.targetTexture = rt;
            try
            {
                camara.Render();
                RenderTexture previa = RenderTexture.active;
                RenderTexture.active = rt;
                var grande = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                grande.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                grande.Apply();
                RenderTexture.active = previa;
                // A 256 con la media de cada 2 x 2: bordes suaves.
                var chica = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                for (int y = 0; y < 256; y++)
                    for (int x = 0; x < 256; x++)
                    {
                        Color c = (grande.GetPixel(2 * x, 2 * y) + grande.GetPixel(2 * x + 1, 2 * y)
                                   + grande.GetPixel(2 * x, 2 * y + 1) + grande.GetPixel(2 * x + 1, 2 * y + 1)) * 0.25f;
                        chica.SetPixel(x, y, c);
                    }
                chica.Apply();
                CarpetasNostos.Asegurar("Assets/Nostos/Resources/UI/Edificios");
                File.WriteAllBytes(ruta, chica.EncodeToPNG());
                Object.DestroyImmediate(grande);
                Object.DestroyImmediate(chica);
                AssetDatabase.ImportAsset(ruta);
                var importador = AssetImporter.GetAtPath(ruta) as TextureImporter;
                if (importador != null && (!importador.alphaIsTransparency || importador.mipmapEnabled))
                {
                    importador.alphaIsTransparency = true;
                    importador.mipmapEnabled = false;
                    importador.SaveAndReimport();
                }
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("Nostos: no se pudo hacer la miniatura de la herrería. " + error.Message);
            }
            finally
            {
                camara.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camaraGo);
                Object.DestroyImmediate(luzGo);
                Object.DestroyImmediate(copia);
            }
        }
    }
}
