using System.Collections.Generic;
using Nostos.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos
{
    /// <summary>
    /// Hierba densa por todo el valle: decenas de miles de matas (textura del paquete "A piece of
    /// nature") pintadas con GPU instancing, sin GameObjects. Las posiciones las calcula
    /// ConstructorEscena al montar la escena y van en un archivo de bytes. Se pintan solo los trozos
    /// cerca de la cámara, y desaparecen donde se construye un edificio o se traza un camino.
    /// </summary>
    public class HierbaDelValle : MonoBehaviour
    {
        /// <summary>Lado de cada trozo del valle, en metros.</summary>
        public const float LadoTrozo = 16f;
        /// <summary>Máximo de matas por llamada de instancing.</summary>
        const int PorLlamada = 1023;
        static readonly int IdCentro = Shader.PropertyToID("_CentroHierba");

        public TextAsset datos;
        public Mesh mata;
        public Material material;
        [Tooltip("Hasta dónde se pinta la hierba desde la cámara (m). Debe coincidir con _Distancia del material.")]
        public float distancia = 55f;

        class Trozo
        {
            public Vector2Int clave;
            public Bounds caja;
            public readonly List<Matrix4x4> todas = new List<Matrix4x4>();
            public readonly List<Celda> celdas = new List<Celda>();
            public Matrix4x4[] visibles = new Matrix4x4[0];
            public int cuantas;
        }

        readonly Dictionary<Vector2Int, Trozo> trozos = new Dictionary<Vector2Int, Trozo>();
        readonly List<Trozo> lista = new List<Trozo>();
        int firma = int.MinValue;
        float siguienteRevision;
        RenderParams parametros;
        bool listo;

        void Start()
        {
            if (datos == null || mata == null || material == null) return;
            material.enableInstancing = true;
            Leer(datos.bytes);
            parametros = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
            };
            listo = true;
            Revisar(true);
        }

        /// <summary>Formato: por mata, x, y, z (float) y giro y escala juntos en un float: la parte entera es
        /// el giro en décimas de grado y la decimal, la escala menos 0,6.</summary>
        void Leer(byte[] bytes)
        {
            int n = bytes.Length / 16;
            Juego juego = Juego.Instancia;
            Rejilla rejilla = juego != null ? juego.Mundo.Rejilla : null;
            for (int i = 0; i < n; i++)
            {
                int o = i * 16;
                float x = System.BitConverter.ToSingle(bytes, o);
                float y = System.BitConverter.ToSingle(bytes, o + 4);
                float z = System.BitConverter.ToSingle(bytes, o + 8);
                float gz = System.BitConverter.ToSingle(bytes, o + 12);
                float giro = Mathf.Floor(gz) / 10f;
                float escala = gz - Mathf.Floor(gz) + 0.6f;
                var clave = new Vector2Int(Mathf.FloorToInt(x / LadoTrozo), Mathf.FloorToInt(z / LadoTrozo));
                if (!trozos.TryGetValue(clave, out Trozo trozo))
                {
                    trozo = new Trozo { clave = clave };
                    trozo.caja = new Bounds(new Vector3((clave.x + 0.5f) * LadoTrozo, y, (clave.y + 0.5f) * LadoTrozo), new Vector3(LadoTrozo, 2f, LadoTrozo));
                    trozos.Add(clave, trozo);
                    lista.Add(trozo);
                }
                trozo.caja.Encapsulate(new Vector3(x, y + 1.2f, z));
                trozo.caja.Encapsulate(new Vector3(x, y - 0.2f, z));
                trozo.todas.Add(Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, giro, 0f), Vector3.one * escala));
                trozo.celdas.Add(rejilla != null ? rejilla.CeldaDesdeMundo(x, z) : default);
            }
        }

        /// <summary>Quita las matas de las celdas con edificio o camino. Solo cuando cambia algo.</summary>
        void Revisar(bool forzar)
        {
            Juego juego = Juego.Instancia;
            if (juego == null || juego.Mundo == null) return;
            Mundo mundo = juego.Mundo;
            int nueva = mundo.Rejilla.Ocupadas * 7919 + mundo.Caminos.Count;
            if (!forzar && nueva == firma) return;
            firma = nueva;
            foreach (Trozo trozo in lista)
            {
                if (trozo.visibles.Length < trozo.todas.Count) trozo.visibles = new Matrix4x4[trozo.todas.Count];
                int k = 0;
                for (int i = 0; i < trozo.todas.Count; i++)
                {
                    Celda celda = trozo.celdas[i];
                    if (!mundo.Rejilla.EstaLibre(celda) || mundo.EsCamino(celda)) continue;
                    // Junto a un camino la hierba clarea y es más baja: el borde se funde.
                    Matrix4x4 m = trozo.todas[i];
                    float d = Caminos.DistanciaACamino(mundo, m.m03, m.m23);
                    if (d < 1.3f)
                    {
                        if (d < 0.35f || (i * 7919 % 10) < Mathf.RoundToInt(7f * (1f - d / 1.3f))) continue;
                        m *= Matrix4x4.Scale(Vector3.one * Mathf.Lerp(0.55f, 1f, d / 1.3f));
                    }
                    trozo.visibles[k++] = m;
                }
                trozo.cuantas = k;
            }
        }

        void Update()
        {
            if (!listo) return;
            if (Time.unscaledTime >= siguienteRevision)
            {
                siguienteRevision = Time.unscaledTime + 0.5f;
                Revisar(false);
            }

            Camera camara = Camera.main;
            if (camara == null) return;
            // A pie, la hierba rodea la cámara; desde arriba (constructor), el punto que mira.
            Vector3 ojo = camara.transform.position;
            Vector3 mira = camara.transform.forward;
            if (ojo.y > 14f && mira.y < -0.1f)
            {
                float t = (ojo.y - 0.5f) / -mira.y;
                ojo += mira * Mathf.Min(t, 400f);
            }
            Shader.SetGlobalVector(IdCentro, ojo);
            float alcance = distancia + LadoTrozo;
            Plane[] planos = GeometryUtility.CalculateFrustumPlanes(camara);
            foreach (Trozo trozo in lista)
            {
                if (trozo.cuantas == 0) continue;
                if (trozo.caja.SqrDistance(ojo) > alcance * alcance) continue;
                if (!GeometryUtility.TestPlanesAABB(planos, trozo.caja)) continue;
                parametros.worldBounds = trozo.caja;
                for (int desde = 0; desde < trozo.cuantas; desde += PorLlamada)
                {
                    int n = Mathf.Min(PorLlamada, trozo.cuantas - desde);
                    Graphics.RenderMeshInstanced(parametros, mata, 0, trozo.visibles, n, desde);
                }
            }
        }
    }
}
