using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Acantilados con las rocas de CliffStonePackM (fase 1 de la biblia artística): las paredes de
    /// las montañas del valle eran facetas lisas de color malva. Ahora llevan encima, medio metidas
    /// en la pared, las cinco rocas del paquete (acabado semiestilizado gris y marrón) a varias
    /// escalas y giros, como bloques y estratos. Lo que queda a la vista entre ellas se pinta con
    /// piedra (SueloValle, triplanar).
    /// </summary>
    public static partial class ConstructorEscena
    {
        /// <summary>Una roca por celda de este lado (m) como mucho.</summary>
        const float EspacioAcantilado = 7f;
        const int MaximoAcantilados = 1400;

        static void AcantiladosDelValle()
        {
            if (!HayCliff) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaCliffURP + "/SM_Cliff_M1_SS_Grey.prefab") == null) CliffStoneURP();
            var modelos = new List<GameObject>();
            foreach (string color in new[] { "Grey", "Grey", "Brown" })
                for (int i = 1; i <= 5; i++)
                {
                    var m = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaCliffURP + "/SM_Cliff_M" + i + "_SS_" + color + ".prefab");
                    if (m != null) modelos.Add(m);
                }
            GameObject valle = GameObject.Find("Valle");
            Transform sueloT = valle != null ? BuscarHijo(valle.transform, "Suelo") : null;
            MeshFilter filtro = sueloT != null ? sueloT.GetComponent<MeshFilter>() : null;
            if (modelos.Count == 0 || filtro == null || filtro.sharedMesh == null || !File.Exists(ImportadorKit.RutaTextura)) return;

            var paleta = new Texture2D(2, 2);
            paleta.LoadImage(File.ReadAllBytes(ImportadorKit.RutaTextura));
            Color32[] pixeles = paleta.GetPixels32();
            int anchoP = paleta.width, altoP = paleta.height;
            Object.DestroyImmediate(paleta);

            Mesh malla = filtro.sharedMesh;
            Vector3[] v = malla.vertices;
            Vector2[] uvs = malla.uv;
            int[] t = malla.triangles;
            Matrix4x4 mundo = filtro.transform.localToWorldMatrix;
            var azar = new System.Random(71);
            var ocupadas = new HashSet<long>();
            var raiz = new GameObject("Acantilados (CliffStonePackM)");
            raiz.isStatic = true;
            int puestas = 0;

            // Caras de roca en orden al azar, para no llenar primero un lado del valle.
            var orden = new List<int>();
            for (int i = 0; i < t.Length; i += 3) orden.Add(i);
            for (int i = orden.Count - 1; i > 0; i--)
            {
                int j = azar.Next(i + 1);
                (orden[i], orden[j]) = (orden[j], orden[i]);
            }
            foreach (int i in orden)
            {
                if (puestas >= MaximoAcantilados) break;
                Vector2 q = uvs[t[i]];
                Color32 c = pixeles[Mathf.Clamp((int)(q.y * altoP), 0, altoP - 1) * anchoP + Mathf.Clamp((int)(q.x * anchoP), 0, anchoP - 1)];
                if (!Parecido(c, 160, 142, 150) && !Parecido(c, 116, 102, 122)) continue;
                Vector3 p0 = mundo.MultiplyPoint3x4(v[t[i]]), p1 = mundo.MultiplyPoint3x4(v[t[i + 1]]), p2 = mundo.MultiplyPoint3x4(v[t[i + 2]]);
                Vector3 centro = (p0 + p1 + p2) / 3f;
                if (new Vector2(centro.x, centro.z).magnitude < 50f) continue;      // lejos del llano edificable
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                if (normal.y < 0f) normal = -normal;
                long clave = ClaveAcantilado(centro, EspacioAcantilado);
                if (ocupadas.Contains(clave)) continue;
                ocupadas.Add(clave);

                GameObject modelo = modelos[azar.Next(modelos.Count)];
                var roca = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
                float escala = Mathf.Lerp(1.4f, 2.6f, (float)azar.NextDouble());
                if (centro.y > 60f) escala *= 1.4f;   // las cumbres, con bloques más grandes
                roca.transform.localScale = Vector3.one * escala;
                // Hacia fuera de la pared, con el arriba casi vertical y un poco de vaivén.
                Vector3 fuera = new Vector3(normal.x, 0f, normal.z);
                if (fuera.sqrMagnitude < 0.01f) fuera = new Vector3((float)azar.NextDouble() - 0.5f, 0f, (float)azar.NextDouble() - 0.5f);
                fuera.Normalize();
                float giro = Mathf.Atan2(fuera.x, fuera.z) * Mathf.Rad2Deg + Mathf.Lerp(-35f, 35f, (float)azar.NextDouble());
                float vaiven = Mathf.Lerp(-8f, 8f, (float)azar.NextDouble());
                roca.transform.rotation = Quaternion.Euler(vaiven, giro, Mathf.Lerp(-6f, 6f, (float)azar.NextDouble()));
                foreach (Collider col in roca.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                // Medio metida en la pared: su centro detrás de la cara y un poco por debajo.
                Bounds b = Medir(roca);
                float hondo = Mathf.Min(b.extents.x, b.extents.z) * 0.55f;
                roca.transform.position += centro - fuera * hondo - Vector3.up * b.extents.y * 0.25f - b.center;
                foreach (Renderer r in roca.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = escala > 2f ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.gameObject.isStatic = true;
                }
                roca.isStatic = true;
                puestas++;
            }
            Debug.Log("Nostos: " + puestas + " rocas de CliffStonePackM en las paredes de las montañas.");
        }

        static long ClaveAcantilado(Vector3 p, float lado)
        {
            long x = Mathf.FloorToInt(p.x / lado) + 1000, y = Mathf.FloorToInt(p.y / lado) + 1000, z = Mathf.FloorToInt(p.z / lado) + 1000;
            return (x * 4000L + y) * 4000L + z;
        }
    }
}
