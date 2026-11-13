"""Nostos: interiores detallados de los edificios. Cada función devuelve (formas, formas móviles, centro del móvil)."""

import math

from detalle import Det, LOSA, PARED, Z_MURO, PIEDRAS


def cama(b, manta, hecha=True):
    """Cama de madera. Origen: centro del cabecero en el suelo; la cama se extiende hacia +Y."""
    largo, a = 1.95, 0.405
    for sx in (-1, 1):
        b.caja("madera2", sx * a - 0.045, sx * a + 0.045, 0.0, 0.09, 0.0, 0.95, bisel=0.015, var=0)
        b.caja("madera2", sx * a - 0.045, sx * a + 0.045, largo - 0.09, largo, 0.0, 0.6, bisel=0.015, var=0)
        b.caja("madera", sx * a - 0.02, sx * a + 0.02, 0.09, largo - 0.09, 0.26, 0.4, var=0)
    for i in range(3):
        b.caja(["madera", "madera3"], -a + 0.045, a - 0.045, 0.025, 0.065, 0.38 + i * 0.18, 0.38 + i * 0.18 + 0.165, var=0.04)
    b.caja("madera", -a + 0.045, a - 0.045, largo - 0.065, largo - 0.025, 0.3, 0.54, var=0)
    b.caja("madera3", -a + 0.02, a - 0.02, 0.09, largo - 0.09, 0.3, 0.34, var=0)
    if not hecha:
        return
    b.caja("yeso3", -a + 0.012, a - 0.012, 0.1, largo - 0.1, 0.34, 0.5, bisel=0.04, var=0)
    b.box("blanco", (0.56, 0.32, 0.11), (0, 0.33, 0.55), rot=(6, 0, b.r.uniform(-4, 4)), bisel=0.04, var=0)
    b.caja(manta, -a - 0.008, a + 0.008, 0.78, largo - 0.085, 0.345, 0.525, bisel=0.03, var=0)
    b.caja("blanco", -a - 0.012, a + 0.012, 0.66, 0.8, 0.5, 0.54, bisel=0.015, var=0)


def arcon(b):
    """Arcón con herrajes. Origen: centro de la base."""
    b.caja("madera3", -0.35, 0.35, -0.21, 0.21, 0.0, 0.34, bisel=0.02, var=0)
    b.caja("madera", -0.37, 0.37, -0.23, 0.23, 0.34, 0.45, bisel=0.03, var=0)
    for x in (-0.22, 0.22):
        b.caja("hierro", x - 0.025, x + 0.025, -0.24, 0.24, 0.02, 0.46, var=0)
    b.caja("luz", -0.035, 0.035, -0.245, -0.21, 0.24, 0.33, var=0)


def taburete(b):
    b.cil("madera3", 0.17, 0.05, (0, 0, 0.42), lados=10, var=0)
    for i in range(3):
        a = i * 2.094 + 0.5
        b.cil("madera2", 0.025, 0.42, (math.cos(a) * 0.1, math.sin(a) * 0.1, 0.2), lados=5,
              rot=(-math.sin(a) * 12, math.cos(a) * 12, 0), var=0)


