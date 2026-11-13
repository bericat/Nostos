"""Miniaturas para la interfaz: cada edificio terminado visto desde arriba en diagonal, el
camino, y el retrato de Nicias. Fondo transparente con una sombra suave en el suelo.

Uso:  python3 Pipeline/generar_miniaturas.py [muestras]

Salida en Assets/Nostos/Resources/UI/Edificios/<id>.png (256 px) y
Assets/Nostos/Resources/UI/Retratos/nicias.png (256 px). Necesita bpy (Blender como módulo).
"""
import math
import os
import sys

import bpy
from mathutils import Vector

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ARTE = os.path.join(RAIZ, "Assets", "Nostos", "Arte")
UI = os.path.join(RAIZ, "Assets", "Nostos", "Resources", "UI")
EDIFICIOS = ["refugio", "banco", "aserradero", "fundicion", "almacen", "casa", "comedor", "pozo",
             "taller", "optica", "astillero", "teleferico"]
LADO = 256
MUESTRAS = int(sys.argv[1]) if len(sys.argv) > 1 else 48


def limpiar():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene
    s.render.engine = 'CYCLES'
    s.cycles.samples = MUESTRAS
    s.cycles.device = 'CPU'
    s.cycles.use_denoising = True
    s.render.film_transparent = True
    s.view_settings.view_transform = 'Standard'
    s.render.resolution_x = LADO
    s.render.resolution_y = LADO
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    w = bpy.data.worlds.new("w")
    s.world = w
    w.use_nodes = True
    fondo = w.node_tree.nodes["Background"]
    fondo.inputs[0].default_value = (0.62, 0.66, 0.78, 1)
    fondo.inputs[1].default_value = 0.9
    return s


def material_paleta():
    mat = bpy.data.materials.new("Paleta")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(ARTE, "Kit", "Paleta.png"))
    tex.interpolation = 'Closest'
    nt.links.new(tex.outputs[0], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def importar(ruta, material=None):
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=ruta)
    nuevos = [o for o in bpy.data.objects if o not in antes and o.type == 'MESH']
    if material is not None:
        for o in nuevos:
            o.data.materials.clear()
            o.data.materials.append(material)
    return nuevos


def caja(objetos):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objetos:
        for v in o.bound_box:
            p = o.matrix_world @ Vector(v)
            lo = Vector((min(lo.x, p.x), min(lo.y, p.y), min(lo.z, p.z)))
            hi = Vector((max(hi.x, p.x), max(hi.y, p.y), max(hi.z, p.z)))
    return lo, hi


def suelo_sombra(lado):
    bpy.ops.mesh.primitive_plane_add(size=lado, location=(0, 0, 0))
    plano = bpy.context.object
    plano.is_shadow_catcher = True
    return plano


def luces(s):
    sol = bpy.data.objects.new("sol", bpy.data.lights.new("sol", 'SUN'))
    sol.data.energy = 4.2
    sol.data.color = (1.0, 0.93, 0.82)
    sol.data.angle = math.radians(6)
    s.collection.objects.link(sol)
    az, el = math.radians(200), math.radians(55)
    hacia = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el)))
    sol.rotation_euler = (-hacia).to_track_quat('-Z', 'Y').to_euler()
    return sol


def camara(s, mira, direccion, escala_orto=None, lente=None, distancia=40.0):
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    s.collection.objects.link(cam)
    s.camera = cam
    d = Vector(direccion).normalized()
    cam.location = Vector(mira) + d * distancia
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    cam.data.clip_start = 0.05
    cam.data.clip_end = 400
    if escala_orto is not None:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = escala_orto
    else:
        cam.data.lens = lente
    return cam


# Se quita el tejado para que se vea lo de dentro, que es lo que distingue un edificio de otro.
CON_TEJADO = {"casa", "refugio", "almacen"}


def encuadrar(s, cam, objetos, margen=1.08):
    """Ajusta la cámara ortográfica para que el modelo llene el cuadro, centrado."""
    from bpy_extras.object_utils import world_to_camera_view
    bpy.context.view_layer.update()
    puntos = []
    for o in objetos:
        for v in o.data.vertices:
            puntos.append(o.matrix_world @ v.co)
    for _ in range(2):
        bpy.context.view_layer.update()
        uv = [world_to_camera_view(s, cam, p) for p in puntos]
        u0, u1 = min(p.x for p in uv), max(p.x for p in uv)
        v0, v1 = min(p.y for p in uv), max(p.y for p in uv)
        escala = cam.data.ortho_scale
        derecha = cam.matrix_world.to_quaternion() @ Vector((1, 0, 0))
        arriba = cam.matrix_world.to_quaternion() @ Vector((0, 1, 0))
        cam.location += derecha * ((u0 + u1) * 0.5 - 0.5) * escala + arriba * ((v0 + v1) * 0.5 - 0.5) * escala
        cam.data.ortho_scale = escala * max(u1 - u0, v1 - v0) * margen


