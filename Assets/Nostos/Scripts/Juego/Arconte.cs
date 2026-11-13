using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// El arconte de Calcis: aparece en la plaza el día del tributo y se va al cobrar o al
    /// acabar el día. Hablar con él paga la cuota con lo que haya en el almacén y la mochila.
    /// </summary>
    public class Arconte : MonoBehaviour, IInteractuable
    {
        public Juego juego;
        [Tooltip("Lo que se muestra y se oculta: la figura del arconte.")]
        public GameObject figura;

        void Update()
        {
            if (juego == null || juego.Mundo == null || figura == null) return;
            bool presente = juego.Mundo.Tributo.ArcontePresente;
            if (figura.activeSelf != presente) figura.SetActive(presente);
        }

        public string Texto
        {
            get
            {
                if (juego == null || !juego.Mundo.Tributo.ArcontePresente) return null;
                Tributo tributo = juego.Mundo.Tributo;
                string cuota = Recursos.Lista(tributo.Cuota);
                return tributo.SePuedePagar(juego.Mundo.Almacen, juego.Mundo.Mochila)
                    ? "Pagar el tributo al arconte (" + cuota + ")"
                    : "El arconte exige " + cuota + ". Falta: "
                      + tributo.DescribirFalta(juego.Mundo.Almacen, juego.Mundo.Mochila);
            }
        }

        public void Interactuar(GameObject quien)
        {
            if (juego == null || !juego.Mundo.Tributo.ArcontePresente) return;
            if (juego.Mundo.PagarTributo())
            {
                Sonido.Tocar(Efecto.Entrega);
                juego.Avisar("«Calcis agradece la puntualidad, ingeniero.» Los colonos te han visto pagar por ellos. (+8 de confianza)", 7f);
            }
            else
            {
                juego.Avisar("«Tú eres de los nuestros. Sabes que la cuota no se discute.» Si acaba el día sin pagar, se la cobrarán.", 7f);
            }
        }
    }
}
