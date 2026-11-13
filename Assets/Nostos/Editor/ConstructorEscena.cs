using System.Collections.Generic;
using System.IO;
using Nostos.Sim;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Nostos.EditorTools
{
    /// <summary>
    /// Monta la escena del juego (menú Nostos > Crear escena): el valle, Nicias con su modelo,
    /// las dos cámaras, la rejilla, el catálogo de edificios, árboles, vetas y el casco de la nave.
    /// Se ejecuta sola una vez cuando cambia la versión de la escena.
    /// </summary>
    public static partial class ConstructorEscena
    {
        const string Version = "F18-inventario";
        const string RutaMarca = "Library/NostosEscena.txt";
        const string RutaEscena = "Assets/Nostos/Escenas/Nostos.unity";
        const string RutaAcciones = "Assets/Nostos/Input/NostosInput.inputactions";
        const string CarpetaMateriales = "Assets/Nostos/Arte/Materiales";
        const string CarpetaTerreno = "Assets/Nostos/Arte/Terreno";
        const string CarpetaTexturas = "Assets/Nostos/Arte/Texturas";
        const string CarpetaDatos = "Assets/Nostos/Datos/Edificios";
        const string CarpetaNicias = "Assets/Nostos/Personajes/Nicias";
        const string CarpetaSonido = "Assets/Nostos/Sonido";
        const float TamCelda = 2f;
        const float RadioEdificable = 34f;

        [System.Serializable]
        class InfoValle
        {
            public int resolucion = 257;
            public float tamano = 400f;
            public float alturaMinima = 0f;
            public float alturaMaxima = 60f;
            public float radioLlano = 44f;
            public float tunelX = 70f;
            public float tunelZ = 59f;
            public float puenteX = -51f;
            public float puenteZ = 4f;
            public float cascadaX = -96f;
            public float cascadaZ = 96f;
        }

        /// <summary>Alturas del valle (de Pipeline/generar_terreno.py) para sembrar sin depender de la física.</summary>
        class Valle
        {
            public InfoValle info = new InfoValle();
            public float[,] alturas;

            public float Altura(float x, float z)
            {
                if (alturas == null) return 0f;
                int lado = info.resolucion;
                float u = Mathf.Clamp01((x + info.tamano * 0.5f) / info.tamano) * (lado - 1);
                float v = Mathf.Clamp01((z + info.tamano * 0.5f) / info.tamano) * (lado - 1);
                int i = Mathf.Min((int)u, lado - 2);
                int j = Mathf.Min((int)v, lado - 2);
                float fu = u - i;
                float fv = v - j;
                float abajo = Mathf.Lerp(alturas[j, i], alturas[j, i + 1], fu);
                float arriba = Mathf.Lerp(alturas[j + 1, i], alturas[j + 1, i + 1], fu);
                return Mathf.Lerp(abajo, arriba, fv);
            }

            /// <summary>True si ahí se puede plantar algo: suelo casi llano, fuera del río y de las paredes.</summary>
            public bool Llano(float x, float z, float alturaMaxima)
            {
                float h = Altura(x, z);
                if (h < -0.05f || h > alturaMaxima) return false;
                const float d = 1.6f;
                float desnivel = Mathf.Max(
                    Mathf.Abs(Altura(x + d, z) - Altura(x - d, z)),
                    Mathf.Abs(Altura(x, z + d) - Altura(x, z - d)));
                return desnivel < 0.9f && Altura(x + 4f, z) > -0.05f && Altura(x - 4f, z) > -0.05f
                       && Altura(x, z + 4f) > -0.05f && Altura(x, z - 4f) > -0.05f;
            }
        }

        [InitializeOnLoadMethod]
        static void AlCargar()
        {
            EditorApplication.delayCall += () =>
            {
                bool alDia = File.Exists(RutaEscena) && File.Exists(RutaMarca)
                             && File.ReadAllText(RutaMarca).Trim() == Version;
                if (alDia) return;
                // Un solo intento automático por sesión del editor; después, por el menú.
                if (SessionState.GetBool("Nostos.Escena." + Version, false)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool("Nostos.Escena." + Version, true);
                if (SceneManager.GetActiveScene().isDirty)
                {
                    Debug.Log("Nostos: guarda la escena actual y usa el menú Nostos > Crear escena para montar la " + Version + ".");
                    return;
                }
                Crear();
            };
        }

        [MenuItem("Nostos/Crear escena")]
        public static void Crear()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Nostos: sal del modo Play antes de crear la escena.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var acciones = AssetDatabase.LoadAssetAtPath<InputActionAsset>(RutaAcciones);
            if (acciones == null)
            {
                Debug.LogError("Nostos: no se encuentra " + RutaAcciones + ".");
                return;
            }

            CarpetasNostos.Asegurar("Assets/Nostos/Escenas");
            CarpetasNostos.Asegurar(CarpetaMateriales);
            CarpetasNostos.Asegurar(CarpetaDatos);
            // Los prefabs hechos con piezas de los paquetes se rehacen en cada escena nueva.
            AssetDatabase.DeleteAsset(CarpetaPrefabsPaquetes);
            CarpetasNostos.Asegurar(CarpetaPrefabsPaquetes);
            escalaPinos = -1f;

            ImportadorKit.ReimportarKit();

            Material matSuelo = MaterialLit("Suelo", new Color(0.45f, 0.52f, 0.34f));
            Material matNicias = MaterialLit("Nicias", new Color(0.91f, 0.93f, 0.95f));
            Material matFarol = MaterialLit("Farol", new Color(0.75f, 0.44f, 0.28f));
            Material matColono = MaterialLit("Colono", new Color(0.78f, 0.6f, 0.36f));
            Material matArconte = MaterialLit("Arconte", new Color(0.93f, 0.95f, 0.98f));
            Material matValido = MaterialTransparente("Previsualización válida", new Color(0.35f, 0.9f, 0.5f, 0.4f));
            Material matInvalido = MaterialTransparente("Previsualización inválida", new Color(0.95f, 0.3f, 0.25f, 0.4f));
            Material matRejilla = MaterialTransparente("Rejilla", new Color(1f, 1f, 1f, 0.22f));
            DefinicionEdificio[] catalogo = CrearCatalogo();

            Scene escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Luz, ambiente y niebla
            var solGo = new GameObject("Sol");
            var sol = solGo.AddComponent<Light>();
            sol.type = LightType.Directional;
            sol.color = new Color(1f, 0.95f, 0.86f);
            sol.intensity = 1.3f;
            sol.shadows = LightShadows.Soft;
            solGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sol;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.5f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.26f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.82f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 520f;

            // Terreno: la malla facetada del valle, con su río
            Valle valle = CrearTerreno();
            if (valle == null)
            {
                valle = new Valle();
                GameObject suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
                suelo.name = "Suelo";
                suelo.transform.localScale = new Vector3(40f, 1f, 40f);
                suelo.GetComponent<Renderer>().sharedMaterial = matSuelo;
                Debug.LogWarning("Nostos: falta el terreno. Ejecuta Pipeline/generar_terreno.py y repite Nostos > Crear escena.");
            }

            // Cámara principal con el cerebro de Cinemachine
            var camaraGo = new GameObject("Cámara principal");
            camaraGo.tag = "MainCamera";
            var camara = camaraGo.AddComponent<Camera>();
            camara.nearClipPlane = 0.1f;
            camara.farClipPlane = 800f;
            camaraGo.AddComponent<AudioListener>();
            var cerebro = camaraGo.AddComponent<CinemachineBrain>();
            cerebro.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.4f);
            camaraGo.transform.position = new Vector3(0f, 1.65f, -4f);

            // Sistemas
            var sistemas = new GameObject("Sistemas");
            var control = sistemas.AddComponent<ControlModo>();
            control.acciones = acciones;
            var juego = sistemas.AddComponent<Juego>();
            juego.catalogo = catalogo;
            juego.tamCelda = TamCelda;
            juego.materialColono = matColono;
            juego.colonosIniciales = new[]
            {
                new ColonoInicial { nombre = "Livia", rol = "ingeniera", confianza = 30f, altura = 1f },
                new ColonoInicial { nombre = "Septimio", rol = "anciano del consejo", confianza = 22f, altura = 0.95f },
                new ColonoInicial { nombre = "Marco", rol = "niño curioso", confianza = 40f, altura = 0.7f },
                new ColonoInicial { nombre = "Fabia", rol = "leñadora", confianza = 24f, altura = 1f },
                new ColonoInicial { nombre = "Cayo", rol = "cantero", confianza = 18f, altura = 1.05f }
            };
            var panel = sistemas.AddComponent<PanelColono>();
            panel.control = control;
            panel.juego = juego;
            var diaNoche = sistemas.AddComponent<DiaNoche>();
            diaNoche.juego = juego;
            diaNoche.sol = sol;

            // Cielo de noche con las imágenes de Dynamic Space Background (si el asset está en el proyecto)
            const string CarpetaEspacio = "Assets/DinV/Dynamic Space Background/Sprites/";
            Texture2D nebulosa = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaEspacio + "Nebula Blue.png");
            if (nebulosa != null)
            {
                var cielo = new GameObject("Cielo de noche").AddComponent<CieloNocturno>();
                cielo.juego = juego;
                cielo.capas = new[]
                {
                    new CieloNocturno.Capa { textura = nebulosa, repeticion = 1f, opacidad = 1f, brillo = 1.5f, giro = 0.12f },
                    new CieloNocturno.Capa
                    {
                        textura = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaEspacio + "Nebula Aqua-Pink.png"),
                        repeticion = 0.8f, opacidad = 0.35f, brillo = 1.6f, giro = 0.08f
                    },
                    new CieloNocturno.Capa
                    {
                        textura = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaEspacio + "Stars Small_1.png"),
                        repeticion = 2.5f, opacidad = 1f, brillo = 1.3f, giro = 0.2f
                    },
                    new CieloNocturno.Capa
                    {
                        textura = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaEspacio + "Stars-Big_1.png"),
                        repeticion = 1.5f, opacidad = 1f, brillo = 1.4f, giro = 0.26f, parpadeo = 0.35f
                    }
                };
            }

            // Nicias: su modelo si está en Personajes/Nicias; si no, una cápsula
            var nicias = new GameObject("Nicias");
            nicias.transform.position = new Vector3(0f, 0.1f, -4f);
            var cuerpo = nicias.AddComponent<CharacterController>();
            cuerpo.height = 1.8f;
            cuerpo.radius = 0.35f;
            cuerpo.center = new Vector3(0f, 0.9f, 0f);

            GameObject cuerpoVisible;
            // Primero el modelo con esqueleto y animaciones; si no está, el estático.
            string rutaAnimado = CarpetaNicias + "/nicias_animado.fbx";
            bool animado = PrepararModeloAnimado(rutaAnimado);
            var modeloNicias = AssetDatabase.LoadAssetAtPath<GameObject>(animado ? rutaAnimado : CarpetaNicias + "/nicias.fbx");
            if (modeloNicias != null)
            {
                cuerpoVisible = (GameObject)PrefabUtility.InstantiatePrefab(modeloNicias, nicias.transform);
                cuerpoVisible.name = "Cuerpo";
                cuerpoVisible.transform.localPosition = Vector3.zero;
                cuerpoVisible.transform.localRotation = Quaternion.identity;
                if (animado)
                {
                    AjustarModeloAnimado(cuerpoVisible, nicias.transform);
                    var animador = cuerpoVisible.GetComponent<Animator>();
                    if (animador == null) animador = cuerpoVisible.AddComponent<Animator>();
                    animador.runtimeAnimatorController = ControladorDeNicias(rutaAnimado);
                    animador.applyRootMotion = false;
                    foreach (SkinnedMeshRenderer piel in cuerpoVisible.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        piel.updateWhenOffscreen = true;
                    }
                }
                Material matModelo = MaterialNicias();
                foreach (Renderer render in cuerpoVisible.GetComponentsInChildren<Renderer>(true))
                {
                    var materiales = new Material[render.sharedMaterials.Length];
                    for (int i = 0; i < materiales.Length; i++) materiales[i] = matModelo;
                    render.sharedMaterials = materiales;
                }
                foreach (Collider estorbo in cuerpoVisible.GetComponentsInChildren<Collider>(true))
                {
                    Object.DestroyImmediate(estorbo);
                }
            }
            else
            {
                Debug.LogWarning("Nostos: falta " + CarpetaNicias + "/nicias.fbx. Nicias será una cápsula.");
                cuerpoVisible = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cuerpoVisible.name = "Cápsula";
                Object.DestroyImmediate(cuerpoVisible.GetComponent<Collider>());
                cuerpoVisible.transform.SetParent(nicias.transform, false);
                cuerpoVisible.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                cuerpoVisible.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                cuerpoVisible.GetComponent<Renderer>().sharedMaterial = matNicias;
            }

            var cabeza = new GameObject("Cabeza");
            cabeza.transform.SetParent(nicias.transform, false);
            cabeza.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            var camAPieGo = new GameObject("Cámara a pie");
            camAPieGo.transform.SetParent(cabeza.transform, false);
            var camAPie = camAPieGo.AddComponent<CinemachineCamera>();
            camAPie.Lens.FieldOfView = 70f;
            camAPie.Lens.NearClipPlane = 0.1f;
            camAPie.Lens.FarClipPlane = 800f;
            camAPie.Priority.Value = 20;

            juego.jugador = nicias.transform;
            var controlador = nicias.AddComponent<ControladorAPie>();
            controlador.control = control;
            controlador.cabeza = cabeza.transform;
            var cuerpoNicias = nicias.AddComponent<CuerpoNicias>();
            cuerpoNicias.cabeza = cabeza.transform;
            cuerpoNicias.camara = camaraGo.transform;
            cuerpoNicias.modelo = cuerpoVisible;

            // La herramienta en la mano (hacha, pico, pala): se ve solo en primera persona.
            var manoGo = new GameObject("Mano");
            manoGo.transform.SetParent(cabeza.transform, false);
            var mano = manoGo.AddComponent<ManoNicias>();
            mano.pie = controlador;
            mano.control = control;
            mano.camara = camaraGo.transform;
            mano.modelos = HerramientasEnMano(manoGo.transform);

            // Cámara del título: un travelling lento sobre el valle, sin Nicias (portada de la F10)
            var camTituloGo = new GameObject("Cámara título");
            var camTitulo = camTituloGo.AddComponent<CinemachineCamera>();
            camTitulo.Lens.FieldOfView = 50f;
            camTitulo.Lens.NearClipPlane = 0.1f;
            camTitulo.Lens.FarClipPlane = 800f;
            camTitulo.Priority.Value = 30;
            var orbita = camTituloGo.AddComponent<CamaraTitulo>();
            orbita.objetivo = null;
            camTituloGo.transform.position = orbita.desde;
            camTituloGo.transform.LookAt(orbita.mira);

            // Calcis en el cielo
            new GameObject("Calcis en el cielo").AddComponent<CieloCalcis>();

            // Sonido y narrativa
            var sonido = sistemas.AddComponent<Sonido>();
            sonido.juego = juego;
            sonido.paso = Clip("paso");
            sonido.golpe = Clip("golpe");
            sonido.entrega = Clip("entrega");
            sonido.campana = Clip("campana");
            sonido.pagina = Clip("pagina");
            sonido.despegue = Clip("despegue");
            sonido.viento = Clip("viento");
            sonido.noche = Clip("noche");
            sonido.musica = Clip("musica");
            var narrativa = sistemas.AddComponent<Narrativa>();
            narrativa.control = control;
            narrativa.juego = juego;
            narrativa.camaraTitulo = camTitulo;

            // Cámara de constructor
            var pivote = new GameObject("Pivote constructor");
            var camConstructorGo = new GameObject("Cámara constructor");
            camConstructorGo.transform.SetParent(pivote.transform, false);
            var camConstructor = camConstructorGo.AddComponent<CinemachineCamera>();
            camConstructor.Lens.FieldOfView = 25f;
            camConstructor.Lens.NearClipPlane = 0.3f;
            camConstructor.Lens.FarClipPlane = 800f;
            camConstructor.Priority.Value = 0;

            var camaraConstructor = pivote.AddComponent<CamaraConstructor>();
            camaraConstructor.control = control;
            camaraConstructor.camara = camConstructor;
            camaraConstructor.objetivoInicial = nicias.transform;
            camaraConstructor.limite = RadioEdificable + 10f;
            pivote.transform.rotation = Quaternion.Euler(0f, camaraConstructor.rumboInicial, 0f);
            Quaternion giro = Quaternion.Euler(camaraConstructor.inclinacion, 0f, 0f);
            camConstructorGo.transform.localRotation = giro;
            camConstructorGo.transform.localPosition = giro * new Vector3(0f, 0f, -camaraConstructor.distancia);

            control.camaraAPie = camAPie;
            control.camaraConstructor = camConstructor;

            // Rejilla y colocación de obras
            var rejillaGo = new GameObject("Rejilla");
            var colocador = rejillaGo.AddComponent<ColocadorRejilla>();
            colocador.control = control;
            colocador.juego = juego;
            colocador.camara = camara;
            colocador.jugador = nicias.transform;
            colocador.materialValido = matValido;
            colocador.materialInvalido = matInvalido;
            colocador.radioEdificable = RadioEdificable;

            // Caminos: losas de 2 × 2 m que se trazan en modo constructor (F10)
            var caminosGo = new GameObject("Caminos");
            var caminos = caminosGo.AddComponent<Caminos>();
            caminos.juego = juego;
            caminos.losas = new[] { Modelo("camino_a"), Modelo("camino_b") };
            if (caminos.losas[0] == null || caminos.losas[1] == null)
                Debug.LogWarning("Nostos: faltan los modelos Naturaleza/camino_a.fbx o camino_b.fbx; los caminos se verán como cajas.");
            caminos.losas = System.Array.FindAll(caminos.losas, m => m != null);
            // Empedrado de la plaza con el borde roto contra la hierba (F17).
            caminos.material = MaterialCamino();
            caminos.colorHierba = UvPaleta(new Color32(150, 160, 74, 255));
            GameObject valleCaminos = GameObject.Find("Valle");
            Transform sueloCaminos = valleCaminos != null ? BuscarHijo(valleCaminos.transform, "Suelo") : null;
            caminos.suelo = sueloCaminos != null ? sueloCaminos.GetComponent<Collider>() : null;
            if (caminos.suelo == null) Debug.LogWarning("Nostos: el suelo del valle no tiene colisionador; los caminos irán a altura fija.");
            colocador.caminos = caminos;

            var lineas = new GameObject("Líneas");
            lineas.transform.SetParent(rejillaGo.transform, false);
            lineas.AddComponent<MeshFilter>();
            var renderLineas = lineas.AddComponent<MeshRenderer>();
            renderLineas.sharedMaterial = matRejilla;
            renderLineas.shadowCastingMode = ShadowCastingMode.Off;
            renderLineas.receiveShadows = false;
            var visual = lineas.AddComponent<RejillaVisual>();
            visual.control = control;
            visual.tamCelda = TamCelda;
            visual.celdasPorLado = Mathf.CeilToInt(RadioEdificable / TamCelda) * 2;

            // HUD
            var hud = sistemas.AddComponent<HudPrototipo>();
            hud.control = control;
            hud.juego = juego;
            hud.jugador = controlador;
            hud.colocador = colocador;
            hud.camara = camara;
            // Inventario a pie (I / ▲): mochila y herramientas (F18).
            var inventario = sistemas.AddComponent<Inventario>();
            inventario.control = control;
            hud.inventario = inventario;

            // Menú de construcción (cuaderno de Nicias) y menú de pausa
            var menuObras = sistemas.AddComponent<MenuConstruccion>();
            menuObras.control = control;
            menuObras.juego = juego;
            menuObras.colocador = colocador;
            var pausa = sistemas.AddComponent<MenuPausa>();
            pausa.control = control;
            pausa.juego = juego;
            hud.pausa = pausa;

            // Farol junto al punto de partida
            GameObject farol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farol.name = "Farol";
            farol.transform.position = new Vector3(-3f, 0.7f, -1f);
            farol.transform.localScale = new Vector3(0.5f, 1.4f, 0.5f);
            farol.GetComponent<Renderer>().sharedMaterial = matFarol;
            var luzFarolGo = new GameObject("Luz");
            luzFarolGo.transform.SetParent(farol.transform, false);
            luzFarolGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var luzFarol = luzFarolGo.AddComponent<Light>();
            luzFarol.type = LightType.Point;
            luzFarol.color = new Color(1f, 0.72f, 0.4f);
            luzFarol.intensity = 12f;
            luzFarol.range = 12f;
            var farolComp = farol.AddComponent<Farol>();
            farolComp.luz = luzFarol;
            farolComp.encendido = true;

            // La nave de Nicias se coloca más abajo, con el surco del aterrizaje (ConstructorNave.cs).

            // Plaza de Nova Lavinia: la hoguera donde se reúnen los colonos
            var plaza = new GameObject("Plaza de Nova Lavinia");
            plaza.transform.position = new Vector3(14f, 0f, -12f);
            juego.plaza = plaza.transform;
            var modeloHoguera = AssetDatabase.LoadAssetAtPath<GameObject>(ImportadorKit.CarpetaArte + "/Naturaleza/hoguera.fbx");
            // La hoguera con el fuego del paquete; su luz es la de la plaza, que parpadea.
            modeloHoguera = ConFuego(modeloHoguera, "hoguera", false);
            if (modeloHoguera != null)
            {
                var hoguera = (GameObject)PrefabUtility.InstantiatePrefab(modeloHoguera, plaza.transform);
                hoguera.transform.localPosition = Vector3.zero;
            }
            var luzHogueraGo = new GameObject("Luz de la hoguera");
            luzHogueraGo.transform.SetParent(plaza.transform, false);
            luzHogueraGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var luzHoguera = luzHogueraGo.AddComponent<Light>();
            luzHoguera.type = LightType.Point;
            luzHoguera.color = new Color(1f, 0.6f, 0.28f);
            luzHoguera.intensity = 14f;
            luzHoguera.range = 14f;
            var llamaHoguera = luzHogueraGo.AddComponent<Llama>();
            llamaHoguera.luz = luzHoguera;
            llamaHoguera.intensidad = 14f;
            AudioClip clipFuego = Clip("fuego");
            if (clipFuego != null)
            {
                var crepitar = plaza.AddComponent<AudioSource>();
                crepitar.clip = clipFuego;
                crepitar.loop = true;
                crepitar.playOnAwake = true;
                crepitar.spatialBlend = 1f;
                crepitar.rolloffMode = AudioRolloffMode.Linear;
                crepitar.minDistance = 2f;
                crepitar.maxDistance = 26f;
                crepitar.volume = 0.8f;
            }
            // El arconte de Calcis: solo aparece el día del tributo
            var arconteGo = new GameObject("Arconte de Calcis");
            arconteGo.transform.SetParent(plaza.transform, false);
            arconteGo.transform.localPosition = new Vector3(-4.5f, 0f, 3f);
            GameObject figura = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            figura.name = "Figura";
            figura.transform.SetParent(arconteGo.transform, false);
            figura.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            figura.transform.localScale = new Vector3(0.7f, 1.05f, 0.7f);
            figura.layer = Juego.CapaColonos;
            figura.GetComponent<Collider>().isTrigger = true;
            figura.GetComponent<Renderer>().sharedMaterial = matArconte;
            var arconte = arconteGo.AddComponent<Arconte>();
            arconte.juego = juego;
            arconte.figura = figura;
            figura.SetActive(false);

            // La hoguera ocupa su sitio en la rejilla para que no se construya encima.
            var reserva = plaza.AddComponent<ReservaRejilla>();
            reserva.radio = 5f;
            AdornarPlaza(plaza.transform, valle);

            libres.Clear();
            libres.Add(new Vector3(plaza.transform.position.x, 6.5f, plaza.transform.position.z));
            ColocarPanaderia(valle, plaza.transform.position);
            ColocarNave(valle);
            SembrarNaturaleza(valle);
            ColocarTunelYPuente(valle, juego);

            // Navegación de los colonos: NavMesh del llano, generado al empezar a jugar
            var navegacionGo = new GameObject("Navegación");
            var superficie = navegacionGo.AddComponent<NavMeshSurface>();
            superficie.collectObjects = CollectObjects.Volume;
            superficie.center = new Vector3(0f, 8f, 0f);
            superficie.size = new Vector3(216f, 30f, 216f);
            superficie.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            superficie.layerMask = ~(1 << Juego.CapaColonos);
            var navegacion = navegacionGo.AddComponent<Navegacion>();
            navegacion.juego = juego;

            EditorSceneManager.SaveScene(escena, RutaEscena);
            PonerPrimeraEnBuild(RutaEscena);
            AssetDatabase.SaveAssets();
            File.WriteAllText(RutaMarca, Version);
            Debug.Log("Nostos: escena " + Version + " creada en " + RutaEscena + ". Pulsa Play.");
        }

        // ------------------------------------------------------------ catálogo

        static Cantidad C(Recurso recurso, int cantidad) => new Cantidad(recurso, cantidad);
        static Cantidad[] L(params Cantidad[] lista) => lista;

        static DefinicionEdificio[] CrearCatalogo()
        {
            DefinicionEdificio refugio = Definir("refugio", "Refugio del náufrago", 2, 2, TipoEdificio.Refugio,
                L(C(Recurso.Madera, 4)),
                L(C(Recurso.Madera, 6)),
                L(C(Recurso.Madera, 4), C(Recurso.Mineral, 2)),
                L(), L(), 1);

            // Banco de trabajo (F10): madera en herramientas. Las herramientas en la mochila dan
            // una unidad más al talar o picar y las gasta la palanca de seguridad de los colonos.
            DefinicionEdificio banco = Definir("banco", "Banco de trabajo", 2, 2, TipoEdificio.Produccion,
                L(C(Recurso.Madera, 3)),
                L(C(Recurso.Madera, 4)),
                L(C(Recurso.Madera, 3), C(Recurso.Mineral, 1)),
                L(C(Recurso.Madera, 2)), L(C(Recurso.Herramientas, 1)), 6);

            DefinicionEdificio aserradero = Definir("aserradero", "Aserradero", 3, 2, TipoEdificio.Produccion,
                L(C(Recurso.Madera, 6), C(Recurso.Mineral, 2)),
                L(C(Recurso.Madera, 8)),
                L(C(Recurso.Madera, 4), C(Recurso.Mineral, 4)),
                L(C(Recurso.Madera, 1)), L(C(Recurso.Tablones, 2)), 4);

            DefinicionEdificio fundicion = Definir("fundicion", "Fundición", 3, 3, TipoEdificio.Produccion,
                L(C(Recurso.Madera, 4), C(Recurso.Mineral, 6)),
                L(C(Recurso.Tablones, 8)),
                L(C(Recurso.Tablones, 6), C(Recurso.Mineral, 6)),
                L(C(Recurso.Mineral, 2), C(Recurso.Madera, 1)),
                L(C(Recurso.Lingotes, 1), C(Recurso.Chapa, 1)), 6);

            DefinicionEdificio almacen = Definir("almacen", "Almacén", 3, 2, TipoEdificio.Almacen,
                L(C(Recurso.Madera, 6)),
                L(C(Recurso.Tablones, 6)),
                L(C(Recurso.Tablones, 4), C(Recurso.Mineral, 2)),
                L(), L(), 1);

            DefinicionEdificio casa = Definir("casa", "Casa de colonos", 2, 2, TipoEdificio.Casa,
                L(C(Recurso.Madera, 4)),
                L(C(Recurso.Tablones, 6)),
                L(C(Recurso.Tablones, 4), C(Recurso.Lingotes, 2)),
                L(), L(), 1);

            DefinicionEdificio comedor = Definir("comedor", "Comedor", 3, 2, TipoEdificio.Produccion,
                L(C(Recurso.Madera, 6), C(Recurso.Mineral, 2)),
                L(C(Recurso.Tablones, 8)),
                L(C(Recurso.Tablones, 4), C(Recurso.Lingotes, 2)),
                L(C(Recurso.Comida, 1)), L(C(Recurso.Raciones, 2)), 5);

            DefinicionEdificio pozo = Definir("pozo", "Pozo", 2, 2, TipoEdificio.Produccion,
                L(C(Recurso.Mineral, 4)),
                L(C(Recurso.Tablones, 4)),
                L(C(Recurso.Lingotes, 2)),
                L(), L(C(Recurso.Agua, 1)), 4);

            DefinicionEdificio taller = Definir("taller", "Taller eléctrico", 3, 2, TipoEdificio.Produccion,
                L(C(Recurso.Tablones, 6), C(Recurso.Mineral, 4)),
                L(C(Recurso.Tablones, 6), C(Recurso.Lingotes, 4)),
                L(C(Recurso.Chapa, 4), C(Recurso.Cobre, 4)),
                L(C(Recurso.Cobre, 2)), L(C(Recurso.Cable, 1), C(Recurso.Bobinas, 1)), 6);

            DefinicionEdificio optica = Definir("optica", "Óptica", 3, 3, TipoEdificio.Produccion,
                L(C(Recurso.Tablones, 8), C(Recurso.Mineral, 4)),
                L(C(Recurso.Lingotes, 6), C(Recurso.Chapa, 4)),
                L(C(Recurso.Cable, 4), C(Recurso.Cristal, 4)),
                L(C(Recurso.Cristal, 2)), L(C(Recurso.Lentes, 1), C(Recurso.Nucleos, 1)), 8);

            DefinicionEdificio astillero = Definir("astillero", "Astillero", 3, 3, TipoEdificio.Servicio,
                L(C(Recurso.Tablones, 8), C(Recurso.Mineral, 6)),
                L(C(Recurso.Tablones, 8), C(Recurso.Lingotes, 6)),
                L(C(Recurso.Chapa, 4), C(Recurso.Lingotes, 4)),
                L(), L(), 1);

            DefinicionEdificio teleferico = Definir("teleferico", "Teleférico de carga", 2, 2, TipoEdificio.Servicio,
                L(C(Recurso.Tablones, 6), C(Recurso.Mineral, 4)),
                L(C(Recurso.Tablones, 8), C(Recurso.Lingotes, 4)),
                L(C(Recurso.Cobre, 6), C(Recurso.Cable, 4)),
                L(), L(), 1);

            // Herrería (F17): la casa del herrero del paquete, tal cual. Hace herramientas para el pueblo
            // con mineral y leña, y en su yunque Nicias forja también el pico bueno.
            if (HayHerreria)
            {
                DefinicionEdificio herreria = Definir("herreria", "Herrería", AnchoHerreria, FondoHerreria, TipoEdificio.Produccion,
                    L(C(Recurso.Madera, 8), C(Recurso.Mineral, 6)),
                    L(C(Recurso.Tablones, 10)),
                    L(C(Recurso.Tablones, 6), C(Recurso.Lingotes, 4)),
                    L(C(Recurso.Mineral, 2), C(Recurso.Madera, 1)), L(C(Recurso.Herramientas, 3)), 6);
                return new[] { refugio, banco, herreria, aserradero, fundicion, almacen, casa, comedor, pozo, taller, optica, astillero, teleferico };
            }
            return new[] { refugio, banco, aserradero, fundicion, almacen, casa, comedor, pozo, taller, optica, astillero, teleferico };
        }

        static DefinicionEdificio Definir(string id, string nombre, int ancho, int fondo, TipoEdificio tipo,
            Cantidad[] cimientos, Cantidad[] esqueleto, Cantidad[] casi,
            Cantidad[] entradas, Cantidad[] salidas, int ticks)
        {
            string ruta = CarpetaDatos + "/" + id + ".asset";
            var definicion = AssetDatabase.LoadAssetAtPath<DefinicionEdificio>(ruta);
            if (definicion == null)
            {
                // Solo se rellenan los valores al crear la ficha: después manda lo que se edite en el inspector.
                definicion = ScriptableObject.CreateInstance<DefinicionEdificio>();
                definicion.id = id;
                definicion.nombre = nombre;
                definicion.tipo = tipo;
                definicion.costeCimientos = cimientos;
                definicion.costeEsqueleto = esqueleto;
                definicion.costeCasiTerminado = casi;
                definicion.entradas = entradas;
                definicion.salidas = salidas;
                definicion.ticksPorCiclo = ticks;
                AssetDatabase.CreateAsset(definicion, ruta);
            }

            // El tamaño lo marca el modelo (la casa pasó de 2x2 a 3x2 con sus dos plantas): se fija siempre.
            definicion.ancho = ancho;
            definicion.fondo = fondo;

            if (id == "herreria")
            {
                definicion.modelos = ModelosHerreria() ?? new GameObject[4];
                EditorUtility.SetDirty(definicion);
                return definicion;
            }

            var modelos = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                string rutaModelo = ImportadorKit.CarpetaArte + "/Edificios/" + id + "_" + (i + 1) + ".fbx";
                modelos[i] = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
                if (modelos[i] == null) Debug.LogWarning("Nostos: falta el modelo " + rutaModelo + ".");
            }
            if (id == "comedor" && modelos[3] != null) modelos[3] = ComedorConMesa(modelos[3]);
            // Hogares, hornos y la olla: fuego del paquete en lugar de las llamas de color plano.
            for (int i = 0; i < 4; i++) modelos[i] = ConFuego(modelos[i], id + "_" + (i + 1));
            definicion.modelos = modelos;
            EditorUtility.SetDirty(definicion);
            return definicion;
        }

        // ------------------------------------------------------------ terreno y naturaleza

        static Valle CrearTerreno()
        {
            string rutaJson = CarpetaTerreno + "/valle.json";
            string rutaAltura = CarpetaTerreno + "/valle_altura.bytes";
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaTerreno + "/valle.fbx");
            if (modelo == null || !File.Exists(rutaJson) || !File.Exists(rutaAltura)) return null;

            var valle = new Valle();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(rutaJson), valle.info);
            byte[] bytes = File.ReadAllBytes(rutaAltura);
            int lado = valle.info.resolucion;
            if (bytes.Length != lado * lado * 2)
            {
                Debug.LogError("Nostos: valle_altura.bytes no coincide con valle.json. Vuelve a copiar los tres archivos del terreno.");
                return null;
            }
            float rango = valle.info.alturaMaxima - valle.info.alturaMinima;
            valle.alturas = new float[lado, lado];
            for (int z = 0; z < lado; z++)
            {
                for (int x = 0; x < lado; x++)
                {
                    int i = (z * lado + x) * 2;
                    valle.alturas[z, x] = valle.info.alturaMinima + (bytes[i] | (bytes[i + 1] << 8)) / 65535f * rango;
                }
            }

            // Lo que quedaba del terreno anterior (Terrain de Unity) ya no se usa.
            AssetDatabase.DeleteAsset(CarpetaTerreno + "/Valle.asset");
            AssetDatabase.DeleteAsset(CarpetaTerreno + "/Valle.terrainlayer");

            var objeto = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            objeto.name = "Valle";
            objeto.transform.position = Vector3.zero;
            objeto.isStatic = true;
            foreach (Transform hijo in objeto.GetComponentsInChildren<Transform>(true))
            {
                hijo.gameObject.isStatic = true;
                if (hijo.name == "Suelo")
                {
                    // Césped del paquete "A piece of nature" donde la paleta es hierba.
                    Material sueloValle = MaterialSueloValle();
                    var renderSuelo = hijo.GetComponent<Renderer>();
                    if (sueloValle != null && renderSuelo != null) renderSuelo.sharedMaterial = sueloValle;
                }
                if (hijo.name != "Agua") continue;
                // El agua no es suelo: se vadea, no se pisa.
                foreach (Collider c in hijo.GetComponents<Collider>()) Object.DestroyImmediate(c);
                hijo.gameObject.layer = Juego.CapaColonos;
                var render = hijo.GetComponent<Renderer>();
                if (render != null)
                {
                    render.shadowCastingMode = ShadowCastingMode.Off;
                    render.sharedMaterial = MaterialAgua();
                    TexturarAgua(hijo.gameObject);
                }
            }
            return valle;
        }

        /// <summary>El agua usa la misma paleta, pero brilla y deja ver un poco el fondo.</summary>
        static Material MaterialAgua()
        {
            Material material = CargarOCrear("Agua", "Universal Render Pipeline/Lit");
            var paleta = AssetDatabase.LoadAssetAtPath<Texture2D>(ImportadorKit.RutaTextura);
            if (paleta != null)
            {
                material.SetTexture("_BaseMap", paleta);
                material.mainTexture = paleta;
            }
            var color = new Color(1f, 1f, 1f, 0.82f);
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Smoothness", 0.85f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// El río con la textura de agua original del paquete "Lowpoly Textures Pack". La malla se
        /// copia con UV en planta, un dibujo cada 6 m;
        /// la espuma de la cascada se queda con el color de la paleta en un segundo material.
        /// </summary>
        static void TexturarAgua(GameObject agua)
        {
            var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturasPaquete + "Water_A.png");
            var filtro = agua.GetComponent<MeshFilter>();
            var render = agua.GetComponent<MeshRenderer>();
            if (textura == null || filtro == null || render == null || filtro.sharedMesh == null) return;

            Mesh malla = Object.Instantiate(filtro.sharedMesh);
            malla.name = "Agua del río";
            Vector3[] vertices = malla.vertices;
            Vector2[] uv = malla.uv;
            int[] triangulos = malla.triangles;
            if (uv == null || uv.Length != vertices.Length) return;

            // Los triángulos de agua son los del color de paleta que más se repite; el resto, espuma.
            var cuenta = new Dictionary<int, int>();
            for (int i = 0; i < triangulos.Length; i += 3)
            {
                int fila = Mathf.RoundToInt(uv[triangulos[i]].y * 1000f);
                cuenta[fila] = cuenta.TryGetValue(fila, out int n) ? n + 1 : 1;
            }
            int filaAgua = 0, maximo = -1;
            foreach (KeyValuePair<int, int> par in cuenta)
            {
                if (par.Value > maximo) { maximo = par.Value; filaAgua = par.Key; }
            }
            var agua0 = new List<int>();
            var espuma = new List<int>();
            var nuevaUv = (Vector2[])uv.Clone();
            for (int i = 0; i < triangulos.Length; i += 3)
            {
                bool esAgua = Mathf.RoundToInt(uv[triangulos[i]].y * 1000f) == filaAgua;
                for (int k = 0; k < 3; k++)
                {
                    int v = triangulos[i + k];
                    (esAgua ? agua0 : espuma).Add(v);
                    if (!esAgua) continue;
                    Vector3 w = agua.transform.TransformPoint(vertices[v]);
                    nuevaUv[v] = new Vector2(w.x, w.z) / 6f;
                }
            }
            malla.uv = nuevaUv;
            malla.subMeshCount = 2;
            malla.SetTriangles(agua0, 0);
            malla.SetTriangles(espuma, 1);
            malla.RecalculateBounds();

            string ruta = CarpetaTerreno + "/agua_rio.asset";
            AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(malla, ruta);
            filtro.sharedMesh = malla;

            Material rio = CargarOCrear("AguaRio", "Universal Render Pipeline/Lit");
            Material deLaPaleta = MaterialAgua();
            rio.CopyPropertiesFromMaterial(deLaPaleta);
            rio.shader = deLaPaleta.shader;
            rio.SetTexture("_BaseMap", textura);
            rio.mainTexture = textura;
            rio.SetTextureScale("_BaseMap", Vector2.one);
            rio.SetFloat("_Smoothness", 0.8f);
            rio.SetOverrideTag("RenderType", "Transparent");
            rio.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            rio.SetShaderPassEnabled("DepthOnly", false);
            rio.SetShaderPassEnabled("ShadowCaster", false);
            rio.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(rio);
            render.sharedMaterials = new[] { rio, deLaPaleta };
            if (agua.GetComponent<AguaQueCorre>() == null) agua.AddComponent<AguaQueCorre>();
        }

        /// <summary>Material con una textura original del paquete "Lowpoly Textures Pack".</summary>
        /// <summary>Material de los caminos: empedrado Summer_Rocks y tierra Summer_Dirt_A en el borde.</summary>
        static Material MaterialCamino()
        {
            var piedra = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturasPaquete + "Summer_Rocks.png");
            var tierra = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturasPaquete + "Summer_Dirt_A.png");
            if (piedra == null || Shader.Find("Nostos/Camino") == null) return null;
            Material material = CargarOCrear("CaminoEmpedrado", "Nostos/Camino");
            material.shader = Shader.Find("Nostos/Camino");
            material.SetTexture("_BaseMap", piedra);
            material.mainTexture = piedra;
            if (tierra != null) material.SetTexture("_Tierra", tierra);
            // El borde se funde en el césped pintado igual que el suelo del valle.
            var paleta = AssetDatabase.LoadAssetAtPath<Texture2D>(ImportadorKit.RutaTextura);
            if (paleta != null) material.SetTexture("_Paleta", paleta);
            CespedEn(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MaterialTextura(string nombre, string archivo)
        {
            var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturasPaquete + archivo);
            if (textura == null) return null;
            Material material = CargarOCrear(nombre, "Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", textura);
            material.mainTexture = textura;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Disco plano de borde irregular a ras de suelo (empedrado de la plaza, tierra pisada),
        /// con UV en planta para que la textura no se estire. Sin colisionador: se pisa el valle.
        /// </summary>
        static GameObject Disco(string nombre, Vector3 centro, float radio, float metrosPorDibujo, Material material, int semilla)
        {
            const int Lados = 40;
            var azar = new System.Random(semilla);
            var vertices = new Vector3[Lados + 1];
            var uv = new Vector2[Lados + 1];
            var triangulos = new int[Lados * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(centro.x, centro.z) / metrosPorDibujo;
            for (int i = 0; i < Lados; i++)
            {
                float a = i * Mathf.PI * 2f / Lados;
                float r = radio * (0.94f + 0.08f * (float)azar.NextDouble());
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                uv[i + 1] = new Vector2(centro.x + vertices[i + 1].x, centro.z + vertices[i + 1].z) / metrosPorDibujo;
                triangulos[i * 3] = 0;
                triangulos[i * 3 + 1] = (i + 1) % Lados + 1;
                triangulos[i * 3 + 2] = i + 1;
            }
            var malla = new Mesh { name = nombre, vertices = vertices, uv = uv, triangles = triangulos };
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            string ruta = CarpetaTerreno + "/" + nombre.ToLowerInvariant().Replace(' ', '_') + ".asset";
            AssetDatabase.DeleteAsset(ruta);
            AssetDatabase.CreateAsset(malla, ruta);

            var go = new GameObject(nombre);
            go.transform.position = centro;
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var render = go.AddComponent<MeshRenderer>();
            render.sharedMaterial = material;
            render.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }

        static GameObject Modelo(string nombre)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(ImportadorKit.CarpetaArte + "/Naturaleza/" + nombre + ".fbx");
        }

        /// <summary>La boca del túnel hacia la civilización, en la pared del noreste, y el puente sobre el río.</summary>
        static void ColocarTunelYPuente(Valle valle, Juego juego)
        {
            GameObject modeloTunel = Modelo("tunel");
            if (modeloTunel != null)
            {
                var tunel = (GameObject)PrefabUtility.InstantiatePrefab(modeloTunel);
                tunel.name = "Túnel a la civilización";
                var lugar = new Vector3(valle.info.tunelX, 0f, valle.info.tunelZ);
                tunel.transform.position = lugar;
                // La entrada del modelo mira a +Z: se gira hacia el centro del valle.
                tunel.transform.rotation = Quaternion.LookRotation(-lugar.normalized, Vector3.up);
                tunel.AddComponent<Tunel>().juego = juego;
                foreach (float lado in new[] { -2.2f, 2.2f })
                {
                    // Antorchas con el fuego rojo del paquete y su luz.
                    Llama antorcha = PonerLlama(tunel.transform, new Vector3(lado, 2.75f, -2.5f), 0.3f, 10f, 9f);
                    antorcha.name = "Antorcha";
                }
            }

            // Restos de la vieja calzada de Calcis, junto al camino que baja del túnel.
            var ruinas = new[]
            {
                ("ruina_arco", new Vector3(60.2f, 0f, 41.8f), 40f),
                ("columna_rota", new Vector3(44.5f, 0f, 41.5f), 110f),
                ("obelisco", new Vector3(36.5f, 0f, 33.5f), 225f),
                ("columna_rota", new Vector3(-66f, 0f, 9f), 300f)
            };
            foreach ((string nombre, Vector3 lugar, float giro) in ruinas)
            {
                GameObject modeloRuina = Modelo(nombre);
                if (modeloRuina == null || !valle.Llano(lugar.x, lugar.z, 2.4f)) continue;
                var ruina = (GameObject)PrefabUtility.InstantiatePrefab(modeloRuina);
                ruina.name = "Ruina (" + nombre + ")";
                ruina.transform.position = new Vector3(lugar.x, valle.Altura(lugar.x, lugar.z) - 0.05f, lugar.z);
                ruina.transform.rotation = Quaternion.Euler(0f, giro, 0f);
            }

            GameObject modeloPuente = Modelo("puente");
            if (modeloPuente != null)
            {
                var puente = (GameObject)PrefabUtility.InstantiatePrefab(modeloPuente);
                puente.name = "Puente del río";
                puente.transform.position = new Vector3(valle.info.puenteX, 0.05f, valle.info.puenteZ);
            }
        }

        // Paquete "Low Poly Environment - Nature Free" de Polytope Studio (lo añadió Toni). Si no
        // está, se siembra con los modelos propios de Arte/Naturaleza como antes.
        const string CarpetaPolytope = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";

        static GameObject Polytope(string ruta)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaPolytope + ruta + ".prefab");
        }

        /// <summary>Tronco para chocar e interactuar: los prefabs de Polytope no traen colisionador.</summary>
        static void Tronco(GameObject arbol, float radio, float alto)
        {
            if (arbol.GetComponentInChildren<Collider>() != null) return;
            var capsula = arbol.AddComponent<CapsuleCollider>();
            capsula.radius = radio;
            capsula.height = alto;
            capsula.center = new Vector3(0f, alto / 2f, 0f);
        }

        /// <summary>Caja con el tamaño de lo que se ve, para rocas (algo más estrecha que la roca).</summary>
        /// <summary>Roca que se abolla y suelta esquirlas al picarla. La malla tiene que ser legible.</summary>
        static void Picable(GameObject roca)
        {
            foreach (MeshFilter f in roca.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null || f.sharedMesh.isReadable) continue;
                var importador = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(f.sharedMesh)) as ModelImporter;
                if (importador == null || importador.isReadable) continue;
                importador.isReadable = true;
                importador.SaveAndReimport();
            }
            var picable = roca.AddComponent<RocaPicable>();
            picable.polvo = MaterialHumo();
        }

        static void CajaVisible(GameObject objeto, float encoger = 0.85f)
        {
            if (objeto.GetComponentInChildren<Collider>() != null) return;
            // Se mide con el objeto sin girar ni escalar, así la caja sale en sus ejes.
            Transform t = objeto.transform;
            Vector3 posicion = t.position, escala = t.localScale;
            Quaternion giro = t.rotation;
            t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
            Renderer[] renders = objeto.GetComponentsInChildren<Renderer>();
            if (renders.Length > 0)
            {
                Bounds caja = renders[0].bounds;
                foreach (Renderer r in renders) caja.Encapsulate(r.bounds);
                var colisionador = objeto.AddComponent<BoxCollider>();
                colisionador.center = caja.center;
                colisionador.size = new Vector3(caja.size.x * encoger, caja.size.y, caja.size.z * encoger);
            }
            t.SetPositionAndRotation(posicion, giro);
            t.localScale = escala;
        }

        /// <summary>El adorno del suelo (hierba, flores) no echa sombra: es mucho y no se nota.</summary>
        static void SinSombra(GameObject objeto)
        {
            foreach (Renderer r in objeto.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------ paquetes de la tienda
        // Toni añadió "Pine Forest - LowPoly", "Lowpoly Baker's House", "Lowpoly Dinner Table" y
        // "Lowpoly Textures Pack". Se usan tal cual, con sus texturas y materiales: son el listón
        // de calidad del juego. Solo se colocan y se ajusta el tamaño.
        const string CarpetaPinos = "Assets/AssetPacks/Pine Forest_LowPoly/";
        const string CarpetaMesa = "Assets/Dinner table/Prefabs/";
        const string CarpetaPanadero = "Assets/LowpolyBakersHouse/Prefabs/";
        const string CarpetaTexturasPaquete = "Assets/LowpolyTexturesPack/Textures/";
        const string CarpetaPrefabsPaquetes = "Assets/Nostos/Prefabs/Paquetes";

        // Escala de los pinos: el más alto del paquete (Evergreen_TallTall) mide 11,8 m.
        static float escalaPinos = -1f;

        /// <summary>Caja de todo lo que se ve del objeto, en coordenadas del mundo.</summary>
        static Bounds Medir(GameObject objeto)
        {
            Renderer[] renders = objeto.GetComponentsInChildren<Renderer>();
            if (renders.Length == 0) return new Bounds(objeto.transform.position, Vector3.zero);
            Bounds caja = renders[0].bounds;
            foreach (Renderer r in renders) caja.Encapsulate(r.bounds);
            return caja;
        }

        /// <summary>Mueve el objeto para que su base quede en `punto` y su centro en planta encima.</summary>
        static void Asentar(GameObject objeto, Vector3 punto)
        {
            Bounds caja = Medir(objeto);
            objeto.transform.position += new Vector3(punto.x - caja.center.x, punto.y - caja.min.y, punto.z - caja.center.z);
        }

        static Transform BuscarHijo(Transform t, string nombre)
        {
            if (t.name == nombre) return t;
            foreach (Transform hijo in t)
            {
                Transform r = BuscarHijo(hijo, nombre);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>
        /// Prefab con una de las mallas del FBX de "Pine Forest - LowPoly" y el material original del
        /// paquete (el de viento para los árboles). Base en el suelo y centrado; `factor` sobre la escala
        /// común de los pinos.
        /// </summary>
        static GameObject PinoDelPaquete(string nodo, float factor, bool viento)
        {
            string ruta = CarpetaPrefabsPaquetes + "/" + nodo + ".prefab";
            var hecho = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (hecho != null) return hecho;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaPinos + "Mesh/Pine Forest_LowPoly.fbx");
            if (fbx == null) return null;
            if (escalaPinos < 0f)
            {
                Transform alto = BuscarHijo(fbx.transform, "Evergreen_TallTall");
                MeshFilter f = alto != null ? alto.GetComponent<MeshFilter>() : null;
                if (f == null || f.sharedMesh == null) return null;
                escalaPinos = 11.76f / CajaGirada(f.sharedMesh, alto.rotation, alto.lossyScale).size.y;
            }
            Transform t = BuscarHijo(fbx.transform, nodo);
            MeshFilter filtro = t != null ? t.GetComponent<MeshFilter>() : null;
            if (filtro == null || filtro.sharedMesh == null)
            {
                Debug.LogWarning("Nostos: no está la malla " + nodo + " en el paquete de pinos.");
                return null;
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                CarpetaPinos + "Materials/Pine Forest_LowPoly" + (viento ? "_Wind" : "") + "_material.mat");

            var raiz = new GameObject(nodo);
            var pieza = new GameObject("Modelo");
            pieza.transform.SetParent(raiz.transform, false);
            pieza.AddComponent<MeshFilter>().sharedMesh = filtro.sharedMesh;
            pieza.AddComponent<MeshRenderer>().sharedMaterial = material;
            float k = escalaPinos * factor;
            Vector3 escala = t.lossyScale * k;
            Bounds caja = CajaGirada(filtro.sharedMesh, t.rotation, escala);
            pieza.transform.localRotation = t.rotation;
            pieza.transform.localScale = escala;
            pieza.transform.localPosition = new Vector3(-caja.center.x, -caja.min.y, -caja.center.z);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            Object.DestroyImmediate(raiz);
            return prefab;
        }

        static Bounds CajaGirada(Mesh malla, Quaternion giro, Vector3 escala)
        {
            Bounds b = malla.bounds;
            var caja = new Bounds(giro * Vector3.Scale(b.min, escala), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var esquina = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                caja.Encapsulate(giro * Vector3.Scale(esquina, escala));
            }
            return caja;
        }

        /// <summary>Los nombres que usa la siembra, con su malla del paquete de pinos.</summary>
        static GameObject Pinar(string nombre)
        {
            switch (nombre)
            {
                case "pino_1": return PinoDelPaquete("Evergreen_TallTall", 1f, true);
                case "pino_2": return PinoDelPaquete("Evergreen_TallWide", 1f, true);
                case "pino_3": return PinoDelPaquete("Evergreen_TallMed", 1f, true);
                case "pino_4": return PinoDelPaquete("EvergreenTallThick", 1f, true);
                case "pino_5": return PinoDelPaquete("Evergreen_TallShort", 1f, true);
                case "pino_6": return PinoDelPaquete("Evergreen_Med_Tall", 1f, true);
                case "pino_joven_1": return PinoDelPaquete("Evergreen_Med", 1f, true);
                case "pino_joven_2": return PinoDelPaquete("Evergreen_SmallBig", 1f, true);
                case "pino_seco_1": return PinoDelPaquete("Dead4", 1f, false);
                case "pino_seco_2": return PinoDelPaquete("Dead2", 1f, false);
                case "tocon_pino": return PinoDelPaquete("Dead3", 0.45f, false);
                case "mata_1": return PinoDelPaquete("Bush1_", 1.5f, false);
                case "mata_2": return PinoDelPaquete("Bush2_", 1.5f, false);
                case "suelo_hierba_1": return PinoDelPaquete("GroundCover_Grass2", 1f, false);
                case "suelo_hierba_2": return PinoDelPaquete("GroundCover_Grass3", 1f, false);
                case "suelo_tierra_1": return PinoDelPaquete("GroundCover_Dirt", 1f, false);
                case "suelo_tierra_2": return PinoDelPaquete("GroundCover_Dirt1", 1f, false);
            }
            return null;
        }

        /// <summary>
        /// Pone una pieza de "Lowpoly Dinner Table" dentro del comedor. Las medidas son las de la
        /// colocación que se probó en Blender (Pipeline/modelos_externos.py, mesa_comedor): posición
        /// en el plano del suelo del edificio (x, z de Unity), giro en Y y escala relativa a la mesa.
        /// </summary>
        static GameObject PiezaDeMesa(string prefab, Transform padre, float escala, Vector3 sitio, float giro, bool chocar)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaMesa + prefab + ".prefab");
            if (modelo == null)
            {
                Debug.LogWarning("Nostos: falta " + CarpetaMesa + prefab + ".prefab (paquete Lowpoly Dinner Table con sus materiales URP).");
                return null;
            }
            var pieza = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
            pieza.transform.localRotation = Quaternion.Euler(0f, giro, 0f);
            pieza.transform.localScale = Vector3.one * escala;
            Asentar(pieza, padre.TransformPoint(sitio));
            foreach (Collider c in pieza.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            if (chocar) CajaVisible(pieza, 0.95f);
            return pieza;
        }

        /// <summary>
        /// El comedor terminado con la mesa original del paquete: mesa, cuatro sillas, alfombra, la
        /// fuente del pollo con verduras, platos, jarras y velas encendidas. Se guarda como prefab y
        /// es el modelo del último estado del comedor.
        /// </summary>
        static GameObject ComedorConMesa(GameObject comedor)
        {
            var mesaModelo = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaMesa + "Table.prefab");
            if (mesaModelo == null)
            {
                Debug.LogWarning("Nostos: falta la mesa del paquete Lowpoly Dinner Table; el comedor se queda sin mesa.");
                return comedor;
            }
            var raiz = (GameObject)PrefabUtility.InstantiatePrefab(comedor);
            raiz.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var mesa = new GameObject("Mesa del comedor").transform;
            mesa.SetParent(raiz.transform, false);

            const float Suelo = 0.12f;                 // LOSA de Pipeline/detalle.py
            float cx = 0.75f, cz = 0f;                 // centro de la mesa en el comedor
            // Escala del paquete: la que deja el tablero a 0,73 m.
            var prueba = (GameObject)PrefabUtility.InstantiatePrefab(mesaModelo);
            float altoMesa = Medir(prueba).size.y;
            Object.DestroyImmediate(prueba);
            float k = altoMesa > 0.01f ? 0.73f / altoMesa : 1f;
            float R(float escalaEnBlender) => k * escalaEnBlender / 0.82f;

            PiezaDeMesa("Carpet_2_1", mesa, R(1.45f), new Vector3(cx, Suelo + 0.003f, cz), 0f, false);
            GameObject tablero = PiezaDeMesa("Table", mesa, R(0.82f), new Vector3(cx, Suelo, cz), -90f, true);
            foreach (float lado in new[] { -1f, 1f })
            {
                foreach (float dx in new[] { -0.5f, 0.5f })
                {
                    // En Blender: silla en (x + dx, y + lado * 0,62) girada 180 (lado -1) o 360 (lado 1).
                    PiezaDeMesa("Chair", mesa, R(0.82f), new Vector3(cx - dx, Suelo, cz - lado * 0.62f), lado < 0f ? -180f : -360f, true);
                }
            }
            float sobre = tablero != null ? Medir(tablero).max.y : Suelo + 0.73f;
            PiezaDeMesa("Chicken_plate", mesa, R(0.55f), new Vector3(cx, sobre, cz), 0f, false);
            foreach (Vector2 p in new[] { new Vector2(-0.5f, -1f), new Vector2(0.5f, 1f), new Vector2(0.5f, -1f), new Vector2(-0.5f, 1f) })
                PiezaDeMesa("Plate", mesa, R(0.4f), new Vector3(cx - p.x, sobre, cz - p.y * 0.3f), 0f, false);
            PiezaDeMesa("Cup", mesa, R(0.42f), new Vector3(cx + 0.22f, sobre, cz + 0.3f), 90f, false);
            PiezaDeMesa("Cup", mesa, R(0.42f), new Vector3(cx - 0.75f, sobre, cz - 0.3f), -90f, false);
            PiezaDeMesa("Candle_1", mesa, R(0.7f), new Vector3(cx + 0.78f, sobre, cz + 0.05f), 0f, false);
            PiezaDeMesa("Candle_1", mesa, R(0.7f), new Vector3(cx - 0.8f, sobre, cz + 0.05f), 0f, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, CarpetaPrefabsPaquetes + "/comedor_4_con_mesa.prefab");
            Object.DestroyImmediate(raiz);
            return prefab != null ? prefab : comedor;
        }

        // Sitios donde no se siembra nada (x, radio, z): la plaza y la vieja panadería.
        static readonly List<Vector3> libres = new List<Vector3>();

        static bool Ocupado(float x, float z)
        {
            foreach (Vector3 l in libres)
            {
                if (new Vector2(x - l.x, z - l.z).magnitude < l.y) return true;
            }
            return false;
        }

        /// <summary>Barril original del paquete "Lowpoly Baker's House", de 0,85 m de alto.</summary>
        static void Barril(Transform padre, Valle valle, Vector3 p, float giro, float escala = 1f)
        {
            GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaPanadero + "Barrel.prefab");
            if (modelo == null) return;
            var barril = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
            barril.transform.rotation = Quaternion.Euler(0f, giro, 0f);
            float alto = Medir(barril).size.y;
            barril.transform.localScale = Vector3.one * (alto > 0.01f ? 0.85f * escala / alto : escala);
            float suelo = valle != null ? valle.Altura(p.x, p.z) : p.y;
            Asentar(barril, new Vector3(p.x, suelo, p.z));
            CajaVisible(barril, 0.9f);
        }

        /// <summary>
        /// Empedrado alrededor de la hoguera con un anillo de tierra pisada (texturas originales del
        /// paquete de texturas) y unos barriles con las provisiones de la plaza.
        /// </summary>
        static void AdornarPlaza(Transform plaza, Valle valle)
        {
            Vector3 c = plaza.position;
            float y = valle != null ? valle.Altura(c.x, c.z) : c.y;
            Material tierra = MaterialTextura("TierraPisada", "Summer_Dirt_A.png");
            Material adoquin = MaterialTextura("Adoquin", "Summer_Rocks.png");
            if (tierra != null) Disco("Tierra de la plaza", new Vector3(c.x, y + 0.015f, c.z), 5.6f, 4f, tierra, 5).transform.SetParent(plaza, true);
            if (adoquin != null) Disco("Empedrado de la plaza", new Vector3(c.x, y + 0.03f, c.z), 4.6f, 2.5f, adoquin, 6).transform.SetParent(plaza, true);
            Barril(plaza, valle, c + new Vector3(4.2f, 0f, 2.4f), 20f);
            Barril(plaza, valle, c + new Vector3(4.9f, 0f, 1.5f), 75f, 0.9f);
            Barril(plaza, valle, c + new Vector3(-3.6f, 0f, -3.4f), 140f);
            CajasDeLaPlaza(plaza, valle);
        }

        /// <summary>
        /// La casa del panadero del paquete "Lowpoly Baker's House", pasada a la paleta: la vieja
        /// panadería de Nova Lavinia, de antes de la llegada de Nicias. Es adorno, fuera de la zona
        /// edificable, en el primer llano que encuentra mirando hacia la plaza, con la puerta hacia
        /// ella y unos barriles junto a la entrada.
        /// </summary>
        static void ColocarPanaderia(Valle valle, Vector3 plaza)
        {
            GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaPanadero + "Baker_house.prefab");
            if (modelo == null || valle == null) return;
            Vector2 haciaPlaza = new Vector2(plaza.x, plaza.z).normalized;
            float base0 = Mathf.Atan2(haciaPlaza.y, haciaPlaza.x);
            for (int intento = 0; intento < 60; intento++)
            {
                float radio = RadioEdificable + 7f + (intento % 4) * 2.5f;
                float angulo = base0 + ((intento / 4) % 2 == 0 ? 1f : -1f) * (intento / 8) * 0.12f;
                float x = Mathf.Cos(angulo) * radio, z = Mathf.Sin(angulo) * radio;
                if (!valle.Llano(x, z, 2.4f)) continue;
                // que todo el solar (unos 10 x 10 m) esté a la misma altura
                float h = valle.Altura(x, z), desnivel = 0f;
                for (int i = -1; i <= 1; i++)
                    for (int k = -1; k <= 1; k++)
                        desnivel = Mathf.Max(desnivel, Mathf.Abs(valle.Altura(x + i * 5.5f, z + k * 5.5f) - h));
                if (desnivel > 0.5f) continue;

                var casa = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
                casa.name = "Vieja panadería";
                // la puerta del modelo mira a -Z: se gira para que mire a la plaza
                Vector3 haciaLaPlaza = new Vector3(plaza.x - x, 0f, plaza.z - z).normalized;
                casa.transform.rotation = Quaternion.LookRotation(-haciaLaPlaza, Vector3.up);
                // a su tamaño original (unos 9,5 x 10 m y la puerta a la altura de Nicias)
                Asentar(casa, new Vector3(x, h - 0.1f, z));
                casa.isStatic = true;
                if (casa.GetComponentInChildren<Collider>() == null)
                {
                    foreach (MeshFilter f in casa.GetComponentsInChildren<MeshFilter>())
                        f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
                }
                Transform t = casa.transform;
                Barril(t, valle, t.TransformPoint(new Vector3(-2.8f, 0f, -6.0f)), 30f);
                Barril(t, valle, t.TransformPoint(new Vector3(-3.7f, 0f, -5.4f)), 100f, 0.9f);
                Barril(t, valle, t.TransformPoint(new Vector3(5.6f, 0f, 1.2f)), 210f);
                CajasDeLaPanaderia(t, valle);
                libres.Add(new Vector3(x, 11f, z));
                return;
            }
            Debug.LogWarning("Nostos: no se encontró un llano para la vieja panadería.");
        }

        static void SembrarNaturaleza(Valle valle)
        {
            var azar = new System.Random(11);
            var padre = new GameObject("Naturaleza").transform;
            const float Todo = Mathf.PI * 2f;
            PaquetesURP.PasarTodo();

            // Paquete "A piece of nature": su pino y sus dos rocas, tal cual, mandan sobre los demás.
            GameObject pinoPieza = Pieza("Pine_tree");
            var rocasPieza = new List<GameObject>();
            foreach (string nombre in new[] { "Rock_3", "Rock_4" })
            {
                GameObject modelo = Pieza(nombre);
                if (modelo != null) rocasPieza.Add(modelo);
            }

            // ---- modelos
            GameObject pino = Polytope("Trees/PT_Pine_Tree_03_green");
            GameObject frondoso = Polytope("Trees/PT_Fruit_Tree_01_green");
            GameObject toconPino = Polytope("Trees/PT_Pine_Tree_03_stump");
            GameObject toconFrondoso = Polytope("Trees/PT_Fruit_Tree_01_stump");
            var frutales = new List<GameObject>();
            foreach (string nombre in new[] { "PT_Fruit_Tree_01_apples", "PT_Fruit_Tree_01_pears", "PT_Fruit_Tree_01_plums" })
            {
                GameObject modelo = Polytope("Trees/" + nombre);
                if (modelo != null) frutales.Add(modelo);
            }
            bool conPolytope = pino != null && frondoso != null;
            if (!conPolytope) Debug.LogWarning("Nostos: no está el paquete de naturaleza de Polytope; se usan los árboles propios.");

            // Pinos originales del paquete "Pine Forest - LowPoly" (con sus shaders pasados a URP):
            // de 12 m el más alto a 4,7 m el más bajo, con tocón propio al talarlos.
            var pinos = new List<(GameObject modelo, float min, float max)>();
            void Pino(string nombre, float min, float max)
            {
                GameObject modelo = Pinar(nombre);
                if (modelo != null) pinos.Add((modelo, min, max));
            }
            if (pinoPieza != null)
            {
                // El pino del paquete mide 5,4 m: de 7 a 12 m, más alguno pequeño.
                pinos.Add((pinoPieza, 1.3f, 1.75f));
                pinos.Add((pinoPieza, 1.6f, 2.2f));
                pinos.Add((pinoPieza, 1.1f, 1.4f));
            }
            else
            {
                Pino("pino_1", 0.6f, 0.85f);
                Pino("pino_2", 0.6f, 0.85f);
                Pino("pino_3", 0.7f, 0.95f);
                Pino("pino_4", 0.8f, 1.1f);
                Pino("pino_5", 0.85f, 1.15f);
                Pino("pino_6", 1.0f, 1.35f);
            }
            GameObject toconNuevo = Pinar("tocon_pino");
            var jovenes = new List<GameObject>();
            if (pinoPieza != null) jovenes.Add(pinoPieza);
            else
            {
                foreach (string nombre in new[] { "pino_joven_1", "pino_joven_2" })
                {
                    GameObject modelo = Pinar(nombre);
                    if (modelo != null) jovenes.Add(modelo);
                }
            }

            // Árboles para talar: (modelo, tocón, radio del tronco, escala mínima, máxima)
            var talables = new List<(GameObject modelo, GameObject tocon, float radio, float min, float max)>();
            if (pinos.Count > 0)
            {
                // Cápsula en el tronco: se llega hasta él para talar y el NavMesh no se corta bajo las ramas.
                foreach (var p in pinos) talables.Add((p.modelo, toconNuevo, 0.35f, p.min, p.max));
                // Con el paquete "A piece of nature", todos los árboles son su pino.
                if (frondoso != null && pinoPieza == null) talables.Add((frondoso, toconFrondoso, 0.42f, 0.75f, 1.0f));
            }
            else if (conPolytope)
            {
                talables.Add((pino, toconPino, 0.32f, 0.8f, 1.15f));
                talables.Add((pino, toconPino, 0.32f, 0.8f, 1.15f));
                talables.Add((frondoso, toconFrondoso, 0.42f, 0.75f, 1.0f));
            }
            else
            {
                foreach (string nombre in new[] { "arbol_a", "arbol_b", "arbol_c", "arbol_a" })
                {
                    GameObject modelo = Modelo(nombre);
                    if (modelo != null) talables.Add((modelo, null, 0f, 0.85f, 1.45f));
                }
            }
            GameObject veta = rocasPieza.Count > 0 ? rocasPieza[0] : Polytope("Rocks/PT_Ore_Rock_01");
            float vetaMin = 2.4f, vetaMax = 3.2f;
            if (rocasPieza.Count > 0)
            {
                // Las rocas del paquete miden menos de 1 m: vetas de 1,3 a 1,8 m.
                vetaMin = 1.5f;
                vetaMax = 2.1f;
            }
            else if (veta == null)
            {
                veta = Modelo("veta");
                vetaMin = 0.9f;
                vetaMax = 1.4f;
            }
            if (talables.Count == 0 || veta == null)
            {
                Debug.LogWarning("Nostos: faltan modelos de Naturaleza. Ejecuta Pipeline/generar_kit.py.");
            }

            // Suelo del bosque bajo cada bosquecillo: manchas de pinaza y tierra, matas y pinos jóvenes.
            var manchas = new List<GameObject>();
            // Sin las manchas de hierba del pinar: ahora hay hierba de verdad por todo el valle.
            foreach (string nombre in new[] { "suelo_tierra_1", "suelo_tierra_2" })
            {
                GameObject modelo = Pinar(nombre);
                if (modelo != null) manchas.Add(modelo);
            }
            var matas = new List<GameObject>();
            foreach (string nombre in new[] { "mata_1", "mata_2" })
            {
                GameObject modelo = Pinar(nombre);
                if (modelo != null) matas.Add(modelo);
            }
            var sotobosque = new GameObject("Suelo del bosque").transform;
            sotobosque.SetParent(padre, false);
            void SueloDeBosque(Vector3 centro)
            {
                int cuantas = manchas.Count > 0 ? 2 + azar.Next(3) : 0;
                for (int k = 0; k < cuantas + 4; k++)
                {
                    float x = centro.x + ((float)azar.NextDouble() - 0.5f) * 14f;
                    float z = centro.z + ((float)azar.NextDouble() - 0.5f) * 14f;
                    if (new Vector2(x, z).magnitude < RadioEdificable + 3f || !valle.Llano(x, z, 2.4f) || Ocupado(x, z)) continue;
                    var q = new Vector3(x, valle.Altura(x, z), z);
                    if (k < cuantas)
                    {
                        SinSombra(Plantar(manchas[azar.Next(manchas.Count)], sotobosque, q + Vector3.down * 0.02f, azar, 0.8f, 1.3f));
                    }
                    else if (k < cuantas + 2 && matas.Count > 0)
                    {
                        Plantar(matas[azar.Next(matas.Count)], sotobosque, q + Vector3.down * 0.05f, azar, 0.8f, 1.4f);
                    }
                    else if (jovenes.Count > 0 && azar.Next(2) == 0)
                    {
                        Tronco(Plantar(jovenes[azar.Next(jovenes.Count)], sotobosque, q + Vector3.down * 0.1f, azar, 0.7f, 1.1f), 0.2f, 2f);
                    }
                }
            }

            NodoRecurso Talable(Vector3 p, float escalaMin = -1f, float escalaMax = -1f)
            {
                var t = talables[azar.Next(talables.Count)];
                GameObject arbol = Plantar(t.modelo, padre, p, azar, escalaMin > 0f ? escalaMin : t.min, escalaMax > 0f ? escalaMax : t.max);
                if (t.radio > 0f)
                {
                    foreach (Collider c in arbol.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    Tronco(arbol, t.radio, 3f);
                }
                var nodo = arbol.AddComponent<NodoRecurso>();
                nodo.recurso = Recurso.Madera;
                nodo.unidades = 6;
                nodo.verbo = "Talar";
                nodo.restos = t.tocon;
                return nodo;
            }

            NodoRecurso Veta(Vector3 p, float escalaMin, float escalaMax)
            {
                GameObject modeloVeta = rocasPieza.Count > 1 ? rocasPieza[azar.Next(rocasPieza.Count)] : veta;
                GameObject roca = Plantar(modeloVeta, padre, p, azar, escalaMin, escalaMax);
                CajaVisible(roca);
                var nodo = roca.AddComponent<NodoRecurso>();
                nodo.recurso = Recurso.Mineral;
                nodo.unidades = 8;
                nodo.verbo = "Picar";
                Picable(roca);
                return nodo;
            }

            // Árboles: en bosquecillos por todo el llano, fuera de la zona edificable.
            for (int grupo = 0; grupo < 34 && talables.Count > 0; grupo++)
            {
                if (!Buscar(valle, azar, 40f, 92f, 0f, Todo, 2.2f, out Vector3 centro)) continue;
                int cuantos = 3 + azar.Next(5);
                for (int k = 0; k < cuantos; k++)
                {
                    float x = centro.x + ((float)azar.NextDouble() - 0.5f) * 16f;
                    float z = centro.z + ((float)azar.NextDouble() - 0.5f) * 16f;
                    if (new Vector2(x, z).magnitude < RadioEdificable + 3f || !valle.Llano(x, z, 2.4f) || Ocupado(x, z)) continue;
                    Talable(new Vector3(x, valle.Altura(x, z) - 0.1f, z));
                }
                SueloDeBosque(centro);
            }

            // Vetas y cobre: al pie de las paredes de roca.
            for (int i = 0; i < 16 && veta != null; i++)
            {
                if (!Buscar(valle, azar, 56f, 90f, 0f, Todo, 2.4f, out Vector3 p)) continue;
                Veta(p, vetaMin, vetaMax);
            }
            GameObject cobre = Modelo("cobre");
            for (int i = 0; i < 8 && cobre != null; i++)
            {
                if (!Buscar(valle, azar, 58f, 90f, 0f, Todo, 2.4f, out Vector3 p)) continue;
                var nodo = Plantar(cobre, padre, p, azar, 0.9f, 1.3f).AddComponent<NodoRecurso>();
                Picable(nodo.gameObject);
                nodo.recurso = Recurso.Cobre;
                nodo.unidades = 6;
                nodo.verbo = "Extraer cobre";
            }

            // Cristal: solo al otro lado del río, en el prado del oeste. Para eso está el puente.
            GameObject cristal = Modelo("cristal");
            for (int i = 0; i < 12 && cristal != null; i++)
            {
                if (!Buscar(valle, azar, 62f, 96f, Mathf.PI * 0.83f, Mathf.PI * 1.22f, 2.4f, out Vector3 p)) continue;
                var nodo = Plantar(cristal, padre, p, azar, 1f, 1.8f).AddComponent<NodoRecurso>();
                nodo.recurso = Recurso.Cristal;
                nodo.unidades = 4;
                nodo.verbo = "Tallar cristal";
            }

            // Comida que rebrota cada mañana: frutales (manzanas, peras, ciruelas) que se quedan
            // sin fruta al recogerla. Sin el paquete, los arbustos de bayas de antes.
            GameObject arbusto = Modelo("arbusto");
            for (int i = 0; i < 24 && (frutales.Count > 0 || arbusto != null); i++)
            {
                Vector3 p;
                if (i < 3) p = new Vector3(8f + i * 6.5f, 0f, -21f - i * 1.5f);
                else if (!Buscar(valle, azar, 38f, 80f, 0f, Todo, 2.4f, out p)) continue;
                NodoRecurso nodo;
                if (frutales.Count > 0)
                {
                    int cual = azar.Next(frutales.Count);
                    var raiz = new GameObject("Frutal");
                    raiz.transform.SetParent(padre, false);
                    raiz.transform.position = p;
                    raiz.transform.rotation = Quaternion.Euler(0f, (float)azar.NextDouble() * 360f, 0f);
                    raiz.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 0.8f, (float)azar.NextDouble());
                    var conFruto = (GameObject)PrefabUtility.InstantiatePrefab(frutales[cual], raiz.transform);
                    GameObject sinFruto = null;
                    if (frondoso != null)
                    {
                        sinFruto = (GameObject)PrefabUtility.InstantiatePrefab(frondoso, raiz.transform);
                        sinFruto.SetActive(false);
                    }
                    Tronco(raiz, 0.42f, 3f);
                    nodo = raiz.AddComponent<NodoRecurso>();
                    nodo.conFruto = conFruto;
                    nodo.sinFruto = sinFruto;
                    nodo.verbo = "Recoger fruta";
                }
                else
                {
                    nodo = Plantar(arbusto, padre, p, azar, 0.9f, 1.3f).AddComponent<NodoRecurso>();
                    nodo.verbo = "Recoger bayas";
                }
                nodo.recurso = Recurso.Comida;
                nodo.unidades = 4;
                nodo.rebrota = true;
            }

            // Peñascos: solo adorno, también a media ladera. Sin menhires: Toni pidió quitarlos.
            var penascos = new List<(GameObject modelo, float min, float max)>();
            GameObject piedra = Polytope("Rocks/PT_Generic_Rock_01");
            foreach (GameObject roca in rocasPieza) penascos.Add((roca, 2.2f, 6.5f));
            if (penascos.Count == 0 && piedra != null) penascos.Add((piedra, 5f, 11f));
            if (penascos.Count == 0)
            {
                foreach (string nombre in new[] { "penasco_a", "penasco_b" })
                {
                    GameObject modelo = Modelo(nombre);
                    if (modelo != null) penascos.Add((modelo, 0.6f, 2.2f));
                }
            }
            for (int i = 0; i < 46 && penascos.Count > 0; i++)
            {
                if (!Buscar(valle, azar, 40f, 130f, 0f, Todo, 40f, out Vector3 p)) continue;
                var pe = penascos[azar.Next(penascos.Count)];
                GameObject roca = Plantar(pe.modelo, padre, p, azar, pe.min, pe.max);
                if (pe.modelo == piedra || rocasPieza.Contains(pe.modelo)) CajaVisible(roca);
            }
            // Pinos en las terrazas, donde no se llega: dan vida a las montañas.
            GameObject pinoLejano = pino != null ? pino : (talables.Count > 1 ? talables[1].modelo : null);
            for (int i = 0; i < 110 && (pinos.Count > 0 || pinoLejano != null); i++)
            {
                if (!Buscar(valle, azar, 84f, 170f, 0f, Todo, 60f, out Vector3 p) || p.y < 5f) continue;
                if (pinos.Count > 0)
                {
                    var pn = pinos[azar.Next(pinos.Count)];
                    Plantar(pn.modelo, padre, p, azar, pn.min * 1.1f, pn.max * 1.25f);
                }
                else
                {
                    Plantar(pinoLejano, padre, p, azar, pino != null ? 0.9f : 1f, pino != null ? 1.6f : 1.9f);
                }
            }

            // Árboles secos y matorral sueltos entre los bosquecillos.
            var secos = new List<(GameObject modelo, float min, float max, bool tronco)>();
            void Anadir(string ruta, float min, float max, bool tronco)
            {
                GameObject modelo = Polytope(ruta);
                if (modelo != null) secos.Add((modelo, min, max, tronco));
            }
            GameObject seco1 = Pinar("pino_seco_1"), seco2 = Pinar("pino_seco_2");
            if (seco1 != null) secos.Add((seco1, 0.8f, 1.2f, true));
            if (seco2 != null) secos.Add((seco2, 0.9f, 1.3f, true));
            if (seco1 == null) Anadir("Trees/PT_Pine_Tree_03_dead", 0.7f, 1f, true);
            Anadir("Trees/PT_Fruit_Tree_01_dead", 0.7f, 1f, true);
            Anadir("Shrubs/PT_Generic_Shrub_01_green", 0.8f, 1.3f, false);
            Anadir("Shrubs/PT_Generic_Shrub_01_green", 0.8f, 1.3f, false);
            Anadir("Shrubs/PT_Generic_Shrub_01_dead", 0.8f, 1.2f, false);
            Anadir("Trees/PT_Fruit_Tree_01_logs", 0.8f, 1f, false);
            for (int i = 0; i < 40 && secos.Count > 0; i++)
            {
                if (!Buscar(valle, azar, RadioEdificable + 5f, 95f, 0f, Todo, 3f, out Vector3 p)) continue;
                var s = secos[azar.Next(secos.Count)];
                GameObject cosa = Plantar(s.modelo, padre, p, azar, s.min, s.max);
                if (s.tronco) Tronco(cosa, 0.3f, 3f);
            }

            // Adorno del suelo: hierba alta, amapolas y setas, sin colisionador ni sombra. Fuera de
            // la zona edificable para que no asome por los suelos de las casas ni por los caminos.
            var suelo = new List<(GameObject modelo, int cuantos, float min, float max, float desde)>();
            GameObject hierba = Polytope("Plants/PT_Grass_02");
            GameObject amapola = Polytope("Flowers/PT_Poppy_02");
            GameObject seta = Polytope("Mushrooms/PT_Caesars_Mushroom_01");
            GameObject cantos = Polytope("Rocks/PT_River_Rock_Pile_02");
            // La hierba alta de Polytope ya no: la pone HierbaDelValle por todo el valle.
            if (hierba != null && Pieza("Pine_tree") == null) suelo.Add((hierba, 520, 0.8f, 1.4f, RadioEdificable + 2f));
            if (amapola != null) suelo.Add((amapola, 170, 0.8f, 1.2f, RadioEdificable + 2f));
            if (seta != null) suelo.Add((seta, 50, 0.9f, 1.6f, RadioEdificable + 4f));
            if (cantos != null) suelo.Add((cantos, 40, 1.5f, 3f, RadioEdificable + 4f));
            if (suelo.Count > 0)
            {
                var adorno = new GameObject("Adorno del suelo").transform;
                adorno.SetParent(padre, false);
                foreach (var a in suelo)
                {
                    for (int i = 0; i < a.cuantos; i++)
                    {
                        if (!Buscar(valle, azar, a.desde, 98f, 0f, Todo, 2.4f, out Vector3 p)) continue;
                        // en matas: unas cuantas juntas
                        int mata = a.modelo == hierba ? 1 + azar.Next(3) : 1;
                        for (int k = 0; k < mata; k++)
                        {
                            float x = p.x + ((float)azar.NextDouble() - 0.5f) * 2.4f * k;
                            float z = p.z + ((float)azar.NextDouble() - 0.5f) * 2.4f * k;
                            if (!valle.Llano(x, z, 2.4f) || Ocupado(x, z)) continue;
                            var q = new Vector3(x, valle.Altura(x, z) - 0.03f, z);
                            SinSombra(Plantar(a.modelo, adorno, q, azar, a.min, a.max));
                        }
                    }
                }
            }

            // Dos árboles y una veta cerca del punto de partida, para probar sin caminar.
            if (talables.Count > 0)
            {
                foreach (Vector3 p in new[] { new Vector3(6f, 0f, -10f), new Vector3(12f, 0f, -6f) })
                {
                    Talable(p, 0.8f, 0.9f);
                }
            }
            if (veta != null)
            {
                Veta(new Vector3(-7f, 0f, -9f), vetaMin, vetaMin + 0.2f);
            }

            // Hierba densa por todo el valle (paquete "A piece of nature").
            SembrarHierba();
        }

        /// <summary>
        /// Busca un punto llano entre dos radios y dos ángulos. Devuelve false si tras varios
        /// intentos no lo encuentra (río, pared de roca).
        /// </summary>
        static bool Buscar(Valle valle, System.Random azar, float radioMin, float radioMax, float anguloMin, float anguloMax,
            float alturaMaxima, out Vector3 punto)
        {
            for (int intento = 0; intento < 40; intento++)
            {
                float radio = Mathf.Lerp(radioMin, radioMax, (float)azar.NextDouble());
                float angulo = Mathf.Lerp(anguloMin, anguloMax, (float)azar.NextDouble());
                float x = Mathf.Cos(angulo) * radio;
                float z = Mathf.Sin(angulo) * radio;
                if (!valle.Llano(x, z, alturaMaxima) || Ocupado(x, z)) continue;
                punto = new Vector3(x, valle.Altura(x, z) - 0.1f, z);
                return true;
            }
            punto = Vector3.zero;
            return false;
        }

        static GameObject Plantar(GameObject modelo, Transform padre, Vector3 posicion, System.Random azar, float escalaMin, float escalaMax)
        {
            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
            instancia.transform.position = posicion;
            instancia.transform.rotation = Quaternion.Euler(0f, (float)azar.NextDouble() * 360f, 0f);
            float escala = Mathf.Lerp(escalaMin, escalaMax, (float)azar.NextDouble());
            instancia.transform.localScale = Vector3.one * escala;
            return instancia;
        }

        // ------------------------------------------------------------ materiales y utilidades

        static AudioClip Clip(string nombre)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CarpetaSonido + "/" + nombre + ".wav");
            if (clip == null) Debug.LogWarning("Nostos: falta el sonido " + nombre + ".wav. Ejecuta Pipeline/generar_sonidos.py.");
            return clip;
        }

        /// <summary>
        /// Deja el FBX animado importado como hace falta: esqueleto genérico, sin materiales
        /// propios y con todas las animaciones en bucle. Devuelve false si el archivo no está.
        /// </summary>
        static bool PrepararModeloAnimado(string ruta)
        {
            var importador = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (importador == null) return false;

            bool enBucle = importador.clipAnimations != null && importador.clipAnimations.Length > 0;
            if (enBucle && importador.animationType == ModelImporterAnimationType.Generic
                        && importador.materialImportMode == ModelImporterMaterialImportMode.None) return true;

            importador.animationType = ModelImporterAnimationType.Generic;
            importador.materialImportMode = ModelImporterMaterialImportMode.None;
            importador.importAnimation = true;
            importador.importCameras = false;
            importador.importLights = false;
            ModelImporterClipAnimation[] clips = importador.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = true;
                clip.loopPose = true;
            }
            if (clips.Length > 0) importador.clipAnimations = clips;
            importador.SaveAndReimport();
            return true;
        }

        static Transform Hueso(GameObject modelo, string nombre)
        {
            foreach (Transform t in modelo.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith(nombre)) return t;
            }
            return null;
        }

        /// <summary>
        /// Gira y escala el modelo con ayuda de sus huesos para que mire hacia delante y mida
        /// 1,75 m, venga como venga el FBX (ejes y unidades cambian según quién lo exporte).
        /// </summary>
        static void AjustarModeloAnimado(GameObject modelo, Transform padre)
        {
            Transform brazoIzq = Hueso(modelo, "LeftArm");
            Transform brazoDer = Hueso(modelo, "RightArm");
            if (brazoIzq != null && brazoDer != null)
            {
                Vector3 derecha = brazoDer.position - brazoIzq.position;
                Vector3 frente = Vector3.Cross(derecha, Vector3.up);
                frente.y = 0f;
                if (frente.sqrMagnitude > 1e-6f)
                {
                    float giro = Vector3.SignedAngle(frente, padre.forward, Vector3.up);
                    modelo.transform.Rotate(0f, giro, 0f, Space.World);
                }
            }

            Transform coronilla = Hueso(modelo, "HeadTop_End");
            if (coronilla != null)
            {
                float altura = coronilla.position.y - padre.position.y;
                if (altura > 0.001f) modelo.transform.localScale *= 1.75f / altura;
            }
        }

        /// <summary>Controlador de animación de Nicias: reposo, andar y correr mezclados por la velocidad.</summary>
        static RuntimeAnimatorController ControladorDeNicias(string rutaModelo)
        {
            AnimationClip reposo = null, andar = null, correr = null;
            foreach (Object recurso in AssetDatabase.LoadAllAssetsAtPath(rutaModelo))
            {
                var clip = recurso as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                if (clip.name.Contains("Alert")) reposo = clip;
                else if (clip.name.Contains("Walking")) andar = clip;
                else if (clip.name.Contains("Running")) correr = clip;
            }
            if (reposo == null && andar == null && correr == null)
            {
                Debug.LogWarning("Nostos: " + rutaModelo + " no trae animaciones reconocibles (Alert, Walking, Running).");
                return null;
            }

            string ruta = CarpetaNicias + "/Nicias.controller";
            AssetDatabase.DeleteAsset(ruta);
            AnimatorController controlador = AnimatorController.CreateAnimatorControllerAtPath(ruta);
            controlador.AddParameter("Velocidad", AnimatorControllerParameterType.Float);
            controlador.CreateBlendTreeInController("Movimiento", out BlendTree arbol);
            arbol.blendType = BlendTreeType.Simple1D;
            arbol.blendParameter = "Velocidad";
            arbol.useAutomaticThresholds = false;
            if (reposo != null) arbol.AddChild(reposo, 0f);
            if (andar != null) arbol.AddChild(andar, 2.2f);
            if (correr != null) arbol.AddChild(correr, 5f);
            EditorUtility.SetDirty(controlador);
            return controlador;
        }

        /// <summary>Material del modelo de Nicias: color, normales y metal/suavidad de Pipeline/preparar_nicias.py.</summary>
        static Material MaterialNicias()
        {
            string rutaColor = CarpetaNicias + "/nicias_color.png";
            string rutaNormal = CarpetaNicias + "/nicias_normal.png";
            string rutaMetal = CarpetaNicias + "/nicias_metal.png";

            var impNormal = AssetImporter.GetAtPath(rutaNormal) as TextureImporter;
            if (impNormal != null && impNormal.textureType != TextureImporterType.NormalMap)
            {
                impNormal.textureType = TextureImporterType.NormalMap;
                impNormal.SaveAndReimport();
            }
            var impMetal = AssetImporter.GetAtPath(rutaMetal) as TextureImporter;
            if (impMetal != null && impMetal.sRGBTexture)
            {
                impMetal.sRGBTexture = false;
                impMetal.SaveAndReimport();
            }

            Material material = CargarOCrear("Nicias modelo", "Universal Render Pipeline/Lit");
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaColor);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaNormal);
            var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaMetal);
            material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            if (color != null)
            {
                material.SetTexture("_BaseMap", color);
                material.mainTexture = color;
            }
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (metal != null)
            {
                material.SetTexture("_MetallicGlossMap", metal);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Smoothness", 0.7f);
            }
            else
            {
                material.SetFloat("_Smoothness", 0.2f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MaterialLit(string nombre, Color color)
        {
            Material material = CargarOCrear(nombre, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MaterialTransparente(string nombre, Color color)
        {
            Material material = CargarOCrear(nombre, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material CargarOCrear(string nombre, string nombreShader)
        {
            string ruta = CarpetaMateriales + "/" + nombre + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (material != null) return material;

            Shader shader = Shader.Find(nombreShader);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = nombre };
            AssetDatabase.CreateAsset(material, ruta);
            return material;
        }

        static void PonerPrimeraEnBuild(string ruta)
        {
            var escenas = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ruta, true) };
            foreach (EditorBuildSettingsScene e in EditorBuildSettings.scenes)
            {
                if (e.path != ruta) escenas.Add(e);
            }
            EditorBuildSettings.scenes = escenas.ToArray();
        }
    }
}
