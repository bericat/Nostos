using System.Collections;
using System.IO;
using Unity.Cinemachine;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Capturas para comparar el juego con la referencia artística (CliffStonePackM): con la partida
    /// en marcha (hierba, luz del día, niebla y fuego tal cual se ven al jugar) pone la cámara
    /// principal en cada vista y guarda la imagen en Art/References/benchmark. Solo actúa cuando el
    /// editor lo pide (menú Nostos > Capturas de comparación); al jugar la escena a mano no hace nada.
    /// </summary>
    public class CapturasReferencia : MonoBehaviour
    {
        [System.Serializable]
        public struct Vista
        {
            public string nombre;
            public Vector3 posicion;
            public Vector3 mira;
            public float campo;
        }

        public const string ClavePedidas = "Nostos.Capturando";
        public const string ClaveHechas = "Nostos.CapturasHechas";

        public Vista[] vistas = new Vista[0];
        public string carpeta = "Art/References/benchmark";
        public int ancho = 1600;
        public int alto = 900;
        public float hora = 11f;

        IEnumerator Start()
        {
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool(ClavePedidas, false)) yield break;
            // Que arranque todo (colonos, hierba, cielo) antes de mirar.
            yield return new WaitForSecondsRealtime(3f);
            Juego juego = Juego.Instancia;
            if (juego != null && juego.Mundo != null) juego.Mundo.Ciclo.Fijar(hora, 1);
            yield return new WaitForSecondsRealtime(1.5f);

            Camera camara = Camera.main;
            if (camara == null)
            {
                Debug.LogWarning("Nostos: no hay cámara principal para las capturas.");
                UnityEditor.SessionState.SetBool(ClaveHechas, true);
                yield break;
            }
            var cerebro = camara.GetComponent<CinemachineBrain>();
            if (cerebro != null) cerebro.enabled = false;
            float campoAntes = camara.fieldOfView;
            var rt = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", carpeta));
            Directory.CreateDirectory(dir);
            foreach (Vista v in vistas)
            {
                camara.transform.SetPositionAndRotation(v.posicion, Quaternion.LookRotation(v.mira - v.posicion));
                camara.fieldOfView = v.campo;
                camara.targetTexture = rt;
                // Un fotograma para que la hierba y las sombras se pongan alrededor de la cámara nueva.
                yield return null;
                yield return null;
                yield return new WaitForEndOfFrame();
                RenderTexture previa = RenderTexture.active;
                RenderTexture.active = rt;
                var imagen = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
                imagen.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
                imagen.Apply();
                RenderTexture.active = previa;
                File.WriteAllBytes(Path.Combine(dir, v.nombre + ".png"), imagen.EncodeToPNG());
                Destroy(imagen);
                camara.targetTexture = null;
            }
            rt.Release();
            Destroy(rt);
            camara.fieldOfView = campoAntes;
            if (cerebro != null) cerebro.enabled = true;
            Debug.Log("Nostos: " + vistas.Length + " capturas de comparación en " + dir + ".");
            UnityEditor.SessionState.SetBool(ClaveHechas, true);
#else
            yield break;
#endif
        }
    }
}
