using UnityEngine;

namespace Nostos
{
    /// <summary>Dibuja las líneas de la rejilla sobre el suelo, solo en modo constructor.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RejillaVisual : MonoBehaviour
    {
        public ControlModo control;
        public float tamCelda = 2f;
        public int celdasPorLado = 80;
        public float altura = 0.03f;

        MeshRenderer render;

        void Awake()
        {
            render = GetComponent<MeshRenderer>();
            GetComponent<MeshFilter>().sharedMesh = CrearMalla();
        }

        void LateUpdate()
        {
            bool visible = control != null && control.Modo == ModoJuego.Constructor;
            if (render.enabled != visible) render.enabled = visible;
        }

        Mesh CrearMalla()
        {
            int lineas = celdasPorLado + 1;
            float mitad = celdasPorLado * tamCelda * 0.5f;
            var vertices = new Vector3[lineas * 4];
            var indices = new int[lineas * 4];

            for (int i = 0; i < lineas; i++)
            {
                float d = -mitad + i * tamCelda;
                int v = i * 4;
                vertices[v] = new Vector3(d, altura, -mitad);
                vertices[v + 1] = new Vector3(d, altura, mitad);
                vertices[v + 2] = new Vector3(-mitad, altura, d);
                vertices[v + 3] = new Vector3(mitad, altura, d);
                indices[v] = v;
                indices[v + 1] = v + 1;
                indices[v + 2] = v + 2;
                indices[v + 3] = v + 3;
            }

            var malla = new Mesh { name = "Rejilla" };
            malla.vertices = vertices;
            malla.SetIndices(indices, MeshTopology.Lines, 0);
            malla.RecalculateBounds();
            return malla;
        }
    }
}
