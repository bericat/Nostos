using System;
using System.Collections.Generic;

namespace Nostos.Sim
{
    public enum NivelConfianza
    {
        Baja,
        Media,
        Alta
    }

    /// <summary>Las cinco palancas de un acuerdo de trabajo.</summary>
    public enum Palanca
    {
        Jornada,
        Raciones,
        Reparto,
        Seguridad,
        Voz
    }

    /// <summary>
    /// Condiciones de trabajo pactadas con un colono. Cada palanca va de 0 (duro) a 2 (flexible).
    /// Cuanto más duro, más produce y más desgasta la confianza.
    /// </summary>
    public sealed class Acuerdo
    {
        public const int NumPalancas = 5;
        public readonly int[] Niveles = { 1, 1, 1, 1, 1 };

        /// <summary>De 0 (todo flexible) a 10 (todo duro).</summary>
        public int Dureza
        {
            get
            {
                int dureza = 0;
                foreach (int nivel in Niveles) dureza += 2 - nivel;
                return dureza;
            }
        }

        public float Productividad => 0.7f + 0.06f * Dureza;

        /// <summary>Cuánto cambia la confianza cada día solo por las condiciones.</summary>
        public float DerivaDiaria => (5 - Dureza) * 1.2f;

        public int RacionesPorDia => Niveles[(int)Palanca.Raciones] == 2 ? 2 : 1;

        public void HorasDeTrabajo(out float inicio, out float fin)
        {
            switch (Niveles[(int)Palanca.Jornada])
            {
                case 0: inicio = 6f; fin = 22f; break;
                case 1: inicio = 7f; fin = 19f; break;
                default: inicio = 8f; fin = 16f; break;
            }
        }

        public Acuerdo Copia()
        {
            var copia = new Acuerdo();
            Array.Copy(Niveles, copia.Niveles, NumPalancas);
            return copia;
        }
    }

    public enum RespuestaNegociacion
    {
        Rechaza,
        Contraoferta,
        Acepta
    }

    public enum TipoTarea
    {
        /// <summary>Del almacén a un edificio (obra o depósito de entrada).</summary>
        Llevar,
        /// <summary>De la salida de un edificio al almacén.</summary>
        Recoger
    }

    public sealed class Tarea
    {
        public TipoTarea Tipo;
        public Edificio Edificio;
        public bool ParaObra;
        public bool SegundaFase;
        public readonly Bolsa Carga = new Bolsa();
    }

    public enum TipoDestino
    {
        Plaza,
        Almacen,
        Edificio,
        Casa,
        Comedor
    }

    public struct Destino
    {
        public TipoDestino Tipo;
        public Edificio Edificio;

        public Destino(TipoDestino tipo, Edificio edificio)
        {
            Tipo = tipo;
            Edificio = edificio;
        }
    }

    public sealed class Colono
    {
        public string Nombre;
        public string Rol;
        public float Confianza { get; private set; }
        public Acuerdo Acuerdo;
        public Edificio Casa;
        public Tarea Tarea;
        public Cantidad Encargo;
        public bool EncargoPendiente;
        public bool Hambriento;
        /// <summary>Si hoy no trabaja, por qué ("herido", "en asamblea"). Null si trabaja.</summary>
        public string Ausente;
        public int DiasDeRiesgo;

        public Colono(string nombre, string rol, float confianza)
        {
            Nombre = nombre;
            Rol = rol;
            Confianza = confianza;
        }

        public NivelConfianza Nivel =>
            Confianza < GestorColonos.UmbralMedia ? NivelConfianza.Baja
            : Confianza < GestorColonos.UmbralAlta ? NivelConfianza.Media
            : NivelConfianza.Alta;

        public float Productividad => Acuerdo != null ? Acuerdo.Productividad : 1f;

        public void CambiarConfianza(float delta)
        {
            Confianza = Math.Max(0f, Math.Min(100f, Confianza + delta));
        }

        public void FijarConfianza(float valor)
        {
            Confianza = Math.Max(0f, Math.Min(100f, valor));
        }
    }

