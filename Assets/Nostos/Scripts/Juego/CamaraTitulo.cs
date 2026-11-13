using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Cámara de la pantalla de título. Con `objetivo` gira alrededor de él; sin objetivo hace un
    /// travelling muy lento entre `desde` y `hasta` mirando al valle (y vuelve), con un leve
    /// balanceo, para que el paisaje respire detrás del menú.
    /// </summary>
    public class CamaraTitulo : MonoBehaviour
    {
        public Transform objetivo;
        public float radio = 3.6f;
        public float altura = 1.5f;
        public float alturaMirada = 1.15f;
        public float gradosPorSegundo = 7f;
        public float anguloInicial = 150f;

        [Header("Vista del valle (sin objetivo)")]
        public Vector3 desde = new Vector3(78f, 52f, -78f);
        public Vector3 hasta = new Vector3(64f, 55f, -90f);
        public Vector3 mira = new Vector3(-60f, 40f, 60f);
        public float segundosTravelling = 70f;

        void LateUpdate()
        {
            float t = Time.unscaledTime;
            if (objetivo == null)
            {
                float k = 0.5f - 0.5f * Mathf.Cos(t / segundosTravelling * Mathf.PI);
                Vector3 posicion = Vector3.Lerp(desde, hasta, k);
                posicion.y += Mathf.Sin(t * 0.21f) * 0.35f;
                Vector3 punto = mira + new Vector3(Mathf.Sin(t * 0.05f) * 6f, 0f, 0f);
                transform.position = posicion;
                transform.rotation = Quaternion.LookRotation(punto - posicion, Vector3.up)
                                     * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.17f) * 0.4f);
                return;
            }
            float angulo = (anguloInicial + t * gradosPorSegundo) * Mathf.Deg2Rad;
            Vector3 centro = objetivo.position;
            transform.position = centro + new Vector3(Mathf.Sin(angulo) * radio, altura, Mathf.Cos(angulo) * radio);
            transform.rotation = Quaternion.LookRotation(centro + Vector3.up * alturaMirada - transform.position, Vector3.up);
        }
    }
}
