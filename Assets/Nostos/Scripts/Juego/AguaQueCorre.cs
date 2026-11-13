using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Mueve despacio el dibujo del agua del río para que parezca que corre. Trabaja sobre una
    /// copia del material al jugar, así no toca el material guardado.
    /// </summary>
    public class AguaQueCorre : MonoBehaviour
    {
        public Vector2 velocidad = new Vector2(0.012f, 0.03f);

        Material material;

        void Start()
        {
            var render = GetComponent<Renderer>();
            if (render != null && render.sharedMaterials.Length > 0) material = render.materials[0];
        }

        void Update()
        {
            if (material != null) material.SetTextureOffset("_BaseMap", velocidad * Time.time);
        }
    }
}
