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

            // Con herramientas en la mochila sale una unidad más, y cada herramienta se gasta.
            if (juego.Mundo.Mochila[Recurso.Herramientas] > 0 && !rebrota)
            {
                if (unidades > 1 && juego.Mundo.Mochila.Meter(recurso, 1) > 0) unidades--;
                if (juego.Mundo.GastarHerramienta())
                    juego.Avisar("Se te ha roto una herramienta. El banco de trabajo hace más.");
            }

            unidades--;
            golpe = 1f;
            Sonido.Tocar(Efecto.Golpe, 0.8f, true);
            Mostrar();
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
