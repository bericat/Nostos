using System.Collections;
using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// La nave, visible en el mapa desde el principio. Sus cinco módulos se montan a pie y en
    /// orden: cada uno aparece sobre el casco al completarse. El motor y la navegación piden además
    /// una prueba (PruebasNave). Con los cinco, un vuelo de prueba con el piloto automático, y
    /// después Nicias puede despegar.
    /// </summary>
    public class CascoNave : MonoBehaviour, IInteractuable
    {
        [Header("Solar que ocupa en la rejilla")]
        public int celdaX;
        public int celdaZ;
        public int ancho = 3;
        public int fondo = 6;

        [Header("Naufragio: así está al empezar la partida")]
        [Tooltip("Lo que se inclina y se hunde mientras el casco está roto (el modelo, sin la raíz).")]
        public Transform cuerpo;
        public Vector3 giroNaufragio = new Vector3(5f, 0f, -10f);
        public float hundimiento = 0.45f;
        [Tooltip("Piezas arrancadas tiradas por el surco, una entrada por módulo: se recogen al montarlo.")]
        public GameObject[] restos = new GameObject[0];
        [Tooltip("Fuegos del choque: arden la primera mañana y se apagan hacia mediodía.")]
        public ParticleSystem[] fuegos = new ParticleSystem[0];
        public Light[] lucesFuego = new Light[0];
        [Tooltip("Humo que sale del casco hasta que se repara.")]
        public ParticleSystem[] humos = new ParticleSystem[0];

        public const float AlturaVueloPrueba = 7f;
        public const float ConfianzaVueloPrueba = 6f;

        public static CascoNave Instancia { get; private set; }

        readonly GameObject[] piezas = new GameObject[5];
        int mostrados = -1;
        float velocidadSubida;
        Vector3 reposo;
        float alturaVuelo;
        float temblorHasta;
        float fuerzaTemblor;
        float enderezada;
        bool naufragioIniciado;
        float[] intensidadFuego;
        float[] ritmoHumo;

        /// <summary>Está haciendo el vuelo de prueba.</summary>
        public bool Volando { get; private set; }

        void Awake()
        {
            Instancia = this;
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        /// <summary>La nave tiembla un rato (encendidos del motor, aterrizaje).</summary>
        public void Temblar(float segundos, float fuerza)
        {
            bool yaTiembla = Time.unscaledTime < temblorHasta;
            fuerzaTemblor = yaTiembla ? Mathf.Max(fuerzaTemblor, fuerza) : fuerza;
            temblorHasta = Mathf.Max(temblorHasta, Time.unscaledTime + segundos);
        }

        void Start()
        {
            reposo = transform.position;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                for (int i = 0; i < piezas.Length; i++)
                {
                    if (t.name == "Modulo" + (i + 1)) piezas[i] = t.gameObject;
                }
            }

            intensidadFuego = new float[lucesFuego.Length];
            for (int i = 0; i < lucesFuego.Length; i++) intensidadFuego[i] = lucesFuego[i] != null ? lucesFuego[i].intensity : 0f;
            ritmoHumo = new float[humos.Length];
            for (int i = 0; i < humos.Length; i++) ritmoHumo[i] = humos[i] != null ? humos[i].emission.rateOverTimeMultiplier : 0f;

            Juego juego = Juego.Instancia;
            if (juego != null) juego.Mundo.Rejilla.OcuparArea(new Celda(celdaX, celdaZ), ancho, fondo);
        }

        /// <summary>
        /// La nave llega estrellada: inclinada y medio hundida en su surco, con humo y algún fuego, y
        /// las piezas arrancadas tiradas por el camino. Al montar el casco se endereza; cada módulo
        /// montado recoge sus piezas del surco.
        /// </summary>
        void ActualizarNaufragio(Juego juego)
        {
            ModuloNave[] modulos = juego.Mundo.Nave.Modulos;
            bool rota = modulos.Length > 0 && !modulos[0].Completo;
            float objetivo = rota ? 0f : 1f;
            enderezada = naufragioIniciado ? Mathf.MoveTowards(enderezada, objetivo, Time.deltaTime / 2.5f) : objetivo;
            naufragioIniciado = true;
            if (cuerpo != null)
            {
                float s = Mathf.SmoothStep(0f, 1f, enderezada);
                cuerpo.localRotation = Quaternion.Slerp(Quaternion.Euler(giroNaufragio), Quaternion.identity, s);
                cuerpo.localPosition = Vector3.down * (hundimiento * (1f - s));
            }

            for (int i = 0; i < restos.Length; i++)
            {
                bool quedan = i >= modulos.Length || !modulos[i].Completo;
                if (restos[i] != null && restos[i].activeSelf != quedan) restos[i].SetActive(quedan);
            }

            // El fuego aguanta la primera mañana y se apaga entre las 10 y las 14.
            CicloDia ciclo = juego.Mundo.Ciclo;
            float fuego = rota && ciclo.Dia == 1 ? Mathf.Clamp01((14f - ciclo.Hora) / 4f) : 0f;
            foreach (ParticleSystem f in fuegos)
            {
                if (f == null) continue;
                if (fuego > 0f && !f.isEmitting) f.Play(true);
                else if (fuego <= 0f && f.isEmitting) f.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            for (int i = 0; i < lucesFuego.Length; i++)
            {
                if (lucesFuego[i] == null) continue;
                float parpadeo = 0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 7f, i * 3.1f);
                lucesFuego[i].intensity = intensidadFuego[i] * fuego * parpadeo;
                lucesFuego[i].enabled = fuego > 0f;
            }

            // El humo sigue, más flojo, hasta que el casco se repara.
            for (int i = 0; i < humos.Length; i++)
            {
                ParticleSystem h = humos[i];
                if (h == null) continue;
                var emision = h.emission;
                emision.rateOverTimeMultiplier = ritmoHumo[i] * (0.35f + 0.65f * fuego);
                if (rota && !h.isEmitting) h.Play(true);
                else if (!rota && h.isEmitting) h.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void Update()
        {
            Juego juego = Juego.Instancia;
            if (juego == null) return;
            if (juego.Despegado)
            {
                // El despegue: la nave tiembla y sube cada vez más deprisa.
                velocidadSubida += 2.2f * Time.unscaledDeltaTime;
                Vector3 temblor = new Vector3(Mathf.PerlinNoise(Time.unscaledTime * 22f, 0f) - 0.5f, 0f,
                    Mathf.PerlinNoise(0f, Time.unscaledTime * 22f) - 0.5f) * 0.06f;
                transform.position += Vector3.up * (velocidadSubida * Time.unscaledDeltaTime) + temblor;
                return;
            }

            // Temblor y vuelo de prueba, siempre alrededor del punto donde está posada.
            Vector3 desvio = Vector3.zero;
            if (Time.unscaledTime < temblorHasta)
            {
                float t = Time.unscaledTime * 26f;
                float queda = Mathf.Clamp01((temblorHasta - Time.unscaledTime) / 0.3f);
                desvio = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, (Mathf.PerlinNoise(0f, t) - 0.5f) * 0.5f,
                    Mathf.PerlinNoise(t, t) - 0.5f) * (2f * fuerzaTemblor * queda);
            }
            transform.position = reposo + Vector3.up * alturaVuelo + desvio;
            ActualizarNaufragio(juego);

            int completos = juego.Mundo.Nave.Completos;
            if (completos == mostrados) return;
            mostrados = completos;
            ModuloNave[] modulos = juego.Mundo.Nave.Modulos;
            for (int i = 0; i < piezas.Length; i++)
            {
                if (piezas[i] != null) piezas[i].SetActive(i < modulos.Length && modulos[i].Completo);
            }
        }

        public string Texto
        {
            get
            {
                Juego juego = Juego.Instancia;
                if (juego == null) return null;
                Nave nave = juego.Mundo.Nave;
                ModuloNave modulo = nave.Actual;
                if (Volando || PruebasNave.Ocupada) return null;
                if (modulo == null)
                {
                    if (juego.Despegado) return null;
                    return nave.VueloDePrueba ? "Despegar. Volver a Calcis." : "Vuelo de prueba con el piloto automático";
                }

                string cabecera = "Módulo " + (nave.Completos + 1) + " de " + nave.Modulos.Length + ", " + modulo.Nombre + ": ";
                if (nave.Completos >= 1 && !juego.Mundo.HayAstillero && !juego.ModoDios) return cabecera + "hace falta un astillero para montarlo";
                if (modulo.EsperaPrueba)
                {
                    if (modulo.Prueba == PruebaNave.Rumbo && !juego.Mundo.Ciclo.EsDeNoche && !juego.ModoDios)
                        return cabecera + "montado. Falta fijar el rumbo a Calcis, de noche (después de las 20:00)";
                    return cabecera + "montado. Hacer la prueba: " + PruebasNave.Describir(modulo.Prueba);
                }
                if (modulo.FaltaMaterial) return cabecera + "falta " + modulo.DescribirFalta();
                return cabecera + "el material está, pero Nova Lavinia no confía lo bastante (hace falta "
                       + modulo.ConfianzaMinima + " de confianza global)";
            }
        }

        public void Interactuar(GameObject quien)
        {
            Juego juego = Juego.Instancia;
            if (juego == null) return;
            Nave nave = juego.Mundo.Nave;
            ModuloNave modulo = nave.Actual;
            if (Volando || PruebasNave.Ocupada || juego.Despegado) return;
            if (modulo == null)
            {
                if (!nave.VueloDePrueba) StartCoroutine(RutinaVuelo(juego));
                else juego.Despegar();
                return;
            }

            float confianza = juego.Mundo.Colonos.ConfianzaGlobal;
            bool astillero = juego.Mundo.HayAstillero;
            if (nave.Completos >= 1 && !astillero && !juego.ModoDios)
            {
                juego.Avisar("El casco ya cierra, pero el resto no se monta a pulso. Construye un astillero (modo constructor).", 6f);
                return;
            }
            int entregadas = nave.Entregar(juego.Mundo.Mochila, confianza, out bool completado, out bool faltaConfianza);
            if ((astillero || juego.ModoDios) && !completado)
            {
                // Con astillero (o en modo dios), la grúa trae del almacén lo que no llevas encima.
                entregadas += nave.Entregar(juego.Mundo.Almacen, confianza, out completado, out faltaConfianza);
            }
            if (completado) Sonido.Tocar(Efecto.Campana);
            else if (entregadas > 0) Sonido.Tocar(Efecto.Entrega);
            if (completado)
            {
                juego.Avisar(nave.Terminada
                    ? "La nave está montada. Antes de irte, un vuelo de prueba con el piloto automático."
                    : modulo.Nombre + " montado. Siguiente: " + nave.Actual.Nombre + ".", 6f);
            }
            else if (modulo.EsperaPrueba)
            {
                // El material está: la prueba empieza ya (o, si es el rumbo de día, avisa de cuándo).
                if (entregadas > 0) juego.Avisar(modulo.Nombre + " montado. Falta la prueba: " + PruebasNave.Describir(modulo.Prueba) + ".", 5f);
                PruebasNave.Instancia.Empezar(modulo.Prueba);
            }
            else if (faltaConfianza)
            {
                juego.Avisar("Sin los especialistas de Nova Lavinia no se puede cargar el combustible. Gánate su confianza.", 6f);
            }
            else if (entregadas > 0)
            {
                juego.Avisar("Montas " + entregadas + ". Aún falta: " + modulo.DescribirFalta() + ".");
            }
            else
            {
                juego.Avisar("No llevas nada de lo que pide " + modulo.Nombre + ": " + modulo.DescribirFalta() + ".");
            }
        }

        /// <summary>
        /// El vuelo de prueba: la nave sube sola, sin nadie dentro, se sostiene un momento y vuelve a
        /// posarse. Nova Lavinia lo ve, y eso también cuenta.
        /// </summary>
        IEnumerator RutinaVuelo(Juego juego)
        {
            Volando = true;
            juego.Avisar("Piloto automático en marcha. Apártate.", 4f);
            Sonido.Tocar(Efecto.Despegue, 0.45f);
            Temblar(1.4f, 0.05f);
            yield return Esperar(juego, 1.2f);

            for (float t = 0f; t < 1f; t += Paso(juego) / 3f)
            {
                alturaVuelo = Mathf.SmoothStep(0f, AlturaVueloPrueba, t);
                yield return null;
            }
            for (float t = 0f; t < 2.5f; t += Paso(juego))
            {
                alturaVuelo = AlturaVueloPrueba + Mathf.Sin(t * 2.4f) * 0.35f;
                yield return null;
            }
            float desde = alturaVuelo;
            for (float t = 0f; t < 1f; t += Paso(juego) / 3.5f)
            {
                alturaVuelo = Mathf.SmoothStep(desde, 0f, t);
                yield return null;
            }
            alturaVuelo = 0f;
            Temblar(0.4f, 0.06f);
            Sonido.Tocar(Efecto.Golpe, 0.9f);

            juego.Mundo.Nave.VueloDePrueba = true;
            juego.Mundo.Colonos.CambiarConfianzaDeTodos(ConfianzaVueloPrueba);
            Sonido.Tocar(Efecto.Campana);
            juego.Avisar("La nave vuela. Media Nova Lavinia lo ha visto desde los tejados. Cuando quieras, despega.", 7f);
            Volando = false;
        }

        /// <summary>Tiempo que pasa en este frame; con el menú de pausa abierto, ninguno (la nave se queda quieta).</summary>
        static float Paso(Juego juego) => juego.Pausa ? 0f : Time.deltaTime;

        /// <summary>Espera en tiempo de juego.</summary>
        static IEnumerator Esperar(Juego juego, float segundos)
        {
            for (float t = 0f; t < segundos; t += Paso(juego)) yield return null;
        }
    }
}