    /// <summary>
    /// Reglas de los colonos: confianza, negociación de acuerdos, encargos, reparto de tareas
    /// de porteo y lo que pasa cada día (comida, casa, desgaste por las condiciones).
    /// </summary>
    public sealed class GestorColonos
    {
        public const float UmbralMedia = 35f;
        public const float UmbralAlta = 70f;
        public const float UmbralAbandono = 20f;
        public const int PlazasPorCasa = 2;
        public const float PremioEncargo = 12f;
        public const int DiasEntreAccidentesSinPausas = 3;
        public const int DiasEntreAccidentesConPausas = 8;
        public const int DiasEntreAsambleas = 4;

        static readonly Recurso[] ParaElPueblo = { Recurso.Tablones, Recurso.Madera, Recurso.Lingotes, Recurso.Mineral };

        static readonly Cantidad[] EncargosPosibles =
        {
            new Cantidad(Recurso.Comida, 3),
            new Cantidad(Recurso.Madera, 4),
            new Cantidad(Recurso.Mineral, 3),
            new Cantidad(Recurso.Comida, 4),
            new Cantidad(Recurso.Tablones, 4)
        };

        readonly Mundo mundo;
        readonly List<Colono> colonos = new List<Colono>();
        int siguienteEncargo;

        public IReadOnlyList<Colono> Colonos => colonos;
        public event Action<string> Aviso;

        public GestorColonos(Mundo mundo)
        {
            this.mundo = mundo;
        }

        /// <summary>Confianza global del asentamiento: la media de todos los colonos.</summary>
        public float ConfianzaGlobal
        {
            get
            {
                if (colonos.Count == 0) return 0f;
                float suma = 0f;
                foreach (Colono c in colonos) suma += c.Confianza;
                return suma / colonos.Count;
            }
        }

        public void CambiarConfianzaDeTodos(float delta)
        {
            foreach (Colono c in colonos) c.CambiarConfianza(delta);
        }

        /// <summary>Deja a todos sin tarea, devolviendo al almacén lo que llevaran (antes de guardar).</summary>
        public void SoltarTodo()
        {
            foreach (Colono c in colonos) Soltar(c);
        }

        public Colono Anadir(string nombre, string rol, float confianza)
        {
            var colono = new Colono(nombre, rol, confianza);
            NuevoEncargo(colono);
            colonos.Add(colono);
            return colono;
        }

        // ------------------------------------------------------------ negociación

        /// <summary>
        /// El colono valora una propuesta. Con confianza baja no acepta acuerdos; con media
        /// admite condiciones moderadas y con alta, sacrificios. Si pides de más, contraoferta.
        /// </summary>
        public RespuestaNegociacion Negociar(Colono colono, Acuerdo propuesta, out Acuerdo contraoferta)
        {
            contraoferta = null;
            if (colono.Nivel == NivelConfianza.Baja) return RespuestaNegociacion.Rechaza;

            int limite = DurezaMaxima(colono);
            if (propuesta.Dureza <= limite) return RespuestaNegociacion.Acepta;

            contraoferta = propuesta.Copia();
            while (contraoferta.Dureza > limite)
            {
                int masDura = 0;
                for (int i = 1; i < Acuerdo.NumPalancas; i++)
                {
                    if (contraoferta.Niveles[i] < contraoferta.Niveles[masDura]) masDura = i;
                }
                contraoferta.Niveles[masDura]++;
            }
            return RespuestaNegociacion.Contraoferta;
        }

        public int DurezaMaxima(Colono colono)
        {
            switch (colono.Nivel)
            {
                case NivelConfianza.Alta: return 8;
                case NivelConfianza.Media: return 5;
                default: return -1;
            }
        }

        /// <summary>Cierra un acuerdo. Endurecer uno que ya existía cuesta confianza.</summary>
        public void Firmar(Colono colono, Acuerdo acuerdo)
        {
            if (colono.Acuerdo != null && acuerdo.Dureza > colono.Acuerdo.Dureza)
            {
                colono.CambiarConfianza(-5f);
            }
            colono.Acuerdo = acuerdo.Copia();
        }

        public void Romper(Colono colono)
        {
            if (colono.Acuerdo == null) return;
            colono.Acuerdo = null;
            colono.Casa = null;
            Soltar(colono);
        }

