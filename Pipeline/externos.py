"""Modelos de paquetes de la tienda pasados a la paleta del juego.

Lee un FBX de un paquete (pinos de "Pine Forest - LowPoly", mesa y platos de "Lowpoly Dinner
Table", barril de "Lowpoly Baker's House"), toma el color de cada cara (de la textura o de los
colores por vértice) y lo cambia por el color de la paleta más parecido de una lista. El
resultado es una forma "externa" que generar_kit sabe meter en sus mallas, así que sale con el
mismo material de paleta, colisionador y sombreado plano que el resto del kit.

Uso:
    forma = externos.forma(FBX, "Evergreen_Med", escala=12, colores=VERDES_Y_TRONCO)
"""
import math
import os

import bpy
from mathutils import Vector

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(RAIZ, "Assets")

PINOS = os.path.join(ASSETS, "AssetPacks", "Pine Forest_LowPoly", "Mesh", "Pine Forest_LowPoly.fbx")
MESA = os.path.join(ASSETS, "Dinner table")
PANADERO = os.path.join(ASSETS, "LowpolyBakersHouse")

_cache = {}
_texturas = {}


def _rgb_paleta():
    from generar_kit import PALETA
    return dict(PALETA)


def _textura(ruta):
    """Píxeles de una textura como lista de filas (PIL), con la v hacia arriba como en Blender."""
    if ruta not in _texturas:
        from PIL import Image
        im = Image.open(ruta).convert("RGB")
        im.thumbnail((512, 512))
        _texturas[ruta] = im
    return _texturas[ruta]


def _muestra(im, u, v):
    w, h = im.size
    x = min(w - 1, max(0, int((u % 1.0) * w)))
    y = min(h - 1, max(0, int((1.0 - (v % 1.0)) * h)))
    return im.getpixel((x, y))


def _leer(fbx):
    """Importa el FBX una vez y guarda, por objeto, vértices (en metros, mundo), caras, UV y colores."""
    if fbx in _cache:
        return _cache[fbx]
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    nuevos = [o for o in bpy.data.objects if o not in antes]
    datos = {}
    for o in nuevos:
        if o.type != "MESH":
            continue
        me = o.data
        M = o.matrix_world
        verts = [tuple(M @ v.co) for v in me.vertices]
        uv = me.uv_layers.active.data if me.uv_layers else None
        col = me.color_attributes[0] if me.color_attributes else None
        caras = []
        for p in me.polygons:
            uvs = [tuple(uv[li].uv) for li in p.loop_indices] if uv else None
            vc = None
            if col is not None:
                cs = []
                for li in p.loop_indices:
                    d = col.data[li] if col.domain == "CORNER" else col.data[me.loops[li].vertex_index]
                    cs.append(tuple(d.color_srgb))
                vc = tuple(sum(c[i] for c in cs) / len(cs) for i in range(4))
            caras.append((tuple(p.vertices), uvs, vc))
        datos[o.name] = (verts, caras)
    for o in nuevos:
        bpy.data.objects.remove(o, do_unlink=True)
    _cache[fbx] = datos
    return datos


def _distancia(a, b):
    # RGB con peso perceptivo sencillo; basta para elegir entre una docena de colores.
    r = (a[0] + b[0]) / 2
    dr, dg, db = a[0] - b[0], a[1] - b[1], a[2] - b[2]
    return (2 + r / 256) * dr * dr + 4 * dg * dg + (2 + (255 - r) / 256) * db * db


def mas_cercano(rgb, colores):
    pal = _rgb_paleta()
    return min(colores, key=lambda n: _distancia(rgb, pal[n]))


def _tapas(caras):
    """Caras que cierran los huecos de la malla (bordes que solo tocan una cara)."""
    usadas = {}
    for c in caras:
        for i in range(len(c)):
            a, b = c[i], c[(i + 1) % len(c)]
            usadas[(a, b)] = usadas.get((a, b), 0) + 1
    siguiente = {}
    for (a, b) in usadas:
        if (b, a) not in usadas:
            siguiente.setdefault(b, []).append(a)   # la tapa recorre el borde al revés
    tapas = []
    while siguiente:
        inicio = next(iter(siguiente))
        lazo, v = [], inicio
        while v in siguiente and siguiente[v]:
            lazo.append(v)
            n = siguiente[v].pop()
            if not siguiente[v]:
                del siguiente[v]
            v = n
            if v == inicio:
                break
        if v == inicio and len(lazo) >= 3:
            tapas.append(tuple(lazo))
    return tapas


def _muestras_dentro(im, uvs):
    """Muestras de la textura repartidas por la cara (abanico de triángulos, 6 puntos en cada uno)."""
    pesos = [(1 / 3, 1 / 3, 1 / 3), (0.6, 0.2, 0.2), (0.2, 0.6, 0.2), (0.2, 0.2, 0.6),
             (0.45, 0.45, 0.1), (0.1, 0.45, 0.45)]
    a = uvs[0]
    for i in range(1, len(uvs) - 1):
        b, c = uvs[i], uvs[i + 1]
        for wa, wb, wc in pesos:
            yield _muestra(im, wa * a[0] + wb * b[0] + wc * c[0], wa * a[1] + wb * b[1] + wc * c[1])


