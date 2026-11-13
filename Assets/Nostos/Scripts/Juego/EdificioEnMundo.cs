using Nostos.Sim;
using UnityEngine;

namespace Nostos
{
    /// <summary>
    /// La cara visible de un edificio: muestra el modelo del estado de obra actual, recibe el
    /// material que Nicias entrega a mano y, una vez terminado, anima su máquina mientras trabaja.
    /// </summary>
    public class EdificioEnMundo : MonoBehaviour, IInteractuable
    {
        /// <summary>En el modo constructor se pueden quitar los tejados para ver los interiores.</summary>
        public static bool SinTejados;

        Juego juego;
        Renderer[] tejado;
        bool tejadoOculto;
        GameObject modelo;
        Transform movil;
        Light luzTrabajo;
        EstadoObra estadoMostrado;
        float intensidadLuz;
        bool iniciado;

        public Edificio Sim { get; private set; }
        public DefinicionEdificio Definicion { get; private set; }

        public void Iniciar(Juego juego, DefinicionEdificio definicion, Edificio sim)
        {
            this.juego = juego;
            Definicion = definicion;
            Sim = sim;
            Refrescar();
        }

        void Update()
        {
            if (Sim == null) return;
            if (Sim.Estado != estadoMostrado) Refrescar();
            if (tejado != null && tejadoOculto != SinTejados)
            {
                tejadoOculto = SinTejados;
                foreach (Renderer r in tejado)
                {
                    if (r != null) r.enabled = !SinTejados;
                }
            }

            bool trabajando = Sim.Trabajando
                              || (Sim.Terminado && Sim.Datos.Id == Mundo.IdTeleferico
                                  && juego.Mundo.Ticks - juego.Mundo.UltimoViajeTeleferico < 3);
            if (movil != null && trabajando)
            {
                movil.Rotate(540f * Time.deltaTime, 0f, 0f, Space.Self);
            }
            if (luzTrabajo != null)
            {
                float objetivo = trabajando ? 5f + Mathf.PerlinNoise(Time.time * 6f, 0.3f) * 5f : 0f;
                intensidadLuz = Mathf.Lerp(intensidadLuz, objetivo, 1f - Mathf.Exp(-8f * Time.deltaTime));
                luzTrabajo.intensity = intensidadLuz;
                luzTrabajo.enabled = intensidadLuz > 0.05f;
            }
        }

        void Refrescar()
        {
            if (modelo != null)
            {
                modelo.SetActive(false);
                Destroy(modelo);
            }
            movil = null;
            luzTrabajo = null;
            tejado = null;
            tejadoOculto = false;
            estadoMostrado = Sim.Estado;

            GameObject prefab = Definicion.Modelo(Sim.Estado);
            if (prefab != null)
            {
                modelo = Instantiate(prefab, transform);
                modelo.transform.localPosition = Vector3.zero;
                modelo.transform.localRotation = Quaternion.identity;
            }
            if (iniciado) juego.NotificarEdificiosCambiados();
            iniciado = true;
            if (modelo == null) return;

            Transform piezaTejado = Buscar(modelo.transform, "Tejado");
            if (piezaTejado != null) tejado = piezaTejado.GetComponentsInChildren<Renderer>(true);

            if (!Sim.Terminado) return;

            Transform maquina = Buscar(modelo.transform, "Maquina");
            movil = Buscar(modelo.transform, "Movil");
            if (maquina != null) maquina.gameObject.AddComponent<PuntoDeUso>().edificio = this;
            if (movil != null) movil.gameObject.AddComponent<PuntoDeUso>().edificio = this;

            if (Definicion.tipo == TipoEdificio.Produccion)
            {
                var luz = new GameObject("Luz de trabajo");
                luz.transform.SetParent(modelo.transform, false);
                luz.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                luzTrabajo = luz.AddComponent<Light>();
                luzTrabajo.type = LightType.Point;
                luzTrabajo.color = new Color(1f, 0.62f, 0.3f);
                luzTrabajo.range = 7f;
                luzTrabajo.intensity = 0f;
                luzTrabajo.enabled = false;
            }
        }

