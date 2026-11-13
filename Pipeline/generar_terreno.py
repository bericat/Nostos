"""Genera el valle de Nostos como una malla facetada (low-poly) con Blender y numpy.

Uso:  python3 Pipeline/generar_terreno.py

Un valle llano rodeado de montañas en terrazas, con un río que baja de una cascada al
noroeste y sale por un cañón al sur, y sitio para la boca del túnel al noreste.

Escribe en Assets/Nostos/Arte/Terreno:
  valle.fbx            objetos "Suelo" (terreno, con colisión) y "Agua" (río y cascada)
  valle_altura.bytes   alturas en rejilla regular (uint16) para sembrar árboles desde Unity
  valle.json           medidas, y dónde van el túnel y el puente

Las coordenadas de este archivo son las de Unity (X este, Z norte, Y arriba); la conversión
a Blender se hace al crear los vértices. El llano central queda exactamente a altura 0.
"""
import json
import math
import os
import sys

import bpy
import bmesh  # después de bpy: lo aporta el propio Blender
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from generar_kit import uv_de  # noqa: E402

TAMANO = 400.0
CELDAS = 140            # celdas por lado de la malla
RES_ALTURAS = 257       # resolución de la rejilla de alturas que lee Unity
RADIO_LLANO = 44.0      # dentro, altura 0 exacta (zona edificable)
NIVEL_AGUA = -0.35
FONDO_RIO = -0.9
ANGULO_TUNEL = math.radians(40)

RIO = [(-96, 96), (-64, 58), (-52, 12), (-47, -30), (-22, -72), (8, -112), (22, -150), (30, -210)]
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESTINO = os.path.join(RAIZ, "Assets", "Nostos", "Arte", "Terreno")


# ---------------------------------------------------------------- utilidades

