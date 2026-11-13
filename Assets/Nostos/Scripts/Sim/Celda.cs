using System;

namespace Nostos.Sim
{
    /// <summary>Coordenada entera de una celda de la rejilla de construcción.</summary>
    public readonly struct Celda : IEquatable<Celda>
    {
        public readonly int X;
        public readonly int Z;

        public Celda(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(Celda otra) => X == otra.X && Z == otra.Z;
        public override bool Equals(object obj) => obj is Celda otra && Equals(otra);
        public override int GetHashCode() => unchecked((X * 397) ^ Z);
        public override string ToString() => $"({X}, {Z})";

        public static bool operator ==(Celda a, Celda b) => a.Equals(b);
        public static bool operator !=(Celda a, Celda b) => !a.Equals(b);
    }
}
