using System;

namespace Nostos.Sim
{
    /// <summary>Los cuatro estados de obra de la hoja de ruta.</summary>
    public enum EstadoObra
    {
        Cimientos,
        Esqueleto,
        CasiTerminado,
        Terminado
    }

    public enum TipoEdificio
    {
        Refugio,
        Produccion,
        Almacen,
        Casa,
        /// <summary>Edificios que no producen pero cambian las reglas: astillero y teleférico.</summary>
        Servicio
    }

    /// <summary>Lo que un edificio de producción transforma cada ciclo.</summary>
    public sealed class Receta
    {
        public Cantidad[] Entradas = new Cantidad[0];
        public Cantidad[] Salidas = new Cantidad[0];
        public int Ticks = 4;
    }

    /// <summary>Datos fijos de un tipo de edificio (en Unity los aporta un ScriptableObject).</summary>
    public sealed class DatosEdificio
    {
        public string Id;
        public string Nombre;
        public int Ancho = 1;
        public int Fondo = 1;
        public TipoEdificio Tipo;
        /// <summary>Lo que pide cada estado para pasar al siguiente: cimientos, esqueleto, casi terminado.</summary>
        public Cantidad[][] Costes = { new Cantidad[0], new Cantidad[0], new Cantidad[0] };
        public Receta Receta;
    }

    /// <summary>Un edificio colocado: primero obra que recibe material, luego edificio que produce.</summary>
    public sealed class Edificio
    {
        /// <summary>Cuántos ciclos de materia prima admite el depósito de entrada.</summary>
        public const int LotesMaximos = 8;
        public const int SalidaMaxima = 24;

        static readonly Cantidad[] SinCoste = new Cantidad[0];
        readonly int[] entregado = new int[Recursos.Numero];

        public int Id { get; }
        public DatosEdificio Datos { get; }
        public Celda Origen { get; }
        public EstadoObra Estado { get; private set; }
        public Bolsa Entrada { get; } = new Bolsa();
        public Bolsa Salida { get; } = new Bolsa();
        public int Progreso { get; private set; }
        public bool Trabajando { get; private set; }

        // ---- Faenas: lo que Nicias hace a mano dentro del edificio ----

        /// <summary>Máximo de ciclos dobles acumulables con las faenas.</summary>
        public const int LotesDoblesMaximos = 6;
        public const int ConfortMaximo = 2;

        /// <summary>Ticks de trabajo que quedan con la máquina afinada: avanza al doble de velocidad.</summary>
        public int TicksAfinado { get; private set; }
        /// <summary>Ciclos que quedan en los que sale el doble de producto.</summary>
        public int LotesDobles { get; private set; }
        /// <summary>Mejoras de confort de una casa de colonos (0 a ConfortMaximo).</summary>
        public int Confort { get; private set; }
        /// <summary>Último día en que se hizo una faena que solo se puede hacer una vez al día.</summary>
        public int DiaUltimaFaena { get; set; }

        public void Afinar(int ticks)
        {
            TicksAfinado = Math.Max(TicksAfinado, ticks);
        }

        public void AnadirLotesDobles(int ciclos)
        {
            LotesDobles = Math.Min(LotesDoblesMaximos, LotesDobles + Math.Max(0, ciclos));
        }

        public bool MejorarConfort()
        {
            if (Confort >= ConfortMaximo) return false;
            Confort++;
            return true;
        }

        public void RestaurarFaenas(int afinado, int lotesDobles, int confort, int diaUltimaFaena)
        {
            TicksAfinado = Math.Max(0, afinado);
            LotesDobles = Math.Max(0, Math.Min(LotesDoblesMaximos, lotesDobles));
            Confort = Math.Max(0, Math.Min(ConfortMaximo, confort));
            DiaUltimaFaena = diaUltimaFaena;
        }

        public Edificio(int id, DatosEdificio datos, Celda origen)
        {
            Id = id;
            Datos = datos;
            Origen = origen;
            SaltarEtapasGratis();
        }

        public bool Terminado => Estado == EstadoObra.Terminado;

        public int[] VolcarEntregado() => (int[])entregado.Clone();

        /// <summary>Repone el estado guardado de una partida.</summary>
        public void Restaurar(EstadoObra estado, int[] entregadoGuardado, int progreso)
        {
            Estado = estado;
            for (int i = 0; i < entregado.Length; i++)
            {
                entregado[i] = entregadoGuardado != null && i < entregadoGuardado.Length ? entregadoGuardado[i] : 0;
            }
            Progreso = Math.Max(0, progreso);
        }

        public Cantidad[] CosteActual => Terminado ? SinCoste : Datos.Costes[(int)Estado];

        public int Entregado(Recurso recurso) => entregado[(int)recurso];

