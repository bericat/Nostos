using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// Ajustes del jugador (volumen, música, sensibilidad de la mirada y pantalla completa),
    /// guardados en PlayerPrefs. El panel se usa en la portada y en el menú de pausa, con ratón,
    /// teclado o mando (arriba y abajo eligen, izquierda y derecha cambian el valor).
    /// </summary>
    public static class Ajustes
    {
        const string ClaveVolumen = "nostos.volumen";
        const string ClaveMusica = "nostos.musica";
        const string ClaveSensibilidad = "nostos.sensibilidad";
        const string ClaveInvertir = "nostos.invertir";

        static bool cargado;
        static float volumen = 0.8f;
        static float musica = 0.8f;
        static float sensibilidad = 1f;
        static bool invertir;
        static int foco;
        static int direccionAnterior;
        static float repetirEn;

        public const int Filas = 5;

        public static float Volumen { get { Cargar(); return volumen; } }
        /// <summary>Multiplica el volumen de la música (0 a 1).</summary>
        public static float Musica { get { Cargar(); return musica; } }
        /// <summary>Multiplica la sensibilidad de la mirada (0,4 a 2).</summary>
        public static float Sensibilidad { get { Cargar(); return sensibilidad; } }
        public static bool InvertirY { get { Cargar(); return invertir; } }

        static void Cargar()
        {
            if (cargado) return;
            cargado = true;
            volumen = PlayerPrefs.GetFloat(ClaveVolumen, 0.8f);
            musica = PlayerPrefs.GetFloat(ClaveMusica, 0.8f);
            sensibilidad = PlayerPrefs.GetFloat(ClaveSensibilidad, 1f);
            invertir = PlayerPrefs.GetInt(ClaveInvertir, 0) == 1;
            Aplicar();
        }

        static void Aplicar()
        {
            AudioListener.volume = volumen;
        }

        static void Guardar()
        {
            PlayerPrefs.SetFloat(ClaveVolumen, volumen);
            PlayerPrefs.SetFloat(ClaveMusica, musica);
            PlayerPrefs.SetFloat(ClaveSensibilidad, sensibilidad);
            PlayerPrefs.SetInt(ClaveInvertir, invertir ? 1 : 0);
            PlayerPrefs.Save();
            Aplicar();
        }

        /// <summary>Al abrir el panel: el foco en la primera fila.</summary>
        public static void Abrir()
        {
            Cargar();
            foco = 0;
            direccionAnterior = Tema.DireccionActual();
        }

        /// <summary>
        /// Teclado y mando. Devuelve true cuando hay que cerrar el panel (Volver, o Aceptar sobre
        /// la última fila).
        /// </summary>
        public static bool Actualizar()
        {
            Cargar();
            int dir = Tema.DireccionActual();
            bool nueva = dir != direccionAnterior;
            direccionAnterior = dir;
            // Mantener izquierda o derecha repite, para mover los deslizadores deprisa.
            bool repite = !nueva && dir != 0 && Time.unscaledTime >= repetirEn;
            if (nueva) repetirEn = Time.unscaledTime + 0.35f;
            else if (repite) repetirEn = Time.unscaledTime + 0.06f;

            if (nueva && dir == Tema.Arriba) foco = (foco + Filas - 1) % Filas;
            if (nueva && dir == Tema.Abajo) foco = (foco + 1) % Filas;
            if ((nueva || repite) && (dir == Tema.Izquierda || dir == Tema.Derecha))
            {
                Cambiar(foco, dir == Tema.Derecha ? 1 : -1, nueva);
            }
            if (Tema.PulsaVolver()) return true;
            if (Tema.PulsaAceptar())
            {
                if (foco == Filas - 1) return true;
                if (foco == 3) Cambiar(3, 1, true);
            }
            return false;
        }

        static void Cambiar(int fila, int signo, bool paso)
        {
            switch (fila)
            {
                case 0:
                    volumen = Mathf.Clamp01(Mathf.Round((volumen + signo * 0.05f) * 20f) / 20f);
                    break;
                case 1:
                    musica = Mathf.Clamp01(Mathf.Round((musica + signo * 0.05f) * 20f) / 20f);
                    break;
                case 2:
                    sensibilidad = Mathf.Clamp(Mathf.Round((sensibilidad + signo * 0.1f) * 10f) / 10f, 0.4f, 2f);
                    break;
                case 3:
                    if (paso) invertir = !invertir;
                    break;
            }
            Guardar();
        }

        /// <summary>Dibuja el panel dentro de `r`. Devuelve true si se ha pulsado "Volver" con el ratón.</summary>
        public static bool Dibujar(Rect r, bool sobrePapel = false)
        {
            Cargar();
            Color tinta = sobrePapel ? Tema.Tinta : Tema.Pergamino;
            Color suave = sobrePapel ? new Color(0.35f, 0.27f, 0.2f) : Tema.Apagado;
            Tema.Escribir(new Rect(r.x, r.y, r.width, 34f), "Ajustes", Tema.Titulo, tinta);
            Tema.Meandro(new Rect(r.x, r.y + 40f, r.width, 12f), new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.55f));

            float y = r.y + 70f;
            const float alto = 46f;
            string[] nombres = { "Volumen", "Música", "Sensibilidad de la mirada", "Invertir el eje vertical", "Volver" };
            for (int i = 0; i < Filas; i++)
            {
                var fila = new Rect(r.x, y + i * (alto + 6f), r.width, alto);
                bool encima = fila.Contains(Event.current.mousePosition);
                if (Tema.RatonPasa(fila)) foco = i;
                bool enfocada = foco == i;
                if (enfocada) Tema.Relleno(fila, new Color(Tema.Ocre.r, Tema.Ocre.g, Tema.Ocre.b, 0.22f), 8f);
                if (enfocada) Tema.Relleno(new Rect(fila.x, fila.y + 8f, 4f, fila.height - 16f), Tema.Ocre, 2f);
                Tema.Escribir(new Rect(fila.x + 18f, fila.y, 260f, fila.height), nombres[i],
                    new GUIStyle(Tema.BotonTexto) { wordWrap = false }, tinta);

                var control = new Rect(fila.x + fila.width * 0.55f, fila.y + 14f, fila.width * 0.45f - 20f, 18f);
                switch (i)
                {
                    case 0:
                        Deslizador(control, ref volumen, 0f, 1f, Mathf.RoundToInt(volumen * 100f) + " %", suave);
                        break;
                    case 1:
                        Deslizador(control, ref musica, 0f, 1f, Mathf.RoundToInt(musica * 100f) + " %", suave);
                        break;
                    case 2:
                        Deslizador(control, ref sensibilidad, 0.4f, 2f, sensibilidad.ToString("0.0") + "×", suave);
                        break;
                    case 3:
                        string texto = invertir ? "sí" : "no";
                        var caja = new Rect(control.xMax - 70f, fila.y + 10f, 70f, 26f);
                        Tema.Relleno(caja, invertir ? Tema.Ocre : Tema.FondoHueco, 13f);
                        Tema.Escribir(caja, texto, Tema.TextoCentro, invertir ? Tema.Tinta : Tema.Pergamino);
                        if (GUI.Button(fila, GUIContent.none, GUIStyle.none))
                        {
                            invertir = !invertir;
                            Guardar();
                        }
                        break;
                    case 4:
                        if (GUI.Button(fila, GUIContent.none, GUIStyle.none)) return true;
                        break;
                }
            }
            Tema.Indicaciones(r.center.x, r.yMax - 14f, Tema.Pequeno, (Boton.Elegir, "elegir"), (Boton.Ajustar, "cambiar"),
                (Boton.Salir, "volver"));
            return false;
        }

        static void Deslizador(Rect r, ref float valor, float min, float max, string etiqueta, Color suave)
        {
            float t = Mathf.InverseLerp(min, max, valor);
            var pista = new Rect(r.x, r.center.y - 3f, r.width - 64f, 6f);
            Tema.Barra(pista, t, Tema.Ocre);
            float px = pista.x + pista.width * t;
            Tema.Relleno(new Rect(px - 8f, pista.center.y - 8f, 16f, 16f), Tema.Pergamino, 8f);
            Tema.Filo(new Rect(px - 8f, pista.center.y - 8f, 16f, 16f), Tema.Cobre, 2f, 8f);
            Tema.Escribir(new Rect(pista.xMax + 12f, r.y - 3f, 56f, 24f), etiqueta, Tema.Cifra, suave);

            Event e = Event.current;
            var zona = new Rect(pista.x - 8f, r.y - 8f, pista.width + 16f, r.height + 16f);
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && zona.Contains(e.mousePosition))
            {
                float nuevo = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - pista.x) / pista.width));
                valor = Mathf.Round(nuevo * 20f) / 20f;
                Guardar();
                e.Use();
            }
        }
    }
}
