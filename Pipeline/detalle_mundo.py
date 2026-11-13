"""Nostos: naturaleza, ruinas, túnel y puente con el mismo nivel de detalle que los edificios."""

import math

from detalle import Det, PIEDRAS, HOJAS, bloques, fila_piedras, pendon, mata, mato

MALVAS = ["roca_malva", "roca_malva", "malva_claro", "roca_sombra"]


def penon(b, pos, tam, hierba=True, rz=0.0):
    """Bloque de roca anguloso con la cima plana y, si se quiere, un manto de hierba."""
    R = b.r
    x, y, z = pos
    w, d, h = tam
    b.push(pos=(x, y, z), rz=rz)
    lados = R.choice((5, 6, 7))
    m = min(w, d, h)
    b.cil(MALVAS, 0.56, h, (0, 0, h / 2 - 0.15), lados=lados, r2=0.56 * R.uniform(0.6, 0.8), esc=(w, d, 1),
          rot=(R.uniform(-6, 6), R.uniform(-6, 6), R.uniform(0, 60)), var=0.05, deform=m * 0.07, luz_z=True)
    # contrafuerte: un segundo bloque más bajo que rompe la silueta
    a = R.uniform(0, 6.28)
    b.cil(MALVAS, 0.5, h * 0.55, (math.cos(a) * w * 0.3, math.sin(a) * d * 0.3, h * 0.27 - 0.15), lados=5, r2=0.34, esc=(w * 0.8, d * 0.8, 1),
          rot=(R.uniform(-8, 8), R.uniform(-8, 8), R.uniform(0, 60)), var=0.05, deform=m * 0.06, luz_z=True)
    if hierba and min(w, d) > 0.9:
        b.cil(["hierba", "hierba_oscura"], 0.4, 0.14, (0, 0, h - 0.13), lados=lados, r2=0.34, esc=(w * 0.78, d * 0.78, 1),
              rot=(0, 0, R.uniform(0, 60)), var=0.04, deform=0.04)
    b.pop()


def arbol(nombre, alto_tronco, radio_copa, semilla):
    """Árbol de copa redonda y facetada, con ramas y raíces, iluminado de arriba abajo por tonos."""
    b = Det(nombre, semilla)
    R = b.r
    b.cil("tronco", 0.27, alto_tronco, (0, 0, alto_tronco / 2), lados=6, r2=0.16, var=0.03, luz_z=True)
    for k in range(4):
        a = k * 1.57 + R.uniform(-.4, .4)
        b.cil("tronco", 0.11, 0.6, (math.cos(a) * 0.26, math.sin(a) * 0.26, 0.12), lados=4,
              rot=(0, 58, math.degrees(a) + 180), r2=0.03, var=0.03)
    z = alto_tronco + radio_copa * 0.5
    b.bola(HOJAS, radio_copa, (0, 0, z), esc=(1, 1, 0.82), ruido=0.1, luz_z=True, sub=2)
    n = 5
    for k in range(n):
        a = math.radians(360 / n * k + R.uniform(-22, 22))
        d = radio_copa * R.uniform(0.6, 0.8)
        b.bola(HOJAS, radio_copa * R.uniform(0.5, 0.68),
               (math.cos(a) * d, math.sin(a) * d, z + R.uniform(-0.3, 0.45) * radio_copa), esc=(1, 1, 0.85), ruido=0.18, luz_z=True)
    b.bola("hoja_clara", radio_copa * 0.55, (R.uniform(-.2, .2), R.uniform(-.2, .2), z + radio_copa * 0.62), esc=(1, 1, 0.7), luz_z=True)
    return {nombre: b.formas}


def pino(nombre, alto, radio, semilla):
    """Pino de pisos escalonados, cada uno con su falda más oscura."""
    b = Det(nombre, semilla)
    R = b.r
    tronco = alto * 0.2
    b.cil("tronco", 0.22, tronco + 0.6, (0, 0, (tronco + 0.6) / 2), lados=6, r2=0.13, var=0.03, luz_z=True)
    pisos = 5
    z = tronco
    for k in range(pisos):
        t = k / (pisos - 1)
        r = radio * (1 - 0.72 * t)
        tramo = (alto - tronco) * (0.34 - 0.03 * k)
        giro = R.uniform(0, 50)
        b.cil("pino_oscuro", r * 0.92, 0.18, (0, 0, z + 0.05), lados=7, rot=(0, 0, giro), r2=r * 0.5, var=0.03)
        b.cil(["pino", "pino", "pino_claro"] if k < pisos - 1 else "pino_claro", r, tramo, (R.uniform(-.05, .05), R.uniform(-.05, .05), z + tramo / 2 + 0.1),
              lados=7, rot=(R.uniform(-3, 3), R.uniform(-3, 3), giro), r2=0.0 if k == pisos - 1 else r * 0.12, var=0.05)
        z += tramo * 0.56
    return {nombre: b.formas}


