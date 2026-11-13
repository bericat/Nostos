using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Hacha, pico y pala de "FREE Stylized Tools" y el pico de "Low Poly Stylized Pickaxe", tal
    /// cual, preparados para llevarlos en la mano: el modelo se mide y se gira para que el mango
    /// quede hacia abajo, la cabeza hacia arriba y el filo hacia delante, con el sitio donde se
    /// agarra en el origen.
    /// </summary>
    public static partial class ConstructorEscena
    {
        const string CarpetaHerramientas = CarpetaPrefabsPaquetes + "/Herramientas";

        /// <summary>Cómo mira la cabeza de la herramienta hacia delante.</summary>
        enum Frente
        {
            /// <summary>El lado al que sale la cabeza (el filo del hacha).</summary>
            LadoCabeza,
            /// <summary>A lo largo de la cabeza (las puntas del pico).</summary>
            LargoCabeza,
            /// <summary>La cara de la hoja (la pala).</summary>
            CaraHoja
        }

        /// <summary>Las cuatro herramientas en la mano, en el orden de Herramienta. Las que falten quedan a null.</summary>
        static GameObject[] HerramientasEnMano(Transform mano)
        {
            var prefabs = new[]
            {
                Empunadura("Assets/Stylized Tools/Prefabs/axe.prefab", "hacha", 0.62f, Frente.LadoCabeza),
                Empunadura("Assets/Stylized Tools/Prefabs/pickaxe.prefab", "pico", 0.66f, Frente.LargoCabeza),
                Empunadura("Assets/Stylized Tools/Prefabs/shovel.prefab", "pala", 0.9f, Frente.CaraHoja),
                Empunadura("Assets/Propsybrush/Pickaxe/Prefabs/Pickaxe.prefab", "pico_bueno", 0.7f, Frente.LargoCabeza),
            };
            var enMano = new GameObject[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                enMano[i] = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], mano);
                enMano[i].SetActive(false);
            }
            return enMano;
        }

        static GameObject Empunadura(string ruta, string nombre, float largo, Frente frente)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (modelo == null)
            {
                Debug.LogWarning("Nostos: falta " + ruta + "; " + nombre + " no se verá en la mano.");
                return null;
            }
            HacerLegible(modelo);
            modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);

            var copia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            if (PrefabUtility.IsPartOfPrefabInstance(copia)) PrefabUtility.UnpackPrefabInstance(copia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copia.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copia.transform.localScale = Vector3.one;

            // Fuera las cajas de colisión (UCX) y los colisionadores: en la mano no chocan con nada.
            foreach (Collider c in copia.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Transform t in copia.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == copia.transform) continue;
                var r = t.GetComponent<Renderer>();
                if (t.name.StartsWith("UCX") || (r != null && !r.enabled)) Object.DestroyImmediate(t.gameObject);
            }

            var puntos = new List<Vector3>();
            foreach (MeshFilter f in copia.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                foreach (Vector3 v in f.sharedMesh.vertices) puntos.Add(copia.transform.InverseTransformPoint(f.transform.TransformPoint(v)));
            }
            if (puntos.Count < 8)
            {
                Object.DestroyImmediate(copia);
                return null;
            }

            Medir(puntos, frente, out Vector3 agarre, out Vector3 haciaCabeza, out Vector3 delante, out float largoModelo);
            Quaternion giro = Quaternion.Inverse(Quaternion.LookRotation(delante, haciaCabeza));
            float escala = largo / Mathf.Max(0.001f, largoModelo);

            var raiz = new GameObject("mano_" + nombre);
            copia.transform.SetParent(raiz.transform, false);
            copia.transform.localRotation = giro;
            copia.transform.localScale = Vector3.one * escala;
            copia.transform.localPosition = -(giro * (agarre * escala));
            foreach (Renderer r in copia.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
            }

            CarpetasNostos.Asegurar(CarpetaHerramientas);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, CarpetaHerramientas + "/mano_" + nombre + ".prefab");
            Object.DestroyImmediate(raiz);
            Debug.Log("Nostos: " + nombre + " en la mano, " + largoModelo.ToString("0.00") + " m en el modelo, escala " + escala.ToString("0.00") + ".");
            return prefab;
        }

        static void HacerLegible(GameObject modelo)
        {
            foreach (MeshFilter f in modelo.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || f.sharedMesh.isReadable) continue;
                var importador = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(f.sharedMesh)) as ModelImporter;
                if (importador == null || importador.isReadable) continue;
                importador.isReadable = true;
                importador.SaveAndReimport();
            }
        }

        /// <summary>
        /// El mango es el eje en el que un extremo es estrecho (la empuñadura) y el otro ancho (la
        /// cabeza). Da el sitio del agarre, la dirección hacia la cabeza, hacia dónde mira el filo y
        /// el largo, todo en los ejes del modelo.
        /// </summary>
        static void Medir(List<Vector3> puntos, Frente frente, out Vector3 agarre, out Vector3 haciaCabeza, out Vector3 delante, out float largo)
        {
            var caja = new Bounds(puntos[0], Vector3.zero);
            foreach (Vector3 p in puntos) caja.Encapsulate(p);
            Vector3 tam = caja.size;
            float mayor = Mathf.Max(tam.x, tam.y, tam.z);

            int eje = 0;
            float mejor = -1f;
            bool cabezaArriba = true;
            for (int a = 0; a < 3; a++)
            {
                if (tam[a] < 0.5f * mayor) continue;
                float bajo = Anchura(puntos, caja, a, 0f, 0.2f);
                float alto = Anchura(puntos, caja, a, 0.8f, 1f);
                float razon = Mathf.Max(bajo, alto) / Mathf.Max(Mathf.Min(bajo, alto), 1e-4f);
                if (razon > mejor)
                {
                    mejor = razon;
                    eje = a;
                    cabezaArriba = alto > bajo;
                }
            }
            int p1 = (eje + 1) % 3, p2 = (eje + 2) % 3;
            largo = tam[eje];

            // Centro del mango, en el extremo estrecho.
            float d0 = cabezaArriba ? 0f : 0.8f, d1 = cabezaArriba ? 0.2f : 1f;
            Vector3 centroMango = Media(puntos, caja, eje, d0, d1);
            // La cabeza: el cuarto ancho.
            float c0 = cabezaArriba ? 0.75f : 0f, c1 = cabezaArriba ? 1f : 0.25f;
            Bounds cabeza = Caja(puntos, caja, eje, c0, c1);
            Vector3 centroCabeza = Media(puntos, caja, eje, c0, c1);

            int largoCabeza = cabeza.size[p1] >= cabeza.size[p2] ? p1 : p2;
            int finoCabeza = largoCabeza == p1 ? p2 : p1;
            delante = Vector3.zero;
            switch (frente)
            {
                case Frente.LadoCabeza:
                    delante[largoCabeza] = centroCabeza[largoCabeza] >= centroMango[largoCabeza] ? 1f : -1f;
                    break;
                case Frente.LargoCabeza:
                    delante[largoCabeza] = 1f;
                    break;
                default:
                    delante[finoCabeza] = 1f;
                    break;
            }
            haciaCabeza = Vector3.zero;
            haciaCabeza[eje] = cabezaArriba ? 1f : -1f;

            agarre = centroMango;
            agarre[eje] = cabezaArriba ? caja.min[eje] + 0.14f * largo : caja.max[eje] - 0.14f * largo;
        }

        static IEnumerable<Vector3> Tramo(List<Vector3> puntos, Bounds caja, int eje, float desde, float hasta)
        {
            float lado = Mathf.Max(caja.size[eje], 1e-5f);
            foreach (Vector3 p in puntos)
            {
                float t = (p[eje] - caja.min[eje]) / lado;
                if (t >= desde && t <= hasta) yield return p;
            }
        }

        static Bounds Caja(List<Vector3> puntos, Bounds todo, int eje, float desde, float hasta)
        {
            bool hay = false;
            var caja = new Bounds();
            foreach (Vector3 p in Tramo(puntos, todo, eje, desde, hasta))
            {
                if (!hay) caja = new Bounds(p, Vector3.zero);
                else caja.Encapsulate(p);
                hay = true;
            }
            return caja;
        }

        static Vector3 Media(List<Vector3> puntos, Bounds todo, int eje, float desde, float hasta)
        {
            Vector3 suma = Vector3.zero;
            int n = 0;
            foreach (Vector3 p in Tramo(puntos, todo, eje, desde, hasta))
            {
                suma += p;
                n++;
            }
            return n > 0 ? suma / n : todo.center;
        }

        /// <summary>Lo ancho que es el modelo en un tramo del eje (el mayor de los otros dos lados).</summary>
        static float Anchura(List<Vector3> puntos, Bounds todo, int eje, float desde, float hasta)
        {
            Bounds b = Caja(puntos, todo, eje, desde, hasta);
            int p1 = (eje + 1) % 3, p2 = (eje + 2) % 3;
            return Mathf.Max(b.size[p1], b.size[p2]);
        }
    }
}
