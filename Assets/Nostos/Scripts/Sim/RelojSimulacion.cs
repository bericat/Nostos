using System;

namespace Nostos.Sim
{
    /// <summary>
    /// Reloj de tick fijo para la simulación. La presentación le pasa el tiempo
    /// transcurrido y él devuelve cuántos ticks hay que simular.
    /// </summary>
    public sealed class RelojSimulacion
    {
        readonly float segundosPorTick;
        float acumulado;

        public long Tick { get; private set; }
        /// <summary>Cuánto del tick siguiente ha pasado ya (0 a 1). Para interpolar la presentación.</summary>
        public float Fraccion => acumulado / segundosPorTick;
        public int MaxTicksPorAvance { get; set; } = 8;

        public RelojSimulacion(float segundosPorTick)
        {
            if (segundosPorTick <= 0f) throw new ArgumentOutOfRangeException(nameof(segundosPorTick));
            this.segundosPorTick = segundosPorTick;
        }

        public int Avanzar(float delta)
        {
            if (delta <= 0f) return 0;
            acumulado += delta;
            int ticks = 0;
            while (acumulado >= segundosPorTick && ticks < MaxTicksPorAvance)
            {
                acumulado -= segundosPorTick;
                ticks++;
            }
            if (ticks == MaxTicksPorAvance && acumulado > segundosPorTick) acumulado = 0f;
            Tick += ticks;
            return ticks;
        }
    }
}
