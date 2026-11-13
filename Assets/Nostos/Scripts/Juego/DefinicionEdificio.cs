using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Ficha de un tipo de edificio, editable desde el inspector: tamaño en celdas, coste de
    /// cada estado de obra, modelos de los cuatro estados y, si produce, su receta.
    /// </summary>
    [CreateAssetMenu(menuName = "Nostos/Edificio", fileName = "Edificio")]
    public class DefinicionEdificio : ScriptableObject
    {
        public string id = "edificio";
        public string nombre = "Edificio";
        [Min(1)] public int ancho = 1;
        [Min(1)] public int fondo = 1;
        public TipoEdificio tipo = TipoEdificio.Produccion;

        [Header("Modelos: cimientos, esqueleto, casi terminado, terminado")]
        public GameObject[] modelos = new GameObject[4];

        [Header("Qué pide cada estado de obra para pasar al siguiente")]
        public Cantidad[] costeCimientos = new Cantidad[0];
        public Cantidad[] costeEsqueleto = new Cantidad[0];
        public Cantidad[] costeCasiTerminado = new Cantidad[0];

        [Header("Producción (solo si el tipo es Producción)")]
        public Cantidad[] entradas = new Cantidad[0];
        public Cantidad[] salidas = new Cantidad[0];
        [Min(1)] public int ticksPorCiclo = 4;

        DatosEdificio datos;

        /// <summary>Los mismos datos en la forma que entiende la simulación.</summary>
        public DatosEdificio Datos
        {
            get
            {
                if (datos == null)
                {
                    datos = new DatosEdificio
                    {
                        Id = id,
                        Nombre = nombre,
                        Ancho = ancho,
                        Fondo = fondo,
                        Tipo = tipo,
                        Costes = new[] { costeCimientos, costeEsqueleto, costeCasiTerminado },
                        Receta = tipo == TipoEdificio.Produccion
                            ? new Receta { Entradas = entradas, Salidas = salidas, Ticks = ticksPorCiclo }
                            : null
                    };
                }
                return datos;
            }
        }

        public GameObject Modelo(EstadoObra estado)
        {
            int i = (int)estado;
            return modelos != null && i < modelos.Length ? modelos[i] : null;
        }

        public string CosteTotal()
        {
            var total = new int[Recursos.Numero];
            foreach (Cantidad[] etapa in new[] { costeCimientos, costeEsqueleto, costeCasiTerminado })
            {
                foreach (Cantidad c in etapa) total[(int)c.recurso] += c.cantidad;
            }
            var lista = new Cantidad[Recursos.Numero];
            for (int i = 0; i < total.Length; i++) lista[i] = new Cantidad((Recurso)i, total[i]);
            return Recursos.Lista(lista);
        }

        void OnValidate()
        {
            datos = null;
        }
    }
}