        public bool CumplirEncargo(Colono colono, Bolsa desde)
        {
            if (!colono.EncargoPendiente) return false;
            if (desde[colono.Encargo.recurso] < colono.Encargo.cantidad) return false;
            desde.Sacar(colono.Encargo.recurso, colono.Encargo.cantidad);
            colono.EncargoPendiente = false;
            colono.CambiarConfianza(PremioEncargo);
            return true;
        }

        void NuevoEncargo(Colono colono)
        {
            colono.Encargo = EncargosPosibles[siguienteEncargo % EncargosPosibles.Length];
            colono.EncargoPendiente = true;
            siguienteEncargo++;
        }

        // ------------------------------------------------------------ rutinas y tareas

        public bool EnHorario(Colono colono)
        {
            if (colono.Acuerdo == null || colono.Ausente != null) return false;
            colono.Acuerdo.HorasDeTrabajo(out float inicio, out float fin);
            float hora = mundo.Ciclo.Hora;
            return hora >= inicio && hora < fin;
        }

        /// <summary>A dónde debería ir ahora mismo este colono.</summary>
        public Destino DestinoDe(Colono colono)
        {
            Tarea tarea = colono.Tarea;
            if (tarea != null)
            {
                bool alAlmacen = (tarea.Tipo == TipoTarea.Llevar) != tarea.SegundaFase;
                return alAlmacen
                    ? new Destino(TipoDestino.Almacen, mundo.EdificioAlmacen)
                    : new Destino(TipoDestino.Edificio, tarea.Edificio);
            }

            float hora = mundo.Ciclo.Hora;
            if (hora >= 21f || hora < 6f)
            {
                return colono.Casa != null
                    ? new Destino(TipoDestino.Casa, colono.Casa)
                    : new Destino(TipoDestino.Plaza, null);
            }
            if (hora >= 13f && hora < 14f)
            {
                Edificio comedor = mundo.PrimerTerminado(TipoEdificio.Produccion, "comedor");
                if (comedor != null) return new Destino(TipoDestino.Comedor, comedor);
            }
            if (EnHorario(colono) && mundo.EdificioAlmacen != null)
            {
                return new Destino(TipoDestino.Almacen, mundo.EdificioAlmacen);
            }
            return new Destino(TipoDestino.Plaza, null);
        }

        /// <summary>La presentación avisa de que el colono ha llegado a su destino.</summary>
        public void Llegar(Colono colono)
        {
            Tarea tarea = colono.Tarea;
            if (tarea == null) return;

            if (!tarea.SegundaFase)
            {
                if (tarea.Tipo == TipoTarea.Recoger)
                {
                    int carga = CargaMaxima(colono);
                    for (int i = 0; i < Recursos.Numero && tarea.Carga.Total < carga; i++)
                    {
                        var recurso = (Recurso)i;
                        int n = tarea.Edificio.Salida.Sacar(recurso, carga - tarea.Carga.Total);
                        tarea.Carga.Meter(recurso, n);
                    }
                }
                tarea.SegundaFase = true;
                return;
            }

            if (tarea.Tipo == TipoTarea.Llevar)
            {
                Edificio destino = tarea.Edificio;
                for (int i = 0; i < Recursos.Numero; i++)
                {
                    var recurso = (Recurso)i;
                    int n = tarea.Carga[recurso];
                    if (n <= 0) continue;
                    int aceptadas = 0;
                    if (tarea.ParaObra)
                    {
                        aceptadas = destino.RecibirEnObra(recurso, n, out bool avanzo);
                        if (avanzo && destino.Terminado) Avisar(destino.Datos.Nombre + " terminado por los colonos.");
                    }
                    else if (destino.Terminado && destino.Datos.Receta != null)
                    {
                        aceptadas = destino.Entrada.Meter(recurso, n);
                    }
                    tarea.Carga.Sacar(recurso, aceptadas);
                }
            }
            Soltar(colono);
        }

