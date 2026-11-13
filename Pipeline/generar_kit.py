"""Nostos: genera los modelos del juego y los exporta a FBX para Unity.

Uso (cualquiera de los dos):
    blender --background --python Pipeline/generar_kit.py
    python Pipeline/generar_kit.py            (con `pip install bpy pillow`)

Opciones (detrás de `--` si se lanza con blender):
    --salida RUTA    carpeta Arte de destino (por defecto Assets/Nostos/Arte)
    --solo NOMBRE    genera solo ese modelo (se puede repetir)

Qué genera, dentro de Assets/Nostos/Arte:
    Kit/         muro, suelo, viga y Paleta.png
    Edificios/   refugio, aserradero, fundicion, almacen, casa, comedor, pozo, taller y optica, cada uno en sus 4 estados de obra
                 (_1 cimientos, _2 esqueleto, _3 casi terminado, _4 terminado)
    Naturaleza/  arbol_a, arbol_b, veta, arbusto, hoguera, cobre, cristal
    Nave/        casco (con los cinco módulos, Modulo1 a Modulo5, como objetos aparte)

Un modelo es un diccionario {nombre_objeto: [formas]}. Cada forma (caja, cilindro, cono
o roca) lleva un color de la paleta: todas sus caras apuntan en UV al centro de su muestra
en Paleta.png, así todo comparte un único material. Unidades en metros, Z arriba (Blender);
el origen de cada modelo está en el centro de su base. La celda de la rejilla mide 2 m.

Paleta.png tiene una fila por color y ocho columnas de variantes de tono. Las piezas detalladas
(sillares, tejas, ventanas, árboles, ruinas...) están en detalle.py y detalle_mundo.py.

Los cuatro estados de obra de un edificio salen de las mismas capas (losa, estructura,
paredes, tejado, máquina), quitando capas: por eso siempre encajan entre sí.
"""

import argparse
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Euler, Matrix, Vector  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import detalle  # noqa: E402
from detalle_interiores import maquina_casa, maquina_casa_dos_plantas, maquina_banco  # noqa: E402
from detalle_mundo import arbol, pino, penasco, arbusto, tunel, ruina_arco, columna_rota, obelisco, puente, camino  # noqa: E402
from detalle import LOSA, ALTO, PILAR, PARED, PUERTA_ANCHO, PUERTA_ALTO, MARGEN, VARIANTE_BASE  # noqa: E402

CELDA = 2.0
ALTURA_MURO = 3.0
BISEL = 0.015

# Paleta compartida: rejilla de 4 x 4 muestras. Filas 0-1 colonos, fila 2 Calcis, fila 3 naturaleza y acentos.
# Una fila por color y ocho variantes de tono por fila (la columna VARIANTE_BASE es el color exacto).
COLUMNAS = 8
PIXELES_MUESTRA = 8
VARIANTES = [0.84, 0.90, 0.95, 1.0, 1.05, 1.10, (1.06, 1.0, 0.92), (0.94, 0.99, 1.07)]
PALETA = [
    ("ocre_claro", (214, 170, 98)),
    ("ocre_oscuro", (168, 118, 62)),
    ("madera_clara", (176, 132, 86)),
    ("madera_oscura", (104, 72, 48)),
    ("cobre", (190, 112, 72)),
    ("cobre_verdin", (98, 152, 132)),
    ("tela_cruda", (226, 212, 184)),
    ("tierra", (132, 104, 78)),
    ("calcis_blanco", (232, 238, 242)),
    ("calcis_gris", (168, 182, 194)),
    ("calcis_metal", (126, 142, 158)),
    ("calcis_azul", (86, 156, 214)),
    ("musgo", (104, 134, 84)),
    ("roca", (128, 124, 118)),
    ("fuego", (255, 150, 60)),
    ("cristal", (150, 230, 220)),
    # Filas 4-6: el valle (hierba, roca malva de las montañas, agua).
    ("hierba", (150, 160, 74)),
    ("hierba_seca", (184, 172, 92)),
    ("hierba_oscura", (106, 132, 66)),
    ("hoja", (120, 150, 72)),
    ("hoja_clara", (150, 170, 84)),
    ("pino", (60, 100, 76)),
    ("roca_malva", (160, 142, 150)),
    ("roca_sombra", (116, 102, 122)),
    ("arena", (200, 168, 114)),
    ("agua", (74, 134, 194)),
    ("espuma", (226, 240, 246)),
    ("sombra", (30, 26, 36)),
    # Piezas detalladas: yeso, sillería, teja, madera, azules del asentamiento y vegetación.
    ("yeso", (232, 218, 190)), ("yeso2", (220, 202, 170)), ("yeso3", (240, 228, 204)),
    ("piedra", (176, 168, 160)), ("piedra2", (150, 144, 142)), ("piedra3", (196, 186, 172)), ("piedra4", (132, 124, 132)),
    ("teja", (196, 96, 62)), ("teja2", (214, 118, 70)), ("teja3", (172, 78, 56)), ("teja4", (226, 140, 84)), ("teja5", (150, 70, 60)),
    ("madera", (122, 82, 52)), ("madera2", (98, 64, 42)), ("madera3", (150, 106, 66)),
    ("azul", (58, 96, 178)), ("azul2", (44, 76, 150)), ("azul3", (84, 124, 200)), ("blanco", (236, 236, 228)),
    ("vidrio", (40, 52, 84)), ("luz", (255, 206, 120)), ("hierro", (60, 58, 66)),
    ("hoja_oscura", (80, 118, 66)), ("flor", (226, 190, 70)), ("flor2", (206, 84, 80)),
    ("ladrillo", (170, 104, 80)), ("ladrillo2", (150, 90, 72)), ("tierra_clara", (176, 142, 100)),
    ("pino_claro", (84, 124, 84)), ("pino_oscuro", (46, 82, 68)), ("tronco", (112, 84, 62)),
    ("malva_claro", (184, 166, 170)), ("malva_oscuro", (92, 80, 104)),
]
FILAS = len(PALETA)
INDICE = {nombre: i for i, (nombre, _) in enumerate(PALETA)}


