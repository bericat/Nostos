using System.Collections.Generic;
using Nostos.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>
    /// El menú de construcción del modo constructor: una hoja del cuaderno de Nicias (papel,
    /// cuadrícula de dibujante y bocetos de la nave) en un marco de nogal con cantoneras de cobre,
    /// con pestañas de cuero (vivienda, producción, servicios, caminos), una lámina por edificio
    /// y la ficha del elegido con su coste. Al elegir, el menú se pliega en una ficha pequeña y se
    /// coloca la obra en el mapa como siempre.
    ///
    /// Se abre al pasar al modo constructor, con B (teclado) o ▼ en la cruceta, o con un clic en
    /// la ficha pequeña. Mientras está abierto, el mapa no se mueve.
    /// </summary>
    public class MenuConstruccion : MonoBehaviour
    {
        public ControlModo control;
        public Juego juego;
        public ColocadorRejilla colocador;

        public static bool Abierto { get; private set; }
        /// <summary>Rectángulo (en la pantalla virtual) que ocupa el menú o la ficha, para no colocar debajo.</summary>
        public static Rect Ocupado { get; private set; }

        /// <summary>¿Está el puntero (en píxeles de pantalla, origen abajo) sobre el menú o la ficha?</summary>
        public static bool TapaPuntero(Vector2 pantalla)
        {
            if (Ocupado.width <= 0f) return false;
            var virtual_ = new Vector2(pantalla.x / Tema.Escala, (Screen.height - pantalla.y) / Tema.Escala);
            return Ocupado.Contains(virtual_);
        }

        enum Pestana
        {
            Vivienda,
            Produccion,
            Servicios,
            Caminos
        }

        static readonly string[] NombresPestana = { "Vivienda", "Producción", "Servicios", "Caminos" };
        static readonly string[] IconosPestana = { "vivienda", "produccion", "servicios", "camino" };
        const int Columnas = 4;

        static readonly Dictionary<string, string> Descripciones = new Dictionary<string, string>
        {
            { "refugio", "Donde Nicias duerme y pasa la noche a salvo. Lo primero que hay que levantar." },
            { "banco", "Un banco con tornillo y muela. Aquí forjas tu hacha, tu pico y tu pala, y herramientas para el pueblo." },
            { "aserradero", "Sierra la madera en tablones, el material de casi todo lo demás." },
            { "fundicion", "Funde el mineral en lingotes y chapa. Necesita leña para el horno." },
            { "almacen", "Guarda lo que traen Nicias y los colonos. Sin almacén no hay reparto ni porteadores." },
            { "casa", "Hogar para dos colonos. Quien tiene casa duerme mejor y se fía más." },
            { "comedor", "Convierte la comida en raciones. Un pueblo con hambre no trabaja." },
            { "pozo", "Sube agua limpia para el pueblo y para la nave." },
            { "taller", "Con el cobre hace cable y bobinas para los sistemas de la nave." },
            { "optica", "Talla el cristal en lentes y núcleos: la navegación y el combustible." },
            { "astillero", "Grúa y gradas para montar la nave con lo que hay en el almacén." },
            { "teleferico", "Vagonetas que llevan material del almacén a las obras sin manos." },
            { "camino", "Losas sobre la tierra. Gratis. Los colonos los prefieren y por ellos se anda más deprisa." }
        };

        // Colores del papel y de la tinta.
        static readonly Color Papel = new Color(0.96f, 0.92f, 0.83f);
        static readonly Color Tinta = new Color(0.17f, 0.12f, 0.08f);
        static readonly Color TintaSuave = new Color(0.36f, 0.28f, 0.2f);
        static readonly Color Cuero = new Color(0.36f, 0.22f, 0.14f);
        static readonly Color CueroOscuro = new Color(0.24f, 0.15f, 0.1f);

        Pestana pestana;
        int foco;
        int direccionAnterior;
        ModoJuego modoAnterior = ModoJuego.APie;
        bool bloqueadoPorMi;
        readonly List<int> casillas = new List<int>();
        GUIStyle nombreLamina;
        GUIStyle tituloFicha;
        GUIStyle textoFicha;
        GUIStyle pestanaTexto;
        GUIStyle pista;

        void OnDestroy()
        {
            Abierto = false;
            Ocupado = Rect.zero;
        }

        void Update()
        {
            if (control == null || juego == null || colocador == null || juego.catalogo == null) return;
            bool enConstructor = control.Modo == ModoJuego.Constructor;

            if (!enConstructor)
            {
                if (Abierto) Cerrar();
                modoAnterior = control.Modo;
                return;
            }
            if (modoAnterior != ModoJuego.Constructor && !control.Bloqueado)
            {
                modoAnterior = control.Modo;
                Abrir();
                return;
            }
            modoAnterior = control.Modo;

            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            bool alternar = (teclado != null && teclado.bKey.wasPressedThisFrame)
                            || (mando != null && mando.dpad.down.wasPressedThisFrame);

            if (!Abierto)
            {
                if (alternar && !control.Bloqueado) Abrir();
                return;
            }

            // Cambiar de modo con el menú abierto: se cierra y se pasa a pie.
            bool modo = (teclado != null && teclado.tabKey.wasPressedThisFrame) || (mando != null && mando.buttonNorth.wasPressedThisFrame);
            if (modo && Time.frameCount > abiertoEn)
            {
                Cerrar();
                control.Aplicar(ModoJuego.APie);
                return;
            }
            // Esc o B (o la tecla del menú) lo cierran.
            bool volver = (teclado != null && teclado.escapeKey.wasPressedThisFrame) || (mando != null && mando.buttonEast.wasPressedThisFrame);
            // Con el menú abierto la cruceta mueve el foco, así que solo la tecla B lo pliega.
            bool plegar = teclado != null && teclado.bKey.wasPressedThisFrame;
            if ((plegar || volver) && Time.frameCount > abiertoEn)
            {
                Cerrar();
                return;
            }
            bool izquierda = (teclado != null && teclado.qKey.wasPressedThisFrame) || (mando != null && mando.leftShoulder.wasPressedThisFrame);
            bool derecha = (teclado != null && teclado.eKey.wasPressedThisFrame) || (mando != null && mando.rightShoulder.wasPressedThisFrame);
            if (izquierda) CambiarPestana(-1);
            if (derecha) CambiarPestana(1);

            int dir = Tema.DireccionActual();
            bool nueva = dir != direccionAnterior;
            direccionAnterior = dir;
            if (nueva && casillas.Count > 0)
            {
                if (dir == Tema.Derecha) foco = Mathf.Min(casillas.Count - 1, foco + 1);
                if (dir == Tema.Izquierda) foco = Mathf.Max(0, foco - 1);
                if (dir == Tema.Abajo && foco + Columnas < casillas.Count) foco += Columnas;
                if (dir == Tema.Arriba && foco - Columnas >= 0) foco -= Columnas;
            }
            bool aceptar = (teclado != null && (teclado.enterKey.wasPressedThisFrame || teclado.spaceKey.wasPressedThisFrame))
                           || (mando != null && mando.buttonSouth.wasPressedThisFrame);
            if (aceptar) ElegirFoco();
        }

        int abiertoEn;

        void Abrir()
        {
            Abierto = true;
            abiertoEn = Time.frameCount;
            Rellenar(PestanaDe(colocador.Indice));
            int i = casillas.IndexOf(colocador.Indice);
            foco = i >= 0 ? i : 0;
            direccionAnterior = Tema.DireccionActual();
            if (!control.Bloqueado)
            {
                control.Bloquear(true);
                bloqueadoPorMi = true;
            }
            Sonido.Tocar(Efecto.Pagina, 0.6f);
        }

        void Cerrar()
        {
            Abierto = false;
            if (bloqueadoPorMi && control.Bloqueado) control.Bloquear(false);
            bloqueadoPorMi = false;
        }

        void ElegirFoco()
        {
            if (foco < 0 || foco >= casillas.Count) return;
            colocador.Elegir(casillas[foco]);
            Sonido.Tocar(Efecto.Golpe, 0.4f, true);
            Cerrar();
        }

        void CambiarPestana(int paso)
        {
            int n = NombresPestana.Length;
            Rellenar((Pestana)(((int)pestana + paso + n) % n));
            foco = 0;
            Sonido.Tocar(Efecto.Paso, 0.3f, true);
        }

        Pestana PestanaDe(int indice)
        {
            DefinicionEdificio[] catalogo = juego.catalogo;
            if (indice >= catalogo.Length) return Pestana.Caminos;
            switch (catalogo[indice].tipo)
            {
                case TipoEdificio.Refugio:
                case TipoEdificio.Casa:
                    return Pestana.Vivienda;
                case TipoEdificio.Produccion:
                    return Pestana.Produccion;
                default:
                    return Pestana.Servicios;
            }
        }

        void Rellenar(Pestana p)
        {
            pestana = p;
            casillas.Clear();
            int total = colocador.Casillas;
            for (int i = 0; i < total; i++)
            {
                if (PestanaDe(i) == p) casillas.Add(i);
            }
        }

        // ---------------------------------------------------------------- datos de una casilla

        string Id(int indice) => indice >= juego.catalogo.Length ? "camino" : juego.catalogo[indice].id;
        string Nombre(int indice) => indice >= juego.catalogo.Length ? "Camino" : juego.catalogo[indice].nombre;

        static string Descripcion(string id) => Descripciones.TryGetValue(id, out string d) ? d : "";

        static Cantidad[] CosteTotal(DefinicionEdificio d)
        {
            var total = new int[Recursos.Numero];
            foreach (Cantidad[] etapa in new[] { d.costeCimientos, d.costeEsqueleto, d.costeCasiTerminado })
            {
                if (etapa == null) continue;
                foreach (Cantidad c in etapa) total[(int)c.recurso] += c.cantidad;
            }
            var lista = new List<Cantidad>();
            for (int i = 0; i < total.Length; i++)
            {
                if (total[i] > 0) lista.Add(new Cantidad((Recurso)i, total[i]));
            }
            return lista.ToArray();
        }

        // ---------------------------------------------------------------- dibujo

        void OnGUI()
        {
            if (control == null || juego == null || colocador == null || juego.catalogo == null) return;
            if (control.Modo != ModoJuego.Constructor)
            {
                Ocupado = Rect.zero;
                return;
            }
            if (Narrativa.Instancia != null && (Narrativa.Instancia.EnTitulo || Narrativa.Instancia.EnEscena)) return;
            if (MenuPausa.Abierto) return;
            GUI.depth = 4;
            Tema.Empezar();
            if (nombreLamina == null) CrearEstilos();

            if (Abierto) DibujarMenu();
            else DibujarFicha();
        }

        void CrearEstilos()
        {
            nombreLamina = new GUIStyle(Tema.PequenoCentro) { fontSize = 14, wordWrap = false };
            tituloFicha = new GUIStyle(Tema.Titulo) { fontSize = 24 };
            textoFicha = new GUIStyle(Tema.Texto) { fontSize = 16 };
            pestanaTexto = new GUIStyle(Tema.BotonTexto) { fontSize = 16, wordWrap = false };
            pista = new GUIStyle(Tema.Pequeno) { wordWrap = false };
            pista.normal.textColor = TintaSuave;
        }

        /// <summary>Marco de nogal con doble filo y cantoneras de cobre, y el papel dentro.</summary>
        static Rect Marco(Rect r)
        {
            Tema.Relleno(new Rect(r.x + 3f, r.y + 6f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f), 14f);
            Tema.Relleno(r, new Color(0.2f, 0.13f, 0.08f, 0.98f), 12f);
            Tema.Filo(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.55f), 1.2f, 10f);
            var papel = new Rect(r.x + 10f, r.y + 10f, r.width - 20f, r.height - 20f);
            Tema.Lamina(papel, "Fondos/plano", Color.white, 6f);
            Tema.Filo(papel, new Color(0.25f, 0.16f, 0.1f, 0.6f), 1.5f, 6f);
            // Cantoneras de cobre en las cuatro esquinas.
            const float l = 22f, g = 5f;
            Color cobre = Tema.Cobre;
            Color brillo = new Color(1f, 0.78f, 0.55f, 0.8f);
            foreach (Vector2 esquina in new[] { new Vector2(r.x, r.y), new Vector2(r.xMax, r.y), new Vector2(r.x, r.yMax), new Vector2(r.xMax, r.yMax) })
            {
                float sx = esquina.x <= r.x ? 1f : -1f;
                float sy = esquina.y <= r.y ? 1f : -1f;
                float x0 = sx > 0 ? esquina.x + 2f : esquina.x - 2f - l;
                float y0 = sy > 0 ? esquina.y + 2f : esquina.y - 2f - g;
                Tema.Relleno(new Rect(x0, y0, l, g), cobre, 1.5f);
                float x1 = sx > 0 ? esquina.x + 2f : esquina.x - 2f - g;
                float y1 = sy > 0 ? esquina.y + 2f : esquina.y - 2f - l;
                Tema.Relleno(new Rect(x1, y1, g, l), cobre, 1.5f);
                Tema.Relleno(new Rect(x1 + 1.5f, y0 + 1.5f, 2f, 2f), brillo, 1f);
            }
            return papel;
        }

        void DibujarMenu()
        {
            float ancho = Mathf.Min(820f, Tema.Ancho - 32f);
            const float alto = 400f;
            var r = new Rect(16f, Tema.Alto - 16f - alto, ancho, alto);
            Ocupado = r;
            Rect papel = Marco(r);
            Event e = Event.current;

            // Pestañas de cuero cosidas al borde de arriba del papel.
            float px = papel.x + 14f;
            float py = papel.y + 10f;
            for (int i = 0; i < NombresPestana.Length; i++)
            {
                bool activa = (int)pestana == i;
                float w = 42f + Tema.AnchoDe(NombresPestana[i], pestanaTexto) + 18f;
                var t = new Rect(px, py, w, 34f);
                if (activa)
                {
                    Tema.Relleno(new Rect(t.x + 1f, t.y + 3f, t.width, t.height), new Color(0f, 0f, 0f, 0.25f), 6f);
                    Tema.Relleno(t, Cuero, 6f);
                    Tema.Filo(new Rect(t.x + 3f, t.y + 3f, t.width - 6f, t.height - 6f), new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.7f), 1f, 4f);
                }
                else if (t.Contains(e.mousePosition))
                {
                    Tema.Relleno(t, new Color(Cuero.r, Cuero.g, Cuero.b, 0.25f), 6f);
                }
                Color c = activa ? Tema.Pergamino : TintaSuave;
                Tema.Linea(new Rect(t.x + 12f, t.y + 7f, 20f, 20f), IconosPestana[i], c);
                Tema.Escribir(new Rect(t.x + 40f, t.y, w - 40f, t.height), NombresPestana[i], pestanaTexto, c);
                if (GUI.Button(t, GUIContent.none, GUIStyle.none) && !activa)
                {
                    Rellenar((Pestana)i);
                    foco = 0;
                    Sonido.Tocar(Efecto.Paso, 0.3f, true);
                }
                px += w + 6f;
            }

            // Cerrar, arriba a la derecha.
            var cerrar = new Rect(papel.xMax - 40f, papel.y + 12f, 28f, 28f);
            bool encimaCerrar = cerrar.Contains(e.mousePosition);
            Tema.Linea(new Rect(cerrar.x + 5f, cerrar.y + 5f, 18f, 18f), "cerrar", encimaCerrar ? Tema.Cobre : TintaSuave);
            float wk = Tema.AnchoTecla(Boton.Obras);
            Tema.DibujarTecla(cerrar.x - wk - 4f, cerrar.center.y, Boton.Obras);
            if (GUI.Button(cerrar, GUIContent.none, GUIStyle.none))
            {
                Cerrar();
                return;
            }

            // Raya de tinta bajo las pestañas.
            Tema.Relleno(new Rect(papel.x + 14f, py + 40f, papel.width - 28f, 1.5f), new Color(Tinta.r, Tinta.g, Tinta.b, 0.45f));

            // Láminas.
            float anchoFicha = 268f;
            var rejilla = new Rect(papel.x + 18f, py + 54f, papel.width - anchoFicha - 50f, papel.height - (py + 54f - papel.y) - 34f);
            float lado = Mathf.Min(116f, (rejilla.width - (Columnas - 1) * 12f) / Columnas);
            for (int k = 0; k < casillas.Count; k++)
            {
                int col = k % Columnas;
                int fila = k / Columnas;
                var l = new Rect(rejilla.x + col * (lado + 12f), rejilla.y + fila * (lado + 14f), lado, lado);
                if (l.yMax > rejilla.yMax + 20f) break;
                if (e.type == EventType.MouseMove && l.Contains(e.mousePosition) && foco != k)
                {
                    foco = k;
                    e.Use();
                }
                Lamina(l, casillas[k], k == foco, casillas[k] == colocador.Indice);
                if (GUI.Button(l, GUIContent.none, GUIStyle.none))
                {
                    foco = k;
                    ElegirFoco();
                    return;
                }
            }

            // Separador vertical de tinta y la ficha.
            float sx = papel.xMax - anchoFicha - 26f;
            Tema.Relleno(new Rect(sx, py + 54f, 1.5f, papel.height - 100f), new Color(Tinta.r, Tinta.g, Tinta.b, 0.3f));
            if (foco >= 0 && foco < casillas.Count)
            {
                Ficha(new Rect(sx + 16f, py + 50f, anchoFicha, papel.yMax - py - 60f), casillas[foco]);
            }

            Tema.Indicaciones(papel.x + (sx - papel.x) * 0.5f, papel.yMax - 16f, pista,
                (Boton.Pestana, "pestaña"), (Boton.Elegir, "elegir"), (Boton.Aceptar, "construir"), (Boton.Salir, "cerrar"));
        }

        /// <summary>Una lámina: papel algo más claro, la miniatura del edificio y su nombre a mano.</summary>
        void Lamina(Rect l, int indice, bool enfocada, bool elegida)
        {
            float lev = enfocada ? 3f : 0f;
            var r = new Rect(l.x, l.y - lev, l.width, l.height);
            Tema.Relleno(new Rect(r.x + 2f, r.y + 3f + lev, r.width, r.height), new Color(0.3f, 0.2f, 0.1f, enfocada ? 0.35f : 0.18f), 6f);
            Tema.Relleno(r, new Color(1f, 0.98f, 0.93f, enfocada ? 0.95f : 0.7f), 6f);
            Tema.Filo(r, enfocada ? Tema.Cobre : new Color(Tinta.r, Tinta.g, Tinta.b, 0.3f), enfocada ? 2.5f : 1f, 6f);
            // Chincheta de cobre arriba.
            Tema.Relleno(new Rect(r.center.x - 4f, r.y + 4f, 8f, 8f), elegida ? Tema.Ocre : Tema.Cobre, 4f);

            string id = Id(indice);
            Tema.Dibujar(new Rect(r.x + 8f, r.y + 10f, r.width - 16f, r.height - 34f), "Edificios/" + id, Color.white);
            Tema.Escribir(new Rect(r.x + 2f, r.yMax - 24f, r.width - 4f, 20f), Nombre(indice), nombreLamina, Tinta);

            // Si hoy no da para empezarla, una marca roja discreta.
            if (indice < juego.catalogo.Length && !AlcanzaParaCimientos(juego.catalogo[indice]))
            {
                Tema.Relleno(new Rect(r.xMax - 14f, r.y + 6f, 8f, 8f), new Color(0.75f, 0.25f, 0.18f, 0.85f), 4f);
            }
        }

        bool AlcanzaParaCimientos(DefinicionEdificio d)
        {
            if (d.costeCimientos == null) return true;
            foreach (Cantidad c in d.costeCimientos)
            {
                if (juego.Mundo.Almacen[c.recurso] + juego.Mundo.Mochila[c.recurso] < c.cantidad) return false;
            }
            return true;
        }

        void Ficha(Rect r, int indice)
        {
            string id = Id(indice);
            bool camino = indice >= juego.catalogo.Length;
            DefinicionEdificio d = camino ? null : juego.catalogo[indice];

            Tema.Dibujar(new Rect(r.x + 50f, r.y, r.width - 100f, 100f), "Edificios/" + id, Color.white);
            float y = r.y + 102f;
            Tema.Escribir(new Rect(r.x, y, r.width, 30f), Nombre(indice), tituloFicha, Tinta);
            y += 30f;
            string tam = camino ? "una celda · gratis" : d.ancho + " × " + d.fondo + " celdas";
            Tema.Escribir(new Rect(r.x, y, r.width, 18f), tam, Tema.Pequeno, TintaSuave);
            y += 20f;
            string texto = Descripcion(id);
            float h = Tema.AltoDe(texto, textoFicha, r.width);
            Tema.Escribir(new Rect(r.x, y, r.width, h), texto, textoFicha, Tinta);
            y += h + 6f;

            if (!camino)
            {
                bool hace = d.salidas != null && d.salidas.Length > 0;
                Tema.Escribir(new Rect(r.x, y, r.width, 18f), "COSTE TOTAL", Tema.Seccion, new Color(0.55f, 0.32f, 0.12f));
                if (hace)
                {
                    float wh = Tema.AnchoCantidades(d.salidas, 22f);
                    Tema.Escribir(new Rect(r.xMax - wh - 4f, y, wh + 4f, 18f), "HACE", Tema.Seccion, new Color(0.55f, 0.32f, 0.12f));
                }
                y += 20f;
                Tema.Cantidades(r.x, y + 12f, CosteTotal(d), 24f, juego.Mundo.Almacen, juego.Mundo.Mochila, Tinta,
                    new Color(0.72f, 0.2f, 0.12f), false);
                if (hace)
                {
                    float wh = Tema.AnchoCantidades(d.salidas, 22f);
                    Tema.Cantidades(r.xMax - wh, y + 12f, d.salidas, 22f, null, null, Tinta, Tinta, false);
                }
            }

            // Botón de construir: banda de ocre con la tecla.
            var boton = new Rect(r.x, r.yMax - 46f, r.width, 40f);
            bool encima = boton.Contains(Event.current.mousePosition);
            Tema.Relleno(new Rect(boton.x + 1f, boton.y + 3f, boton.width, boton.height), new Color(0.3f, 0.18f, 0.08f, 0.35f), 6f);
            Tema.Relleno(boton, encima ? new Color(0.95f, 0.72f, 0.36f) : Tema.Ocre, 6f);
            string etiqueta = camino ? "Trazar caminos" : "Construir";
            float wt = Tema.AnchoDe(etiqueta, Tema.BotonTexto);
            float wk = Tema.AnchoTecla(Boton.Aceptar);
            float x = boton.center.x - (wt + wk + 10f) * 0.5f;
            Tema.DibujarTecla(x, boton.center.y, Boton.Aceptar);
            Tema.Escribir(new Rect(x + wk + 10f, boton.y, wt + 4f, boton.height), etiqueta, new GUIStyle(Tema.BotonTexto) { wordWrap = false },
                Tema.Tinta);
            if (GUI.Button(boton, GUIContent.none, GUIStyle.none)) ElegirFoco();
        }

        /// <summary>Con el menú plegado: lo que hay elegido, para colocarlo, y cómo volver al menú.</summary>
        void DibujarFicha()
        {
            int indice = colocador.Indice;
            bool camino = indice >= juego.catalogo.Length;
            DefinicionEdificio d = camino ? null : juego.catalogo[indice];
            string nombre = Nombre(indice);

            float anchoNombre = Tema.AnchoDe(nombre, Tema.Titulo);
            string nota = camino ? "mantén para trazar · gratis" : d.ancho + " × " + d.fondo + " celdas";
            Cantidad[] coste = camino ? null : CosteTotal(d);
            float anchoCoste = camino ? Tema.AnchoDe(nota, Tema.Pequeno) : Tema.AnchoCantidades(coste, 20f);
            float ancho = Mathf.Max(330f, 98f + Mathf.Max(anchoNombre, anchoCoste) + 28f);
            var r = new Rect(16f, Tema.Alto - 16f - 104f, ancho, 104f);
            Ocupado = r;
            Rect papel = Marco(r);

            Tema.Dibujar(new Rect(papel.x + 6f, papel.y + 4f, 76f, 76f), "Edificios/" + Id(indice), Color.white);
            float x = papel.x + 90f;
            Tema.Escribir(new Rect(x, papel.y + 6f, anchoNombre + 4f, 28f), nombre, Tema.Titulo, Tinta);
            if (camino) Tema.Escribir(new Rect(x, papel.y + 38f, anchoCoste + 4f, 20f), nota, Tema.Pequeno, TintaSuave);
            else Tema.Cantidades(x, papel.y + 48f, coste, 20f, juego.Mundo.Almacen, juego.Mundo.Mochila, Tinta, new Color(0.72f, 0.2f, 0.12f), false);

            float yi = papel.yMax - 13f;
            float xi = x;
            xi += Tema.Indicacion(xi, yi, Boton.Obras, "menú", pista) + 14f;
            Tema.Indicacion(xi, yi, Boton.Cambiar, "cambiar", pista);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) Abrir();
        }
    }
}
