using Nostos.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>
    /// El HUD del juego (OnGUI con el aspecto de Tema): reloj y día, mochila con iconos, el
    /// estado de Nova Lavinia (confianza, nave, tributo), los avisos, la ayuda de controles del
    /// dispositivo que se está usando, el punto de mira con lo que se puede hacer, las fichas
    /// sobre obras, edificios y colonos, y el fundido al dormir y al final.
    /// </summary>
    public class HudPrototipo : MonoBehaviour
    {
        public ControlModo control;
        public Juego juego;
        public ControladorAPie jugador;
        public ColocadorRejilla colocador;
        public Camera camara;
        public MenuPausa pausa;
        public float distanciaEtiquetasAPie = 30f;

        const float Margen = 16f;
        const float AnchoLateral = 360f;
        const float DistanciaColonos = 20f;

        bool ayudaVisible = true;
        string avisoTexto;
        float avisoDesde;
        float avisoHasta;

        void Update()
        {
            Tema.ActualizarDispositivo();
            VigilarMochila();
            if (control == null) return;
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            if (control.Bloqueado) return;
            if ((teclado != null && teclado.hKey.wasPressedThisFrame) || (mando != null && mando.selectButton.wasPressedThisFrame))
            {
                ayudaVisible = !ayudaVisible;
            }

            if (control.Modo != ModoJuego.Constructor)
            {
                EdificioEnMundo.SinTejados = false;
                return;
            }
            bool alternar = (teclado != null && teclado.tKey.wasPressedThisFrame)
                            || (mando != null && mando.dpad.up.wasPressedThisFrame);
            if (alternar) EdificioEnMundo.SinTejados = !EdificioEnMundo.SinTejados;
        }

        void OnGUI()
        {
            if (control == null || juego == null || juego.Mundo == null) return;
            if (Narrativa.Instancia != null && Narrativa.Instancia.EnTitulo) return;
            GUI.depth = 10;
            Tema.Empezar();

            bool aPie = control.Modo == ModoJuego.APie;
            Mundo mundo = juego.Mundo;
            bool final = juego.Despegado && juego.Fundido >= 1f;

            if (!final)
            {
                Etiquetas(aPie);
                float y = FichaNicias(mundo, aPie);
                Mochila(mundo.Mochila, y + 8f);
                float yd = BarraRecursos(mundo);
                yd = Minimapa(mundo, aPie, yd + 16f);
                Objetivo(mundo, yd + 10f);
                if (!(MenuConstruccion.Abierto && !aPie)) Ayuda(aPie);
                Botones(aPie);
                if (aPie && !control.Bloqueado) PuntoDeMira();
                Obtenidos();
                if (juego.ModoDios) ChipModoDios();
            }
            Aviso();

            if (juego.Fundido > 0f)
            {
                Tema.Relleno(new Rect(0f, 0f, Tema.Ancho, Tema.Alto), new Color(0f, 0f, 0f, juego.Fundido));
            }
            if (final)
            {
                string texto = Textos.Final(mundo.Ciclo.Dia, mundo.Colonos.ConfianzaGlobal);
                float ancho = Mathf.Min(980f, Tema.Ancho - 40f);
                Tema.Escribir(new Rect((Tema.Ancho - ancho) * 0.5f, Tema.Alto * 0.5f - 260f, ancho, 520f), texto, Tema.TextoGrande);
            }
        }

        // ---------------------------------------------------------------- ficha de Nicias

        static string Momento(float hora)
        {
            if (hora < 6f) return "noche cerrada";
            if (hora < 8f) return "amanece";
            if (hora < 12f) return "mañana";
            if (hora < 15f) return "mediodía";
            if (hora < 18f) return "tarde";
            if (hora < 20f) return "atardece";
            return "noche";
        }

        /// <summary>
        /// Arriba a la izquierda: el retrato de Nicias rodeado de 24 puntos (las horas del día),
        /// el día y la hora, la carga de la mochila y la confianza del pueblo. Debajo, el modo.
        /// Devuelve dónde acaba.
        /// </summary>
        float FichaNicias(Mundo mundo, bool aPie)
        {
            var panel = new Rect(Margen, Margen, 352f, 108f);
            Tema.Panel(panel);
            var centro = new Vector2(panel.x + 58f, panel.center.y);
            float hora = juego.HoraSuave;
            bool deNoche = hora >= 20f || hora < 6f;
            Tema.Relleno(new Rect(centro.x - 47f, centro.y - 47f, 94f, 94f), Tema.FondoHueco, 47f);
            Tema.AnilloDePuntos(centro, 44f, 24, hora / 24f, 4.5f, deNoche ? Tema.Azul : Tema.Ocre,
                new Color(Tema.Apagado.r, Tema.Apagado.g, Tema.Apagado.b, 0.25f));
            var retrato = new Rect(centro.x - 37f, centro.y - 37f, 74f, 74f);
            if (Tema.Imagen("Retratos/nicias_medallon") != null) Tema.Dibujar(retrato, "Retratos/nicias_medallon", Color.white);
            else Tema.Relleno(retrato, Tema.Cobre, 37f);
            Tema.Filo(new Rect(retrato.x - 1f, retrato.y - 1f, retrato.width + 2f, retrato.height + 2f), Tema.Borde, 2f, 38f);
            // Sol o luna sobre el anillo, en la hora que es.
            float a = hora / 24f * Mathf.PI * 2f - Mathf.PI * 0.5f;
            var astro = new Rect(centro.x + Mathf.Cos(a) * 44f - 10f, centro.y + Mathf.Sin(a) * 44f - 10f, 20f, 20f);
            Tema.Icono(astro, deNoche ? Tema.Simbolo.Luna : Tema.Simbolo.Sol);

            float x = panel.x + 118f;
            float w = panel.xMax - 14f - x;
            Tema.Escribir(new Rect(x, panel.y + 8f, 120f, 26f), "Nicias", Tema.Titulo);
            int h = Mathf.FloorToInt(hora);
            int m = Mathf.FloorToInt((hora - h) * 60f);
            string linea = "Día " + mundo.Ciclo.Dia + " · " + h.ToString("00") + ":" + m.ToString("00") + " · " + Momento(hora);
            if (mundo.Ciclo.PuedeDormir) linea += " · a dormir";
            Tema.Escribir(new Rect(x, panel.y + 36f, w, 20f), linea, Tema.Pequeno);

            Bolsa mochila = mundo.Mochila;
            bool llena = mochila.Capacidad > 0 && mochila.Total >= mochila.Capacidad;
            float carga = mochila.Capacidad > 0 ? mochila.Total / (float)mochila.Capacidad : 0f;
            Tema.Icono(new Rect(x - 2f, panel.y + 58f, 20f, 20f), Tema.Simbolo.Mochila);
            Tema.Barra(new Rect(x + 24f, panel.y + 65f, w - 72f, 7f), carga, llena ? Tema.Rojo : Tema.Ocre);
            string cuenta = mochila.Total + "/" + mochila.Capacidad;
            Tema.Escribir(new Rect(x + w - 44f, panel.y + 57f, 46f, 22f), cuenta, Tema.Cifra, llena ? Tema.Rojo : Tema.Pergamino);

            if (mundo.Colonos.Colonos.Count > 0)
            {
                float confianza = mundo.Colonos.ConfianzaGlobal;
                Tema.Icono(new Rect(x - 2f, panel.y + 81f, 20f, 20f), Tema.Simbolo.Confianza);
                Tema.Barra(new Rect(x + 24f, panel.y + 88f, w - 72f, 7f), confianza / 100f, ColorConfianza(confianza),
                    GestorColonos.UmbralMedia / 100f, GestorColonos.UmbralAlta / 100f);
                Tema.Escribir(new Rect(x + w - 44f, panel.y + 80f, 46f, 22f), Mathf.RoundToInt(confianza).ToString(), Tema.Cifra);
            }
            else
            {
                Tema.Escribir(new Rect(x, panel.y + 80f, w, 20f), "Ingeniero de Calcis", Tema.Pequeno, new Color(Tema.Apagado.r, Tema.Apagado.g, Tema.Apagado.b, 0.7f));
            }

            // Chapa del modo, con la tecla para cambiar.
            string modo = (aPie ? "A pie" : "Constructor").ToUpperInvariant();
            string otro = aPie ? "constructor" : "a pie";
            float anchoModo = Tema.AnchoDe(modo, Tema.Seccion);
            float ancho = 46f + anchoModo + 14f + Tema.AnchoIndicacion(Boton.Modo, otro, Tema.Pequeno);
            var chapa = new Rect(Margen, panel.yMax + 8f, ancho, 34f);
            Tema.Ficha(chapa);
            Tema.Linea(new Rect(chapa.x + 12f, chapa.y + 7f, 20f, 20f), aPie ? "apie" : "construir", Tema.Ocre);
            Tema.Escribir(new Rect(chapa.x + 40f, chapa.y, anchoModo + 2f, chapa.height), modo, Tema.Seccion);
            Tema.Indicacion(chapa.x + 40f + anchoModo + 14f, chapa.center.y, Boton.Modo, otro, Tema.Pequeno);
            return chapa.yMax;
        }

        /// <summary>Lo que lleva Nicias, en fichas pequeñas bajo su retrato.</summary>
        void Mochila(Bolsa mochila, float y)
        {
            float x = Margen;
            const float alto = 32f;
            for (int i = 0; i < Recursos.Numero; i++)
            {
                var r = (Recurso)i;
                int cantidad = mochila[r];
                if (cantidad <= 0) continue;
                string n = cantidad.ToString();
                float w = 32f + Tema.AnchoDe(n, Tema.Cifra) + 12f;
                if (x + w > Margen + 352f)
                {
                    x = Margen;
                    y += alto + 6f;
                }
                var ficha = new Rect(x, y, w, alto);
                Tema.Ficha(ficha, 0.85f);
                Tema.Icono(new Rect(ficha.x + 5f, ficha.y + 3f, 26f, 26f), r);
                Tema.Escribir(new Rect(ficha.x + 33f, ficha.y, w - 33f, alto), n, Tema.Cifra);
                x += w + 6f;
            }
        }

        // ---------------------------------------------------------------- recursos del almacén

        public static Color ColorConfianza(float confianza)
        {
            return confianza < GestorColonos.UmbralMedia ? Tema.Rojo
                : confianza < GestorColonos.UmbralAlta ? Tema.Ocre : Tema.Verde;
        }

        static readonly Recurso[] Basicos = { Recurso.Madera, Recurso.Mineral, Recurso.Tablones, Recurso.Lingotes, Recurso.Comida };

        /// <summary>Arriba a la derecha: lo que hay en el almacén y cuántos colonos. Devuelve dónde acaba.</summary>
        float BarraRecursos(Mundo mundo)
        {
            Bolsa almacen = mundo.Almacen;
            var lista = new System.Collections.Generic.List<Recurso>(Basicos);
            for (int i = 0; i < Recursos.Numero; i++)
            {
                var r = (Recurso)i;
                if (almacen[r] > 0 && !lista.Contains(r)) lista.Add(r);
            }
            float ancho = 24f;
            foreach (Recurso r in lista) ancho += 30f + Tema.AnchoDe(almacen[r].ToString(), Tema.Cifra) + 14f;
            int colonos = mundo.Colonos.Colonos.Count;
            ancho += 34f + Tema.AnchoDe(colonos.ToString(), Tema.Cifra) + 6f;
            ancho = Mathf.Min(ancho, Tema.Ancho - 400f);

            var barra = new Rect(Tema.Ancho - Margen - ancho, Margen, ancho, 46f);
            Tema.Panel(barra);
            float x = barra.x + 14f;
            foreach (Recurso r in lista)
            {
                string n = almacen[r].ToString();
                float w = Tema.AnchoDe(n, Tema.Cifra);
                if (x + 30f + w > barra.xMax - 50f) break;
                Tema.Icono(new Rect(x, barra.y + 10f, 26f, 26f), r);
                Tema.Escribir(new Rect(x + 30f, barra.y, w + 4f, barra.height), n, Tema.Cifra);
                x += 30f + w + 14f;
            }
            Tema.Relleno(new Rect(barra.xMax - 52f - Tema.AnchoDe(colonos.ToString(), Tema.Cifra), barra.y + 10f, 1f, 26f),
                new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.4f));
            float wc = Tema.AnchoDe(colonos.ToString(), Tema.Cifra);
            Tema.Linea(new Rect(barra.xMax - 14f - wc - 30f, barra.y + 11f, 24f, 24f), "colonos", Tema.Pergamino);
            Tema.Escribir(new Rect(barra.xMax - 12f - wc, barra.y, wc + 4f, barra.height), colonos.ToString(), Tema.Cifra);
            return barra.yMax;
        }

        // ---------------------------------------------------------------- minimapa

        const float MitadMapa = 45f;
        const float TamTerreno = 400f;

        /// <summary>El valle visto desde arriba alrededor de Nicias (o de la cámara del constructor). Devuelve dónde acaba.</summary>
        float Minimapa(Mundo mundo, bool aPie, float y)
        {
            Texture2D mapa = Tema.Imagen("Mapa/valle");
            const float lado = 188f;
            var marco = new Rect(Tema.Ancho - Margen - lado, y, lado, lado);
            Tema.Panel(marco);
            var r = new Rect(marco.x + 7f, marco.y + 7f, lado - 14f, lado - 14f);

            Vector3 centro = jugador != null ? jugador.transform.position : Vector3.zero;
            if (!aPie && camara != null)
            {
                var rayo = new Ray(camara.transform.position, camara.transform.forward);
                if (new Plane(Vector3.up, Vector3.zero).Raycast(rayo, out float d)) centro = rayo.GetPoint(d);
            }
            float x0 = centro.x - MitadMapa;
            float z0 = centro.z - MitadMapa;
            if (mapa != null)
            {
                var coords = new Rect((x0 + TamTerreno * 0.5f) / TamTerreno, (z0 + TamTerreno * 0.5f) / TamTerreno,
                    MitadMapa * 2f / TamTerreno, MitadMapa * 2f / TamTerreno);
                GUI.DrawTextureWithTexCoords(r, mapa, coords);
            }
            else
            {
                Tema.Relleno(r, new Color(0.45f, 0.55f, 0.3f));
            }

            float escala = r.width / (MitadMapa * 2f);
            Vector2 APunto(Vector3 p) => new Vector2(r.x + (p.x - x0) * escala, r.yMax - (p.z - z0) * escala);

            // Caminos y edificios.
            float celda = mundo.Rejilla.TamCelda;
            foreach (Celda c in mundo.Caminos)
            {
                mundo.Rejilla.CentroDeCelda(c, out float cx, out float cz);
                Vector2 p = APunto(new Vector3(cx, 0f, cz));
                var t = new Rect(p.x - celda * escala * 0.5f, p.y - celda * escala * 0.5f, celda * escala, celda * escala);
                if (r.Contains(t.center)) Tema.Relleno(t, new Color(0.9f, 0.84f, 0.7f, 0.85f));
            }
            foreach (EdificioEnMundo e in juego.Edificios)
            {
                if (e == null || e.Sim == null) continue;
                Vector2 p = APunto(e.transform.position);
                var t = new Rect(p.x - e.Definicion.ancho * celda * escala * 0.5f, p.y - e.Definicion.fondo * celda * escala * 0.5f,
                    e.Definicion.ancho * celda * escala, e.Definicion.fondo * celda * escala);
                if (!r.Contains(p)) continue;
                if (e.Sim.Terminado)
                {
                    Tema.Relleno(new Rect(t.x + 1f, t.y + 1.5f, t.width, t.height), new Color(0f, 0f, 0f, 0.35f), 1.5f);
                    Tema.Relleno(t, new Color(0.96f, 0.92f, 0.84f), 1.5f);
                    Tema.Filo(t, new Color(0.72f, 0.3f, 0.18f), 1.5f, 1.5f);
                }
                else
                {
                    Tema.Filo(t, Tema.Ocre, 1.5f, 1.5f);
                }
            }
            foreach (ColonoEnMundo c in juego.Colonos)
            {
                if (c == null) continue;
                Vector2 p = APunto(c.transform.position);
                if (r.Contains(p)) Tema.Relleno(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), Tema.Azul, 3f);
            }

            // La nave.
            Vector2 nave = APunto(PosicionNave);
            if (r.Contains(nave)) Tema.Icono(new Rect(nave.x - 9f, nave.y - 9f, 18f, 18f), Tema.Simbolo.Nave);

            // Nicias: punta de lanza girada hacia donde mira.
            if (jugador != null)
            {
                Vector2 p = APunto(jugador.transform.position);
                if (r.Contains(p))
                {
                    var f = new Rect(p.x - 9f, p.y - 9f, 18f, 18f);
                    Tema.Girado(new Rect(f.x + 1f, f.y + 1.5f, f.width, f.height), "Adornos/flecha", new Color(0f, 0f, 0f, 0.5f), jugador.transform.eulerAngles.y);
                    Tema.Girado(f, "Adornos/flecha", Tema.Pergamino, jugador.transform.eulerAngles.y);
                }
            }

            // Vista de la cámara en el constructor: un rombo.
            if (!aPie)
            {
                Vector2 c = APunto(centro);
                Tema.Filo(new Rect(c.x - 6f, c.y - 6f, 12f, 12f), Tema.Pergamino, 1.5f, 2f);
            }

            // Norte.
            var norte = new Rect(marco.center.x - 11f, marco.y - 9f, 22f, 20f);
            Tema.Relleno(norte, Tema.FondoClaro, 6f);
            Tema.Escribir(norte, "N", new GUIStyle(Tema.Seccion) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            return marco.yMax;
        }

        static readonly Vector3 PosicionNave = new Vector3(-15f, 0f, 14f);

        // ---------------------------------------------------------------- objetivo

        /// <summary>El paso siguiente hacia la nave, con su progreso.</summary>
        string Paso(Mundo mundo, out int hecho, out int total)
        {
            hecho = 0;
            total = 1;
            if (mundo.PrimerTerminado(TipoEdificio.Refugio, null) == null) return "Levanta tu refugio";

            var colonos = mundo.Colonos.Colonos;
            if (colonos.Count > 0)
            {
                int acuerdos = 0;
                foreach (Colono c in colonos)
                {
                    if (c.Acuerdo != null) acuerdos++;
                }
                if (acuerdos == 0)
                {
                    total = colonos.Count;
                    return "Llega a un acuerdo con un colono";
                }
            }
            if (mundo.EdificioAlmacen == null) return "Construye un almacén";

            Nave nave = mundo.Nave;
            if (nave.Terminada)
            {
                if (!nave.VueloDePrueba)
                {
                    total = 1;
                    return "Haz el vuelo de prueba desde la nave";
                }
                hecho = 1;
                return "La nave está lista: despega desde el casco";
            }
            if (nave.Completos >= 1 && !mundo.HayAstillero && !juego.ModoDios) return "Construye un astillero";
            ModuloNave m = nave.Actual;
            if (m.EsperaPrueba)
            {
                total = 1;
                if (m.Prueba == PruebaNave.Rumbo)
                    return mundo.Ciclo.EsDeNoche || juego.ModoDios ? "Fija el rumbo a Calcis desde la nave" : "Espera a la noche para fijar el rumbo a Calcis";
                return "Prueba el motor: encendido en la nave";
            }
            total = 0;
            foreach (Cantidad c in m.Coste)
            {
                total += c.cantidad;
                hecho += c.cantidad - m.Falta(c.recurso);
            }
            if (!m.FaltaMaterial && m.ConfianzaMinima > 0f) return "Gánate la confianza del pueblo para el " + m.Nombre.ToLowerInvariant();
            return "Monta en la nave: " + m.Nombre.ToLowerInvariant();
        }

        void Objetivo(Mundo mundo, float y)
        {
            const float ancho = 300f;
            string paso = Paso(mundo, out int hecho, out int total);
            Tributo tributo = mundo.Tributo;
            float altoPaso = Tema.AltoDe(paso, Tema.Texto, ancho - 70f);
            float alto = 46f + altoPaso + 30f + 40f;
            var panel = new Rect(Tema.Ancho - Margen - ancho, y, ancho, alto);
            Tema.Panel(panel, tributo.ArcontePresente);

            Tema.Linea(new Rect(panel.x + 14f, panel.y + 12f, 22f, 22f), "objetivo", Tema.Ocre);
            Tema.Escribir(new Rect(panel.x + 44f, panel.y + 10f, ancho - 56f, 26f), "Volver a Calcis", Tema.Titulo, Tema.Ocre);

            float py = panel.y + 42f;
            var casilla = new Rect(panel.x + 46f, py + 4f, 12f, 12f);
            Tema.Filo(casilla, Tema.Pergamino, 1.5f, 2f);
            if (total > 0 && hecho >= total) Tema.Relleno(new Rect(casilla.x + 3f, casilla.y + 3f, 6f, 6f), Tema.Verde, 1f);
            Tema.Escribir(new Rect(panel.x + 66f, py, ancho - 80f, altoPaso), paso, Tema.Texto);
            py += altoPaso + 4f;
            if (total > 1)
            {
                Tema.Barra(new Rect(panel.x + 66f, py + 6f, ancho - 130f, 6f), hecho / (float)total, Tema.Ocre);
                Tema.Escribir(new Rect(panel.xMax - 58f, py - 2f, 48f, 20f), hecho + "/" + total, Tema.Pequeno);
            }
            else
            {
                Tema.Escribir(new Rect(panel.x + 66f, py - 2f, 80f, 20f), "(" + hecho + "/" + total + ")", Tema.Pequeno);
            }
            py += 22f;

            // Módulos de la nave y tributo, en una línea.
            Nave nave = mundo.Nave;
            Tema.Icono(new Rect(panel.x + 14f, py + 2f, 22f, 22f), Tema.Simbolo.Nave);
            for (int i = 0; i < nave.Modulos.Length; i++)
            {
                var pip = new Rect(panel.x + 44f + i * 18f, py + 7f, 12f, 12f);
                if (i < nave.Completos) Tema.Relleno(pip, Tema.Ocre, 6f);
                else Tema.Filo(pip, new Color(Tema.Apagado.r, Tema.Apagado.g, Tema.Apagado.b, 0.8f), 1.5f, 6f);
            }
            float xt = panel.x + 150f;
            Tema.Icono(new Rect(xt, py + 2f, 22f, 22f), Tema.Simbolo.Tributo);
            string cuando = tributo.ArcontePresente ? "¡el arconte!" : "día " + tributo.DiaProximo;
            Tema.Escribir(new Rect(xt + 26f, py + 2f, 90f, 20f), cuando, Tema.Pequeno, tributo.ArcontePresente ? Tema.Rojo : Tema.Apagado);
            Tema.Cantidades(panel.x + 44f, py + 36f, tributo.Cuota, 20f, mundo.Almacen, mundo.Mochila);
            Tema.Escribir(new Rect(panel.x + 14f, py + 26f, 30f, 20f), "cuota", new GUIStyle(Tema.Pequeno) { fontSize = 11 });
        }

        // ---------------------------------------------------------------- botones redondos

        void Botones(bool aPie)
        {
            const float lado = 54f;
            const float hueco = 14f;
            var lista = new System.Collections.Generic.List<(string icono, Boton boton, int accion)>
            {
                (aPie ? "construir" : "apie", Boton.Modo, 0),
                ("ayuda", Boton.Ayuda, 1),
                ("menu", Boton.Pausa, 2)
            };
            float x = Tema.Ancho - Margen - lista.Count * lado - (lista.Count - 1) * hueco;
            float y = Tema.Alto - Margen - lado - 22f;
            foreach (var b in lista)
            {
                var r = new Rect(x, y, lado, lado);
                bool encima = !aPie && r.Contains(Event.current.mousePosition);
                Tema.Relleno(new Rect(r.x + 1f, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.3f), lado * 0.5f);
                Tema.Relleno(r, encima ? new Color32(88, 66, 46, 245) : Tema.Fondo, lado * 0.5f);
                Tema.Filo(r, new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.8f), 1.5f, lado * 0.5f);
                Tema.Filo(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f), new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.25f), 1f, lado * 0.5f - 4f);
                Tema.Linea(new Rect(r.x + 14f, r.y + 14f, lado - 28f, lado - 28f), b.icono, Tema.Pergamino);
                float wk = Tema.AnchoTecla(b.boton);
                Tema.DibujarTecla(r.center.x - wk * 0.5f, r.yMax + 12f, b.boton);
                if (!aPie && GUI.Button(r, GUIContent.none, GUIStyle.none) && !control.Bloqueado)
                {
                    if (b.accion == 0) control.Aplicar(ModoJuego.APie);
                    else if (b.accion == 1) ayudaVisible = !ayudaVisible;
                    else if (pausa != null) pausa.Abrir();
                }
                x += lado + hueco;
            }
        }

        // ---------------------------------------------------------------- lo recogido

        struct Obtenido
        {
            public Recurso recurso;
            public int cantidad;
            public float desde;
            public float ultimo;
        }

        readonly System.Collections.Generic.List<Obtenido> obtenidos = new System.Collections.Generic.List<Obtenido>();
        int[] mochilaAntes;

        /// <summary>Mira qué ha entrado en la mochila desde el último frame y lo anuncia a la derecha.</summary>
        void VigilarMochila()
        {
            if (juego == null || juego.Mundo == null) return;
            Bolsa mochila = juego.Mundo.Mochila;
            bool titulo = Narrativa.Instancia != null && Narrativa.Instancia.EnTitulo;
            if (mochilaAntes == null || titulo)
            {
                mochilaAntes = mochila.Volcar();
                return;
            }
            int[] ahora = mochila.Volcar();
            int subida = 0;
            for (int i = 0; i < ahora.Length && i < mochilaAntes.Length; i++)
            {
                if (ahora[i] > mochilaAntes[i]) subida += ahora[i] - mochilaAntes[i];
            }
            // Una subida enorme de golpe es cargar partida, no recoger.
            if (subida > 0 && subida <= 12)
            {
                for (int i = 0; i < ahora.Length && i < mochilaAntes.Length; i++)
                {
                    int n = ahora[i] - mochilaAntes[i];
                    if (n > 0) Anunciar((Recurso)i, n);
                }
            }
            mochilaAntes = ahora;
        }

        void Anunciar(Recurso recurso, int cantidad)
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < obtenidos.Count; i++)
            {
                Obtenido o = obtenidos[i];
                if (o.recurso == recurso && t - o.ultimo < 2f)
                {
                    o.cantidad += cantidad;
                    o.ultimo = t;
                    obtenidos[i] = o;
                    return;
                }
            }
            obtenidos.Add(new Obtenido { recurso = recurso, cantidad = cantidad, desde = t, ultimo = t });
            if (obtenidos.Count > 3) obtenidos.RemoveAt(0);
        }

        void Obtenidos()
        {
            float t = Time.unscaledTime;
            obtenidos.RemoveAll(o => t - o.ultimo > 3.2f);
            const float ancho = 280f;
            const float alto = 70f;
            // Abajo a la derecha, encima de los botones redondos, apilados hacia arriba.
            float y = Tema.Alto - Margen - 54f - 40f - alto;
            for (int k = obtenidos.Count - 1; k >= 0; k--)
            {
                Obtenido o = obtenidos[k];
                float entra = Mathf.SmoothStep(0f, 1f, (t - o.desde) / 0.25f);
                float sale = Mathf.Clamp01((t - o.ultimo - 2.7f) / 0.5f);
                float alfa = entra * (1f - sale);
                var r = new Rect(Tema.Ancho - Margen - ancho + (1f - entra) * 40f + sale * 30f, y, ancho, alto);
                Color previo = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alfa);
                Tema.Panel(r, true);
                // Brillo que barre el filo al llegar.
                float barrido = Mathf.Clamp01((t - o.desde) / 0.6f);
                if (barrido < 1f)
                {
                    Tema.Relleno(new Rect(r.x + 10f + (r.width - 60f) * barrido, r.y + 1f, 40f, 2f), new Color(1f, 0.9f, 0.6f, 0.9f), 1f);
                }
                var caja = new Rect(r.x + 10f, r.y + 9f, 52f, 52f);
                Tema.Relleno(caja, Tema.FondoHueco, 8f);
                Tema.Filo(caja, new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.7f), 1.5f, 8f);
                Tema.Icono(new Rect(caja.x + 7f, caja.y + 7f, 38f, 38f), o.recurso);
                Tema.Escribir(new Rect(r.x + 74f, r.y + 10f, ancho - 84f, 22f), "¡A LA MOCHILA!", Tema.Seccion);
                string nombre = Recursos.Nombre(o.recurso);
                nombre = char.ToUpperInvariant(nombre[0]) + nombre.Substring(1);
                Tema.Escribir(new Rect(r.x + 74f, r.y + 32f, ancho - 84f, 26f), nombre + "  ×" + o.cantidad, Tema.BotonTexto);
                GUI.color = previo;
                y -= alto + 8f;
            }
        }

        // ---------------------------------------------------------------- ayuda

        /// <summary>Recordatorio arriba en el centro de que el modo dios está puesto (F1 o el menú de pausa).</summary>
        void ChipModoDios()
        {
            const string texto = "MODO DIOS";
            float w = Tema.AnchoDe(texto, Tema.Seccion) + 70f;
            var r = new Rect((Tema.Ancho - w) * 0.5f, Margen, w, 30f);
            Tema.Relleno(r, Tema.Fondo, 15f);
            Tema.Filo(r, Tema.Ocre, 1.5f, 15f);
            Tema.Linea(new Rect(r.x + 12f, r.y + 6f, 18f, 18f), "objetivo", Tema.Ocre);
            Tema.Escribir(new Rect(r.x + 36f, r.y + 5f, w - 40f, 22f), texto, Tema.Seccion, Tema.Ocre);
        }

        void Ayuda(bool aPie)
        {
            // Oculta, se vuelve a abrir con su botón redondo (abajo a la derecha) o con su tecla.
            if (!ayudaVisible) return;

            (Boton, string)[] filas = aPie
                ? new[]
                {
                    (Boton.Mover, "moverse"), (Boton.Mirar, "mirar"), (Boton.Saltar, "saltar"), (Boton.Usar, "usar lo que miras"),
                    (Boton.Faena, "faena en un edificio"), (Boton.Guardar, "guardar"), (Boton.Cargar, "cargar"),
                    (Boton.Pausa, "pausa"), (Boton.Ayuda, "ocultar esta ayuda")
                }
                : new[]
                {
                    (Boton.Mover, "desplazar"), (Boton.Rotar, "girar"), (Boton.Zoom, "acercar"), (Boton.Obras, "menú de obras"),
                    (Boton.Cambiar, "cambiar de obra"),
                    (Boton.Colocar, colocador != null && colocador.ModoCamino ? "trazar camino" : "marcar obra"),
                    (Boton.Cancelar, colocador != null && colocador.ModoCamino ? "levantar camino" : "cancelar obra"),
                    (Boton.Tejados, "ver interiores"),
                    (Boton.Ayuda, "ocultar esta ayuda")
                };
            const float filaAlto = 30f;
            float ancho = Tema.AnchoDe("CONTROLES", Tema.Seccion);
            foreach ((Boton b, string t) in filas) ancho = Mathf.Max(ancho, Tema.AnchoIndicacion(b, t, Tema.Pequeno));
            float alto = filas.Length * filaAlto + 44f;
            // En el constructor, encima de la ficha de la obra elegida.
            float abajo = aPie ? 0f : MenuConstruccion.Ocupado.height + 12f;
            var panel = new Rect(Margen, Tema.Alto - Margen - abajo - alto, ancho + 32f, alto);
            Tema.Panel(panel);
            Tema.Escribir(new Rect(panel.x + 16f, panel.y + 9f, 200f, 22f), "CONTROLES", Tema.Seccion);
            for (int i = 0; i < filas.Length; i++)
            {
                Tema.Indicacion(panel.x + 16f, panel.y + 48f + i * filaAlto, filas[i].Item1, filas[i].Item2, Tema.Pequeno);
            }
        }

        // ---------------------------------------------------------------- punto de mira

        void PuntoDeMira()
        {
            float cx = Tema.Ancho * 0.5f;
            float cy = Tema.Alto * 0.5f;
            IInteractuable enfocado = jugador != null ? jugador.Enfocado : null;

            if (enfocado == null)
            {
                Tema.Relleno(new Rect(cx - 3.5f, cy - 3.5f, 7f, 7f), new Color(0f, 0f, 0f, 0.45f), 3.5f);
                Tema.Relleno(new Rect(cx - 2.5f, cy - 2.5f, 5f, 5f), new Color(1f, 1f, 1f, 0.9f), 2.5f);
                return;
            }

            Tema.Filo(new Rect(cx - 12f, cy - 12f, 24f, 24f), new Color(0f, 0f, 0f, 0.35f), 4f, 12f);
            Tema.Filo(new Rect(cx - 11f, cy - 11f, 22f, 22f), Tema.Ocre, 2f, 11f);
            Tema.Relleno(new Rect(cx - 2.5f, cy - 2.5f, 5f, 5f), Tema.Pergamino, 2.5f);

            // Si lo enfocado no está justo en el centro (el juego ayuda a apuntar), se marca dónde está.
            if (camara != null && APantalla(jugador.PuntoEnfocado, out Vector2 p, out float _))
            {
                if ((p - new Vector2(cx, cy)).sqrMagnitude > 40f * 40f)
                {
                    Tema.Escribir(new Rect(p.x - 20f, p.y - 34f, 40f, 24f), "▼", Tema.TextoCentro, Tema.Ocre, true);
                }
            }

            string texto = enfocado.Texto;
            string faena = enfocado is IConFaena conFaena ? conFaena.TextoFaena : null;
            bool hayFaena = !string.IsNullOrEmpty(faena);
            float ancho = Tema.AnchoIndicacion(Boton.Usar, texto, Tema.Texto);
            if (hayFaena) ancho = Mathf.Max(ancho, Tema.AnchoIndicacion(Boton.Faena, faena, Tema.Texto));
            var ficha = new Rect(cx - ancho * 0.5f - 14f, cy + 30f, ancho + 28f, hayFaena ? 76f : 42f);
            Tema.Ficha(ficha, 0.9f);
            Tema.Indicacion(ficha.x + 14f, ficha.y + 21f, Boton.Usar, texto, Tema.Texto);
            if (hayFaena) Tema.Indicacion(ficha.x + 14f, ficha.y + 55f, Boton.Faena, faena, Tema.Texto);
        }

        // ---------------------------------------------------------------- avisos

        void Aviso()
        {
            string aviso = juego.Aviso;
            float ahora = Time.unscaledTime;
            if (!string.IsNullOrEmpty(aviso))
            {
                if (aviso != avisoTexto) avisoDesde = ahora;
                avisoTexto = aviso;
                avisoHasta = ahora;
            }
            if (string.IsNullOrEmpty(avisoTexto)) return;
            float entra = Mathf.Clamp01((ahora - avisoDesde) / 0.2f);
            float alfa = Mathf.Min(entra, 1f - Mathf.Clamp01((ahora - avisoHasta) / 0.35f));
            if (alfa <= 0f)
            {
                avisoTexto = null;
                return;
            }

            float libre = Tema.Ancho - 2f * (Margen + AnchoLateral + 20f);
            float y = Margen;
            if (libre < 340f)
            {
                y = Tema.Alto * 0.2f;
                libre = Tema.Ancho - 40f;
            }
            float ancho = Mathf.Min(620f, libre, Tema.AnchoDe(avisoTexto, Tema.TextoCentro) + 48f);
            float alto = Tema.AltoDe(avisoTexto, Tema.TextoCentro, ancho - 40f) + 28f;
            var panel = new Rect((Tema.Ancho - ancho) * 0.5f, y - (1f - entra) * 10f, ancho, alto);
            Color previo = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alfa);
            Tema.Panel(panel, true);
            Tema.Escribir(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, panel.height - 28f), avisoTexto, Tema.TextoCentro);
            GUI.color = previo;
        }

        // ---------------------------------------------------------------- fichas en el mundo

        bool APantalla(Vector3 mundo, out Vector2 punto, out float distancia)
        {
            Vector3 p = camara.WorldToScreenPoint(mundo);
            punto = new Vector2(p.x / Tema.Escala, (Screen.height - p.y) / Tema.Escala);
            distancia = p.z;
            return p.z > 0f;
        }

        void Etiquetas(bool aPie)
        {
            if (camara == null) return;
            Color previo = GUI.color;
            foreach (EdificioEnMundo e in juego.Edificios)
            {
                if (e == null || e.Sim == null) continue;
                if (!APantalla(e.transform.position + Vector3.up * 4.2f, out Vector2 p, out float distancia)) continue;
                float alfa = aPie ? Mathf.Clamp01((distanciaEtiquetasAPie - distancia) / 8f) : 1f;
                if (alfa <= 0.01f) continue;
                GUI.color = new Color(1f, 1f, 1f, alfa);
                if (!e.Sim.Terminado) FichaObra(e, p);
                else FichaEdificio(e, p);
            }
            foreach (ColonoEnMundo c in juego.Colonos)
            {
                if (c == null || c.Sim == null) continue;
                if (!APantalla(c.transform.position + Vector3.up * 1.8f, out Vector2 p, out float distancia)) continue;
                float alfa = aPie ? Mathf.Clamp01((DistanciaColonos - distancia) / 6f) : 0.9f;
                if (alfa <= 0.01f) continue;
                GUI.color = new Color(1f, 1f, 1f, alfa);
                FichaColono(c, p);
            }
            GUI.color = previo;
        }

        void FichaObra(EdificioEnMundo e, Vector2 p)
        {
            Edificio sim = e.Sim;
            string nombre = e.Definicion.nombre.ToUpperInvariant();
            string estado = e.NombreEstado;

            int n = 0;
            foreach (Cantidad c in sim.CosteActual)
            {
                if (sim.Falta(c.recurso) > 0) n++;
            }
            var falta = new Cantidad[n];
            n = 0;
            foreach (Cantidad c in sim.CosteActual)
            {
                int f = sim.Falta(c.recurso);
                if (f > 0) falta[n++] = new Cantidad(c.recurso, f);
            }

            float wn = Tema.AnchoDe(nombre, Tema.Seccion);
            float anchoFalta = Tema.AnchoCantidades(falta, 22f);
            float ancho = Mathf.Max(wn + 10f + Tema.AnchoDe(estado, Tema.Pequeno), anchoFalta + 44f, 150f) + 24f;
            var r = new Rect(p.x - ancho * 0.5f, p.y - 76f, ancho, 76f);
            Tema.Ficha(r, 0.92f);
            Tema.Filo(r, new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.45f), 1f, 10f);
            Tema.Escribir(new Rect(r.x + 12f, r.y + 6f, wn + 2f, 20f), nombre, Tema.Seccion);
            Tema.Escribir(new Rect(r.x + 12f + wn + 10f, r.y + 6f, 140f, 20f), estado, Tema.Pequeno);

            // Tres tramos de obra: cimientos, esqueleto, casi terminado.
            float tramo = (r.width - 24f - 8f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var t = new Rect(r.x + 12f + i * (tramo + 4f), r.y + 30f, tramo, 5f);
                Color color = i < (int)sim.Estado ? Tema.Ocre : (i == (int)sim.Estado ? Tema.Cobre : Tema.FondoHueco);
                Tema.Relleno(t, color, 2.5f);
            }
            if (n > 0)
            {
                Tema.Escribir(new Rect(r.x + 12f, r.y + 44f, 40f, 20f), "falta", Tema.Pequeno);
                Tema.Cantidades(r.x + 50f, r.y + 55f, falta, 22f);
            }
            else
            {
                Tema.Escribir(new Rect(r.x + 12f, r.y + 44f, r.width - 24f, 20f), "con el material listo", Tema.Pequeno);
            }
        }

        void FichaEdificio(EdificioEnMundo e, Vector2 p)
        {
            string resumen = e.Resumen;
            if (resumen == null) return;
            string nombre = e.Definicion.nombre.ToUpperInvariant();
            const float anchoMaximo = 260f;
            float anchoResumen = 0f;
            foreach (string linea in resumen.Split('\n')) anchoResumen = Mathf.Max(anchoResumen, Tema.AnchoDe(linea, Tema.Pequeno));
            float ancho = Mathf.Min(anchoMaximo, Mathf.Max(anchoResumen, Tema.AnchoDe(nombre, Tema.Seccion))) + 24f;
            float alto = Tema.AltoDe(resumen, Tema.Pequeno, ancho - 24f) + 38f;
            var r = new Rect(p.x - ancho * 0.5f, p.y - alto, ancho, alto);
            Tema.Ficha(r, 0.88f);
            Tema.Escribir(new Rect(r.x + 12f, r.y + 7f, ancho - 24f, 20f), nombre, Tema.Seccion);
            Tema.Escribir(new Rect(r.x + 12f, r.y + 29f, ancho - 24f, alto - 34f), resumen, Tema.Pequeno);
        }

        void FichaColono(ColonoEnMundo c, Vector2 p)
        {
            string nombre = c.Sim.Nombre;
            string actividad = c.Actividad;
            float wn = Tema.AnchoDe(nombre, Tema.Cifra);
            float ancho = Mathf.Max(wn + 60f, Tema.AnchoDe(actividad, Tema.Pequeno)) + 20f;
            var r = new Rect(p.x - ancho * 0.5f, p.y - 50f, ancho, 50f);
            Tema.Ficha(r, 0.8f);
            Tema.Escribir(new Rect(r.x + 10f, r.y + 4f, wn + 2f, 22f), nombre, Tema.Cifra);
            float confianza = c.Sim.Confianza;
            float xBarra = r.x + 18f + wn;
            Tema.Barra(new Rect(xBarra, r.y + 12f, r.xMax - 10f - xBarra, 6f), confianza / 100f, ColorConfianza(confianza));
            Tema.Escribir(new Rect(r.x + 10f, r.y + 26f, ancho - 20f, 20f), actividad, Tema.Pequeno);
        }
    }
}
