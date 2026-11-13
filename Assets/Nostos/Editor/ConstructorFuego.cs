using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Fuego de verdad (paquete "Free Pack - Fire Effects", el rojo) en lugar de las llamas de color
    /// plano de los modelos propios: hogares de las casas, hornos y fraguas, la olla del comedor, la
    /// hoguera de la plaza y las antorchas del túnel. Las caras de color "fuego" de la paleta se
    /// quitan de la malla y en su sitio se pone el fuego del paquete con una luz que parpadea.
    /// </summary>
    public static partial class ConstructorEscena
    {
        static readonly Color32 ColorFuego = new Color32(255, 150, 60, 255);
        static int filaFuego = -2;
        static int filasPaleta;

        /// <summary>Fila de la paleta del color "fuego" (8 tonos por fila), o -1 si no está.</summary>
        static int FilaFuego()
        {
            if (filaFuego != -2) return filaFuego;
            filaFuego = -1;
            if (!File.Exists(ImportadorKit.RutaTextura)) return filaFuego;
            var paleta = new Texture2D(2, 2);
            paleta.LoadImage(File.ReadAllBytes(ImportadorKit.RutaTextura));
            int ancho = paleta.width, alto = paleta.height;
            int lado = Mathf.Max(1, ancho / 8);   // cada muestra es un cuadrado de ancho/8 píxeles
            filasPaleta = alto / lado;
            Color32[] pixeles = paleta.GetPixels32();
            Object.DestroyImmediate(paleta);
            for (int fila = 0; fila < filasPaleta; fila++)
            {
                // La fila 0 es la de arriba de la imagen; la columna 3 es el color exacto.
                int y = alto - 1 - (fila * lado + lado / 2);
                Color32 c = pixeles[y * ancho + 3 * lado + lado / 2];
                if (Mathf.Abs(c.r - ColorFuego.r) + Mathf.Abs(c.g - ColorFuego.g) + Mathf.Abs(c.b - ColorFuego.b) < 6)
                {
                    filaFuego = fila;
                    break;
                }
            }
            return filaFuego;
        }

        /// <summary>Coordenada de paleta (columna 3, la del color exacto) del color más parecido a `c`.</summary>
        static Vector2 UvPaleta(Color32 c)
        {
            if (!File.Exists(ImportadorKit.RutaTextura)) return new Vector2(0.4375f, 0.5f);
            var paleta = new Texture2D(2, 2);
            paleta.LoadImage(File.ReadAllBytes(ImportadorKit.RutaTextura));
            int ancho = paleta.width, alto = paleta.height;
            int lado = Mathf.Max(1, ancho / 8);
            int filas = alto / lado;
            Color32[] pixeles = paleta.GetPixels32();
            Object.DestroyImmediate(paleta);
            int mejor = 0, menor = int.MaxValue;
            for (int fila = 0; fila < filas; fila++)
            {
                int y = alto - 1 - (fila * lado + lado / 2);
                Color32 p = pixeles[y * ancho + 3 * lado + lado / 2];
                int d = Mathf.Abs(p.r - c.r) + Mathf.Abs(p.g - c.g) + Mathf.Abs(p.b - c.b);
                if (d < menor)
                {
                    menor = d;
                    mejor = fila;
                }
            }
            return new Vector2(3.5f / 8f, 1f - (mejor + 0.5f) / filas);
        }

        static bool EsCaraDeFuego(Vector2 uv)
        {
            int fila = FilaFuego();
            if (fila < 0 || filasPaleta <= 0) return false;
            return Mathf.Clamp(Mathf.FloorToInt((1f - uv.y) * filasPaleta), 0, filasPaleta - 1) == fila;
        }

        /// <summary>
        /// Copia del modelo con fuego del paquete donde tenía caras de color fuego. Devuelve el
        /// modelo tal cual si no tiene ninguna o si falta el paquete.
        /// </summary>
        static GameObject ConFuego(GameObject modelo, string nombre, bool conLuz = true)
        {
            if (modelo == null || AssetDatabase.LoadAssetAtPath<GameObject>(FuegoRojo) == null || FilaFuego() < 0) return modelo;

            // ¿Tiene caras de fuego? Se mira antes de copiar nada.
            bool tiene = false;
            foreach (MeshFilter f in modelo.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh m = f.sharedMesh;
                if (m == null) continue;
                Vector2[] uv = m.uv;
                int[] t = m.triangles;
                for (int i = 0; i < t.Length && !tiene; i += 3) tiene = uv.Length > 0 && EsCaraDeFuego(uv[t[i]]);
                if (tiene) break;
            }
            if (!tiene) return modelo;

            var copia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            if (PrefabUtility.IsPartOfPrefabInstance(copia)) PrefabUtility.UnpackPrefabInstance(copia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copia.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            CarpetasNostos.Asegurar(CarpetaPrefabsPaquetes + "/Mallas");

            var centros = new List<Vector3>();
            int numero = 0;
            foreach (MeshFilter f in copia.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh m = f.sharedMesh;
                if (m == null) continue;
                Vector2[] uv = m.uv;
                Vector3[] v = m.vertices;
                if (uv.Length == 0) continue;
                var limpia = Object.Instantiate(m);
                limpia.name = m.name;
                bool cambio = false;
                for (int sub = 0; sub < m.subMeshCount; sub++)
                {
                    int[] t = m.GetTriangles(sub);
                    var quedan = new List<int>(t.Length);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        if (EsCaraDeFuego(uv[t[i]]))
                        {
                            Vector3 c = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                            centros.Add(copia.transform.InverseTransformPoint(f.transform.TransformPoint(c)));
                            cambio = true;
                            continue;
                        }
                        quedan.Add(t[i]);
                        quedan.Add(t[i + 1]);
                        quedan.Add(t[i + 2]);
                    }
                    limpia.SetTriangles(quedan, sub);
                }
                if (!cambio)
                {
                    Object.DestroyImmediate(limpia);
                    continue;
                }
                limpia.RecalculateBounds();
                AssetDatabase.CreateAsset(limpia, CarpetaPrefabsPaquetes + "/Mallas/" + nombre + "_" + (numero++) + ".asset");
                f.sharedMesh = limpia;
                var mc = f.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh == m) mc.sharedMesh = limpia;
            }

            // Las caras de fuego, agrupadas: cada grupo es un fuego (un hogar, un horno...).
            var grupos = new List<Bounds>();
            foreach (Vector3 c in centros)
            {
                int cual = -1;
                for (int g = 0; g < grupos.Count; g++)
                {
                    Bounds b = grupos[g];
                    b.Expand(1.4f);
                    if (b.Contains(c))
                    {
                        cual = g;
                        break;
                    }
                }
                if (cual < 0) grupos.Add(new Bounds(c, Vector3.zero));
                else
                {
                    Bounds b = grupos[cual];
                    b.Encapsulate(c);
                    grupos[cual] = b;
                }
            }
            foreach (Bounds b in grupos)
            {
                float tam = Mathf.Max(b.size.x, b.size.z, 0.3f);
                float escala = Mathf.Clamp(tam / 1.1f, 0.25f, 1.1f);
                PonerLlama(copia.transform, new Vector3(b.center.x, b.min.y, b.center.z), escala,
                    conLuz ? 1.5f + 3f * escala : 0f, 3f + 5f * escala);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copia, CarpetaPrefabsPaquetes + "/" + nombre + "_fuego.prefab");
            Object.DestroyImmediate(copia);
            return prefab != null ? prefab : modelo;
        }

        /// <summary>Un fuego del paquete con su luz (si intensidad > 0), como hijo de `padre`.</summary>
        static Llama PonerLlama(Transform padre, Vector3 sitioLocal, float escala, float intensidad, float alcance)
        {
            var raiz = new GameObject("Fuego");
            raiz.transform.SetParent(padre, false);
            raiz.transform.localPosition = sitioLocal;
            var llama = raiz.AddComponent<Llama>();
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(FuegoRojo);
            if (modelo != null)
            {
                var fuego = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
                fuego.transform.localPosition = Vector3.zero;
                fuego.transform.localRotation = Quaternion.identity;
                fuego.transform.localScale = Vector3.one * escala;
                foreach (ParticleSystem s in fuego.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = s.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
                foreach (MeshRenderer r in fuego.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            }
            if (intensidad > 0f)
            {
                var luzGo = new GameObject("Luz");
                luzGo.transform.SetParent(raiz.transform, false);
                luzGo.transform.localPosition = Vector3.up * (0.5f * escala + 0.2f);
                var luz = luzGo.AddComponent<Light>();
                luz.type = LightType.Point;
                luz.color = new Color(1f, 0.58f, 0.26f);
                luz.intensity = intensidad;
                luz.range = alcance;
                luz.shadows = LightShadows.None;
                llama.luz = luz;
                llama.intensidad = intensidad;
            }
            return llama;
        }
    }
}
