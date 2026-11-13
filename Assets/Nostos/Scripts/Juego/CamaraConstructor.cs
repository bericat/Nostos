using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>
    /// Cámara de constructor: un pivote sobre el suelo que se desplaza y gira,
    /// con la cámara de Cinemachine colgada a cierta distancia e inclinación.
    /// Usa perspectiva con campo de visión estrecho en vez de ortográfica para que
    /// la transición con la cámara en primera persona sea continua.
    /// </summary>
    public class CamaraConstructor : MonoBehaviour
    {
        public ControlModo control;
        public CinemachineCamera camara;
        [Tooltip("Al entrar en modo constructor el pivote se centra aquí (el jugador).")]
        public Transform objetivoInicial;

        [Header("Desplazamiento")]
        public float velocidadDesplazamiento = 0.6f;
        public float limite = 90f;

        [Header("Rotación")]
        public float velocidadRotacion = 110f;
        public float inclinacion = 40f;
        public float rumboInicial = 45f;

        [Header("Zoom")]
        public float distancia = 34f;
        public float distanciaMinima = 12f;
        public float distanciaMaxima = 80f;
        public float pasoZoom = 5f;
        public float velocidadZoomMando = 30f;
        public float suavizado = 10f;

        InputAction desplazar;
        InputAction rotar;
        InputAction zoom;
        InputAction zoomMando;
        float distanciaObjetivo;
        bool estabaActivo;

        void Start()
        {
            InputActionMap mapa = control.acciones.FindActionMap(ControlModo.MapaConstructor, true);
            desplazar = mapa.FindAction("Desplazar", true);
            rotar = mapa.FindAction("Rotar", true);
            zoom = mapa.FindAction("Zoom", true);
            zoomMando = mapa.FindAction("Zoom mando", true);

            distanciaObjetivo = distancia;
            transform.rotation = Quaternion.Euler(0f, rumboInicial, 0f);
            Centrar();
            ColocarCamara();
        }

        void Update()
        {
            bool activo = control.Modo == ModoJuego.Constructor;
            if (activo && !estabaActivo) Centrar();
            estabaActivo = activo;
            if (!activo) return;

            float dt = Time.unscaledDeltaTime;

            // Desplazamiento relativo a hacia dónde mira la cámara; más rápido cuanto más lejos.
            Vector2 entrada = desplazar.ReadValue<Vector2>();
            Vector3 movimiento = transform.right * entrada.x + transform.forward * entrada.y;
            Vector3 posicion = transform.position + movimiento * (velocidadDesplazamiento * distancia * dt);
            posicion.x = Mathf.Clamp(posicion.x, -limite, limite);
            posicion.z = Mathf.Clamp(posicion.z, -limite, limite);
            transform.position = posicion;

            transform.Rotate(0f, rotar.ReadValue<float>() * velocidadRotacion * dt, 0f, Space.World);

            float rueda = Mathf.Clamp(zoom.ReadValue<float>(), -1f, 1f);
            distanciaObjetivo -= rueda * pasoZoom;
            distanciaObjetivo -= zoomMando.ReadValue<float>() * velocidadZoomMando * dt;
            distanciaObjetivo = Mathf.Clamp(distanciaObjetivo, distanciaMinima, distanciaMaxima);
            distancia = Mathf.Lerp(distancia, distanciaObjetivo, 1f - Mathf.Exp(-suavizado * dt));

            ColocarCamara();
        }

        void Centrar()
        {
            if (objetivoInicial == null) return;
            Vector3 p = objetivoInicial.position;
            transform.position = new Vector3(p.x, 0f, p.z);
        }

        void ColocarCamara()
        {
            Quaternion giro = Quaternion.Euler(inclinacion, 0f, 0f);
            Transform t = camara.transform;
            t.localRotation = giro;
            t.localPosition = giro * new Vector3(0f, 0f, -distancia);
        }
    }
}