def hogar(b, encendido=True, alto_tiro=3.2):
    """Chimenea de piedra con campana de yeso. Origen: centro de la base contra la pared; mira a +Y."""
    b.caja("piedra2", -0.56, 0.56, 0.0, 0.78, 0.0, 0.08, bisel=0.02, var=0.02)
    for sx in (-1, 1):
        for i in range(3):
            ancho = 0.22 if i % 2 == 0 else 0.18
            x0, x1 = 0.31, 0.31 + ancho
            if sx < 0:
                x0, x1 = -x1, -x0
            b.caja(PIEDRAS, x0, x1, 0.0, 0.46 if i % 2 == 0 else 0.43, 0.08 + i * 0.34 + 0.005, 0.08 + (i + 1) * 0.34 - 0.005,
                   bisel=0.03, var=0.07)
    b.caja("sombra", -0.32, 0.32, 0.0, 0.05, 0.08, 1.1, var=0)
    if not encendido:
        return
    b.caja("madera", -0.58, 0.58, 0.0, 0.54, 1.1, 1.25, bisel=0.025, var=0)
    b.prisma("yeso2", [(-0.52, 1.25), (0.52, 1.25), (0.3, 2.3), (-0.3, 2.3)], 0.0, 0.42, var=0)
    b.caja("yeso2", -0.3, 0.3, 0.0, 0.36, 2.3, alto_tiro, var=0)
    # leña, llamas y morillos
    for i, (x, y, rz) in enumerate(((-0.08, 0.26, 80), (0.1, 0.2, 100), (0.0, 0.3, 60))):
        b.cil("tronco", 0.055, 0.5, (x, y, 0.14 + i * 0.045), lados=6, rot=(90, 0, rz), var=0.04)
    for k, (x, y, h, color) in enumerate(((-0.07, 0.24, 0.34, "fuego"), (0.08, 0.26, 0.26, "fuego"), (0.0, 0.22, 0.2, "luz"))):
        b.cil(color, 0.085, h, (x, y, 0.23 + k * 0.012 + h / 2), lados=5, r2=0.0, rot=(0, 0, b.r.uniform(0, 60)), var=0)
    for sx in (-1, 1):
        b.caja("hierro", sx * 0.24 - 0.015, sx * 0.24 + 0.015, 0.1, 0.42, 0.1, 0.13, var=0)
        b.caja("hierro", sx * 0.24 - 0.015, sx * 0.24 + 0.015, 0.39, 0.42, 0.08, 0.34, var=0)
    # olla colgada y cosas en la repisa
    b.caja("hierro", -0.012, 0.012, 0.2, 0.224, 0.72, 1.1, var=0)
    b.cil("cobre", 0.14, 0.17, (0, 0.212, 0.64), lados=10, r2=0.1, rot=(180, 0, 0), var=0)
    b.cil("hierro", 0.15, 0.025, (0, 0.212, 0.735), lados=10, var=0)
    # delante de la campana (que llega a y = 0.42), sobre el borde de la repisa
    b.cil("blanco", 0.028, 0.14, (-0.42, 0.48, 1.32), lados=6, var=0)
    b.cil("luz", 0.022, 0.07, (-0.42, 0.48, 1.425), lados=5, r2=0.0, var=0)
    b.cil("teja2", 0.048, 0.16, (0.38, 0.48, 1.33), lados=8, r2=0.035, var=0)
    b.cil("cobre_verdin", 0.045, 0.035, (0.08, 0.48, 1.268), lados=10, r2=0.055, var=0)


def repisa(b, largo=0.8):
    """Balda de pared con tarros y libros. Origen: centro de la balda contra la pared; mira a +Y."""
    a = largo / 2
    b.caja("madera", -a, a, 0.0, 0.2, 0.0, 0.035, bisel=0.01, var=0)
    for sx in (-1, 1):
        b.prisma("madera2", [(0.0, 0.0), (0.16, 0.0), (0.0, -0.16)], sx * (a - 0.12) - 0.018, sx * (a - 0.12) + 0.018, var=0)
    b.cil("teja", 0.06, 0.14, (-a + 0.12, 0.1, 0.105), lados=8, r2=0.045, var=0)
    b.cil("cobre_verdin", 0.045, 0.1, (-a + 0.27, 0.11, 0.085), lados=8, var=0)
    for i, (color, alto) in enumerate((("azul", 0.2), ("flor2", 0.17), ("ocre_claro", 0.19))):
        b.caja(color, a - 0.3 + i * 0.05, a - 0.3 + i * 0.05 + 0.042, 0.03, 0.17, 0.035, 0.035 + alto, var=0)
    b.cil("blanco", 0.07, 0.04, (0.02, 0.1, 0.055), lados=10, r2=0.1, var=0)


def alfombra(b, ancho=1.2, largo=1.7):
    """Alfombra de franjas. Origen: centro, sobre el suelo."""
    franjas = [("teja3", 0.14), ("yeso3", 0.05), ("azul2", 0.16), ("yeso3", 0.05), ("teja3", 0.2),
               ("yeso3", 0.05), ("azul2", 0.16), ("yeso3", 0.05), ("teja3", 0.14)]
    total = sum(f[1] for f in franjas)
    y = -largo / 2
    for color, peso in franjas:
        l = largo * peso / total
        b.caja(color, -ancho / 2, ancho / 2, y, y + l, 0.0, 0.015, var=0)
        y += l


def lampara(b, largo=0.5):
    """Lámpara colgante. Origen: punto de cuelgue; cuelga hacia -Z."""
    b.caja("hierro", -0.01, 0.01, -0.01, 0.01, -largo, 0.0, var=0)
    b.cil("hierro", 0.12, 0.05, (0, 0, -largo - 0.025), lados=6, r2=0.04, var=0)
    b.cil("luz", 0.07, 0.16, (0, 0, -largo - 0.13), lados=6, var=0)
    b.cil("hierro", 0.09, 0.025, (0, 0, -largo - 0.222), lados=6, var=0)


