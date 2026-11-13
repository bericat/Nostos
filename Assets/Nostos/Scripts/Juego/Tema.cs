using System.Collections.Generic;
using Nostos.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>Las acciones que se enseñan en pantalla con su tecla o su botón.</summary>
    public enum Boton
    {
        Usar,
        Faena,
        Accion,
        Saltar,
        Salir,
        Modo,
        Mover,
        Mirar,
        Ayuda,
        Rotar,
        Zoom,
        Cambiar,
        Colocar,
        Cancelar,
        Tejados,
        Guardar,
        Cargar,
        Elegir,
        Ajustar,
        Obras,
        Pestana,
        Pausa,
        Aceptar,
        Inventario
    }

    /// <summary>
    /// El aspecto común de toda la interfaz: paleta de Nova Lavinia (nogal, ocre, cobre y
    /// pergamino, con el azul de los detalles de las casas), letras romanas de inscripción para
    /// los títulos (Cinzel) y una humanista para el texto (Alegreya Sans), iconos de recursos y
    /// los dibujos de teclas y botones del mando. Todo se dibuja con OnGUI sobre una pantalla
    /// virtual de 800 píxeles de alto (la de la Steam Deck), escalada al tamaño real.
    /// </summary>
    public static class Tema
    {
        // ---------------------------------------------------------------- paleta

        public static readonly Color Fondo = new Color32(33, 25, 19, 226);
        public static readonly Color FondoClaro = new Color32(60, 46, 35, 236);
        public static readonly Color FondoHueco = new Color32(20, 15, 11, 200);
        public static readonly Color Borde = new Color32(201, 160, 99, 255);
        public static readonly Color Pergamino = new Color32(243, 233, 212, 255);
        public static readonly Color Apagado = new Color32(190, 172, 145, 255);
        public static readonly Color Ocre = new Color32(226, 168, 76, 255);
        public static readonly Color Cobre = new Color32(200, 110, 56, 255);
        public static readonly Color Azul = new Color32(96, 146, 196, 255);
        public static readonly Color Verde = new Color32(132, 182, 104, 255);
        public static readonly Color Rojo = new Color32(214, 92, 66, 255);
        public static readonly Color Tinta = new Color32(28, 20, 15, 255);

        public const float AltoReferencia = 800f;

        /// <summary>Píxeles reales por píxel virtual.</summary>
        public static float Escala { get; private set; } = 1f;
        /// <summary>Tamaño de la pantalla en píxeles virtuales.</summary>
        public static float Ancho => Screen.width / Escala;
        public static float Alto => Screen.height / Escala;

        /// <summary>True si lo último que se ha tocado es el mando: se enseñan sus botones en vez de las teclas.</summary>
        public static bool UsaMando { get; private set; }

        static bool cargado;
        static int ultimoFrameDispositivo = -1;
        static Font fuenteTitulo;
        static Font fuenteTituloFuerte;
        static Font fuenteTexto;
        static Font fuenteMedia;
        static Font fuenteNegrita;
        static Texture2D[] iconos;
        static Texture2D[] iconosInterfaz;
        static Texture2D degradado;
        static readonly string[] NombresInterfaz = { "confianza", "nave", "tributo", "sol", "luna", "mochila" };

        public static GUIStyle Titulo;
        public static GUIStyle TituloGrande;
        public static GUIStyle TituloPortada;
        public static GUIStyle Seccion;
        public static GUIStyle Texto;
        public static GUIStyle TextoCentro;
        public static GUIStyle TextoGrande;
        public static GUIStyle Pequeno;
        public static GUIStyle PequenoCentro;
        public static GUIStyle Cifra;
        public static GUIStyle Tecla;
        public static GUIStyle BotonTexto;

        // ---------------------------------------------------------------- preparación

        /// <summary>Llamar al principio de cada OnGUI: carga lo necesario y fija la escala.</summary>
        static Vector2 ratonVisto = new Vector2(-1000f, -1000f);
        static int frameRaton = -10;

        /// <summary>
        /// ¿El ratón acaba de moverse y está sobre `r`? Para resaltar opciones solo con pasar el
        /// ratón por encima: Unity casi no manda MouseMove en juego, así que se compara la posición
        /// en cada evento. Si el ratón está quieto devuelve false y el teclado o el mando mandan.
        /// </summary>
        public static bool RatonPasa(Rect r)
        {
            Event e = Event.current;
            if (e == null || e.type == EventType.Layout) return false;
            Vector2 p = e.mousePosition;
            if ((p - ratonVisto).sqrMagnitude > 0.25f)
            {
                ratonVisto = p;
                frameRaton = Time.frameCount;
            }
            return Time.frameCount - frameRaton <= 1 && r.Contains(p);
        }

        public static void Empezar()
        {
            if (!cargado) Cargar();
            Escala = Mathf.Clamp(Screen.height / AltoReferencia, 0.6f, 3f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(Escala, Escala, 1f));
            GUI.color = Color.white;
        }

        static void Cargar()
        {
            cargado = true;
            fuenteTitulo = Resources.Load<Font>("UI/Fuentes/Cinzel-SemiBold");
            fuenteTituloFuerte = Resources.Load<Font>("UI/Fuentes/Cinzel-ExtraBold");
            fuenteTexto = Resources.Load<Font>("UI/Fuentes/AlegreyaSans-Regular");
            fuenteMedia = Resources.Load<Font>("UI/Fuentes/AlegreyaSans-Medium");
            fuenteNegrita = Resources.Load<Font>("UI/Fuentes/AlegreyaSans-Bold");
            if (fuenteTituloFuerte == null) fuenteTituloFuerte = fuenteTitulo;
            if (fuenteMedia == null) fuenteMedia = fuenteTexto;
            if (fuenteNegrita == null) fuenteNegrita = fuenteMedia;

            iconos = new Texture2D[Recursos.Numero];
            for (int i = 0; i < Recursos.Numero; i++)
            {
                iconos[i] = Resources.Load<Texture2D>("UI/Iconos/" + NombreArchivo((Recurso)i));
            }
            iconosInterfaz = new Texture2D[NombresInterfaz.Length];
            for (int i = 0; i < NombresInterfaz.Length; i++)
            {
                iconosInterfaz[i] = Resources.Load<Texture2D>("UI/Iconos/" + NombresInterfaz[i]);
            }

            degradado = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;
                degradado.SetPixel(0, y, new Color(1f, 1f, 1f, t * t));
            }
            degradado.Apply();

            Texto = Estilo(fuenteTexto, 17, Pergamino, TextAnchor.UpperLeft);
            TextoCentro = Estilo(fuenteTexto, 17, Pergamino, TextAnchor.MiddleCenter);
            TextoGrande = Estilo(fuenteTexto, 21, Pergamino, TextAnchor.MiddleCenter);
            Pequeno = Estilo(fuenteTexto, 14, Apagado, TextAnchor.UpperLeft);
            PequenoCentro = Estilo(fuenteTexto, 14, Apagado, TextAnchor.MiddleCenter);
            Cifra = Estilo(fuenteNegrita, 16, Pergamino, TextAnchor.MiddleLeft);
            BotonTexto = Estilo(fuenteMedia, 17, Pergamino, TextAnchor.MiddleLeft);
            Tecla = Estilo(fuenteNegrita, 14, Tinta, TextAnchor.MiddleCenter);
            Tecla.wordWrap = false;
            Titulo = Estilo(fuenteTitulo, 22, Pergamino, TextAnchor.UpperLeft);
            Titulo.wordWrap = false;
            TituloGrande = Estilo(fuenteTitulo, 30, Pergamino, TextAnchor.MiddleCenter);
            TituloPortada = Estilo(fuenteTituloFuerte, 108, Pergamino, TextAnchor.MiddleCenter);
            TituloPortada.wordWrap = false;
            Seccion = Estilo(fuenteTitulo, 13, Ocre, TextAnchor.MiddleLeft);
            Seccion.wordWrap = false;
            if (fuenteTitulo == null)
            {
                // Sin las fuentes importadas, al menos que los títulos se distingan.
                Titulo.fontStyle = FontStyle.Bold;
                TituloGrande.fontStyle = FontStyle.Bold;
                TituloPortada.fontStyle = FontStyle.Bold;
                Seccion.fontStyle = FontStyle.Bold;
            }
        }

        static GUIStyle Estilo(Font fuente, int tam, Color color, TextAnchor ancla)
        {
            var e = new GUIStyle
            {
                font = fuente,
                fontSize = tam,
                alignment = ancla,
                richText = true,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0)
            };
            e.normal.textColor = color;
            return e;
        }

        static string NombreArchivo(Recurso r)
        {
            return r == Recurso.Nucleos ? "nucleos" : r.ToString().ToLowerInvariant();
        }

        // ---------------------------------------------------------------- dispositivo

        /// <summary>Mira qué se ha tocado este frame. Lo llama el HUD en su Update.</summary>
        public static void ActualizarDispositivo()
        {
            if (ultimoFrameDispositivo == Time.frameCount) return;
            if (ultimoFrameDispositivo < 0) UsaMando = Gamepad.current != null;
            ultimoFrameDispositivo = Time.frameCount;

            Gamepad mando = Gamepad.current;
            if (mando != null)
            {
                bool tocado = mando.buttonSouth.wasPressedThisFrame || mando.buttonEast.wasPressedThisFrame
                              || mando.buttonWest.wasPressedThisFrame || mando.buttonNorth.wasPressedThisFrame
                              || mando.startButton.wasPressedThisFrame || mando.leftShoulder.wasPressedThisFrame
                              || mando.rightShoulder.wasPressedThisFrame
                              || mando.leftStick.ReadValue().sqrMagnitude > 0.3f
                              || mando.rightStick.ReadValue().sqrMagnitude > 0.3f
                              || mando.dpad.ReadValue().sqrMagnitude > 0.3f;
                if (tocado) UsaMando = true;
            }
            Keyboard teclado = Keyboard.current;
            if (teclado != null && teclado.anyKey.wasPressedThisFrame) UsaMando = false;
            Mouse raton = Mouse.current;
            if (raton != null && (raton.leftButton.wasPressedThisFrame || raton.delta.ReadValue().sqrMagnitude > 16f))
            {
                UsaMando = false;
            }
        }

        public const int Arriba = 1, Derecha = 2, Abajo = 3, Izquierda = 4;

        /// <summary>Dirección que se mantiene pulsada (flechas, WASD, cruceta o stick izquierdo), o 0.</summary>
        public static int DireccionActual()
        {
            Vector2 v = Vector2.zero;
            Keyboard teclado = Keyboard.current;
            if (teclado != null)
            {
                if (teclado.wKey.isPressed || teclado.upArrowKey.isPressed) v.y += 1f;
                if (teclado.sKey.isPressed || teclado.downArrowKey.isPressed) v.y -= 1f;
                if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) v.x += 1f;
                if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) v.x -= 1f;
            }
            Gamepad mando = Gamepad.current;
            if (mando != null)
            {
                v += mando.dpad.ReadValue();
                v += mando.leftStick.ReadValue();
            }
            if (v.magnitude < 0.5f) return 0;
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x > 0f ? Derecha : Izquierda;
            return v.y > 0f ? Arriba : Abajo;
        }

        /// <summary>Aceptar en un panel: Intro, espacio o E en el teclado; A en el mando.</summary>
        public static bool PulsaAceptar()
        {
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            return (teclado != null && (teclado.enterKey.wasPressedThisFrame || teclado.spaceKey.wasPressedThisFrame
                                        || teclado.eKey.wasPressedThisFrame))
                   || (mando != null && mando.buttonSouth.wasPressedThisFrame);
        }

        /// <summary>Volver o cerrar un panel: Esc o Tab en el teclado; B en el mando.</summary>
        public static bool PulsaVolver()
        {
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            return (teclado != null && (teclado.escapeKey.wasPressedThisFrame || teclado.tabKey.wasPressedThisFrame))
                   || (mando != null && mando.buttonEast.wasPressedThisFrame);
        }

        /// <summary>Lo que pone en la tecla o el botón de una acción, según el dispositivo.</summary>
        public static string Etiqueta(Boton boton)
        {
            if (UsaMando)
            {
                switch (boton)
                {
                    case Boton.Usar: return "X";
                    case Boton.Faena: return "B";
                    case Boton.Accion: return "A";
                    case Boton.Saltar: return "A";
                    case Boton.Salir: return "B";
                    case Boton.Modo: return "Y";
                    case Boton.Mover: return "L";
                    case Boton.Mirar: return "R";
                    case Boton.Ayuda: return "View";
                    case Boton.Rotar: return "LB RB";
                    case Boton.Zoom: return "LT RT";
                    case Boton.Cambiar: return "◀ ▶";
                    case Boton.Colocar: return "A";
                    case Boton.Cancelar: return "B";
                    case Boton.Tejados: return "▲";
                    case Boton.Guardar: return "F5";
                    case Boton.Cargar: return "F9";
                    case Boton.Elegir: return "▲ ▼";
                    case Boton.Ajustar: return "◀ ▶";
                    case Boton.Obras: return "▼";
                    case Boton.Pestana: return "LB RB";
                    case Boton.Pausa: return "Menu";
                    case Boton.Aceptar: return "A";
                    case Boton.Inventario: return "▲";
                }
            }
            switch (boton)
            {
                case Boton.Usar: return "E";
                case Boton.Faena: return "F";
                case Boton.Accion: return "E";
                case Boton.Saltar: return "Espacio";
                case Boton.Salir: return "Esc";
                case Boton.Modo: return "Tab";
                case Boton.Mover: return "WASD";
                case Boton.Mirar: return "Ratón";
                case Boton.Ayuda: return "H";
                case Boton.Rotar: return "Q E";
                case Boton.Zoom: return "Rueda";
                case Boton.Cambiar: return "Z C";
                case Boton.Colocar: return "Clic";
                case Boton.Cancelar: return "Clic der.";
                case Boton.Tejados: return "T";
                case Boton.Guardar: return "F5";
                case Boton.Cargar: return "F9";
                case Boton.Elegir: return "↑ ↓";
                case Boton.Ajustar: return "← →";
                case Boton.Obras: return "B";
                case Boton.Pestana: return "Q E";
                case Boton.Pausa: return "Esc";
                case Boton.Aceptar: return "Intro";
                case Boton.Inventario: return "I";
            }
            return "?";
        }

        /// <summary>Los botones de la cara del mando se dibujan redondos, como en el mando.</summary>
        static bool EsRedondo(string etiqueta)
        {
            return UsaMando && etiqueta.Length == 1 && "ABXYLR".IndexOf(etiqueta[0]) >= 0;
        }

        // ---------------------------------------------------------------- dibujo básico

        static Color ConAlfa(Color c)
        {
            c.a *= GUI.color.a;
            return c;
        }

        /// <summary>Rectángulo relleno con esquinas redondeadas.</summary>
        public static void Relleno(Rect r, Color color, float radio = 0f)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            radio = Mathf.Min(radio, r.width * 0.5f, r.height * 0.5f);
            Color previo = GUI.color;
            Color c = ConAlfa(color);
            GUI.color = Color.white;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radio);
            GUI.color = previo;
        }

        /// <summary>Solo el filo de un rectángulo redondeado.</summary>
        public static void Filo(Rect r, Color color, float grosor, float radio = 0f)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            radio = Mathf.Min(radio, r.width * 0.5f, r.height * 0.5f);
            Color previo = GUI.color;
            Color c = ConAlfa(color);
            GUI.color = Color.white;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, grosor, radio);
            GUI.color = previo;
        }

        /// <summary>El panel de la casa: nogal oscuro con doble filo de ocre, como un marco de madera.</summary>
        public static void Panel(Rect r, bool destacado = false)
        {
            Relleno(new Rect(r.x + 2f, r.y + 4f, r.width, r.height), new Color(0f, 0f, 0f, 0.28f), 12f);
            Relleno(r, Fondo, 10f);
            Filo(r, new Color(Borde.r, Borde.g, Borde.b, destacado ? 0.95f : 0.7f), 1.5f, 10f);
            Filo(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f), new Color(Borde.r, Borde.g, Borde.b, 0.22f), 1f, 7f);
        }

        /// <summary>Ficha pequeña y sin filo, para chapas y avisos breves.</summary>
        public static void Ficha(Rect r, float opacidad = 1f)
        {
            Relleno(r, new Color(Fondo.r, Fondo.g, Fondo.b, Fondo.a * opacidad), Mathf.Min(10f, r.height * 0.5f));
        }

        /// <summary>Texto con una sombra suave para que se lea sobre el cielo y la hierba.</summary>
        public static void Escribir(Rect r, string texto, GUIStyle estilo, bool sombra = false)
        {
            if (string.IsNullOrEmpty(texto)) return;
            if (sombra)
            {
                Color original = estilo.normal.textColor;
                estilo.normal.textColor = new Color(0f, 0f, 0f, 0.7f * original.a);
                GUI.Label(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), texto, estilo);
                estilo.normal.textColor = original;
            }
            GUI.Label(r, texto, estilo);
        }

        public static void Escribir(Rect r, string texto, GUIStyle estilo, Color color, bool sombra = false)
        {
            Color original = estilo.normal.textColor;
            estilo.normal.textColor = color;
            Escribir(r, texto, estilo, sombra);
            estilo.normal.textColor = original;
        }

        public static float AnchoDe(string texto, GUIStyle estilo)
        {
            if (string.IsNullOrEmpty(texto)) return 0f;
            bool envolver = estilo.wordWrap;
            estilo.wordWrap = false;
            float ancho = estilo.CalcSize(new GUIContent(texto)).x;
            estilo.wordWrap = envolver;
            return ancho;
        }

        public static float AltoDe(string texto, GUIStyle estilo, float ancho)
        {
            if (string.IsNullOrEmpty(texto)) return 0f;
            return estilo.CalcHeight(new GUIContent(texto), ancho);
        }

        public static void Icono(Rect r, Recurso recurso)
        {
            Texture2D t = iconos != null ? iconos[(int)recurso] : null;
            if (t != null) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true);
            else Relleno(r, Cobre, r.width * 0.5f);
        }

        public enum Simbolo
        {
            Confianza,
            Nave,
            Tributo,
            Sol,
            Luna,
            Mochila
        }

        public static void Icono(Rect r, Simbolo simbolo)
        {
            Texture2D t = iconosInterfaz != null ? iconosInterfaz[(int)simbolo] : null;
            if (t != null) GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true);
            else Relleno(r, Ocre, r.width * 0.5f);
        }

        /// <summary>Barra redondeada de 0 a 1, con marcas opcionales (por ejemplo los umbrales de confianza).</summary>
        public static void Barra(Rect r, float valor, Color color, params float[] marcas)
        {
            float radio = r.height * 0.5f;
            Relleno(r, FondoHueco, radio);
            float v = Mathf.Clamp01(valor);
            if (v > 0f)
            {
                float ancho = Mathf.Max(r.height, r.width * v);
                Relleno(new Rect(r.x, r.y, ancho, r.height), color, radio);
                Relleno(new Rect(r.x + radio * 0.6f, r.y + 1f, ancho - radio * 1.2f, Mathf.Max(1f, r.height * 0.3f)),
                    new Color(1f, 1f, 1f, 0.18f), radio * 0.3f);
            }
            foreach (float m in marcas)
            {
                Relleno(new Rect(r.x + r.width * m - 1f, r.y - 2f, 2f, r.height + 4f), new Color(Pergamino.r, Pergamino.g, Pergamino.b, 0.75f));
            }
        }

        /// <summary>Línea de meandro griego: el sello de Calcis que Nicias lleva consigo.</summary>
        public static void Meandro(Rect r, Color color)
        {
            float s = r.height / 6f;
            float paso = s * 6f;
            int n = Mathf.FloorToInt(r.width / paso);
            float x0 = r.x + (r.width - n * paso) * 0.5f;
            Relleno(new Rect(x0, r.y + 5f * s, n * paso, s), color);
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * paso;
                Relleno(new Rect(x, r.y, s, 5f * s), color);
                Relleno(new Rect(x, r.y, 4f * s, s), color);
                Relleno(new Rect(x + 3f * s, r.y, s, 3f * s), color);
                Relleno(new Rect(x + 2f * s, r.y + 2f * s, 2f * s, s), color);
            }
        }

        static Texture2D degradadoLateral;

        /// <summary>Oscurece de izquierda a derecha (opaco a la izquierda) o al revés.</summary>
        public static void DegradadoLateral(Rect r, Color color, bool opacoIzquierda)
        {
            if (degradadoLateral == null)
            {
                degradadoLateral = new Texture2D(64, 1, TextureFormat.RGBA32, false)
                    { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                for (int x = 0; x < 64; x++)
                {
                    float t = 1f - x / 63f;
                    degradadoLateral.SetPixel(x, 0, new Color(1f, 1f, 1f, t * t * (3f - 2f * t)));
                }
                degradadoLateral.Apply();
            }
            Color previo = GUI.color;
            GUI.color = ConAlfa(color);
            if (opacoIzquierda) GUI.DrawTexture(r, degradadoLateral, ScaleMode.StretchToFill);
            else GUI.DrawTextureWithTexCoords(r, degradadoLateral, new Rect(1f, 0f, -1f, 1f));
            GUI.color = previo;
        }

        /// <summary>Oscurece de abajo arriba (o al revés) para que el texto se lea sobre el paisaje.</summary>
        public static void Degradado(Rect r, Color color, bool haciaAbajo)
        {
            if (degradado == null) return;
            Color previo = GUI.color;
            GUI.color = ConAlfa(color);
            if (haciaAbajo) GUI.DrawTextureWithTexCoords(r, degradado, new Rect(0f, 1f, 1f, -1f));
            else GUI.DrawTexture(r, degradado, ScaleMode.StretchToFill);
            GUI.color = previo;
        }

        // ---------------------------------------------------------------- teclas y botones

        public static float AnchoTecla(Boton boton)
        {
            string e = Etiqueta(boton);
            return Mathf.Max(26f, AnchoDe(e, Tecla) + 14f);
        }

        /// <summary>Dibuja la tecla (o el botón del mando) con el centro vertical en y. Devuelve el ancho.</summary>
        public static float DibujarTecla(float x, float yCentro, Boton boton)
        {
            string e = Etiqueta(boton);
            float ancho = AnchoTecla(boton);
            var r = new Rect(x, yCentro - 13f, ancho, 26f);
            bool redondo = EsRedondo(e);
            float radio = redondo ? 13f : 6f;
            // Sombra, canto y cara: una tecla con algo de relieve.
            Relleno(new Rect(r.x, r.y + 2f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f), radio);
            Relleno(r, new Color(0.72f, 0.64f, 0.52f, 1f), radio);
            Relleno(new Rect(r.x, r.y, r.width, r.height - 3f), Pergamino, radio);
            Escribir(new Rect(r.x, r.y - 1.5f, r.width, r.height), e, Tecla);
            return ancho;
        }

        public static float AnchoIndicacion(Boton boton, string texto, GUIStyle estilo)
        {
            return AnchoTecla(boton) + 8f + AnchoDe(texto, estilo);
        }

        /// <summary>"[E] Hablar con Livia". Devuelve el ancho ocupado.</summary>
        public static float Indicacion(float x, float yCentro, Boton boton, string texto, GUIStyle estilo, bool sombra = false)
        {
            float ancho = DibujarTecla(x, yCentro, boton);
            float w = AnchoDe(texto, estilo);
            Escribir(new Rect(x + ancho + 8f, yCentro - 14f, w + 4f, 28f), texto, Alinear(estilo, TextAnchor.MiddleLeft), sombra);
            return ancho + 8f + w;
        }

        static GUIStyle alineado;

        static GUIStyle Alinear(GUIStyle estilo, TextAnchor ancla)
        {
            if (estilo.alignment == ancla && !estilo.wordWrap) return estilo;
            if (alineado == null) alineado = new GUIStyle(estilo);
            alineado.font = estilo.font;
            alineado.fontSize = estilo.fontSize;
            alineado.fontStyle = estilo.fontStyle;
            alineado.richText = estilo.richText;
            alineado.normal.textColor = estilo.normal.textColor;
            alineado.alignment = ancla;
            alineado.wordWrap = false;
            alineado.clipping = TextClipping.Overflow;
            alineado.padding = new RectOffset(0, 0, 0, 0);
            return alineado;
        }

        /// <summary>Una fila de indicaciones centrada en cx. Para el pie de los paneles.</summary>
        public static void Indicaciones(float cx, float yCentro, GUIStyle estilo, params (Boton boton, string texto)[] lista)
        {
            const float separacion = 22f;
            float total = 0f;
            for (int i = 0; i < lista.Length; i++)
            {
                total += AnchoIndicacion(lista[i].boton, lista[i].texto, estilo) + (i > 0 ? separacion : 0f);
            }
            float x = cx - total * 0.5f;
            for (int i = 0; i < lista.Length; i++)
            {
                x += Indicacion(x, yCentro, lista[i].boton, lista[i].texto, estilo) + separacion;
            }
        }

        /// <summary>
        /// Botón de los paneles. Devuelve true si se ha pulsado con el ratón; la pulsación con el
        /// mando o el teclado sobre el botón enfocado la gestiona el panel en su Update.
        /// </summary>
        public static bool BotonPanel(Rect r, string texto, bool enfocado, bool elegido = false, bool apagado = false,
            TextAnchor ancla = TextAnchor.MiddleLeft)
        {
            bool encima = r.Contains(Event.current.mousePosition) && !apagado;
            Color fondo = elegido ? Ocre : (enfocado || encima ? new Color32(88, 66, 46, 245) : FondoClaro);
            Relleno(r, fondo, 8f);
            if (enfocado)
            {
                Filo(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Ocre, 2f, 10f);
            }
            else if (!elegido)
            {
                Filo(r, new Color(Borde.r, Borde.g, Borde.b, 0.35f), 1f, 8f);
            }
            Color color = elegido ? Tinta : (apagado ? new Color(Apagado.r, Apagado.g, Apagado.b, 0.6f) : Pergamino);
            float margen = ancla == TextAnchor.MiddleCenter ? 6f : 14f;
            GUIStyle estilo = Alinear(BotonTexto, ancla);
            Escribir(new Rect(r.x + margen, r.y, r.width - margen * 2f, r.height), texto, estilo, color);
            if (apagado) return false;
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Lista de cantidades con icono: "[madera] 4  [mineral] 2". Devuelve el ancho.</summary>
        public static float Cantidades(float x, float yCentro, Cantidad[] lista, float lado = 22f, Bolsa disponible = null,
            Bolsa otra = null)
        {
            return Cantidades(x, yCentro, lista, lado, disponible, otra, Pergamino, new Color32(240, 150, 120, 255), true);
        }

        /// <summary>Lo mismo con colores propios (por ejemplo tinta sobre papel).</summary>
        public static float Cantidades(float x, float yCentro, Cantidad[] lista, float lado, Bolsa disponible, Bolsa otra,
            Color normal, Color falta, bool sombra)
        {
            float x0 = x;
            if (lista == null) return 0f;
            foreach (Cantidad c in lista)
            {
                if (c.cantidad <= 0) continue;
                Icono(new Rect(x, yCentro - lado * 0.5f, lado, lado), c.recurso);
                x += lado + 2f;
                string n = c.cantidad.ToString();
                Color color = normal;
                if (disponible != null)
                {
                    int hay = disponible[c.recurso] + (otra != null ? otra[c.recurso] : 0);
                    if (hay < c.cantidad) color = falta;
                }
                float w = AnchoDe(n, Cifra);
                Escribir(new Rect(x, yCentro - 12f, w + 2f, 24f), n, Cifra, color, sombra);
                x += w + 10f;
            }
            return x - x0;
        }

        public static float AnchoCantidades(Cantidad[] lista, float lado = 22f)
        {
            float w = 0f;
            if (lista == null) return 0f;
            foreach (Cantidad c in lista)
            {
                if (c.cantidad <= 0) continue;
                w += lado + 2f + AnchoDe(c.cantidad.ToString(), Cifra) + 10f;
            }
            return w;
        }
    
        // ---------------------------------------------------------------- imágenes sueltas

        static readonly Dictionary<string, Texture2D> imagenes = new Dictionary<string, Texture2D>();

        /// <summary>Una imagen de Resources/UI (por ejemplo "Lineas/ajustes"), cargada una vez.</summary>
        public static Texture2D Imagen(string ruta)
        {
            if (!imagenes.TryGetValue(ruta, out Texture2D t))
            {
                t = Resources.Load<Texture2D>("UI/" + ruta);
                imagenes[ruta] = t;
            }
            return t;
        }

        /// <summary>Dibuja una imagen de Resources/UI teñida de un color (las de trazo son blancas).</summary>
        public static void Dibujar(Rect r, string ruta, Color color, ScaleMode modo = ScaleMode.ScaleToFit)
        {
            Texture2D t = Imagen(ruta);
            if (t == null) return;
            Color previo = GUI.color;
            GUI.color = ConAlfa(color);
            GUI.DrawTexture(r, t, modo, true);
            GUI.color = previo;
        }

        /// <summary>Imagen con las esquinas redondeadas, recortada para llenar el rectángulo.</summary>
        public static void Lamina(Rect r, string ruta, Color color, float radio)
        {
            Texture2D t = Imagen(ruta);
            if (t == null)
            {
                Relleno(r, color, radio);
                return;
            }
            GUI.DrawTexture(r, t, ScaleMode.ScaleAndCrop, false, 0f, ConAlfa(color), 0f, radio);
        }

        /// <summary>Icono de trazo (carpeta Lineas) teñido.</summary>
        public static void Linea(Rect r, string nombre, Color color)
        {
            Dibujar(r, "Lineas/" + nombre, color);
        }

        /// <summary>Imagen girada sobre su centro y estirada en horizontal (para que las hojas aleteen).</summary>
        public static void Girado(Rect r, string ruta, Color color, float grados, float escalaX = 1f)
        {
            Texture2D t = Imagen(ruta);
            if (t == null) return;
            Matrix4x4 previa = GUI.matrix;
            Vector3 centro = new Vector3(r.center.x, r.center.y, 0f);
            GUI.matrix = previa * Matrix4x4.Translate(centro) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, grados))
                         * Matrix4x4.Scale(new Vector3(escalaX, 1f, 1f)) * Matrix4x4.Translate(-centro);
            Color previo = GUI.color;
            GUI.color = ConAlfa(color);
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
            GUI.color = previo;
            GUI.matrix = previa;
        }

        /// <summary>Texto con las letras separadas, como en una inscripción. Devuelve el ancho.</summary>
        public static float Espaciado(float x, float y, float alto, string texto, GUIStyle estilo, Color color, float espacio,
            bool sombra = false, bool medir = false)
        {
            float x0 = x;
            bool envolver = estilo.wordWrap;
            estilo.wordWrap = false;
            for (int i = 0; i < texto.Length; i++)
            {
                string letra = texto[i].ToString();
                float w = estilo.CalcSize(new GUIContent(letra)).x;
                if (!medir) Escribir(new Rect(x, y, w + 2f, alto), letra, estilo, color, sombra);
                x += w + (i < texto.Length - 1 ? espacio : 0f);
            }
            estilo.wordWrap = envolver;
            return x - x0;
        }

        /// <summary>Un círculo de puntos que se encienden de 0 a 1 (horas del día, progreso).</summary>
        public static void AnilloDePuntos(Vector2 centro, float radio, int puntos, float valor, float lado, Color encendido, Color apagado)
        {
            for (int i = 0; i < puntos; i++)
            {
                float a = (i / (float)puntos) * Mathf.PI * 2f - Mathf.PI * 0.5f;
                var p = new Vector2(centro.x + Mathf.Cos(a) * radio, centro.y + Mathf.Sin(a) * radio);
                bool on = i < Mathf.RoundToInt(valor * puntos);
                Relleno(new Rect(p.x - lado * 0.5f, p.y - lado * 0.5f, lado, lado), on ? encendido : apagado, lado * 0.5f);
            }
        }

    }
}
