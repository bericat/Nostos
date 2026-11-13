using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// El cielo de noche de Lacio con las imágenes de Dynamic Space Background: una cúpula que sigue
    /// a la cámara con capas (nebulosa, segunda nebulosa más tenue, estrellas pequeñas y grandes)
    /// que aparecen al anochecer y se van al amanecer. Cada capa gira despacio a su ritmo, para que
    /// el cielo se mueva un poco. Va detrás de las montañas y de Calcis, y no le afecta la niebla.
    /// </summary>
    public class CieloNocturno : MonoBehaviour
    {
        [System.Serializable]
        public class Capa
        {
            public Texture2D textura;
            [Tooltip("Cuántas veces se repite la imagen por el cielo.")]
            public float repeticion = 1f;
            [Range(0f, 1f)] public float opacidad = 1f;
            [Tooltip("Brillo: más de 1 aclara la imagen.")]
            public float brillo = 1f;
            [Tooltip("Grados por segundo que gira alrededor de la vertical.")]
            public float giro = 0.2f;
            [Tooltip("Cuánto parpadea (0 = nada). Para las estrellas grandes.")]
            [Range(0f, 1f)] public float parpadeo;
        }

        public Juego juego;
        public Capa[] capas = new Capa[0];
        public float radio = 650f;
        [Tooltip("Cuánto se aplana la proyección: más alto, menos se estira la imagen en el horizonte.")]
        public float aplanado = 0.45f;

        Transform[] objetos;
        Material[] materiales;
        Renderer[] renders;
        Mesh cupula;

        void Start()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null || capas == null || capas.Length == 0)
            {
                enabled = false;
                return;
            }
            cupula = CrearCupula(aplanado);
            objetos = new Transform[capas.Length];
            materiales = new Material[capas.Length];
            renders = new Renderer[capas.Length];
            for (int i = 0; i < capas.Length; i++)
            {
                Capa capa = capas[i];
                if (capa == null || capa.textura == null) continue;
                // Las imágenes vienen importadas como sprites (sin repetir): aquí se repiten.
                capa.textura.wrapMode = TextureWrapMode.Repeat;

                var objeto = new GameObject("Cielo de noche " + (i + 1));
                objeto.transform.SetParent(transform, false);
                objeto.AddComponent<MeshFilter>().sharedMesh = cupula;
                var render = objeto.AddComponent<MeshRenderer>();
                // Detrás de todo lo sólido; Calcis (2600) se pinta encima.
                var material = new Material(shader) { mainTexture = capa.textura, renderQueue = 2550 + i };
                material.mainTextureScale = new Vector2(capa.repeticion, capa.repeticion);
                render.sharedMaterial = material;
                render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                render.receiveShadows = false;
                render.enabled = false;
                objeto.transform.localScale = Vector3.one * (radio - i * 4f);
                objetos[i] = objeto.transform;
                materiales[i] = material;
                renders[i] = render;
            }
        }

        /// <summary>
        /// Media esfera (un poco por debajo del horizonte) con las coordenadas de textura proyectadas
        /// desde arriba, como un techo lejano: así no se pinza en lo alto como una esfera normal.
        /// </summary>
        static Mesh CrearCupula(float aplanado)
        {
            const int anillos = 24;
            const int sectores = 48;
            const float desdeGrados = -8f;
            var vertices = new Vector3[(anillos + 1) * (sectores + 1)];
            var uv = new Vector2[vertices.Length];
            int v = 0;
            for (int a = 0; a <= anillos; a++)
            {
                float elevacion = Mathf.Lerp(desdeGrados, 90f, a / (float)anillos) * Mathf.Deg2Rad;
                for (int s = 0; s <= sectores; s++)
                {
                    float azimut = s / (float)sectores * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(elevacion) * Mathf.Cos(azimut), Mathf.Sin(elevacion), Mathf.Cos(elevacion) * Mathf.Sin(azimut));
                    vertices[v] = d;
                    float k = 1f / (Mathf.Max(d.y, -0.1f) + aplanado);
                    uv[v] = new Vector2(d.x * k, d.z * k) * 0.5f;
                    v++;
                }
            }
            var triangulos = new int[anillos * sectores * 6];
            int t = 0;
            for (int a = 0; a < anillos; a++)
            {
                for (int s = 0; s < sectores; s++)
                {
                    int i0 = a * (sectores + 1) + s;
                    int i1 = i0 + sectores + 1;
                    triangulos[t++] = i0;
                    triangulos[t++] = i1;
                    triangulos[t++] = i0 + 1;
                    triangulos[t++] = i0 + 1;
                    triangulos[t++] = i1;
                    triangulos[t++] = i1 + 1;
                }
            }
            var colores = new Color[vertices.Length];
            for (int i = 0; i < colores.Length; i++) colores[i] = Color.white;
            var malla = new Mesh { name = "Cúpula del cielo", vertices = vertices, uv = uv, colors = colores, triangles = triangulos };
            malla.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return malla;
        }

        /// <summary>0 de día, 1 de noche cerrada, con el mismo reparto de horas que DiaNoche.</summary>
        float Noche()
        {
            if (juego == null || juego.Mundo == null) return 0f;
            float t = (juego.HoraSuave - 6f) / 12f;
            bool esDeDia = t > 0f && t < 1f;
            float dia = esDeDia ? Mathf.Clamp01(Mathf.Sin(t * Mathf.PI) * 4f) : 0f;
            return 1f - dia;
        }

        void LateUpdate()
        {
            if (objetos == null) return;
            Camera camara = Camera.main;
            if (camara == null) return;
            float far = camara.farClipPlane * 0.9f;
            float noche = Noche();
            float tiempo = Time.unscaledTime;
            for (int i = 0; i < objetos.Length; i++)
            {
                if (objetos[i] == null) continue;
                Capa capa = capas[i];
                float alfa = noche * capa.opacidad;
                if (capa.parpadeo > 0f)
                {
                    alfa *= 1f - capa.parpadeo * (0.5f + 0.5f * Mathf.Sin(tiempo * 1.7f + i * 2.3f)) * Mathf.PerlinNoise(tiempo * 0.6f, i);
                }
                bool visible = alfa > 0.003f;
                renders[i].enabled = visible;
                if (!visible) continue;
                objetos[i].position = camara.transform.position;
                objetos[i].rotation = Quaternion.Euler(0f, tiempo * capa.giro, 0f);
                objetos[i].localScale = Vector3.one * Mathf.Min(radio - i * 4f, far - i * 4f);
                materiales[i].color = new Color(capa.brillo, capa.brillo, capa.brillo, alfa);
            }
        }

        void OnDestroy()
        {
            if (materiales != null)
            {
                foreach (Material m in materiales)
                {
                    if (m != null) Destroy(m);
                }
            }
            if (cupula != null) Destroy(cupula);
        }
    }
}