def penasco(nombre, semilla):
    """Peñasco de bloques de roca malva con hierba encima, como los de la hoja de referencia."""
    b = Det(nombre, semilla)
    R = b.r
    penon(b, (0, 0, 0), (2.2, 1.9, 1.7), rz=R.uniform(0, 90))
    penon(b, (1.5, 0.5, 0), (1.5, 1.4, 1.0), rz=R.uniform(0, 90))
    penon(b, (-1.3, -0.7, 0), (1.2, 1.1, 0.75), rz=R.uniform(0, 90))
    for _ in range(4):
        a = R.uniform(0, 6.28)
        d = R.uniform(1.9, 2.6)
        penon(b, (math.cos(a) * d, math.sin(a) * d, 0), (R.uniform(.35, .6), R.uniform(.3, .5), R.uniform(.3, .45)), hierba=False, rz=R.uniform(0, 90))
    for _ in range(5):
        a = R.uniform(0, 6.28)
        mata(b, (math.cos(a) * 2.2, math.sin(a) * 2.0, 0))
    mato(b, (-1.9, 0.9, 0), 0.38)
    return {nombre: b.formas}


def arbusto():
    """Arbusto de bayas (comida)."""
    b = Det("arbusto")
    R = b.r
    mato(b, (0, 0, 0), 0.62)
    mato(b, (0.5, 0.25, 0), 0.42)
    for _ in range(12):
        a = R.uniform(0, 6.28)
        d = R.uniform(0.15, 0.62)
        b.box(["flor2", "flor2", "fuego"], (0.1, 0.1, 0.1), (math.cos(a) * d + 0.1, math.sin(a) * d + 0.05, R.uniform(0.5, 0.86)),
              rot=(R.uniform(0, 45), R.uniform(0, 45), 0), var=0.03)
    return {"arbusto": b.formas}


def _arco(b, radio, zc, fondo, grosor, desde=0.0, hasta=math.pi, n=13, y=0.0):
    """Dovelas de un arco de medio punto en el plano XZ, de `desde` a `hasta` (radianes desde +X)."""
    for i in range(n):
        a = math.pi * (i + 0.5) / n
        if a < desde or a > hasta:
            continue
        clave = i == n // 2
        rr = radio + grosor / 2
        b.box("piedra3" if clave else PIEDRAS,
              (grosor * (1.18 if clave else 1.0), fondo * (1.06 if clave else 1.0), math.pi * rr / n * 1.02),
              (math.cos(a) * rr, y, zc + math.sin(a) * rr), rot=(0, 180 - math.degrees(a), 0), bisel=0.05, var=0.08, deform=0.015)


