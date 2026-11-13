using Nostos.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>
    /// Las pruebas de la nave (F11). Montar el material de un módulo no basta: el motor hay que
    /// encenderlo a mano, cilindro a cilindro, y la navegación se calibra de noche fijando el rumbo
    /// en Calcis, que se ve en el cielo. Si sale mal no se pierde nada y se puede repetir.
    /// El vuelo de prueba final lo hace la propia nave (CascoNave).
    /// </summary>
    public class PruebasNave : MonoBehaviour
    {
        const int Cilindros = 3;
        /// <summary>Lo deprisa que sube la presión en cada cilindro.</summary>
        static readonly float[] Subida = { 0.55f, 0.7f, 0.85f };
        const float AnchoZona = 0.14f;
        const int FallosMaximos = 3;

        const float SegundosRumbo = 3f;
        const float GradosRumbo = 3.5f;
        const float DistanciaRumbo = 25f;

        static PruebasNave instancia;

        /// <summary>Se crea sola la primera vez que hace falta: no necesita estar en la escena.</summary>
        public static PruebasNave Instancia
        {
            get
            {
                if (instancia == null) instancia = new GameObject("Pruebas de la nave").AddComponent<PruebasNave>();
                return instancia;
            }
        }

        /// <summary>Hay una prueba en marcha.</summary>
        public static bool Ocupada => instancia != null && instancia.tipo != PruebaNave.Ninguna;

        public static string Describir(PruebaNave prueba)
        {
            switch (prueba)
            {
                case PruebaNave.Encendido: return "encendido de prueba del motor";
                case PruebaNave.Rumbo: return "fijar el rumbo a Calcis, de noche";
                default: return "";
            }
        }

        ControlModo control;
        PruebaNave tipo;
        int abiertaEnFrame;
        static int cerradaEnFrame = -1;

        // Encendido
        int cilindro;
        int fallos;
        float presion;
        float zona;
        float ahogoHasta;
        float destello;
        string mensaje;
        float mensajeDesde;

        // Rumbo
        float progreso;
        bool apuntando;

        void OnDestroy()
        {
            if (instancia == this) instancia = null;
        }

        public void Empezar(PruebaNave nueva)
        {
            if (Ocupada || nueva == PruebaNave.Ninguna) return;
            // La misma pulsación que cierra una prueba no debe volver a abrirla.
            if (Time.frameCount == cerradaEnFrame) return;
            Juego juego = Juego.Instancia;
            if (juego == null) return;
            if (control == null) control = FindFirstObjectByType<ControlModo>();
            if (control == null) return;

            if (nueva == PruebaNave.Rumbo && !juego.Mundo.Ciclo.EsDeNoche && !juego.ModoDios)
            {
                juego.Avisar("De día Calcis se pierde en el cielo. Vuelve a la nave de noche, después de las 20:00, para fijar el rumbo.", 6f);
                return;
            }

            tipo = nueva;
            abiertaEnFrame = Time.frameCount;
            mensaje = null;
            if (tipo == PruebaNave.Encendido)
            {
                cilindro = 0;
                fallos = 0;
                presion = 0f;
                ahogoHasta = 0f;
                destello = 0f;
                zona = NuevaZona();
                control.Bloquear(true);
                Sonido.Tocar(Efecto.Pagina, 0.6f);
            }
            else
            {
                progreso = 0f;
                apuntando = false;
                juego.Avisar("Busca a Calcis en el cielo y mantenla en el centro del sextante.", 5f);
            }
        }

        static float NuevaZona() => Random.Range(0.55f, 0.85f);

        void Cerrar()
        {
            if (tipo == PruebaNave.Ninguna) return;
            if (tipo == PruebaNave.Encendido && control != null) control.Bloquear(false);
            tipo = PruebaNave.Ninguna;
            cerradaEnFrame = Time.frameCount;
        }

        void Update()
        {
            if (tipo == PruebaNave.Ninguna) return;
            Juego juego = Juego.Instancia;
            if (juego == null || juego.Despegado)
            {
                Cerrar();
                return;
            }
            ModuloNave modulo = juego.Mundo.Nave.Actual;
            if (modulo == null || !modulo.EsperaPrueba || modulo.Prueba != tipo)
            {
                Cerrar();
                return;
            }
            if (Time.frameCount == abiertaEnFrame || MenuPausa.Abierto) return;
            if (Sale())
            {
                Cerrar();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            destello = Mathf.Max(0f, destello - dt * 2.5f);
            if (tipo == PruebaNave.Encendido) Encendido(juego, dt);
            else Rumbo(juego, dt);
        }

        // ---------------------------------------------------------------- encendido

        void Encendido(Juego juego, float dt)
        {
            if (Time.unscaledTime < ahogoHasta)
            {
                presion = Mathf.Max(0f, presion - dt * 1.5f);
                return;
            }
            // La presión sube cada vez más deprisa; si llega arriba salta la válvula.
            presion += dt * Subida[cilindro] * (0.6f + presion);
            if (presion >= 1f)
            {
                Fallo(juego, "Demasiada presión: salta la válvula de alivio.");
                return;
            }
            if (!Pulsa()) return;

            if (Mathf.Abs(presion - zona) <= AnchoZona * 0.5f)
            {
                cilindro++;
                destello = 1f;
                Sonido.Tocar(Efecto.Golpe, 0.9f, true);
                if (CascoNave.Instancia != null) CascoNave.Instancia.Temblar(0.5f, 0.04f + 0.02f * cilindro);
                if (cilindro >= Cilindros)
                {
                    Exito(juego, "¡El motor arranca! Los tres cilindros a su presión. Livia querrá oírlo.");
                    return;
                }
                presion = 0f;
                zona = NuevaZona();
                Decir("Cilindro " + cilindro + " encendido.");
            }
            else
            {
                Fallo(juego, presion < zona ? "Le falta presión: el cilindro tose y se apaga." : "Te has pasado: el cilindro petardea.");
            }
        }

        void Fallo(Juego juego, string texto)
        {
            fallos++;
            Sonido.Tocar(Efecto.Paso, 0.5f, true);
            if (CascoNave.Instancia != null) CascoNave.Instancia.Temblar(0.25f, 0.02f);
            presion = Mathf.Min(presion, 1f);
            ahogoHasta = Time.unscaledTime + 0.7f;
            Decir(texto);
            if (fallos >= FallosMaximos)
            {
                juego.Avisar("El motor se ahoga. Deja que se despeje y vuelve a probar.", 5f);
                Cerrar();
                return;
            }
            zona = NuevaZona();
        }

        void Decir(string texto)
        {
            mensaje = texto;
            mensajeDesde = Time.unscaledTime;
        }

        void Exito(Juego juego, string aviso)
        {
            if (juego.Mundo.Nave.CompletarPrueba())
            {
                Sonido.Tocar(Efecto.Campana);
                if (CascoNave.Instancia != null) CascoNave.Instancia.Temblar(1.6f, 0.07f);
                juego.Avisar(aviso, 7f);
            }
            Cerrar();
        }

        // ---------------------------------------------------------------- rumbo

        void Rumbo(Juego juego, float dt)
        {
            if (!juego.Mundo.Ciclo.EsDeNoche && !juego.ModoDios)
            {
                juego.Avisar("Amanece y Calcis se pierde en el cielo. Habrá que esperar a la noche.", 5f);
                Cerrar();
                return;
            }
            if (control.Modo != ModoJuego.APie)
            {
                Cerrar();
                return;
            }
            Camera camara = Camera.main;
            if (camara == null) return;
            CascoNave casco = CascoNave.Instancia;
            if (casco != null && (camara.transform.position - casco.transform.position).sqrMagnitude > DistanciaRumbo * DistanciaRumbo)
            {
                juego.Avisar("Te has alejado de la nave: el rumbo se fija desde ella.", 4f);
                Cerrar();
                return;
            }

            apuntando = !control.Bloqueado && Vector3.Angle(camara.transform.forward, CieloCalcis.Direccion) <= GradosRumbo;
            if (apuntando) progreso += dt / SegundosRumbo;
            else progreso = Mathf.Max(0f, progreso - dt * 0.4f);
            if (progreso >= 1f)
            {
                progreso = 1f;
                Exito(juego, "Rumbo fijado: Calcis, cuarenta días de vuelo. La navegación queda calibrada.");
            }
        }

        // ---------------------------------------------------------------- entrada

        static bool Pulsa()
        {
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            return (teclado != null && (teclado.eKey.wasPressedThisFrame || teclado.spaceKey.wasPressedThisFrame
                                        || teclado.enterKey.wasPressedThisFrame))
                   || (mando != null && (mando.buttonSouth.wasPressedThisFrame || mando.buttonWest.wasPressedThisFrame));
        }

        bool Sale()
        {
            Keyboard teclado = Keyboard.current;
            Gamepad mando = Gamepad.current;
            bool faena = (teclado != null && teclado.fKey.wasPressedThisFrame) || (mando != null && mando.buttonEast.wasPressedThisFrame);
            // En el rumbo se anda y se mira libremente: Esc abre la pausa como siempre.
            if (tipo == PruebaNave.Rumbo) return faena;
            return faena || (teclado != null && (teclado.escapeKey.wasPressedThisFrame || teclado.tabKey.wasPressedThisFrame));
        }

        // ---------------------------------------------------------------- dibujo

        void OnGUI()
        {
            if (tipo == PruebaNave.Ninguna || MenuPausa.Abierto) return;
            GUI.depth = -5;
            Tema.Empezar();
            if (tipo == PruebaNave.Encendido) DibujarEncendido();
            else DibujarRumbo();
        }

        void DibujarEncendido()
        {
            float ancho = Mathf.Min(720f, Tema.Ancho - 32f);
            const float alto = 330f;
            var panel = new Rect((Tema.Ancho - ancho) * 0.5f, Mathf.Max(16f, Tema.Alto * 0.5f - 60f), ancho, alto);
            Tema.Panel(panel, true);
            var dentro = new Rect(panel.x + 28f, panel.y + 20f, panel.width - 56f, panel.height - 40f);

            Tema.Escribir(new Rect(dentro.x, dentro.y, dentro.width, 18f), "LA NAVE · MOTOR", Tema.Seccion);
            Tema.Escribir(new Rect(dentro.x, dentro.y + 20f, dentro.width, 30f), "Encendido de prueba", Tema.Titulo);
            Tema.Meandro(new Rect(dentro.x, dentro.y + 54f, dentro.width, 12f), new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.35f));
            Tema.Escribir(new Rect(dentro.x, dentro.y + 72f, dentro.width, 44f),
                "La presión sube sola. Pulsa cuando la aguja esté en la franja verde para encender el cilindro. Si llega arriba, salta la válvula.",
                Tema.Texto);

            // Marcas de fallo, arriba a la derecha.
            for (int i = 0; i < FallosMaximos; i++)
            {
                var r = new Rect(dentro.xMax - FallosMaximos * 22f + i * 22f + 4f, dentro.y + 4f, 14f, 14f);
                if (i < fallos) Tema.Relleno(r, Tema.Rojo, 7f);
                else Tema.Filo(r, Tema.Apagado, 1.5f, 7f);
            }

            // Manómetro: un arco de puntos de 240 grados con su aguja.
            var centro = new Vector2(dentro.x + 96f, dentro.y + 210f);
            const float radio = 76f;
            const int puntos = 33;
            Tema.Relleno(new Rect(centro.x - radio - 14f, centro.y - radio - 14f, (radio + 14f) * 2f, (radio + 14f) * 2f), Tema.FondoHueco, radio + 14f);
            for (int i = 0; i < puntos; i++)
            {
                float v = i / (float)(puntos - 1);
                Vector2 p = PuntoArco(centro, radio, v);
                bool enZona = Mathf.Abs(v - zona) <= AnchoZona * 0.5f;
                Color color = enZona ? Tema.Verde : v > 0.92f ? Tema.Rojo : new Color(Tema.Apagado.r, Tema.Apagado.g, Tema.Apagado.b, 0.45f);
                if (!enZona && v <= presion) color = Tema.Ocre;
                float lado = enZona ? 8f : 6f;
                Tema.Relleno(new Rect(p.x - lado * 0.5f, p.y - lado * 0.5f, lado, lado), color, lado * 0.5f);
            }
            for (float d = 0f; d <= radio - 14f; d += 5f)
            {
                Vector2 p = PuntoArco(centro, d, presion);
                Tema.Relleno(new Rect(p.x - 2.5f, p.y - 2.5f, 5f, 5f), Tema.Pergamino, 2.5f);
            }
            Tema.Relleno(new Rect(centro.x - 9f, centro.y - 9f, 18f, 18f), Tema.Cobre, 9f);
            Tema.Escribir(new Rect(centro.x - 60f, centro.y + 26f, 120f, 20f), "presión", Tema.PequenoCentro);

            // Los tres cilindros, a la derecha.
            float x = centro.x + radio + 60f;
            Tema.Escribir(new Rect(x, dentro.y + 128f, 260f, 22f),
                "Cilindro " + Mathf.Min(cilindro + 1, Cilindros) + " de " + Cilindros, Tema.Pequeno);
            for (int i = 0; i < Cilindros; i++)
            {
                var r = new Rect(x + i * 74f, dentro.y + 156f, 58f, 84f);
                bool hecho = i < cilindro;
                Tema.Relleno(r, hecho ? Tema.Cobre : Tema.FondoClaro, 10f);
                if (hecho)
                {
                    // Llama: tres franjas que tiemblan.
                    float t = Time.unscaledTime * 9f + i;
                    for (int k = 0; k < 3; k++)
                    {
                        float h = 22f + 10f * Mathf.PerlinNoise(t, k) - k * 5f;
                        float w = 30f - k * 9f;
                        Color c = k == 0 ? Tema.Ocre : k == 1 ? new Color(1f, 0.85f, 0.5f) : Tema.Pergamino;
                        Tema.Relleno(new Rect(r.center.x - w * 0.5f, r.yMax - 12f - h, w, h), c, w * 0.5f);
                    }
                }
                bool actual = i == cilindro;
                Tema.Filo(r, actual ? Tema.Ocre : new Color(Tema.Borde.r, Tema.Borde.g, Tema.Borde.b, 0.35f), actual ? 2f : 1f, 10f);
            }
            if (destello > 0f)
            {
                Tema.Relleno(panel, new Color(1f, 0.85f, 0.5f, destello * 0.12f), 12f);
            }
            if (!string.IsNullOrEmpty(mensaje) && Time.unscaledTime - mensajeDesde < 2.5f)
            {
                Tema.Escribir(new Rect(x, dentro.y + 248f, dentro.xMax - x, 22f), mensaje, Tema.Pequeno, Tema.Pergamino);
            }

            Tema.Indicaciones(panel.center.x, panel.yMax - 26f, Tema.Pequeno, (Boton.Accion, "encender"), (Boton.Faena, "dejarlo"));
        }

        /// <summary>Punto del manómetro para un valor de 0 a 1: de abajo a la izquierda a abajo a la derecha, por arriba.</summary>
        static Vector2 PuntoArco(Vector2 centro, float radio, float valor)
        {
            float a = (210f - 240f * Mathf.Clamp01(valor)) * Mathf.Deg2Rad;
            return new Vector2(centro.x + Mathf.Cos(a) * radio, centro.y - Mathf.Sin(a) * radio);
        }

        void DibujarRumbo()
        {
            var c = new Vector2(Tema.Ancho * 0.5f, Tema.Alto * 0.5f);
            const float radio = 50f;

            // El sextante: un anillo con su cruz y el progreso alrededor.
            Color tinta = apuntando ? Tema.Ocre : new Color(Tema.Pergamino.r, Tema.Pergamino.g, Tema.Pergamino.b, 0.8f);
            Tema.Filo(new Rect(c.x - radio, c.y - radio, radio * 2f, radio * 2f), new Color(0f, 0f, 0f, 0.35f), 4f, radio);
            Tema.Filo(new Rect(c.x - radio, c.y - radio, radio * 2f, radio * 2f), tinta, 2f, radio);
            Tema.Relleno(new Rect(c.x - radio - 10f, c.y - 1f, 16f, 2f), tinta);
            Tema.Relleno(new Rect(c.x + radio - 6f, c.y - 1f, 16f, 2f), tinta);
            Tema.Relleno(new Rect(c.x - 1f, c.y - radio - 10f, 2f, 16f), tinta);
            Tema.Relleno(new Rect(c.x - 1f, c.y + radio - 6f, 2f, 16f), tinta);
            Tema.AnilloDePuntos(c, radio + 18f, 32, progreso, 5f, Tema.Ocre, new Color(Tema.Apagado.r, Tema.Apagado.g, Tema.Apagado.b, 0.3f));

            // Dónde está Calcis: una marca si se ve en pantalla, una flecha en el borde si no.
            Camera camara = Camera.main;
            if (camara != null)
            {
                Vector3 p = camara.WorldToScreenPoint(camara.transform.position + CieloCalcis.Direccion * 100f);
                var g = new Vector2(p.x / Tema.Escala, (Screen.height - p.y) / Tema.Escala);
                bool delante = p.z > 0f;
                var margen = new Rect(70f, 70f, Tema.Ancho - 140f, Tema.Alto - 140f);
                if (delante && margen.Contains(g))
                {
                    if (!apuntando)
                    {
                        Tema.Filo(new Rect(g.x - 14f, g.y - 14f, 28f, 28f), Tema.Azul, 2f, 14f);
                        Tema.Relleno(new Rect(g.x - 3f, g.y - 3f, 6f, 6f), Tema.Azul, 3f);
                    }
                }
                else
                {
                    Vector2 v = delante ? g - c : c - g;
                    if (v.sqrMagnitude < 1f) v = Vector2.down;
                    v.Normalize();
                    float tx = Mathf.Abs(v.x) > 0.001f ? margen.width * 0.5f / Mathf.Abs(v.x) : float.MaxValue;
                    float ty = Mathf.Abs(v.y) > 0.001f ? margen.height * 0.5f / Mathf.Abs(v.y) : float.MaxValue;
                    Vector2 punto = c + v * Mathf.Min(tx, ty);
                    float grados = Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg;
                    Tema.Girado(new Rect(punto.x - 15f, punto.y - 15f, 30f, 30f), "Adornos/flecha", Tema.Ocre, grados);
                }
            }

            string texto = apuntando ? "Aguanta el pulso…" : "Busca a Calcis en el cielo y céntrala en el sextante";
            var caja = new Rect(c.x - 230f, c.y + radio + 40f, 460f, 78f);
            Tema.Ficha(caja, 0.85f);
            Tema.Escribir(new Rect(caja.x, caja.y + 6f, caja.width, 20f), "FIJAR RUMBO A CALCIS", new GUIStyle(Tema.Seccion) { alignment = TextAnchor.MiddleCenter });
            Tema.Escribir(new Rect(caja.x, caja.y + 26f, caja.width, 18f), texto, Tema.PequenoCentro);
            Tema.Indicaciones(c.x, caja.yMax - 16f, Tema.Pequeno, (Boton.Faena, "dejarlo"));
        }
    }
}
