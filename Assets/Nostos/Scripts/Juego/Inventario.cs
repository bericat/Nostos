using Nostos.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>
    /// El inventario de Nicias, en su propia pantalla (I en el teclado, ▲ en el mando, a pie): la
    /// mochila en casillas con lo que lleva y cuánto cabe, y debajo sus herramientas con lo que les
    /// queda. Al pasar el ratón o moverse con las flechas por una casilla se lee qué es.
    /// </summary>
    public class Inventario : MonoBehaviour
    {
        public ControlModo control;

        const int Columnas = 5;
        const float Casilla = 92f;
        const float Hueco = 10f;

        static Inventario instancia;
        bool abierto;
        int abiertoEn;
        int foco;

        public static bool Abierto => instancia != null && instancia.abierto;

        void Awake()
        {
            instancia = this;
        }

        void OnDestroy()
        {
            if (instancia == this) instancia = null;
        }

        public void Alternar()
        {
            if (abierto) Cerrar();
            else Abrir();
        }

        public void Abrir()
        {
            if (abierto || control == null || control.Bloqueado || control.Modo != ModoJuego.APie) return;
            if (Faenas.Abierta || MenuConstruccion.Abierto) return;
            abierto = true;
            abiertoEn = Time.frameCount;
            foco = 0;
            control.Bloquear(true);
            Sonido.Tocar(Efecto.Paso, 0.5f);
        }

        void Cerrar()
        {
            if (!abierto) return;
            abierto = false;
            if (control != null) control.Bloquear(false);
        }

        void Update()
        {
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            bool pulsa = (teclado != null && teclado.iKey.wasPressedThisFrame)
                         || (mando != null && control != null && control.Modo == ModoJuego.APie && mando.dpad.up.wasPressedThisFrame);
            if (!abierto)
            {
                if (pulsa) Abrir();
                return;
            }
            if (Time.frameCount == abiertoEn) return;
            bool sale = pulsa || (teclado != null && (teclado.escapeKey.wasPressedThisFrame || teclado.tabKey.wasPressedThisFrame))
                        || (mando != null && (mando.buttonEast.wasPressedThisFrame || mando.startButton.wasPressedThisFrame));
            if (sale)
            {
                Cerrar();
                return;
            }
            // Moverse por las casillas con las flechas o la cruceta.
            int total = Recursos.Numero + Equipo.Numero;
            if (Pulsada(teclado, mando, 0)) foco = Mathf.Min(total - 1, foco + 1);
            if (Pulsada(teclado, mando, 1)) foco = Mathf.Max(0, foco - 1);
            if (Pulsada(teclado, mando, 2)) foco = Mathf.Min(total - 1, foco + Columnas);
            if (Pulsada(teclado, mando, 3)) foco = Mathf.Max(0, foco - Columnas);
        }

        /// <summary>0 derecha, 1 izquierda, 2 abajo, 3 arriba (flechas, WASD o cruceta).</summary>
        static bool Pulsada(Keyboard t, Gamepad m, int d)
        {
            switch (d)
            {
                case 0: return (t != null && (t.rightArrowKey.wasPressedThisFrame || t.dKey.wasPressedThisFrame)) || (m != null && m.dpad.right.wasPressedThisFrame);
                case 1: return (t != null && (t.leftArrowKey.wasPressedThisFrame || t.aKey.wasPressedThisFrame)) || (m != null && m.dpad.left.wasPressedThisFrame);
                case 2: return (t != null && (t.downArrowKey.wasPressedThisFrame || t.sKey.wasPressedThisFrame)) || (m != null && m.dpad.down.wasPressedThisFrame);
                default: return (t != null && (t.upArrowKey.wasPressedThisFrame || t.wKey.wasPressedThisFrame));
            }
        }

        void OnGUI()
        {
            if (!abierto) return;
            Juego juego = Juego.Instancia;
            if (juego == null || juego.Mundo == null) return;
            GUI.depth = -6;
            Tema.Empezar();
            Mundo mundo = juego.Mundo;
            Bolsa mochila = mundo.Mochila;

            Tema.Relleno(new Rect(0f, 0f, Tema.Ancho, Tema.Alto), new Color(0f, 0f, 0f, 0.45f));
            float ancho = Columnas * Casilla + (Columnas - 1) * Hueco + 56f;
            const float alto = 620f;
            var panel = new Rect((Tema.Ancho - ancho) * 0.5f, (Tema.Alto - alto) * 0.5f, ancho, alto);
            Tema.Panel(panel, true);
            var dentro = new Rect(panel.x + 28f, panel.y + 20f, panel.width - 56f, panel.height - 40f);

            // Cabecera: la mochila y cuánto lleva.
            Tema.Icono(new Rect(dentro.x, dentro.y + 2f, 30f, 30f), Tema.Simbolo.Mochila);
            Tema.Escribir(new Rect(dentro.x + 40f, dentro.y, 220f, 34f), "Mochila", Tema.Titulo);
            bool llena = mochila.Capacidad > 0 && mochila.Total >= mochila.Capacidad;
            string carga = mochila.Total + " / " + mochila.Capacidad;
            float wc = Tema.AnchoDe(carga, Tema.Cifra);
            Tema.Escribir(new Rect(dentro.xMax - wc, dentro.y + 4f, wc + 2f, 26f), carga, Tema.Cifra, llena ? Tema.Rojo : Tema.Pergamino);
            Tema.Barra(new Rect(dentro.x, dentro.y + 42f, dentro.width, 6f),
                mochila.Capacidad > 0 ? mochila.Total / (float)mochila.Capacidad : 0f, llena ? Tema.Rojo : Tema.Ocre);

            // Casillas: primero lo que lleva, luego huecos vacíos.
            var orden = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Recursos.Numero; i++) if (mochila[(Recurso)i] > 0) orden.Add(i);
            float y0 = dentro.y + 62f;
            string titulo = null, detalle = null;
            for (int k = 0; k < Recursos.Numero; k++)
            {
                var r = new Rect(dentro.x + (k % Columnas) * (Casilla + Hueco), y0 + (k / Columnas) * (Casilla + Hueco), Casilla, Casilla);
                if (Tema.RatonPasa(r)) foco = k;
                bool enfocada = foco == k;
                Tema.Relleno(r, Tema.FondoHueco, 10f);
                Tema.Filo(r, enfocada ? Tema.Ocre : new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.3f), enfocada ? 2f : 1f, 10f);
                if (k >= orden.Count) continue;
                var recurso = (Recurso)orden[k];
                Tema.Icono(new Rect(r.x + 20f, r.y + 12f, 52f, 52f), recurso);
                string n = mochila[recurso].ToString();
                float wn = Tema.AnchoDe(n, Tema.Cifra);
                Tema.Escribir(new Rect(r.xMax - wn - 8f, r.yMax - 28f, wn + 2f, 24f), n, Tema.Cifra);
                if (enfocada)
                {
                    titulo = Mayuscula(Recursos.Nombre(recurso));
                    detalle = mochila[recurso] + (mochila[recurso] == 1 ? " unidad." : " unidades.") + " Se entrega en las obras o se guarda en el almacén.";
                }
            }

            // Herramientas de Nicias.
            float y1 = y0 + 3 * (Casilla + Hueco) + 12f;
            Tema.Escribir(new Rect(dentro.x, y1, dentro.width, 22f), "HERRAMIENTAS", Tema.Seccion);
            Equipo equipo = mundo.Equipo;
            float anchoHerramienta = (dentro.width - (Equipo.Numero - 1) * Hueco) / Equipo.Numero;
            for (int i = 0; i < Equipo.Numero; i++)
            {
                var h = (Herramienta)i;
                int k = Recursos.Numero + i;
                var r = new Rect(dentro.x + i * (anchoHerramienta + Hueco), y1 + 30f, anchoHerramienta, Casilla + 8f);
                if (Tema.RatonPasa(r)) foco = k;
                bool enfocada = foco == k;
                bool tiene = equipo.Tiene(h);
                Tema.Relleno(r, Tema.FondoHueco, 10f);
                Tema.Filo(r, enfocada ? Tema.Ocre : new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.3f), enfocada ? 2f : 1f, 10f);
                string imagen = "Herramientas/" + Archivo(h);
                var dibujo = new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 34f);
                if (Tema.Imagen(imagen) != null) Tema.Dibujar(dibujo, imagen, tiene ? Color.white : new Color(1f, 1f, 1f, 0.18f));
                else Tema.Icono(new Rect(r.center.x - 24f, r.y + 10f, 48f, 48f), Recurso.Herramientas);
                Tema.Escribir(new Rect(r.x, r.yMax - 28f, r.width, 20f), Mayuscula(Equipo.Nombre(h)), Tema.PequenoCentro,
                    tiene ? Tema.Pergamino : Tema.Apagado);
                if (tiene)
                {
                    float queda = equipo.Golpes(h) / (float)Equipo.Aguante(h);
                    Tema.Barra(new Rect(r.x + 10f, r.yMax - 8f, r.width - 20f, 4f), queda, queda < 0.2f ? Tema.Rojo : Tema.Ocre);
                }
                if (enfocada)
                {
                    titulo = Mayuscula(Equipo.Nombre(h));
                    detalle = tiene
                        ? "Le quedan " + equipo.Golpes(h) + " de " + Equipo.Aguante(h) + " golpes. " + ParaQue(h)
                        : "No la tienes. Se forja en " + (h == Herramienta.PicoBueno ? "la herrería" : "el banco de trabajo")
                          + " con " + Recursos.Lista(Equipo.Coste(h)) + ". " + ParaQue(h);
                }
            }

            // Qué es lo enfocado.
            float y2 = y1 + 30f + Casilla + 22f;
            Tema.Meandro(new Rect(dentro.x, y2, dentro.width, 10f), new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.3f));
            if (titulo != null)
            {
                Tema.Escribir(new Rect(dentro.x, y2 + 16f, dentro.width, 26f), titulo, Tema.Seccion, Tema.Ocre);
                Tema.Escribir(new Rect(dentro.x, y2 + 42f, dentro.width, 40f), detalle, Tema.Texto);
            }
            else
            {
                Tema.Escribir(new Rect(dentro.x, y2 + 16f, dentro.width, 26f), "Hueco libre", Tema.Seccion, Tema.Apagado);
            }

            Tema.Indicaciones(panel.center.x, panel.yMax - 22f, Tema.Pequeno, (Boton.Elegir, "mirar"), (Boton.Inventario, "cerrar"));
        }

        static string ParaQue(Herramienta h)
        {
            switch (h)
            {
                case Herramienta.Hacha: return "Con ella talas el doble de madera.";
                case Herramienta.Pico: return "Con él picas el doble de mineral.";
                case Herramienta.Pala: return "Con ella sacas el doble de cobre.";
                default: return "Triple de mineral y doble de cobre.";
            }
        }

        /// <summary>Nombre del archivo de su dibujo en Resources/UI/Herramientas.</summary>
        public static string Archivo(Herramienta h)
        {
            switch (h)
            {
                case Herramienta.Hacha: return "hacha";
                case Herramienta.Pico: return "pico";
                case Herramienta.Pala: return "pala";
                default: return "pico_bueno";
            }
        }

        static string Mayuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
