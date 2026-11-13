using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Importa los modelos generados por Pipeline/generar_kit.py (kit, edificios, naturaleza,
    /// nave) con el material de paleta compartido, sin materiales propios, y con colisionador.
    /// Solo afecta a lo que hay dentro de Assets/Nostos/Arte.
    /// </summary>
    public class ImportadorKit : AssetPostprocessor
    {
        public const string CarpetaArte = "Assets/Nostos/Arte";
        public const string CarpetaKit = CarpetaArte + "/Kit";
        public const string RutaTextura = CarpetaKit + "/Paleta.png";
        public const string RutaMaterial = "Assets/Nostos/Arte/Materiales/Paleta.mat";

        // Subir este número fuerza la reimportación de todo el kit.
        public override uint GetVersion() => 3;

        static bool EsDelKit(string ruta)
        {
            return ruta.Replace('\\', '/').StartsWith(CarpetaArte + "/");
        }

        void OnPreprocessTexture()
        {
            // Imágenes de la interfaz (portada, HUD, menú de obras): nítidas, sin reescalar y con
            // la transparencia bien tratada en los bordes.
            if (assetPath.Replace('\\', '/').StartsWith("Assets/Nostos/Resources/UI/") && !assetPath.Contains("/Fuentes/"))
            {
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Default;
                ui.sRGBTexture = true;
                ui.alphaIsTransparency = true;
                ui.mipmapEnabled = false;
                ui.wrapMode = TextureWrapMode.Clamp;
                ui.filterMode = FilterMode.Bilinear;
                ui.npotScale = TextureImporterNPOTScale.None;
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (assetPath != RutaTextura) return;
            var importador = (TextureImporter)assetImporter;
            importador.textureType = TextureImporterType.Default;
            importador.sRGBTexture = true;
            importador.mipmapEnabled = false;
            importador.filterMode = FilterMode.Point;
            importador.wrapMode = TextureWrapMode.Clamp;
            importador.textureCompression = TextureImporterCompression.Uncompressed;
            importador.npotScale = TextureImporterNPOTScale.None;
        }

        void OnPreprocessModel()
        {
            if (!EsDelKit(assetPath)) return;
            var importador = (ModelImporter)assetImporter;
            importador.materialImportMode = ModelImporterMaterialImportMode.None;
            importador.importAnimation = false;
            importador.animationType = ModelImporterAnimationType.None;
            importador.importCameras = false;
            importador.importLights = false;
            importador.importBlendShapes = false;
            // Las manchas del suelo del bosque y las matas se atraviesan: sin colisionador.
            string archivo = System.IO.Path.GetFileName(assetPath);
            importador.addCollider = !(archivo.StartsWith("suelo_") || archivo.StartsWith("mata_"));
            importador.globalScale = 1f;
            importador.useFileScale = true;
        }

        void OnPostprocessModel(GameObject raiz)
        {
            if (!EsDelKit(assetPath)) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
            if (material == null) return; // Se crea en OnPostprocessAllAssets y se reimporta.

            foreach (Renderer render in raiz.GetComponentsInChildren<Renderer>(true))
            {
                var materiales = new Material[render.sharedMaterials.Length];
                for (int i = 0; i < materiales.Length; i++) materiales[i] = material;
                render.sharedMaterials = materiales;
            }
        }

        static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] movidosDesde)
        {
            var modelos = new List<string>();
            bool hayPaleta = false;
            foreach (string ruta in importados)
            {
                if (!EsDelKit(ruta)) continue;
                if (ruta == RutaTextura) hayPaleta = true;
                else if (ruta.ToLowerInvariant().EndsWith(".fbx")) modelos.Add(ruta);
            }
            if (!hayPaleta && modelos.Count == 0) return;

            bool existia = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial) != null;
            Material material = AsegurarMaterial();
            if (material == null || existia) return;

            // El material no existía cuando se importaron estos modelos: se reimportan una vez.
            foreach (string ruta in modelos) AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>Crea el material de paleta si falta y le asigna la textura. Devuelve null si aún no hay textura.</summary>
        public static Material AsegurarMaterial()
        {
            var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTextura);
            var material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);

            if (material == null)
            {
                if (textura == null) return null;
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader) { name = "Paleta" };
                material.SetFloat("_Smoothness", 0.15f);
                CarpetasNostos.Asegurar("Assets/Nostos/Arte/Materiales");
                AssetDatabase.CreateAsset(material, RutaMaterial);
            }

            if (textura != null && material.mainTexture != textura)
            {
                material.mainTexture = textura;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", textura);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            return material;
        }

        /// <summary>Reimporta paleta y modelos del kit. Útil tras cambiar este importador.</summary>
        [MenuItem("Nostos/Reimportar kit")]
        public static void ReimportarKit()
        {
            if (!AssetDatabase.IsValidFolder(CarpetaArte)) return;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(RutaTextura) != null)
            {
                AssetDatabase.ImportAsset(RutaTextura, ImportAssetOptions.ForceUpdate);
            }
            AsegurarMaterial();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CarpetaArte }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }
    }

    public static class CarpetasNostos
    {
        public static void Asegurar(string carpeta)
        {
            if (AssetDatabase.IsValidFolder(carpeta)) return;
            int corte = carpeta.LastIndexOf('/');
            string padre = carpeta.Substring(0, corte);
            Asegurar(padre);
            AssetDatabase.CreateFolder(padre, carpeta.Substring(corte + 1));
        }
    }
}
