using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Nostos
{
    /// <summary>
    /// Menú de pausa (Esc o el botón Menu del mando): seguir, guardar, cargar, ajustes, volver a
    /// la portada y salir. Para la simulación mientras está abierto.
    /// </summary>
    public class MenuPausa : MonoBehaviour
    {
        public ControlModo control;
        public Juego juego;

        public static bool Abierto { get; private set; }

        static readonly string[] Opciones = { "Seguir", "Guardar partida", "Cargar partida", "Ajustes", "Modo dios", "Volver a la portada", "Salir del juego" };
        static readonly string[] Iconos = { "continuar", "guardar", "cargar", "ajustes", "objetivo", "nueva", "salir" };

        int foco;
        int direccionAnterior;
        bool enAjustes;
        bool pausaAntes;
        bool bloqueadoAntes = true;

        void OnDestroy()
        {
            Abierto = false;
        }

        void Update()
        {
            if (control == null || juego == null) return;
            bool bloqueadoAhora = control.Bloqueado;
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;

            if (!Abierto)
            {
                bool pide = (teclado != null && teclado.escapeKey.wasPressedThisFrame)
                            || (mando != null && mando.startButton.wasPressedThisFrame);
                // Si en este frame o en el anterior había un panel abierto, el Esc era para cerrarlo.
                bool libre = !bloqueadoAhora && !bloqueadoAntes && !juego.Durmiendo && !juego.Despegado
                             && (Narrativa.Instancia == null || (!Narrativa.Instancia.EnTitulo && !Narrativa.Instancia.EnEscena));
                if (pide && libre) Abrir();
                bloqueadoAntes = bloqueadoAhora;
                return;
            }

            if (enAjustes)
            {
                if (Ajustes.Actualizar()) enAjustes = false;
                return;
            }
            int dir = Tema.DireccionActual();
            bool nueva = dir != direccionAnterior;
            direccionAnterior = dir;
            if (nueva && dir == Tema.Arriba) foco = (foco + Opciones.Length - 1) % Opciones.Length;
            if (nueva && dir == Tema.Abajo) foco = (foco + 1) % Opciones.Length;
            bool cerrar = (teclado != null && (teclado.escapeKey.wasPressedThisFrame || teclado.tabKey.wasPressedThisFrame))
                          || (mando != null && (mando.buttonEast.wasPressedThisFrame || mando.startButton.wasPressedThisFrame));
            if (cerrar && Time.frameCount > abiertoEn)
            {
                Cerrar();
                return;
            }
            if (Tema.PulsaAceptar()) Elegir(foco);
        }

        int abiertoEn;

        public void Abrir()
        {
            if (Abierto) return;
            Abierto = true;
            abiertoEn = Time.frameCount;
            foco = 0;
            enAjustes = false;
            direccionAnterior = Tema.DireccionActual();
            pausaAntes = juego.Pausa;
            juego.Pausa = true;
            control.Bloquear(true);
            Sonido.Tocar(Efecto.Pagina, 0.6f);
        }

        void Cerrar()
        {
            Abierto = false;
            juego.Pausa = pausaAntes;
            control.Bloquear(false);
            bloqueadoAntes = true;
        }

        void Elegir(int i)
        {
            switch (i)
            {
                case 0:
                    Cerrar();
                    break;
                case 1:
                    juego.GuardarPartida();
                    Cerrar();
                    juego.Avisar("Partida guardada.");
                    break;
                case 2:
                    Cerrar();
                    juego.Avisar(Guardado.Cargar(juego, juego.jugador) ? "Partida cargada." : "No hay ninguna partida guardada.");
                    break;
                case 3:
                    enAjustes = true;
                    Ajustes.Abrir();
                    break;
                case 4:
                    juego.CambiarModoDios();
                    break;
                case 5:
                    Abierto = false;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    break;
                case 6:
                    Narrativa.Salir();
                    break;
            }
        }

        void OnGUI()
        {
            if (!Abierto) return;
            GUI.depth = -8;
            Tema.Empezar();
            float ancho = Tema.Ancho;
            float alto = Tema.Alto;
            Tema.Relleno(new Rect(0f, 0f, ancho, alto), new Color(0.04f, 0.03f, 0.05f, 0.55f));
            Tema.DegradadoLateral(new Rect(0f, 0f, ancho * 0.5f, alto), new Color(0f, 0f, 0f, 0.45f), true);

            var panel = new Rect(Mathf.Max(32f, ancho * 0.08f), alto * 0.5f - 255f, 460f, 510f);
            Tema.Panel(panel, true);
            var dentro = new Rect(panel.x + 28f, panel.y + 22f, panel.width - 56f, panel.height - 44f);
            if (enAjustes)
            {
                if (Ajustes.Dibujar(dentro)) enAjustes = false;
                return;
            }

            Tema.Escribir(new Rect(dentro.x, dentro.y, dentro.width, 34f), "Pausa", Tema.Titulo);
            string dia = "Día " + juego.Mundo.Ciclo.Dia + " en Nova Lavinia";
            float wd = Tema.AnchoDe(dia, Tema.Pequeno);
            Tema.Escribir(new Rect(dentro.xMax - wd, dentro.y + 8f, wd + 2f, 20f), dia, Tema.Pequeno);
            Tema.Meandro(new Rect(dentro.x, dentro.y + 40f, dentro.width, 12f), new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.55f));

            var estilo = new GUIStyle(Tema.BotonTexto) { fontSize = 19, wordWrap = false };
            for (int i = 0; i < Opciones.Length; i++)
            {
                var fila = new Rect(dentro.x - 8f, dentro.y + 66f + i * 50f, dentro.width + 16f, 44f);
                if (Tema.RatonPasa(fila)) foco = i;
                bool elegida = foco == i;
                if (elegida)
                {
                    Tema.Relleno(fila, new Color(0.97f, 0.94f, 0.87f, 0.95f), 6f);
                    Tema.Relleno(new Rect(fila.x, fila.y, 4f, fila.height), Tema.Ocre, 2f);
                }
                Color color = elegida ? Tema.Tinta : Tema.Pergamino;
                Tema.Linea(new Rect(fila.x + 16f, fila.y + 11f, 22f, 22f), Iconos[i], color);
                string texto = Opciones[i];
                if (i == 4) texto += juego.ModoDios ? ": sí" : ": no";
                Tema.Escribir(new Rect(fila.x + 54f, fila.y, fila.width - 60f, fila.height), texto, estilo, color);
                if (GUI.Button(fila, GUIContent.none, GUIStyle.none))
                {
                    foco = i;
                    Elegir(i);
                    return;
                }
            }
            Tema.Indicaciones(panel.center.x, panel.yMax - 22f, Tema.Pequeno, (Boton.Elegir, "elegir"), (Boton.Aceptar, "aceptar"),
                (Boton.Pausa, "seguir"));
        }
    }
}
