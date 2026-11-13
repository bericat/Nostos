using UnityEngine;

namespace Nostos
{
    public enum Efecto
    {
        Paso,
        Golpe,
        Entrega,
        Campana,
        Pagina,
        Despegue
    }

    /// <summary>
    /// Sonido del juego: efectos sueltos, el ambiente (viento de día, grillos de noche) y la
    /// música del título y del final. Los clips los genera Pipeline/generar_sonidos.py.
    /// </summary>
    public class Sonido : MonoBehaviour
    {
        public static Sonido Instancia { get; private set; }

        public Juego juego;
        public AudioClip paso;
        public AudioClip golpe;
        public AudioClip entrega;
        public AudioClip campana;
        public AudioClip pagina;
        public AudioClip despegue;
        public AudioClip viento;
        public AudioClip noche;
        public AudioClip musica;
        [Range(0f, 1f)] public float volumenAmbiente = 0.35f;
        [Range(0f, 1f)] public float volumenMusica = 0.4f;

        /// <summary>La música suena en el título y en el final; el resto del tiempo, solo ambiente.</summary>
        public bool ConMusica { get; set; }

        AudioSource efectos;
        AudioSource fuenteViento;
        AudioSource fuenteNoche;
        AudioSource fuenteMusica;

        void Awake()
        {
            Instancia = this;
            efectos = Fuente(null);
            fuenteViento = Fuente(viento);
            fuenteNoche = Fuente(noche);
            fuenteMusica = Fuente(musica);
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        AudioSource Fuente(AudioClip bucle)
        {
            var fuente = gameObject.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f;
            if (bucle != null)
            {
                fuente.clip = bucle;
                fuente.loop = true;
                fuente.volume = 0f;
                fuente.Play();
            }
            return fuente;
        }

        void Update()
        {
            float dia = 1f;
            if (juego != null && juego.Mundo != null)
            {
                float t = (juego.HoraSuave - 6f) / 12f;
                dia = t > 0f && t < 1f ? Mathf.Clamp01(Mathf.Sin(t * Mathf.PI) * 4f) : 0f;
            }
            float ritmo = Time.unscaledDeltaTime * 0.6f;
            float ambiente = ConMusica ? 0.4f : 1f;
            fuenteViento.volume = Mathf.MoveTowards(fuenteViento.volume, volumenAmbiente * (0.3f + 0.7f * dia) * ambiente, ritmo);
            fuenteNoche.volume = Mathf.MoveTowards(fuenteNoche.volume, volumenAmbiente * (1f - dia) * ambiente, ritmo);
            fuenteMusica.volume = Mathf.MoveTowards(fuenteMusica.volume, ConMusica ? volumenMusica * Ajustes.Musica : 0f, ritmo * 0.5f);
        }

        AudioClip Clip(Efecto efecto)
        {
            switch (efecto)
            {
                case Efecto.Paso: return paso;
                case Efecto.Golpe: return golpe;
                case Efecto.Entrega: return entrega;
                case Efecto.Campana: return campana;
                case Efecto.Pagina: return pagina;
                default: return despegue;
            }
        }

        /// <summary>Toca un efecto. No hace nada si no hay sonido en la escena.</summary>
        public static void Tocar(Efecto efecto, float volumen = 1f, bool variarTono = false)
        {
            Sonido sonido = Instancia;
            if (sonido == null || sonido.efectos == null) return;
            AudioClip clip = sonido.Clip(efecto);
            if (clip == null) return;
            sonido.efectos.pitch = variarTono ? Random.Range(0.88f, 1.12f) : 1f;
            sonido.efectos.PlayOneShot(clip, volumen);
        }
    }
}