def maquina_casa(w, d, montada):
    """La casa: dos camas, hogar de piedra, arcón, alfombra, repisa y lámpara."""
    b = Det("casa", "interior")
    xi, yi = w / 2 - PARED, d / 2 - PARED
    b.push(pos=(-xi + 0.5, -yi + 0.07, LOSA))
    cama(b, "azul", montada)
    b.pop()
    b.push(pos=(xi - 0.5, yi - 0.14, LOSA), rz=180)
    cama(b, "teja", montada)
    b.pop()
    b.push(pos=(xi - 0.6, -yi, LOSA))
    hogar(b, montada, alto_tiro=3.2)
    b.pop()
    if montada:
        b.push(pos=(-xi + 0.42, yi - 0.55, LOSA), rz=90)
        arcon(b)
        b.pop()
        b.push(pos=(0, 0.1, LOSA))
        alfombra(b)
        b.pop()
        b.push(pos=(0.15, -yi + 0.75, LOSA))
        taburete(b)
        b.pop()
        b.push(pos=(-xi, yi - 0.6, LOSA + 1.55), rz=270)
        repisa(b)
        b.pop()
        b.push(pos=(0, 0.1, Z_MURO + 0.02))
        lampara(b)
        b.pop()
    return b.formas, [], None


def mesa(b, largo=1.3, ancho=0.72):
    """Mesa de tablas con dos bancos. Origen: centro, en el suelo; el largo va en X."""
    a, f = largo / 2, ancho / 2
    for k in range(4):
        y0 = -f + k * ancho / 4
        b.caja(["madera", "madera3"], -a, a, y0 + 0.004, y0 + ancho / 4 - 0.004, 0.72, 0.77, bisel=0.008, var=0.04)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.caja("madera2", sx * (a - 0.1) - 0.04, sx * (a - 0.1) + 0.04, sy * (f - 0.08) - 0.04, sy * (f - 0.08) + 0.04, 0.0, 0.72, var=0)
        b.caja("madera2", sx * (a - 0.1) - 0.03, sx * (a - 0.1) + 0.03, -f + 0.08, f - 0.08, 0.16, 0.22, var=0)
    b.caja("madera2", -a + 0.1, a - 0.1, -0.03, 0.03, 0.16, 0.22, var=0)
    for sy in (-1, 1):
        y = sy * (f + 0.28)
        b.caja("madera", -a + 0.05, a - 0.05, y - 0.13, y + 0.13, 0.42, 0.46, bisel=0.008, var=0)
        for sx in (-1, 1):
            b.caja("madera2", sx * (a - 0.18) - 0.035, sx * (a - 0.18) + 0.035, y - 0.1, y + 0.1, 0.0, 0.42, var=0)
    # encima: pan, jarra y dos cuencos
    b.cil("ocre_claro", 0.11, 0.08, (-0.25, 0.05, 0.81), lados=8, esc=(1.4, 1, 1), var=0)
    b.cil("teja2", 0.07, 0.2, (0.2, -0.08, 0.87), lados=8, r2=0.05, var=0)
    for x in (-0.45, 0.45):
        b.cil("blanco", 0.08, 0.05, (x, 0.12, 0.795), lados=10, r2=0.1, var=0)


def maquina_casa_dos_plantas(w, d, montada):
    """La casa de dos plantas: abajo el hogar, la mesa y el arcón; arriba dos camas, arcón y balda.
    La escalera y el suelo de arriba van en la estructura (detalle.forjado_y_escalera)."""
    import detalle as D
    b = Det("casa", "interior")
    xi, yi = w / 2 - PARED, d / 2 - PARED
    zp = D.Z_PLANTA
    # Hogar contra el lateral derecho; el tiro sube hasta el suelo de arriba y sigue por la planta alta.
    b.push(pos=(xi, 0.0, LOSA), rz=90)
    hogar(b, montada, alto_tiro=zp - 0.05 - LOSA)
    b.pop()
    if montada:
        # el tiro, por la planta de arriba hasta meterse en el tejado
        zc = D.Z_MURO + D.PILAR + (d / 2) * math.tan(math.radians(38 if d < 5 else 33))
        b.caja("yeso2", xi - 0.36, xi, -0.3, 0.3, zp, zc - 0.25, var=0)
        b.caja("madera", xi - 0.4, xi - 0.005, -0.36, 0.36, zp + 1.7, zp + 1.78, bisel=0.01, var=0)
    if not montada:
        return b.formas, [], None
    # abajo
    b.push(pos=(-1.75, 0.55, LOSA), rz=90)
    mesa(b, largo=1.1)
    b.pop()
    b.push(pos=(xi - 0.45, yi - 0.28, LOSA), rz=180)
    arcon(b)
    b.pop()
    b.push(pos=(0.95, 0.25, LOSA), rz=90)
    alfombra(b, ancho=1.0, largo=1.5)
    b.pop()
    b.push(pos=(1.55, 0.75, LOSA))
    taburete(b)
    b.pop()
    b.push(pos=(-1.75, 0.55, zp - 0.05))
    lampara(b, largo=0.7)
    b.pop()
    # arriba
    b.push(pos=(-xi, 0.85, zp), rz=-90)
    cama(b, "azul", True)
    b.pop()
    b.push(pos=(xi, 0.85, zp), rz=90)
    cama(b, "teja", True)
    b.pop()
    b.push(pos=(0.0, yi - 0.25, zp), rz=180)
    arcon(b)
    b.pop()
    b.push(pos=(-0.85, yi, zp + 1.45), rz=180)
    repisa(b)
    b.pop()
    b.push(pos=(0.0, 0.45, zp), rz=90)
    alfombra(b, ancho=0.8, largo=1.1)
    b.pop()
    return b.formas, [], None