# ---------------------------------------------------------------- formas

def caja(color, tam, centro, rot=(0, 0, 0)):
    """Caja de tamaño (x, y, z) centrada en `centro`, girada `rot` grados (XYZ)."""
    return {"tipo": "caja", "color": color, "tam": tam, "centro": centro, "rot": rot}


def cilindro(color, radio, alto, centro, lados=10, rot=(0, 0, 0), radio2=None):
    """Cilindro (o cono truncado si radio2) con el eje en Z antes de girar."""
    return {"tipo": "cilindro", "color": color, "radio": radio, "radio2": radio if radio2 is None else radio2,
            "alto": alto, "centro": centro, "lados": lados, "rot": rot}


def cono(color, radio, alto, centro, lados=8, rot=(0, 0, 0)):
    return cilindro(color, radio, alto, centro, lados, rot, radio2=0.0)


def roca(color, radio, centro, semilla, aplastado=0.7):
    return {"tipo": "roca", "color": color, "radio": radio, "centro": centro, "semilla": semilla,
            "aplastado": aplastado}


# ---------------------------------------------------------------- kit

def pieza_muro():
    grosor, zocalo, remate = 0.2, 0.3, 0.08
    return {"muro": [
        caja("ocre_claro", (CELDA, grosor, ALTURA_MURO - zocalo - remate),
             (0, 0, zocalo + (ALTURA_MURO - zocalo - remate) / 2)),
        caja("madera_oscura", (CELDA, grosor + 0.06, zocalo), (0, 0, zocalo / 2)),
        caja("cobre", (CELDA, grosor + 0.04, remate), (0, 0, ALTURA_MURO - remate / 2)),
    ]}


def pieza_suelo():
    base, tabla, tablas = 0.06, 0.04, 4
    ancho = CELDA / tablas
    formas = [caja("madera_oscura", (CELDA, CELDA, base), (0, 0, base / 2))]
    for i in range(tablas):
        y = -CELDA / 2 + ancho * (i + 0.5)
        color = "madera_clara" if i % 2 == 0 else "ocre_oscuro"
        formas.append(caja(color, (CELDA - 0.04, ancho - 0.03, tabla), (0, y, base + tabla / 2)))
    return {"suelo": formas}


def pieza_viga():
    lado, banda, holgura = 0.25, 0.12, 0.015
    formas = [caja("madera_oscura", (CELDA, lado, lado), (0, 0, lado / 2 + holgura))]
    for signo in (-1, 1):
        x = signo * (CELDA / 2 - banda / 2 - 0.1)
        formas.append(caja("cobre", (banda, lado + 2 * holgura, lado + 2 * holgura), (x, 0, lado / 2 + holgura)))
    return {"viga": formas}


# ---------------------------------------------------------------- edificios

def _medidas(ancho_celdas, fondo_celdas):
    return ancho_celdas * CELDA - 2 * MARGEN, fondo_celdas * CELDA - 2 * MARGEN


def maquina_refugio(w, d, montada):
    """Devuelve (formas de la máquina, formas móviles, centro del objeto móvil)."""
    x, y = -w / 2 + 0.85, -d / 2 + 1.3
    cama = [caja("madera_oscura", (0.95, 1.95, 0.3), (x, y, LOSA + 0.15))]
    if montada:
        cama += [
            caja("tela_cruda", (0.85, 1.85, 0.14), (x, y, LOSA + 0.37)),
            caja("calcis_blanco", (0.6, 0.35, 0.1), (x, y - 0.65, LOSA + 0.49)),
            caja("calcis_azul", (0.85, 0.9, 0.05), (x, y + 0.4, LOSA + 0.465)),
            # mesa con los planos de la nave
            caja("madera_clara", (0.9, 0.6, 0.06), (w / 2 - 0.75, -d / 2 + 0.6, LOSA + 0.8)),
            caja("madera_oscura", (0.08, 0.08, 0.8), (w / 2 - 1.1, -d / 2 + 0.4, LOSA + 0.4)),
            caja("madera_oscura", (0.08, 0.08, 0.8), (w / 2 - 0.4, -d / 2 + 0.4, LOSA + 0.4)),
            caja("madera_oscura", (0.08, 0.08, 0.8), (w / 2 - 1.1, -d / 2 + 0.8, LOSA + 0.4)),
            caja("madera_oscura", (0.08, 0.08, 0.8), (w / 2 - 0.4, -d / 2 + 0.8, LOSA + 0.4)),
            caja("calcis_azul", (0.6, 0.4, 0.02), (w / 2 - 0.75, -d / 2 + 0.6, LOSA + 0.84), rot=(0, 0, 8)),
        ]
    return cama, [], None


