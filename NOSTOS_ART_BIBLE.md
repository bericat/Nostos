# NOSTOS — BIBLIA ARTÍSTICA Y HOJA DE RUTA DE ASSETS

**Versión:** 1.0 — 13/11/2026  
**Estado:** dirección artística obligatoria para todo asset nuevo  
**Referencia principal:** [CliffStonePackM, Dzeruza — Unity Asset Store](https://assetstore.unity.com/packages/3d/props/exterior/cliffstonepackm-327619)  
**Referencias secundarias:** imagen oficial y capturas del pack adjuntas al proyecto, referencia de personaje Nicias y los diseños de Calcis / Nova Lavinia ya aprobados.

> **Para Claude:** Trata este documento como la especificación principal de estilo visual del proyecto. Cada asset futuro debe pasar sus criterios. No confundas la inspiración visual con la copia literal de mallas, texturas o elementos protegidos de terceros. Si falta la imagen del pack en tu contexto, pide acceder a la captura existente en el proyecto o indica claramente que solo puedes orientar el diseño con la descripción escrita; no pretendas haberla visto.

## 0. Objetivo visual

El mundo de **Nostos** debe percibirse como un único juego **3D estilizado/semiestilizado con materiales ricos, siluetas expresivas y geometría natural cuidadosamente esculpida**. La referencia principal de entorno es **CliffStonePackM** (no otro paquete de acantilados de nombre parecido). Las estructuras retrofuturistas, los personajes y la nave deben compartir la misma calidad de superficies, densidad visual, iluminación y nivel de estilización que el entorno.

**Eslogan artístico:** «Formas estilizadas, materiales creíbles, tecnología artesanal».

Se abandona la idea de que «low-poly» signifique necesariamente **caras planas de color uniforme, piedras triangulares sin textura o modelos hechos con cubos sin terminar**. El objetivo es un acabado artístico integrado: volumen legible a distancia y detalle controlado de cerca. No se busca fotorealismo ni aspecto de juguete de plástico.

## 1. Lo que realmente ofrece la referencia

El listado de **CliffStonePackM** de Dzeruza presenta un pack gratuito de rocas semi-estilizadas. La información encontrada sobre su contenido indica cinco mallas de roca, tres variantes de hierba, seis variaciones de material de piedra, tres materiales de terreno, dos escenas de demostración y un **atlas compartido** para las piedras. La imagen oficial muestra **paredes rocosas por bloques y estratos**, superficies suavizadas e irregulares con detalle mineral, **césped vivo** entre plataformas, tierra marrón/cobriza y una composición luminosa, limpia y legible.

**Dato técnico crítico:** La ficha oficial del pack `CliffStonePackM` en Unity indica **HDRP compatible / URP no compatible**. El proyecto Nostos está planeado en **URP**. No importes el pack suponiendo que funcionará visualmente igual; si se utiliza, evalúa compatibilidad y crea materiales URP equivalentes, con reconstrucción manual de mapas y parámetros si procede. Nunca afirmes que un convertidor automático resolverá correctamente todos los shaders.

Esta biblia **traslada el lenguaje visual del pack a otras familias de assets**, no afirma que el propio paquete contenga árboles, edificios, vehículos o personajes.

## 2. Reglas de estilo no negociables

### 2.1 Forma y modelado

- Siluetas fuertes y sencillas desde lejos; irregularidades orgánicas cuidadosamente distribuidas de cerca.
- Masas grandes primero, formas medianas después, microdetalle al final.
- En naturaleza: bordes desgastados y erosionados; evitar triángulos gigantes artificiales o superficies excesivamente geométricas.
- En arquitectura y maquinaria: aristas definidas, **biseladas**, con construcción industrial creíble; no cajas primitivas sin detalle.
- Asimetría moderada y propósito visual para cada elemento; evitar ruido aleatorio.
- Proporciones estilizadas y coherentes con Nicias, no realismo extremo.

### 2.2 Materiales y texturas

- Materiales suaves, ligeramente texturizados, con variación de albedo y roughness; **no solo colores sólidos**.
- Piedra: estratificación, pequeñas grietas, variaciones tonales, zonas erosionadas y AO sutil.
- Tierra: variación orgánica visible, transiciones de camino, cantos y pequeñas piedras.
- Hierba: masas reconocibles, verde natural y variación de tono; no fosforito homogéneo.
- Madera: veta controlada, desgaste en bordes y marcas de uso; no patrón hiperrealista de primer plano.
- Metal: pintura industrial mate/satinada, arañazos localizados, cobre envejecido, remaches funcionales.
- Texturas y normales deben compartir una **densidad de detalle comparable** en todo el mundo.
- Preferir atlas compartidos y familias de materiales cuando se repitan assets; no crear decenas de materiales únicos sin necesidad.

### 2.3 Iluminación y color

- Exterior: luz diurna luminosa, sombras suaves y volúmenes claros; atmósfera agradable pero no infantil.
- Interior: luz cálida de lámparas, hornos y talleres, contrastada con iluminación fría sutil de instrumentos o cielo.
- Evitar negros excesivos, bloom descontrolado, neones cyberpunk y grises sin vida.
- El detalle debe ser legible **en primera persona y en isométrica**.
- Usar la misma configuración de exposición, tonemapping, sombras y color grading al comparar assets.

### 2.4 Densidad de detalle

- Primero jerarquía de formas; no llenar superficies de greebles, tornillos, grietas o carteles.
- Los objetos cotidianos y edificios deben tener un nivel de detalle perceptivo comparable al de las rocas de referencia vistas a la misma distancia.
- Si un elemento destaca por ser muchísimo más realista o mucho más simplón que sus vecinos, no está aprobado.

## 3. Paleta orientativa (NO extraída oficialmente)

Esta paleta inicial solo sirve para crear prototipos; validar los colores finales contra la **captura original del pack, dentro de una escena Unity con iluminación controlada**.

| Familia | Colores sugeridos | Uso |
|---|---|---|
| Piedra de Lacio | `#777969`, `#95917E`, `#565B50` | Acantilados y rocas |
| Tierra/camino | `#9D6D44`, `#BA8651`, `#76553A` | Caminos, suelos, excavaciones |
| Vegetación | `#76A946`, `#4F8235`, `#A1BF62` | Prado, musgo, hojas |
| Madera | `#88684C`, `#B28C61`, `#594735` | Vigas, tablones, construcciones |
| Nova Lavinia | `#B66F48`, `#718069`, `#D0BD9B` | Cobre, tejidos, cal, materiales locales |
| Calcis | `#DED9CC`, `#365B7A`, `#424A54` | Cerámica blanca, azul frío, metal |
| Acentos tecnológicos | `#ECA65D`, `#74B8CF` | Motores, cristales e instrumentos |

No convertir los colores RGB anteriores en un requisito inmutable ni aplicar el mismo color uniforme a todos los objetos. Crear variaciones compatibles por material, luz y desgaste.

## 4. Aplicación a cada familia de assets

### 4.1 Terreno, montañas y acantilados

**Objetivo:** Geología de capas y grandes bloques naturales como en CliffStonePackM.

- Crear módulos de paredes de roca, esquinas, cimas, terrazas, transiciones de talud, plataformas y desprendimientos.
- Combinar mallas de acantilado con Unity Terrain o mallas de suelo, disimulando sus intersecciones con pequeñas rocas, hierba y suelo.
- Añadir grietas y estratos en zonas significativas. Evitar líneas de corte perfectamente repetidas.
- Usar variedad de escala y rotación sin deformar ni estirar excesivamente texturas.
- Asegurar cimas transitables, pies de pared naturales y ausencia de huecos visibles.
- Generar variantes para pradera/valle, ribera, cantera y zonas alejadas, compartiendo el lenguaje de piedra.

### 4.2 Rocas pequeñas, suelo, caminos, ríos

- Crear piedras pequeñas, guijarros y escombros **de la misma familia mineral** que los acantilados.
- Caminos de tierra marrón/cobriza con bordes erosionados, mezcla de hierba y pequeñas piedras.
- Transición creíble entre roca, tierra, hierba y arena húmeda; no usar cuatro texturas que parezcan pertenecer a juegos diferentes.
- Ribera con piedras redondeadas y vegetación un poco más densa; agua estilizada simple con reflejos controlados.

### 4.3 Vegetación

- Hierba en grupos y variaciones naturales (no millones de tallos hiperrealistas).
- Arbustos y árboles de volúmenes claros, copas orgánicas con detalle de materiales comparable al de la piedra.
- Ramas y troncos visualmente creíbles; evitar tanto árboles fotorrealistas escaneados como low-poly triangulares ultra-planos.
- Densidad reducida cerca de áreas de trabajo; vegetación más rica en espacios silvestres.

### 4.4 Edificios de Nova Lavinia

**Conservar el lore:** son descendientes de Calcis, pero llevan generaciones construyendo con materiales locales.

- Arquitectura de estructura creíble con piedra local, madera gruesa, yeso/estucos, cobre y textiles.
- Formas funcionales con una discreta herencia geométrica calcidense; evitar fantasía medieval genérica.
- Paredes modulares con irregularidades, bordes suavizados, remaches, herrajes y desgaste localizado.
- Puertas, techos, ventanas, cimientos y soportes deben parecer construidos y mantenidos por personas reales.
- Edificios que permitan entrar en primera persona y observar una actividad visible: serrería, fundición, almacén, cocina, taller eléctrico, óptica, astillero y vivienda.
- Crear **cuatro estados físicos de construcción:** cimientos → esqueleto → casi terminado → terminado. Las piezas aparecen físicamente, no como reemplazo cosmético de edificios ya completos.

### 4.5 Construcciones y tecnología de Calcis

- Mantener identidad blanca y azul fría, materiales industriales más uniformes y fabricación precisa.
- Aunque sea tecnología avanzada, renderizarla con **el mismo lenguaje de materiales y sombreado** que el mundo natural.
- Utilizar pintura, cerámica y metal con microvariaciones discretas, no superficies de plástico ni espejos cromados.
- Evitar que parezca un asset sacado de un shooter espacial distinto.

### 4.6 Herramientas, mobiliario, máquinas y props

- Herramientas artesanales que combinen madera, metal y soluciones retrofuturistas (engranajes visibles, bobinas, pequeñas luces técnicas).
- Mesas, cajas, contenedores, barriles, lámparas y hornos con silueta limpia y materiales adecuados.
- Diseñar piezas **reutilizables**, con variantes de tamaño/color/daño.
- Las máquinas deben explicar su función visualmente (entrada, proceso, salida).

### 4.7 Nave de Nicias y sus cinco etapas

- La nave es un **carguero para un piloto**, no un crucero; la bodega domina su volumen, la cabina/litera/cocina son mínimas.
- Versión original de Calcis: silueta de carguero, cerámica blanca, azul frío y ensamblaje pulcro.
- Versiones reconstruidas: mismos puntos estructurales, piezas locales de cobre, metal reutilizado, madera auxiliar y soluciones artesanales.
- **Construcción acumulativa en cinco etapas**: restos del choque → esqueleto → bodega y casco a medio construir → motores y sistemas → nave completa. No reemplazar una nave terminada por otra más pintada: **añadir piezas visibles**.
- Rampa, contenedores, raíles y grúa como assets reales interactuables; materiales en armonía con el resto de Nova Lavinia.

### 4.8 Personajes y ropa

- Mantener las proporciones y rostros estilizados de Nicias y NPC aprobados.
- Ajustar materiales de ropa, cuero, metal y armaduras al mismo tratamiento cromático/roughness que edificios y entorno.
- No cambiar su identidad cultural ni sus siluetas por intentar copiar literalmente rocas sobre ropa.
- Ropa animable: capas, faldones, tiras y bolsas preparadas para huesos secundarios donde corresponda.

## 5. Pipeline obligatorio: Blender/Python → Unity URP

1. **Ficha de asset** (YAML/JSON): tipo, escala real, referencias, bioma/cultura, materiales, módulos, colisiones, LOD, estados constructivos y uso FPS/isométrico.
2. **Blockout**: malla de volúmenes, pivotes, dimensiones y composición. Aprobación de silueta antes del detalle.
3. **Modelado definitivo**: esculpir formas principales; biseles/erosión/estratificación; evitar geometría vacía y detalles arbitrarios.
4. **UV y texturizado**: atlas/materiales compartidos donde proceda; albedo con variación, normal y AO controlados, roughness apropiado.
5. **Exportación**: FBX o glTF; guardar además los `.blend` y scripts `bpy` reproducibles. Escala `1 unidad Unity = 1 metro`, origen y orientación coherentes.
6. **Importación Unity**: prefabs, `MeshRenderer`, URP/Lit o Shader Graph URP, colliders simplificados, LODGroup si aporta valor.
7. **Prueba visual**: asset al lado de una piedra original del pack y una referencia construida, con luces y cámara idénticas; vista FPS e isométrica.
8. **Prueba funcional**: suelo caminable, NavMesh compatible, interiores transitables, partes de obra integrables, sin z-fighting ni geometría rota.
9. **Optimización**: profiler, instancing, batching, atlas, LOD y colliders por necesidad medida; no fijar cifras de polígonos arbitrarias sin prototipo.
10. **Entrega**: archivos, prefabs, capturas, ficha, cambios y problemas pendientes. No afirmar que algo está verificado si no se ejecutó.

### Importante: HDRP vs URP

La ficha oficial del paquete enlazado muestra compatibilidad HDRP y **no compatibilidad URP**. No asumir compatibilidad directa: si el paquete se usa dentro del proyecto, inspeccionar shaders y texturas, reconstruir materiales en URP y verificar comparativas. `BaseMap`, `Normal`, `AO`, `Metallic`, `Smoothness`, `Mask Map` deben conectarse según sus canales efectivos; HDRP y URP no interpretan necesariamente las mismas máscaras igual. Revisar césped/transparencia y lighting en especial. Si la conversión no conserva el aspecto, preferir **assets originales inspirados en la estética** con shaders URP.

## 6. Hoja de ruta por fases (con puertas de aprobación)

### Fase 0 — Congelar el referente visual
- Importar capturas del pack y del protagonista a `Art/References/` (si su uso está permitido por licencia).
- Si se dispone del pack, revisarlo en un proyecto HDRP aislado o una escena de referencia; no contaminar el proyecto URP principal.
- Capturar una escena patrón con piedra, terreno, hierba, luz y cámara; registrar qué cualidades visuales se quieren igualar.
- Aprobar una **imagen de benchmark**, no solo palabras.

### Fase 1 — Prototipo vertical de estilo (6 assets)
Generar: roca mediana, acantilado modular, parche de hierba, tramo de camino, pared de edificio de Nova Lavinia y cajón/maquinaria retrofuturista. Montarlos juntos a escala humana. Revisar estilo en FPS e isométrica. **No generar cien assets todavía.**

### Fase 2 — Librería unificada de materiales
Crear materiales compartidos para piedra, tierra, hierba, madera, yeso, cobre, acero, tejido, cerámica calcidense y cristal/tecnología. Establecer variantes, normal/AO y reglas de desgaste. Preparar atlas y shaders URP probados.

### Fase 3 — Kit de entorno natural
Acantilados, colinas, rocas, guijarros, suelo, caminos, vegetación, árboles, riberas y cuevas. Ensamblar un valle pequeño con caminos y una zona de extracción. Cada módulo probado con terreno y vecinos.

### Fase 4 — Kit de asentamiento
Crear piezas modulares de cimientos, vigas, muros, puertas, techos, escaleras y mobiliario, además de los cuatro estados de obra. Construir primero banco de trabajo, aserradero, almacén y fundición con interiores funcionales.

### Fase 5 — Industria y nave
Crear máquinas, contenedores, raíles, fundición, taller eléctrico, instrumentos y componentes de carga. Construir el carguero y sus cinco etapas **por instalación acumulativa de piezas**. Mantener materiales industriales a la altura visual del entorno.

### Fase 6 — Control global de calidad
Montar una escena con piedra de referencia + terreno + vegetación + casa + máquina + NPC + nave. Probar día/noche, FPS/isométrica, distancia cercana/media/alta y rendimiento real. Corregir incoherencias de color, tamaño, brillo, ruido de textura y acabado.

## 7. Checklist obligatoria de aceptación (sí/no)

- [ ] ¿La silueta es clara desde la vista isométrica?
- [ ] ¿La superficie presenta riqueza de material **sin** volverse fotorealista?
- [ ] ¿Parece del mismo juego que CliffStonePackM?
- [ ] ¿Comparte iluminación, color grading y densidad de detalle con el benchmark?
- [ ] ¿Se integra culturalmente en Calcis o Nova Lavinia sin perder la estética común?
- [ ] ¿No tiene materiales rosados/error ni shaders HDRP incompatibles con URP?
- [ ] ¿Tiene escala, pivote, UV, normals y colliders correctos?
- [ ] ¿Está preparado para FPS e isométrica?
- [ ] ¿Comparte materiales cuando conviene?
- [ ] ¿Incluye capturas comparativas y archivos fuente?
- [ ] Si es un edificio o parte de nave, ¿sus fases se construyen **pieza a pieza**?

Si falla un punto relevante, corregir antes de aprobar y propagar el asset al proyecto.

## 8. Contrato de trabajo para Claude a partir de ahora

**Cada vez que el usuario solicite un asset nuevo para Nostos:**

1. Consultar este documento y las capturas visuales, identificar familia y cultura.
2. Mantener intactas las decisiones de lore y silueta ya acordadas.
3. Describir rápidamente las masas principales, materiales y paleta que compartirán las referencias.
4. Construir primero una versión de prueba y mostrarla al lado del benchmark si tiene capacidad de render.
5. Crear archivos 3D reales con Blender/Python y prefabs Unity, cuando existan esas herramientas; si no, generar código reproducible y explicitar limitaciones.
6. Probar en la misma iluminación y cámaras; no dar por aprobada una forma que solo se ve bien en un render aislado.
7. Entregar los archivos de origen y exportación, ficha técnica, pruebas y notas de integración.
8. No introducir un nuevo estilo, render pipeline o paleta global sin petición explícita.

### Plantilla de ficha mínima

```yaml
name: ENV_Cliff_Medium_A
category: terrain/rock
planet: Lacio
culture: nature
art_reference: CliffStonePackM_Dzeruza
style_bible: NOSTOS_ART_BIBLE.md
scale_m: [3.0, 2.5, 1.8]
mesh_variants: 3
materials: [M_Stone_Lacio, M_Moss_Lacio]
render_pipeline: URP
lods: required_if_beneficial
colliders: simplified
fps_readable: true
isometric_readable: true
construction_states: null
source: Blender_Python
```

## 9. Propiedad intelectual y fidelidad de referencias

La referencia estética puede servir para orientar **assets originales**. No extraer, redistribuir ni incorporar las mallas o texturas del autor a entregables de forma incompatible con los términos de la Unity Asset Store EULA. Si se importa el paquete original, respetar su licencia y su uso permitido dentro del proyecto. Cuando se pida «idéntico», distinguir entre *coincidencia de lenguaje artístico* y *copia exacta de material protegido*.

---

**Principio rector:** No aceptamos un conjunto de piedras bonitas, una casa genérica, un NPC de otro juego y una nave hiperdetallada pegados en la misma escena. **Todo debe pertenecer visualmente al mismo Nostos.**