# ---------------------------------------------------------------- banco de trabajo

def herramienta_martillo(b):
    """Martillo colgado. Origen: punto de cuelgue; cuelga hacia -Z, plano en XZ."""
    b.caja("madera3", -0.018, 0.018, -0.012, 0.012, -0.34, 0.0, var=0)
    b.caja("hierro", -0.07, 0.07, -0.022, 0.022, -0.40, -0.34, bisel=0.008, var=0)


def herramienta_sierra(b):
    """Serrucho colgado: hoja trapezoidal y mango."""
    b.prisma("blanco", [(-0.05, 0.0), (0.06, 0.0), (0.03, -0.46), (-0.05, -0.46)], -0.004, 0.004, var=0)
    b.caja("madera", -0.06, 0.07, -0.016, 0.016, -0.02, 0.09, bisel=0.01, var=0)


def herramienta_formon(b):
    b.caja("madera3", -0.016, 0.016, -0.012, 0.012, -0.14, 0.0, var=0)
    b.caja("hierro", -0.012, 0.012, -0.006, 0.006, -0.30, -0.14, var=0)


def herramienta_tenaza(b):
    for s in (-1, 1):
        b.box("hierro", (0.016, 0.012, 0.3), (s * 0.02, 0, -0.15), rot=(0, s * 6, 0), var=0)