        /// <summary>Devuelve al almacén lo que llevara encima y deja al colono sin tarea.</summary>
        void Soltar(Colono colono)
        {
            Tarea tarea = colono.Tarea;
            if (tarea == null) return;
            for (int i = 0; i < Recursos.Numero; i++)
            {
                var recurso = (Recurso)i;
                mundo.Almacen.Meter(recurso, tarea.Carga.Sacar(recurso, tarea.Carga[recurso]));
            }
            colono.Tarea = null;
        }

        public void EdificioQuitado(Edificio edificio)
        {
            foreach (Colono c in colonos)
            {
                if (c.Tarea != null && c.Tarea.Edificio == edificio) Soltar(c);
                if (c.Casa == edificio) c.Casa = null;
            }
        }

        int CargaMaxima(Colono colono)
        {
            return Math.Max(1, (int)Math.Round(3f * colono.Productividad));
        }

        int EnCamino(Edificio edificio, Recurso recurso, bool paraObra)
        {
            int total = 0;
            foreach (Colono c in colonos)
            {
                Tarea t = c.Tarea;
                if (t != null && t.Tipo == TipoTarea.Llevar && t.Edificio == edificio && t.ParaObra == paraObra)
                {
                    total += t.Carga[recurso];
                }
            }
            return total;
        }

        bool AlguienRecoge(Edificio edificio)
        {
            foreach (Colono c in colonos)
            {
                if (c.Tarea != null && c.Tarea.Tipo == TipoTarea.Recoger && c.Tarea.Edificio == edificio) return true;
            }
            return false;
        }

        public void Tick()
        {
            if (mundo.EdificioAlmacen == null) return;
            foreach (Colono c in colonos)
            {
                if (c.Tarea == null && EnHorario(c)) c.Tarea = BuscarTarea(c);
            }
        }

        Tarea BuscarTarea(Colono colono)
        {
            int carga = CargaMaxima(colono);
            Bolsa almacen = mundo.Almacen;

            // 1. Obras: llevar lo que falta.
            foreach (Edificio e in mundo.Edificios)
            {
                if (e.Terminado) continue;
                foreach (Cantidad c in e.CosteActual)
                {
                    int pendiente = e.Falta(c.recurso) - EnCamino(e, c.recurso, true);
                    int n = Math.Min(carga, Math.Min(pendiente, almacen[c.recurso]));
                    if (n <= 0) continue;
                    var tarea = new Tarea { Tipo = TipoTarea.Llevar, Edificio = e, ParaObra = true };
                    tarea.Carga.Meter(c.recurso, almacen.Sacar(c.recurso, n));
                    return tarea;
                }
            }

            // 2. Edificios de producción: recoger lo hecho y reponer materia prima.
            foreach (Edificio e in mundo.Edificios)
            {
                if (!e.Terminado || e.Datos.Receta == null) continue;

                if (e.Salida.Total >= 3 && !AlguienRecoge(e))
                {
                    return new Tarea { Tipo = TipoTarea.Recoger, Edificio = e };
                }

                foreach (Cantidad c in e.Datos.Receta.Entradas)
                {
                    int deseado = c.cantidad * 3;
                    int pendiente = deseado - e.Entrada[c.recurso] - EnCamino(e, c.recurso, false);
                    int n = Math.Min(carga, Math.Min(pendiente, almacen[c.recurso]));
                    if (n <= 0) continue;
                    var tarea = new Tarea { Tipo = TipoTarea.Llevar, Edificio = e, ParaObra = false };
                    tarea.Carga.Meter(c.recurso, almacen.Sacar(c.recurso, n));
                    return tarea;
                }
            }
            return null;
        }

        // ------------------------------------------------------------ cambio de día

