using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>Un árbol o una veta: cada interacción da una unidad a la mochila hasta agotarse.</summary>
    public class NodoRecurso : MonoBehaviour, IInteractuable
    {
        public Recurso recurso = Recurso.Madera;
        public int unidades = 6;
        public string verbo = "Talar";
        [Tooltip("Si rebrota, no desaparece al agotarse y se rellena cada mañana (arbustos).")]
        public bool rebrota;
        [Tooltip("Lo que se ve con fruto y sin él (frutales). Opcional.")]
        public GameObject conFruto;
        public GameObject sinFruto;
        [Tooltip("Lo que queda al agotarse, como el tocón de un árbol. Opcional.")]
        public GameObject restos;

        int maximo;
        int diaVisto = -1;

        Vector3 escalaBase;
        float golpe;

        public string Texto => unidades > 0
            ? verbo + " (" + unidades + " de " + Recursos.Nombre(recurso) + ")"
            : "Sin fruto hasta mañana";

        void Awake()
        {
            escalaBase = transform.localScale;
            maximo = unidades;
        }

        void Update()
        {
            if (rebrota && Juego.Instancia != null)
            {
                int dia = Juego.Instancia.Mundo.Ciclo.Dia;
                if (diaVisto < 0) diaVisto = dia;
                if (dia != diaVisto)
                {
                    diaVisto = dia;
                    unidades = maximo;
                    Mostrar();
                }
            }
            if (golpe <= 0f) return;
            golpe = Mathf.Max(0f, golpe - Time.deltaTime * 5f);
            transform.localScale = escalaBase * (1f - 0.08f * Mathf.Sin(golpe * Mathf.PI));
        }

        public void Interactuar(GameObject quien)
        {
            Juego juego = Juego.Instancia;
            if (juego == null || unidades <= 0) return;

            if (juego.Mundo.Mochila.Meter(recurso, 1) == 0)
            {
                juego.Avisar("Mochila llena (" + juego.Mundo.Mochila.Capacidad + "). Entrega algo en una obra.");
                return;
            }

            // Con el hacha, el pico o la pala sale más en cada golpe, y la herramienta se gasta.
            // Sin ellas, las herramientas del pueblo que lleve en la mochila dan una más.
            Equipo equipo = juego.Mundo.Equipo;
            if (!rebrota && equipo.ParaRecoger(recurso, out Herramienta h, out int extra))
            {
                for (int i = 0; i < extra && unidades > 1; i++)
                {
                    if (juego.Mundo.Mochila.Meter(recurso, 1) == 0) break;
                    unidades--;
                }
                if (equipo.Gastar(h))
                    juego.Avisar("Se te ha roto " + Equipo.ConArticulo(h) + ". En el banco de trabajo puedes forjar " + (h == Herramienta.Pala ? "otra." : "otro."));
                ManoNicias.Golpe(h);
            }
            else if (juego.Mundo.Mochila[Recurso.Herramientas] > 0 && !rebrota)
            {
                if (unidades > 1 && juego.Mundo.Mochila.Meter(recurso, 1) > 0) unidades--;
                if (juego.Mundo.GastarHerramienta())
                    juego.Avisar("Se te ha roto una herramienta. El banco de trabajo hace más.");
            }

            unidades--;
            golpe = 1f;
            Sonido.Tocar(Efecto.Golpe, 0.8f, true);
            Mostrar();
            Picar(quien);
            if (unidades <= 0 && !rebrota)
            {
                if (restos != null)
                {
                    GameObject tocon = Instantiate(restos, transform.position, transform.rotation, transform.parent);
                    tocon.transform.localScale = escalaBase;
                }
                Destroy(gameObject);
            }
        }

        /// <summary>Las rocas y vetas se abollan donde da el pico y sueltan esquirlas.</summary>
        void Picar(GameObject quien)
        {
            var roca = GetComponent<RocaPicable>();
            if (roca == null) return;
            Vector3 punto = transform.position;
            Vector3 direccion = Vector3.down;
            var pie = quien != null ? quien.GetComponent<ControladorAPie>() : null;
            Camera camara = Camera.main;
            if (pie != null && camara != null)
            {
                punto = pie.PuntoEnfocado;
                direccion = camara.transform.forward;
            }
            else if (quien != null)
            {
                var caja = GetComponentInChildren<Collider>();
                Vector3 desde = quien.transform.position + Vector3.up;
                if (caja != null) punto = caja.ClosestPoint(desde);
                direccion = punto - desde;
            }
            roca.Golpear(punto, direccion, unidades <= 0 && !rebrota);
        }

        /// <summary>Un frutal sin fruta se ve como un árbol verde hasta que rebrota.</summary>
        void Mostrar()
        {
            if (conFruto == null || sinFruto == null) return;
            bool hay = unidades > 0;
            if (conFruto.activeSelf != hay) conFruto.SetActive(hay);
            if (sinFruto.activeSelf == hay) sinFruto.SetActive(!hay);
        }
    }
}