def maquina_aserradero(w, d, montada):
    y = -d / 2 + 1.2
    alto_mesa = 0.85
    formas = [caja("madera_oscura", (2.6, 0.9, 0.1), (0, y, LOSA + alto_mesa))]
    for sx in (-1, 1):
        for sy in (-1, 1):
            formas.append(caja("madera_oscura", (0.12, 0.12, alto_mesa), (sx * 1.15, y + sy * 0.35, LOSA + alto_mesa / 2)))
    if not montada:
        return formas, [], None
    formas += [
        # tronco sobre la mesa y tablones apilados
        cilindro("madera_clara", 0.2, 1.6, (-0.4, y - 0.15, LOSA + alto_mesa + 0.25), lados=8, rot=(0, 90, 0)),
        caja("ocre_claro", (1.4, 0.3, 0.05), (w / 2 - 1.1, d / 2 - 1.0, LOSA + 0.05)),
        caja("ocre_claro", (1.4, 0.3, 0.05), (w / 2 - 1.1, d / 2 - 1.0, LOSA + 0.11), rot=(0, 0, 5)),
        caja("ocre_claro", (1.4, 0.3, 0.05), (w / 2 - 1.1, d / 2 - 1.0, LOSA + 0.17), rot=(0, 0, -4)),
        # soporte de la sierra
        caja("cobre", (0.1, 0.5, 0.5), (0.72, y, LOSA + alto_mesa + 0.25)),
    ]
    centro = (0.6, y, LOSA + alto_mesa + 0.3)
    sierra = [
        cilindro("calcis_metal", 0.5, 0.03, (0, 0, 0), lados=12, rot=(0, 90, 0)),
        cilindro("cobre", 0.12, 0.08, (0, 0, 0), lados=8, rot=(0, 90, 0)),
        caja("calcis_gris", (0.035, 0.12, 0.9), (0, 0, 0)),
        caja("calcis_gris", (0.035, 0.9, 0.12), (0, 0, 0)),
    ]
    return formas, sierra, centro


def maquina_fundicion(w, d, montada):
    y = -d / 2 + 1.3
    formas = [caja("tierra", (1.6, 1.5, 0.5), (0, y, LOSA + 0.25))]
    if not montada:
        return formas, [], None
    formas += [
        caja("roca", (1.5, 1.4, 1.3), (0, y, LOSA + 0.5 + 0.65)),
        caja("fuego", (0.7, 0.1, 0.5), (0, y + 0.69, LOSA + 0.95)),
        caja("cobre", (0.9, 0.08, 0.1), (0, y + 0.72, LOSA + 1.27)),
        caja("roca", (0.6, 0.6, ALTO + 1.6 - 1.8), (0, y - 0.2, LOSA + 1.8 + (ALTO + 1.6 - 1.8) / 2)),
        caja("cobre", (0.7, 0.7, 0.1), (0, y - 0.2, LOSA + ALTO + 1.6)),
        # canal de colada y molde
        caja("cobre", (0.2, 0.9, 0.08), (0.5, y + 1.1, LOSA + 0.5), rot=(-12, 0, 0)),
        caja("tierra", (0.6, 0.5, 0.2), (0.5, y + 1.7, LOSA + 0.1)),
        caja("fuego", (0.45, 0.35, 0.04), (0.5, y + 1.7, LOSA + 0.21)),
        # lingotes
        caja("calcis_metal", (0.3, 0.14, 0.1), (-w / 2 + 0.8, d / 2 - 0.9, LOSA + 0.05)),
        caja("calcis_metal", (0.3, 0.14, 0.1), (-w / 2 + 0.8, d / 2 - 0.7, LOSA + 0.05)),
        caja("calcis_metal", (0.3, 0.14, 0.1), (-w / 2 + 0.8, d / 2 - 0.8, LOSA + 0.15), rot=(0, 0, 90)),
    ]
    return formas, [], None


def maquina_almacen(w, d, montada):
    """Estanterías con el stock a la vista."""
    y = -d / 2 + 0.55
    formas = [caja("madera_oscura", (w - 1.0, 0.5, 0.1), (0, y, LOSA + 0.05))]
    if not montada:
        return formas, [], None
    for sx in (-1, 0, 1):
        formas.append(caja("madera_oscura", (0.1, 0.5, 2.0), (sx * (w / 2 - 0.55), y, LOSA + 1.0)))
    for z in (0.7, 1.35, 2.0):
        formas.append(caja("madera_clara", (w - 1.0, 0.55, 0.06), (0, y, LOSA + z)))
    formas += [
        caja("ocre_claro", (1.2, 0.35, 0.3), (-w / 4, y, LOSA + 0.88)),
        caja("calcis_metal", (0.6, 0.3, 0.2), (w / 4, y, LOSA + 0.83)),
        caja("roca", (0.5, 0.4, 0.35), (w / 4 - 0.2, y, LOSA + 1.56)),
        caja("tela_cruda", (0.7, 0.4, 0.4), (-w / 4 + 0.3, y, LOSA + 1.58)),
        cilindro("madera_clara", 0.3, 0.7, (w / 2 - 0.9, d / 2 - 0.9, LOSA + 0.35), lados=8),
        cilindro("madera_clara", 0.3, 0.7, (w / 2 - 1.6, d / 2 - 0.8, LOSA + 0.35), lados=8),
        caja("ocre_oscuro", (0.7, 0.7, 0.6), (-w / 2 + 0.9, d / 2 - 0.9, LOSA + 0.3), rot=(0, 0, 12)),
    ]
    return formas, [], None


def maquina_comedor(w, d, montada):
    """Mesa larga y la olla al fuego. Terminado, solo la olla: la mesa, las sillas y la comida son
    las originales del paquete "Lowpoly Dinner Table" y las pone Unity (ConstructorEscena.ComedorConMesa)."""
    olla = [
        caja("roca", (0.9, 0.9, 0.45), (w / 2 - 0.9, -d / 2 + 0.9, LOSA + 0.225)),
        caja("fuego", (0.5, 0.5, 0.06), (w / 2 - 0.9, -d / 2 + 0.9, LOSA + 0.48)),
        cilindro("cobre", 0.36, 0.45, (w / 2 - 0.9, -d / 2 + 0.9, LOSA + 0.8), lados=8, radio2=0.42),
    ]
    if montada:
        return olla, [], None
    formas = [caja("madera_oscura", (2.6, 0.9, 0.08), (-0.4, 0.2, LOSA + 0.78))]
    for sx in (-1, 1):
        formas.append(caja("madera_oscura", (0.12, 0.7, 0.75), (-0.4 + sx * 1.1, 0.2, LOSA + 0.375)))
    return formas, [], None


