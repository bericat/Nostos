using UnityEngine;

namespace Nostos
{
    /// <summary>Cualquier cosa del mundo con la que Nicias puede interactuar a pie.</summary>
    public interface IInteractuable
    {
        /// <summary>
        /// Texto corto para el aviso en pantalla, por ejemplo "Encender el farol".
        /// Si es null o vacío, ahora mismo no se puede interactuar.
        /// </summary>
        string Texto { get; }

        void Interactuar(GameObject quien);
    }
}
