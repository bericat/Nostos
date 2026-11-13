using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// La boca del túnel que une el valle con la civilización de Calcis en Lacio. Por ahora no
    /// se puede cruzar: es por donde llegan los arcontes y por donde se va el tributo.
    /// </summary>
    public class Tunel : MonoBehaviour, IInteractuable
    {
        public Juego juego;

        public string Texto => "Túnel a la ciudad de los arcontes (cerrado)";

        public void Interactuar(GameObject quien)
        {
            if (juego == null) return;
            juego.Avisar("El túnel se pierde en la montaña. Al otro lado está la ciudad de los arcontes. "
                         + "Todavía no tienes nada que hacer allí.", 6f);
        }
    }
}
