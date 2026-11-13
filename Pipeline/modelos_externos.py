"""YA NO SE USA (Toni, 2026-11-13): los paquetes van con sus texturas originales. Se deja como referencia.

Modelos de los paquetes de la tienda que añadió Toni, pasados a la paleta (ver externos.py).

- "Pine Forest - LowPoly": pinos (para talar y para el monte), pinos secos, troncos caídos,
  tocón (con la madera del corte tapada), matas y manchas de hierba y tierra para el suelo del bosque.
- "Lowpoly Dinner Table": mesa, sillas, platos, pollo, verduras, jarras, velas y alfombra, que
  amueblan el comedor.
- "Lowpoly Baker's House": el barril. La casa del panadero se usa tal cual en Unity, con su
  textura, como casa antigua de Nova Lavinia.
"""
import os

import externos as X

# Los pinos del paquete vienen a escala de décimas: por 12 el más alto mide unos 12 m.
ESCALA_PINOS = 12.0


def _pino(rgb, alfa, z):
    r, g, b = rgb
    if r > g:
        return "tronco" if r >= 88 else "madera2"
    if g < 74:
        return "pino_oscuro"
    return "pino" if g < 82 else "pino_claro"


def _seco(rgb, alfa, z):
    return "tronco" if rgb[0] >= 95 else "madera2"


def _mata(rgb, alfa, z):
    g = rgb[1]
    return "pino_oscuro" if g < 60 else ("hoja_oscura" if g < 100 else "hierba_oscura")


def _hierba(rgb, alfa, z):
    return "hierba_oscura" if rgb[1] < 110 else "hierba"


def _tierra(rgb, alfa, z):
    return "tierra" if sum(rgb) < 300 else "tierra_clara"


# nombre del modelo: (objeto del paquete, colores, escala relativa, alfa mínimo)
PINOS = {
    "pino_1": ("Evergreen_TallTall", _pino, 1.0, None),
    "pino_2": ("Evergreen_TallWide", _pino, 1.0, None),
    "pino_3": ("Evergreen_TallMed", _pino, 1.0, None),
    "pino_4": ("EvergreenTallThick", _pino, 1.0, None),
    "pino_5": ("Evergreen_TallShort", _pino, 1.0, None),
    "pino_6": ("Evergreen_Med_Tall", _pino, 1.0, None),
    "pino_joven_1": ("Evergreen_Med", _pino, 1.0, None),
    "pino_joven_2": ("Evergreen_SmallBig", _pino, 1.0, None),
    "pino_seco_1": ("Dead4", _seco, 1.0, None),
    "pino_seco_2": ("Dead2", _seco, 1.0, None),
    "tocon_pino": ("Dead3", _seco, 0.45, None),
    "mata_1": ("Bush1_", _mata, 1.5, None),
    "mata_2": ("Bush2_", _mata, 1.5, None),
    "suelo_hierba_1": ("GroundCover_Grass2", _hierba, 1.0, 0.45),
    "suelo_hierba_2": ("GroundCover_Grass3", _hierba, 1.0, 0.45),
    "suelo_tierra_1": ("GroundCover_Dirt", _tierra, 1.0, 0.6),
    "suelo_tierra_2": ("GroundCover_Dirt1", _tierra, 1.0, 0.6),
}


def naturaleza():
    """{nombre: [formas]} para Arte/Naturaleza."""
    modelos = {}
    for nombre, (objeto, elegir, esc, alfa) in PINOS.items():
        f = X.forma(X.PINOS, objeto, escala=ESCALA_PINOS * esc, elegir=elegir, alfa_min=alfa,
                    tapar=elegir is _seco)
        if nombre.startswith("suelo_"):
            # manchas planas: un pelo por encima del suelo para que no parpadeen
            f["centro"] = (0, 0, 0.02)
        modelos[nombre] = [f]
    modelos["barril"] = [barril()]
    modelos["panaderia"] = [panaderia()]
    return modelos


# ---------------------------------------------------------------- casa del panadero

def _panaderia(rgb, alfa, z):
    """Por tono y no por cercanía: la textura pintada a mano es más oscura que la paleta."""
    import colorsys
    r, g, b = (c / 255 for c in rgb)
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    h *= 360
    if b > r + 0.06 and b > g:
        return "azul3"                                  # cristal de las ventanas
    if s < 0.46 and v < 0.62:                           # zócalo, escalones y chimenea de piedra
        return "piedra2" if v > 0.45 else "piedra4"
    if s < 0.52 and v > 0.62 and 25 <= h <= 55:         # revoco
        return "yeso" if v > 0.76 else "yeso2"
    if h < 22 and s > 0.45:                             # teja y ladrillo del horno
        return "teja2" if v > 0.72 else ("teja" if v > 0.5 else "teja3")
    return "madera3" if v > 0.62 else ("madera" if v > 0.42 else "madera2")