def maquina_pozo(w, d, montada):
    """Brocal de piedra con torno; el torno es la pieza móvil."""
    formas = [cilindro("roca", 0.75, 0.7, (0, 0, LOSA + 0.35), lados=10)]
    if not montada:
        return formas, [], None
    formas += [
        cilindro("calcis_azul", 0.6, 0.04, (0, 0, LOSA + 0.66), lados=10),
        caja("madera_oscura", (0.14, 0.14, 1.9), (-0.85, 0, LOSA + 0.95)),
        caja("madera_oscura", (0.14, 0.14, 1.9), (0.85, 0, LOSA + 0.95)),
        cilindro("madera_clara", 0.22, 0.4, (1.4, 0.9, LOSA + 0.2), lados=8),
        cilindro("calcis_azul", 0.19, 0.02, (1.4, 0.9, LOSA + 0.4), lados=8),
    ]
    centro = (0, 0, LOSA + 1.7)
    torno = [
        cilindro("madera_clara", 0.12, 1.7, (0, 0, 0), lados=8, rot=(0, 90, 0)),
        caja("cobre", (0.06, 0.06, 0.5), (0.95, 0, 0.2)),
        caja("cobre", (0.3, 0.06, 0.06), (1.1, 0, 0.45)),
    ]
    return formas, torno, centro


def maquina_taller(w, d, montada):
    """Banco de montaje con bobinas de cobre."""
    y = -d / 2 + 0.75
    formas = [caja("madera_oscura", (w - 1.2, 0.8, 0.1), (0, y, LOSA + 0.85))]
    for sx in (-1, 1):
        formas.append(caja("madera_oscura", (0.12, 0.7, 0.85), (sx * (w / 2 - 0.8), y, LOSA + 0.425)))
    if not montada:
        return formas, [], None
    for i, x in enumerate((-1.2, -0.4, 0.5)):
        formas.append(cilindro("cobre", 0.16, 0.3, (x, y, LOSA + 1.05), lados=8))
        formas.append(cilindro("madera_clara", 0.2, 0.04, (x, y, LOSA + 1.22), lados=8))
    formas += [
        caja("calcis_metal", (0.5, 0.3, 0.25), (1.3, y, LOSA + 1.02)),
        caja("cristal", (0.12, 0.12, 0.12), (1.3, y, LOSA + 1.2)),
        caja("cobre", (2.4, 0.04, 0.04), (0, y + 0.3, LOSA + 1.35)),
        cilindro("cobre", 0.35, 0.5, (w / 2 - 0.9, d / 2 - 0.9, LOSA + 0.25), lados=10, rot=(90, 0, 20)),
    ]
    return formas, [], None


def maquina_optica(w, d, montada):
    """Banco de lentes: un cristal grande en un aro de cobre que gira."""
    formas = [caja("madera_oscura", (1.2, 1.2, 0.9), (0, -0.3, LOSA + 0.45))]
    if not montada:
        return formas, [], None
    formas += [
        caja("cobre", (0.12, 0.12, 1.3), (-0.5, -0.3, LOSA + 1.55)),
        caja("cobre", (0.12, 0.12, 1.3), (0.5, -0.3, LOSA + 1.55)),
        caja("calcis_blanco", (0.8, 0.5, 0.05), (-w / 2 + 0.9, d / 2 - 0.9, LOSA + 0.8)),
        caja("madera_oscura", (0.7, 0.4, 0.8), (-w / 2 + 0.9, d / 2 - 0.9, LOSA + 0.4)),
        cilindro("cristal", 0.14, 0.04, (-w / 2 + 0.8, d / 2 - 0.9, LOSA + 0.85), lados=8),
        cilindro("cristal", 0.1, 0.04, (-w / 2 + 1.1, d / 2 - 0.8, LOSA + 0.85), lados=8),
    ]
    centro = (0, -0.3, LOSA + 1.75)
    lente = [
        cilindro("cobre", 0.5, 0.08, (0, 0, 0), lados=12, rot=(90, 0, 0)),
        cilindro("cristal", 0.42, 0.12, (0, 0, 0), lados=12, rot=(90, 0, 0)),
        caja("cobre", (0.9, 0.05, 0.05), (0, 0, 0)),
    ]
    return formas, lente, centro


def maquina_astillero(w, d, montada):
    """Grúa de pórtico sobre un banco de montaje: aquí se ensamblan los módulos de la nave."""
    formas = [
        caja("madera_oscura", (2.4, 1.2, 0.12), (0, 0, LOSA + 0.8)),
        caja("madera_oscura", (0.14, 1.0, 0.8), (-1.0, 0, LOSA + 0.4)),
        caja("madera_oscura", (0.14, 1.0, 0.8), (1.0, 0, LOSA + 0.4)),
    ]
    if not montada:
        return formas, [], None
    formas += [
        caja("cobre", (0.16, 0.16, 2.5), (-w / 2 + 0.7, 0, LOSA + 1.25)),
        caja("cobre", (0.16, 0.16, 2.5), (w / 2 - 0.7, 0, LOSA + 1.25)),
        caja("madera_clara", (w - 1.2, 0.2, 0.2), (0, 0, LOSA + 2.55)),
        caja("calcis_metal", (0.05, 0.05, 0.9), (0.3, 0, LOSA + 2.05)),
        caja("cobre", (0.22, 0.1, 0.16), (0.3, 0, LOSA + 1.55)),
        caja("calcis_blanco", (1.1, 0.7, 0.3), (0, 0, LOSA + 1.01)),
        caja("calcis_azul", (0.3, 0.72, 0.08), (0.2, 0, LOSA + 1.2)),
        caja("madera_clara", (0.7, 0.5, 0.5), (-w / 2 + 0.9, d / 2 - 0.9, LOSA + 0.25)),
        cilindro("calcis_gris", 0.2, 0.6, (w / 2 - 0.9, d / 2 - 0.9, LOSA + 0.3), lados=8),
    ]
    return formas, [], None


