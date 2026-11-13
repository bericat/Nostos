using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    public enum ModoJuego
    {
        APie,
        Constructor
    }

    /// <summary>
    /// Dueño del modo de juego. Activa el mapa de acciones que toca ("A pie" o "Constructor"),
    /// sube la prioridad de la cámara correspondiente (Cinemachine hace la transición)
    /// y gestiona el cursor.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ControlModo : MonoBehaviour
    {
        public const string MapaAPie = "A pie";
        public const string MapaConstructor = "Constructor";
        public const string AccionCambiarModo = "Cambiar modo";

        public InputActionAsset acciones;
        public CinemachineCamera camaraAPie;
        public CinemachineCamera camaraConstructor;
        public ModoJuego modoInicial = ModoJuego.APie;

        InputActionMap mapaAPie;
        InputActionMap mapaConstructor;
        InputAction cambiarAPie;
        InputAction cambiarConstructor;

        public ModoJuego Modo { get; private set; }
        /// <summary>Con la entrada bloqueada (un diálogo abierto) no hay mapa activo y el cursor queda libre.</summary>
        public bool Bloqueado { get; private set; }
        public event Action<ModoJuego> ModoCambiado;

        public InputActionMap MapaActivo => Modo == ModoJuego.APie ? mapaAPie : mapaConstructor;

        void Awake()
        {
            mapaAPie = acciones.FindActionMap(MapaAPie, true);
            mapaConstructor = acciones.FindActionMap(MapaConstructor, true);
            cambiarAPie = mapaAPie.FindAction(AccionCambiarModo, true);
            cambiarConstructor = mapaConstructor.FindAction(AccionCambiarModo, true);
        }

        void OnEnable()
        {
            Aplicar(modoInicial);
        }

        void OnDisable()
        {
            mapaAPie.Disable();
            mapaConstructor.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            // Se consulta aquí (y no en un callback) para no desactivar un mapa
            // desde dentro del callback de una de sus propias acciones.
            InputAction cambiar = Modo == ModoJuego.APie ? cambiarAPie : cambiarConstructor;
            if (cambiar.WasPressedThisFrame())
            {
                Aplicar(Modo == ModoJuego.APie ? ModoJuego.Constructor : ModoJuego.APie);
            }
        }

        public void Bloquear(bool bloquear)
        {
            Bloqueado = bloquear;
            if (bloquear)
            {
                mapaAPie.Disable();
                mapaConstructor.Disable();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Aplicar(Modo);
            }
        }

        public void Aplicar(ModoJuego modo)
        {
            Modo = modo;
            Bloqueado = false;
            bool aPie = modo == ModoJuego.APie;

            if (aPie)
            {
                mapaConstructor.Disable();
                mapaAPie.Enable();
            }
            else
            {
                mapaAPie.Disable();
                mapaConstructor.Enable();
            }

            if (camaraAPie != null) camaraAPie.Priority.Value = aPie ? 20 : 0;
            if (camaraConstructor != null) camaraConstructor.Priority.Value = aPie ? 0 : 20;

            Cursor.lockState = aPie ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !aPie;

            ModoCambiado?.Invoke(modo);
        }
    }
}