def panaderia(escala=0.72):
    """La casa del panadero del paquete con los colores del juego: la vieja panadería de Nova
    Lavinia, junto al camino. A 0,72 queda de unos 7 m de lado, como una casa del valle."""
    return X.forma(os.path.join(X.PANADERO, "Models", "Baker_house.FBX"), "Baker_house", escala=escala,
                   textura=os.path.join(X.PANADERO, "Textures", "Baker_house.tif"), elegir=_panaderia, votar=True)


# ---------------------------------------------------------------- comedor

def _tex(nombre):
    return os.path.join(X.MESA, "Textures", nombre)


MADERAS = ["madera", "madera2", "madera3", "madera_clara", "madera_oscura", "tronco"]
VAJILLA = ["blanco", "yeso3", "tela_cruda", "piedra3", "calcis_gris", "azul", "azul3"]
COMIDA = ["ocre_claro", "ocre_oscuro", "teja2", "teja4", "flor", "madera3", "tela_cruda"]
VERDURA = ["hoja", "hoja_oscura", "hierba", "teja4", "teja2", "ocre_claro"]
METAL = ["hierro", "calcis_metal", "piedra4"]


def _mesa(objeto, archivo, textura, escala, colores, centro, rot=(0, 0, 0), junto=False):
    return X.forma(os.path.join(X.MESA, "Models", archivo), objeto, escala=escala, textura=_tex(textura),
                   colores=colores, centro=centro, rot=rot, apoyar=not junto, centrar=not junto)


def barril(centro=(0, 0, 0), escala=0.62, rot=(0, 0, 0)):
    return X.forma(os.path.join(X.PANADERO, "Models", "Barrel.FBX"), "Barrel", escala=escala,
                   textura=os.path.join(X.PANADERO, "Textures", "Barrel.tif"),
                   colores=MADERAS + METAL, centro=centro, rot=rot)


def mesa_comedor(x, y, suelo):
    """La mesa del paquete con cuatro sillas, alfombra, pollo, platos, jarras y velas.
    (x, y) es el centro de la mesa y `suelo` la altura del suelo; la mesa va a lo largo de X."""
    e = 0.82                       # mesa y sillas: tablero a unos 0,74 m
    alto = 0.89 * e
    formas = [
        _mesa("Carpet_2_1", "Carpet_2_1.FBX", "Carpet_2_texture.tif", 1.45, ["teja", "teja3", "teja5", "ocre_claro", "azul2", "tela_cruda"],
              (x, y, suelo + 0.003)),
        _mesa("Table", "Table.FBX", "Table_texture.tif", e, MADERAS, (x, y, suelo), rot=(0, 0, 90)),
    ]
    # sillas: dos a cada lado largo, mirando a la mesa
    for sy, giro in ((-1, 0), (1, 180)):
        for dx in (-0.5, 0.5):
            formas.append(_mesa("Chair", "Chair.FBX", "Chair_texture.tif", e, MADERAS + ["teja3", "teja5", "hierro"],
                                (x + dx, y + sy * 0.62, suelo), rot=(0, 0, giro + SILLA_GIRO)))
    sobre = suelo + alto
    # la fuente del pollo con sus verduras, tal como viene colocada en el paquete
    formas.append(_mesa("Chicken.plate", "Chicken_plate.FBX", "Chicken_texture.tif", 0.55, COMIDA, (x, y, sobre), junto=True))
    formas.append(_mesa("Plate.001", "Chicken_plate.FBX", "Plate_texture.tif", 0.55, VAJILLA, (x, y, sobre), junto=True))
    verduras = [("Broccoli" + s, "broccoli.tif") for s in ("", ".001", ".002", ".003", ".004", ".005")]
    verduras += [("Carrot" + s, "carrot.tif") for s in ("", ".001", ".002", ".003")]
    for nombre, tex in verduras:
        f = X.forma(os.path.join(X.MESA, "Models", "Chicken_plate.FBX"), nombre, escala=0.55, textura=_tex(tex),
                    colores=VERDURA, apoyar=False, centrar=False, centro=(x, y, sobre))
        formas.append(f)
    for dx, sy in ((-0.5, -1), (0.5, 1), (0.5, -1), (-0.5, 1)):
        formas.append(_mesa("Plate", "Plate.FBX", "Plate_texture.tif", 0.4, VAJILLA, (x + dx, y + sy * 0.3, sobre)))
    for dx, sy in ((-0.22, -1), (0.75, 1)):
        formas.append(_mesa("Cup", "Cup.FBX", "Cup_texture.tif", 0.42, MADERAS + METAL, (x + dx, y + sy * 0.3, sobre),
                            rot=(0, 0, 90 * sy)))
    for dx in (-0.78, 0.8):
        formas.append(_mesa("Candle_1", "Candle_1.FBX", "Candles.tif", 0.7, ["blanco", "yeso3", "tela_cruda", "hierro", "luz", "fuego"],
                            (x + dx, y - 0.05, sobre)))
    return formas


# Giro de las sillas del paquete para que el respaldo quede hacia -Y con giro 0 (se calcula).
SILLA_GIRO = 180