def tunel():
    """Boca del túnel que une el valle con la civilización. La entrada mira a -Y (en Unity, a +Z)."""
    b = Det("tunel")
    R = b.r
    oscuro = "sombra"
    b.box("arena", (5.4, 11.0, 0.12), (0, 2.6, 0.04), var=0)
    for i in range(16):
        b.box(PIEDRAS, (R.uniform(.5, .9), R.uniform(.5, .8), 0.08), (R.uniform(-2.2, 2.2), -2.4 + i * 0.4, 0.09), rot=(0, 0, R.uniform(0, 90)), bisel=0.025, deform=0.01)
    b.box(oscuro, (0.3, 8.0, 5.6), (-2.85, 4.6, 2.8), var=0)
    b.box(oscuro, (0.3, 8.0, 5.6), (2.85, 4.6, 2.8), var=0)
    b.box(oscuro, (6.0, 8.0, 0.3), (0, 4.6, 6.2), var=0)
    b.box(oscuro, (6.0, 0.3, 6.4), (0, 8.4, 3.1), var=0)
    # Pilas de sillares, arco de dovelas y frontón.
    for sx in (-1, 1):
        bloques(b, sx * 3.55 - 0.85, sx * 3.55 + 0.85, 0.45, 6.0, 0, 2.2, alto=0.55, minimo=0.6, maximo=1.1)
        fila_piedras(b, sx * 3.55 - 1.05, sx * 3.55 + 1.05, 0, 0, 0.5, 2.6, minimo=0.6, maximo=1.1)
        fila_piedras(b, sx * 3.55 - 1.0, sx * 3.55 + 1.0, 0, 5.95, 0.3, 2.45, minimo=0.6, maximo=1.0)
        b.push(pos=(sx * 3.55, -1.13, 5.5))
        pendon(b, largo=2.5, ancho=0.9)
        b.pop()
        # farol de pie junto a la boca
        b.box("hierro", (0.1, 0.1, 2.2), (sx * 2.45, -1.5, 1.1))
        b.box("luz", (0.26, 0.26, 0.34), (sx * 2.45, -1.5, 2.36), var=0)
        b.box("hierro", (0.36, 0.36, 0.06), (sx * 2.45, -1.5, 2.56))
        for k in range(3):
            b.box("madera2", (0.1, 0.1, 0.5), (sx * 2.55, 2.0 + k * 2.2, 2.5))
            b.box("fuego", (0.18, 0.18, 0.22), (sx * 2.55, 2.0 + k * 2.2, 2.85), var=0)
    _arco(b, 2.7, 3.4, 2.3, 0.8, n=13)
    bloques(b, -4.4, 4.4, 6.3, 7.7, 0.15, 1.9, alto=0.5, minimo=0.7, maximo=1.3)
    for sx in (-1, 1):
        b.box(PIEDRAS, (1.3, 1.9, 1.3), (sx * 2.45, 0.15, 5.85), rot=(0, 45, 0), bisel=0.05, var=0.07)
    fila_piedras(b, -4.75, 4.75, 0.05, 7.68, 0.42, 2.5, minimo=0.7, maximo=1.3)
    # La montaña alrededor: grandes bloques con hierba.
    for (x, y, z, w, d, h, rz) in ((-8.0, 4.0, 0, 7.5, 8.5, 7.5, 8), (8.0, 4.0, 0, 7.5, 8.5, 6.8, -6), (0, 6.2, 7.2, 10.5, 7.5, 5.0, 3),
                                   (-6.2, 5.2, 6.4, 5.5, 6.5, 4.6, 20), (6.4, 5.4, 6.0, 5.5, 6.5, 4.2, -18),
                                   (-11.5, 2.5, 0, 4.0, 4.5, 3.6, 30), (11.8, 2.2, 0, 4.2, 4.2, 3.0, 12),
                                   (-6.3, -1.0, 0, 2.2, 2.0, 1.5, 40), (6.4, -1.2, 0, 1.8, 1.7, 1.2, 15)):
        penon(b, (x, y, z), (w, d, h), rz=rz)
    for _ in range(8):
        sx = R.choice((-1, 1))
        mata(b, (sx * R.uniform(4.6, 7.5), R.uniform(-2.6, -1.3), 0))
    mato(b, (-5.0, -1.9, 0), 0.5)
    mato(b, (5.2, -2.1, 0), 0.45, flores=True)
    mato(b, (-3.9, 0.3, 7.9), 0.5)
    mato(b, (3.3, 0.2, 7.9), 0.4)
    return {"tunel": b.formas}


def _cascotes(b, puntos):
    R = b.r
    for x, y in puntos:
        b.box(PIEDRAS, (R.uniform(.5, .9), R.uniform(.4, .7), R.uniform(.35, .5)), (x, y, 0.18),
              rot=(R.uniform(-8, 8), R.uniform(-8, 8), R.uniform(0, 90)), bisel=0.05, var=0.08, deform=0.02)