def maquina_teleferico(w, d, montada):
    """Estación de cable: una gran rueda de madera y cobre y una vagoneta lista para salir."""
    formas = [
        caja("madera_oscura", (0.2, 0.2, 2.3), (-0.6, 0.3, LOSA + 1.15)),
        caja("madera_oscura", (0.2, 0.2, 2.3), (0.6, 0.3, LOSA + 1.15)),
    ]
    if not montada:
        return formas, [], None
    formas += [
        caja("madera_clara", (0.9, 0.6, 0.45), (0.0, -0.9, LOSA + 0.5)),
        caja("cobre", (0.95, 0.65, 0.06), (0.0, -0.9, LOSA + 0.26)),
        caja("cobre", (0.05, 0.05, 0.9), (0.0, -0.9, LOSA + 1.15)),
        caja("calcis_metal", (0.04, 2.6, 0.04), (0.0, 0.0, LOSA + 1.6)),
    ]
    centro = (0, 0.3, LOSA + 2.2)
    rueda = [
        cilindro("madera_clara", 0.75, 0.14, (0, 0, 0), lados=12, rot=(0, 90, 0)),
        cilindro("cobre", 0.2, 1.3, (0, 0, 0), lados=8, rot=(0, 90, 0)),
        caja("cobre", (0.16, 1.4, 0.08), (0, 0, 0)),
        caja("cobre", (0.16, 0.08, 1.4), (0, 0, 0)),
    ]
    return formas, rueda, centro


def afloramiento_cobre():
    formas = [
        roca("roca", 0.7, (0, 0, 0.3), semilla=41),
        roca("cobre", 0.45, (0.4, 0.2, 0.45), semilla=42),
        roca("cobre_verdin", 0.35, (-0.35, 0.3, 0.4), semilla=43),
        roca("cobre", 0.3, (0.1, -0.45, 0.35), semilla=44),
    ]
    return {"cobre": formas}


def cristal():
    formas = [roca("roca", 0.6, (0, 0, 0.2), semilla=51, aplastado=0.5)]
    r = random.Random(9)
    for i in range(5):
        a = i * math.tau / 5
        alto = r.uniform(0.7, 1.5)
        formas.append(cono("cristal", r.uniform(0.14, 0.22), alto,
                           (math.cos(a) * 0.3, math.sin(a) * 0.3, 0.2 + alto / 2), lados=5,
                           rot=(math.degrees(math.sin(a)) * 0.3, -math.degrees(math.cos(a)) * 0.3, 0)))
    formas.append(cono("cristal", 0.25, 1.9, (0, 0, 1.1), lados=5))
    return {"cristal": formas}


def hoguera():
    formas = []
    for i in range(8):
        a = i * math.tau / 8
        formas.append(roca("roca", 0.2, (math.cos(a) * 0.6, math.sin(a) * 0.6, 0.1), semilla=30 + i))
    for i in range(3):
        formas.append(cilindro("madera_oscura", 0.08, 0.9, (0, 0, 0.3), lados=6, rot=(62, 0, i * 120)))
    formas.append(cono("fuego", 0.28, 0.7, (0, 0, 0.45), lados=6))
    # bancos de tronco alrededor
    for i in range(3):
        a = i * math.tau / 3 + 0.5
        formas.append(cilindro("madera_clara", 0.22, 1.5, (math.cos(a) * 1.9, math.sin(a) * 1.9, 0.22), lados=7,
                               rot=(90, 0, math.degrees(a))))
    return {"hoguera": formas}


LETREROS = {"refugio": "azul3", "aserradero": "madera3", "fundicion": "fuego", "almacen": "ocre_claro", "casa": "flor2",
            "comedor": "flor", "pozo": "agua", "taller": "cobre", "optica": "cristal", "astillero": "calcis_blanco",
            "teleferico": "cobre_verdin", "banco": "madera3"}


def edificio(nombre, ancho_celdas, fondo_celdas, maquina):
    """Devuelve los 4 modelos de obra de un edificio: {nombre_1: {...}, ..., nombre_4: {...}}."""
    w, d = _medidas(ancho_celdas, fondo_celdas)
    base, _, _ = maquina(w, d, False)
    completa, movil, centro_movil = maquina(w, d, True)
    D = detalle

    cimientos = {"Cuerpo": D.capa_losa(nombre, w, d, True)}
    esqueleto = {"Cuerpo": D.capa_losa(nombre, w, d, False) + D.capa_estructura(nombre, w, d, True)}
    casi = {"Cuerpo": D.capa_losa(nombre, w, d, False) + D.capa_estructura(nombre, w, d, False)
            + D.capa_paredes(nombre, w, d, True) + D.capa_tejado(nombre, w, d, True) + base}
    terminado = {
        "Cuerpo": D.capa_losa(nombre, w, d, False) + D.capa_estructura(nombre, w, d, False)
        + D.capa_paredes(nombre, w, d, False, LETREROS.get(nombre)),
        "Tejado": D.capa_tejado(nombre, w, d, False, chimenea_grande=nombre == "fundicion"),
        "Maquina": completa,
    }
    if movil:
        terminado["Movil"] = {"origen": centro_movil, "formas": movil}
    return {f"{nombre}_1": cimientos, f"{nombre}_2": esqueleto, f"{nombre}_3": casi, f"{nombre}_4": terminado}


# ---------------------------------------------------------------- naturaleza y nave

