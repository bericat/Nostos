# Instrucciones permanentes para Claude — Proyecto Nostos

## Dirección artística obligatoria

Antes de generar, modificar o integrar **cualquier asset 3D, terreno, vegetación, edificio, máquina, personaje, prop, nave, escenario o material**, lee **`NOSTOS_ART_BIBLE.md`** completa y úsala como fuente de verdad artística.

**Referencia principal:** `CliffStonePackM` de Dzeruza (Unity Asset Store): https://assetstore.unity.com/packages/3d/props/exterior/cliffstonepackm-327619 . Comparar con las capturas guardadas en `Art/References/` cuando existan. No confundir con otros packs de rocas.

**Resumen:** Mundo 3D estilizado/semiestilizado: rocas esculpidas con estratos suaves, superficie mineral texturizada, tierras cálidas, vegetación viva, siluetas legibles, materiales PBR moderados, nivel de detalle coherente, iluminación natural y retrofuturismo artesanal en las construcciones. No usar low-poly plano sin textura, fotorrealismo ni cyberpunk.

**Lore:** Calcis es tecnología precisa blanca/azul. Nova Lavinia reutiliza piedra, madera, cobre y textiles. La nave de Nicias es un carguero de un piloto con una gran bodega; sus cinco etapas reconstruyen físicamente una nave destruida añadiendo piezas, no mediante cambio de pintura.

**Tecnología:** Proyecto principal en Unity URP, con modelos procedentes de Blender/Python `bpy`. El pack de referencia aparece listado como HDRP-only; no asumir compatibilidad directa de sus shaders o materiales en URP. El trabajo debe ser modular, reproducible y comprobable en FPS e isométrica.

**Reglas de entrega:** Mantener nombres/escala/pivotes/UV/normales/materiales/prefabs/colliders; usar atlas y LOD cuando corresponda; validar cada asset en una escena benchmark junto a la referencia. No afirmar haber creado o probado archivos si no están disponibles.

**Ante una nueva petición:** consultar la biblia → clasificar asset → bloquear silueta → modelar/texturizar → importar URP → comparar en escena benchmark → corregir → entregar.
