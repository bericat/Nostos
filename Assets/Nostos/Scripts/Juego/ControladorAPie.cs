using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>
    /// Control en primera persona: moverse, mirar, saltar e interactuar.
    /// Solo actúa en modo "A pie"; en modo constructor la cápsula se queda quieta.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ControladorAPie : MonoBehaviour
    {
        public ControlModo control;
        public Transform cabeza;

        [Header("Movimiento")]
        public float velocidad = 5f;
        public float alturaSalto = 1.2f;
        public float gravedad = -20f;

        [Header("Mirada")]
        public float sensibilidadRaton = 0.12f;
        public float sensibilidadMando = 160f;
        public float limiteVertical = 85f;

        [Header("Interacción")]
        public float alcance = 3f;
        public LayerMask capasInteraccion = ~0;

        CharacterController cuerpo;
        InputAction mover;
        InputAction mirar;
        InputAction mirarMando;
        InputAction saltar;
        InputAction interactuar;
        float velocidadVertical;
        float inclinacion;
        float caminado;

        /// <summary>Lo que Nicias tiene delante y al alcance, o null.</summary>
        public IInteractuable Enfocado { get; private set; }

        void Awake()
        {
            cuerpo = GetComponent<CharacterController>();
        }

        void Start()
        {
            InputActionMap mapa = control.acciones.FindActionMap(ControlModo.MapaAPie, true);
            mover = mapa.FindAction("Mover", true);
            mirar = mapa.FindAction("Mirar", true);
            mirarMando = mapa.FindAction("Mirar mando", true);
            saltar = mapa.FindAction("Saltar", true);
            interactuar = mapa.FindAction("Interactuar", true);
        }

        void Update()
        {
            bool activo = control.Modo == ModoJuego.APie && !control.Bloqueado;

            if (activo) Mirar();

            Vector2 entrada = activo ? mover.ReadValue<Vector2>() : Vector2.zero;
            Vector3 horizontal = transform.right * entrada.x + transform.forward * entrada.y;
            if (horizontal.sqrMagnitude > 1f) horizontal.Normalize();

            if (cuerpo.isGrounded)
            {
                if (velocidadVertical < 0f) velocidadVertical = -2f;
                if (activo && saltar.WasPressedThisFrame())
                {
                    velocidadVertical = Mathf.Sqrt(-2f * gravedad * alturaSalto);
                }
            }
            velocidadVertical += gravedad * Time.deltaTime;

            float rapidez = velocidad;
            if (cuerpo.isGrounded && Caminos.EnCamino(transform.position)) rapidez *= Caminos.VelocidadNicias;
            Vector3 desplazamiento = horizontal * rapidez + Vector3.up * velocidadVertical;
            cuerpo.Move(desplazamiento * Time.deltaTime);

            if (cuerpo.isGrounded && horizontal.sqrMagnitude > 0.01f)
            {
                caminado += horizontal.magnitude * rapidez * Time.deltaTime;
                if (caminado >= 2.1f)
                {
                    caminado = 0f;
                    Sonido.Tocar(Efecto.Paso, 0.5f, true);
                }
            }

            Enfocado = activo ? BuscarInteractuable() : null;
            if (Enfocado != null && interactuar.WasPressedThisFrame())
            {
                Enfocado.Interactuar(gameObject);
            }
            else if (Enfocado is IConFaena conFaena && PulsaFaena() && !string.IsNullOrEmpty(conFaena.TextoFaena))
            {
                conFaena.Faena(control);
            }
        }

        /// <summary>F en el teclado o B en el mando: la faena del edificio que tienes delante.</summary>
        static bool PulsaFaena()
        {
            return (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                   || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        }

        void Mirar()
        {
            Vector2 giro = mirar.ReadValue<Vector2>() * sensibilidadRaton
                           + mirarMando.ReadValue<Vector2>() * (sensibilidadMando * Time.deltaTime);
            giro *= Ajustes.Sensibilidad;
            if (Ajustes.InvertirY) giro.y = -giro.y;

            transform.Rotate(0f, giro.x, 0f, Space.World);
            inclinacion = Mathf.Clamp(inclinacion - giro.y, -limiteVertical, limiteVertical);
            cabeza.localRotation = Quaternion.Euler(inclinacion, 0f, 0f);
        }

        // ---- A qué se está mirando ----
        // No hace falta clavar el punto de mira: vale lo que quede cerca de la línea de la mirada
        // (un cono de unos 20 grados), al alcance y sin una pared por medio. Lo que ya estaba
        // enfocado se mantiene mientras no se aparte mucho, para que el aviso no parpadee.

        const float AlcanceExtra = 0.7f;
        const float AnguloMaximo = 20f;
        const float AnguloMantener = 30f;
        readonly Collider[] cercanos = new Collider[64];

        /// <summary>Punto del mundo donde está lo enfocado (el HUD lo marca si queda lejos del centro).</summary>
        public Vector3 PuntoEnfocado { get; private set; }

        static bool Valido(IInteractuable candidato)
        {
            // Un interactuable sin texto no tiene nada que ofrecer ahora mismo.
            return candidato != null && !string.IsNullOrEmpty(candidato.Texto);
        }

        IInteractuable BuscarInteractuable()
        {
            Vector3 ojo = cabeza.position;
            Vector3 mira = cabeza.forward;
            float radio = alcance + AlcanceExtra;

            // Lo que está justo en el centro manda.
            if (Physics.Raycast(ojo, mira, out RaycastHit golpe, radio, capasInteraccion, QueryTriggerInteraction.Collide))
            {
                var directo = golpe.collider.GetComponentInParent<IInteractuable>();
                if (Valido(directo))
                {
                    PuntoEnfocado = golpe.point;
                    return directo;
                }
            }

            int n = Physics.OverlapSphereNonAlloc(ojo, radio, cercanos, capasInteraccion, QueryTriggerInteraction.Collide);
            IInteractuable mejor = null;
            float mejorPuntos = float.MaxValue;
            float mejorAngulo = 180f;
            Vector3 mejorPunto = ojo;
            float anguloPrevio = 180f;
            Vector3 puntoPrevio = ojo;

            for (int i = 0; i < n; i++)
            {
                Collider c = cercanos[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                var candidato = c.GetComponentInParent<IInteractuable>();
                if (!Valido(candidato)) continue;
                if (!Medir(c.bounds, ojo, mira, radio, out float angulo, out Vector3 punto, out float distancia)) continue;
                if (angulo > AnguloMantener) continue;
                if (!SeVe(ojo, punto, candidato) && !SeVe(ojo, c.bounds.center, candidato)) continue;

                if (ReferenceEquals(candidato, Enfocado) && angulo < anguloPrevio)
                {
                    anguloPrevio = angulo;
                    puntoPrevio = punto;
                }
                // Manda el ángulo; a igualdad, lo más cercano.
                float puntos = angulo * 10f + distancia;
                if (puntos < mejorPuntos)
                {
                    mejorPuntos = puntos;
                    mejorAngulo = angulo;
                    mejor = candidato;
                    mejorPunto = punto;
                }
            }

            if (Enfocado != null && anguloPrevio <= AnguloMantener && (mejor == null || anguloPrevio - mejorAngulo < 8f))
            {
                PuntoEnfocado = puntoPrevio;
                return Enfocado;
            }
            if (mejor != null && mejorAngulo <= AnguloMaximo)
            {
                PuntoEnfocado = mejorPunto;
                return mejor;
            }
            return null;
        }

        /// <summary>
        /// Lo cerca que pasa la mirada de una caja: recorre la línea de la mirada y busca el punto
        /// de la caja con menor ángulo respecto a ella. Si la mirada la atraviesa, el ángulo es 0.
        /// </summary>
        static bool Medir(Bounds caja, Vector3 ojo, Vector3 mira, float radio, out float angulo, out Vector3 punto,
            out float distancia)
        {
            angulo = 180f;
            punto = caja.center;
            distancia = radio;
            bool alguno = false;
            const int pasos = 10;
            for (int k = 1; k <= pasos; k++)
            {
                Vector3 enLinea = ojo + mira * (radio * k / pasos);
                Vector3 q = caja.ClosestPoint(enLinea);
                Vector3 hacia = q - ojo;
                float d = hacia.magnitude;
                if (d > radio) continue;
                float a = d < 0.05f ? 0f : Vector3.Angle(mira, hacia);
                if (!alguno || a < angulo - 0.01f)
                {
                    alguno = true;
                    angulo = a;
                    punto = q;
                    distancia = d;
                }
            }
            return alguno;
        }

        /// <summary>True si no hay nada de otro objeto entre el ojo y el punto.</summary>
        bool SeVe(Vector3 ojo, Vector3 punto, IInteractuable candidato)
        {
            Vector3 hacia = punto - ojo;
            float d = hacia.magnitude;
            if (d < 0.1f) return true;
            if (!Physics.Raycast(ojo, hacia / d, out RaycastHit golpe, d - 0.05f, capasInteraccion, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
            if (golpe.collider.transform.IsChildOf(transform)) return true;
            return ReferenceEquals(golpe.collider.GetComponentInParent<IInteractuable>(), candidato);
        }
    }
}
