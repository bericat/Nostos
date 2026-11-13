using System.Collections.Generic;
using Nostos.Sim;
using Unity.AI.Navigation;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Dibuja las losas de los caminos que guarda la simulación y marca su trozo de NavMesh con
    /// un área propia, más barata, para que los colonos los prefieran. Andar por un camino es
    /// más rápido, tanto para los colonos como para Nicias.
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

            foreach (Celda celda in mundo.Caminos)
            {
                if (visibles.ContainsKey(celda)) continue;
                visibles.Add(celda, CrearLosa(celda));
                cambio = true;
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

            if (losas != null && losas.Length > 0)
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