def miniatura(nombre, rutas, salida):
    s = limpiar()
    mat = material_paleta()
    objetos = []
    for ruta, desplazamiento in rutas:
        nuevos = importar(ruta, mat)
        for o in nuevos:
            o.location += Vector(desplazamiento)
        objetos += nuevos
    if nombre not in CON_TEJADO:
        for o in list(objetos):
            if o.name.startswith("Tejado"):
                objetos.remove(o)
                bpy.data.objects.remove(o)
    bpy.context.view_layer.update()
    lo, hi = caja(objetos)
    centro = (lo + hi) * 0.5
    tam = hi - lo
    # La puerta mira a +Y: se ve de frente y algo de lado, desde arriba.
    direccion = (0.62, 1.0, 1.5)
    radio = max(tam.x, tam.y, tam.z * 1.3)
    cam = camara(s, (centro.x, centro.y, centro.z * 0.6), direccion, escala_orto=radio * 1.12)
    encuadrar(s, cam, objetos)
    suelo_sombra(radio * 6)
    luces(s)
    s.render.filepath = salida
    bpy.ops.render.render(write_still=True)
    print("miniatura", nombre, "->", salida)


def retrato(salida):
    s = limpiar()
    s.render.film_transparent = True
    objetos = importar(os.path.join(RAIZ, "Assets", "Nostos", "Personajes", "Nicias", "nicias.fbx"))
    mat = bpy.data.materials.new("Nicias")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(RAIZ, "Assets", "Nostos", "Personajes", "Nicias", "nicias_color.png"))
    nt.links.new(tex.outputs[0], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.75
    for o in objetos:
        o.data.materials.clear()
        o.data.materials.append(mat)
    bpy.context.view_layer.update()
    lo, hi = caja(objetos)
    alto = hi.z - lo.z
    cabeza = Vector(((lo.x + hi.x) * 0.5, (lo.y + hi.y) * 0.5, lo.z + alto * 0.9))
    # En Blender Nicias mira hacia -Y (en Unity, +Z). Retrato de tres cuartos.
    camara(s, cabeza, (0.38, -1.0, 0.08), lente=85, distancia=1.9)
    sol = luces(s)
    az, el = math.radians(-60), math.radians(35)
    hacia = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el)))
    sol.rotation_euler = (-hacia).to_track_quat('-Z', 'Y').to_euler()
    relleno = bpy.data.objects.new("relleno", bpy.data.lights.new("relleno", 'AREA'))
    relleno.data.energy = 60
    relleno.data.size = 2.0
    relleno.data.color = (0.75, 0.82, 1.0)
    relleno.location = cabeza + Vector((-1.4, -1.2, 0.4))
    relleno.rotation_euler = (cabeza - relleno.location).to_track_quat('-Z', 'Y').to_euler()
    s.collection.objects.link(relleno)
    s.render.filepath = salida
    bpy.ops.render.render(write_still=True)
    print("retrato ->", salida)


def mapa(salida, lado=1024):
    """El valle desde arriba, con el norte (+Z de Unity) hacia arriba, para el minimapa.
    Cubre los 400 m del terreno: el píxel (u, v) es x = -200 + 400 u, z = -200 + 400 v."""
    s = limpiar()
    s.render.film_transparent = False
    s.render.resolution_x = lado
    s.render.resolution_y = lado
    s.cycles.samples = 16
    importar(os.path.join(ARTE, "Terreno", "valle.fbx"), material_paleta())
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    s.collection.objects.link(cam)
    s.camera = cam
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 400
    cam.data.clip_end = 1000
    cam.location = (0, 0, 400)
    # En Blender, x = -x de Unity e y = -z de Unity: girada media vuelta, arriba queda +Z de Unity.
    cam.rotation_euler = (0, 0, math.pi)
    sol = luces(s)
    az, el = math.radians(120), math.radians(55)
    hacia = Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el)))
    sol.rotation_euler = (-hacia).to_track_quat('-Z', 'Y').to_euler()
    s.render.filepath = salida
    bpy.ops.render.render(write_still=True)
    print("mapa ->", salida)


def principal():
    carpeta = os.path.join(UI, "Edificios")
    os.makedirs(carpeta, exist_ok=True)
    os.makedirs(os.path.join(UI, "Retratos"), exist_ok=True)
    solo = os.environ.get("SOLO")
    for nombre in EDIFICIOS:
        if solo and nombre not in solo.split(","):
            continue
        miniatura(nombre, [(os.path.join(ARTE, "Edificios", nombre + "_4.fbx"), (0, 0, 0))],
                  os.path.join(carpeta, nombre + ".png"))
    if not solo or "camino" in solo:
        tramo = []
        for i, (x, y) in enumerate([(0, -2), (0, 0), (0, 2), (2, 2)]):
            tramo.append((os.path.join(ARTE, "Naturaleza", "camino_" + "ab"[i % 2] + ".fbx"), (x, y, 0)))
        miniatura("camino", tramo, os.path.join(carpeta, "camino.png"))
    if not solo or "mapa" in solo:
        os.makedirs(os.path.join(UI, "Mapa"), exist_ok=True)
        mapa(os.path.join(UI, "Mapa", "valle.png"))
    if not solo or "nicias" in solo:
        retrato(os.path.join(UI, "Retratos", "nicias.png"))


principal()
