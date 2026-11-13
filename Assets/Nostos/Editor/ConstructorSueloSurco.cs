using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos.EditorTools
{
    /// <summary>
    /// El surco de la nave tiene fondo hundido, por debajo del suelo del valle. Para que se vea, el
    /// suelo se rehace en esa zona: los triángulos del valle bajo el surco (y una vuelta más) se
    /// cambian por los mismos triángulos partidos en trozos pequeños y bajados justo por debajo de
    /// la tierra del surco. El borde de la zona queda a la altura de siempre, sin costuras.
    /// </summary>
    public static partial class ConstructorEscena
    {
        const int PartesTriangulo = 10;
        const float BajoLaTierra = 0.08f;

        static MeshCollider colisionSurco;
        static readonly List<MeshCollider> montones = new List<MeshCollider>();

        /// <summary>Altura del suelo donde se ve, contando el surco (su tierra manda donde está).</summary>
        static float SueloConSurco(Valle valle, float x, float z)
        {
            float h = valle != null ? valle.Altura(x, z) : 0f;
            var rayo = new Ray(new Vector3(x, h + 30f, z), Vector3.down);
            float alto = h;
            if (colisionSurco != null && colisionSurco.Raycast(rayo, out RaycastHit golpe, 60f)) alto = golpe.point.y;
            // La tierra echada sobre la nave y las piezas va por encima.
            foreach (MeshCollider m in montones)
            {
                if (m != null && m.Raycast(rayo, out RaycastHit encima, 60f)) alto = Mathf.Max(alto, encima.point.y);
            }
            return alto;
        }

        static void HundirSueloBajoElSurco(MeshCollider surco)
        {
            colisionSurco = surco;
            GameObject valleGo = GameObject.Find("Valle");
            Transform sueloT = valleGo != null ? BuscarHijo(valleGo.transform, "Suelo") : null;
            MeshFilter filtro = sueloT != null ? sueloT.GetComponent<MeshFilter>() : null;
            if (filtro == null || filtro.sharedMesh == null || surco == null) return;
            Physics.SyncTransforms();

            Mesh original = filtro.sharedMesh;
            Transform t = filtro.transform;
            Vector3[] vLocal = original.vertices;
            Vector2[] uvs = original.uv;
            int[] tris = original.triangles;
            int nTri = tris.Length / 3;
            var w = new Vector3[vLocal.Length];
            for (int i = 0; i < vLocal.Length; i++) w[i] = t.TransformPoint(vLocal[i]);

            Bounds zona = surco.bounds;
            zona.Expand(new Vector3(8f, 100f, 8f));

            // 1. Triángulos con algún punto donde la tierra del surco queda por debajo del suelo.
            var hondo = new bool[nTri];
            var cerca = new List<int>();
            for (int k = 0; k < nTri; k++)
            {
                Vector3 a = w[tris[3 * k]], b = w[tris[3 * k + 1]], c = w[tris[3 * k + 2]];
                Vector3 centro = (a + b + c) / 3f;
                if (!zona.Contains(centro)) continue;
                cerca.Add(k);
                for (int i = 0; i <= PartesTriangulo && !hondo[k]; i++)
                    for (int j = 0; j <= PartesTriangulo - i; j++)
                    {
                        Vector3 p = Baricentro(a, b, c, i, j);
                        if (Hundido(surco, p, out _))
                        {
                            hondo[k] = true;
                            break;
                        }
                    }
            }

            // 2. Más una vuelta de vecinos (los que comparten un vértice): el borde queda a ras.
            var clave = new Dictionary<long, List<int>>();
            foreach (int k in cerca)
                for (int e = 0; e < 3; e++)
                {
                    long q = Clave(w[tris[3 * k + e]]);
                    if (!clave.TryGetValue(q, out List<int> lista)) clave[q] = lista = new List<int>();
                    lista.Add(k);
                }
            var rehacer = new HashSet<int>();
            foreach (int k in cerca)
            {
                if (!hondo[k]) continue;
                rehacer.Add(k);
                for (int e = 0; e < 3; e++)
                    foreach (int vecino in clave[Clave(w[tris[3 * k + e]])])
                        rehacer.Add(vecino);
            }
            if (rehacer.Count == 0) return;

            // Vértices que también tocan un triángulo que no se rehace: esos no se mueven.
            var fijos = new HashSet<long>();
            foreach (int k in cerca)
            {
                if (rehacer.Contains(k)) continue;
                for (int e = 0; e < 3; e++) fijos.Add(Clave(w[tris[3 * k + e]]));
            }
            // Aristas compartidas con un triángulo que no se rehace: tampoco se mueven sus puntos.
            var aristasFijas = new HashSet<(long, long)>();
            foreach (int k in cerca)
            {
                if (rehacer.Contains(k)) continue;
                for (int e = 0; e < 3; e++)
                {
                    long p = Clave(w[tris[3 * k + e]]), q = Clave(w[tris[3 * k + (e + 1) % 3]]);
                    aristasFijas.Add(p < q ? (p, q) : (q, p));
                }
            }

            // 3. Malla nueva: los triángulos que no se tocan, tal cual; los demás, partidos y bajados.
            var vertices = new List<Vector3>(vLocal.Length + rehacer.Count * PartesTriangulo * PartesTriangulo * 3);
            var nuevasUV = new List<Vector2>(vertices.Capacity);
            var indices = new List<int>(tris.Length + rehacer.Count * PartesTriangulo * PartesTriangulo * 3);
            bool hayUV = uvs.Length == vLocal.Length;
            for (int k = 0; k < nTri; k++)
            {
                if (rehacer.Contains(k)) continue;
                for (int e = 0; e < 3; e++)
                {
                    int v = tris[3 * k + e];
                    indices.Add(vertices.Count);
                    vertices.Add(vLocal[v]);
                    nuevasUV.Add(hayUV ? uvs[v] : Vector2.zero);
                }
            }
            var rejilla = new Vector3[(PartesTriangulo + 1) * (PartesTriangulo + 1)];
            foreach (int k in rehacer)
            {
                int ia = tris[3 * k], ib = tris[3 * k + 1], ic = tris[3 * k + 2];
                Vector3 a = w[ia], b = w[ib], c = w[ic];
                Vector2 uv = hayUV ? uvs[ia] : Vector2.zero;
                bool fijaAB = aristasFijas.Contains(Par(a, b)), fijaBC = aristasFijas.Contains(Par(b, c)), fijaCA = aristasFijas.Contains(Par(c, a));
                for (int i = 0; i <= PartesTriangulo; i++)
                    for (int j = 0; j <= PartesTriangulo - i; j++)
                    {
                        Vector3 p = Baricentro(a, b, c, i, j);
                        int resto = PartesTriangulo - i - j;
                        // i: peso de b, j: peso de c. En AB j = 0, en BC resto = 0, en CA i = 0.
                        bool fijo = (j == 0 && fijaAB) || (resto == 0 && fijaBC) || (i == 0 && fijaCA)
                                    || ((i == 0 && j == 0) || (i == PartesTriangulo) || (j == PartesTriangulo)) && fijos.Contains(Clave(p));
                        if (!fijo && Hundido(surco, p, out float tierra)) p.y = Mathf.Min(p.y, tierra - BajoLaTierra);
                        rejilla[i * (PartesTriangulo + 1) + j] = p;
                    }
                for (int i = 0; i < PartesTriangulo; i++)
                    for (int j = 0; j < PartesTriangulo - i; j++)
                    {
                        Vector3 p0 = rejilla[i * (PartesTriangulo + 1) + j];
                        Vector3 p1 = rejilla[(i + 1) * (PartesTriangulo + 1) + j];
                        Vector3 p2 = rejilla[i * (PartesTriangulo + 1) + j + 1];
                        Triangulo(vertices, nuevasUV, indices, t, p0, p1, p2, uv);
                        if (j < PartesTriangulo - i - 1)
                        {
                            Vector3 p3 = rejilla[(i + 1) * (PartesTriangulo + 1) + j + 1];
                            Triangulo(vertices, nuevasUV, indices, t, p1, p3, p2, uv);
                        }
                    }
            }

            var malla = new Mesh { name = "Suelo con surco", indexFormat = IndexFormat.UInt32 };
            malla.SetVertices(vertices);
            malla.SetUVs(0, nuevasUV);
            malla.SetTriangles(indices, 0);
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            string ruta = CarpetaPrefabsPaquetes + "/suelo_con_surco.asset";
            AssetDatabase.DeleteAsset(ruta);
            CarpetasNostos.Asegurar(CarpetaPrefabsPaquetes);
            AssetDatabase.CreateAsset(malla, ruta);
            filtro.sharedMesh = malla;
            foreach (MeshCollider mc in sueloT.GetComponents<MeshCollider>())
                if (mc.sharedMesh == original || mc.sharedMesh == null) mc.sharedMesh = malla;
            Physics.SyncTransforms();
            Debug.Log("Nostos: suelo hundido bajo el surco, " + rehacer.Count + " triángulos del valle rehechos.");
        }

        /// <summary>¿La tierra del surco queda por debajo del suelo en p? Devuelve su altura.</summary>
        static bool Hundido(MeshCollider surco, Vector3 p, out float tierra)
        {
            tierra = p.y;
            if (!surco.Raycast(new Ray(new Vector3(p.x, p.y + 20f, p.z), Vector3.down), out RaycastHit golpe, 40f)) return false;
            tierra = golpe.point.y;
            return tierra < p.y + 0.005f;
        }

        static Vector3 Baricentro(Vector3 a, Vector3 b, Vector3 c, int i, int j)
        {
            float u = i / (float)PartesTriangulo, v = j / (float)PartesTriangulo;
            return a + (b - a) * u + (c - a) * v;
        }

        static void Triangulo(List<Vector3> vertices, List<Vector2> uv, List<int> indices, Transform t, Vector3 a, Vector3 b, Vector3 c, Vector2 color)
        {
            // Mismo sentido que el original (a, b, c con i hacia b y j hacia c).
            foreach (Vector3 p in new[] { a, b, c })
            {
                indices.Add(vertices.Count);
                vertices.Add(t.InverseTransformPoint(p));
                uv.Add(color);
            }
        }

        static long Clave(Vector3 p)
        {
            long x = Mathf.RoundToInt(p.x * 100f), z = Mathf.RoundToInt(p.z * 100f);
            return (x << 32) ^ (z & 0xffffffffL);
        }

        static (long, long) Par(Vector3 a, Vector3 b)
        {
            long p = Clave(a), q = Clave(b);
            return p < q ? (p, q) : (q, p);
        }
    }
}