def veta():
    formas = [
        roca("roca", 1.0, (0, 0, 0.45), semilla=3),
        roca("roca", 0.6, (0.8, 0.3, 0.3), semilla=5),
        roca("tierra", 0.5, (-0.7, -0.4, 0.25), semilla=8),
        caja("cobre", (0.25, 0.25, 0.5), (0.2, -0.5, 0.75), rot=(25, 15, 30)),
        caja("cobre", (0.2, 0.2, 0.4), (-0.3, 0.3, 0.95), rot=(-20, 30, 10)),
        caja("calcis_metal", (0.18, 0.18, 0.35), (0.6, 0.1, 0.75), rot=(15, -25, 40)),
    ]
    return {"veta": formas}


def casco():
    """Nave de Calcis estrellada, apoyada en una cuna de madera colona. Ocupa 3 x 6 celdas."""
    z = 1.9
    cuerpo = [
        caja("calcis_blanco", (3.0, 7.0, 2.4), (0, 0, z)),
        caja("calcis_gris", (2.6, 7.2, 0.5), (0, 0, z - 1.15)),
        cilindro("calcis_blanco", 1.5, 2.6, (0, 4.6, z), lados=8, rot=(-90, 0, 0), radio2=0.45),
        caja("calcis_azul", (3.04, 5.0, 0.18), (0, 0.4, z + 0.55)),
        cilindro("calcis_metal", 1.25, 1.0, (0, -4.0, z), lados=10, rot=(90, 0, 0), radio2=1.0),
        cilindro("calcis_gris", 0.8, 0.5, (0, -4.6, z), lados=10, rot=(90, 0, 0)),
        caja("calcis_azul", (1.6, 0.9, 0.5), (0, 2.6, z + 1.3)),
        # el boquete del costado: interior a la vista
        caja("madera_oscura", (0.06, 2.3, 1.5), (1.5, 0.2, z - 0.1)),
        # cuna y puntales de madera colona
        caja("madera_oscura", (3.8, 0.3, 0.3), (0, 2.4, 0.15)),
        caja("madera_oscura", (3.8, 0.3, 0.3), (0, -2.4, 0.15)),
        caja("madera_clara", (0.25, 0.25, 0.7), (1.2, 2.4, 0.45)),
        caja("madera_clara", (0.25, 0.25, 0.7), (-1.2, 2.4, 0.45)),
        caja("madera_clara", (0.25, 0.25, 0.7), (1.2, -2.4, 0.45)),
        caja("madera_clara", (0.25, 0.25, 0.7), (-1.2, -2.4, 0.45)),
        caja("madera_clara", (0.14, 0.14, 2.6), (2.1, -1.6, 1.1), rot=(0, -28, 0)),
        caja("madera_clara", (0.14, 0.14, 2.6), (-2.1, 1.2, 1.1), rot=(0, 28, 0)),
    ]
    placa = [
        caja("cobre", (0.08, 2.5, 1.7), (1.56, 0.2, z - 0.1)),
        caja("madera_clara", (0.05, 2.6, 0.12), (1.61, 0.2, z + 0.55)),
        caja("madera_clara", (0.05, 2.6, 0.12), (1.61, 0.2, z - 0.75)),
    ]
    for sy in (-1, 0, 1):
        for sz in (-1, 1):
            placa.append(caja("cobre_verdin", (0.06, 0.1, 0.1), (1.62, 0.2 + sy * 1.05, z - 0.1 + sz * 0.5)))
    soporte = [
        cilindro("calcis_gris", 0.5, 1.8, (-0.6, 0.6, z + 1.45), lados=10, rot=(90, 0, 0)),
        cilindro("calcis_azul", 0.4, 1.2, (0.6, 0.9, z + 1.4), lados=10, rot=(90, 0, 0)),
        caja("cobre", (1.3, 0.08, 0.08), (0, 0.2, z + 1.6)),
        caja("cobre", (0.08, 0.08, 0.5), (-0.6, -0.4, z + 1.35)),
        caja("cobre", (0.08, 0.08, 0.5), (0.6, 0.2, z + 1.35)),
    ]
    motor = [
        cilindro("cobre", 1.42, 0.35, (0, -3.75, z), lados=12, rot=(90, 0, 0)),
        cilindro("cobre_verdin", 1.05, 0.3, (0, -4.95, z), lados=12, rot=(90, 0, 0), radio2=0.85),
        cilindro("fuego", 0.6, 0.12, (0, -5.12, z), lados=10, rot=(90, 0, 0)),
    ]
    for i in range(6):
        a = i * math.tau / 6
        motor.append(caja("madera_clara", (0.12, 1.3, 0.12), (math.cos(a) * 1.3, -4.3, z + math.sin(a) * 1.3)))
    navegacion = [
        caja("cobre", (0.1, 0.1, 1.6), (0, 3.2, z + 2.0)),
        cilindro("cobre", 0.42, 0.06, (0, 3.2, z + 2.9), lados=12, rot=(90, 0, 0)),
        cilindro("cristal", 0.35, 0.1, (0, 3.2, z + 2.9), lados=12, rot=(90, 0, 0)),
        caja("madera_clara", (0.9, 0.06, 0.06), (0, 3.2, z + 2.2)),
        caja("cristal", (0.2, 0.2, 0.2), (0, 5.85, z), rot=(45, 0, 45)),
    ]
    combustible = []
    for i in range(4):
        y = -2.4 + i * 1.3
        combustible.append(cilindro("cobre", 0.3, 0.9, (-1.75, y, z - 0.3), lados=8))
        combustible.append(cilindro("cristal", 0.22, 1.1, (-1.75, y, z - 0.3), lados=8))
        combustible.append(caja("madera_clara", (0.4, 0.08, 0.08), (-1.6, y, z + 0.25)))
    return {"casco": {"Casco": cuerpo, "Modulo1": placa, "Modulo2": soporte, "Modulo3": motor,
                      "Modulo4": navegacion, "Modulo5": combustible}}


