using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos
{
    /// <summary>
    /// El modelo de Nicias. En primera persona la cámara está dentro de su cabeza, así que el
    /// cuerpo solo proyecta sombra; cuando la cámara se aleja (vista de constructor, título)
    /// se ve entero.
    /// </summary>
    public class CuerpoNicias : MonoBehaviour
    {
        public Transform cabeza;
        public Transform camara;
        public GameObject modelo;
        [Tooltip("A menos de esta distancia de la cabeza, el cuerpo no se dibuja.")]
        public float distanciaOculto = 0.9f;

        Renderer[] renders;
        bool visible = true;
        Animator animador;
        CharacterController cuerpo;
        static readonly int Velocidad = Animator.StringToHash("Velocidad");

        void Awake()
        {
            renders = modelo != null ? modelo.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            animador = modelo != null ? modelo.GetComponentInChildren<Animator>() : null;
            cuerpo = GetComponent<CharacterController>();
            if (animador != null)
            {
                animador.applyRootMotion = false;
                animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        void Update()
        {
            if (animador == null || animador.runtimeAnimatorController == null || cuerpo == null) return;
            Vector3 v = cuerpo.velocity;
            v.y = 0f;
            animador.SetFloat(Velocidad, v.magnitude, 0.12f, Time.deltaTime);
        }

        void LateUpdate()
        {
            if (cabeza == null || camara == null) return;
            bool lejos = (camara.position - cabeza.position).sqrMagnitude > distanciaOculto * distanciaOculto;
            if (lejos == visible) return;
            visible = lejos;
            foreach (Renderer r in renders)
            {
                if (r != null) r.shadowCastingMode = lejos ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            }
        }
    }
}