        public void NuevoDia()
        {
            AsignarCasas();
            bool hayAlmacen = mundo.EdificioAlmacen != null;

            foreach (Colono c in colonos)
            {
                if (!c.EncargoPendiente) NuevoEncargo(c);
                c.Ausente = null;
                if (c.Acuerdo == null)
                {
                    c.Hambriento = false;
                    continue;
                }

                // Quien trabaja para ti come y duerme a tu cuenta.
                float cambio = c.Acuerdo.DerivaDiaria;
                int raciones = hayAlmacen ? mundo.Almacen.Sacar(Recurso.Raciones, c.Acuerdo.RacionesPorDia) : 0;
                if (raciones > 0)
                {
                    c.Hambriento = false;
                    cambio += 1f + raciones;
                }
                else if (hayAlmacen && mundo.Almacen.Sacar(Recurso.Comida, 1) > 0)
                {
                    c.Hambriento = false;
                }
                else
                {
                    c.Hambriento = true;
                    cambio -= 6f;
                }
                cambio += c.Casa != null ? 1f + c.Casa.Confort : -2f;
                cambio += Reparto(c);
                cambio += Seguridad(c);

                // Voz: a quien no se le escucha le cuesta fiarse; quien decide, se fía antes y se reúne.
                int voz = c.Acuerdo.Niveles[(int)Palanca.Voz];
                if (voz == 0 && cambio > 0f) cambio *= 0.5f;
                else if (voz == 2)
                {
                    if (cambio > 0f) cambio *= 1.5f;
                    if (c.Ausente == null && mundo.Ciclo.Dia % DiasEntreAsambleas == 0) c.Ausente = "en asamblea";
                }
                c.CambiarConfianza(cambio);

                if (c.Confianza < UmbralAbandono)
                {
                    Romper(c);
                    Avisar(c.Nombre + " deja de trabajar contigo: ya no confía en ti.");
                }
            }
        }

        /// <summary>
        /// Reparto: parte de lo que hay en el almacén se queda en el pueblo. Devuelve el cambio
        /// de confianza. Si se prometió reparto y el almacén está vacío, sienta mal.
        /// </summary>
        float Reparto(Colono c)
        {
            int nivel = c.Acuerdo.Niveles[(int)Palanca.Reparto];
            if (nivel == 0) return 0f;

            int dado = 0;
            foreach (Recurso recurso in ParaElPueblo)
            {
                if (dado >= nivel) break;
                dado += mundo.Almacen.Sacar(recurso, nivel - dado);
            }
            if (dado == 0) return -1f;
            if (nivel == 2 && dado == 2)
            {
                foreach (Colono otro in colonos)
                {
                    if (otro != c) otro.CambiarConfianza(0.5f);
                }
            }
            return 1.5f * dado;
        }

        /// <summary>
        /// Seguridad: sin pausas hay un accidente cada pocos días; con pausas, muy de tarde en
        /// tarde; con buenas herramientas nunca, pero gastan una herramienta del almacén cada tres
        /// días (o, si no hay, un lingote para apañar las viejas).
        /// </summary>
        float Seguridad(Colono c)
        {
            int nivel = c.Acuerdo.Niveles[(int)Palanca.Seguridad];
            c.DiasDeRiesgo++;
            if (nivel == 2)
            {
                if (c.DiasDeRiesgo % 3 != 0) return 0f;
                if (mundo.Almacen.Sacar(Recurso.Herramientas, 1) > 0) return 0f;
                return mundo.Almacen.Sacar(Recurso.Lingotes, 1) > 0 ? 0f : -1f;
            }

            int cada = nivel == 0 ? DiasEntreAccidentesSinPausas : DiasEntreAccidentesConPausas;
            if (c.DiasDeRiesgo % cada != 0) return 0f;
            c.Ausente = "herido";
            foreach (Colono otro in colonos)
            {
                if (otro != c) otro.CambiarConfianza(-2f);
            }
            Avisar(c.Nombre + " se ha hecho daño en el trabajo. Hoy no trabaja, y Nova Lavinia toma nota.");
            return -8f;
        }

        void AsignarCasas()
        {
            foreach (Colono c in colonos)
            {
                if (c.Acuerdo == null || c.Casa != null) continue;
                foreach (Edificio e in mundo.Edificios)
                {
                    if (!e.Terminado || e.Datos.Tipo != TipoEdificio.Casa) continue;
                    if (Habitantes(e) >= PlazasPorCasa) continue;
                    c.Casa = e;
                    break;
                }
            }
        }

        public int Habitantes(Edificio casa)
        {
            int total = 0;
            foreach (Colono c in colonos)
            {
                if (c.Casa == casa) total++;
            }
            return total;
        }

        void Avisar(string texto)
        {
            Aviso?.Invoke(texto);
        }
    }
}
