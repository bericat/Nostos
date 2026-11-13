using Nostos.Sim;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nostos
{
    /// <summary>
    /// Modo constructor: elige un edificio del catálogo, muestra su plano fantasma sobre la
    /// rejilla bajo el puntero y coloca la obra. También cancela obras sin terminar.
    /// El estado (qué celdas están ocupadas, qué obras hay) vive en Nostos.Sim.
    /// </summary>
    public class ColocadorRejilla : MonoBehaviour
    {
        public ControlModo control;
        public Juego juego;
        public Camera camara;
        public Transform jugador;
        public Material materialValido;
        public Material materialInvalido;
        [Tooltip("Radio en metros, desde el centro del valle, dentro del cual se puede construir.")]
        public float radioEdificable = 34f;
        [Tooltip("Opcional. Si está asignada, el NavMesh se regenera al colocar o quitar (para los colonos de F2).")]
        public NavMeshSurface superficie;
        [Tooltip("Losas de camino. El catálogo tiene una casilla más, después de los edificios, para trazar caminos.")]
        public Caminos caminos;

        InputAction puntero;
        InputAction colocar;
        InputAction quitar;
        InputAction siguiente;
        InputAction anterior;
        GameObject fantasma;
        Renderer[] rendersFantasma;
        bool fantasmaValido;
        int seleccion;
        int seleccionMostrada = -1;
        Vector2 ultimoPuntero;
        int sinColocarHasta;
        bool punteroEnCentro;

        /// <summary>El edificio elegido para colocar, o null si el catálogo está vacío o se trazan caminos.</summary>
        public DefinicionEdificio Seleccion
        {
            get
            {
                DefinicionEdificio[] catalogo = juego.catalogo;
                if (catalogo == null || catalogo.Length == 0 || ModoCamino) return null;
                return catalogo[seleccion % catalogo.Length];
            }
        }

        /// <summary>La casilla después del último edificio: trazar caminos (gratis).</summary>
        public bool ModoCamino => juego.catalogo != null && seleccion == juego.catalogo.Length;

        /// <summary>La casilla elegida: un edificio del catálogo o, la última, el camino.</summary>
        public int Indice => seleccion;

        /// <summary>Elige una casilla desde el menú de construcción.</summary>
        public void Elegir(int indice)
        {
            seleccion = Mathf.Clamp(indice, 0, Casillas - 1);
            // El clic o el botón que elige en el menú no debe marcar también una obra.
            sinColocarHasta = Time.frameCount + 2;
        }

        /// <summary>Casillas del catálogo: los edificios y, al final, el camino.</summary>
        public int Casillas => (juego.catalogo != null ? juego.catalogo.Length : 0) + 1;

        /// <summary>La obra sin terminar que hay bajo el puntero, si la hay.</summary>
        public EdificioEnMundo ObraApuntada { get; private set; }

        void Start()
        {
            if (caminos == null) caminos = FindFirstObjectByType<Caminos>();
            InputActionMap mapa = control.acciones.FindActionMap(ControlModo.MapaConstructor, true);
            puntero = mapa.FindAction("Puntero", true);
            colocar = mapa.FindAction("Colocar", true);
            quitar = mapa.FindAction("Quitar", true);
            siguiente = mapa.FindAction("Edificio siguiente", true);
            anterior = mapa.FindAction("Edificio anterior", true);
        }

        void Update()
        {
            ObraApuntada = null;
            if (control.Modo != ModoJuego.Constructor || juego.catalogo == null)
            {
                OcultarFantasma();
                trazando = 0;
                return;
            }

            int total = Casillas;
            if (siguiente.WasPressedThisFrame()) seleccion = (seleccion + 1) % total;
            if (anterior.WasPressedThisFrame()) seleccion = (seleccion + total - 1) % total;
            if (Time.frameCount <= sinColocarHasta || MenuConstruccion.Abierto)
            {
                OcultarFantasma();
                trazando = 0;
                return;
            }
            if (ModoCamino)
            {
                ActualizarCamino();
                return;
            }
            DefinicionEdificio definicion = Seleccion;
            if (definicion == null)
            {
                OcultarFantasma();
                return;
            }
            if (seleccion != seleccionMostrada) CrearFantasma(definicion);

            if (!CeldaBajoPuntero(out Celda celda) || PunteroSobreMenu())
            {
                OcultarFantasma();
                return;
            }

            EdificioEnMundo existente = juego.EnCelda(celda);
            if (existente != null && !existente.Sim.Terminado) ObraApuntada = existente;

            // El puntero marca el centro del solar.
            var origen = new Celda(celda.X - definicion.ancho / 2, celda.Z - definicion.fondo / 2);
            bool valido = SolarValido(definicion, origen);

            if (fantasma != null)
            {
                if (!fantasma.activeSelf) fantasma.SetActive(true);
                fantasma.transform.position = juego.CentroDeArea(origen, definicion.ancho, definicion.fondo);
                PintarFantasma(valido);
            }

            if (colocar.WasPressedThisFrame())
            {
                if (valido)
                {
                    juego.Construir(definicion, origen);
                    RegenerarNavegacion();
                    juego.Avisar("Obra de " + definicion.nombre + " marcada. Lleva material a pie: "
                                 + Recursos.Lista(definicion.costeCimientos) + ".");
                }
                else
                {
                    juego.Avisar("Ahí no cabe: el solar tiene que estar libre y dentro del llano.");
                }
            }
            else if (ObraApuntada != null && quitar.WasPressedThisFrame())
            {
                string nombre = ObraApuntada.Definicion.nombre;
                juego.Demoler(ObraApuntada);
                ObraApuntada = null;
                RegenerarNavegacion();
                juego.Avisar("Obra de " + nombre + " cancelada.");
            }
        }

        // ---- Caminos ----

        // 0: nada; 1: trazando; -1: borrando. Sirve para avisar una vez por trazo.
        int trazando;
        int puestas;

        void ActualizarCamino()
        {
            if (seleccion != seleccionMostrada) CrearFantasmaCamino();

            if (!CeldaBajoPuntero(out Celda celda) || PunteroSobreMenu())
            {
                OcultarFantasma();
                return;
            }

            Mundo mundo = juego.Mundo;
            bool esCamino = mundo.EsCamino(celda);
            bool valido = esCamino || CeldaCaminable(celda);
            if (fantasma != null)
            {
                if (!fantasma.activeSelf) fantasma.SetActive(true);
                mundo.Rejilla.CentroDeCelda(celda, out float x, out float z);
                fantasma.transform.position = new Vector3(x, 0.03f, z);
                PintarFantasma(valido);
            }

            // Un trazo empieza con una pulsación nueva y sigue mientras se mantiene.
            if (colocar.IsPressed() && (trazando == 1 || colocar.WasPressedThisFrame()))
            {
                if (trazando != 1)
                {
                    trazando = 1;
                    puestas = 0;
                    if (!valido) juego.Avisar("Ahí no se puede trazar camino: tiene que ser suelo libre del llano.");
                }
                if (!esCamino && valido && mundo.PonerCamino(celda))
                {
                    puestas++;
                    Sonido.Tocar(Efecto.Golpe, 0.35f, true);
                }
            }
            else if (quitar.IsPressed() && (trazando == -1 || quitar.WasPressedThisFrame()))
            {
                if (trazando != -1)
                {
                    trazando = -1;
                    puestas = 0;
                }
                if (esCamino && mundo.QuitarCamino(celda)) puestas++;
            }
            else
            {
                if (trazando == 1 && puestas > 0)
                    juego.Avisar("Camino trazado. Los colonos lo preferirán y por él se anda más deprisa.");
                else if (trazando == -1 && puestas > 0)
                    juego.Avisar("Camino levantado.");
                trazando = 0;
                puestas = 0;
            }
        }

        bool CeldaCaminable(Celda celda)
        {
            Rejilla rejilla = juego.Mundo.Rejilla;
            if (!rejilla.EstaLibre(celda)) return false;
            rejilla.CentroDeCelda(celda, out float cx, out float cz);
            return cx * cx + cz * cz <= radioEdificable * radioEdificable;
        }

        void CrearFantasmaCamino()
        {
            if (fantasma != null) Destroy(fantasma);
            fantasma = null;
            rendersFantasma = new Renderer[0];
            seleccionMostrada = seleccion;

            GameObject modelo = caminos != null && caminos.losas != null && caminos.losas.Length > 0
                ? caminos.losas[0] : null;
            if (modelo != null)
            {
                fantasma = Instantiate(modelo, transform);
            }
            else
            {
                fantasma = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fantasma.transform.SetParent(transform, false);
                float lado = juego.Mundo.Rejilla.TamCelda * 0.96f;
                fantasma.transform.localScale = new Vector3(lado, 0.05f, lado);
            }
            PrepararFantasma();
        }

        bool SolarValido(DefinicionEdificio definicion, Celda origen)
        {
            if (!juego.Mundo.PuedeColocar(definicion.Datos, origen)) return false;

            Rejilla rejilla = juego.Mundo.Rejilla;
            Celda celdaJugador = default;
            bool hayJugador = jugador != null;
            if (hayJugador) celdaJugador = rejilla.CeldaDesdeMundo(jugador.position.x, jugador.position.z);

            for (int x = 0; x < definicion.ancho; x++)
            {
                for (int z = 0; z < definicion.fondo; z++)
                {
                    var celda = new Celda(origen.X + x, origen.Z + z);
                    if (hayJugador && celda == celdaJugador) return false;
                    rejilla.CentroDeCelda(celda, out float cx, out float cz);
                    if (cx * cx + cz * cz > radioEdificable * radioEdificable) return false;
                }
            }
            return true;
        }

        void RegenerarNavegacion()
        {
            if (superficie != null) superficie.BuildNavMesh();
        }

        /// <summary>Con el ratón sobre el menú de construcción (o su ficha) no se coloca nada.</summary>
        bool PunteroSobreMenu()
        {
            return !punteroEnCentro && MenuConstruccion.TapaPuntero(puntero.ReadValue<Vector2>());
        }

        bool CeldaBajoPuntero(out Celda celda)
        {
            celda = default;

            // Con ratón (o trackpad) se apunta con el cursor. Si el cursor lleva quieto y se usa
            // el mando, se apunta al centro de la pantalla y se mueve la cámara.
            Vector2 posicion = puntero.ReadValue<Vector2>();
            if ((posicion - ultimoPuntero).sqrMagnitude > 0.01f)
            {
                punteroEnCentro = false;
            }
            else if (MandoEnUso())
            {
                punteroEnCentro = true;
            }
            ultimoPuntero = posicion;
            if (punteroEnCentro) posicion = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Ray rayo = camara.ScreenPointToRay(posicion);
            var suelo = new Plane(Vector3.up, Vector3.zero);
            if (!suelo.Raycast(rayo, out float distancia)) return false;

            Vector3 punto = rayo.GetPoint(distancia);
            if (punto.x * punto.x + punto.z * punto.z > (radioEdificable + 6f) * (radioEdificable + 6f)) return false;
            celda = juego.Mundo.Rejilla.CeldaDesdeMundo(punto.x, punto.z);
            return true;
        }

        static bool MandoEnUso()
        {
            Gamepad mando = Gamepad.current;
            if (mando == null) return false;
            return mando.leftStick.ReadValue().sqrMagnitude > 0.09f
                || mando.buttonSouth.isPressed
                || mando.buttonEast.isPressed;
        }

        void OcultarFantasma()
        {
            if (fantasma != null && fantasma.activeSelf) fantasma.SetActive(false);
        }

        void CrearFantasma(DefinicionEdificio definicion)
        {
            if (fantasma != null) Destroy(fantasma);
            fantasma = null;
            rendersFantasma = new Renderer[0];
            seleccionMostrada = seleccion;

            GameObject prefab = definicion.Modelo(EstadoObra.Terminado);
            if (prefab == null) return;

            fantasma = Instantiate(prefab, transform);
            PrepararFantasma();
        }

        void PrepararFantasma()
        {
            fantasma.name = "Plano fantasma";
            foreach (Collider c in fantasma.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
                Destroy(c);
            }
            rendersFantasma = fantasma.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rendersFantasma)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            fantasmaValido = false;
            PintarFantasma(true);
            fantasma.SetActive(false);
        }

        void PintarFantasma(bool valido)
        {
            if (valido == fantasmaValido && rendersFantasma.Length > 0
                && rendersFantasma[0].sharedMaterial == (valido ? materialValido : materialInvalido)) return;
            fantasmaValido = valido;
            Material material = valido ? materialValido : materialInvalido;
            foreach (Renderer r in rendersFantasma) r.sharedMaterial = material;
        }
    }
}
