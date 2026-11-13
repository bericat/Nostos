using System;
using System.Collections.Generic;

namespace Nostos.Sim
{
    /// <summary>Hora del día. Avanza con los ticks; dormir salta a la mañana siguiente.</summary>
    public sealed class CicloDia
    {
        public const float HoraDespertar = 7f;
        public const float HoraSueno = 18f;

        public float Hora { get; private set; } = 8f;
        public int Dia { get; private set; } = 1;
        public float HorasPorTick { get; }

        /// <param name="ticksPorDia">Cuántos ticks dura un día completo.</param>
        public CicloDia(int ticksPorDia)
        {
            if (ticksPorDia <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPorDia));
            HorasPorTick = 24f / ticksPorDia;
        }

        public bool EsDeNoche => Hora >= 20f || Hora < 6f;
        public bool PuedeDormir => Hora >= HoraSueno || Hora < 6f;

        public void Fijar(float hora, int dia)
        {
            Hora = Math.Max(0f, Math.Min(23.99f, hora));
            Dia = Math.Max(1, dia);
        }

        public void Tick()
        {
            Hora += HorasPorTick;
            if (Hora >= 24f)
            {
                Hora -= 24f;
                Dia++;
            }
        }

        /// <summary>Salta a la mañana siguiente. Devuelve false si todavía es de día.</summary>
        public bool Dormir()
        {
            if (!PuedeDormir) return false;
            if (Hora >= HoraSueno) Dia++;
            Hora = HoraDespertar;
            return true;
        }
    }

    /// <summary>La prueba que hay que pasar en la nave, con el material ya montado, para dar un módulo por bueno.</summary>
    public enum PruebaNave
    {
        Ninguna,
        /// <summary>Motor: encender los tres cilindros a su presión.</summary>
        Encendido,
        /// <summary>Navegación: de noche, fijar el rumbo mirando a Calcis.</summary>
        Rumbo
    }

    /// <summary>Uno de los cinco módulos de la nave.</summary>
    public sealed class ModuloNave
    {
        public readonly string Nombre;
        public readonly Cantidad[] Coste;
        /// <summary>Confianza global mínima para poder terminarlo (solo el último la exige).</summary>
        public readonly float ConfianzaMinima;
        public readonly PruebaNave Prueba;
        public readonly int[] Entregado = new int[Recursos.Numero];
        public bool Completo;
        /// <summary>Si ya se ha pasado su prueba (los que no tienen prueba no lo usan).</summary>
        public bool Probado;

        public ModuloNave(string nombre, float confianzaMinima, params Cantidad[] coste)
            : this(nombre, confianzaMinima, PruebaNave.Ninguna, coste)
        {
        }

        public ModuloNave(string nombre, float confianzaMinima, PruebaNave prueba, params Cantidad[] coste)
        {
            Nombre = nombre;
            ConfianzaMinima = confianzaMinima;
            Prueba = prueba;
            Coste = coste;
        }

        /// <summary>El material está montado y solo falta pasar la prueba.</summary>
        public bool EsperaPrueba => !Completo && Prueba != PruebaNave.Ninguna && !Probado && !FaltaMaterial;

        public int Falta(Recurso recurso)
        {
            foreach (Cantidad c in Coste)
            {
                if (c.recurso == recurso) return Math.Max(0, c.cantidad - Entregado[(int)recurso]);
            }
            return 0;
        }

        public bool FaltaMaterial
        {
            get
            {
                foreach (Cantidad c in Coste)
                {
                    if (Falta(c.recurso) > 0) return true;
                }
                return false;
            }
        }

        public string DescribirFalta()
        {
            string texto = "";
            foreach (Cantidad c in Coste)
            {
                int falta = Falta(c.recurso);
                if (falta <= 0) continue;
                if (texto.Length > 0) texto += ", ";
                texto += falta + " de " + Recursos.Nombre(c.recurso);
            }
            return texto;
        }
    }

    /// <summary>
    /// La nave de vuelta: cinco módulos que se montan en orden, a mano, sobre el casco.
    /// El último exige además confianza alta en el asentamiento.
    /// </summary>
    public sealed class Nave
    {
        public readonly ModuloNave[] Modulos =
        {
            new ModuloNave("Casco y coraza", 0f, new Cantidad(Recurso.Chapa, 6), new Cantidad(Recurso.Tablones, 8)),
            new ModuloNave("Soporte vital", 0f, new Cantidad(Recurso.Agua, 6), new Cantidad(Recurso.Cable, 4), new Cantidad(Recurso.Raciones, 6)),
            new ModuloNave("Motor", 0f, PruebaNave.Encendido, new Cantidad(Recurso.Lingotes, 8), new Cantidad(Recurso.Bobinas, 4)),
            new ModuloNave("Navegación", 0f, PruebaNave.Rumbo, new Cantidad(Recurso.Lentes, 4), new Cantidad(Recurso.Cable, 4)),
            new ModuloNave("Combustible", GestorColonos.UmbralAlta, new Cantidad(Recurso.Nucleos, 4))
        };

        public int Completos
        {
            get
            {
                int total = 0;
                foreach (ModuloNave m in Modulos)
                {
                    if (m.Completo) total++;
                }
                return total;
            }
        }

        public bool Terminada => Completos == Modulos.Length;

        /// <summary>Con la nave terminada, el vuelo de prueba con el piloto automático, antes de irse.</summary>
        public bool VueloDePrueba;

        public bool ListaParaDespegar => Terminada && VueloDePrueba;

        /// <summary>Da por pasada la prueba del módulo actual y lo completa. False si no esperaba ninguna.</summary>
        public bool CompletarPrueba()
        {
            ModuloNave modulo = Actual;
            if (modulo == null || !modulo.EsperaPrueba) return false;
            modulo.Probado = true;
            modulo.Completo = true;
            return true;
        }

        /// <summary>El módulo en el que se está trabajando, o null si la nave está terminada.</summary>
        public ModuloNave Actual
        {
            get
            {
                foreach (ModuloNave m in Modulos)
                {
                    if (!m.Completo) return m;
                }
                return null;
            }
        }

        /// <summary>
        /// Entrega al módulo actual lo que haga falta de la bolsa. `completado` indica si con ello
        /// queda montado; `faltaConfianza`, si el material está pero el asentamiento no confía lo bastante.
        /// </summary>
        public int Entregar(Bolsa desde, float confianzaGlobal, out bool completado, out bool faltaConfianza)
        {
            completado = false;
            faltaConfianza = false;
            ModuloNave modulo = Actual;
            if (modulo == null) return 0;

            int total = 0;
            foreach (Cantidad c in modulo.Coste)
            {
                int movidas = desde.Sacar(c.recurso, modulo.Falta(c.recurso));
                modulo.Entregado[(int)c.recurso] += movidas;
                total += movidas;
            }

            if (!modulo.FaltaMaterial)
            {
                if (confianzaGlobal < modulo.ConfianzaMinima)
                {
                    faltaConfianza = true;
                }
                else if (!modulo.EsperaPrueba)
                {
                    // Con prueba pendiente se queda montado pero sin completar: lo completa CompletarPrueba.
                    modulo.Completo = true;
                    completado = true;
                }
            }
            return total;
        }
    }

    /// <summary>
    /// El tributo de Calcis: cada cierto número de días llega el arconte y exige una cuota, que
    /// sube con cada visita. Pagar da confianza; no pagar se cobra del almacén y de la confianza.
    /// </summary>
    public sealed class Tributo
    {
        public int Intervalo = 4;
        public int DiaProximo;
        public int Visitas;
        public bool ArcontePresente;

        public Tributo()
        {
            DiaProximo = 1 + Intervalo;
        }

        /// <summary>La cuota de la próxima visita: crece un 30 % cada vez.</summary>
        public Cantidad[] Cuota
        {
            get
            {
                float escala = (float)Math.Pow(1.3, Math.Max(0, Visitas - 1));
                if (Visitas == 0)
                {
                    return new[] { new Cantidad(Recurso.Madera, 6), new Cantidad(Recurso.Mineral, 4) };
                }
                return new[]
                {
                    new Cantidad(Recurso.Tablones, (int)Math.Round(6 * escala)),
                    new Cantidad(Recurso.Lingotes, (int)Math.Round(3 * escala)),
                    new Cantidad(Recurso.Raciones, (int)Math.Round(4 * escala))
                };
            }
        }

        /// <summary>¿Hay suficiente entre las dos bolsas (almacén y mochila) para pagar?</summary>
        public bool SePuedePagar(Bolsa a, Bolsa b)
        {
            foreach (Cantidad c in Cuota)
            {
                if (a[c.recurso] + b[c.recurso] < c.cantidad) return false;
            }
            return true;
        }

        public string DescribirFalta(Bolsa a, Bolsa b)
        {
            string texto = "";
            foreach (Cantidad c in Cuota)
            {
                int falta = c.cantidad - a[c.recurso] - b[c.recurso];
                if (falta <= 0) continue;
                if (texto.Length > 0) texto += ", ";
                texto += falta + " de " + Recursos.Nombre(c.recurso);
            }
            return texto;
        }

        /// <summary>Paga la cuota, primero del almacén y luego de la mochila.</summary>
        public bool Pagar(Bolsa almacen, Bolsa mochila)
        {
            if (!ArcontePresente || !SePuedePagar(almacen, mochila)) return false;
            foreach (Cantidad c in Cuota)
            {
                int resto = c.cantidad - almacen.Sacar(c.recurso, c.cantidad);
                mochila.Sacar(c.recurso, resto);
            }
            Cerrar();
            return true;
        }

        /// <summary>Calcis se cobra por su cuenta: se lleva la mitad de lo que haya en el almacén.</summary>
        public void Confiscar(Bolsa almacen)
        {
            for (int i = 0; i < Recursos.Numero; i++)
            {
                var recurso = (Recurso)i;
                almacen.Sacar(recurso, (almacen[recurso] + 1) / 2);
            }
            Cerrar();
        }

        void Cerrar()
        {
            ArcontePresente = false;
            Visitas++;
            DiaProximo += Intervalo;
        }
    }

    /// <summary>
    /// Todo el estado de la partida, en C# puro. La presentación (MonoBehaviours) lo lee
    /// y le pide cambios; el tiempo solo avanza con Tick().
    /// </summary>
    public sealed class Mundo
    {
        readonly List<Edificio> edificios = new List<Edificio>();

        public Rejilla Rejilla { get; }
        public Bolsa Mochila { get; }
        public CicloDia Ciclo { get; }
        public Nave Nave { get; } = new Nave();
        /// <summary>Lo guardado en el almacén. Solo se puede usar si hay un almacén terminado.</summary>
        public Bolsa Almacen { get; } = new Bolsa();
        /// <summary>Hacha, pico y pala de Nicias, con lo que les queda.</summary>
        public Equipo Equipo { get; } = new Equipo();
        public GestorColonos Colonos { get; }
        public Tributo Tributo { get; } = new Tributo();
        /// <summary>Avisos de la simulación para mostrar en pantalla.</summary>
        public event Action<string> Aviso;
        public long Ticks { get; private set; }
        public IReadOnlyList<Edificio> Edificios => edificios;

        public Mundo(float tamCelda, int capacidadMochila, int ticksPorDia)
        {
            Rejilla = new Rejilla(tamCelda);
            Mochila = new Bolsa(capacidadMochila);
            Ciclo = new CicloDia(ticksPorDia);
            Colonos = new GestorColonos(this);
            ultimoDia = Ciclo.Dia;
        }

        int ultimoDia;

        // ---- Caminos ----
        // Celdas de camino trazadas en modo constructor. No ocupan la rejilla: un edificio se
        // puede levantar encima y el camino de debajo desaparece.

        readonly HashSet<Celda> caminos = new HashSet<Celda>();

        public IReadOnlyCollection<Celda> Caminos => caminos;
        public event Action CaminosCambiados;

        public bool EsCamino(Celda celda) => caminos.Contains(celda);

        /// <summary>Traza camino en una celda libre. Devuelve false si ya lo había o hay un edificio.</summary>
        public bool PonerCamino(Celda celda)
        {
            if (!Rejilla.EstaLibre(celda) || !caminos.Add(celda)) return false;
            CaminosCambiados?.Invoke();
            return true;
        }

        public bool QuitarCamino(Celda celda)
        {
            if (!caminos.Remove(celda)) return false;
            CaminosCambiados?.Invoke();
            return true;
        }

        public void BorrarCaminos()
        {
            if (caminos.Count == 0) return;
            caminos.Clear();
            CaminosCambiados?.Invoke();
        }

        void QuitarCaminosEn(Celda origen, int ancho, int fondo)
        {
            bool alguno = false;
            for (int x = 0; x < ancho; x++)
            {
                for (int z = 0; z < fondo; z++)
                {
                    if (caminos.Remove(new Celda(origen.X + x, origen.Z + z))) alguno = true;
                }
            }
            if (alguno) CaminosCambiados?.Invoke();
        }

        // ---- Herramientas ----

        /// <summary>Golpes que aguanta una herramienta antes de romperse al recoger.</summary>
        public const int GolpesPorHerramienta = 12;
        int golpesConHerramienta;

        /// <summary>
        /// Nicias recoge con una herramienta de la mochila. Devuelve true si con este golpe se ha
        /// roto (y se ha quitado de la mochila).
        /// </summary>
        public bool GastarHerramienta()
        {
            if (Mochila[Recurso.Herramientas] <= 0) return false;
            golpesConHerramienta++;
            if (golpesConHerramienta < GolpesPorHerramienta) return false;
            golpesConHerramienta = 0;
            Mochila.Sacar(Recurso.Herramientas, 1);
            return true;
        }

        /// <summary>¿Hay esto entre la mochila y el almacén?</summary>
        public bool AlAlcance(Cantidad[] coste)
        {
            foreach (Cantidad c in coste)
            {
                if (Mochila[c.recurso] + Almacen[c.recurso] < c.cantidad) return false;
            }
            return true;
        }

        /// <summary>Lo paga primero de la mochila y luego del almacén. Devuelve false (sin quitar nada) si no llega.</summary>
        public bool PagarAMano(Cantidad[] coste)
        {
            if (!AlAlcance(coste)) return false;
            foreach (Cantidad c in coste)
            {
                int falta = c.cantidad - Mochila.Sacar(c.recurso, c.cantidad);
                if (falta > 0) Almacen.Sacar(c.recurso, falta);
            }
            return true;
        }

        /// <summary>El primer almacén terminado, o null si aún no hay ninguno.</summary>
        public Edificio EdificioAlmacen => PrimerTerminado(TipoEdificio.Almacen, null);

        public const string IdAstillero = "astillero";
        public const string IdTeleferico = "teleferico";
        public const int TicksPorViajeTeleferico = 6;
        public const int CargaTeleferico = 2;

        /// <summary>Con astillero, los módulos de la nave se montan también con lo que hay en el almacén.</summary>
        public bool HayAstillero => PrimerTerminado(TipoEdificio.Servicio, IdAstillero) != null;
        public bool HayTeleferico => PrimerTerminado(TipoEdificio.Servicio, IdTeleferico) != null;
        /// <summary>Tick del último viaje de vagoneta (para animar la rueda).</summary>
        public long UltimoViajeTeleferico { get; private set; } = -1000;

        /// <summary>
        /// Una vagoneta del teleférico: lleva material del almacén a la primera obra que lo
        /// necesite; si no hay obras, recoge lo producido o repone un taller. Sin manos.
        /// </summary>
        bool ViajeDeTeleferico()
        {
            if (EdificioAlmacen == null) return false;

            foreach (Edificio e in edificios)
            {
                if (e.Terminado) continue;
                foreach (Cantidad c in e.CosteActual)
                {
                    int n = Math.Min(CargaTeleferico, Math.Min(e.Falta(c.recurso), Almacen[c.recurso]));
                    if (n <= 0) continue;
                    e.RecibirEnObra(c.recurso, Almacen.Sacar(c.recurso, n), out bool _);
                    return true;
                }
            }

            foreach (Edificio e in edificios)
            {
                if (!e.Terminado || e.Datos.Receta == null) continue;
                if (e.Salida.Total > 0 && e.Recoger(Almacen) > 0) return true;
                if (e.Entrada.Total == 0 && e.Datos.Receta.Entradas.Length > 0 && e.Depositar(Almacen) > 0) return true;
            }
            return false;
        }

        public Edificio PrimerTerminado(TipoEdificio tipo, string id)
        {
            foreach (Edificio e in edificios)
            {
                if (!e.Terminado || e.Datos.Tipo != tipo) continue;
                if (id == null || e.Datos.Id == id) return e;
            }
            return null;
        }

        /// <summary>Dormir: salta a la mañana y aplica el cambio de día. Devuelve false si aún es de día.</summary>
        public bool Dormir()
        {
            if (!Ciclo.Dormir()) return false;
            ComprobarDia();
            return true;
        }

        void ComprobarDia()
        {
            if (Ciclo.Dia == ultimoDia) return;
            ultimoDia = Ciclo.Dia;
            DiaDeTributo();
            Colonos.NuevoDia();
        }

        void DiaDeTributo()
        {
            if (Tributo.ArcontePresente)
            {
                // El arconte se fue sin cobrar.
                Tributo.Confiscar(Almacen);
                Colonos.CambiarConfianzaDeTodos(-6f);
                Aviso?.Invoke("No se pagó el tributo: los arcontes se llevan la mitad del almacén y Nova Lavinia te lo reprocha.");
            }
            if (Ciclo.Dia == Tributo.DiaProximo)
            {
                Tributo.ArcontePresente = true;
                Aviso?.Invoke("Ha llegado el arconte de Calcis. Exige " + Recursos.Lista(Tributo.Cuota) + " antes de que acabe el día.");
            }
            else if (Ciclo.Dia == Tributo.DiaProximo - 1)
            {
                Aviso?.Invoke("Mañana llega el arconte. La cuota: " + Recursos.Lista(Tributo.Cuota) + ".");
            }
        }

        /// <summary>Pagar el tributo ayuda a Nova Lavinia: sube la confianza de todos.</summary>
        public bool PagarTributo()
        {
            if (!Tributo.Pagar(Almacen, Mochila)) return false;
            Colonos.CambiarConfianzaDeTodos(8f);
            return true;
        }

        /// <summary>Tras cargar una partida: el día guardado no cuenta como día nuevo.</summary>
        public void Sincronizar()
        {
            ultimoDia = Ciclo.Dia;
        }

        public bool PuedeColocar(DatosEdificio datos, Celda origen)
        {
            return Rejilla.AreaLibre(origen, datos.Ancho, datos.Fondo);
        }

        /// <summary>Coloca la obra de un edificio. Devuelve null si el solar no está libre.</summary>
        public Edificio Colocar(DatosEdificio datos, Celda origen)
        {
            int id = Rejilla.OcuparArea(origen, datos.Ancho, datos.Fondo);
            if (id == 0) return null;
            QuitarCaminosEn(origen, datos.Ancho, datos.Fondo);
            var edificio = new Edificio(id, datos, origen);
            edificios.Add(edificio);
            return edificio;
        }

        public bool Quitar(Edificio edificio)
        {
            if (!edificios.Remove(edificio)) return false;
            Colonos.EdificioQuitado(edificio);
            Rejilla.LiberarId(edificio.Id);
            return true;
        }

        public Edificio EnCelda(Celda celda)
        {
            if (!Rejilla.Ocupante(celda, out int id)) return null;
            foreach (Edificio e in edificios)
            {
                if (e.Id == id) return e;
            }
            return null;
        }

        public void Tick()
        {
            Ticks++;
            Ciclo.Tick();
            foreach (Edificio e in edificios) e.Tick();
            if (Ticks % TicksPorViajeTeleferico == 0 && HayTeleferico && ViajeDeTeleferico()) UltimoViajeTeleferico = Ticks;
            ComprobarDia();
            Colonos.Tick();
        }
    }
}
