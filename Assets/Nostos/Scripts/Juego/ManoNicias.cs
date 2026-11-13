using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// La herramienta en la mano, en primera persona: Nicias saca el hacha delante de un árbol, el
    /// pico delante de una veta y la pala delante del cobre (si los tiene), y la blande en cada
    /// golpe. Los modelos son los de los paquetes "Stylized Tools" y "Stylized Pickaxe", ya
    /// preparados por ConstructorEscena con el mango hacia abajo y la cabeza hacia arriba.
    /// </summary>
    public class ManoNicias : MonoBehaviour
    {
        [Tooltip("Hacha, pico, pala y pico bueno, en el orden de Herramienta.")]
        public GameObject[] modelos = new GameObject[Equipo.Numero];
        public ControladorAPie pie;
        public ControlModo control;
        public Transform camara;

        [Header("Postura")]
        public Vector3 reposo = new Vector3(0.3f, -0.4f, 0.5f);
        public Vector3 giroReposo = new Vector3(14f, -10f, 22f);
        [Tooltip("Segundos que sigue en la mano después de dejar de mirar al árbol o la roca.")]
        public float guardarTras = 1.5f;

        const float DuracionGolpe = 0.5f;
        static ManoNicias instancia;

        int actual = -1;     // la que está en la mano (puede estar bajando)
        int deseada = -1;
        float subida;        // 0 guardada, 1 en la mano
        float golpe = -1f;   // tiempo desde el último golpe, o < 0 si no hay golpe
        float ultimaVez = -10f;

        void Awake()
        {
            instancia = this;
            foreach (GameObject m in modelos)
            {
                if (m != null) m.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (instancia == this) instancia = null;
        }

        /// <summary>Lo llama NodoRecurso cuando Nicias golpea con una herramienta.</summary>
        public static void Golpe(Herramienta h)
        {
            if (instancia == null) return;
            instancia.Blandir((int)h);
        }

        void Blandir(int h)
        {
            if (h < 0 || h >= modelos.Length || modelos[h] == null) return;
            if (actual != h)
            {
                Mostrar(h);
                subida = 0.6f;
            }
            deseada = h;
            golpe = 0f;
            ultimaVez = Time.time;
        }

        void Mostrar(int h)
        {
            for (int i = 0; i < modelos.Length; i++)
            {
                if (modelos[i] != null) modelos[i].SetActive(i == h);
            }
            actual = h;
        }

        void Update()
        {
            int quiere = Buscar();
            if (quiere >= 0)
            {
                deseada = quiere;
                ultimaVez = Time.time;
            }
            else if (Time.time - ultimaVez > guardarTras || !EnPrimeraPersona())
            {
                deseada = -1;
            }

            // Se guarda la que hay antes de sacar otra.
            float objetivo = deseada >= 0 && deseada == actual ? 1f : 0f;
            subida = Mathf.MoveTowards(subida, objetivo, Time.deltaTime * 4f);
            if (subida <= 0f)
            {
                if (deseada != actual) Mostrar(deseada);
            }

            if (golpe >= 0f)
            {
                golpe += Time.deltaTime;
                if (golpe > DuracionGolpe) golpe = -1f;
            }
            Colocar();
        }

        bool EnPrimeraPersona()
        {
            if (control != null && control.Modo != ModoJuego.APie) return false;
            Transform cabeza = transform.parent;
            if (camara == null || cabeza == null) return true;
            return (camara.position - cabeza.position).sqrMagnitude < 0.6f * 0.6f;
        }

        /// <summary>La herramienta que pide lo que Nicias tiene delante, o -1.</summary>
        int Buscar()
        {
            if (pie == null || !EnPrimeraPersona()) return -1;
            if (!(pie.Enfocado is NodoRecurso nodo) || nodo.unidades <= 0 || nodo.rebrota) return -1;
            Juego juego = Juego.Instancia;
            if (juego == null || juego.Mundo == null) return -1;
            return juego.Mundo.Equipo.ParaRecoger(nodo.recurso, out Herramienta h, out _) ? (int)h : -1;
        }

        void Colocar()
        {
            float s = Mathf.SmoothStep(0f, 1f, subida);
            Vector3 sitio = reposo + new Vector3(0.05f, -0.45f, -0.1f) * (1f - s);
            Vector3 giro = giroReposo + new Vector3(-35f, 0f, 0f) * (1f - s);

            if (golpe >= 0f)
            {
                // Levanta un poco, baja de golpe y vuelve despacio.
                float t = golpe;
                float angulo;
                if (t < 0.08f) angulo = Mathf.Lerp(0f, -28f, t / 0.08f);
                else if (t < 0.17f) angulo = Mathf.Lerp(-28f, 72f, Mathf.SmoothStep(0f, 1f, (t - 0.08f) / 0.09f));
                else angulo = Mathf.Lerp(72f, 0f, Mathf.SmoothStep(0f, 1f, (t - 0.17f) / (DuracionGolpe - 0.17f)));
                giro.x += angulo;
                float empuje = Mathf.Clamp01(angulo / 72f);
                sitio += new Vector3(-0.06f, -0.06f, 0.14f) * empuje;
            }

            transform.localPosition = sitio;
            transform.localRotation = Quaternion.Euler(giro);
        }
    }
}
