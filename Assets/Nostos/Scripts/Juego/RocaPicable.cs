using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos
{
    /// <summary>
    /// Roca o veta que se abolla al picarla: cada golpe hunde los vértices cerca del punto donde da
    /// el pico (la misma cuenta que "Realistic Mesh Deformation", que solo se dispara con choques de
    /// física) y salta un puñado de esquirlas de la propia roca con algo de polvo. Al último golpe
    /// se parte en trozos que caen rodando.
    /// </summary>
    public class RocaPicable : MonoBehaviour
    {
        [Tooltip("Radio de cada golpe, en proporción al tamaño de la roca.")]
        public float radio = 0.4f;
        [Tooltip("Lo que se hunde cada golpe, en proporción al tamaño de la roca.")]
        public float hondo = 0.12f;
        [Tooltip("Ningún vértice baja de esta parte de su distancia al centro: la roca no se da la vuelta.")]
        public float minimo = 0.4f;
        [Tooltip("Material de partículas para el polvo (el del humo).")]
        public Material polvo;
        public Color colorPolvo = new Color(0.62f, 0.58f, 0.52f, 0.55f);

        MeshFilter[] filtros;
        Mesh[] mallas;
        Vector3[][] originales;
        Vector3[][] actuales;
        Vector3 centro;      // centro de la roca, en el mundo, antes del primer golpe
        float tam;           // medio tamaño de la roca, en metros
        bool listo;
        ParticleSystem esquirlas, nube;
        Mesh mallaEsquirla;  // la malla de la roca sin abollar, para las esquirlas

        void Preparar()
        {
            if (listo) return;
            listo = true;
            filtros = GetComponentsInChildren<MeshFilter>();
            mallas = new Mesh[filtros.Length];
            originales = new Vector3[filtros.Length][];
            actuales = new Vector3[filtros.Length][];
            var caja = new Bounds(transform.position, Vector3.zero);
            bool hay = false;
            foreach (Renderer r in GetComponentsInChildren<MeshRenderer>())
            {
                if (!hay) caja = r.bounds;
                else caja.Encapsulate(r.bounds);
                hay = true;
            }
            centro = caja.center;
            tam = Mathf.Max(0.2f, caja.extents.magnitude);
            for (int i = 0; i < filtros.Length; i++)
            {
                Mesh m = filtros[i].sharedMesh;
                if (m == null || !m.isReadable) continue;
                if (mallaEsquirla == null) mallaEsquirla = m;
                mallas[i] = filtros[i].mesh;   // copia propia de esta roca
                originales[i] = mallas[i].vertices;
                actuales[i] = (Vector3[])originales[i].Clone();
            }
        }

        /// <summary>Un golpe de pico en `punto` (mundo), dado en la dirección `direccion`.</summary>
        public void Golpear(Vector3 punto, Vector3 direccion, bool ultimo)
        {
            Preparar();
            Vector3 haciaDentro = (centro - punto).normalized;
            Vector3 dir = (direccion.normalized + haciaDentro).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = haciaDentro;

            float r = radio * tam;
            // Si el golpe no alcanza ningún vértice (cara grande), se agranda hasta el más cercano.
            float masCerca = float.MaxValue;
            for (int i = 0; i < filtros.Length; i++)
            {
                if (actuales[i] == null) continue;
                Transform t = filtros[i].transform;
                foreach (Vector3 v in actuales[i]) masCerca = Mathf.Min(masCerca, (t.TransformPoint(v) - punto).sqrMagnitude);
            }
            if (masCerca < float.MaxValue) r = Mathf.Max(r, Mathf.Sqrt(masCerca) * 1.35f);

            for (int i = 0; i < filtros.Length; i++)
            {
                if (actuales[i] == null) continue;
                Transform t = filtros[i].transform;
                Vector3[] v = actuales[i];
                Vector3[] o = originales[i];
                bool cambio = false;
                for (int k = 0; k < v.Length; k++)
                {
                    Vector3 w = t.TransformPoint(v[k]);
                    float d = Vector3.Distance(w, punto);
                    if (d >= r) continue;
                    float f = 1f - d / r;
                    f = f * f * (3f - 2f * f);
                    // El azar sale de la posición original: los vértices repetidos de una arista
                    // (caras planas) se mueven igual y la malla no se abre.
                    float azar = 0.65f + 0.7f * Ruido(o[k]);
                    Vector3 nuevo = w + dir * (hondo * tam * f * azar);
                    Vector3 origen = t.TransformPoint(o[k]);
                    float tope = minimo * Vector3.Distance(origen, centro);
                    Vector3 desdeCentro = nuevo - centro;
                    if (desdeCentro.magnitude < tope) nuevo = centro + desdeCentro.normalized * tope;
                    v[k] = t.InverseTransformPoint(nuevo);
                    cambio = true;
                }
                if (!cambio) continue;
                mallas[i].vertices = v;
                mallas[i].RecalculateNormals();
                mallas[i].RecalculateBounds();
            }

            Saltar(punto, -dir, ultimo);
        }

        static float Ruido(Vector3 p)
        {
            int x = Mathf.RoundToInt(p.x * 1000f), y = Mathf.RoundToInt(p.y * 1000f), z = Mathf.RoundToInt(p.z * 1000f);
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ z * 83492791;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        // ------------------------------------------------------------------ esquirlas y polvo

        void Saltar(Vector3 punto, Vector3 haciaFuera, bool ultimo)
        {
            CrearParticulas();
            Quaternion giro = Quaternion.LookRotation(haciaFuera + Vector3.up * 0.6f);
            if (esquirlas != null)
            {
                esquirlas.transform.SetPositionAndRotation(punto, giro);
                var main = esquirlas.main;
                float lado = TamMalla();
                float pieza = ultimo ? 0.22f * tam : 0.07f * tam;
                main.startSize = new ParticleSystem.MinMaxCurve(pieza * 0.5f / lado, pieza / lado);
                var forma = esquirlas.shape;
                forma.angle = ultimo ? 70f : 35f;
                forma.radius = ultimo ? 0.5f * tam : 0.05f;
                esquirlas.Emit(ultimo ? 12 : 7 + Random.Range(0, 4));
            }
            if (nube != null)
            {
                nube.transform.SetPositionAndRotation(punto, giro);
                var forma = nube.shape;
                forma.radius = ultimo ? 0.6f * tam : 0.1f;
                nube.Emit(ultimo ? 14 : 5);
            }
            if (ultimo)
            {
                // La roca desaparece ya: los trozos y el polvo se quedan un rato solos.
                foreach (ParticleSystem s in new[] { esquirlas, nube })
                {
                    if (s == null) continue;
                    s.transform.SetParent(null, true);
                    Destroy(s.gameObject, 6f);
                }
                esquirlas = nube = null;
            }
        }

        float TamMalla()
        {
            Mesh m = mallaEsquirla;
            if (m == null) return 1f;
            Vector3 s = m.bounds.size;
            return Mathf.Max(0.001f, Mathf.Max(s.x, s.y, s.z));
        }

        void CrearParticulas()
        {
            if (esquirlas != null || mallaEsquirla == null) return;
            MeshRenderer render = GetComponentInChildren<MeshRenderer>();
            Mesh malla = mallaEsquirla;
            if (render == null) return;

            // Esquirlas: la misma malla y el mismo material de la roca, en pequeño.
            var go = new GameObject("Esquirlas");
            go.transform.SetParent(transform, false);
            esquirlas = go.AddComponent<ParticleSystem>();
            esquirlas.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = esquirlas.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 40;
            var emision = esquirlas.emission;
            emision.enabled = false;
            var forma = esquirlas.shape;
            forma.shapeType = ParticleSystemShapeType.Cone;
            var giro = esquirlas.rotationOverLifetime;
            giro.enabled = true;
            giro.separateAxes = true;
            giro.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            giro.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            giro.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var choque = esquirlas.collision;
            choque.enabled = true;
            choque.type = ParticleSystemCollisionType.World;
            choque.quality = ParticleSystemCollisionQuality.Low;
            choque.bounce = 0.25f;
            choque.dampen = 0.55f;
            choque.radiusScale = 0.4f;
            choque.enableDynamicColliders = false;
            var tamano = esquirlas.sizeOverLifetime;
            tamano.enabled = true;
            tamano.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = malla;
            pr.alignment = ParticleSystemRenderSpace.World;
            pr.sharedMaterial = render.sharedMaterial;
            pr.shadowCastingMode = ShadowCastingMode.On;

            if (polvo == null) return;
            var gn = new GameObject("Polvo");
            gn.transform.SetParent(transform, false);
            nube = gn.AddComponent<ParticleSystem>();
            nube.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var mn = nube.main;
            mn.loop = false;
            mn.playOnAwake = false;
            mn.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            mn.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            mn.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            mn.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            mn.startColor = colorPolvo;
            mn.gravityModifier = -0.03f;
            mn.simulationSpace = ParticleSystemSimulationSpace.World;
            mn.maxParticles = 40;
            var en = nube.emission;
            en.enabled = false;
            var fn = nube.shape;
            fn.shapeType = ParticleSystemShapeType.Cone;
            fn.angle = 50f;
            var tn = nube.sizeOverLifetime;
            tn.enabled = true;
            tn.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
            var cn = nube.colorOverLifetime;
            cn.enabled = true;
            var degradado = new Gradient();
            degradado.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            cn.color = new ParticleSystem.MinMaxGradient(degradado);
            var vn = nube.velocityOverLifetime;
            vn.enabled = true;
            vn.space = ParticleSystemSimulationSpace.World;
            vn.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vn.y = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            vn.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var rn = gn.GetComponent<ParticleSystemRenderer>();
            rn.sharedMaterial = polvo;
            rn.sortMode = ParticleSystemSortMode.Distance;
            rn.shadowCastingMode = ShadowCastingMode.Off;
            rn.receiveShadows = false;
        }
    }
}
