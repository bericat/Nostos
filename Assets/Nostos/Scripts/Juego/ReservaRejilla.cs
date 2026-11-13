using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>Reserva en la rejilla las celdas alrededor de este objeto para que no se construya encima.</summary>
    public class ReservaRejilla : MonoBehaviour
    {
        [Tooltip("Radio en metros.")]
        public float radio = 4f;

        void Start()
        {
            Juego juego = Juego.Instancia;
            if (juego == null) return;
            Rejilla rejilla = juego.Mundo.Rejilla;
            float tam = rejilla.TamCelda;
            Celda min = rejilla.CeldaDesdeMundo(transform.position.x - radio, transform.position.z - radio);
            Celda max = rejilla.CeldaDesdeMundo(transform.position.x + radio, transform.position.z + radio);
            for (int x = min.X; x <= max.X; x++)
            {
                for (int z = min.Z; z <= max.Z; z++)
                {
                    rejilla.Ocupar(new Celda(x, z));
                }
            }
        }
    }
}
