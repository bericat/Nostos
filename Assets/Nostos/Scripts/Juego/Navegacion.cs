using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Nostos
{
    /// <summary>
    /// Genera el NavMesh del llano al empezar y lo actualiza (sin parar el juego) cada vez que
    /// cambia un edificio. Solo cubre un volumen alrededor del asentamiento para que sea rápido.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class Navegacion : MonoBehaviour
    {
        public Juego juego;
        public float retraso = 0.5f;

        NavMeshSurface superficie;
        AsyncOperation actualizacion;
        float actualizarEn = -1f;

        void Awake()
        {
            superficie = GetComponent<NavMeshSurface>();
            superficie.BuildNavMesh();
        }

        void OnEnable()
        {
            if (juego != null) juego.EdificiosCambiados += Pedir;
        }

        void OnDisable()
        {
            if (juego != null) juego.EdificiosCambiados -= Pedir;
        }

        void Pedir()
        {
            actualizarEn = Time.time + retraso;
        }

        void Update()
        {
            if (actualizarEn < 0f || Time.time < actualizarEn) return;
            if (actualizacion != null && !actualizacion.isDone) return;
            actualizarEn = -1f;
            if (superficie.navMeshData == null) superficie.BuildNavMesh();
            else actualizacion = superficie.UpdateNavMesh(superficie.navMeshData);
        }
    }
}
