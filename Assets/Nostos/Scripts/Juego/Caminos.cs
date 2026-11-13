using System.Collections.Generic;
using Nostos.Sim;
using Unity.AI.Navigation;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Dibuja los caminos que guarda la simulación y marca su trozo de NavMesh con un área propia,
    /// más barata, para que los colonos los prefieran. Andar por un camino es más rápido, tanto
    /// para los colonos como para Nicias. Con `material` (shader Nostos/Camino), el camino es una
    /// sola malla por trozo del valle que sigue el suelo, con el empedrado de la plaza y el borde
    /// roto contra la hierba; sin él, losas sueltas por celda como antes.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class Caminos : MonoBehaviour
    {
        /// <summary>Área del NavMesh para los caminos (las 0, 1 y 2 son las de Unity).</summary>
        public const int AreaCamino = 3;
        /// <summary>Coste de andar fuera del camino para los colonos (por camino cuesta 1).</summary>
        public const float CosteCampo = 1.8f;
        public const float VelocidadColonos = 1.6f;
        public const float VelocidadNicias = 1.3f;

        public Juego juego;
        [Tooltip("Modelos de losa de 2 × 2 m. Se elige uno por celda.")]
        public GameObject[] losas;
        public float altura = 0.01f;
        [Tooltip("Shader Nostos/Camino: empedrado continuo con el borde irregular.")]
        public Material material;
        [Tooltip("El suelo del valle, para que el camino siga su forma.")]
        public Collider suelo;
        [Tooltip("Coordenada de la paleta del color hierba, por si el suelo no da la suya.")]
        public Vector2 colorHierba = new Vector2(0.4375f, 0.5f);

        /// <summary>Partes por lado de cada celda en la malla del camino.</summary>
        const int Partes = 6;
        /// <summary>Hasta dónde sale la malla fuera de las celdas de camino (m): lo que el borde puede morder.</summary>
        public const float Margen = 1.5f;
        const int CeldasTrozo = 8;
        readonly Dictionary<Vector2Int, GameObject> trozos = new Dictionary<Vector2Int, GameObject>();

        readonly Dictionary<Celda, GameObject> visibles = new Dictionary<Celda, GameObject>();
        bool pendiente;

        void OnEnable()
        {
            pendiente = true;
        }

        void Update()
        {
            if (juego == null || juego.Mundo == null) return;
            if (!suscrito)
            {
                juego.Mundo.CaminosCambiados += Marcar;
                suscrito = true;
                pendiente = true;
            }
            if (!pendiente) return;
            pendiente = false;
            Sincronizar();
        }

        bool suscrito;

        void OnDisable()
        {
            if (suscrito && juego != null && juego.Mundo != null) juego.Mundo.CaminosCambiados -= Marcar;
            suscrito = false;
        }

        void Marcar()
        {
            pendiente = true;
        }

        /// <summary>
        /// Distancia (m) del punto a la celda de camino más cercana entre las vecinas de su celda, o
        /// infinito si no hay ninguna. 0 si está sobre un camino. La usa la hierba para clarear.
        /// </summary>
        public static float DistanciaACamino(Mundo mundo, float x, float z)
        {
            Rejilla rejilla = mundo.Rejilla;
            Celda celda = rejilla.CeldaDesdeMundo(x, z);
            if (mundo.EsCamino(celda)) return 0f;
            float lado = rejilla.TamCelda, mejor = float.PositiveInfinity;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var otra = new Celda(celda.X + dx, celda.Z + dz);
                    if (!mundo.EsCamino(otra)) continue;
                    rejilla.CentroDeCelda(otra, out float cx, out float cz);
                    float ax = Mathf.Max(Mathf.Abs(x - cx) - lado * 0.5f, 0f);
                    float az = Mathf.Max(Mathf.Abs(z - cz) - lado * 0.5f, 0f);
                    mejor = Mathf.Min(mejor, Mathf.Sqrt(ax * ax + az * az));
                }
            return mejor;
        }

        /// <summary>¿Está este punto del mundo sobre un camino?</summary>
        public static bool EnCamino(Vector3 punto)
        {
            Juego juego = Juego.Instancia;
            if (juego == null || juego.Mundo == null) return false;
            Celda celda = juego.Mundo.Rejilla.CeldaDesdeMundo(punto.x, punto.z);
            return juego.Mundo.EsCamino(celda);
        }

        void Sincronizar()
        {
            Mundo mundo = juego.Mundo;
            bool cambio = false;

            var sobrantes = new List<Celda>();
            foreach (KeyValuePair<Celda, GameObject> par in visibles)
            {
                if (!mundo.EsCamino(par.Key)) sobrantes.Add(par.Key);
            }
            foreach (Celda celda in sobrantes)
            {
                if (visibles[celda] != null) Destroy(visibles[celda]);
                visibles.Remove(celda);
                cambio = true;
            }

            var cambiadas = new List<Celda>(sobrantes);
            foreach (Celda celda in mundo.Caminos)
            {
                if (visibles.ContainsKey(celda)) continue;
                visibles.Add(celda, CrearLosa(celda));
                cambiadas.Add(celda);
                cambio = true;
            }

            if (material != null && cambiadas.Count > 0)
            {
                var sucios = new HashSet<Vector2Int>();
                foreach (Celda c in cambiadas)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                            sucios.Add(Trozo(c.X + dx, c.Z + dz));
                foreach (Vector2Int t in sucios) RehacerTrozo(t);
            }

            // El NavMesh se rehace con calma (Navegacion espera medio segundo y agrupa avisos).
            if (cambio) juego.NotificarEdificiosCambiados();
        }

        GameObject CrearLosa(Celda celda)
        {
            Rejilla rejilla = juego.Mundo.Rejilla;
            rejilla.CentroDeCelda(celda, out float x, out float z);
            int azar = Mezcla(celda.X, celda.Z);

            var losa = new GameObject("Camino " + celda.X + "," + celda.Z);
            losa.transform.SetParent(transform, false);
            losa.transform.position = new Vector3(x, altura, z);
            losa.transform.rotation = Quaternion.Euler(0f, 90f * (azar & 3), 0f);

            if (material == null && losas != null && losas.Length > 0)
            {
                GameObject modelo = losas[(azar >> 2) % losas.Length];
                if (modelo != null)
                {
                    GameObject copia = Instantiate(modelo, losa.transform, false);
                    foreach (Collider c in copia.GetComponentsInChildren<Collider>())
                    {
                        c.enabled = false;
                        Destroy(c);
                    }
                    foreach (Renderer r in copia.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }

            var volumen = losa.AddComponent<NavMeshModifierVolume>();
            float lado = rejilla.TamCelda;
            volumen.size = new Vector3(lado, 2f, lado);
            volumen.center = new Vector3(0f, 0.5f, 0f);
            volumen.area = AreaCamino;
            return losa;
        }

        // ---------------------------------------------------------------- malla continua

        static Vector2Int Trozo(int x, int z)
        {
            return new Vector2Int(Mathf.FloorToInt(x / (float)CeldasTrozo), Mathf.FloorToInt(z / (float)CeldasTrozo));
        }

        void RehacerTrozo(Vector2Int clave)
        {
            if (trozos.TryGetValue(clave, out GameObject viejo) && viejo != null)
            {
                Mesh vieja = viejo.GetComponent<MeshFilter>().sharedMesh;
                Destroy(viejo);
                if (vieja != null) Destroy(vieja);
            }
            trozos.Remove(clave);

            Mundo mundo = juego.Mundo;
            Rejilla rejilla = mundo.Rejilla;
            float lado = rejilla.TamCelda;
            var vertices = new List<Vector3>();
            var normales = new List<Vector3>();
            var dentro = new List<Vector2>();
            var paleta = new List<Vector2>();
            var indices = new List<int>();
            var fila = new int[(Partes + 1) * (Partes + 1)];
            var cerca = new bool[3, 3];

            for (int cx = clave.x * CeldasTrozo; cx < (clave.x + 1) * CeldasTrozo; cx++)
                for (int cz = clave.y * CeldasTrozo; cz < (clave.y + 1) * CeldasTrozo; cz++)
                {
                    bool alguno = false;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            cerca[dx + 1, dz + 1] = mundo.EsCamino(new Celda(cx + dx, cz + dz));
                            alguno |= cerca[dx + 1, dz + 1];
                        }
                    if (!alguno) continue;
                    bool esCamino = cerca[1, 1];
                    rejilla.CentroDeCelda(new Celda(cx, cz), out float x0, out float z0);
                    x0 -= lado * 0.5f;
                    z0 -= lado * 0.5f;

                    for (int k = 0; k < fila.Length; k++) fila[k] = -1;
                    for (int i = 0; i < Partes; i++)
                        for (int j = 0; j < Partes; j++)
                        {
                            float mx = x0 + (i + 0.5f) * lado / Partes, mz = z0 + (j + 0.5f) * lado / Partes;
                            if (Dentro(mx, mz, x0, z0, lado, esCamino, cerca) < -Margen) continue;
                            int a = Vertice(i, j), b = Vertice(i + 1, j), c = Vertice(i, j + 1), d = Vertice(i + 1, j + 1);
                            indices.Add(a); indices.Add(c); indices.Add(b);
                            indices.Add(b); indices.Add(c); indices.Add(d);
                        }

                    int Vertice(int i, int j)
                    {
                        int k = i * (Partes + 1) + j;
                        if (fila[k] >= 0) return fila[k];
                        float x = x0 + i * lado / Partes, z = z0 + j * lado / Partes;
                        Vector3 normal = Vector3.up;
                        float y = altura;
                        Vector2 color = colorHierba;
                        if (suelo != null && suelo.Raycast(new Ray(new Vector3(x, 200f, z), Vector3.down), out RaycastHit golpe, 400f))
                        {
                            y = golpe.point.y + 0.03f;
                            normal = golpe.normal;
                            if (golpe.textureCoord != Vector2.zero) color = golpe.textureCoord;
                        }
                        fila[k] = vertices.Count;
                        vertices.Add(new Vector3(x, y, z));
                        normales.Add(normal);
                        dentro.Add(new Vector2(Dentro(x, z, x0, z0, lado, esCamino, cerca), 0f));
                        paleta.Add(color);
                        return fila[k];
                    }
                }
            if (indices.Count == 0) return;

            var malla = new Mesh { name = "Camino " + clave.x + "," + clave.y };
            if (vertices.Count > 65000) malla.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            malla.SetVertices(vertices);
            malla.SetNormals(normales);
            malla.SetUVs(0, dentro);
            malla.SetUVs(1, paleta);
            malla.SetTriangles(indices, 0);
            malla.RecalculateBounds();
            var go = new GameObject("Camino (trozo " + clave.x + "," + clave.y + ")");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var render = go.AddComponent<MeshRenderer>();
            render.sharedMaterial = material;
            render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            render.receiveShadows = true;
            trozos[clave] = go;
        }

        /// <summary>
        /// Metros dentro del camino del punto (x, z) de la celda que empieza en (x0, z0): positivo
        /// dentro (hasta la celda vecina sin camino), negativo fuera (hasta la vecina con camino).
        /// Solo cuentan las 8 vecinas: el margen es menor que una celda.
        /// </summary>
        static float Dentro(float x, float z, float x0, float z0, float lado, bool esCamino, bool[,] cerca)
        {
            float mejor = Margen + 0.1f;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (cerca[dx + 1, dz + 1] == esCamino) continue;
                    float cx = x0 + (dx + 0.5f) * lado, cz = z0 + (dz + 0.5f) * lado;
                    float ax = Mathf.Max(Mathf.Abs(x - cx) - lado * 0.5f, 0f);
                    float az = Mathf.Max(Mathf.Abs(z - cz) - lado * 0.5f, 0f);
                    mejor = Mathf.Min(mejor, Mathf.Sqrt(ax * ax + az * az));
                }
            return esCamino ? mejor : -mejor;
        }

        static int Mezcla(int x, int z)
        {
            unchecked
            {
                int h = x * 73856093 ^ z * 19349663;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }
    }
}