def ruina_arco():
    """Arco en ruinas de la vieja calzada: dos pilas de sillares, medio arco y cascotes."""
    b = Det("ruina_arco")
    for sx, alto, roto in ((-1, 4.6, 0.0), (1, 3.4, 0.5)):
        fila_piedras(b, sx * 1.9 - 0.78, sx * 1.9 + 0.78, 0, 0, 0.42, 1.55, minimo=0.5, maximo=0.8)
        bloques(b, sx * 1.9 - 0.58, sx * 1.9 + 0.58, 0.4, alto, 0, 1.12, alto=0.46, roto=roto, minimo=0.5, maximo=0.75)
    fila_piedras(b, -2.6, -1.2, 0, 4.58, 0.26, 1.3, minimo=0.5, maximo=0.8)
    _arco(b, 1.32, 4.84, 1.1, 0.6, desde=math.pi * 0.42, n=11)
    bloques(b, -2.5, -0.9, 5.5, 6.9, 0, 1.0, alto=0.46, roto=0.6, minimo=0.5, maximo=0.8)
    b.push(pos=(-1.9, -0.58, 4.2))
    pendon(b, largo=2.0, ancho=0.62)
    b.pop()
    _cascotes(b, ((0.6, -1.3), (3.2, 0.9), (-0.4, 1.2), (1.2, 0.9), (2.9, -1.1)))
    mato(b, (-2.95, -0.7, 0), 0.45)
    mato(b, (2.9, 0.2, 0), 0.4, flores=True)
    for p in ((-1.0, -1.0, 0), (0.9, -0.2, 0), (-2.6, 0.9, 0), (2.2, 1.4, 0)):
        mata(b, p)
    return {"ruina_arco": b.formas}


def columna_rota():
    b = Det("columna_rota")
    R = b.r
    fila_piedras(b, -0.7, 0.7, 0, 0, 0.34, 1.35, minimo=0.6, maximo=0.8)
    b.box("piedra3", (1.02, 1.02, 0.3), (0, 0, 0.48), bisel=0.05)
    z = 0.63
    for i in range(5):
        h = R.uniform(0.38, 0.5)
        b.cil(PIEDRAS, 0.42 - i * 0.008, h - 0.02, (R.uniform(-.02, .02), R.uniform(-.02, .02), z + h / 2), lados=8, rot=(R.uniform(-2, 2), 0, R.uniform(0, 40)), var=0.07, luz_z=True)
        z += h
    b.cil("piedra2", 0.36, 0.28, (0.05, 0, z + 0.08), lados=8, rot=(14, 6, 0), r2=0.2, var=0.05, luz_z=True)
    for i, (x, y, rz) in enumerate(((1.5, 0.4, 35), (2.1, 0.85, 31), (1.2, -1.0, 100))):
        b.cil(PIEDRAS, 0.4, 0.62, (x, y, 0.36), lados=8, rot=(90, 0, rz), var=0.07, luz_z=True)
    b.box("piedra3", (0.95, 0.95, 0.26), (2.5, 1.5, 0.14), rot=(6, 0, 20), bisel=0.05)
    b.cil("piedra", 0.5, 0.22, (2.5, 1.5, 0.36), lados=8, rot=(6, 0, 20), r2=0.4)
    mato(b, (-0.95, 0.55, 0), 0.42)
    for p in ((0.8, -0.6, 0), (-0.6, -0.9, 0), (1.9, -0.2, 0)):
        mata(b, p)
    return {"columna_rota": b.formas}


def obelisco():
    b = Det("obelisco")
    for lado, z, h in ((2.5, 0.0, 0.36), (1.9, 0.36, 0.34)):
        for rz in (0, 90, 180, 270):
            b.push(rz=rz)
            fila_piedras(b, -lado / 2, lado / 2, -lado / 2 + 0.3, z, h, 0.62, minimo=0.5, maximo=0.8)
            b.pop()
        b.box("piedra2", (lado - 0.8, lado - 0.8, h - 0.03), (0, 0, z + h / 2), var=0)
    b.box("piedra3", (1.22, 1.22, 0.92), (0, 0, 1.15), bisel=0.06)
    for rz in (0, 90, 180, 270):
        b.push(rz=rz)
        b.box("azul", (0.5, 0.05, 0.5), (0, -0.6, 1.15), var=0.02)
        b.box("blanco", (0.2, 0.06, 0.2), (0, -0.61, 1.15), rot=(0, 45, 0), var=0)
        b.pop()
    b.box("piedra", (1.4, 1.4, 0.2), (0, 0, 1.7), bisel=0.05)
    z = 1.8
    r = 0.64
    for i in range(4):
        h = 1.05
        r2 = r - 0.07
        b.cil(PIEDRAS, r, h - 0.03, (0, 0, z + h / 2), lados=4, rot=(0, 0, 45), r2=r2, var=0.06)
        z += h
        r = r2
    b.cil("piedra3", r + 0.05, 0.12, (0, 0, z + 0.05), lados=4, rot=(0, 0, 45))
    b.cil("luz", r, 0.7, (0, 0, z + 0.45), lados=4, rot=(0, 0, 45), r2=0.0, var=0)
    b.push(pos=(0, -0.44, 4.9))
    pendon(b, largo=1.6, ancho=0.4)
    b.pop()
    for p in ((1.5, -1.3, 0), (-1.5, 1.2, 0), (-1.4, -1.4, 0)):
        mata(b, p)
    mato(b, (1.45, 1.3, 0), 0.4, flores=True)
    return {"obelisco": b.formas}


