using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Un fuego del paquete "Fire Effects" (hogares, hornos, la hoguera, antorchas) con su luz que
    /// parpadea. Si se apaga, las partículas dejan de salir y la luz se va despacio.
    /// </summary>
    public class Llama : MonoBehaviour
    {
        public Light luz;
        public float intensidad = 3f;
        [Tooltip("Encendida o no. Los hornos de los talleres lo cambian según trabajen (EdificioEnMundo).")]
        public bool encendida = true;

        ParticleSystem[] particulas;
        float actual = -1f;
        bool emitiendo = true;
        float semilla;

        void Awake()
        {
            particulas = GetComponentsInChildren<ParticleSystem>(true);
            semilla = Random.value * 50f;
        }

        void Update()
        {
            if (encendida != emitiendo)
            {
                emitiendo = encendida;
                foreach (ParticleSystem p in particulas)
                {
                    if (p == null) continue;
                    if (encendida) p.Play(true);
                    else p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            if (luz == null) return;
            float objetivo = encendida ? intensidad * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 7f, semilla)) : 0f;
            actual = actual < 0f ? objetivo : Mathf.Lerp(actual, objetivo, 1f - Mathf.Exp(-10f * Time.deltaTime));
            luz.intensity = actual;
            luz.enabled = actual > 0.02f;
        }
    }
}