def suave(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


def ruido(x, z, escala, semilla):
    """Ruido de valor suave en [0, 1]."""
    x = np.asarray(x, dtype=np.float64) / escala
    z = np.asarray(z, dtype=np.float64) / escala
    xi = np.floor(x).astype(np.int64)
    zi = np.floor(z).astype(np.int64)
    fx = suave(x - xi)
    fz = suave(z - zi)

    def azar(a, b):
        n = (a * 374761393 + b * 668265263 + semilla * 1442695041) & 0xFFFFFFFF
        n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0

    a = azar(xi, zi)
    b = azar(xi + 1, zi)
    c = azar(xi, zi + 1)
    d = azar(xi + 1, zi + 1)
    return (a * (1 - fx) + b * fx) * (1 - fz) + (c * (1 - fx) + d * fx) * fz


def curva(puntos, rondas=3):
    """Suaviza una línea quebrada (Chaikin) sin mover sus extremos."""
    p = np.array(puntos, dtype=np.float64)
    for _ in range(rondas):
        q = [p[0]]
        for a, b in zip(p[:-1], p[1:]):
            q.append(a * 0.75 + b * 0.25)
            q.append(a * 0.25 + b * 0.75)
        q.append(p[-1])
        p = np.array(q)
    return p


def distancia_a_linea(x, z, linea):
    """Distancia de cada punto (x, z) a una línea quebrada."""
    x = np.asarray(x, dtype=np.float64)
    z = np.asarray(z, dtype=np.float64)
    mejor = np.full(x.shape, 1e9)
    for (ax, az), (bx, bz) in zip(linea[:-1], linea[1:]):
        dx, dz = bx - ax, bz - az
        largo2 = dx * dx + dz * dz or 1e-9
        t = np.clip(((x - ax) * dx + (z - az) * dz) / largo2, 0.0, 1.0)
        mejor = np.minimum(mejor, np.hypot(x - (ax + t * dx), z - (az + t * dz)))
    return mejor


def radio_valle(angulo):
    """Hasta dónde llega el llano en cada dirección: más ancho al oeste, donde cruza el río."""
    return 80 + 10 * np.sin(3 * angulo - math.pi / 2) + 6 * np.sin(5 * angulo - math.pi / 2)


CURSO = curva(RIO)
PUNTO_TUNEL = (math.cos(ANGULO_TUNEL) * (float(radio_valle(ANGULO_TUNEL)) + 3.0),
               math.sin(ANGULO_TUNEL) * (float(radio_valle(ANGULO_TUNEL)) + 3.0))
PUNTO_PUENTE = (-51.2, 4.0)
CAMINOS = [
    curva([PUNTO_TUNEL, (46, 34), (20, 8), (4, -4)], 2),
    curva([(4, -4), (-20, -2), (-44, 4), PUNTO_PUENTE, (-60, 4), (-78, -4)], 2),
    curva([(4, -4), (10, -10), (14, -12)], 1),
]


def altura(x, z):
    x = np.asarray(x, dtype=np.float64)
    z = np.asarray(z, dtype=np.float64)
    r = np.hypot(x, z)
    angulo = np.arctan2(z, x)
    m = np.maximum(0.0, r - radio_valle(angulo))

    # Montañas: un primer escalón brusco y después crestas cada vez más altas.
    cresta = 1.0 - np.abs(2.0 * ruido(x, z, 60.0, 3) - 1.0)
    h = 8.0 * suave(m / 6.0) + 20.0 * (1.0 - np.exp(-m / 50.0)) + 24.0 * suave((m - 50.0) / 80.0)
    h += cresta * cresta * 70.0 * suave((m - 6.0) / 70.0)
    h += (ruido(x, z, 24.0, 5) - 0.5) * 12.0 * suave(m / 25.0)

    # Terrazas: rellanos y paredes casi verticales, como en las referencias.
    paso = 8.0
    desfase = ruido(x, z, 48.0, 13) * 1.6
    t = h / paso + desfase
    piso = np.floor(t)
    escalon = (piso + suave((t - piso - 0.3) / 0.28) - desfase) * paso
    fuerza = 0.8 - 0.6 * suave((h - 26.0) / 22.0)
    h = np.where(m > 0, h * (1.0 - fuerza) + np.maximum(escalon, 0.0) * fuerza, h)

    # El llano: plano del todo en el centro y con lomas suaves hacia el borde.
    h += np.maximum(0.0, ruido(x, z, 26.0, 9) - 0.4) * 2.4 * suave((r - RADIO_LLANO) / 22.0) * (1.0 - suave(m / 6.0))

    # El túnel necesita una explanada delante.
    dt = np.hypot(x - PUNTO_TUNEL[0], z - PUNTO_TUNEL[1])
    h = np.where((m <= 0) & (dt < 16), h * suave((dt - 8) / 8.0), h)

    # El río: cauce suave en el llano y cañón al cruzar las montañas.
    d = distancia_a_linea(x, z, CURSO)
    orilla = FONDO_RIO + 0.36 * (np.maximum(d, 3.2) - 3.2)
    orilla = np.where(orilla > 0, 3.2 * (d - 5.7), orilla)
    return np.minimum(h, orilla)


# ---------------------------------------------------------------- malla

def a_blender(x, y, z):
    """Unity (x, y, z) -> Blender, para el exportador con los ejes del kit."""
    return (-x, -z, y)


def color_de(cx, cz, cy, normal_y, normal_x, normal_z):
    r = math.hypot(cx, cz)
    if cy < -0.25 and float(distancia_a_linea(cx, cz, CURSO)) < 6.5:
        return "tierra"
    if normal_y < 0.74:
        return "roca_malva" if (normal_x * 0.6 - normal_z * 0.5 + normal_y * 0.3) > 0.05 else "roca_sombra"
    if cy > 70:
        return "roca_malva"
    if cy < 2.5:
        if min(float(distancia_a_linea(cx, cz, c)) for c in CAMINOS) < 1.7 and r > 3:
            return "arena"
        manchas = float(ruido(cx, cz, 17.0, 21))
        if manchas > 0.66:
            return "hierba_seca"
        return "hierba_oscura" if manchas < 0.22 and r > RADIO_LLANO else "hierba"
    return "hierba_oscura" if float(ruido(cx, cz, 13.0, 23)) > 0.4 else "hoja"


def malla_suelo():
    n = CELDAS + 1
    eje = np.linspace(-TAMANO / 2, TAMANO / 2, n)
    xs, zs = np.meshgrid(eje, eje)
    # Vértices algo desordenados fuera de la zona edificable: evita el aspecto de cuadrícula.
    paso = TAMANO / CELDAS
    fuera = suave((np.hypot(xs, zs) - RADIO_LLANO - 4) / 10.0)
    borde = (np.abs(xs) < TAMANO / 2 - 1) & (np.abs(zs) < TAMANO / 2 - 1)
    xs = xs + (ruido(xs, zs, 3.1, 31) - 0.5) * paso * 0.55 * fuera * borde
    zs = zs + (ruido(xs, zs, 3.3, 37) - 0.5) * paso * 0.55 * fuera * borde
    ys = altura(xs, zs)

    bm = bmesh.new()
    capa = bm.loops.layers.uv.new("UVMap")
    vertices = [[bm.verts.new(a_blender(xs[j, i], ys[j, i], zs[j, i])) for i in range(n)] for j in range(n)]
    for j in range(CELDAS):
        for i in range(CELDAS):
            a, b, c, d = (j, i), (j, i + 1), (j + 1, i + 1), (j + 1, i)
            tris = ((a, b, c), (a, c, d)) if (i + j) % 2 == 0 else ((a, b, d), (b, c, d))
            for tri in tris:
                p = [np.array((xs[k], ys[k], zs[k])) for k in tri]
                normal = np.cross(p[2] - p[0], p[1] - p[0])
                normal = normal / (np.linalg.norm(normal) or 1.0)
                centro = (p[0] + p[1] + p[2]) / 3.0
                color = color_de(centro[0], centro[2], centro[1], normal[1], normal[0], normal[2])
                # En Unity las caras miran hacia arriba con este orden de vértices.
                cara = bm.faces.new([vertices[k[0]][k[1]] for k in (tri[0], tri[2], tri[1])])
                cara.smooth = False
                uv = uv_de(color)
                for bucle in cara.loops:
                    bucle[capa].uv = uv
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    malla = bpy.data.meshes.new("Suelo")
    bm.to_mesh(malla)
    bm.free()
    return malla


def cinta(bm, capa, centros, medio_ancho, alturas, color):
    """Una cinta de caras a lo largo de una línea: el río o la cascada."""
    uv = uv_de(color)
    lados = []
    for i, (cx, cz) in enumerate(centros):
        ax, az = centros[max(i - 1, 0)]
        bx, bz = centros[min(i + 1, len(centros) - 1)]
        dx, dz = bx - ax, bz - az
        largo = math.hypot(dx, dz) or 1.0
        nx, nz = -dz / largo * medio_ancho, dx / largo * medio_ancho
        y = alturas[i]
        lados.append((bm.verts.new(a_blender(cx + nx, y, cz + nz)), bm.verts.new(a_blender(cx - nx, y, cz - nz))))
    for (a, b), (c, d) in zip(lados[:-1], lados[1:]):
        for tri in ((a, b, d), (a, d, c)):
            cara = bm.faces.new(tri)
            cara.smooth = False
            for bucle in cara.loops:
                bucle[capa].uv = uv


def malla_agua():
    bm = bmesh.new()
    capa = bm.loops.layers.uv.new("UVMap")
    curso = [tuple(p) for p in CURSO]
    cinta(bm, capa, curso, 5.3, [NIVEL_AGUA] * len(curso), "agua")

    # Cascada: sube por la pared del fondo del cañón, pegada a la roca.
    (x0, z0), (x1, z1) = curso[0], curso[1]
    largo = math.hypot(x0 - x1, z0 - z1)
    ux, uz = (x0 - x1) / largo, (z0 - z1) / largo
    tramo, alturas = [], []
    for s in np.arange(3.5, 26.0, 1.5):
        px, pz = x0 + ux * s, z0 + uz * s
        y = float(altura(px, pz))
        tramo.append((px - ux * 0.5, pz - uz * 0.5))
        alturas.append(max(y, NIVEL_AGUA) + 0.3)
        if len(alturas) > 3 and alturas[-1] - alturas[-2] < 0.4 and alturas[-1] > 12:
            break
    cinta(bm, capa, tramo, 2.6, alturas, "espuma")
    cinta(bm, capa, [(x0 + ux * s, z0 + uz * s) for s in (-1.0, 1.5, 4.0)], 4.0, [NIVEL_AGUA + 0.06] * 3, "espuma")

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    malla = bpy.data.meshes.new("Agua")
    bm.to_mesh(malla)
    bm.free()
    # Las cintas son planas: que miren hacia arriba (en Blender, +Z).
    for cara in malla.polygons:
        if cara.normal.z < 0:
            cara.flip()
    return malla, alturas[-1]


def exportar(mallas):
    for objeto in list(bpy.data.objects):
        bpy.data.objects.remove(objeto, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for malla in mallas:
        objeto = bpy.data.objects.new(malla.name, malla)
        bpy.context.scene.collection.objects.link(objeto)
        objeto.select_set(True)
        bpy.context.view_layer.objects.active = objeto
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(DESTINO, "valle.fbx"),
        use_selection=True,
        object_types={"MESH"},
        mesh_smooth_type="FACE",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
    )


def main():
    os.makedirs(DESTINO, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    suelo = malla_suelo()
    agua, alto_cascada = malla_agua()
    exportar([suelo, agua])

    eje = np.linspace(-TAMANO / 2, TAMANO / 2, RES_ALTURAS)
    xs, zs = np.meshgrid(eje, eje)
    alturas = altura(xs, zs)
    minimo, maximo = float(alturas.min()), float(alturas.max())
    codificado = np.round((alturas - minimo) / (maximo - minimo) * 65535).astype("<u2")
    with open(os.path.join(DESTINO, "valle_altura.bytes"), "wb") as archivo:
        archivo.write(codificado.tobytes())

    info = {
        "resolucion": RES_ALTURAS,
        "tamano": TAMANO,
        "alturaMinima": minimo,
        "alturaMaxima": maximo,
        "radioLlano": RADIO_LLANO,
        "tunelX": PUNTO_TUNEL[0],
        "tunelZ": PUNTO_TUNEL[1],
        "puenteX": PUNTO_PUENTE[0],
        "puenteZ": PUNTO_PUENTE[1],
        "cascadaX": float(CURSO[0][0]),
        "cascadaZ": float(CURSO[0][1]),
    }
    with open(os.path.join(DESTINO, "valle.json"), "w", encoding="utf-8") as archivo:
        json.dump(info, archivo, indent=2)

    print(f"Valle: {len(suelo.polygons)} triángulos de suelo, {len(agua.polygons)} de agua, "
          f"alturas de {minimo:.1f} a {maximo:.1f} m, cascada de {alto_cascada:.0f} m. En {DESTINO}")


if __name__ == "__main__":
    main()