def _igualar_parejas(verts, caras, colores, votos):
    """Los cuadrados que el paquete partió en dos triángulos llevan un solo color: cada triángulo
    se empareja con el vecino de su lado más largo si están en el mismo plano, y los dos se quedan
    con lo que más salga sumando sus muestras."""
    def normal(c):
        a, b, d = (Vector(verts[i]) for i in c[:3])
        n = (b - a).cross(d - a)
        return n.normalized() if n.length > 1e-12 else n

    def lado_largo(c):
        if len(c) != 3:
            return None
        lados = [(c[i], c[(i + 1) % 3]) for i in range(3)]
        a, b = max(lados, key=lambda e: (Vector(verts[e[0]]) - Vector(verts[e[1]])).length)
        return (min(a, b), max(a, b))

    por_lado = {}
    for k, c in enumerate(caras):
        e = lado_largo(c)
        if e is not None and votos[k] is not None:
            por_lado.setdefault(e, []).append(k)
    for ks in por_lado.values():
        if len(ks) != 2:
            continue
        a, b = ks
        if normal(caras[a]).dot(normal(caras[b])) < 0.999:
            continue
        suma = dict(votos[a])
        for n, v in votos[b].items():
            suma[n] = suma.get(n, 0) + v
        colores[a] = colores[b] = max(suma, key=suma.get)


def forma(fbx, objeto, escala=1.0, colores=None, textura=None, elegir=None, alfa_min=None,
          centro=(0, 0, 0), rot=(0, 0, 0), apoyar=True, centrar=True, tapar=False, votar=False):
    """Forma "externa" con la malla `objeto` del FBX.

    - `textura`: ruta de la imagen de la que sacar el color de cada cara (por sus UV); si no, se
      usan los colores por vértice.
    - `elegir(rgb, alfa, z)`: función que devuelve el nombre de color de la paleta (o None para
      quitar la cara); si no, el más parecido de `colores`.
    - `alfa_min`: con colores por vértice, las caras con menos alfa se quitan (eran recortes).
    - La malla se lleva al origen: centrada en planta y con la base en z = 0 si `apoyar`.
    - `tapar`: cierra los extremos abiertos (troncos y ramas que en el paquete son tubos huecos).
    - `votar`: con textura, cada cara toma unas cuantas muestras repartidas por dentro y se queda
      con el color que más sale (mejor para caras grandes que cruzan zonas de la textura).
    """
    verts, caras = _leer(fbx)[objeto]
    xs = [v[0] for v in verts]
    ys = [v[1] for v in verts]
    zs = [v[2] for v in verts]
    cx, cy = ((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2) if centrar else (0.0, 0.0)
    z0 = min(zs) if apoyar else 0.0
    im = _textura(textura) if textura else None

    salida_v = [((x - cx) * escala, (y - cy) * escala, (z - z0) * escala) for x, y, z in verts]
    salida_c, salida_col, todos_votos = [], [], []
    for indices, uvs, vc in caras:
        alfa = 1.0
        if im is not None and uvs:
            pts = list(uvs) + [(sum(u for u, _ in uvs) / len(uvs), sum(v for _, v in uvs) / len(uvs))]
            muestras = [_muestra(im, u, v) for u, v in pts]
            rgb = tuple(sorted(m[i] for m in muestras)[len(muestras) // 2] for i in range(3))
        elif vc is not None:
            rgb = tuple(round(c * 255) for c in vc[:3])
            alfa = vc[3]
        else:
            rgb = (128, 128, 128)
        if alfa_min is not None and alfa < alfa_min:
            continue
        zc = sum(salida_v[i][2] for i in indices) / len(indices)
        if votar and im is not None and uvs:
            votos = {}
            for rgb_m in _muestras_dentro(im, uvs):
                n = elegir(rgb_m, alfa, zc) if elegir else mas_cercano(rgb_m, colores)
                votos[n] = votos.get(n, 0) + 1
            todos_votos.append(votos)
            nombre = max(votos, key=votos.get)
        else:
            todos_votos.append(None)
            nombre = elegir(rgb, alfa, zc) if elegir else mas_cercano(rgb, colores)
        if nombre is None:
            todos_votos.pop()
            continue
        salida_c.append(indices)
        salida_col.append(nombre)
    if votar:
        _igualar_parejas(salida_v, salida_c, salida_col, todos_votos)
    if tapar:
        for t in _tapas(salida_c):
            salida_c.append(t)
            salida_col.append("madera3")   # corte de la madera, más claro
    return {"tipo": "externa", "verts": salida_v, "caras": salida_c, "colores": salida_col,
            "centro": centro, "rot": rot, "color": salida_col[0] if salida_col else "roca"}


def medidas(f):
    """(ancho x, fondo y, alto z) de una forma externa ya escalada."""
    vs = f["verts"]
    return tuple(max(v[i] for v in vs) - min(v[i] for v in vs) for i in range(3))
