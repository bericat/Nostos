using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Calcis en el cielo de Lacio: un disco grande y pálido, siempre a la misma distancia de la
    /// cámara y mirándola, para que parezca infinitamente lejos. Es la casa a la que Nicias quiere
    /// volver, y se ve desde la portada y desde el valle. No le afecta la niebla.
    /// </summary>
    public class CieloCalcis : MonoBehaviour
    {
        [Tooltip("Hacia dónde está Calcis en el cielo (se normaliza).")]
        public Vector3 direccion = new Vector3(-0.2f, 0.62f, 0.76f);
        [Tooltip("En la portada, dónde se ve respecto a la cámara del título al empezar: x a la derecha, y arriba, z al frente.")]
        public Vector3 enPortada = new Vector3(0.5f, 0.3f, 1f);
        public float distancia = 600f;
        public float tamano = 210f;
        [Range(0f, 1f)] public float opacidad = 0.95f;

        /// <summary>Hacia dónde está Calcis en el cielo durante la partida (normalizado). Lo usa la prueba del rumbo.</summary>
        public static Vector3 Direccion { get; private set; } = new Vector3(-0.2f, 0.62f, 0.76f).normalized;

        Transform disco;
        Material material;
        Vector3 direccionPortada;
        bool hayPortada;

        void Start()
        {
            Direccion = direccion.normalized;
            Texture2D textura = Resources.Load<Texture2D>("UI/Cielo/calcis");
            Shader shader = Shader.Find("Sprites/Default");
            if (textura == null || shader == null)
            {
                enabled = false;
                return;
            }
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Disco de Calcis";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            material = new Material(shader) { mainTexture = textura, renderQueue = 2600 };
            material.color = new Color(1f, 1f, 1f, opacidad);
            var render = quad.GetComponent<MeshRenderer>();
            render.sharedMaterial = material;
            render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            render.receiveShadows = false;
            disco = quad.transform;
            disco.localScale = Vector3.one * tamano;
        }

        void LateUpdate()
        {
            if (disco == null) return;
            Camera camara = Camera.main;
            if (camara == null) return;
            Vector3 dir = direccion.normalized;
            bool titulo = Narrativa.Instancia != null && Narrativa.Instancia.EnTitulo;
            if (titulo)
            {
                // Se fija una vez, con la cámara del título ya colocada, para que esté quieto en el cielo.
                if (!hayPortada && Time.frameCount > 2)
                {
                    Transform c = camara.transform;
                    direccionPortada = (c.right * enPortada.x + c.up * enPortada.y + c.forward * enPortada.z).normalized;
                    hayPortada = true;
                }
                if (hayPortada) dir = direccionPortada;
            }
            float d = Mathf.Min(distancia, camara.farClipPlane * 0.85f);
            disco.position = camara.transform.position + dir * d;
            disco.localScale = Vector3.one * (tamano * d / distancia);
            // El quad se ve por su cara -Z: que mire a la cámara.
            disco.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
