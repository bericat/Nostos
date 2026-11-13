using System;
using System.Collections.Generic;
using System.IO;
using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Guardado en JSON del estado de la simulación (no de los objetos de escena): al cargar,
    /// los edificios se vuelven a crear a partir de sus datos. Los árboles, vetas y arbustos
    /// no se guardan: reaparecen enteros.
    /// </summary>
    public static class Guardado
    {
        [Serializable]
        public class EdificioGuardado
        {
            public string id;
            public int x;
            public int z;
            public int estado;
            public int[] entregado;
            public int progreso;
            public int[] entrada;
            public int[] salida;
            public int afinado;
            public int lotesDobles;
            public int confort;
            public int diaFaena;
        }

        [Serializable]
        public class ColonoGuardado
        {
            public string nombre;
            public float confianza;
            public bool conAcuerdo;
            public int[] palancas;
            public int casa = -1;
            public int encargoRecurso;
            public int encargoCantidad;
            public bool encargoPendiente;
            public bool hambriento;
        }

        [Serializable]
        public class ModuloGuardado
        {
            public int[] entregado;
            public bool completo;
            public bool probado;
        }

        [Serializable]
        public class Partida
        {
            public int version = 1;
            public float hora;
            public int dia;
            public int[] mochila;
            public int[] almacen;
            public Vector3 posicionJugador;
            public List<EdificioGuardado> edificios = new List<EdificioGuardado>();
            public List<ColonoGuardado> colonos = new List<ColonoGuardado>();
            public List<ModuloGuardado> modulos = new List<ModuloGuardado>();
            public bool vueloDePrueba;
            public int tributoDia;
            public int tributoVisitas;
            public bool arcontePresente;
            public List<string> narrativa = new List<string>();
            /// <summary>Celdas de camino, por pares x, z.</summary>
            public List<int> caminos = new List<int>();
        }

        public static string Ruta => Path.Combine(Application.persistentDataPath, "nostos_partida.json");

        public static bool HayPartida => File.Exists(Ruta);

        public static void Guardar(Juego juego, Transform jugador)
        {
            Mundo mundo = juego.Mundo;
            mundo.Colonos.SoltarTodo();

            var partida = new Partida
            {
                hora = mundo.Ciclo.Hora,
                dia = mundo.Ciclo.Dia,
                mochila = mundo.Mochila.Volcar(),
                almacen = mundo.Almacen.Volcar(),
                posicionJugador = jugador != null ? jugador.position : Vector3.zero,
                tributoDia = mundo.Tributo.DiaProximo,
                tributoVisitas = mundo.Tributo.Visitas,
                arcontePresente = mundo.Tributo.ArcontePresente
            };

            var indices = new Dictionary<Edificio, int>();
            foreach (Edificio e in mundo.Edificios)
            {
                indices[e] = partida.edificios.Count;
                partida.edificios.Add(new EdificioGuardado
                {
                    id = e.Datos.Id,
                    x = e.Origen.X,
                    z = e.Origen.Z,
                    estado = (int)e.Estado,
                    entregado = e.VolcarEntregado(),
                    progreso = e.Progreso,
                    entrada = e.Entrada.Volcar(),
                    salida = e.Salida.Volcar(),
                    afinado = e.TicksAfinado,
                    lotesDobles = e.LotesDobles,
                    confort = e.Confort,
                    diaFaena = e.DiaUltimaFaena
                });
            }

            foreach (Celda celda in mundo.Caminos)
            {
                partida.caminos.Add(celda.X);
                partida.caminos.Add(celda.Z);
            }

            foreach (Colono c in mundo.Colonos.Colonos)
            {
                partida.colonos.Add(new ColonoGuardado
                {
                    nombre = c.Nombre,
                    confianza = c.Confianza,
                    conAcuerdo = c.Acuerdo != null,
                    palancas = c.Acuerdo != null ? (int[])c.Acuerdo.Niveles.Clone() : null,
                    casa = c.Casa != null && indices.ContainsKey(c.Casa) ? indices[c.Casa] : -1,
                    encargoRecurso = (int)c.Encargo.recurso,
                    encargoCantidad = c.Encargo.cantidad,
                    encargoPendiente = c.EncargoPendiente,
                    hambriento = c.Hambriento
                });
            }

            foreach (ModuloNave m in mundo.Nave.Modulos)
            {
                partida.modulos.Add(new ModuloGuardado { entregado = (int[])m.Entregado.Clone(), completo = m.Completo, probado = m.Probado });
            }
            partida.vueloDePrueba = mundo.Nave.VueloDePrueba;

            if (Narrativa.Instancia != null) partida.narrativa = Narrativa.Instancia.Volcar();

            File.WriteAllText(Ruta, JsonUtility.ToJson(partida, true));
        }

        /// <summary>Carga la partida guardada. Devuelve false si no hay o no se puede leer.</summary>
        public static bool Cargar(Juego juego, Transform jugador)
        {
            if (!HayPartida) return false;
            Partida partida;
            try
            {
                partida = JsonUtility.FromJson<Partida>(File.ReadAllText(Ruta));
            }
            catch (Exception error)
            {
                Debug.LogWarning("Nostos: no se pudo leer la partida guardada. " + error.Message);
                return false;
            }
            if (partida == null) return false;

            Mundo mundo = juego.Mundo;
            mundo.Colonos.SoltarTodo();
            juego.DemolerTodo();

            mundo.Ciclo.Fijar(partida.hora, partida.dia);
            mundo.Sincronizar();
            mundo.Mochila.Cargar(partida.mochila);
            mundo.Almacen.Cargar(partida.almacen);
            mundo.Tributo.DiaProximo = partida.tributoDia;
            mundo.Tributo.Visitas = partida.tributoVisitas;
            mundo.Tributo.ArcontePresente = partida.arcontePresente;

            var creados = new List<Edificio>();
            foreach (EdificioGuardado g in partida.edificios)
            {
                DefinicionEdificio definicion = juego.Definicion(g.id);
                EdificioEnMundo edificio = definicion != null ? juego.Construir(definicion, new Celda(g.x, g.z)) : null;
                creados.Add(edificio != null ? edificio.Sim : null);
                if (edificio == null) continue;
                edificio.Sim.Restaurar((EstadoObra)Mathf.Clamp(g.estado, 0, 3), g.entregado, g.progreso);
                edificio.Sim.Entrada.Cargar(g.entrada);
                edificio.Sim.Salida.Cargar(g.salida);
                edificio.Sim.RestaurarFaenas(g.afinado, g.lotesDobles, g.confort, g.diaFaena);
            }

            mundo.BorrarCaminos();
            if (partida.caminos != null)
            {
                for (int i = 0; i + 1 < partida.caminos.Count; i += 2)
                {
                    mundo.PonerCamino(new Celda(partida.caminos[i], partida.caminos[i + 1]));
                }
            }

            foreach (ColonoGuardado g in partida.colonos)
            {
                foreach (Colono c in mundo.Colonos.Colonos)
                {
                    if (c.Nombre != g.nombre) continue;
                    c.FijarConfianza(g.confianza);
                    c.Acuerdo = null;
                    if (g.conAcuerdo)
                    {
                        c.Acuerdo = new Acuerdo();
                        for (int i = 0; i < Acuerdo.NumPalancas && g.palancas != null && i < g.palancas.Length; i++)
                        {
                            c.Acuerdo.Niveles[i] = Mathf.Clamp(g.palancas[i], 0, 2);
                        }
                    }
                    c.Casa = g.casa >= 0 && g.casa < creados.Count ? creados[g.casa] : null;
                    c.Encargo = new Cantidad((Recurso)Mathf.Clamp(g.encargoRecurso, 0, Recursos.Numero - 1), g.encargoCantidad);
                    c.EncargoPendiente = g.encargoPendiente;
                    c.Hambriento = g.hambriento;
                }
            }

            ModuloNave[] modulos = mundo.Nave.Modulos;
            for (int i = 0; i < modulos.Length; i++)
            {
                bool hay = i < partida.modulos.Count;
                modulos[i].Completo = hay && partida.modulos[i].completo;
                modulos[i].Probado = hay && partida.modulos[i].probado;
                for (int r = 0; r < modulos[i].Entregado.Length; r++)
                {
                    int[] guardado = hay ? partida.modulos[i].entregado : null;
                    modulos[i].Entregado[r] = guardado != null && r < guardado.Length ? guardado[r] : 0;
                }
            }
            mundo.Nave.VueloDePrueba = partida.vueloDePrueba;

            if (jugador != null)
            {
                var cuerpo = jugador.GetComponent<CharacterController>();
                if (cuerpo != null) cuerpo.enabled = false;
                jugador.position = partida.posicionJugador + Vector3.up * 0.1f;
                if (cuerpo != null) cuerpo.enabled = true;
            }
            if (Narrativa.Instancia != null) Narrativa.Instancia.Cargar(partida.narrativa);
            return true;
        }
    }
}
