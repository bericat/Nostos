using Nostos.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>Algo del mundo que, además de usarse, admite una faena a mano (F en el teclado, B en el mando).</summary>
    public interface IConFaena
    {
        /// <summary>Qué faena se puede hacer ahora, o null si ninguna.</summary>
        string TextoFaena { get; }

        void Faena(ControlModo control);
    }

    public enum TipoFaena
    {
        Ninguna,
        Sierra,
        Colada,
        Circuito,
        Lentes,
        Guiso,
        Manivela,
        Confort,
        Martillo
    }

    /// <summary>
    /// Lo que Nicias hace con sus manos dentro de cada edificio (pilar 2 de la hoja de ruta:
    /// entrar debe dar una mejora, no solo vistas). Cada faena es un juego breve; si sale bien,
    /// el edificio rinde más durante un rato. Si sale mal no se pierde nada y se puede repetir.
    /// </summary>
    public class Faenas : MonoBehaviour
    {
        public const int TicksSierra = 300;
        public const int TicksLentes = 400;
        public const int LotesColada = 3;
        public const int LotesCircuito = 3;
        public const int RacionesGuiso = 2;
        public const float ConfianzaGuiso = 2f;
        public static readonly Cantidad[] CosteConfort = { new Cantidad(Recurso.Tablones, 3), new Cantidad(Recurso.Cobre, 1) };

        const int Arriba = 1, Derecha = 2, Abajo = 3, Izquierda = 4;
        static readonly string[] Flechas = { "", "↑", "→", "↓", "←" };
        static readonly string[] NombresDireccion = { "", "arriba", "derecha", "abajo", "izquierda" };
        static readonly float[] AnchoSierra = { 0.24f, 0.17f, 0.11f };
        static readonly float[] VelocidadSierra = { 2.4f, 3.0f, 3.7f };
        const float FranjaColadaMin = 0.6f;
        const float FranjaColadaMax = 0.78f;
        const float SegundosColada = 3f;
        public const int HerramientasMartillo = 2;
        const int GolpesMartillo = 4;
        const float ZonaMartilloMin = 0.72f;
        const float ZonaMartilloMax = 0.86f;

        static Faenas instancia;

        /// <summary>Se crea sola la primera vez que hace falta: no necesita estar en la escena.</summary>
        public static Faenas Instancia
        {
            get
            {
                if (instancia == null) instancia = new GameObject("Faenas").AddComponent<Faenas>();
                return instancia;
            }
        }

        public static bool Abierta => instancia != null && instancia.edificio != null;

        ControlModo control;
        EdificioEnMundo edificio;
        TipoFaena tipo;
        int abiertaEnFrame;
        static int cerradaEnFrame = -1;
        float tiempo;
        int fallos;
        int direccionAnterior;

        // Sierra
        int etapa;
        float centro;
        float aguja;
        // Colada
        float temperatura;
        float enFranja;
        // Circuito
        int[] secuencia;
        int acertados;
        float mostrarHasta;
        // Lentes
        readonly int[] objetivo = new int[2];
        readonly int[] posicion = new int[2];
        int anillo;
        // Martillo: primero se elige qué forjar
        int golpes;
        float fase;
        bool eligiendo;
        int eleccion;
        // Guiso y manivela
        float nivel;
        int esperado;

        GUIStyle grande;

        void OnDestroy()
        {
            if (instancia == this) instancia = null;
        }

        // ---------------------------------------------------------------- qué faena hay en cada edificio

        public static TipoFaena TipoDe(Edificio e)
        {
            if (e == null || !e.Terminado) return TipoFaena.Ninguna;
            switch (e.Datos.Id)
            {
                case "aserradero": return TipoFaena.Sierra;
                case "fundicion": return TipoFaena.Colada;
                case "taller": return TipoFaena.Circuito;
                case "optica": return TipoFaena.Lentes;
                case "comedor": return TipoFaena.Guiso;
                case "pozo": return TipoFaena.Manivela;
                case "banco": return TipoFaena.Martillo;
                case "herreria": return TipoFaena.Martillo;
            }
            return e.Datos.Tipo == TipoEdificio.Casa ? TipoFaena.Confort : TipoFaena.Ninguna;
        }

        public static string Texto(Juego juego, Edificio e)
        {
            switch (TipoDe(e))
            {
                case TipoFaena.Sierra:
                    return e.TicksAfinado > 0 ? "Ajustar la sierra (ya corta fino)" : "Ajustar la sierra";
                case TipoFaena.Colada:
                    return "Vigilar el horno y hacer la colada";
                case TipoFaena.Circuito:
                    return "Montar circuitos";
                case TipoFaena.Lentes:
                    return e.TicksAfinado > 0 ? "Calibrar las lentes (ya están calibradas)" : "Calibrar las lentes";
                case TipoFaena.Guiso:
                    return e.DiaUltimaFaena == juego.Mundo.Ciclo.Dia
                        ? "Guiso: hoy ya has cocinado"
                        : "Cocinar un guiso para el pueblo (1 de comida)";
                case TipoFaena.Manivela:
                    return "Subir un cubo con la manivela";
                case TipoFaena.Martillo:
                    return "Forjar herramientas";
                case TipoFaena.Confort:
                    return e.Confort >= Edificio.ConfortMaximo
                        ? "La casa ya está todo lo cómoda que puede"
                        : "Mejorar la casa (" + Recursos.Lista(CosteConfort) + ")";
                default:
                    return null;
            }
        }

        // ---------------------------------------------------------------- abrir y cerrar

        public void Comenzar(EdificioEnMundo quien, ControlModo modo)
        {
            if (Abierta || quien == null || quien.Sim == null || modo == null) return;
            // La misma pulsación que cierra una faena no debe volver a abrirla.
            if (Time.frameCount == cerradaEnFrame) return;
            Juego juego = Juego.Instancia;
            if (juego == null) return;
            Edificio e = quien.Sim;
            TipoFaena nuevo = TipoDe(e);
            if (nuevo == TipoFaena.Ninguna) return;

            if (nuevo == TipoFaena.Confort)
            {
                MejorarCasa(juego, e);
                return;
            }
            if (nuevo == TipoFaena.Guiso)
            {
                if (e.DiaUltimaFaena == juego.Mundo.Ciclo.Dia)
                {
                    juego.Avisar("Hoy ya has cocinado. Mañana más.");
                    return;
                }
                if (juego.Mundo.Mochila[Recurso.Comida] + juego.Mundo.Almacen[Recurso.Comida] < 1)
                {
                    juego.Avisar("Para el guiso hace falta 1 de comida, en la mochila o en el almacén.");
                    return;
                }
            }

            control = modo;
            edificio = quien;
            tipo = nuevo;
            abiertaEnFrame = Time.frameCount;
            tiempo = 0f;
            fallos = 0;
            direccionAnterior = DireccionActual();
            nivel = 0f;
            esperado = Izquierda;
            switch (tipo)
            {
                case TipoFaena.Sierra:
                    etapa = 0;
                    centro = Random.Range(0.2f, 0.8f);
                    break;
                case TipoFaena.Colada:
                    temperatura = 0.2f;
                    enFranja = 0f;
                    break;
                case TipoFaena.Circuito:
                    NuevaSecuencia();
                    break;
                case TipoFaena.Martillo:
                    golpes = 0;
                    fase = 0f;
                    eligiendo = true;
                    eleccion = PrimeraQueFalta(juego.Mundo);
                    break;
                case TipoFaena.Lentes:
                    anillo = 0;
                    for (int i = 0; i < 2; i++)
                    {
                        objetivo[i] = Random.Range(0, 8);
                        posicion[i] = (objetivo[i] + Random.Range(2, 7)) % 8;
                    }
                    break;
            }
            control.Bloquear(true);
        }

        void Cerrar()
        {
            if (edificio == null) return;
            edificio = null;
            tipo = TipoFaena.Ninguna;
            cerradaEnFrame = Time.frameCount;
            if (control != null) control.Bloquear(false);
        }

        void Exito()
        {
            Juego juego = Juego.Instancia;
            Edificio e = edificio != null ? edificio.Sim : null;
            if (juego == null || e == null)
            {
                Cerrar();
                return;
            }

            string mensaje = null;
            switch (tipo)
            {
                case TipoFaena.Sierra:
                    e.Afinar(TicksSierra);
                    mensaje = "La sierra corta limpia: el aserradero trabaja al doble de rápido un buen rato.";
                    break;
                case TipoFaena.Colada:
                    e.AnadirLotesDobles(LotesColada);
                    mensaje = "Colada limpia: las próximas " + LotesColada + " tandas de la fundición salen dobles.";
                    break;
                case TipoFaena.Circuito:
                    e.AnadirLotesDobles(LotesCircuito);
                    mensaje = "Circuitos bien montados: las próximas " + LotesCircuito + " tandas del taller salen dobles.";
                    break;
                case TipoFaena.Lentes:
                    e.Afinar(TicksLentes);
                    mensaje = "Lentes calibradas: la óptica trabaja al doble de rápido un buen rato.";
                    break;
                case TipoFaena.Guiso:
                    Mundo mundo = juego.Mundo;
                    if (mundo.Mochila.Sacar(Recurso.Comida, 1) == 0 && mundo.Almacen.Sacar(Recurso.Comida, 1) == 0)
                    {
                        mensaje = "Se te ha acabado la comida antes de terminar el guiso.";
                        break;
                    }
                    e.DiaUltimaFaena = mundo.Ciclo.Dia;
                    e.Salida.Meter(Recurso.Raciones, RacionesGuiso);
                    mundo.Colonos.CambiarConfianzaDeTodos(ConfianzaGuiso);
                    mensaje = "El olor del guiso reúne a medio pueblo y te lo agradecen. Quedan "
                              + RacionesGuiso + " raciones en el comedor.";
                    break;
                case TipoFaena.Manivela:
                    if (juego.Mundo.Mochila.Meter(Recurso.Agua, 1) == 0) e.Salida.Meter(Recurso.Agua, 1);
                    mensaje = "Subes un cubo lleno: 1 de agua.";
                    break;
                case TipoFaena.Martillo:
                {
                    Mundo m = juego.Mundo;
                    if (eleccion < Equipo.Numero)
                    {
                        var h = (Herramienta)eleccion;
                        if (!m.PagarAMano(Equipo.Coste(h)))
                        {
                            mensaje = "Te falta material: " + Recursos.Lista(Equipo.Coste(h)) + ".";
                            break;
                        }
                        m.Equipo.Dar(h);
                        mensaje = MensajeForja(h);
                        break;
                    }
                    if (m.Mochila.Sacar(Recurso.Madera, 1) == 0 && m.Almacen.Sacar(Recurso.Madera, 1) == 0)
                    {
                        mensaje = "Te has quedado sin madera para los mangos.";
                        break;
                    }
                    int metidas = m.Mochila.Meter(Recurso.Herramientas, HerramientasMartillo);
                    if (metidas < HerramientasMartillo) e.Salida.Meter(Recurso.Herramientas, HerramientasMartillo - metidas);
                    mensaje = "Cabezas bien templadas y mangos firmes: " + HerramientasMartillo
                              + " herramientas para el pueblo. Los colonos trabajan mejor con ellas.";
                    break;
                }
            }
            Sonido.Tocar(Efecto.Campana, 0.5f);
            if (mensaje != null) juego.Avisar(mensaje, 5f);
            Cerrar();
        }

        void Fracaso(string mensaje)
        {
            if (Juego.Instancia != null) Juego.Instancia.Avisar(mensaje + " Puedes volver a intentarlo.", 4.5f);
            Cerrar();
        }

        static void MejorarCasa(Juego juego, Edificio casa)
        {
            if (casa.Confort >= Edificio.ConfortMaximo)
            {
                juego.Avisar("Esta casa ya está todo lo cómoda que puede.");
                return;
            }
            Bolsa mochila = juego.Mundo.Mochila;
            Bolsa almacen = juego.Mundo.Almacen;
            foreach (Cantidad c in CosteConfort)
            {
                if (mochila[c.recurso] + almacen[c.recurso] < c.cantidad)
                {
                    juego.Avisar("Para mejorar la casa hacen falta " + Recursos.Lista(CosteConfort) + ", en la mochila o en el almacén.");
                    return;
                }
            }
            foreach (Cantidad c in CosteConfort)
            {
                int resto = c.cantidad - mochila.Sacar(c.recurso, c.cantidad);
                almacen.Sacar(c.recurso, resto);
            }
            casa.MejorarConfort();
            Sonido.Tocar(Efecto.Golpe, 0.7f);
            juego.Avisar(casa.Confort == 1
                ? "Arreglas las camas y pones contraventanas nuevas. Quien viva aquí se fiará un poco más de ti cada día."
                : "Una estufa de cobre y un buen colchón: esta casa ya es la mejor de Nova Lavinia.", 5f);
        }

        // ---------------------------------------------------------------- entrada

        static bool Pulsa()
        {
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            return (teclado != null && (teclado.eKey.wasPressedThisFrame || teclado.spaceKey.wasPressedThisFrame
                                        || teclado.enterKey.wasPressedThisFrame))
                   || (mando != null && (mando.buttonSouth.wasPressedThisFrame || mando.buttonWest.wasPressedThisFrame));
        }

        static bool Mantiene()
        {
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            return (teclado != null && (teclado.eKey.isPressed || teclado.spaceKey.isPressed))
                   || (mando != null && (mando.buttonSouth.isPressed || mando.buttonWest.isPressed));
        }

        static bool Sale()
        {
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            return (teclado != null && (teclado.escapeKey.wasPressedThisFrame || teclado.tabKey.wasPressedThisFrame
                                        || teclado.fKey.wasPressedThisFrame))
                   || (mando != null && mando.buttonEast.wasPressedThisFrame);
        }

        static int DireccionActual()
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

        /// <summary>La dirección pulsada en este frame (solo al empezar a pulsarla), o 0.</summary>
        int NuevaDireccion()
        {
            int actual = DireccionActual();
            int nueva = actual != direccionAnterior ? actual : 0;
            direccionAnterior = actual;
            return nueva;
        }

        // ---------------------------------------------------------------- juego

        void Update()
        {
            if (edificio == null) return;
            if (edificio.Sim == null || !edificio.Sim.Terminado)
            {
                Cerrar();
                return;
            }
            if (Time.frameCount == abiertaEnFrame) return;
            if (Sale())
            {
                Cerrar();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            tiempo += dt;
            int direccion = NuevaDireccion();
            switch (tipo)
            {
                case TipoFaena.Sierra: Sierra(); break;
                case TipoFaena.Colada: Colada(dt); break;
                case TipoFaena.Circuito: Circuito(direccion); break;
                case TipoFaena.Lentes: Lentes(direccion); break;
                case TipoFaena.Guiso: Guiso(dt, direccion); break;
                case TipoFaena.Manivela: Manivela(dt); break;
                case TipoFaena.Martillo: Martillo(dt, direccion); break;
            }
        }

        /// <summary>Altura del martillo (0 abajo, 1 arriba). Cada golpe acertado lo hace más rápido.</summary>
        float AlturaMartillo => 0.5f - 0.5f * Mathf.Cos(fase);

        // ---- qué se forja en el banco

        /// <summary>Opciones del banco: las cuatro herramientas de Nicias y, al final, las del pueblo.</summary>
        const int OpcionesBanco = Equipo.Numero + 1;
        static readonly Cantidad[] CostePueblo = { new Cantidad(Recurso.Madera, 1) };

        static Cantidad[] CosteOpcion(int i) => i < Equipo.Numero ? Equipo.Coste((Herramienta)i) : CostePueblo;

        static string NombreOpcion(int i)
        {
            if (i >= Equipo.Numero) return "Herramientas para el pueblo (" + HerramientasMartillo + ")";
            string n = Equipo.Nombre((Herramienta)i);
            return char.ToUpperInvariant(n[0]) + n.Substring(1);
        }

        static string ParaQueOpcion(int i)
        {
            switch (i)
            {
                case 0: return "Talas el doble.";
                case 1: return "Picas el doble de mineral.";
                case 2: return "Sacas el doble de cobre.";
                case 3: return "Mineral triple, cobre doble y dura el doble. Solo en la herrería.";
                default: return "Los colonos las cogen del almacén.";
            }
        }

        bool EnHerreria => edificio != null && edificio.Sim != null && edificio.Sim.Datos.Id == "herreria";

        int PrimeraQueFalta(Mundo mundo)
        {
            for (int i = 0; i < Equipo.Numero; i++)
            {
                if (i == (int)Herramienta.PicoBueno) continue;
                if (!mundo.Equipo.Tiene((Herramienta)i) && mundo.AlAlcance(CosteOpcion(i))) return i;
            }
            return EnHerreria && mundo.AlAlcance(Equipo.Coste(Herramienta.PicoBueno)) && !mundo.Equipo.Tiene(Herramienta.PicoBueno)
                ? (int)Herramienta.PicoBueno
                : 0;
        }

        static string MensajeForja(Herramienta h)
        {
            switch (h)
            {
                case Herramienta.Hacha: return "Un hacha con buen filo: ahora cada golpe a un árbol da el doble de madera.";
                case Herramienta.Pico: return "Un pico recio: cada golpe a una veta da el doble de mineral.";
                case Herramienta.Pala: return "Una pala de hoja ancha: el cobre sale el doble de rápido.";
                default: return "El mejor pico que se ha visto en Nova Lavinia: triple de mineral, doble de cobre, y aguanta el doble.";
            }
        }

        void ElegirForja(int direccion)
        {
            if (direccion == Arriba) eleccion = (eleccion + OpcionesBanco - 1) % OpcionesBanco;
            else if (direccion == Abajo) eleccion = (eleccion + 1) % OpcionesBanco;
            if (!Pulsa()) return;
            Juego juego = Juego.Instancia;
            if (juego == null) return;
            if (eleccion == (int)Herramienta.PicoBueno && !EnHerreria)
            {
                juego.Avisar("El pico bueno pide fragua y yunque: se forja en la herrería.");
                Sonido.Tocar(Efecto.Paso, 0.4f, true);
                return;
            }
            if (!juego.Mundo.AlAlcance(CosteOpcion(eleccion)))
            {
                juego.Avisar("Para " + (eleccion < Equipo.Numero ? Equipo.ConArticulo((Herramienta)eleccion) : "eso")
                             + " hace falta " + Recursos.Lista(CosteOpcion(eleccion)) + ", en la mochila o en el almacén.");
                Sonido.Tocar(Efecto.Paso, 0.4f, true);
                return;
            }
            eligiendo = false;
            golpes = 0;
            fase = 0f;
            Sonido.Tocar(Efecto.Golpe, 0.5f, true);
        }

        void Martillo(float dt, int direccion)
        {
            if (eligiendo)
            {
                ElegirForja(direccion);
                return;
            }
            fase += dt * (2.6f + 0.7f * golpes);
            if (!Pulsa()) return;
            float altura = AlturaMartillo;
            if (altura >= ZonaMartilloMin && altura <= ZonaMartilloMax)
            {
                golpes++;
                Sonido.Tocar(Efecto.Golpe, 0.8f, true);
                if (golpes >= GolpesMartillo)
                {
                    Exito();
                    return;
                }
                // Tras el golpe el martillo vuelve abajo y empieza a subir otra vez.
                fase = 0f;
            }
            else
            {
                fallos++;
                Sonido.Tocar(Efecto.Paso, 0.4f, true);
                if (fallos >= 3) Fracaso("El hierro se ha enfriado y las cabezas salen torcidas.");
            }
        }

        void Sierra()
        {
            aguja = 0.5f + 0.5f * Mathf.Sin(tiempo * VelocidadSierra[etapa]);
            if (!Pulsa()) return;
            if (Mathf.Abs(aguja - centro) <= AnchoSierra[etapa] * 0.5f)
            {
                Sonido.Tocar(Efecto.Golpe, 0.6f, true);
                etapa++;
                if (etapa >= AnchoSierra.Length)
                {
                    Exito();
                    return;
                }
                centro = Random.Range(0.2f, 0.8f);
            }
            else
            {
                fallos++;
                if (fallos >= 3) Fracaso("La hoja sigue torcida.");
            }
        }

        void Colada(float dt)
        {
            temperatura += Mantiene() ? 0.42f * dt : -0.28f * dt;
            temperatura = Mathf.Clamp01(temperatura);
            if (temperatura >= FranjaColadaMin && temperatura <= FranjaColadaMax) enFranja += dt;
            if (temperatura >= 0.99f)
            {
                Fracaso("El horno se ha pasado de calor y la colada sale llena de burbujas.");
                return;
            }
            if (enFranja >= SegundosColada)
            {
                Exito();
                return;
            }
            if (tiempo > 25f) Fracaso("El metal se ha enfriado en el crisol.");
        }

        void NuevaSecuencia()
        {
            secuencia = new int[5];
            for (int i = 0; i < secuencia.Length; i++) secuencia[i] = Random.Range(Arriba, Izquierda + 1);
            acertados = 0;
            mostrarHasta = tiempo + 3f;
        }

        void Circuito(int direccion)
        {
            if (tiempo < mostrarHasta || direccion == 0) return;
            if (direccion == secuencia[acertados])
            {
                Sonido.Tocar(Efecto.Golpe, 0.4f, true);
                acertados++;
                if (acertados >= secuencia.Length) Exito();
                return;
            }
            fallos++;
            if (fallos >= 3)
            {
                Fracaso("Saltan chispas: el circuito no cierra.");
                return;
            }
            NuevaSecuencia();
        }

        static int Distancia(int a, int b)
        {
            int d = Mathf.Abs(a - b) % 8;
            return Mathf.Min(d, 8 - d);
        }

        float Brillo => 1f - (Distancia(posicion[0], objetivo[0]) + Distancia(posicion[1], objetivo[1])) / 8f;

        void Lentes(int direccion)
        {
            if (direccion == Arriba || direccion == Abajo) anillo = 1 - anillo;
            else if (direccion == Derecha) posicion[anillo] = (posicion[anillo] + 1) % 8;
            else if (direccion == Izquierda) posicion[anillo] = (posicion[anillo] + 7) % 8;
            if (direccion != 0) Sonido.Tocar(Efecto.Pagina, 0.3f, true);
            if (posicion[0] == objetivo[0] && posicion[1] == objetivo[1]) Exito();
        }

        void Guiso(float dt, int direccion)
        {
            if (direccion == esperado)
            {
                nivel += 0.09f;
                esperado = esperado == Izquierda ? Derecha : Izquierda;
            }
            nivel = Mathf.Max(0f, nivel - 0.12f * dt);
            if (nivel >= 1f)
            {
                Exito();
                return;
            }
            if (tiempo > 12f) Fracaso("El guiso se ha pegado al fondo de la olla.");
        }

        void Manivela(float dt)
        {
            if (Pulsa())
            {
                nivel += 0.11f;
                Sonido.Tocar(Efecto.Paso, 0.35f, true);
            }
            nivel = Mathf.Max(0f, nivel - 0.2f * dt);
            if (nivel >= 1f)
            {
                Exito();
                return;
            }
            if (tiempo > 15f) Fracaso("El cubo vuelve a caer al fondo del pozo.");
        }

        // ---------------------------------------------------------------- dibujo

        void OnGUI()
        {
            if (edificio == null) return;
            GUI.depth = -5;
            Tema.Empezar();
            if (grande == null) CrearEstilos();

            float ancho = Mathf.Min(720f, Tema.Ancho - 32f);
            const float alto = 330f;
            var panel = new Rect((Tema.Ancho - ancho) * 0.5f, Mathf.Max(16f, Tema.Alto * 0.5f - 60f), ancho, alto);
            Tema.Panel(panel, true);
            var dentro = new Rect(panel.x + 28f, panel.y + 20f, panel.width - 56f, panel.height - 40f);

            string nombre, ayuda;
            switch (tipo)
            {
                case TipoFaena.Sierra:
                    nombre = "Ajustar la sierra";
                    ayuda = "Pulsa cuando la aguja pase por la franja verde. Tres veces, cada vez más fina.";
                    break;
                case TipoFaena.Colada:
                    nombre = "La colada";
                    ayuda = "Mantén pulsado para avivar el fuelle y deja el calor en la franja buena hasta que la colada esté lista. Si te pasas, se estropea.";
                    break;
                case TipoFaena.Circuito:
                    nombre = "Montar circuitos";
                    ayuda = tiempo < mostrarHasta
                        ? "Memoriza el orden de los bornes."
                        : "Repite el orden con las direcciones.";
                    break;
                case TipoFaena.Lentes:
                    nombre = "Calibrar las lentes";
                    ayuda = "Arriba y abajo cambian de anillo; izquierda y derecha lo giran. El haz brilla más cuanto mejor alineadas están.";
                    break;
                case TipoFaena.Martillo:
                    nombre = eligiendo ? "Forjar herramientas" : "Forjar " + (eleccion < Equipo.Numero ? Equipo.ConArticulo((Herramienta)eleccion) : "herramientas");
                    ayuda = eligiendo
                        ? "Elige qué forjar. Se paga con lo que lleves y con lo del almacén."
                        : "Pulsa cuando el martillo esté en lo alto, dentro de la franja, para dar el golpe con fuerza. Cuatro golpes, cada vez más rápido.";
                    break;
                case TipoFaena.Guiso:
                    nombre = "Cocinar un guiso";
                    ayuda = "Remueve alternando izquierda y derecha, rápido, antes de que se pegue.";
                    break;
                default:
                    nombre = "La manivela del pozo";
                    ayuda = "Pulsa una y otra vez para subir el cubo.";
                    break;
            }

            string lugar = edificio.Definicion != null ? edificio.Definicion.nombre.ToUpperInvariant() : "";
            Tema.Escribir(new Rect(dentro.x, dentro.y, dentro.width, 18f), lugar, Tema.Seccion);
            Tema.Escribir(new Rect(dentro.x, dentro.y + 20f, dentro.width, 30f), nombre, Tema.Titulo);
            Tema.Meandro(new Rect(dentro.x, dentro.y + 54f, dentro.width, 12f), new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.35f));
            Tema.Escribir(new Rect(dentro.x, dentro.y + 72f, dentro.width, 44f), ayuda, Tema.Texto);
            var zona = new Rect(dentro.x, dentro.y + 124f, dentro.width, 120f);

            switch (tipo)
            {
                case TipoFaena.Sierra: DibujarSierra(zona); break;
                case TipoFaena.Colada: DibujarColada(zona); break;
                case TipoFaena.Circuito: DibujarCircuito(zona); break;
                case TipoFaena.Lentes: DibujarLentes(zona); break;
                case TipoFaena.Martillo: DibujarMartillo(zona); break;
                default: Tema.Barra(new Rect(zona.x, zona.y + 40f, zona.width, 22f), nivel, Tema.Ocre); break;
            }

            float pie = panel.yMax - 26f;
            switch (tipo)
            {
                case TipoFaena.Martillo when eligiendo:
                    Tema.Indicaciones(panel.center.x, pie, Tema.Pequeno, (Boton.Elegir, "elegir"), (Boton.Accion, "forjar"),
                        (Boton.Faena, "dejarlo"));
                    break;
                case TipoFaena.Sierra:
                case TipoFaena.Manivela:
                case TipoFaena.Martillo:
                    Tema.Indicaciones(panel.center.x, pie, Tema.Pequeno, (Boton.Accion, "pulsar"), (Boton.Faena, "dejarlo"));
                    break;
                case TipoFaena.Colada:
                    Tema.Indicaciones(panel.center.x, pie, Tema.Pequeno, (Boton.Accion, "mantener"), (Boton.Faena, "dejarlo"));
                    break;
                case TipoFaena.Lentes:
                    Tema.Indicaciones(panel.center.x, pie, Tema.Pequeno, (Boton.Elegir, "anillo"), (Boton.Ajustar, "girar"),
                        (Boton.Faena, "dejarlo"));
                    break;
                default:
                    Tema.Indicaciones(panel.center.x, pie, Tema.Pequeno, (Boton.Ajustar, "direcciones"), (Boton.Faena, "dejarlo"));
                    break;
            }
            if (tipo == TipoFaena.Sierra || tipo == TipoFaena.Circuito || tipo == TipoFaena.Martillo)
            {
                // Tres marcas de fallo, arriba a la derecha.
                for (int i = 0; i < 3; i++)
                {
                    var r = new Rect(dentro.xMax - 3 * 22f + i * 22f + 4f, dentro.y + 4f, 14f, 14f);
                    if (i < fallos) Tema.Relleno(r, Tema.Rojo, 7f);
                    else Tema.Filo(r, Tema.Apagado, 1.5f, 7f);
                }
            }
        }

        void DibujarSierra(Rect zona)
        {
            Tema.Escribir(new Rect(zona.x, zona.y, zona.width, 24f), "Ajuste " + Mathf.Min(etapa + 1, 3) + " de 3", Tema.Pequeno);
            var pista = new Rect(zona.x, zona.y + 40f, zona.width, 34f);
            Tema.Relleno(pista, Tema.FondoHueco, 8f);
            float w = AnchoSierra[Mathf.Min(etapa, AnchoSierra.Length - 1)] * pista.width;
            Tema.Relleno(new Rect(pista.x + centro * pista.width - w * 0.5f, pista.y + 3f, w, pista.height - 6f), Tema.Verde, 6f);
            var agujaR = new Rect(pista.x + aguja * pista.width - 3f, pista.y - 9f, 6f, pista.height + 18f);
            Tema.Relleno(new Rect(agujaR.x + 1f, agujaR.y + 2f, agujaR.width, agujaR.height), new Color(0f, 0f, 0f, 0.4f), 3f);
            Tema.Relleno(agujaR, Tema.Pergamino, 3f);
        }

        void DibujarMartillo(Rect zona)
        {
            if (eligiendo)
            {
                DibujarEleccion(zona);
                return;
            }
            // A la izquierda, el golpe en vertical; a la derecha, los golpes dados.
            var pista = new Rect(zona.x + 40f, zona.y - 4f, 46f, zona.height + 4f);
            Tema.Relleno(pista, Tema.FondoHueco, 8f);
            float y0 = pista.yMax - ZonaMartilloMax * pista.height;
            float y1 = pista.yMax - ZonaMartilloMin * pista.height;
            var franja = new Rect(pista.x + 3f, y0, pista.width - 6f, y1 - y0);
            Tema.Relleno(franja, new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.45f), 4f);
            Tema.Filo(franja, Tema.Ocre, 1.5f, 4f);
            float y = pista.yMax - AlturaMartillo * pista.height;
            var cabeza = new Rect(pista.x - 10f, y - 7f, pista.width + 20f, 14f);
            Tema.Relleno(new Rect(cabeza.x + 1f, cabeza.y + 2f, cabeza.width, cabeza.height), new Color(0f, 0f, 0f, 0.4f), 4f);
            Tema.Relleno(cabeza, Tema.Pergamino, 4f);

            float x = pista.xMax + 60f;
            Tema.Escribir(new Rect(x, zona.y, zona.width - (x - zona.x), 24f),
                "Golpe " + Mathf.Min(golpes + 1, GolpesMartillo) + " de " + GolpesMartillo, Tema.Pequeno);
            for (int i = 0; i < GolpesMartillo; i++)
            {
                var r = new Rect(x + i * 70f, zona.y + 40f, 56f, 56f);
                bool hecho = i < golpes;
                Tema.Relleno(r, hecho ? Tema.Cobre : Tema.FondoClaro, 10f);
                Tema.Filo(r, i == golpes ? Tema.Ocre : new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.35f),
                    i == golpes ? 2f : 1f, 10f);
                if (hecho) Tema.Icono(new Rect(r.x + 10f, r.y + 10f, 36f, 36f), Recurso.Herramientas);
            }
        }

        void DibujarEleccion(Rect zona)
        {
            Juego juego = Juego.Instancia;
            Mundo mundo = juego != null ? juego.Mundo : null;
            const float fila = 25f;
            float y = zona.y - 8f;
            for (int i = 0; i < OpcionesBanco; i++)
            {
                var r = new Rect(zona.x, y + i * fila, zona.width, fila - 3f);
                bool elegida = i == eleccion;
                bool llega = mundo != null && mundo.AlAlcance(CosteOpcion(i)) && (i != (int)Herramienta.PicoBueno || EnHerreria);
                if (elegida)
                {
                    Tema.Relleno(r, new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.25f), 6f);
                    Tema.Filo(r, Tema.Ocre, 1.5f, 6f);
                }
                Color tinta = llega ? (elegida ? Tema.Ocre : Tema.Pergamino) : Tema.Apagado;
                string tiene = "";
                if (mundo != null && i < Equipo.Numero && mundo.Equipo.Tiene((Herramienta)i))
                {
                    var h = (Herramienta)i;
                    tiene = "  · la tienes (" + mundo.Equipo.Golpes(h) + "/" + Equipo.Aguante(h) + ")";
                }
                Tema.Escribir(new Rect(r.x + 10f, r.y, r.width * 0.42f, r.height), NombreOpcion(i) + tiene, Tema.Pequeno, tinta);
                Tema.Escribir(new Rect(r.x + r.width * 0.42f, r.y, r.width * 0.3f, r.height), Recursos.Lista(CosteOpcion(i)), Tema.Pequeno, tinta);
                Tema.Escribir(new Rect(r.x + r.width * 0.72f, r.y, r.width * 0.28f - 6f, r.height), ParaQueOpcion(i), Tema.Pequeno, Tema.Apagado);
            }
        }

        void DibujarColada(Rect zona)
        {
            Tema.Escribir(new Rect(zona.x, zona.y, zona.width, 22f), "Calor del horno", Tema.Pequeno);
            var pista = new Rect(zona.x, zona.y + 24f, zona.width, 30f);
            Tema.Relleno(pista, Tema.FondoHueco, 8f);
            Tema.Relleno(new Rect(pista.x + FranjaColadaMin * pista.width, pista.y, (FranjaColadaMax - FranjaColadaMin) * pista.width, pista.height),
                new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.35f), 4f);
            Tema.Filo(new Rect(pista.x + FranjaColadaMin * pista.width, pista.y, (FranjaColadaMax - FranjaColadaMin) * pista.width, pista.height),
                Tema.Ocre, 1.5f, 4f);
            Color calor = Color.Lerp(new Color(0.6f, 0.15f, 0.08f), new Color(1f, 0.85f, 0.4f), temperatura);
            Tema.Relleno(new Rect(pista.x + 4f, pista.y + 8f, Mathf.Max(8f, temperatura * pista.width - 8f), pista.height - 16f), calor, 7f);
            Tema.Escribir(new Rect(zona.x, zona.y + 66f, zona.width, 22f), "Colada lista", Tema.Pequeno);
            Tema.Barra(new Rect(zona.x, zona.y + 92f, zona.width, 14f), enFranja / SegundosColada, Tema.Verde);
        }

        void DibujarCircuito(Rect zona)
        {
            int n = secuencia.Length;
            const float lado = 64f;
            const float hueco = 18f;
            float x0 = zona.x + (zona.width - (n * lado + (n - 1) * hueco)) * 0.5f;
            bool mostrando = tiempo < mostrarHasta;
            for (int i = 0; i < n; i++)
            {
                var celda = new Rect(x0 + i * (lado + hueco), zona.y + 10f, lado, lado);
                bool hecho = i < acertados;
                Tema.Relleno(celda, hecho ? new Color(Tema.Verde.r, Tema.Verde.g, Tema.Verde.b, 0.85f) : Tema.FondoClaro, 10f);
                Tema.Filo(celda, i == acertados && !mostrando ? Tema.Ocre : new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.35f),
                    i == acertados && !mostrando ? 2f : 1f, 10f);
                bool visible = mostrando || hecho;
                Tema.Escribir(celda, visible ? Flechas[secuencia[i]] : "?", grande, hecho ? Tema.Tinta : Tema.Pergamino);
                if (visible)
                {
                    Tema.Escribir(new Rect(celda.x - 10f, celda.yMax + 4f, celda.width + 20f, 20f), NombresDireccion[secuencia[i]],
                        Tema.PequenoCentro);
                }
            }
            if (mostrando)
            {
                Tema.Barra(new Rect(zona.x, zona.y + 108f, zona.width, 5f), (mostrarHasta - tiempo) / 3f, Tema.Apagado);
            }
        }

        void DibujarLentes(Rect zona)
        {
            const float muesca = 44f;
            for (int a = 0; a < 2; a++)
            {
                float y = zona.y + a * 38f;
                bool activo = a == anillo;
                Tema.Escribir(new Rect(zona.x, y + 4f, 110f, 26f), "ANILLO " + (a + 1), Tema.Seccion, activo ? Tema.Ocre : Tema.Apagado);
                for (int i = 0; i < 8; i++)
                {
                    var r = new Rect(zona.x + 110f + i * (muesca + 6f), y + 4f, muesca, 24f);
                    Color color = i == posicion[a] ? (activo ? Tema.Ocre : new Color(0.75f, 0.65f, 0.4f)) : Tema.FondoHueco;
                    Tema.Relleno(r, color, 6f);
                }
            }
            Tema.Escribir(new Rect(zona.x, zona.y + 86f, 110f, 26f), "HAZ DE LUZ", Tema.Seccion);
            Tema.Barra(new Rect(zona.x + 110f, zona.y + 92f, zona.width - 110f, 14f), Brillo, new Color(0.6f, 0.95f, 0.9f));
        }

        void CrearEstilos()
        {
            grande = new GUIStyle(Tema.TextoCentro) { fontSize = 34, fontStyle = FontStyle.Bold };
        }
    }
}
