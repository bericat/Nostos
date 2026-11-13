using System.Collections.Generic;
using Nostos.Sim;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nostos
{
    /// <summary>
    /// El hilo narrativo: pantalla de título, introducción y las escenas que saltan cuando la
    /// partida alcanza un hito (primer refugio, primer acuerdo, el arconte, cada módulo de la nave).
    /// Mientras hay texto en pantalla, la simulación está en pausa.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class Narrativa : MonoBehaviour
    {
        enum Fase
        {
            Titulo,
            Intro,
            Juego
        }

        public static Narrativa Instancia { get; private set; }

        public ControlModo control;
        public Juego juego;
        public CinemachineCamera camaraTitulo;
        [Tooltip("Desmárcalo para entrar directo al juego mientras se prueba.")]
        public bool mostrarTitulo = true;

        readonly HashSet<string> vistos = new HashSet<string>();
        readonly Queue<string> cola = new Queue<string>();
        Fase fase = Fase.Juego;
        int tarjeta;
        Linea[] escena;
        int linea;
        float desde;
        float velo;
        float finalDesde = -1f;
        int charlas;
        readonly Portada portada = new Portada();


        /// <summary>True en el título y en la introducción: el HUD no se dibuja.</summary>
        public bool EnTitulo => fase != Fase.Juego;
        public bool EnEscena => escena != null;

        void Awake()
        {
            Instancia = this;
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        void Start()
        {
            Ajustes.Abrir();
            if (mostrarTitulo)
            {
                fase = Fase.Titulo;
                desde = Time.unscaledTime;
                portada.Empezar();
                juego.Pausa = true;
                control.Bloquear(true);
                if (camaraTitulo != null) camaraTitulo.Priority.Value = 30;
                if (Sonido.Instancia != null) Sonido.Instancia.ConMusica = true;
            }
            else
            {
                if (camaraTitulo != null) camaraTitulo.Priority.Value = 0;
            }
        }

        // ---- Guardado ----

        public List<string> Volcar()
        {
            return new List<string>(vistos);
        }

        /// <summary>Tras cargar una partida: recupera lo ya visto y da por vistos los hitos ya cumplidos.</summary>
        public void Cargar(List<string> guardados)
        {
            vistos.Clear();
            cola.Clear();
            if (guardados != null)
            {
                foreach (string id in guardados) vistos.Add(id);
            }
            Vigilar(false);
        }

        // ---- Hitos ----

        void Hito(string id, bool cumplido, bool mostrar)
        {
            if (!cumplido || vistos.Contains(id)) return;
            vistos.Add(id);
            if (mostrar) cola.Enqueue(id);
        }

        void Vigilar(bool mostrar)
        {
            Mundo mundo = juego.Mundo;
            if (mundo == null) return;

            Hito("refugio", mundo.PrimerTerminado(TipoEdificio.Refugio, null) != null, mostrar);
            Hito("dia2", mundo.Ciclo.Dia >= 2, mostrar);
            Hito("astillero", mundo.HayAstillero, mostrar);
            Hito("teleferico", mundo.HayTeleferico, mostrar);
            Hito("arconte", mundo.Tributo.ArcontePresente, mostrar);

            bool acuerdo = false;
            bool confianza = false;
            foreach (Colono c in mundo.Colonos.Colonos)
            {
                if (c.Acuerdo != null) acuerdo = true;
                if (c.Nivel == NivelConfianza.Alta) confianza = true;
            }
            Hito("acuerdo", acuerdo, mostrar);
            Hito("confianza", confianza, mostrar);

            int completos = mundo.Nave.Completos;
            for (int i = 1; i <= completos && i <= 5; i++) Hito("modulo" + i, true, mostrar);
            Hito("vuelo", mundo.Nave.VueloDePrueba, mostrar);
        }

        // ---- Bucle ----

        void Update()
        {
            if (velo > 0f) velo = Mathf.Max(0f, velo - Time.unscaledDeltaTime / 1.6f);

            if (fase == Fase.Titulo)
            {
                ActualizarTitulo();
                return;
            }
            if (fase == Fase.Intro)
            {
                if (Avanza())
                {
                    Sonido.Tocar(Efecto.Pagina);
                    tarjeta++;
                    desde = Time.unscaledTime;
                    if (tarjeta >= Textos.Intro.Length) Empezar(Textos.PrimerObjetivo);
                }
                return;
            }

            if (juego.Despegado)
            {
                ActualizarFinal();
                return;
            }

            if (escena != null)
            {
                if (Avanza())
                {
                    Sonido.Tocar(Efecto.Pagina);
                    linea++;
                    desde = Time.unscaledTime;
                    if (linea >= escena.Length) CerrarEscena();
                }
                return;
            }

            Vigilar(true);
            if (cola.Count > 0 && !control.Bloqueado && !juego.Durmiendo && juego.Fundido <= 0f)
            {
                AbrirEscena(cola.Dequeue());
            }
        }

        void ActualizarTitulo()
        {
            Ejecutar(portada.Actualizar());
        }

        void Ejecutar(Portada.Accion accion)
        {
            switch (accion)
            {
                case Portada.Accion.Continuar:
                    Continuar();
                    break;
                case Portada.Accion.Nueva:
                    NuevaPartida();
                    break;
                case Portada.Accion.Salir:
                    Salir();
                    break;
            }
        }

        public static void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void NuevaPartida()
        {
            Sonido.Tocar(Efecto.Pagina);
            fase = Fase.Intro;
            tarjeta = 0;
            desde = Time.unscaledTime;
        }

        void Continuar()
        {
            Sonido.Tocar(Efecto.Pagina);
            bool cargada = Guardado.Cargar(juego, juego.jugador);
            Empezar(cargada
                ? "Día " + juego.Mundo.Ciclo.Dia + ". Nova Lavinia sigue donde la dejaste."
                : "No se ha podido leer la partida guardada. Empiezas de nuevo.");
        }

        void Empezar(string aviso)
        {
            fase = Fase.Juego;
            velo = 1f;
            juego.Pausa = false;
            if (camaraTitulo != null) camaraTitulo.Priority.Value = 0;
            if (Sonido.Instancia != null) Sonido.Instancia.ConMusica = false;
            control.Bloquear(false);
            juego.Avisar(aviso, 12f);
        }

        void AbrirEscena(string id)
        {
            Linea[] lineas = Textos.Escena(id);
            if (lineas == null || lineas.Length == 0) return;
            escena = lineas;
            linea = 0;
            desde = Time.unscaledTime;
            juego.Pausa = true;
            control.Bloquear(true);
        }

        void CerrarEscena()
        {
            escena = null;
            juego.Pausa = false;
            control.Bloquear(false);
        }

        void ActualizarFinal()
        {
            if (juego.Fundido < 1f) return;
            if (finalDesde < 0f)
            {
                finalDesde = Time.unscaledTime;
                if (Sonido.Instancia != null) Sonido.Instancia.ConMusica = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Time.unscaledTime - finalDesde < 4f) return;
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            bool volver = (teclado != null && teclado.enterKey.wasPressedThisFrame)
                          || (mando != null && mando.buttonSouth.wasPressedThisFrame);
            if (volver) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Pasar de tarjeta o de frase: E, Intro, espacio, clic o A. Con un pequeño margen para no saltarse nada.</summary>
        bool Avanza()
        {
            if (Time.unscaledTime - desde < 0.35f) return false;
            Keyboard teclado = Keyboard.current;
            if (teclado != null && (teclado.eKey.wasPressedThisFrame || teclado.enterKey.wasPressedThisFrame
                                    || teclado.spaceKey.wasPressedThisFrame)) return true;
            Mouse raton = Mouse.current;
            if (raton != null && raton.leftButton.wasPressedThisFrame) return true;
            Gamepad mando = Gamepad.current;
            return mando != null && (mando.buttonSouth.wasPressedThisFrame || mando.buttonWest.wasPressedThisFrame);
        }

        /// <summary>Número de charla para que cada conversación con un colono cuente algo distinto.</summary>
        public int SiguienteCharla()
        {
            return charlas++;
        }

        // ---- Dibujo ----

        void OnGUI()
        {
            GUI.depth = -10;
            Tema.Empezar();
            float ancho = Tema.Ancho;
            float alto = Tema.Alto;

            if (fase == Fase.Titulo)
            {
                Ejecutar(portada.Dibujar());
                return;
            }

            if (fase == Fase.Intro)
            {
                Tema.Relleno(new Rect(0, 0, ancho, alto), Color.black);
                int i = Mathf.Clamp(tarjeta, 0, Textos.Intro.Length - 1);
                float aparece = Mathf.Clamp01((Time.unscaledTime - desde) / 0.8f);
                Color anterior = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, aparece);
                float w = Mathf.Min(960f, ancho - 48f);
                Tema.Escribir(new Rect((ancho - w) * 0.5f, alto * 0.5f - 170f, w, 320f), Textos.Intro[i], Tema.TextoGrande);
                GUI.color = anterior;
                Tema.Meandro(new Rect(ancho * 0.5f - 90f, alto - 98f, 180f, 12f), new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.4f));
                Tema.Indicaciones(ancho * 0.5f, alto - 56f, Tema.Pequeno, (Boton.Accion, "seguir"));
                Tema.Escribir(new Rect(0f, alto - 36f, ancho - 24f, 20f), (i + 1) + " / " + Textos.Intro.Length,
                    new GUIStyle(Tema.Pequeno) { alignment = TextAnchor.MiddleRight });
                return;
            }

            if (velo > 0f) Tema.Relleno(new Rect(0, 0, ancho, alto), new Color(0f, 0f, 0f, velo));

            if (escena != null && linea < escena.Length) DibujarDialogo(ancho, alto);
        }

        void DibujarDialogo(float ancho, float alto)
        {
            Linea l = escena[linea];
            float w = Mathf.Min(940f, ancho - 32f);
            float altoTexto = Mathf.Max(56f, Tema.AltoDe(l.Texto, Tema.TextoGrande, w - 64f));
            float altoPanel = altoTexto + 96f;
            Tema.Degradado(new Rect(0f, alto - altoPanel - 120f, ancho, altoPanel + 120f), new Color(0f, 0f, 0f, 0.55f), true);
            var panel = new Rect((ancho - w) * 0.5f, alto - altoPanel - 28f, w, altoPanel);
            Tema.Panel(panel, true);

            // Placa con el nombre de quien habla, montada sobre el filo del panel.
            float wn = Tema.AnchoDe(l.Quien.ToUpperInvariant(), Tema.Seccion) + 36f;
            var placa = new Rect(panel.x + 28f, panel.y - 15f, wn, 30f);
            Tema.Relleno(placa, Tema.FondoClaro, 8f);
            Tema.Filo(placa, Tema.Borde, 1.5f, 8f);
            Tema.Escribir(placa, l.Quien.ToUpperInvariant(), new GUIStyle(Tema.Seccion) { alignment = TextAnchor.MiddleCenter });

            float aparece = Mathf.Clamp01((Time.unscaledTime - desde) / 0.25f);
            Color previo = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, aparece);
            var estilo = new GUIStyle(Tema.TextoGrande) { alignment = TextAnchor.UpperLeft };
            Tema.Escribir(new Rect(panel.x + 32f, panel.y + 30f, w - 64f, altoTexto), l.Texto, estilo);
            GUI.color = previo;

            Tema.Escribir(new Rect(panel.x + 32f, panel.yMax - 36f, 120f, 22f), (linea + 1) + " / " + escena.Length, Tema.Pequeno);
            float wi = Tema.AnchoIndicacion(Boton.Accion, "seguir", Tema.Pequeno);
            Tema.Indicacion(panel.xMax - 28f - wi, panel.yMax - 25f, Boton.Accion, "seguir", Tema.Pequeno);
        }
    }
}
