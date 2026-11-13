using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// La pantalla de título: el valle de fondo (lo pone la cámara del título), hojas de olivo y
    /// de roble llevadas por ráfagas de viento, motas de luz, el nombre del juego con la estrella
    /// de Calcis en la O y una columna jónica, y el menú a la izquierda (continuar, nueva partida,
    /// ajustes y salir). Lo usa Narrativa mientras está en la fase de título.
    /// </summary>
    public class Portada
    {
        public enum Accion
        {
            Ninguna,
            Continuar,
            Nueva,
            Salir
        }

        struct Hoja
        {
            public Vector2 posicion;
            public Vector2 velocidad;
            public float giro;
            public float velocidadGiro;
            public float aleteo;
            public float ritmoAleteo;
            public float tam;
            public float peso;
            public float alfa;
            public int dibujo;
        }

        struct Mota
        {
            public Vector2 posicion;
            public float fase;
            public float tam;
        }

        static readonly string[] Dibujos = { "Hojas/olivo", "Hojas/olivo_claro", "Hojas/olivo", "Hojas/roble", "Hojas/petalo" };
        const int NumeroHojas = 16;
        const int NumeroMotas = 26;

        readonly Hoja[] hojas = new Hoja[NumeroHojas];
        readonly Mota[] motas = new Mota[NumeroMotas];
        readonly System.Random azar = new System.Random(7);
        float desde;
        float ultimo;
        bool iniciado;
        int foco;
        int direccionAnterior;
        bool enAjustes;
        GUIStyle estiloTitulo;

        string[] opciones = new string[0];
        string[] iconos = new string[0];
        Accion[] acciones = new Accion[0];

        float Azar(float a, float b) => a + (float)azar.NextDouble() * (b - a);

        public void Empezar()
        {
            desde = Time.unscaledTime;
            ultimo = desde;
            iniciado = false;
            enAjustes = false;
            foco = 0;
            direccionAnterior = Tema.DireccionActual();
            bool hayPartida = Guardado.HayPartida;
            if (hayPartida)
            {
                opciones = new[] { "Continuar", "Nueva partida", "Ajustes", "Salir" };
                iconos = new[] { "continuar", "nueva", "ajustes", "salir" };
                acciones = new[] { Accion.Continuar, Accion.Nueva, Accion.Ninguna, Accion.Salir };
            }
            else
            {
                opciones = new[] { "Nueva partida", "Ajustes", "Salir" };
                iconos = new[] { "continuar", "ajustes", "salir" };
                acciones = new[] { Accion.Nueva, Accion.Ninguna, Accion.Salir };
            }
        }

        // ---------------------------------------------------------------- entrada

        /// <summary>Teclado y mando. Lo llama Narrativa en Update mientras se ve la portada.</summary>
        public Accion Actualizar()
        {
            if (Time.unscaledTime - desde < 0.5f) return Accion.Ninguna;
            if (enAjustes)
            {
                if (Ajustes.Actualizar()) enAjustes = false;
                return Accion.Ninguna;
            }

            int dir = Tema.DireccionActual();
            bool nueva = dir != direccionAnterior;
            direccionAnterior = dir;
            if (nueva && dir == Tema.Arriba) Mover(-1);
            if (nueva && dir == Tema.Abajo) Mover(1);
            if (Tema.PulsaAceptar()) return Elegir(foco);
            return Accion.Ninguna;
        }

        void Mover(int paso)
        {
            foco = (foco + paso + opciones.Length) % opciones.Length;
            Sonido.Tocar(Efecto.Paso, 0.25f, true);
        }

        Accion Elegir(int i)
        {
            if (i < 0 || i >= opciones.Length) return Accion.Ninguna;
            if (opciones[i] == "Ajustes")
            {
                Sonido.Tocar(Efecto.Pagina);
                enAjustes = true;
                Ajustes.Abrir();
                return Accion.Ninguna;
            }
            return acciones[i];
        }

        // ---------------------------------------------------------------- dibujo

        /// <summary>Dibuja la portada. Devuelve la acción elegida con el ratón, si la hay.</summary>
        public Accion Dibujar()
        {
            float ancho = Tema.Ancho;
            float alto = Tema.Alto;
            float ahora = Time.unscaledTime;
            float dt = Mathf.Clamp(ahora - ultimo, 0f, 0.1f);
            if (Event.current.type == EventType.Repaint)
            {
                ultimo = ahora;
                Animar(dt, ancho, alto);
            }

            // Velo suave a la izquierda para leer el título y el menú; algo de luz cálida abajo.
            Tema.DegradadoLateral(new Rect(0f, 0f, ancho * 0.62f, alto), new Color(0.06f, 0.05f, 0.1f, 0.62f), true);
            Tema.Degradado(new Rect(0f, alto * 0.72f, ancho, alto * 0.28f), new Color(0.05f, 0.04f, 0.03f, 0.45f), true);

            DibujarMotas(ahora);
            DibujarHojas(false);

            float aparece = Mathf.SmoothStep(0f, 1f, (ahora - desde) / 1.6f);
            Color previo = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, aparece);

            float x0 = Mathf.Max(48f, ancho * 0.085f);
            float yTitulo = alto * 0.16f;
            float anchoTitulo = Titulo(x0, yTitulo);

            Accion accion = Accion.Ninguna;
            if (enAjustes)
            {
                var panel = new Rect(x0 - 16f, alto * 0.42f, Mathf.Min(560f, ancho - x0 - 32f), 380f);
                Tema.Panel(panel, true);
                if (Ajustes.Dibujar(new Rect(panel.x + 26f, panel.y + 18f, panel.width - 52f, panel.height - 36f))) enAjustes = false;
            }
            else
            {
                accion = Menu(x0, alto * 0.47f, Mathf.Max(300f, anchoTitulo * 0.48f));
                Tema.Indicaciones(x0 + 140f, alto - 36f, Tema.Pequeno, (Boton.Elegir, "elegir"), (Boton.Aceptar, "aceptar"));
            }

            var version = new GUIStyle(Tema.Pequeno) { alignment = TextAnchor.MiddleRight };
            Tema.Escribir(new Rect(ancho - 220f, alto - 34f, 196f, 20f), "v0.10", version, new Color(1f, 1f, 1f, 0.75f), true);
            GUI.color = previo;

            // Las hojas grandes pasan por delante de todo.
            DibujarHojas(true);
            return accion;
        }

        /// <summary>NOSTOS con las letras separadas, la estrella dentro de la O y la columna bajo la raya.</summary>
        float Titulo(float x, float y)
        {
            if (estiloTitulo == null) estiloTitulo = new GUIStyle(Tema.TituloGrande) { fontSize = 104, wordWrap = false };
            GUIStyle estilo = estiloTitulo;
            const string nombre = "NOSTOS";
            const float espacio = 26f;
            const float alto = 120f;
            Color letra = new Color(0.97f, 0.94f, 0.87f);
            float total = Tema.Espaciado(x, y, alto, nombre, estilo, letra, espacio, false, true);

            float cx = x;
            for (int i = 0; i < nombre.Length; i++)
            {
                string l = nombre[i].ToString();
                float w = Tema.AnchoDe(l, estilo);
                Tema.Escribir(new Rect(cx, y, w + 2f, alto), l, estilo, letra, true);
                if (i == 1)
                {
                    // La estrella de Calcis en el hueco de la primera O, latiendo muy despacio.
                    float lat = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 1.3f);
                    float lado = 30f * lat;
                    var r = new Rect(cx + w * 0.5f - lado * 0.5f, y + alto * 0.52f - lado * 0.5f, lado, lado);
                    Tema.Dibujar(new Rect(r.x - 10f, r.y - 10f, r.width + 20f, r.height + 20f), "Adornos/mota",
                        new Color(1f, 0.85f, 0.55f, 0.55f));
                    Tema.Dibujar(r, "Adornos/estrella", letra);
                }
                cx += w + espacio;
            }

            float yRaya = y + alto + 12f;
            float mitad = x + total * 0.5f;
            const float hueco = 34f;
            Color raya = new Color(letra.r, letra.g, letra.b, 0.9f);
            Tema.Relleno(new Rect(x + 4f, yRaya, mitad - hueco - x - 4f, 2.5f), raya, 1f);
            Tema.Relleno(new Rect(mitad + hueco, yRaya, x + total - mitad - hueco - 4f, 2.5f), raya, 1f);
            Tema.Dibujar(new Rect(mitad - 24f, yRaya - 22f, 48f, 56f), "Adornos/columna", letra);

            var sub = new GUIStyle(Tema.Seccion) { fontSize = 15 };
            float ws = Tema.Espaciado(0f, 0f, 22f, "EL REGRESO", sub, Tema.Ocre, 7f, false, true);
            Tema.Espaciado(mitad - ws * 0.5f, yRaya + 42f, 22f, "EL REGRESO", sub, new Color(0.95f, 0.78f, 0.5f), 7f, true);
            return total;
        }

        Accion Menu(float x, float y, float ancho)
        {
            Accion accion = Accion.Ninguna;
            const float alto = 52f;
            const float separacion = 16f;
            var estilo = new GUIStyle(Tema.BotonTexto) { fontSize = 23, wordWrap = false };
            for (int i = 0; i < opciones.Length; i++)
            {
                var fila = new Rect(x - 18f, y + i * (alto + separacion), ancho, alto);
                Event e = Event.current;
                if (e.type == EventType.MouseMove && fila.Contains(e.mousePosition) && foco != i)
                {
                    foco = i;
                    e.Use();
                }
                bool elegida = foco == i;
                float entra = elegida ? 1f : 0f;
                if (elegida)
                {
                    // Banda de pergamino con un filo de ocre: la opción elegida.
                    Tema.Relleno(new Rect(fila.x + 2f, fila.y + 4f, fila.width, fila.height), new Color(0f, 0f, 0f, 0.3f), 4f);
                    Tema.Relleno(fila, new Color(0.97f, 0.94f, 0.87f, 0.96f), 4f);
                    Tema.Relleno(new Rect(fila.x, fila.y, 5f, fila.height), Tema.Ocre, 2f);
                    Tema.DegradadoLateral(new Rect(fila.xMax - 90f, fila.y, 90f, fila.height), new Color(0.85f, 0.75f, 0.58f, 0.5f), false);
                }
                Color color = elegida ? Tema.Tinta : new Color(0.97f, 0.94f, 0.87f);
                Tema.Linea(new Rect(fila.x + 22f + entra * 2f, fila.y + 12f, 28f, 28f), iconos[i], color);
                Tema.Escribir(new Rect(fila.x + 70f + entra * 4f, fila.y, fila.width - 80f, fila.height), opciones[i], estilo, color,
                    !elegida);
                if (GUI.Button(fila, GUIContent.none, GUIStyle.none))
                {
                    foco = i;
                    accion = Elegir(i);
                }
            }
            return accion;
        }

        // ---------------------------------------------------------------- viento

        void Animar(float dt, float ancho, float alto)
        {
            float t = Time.unscaledTime;
            if (!iniciado)
            {
                iniciado = true;
                for (int i = 0; i < hojas.Length; i++) hojas[i] = NuevaHoja(ancho, alto, true);
                for (int i = 0; i < motas.Length; i++)
                {
                    motas[i] = new Mota
                    {
                        posicion = new Vector2(Azar(ancho * 0.3f, ancho), Azar(alto * 0.35f, alto)),
                        fase = Azar(0f, 10f),
                        tam = Azar(4f, 11f)
                    };
                }
            }

            // Ráfagas: el viento sube y baja despacio, con golpes de vez en cuando.
            float rafaga = Mathf.PerlinNoise(t * 0.13f, 0.5f);
            float golpe = Mathf.Pow(Mathf.PerlinNoise(t * 0.45f, 3.7f), 3f) * 2.2f;
            float viento = 55f + 150f * rafaga + 120f * golpe;

            for (int i = 0; i < hojas.Length; i++)
            {
                Hoja h = hojas[i];
                float ondea = Mathf.Sin(t * 1.7f + h.ritmoAleteo * 3f) * 30f;
                Vector2 objetivo = new Vector2(viento * h.peso, 26f + ondea * 0.8f) ;
                h.velocidad = Vector2.Lerp(h.velocidad, objetivo, dt * 1.4f);
                h.posicion += h.velocidad * dt;
                h.giro += (h.velocidadGiro + viento * 0.25f * Mathf.Sign(h.velocidadGiro)) * dt;
                h.aleteo += h.ritmoAleteo * (0.6f + viento / 220f) * dt;
                bool fuera = h.posicion.x > ancho + 80f || h.posicion.y > alto + 80f;
                hojas[i] = fuera ? NuevaHoja(ancho, alto, false) : h;
            }

            for (int i = 0; i < motas.Length; i++)
            {
                Mota m = motas[i];
                m.posicion += new Vector2(viento * 0.06f + Mathf.Sin(t * 0.7f + m.fase) * 6f, Mathf.Cos(t * 0.5f + m.fase) * 5f) * dt;
                if (m.posicion.x > ancho + 20f) m.posicion = new Vector2(ancho * 0.25f, Azar(alto * 0.35f, alto));
                motas[i] = m;
            }
        }

        Hoja NuevaHoja(float ancho, float alto, bool repartida)
        {
            bool cerca = azar.NextDouble() < 0.18;
            var h = new Hoja
            {
                // Entran por la izquierda o por arriba; al empezar ya hay algunas en pantalla.
                posicion = repartida
                    ? new Vector2(Azar(-60f, ancho), Azar(-40f, alto * 0.8f))
                    : (azar.NextDouble() < 0.6
                        ? new Vector2(Azar(-120f, -30f), Azar(-40f, alto * 0.7f))
                        : new Vector2(Azar(-60f, ancho * 0.6f), Azar(-120f, -30f))),
                velocidad = new Vector2(Azar(40f, 90f), Azar(10f, 40f)),
                giro = Azar(0f, 360f),
                velocidadGiro = Azar(40f, 140f) * (azar.NextDouble() < 0.5 ? -1f : 1f),
                aleteo = Azar(0f, 6f),
                ritmoAleteo = Azar(2.5f, 5.5f),
                tam = cerca ? Azar(46f, 64f) : Azar(18f, 32f),
                peso = cerca ? Azar(1.3f, 1.7f) : Azar(0.6f, 1.1f),
                alfa = cerca ? 0.75f : Azar(0.75f, 1f),
                dibujo = azar.Next(Dibujos.Length)
            };
            return h;
        }

        void DibujarHojas(bool cercanas)
        {
            for (int i = 0; i < hojas.Length; i++)
            {
                Hoja h = hojas[i];
                if (h.tam > 40f != cercanas) continue;
                float aleteo = Mathf.Cos(h.aleteo);
                float escalaX = Mathf.Max(0.12f, Mathf.Abs(aleteo));
                // La cara de atrás se ve algo más oscura al girar.
                float luz = aleteo >= 0f ? 1f : 0.78f;
                var r = new Rect(h.posicion.x - h.tam * 0.5f, h.posicion.y - h.tam * 0.5f, h.tam, h.tam);
                Tema.Girado(new Rect(r.x + 3f, r.y + 5f, r.width, r.height), Dibujos[h.dibujo], new Color(0f, 0f, 0f, 0.18f * h.alfa),
                    h.giro, escalaX);
                Tema.Girado(r, Dibujos[h.dibujo], new Color(luz, luz, luz, h.alfa), h.giro, escalaX);
            }
        }

        void DibujarMotas(float t)
        {
            for (int i = 0; i < motas.Length; i++)
            {
                Mota m = motas[i];
                float brillo = 0.25f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 1.1f + m.fase * 2f));
                var r = new Rect(m.posicion.x - m.tam * 0.5f, m.posicion.y - m.tam * 0.5f, m.tam, m.tam);
                Tema.Dibujar(r, "Adornos/mota", new Color(1f, 0.93f, 0.75f, brillo));
            }
        }
    }
}
