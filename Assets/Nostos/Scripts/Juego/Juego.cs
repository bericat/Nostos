using System.Collections;
using System.Collections.Generic;
using Nostos.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>Un colono con el que empieza la partida.</summary>
    [System.Serializable]
    public class ColonoInicial
    {
        public string nombre = "Colono";
        public string rol = "colono";
        [Range(0f, 100f)] public float confianza = 20f;
        [Tooltip("1 = adulto. Marco, el niño, es más bajo.")]
        public float altura = 1f;
    }

    /// <summary>
    /// Puente entre Unity y la simulación: crea el Mundo, lo hace avanzar a tick fijo y
    /// mantiene los objetos de escena de cada edificio. Las reglas viven en Nostos.Sim.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class Juego : MonoBehaviour
    {
        public static Juego Instancia { get; private set; }

        /// <summary>Capa "Ignore Raycast": el NavMesh no la tiene en cuenta al generarse.</summary>
        public const int CapaColonos = 2;

        public DefinicionEdificio[] catalogo = new DefinicionEdificio[0];
        public float tamCelda = 2f;
        public int capacidadMochila = 8;
        public float segundosPorTick = 1f;
        [Tooltip("Cuántos ticks dura un día entero (600 = 10 minutos con 1 tick por segundo).")]
        public int ticksPorDia = 600;

        [Header("Colonos")]
        public ColonoInicial[] colonosIniciales = new ColonoInicial[0];
        [Tooltip("Donde se reúnen los colonos cuando no tienen nada que hacer.")]
        public Transform plaza;
        public Material materialColono;

        [Header("Partida")]
        [Tooltip("El jugador: su posición se guarda con la partida.")]
        public Transform jugador;

        readonly List<EdificioEnMundo> edificios = new List<EdificioEnMundo>();
        readonly List<ColonoEnMundo> colonos = new List<ColonoEnMundo>();
        RelojSimulacion reloj;
        float avisoHasta;
        bool durmiendo;

        public Mundo Mundo { get; private set; }
        public IReadOnlyList<EdificioEnMundo> Edificios => edificios;
        public IReadOnlyList<ColonoEnMundo> Colonos => colonos;
        /// <summary>Se lanza cuando aparece, cambia de estado o desaparece un edificio.</summary>
        public event System.Action EdificiosCambiados;

        public void NotificarEdificiosCambiados()
        {
            EdificiosCambiados?.Invoke();
        }

        public Vector3 PuntoPlaza => plaza != null ? plaza.position : Vector3.zero;

        /// <summary>Punto delante de la puerta de un edificio (la puerta mira hacia -Z).</summary>
        public Vector3 PuntoPuerta(Edificio edificio)
        {
            if (edificio == null) return PuntoPlaza;
            Vector3 centro = CentroDeArea(edificio.Origen, edificio.Datos.Ancho, edificio.Datos.Fondo);
            return centro + new Vector3(0f, 0f, -(edificio.Datos.Fondo * tamCelda * 0.5f + 1.2f));
        }
        /// <summary>Opacidad del fundido a negro (0 a 1). La pinta el HUD.</summary>
        public float Fundido { get; private set; }
        public string Aviso => Time.unscaledTime < avisoHasta ? aviso : null;
        string aviso;

        /// <summary>Hora del día interpolada entre ticks, para que el sol no vaya a saltos.</summary>
        public float HoraSuave
        {
            get
            {
                float hora = Mundo.Ciclo.Hora + reloj.Fraccion * Mundo.Ciclo.HorasPorTick;
                return hora >= 24f ? hora - 24f : hora;
            }
        }

        void Awake()
        {
            Instancia = this;
            Mundo = new Mundo(tamCelda, capacidadMochila, ticksPorDia);
            reloj = new RelojSimulacion(segundosPorTick);
            Mundo.Colonos.Aviso += texto => Avisar(texto, 5f);
            Mundo.Aviso += texto => Avisar(texto, 8f);
            CrearColonos();
        }

        void CrearColonos()
        {
            for (int i = 0; i < colonosIniciales.Length; i++)
            {
                ColonoInicial inicial = colonosIniciales[i];
                Colono sim = Mundo.Colonos.Anadir(inicial.nombre, inicial.rol, inicial.confianza);

                GameObject objeto = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                objeto.name = inicial.nombre;
                objeto.transform.SetParent(transform, false);
                float angulo = i * Mathf.PI * 2f / Mathf.Max(1, colonosIniciales.Length);
                objeto.transform.position = PuntoPlaza + new Vector3(Mathf.Cos(angulo) * 2.6f, 0.9f * inicial.altura, Mathf.Sin(angulo) * 2.6f);
                objeto.transform.localScale = new Vector3(0.65f, 0.9f * inicial.altura, 0.65f);
                // Disparador: se puede apuntar con el rayo de interacción, pero no estorba al andar ni al NavMesh.
                objeto.GetComponent<Collider>().isTrigger = true;
                objeto.layer = CapaColonos;
                if (materialColono != null) objeto.GetComponent<Renderer>().sharedMaterial = materialColono;

                var colono = objeto.AddComponent<ColonoEnMundo>();
                colono.Iniciar(this, sim, inicial.altura);
                colonos.Add(colono);
            }
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        /// <summary>True cuando Nicias ya ha despegado: el juego ha terminado.</summary>
        public bool Despegado { get; private set; }
        /// <summary>Con la pausa puesta (título, escenas) la simulación no avanza.</summary>
        public bool Pausa { get; set; }
        public bool Durmiendo => durmiendo;

        /// <summary>
        /// Para probar: el almacén no se acaba nunca (999 de todo) y las obras se terminan en cuanto se
        /// colocan. Se activa en el menú de pausa o con F1; con él puesto, F2 adelanta el reloj dos horas.
        /// No se guarda con la partida.
        /// </summary>
        public bool ModoDios { get; private set; }
        public const int CantidadModoDios = 999;

        public void CambiarModoDios()
        {
            ModoDios = !ModoDios;
            Avisar(ModoDios
                ? "Modo dios activado: almacén ilimitado y obras al instante."
                : "Modo dios desactivado.", 4f);
        }

        void AplicarModoDios()
        {
            Bolsa almacen = Mundo.Almacen;
            for (int r = 0; r < Recursos.Numero; r++)
            {
                var recurso = (Recurso)r;
                int falta = CantidadModoDios - almacen[recurso];
                if (falta > 0) almacen.Meter(recurso, falta);
            }
            foreach (EdificioEnMundo e in edificios)
            {
                if (e.Sim != null && !e.Sim.Terminado) e.Sim.Restaurar(EstadoObra.Terminado, null, 0);
            }
        }

        public DefinicionEdificio Definicion(string id)
        {
            foreach (DefinicionEdificio d in catalogo)
            {
                if (d != null && d.id == id) return d;
            }
            return null;
        }

        public void DemolerTodo()
        {
            for (int i = edificios.Count - 1; i >= 0; i--) Demoler(edificios[i]);
        }

        public void GuardarPartida()
        {
            Guardado.Guardar(this, jugador);
        }

        /// <summary>El final: la nave está terminada y Nicias se va.</summary>
        public void Despegar()
        {
            if (Despegado || !Mundo.Nave.ListaParaDespegar) return;
            StartCoroutine(RutinaDespegar());
        }

        IEnumerator RutinaDespegar()
        {
            Despegado = true;
            durmiendo = true;
            Sonido.Tocar(Efecto.Despegue);
            yield return new WaitForSecondsRealtime(1.5f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 4.5f)
            {
                Fundido = t;
                yield return null;
            }
            Fundido = 1f;
        }

        void Update()
        {
            if (Despegado) return;
            Keyboard teclado = Keyboard.current;
            if (teclado != null && !durmiendo && !Pausa)
            {
                if (teclado.f1Key.wasPressedThisFrame)
                {
                    CambiarModoDios();
                }
                else if (ModoDios && teclado.f2Key.wasPressedThisFrame)
                {
                    // Para probar la noche y el día sin esperar: el reloj salta dos horas.
                    float hora = Mundo.Ciclo.Hora + 2f;
                    int dia = Mundo.Ciclo.Dia;
                    if (hora >= 24f)
                    {
                        hora -= 24f;
                        dia++;
                    }
                    Mundo.Ciclo.Fijar(hora, dia);
                    int h = Mathf.FloorToInt(hora);
                    int m = Mathf.FloorToInt((hora - h) * 60f);
                    Avisar("Modo dios: son las " + h.ToString("00") + ":" + m.ToString("00") + ".");
                }
                else if (teclado.f5Key.wasPressedThisFrame)
                {
                    GuardarPartida();
                    Avisar("Partida guardada.");
                }
                else if (teclado.f9Key.wasPressedThisFrame)
                {
                    Avisar(Guardado.Cargar(this, jugador) ? "Partida cargada." : "No hay ninguna partida guardada.");
                }
            }
            if (ModoDios) AplicarModoDios();
            if (durmiendo || Pausa) return;
            int ticks = reloj.Avanzar(Time.deltaTime);
            for (int i = 0; i < ticks; i++) Mundo.Tick();
        }

        public void Avisar(string texto, float segundos = 3.5f)
        {
            aviso = texto;
            avisoHasta = Time.unscaledTime + segundos;
        }

        public Vector3 CentroDeArea(Celda origen, int ancho, int fondo)
        {
            Mundo.Rejilla.CentroDeArea(origen, ancho, fondo, out float x, out float z);
            return new Vector3(x, 0f, z);
        }

        /// <summary>Coloca la obra de un edificio en el mundo. Devuelve null si el solar no está libre.</summary>
        public EdificioEnMundo Construir(DefinicionEdificio definicion, Celda origen)
        {
            Edificio sim = Mundo.Colocar(definicion.Datos, origen);
            if (sim == null) return null;

            var objeto = new GameObject(definicion.nombre);
            objeto.transform.SetParent(transform, false);
            objeto.transform.position = CentroDeArea(origen, definicion.ancho, definicion.fondo);
            var edificio = objeto.AddComponent<EdificioEnMundo>();
            edificio.Iniciar(this, definicion, sim);
            edificios.Add(edificio);
            NotificarEdificiosCambiados();
            return edificio;
        }

        public bool Demoler(EdificioEnMundo edificio)
        {
            if (edificio == null || !Mundo.Quitar(edificio.Sim)) return false;
            edificios.Remove(edificio);
            edificio.gameObject.SetActive(false);
            Destroy(edificio.gameObject);
            NotificarEdificiosCambiados();
            return true;
        }

        public EdificioEnMundo EnCelda(Celda celda)
        {
            Edificio sim = Mundo.EnCelda(celda);
            if (sim == null) return null;
            foreach (EdificioEnMundo e in edificios)
            {
                if (e.Sim == sim) return e;
            }
            return null;
        }

        /// <summary>Dormir en el refugio: fundido a negro y salto a la mañana siguiente.</summary>
        public void Dormir()
        {
            if (durmiendo) return;
            if (!Mundo.Ciclo.PuedeDormir)
            {
                Avisar("Todavía es de día. Podrás dormir a partir de las 18:00.");
                return;
            }
            StartCoroutine(RutinaDormir());
        }

        IEnumerator RutinaDormir()
        {
            durmiendo = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
            {
                Fundido = t;
                yield return null;
            }
            Fundido = 1f;
            Mundo.Dormir();
            yield return new WaitForSecondsRealtime(0.6f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 1.2f)
            {
                Fundido = t;
                yield return null;
            }
            Fundido = 0f;
            durmiendo = false;
            GuardarPartida();
            Avisar("Día " + Mundo.Ciclo.Dia + ". Amanece en Nova Lavinia. Partida guardada.");
        }
    }
}
