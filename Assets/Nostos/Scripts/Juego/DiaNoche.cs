using UnityEngine;

namespace Nostos
{
    /// <summary>Mueve el sol y cambia la luz ambiente según la hora de la simulación.</summary>
    public class DiaNoche : MonoBehaviour
    {
        public Juego juego;
        public Light sol;
        public float rumbo = -35f;
        public float intensidadDia = 1.3f;

        public Color solMediodia = new Color(1f, 0.96f, 0.88f);
        public Color solHorizonte = new Color(1f, 0.6f, 0.35f);

        public Color cieloDia = new Color(0.55f, 0.66f, 0.78f);
        public Color horizonteDia = new Color(0.52f, 0.5f, 0.46f);
        public Color sueloDia = new Color(0.3f, 0.26f, 0.2f);
        public Color cieloNoche = new Color(0.1f, 0.13f, 0.22f);
        public Color horizonteNoche = new Color(0.05f, 0.06f, 0.1f);
        public Color sueloNoche = new Color(0.03f, 0.03f, 0.05f);
        public Color nieblaDia = new Color(0.72f, 0.78f, 0.82f);
        public Color nieblaNoche = new Color(0.04f, 0.05f, 0.09f);

        void LateUpdate()
        {
            if (juego == null || sol == null || juego.Mundo == null) return;

            // 6:00 amanece, 12:00 mediodía, 18:00 atardece.
            float t = (juego.HoraSuave - 6f) / 12f;
            float altura = Mathf.Sin(t * Mathf.PI);
            bool esDeDia = t > 0f && t < 1f;
            float dia = esDeDia ? Mathf.Clamp01(altura * 4f) : 0f;

            // De noche el sol queda bajo el horizonte: así el cielo procedural también oscurece.
            sol.transform.rotation = Quaternion.Euler(t * 180f, rumbo, 0f);
            sol.intensity = intensidadDia * dia;
            sol.color = Color.Lerp(solHorizonte, solMediodia, Mathf.Clamp01(altura * 1.6f));
            sol.shadows = dia > 0.02f ? LightShadows.Soft : LightShadows.None;

            RenderSettings.ambientSkyColor = Color.Lerp(cieloNoche, cieloDia, dia);
            RenderSettings.ambientEquatorColor = Color.Lerp(horizonteNoche, horizonteDia, dia);
            RenderSettings.ambientGroundColor = Color.Lerp(sueloNoche, sueloDia, dia);
            if (RenderSettings.fog) RenderSettings.fogColor = Color.Lerp(nieblaNoche, nieblaDia, dia);
        }
    }
}