def maquina_banco(w, d, montada):
    """Banco de trabajo: mesa con tornillo, tablero de herramientas en la pared del fondo,
    tajo con hacha, muela de afilar (gira al trabajar) y una caja de mangos terminados."""
    b = Det("banco", "interior")
    xi, yi = w / 2 - PARED, d / 2 - PARED
    alto = 0.86
    # Mesa contra la pared del fondo, a la izquierda (la puerta queda libre en el centro de +Y).
    x0, x1 = -xi + 0.08, -xi + 1.78
    y0, y1 = -yi + 0.06, -yi + 0.76
    b.caja("madera3", x0, x1, y0, y1, LOSA + alto - 0.08, LOSA + alto, bisel=0.015, var=0.03)
    for x in (x0 + 0.08, x1 - 0.08):
        for y in (y0 + 0.08, y1 - 0.08):
            b.caja("madera2", x - 0.045, x + 0.045, y - 0.045, y + 0.045, LOSA, LOSA + alto - 0.08, var=0)
    # Travesaño y balda inferior.
    b.caja("madera2", x0 + 0.125, x1 - 0.125, y0 + 0.06, y0 + 0.1, LOSA + 0.18, LOSA + 0.26, var=0)
    if montada:
        b.caja("madera", x0 + 0.12, x1 - 0.12, y0 + 0.12, y1 - 0.12, LOSA + 0.26, LOSA + 0.29, var=0)
        # Tablas a medio cepillar sobre la mesa.
        b.caja("madera3", x0 + 0.25, x0 + 1.05, y0 + 0.3, y0 + 0.48, LOSA + alto, LOSA + alto + 0.04, bisel=0.008, var=0)
        b.caja("madera3", x0 + 0.3, x0 + 1.1, y0 + 0.5, y0 + 0.62, LOSA + alto, LOSA + alto + 0.04, bisel=0.008, var=0)
        # Tornillo de banco en la esquina derecha.
        b.caja("hierro", x1 - 0.32, x1 - 0.06, y1 - 0.1, y1 + 0.02, LOSA + alto, LOSA + alto + 0.16, bisel=0.01, var=0)
        b.caja("hierro", x1 - 0.32, x1 - 0.06, y1 + 0.06, y1 + 0.12, LOSA + alto - 0.02, LOSA + alto + 0.16, bisel=0.01, var=0)
        b.cil("hierro", 0.015, 0.3, (x1 - 0.19, y1 + 0.2, LOSA + alto + 0.06), lados=6, rot=(90, 0, 0), var=0)
        b.caja("madera3", x1 - 0.3, x1 - 0.08, y1 + 0.33, y1 + 0.36, LOSA + alto + 0.045, LOSA + alto + 0.075, var=0)
        # Tablero de herramientas en la pared del fondo, a la izquierda de la ventana (que está en el centro).
        ty = -yi + 0.012
        b.caja("madera", x0 + 0.04, -0.66, ty, ty + 0.025, LOSA + 1.15, LOSA + 1.85, bisel=0.008, var=0)
        clavos = [(x0 + 0.2, herramienta_martillo), (x0 + 0.42, herramienta_sierra), (x0 + 0.62, herramienta_formon),
                  (x0 + 0.76, herramienta_tenaza)]
        for x, pieza in clavos:
            b.caja("hierro", x - 0.008, x + 0.008, ty + 0.025, ty + 0.07, LOSA + 1.765, LOSA + 1.78, var=0)
            b.push(pos=(x, ty + 0.06, LOSA + 1.76))
            pieza(b)
            b.pop()
        # Tajo con el hacha clavada, a la derecha de la puerta (vista desde dentro).
        tx, ty2 = xi - 0.55, yi - 0.75
        b.cil("tronco", 0.24, 0.5, (tx, ty2, LOSA + 0.25), lados=10, var=0)
        b.cil("madera3", 0.235, 0.012, (tx, ty2, LOSA + 0.506), lados=10, var=0)
        b.box("hierro", (0.03, 0.16, 0.12), (tx + 0.02, ty2, LOSA + 0.56), rot=(0, 18, 0), var=0)
        b.box("madera3", (0.035, 0.035, 0.62), (tx + 0.12, ty2, LOSA + 0.84), rot=(0, 18, 0), var=0)
        # Caja con mangos y herramientas terminadas, junto a la pared derecha.
        cx, cy = xi - 0.35, -yi + 1.55
        b.caja("madera", cx - 0.26, cx + 0.26, cy - 0.2, cy + 0.2, LOSA, LOSA + 0.36, bisel=0.015, var=0.03)
        b.caja("madera2", cx - 0.22, cx + 0.22, cy - 0.16, cy + 0.16, LOSA + 0.36, LOSA + 0.366, var=0)
        for i in range(4):
            b.box("madera3", (0.035, 0.035, 0.55), (cx - 0.12 + i * 0.08, cy - 0.04 + (i % 2) * 0.06, LOSA + 0.5),
                  rot=(8 - i * 5, -10 + i * 6, 0), var=0)
        b.box("hierro", (0.13, 0.04, 0.05), (cx - 0.12, cy - 0.04, LOSA + 0.79), rot=(8, -10, 0), var=0)
    # Bastidor de la muela de afilar, a la derecha de la mesa.
    mx, my = xi - 0.55, -yi + 0.5
    for s in (-1, 1):
        b.caja("madera2", mx + s * 0.16 - 0.03, mx + s * 0.16 + 0.03, my - 0.25, my + 0.25, LOSA, LOSA + 0.07, var=0)
        b.caja("madera2", mx + s * 0.16 - 0.03, mx + s * 0.16 + 0.03, my - 0.03, my + 0.03, LOSA + 0.07, LOSA + 0.72, var=0)
    if not montada:
        return b.formas, [], None
    # Pedal y artesa de agua bajo la muela.
    b.caja("madera3", mx - 0.12, mx + 0.12, my - 0.18, my + 0.18, LOSA + 0.07, LOSA + 0.22, var=0)
    b.caja("agua", mx - 0.1, mx + 0.1, my - 0.16, my + 0.16, LOSA + 0.22, LOSA + 0.225, var=0)
    centro = (mx, my, LOSA + 0.76)
    m = Det("banco", "muela")
    m.cil("piedra2", 0.3, 0.1, (0, 0, 0), lados=14, rot=(0, 90, 0), var=0)
    m.cil("hierro", 0.04, 0.42, (0, 0, 0), lados=6, rot=(0, 90, 0), var=0)
    m.box("madera3", (0.03, 0.03, 0.2), (0.23, 0, 0.1), var=0)
    return b.formas, m.formas, centro
