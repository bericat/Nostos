using UnityEngine;

namespace Nostos
{
    /// <summary>Interactuable de prueba para la F0: un farol que se enciende y se apaga.</summary>
    public class Farol : MonoBehaviour, IInteractuable
    {
        public Light luz;
        public bool encendido;

        public string Texto => encendido ? "Apagar el farol" : "Encender el farol";

        void Start()
        {
            Aplicar();
        }

        public void Interactuar(GameObject quien)
        {
            encendido = !encendido;
            Aplicar();
        }

        void Aplicar()
        {
            if (luz != null) luz.enabled = encendido;
        }
    }
}
