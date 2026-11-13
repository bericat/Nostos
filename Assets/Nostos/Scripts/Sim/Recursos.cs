using System;

namespace Nostos.Sim
{
    public enum Recurso
    {
        Madera,
        Mineral,
        Tablones,
        Lingotes,
        Chapa,
        Comida,
        Raciones,
        Cobre,
        Cristal,
        Agua,
        Cable,
        Bobinas,
        Lentes,
        Nucleos,
        /// <summary>Se hacen en el banco de trabajo. Al final del enum para no romper partidas antiguas.</summary>
        Herramientas
    }

    /// <summary>Una cantidad de un recurso: sirve para costes, recetas y entregas.</summary>
    [Serializable]
    public struct Cantidad
    {
        public Recurso recurso;
        public int cantidad;

        public Cantidad(Recurso recurso, int cantidad)
        {
            this.recurso = recurso;
            this.cantidad = cantidad;
        }
    }

    public static class Recursos
    {
        public const int Numero = 15;

        public static string Nombre(Recurso recurso)
        {
            switch (recurso)
            {
                case Recurso.Madera: return "madera";
                case Recurso.Mineral: return "mineral";
                case Recurso.Tablones: return "tablones";
                case Recurso.Lingotes: return "lingotes";
                case Recurso.Chapa: return "chapa";
                case Recurso.Comida: return "comida";
                case Recurso.Raciones: return "raciones";
                case Recurso.Cobre: return "cobre";
                case Recurso.Cristal: return "cristal";
                case Recurso.Agua: return "agua";
                case Recurso.Cable: return "cable";
                case Recurso.Bobinas: return "bobinas";
                case Recurso.Lentes: return "lentes";
                case Recurso.Nucleos: return "núcleos";
                case Recurso.Herramientas: return "herramientas";
                default: return recurso.ToString();
            }
        }

        /// <summary>"4 de madera, 2 de mineral". Devuelve cadena vacía si no hay nada.</summary>
        public static string Lista(Cantidad[] cantidades)
        {
            if (cantidades == null) return "";
            string texto = "";
            foreach (Cantidad c in cantidades)
            {
                if (c.cantidad <= 0) continue;
                if (texto.Length > 0) texto += ", ";
                texto += c.cantidad + " de " + Nombre(c.recurso);
            }
            return texto;
        }
    }

    /// <summary>Un montón de recursos con capacidad opcional: la mochila, o el depósito de un edificio.</summary>
    public sealed class Bolsa
    {
        readonly int[] cantidades = new int[Recursos.Numero];

        /// <summary>Unidades totales que caben. 0 significa sin límite.</summary>
        public int Capacidad { get; }
        public int Total { get; private set; }

        public Bolsa(int capacidad = 0)
        {
            Capacidad = capacidad;
        }

        public int this[Recurso recurso] => cantidades[(int)recurso];

        public int Hueco => Capacidad <= 0 ? int.MaxValue : Capacidad - Total;

        /// <summary>Mete hasta `n` unidades y devuelve cuántas han cabido.</summary>
        public int Meter(Recurso recurso, int n)
        {
            int metidas = Math.Min(n, Hueco);
            if (metidas <= 0) return 0;
            cantidades[(int)recurso] += metidas;
            Total += metidas;
            return metidas;
        }

        /// <summary>Saca hasta `n` unidades y devuelve cuántas había.</summary>
        public int Sacar(Recurso recurso, int n)
        {
            int sacadas = Math.Min(n, cantidades[(int)recurso]);
            if (sacadas <= 0) return 0;
            cantidades[(int)recurso] -= sacadas;
            Total -= sacadas;
            return sacadas;
        }

        public bool Tiene(Cantidad[] lista)
        {
            foreach (Cantidad c in lista)
            {
                if (cantidades[(int)c.recurso] < c.cantidad) return false;
            }
            return true;
        }

        /// <summary>Copia de las cantidades, por índice de recurso (para guardar la partida).</summary>
        public int[] Volcar()
        {
            return (int[])cantidades.Clone();
        }

        /// <summary>Sustituye el contenido por lo guardado. Admite listas más cortas (partidas antiguas).</summary>
        public void Cargar(int[] valores)
        {
            Total = 0;
            for (int i = 0; i < cantidades.Length; i++)
            {
                cantidades[i] = valores != null && i < valores.Length ? Math.Max(0, valores[i]) : 0;
                Total += cantidades[i];
            }
        }

        /// <summary>"3 de madera, 1 de mineral", o cadena vacía.</summary>
        public string Describir()
        {
            string texto = "";
            for (int i = 0; i < cantidades.Length; i++)
            {
                if (cantidades[i] <= 0) continue;
                if (texto.Length > 0) texto += ", ";
                texto += cantidades[i] + " de " + Recursos.Nombre((Recurso)i);
            }
            return texto;
        }
    }
}
