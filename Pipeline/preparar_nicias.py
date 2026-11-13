"""Prepara el modelo de Nicias (exportado por Meshy) para Unity.

Uso:  python3 Pipeline/preparar_nicias.py <carpeta con el FBX y las texturas de Meshy>

Qué hace:
  - Pone el origen en los pies y ajusta la altura a 1,78 m.
  - Exporta Assets/Nostos/Personajes/Nicias/nicias.fbx con los mismos ejes que el kit
    (en Unity, Nicias mira hacia +Z).
  - Copia el color y las normales, y empaqueta metal y suavidad en una sola textura
    (rojo = metal, alfa = 1 - rugosidad), que es como lo lee el shader Lit de URP.

Necesita bpy (Blender como módulo) y Pillow. El modelo no trae esqueleto ni animaciones.
"""
import glob
import os
import sys

import bpy
from mathutils import Vector
from PIL import Image

ALTURA = 1.78
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESTINO = os.path.join(RAIZ, "Assets", "Nostos", "Personajes", "Nicias")


def uno(carpeta, patron):
    rutas = sorted(glob.glob(os.path.join(carpeta, patron)))
    if not rutas:
        raise SystemExit(f"No se encuentra {patron} en {carpeta}")
    return rutas[0]


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    origen = sys.argv[-1]
    os.makedirs(DESTINO, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=uno(origen, "*_tri.fbx"))
    mallas = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    objeto = mallas[0]
    objeto.name = "Nicias"
    objeto.data.name = "Nicias"

    # Transformación al mundo, pies en z = 0 y altura fija.
    malla = objeto.data
    malla.transform(objeto.matrix_world)
    objeto.matrix_world.identity()
    zs = [v.co.z for v in malla.vertices]
    xs = [v.co.x for v in malla.vertices]
    ys = [v.co.y for v in malla.vertices]
    escala = ALTURA / (max(zs) - min(zs))
    centro = Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, min(zs)))
    for v in malla.vertices:
        v.co = (v.co - centro) * escala
    malla.update()
    malla.materials.clear()

    bpy.ops.object.select_all(action="DESELECT")
    objeto.select_set(True)
    bpy.context.view_layer.objects.active = objeto
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(DESTINO, "nicias.fbx"),
        use_selection=True,
        object_types={"MESH"},
        mesh_smooth_type="OFF",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
    )

    Image.open(uno(origen, "*_tri.png")).convert("RGB").save(os.path.join(DESTINO, "nicias_color.png"), optimize=True)
    Image.open(uno(origen, "*_tri_normal.png")).convert("RGB").save(os.path.join(DESTINO, "nicias_normal.png"), optimize=True)
    metal = Image.open(uno(origen, "*_tri_metallic.png")).convert("L")
    rugosidad = Image.open(uno(origen, "*_tri_roughness.png")).convert("L")
    suavidad = rugosidad.point(lambda p: 255 - p)
    Image.merge("RGBA", (metal, metal, metal, suavidad)).save(os.path.join(DESTINO, "nicias_metal.png"), optimize=True)

    print(f"Nicias: {len(malla.vertices)} vértices, {len(malla.polygons)} caras, {ALTURA} m. En {DESTINO}")


if __name__ == "__main__":
    main()
