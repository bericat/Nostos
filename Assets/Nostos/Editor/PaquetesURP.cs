using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Pasa a URP los materiales de los paquetes de la tienda que vienen con shaders del render
    /// antiguo (salen rosas). Conserva su aspecto: mismas texturas, mismos colores, mismo tipo de
    /// mezcla. Solo toca materiales cuyo shader no es de URP; se puede repetir sin miedo.
    /// Menú Nostos > Pasar paquetes a URP. ConstructorEscena lo llama al montar la escena.
    /// </summary>
    public static class PaquetesURP
    {
        /// <summary>Carpetas de paquetes que se revisan.</summary>
        public static readonly string[] Carpetas =
        {
            "Assets/PolyOne/Fire Effects",
            "Assets/Stylized Tools",
            "Assets/Propsybrush/Pickaxe",
            "Assets/Holy Assets Games/3D The Blacksmith's House",
            "Assets/RealisticMeshDeformation",
        };

        [MenuItem("Nostos/Pasar paquetes a URP")]
        public static void PasarTodo()
        {
            int cambiados = 0;
            var vistos = new List<string>();
            foreach (string carpeta in Carpetas)
            {
                if (!AssetDatabase.IsValidFolder(carpeta)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { carpeta }))
                {
                    string ruta = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                    if (material == null) continue;
                    string antes = material.shader != null ? material.shader.name : "(ninguno)";
                    if (!Pasar(material)) continue;
                    cambiados++;
                    vistos.Add(ruta + ": " + antes + " -> " + material.shader.name);
                }
            }
            if (cambiados > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Nostos: " + cambiados + " materiales de paquetes pasados a URP.\n" + string.Join("\n", vistos));
            }
        }

        /// <summary>Pasa un material a URP si su shader es del render antiguo. Devuelve true si lo cambió.</summary>
        public static bool Pasar(Material m)
        {
            if (m == null || m.shader == null) return false;
            string viejo = m.shader.name;
            if (viejo.StartsWith("Universal Render Pipeline") || viejo.StartsWith("Shader Graphs/")
                || viejo.StartsWith("Nostos/") || viejo.StartsWith("Shader Forge/")) return false;

            if (viejo.Contains("Particles")) return AParticula(m, viejo);
            if (viejo.StartsWith("Unlit/") || viejo.Contains("/Unlit")) return AUnlit(m);
            if (viejo.StartsWith("Standard") || viejo.StartsWith("Legacy Shaders/") || viejo.StartsWith("Mobile/")
                || viejo.StartsWith("Autodesk") || viejo == "Diffuse" || viejo.StartsWith("Bumped")
                || viejo.StartsWith("Hidden/InternalErrorShader")) return ALit(m, viejo);
            return false;
        }

        // ------------------------------------------------------------------ Lit

        static bool ALit(Material m, string viejo)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return false;

            Texture albedo = Tex(m, "_MainTex");
            Vector2 escala = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 desfase = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture normal = Tex(m, "_BumpMap");
            float fuerzaNormal = Num(m, "_BumpScale", 1f);
            Texture metal = Tex(m, "_MetallicGlossMap");
            float metalico = Num(m, "_Metallic", 0f);
            bool especular = viejo.Contains("Specular");
            Texture mapaEspecular = Tex(m, "_SpecGlossMap");
            Color colorEspecular = m.HasProperty("_SpecColor") ? m.GetColor("_SpecColor") : new Color(0.2f, 0.2f, 0.2f);
            bool hayMapa = especular ? mapaEspecular != null : metal != null;
            // En Standard, con mapa manda _GlossMapScale; sin mapa, _Glossiness. Los legacy no tienen.
            float liso = hayMapa ? Num(m, "_GlossMapScale", 1f) : Num(m, "_Glossiness", viejo.StartsWith("Standard") ? 0.5f : 0.1f);
            Texture oclusion = Tex(m, "_OcclusionMap");
            float fuerzaOclusion = Num(m, "_OcclusionStrength", 1f);
            Texture emision = Tex(m, "_EmissionMap");
            Color colorEmision = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            bool emite = m.IsKeywordEnabled("_EMISSION") && (emision != null || colorEmision.maxColorComponent > 0.01f);
            float modo = Num(m, "_Mode", 0f);          // Standard: 0 opaco, 1 recorte, 2 fundido, 3 transparente
            float corte = Num(m, "_Cutoff", 0.5f);
            if (viejo.Contains("Cutout")) modo = 1f;
            if (viejo.Contains("Transparent") && !viejo.Contains("Cutout")) modo = 2f;

            m.shader = lit;
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", albedo);
            m.SetTextureScale("_BaseMap", escala);
            m.SetTextureOffset("_BaseMap", desfase);
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", albedo);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);

            m.SetFloat("_WorkflowMode", especular ? 0f : 1f);
            if (especular)
            {
                m.EnableKeyword("_SPECULAR_SETUP");
                m.SetColor("_SpecColor", colorEspecular);
                if (mapaEspecular != null)
                {
                    m.SetTexture("_SpecGlossMap", mapaEspecular);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
            }
            else
            {
                m.SetFloat("_Metallic", metalico);
                if (metal != null)
                {
                    m.SetTexture("_MetallicGlossMap", metal);
                    m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
            }
            m.SetFloat("_Smoothness", liso);
            m.SetFloat("_SmoothnessTextureChannel", 0f);

            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", fuerzaNormal);
                m.EnableKeyword("_NORMALMAP");
            }
            if (oclusion != null)
            {
                m.SetTexture("_OcclusionMap", oclusion);
                m.SetFloat("_OcclusionStrength", fuerzaOclusion);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            if (emite)
            {
                m.SetTexture("_EmissionMap", emision);
                m.SetColor("_EmissionColor", colorEmision);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            if (modo >= 1.5f) Transparente(m, false);
            else Opaco(m, modo >= 0.5f, corte);
            EditorUtility.SetDirty(m);
            return true;
        }

        // ------------------------------------------------------------------ Unlit

        static bool AUnlit(Material m)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return false;
            Texture albedo = Tex(m, "_MainTex");
            Vector2 escala = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 desfase = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            bool recorte = m.shader.name.Contains("Cutout");
            bool transparente = m.shader.name.Contains("Transparent") && !recorte;
            float corte = Num(m, "_Cutoff", 0.5f);

            m.shader = unlit;
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", albedo);
            m.SetTextureScale("_BaseMap", escala);
            m.SetTextureOffset("_BaseMap", desfase);
            m.SetColor("_BaseColor", color);
            if (transparente) Transparente(m, false);
            else Opaco(m, recorte, corte);
            EditorUtility.SetDirty(m);
            return true;
        }

        // ------------------------------------------------------------------ partículas

        static bool AParticula(Material m, string viejo)
        {
            Shader particulas = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particulas == null) return false;
            Texture textura = Tex(m, "_MainTex");
            Vector2 escala = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 desfase = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            // Los de "Particles/" multiplican por _TintColor x 2; los "Mobile/Particles/" no tienen tinte.
            Color tinte = m.HasProperty("_TintColor") ? m.GetColor("_TintColor") * 2f : Color.white;
            if (m.HasProperty("_Color") && !m.HasProperty("_TintColor")) tinte = m.GetColor("_Color");
            tinte.a = Mathf.Clamp01(tinte.a);
            bool aditivo = viejo.Contains("Additive");
            bool multiplica = viejo.Contains("Multiply");

            m.shader = particulas;
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", textura);
            m.SetTextureScale("_BaseMap", escala);
            m.SetTextureOffset("_BaseMap", desfase);
            m.SetColor("_BaseColor", tinte);
            m.SetFloat("_ColorMode", 0f);   // multiplicar por el color de la partícula, como antes
            Transparente(m, aditivo, multiplica);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(m);
            return true;
        }

        /// <summary>Material de partículas URP sin luz (humo, chispas), transparente o aditivo.</summary>
        public static Material NuevoDeParticulas(string nombre, Texture textura, bool aditivo)
        {
            Shader particulas = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particulas == null) return null;
            var m = new Material(particulas) { name = nombre };
            m.SetTexture("_BaseMap", textura);
            m.SetColor("_BaseColor", Color.white);
            Transparente(m, aditivo);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
            return m;
        }

        // ------------------------------------------------------------------ mezcla

        static void Opaco(Material m, bool recorte, float corte)
        {
            m.SetFloat("_Surface", 0f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            Opcional(m, "_SrcBlendAlpha", (float)BlendMode.One);
            Opcional(m, "_DstBlendAlpha", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            m.SetFloat("_AlphaClip", recorte ? 1f : 0f);
            m.SetFloat("_Cutoff", corte);
            m.SetOverrideTag("RenderType", recorte ? "TransparentCutout" : "Opaque");
            m.renderQueue = recorte ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (recorte) m.EnableKeyword("_ALPHATEST_ON");
            m.SetShaderPassEnabled("ShadowCaster", true);
            m.SetShaderPassEnabled("DepthOnly", true);
        }

        static void Transparente(Material m, bool aditivo, bool multiplica = false)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", aditivo ? 2f : multiplica ? 3f : 0f);
            BlendMode origen = BlendMode.SrcAlpha, destino = BlendMode.OneMinusSrcAlpha;
            if (aditivo) destino = BlendMode.One;
            if (multiplica)
            {
                origen = BlendMode.DstColor;
                destino = BlendMode.Zero;
                m.EnableKeyword("_ALPHAMODULATE_ON");
            }
            m.SetFloat("_SrcBlend", (float)origen);
            m.SetFloat("_DstBlend", (float)destino);
            Opcional(m, "_SrcBlendAlpha", aditivo ? (float)BlendMode.One : (float)BlendMode.One);
            Opcional(m, "_DstBlendAlpha", aditivo ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            Opcional(m, "_AlphaClip", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
        }

        static void Opcional(Material m, string propiedad, float valor)
        {
            if (m.HasProperty(propiedad)) m.SetFloat(propiedad, valor);
        }

        static Texture Tex(Material m, string propiedad) => m.HasProperty(propiedad) ? m.GetTexture(propiedad) : null;

        static float Num(Material m, string propiedad, float porDefecto) =>
            m.HasProperty(propiedad) ? m.GetFloat(propiedad) : porDefecto;
    }
}