        /// <summary>Cuánto falta de un recurso para completar el estado actual.</summary>
        public int Falta(Recurso recurso)
        {
            foreach (Cantidad c in CosteActual)
            {
                if (c.recurso == recurso) return Math.Max(0, c.cantidad - entregado[(int)recurso]);
            }
            return 0;
        }

        /// <summary>"2 de madera, 1 de mineral": lo que aún falta en este estado.</summary>
        public string DescribirFalta()
        {
            string texto = "";
            foreach (Cantidad c in CosteActual)
            {
                int falta = Falta(c.recurso);
                if (falta <= 0) continue;
                if (texto.Length > 0) texto += ", ";
                texto += falta + " de " + Recursos.Nombre(c.recurso);
            }
            return texto;
        }

        /// <summary>
        /// Pasa de la bolsa a la obra todo lo que haga falta. Devuelve cuántas unidades se han
        /// entregado; `avanzo` indica si con ello la obra ha pasado al estado siguiente.
        /// </summary>
        public int Entregar(Bolsa desde, out bool avanzo)
        {
            avanzo = false;
            if (Terminado) return 0;

            int total = 0;
            foreach (Cantidad c in CosteActual)
            {
                int movidas = desde.Sacar(c.recurso, Falta(c.recurso));
                entregado[(int)c.recurso] += movidas;
                total += movidas;
            }

            if (EtapaCompleta())
            {
                Avanzar();
                SaltarEtapasGratis();
                avanzo = true;
            }
            return total;
        }

        /// <summary>
        /// Entrega directa de un recurso a la obra (la usan los porteadores). Devuelve cuántas
        /// unidades ha aceptado; el resto no hacía falta.
        /// </summary>
        public int RecibirEnObra(Recurso recurso, int cantidad, out bool avanzo)
        {
            avanzo = false;
            if (Terminado) return 0;
            int aceptadas = Math.Min(cantidad, Falta(recurso));
            if (aceptadas <= 0) return 0;
            entregado[(int)recurso] += aceptadas;
            if (EtapaCompleta())
            {
                Avanzar();
                SaltarEtapasGratis();
                avanzo = true;
            }
            return aceptadas;
        }

        /// <summary>Deja en el depósito de entrada lo que la receta admite. Devuelve unidades movidas.</summary>
        public int Depositar(Bolsa desde)
        {
            if (!Terminado || Datos.Receta == null) return 0;
            int total = 0;
            foreach (Cantidad c in Datos.Receta.Entradas)
            {
                int hueco = c.cantidad * LotesMaximos - Entrada[c.recurso];
                if (hueco <= 0) continue;
                total += Entrada.Meter(c.recurso, desde.Sacar(c.recurso, hueco));
            }
            return total;
        }

        /// <summary>Pasa a la bolsa lo producido, hasta donde quepa. Devuelve unidades movidas.</summary>
        public int Recoger(Bolsa hacia)
        {
            int total = 0;
            for (int i = 0; i < Recursos.Numero; i++)
            {
                var recurso = (Recurso)i;
                int movidas = hacia.Meter(recurso, Salida[recurso]);
                Salida.Sacar(recurso, movidas);
                total += movidas;
            }
            return total;
        }

        public void Tick()
        {
            if (!Terminado || Datos.Receta == null)
            {
                Trabajando = false;
                return;
            }

            Receta receta = Datos.Receta;
            if (Progreso == 0)
            {
                if (!Entrada.Tiene(receta.Entradas) || Salida.Total >= SalidaMaxima)
                {
                    Trabajando = false;
                    return;
                }
                foreach (Cantidad c in receta.Entradas) Entrada.Sacar(c.recurso, c.cantidad);
            }

            Trabajando = true;
            Progreso += TicksAfinado > 0 ? 2 : 1;
            if (TicksAfinado > 0) TicksAfinado--;
            if (Progreso >= receta.Ticks)
            {
                int veces = LotesDobles > 0 ? 2 : 1;
                if (LotesDobles > 0) LotesDobles--;
                for (int v = 0; v < veces; v++)
                {
                    foreach (Cantidad c in receta.Salidas) Salida.Meter(c.recurso, c.cantidad);
                }
                Progreso = 0;
            }
        }

        bool EtapaCompleta()
        {
            foreach (Cantidad c in CosteActual)
            {
                if (Falta(c.recurso) > 0) return false;
            }
            return true;
        }

        void Avanzar()
        {
            Estado = (EstadoObra)((int)Estado + 1);
            Array.Clear(entregado, 0, entregado.Length);
        }

        void SaltarEtapasGratis()
        {
            while (!Terminado && CosteActual.Length == 0) Avanzar();
        }
    }
}
