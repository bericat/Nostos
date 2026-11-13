using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Tierra echada encima de lo que se estrelló: alrededor del casco y de cada pieza arrancada se
    /// levanta un montón de tierra removida que se arrima a la pieza (sin huecos debajo, metiéndose
    /// bajo los salientes hasta tocarlos), cubre sus bordes y se pierde en la hierba con un borde
    /// irregular. El borde del montón queda por debajo del suelo: no se ve ningún corte.
    /// </summary>
    public static partial class ConstructorEscena
    {
        /// <param name="alto">Lo que sube la tierra arrimada a la pieza (m).</param>
        /// <param name="ancho">Hasta dónde llega el montón desde la pieza (m).</param>
        /// <param name="quema">Cuánto se quemó la tierra (0..1).</param>
        /// <param name="morro">Punta que quedó clavada: la tierra la cubre en cono y la pieza sale de ella.</param>
        static GameObject Enterrar(IEnumerable<GameObject> piezas, Valle valle, ColorTerreno colores, Transform padre,
            string nombre, float alto, float ancho, float quema, Vector3? morro = null)
        {
            var lista = new List<GameObject>(piezas);
            if (lista.Count == 0) return null;
            bool caras = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            PrepararCasco(lista);
            try
            {
                if (cascoProvisional.Count == 0) return null;
                Bounds caja = zonaCasco;
                caja.Expand(new Vector3(-8f, 0f, -8f));   // PrepararCasco la agranda 4 m por lado
                float margen = ancho * 1.35f + 0.6f;
                float minX = caja.min.x - margen, minZ = caja.min.z - margen;
                float lx = caja.size.x + 2f * margen, lz = caja.size.z + 2f * margen;
                float paso = Mathf.Max(0.22f, Mathf.Max(lx, lz) / 150f);
                int nx = Mathf.CeilToInt(lx / paso) + 1, nz = Mathf.CeilToInt(lz / paso) + 1;
                int n = nx * nz;

                // Suelo, panza (por debajo) y lomo (por encima) de la pieza en cada punto.
                var suelo = new float[n];
                var panza = new float[n];
                var lomo = new float[n];
                var dist = new float[n];
                const float Lejos = 1e6f;
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        int k = iz * nx + ix;
                        float x = minX + ix * paso, z = minZ + iz * paso;
                        suelo[k] = SueloConSurco(valle, x, z);
                        panza[k] = BajoDelCasco(x, z);
                        lomo[k] = EncimaDelCasco(x, z);
                        // Toca la tierra donde la panza está a ras de suelo o por debajo.
                        bool toca = !float.IsNaN(panza[k]) && panza[k] < suelo[k] + alto + 0.15f;
                        dist[k] = toca ? 0f : Lejos;
                    }
                Distancias(dist, nx, nz, paso);

                float semilla = Mathf.Repeat(caja.center.x * 0.37f + caja.center.z * 0.91f, 100f) + 5f;
                var vertices = new List<Vector3>(n);
                var colorV = new List<Color32>(n);
                var uv = new List<Vector2>(n);
                var quemado = new List<Vector2>(n);
                var vivo = new bool[n];
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        int k = iz * nx + ix;
                        float x = minX + ix * paso, z = minZ + iz * paso;
                        // Montón irregular: más largo por unos lados, con terrones y regueros.
                        float n1 = Mathf.PerlinNoise(x * 0.55f + semilla, z * 0.55f - semilla);
                        float n2 = Mathf.PerlinNoise(x * 1.4f - semilla, z * 1.4f + 3.1f);
                        float terron = Mathf.PerlinNoise(x * 3.7f + 9.2f, z * 3.7f + semilla) - 0.5f;
                        float radio = ancho * (0.6f + 0.8f * n1);
                        float s = dist[k] / Mathf.Max(0.05f, radio);
                        float h = alto * (0.75f + 0.5f * n2);
                        float y;
                        float tierra;
                        if (s >= 1f)
                        {
                            y = -0.07f;
                            tierra = 0f;
                        }
                        else
                        {
                            float perfil = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * Mathf.Pow(s, 0.85f));
                            y = h * perfil + terron * 0.07f * (1f - s) - 0.07f * Mathf.SmoothStep(0.55f, 1f, s);
                            tierra = 1f - Mathf.SmoothStep(0.45f, 0.95f, s);
                            vivo[k] = true;
                        }
                        float altura = suelo[k] + y;
                        // Bajo un saliente la tierra llega justo a tocarlo; encima de una pieza
                        // plana, sin taparla del todo.
                        if (!float.IsNaN(panza[k]) && panza[k] > suelo[k]) altura = Mathf.Min(altura, panza[k] - 0.02f);
                        if (!float.IsNaN(lomo[k]) && dist[k] == 0f) altura = Mathf.Min(altura, Mathf.Max(suelo[k] - 0.07f, lomo[k] - 0.04f));
                        if (morro.HasValue)
                        {
                            // La punta clavada: un montón en cono por encima de ella, con terrones.
                            Vector3 m = morro.Value;
                            float r = new Vector2(x - m.x, z - m.z).magnitude * (0.85f + 0.3f * n1);
                            float cono = m.y + 0.45f - 0.48f * r + terron * 0.08f;
                            if (cono > altura)
                            {
                                altura = cono;
                                tierra = 1f;
                                vivo[k] = true;
                            }
                        }
                        vertices.Add(new Vector3(x, altura, z));
                        Color32 c = colores.En(x, z);
                        c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(tierra) * 255f);
                        colorV.Add(c);
                        uv.Add(new Vector2((x + z) * 0.7071f / 6f, (z - x) * 0.7071f / 6f));
                        float cerca = 1f - Mathf.Clamp01(s);
                        quemado.Add(new Vector2(quema * cerca * cerca, 0f));
                    }

                var triangulos = new List<int>();
                for (int iz = 0; iz < nz - 1; iz++)
                    for (int ix = 0; ix < nx - 1; ix++)
                    {
                        int a = iz * nx + ix, b = a + 1, c = a + nx, d = c + 1;
                        if (!vivo[a] && !vivo[b] && !vivo[c] && !vivo[d]) continue;
                        triangulos.Add(a); triangulos.Add(c); triangulos.Add(b);
                        triangulos.Add(b); triangulos.Add(c); triangulos.Add(d);
                    }
                if (triangulos.Count == 0) return null;

                var malla = new Mesh { name = "Montón " + nombre };
                if (n > 65000) malla.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                malla.SetVertices(vertices);
                malla.SetColors(colorV);
                malla.SetUVs(0, uv);
                malla.SetUVs(1, quemado);
                malla.SetTriangles(triangulos, 0);
                malla.RecalculateNormals();
                malla.RecalculateBounds();
                string ruta = CarpetaPrefabsPaquetes + "/monton_" + nombre + ".asset";
                AssetDatabase.DeleteAsset(ruta);
                AssetDatabase.CreateAsset(malla, ruta);

                var go = new GameObject("Tierra encima: " + nombre);
                go.transform.SetParent(padre, true);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = malla;
                go.AddComponent<MeshRenderer>().sharedMaterial = CargarOCrear("Surco", "Nostos/TierraRemovida");
                var colision = go.AddComponent<MeshCollider>();
                colision.sharedMesh = malla;
                montones.Add(colision);
                libres.Add(new Vector3(caja.center.x, Mathf.Max(caja.extents.x, caja.extents.z) + ancho, caja.center.z));
                return go;
            }
            finally
            {
                QuitarCasco();
                Physics.SyncTransforms();
                Physics.queriesHitBackfaces = caras;
            }
        }

        /// <summary>Altura de lo más alto de la pieza en (x, z), mirando desde arriba; NaN si no hay.</summary>
        static float EncimaDelCasco(float x, float z)
        {
            var rayo = new Ray(new Vector3(x, zonaCasco.max.y + 3f, z), Vector3.down);
            float mejor = float.NaN;
            foreach (MeshCollider mc in cascoProvisional)
            {
                if (mc == null || !mc.Raycast(rayo, out RaycastHit golpe, zonaCasco.size.y + 6f)) continue;
                if (float.IsNaN(mejor) || golpe.point.y > mejor) mejor = golpe.point.y;
            }
            return mejor;
        }

        /// <summary>Distancia (m) de cada punto de la rejilla al más cercano con valor 0 (chaflán 3-4).</summary>
        static void Distancias(float[] d, int nx, int nz, float paso)
        {
            float recto = paso, diagonal = paso * 1.4142f;
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    int k = iz * nx + ix;
                    float v = d[k];
                    if (ix > 0) v = Mathf.Min(v, d[k - 1] + recto);
                    if (iz > 0)
                    {
                        v = Mathf.Min(v, d[k - nx] + recto);
                        if (ix > 0) v = Mathf.Min(v, d[k - nx - 1] + diagonal);
                        if (ix < nx - 1) v = Mathf.Min(v, d[k - nx + 1] + diagonal);
                    }
                    d[k] = v;
                }
            for (int iz = nz - 1; iz >= 0; iz--)
                for (int ix = nx - 1; ix >= 0; ix--)
                {
                    int k = iz * nx + ix;
                    float v = d[k];
                    if (ix < nx - 1) v = Mathf.Min(v, d[k + 1] + recto);
                    if (iz < nz - 1)
                    {
                        v = Mathf.Min(v, d[k + nx] + recto);
                        if (ix < nx - 1) v = Mathf.Min(v, d[k + nx + 1] + diagonal);
                        if (ix > 0) v = Mathf.Min(v, d[k + nx - 1] + diagonal);
                    }
                    d[k] = v;
                }
        }
    }
}
