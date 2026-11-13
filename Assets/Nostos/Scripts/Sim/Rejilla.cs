using System;
using System.Collections.Generic;

namespace Nostos.Sim
{
    /// <summary>
    /// Estado de ocupación de la rejilla. C# puro, sin UnityEngine:
    /// la presentación (ColocadorRejilla) la consulta y la modifica, pero no vive aquí.
    /// </summary>
    public sealed class Rejilla
    {
        readonly Dictionary<Celda, int> ocupadas = new Dictionary<Celda, int>();
        int siguienteId = 1;

        public float TamCelda { get; }
        public int Ocupadas => ocupadas.Count;

        public event Action<Celda, int> CeldaOcupada;
        public event Action<Celda, int> CeldaLiberada;

        public Rejilla(float tamCelda)
        {
            if (tamCelda <= 0f) throw new ArgumentOutOfRangeException(nameof(tamCelda));
            TamCelda = tamCelda;
        }

        public Celda CeldaDesdeMundo(float x, float z)
        {
            return new Celda((int)Math.Floor(x / TamCelda), (int)Math.Floor(z / TamCelda));
        }

        public void CentroDeCelda(Celda celda, out float x, out float z)
        {
            x = (celda.X + 0.5f) * TamCelda;
            z = (celda.Z + 0.5f) * TamCelda;
        }

        public bool EstaLibre(Celda celda) => !ocupadas.ContainsKey(celda);

        public bool Ocupante(Celda celda, out int id) => ocupadas.TryGetValue(celda, out id);

        /// <summary>Ocupa la celda y devuelve el id de la pieza, o 0 si ya estaba ocupada.</summary>
        public int Ocupar(Celda celda)
        {
            if (ocupadas.ContainsKey(celda)) return 0;
            int id = siguienteId++;
            ocupadas.Add(celda, id);
            CeldaOcupada?.Invoke(celda, id);
            return id;
        }

        /// <summary>¿Están libres todas las celdas del rectángulo que empieza en `origen`?</summary>
        public bool AreaLibre(Celda origen, int ancho, int fondo)
        {
            for (int x = 0; x < ancho; x++)
            {
                for (int z = 0; z < fondo; z++)
                {
                    if (ocupadas.ContainsKey(new Celda(origen.X + x, origen.Z + z))) return false;
                }
            }
            return true;
        }

        /// <summary>Ocupa un rectángulo entero con un mismo id. Devuelve 0 si alguna celda estaba ocupada.</summary>
        public int OcuparArea(Celda origen, int ancho, int fondo)
        {
            if (ancho <= 0 || fondo <= 0 || !AreaLibre(origen, ancho, fondo)) return 0;
            int id = siguienteId++;
            for (int x = 0; x < ancho; x++)
            {
                for (int z = 0; z < fondo; z++)
                {
                    var celda = new Celda(origen.X + x, origen.Z + z);
                    ocupadas.Add(celda, id);
                    CeldaOcupada?.Invoke(celda, id);
                }
            }
            return id;
        }

        /// <summary>Libera todas las celdas que ocupa un id. Devuelve cuántas eran.</summary>
        public int LiberarId(int id)
        {
            if (id == 0) return 0;
            var celdas = new List<Celda>();
            foreach (KeyValuePair<Celda, int> par in ocupadas)
            {
                if (par.Value == id) celdas.Add(par.Key);
            }
            foreach (Celda celda in celdas)
            {
                ocupadas.Remove(celda);
                CeldaLiberada?.Invoke(celda, id);
            }
            return celdas.Count;
        }

        /// <summary>Centro en el mundo de un rectángulo de celdas.</summary>
        public void CentroDeArea(Celda origen, int ancho, int fondo, out float x, out float z)
        {
            x = (origen.X + ancho * 0.5f) * TamCelda;
            z = (origen.Z + fondo * 0.5f) * TamCelda;
        }

        /// <summary>Libera la celda y devuelve el id de la pieza que había, o 0 si estaba libre.</summary>
        public int Liberar(Celda celda)
        {
            if (!ocupadas.TryGetValue(celda, out int id)) return 0;
            ocupadas.Remove(celda);
            CeldaLiberada?.Invoke(celda, id);
            return id;
        }
    }
}