def puente():
    """Puente de madera sobre el río, a lo largo del eje X, con estribos de sillería."""
    b = Det("puente")
    R = b.r
    largo = 14.0
    for sy in (-1, 0, 1):
        b.box("madera2", (largo, 0.22, 0.24), (0, sy * 1.2, 0.0), bisel=0.03)
    n = 38
    for i in range(n):
        x = -largo / 2 + (i + 0.5) * largo / n
        b.box(["madera", "madera3", "madera3", "ocre_oscuro"], (largo / n - 0.02, 3.0 + R.uniform(-.08, .08), 0.1), (x, R.uniform(-.03, .03), 0.17 + R.uniform(0, .012)),
              rot=(0, 0, R.uniform(-1.2, 1.2)), var=0.07)
    for sy in (-1, 1):
        b.box("madera2", (largo, 0.12, 0.14), (0, sy * 1.42, 1.08), bisel=0.02)
        b.box("madera", (largo, 0.08, 0.1), (0, sy * 1.42, 0.66))
        for x in (-6.6, -3.3, 0, 3.3, 6.6):
            b.box("madera", (0.2, 0.2, 1.25), (x, sy * 1.42, 0.62), bisel=0.03)
            b.box("madera2", (0.28, 0.28, 0.08), (x, sy * 1.42, 1.27))
        for x in (-3.0, 3.0):
            b.box("madera2", (0.32, 0.32, 1.9), (x, sy * 1.15, -0.9), bisel=0.04)
            b.box("madera", (0.14, 0.14, 1.9), (x + 0.7, sy * 1.15, -0.75), rot=(0, 42, 0))
    for sx in (-1, 1):
        b.push(pos=(sx * (largo / 2 - 0.4), 0, -1.2))
        bloques(b, -0.75, 0.75, 0, 1.28, 0, 3.9, alto=0.44, minimo=0.6, maximo=0.8)
        b.pop()
        for sy in (-1, 1):
            b.box("piedra3", (0.5, 0.5, 1.5), (sx * (largo / 2 - 0.3), sy * 1.6, 0.6), bisel=0.06, deform=0.015)
            b.box("piedra", (0.62, 0.62, 0.16), (sx * (largo / 2 - 0.3), sy * 1.6, 1.4), bisel=0.04)
    return {"puente": b.formas}


# ---------------------------------------------------------------- caminos

def camino(nombre, variante):
    """Losa de camino de 2 x 2 m (una celda): tierra apisonada con losas de piedra en una rejilla
    ordenada de 4 x 4. Las juntas son iguales en el borde y en el centro, para que las celdas
    vecinas casen sin costura. Origen: centro de la celda, en el suelo."""
    b = Det("camino", variante)
    b.caja("tierra_clara", -1.0, 1.0, -1.0, 1.0, 0.0, 0.02, var=0)
    lado, junta = 0.5, 0.05
    tonos = [["piedra3", "piedra", "piedra3", "piedra3"], ["piedra", "piedra3", "piedra3", "piedra"],
             ["piedra3", "piedra3", "piedra", "piedra3"], ["piedra3", "piedra", "piedra3", "piedra2"]]
    for i in range(4):
        for j in range(4):
            x0 = -1.0 + i * lado + junta / 2
            y0 = -1.0 + j * lado + junta / 2
            alto = 0.045 + 0.008 * ((i * 3 + j * 5 + variante) % 3)
            color = tonos[(i + variante) % 4][(j + 2 * variante) % 4]
            b.caja(color, x0, x0 + lado - junta, y0, y0 + lado - junta, 0.0, alto, bisel=0.012, var=0.02)
    return {nombre: b.formas}
