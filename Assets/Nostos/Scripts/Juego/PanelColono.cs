using System;
using System.Collections.Generic;
using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Conversación cara a cara con un colono: cumplir su encargo y negociar las condiciones
    /// de trabajo con las cinco palancas. Mientras está abierto, el juego suelta el ratón.
    /// Se maneja con el ratón, con el teclado (flechas, Intro, Esc) o con el mando (cruceta, A, B).
    /// </summary>
    public class PanelColono : MonoBehaviour
    {
        public static PanelColono Instancia { get; private set; }

        static readonly string[] NombresPalanca = { "Jornada", "Raciones", "Reparto", "Seguridad", "Voz" };
        static readonly string[][] Opciones =
        {
            new[] { "Turnos largos", "Jornada normal", "Jornada corta" },
            new[] { "Lo justo", "Suficiente", "Abundante" },
            new[] { "Todo a la nave", "Algo al pueblo", "Reparto justo" },
            new[] { "Sin pausas", "Pausas", "Buenas herramientas" },
            new[] { "Decide Nicias", "Se consulta", "Eligen ellos" }
        };

        public ControlModo control;
        public Juego juego;

        ColonoEnMundo colono;
        Acuerdo propuesta = new Acuerdo();
        Acuerdo contraoferta;
        string respuesta = "";
        bool negociando;
        int abiertoEnFrame;
        int foco;
        int direccionAnterior;

        /// <summary>Un botón de la lista principal o de la fila de abajo de la negociación.</summary>
        struct Opcion
        {
            public string Texto;
            public bool Apagado;
            public Action Accion;

            public Opcion(string texto, Action accion, bool apagado = false)
            {
                Texto = texto;
                Accion = accion;
                Apagado = apagado;
            }
        }

        readonly List<Opcion> opciones = new List<Opcion>();

        public bool Abierto => colono != null;

        void Awake()
        {
            Instancia = this;
        }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        public void Abrir(ColonoEnMundo quien)
        {
            colono = quien;
            negociando = false;
            contraoferta = null;
            propuesta = quien.Sim.Acuerdo != null ? quien.Sim.Acuerdo.Copia() : new Acuerdo();
            respuesta = Textos.Saludo(quien.Sim);
            abiertoEnFrame = Time.frameCount;
            foco = 0;
            direccionAnterior = Tema.DireccionActual();
            control.Bloquear(true);
        }

        public void Cerrar()
        {
            if (colono == null) return;
            colono = null;
            control.Bloquear(false);
        }

        // ---------------------------------------------------------------- opciones

        void PrepararOpciones()
        {
            opciones.Clear();
            Colono sim = colono.Sim;
            GestorColonos gestor = juego.Mundo.Colonos;
            if (!negociando)
            {
                opciones.Add(new Opcion("Charlar un rato", () =>
                {
                    int vez = Narrativa.Instancia != null ? Narrativa.Instancia.SiguienteCharla() : Time.frameCount;
                    respuesta = Textos.Charla(sim, vez);
                    Sonido.Tocar(Efecto.Pagina);
                }));
                if (sim.EncargoPendiente)
                {
                    int llevas = juego.Mundo.Mochila[sim.Encargo.recurso];
                    string texto = "Darle " + sim.Encargo.cantidad + " de " + Recursos.Nombre(sim.Encargo.recurso)
                                   + "   (llevas " + llevas + ")";
                    opciones.Add(new Opcion(texto, () =>
                    {
                        respuesta = gestor.CumplirEncargo(sim, juego.Mundo.Mochila)
                            ? "«Vaya. No esperaba que cumplieras. Gracias.»   +" + GestorColonos.PremioEncargo + " de confianza"
                            : "«Cuando lo tengas, hablamos.»";
                    }, llevas < sim.Encargo.cantidad));
                }
                if (sim.Nivel != NivelConfianza.Baja)
                {
                    opciones.Add(new Opcion(sim.Acuerdo == null ? "Proponer un acuerdo de trabajo" : "Renegociar el acuerdo", () =>
                    {
                        negociando = true;
                        contraoferta = null;
                        foco = 0;
                    }));
                }
                if (sim.Acuerdo != null)
                {
                    opciones.Add(new Opcion("Dar por terminado el acuerdo", () =>
                    {
                        gestor.Romper(sim);
                        respuesta = "«Como quieras. Vuelvo a lo mío.»";
                    }));
                }
                opciones.Add(new Opcion("Despedirse", Cerrar));
                return;
            }

            opciones.Add(new Opcion("Proponer", () => Proponer(sim, gestor)));
            if (contraoferta != null)
            {
                opciones.Add(new Opcion("Aceptar su contraoferta", () =>
                {
                    gestor.Firmar(sim, contraoferta);
                    respuesta = "«Entonces estamos de acuerdo.»";
                    contraoferta = null;
                    VolverDeNegociar();
                }));
            }
            opciones.Add(new Opcion("Dejarlo estar", VolverDeNegociar));
        }

        void Proponer(Colono sim, GestorColonos gestor)
        {
            RespuestaNegociacion r = gestor.Negociar(sim, propuesta, out contraoferta);
            Sonido.Tocar(Efecto.Pagina);
            if (r == RespuestaNegociacion.Acepta)
            {
                bool masDuro = sim.Acuerdo != null && propuesta.Dureza > sim.Acuerdo.Dureza;
                gestor.Firmar(sim, propuesta);
                respuesta = masDuro
                    ? "«Eso no es lo que hablamos... pero de acuerdo.»   Acuerdo endurecido: -5 de confianza"
                    : "«Hecho. Mañana empiezo; ten comida en el almacén y, si puede ser, un techo.»";
                VolverDeNegociar();
            }
            else if (r == RespuestaNegociacion.Contraoferta)
            {
                respuesta = "«Así no. Te propongo otra cosa: mira las condiciones.»";
                propuesta = contraoferta.Copia();
            }
            else
            {
                respuesta = "«No trabajo para gente de Calcis.»";
                VolverDeNegociar();
            }
        }

        void VolverDeNegociar()
        {
            negociando = false;
            contraoferta = null;
            foco = 0;
        }

        /// <summary>En la negociación, los focos 0 a 4 son las palancas y los siguientes, los botones.</summary>
        int NumeroDeFocos => negociando ? Acuerdo.NumPalancas + opciones.Count : opciones.Count;

        // ---------------------------------------------------------------- entrada

        void Update()
        {
            if (colono == null || Time.frameCount == abiertoEnFrame) return;
            PrepararOpciones();

            if (Tema.PulsaVolver())
            {
                if (negociando) VolverDeNegociar();
                else Cerrar();
                return;
            }

            int actual = Tema.DireccionActual();
            int direccion = actual != direccionAnterior ? actual : 0;
            direccionAnterior = actual;
            int total = NumeroDeFocos;
            int primeraFila = negociando ? Acuerdo.NumPalancas : 0;

            if (negociando && foco < Acuerdo.NumPalancas)
            {
                if (direccion == Tema.Izquierda || direccion == Tema.Derecha)
                {
                    int nivel = Mathf.Clamp(propuesta.Niveles[foco] + (direccion == Tema.Derecha ? 1 : -1), 0, 2);
                    if (nivel != propuesta.Niveles[foco])
                    {
                        propuesta.Niveles[foco] = nivel;
                        contraoferta = null;
                        Sonido.Tocar(Efecto.Paso, 0.3f, true);
                    }
                }
                else if (direccion == Tema.Arriba) foco = Mathf.Max(0, foco - 1);
                else if (direccion == Tema.Abajo) foco = Mathf.Min(total - 1, foco + 1);
                // Aceptar desde una palanca salta al botón de proponer.
                if (Tema.PulsaAceptar()) foco = primeraFila;
                return;
            }

            if (negociando)
            {
                // Fila de botones: izquierda y derecha entre ellos, arriba vuelve a las palancas.
                if (direccion == Tema.Izquierda) foco = Mathf.Max(primeraFila, foco - 1);
                else if (direccion == Tema.Derecha) foco = Mathf.Min(total - 1, foco + 1);
                else if (direccion == Tema.Arriba) foco = Acuerdo.NumPalancas - 1;
            }
            else if (total > 0)
            {
                if (direccion == Tema.Arriba) foco = (foco + total - 1) % total;
                else if (direccion == Tema.Abajo) foco = (foco + 1) % total;
            }
            foco = Mathf.Clamp(foco, 0, Mathf.Max(0, total - 1));

            if (Tema.PulsaAceptar())
            {
                int i = foco - primeraFila;
                if (i >= 0 && i < opciones.Count && !opciones[i].Apagado) opciones[i].Accion();
            }
        }

        static string NombreNivel(NivelConfianza nivel)
        {
            switch (nivel)
            {
                case NivelConfianza.Baja: return "baja";
                case NivelConfianza.Media: return "media";
                default: return "alta";
            }
        }

        // ---------------------------------------------------------------- dibujo

        void OnGUI()
        {
            if (colono == null) return;
            GUI.depth = -5;
            Tema.Empezar();
            PrepararOpciones();
            foco = Mathf.Clamp(foco, 0, Mathf.Max(0, NumeroDeFocos - 1));

            Colono sim = colono.Sim;
            GestorColonos gestor = juego.Mundo.Colonos;
            float ancho = Mathf.Min(780f, Tema.Ancho - 32f);
            float interior = ancho - 56f;

            // Primero se mide todo para saber el alto del panel.
            float altoRespuesta = Mathf.Max(54f, Tema.AltoDe(respuesta, Tema.Texto, interior - 36f) + 26f);
            float altoCuerpo;
            string aviso = null;
            if (!negociando)
            {
                if (sim.Nivel == NivelConfianza.Baja)
                {
                    aviso = "Con tan poca confianza no acepta trabajar contigo. Cumple sus encargos para ganártela (hacen falta "
                            + GestorColonos.UmbralMedia + ").";
                }
                altoCuerpo = opciones.Count * 48f + (aviso != null ? Tema.AltoDe(aviso, Tema.Pequeno, interior) + 10f : 0f);
            }
            else
            {
                altoCuerpo = Acuerdo.NumPalancas * 46f + 124f + 48f;
            }
            float alto = 22f + 34f + 30f + 22f + altoRespuesta + 16f + altoCuerpo + 48f;
            alto = Mathf.Min(alto, Tema.Alto - 24f);
            var panel = new Rect((Tema.Ancho - ancho) * 0.5f, (Tema.Alto - alto) * 0.5f, ancho, alto);
            Tema.Relleno(new Rect(0f, 0f, Tema.Ancho, Tema.Alto), new Color(0f, 0f, 0f, 0.35f));
            Tema.Panel(panel, true);

            float x = panel.x + 28f;
            float y = panel.y + 22f;

            // Cabecera: nombre y oficio, y su confianza a la derecha.
            float wn = Tema.AnchoDe(sim.Nombre, Tema.Titulo);
            Tema.Escribir(new Rect(x, y, wn + 4f, 30f), sim.Nombre, Tema.Titulo);
            Tema.Escribir(new Rect(x + wn + 12f, y + 6f, 260f, 22f), sim.Rol, Tema.Pequeno);
            float xConfianza = panel.xMax - 28f - 250f;
            Tema.Icono(new Rect(xConfianza, y, 26f, 26f), Tema.Simbolo.Confianza);
            Tema.Barra(new Rect(xConfianza + 34f, y + 10f, 130f, 7f), sim.Confianza / 100f, HudPrototipo.ColorConfianza(sim.Confianza),
                GestorColonos.UmbralMedia / 100f, GestorColonos.UmbralAlta / 100f);
            string cifra = Mathf.RoundToInt(sim.Confianza) + " · " + NombreNivel(sim.Nivel);
            Tema.Escribir(new Rect(xConfianza + 174f, y + 3f, 80f, 22f), cifra, Tema.Pequeno);
            y += 34f;

            // Chapas de estado.
            float xc = x;
            xc += Chapa(xc, y, sim.Acuerdo != null ? "trabaja contigo" : "sin acuerdo", sim.Acuerdo != null ? Tema.Verde : Tema.Apagado);
            if (sim.Casa != null) xc += Chapa(xc, y, "con casa", Tema.Azul);
            if (sim.Hambriento) xc += Chapa(xc, y, "con hambre", Tema.Rojo);
            if (sim.EncargoPendiente) Chapa(xc, y, "tiene un encargo", Tema.Ocre);
            y += 30f;

            Tema.Meandro(new Rect(x, y + 2f, interior, 12f), new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.35f));
            y += 22f;

            // Lo que dice.
            var bocadillo = new Rect(x, y, interior, altoRespuesta);
            Tema.Relleno(bocadillo, Tema.FondoHueco, 8f);
            Tema.Relleno(new Rect(bocadillo.x, bocadillo.y + 8f, 3f, bocadillo.height - 16f), Tema.Ocre, 1.5f);
            Tema.Escribir(new Rect(bocadillo.x + 18f, bocadillo.y + 13f, bocadillo.width - 36f, bocadillo.height - 20f), respuesta, Tema.Texto);
            y += altoRespuesta + 16f;

            if (!negociando)
            {
                if (aviso != null)
                {
                    float h = Tema.AltoDe(aviso, Tema.Pequeno, interior);
                    Tema.Escribir(new Rect(x, y, interior, h), aviso, Tema.Pequeno);
                    y += h + 10f;
                }
                for (int i = 0; i < opciones.Count; i++)
                {
                    var r = new Rect(x, y, interior, 40f);
                    if (Tema.BotonPanel(r, opciones[i].Texto, i == foco, false, opciones[i].Apagado))
                    {
                        foco = i;
                        opciones[i].Accion();
                    }
                    y += 48f;
                }
                Tema.Indicaciones(panel.center.x, panel.yMax - 24f, Tema.Pequeno,
                    (Boton.Elegir, "elegir"), (Boton.Accion, "aceptar"), (Boton.Salir, "despedirse"));
            }
            else
            {
                DibujarNegociacion(sim, gestor, x, y, interior);
                Tema.Indicaciones(panel.center.x, panel.yMax - 24f, Tema.Pequeno,
                    (Boton.Elegir, "palanca"), (Boton.Ajustar, "cambiar"), (Boton.Accion, "proponer"), (Boton.Salir, "dejarlo"));
            }
        }

        static float Chapa(float x, float y, string texto, Color color)
        {
            float w = Tema.AnchoDe(texto, Tema.Pequeno) + 28f;
            var r = new Rect(x, y, w, 22f);
            Tema.Relleno(r, new Color(color.r, color.g, color.b, 0.18f), 11f);
            Tema.Filo(r, new Color(color.r, color.g, color.b, 0.6f), 1f, 11f);
            Tema.Relleno(new Rect(r.x + 9f, r.center.y - 3f, 6f, 6f), color, 3f);
            Tema.Escribir(new Rect(r.x + 20f, r.y + 2f, w, 20f), texto, Tema.Pequeno, Tema.Pergamino);
            return w + 8f;
        }

        void DibujarNegociacion(Colono sim, GestorColonos gestor, float x, float y, float ancho)
        {
            const float anchoNombre = 120f;
            float anchoOpcion = (ancho - anchoNombre - 16f) / 3f;
            for (int i = 0; i < Acuerdo.NumPalancas; i++)
            {
                var fila = new Rect(x - 8f, y - 3f, ancho + 16f, 44f);
                if (foco == i) Tema.Relleno(fila, new Color(1f, 1f, 1f, 0.05f), 8f);
                Tema.Escribir(new Rect(x, y, anchoNombre, 38f), NombresPalanca[i].ToUpperInvariant(), Tema.Seccion,
                    foco == i ? Tema.Ocre : Tema.Apagado);
                for (int o = 0; o < 3; o++)
                {
                    var r = new Rect(x + anchoNombre + o * (anchoOpcion + 8f), y, anchoOpcion, 38f);
                    bool elegido = propuesta.Niveles[i] == o;
                    if (Tema.BotonPanel(r, Opciones[i][o], foco == i && elegido, elegido, false, TextAnchor.MiddleCenter) && !elegido)
                    {
                        propuesta.Niveles[i] = o;
                        contraoferta = null;
                        foco = i;
                    }
                }
                y += 46f;
            }

            // Lo que supone el acuerdo.
            y += 6f;
            int maxima = gestor.DurezaMaxima(sim);
            Tema.Escribir(new Rect(x, y, anchoNombre, 22f), "DUREZA", Tema.Seccion);
            bool pasa = propuesta.Dureza > maxima;
            Tema.Barra(new Rect(x + anchoNombre, y + 8f, 220f, 8f), propuesta.Dureza / 10f, pasa ? Tema.Rojo : Tema.Ocre, maxima / 10f);
            Tema.Escribir(new Rect(x + anchoNombre + 232f, y + 1f, ancho - anchoNombre - 232f, 22f),
                propuesta.Dureza + " de 10 · acepta hasta " + maxima, Tema.Pequeno, pasa ? Tema.Rojo : Tema.Apagado);
            y += 28f;

            propuesta.HorasDeTrabajo(out float inicio, out float fin);
            string deriva = (propuesta.DerivaDiaria >= 0f ? "+" : "") + propuesta.DerivaDiaria.ToString("0.#");
            Tema.Escribir(new Rect(x, y, ancho, 22f),
                "Rinde un " + Mathf.RoundToInt(propuesta.Productividad * 100f) + " %  ·  trabaja de " + inicio + " a " + fin
                + " h  ·  confianza " + deriva + " al día", Tema.Texto);
            y += 28f;
            string explicacion = "Reparto: cada día se queda en el pueblo algo del almacén a cambio de confianza. "
                                 + "Sin pausas hay un accidente cada " + GestorColonos.DiasEntreAccidentesSinPausas
                                 + " días; las buenas herramientas gastan una herramienta del almacén cada 3 (o un lingote si no hay). "
                                 + "Si eligen ellos, la confianza sube más y hay asamblea cada " + GestorColonos.DiasEntreAsambleas + " días.";
            Tema.Escribir(new Rect(x, y, ancho, 56f), explicacion, Tema.Pequeno);
            y += 62f;

            // Botones de abajo.
            float anchoBoton = (ancho - (opciones.Count - 1) * 10f) / opciones.Count;
            for (int i = 0; i < opciones.Count; i++)
            {
                var r = new Rect(x + i * (anchoBoton + 10f), y, anchoBoton, 40f);
                if (Tema.BotonPanel(r, opciones[i].Texto, foco == Acuerdo.NumPalancas + i, false, false, TextAnchor.MiddleCenter))
                {
                    foco = Acuerdo.NumPalancas + i;
                    opciones[i].Accion();
                }
                // "Proponer" se distingue aunque no esté enfocado: es lo que se suele querer.
                if (i == 0 && foco != Acuerdo.NumPalancas)
                {
                    Tema.Filo(r, new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.7f), 1.5f, 8f);
                }
            }
        }
    }
}