        static Transform Buscar(Transform raiz, string nombre)
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == nombre) return t;
            }
            return null;
        }

        // ---- Obra: entregar material a mano ----

        public string NombreEstado
        {
            get
            {
                switch (Sim.Estado)
                {
                    case EstadoObra.Cimientos: return "cimientos";
                    case EstadoObra.Esqueleto: return "esqueleto";
                    case EstadoObra.CasiTerminado: return "casi terminado";
                    default: return "terminado";
                }
            }
        }

        public string Texto
        {
            get
            {
                if (Sim == null || Sim.Terminado) return null;
                return "Entregar material a " + Definicion.nombre + " (falta: " + Sim.DescribirFalta() + ")";
            }
        }

        public void Interactuar(GameObject quien)
        {
            if (Sim == null || Sim.Terminado) return;
            int entregadas = Sim.Entregar(juego.Mundo.Mochila, out bool avanzo);
            if (Sim.Terminado) Sonido.Tocar(Efecto.Campana, 0.6f);
            else if (entregadas > 0) Sonido.Tocar(Efecto.Entrega);
            if (entregadas == 0)
            {
                juego.Avisar("No llevas nada de lo que falta: " + Sim.DescribirFalta() + ".");
            }
            else if (Sim.Terminado)
            {
                juego.Avisar(Definicion.nombre + " terminado. Ya puedes entrar y usarlo.");
            }
            else if (avanzo)
            {
                juego.Avisar(Definicion.nombre + ": la obra pasa a " + NombreEstado + ".");
            }
            else
            {
                juego.Avisar("Entregas " + entregadas + ". Aún falta: " + Sim.DescribirFalta() + ".");
            }
        }

        // ---- Edificio terminado: usar la máquina (o la cama) ----

        public string TextoUso
        {
            get
            {
                if (Sim == null || !Sim.Terminado) return null;
                if (Definicion.tipo == TipoEdificio.Refugio)
                {
                    return juego.Mundo.Ciclo.PuedeDormir ? "Dormir hasta la mañana" : "Cama (podrás dormir desde las 18:00)";
                }
                if (Definicion.tipo == TipoEdificio.Almacen)
                {
                    return juego.Mundo.Mochila.Total > 0
                        ? "Guardar en el almacén todo lo que llevas"
                        : "Coger del almacén lo que falta en las obras";
                }
                if (Definicion.tipo == TipoEdificio.Casa)
                {
                    return "Casa de colonos (" + juego.Mundo.Colonos.Habitantes(Sim) + " de "
                           + GestorColonos.PlazasPorCasa + " plazas ocupadas)";
                }
                if (Sim.Datos.Id == Mundo.IdAstillero)
                {
                    return "Astillero: los módulos de la nave se montan en el casco, y la grúa trae del almacén lo que falte";
                }
                if (Sim.Datos.Id == Mundo.IdTeleferico)
                {
                    return "Teleférico: lleva material del almacén a obras y talleres sin porteadores";
                }
                if (Sim.Datos.Receta == null) return null;
                return Sim.Datos.Receta.Entradas.Length == 0
                    ? "Recoger lo producido"
                    : "Dejar " + Recursos.Lista(Sim.Datos.Receta.Entradas) + " por ciclo y recoger lo producido";
            }
        }

        // ---- Faenas: lo que Nicias hace a mano dentro (F / B) ----

        public string TextoFaena => Sim != null && Sim.Terminado ? Faenas.Texto(juego, Sim) : null;

        public void Faena(ControlModo control)
        {
            if (Sim == null || !Sim.Terminado) return;
            Faenas.Instancia.Comenzar(this, control);
        }

        /// <summary>Lo que dura de las faenas: "afinado · 2 tandas dobles".</summary>
        string ResumenFaenas()
        {
            string texto = "";
            if (Sim.TicksAfinado > 0) texto += " · afinado";
            if (Sim.LotesDobles > 0) texto += " · " + Sim.LotesDobles + (Sim.LotesDobles == 1 ? " tanda doble" : " tandas dobles");
            return texto;
        }

        /// <summary>Estado del depósito para el HUD: "dentro: 2 de madera · listo: 4 de tablones".</summary>
        public string Resumen
        {
            get
            {
                if (Sim == null || !Sim.Terminado) return null;
                if (Definicion.tipo == TipoEdificio.Casa)
                {
                    return juego.Mundo.Colonos.Habitantes(Sim) + " de " + GestorColonos.PlazasPorCasa
                           + " plazas · confort " + Sim.Confort + " de " + Edificio.ConfortMaximo;
                }
                if (Definicion.tipo == TipoEdificio.Almacen)
                {
                    string guardado = juego.Mundo.Almacen.Describir();
                    return guardado.Length > 0 ? guardado : "vacío";
                }
                if (Sim.Datos.Receta == null) return null;
                string dentro = Sim.Entrada.Describir();
                string listo = Sim.Salida.Describir();
                return (Sim.Trabajando ? "trabajando" : "parado") + ResumenFaenas()
                       + "\ndentro: " + (dentro.Length > 0 ? dentro : "nada")
                       + " · listo: " + (listo.Length > 0 ? listo : "nada");
            }
        }

        /// <summary>Con la mochila cargada, guarda todo. Con la mochila vacía, coge lo que piden las obras.</summary>
        void UsarAlmacen()
        {
            Bolsa mochila = juego.Mundo.Mochila;
            Bolsa almacen = juego.Mundo.Almacen;

            // Las herramientas se quedan en la mochila: son para trabajar, no carga.
            if (mochila.Total - mochila[Recurso.Herramientas] > 0)
            {
                int guardadas = 0;
                for (int i = 0; i < Recursos.Numero; i++)
                {
                    var recurso = (Recurso)i;
                    if (recurso == Recurso.Herramientas) continue;
                    guardadas += almacen.Meter(recurso, mochila.Sacar(recurso, mochila[recurso]));
                }
                juego.Avisar("Guardas " + guardadas + " en el almacén.");
                return;
            }

            int cogidas = 0;
            bool herramienta = mochila[Recurso.Herramientas] == 0 && almacen[Recurso.Herramientas] > 0
                               && mochila.Meter(Recurso.Herramientas, almacen.Sacar(Recurso.Herramientas, 1)) > 0;
            foreach (Edificio obra in juego.Mundo.Edificios)
            {
                if (obra.Terminado) continue;
                foreach (Cantidad c in obra.CosteActual)
                {
                    int falta = obra.Falta(c.recurso) - mochila[c.recurso];
                    if (falta <= 0) continue;
                    cogidas += mochila.Meter(c.recurso, almacen.Sacar(c.recurso, Mathf.Min(falta, mochila.Hueco)));
                }
            }
            string extra = herramienta ? " Y una herramienta para trabajar." : "";
            juego.Avisar(cogidas > 0
                ? "Coges " + cogidas + " para las obras." + extra
                : (herramienta ? "Coges una herramienta para trabajar." : "No hay nada en el almacén que haga falta en las obras."));
        }

        public void Usar()
        {
            if (Sim == null || !Sim.Terminado) return;
            if (Definicion.tipo == TipoEdificio.Refugio)
            {
                juego.Dormir();
                return;
            }

            if (Definicion.tipo == TipoEdificio.Almacen)
            {
                UsarAlmacen();
                return;
            }
            if (Definicion.tipo == TipoEdificio.Casa || Sim.Datos.Receta == null) return;

            Bolsa mochila = juego.Mundo.Mochila;
            int dejadas = Sim.Depositar(mochila);
            int recogidas = Sim.Recoger(mochila);
            if (dejadas == 0 && recogidas == 0)
            {
                juego.Avisar(Sim.Salida.Total > 0
                    ? "Mochila llena."
                    : Sim.Datos.Receta.Entradas.Length == 0
                        ? "Todavía no hay nada que recoger."
                        : "Trae " + Recursos.Lista(Sim.Datos.Receta.Entradas) + " para que trabaje.");
            }
            else
            {
                Sonido.Tocar(Efecto.Entrega, 0.8f);
                string texto = "";
                if (dejadas > 0) texto += "Dejas " + dejadas + ". ";
                if (recogidas > 0) texto += "Recoges " + recogidas + ".";
                juego.Avisar(texto);
            }
        }
    }

    /// <summary>La pieza de un edificio terminado que se usa a pie: la cama, la sierra, el horno.</summary>
    public class PuntoDeUso : MonoBehaviour, IInteractuable, IConFaena
    {
        public EdificioEnMundo edificio;

        public string Texto => edificio != null ? edificio.TextoUso : null;

        public string TextoFaena => edificio != null ? edificio.TextoFaena : null;

        public void Interactuar(GameObject quien)
        {
            if (edificio != null) edificio.Usar();
        }

        public void Faena(ControlModo control)
        {
            if (edificio != null) edificio.Faena(control);
        }
    }
}
