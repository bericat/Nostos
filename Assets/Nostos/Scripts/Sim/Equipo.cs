using System;

namespace Nostos.Sim
{
    /// <summary>Las herramientas de Nicias, que se forjan en el banco de trabajo.</summary>
    public enum Herramienta
    {
        Hacha,
        Pico,
        Pala,
        /// <summary>El pico bueno, pintado a mano: saca más y dura el doble.</summary>
        PicoBueno
    }

    /// <summary>
    /// Lo que Nicias lleva colgado (aparte de la mochila): una de cada herramienta como mucho, con
    /// los golpes que le quedan antes de romperse.
    /// </summary>
    public sealed class Equipo
    {
        public const int Numero = 4;
        static readonly int[] aguante = { 40, 40, 30, 80 };
        static readonly Cantidad[][] costes =
        {
            new[] { new Cantidad(Recurso.Madera, 1), new Cantidad(Recurso.Mineral, 1) },
            new[] { new Cantidad(Recurso.Madera, 1), new Cantidad(Recurso.Mineral, 2) },
            new[] { new Cantidad(Recurso.Madera, 1), new Cantidad(Recurso.Mineral, 1) },
            new[] { new Cantidad(Recurso.Madera, 1), new Cantidad(Recurso.Mineral, 2), new Cantidad(Recurso.Cobre, 1) },
        };

        readonly int[] golpes = new int[Numero];

        public static int Aguante(Herramienta h) => aguante[(int)h];
        public static Cantidad[] Coste(Herramienta h) => costes[(int)h];

        public static string Nombre(Herramienta h)
        {
            switch (h)
            {
                case Herramienta.Hacha: return "hacha";
                case Herramienta.Pico: return "pico";
                case Herramienta.Pala: return "pala";
                case Herramienta.PicoBueno: return "pico bueno";
                default: return h.ToString();
            }
        }

        /// <summary>"el hacha", "la pala", "el pico".</summary>
        public static string ConArticulo(Herramienta h) => (h == Herramienta.Pala ? "la " : "el ") + Nombre(h);

        public bool Tiene(Herramienta h) => golpes[(int)h] > 0;

        /// <summary>Golpes que le quedan (0 si no la tiene).</summary>
        public int Golpes(Herramienta h) => golpes[(int)h];

        /// <summary>Una nueva (si ya tenía una, la gastada se cambia por la nueva).</summary>
        public void Dar(Herramienta h) => golpes[(int)h] = Aguante(h);

        /// <summary>Un golpe con ella. Devuelve true si se ha roto con este golpe.</summary>
        public bool Gastar(Herramienta h)
        {
            int i = (int)h;
            if (golpes[i] <= 0) return false;
            golpes[i]--;
            return golpes[i] == 0;
        }

        /// <summary>
        /// La herramienta que sirve para sacar este recurso, la mejor que lleve. Devuelve false si
        /// no lleva ninguna que sirva. `extra` es lo que saca de más en cada golpe.
        /// </summary>
        public bool ParaRecoger(Recurso recurso, out Herramienta h, out int extra)
        {
            h = Herramienta.Hacha;
            extra = 0;
            switch (recurso)
            {
                case Recurso.Madera:
                    if (!Tiene(Herramienta.Hacha)) return false;
                    extra = 1;
                    return true;
                case Recurso.Mineral:
                    if (Tiene(Herramienta.PicoBueno))
                    {
                        h = Herramienta.PicoBueno;
                        extra = 2;
                        return true;
                    }
                    if (!Tiene(Herramienta.Pico)) return false;
                    h = Herramienta.Pico;
                    extra = 1;
                    return true;
                case Recurso.Cobre:
                    if (Tiene(Herramienta.PicoBueno))
                    {
                        h = Herramienta.PicoBueno;
                        extra = 1;
                        return true;
                    }
                    if (!Tiene(Herramienta.Pala)) return false;
                    h = Herramienta.Pala;
                    extra = 1;
                    return true;
            }
            return false;
        }

        public int[] Volcar() => (int[])golpes.Clone();

        public void Cargar(int[] valores)
        {
            Array.Clear(golpes, 0, Numero);
            if (valores == null) return;
            for (int i = 0; i < Numero && i < valores.Length; i++) golpes[i] = Math.Max(0, Math.Min(valores[i], aguante[i]));
        }
    }
}
