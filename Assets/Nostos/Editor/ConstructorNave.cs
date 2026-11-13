using System.Collections.Generic;
using System.IO;
using Nostos.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nostos.EditorTools
{
    /// <summary>
    /// La nave de Nicias, hecha con los módulos del paquete "Star Sparrow" (Assets/StarSparrow), y
    /// el aterrizaje forzoso: la nave inclinada y medio hundida al final de un surco largo, con un
    /// primer rebote, la tierra levantada en los bordes, piezas arrancadas por el camino, humo y fuego.
    /// Cada módulo del juego monta sus piezas del paquete (y recoge las del surco).
    /// </summary>
    public static partial class ConstructorEscena
    {
        const string CarpetaNave = "Assets/StarSparrow/";
        const string FuegoRojo = "Assets/PolyOne/Fire Effects/Prefabs/Fire Effects Red.prefab";
        const string RutaHumo = "Assets/Nostos/Arte/Efectos/humo.png";

        /// <summary>Cuál de los 40 ejemplos por piezas del paquete es la nave de Nicias.</summary>
        const int ModeloNave = 1;
        /// <summary>Color del paquete (Black, Blue, Cyan, Green, Grey, Orange, Purple, Red, White, Yellow).</summary>
        const string ColorNave = "White";
        /// <summary>Largo de la nave en metros (el solar es de 6 x 12 m).</summary>
        const float LargoNave = 12f;

        // Solar de la nave en la rejilla, como antes: 3 x 6 celdas desde la (-9, 4).
        const int NaveCeldaX = -9, NaveCeldaZ = 4, NaveAncho = 3, NaveFondo = 6;

        /// <summary>De dónde viene el surco: la nave entró desde el noroeste.</summary>
        static readonly Vector3 InicioSurco = new Vector3(-33f, 0f, 42f);

        /// <summary>
        /// Qué módulo del juego monta cada tipo de pieza del paquete (0 = el núcleo, siempre está).
        /// Las armas (Weapon) no se usan: en Nostos no hay combate.
        /// 1 Casco y coraza: colas y aletas. 2 Soporte vital: depósitos de plasma. 3 Motor: motores.
        /// 4 Navegación: alas (con los sensores). 5 Combustible: toberas.
        /// </summary>
        static int ModuloDePieza(string tipo)
        {
            switch (tipo)
            {
                case "Core": return 0;
                case "Tail":
                case "Fin": return 1;
                case "Plasma": return 2;
                case "Engine": return 3;
                case "Wing": return 4;
                case "Thruster": return 5;
                default: return -1;
            }
        }

        static readonly string[] TiposPieza = { "Core", "Wing", "Engine", "Fin", "Tail", "Thruster", "Plasma", "Weapon" };

        static string TipoDePieza(GameObject pieza)
        {
            GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(pieza);
            foreach (string tipo in TiposPieza)
            {
                if (pieza.name.Contains("_" + tipo) || (fuente != null && fuente.name.Contains("_" + tipo))) return tipo;
            }
            foreach (string tipo in TiposPieza)
            {
                if (pieza.name.Contains(tipo)) return tipo;
            }
            return null;
        }

        // ------------------------------------------------------------------ la nave

        static void ColocarNave(Valle valle)
        {
            colisionSurco = null;
            montones.Clear();
            Vector3 centro = new Vector3((NaveCeldaX + NaveAncho * 0.5f) * TamCelda, 0f, (NaveCeldaZ + NaveFondo * 0.5f) * TamCelda);
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaNave + "Prefabs/ModularExamples/StarSparrow" + ModeloNave + ".prefab");
            if (modelo == null)
            {
                Debug.LogWarning("Nostos: no encuentro el paquete Star Sparrow; queda el casco antiguo.");
                CascoAntiguo(centro);
                return;
            }
            PaquetesURP.PasarTodo();

            // Todo se monta en el origen y sin girar; al final se lleva a su sitio.
            var raiz = new GameObject("Nave de Nicias");
            var cuerpo = new GameObject("Cuerpo").transform;
            cuerpo.SetParent(raiz.transform, false);
            var nave = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            PrefabUtility.UnpackPrefabInstance(nave, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            nave.name = "StarSparrow" + ModeloNave;
            nave.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            nave.transform.localScale = Vector3.one;

            var piezas = new List<(GameObject go, string tipo)>();
            foreach (Transform hijo in nave.transform)
            {
                string tipo = TipoDePieza(hijo.gameObject);
                if (tipo != null) piezas.Add((hijo.gameObject, tipo));
            }
            if (piezas.Count == 0)
            {
                // Por si los módulos no cuelgan directamente de la raíz.
                foreach (Transform t in nave.GetComponentsInChildren<Transform>(true))
                {
                    if (t == nave.transform || !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                    string tipo = TipoDePieza(t.gameObject);
                    if (tipo != null) piezas.Add((t.gameObject, tipo));
                }
            }

            // La nariz es el lado contrario a motores y toberas; se gira para que mire a +Z.
            Vector3 nucleo = Vector3.zero, popa = Vector3.zero;
            int enPopa = 0;
            foreach (var p in piezas)
            {
                if (p.tipo == "Core") nucleo = Medir(p.go).center;
                if (p.tipo == "Engine" || p.tipo == "Thruster")
                {
                    popa += Medir(p.go).center;
                    enPopa++;
                }
            }
            Vector3 haciaPopa = enPopa > 0 ? popa / enPopa - nucleo : Vector3.back;
            haciaPopa.y = 0f;
            if (haciaPopa.sqrMagnitude < 1e-4f) haciaPopa = Vector3.back;
            float rumboModelo = Mathf.Round(Quaternion.LookRotation(-haciaPopa).eulerAngles.y / 90f) * 90f;

            foreach (var p in piezas)
            {
                if (p.tipo == "Weapon") Object.DestroyImmediate(p.go);
            }
            piezas.RemoveAll(p => p.go == null);

            nave.transform.SetParent(cuerpo, false);
            nave.transform.localRotation = Quaternion.Euler(0f, -rumboModelo, 0f);
            Bounds caja = Medir(nave);
            float escala = LargoNave / Mathf.Max(0.01f, caja.size.z);
            nave.transform.localScale = Vector3.one * escala;
            caja = Medir(nave);
            nave.transform.localPosition -= new Vector3(caja.center.x, caja.min.y, caja.center.z);
            caja = Medir(nave);

            Material pintura = MaterialNave();
            Pintar(nave, pintura);

            // Grupos Modulo1..5: CascoNave los enciende al montar cada módulo.
            var grupos = new Transform[6];
            for (int i = 1; i <= 5; i++)
            {
                grupos[i] = new GameObject("Modulo" + i).transform;
                grupos[i].SetParent(nave.transform, false);
            }
            var cuantas = new int[6];
            foreach (var p in piezas)
            {
                int m = ModuloDePieza(p.tipo);
                if (m < 1) continue;
                p.go.transform.SetParent(grupos[m], true);
                cuantas[m]++;
            }
            Debug.Log("Nostos: nave de Nicias con StarSparrow" + ModeloNave + ", escala " + escala.ToString("0.00")
                      + ", " + caja.size.x.ToString("0.0") + " x " + caja.size.z.ToString("0.0") + " m. Piezas por módulo: "
                      + cuantas[1] + ", " + cuantas[2] + ", " + cuantas[3] + ", " + cuantas[4] + ", " + cuantas[5] + ".");

            // A su sitio: al final del surco, girada un poco por el derrape.
            Vector3 rumbo = centro - InicioSurco;
            rumbo.y = 0f;
            rumbo.Normalize();
            float giro = Mathf.Atan2(rumbo.x, rumbo.z) * Mathf.Rad2Deg + 14f;
            raiz.transform.SetPositionAndRotation(new Vector3(centro.x, valle.Altura(centro.x, centro.z), centro.z), Quaternion.Euler(0f, giro, 0f));

            var casco = raiz.AddComponent<CascoNave>();
            casco.celdaX = NaveCeldaX;
            casco.celdaZ = NaveCeldaZ;
            casco.ancho = NaveAncho;
            casco.fondo = NaveFondo;
            casco.cuerpo = cuerpo;
            casco.giroNaufragio = new Vector3(6f, 0f, -11f);
            casco.hundimiento = 0.75f;

            // La nave ya en su postura de naufragio (CascoNave la mantiene igual al jugar), para que
            // el surco se arrime a la forma real del casco.
            cuerpo.localRotation = Quaternion.Euler(casco.giroNaufragio);
            cuerpo.localPosition = Vector3.down * casco.hundimiento;
            var piezasNucleo = new List<GameObject>();
            foreach (var p in piezas)
            {
                if (p.tipo == "Core" && p.go != null) piezasNucleo.Add(p.go);
            }
            PrepararCasco(piezasNucleo);

            // El surco y lo que quedó en él.
            var restos = new GameObject("Aterrizaje forzoso").transform;
            Surco surco;
            try
            {
                surco = SurcoDelAterrizaje(valle, centro, rumbo, restos);
            }
            finally
            {
                QuitarCasco();
            }
            // Tierra echada sobre el casco: arrimada a la panza y a los costados, en montón.
            var colores = new ColorTerreno(surco);
            Enterrar(piezasNucleo, valle, colores, restos, "casco", 0.6f, 2.4f, 0.45f);
            casco.restos = TirarPiezas(valle, surco, restos, escala, pintura, colores);

            // Humo y fuego: del hueco de los motores y de la tierra quemada.
            var fuegos = new List<ParticleSystem>();
            var luces = new List<Light>();
            var humos = new List<ParticleSystem>();
            Vector3 hueco = new Vector3(0f, caja.size.y * 0.55f, -LargoNave * 0.32f);
            Fuego(cuerpo, hueco, 1f, fuegos, luces);
            humos.Add(Humo(cuerpo, hueco + Vector3.up * 0.4f, 1f));
            Vector3 cerca = surco.Punto(0.9f, 0.6f);
            cerca.y = SueloConSurco(valle, cerca.x, cerca.z) + 0.05f;
            Fuego(restos, cerca, 0.8f, fuegos, luces);
            humos.Add(Humo(restos, cerca + Vector3.up * 0.2f, 0.55f));
            Vector3 toque = surco.Punto(0.06f, -0.3f);
            toque.y = SueloConSurco(valle, toque.x, toque.z) + 0.1f;
            Fuego(restos, toque, 0.6f, fuegos, luces);
            casco.fuegos = fuegos.ToArray();
            casco.lucesFuego = luces.ToArray();
            casco.humos = humos.ToArray();
        }

        /// <summary>El casco hecho con el kit propio, por si falta el paquete.</summary>
        static void CascoAntiguo(Vector3 centro)
        {
            var modeloCasco = AssetDatabase.LoadAssetAtPath<GameObject>(ImportadorKit.CarpetaArte + "/Nave/casco.fbx");
            if (modeloCasco == null)
            {
                Debug.LogWarning("Nostos: falta Nave/casco.fbx. Ejecuta Pipeline/generar_kit.py.");
                return;
            }
            var casco = (GameObject)PrefabUtility.InstantiatePrefab(modeloCasco);
            casco.name = "Casco de la nave";
            casco.transform.position = centro;
            casco.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var cascoComp = casco.AddComponent<CascoNave>();
            cascoComp.celdaX = NaveCeldaX;
            cascoComp.celdaZ = NaveCeldaZ;
            cascoComp.ancho = NaveAncho;
            cascoComp.fondo = NaveFondo;
        }

        /// <summary>
        /// Copia del material del paquete con la suciedad y el desgaste subidos (_Dirty del shader
        /// "Colorize" del paquete): la nave llega castigada. Si la propiedad cambia de nombre, se
        /// queda como el original.
        /// </summary>
        static Material MaterialNave()
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>(CarpetaNave + "Materials/StarSparrow_" + ColorNave + ".mat");
            if (original == null) return null;
            var material = new Material(original) { name = "Nave de Nicias" };
            Shader shader = material.shader;
            var propiedades = new List<string>();
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string nombre = shader.GetPropertyName(i);
                ShaderPropertyType tipo = shader.GetPropertyType(i);
                propiedades.Add(nombre + " (" + tipo + ")");
                // En el shader del paquete el desgaste es una textura (_Wearout) que regula _Dirty.
                if (tipo != ShaderPropertyType.Range) continue;
                if (nombre != "_Dirty" && nombre.IndexOf("wear", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                Vector2 limites = shader.GetPropertyRangeLimits(i);
                material.SetFloat(nombre, Mathf.Lerp(material.GetFloat(nombre), limites.y, 0.7f));
            }
            Debug.Log("Nostos: material de la nave a partir de StarSparrow_" + ColorNave + ". Propiedades: " + string.Join(", ", propiedades));
            AssetDatabase.CreateAsset(material, CarpetaPrefabsPaquetes + "/nave_nicias.mat");
            return material;
        }

        /// <summary>Pone la pintura de Nicias en lugar de los colores del paquete (solo los StarSparrow_*).</summary>
        static void Pintar(GameObject objeto, Material pintura)
        {
            if (pintura == null) return;
            foreach (Renderer r in objeto.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materiales = r.sharedMaterials;
                for (int i = 0; i < materiales.Length; i++)
                {
                    if (materiales[i] != null && materiales[i].name.StartsWith("StarSparrow_")) materiales[i] = pintura;
                }
                r.sharedMaterials = materiales;
            }
        }

        // ------------------------------------------------------------------ el surco

        /// <summary>El recorrido del surco: un rebote corto y el surco largo que acaba bajo la nave.</summary>
        class Surco
        {
            public Vector3 inicio, fin, lado;
            public float largo;
            /// <summary>Tramo del rebote (0..Rebote) y comienzo del surco largo.</summary>
            public const float Rebote = 0.16f, Largo = 0.24f;

            /// <summary>Punto del recorrido en t (0..1), desplazado `lateral` m a un lado (curva suave).</summary>
            public Vector3 Punto(float t, float lateral, bool soloXZ = false)
            {
                Vector3 p = Vector3.Lerp(inicio, fin, t) + lado * (Mathf.Sin(t * Mathf.PI) * 1.8f + lateral);
                return soloXZ ? new Vector3(p.x, 0f, p.z) : p;
            }

            /// <summary>Semiancho del surco en t: estrecho al primer toque, ancho donde se clavó el casco.</summary>
            public float Ancho(float t)
            {
                if (t < Rebote) return Mathf.Lerp(0.6f, 1.1f, Mathf.Sin(t / Rebote * Mathf.PI));
                return Mathf.Lerp(1.1f, 2.7f, Mathf.SmoothStep(0f, 1f, (t - Largo) / 0.45f));
            }
        }

        static Surco SurcoDelAterrizaje(Valle valle, Vector3 centro, Vector3 rumbo, Transform padre)
        {
            var surco = new Surco
            {
                inicio = InicioSurco,
                fin = centro + rumbo * (LargoNave * 0.38f),
                lado = Vector3.Cross(Vector3.up, rumbo).normalized,
            };
            surco.largo = Vector3.Distance(surco.inicio, surco.fin);

            ColorTerreno colores = new ColorTerreno(surco);
            var vertices = new List<Vector3>();
            var colorV = new List<Color32>();
            var uv = new List<Vector2>();
            var quemado = new List<Vector2>();
            var triangulos = new List<int>();
            Tramo(surco, valle, colores, 0f, Surco.Rebote, false, vertices, colorV, uv, quemado, triangulos);
            Tramo(surco, valle, colores, Surco.Largo, 1f, true, vertices, colorV, uv, quemado, triangulos);

            var malla = new Mesh { name = "Surco de la nave" };
            malla.SetVertices(vertices);
            malla.SetColors(colorV);
            malla.SetUVs(0, uv);
            malla.SetUVs(1, quemado);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            AssetDatabase.CreateAsset(malla, CarpetaPrefabsPaquetes + "/surco_nave.asset");

            var go = new GameObject("Surco");
            go.transform.SetParent(padre, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var render = go.AddComponent<MeshRenderer>();
            Material material = CargarOCrear("Surco", "Nostos/TierraRemovida");
            var tierra = AssetDatabase.LoadAssetAtPath<Texture2D>(CarpetaTexturasPaquete + "Summer_Dirt_A.png");
            if (tierra != null)
            {
                material.SetTexture("_BaseMap", tierra);
                material.mainTexture = tierra;
            }
            CespedEn(material);
            EditorUtility.SetDirty(material);
            render.sharedMaterial = material;
            var colision = go.AddComponent<MeshCollider>();
            colision.sharedMesh = malla;
            // El fondo del surco va por debajo del suelo del valle: se hunde el suelo ahí.
            HundirSueloBajoElSurco(colision);

            // Que no se siembre nada encima, y que no se construya sobre el surco.
            for (float t = 0f; t <= 1.001f; t += 0.04f)
            {
                if (t > Surco.Rebote && t < Surco.Largo) continue;
                Vector3 p = surco.Punto(t, 0f, true);
                libres.Add(new Vector3(p.x, surco.Ancho(t) + 2.2f, p.z));
                if (p.magnitude > RadioEdificable + 3f) continue;
                var reserva = new GameObject("Reserva del surco");
                reserva.transform.SetParent(padre, false);
                reserva.transform.position = p;
                reserva.AddComponent<ReservaRejilla>().radio = surco.Ancho(t) + 0.6f;
            }
            libres.Add(new Vector3(centro.x, LargoNave * 0.6f + 2f, centro.z));
            return surco;
        }

        /// <summary>
        /// Un tramo del surco como cinta: fondo apisonado, paredes, cordones de tierra levantada a
        /// los lados que se deshacen en terrones sobre la hierba. Si `empuje`, acaba en el montón de
        /// tierra que la nave empujó por delante.
        /// </summary>
        static void Tramo(Surco surco, Valle valle, ColorTerreno colores, float t0, float t1, bool empuje,
            List<Vector3> vertices, List<Color32> colorV, List<Vector2> uv, List<Vector2> quemado, List<int> triangulos)
        {
            const int Lados = 22;
            const float Empuje = 4.5f;  // metros de montón por delante del final del surco
            float metros = (t1 - t0) * surco.largo + (empuje ? Empuje : 0f);
            int pasos = Mathf.Max(8, Mathf.CeilToInt(metros / 0.45f));
            int base0 = vertices.Count;
            Vector3 avance = (surco.fin - surco.inicio).normalized;
            var sinCasco = new List<float>();   // altura de cada vértice sin arrimar al casco
            var techo = new List<float>();      // panza del casco encima (o +infinito)
            for (int i = 0; i <= pasos; i++)
            {
                float d = i / (float)pasos * metros;                    // metros desde el principio del tramo
                float t = t0 + Mathf.Min(d, (t1 - t0) * surco.largo) / surco.largo;
                float pasado = Mathf.Max(0f, d - (t1 - t0) * surco.largo); // metros más allá del final (montón)
                float e = empuje ? Mathf.Clamp01(pasado / Empuje) : 0f;
                float k = (t - t0) / Mathf.Max(0.001f, t1 - t0);        // 0..1 dentro del tramo
                float w = surco.Ancho(t);
                // Los extremos se cierran redondeados.
                float cierreInicio = Mathf.Sqrt(Mathf.Clamp01(d / Mathf.Min(1.4f, metros * 0.3f)));
                float cierreFinal = empuje ? Mathf.Sqrt(Mathf.Max(0.12f, 1f - e * e * 0.9f)) : Mathf.Sqrt(Mathf.Clamp01((metros - d) / Mathf.Min(1.4f, metros * 0.3f)));
                float cierre = Mathf.Max(0.05f, Mathf.Min(cierreInicio, cierreFinal));
                // Toda la altura baja a cero en los extremos del tramo: nada de paredes cortadas.
                float alturaInicio = Mathf.SmoothStep(0f, 1f, d / 1.6f);
                float alturaFinal = empuje ? 1f - Mathf.SmoothStep(0.55f, 1f, e) : Mathf.SmoothStep(0f, 1f, (metros - d) / 1.6f);
                float altura = Mathf.Min(alturaInicio, alturaFinal);
                float cordon = (empuje ? Mathf.Lerp(0.3f, 0.85f, k) : Mathf.Lerp(0.12f, 0.3f, Mathf.Sin(k * Mathf.PI))) * altura;
                // El montón que la nave empujó por delante: sube y vuelve a bajar hasta el suelo.
                float monton = empuje ? Mathf.Sin(Mathf.Clamp01(e * 1.15f) * Mathf.PI) * 1.2f : 0f;
                Vector3 eje = surco.Punto(t, 0f, true) + avance * pasado;
                float quema = empuje ? Mathf.SmoothStep(0f, 1f, (k - 0.55f) / 0.45f) : Mathf.Clamp01(1f - k * 1.6f) * 0.8f;
                // Lo hondo del surco: poco en el rebote y más cuanto más se clavó el casco. Se
                // deshace hacia los extremos y bajo el montón, para no dejar escalones.
                float hondoInicio = Mathf.SmoothStep(0f, 1f, (d - 0.8f) / 3.2f);
                float hondoFinal = empuje ? 1f - Mathf.SmoothStep(0f, 0.55f, e) : Mathf.SmoothStep(0f, 1f, (metros - d - 0.8f) / 3.2f);
                float hondo = (empuje ? Mathf.Lerp(0.38f, 0.85f, Mathf.SmoothStep(0f, 1f, k)) : 0.28f * Mathf.Sin(k * Mathf.PI))
                              * Mathf.Min(hondoInicio, hondoFinal);

                for (int j = 0; j <= Lados; j++)
                {
                    float u = j / (float)Lados * 2f - 1f;                  // -1..1 de un borde al otro
                    float lado = Mathf.Sign(u);
                    float borde = 0.55f + 0.9f * cordon / 0.55f;            // ancho de la tierra echada fuera
                    float ruidoBorde = Mathf.PerlinNoise(d * 0.45f + (lado > 0 ? 11f : 37f), 5.3f);
                    float radio = (w + borde * (0.65f + 0.7f * ruidoBorde)) * cierre;
                    float x = u * radio;
                    float a = Mathf.Abs(x);
                    float wc = w * cierre;
                    float ondas = (Mathf.PerlinNoise(x * 1.7f + 3f, d * 1.3f) - 0.5f);
                    float y;
                    float tierra;
                    float fondo;   // el fondo del surco, apisonado y en sombra, se oscurece
                    if (a < wc * 0.45f)
                    {
                        // fondo: hundido y apisonado, con estrías a lo largo
                        float cuenco = a / Mathf.Max(0.01f, wc * 0.45f);
                        y = -hondo * (1f - 0.12f * cuenco * cuenco) + ondas * 0.05f + Mathf.Sin(x * 5.5f) * 0.012f;
                        tierra = 1f;
                        fondo = 1f;
                    }
                    else if (a < wc)
                    {
                        // pared: sube del fondo al cordón de tierra echada a los lados
                        float s = Mathf.SmoothStep(0f, 1f, (a - wc * 0.45f) / (wc * 0.55f));
                        y = Mathf.Lerp(-hondo * 0.88f, cordon, s) + ondas * 0.06f * s;
                        tierra = 1f;
                        fondo = 1f - s;
                    }
                    else
                    {
                        float s = Mathf.Clamp01((a - wc) / Mathf.Max(0.01f, radio - wc));
                        y = cordon * Mathf.Pow(1f - s, 1.6f) + ondas * 0.09f * (1f - s);
                        tierra = 1f - Mathf.SmoothStep(0f, 1f, (s - 0.35f) / 0.65f);
                        fondo = 0f;
                    }
                    float haciaFuera = Mathf.Clamp01(a / Mathf.Max(0.01f, radio));
                    float sobre = Mathf.Max(y, 0f), bajo = Mathf.Min(y, 0f);
                    y = sobre * Mathf.Max(altura, monton > 0f ? 1f : 0f) + bajo + monton * Mathf.Pow(1f - haciaFuera * haciaFuera, 1.5f);
                    if (j == 0 || j == Lados || i == 0 || i == pasos)
                    {
                        y = 0f;
                        tierra = 0f;
                    }

                    Vector3 p = eje + surco.lado * x;
                    float suelo = valle.Altura(p.x, p.z);
                    // Fuera del llano el terreno real hace facetas: un poco más alto para no hundirse.
                    float margen = p.magnitude > 44f ? 0.07f : 0.015f;
                    float alto = suelo + margen + y;
                    sinCasco.Add(alto);
                    // Bajo la nave y a su lado, la tierra sube hasta tocar el casco: sin huecos.
                    // Como mucho 1 m por encima de donde estaría: el flanco del casco no la arrastra.
                    float panza = float.PositiveInfinity;
                    if (j != 0 && j != Lados && i != 0 && i != pasos && Arrimar(p.x, p.z, out float casco))
                    {
                        panza = BajoDelCasco(p.x, p.z);
                        if (float.IsNaN(panza)) panza = float.PositiveInfinity;
                        // Justo debajo, hasta la panza; alrededor, el talud llega a 1 m como mucho.
                        float tope = float.IsPositiveInfinity(panza) || panza > suelo + 1.6f ? suelo + 1f : panza + 0.1f;
                        casco = Mathf.Min(casco, Mathf.Max(alto, tope));
                        if (casco > alto)
                        {
                            alto = casco;
                            tierra = 1f;
                        }
                    }
                    techo.Add(panza);
                    vertices.Add(new Vector3(p.x, alto, p.z));
                    Color32 c = colores.En(p.x, p.z);
                    c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(tierra) * 255f);
                    colorV.Add(c);
                    // La textura tiene vetas en diagonal: giradas 45° quedan a lo largo del surco.
                    float lat = x / 6f, lon = (t * surco.largo + pasado) / 6f;
                    uv.Add(new Vector2((lat + lon) * 0.7071f, (lon - lat) * 0.7071f));
                    float centroQuemado = 1f - Mathf.SmoothStep(0f, 1f, a / Mathf.Max(0.01f, wc * 1.4f));
                    quemado.Add(new Vector2(quema * (0.35f + 0.65f * centroQuemado), fondo * (1f - monton * 0.6f)));
                }
            }
            int fila = Lados + 1;
            AlisarArrimado(vertices, colorV, base0, pasos + 1, fila, sinCasco, techo);
            for (int i = 0; i < pasos; i++)
            {
                for (int j = 0; j < Lados; j++)
                {
                    int a = base0 + i * fila + j;
                    int b = a + 1, c = a + fila, d = c + 1;
                    // Caras hacia arriba: el lado +u queda a la derecha del avance.
                    triangulos.Add(a); triangulos.Add(c); triangulos.Add(b);
                    triangulos.Add(b); triangulos.Add(c); triangulos.Add(d);
                }
            }
        }

        /// <summary>
        /// La tierra arrimada al casco sale a dientes (unos vértices tocan el casco y los de al lado
        /// no). Se alisa como un montón redondeado: varias pasadas de media con los vecinos, sin bajar
        /// nunca de la forma sin casco ni subir por encima de la panza del casco.
        /// </summary>
        static void AlisarArrimado(List<Vector3> vertices, List<Color32> colorV, int base0, int filas, int columnas,
            List<float> sinCasco, List<float> techo)
        {
            int n = filas * columnas;
            var alto = new float[n];
            var zona = new bool[n];
            bool alguno = false;
            for (int k = 0; k < n; k++)
            {
                alto[k] = vertices[base0 + k].y;
                if (alto[k] > sinCasco[k] + 0.01f) alguno = true;
            }
            if (!alguno) return;
            // Solo cerca de lo arrimado (dos vértices alrededor), y nunca los bordes de la cinta.
            for (int i = 1; i < filas - 1; i++)
                for (int j = 1; j < columnas - 1; j++)
                {
                    for (int di = -3; di <= 3 && !zona[i * columnas + j]; di++)
                        for (int dj = -3; dj <= 3; dj++)
                        {
                            int ii = i + di, jj = j + dj;
                            if (ii < 0 || jj < 0 || ii >= filas || jj >= columnas) continue;
                            int q = ii * columnas + jj;
                            if (alto[q] > sinCasco[q] + 0.01f)
                            {
                                zona[i * columnas + j] = true;
                                break;
                            }
                        }
                }
            var nuevo = new float[n];
            for (int pasada = 0; pasada < 7; pasada++)
            {
                System.Array.Copy(alto, nuevo, n);
                for (int i = 1; i < filas - 1; i++)
                    for (int j = 1; j < columnas - 1; j++)
                    {
                        int k = i * columnas + j;
                        if (!zona[k]) continue;
                        float suma = 0f;
                        for (int di = -1; di <= 1; di++)
                            for (int dj = -1; dj <= 1; dj++)
                                suma += alto[(i + di) * columnas + j + dj] * (di == 0 && dj == 0 ? 2f : 1f);
                        float media = suma / 10f;
                        nuevo[k] = Mathf.Min(Mathf.Max(sinCasco[k], media), techo[k] - 0.02f);
                    }
                System.Array.Copy(nuevo, alto, n);
            }
            for (int k = 0; k < n; k++)
            {
                Vector3 v = vertices[base0 + k];
                if (Mathf.Abs(v.y - alto[k]) < 1e-4f) continue;
                vertices[base0 + k] = new Vector3(v.x, alto[k], v.z);
                if (alto[k] > sinCasco[k] + 0.02f)
                {
                    Color32 c = colorV[base0 + k];
                    c.a = 255;
                    colorV[base0 + k] = c;
                }
            }
        }

        /// <summary>Colisionadores provisionales con la forma exacta del casco, para arrimarle la tierra.</summary>
        static readonly List<MeshCollider> cascoProvisional = new List<MeshCollider>();
        static Bounds zonaCasco;

        /// <summary>Pone colisionadores con las mallas visibles del núcleo (lo que se ve al empezar).</summary>
        static void PrepararCasco(IEnumerable<GameObject> nucleo)
        {
            QuitarCasco();
            bool hay = false;
            foreach (GameObject pieza in nucleo)
            {
                foreach (MeshFilter filtro in pieza.GetComponentsInChildren<MeshFilter>())
                {
                    if (filtro.sharedMesh == null || filtro.GetComponent<Renderer>() == null) continue;
                    var mc = filtro.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = filtro.sharedMesh;
                    cascoProvisional.Add(mc);
                    Bounds b = filtro.GetComponent<Renderer>().bounds;
                    if (!hay) zonaCasco = b;
                    else zonaCasco.Encapsulate(b);
                    hay = true;
                }
            }
            zonaCasco.Expand(new Vector3(4f, 0f, 4f));
            Physics.SyncTransforms();
        }

        static void QuitarCasco()
        {
            foreach (MeshCollider mc in cascoProvisional)
            {
                if (mc != null) Object.DestroyImmediate(mc);
            }
            cascoProvisional.Clear();
        }

        /// <summary>
        /// Altura a la que llega la tierra arrimada al casco en (x, z): debajo, justo hasta tocarlo;
        /// alrededor, en talud (unos 40°) desde la parte baja del casco. False si el casco queda lejos.
        /// </summary>
        static bool Arrimar(float x, float z, out float altura)
        {
            altura = float.MinValue;
            if (cascoProvisional.Count == 0) return false;
            if (x < zonaCasco.min.x || x > zonaCasco.max.x || z < zonaCasco.min.z || z > zonaCasco.max.z) return false;
            const float Talud = 0.85f;
            bool toca = false;
            for (int anillo = 0; anillo <= 3; anillo++)
            {
                float r = anillo * 0.5f;
                int pasos = anillo == 0 ? 1 : 8;
                for (int k = 0; k < pasos; k++)
                {
                    float ang = k * Mathf.PI * 2f / pasos;
                    float h = BajoDelCasco(x + Mathf.Cos(ang) * r, z + Mathf.Sin(ang) * r);
                    if (float.IsNaN(h)) continue;
                    altura = Mathf.Max(altura, h + 0.12f - Talud * r);
                    toca = true;
                }
            }
            return toca;
        }

        /// <summary>Altura de la panza del casco en (x, z), mirando desde abajo; NaN si no hay casco.</summary>
        static float BajoDelCasco(float x, float z)
        {
            var rayo = new Ray(new Vector3(x, zonaCasco.min.y - 3f, z), Vector3.up);
            float mejor = float.NaN;
            foreach (MeshCollider mc in cascoProvisional)
            {
                if (mc == null || !mc.Raycast(rayo, out RaycastHit golpe, zonaCasco.size.y + 6f)) continue;
                if (float.IsNaN(mejor) || golpe.point.y < mejor) mejor = golpe.point.y;
            }
            return mejor;
        }

        /// <summary>
        /// Color del terreno en un punto: el de la cara del valle que hay debajo (paleta), para que
        /// el borde del surco se funda con la hierba sin costura.
        /// </summary>
        class ColorTerreno
        {
            readonly List<Vector2> a = new List<Vector2>(), b = new List<Vector2>(), c = new List<Vector2>();
            readonly List<Color32> color = new List<Color32>();
            static readonly Color32 Hierba = new Color32(150, 160, 74, 255);

            public ColorTerreno(Surco surco)
            {
                try
                {
                    GameObject valle = GameObject.Find("Valle");
                    Transform suelo = valle != null ? BuscarHijo(valle.transform, "Suelo") : null;
                    MeshFilter filtro = suelo != null ? suelo.GetComponent<MeshFilter>() : null;
                    if (filtro == null || filtro.sharedMesh == null || !File.Exists(ImportadorKit.RutaTextura)) return;
                    var paleta = new Texture2D(2, 2);
                    paleta.LoadImage(File.ReadAllBytes(ImportadorKit.RutaTextura));
                    Color32[] pixeles = paleta.GetPixels32();
                    int ancho = paleta.width, alto = paleta.height;
                    Object.DestroyImmediate(paleta);

                    Vector3 min = Vector3.Min(surco.inicio, surco.fin) - new Vector3(9f, 0f, 9f);
                    Vector3 max = Vector3.Max(surco.inicio, surco.fin) + new Vector3(9f, 0f, 9f);
                    Mesh malla = filtro.sharedMesh;
                    Vector3[] v = malla.vertices;
                    Vector2[] uvs = malla.uv;
                    int[] t = malla.triangles;
                    Matrix4x4 m = filtro.transform.localToWorldMatrix;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 p0 = m.MultiplyPoint3x4(v[t[i]]), p1 = m.MultiplyPoint3x4(v[t[i + 1]]), p2 = m.MultiplyPoint3x4(v[t[i + 2]]);
                        if (Mathf.Max(p0.x, p1.x, p2.x) < min.x || Mathf.Min(p0.x, p1.x, p2.x) > max.x) continue;
                        if (Mathf.Max(p0.z, p1.z, p2.z) < min.z || Mathf.Min(p0.z, p1.z, p2.z) > max.z) continue;
                        Vector2 q = uvs[t[i]];
                        int px = Mathf.Clamp((int)(q.x * ancho), 0, ancho - 1);
                        int py = Mathf.Clamp((int)(q.y * alto), 0, alto - 1);
                        a.Add(new Vector2(p0.x, p0.z));
                        b.Add(new Vector2(p1.x, p1.z));
                        c.Add(new Vector2(p2.x, p2.z));
                        color.Add(pixeles[py * ancho + px]);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("Nostos: no pude leer los colores del valle para el surco (" + ex.Message + "); uso el de la hierba.");
                    a.Clear(); b.Clear(); c.Clear(); color.Clear();
                }
            }

            public Color32 En(float x, float z)
            {
                var p = new Vector2(x, z);
                for (int i = 0; i < a.Count; i++)
                {
                    if (Dentro(p, a[i], b[i], c[i])) return color[i];
                }
                return Hierba;
            }

            static bool Dentro(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
                float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
                float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
                bool negativo = d1 < 0 || d2 < 0 || d3 < 0;
                bool positivo = d1 > 0 || d2 > 0 || d3 > 0;
                return !(negativo && positivo);
            }
        }

        // ------------------------------------------------------------------ piezas arrancadas

        /// <summary>
        /// Piezas que se soltaron en el choque, medio enterradas por el surco. Una entrada por
        /// módulo del juego (la 0 es el módulo 1): al montarlo, desaparecen del surco.
        /// </summary>
        static GameObject[] TirarPiezas(Valle valle, Surco surco, Transform padre, float escala, Material pintura, ColorTerreno colores)
        {
            var grupos = new GameObject[5];
            string[] nombres = { "casco", "soporte vital", "motor", "navegación", "combustible" };
            for (int i = 0; i < 5; i++)
            {
                grupos[i] = new GameObject("Restos: " + nombres[i]);
                grupos[i].transform.SetParent(padre, false);
            }
            // (tipo, t, lateral en m, giro en grados x/y/z, cuánto se hunde)
            var tiradas = new (string tipo, float t, float lateral, Vector3 giro, float hundido)[]
            {
                ("Engine", 0.11f, 2.6f, new Vector3(18f, 40f, 95f), 0.42f),      // se soltó en el primer toque
                ("Fin", 0.3f, -0.4f, new Vector3(-12f, 130f, 160f), 0.4f),
                ("Wing", 0.4f, -3.4f, new Vector3(8f, -35f, 172f), 0.3f),        // ala arrancada, boca abajo
                ("Tail", 0.5f, 0.6f, new Vector3(25f, 80f, -30f), 0.45f),
                ("Wing", 0.63f, 3.8f, new Vector3(-14f, 200f, 24f), 0.38f),
                ("Plasma", 0.72f, -1.1f, new Vector3(70f, 15f, 10f), 0.42f),
                ("Fin", 0.8f, 2.9f, new Vector3(5f, 300f, 110f), 0.35f),
                ("Tail", 0.86f, -2.7f, new Vector3(-30f, 10f, 75f), 0.42f),
                ("Thruster", 0.93f, 1.4f, new Vector3(95f, 60f, 0f), 0.45f),
            };
            int numeroPieza = 0;
            foreach (var p in tiradas)
            {
                int modulo = ModuloDePieza(p.tipo);
                var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaNave + "Prefabs/Modules/StarSparrow_" + p.tipo + ".prefab");
                if (modelo == null || modulo < 1) continue;
                var pieza = (GameObject)PrefabUtility.InstantiatePrefab(modelo, grupos[modulo - 1].transform);
                pieza.transform.localScale = Vector3.one * escala;
                pieza.transform.rotation = Quaternion.Euler(p.giro);
                Pintar(pieza, pintura);
                Vector3 sitio = surco.Punto(p.t, p.lateral, true);
                float suelo = SueloConSurco(valle, sitio.x, sitio.z);
                Asentar(pieza, new Vector3(sitio.x, suelo, sitio.z));
                pieza.transform.position += Vector3.down * (Medir(pieza).size.y * p.hundido);
                foreach (Transform t in pieza.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
                // Tierra encima: según lo que asoma la pieza y lo grande que es.
                Physics.SyncTransforms();
                Bounds b = Medir(pieza);
                float asoma = Mathf.Max(0.1f, b.max.y - suelo);
                float alto = Mathf.Clamp(asoma * 0.4f, 0.15f, 0.55f);
                float ancho = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.45f, 0.9f, 1.8f);
                Enterrar(new[] { pieza }, valle, colores, padre, "pieza_" + (numeroPieza++), alto, ancho, 0.15f);
            }
            return grupos;
        }

        // ------------------------------------------------------------------ fuego y humo

        static void Fuego(Transform padre, Vector3 sitioLocal, float escala, List<ParticleSystem> fuegos, List<Light> luces)
        {
            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(FuegoRojo);
            if (modelo != null)
            {
                var fuego = (GameObject)PrefabUtility.InstantiatePrefab(modelo, padre);
                fuego.name = "Fuego";
                fuego.transform.localPosition = sitioLocal;
                fuego.transform.localRotation = Quaternion.identity;
                fuego.transform.localScale = Vector3.one * escala;
                foreach (ParticleSystem s in fuego.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = s.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    fuegos.Add(s);
                }
                // Por si el prefab trae el suelo de su escena de demostración.
                foreach (MeshRenderer r in fuego.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            }
            var luzGo = new GameObject("Luz del fuego");
            luzGo.transform.SetParent(padre, false);
            luzGo.transform.localPosition = sitioLocal + Vector3.up * 0.9f;
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(1f, 0.55f, 0.25f);
            luz.intensity = 6f * escala;
            luz.range = 9f * escala;
            luz.shadows = LightShadows.None;
            luces.Add(luz);
        }

        static ParticleSystem Humo(Transform padre, Vector3 sitioLocal, float fuerza)
        {
            var go = new GameObject("Humo");
            go.transform.SetParent(padre, false);
            go.transform.localPosition = sitioLocal;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var humo = go.AddComponent<ParticleSystem>();
            humo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = humo.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f * fuerza, 2.2f * fuerza);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.17f, 0.16f, 0.15f, 0.6f), new Color(0.34f, 0.32f, 0.3f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 220;
            main.gravityModifier = -0.02f;

            var emision = humo.emission;
            emision.rateOverTime = 7f * fuerza;

            var forma = humo.shape;
            forma.shapeType = ParticleSystemShapeType.Cone;
            forma.angle = 14f;
            forma.radius = 0.5f * fuerza;

            // Viento suave hacia el este.
            var velocidad = humo.velocityOverLifetime;
            velocidad.enabled = true;
            velocidad.space = ParticleSystemSimulationSpace.World;
            velocidad.x = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            velocidad.y = new ParticleSystem.MinMaxCurve(0f, 0.2f);
            velocidad.z = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

            var tamano = humo.sizeOverLifetime;
            tamano.enabled = true;
            tamano.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.9f));

            var color = humo.colorOverLifetime;
            color.enabled = true;
            var degradado = new Gradient();
            degradado.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(degradado);

            var giro = humo.rotationOverLifetime;
            giro.enabled = true;
            giro.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            var render = go.GetComponent<ParticleSystemRenderer>();
            render.sharedMaterial = MaterialHumo();
            render.sortMode = ParticleSystemSortMode.Distance;
            render.shadowCastingMode = ShadowCastingMode.Off;
            render.receiveShadows = false;
            return humo;
        }

        static Material MaterialHumo()
        {
            const string ruta = "Assets/Nostos/Arte/Materiales/Humo.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (material != null) return material;
            material = PaquetesURP.NuevoDeParticulas("Humo", TexturaHumo(), false);
            if (material == null) return null;
            CarpetasNostos.Asegurar("Assets/Nostos/Arte/Materiales");
            AssetDatabase.CreateAsset(material, ruta);
            return material;
        }

        /// <summary>Bocanada de humo: círculo suave con grumos, hecha una vez y guardada como PNG.</summary>
        static Texture2D TexturaHumo()
        {
            var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaHumo);
            if (textura != null) return textura;
            const int lado = 128;
            var imagen = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            var pixeles = new Color32[lado * lado];
            for (int y = 0; y < lado; y++)
            {
                for (int x = 0; x < lado; x++)
                {
                    float u = (x + 0.5f) / lado * 2f - 1f, v = (y + 0.5f) / lado * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float grumos = Mathf.PerlinNoise(x * 0.06f + 7f, y * 0.06f + 3f) * 0.6f + Mathf.PerlinNoise(x * 0.15f, y * 0.15f + 9f) * 0.4f;
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.25f, 1f, r + (grumos - 0.5f) * 0.45f)) * (0.55f + 0.45f * grumos);
                    byte gris = (byte)Mathf.RoundToInt(Mathf.Lerp(200f, 255f, grumos));
                    pixeles[y * lado + x] = new Color32(gris, gris, gris, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            imagen.SetPixels32(pixeles);
            imagen.Apply();
            CarpetasNostos.Asegurar("Assets/Nostos/Arte/Efectos");
            File.WriteAllBytes(RutaHumo, imagen.EncodeToPNG());
            Object.DestroyImmediate(imagen);
            AssetDatabase.ImportAsset(RutaHumo);
            var importador = (TextureImporter)AssetImporter.GetAtPath(RutaHumo);
            if (importador != null)
            {
                importador.alphaIsTransparency = true;
                importador.wrapMode = TextureWrapMode.Clamp;
                importador.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(RutaHumo);
        }
    }
}
