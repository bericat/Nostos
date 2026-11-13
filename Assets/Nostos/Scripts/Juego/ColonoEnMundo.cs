using Nostos.Sim;
using UnityEngine;
using UnityEngine.AI;

namespace Nostos
{
    /// <summary>
    /// Un colono en el mundo: una cápsula que camina por el NavMesh hacia donde la simulación
    /// dice (obra, almacén, casa, comedor o plaza) y con la que Nicias puede hablar.
    /// </summary>
    public class ColonoEnMundo : MonoBehaviour, IInteractuable
    {
        const float DistanciaLlegada = 1.8f;

        Juego juego;
        NavMeshAgent agente;
        Vector3 objetivo;
        Destino destinoActual;
        bool tieneObjetivo;
        float siguientePaseo;
        float siguienteIntento;
        float alturaCentro = 0.9f;
        float enRuta;

        public Colono Sim { get; private set; }

        public void Iniciar(Juego juego, Colono sim, float altura)
        {
            this.juego = juego;
            Sim = sim;
            alturaCentro = 0.9f * altura;
        }

        public string Texto => Sim != null ? "Hablar con " + Sim.Nombre : null;

        public void Interactuar(GameObject quien)
        {
            PanelColono panel = PanelColono.Instancia;
            if (panel != null) panel.Abrir(this);
        }

        /// <summary>Qué está haciendo, para la etiqueta del HUD.</summary>
        public string Actividad
        {
            get
            {
                if (Sim.Tarea != null)
                {
                    string carga = Sim.Tarea.Carga.Describir();
                    return Sim.Tarea.Tipo == TipoTarea.Llevar
                        ? "lleva " + (carga.Length > 0 ? carga : "material") + " a " + Sim.Tarea.Edificio.Datos.Nombre
                        : (carga.Length > 0 ? "trae " + carga + " al almacén" : "va a recoger a " + Sim.Tarea.Edificio.Datos.Nombre);
                }
                if (Sim.Acuerdo == null) return "sin acuerdo";
                if (Sim.Ausente != null) return Sim.Ausente;
                if (Sim.Hambriento) return "con hambre";
                return juego.Mundo.Colonos.EnHorario(Sim) ? "esperando trabajo" : "descansando";
            }
        }

        void Update()
        {
            if (Sim == null) return;
            AsegurarAgente();

            Destino destino = juego.Mundo.Colonos.DestinoDe(Sim);
            bool cambio = !tieneObjetivo || destino.Tipo != destinoActual.Tipo || destino.Edificio != destinoActual.Edificio;
            bool paseo = destino.Tipo == TipoDestino.Plaza && Time.time >= siguientePaseo;
            if (cambio || paseo)
            {
                destinoActual = destino;
                objetivo = PuntoDe(destino);
                tieneObjetivo = true;
                enRuta = 0f;
                siguientePaseo = Time.time + Random.Range(6f, 14f);
                if (agente != null && agente.isOnNavMesh) agente.SetDestination(objetivo);
            }

            float velocidad = 3.2f * Sim.Productividad;
            if (Caminos.EnCamino(transform.position)) velocidad *= Caminos.VelocidadColonos;
            if (agente != null && agente.isOnNavMesh)
            {
                agente.speed = velocidad;
            }
            else
            {
                // Sin NavMesh (todavía): camina en línea recta para que el juego no se pare.
                Vector3 plano = new Vector3(objetivo.x, transform.position.y, objetivo.z);
                transform.position = Vector3.MoveTowards(transform.position, plano, velocidad * Time.deltaTime);
            }

            Vector3 delta = objetivo - transform.position;
            delta.y = 0f;
            // Si lleva demasiado sin llegar (camino cortado), se da por llegado para no bloquear la obra.
            enRuta += Time.deltaTime;
            bool llegado = delta.magnitude <= DistanciaLlegada || enRuta > 90f;
            if (llegado && Sim.Tarea != null)
            {
                juego.Mundo.Colonos.Llegar(Sim);
                tieneObjetivo = false;
            }
        }

        Vector3 PuntoDe(Destino destino)
        {
            Vector2 azar = Random.insideUnitCircle;
            if (destino.Tipo == TipoDestino.Plaza || destino.Edificio == null)
            {
                Vector2 anillo = azar.normalized * Random.Range(2.2f, 4.5f);
                return juego.PuntoPlaza + new Vector3(anillo.x, 0f, anillo.y);
            }
            return juego.PuntoPuerta(destino.Edificio) + new Vector3(azar.x, 0f, azar.y) * 0.8f;
        }

        void AsegurarAgente()
        {
            if (agente != null || Time.time < siguienteIntento) return;
            siguienteIntento = Time.time + 2f;

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit punto, 6f, NavMesh.AllAreas)) return;
            transform.position = punto.position + Vector3.up * alturaCentro;
            agente = gameObject.AddComponent<NavMeshAgent>();
            agente.radius = 0.35f;
            agente.height = alturaCentro * 2f;
            agente.baseOffset = alturaCentro;
            agente.angularSpeed = 360f;
            agente.acceleration = 12f;
            agente.stoppingDistance = 0.6f;
            agente.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            // Fuera del camino cuesta más andar: así los colonos buscan los caminos trazados.
            agente.SetAreaCost(0, Caminos.CosteCampo);
            agente.SetAreaCost(Caminos.AreaCamino, 1f);
            if (tieneObjetivo && agente.isOnNavMesh) agente.SetDestination(objetivo);
        }
    }
}