def modelos():
    """Devuelve {carpeta: {nombre_modelo: {nombre_objeto: formas}}}."""
    edificios = {}
    edificios.update(edificio("refugio", 2, 2, maquina_refugio))
    edificios.update(edificio("aserradero", 3, 2, maquina_aserradero))
    edificios.update(edificio("fundicion", 3, 3, maquina_fundicion))
    edificios.update(edificio("almacen", 3, 2, maquina_almacen))
    # La casa es de dos plantas (3x2 casillas), como en la hoja de referencia.
    detalle.plantas(2)
    edificios.update(edificio("casa", 3, 2, maquina_casa_dos_plantas))
    detalle.plantas(1)
    edificios.update(edificio("comedor", 3, 2, maquina_comedor))
    edificios.update(edificio("pozo", 2, 2, maquina_pozo))
    edificios.update(edificio("taller", 3, 2, maquina_taller))
    edificios.update(edificio("optica", 3, 3, maquina_optica))
    edificios.update(edificio("astillero", 3, 3, maquina_astillero))
    edificios.update(edificio("teleferico", 2, 2, maquina_teleferico))
    edificios.update(edificio("banco", 2, 2, maquina_banco))
    naturaleza = {}
    naturaleza.update(arbol("arbol_a", 1.9, 1.5, semilla=1))
    naturaleza.update(pino("arbol_b", 5.2, 1.5, semilla=2))
    naturaleza.update(arbol("arbol_c", 2.4, 1.8, semilla=3))
    naturaleza.update(penasco("penasco_a", 41))
    naturaleza.update(penasco("penasco_b", 42))
    naturaleza.update(tunel())
    naturaleza.update(puente())
    naturaleza.update(ruina_arco())
    naturaleza.update(columna_rota())
    naturaleza.update(obelisco())
    naturaleza.update(veta())
    naturaleza.update(arbusto())
    naturaleza.update(hoguera())
    naturaleza.update(afloramiento_cobre())
    naturaleza.update(cristal())
    naturaleza.update(camino("camino_a", 0))
    naturaleza.update(camino("camino_b", 1))
    # Los paquetes de la tienda (pinos, panadería, barril, mesa) se usan tal cual en Unity, con sus
    # texturas: son el listón de calidad. modelos_externos.py (pasarlos a la paleta) ya no se usa.
    return {
        "Kit": {"muro": pieza_muro(), "suelo": pieza_suelo(), "viga": pieza_viga()},
        "Edificios": edificios,
        "Naturaleza": {k: {k: v} for k, v in naturaleza.items()},
        "Nave": casco(),
    }


# ---------------------------------------------------------------- malla y exportación

def uv_de(color, variante=VARIANTE_BASE):
    i = INDICE[color]
    # La fila 0 es la de arriba de la imagen; en UV la v crece hacia arriba.
    return ((variante + 0.5) / COLUMNAS, 1.0 - (i + 0.5) / FILAS)


def _anadir_externa(bm, capa_uv, f):
    """Malla de un paquete ya pasada a la paleta (Pipeline/externos.py): un color por cara."""
    giro = Euler([math.radians(a) for a in f.get("rot", (0, 0, 0))], "XYZ").to_matrix()
    centro = Vector(f["centro"])
    M = f.get("M")
    usados = sorted({i for c in f["caras"] for i in c})
    nuevos = {}
    for i in usados:
        co = giro @ Vector(f["verts"][i]) + centro
        if M is not None:
            co = M @ co
        nuevos[i] = bm.verts.new(co)
    variante = f.get("v", VARIANTE_BASE)
    for indices, color in zip(f["caras"], f["colores"]):
        if len(set(indices)) < 3:
            continue
        try:
            cara = bm.faces.new([nuevos[i] for i in indices])
        except ValueError:
            continue  # cara repetida en el original
        cara.smooth = False
        uv = uv_de(color, variante)
        for bucle in cara.loops:
            bucle[capa_uv].uv = uv


def _anadir_forma(bm, capa_uv, f):
    if f["tipo"] == "externa":
        _anadir_externa(bm, capa_uv, f)
        return
    antes_v = set(bm.verts)
    tipo = f["tipo"]
    bisel = 0.0
    azar = random.Random(f.get("semilla", 0))
    escala = Vector((1, 1, 1))
    if tipo == "caja":
        bmesh.ops.create_cube(bm, size=1.0)
        escala = Vector(f["tam"])
        bisel = f.get("bisel", BISEL)
        if min(f["tam"]) <= bisel * 3:
            bisel = 0.0
    elif tipo == "cilindro":
        r2 = f["radio2"]
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=f["lados"],
                              radius1=f["radio"], radius2=r2, depth=f["alto"])
        escala = Vector(f.get("esc", (1, 1, 1)))
    elif tipo == "roca":
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=f["radio"])
        escala = Vector((1, 1, f["aplastado"]))
    elif tipo == "bola":
        bmesh.ops.create_icosphere(bm, subdivisions=f.get("sub", 1), radius=f["radio"])
        escala = Vector(f["esc"])
    elif tipo == "prisma":
        va = [bm.verts.new((x, f["y0"], z)) for x, z in f["pts"]]
        vb = [bm.verts.new((x, f["y1"], z)) for x, z in f["pts"]]
        n = len(va)
        bm.faces.new(va)
        bm.faces.new(vb[::-1])
        for i in range(n):
            bm.faces.new((va[i], vb[i], vb[(i + 1) % n], va[(i + 1) % n]))
        bisel = f.get("bisel", 0.0)
    else:
        raise ValueError(tipo)

    nuevos = [v for v in bm.verts if v not in antes_v]
    if tipo == "roca":
        r = random.Random(f["semilla"])
        for v in nuevos:
            v.co *= r.uniform(0.78, 1.12)
    if tipo == "bola" and f["ruido"]:
        for v in nuevos:
            v.co *= 1 + azar.uniform(-f["ruido"], f["ruido"])

    deform = f.get("deform", 0.0)
    for v in nuevos:
        v.co = Vector((v.co.x * escala.x, v.co.y * escala.y, v.co.z * escala.z))
        if deform:
            v.co += Vector((azar.uniform(-deform, deform), azar.uniform(-deform, deform), azar.uniform(-deform, deform)))

    if bisel:
        aristas = list({e for v in nuevos for e in v.link_edges})
        bmesh.ops.bevel(bm, geom=aristas, offset=bisel, segments=1, affect="EDGES", profile=0.5)
        nuevos = [v for v in bm.verts if v not in antes_v]

    if tipo != "prisma":
        giro = Euler([math.radians(a) for a in f.get("rot", (0, 0, 0))], "XYZ").to_matrix()
        centro = Vector(f["centro"])
        for v in nuevos:
            v.co = giro @ v.co + centro
    M = f.get("M")
    if M is not None:
        for v in nuevos:
            v.co = M @ v.co

    variante = f.get("v", VARIANTE_BASE)
    luz_z = f.get("luz_z", False)
    caras = {c for v in nuevos for c in v.link_faces}
    if luz_z:
        for cara in caras:
            cara.normal_update()
        bmesh.ops.recalc_face_normals(bm, faces=list(caras))
        for cara in caras:
            cara.normal_update()
    uv = uv_de(f["color"], variante)
    for cara in caras:
        cara.smooth = False
        if luz_z:
            nz = cara.normal.z
            paso = 2 if nz > 0.6 else 1 if nz > 0.15 else -2 if nz < -0.45 else -1 if nz < -0.1 else 0
            uv = uv_de(f["color"], max(0, min(5, min(variante, 5) + paso)))
        for bucle in cara.loops:
            bucle[capa_uv].uv = uv


def construir_malla(nombre, formas):
    bm = bmesh.new()
    capa_uv = bm.loops.layers.uv.new("UVMap")
    for f in formas:
        _anadir_forma(bm, capa_uv, f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    malla = bpy.data.meshes.new(nombre)
    bm.to_mesh(malla)
    bm.free()
    return malla


def vaciar_escena():
    for objeto in list(bpy.data.objects):
        bpy.data.objects.remove(objeto, do_unlink=True)
    for malla in list(bpy.data.meshes):
        if malla.users == 0:
            bpy.data.meshes.remove(malla)


def exportar(nombre, objetos, carpeta):
    vaciar_escena()
    bpy.ops.object.select_all(action="DESELECT")
    triangulos = 0
    for nombre_objeto, contenido in objetos.items():
        origen = (0, 0, 0)
        formas = contenido
        if isinstance(contenido, dict):
            origen = contenido["origen"]
            formas = contenido["formas"]
        malla = construir_malla(nombre_objeto, formas)
        triangulos += len(malla.polygons)
        objeto = bpy.data.objects.new(nombre_objeto, malla)
        objeto.location = origen
        bpy.context.scene.collection.objects.link(objeto)
        objeto.select_set(True)
        bpy.context.view_layer.objects.active = objeto

    ruta = os.path.join(carpeta, f"{nombre}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=ruta,
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
    print(f"  {os.path.basename(carpeta)}/{nombre}.fbx  ({len(objetos)} objetos, {triangulos} triángulos)")
    return ruta


def color_variante(rgb, variante):
    k = VARIANTES[variante]
    if not isinstance(k, tuple):
        k = (k, k, k)
    return tuple(max(0, min(255, round(c * f))) for c, f in zip(rgb, k))


def escribir_paleta(carpeta):
    from PIL import Image

    imagen = Image.new("RGB", (COLUMNAS * PIXELES_MUESTRA, FILAS * PIXELES_MUESTRA))
    for i, (_, rgb) in enumerate(PALETA):
        for v in range(COLUMNAS):
            x = v * PIXELES_MUESTRA
            y = i * PIXELES_MUESTRA
            imagen.paste(color_variante(rgb, v), (x, y, x + PIXELES_MUESTRA, y + PIXELES_MUESTRA))
    ruta = os.path.join(carpeta, "Paleta.png")
    imagen.save(ruta)
    print("  Kit/Paleta.png")
    return ruta


def main():
    if "--" in sys.argv:
        argumentos = sys.argv[sys.argv.index("--") + 1:]
    elif bpy.app.binary_path:
        # Lanzado con el ejecutable de Blender y sin `--`: los argumentos son suyos, no nuestros.
        argumentos = []
    else:
        argumentos = sys.argv[1:]

    todos = modelos()
    nombres = sorted(n for grupo in todos.values() for n in grupo)
    raiz = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    analizador = argparse.ArgumentParser(description="Genera los modelos de Nostos.")
    analizador.add_argument("--salida", default=os.path.join(raiz, "Assets", "Nostos", "Arte"))
    analizador.add_argument("--solo", action="append", choices=nombres)
    opciones = analizador.parse_args(argumentos)

    print(f"Modelos de Nostos -> {opciones.salida}")
    os.makedirs(os.path.join(opciones.salida, "Kit"), exist_ok=True)
    escribir_paleta(os.path.join(opciones.salida, "Kit"))
    for carpeta, grupo in todos.items():
        destino = os.path.join(opciones.salida, carpeta)
        os.makedirs(destino, exist_ok=True)
        for nombre, objetos in grupo.items():
            if opciones.solo and nombre not in opciones.solo:
                continue
            exportar(nombre, objetos, destino)


if __name__ == "__main__":
    main()
