"""Nostos: piezas detalladas (sillares, tejas, ventanas, puertas...) para los modelos del kit.

Todo se describe con el mismo formato de formas que usa generar_kit.py, con unos campos
de más: "v" (variante de tono dentro de la fila de la paleta), "M" (matriz de colocación),
"bisel", "deform" y "semilla". Las funciones trabajan sobre un `Det`, que acumula formas y
lleva una pila de transformaciones. Convención: cada pieza de fachada se modela mirando a -Y.
"""

import math
import random
import zlib

from mathutils import Matrix

LOSA = 0.12
ALTO = 2.8          # altura libre interior
PILAR = 0.2
PARED = 0.24        # grosor del muro; los postes (PILAR) quedan embebidos
PUERTA_ANCHO = 1.2
PUERTA_ALTO = 2.1
MARGEN = 0.2        # hueco entre el edificio y el borde de sus celdas
VARIANTE_BASE = 3   # columna de la paleta con el color exacto
Z_MURO = LOSA + ALTO                      # coronación del muro
ZOCALO_ALTO = 0.52
# Hiladas de las esquinas: (z, alto, vuelo, largo, corto). Las dos primeras son el zócalo.
HILADAS = [(0.0, 0.28, 0.07, 0.62, 0.27), (0.28, 0.24, 0.05, 0.62, 0.25)]
_N_ESQ = 7
HILADAS += [(ZOCALO_ALTO + i * (Z_MURO - ZOCALO_ALTO) / _N_ESQ, (Z_MURO - ZOCALO_ALTO) / _N_ESQ, 0.035, 0.56, 0.25)
            for i in range(_N_ESQ)]
VENTANA_Z = LOSA + 1.05                   # antepecho
VENTANA_ALTO = 0.95
PUERTA_LUZ = 0.6                          # media luz libre de la puerta
PUERTA_ARRANQUE = LOSA + 1.55             # arranque del arco

# Casas de dos plantas: `plantas(2)` cambia la altura del muro, las hiladas de las esquinas y añade la
# imposta entre plantas; `plantas(1)` lo deja como estaba. Las funciones leen estos valores al llamarse.
PLANTAS = 1
Z_PLANTA = None                 # suelo de la planta de arriba
IMPOSTA = None                  # (z0, z1) de la faja de piedra entre plantas
IMPOSTA_HILADA = -1             # hilada de esquina que ocupa la imposta (no lleva sillar)
VENTANA_ALTA = 0.72             # antepecho de las ventanas de arriba sobre su suelo
_UNA_PLANTA = (ALTO, Z_MURO, list(HILADAS))


def plantas(n):
    """Fija una o dos plantas para los edificios que se generen a continuación."""
    global PLANTAS, ALTO, Z_MURO, HILADAS, Z_PLANTA, IMPOSTA, IMPOSTA_HILADA
    PLANTAS = n
    if n == 1:
        ALTO, Z_MURO, HILADAS = _UNA_PLANTA[0], _UNA_PLANTA[1], list(_UNA_PLANTA[2])
        Z_PLANTA, IMPOSTA, IMPOSTA_HILADA = None, None, -1
        return
    Z_PLANTA = LOSA + 2.75
    Z_MURO = Z_PLANTA + 2.0
    ALTO = Z_MURO - LOSA
    IMPOSTA = (Z_PLANTA - 0.12, Z_PLANTA + 0.06)
    HILADAS = list(_UNA_PLANTA[2][:2])
    for z0, z1 in ((ZOCALO_ALTO, IMPOSTA[0]), (IMPOSTA[1], Z_MURO)):
        k = max(1, round((z1 - z0) / 0.33))
        HILADAS += [(z0 + i * (z1 - z0) / k, (z1 - z0) / k, 0.035, 0.56, 0.25) for i in range(k)]
        if z0 == ZOCALO_ALTO:
            IMPOSTA_HILADA = len(HILADAS)
            HILADAS.append((IMPOSTA[0], IMPOSTA[1] - IMPOSTA[0], 0.06, 0.56, 0.25))


PIEDRAS = ["piedra", "piedra2", "piedra3", "piedra", "piedra3"]
TEJAS = ["teja", "teja", "teja", "teja2", "teja3"]
TEJA_CUMBRERA = "teja3"
HOJAS = ["hoja", "hoja_clara", "hoja_oscura"]
AZULES = ["azul", "azul2", "azul3", "azul"]


def semilla_de(*partes):
    return zlib.crc32("/".join(str(p) for p in partes).encode())


class Det:
    def __init__(self, *semilla):
        self.r = random.Random(semilla_de(*semilla))
        self.formas = []
        self.M = Matrix.Identity(4)
        self.pila = []

    # ---- transformaciones
    def push(self, pos=(0, 0, 0), rz=0.0):
        self.pila.append(self.M.copy())
        self.M = self.M @ Matrix.Translation(pos) @ Matrix.Rotation(math.radians(rz), 4, "Z")

    def pop(self):
        self.M = self.pila.pop()

    # ---- color
    def _color(self, color, var):
        r = self.r
        if isinstance(color, (list, tuple)):
            color = r.choice(color)
        if var <= 0:
            return color, VARIANTE_BASE
        if var < 0.04:
            return color, r.choice((2, 3, 3, 4))
        v = r.choice((0, 1, 2, 2, 3, 3, 4, 4, 5))
        if var >= 0.07 and r.random() < 0.14:
            v = r.choice((6, 7))
        return color, v

    def _add(self, f, color, var, v=None):
        f["color"], f["v"] = self._color(color, var)
        if v is not None:
            f["v"] = v
        f["M"] = self.M.copy()
        f["semilla"] = self.r.randrange(1 << 30)
        self.formas.append(f)

    # ---- formas
    def box(self, color, tam, c, rot=(0, 0, 0), bisel=0.0, var=0.05, deform=0.0, luz_z=False, v=None):
        self._add({"tipo": "caja", "tam": tam, "centro": c, "rot": rot, "bisel": bisel, "deform": deform, "luz_z": luz_z},
                  color, var, v)

    def caja(self, color, x0, x1, y0, y1, z0, z1, **kw):
        """Caja dada por sus límites."""
        self.box(color, (x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), **kw)

    def cil(self, color, r, h, c, lados=10, rot=(0, 0, 0), r2=None, var=0.05, luz_z=False, esc=(1, 1, 1), deform=0.0):
        self._add({"tipo": "cilindro", "radio": r, "radio2": r if r2 is None else r2, "alto": h, "centro": c,
                   "lados": lados, "rot": rot, "luz_z": luz_z, "esc": esc, "deform": deform}, color, var)

    def bola(self, color, r, c, esc=(1, 1, 1), ruido=0.18, var=0.05, sub=1, luz_z=False):
        self._add({"tipo": "bola", "radio": r, "centro": c, "esc": esc, "ruido": ruido, "sub": sub, "luz_z": luz_z},
                  color, var)

    def prisma(self, color, pts, y0, y1, var=0.03, bisel=0.0, v=None):
        """Polígono de puntos (x, z) extruido de y0 a y1. El bisel solo vale para polígonos convexos."""
        self._add({"tipo": "prisma", "pts": pts, "y0": y0, "y1": y1, "bisel": bisel}, color, var, v)


# ---------------------------------------------------------------- sillería y muros

def fila_piedras(b, x0, x1, y, z, alto, fondo, minimo=0.38, maximo=0.8):
    """Hilada de sillares irregulares de x0 a x1, con el centro en y."""
    R = b.r
    x = x0
    while x < x1 - 0.05:
        w = min(R.uniform(minimo, maximo), x1 - x)
        if x1 - (x + w) < 0.25:
            w = x1 - x
        h = alto * R.uniform(0.86, 1.0)
        b.box(PIEDRAS, (w - 0.02, fondo * R.uniform(0.85, 1.1), h), (x + w / 2, y, z + h / 2),
              bisel=0.04, var=0.07, deform=0.012)
        x += w


def bloques(b, x0, x1, z0, z1, y, fondo, alto=0.42, roto=0.0, minimo=0.45, maximo=0.95):
    """Muro de sillares por hiladas (para ruinas y túnel). `roto`: probabilidad de que falte un bloque arriba."""
    R = b.r
    z = z0
    fila = 0
    while z < z1 - 0.05:
        h = min(alto * R.uniform(0.9, 1.1), z1 - z)
        x = x0 - (0.25 if fila % 2 else 0)
        while x < x1 - 0.05:
            w = min(R.uniform(minimo, maximo), x1 - x)
            if x1 - (x + w) < 0.25:
                w = x1 - x
            xa = max(x, x0)
            arriba = (z - z0) / max(0.01, z1 - z0)
            if not (roto and arriba > 0.55 and R.random() < roto * arriba):
                b.box(PIEDRAS, (x + w - xa - 0.02, fondo * R.uniform(0.92, 1.06), h - 0.02),
                      ((xa + x + w) / 2, y, z + h / 2), bisel=0.045, var=0.08, deform=0.014)
            x += w
        z += h
        fila += 1


def esquinas(b, w, d, cuales=((-1, -1), (1, -1), (1, 1), (-1, 1))):
    """Sillares de esquina, alternando el lado largo en cada hilada. No asoman al interior."""
    for sx, sy in cuales:
        for c, (z, h, sale, largo, corto) in enumerate(HILADAS):
            if c == IMPOSTA_HILADA:
                continue
            lx, ly = (largo, corto) if c % 2 == 0 else (corto, largo)
            b.box(PIEDRAS, (lx, ly, h - 0.015), (sx * (w / 2 + sale - lx / 2), sy * (d / 2 + sale - ly / 2), z + h / 2),
                  bisel=0.03, var=0.07, deform=0.005)


def _largo_esquina(c, lateral):
    """Cuánto ocupa en esta fachada el sillar de esquina de la hilada c."""
    _, _, _, largo, corto = HILADAS[c]
    return largo if (c % 2 == 0) != lateral else corto


def zocalo(b, L, off, lateral, esq=(True, True), cortes=()):
    """Las dos hiladas del zócalo entre los sillares de esquina. `cortes`: tramos (x0, x1) que se dejan libres."""
    R = b.r
    for c in (0, 1):
        z, h, sale, _, _ = HILADAS[c]
        le = _largo_esquina(c, lateral)
        xa = -(L / 2 + sale - le) + 0.012 if esq[0] else -L / 2
        xb = (L / 2 + sale - le) - 0.012 if esq[1] else L / 2
        tramos = [(xa, xb)]
        for c0, c1 in cortes:
            tramos = [t for a, bb in tramos for t in ((a, min(bb, c0)), (max(a, c1), bb)) if t[1] - t[0] > 0.12]
        for a, bb in tramos:
            n = max(1, round((bb - a) / 0.52))
            pesos = [R.uniform(0.82, 1.18) for _ in range(n)]
            x = a
            for peso in pesos:
                ancho = (bb - a) * peso / sum(pesos)
                b.box(PIEDRAS, (ancho - 0.014, 0.2, h - 0.014), (x + ancho / 2, -off - sale + 0.1, z + h / 2),
                      bisel=0.03, var=0.07, deform=0.005)
                x += ancho


# ---------------------------------------------------------------- huecos

def ventana(b, ancho=0.7, alto=VENTANA_ALTO, contras=True, jardinera=False, luz=False):
    """Ventana en un hueco real del muro. Origen local: centro del hueco en el plano exterior (y=0);
    el muro ocupa y de 0 a PARED y el exterior queda hacia -Y."""
    R = b.r
    a, h, T = ancho / 2, alto / 2, PARED
    # vidrio: una cara para fuera y otra, clara, para dentro
    b.caja("luz" if luz else "vidrio", -a + 0.02, a - 0.02, 0.11, 0.12, -h + 0.02, h - 0.02, var=0)
    b.caja("espuma", -a + 0.02, a - 0.02, 0.12, 0.13, -h + 0.02, h - 0.02, var=0, v=1)
    # bastidor y cruceta de madera
    for sx in (-1, 1):
        b.caja("madera2", sx * a - 0.05 if sx > 0 else -a, sx * a if sx > 0 else -a + 0.05, 0.085, 0.155, -h, h, var=0)
    for sz in (-1, 1):
        b.caja("madera2", -a + 0.05, a - 0.05, 0.085, 0.155, sz * h - 0.05 if sz > 0 else -h, sz * h if sz > 0 else -h + 0.05, var=0)
    b.caja("madera3", -0.02, 0.02, 0.095, 0.145, -h + 0.05, h - 0.05, var=0)
    for x0, x1 in ((-a + 0.05, -0.02), (0.02, a - 0.05)):
        b.caja("madera3", x0, x1, 0.095, 0.145, h * 0.25 - 0.02, h * 0.25 + 0.02, var=0)
    # alféizar de piedra por fuera y repisa de madera por dentro
    A = a + 0.24
    b.caja("piedra3", -A - 0.04, A + 0.04, -0.09, 0.08, -h - 0.1, -h + 0.012, bisel=0.025, var=0.03)
    for sx in (-1, 1):
        b.caja("piedra2", sx * (a + 0.12) - 0.05, sx * (a + 0.12) + 0.05, -0.06, 0.0, -h - 0.19, -h - 0.1, bisel=0.02, var=0.03)
    b.caja("madera", -a - 0.05, a + 0.05, 0.17, T + 0.05, -h - 0.03, -h + 0.012, bisel=0.01, var=0)
    # jambas: tres sillares por lado, el del centro más ancho
    tercio = alto / 3
    for sx in (-1, 1):
        for i in range(3):
            ancho_s = 0.2 if i == 1 else 0.14
            x0, x1 = a - 0.01, a + ancho_s
            if sx < 0:
                x0, x1 = -x1, -x0
            b.caja(PIEDRAS, x0, x1, -0.03, 0.06, -h + 0.012 + i * tercio + 0.006, -h + (i + 1) * tercio - 0.006,
                   bisel=0.022, var=0.07)
    # dintel adovelado: dos salmeres y la clave
    zb, zt = h - 0.012, h + 0.22
    b.prisma(PIEDRAS, [(-A, zb), (-0.075, zb), (-0.115, zt), (-A, zt)], -0.04, 0.06, bisel=0.022, var=0.07)
    b.prisma(PIEDRAS, [(A, zb), (A, zt), (0.115, zt), (0.075, zb)], -0.04, 0.06, bisel=0.022, var=0.07)
    b.prisma("piedra3", [(-0.062, zb), (0.062, zb), (0.102, zt + 0.05), (-0.102, zt + 0.05)], -0.06, 0.06, bisel=0.022, var=0.03)
    if contras:
        cw = a + 0.02
        for sx in (-1, 1):
            # bisagra en el borde exterior de la jamba; la hoja se abre hacia fuera, separándose del muro
            b.push(pos=(sx * (a + 0.225), -0.055, 0), rz=-sx * R.uniform(7, 13))
            for k in range(3):
                x0 = sx * (0.004 + k * cw / 3)
                x1 = sx * ((k + 1) * cw / 3 - 0.004)
                b.caja(AZULES, min(x0, x1), max(x0, x1), -0.02, 0.02, -h + 0.02, h - 0.02, var=0.06)
            for zz in (-h * 0.55, h * 0.55):
                b.caja("azul2", min(0, sx * cw) + 0.015, max(0, sx * cw) - 0.015, -0.045, -0.02, zz - 0.035, zz + 0.035, var=0)
            b.pop()
            for zz in (-h * 0.55, h * 0.55):
                b.caja("hierro", sx * (a + 0.2) - 0.035, sx * (a + 0.2) + 0.035, -0.05, 0.0, zz - 0.02, zz + 0.02, var=0)
    if jardinera:
        z1 = -h - 0.115
        b.caja("madera", -a - 0.02, a + 0.02, -0.33, -0.1, z1 - 0.2, z1, bisel=0.015, var=0.03)
        b.caja("tierra", -a + 0.01, a - 0.01, -0.3, -0.13, z1 - 0.01, z1 + 0.012, var=0)
        for sx in (-1, 1):
            b.caja("hierro", sx * a * 0.6 - 0.02, sx * a * 0.6 + 0.02, -0.32, 0.0, z1 - 0.235, z1 - 0.2, var=0)
            b.caja("hierro", sx * a * 0.6 - 0.02, sx * a * 0.6 + 0.02, -0.035, 0.0, z1 - 0.36, z1 - 0.2, var=0)
        n = 5
        for i in range(n):
            x = (i / (n - 1) - 0.5) * (ancho - 0.14)
            b.bola(HOJAS, 0.105, (x, -0.215 + R.uniform(-.02, .02), z1 + 0.07), esc=(1, 1, 0.8), ruido=0.12, luz_z=True)
            if i % 2 == 0 or R.random() < 0.5:
                b.cil(["flor", "flor2", "blanco"], 0.045, 0.05, (x + R.uniform(-.04, .04), -0.25 + R.uniform(-.04, .03), z1 + 0.165),
                      lados=5, r2=0.02, rot=(R.uniform(-25, 25), R.uniform(-25, 25), 0), var=0.03)


def hoja_de_puerta(b, ancho, radio, arranque, pie):
    """Hoja de tablas con el canto superior en arco. Bisagra en el origen, la hoja se extiende hacia +X local;
    `arranque` y `pie` son alturas absolutas."""
    def arriba(u):
        return arranque + math.sqrt(max(0.0, radio * radio - (ancho - u) ** 2))
    tablas = 3
    for k in range(tablas):
        u0 = k * ancho / tablas + 0.004
        u1 = (k + 1) * ancho / tablas - 0.004
        um = (u0 + u1) / 2
        b.prisma(AZULES, [(u0, pie), (u1, pie), (u1, arriba(u1)), (um, arriba(um)), (u0, arriba(u0))], -0.025, 0.025, var=0.06)
    for zz in (pie + 0.32, arranque - 0.12):
        b.caja("hierro", 0.0, ancho - 0.06, -0.034, 0.034, zz - 0.035, zz + 0.035, var=0)
    b.caja("hierro", ancho - 0.13, ancho - 0.07, -0.06, 0.06, pie + 0.92, pie + 0.98, var=0)


def portada(b, hojas=True):
    """Puerta de arco con sillares que atraviesan el muro. Origen: centro del umbral en el plano exterior,
    z absoluta (suelo de fuera = 0, suelo de dentro = LOSA)."""
    T = PARED
    r, zs = PUERTA_LUZ, PUERTA_ARRANQUE
    y0, y1 = -0.05, T + 0.03
    n = 5
    for sx in (-1, 1):
        for i in range(n):
            ancho_s = 0.27 if i % 2 == 0 else 0.2
            x0, x1 = r, r + ancho_s
            if sx < 0:
                x0, x1 = -x1, -x0
            b.caja(PIEDRAS, x0, x1, y0, y1, i * zs / n + 0.006, (i + 1) * zs / n - 0.006, bisel=0.03, var=0.07)
    nd = 9
    for i in range(nd):
        clave = i == nd // 2
        a0 = math.pi * i / nd + 0.012
        a1 = math.pi * (i + 1) / nd - 0.012
        r1 = r + (0.34 if clave else 0.25)
        pts = [(math.cos(a0) * r, zs + math.sin(a0) * r), (math.cos(a0) * r1, zs + math.sin(a0) * r1),
               (math.cos(a1) * r1, zs + math.sin(a1) * r1), (math.cos(a1) * r, zs + math.sin(a1) * r)]
        b.prisma("piedra3" if clave else PIEDRAS, pts, y0 - (0.03 if clave else 0), y1, bisel=0.028, var=0.03 if clave else 0.07)
    # umbral y escalón
    b.caja("piedra2", -r - 0.01, r + 0.01, -0.12, T + 0.06, 0.0, LOSA + 0.012, var=0.02)
    b.caja("piedra", -r - 0.32, r + 0.32, -0.6, -0.04, -0.05, LOSA * 0.55, bisel=0.025, var=0.03)
    if hojas:
        ancho = r - 0.03
        for sx in (-1, 1):
            b.push(pos=(sx * ancho, -0.085, 0), rz=-100 if sx < 0 else -80)
            hoja_de_puerta(b, ancho, r - 0.01, zs, LOSA + 0.04)
            b.pop()
            for zz in (LOSA + 0.36, zs - 0.12):
                b.caja("hierro", sx * (r + 0.02) - 0.05, sx * (r + 0.02) + 0.05, -0.085, -0.045, zz - 0.03, zz + 0.03, var=0)


def estandarte(b, largo=1.2, ancho=0.46, color="azul"):
    """Cuelga de un asta que sale de la pared; origen = arranque del asta, mirando a -Y."""
    b.cil("madera2", 0.03, 0.7, (0, -0.33, 0), lados=6, rot=(90, 0, 0))
    b.box("luz", (0.08, 0.08, 0.08), (0, -0.7, 0), rot=(45, 0, 45), var=0)
    b.box("hierro", (0.04, 0.3, 0.04), (0, -0.16, -0.14), rot=(45, 0, 0))
    b.push(pos=(0, -0.46, -0.04), rz=90)
    b.box(color, (ancho, 0.03, largo), (0, 0, -largo / 2), var=0.03)
    b.prisma(color, [(-ancho / 2, -largo + 0.01), (ancho / 2, -largo + 0.01), (0, -largo - 0.26)], -0.015, 0.015)
    for sy in (-1, 1):
        b.box("blanco", (ancho, 0.012, 0.05), (0, sy * 0.016, -0.12), var=0)
        b.box("blanco", (ancho, 0.012, 0.05), (0, sy * 0.016, -largo + 0.1), var=0)
        b.box("blanco", (0.2, 0.012, 0.2), (0, sy * 0.016, -largo * 0.5), rot=(0, 45, 0), var=0)
        b.box(color, (0.1, 0.014, 0.1), (0, sy * 0.017, -largo * 0.5), rot=(0, 45, 0), var=0)
    b.pop()


def pendon(b, largo=1.5, ancho=0.5, color="azul"):
    """Estandarte plano pegado a la pared (para ruinas, túnel y torres). Origen arriba en el centro, mirando a -Y."""
    b.box("madera2", (ancho + 0.16, 0.06, 0.06), (0, -0.05, 0), bisel=0.01)
    b.box(color, (ancho, 0.03, largo), (0, -0.05, -largo / 2), var=0.03)
    b.prisma(color, [(-ancho / 2, -largo + 0.01), (ancho / 2, -largo + 0.01), (0, -largo - 0.26)], -0.065, -0.035)
    b.box("blanco", (ancho, 0.012, 0.05), (0, -0.07, -0.14), var=0)
    b.box("blanco", (ancho, 0.012, 0.05), (0, -0.07, -largo + 0.1), var=0)
    b.box("blanco", (0.2, 0.012, 0.2), (0, -0.07, -largo * 0.5), rot=(0, 45, 0), var=0)
    b.box(color, (0.1, 0.012, 0.1), (0, -0.075, -largo * 0.5), rot=(0, 45, 0), var=0)


def farol(b):
    """Farol de pared; origen en el anclaje sobre el plano exterior, mirando a -Y."""
    b.caja("hierro", -0.05, 0.05, -0.02, 0.0, -0.06, 0.06, var=0)
    b.caja("hierro", -0.018, 0.018, -0.3, 0.0, 0.0, 0.035, var=0)
    b.caja("hierro", -0.012, 0.012, -0.262, -0.238, -0.07, 0.0, var=0)
    b.cil("hierro", 0.11, 0.05, (0, -0.25, -0.085), lados=4, rot=(0, 0, 45), r2=0.04, var=0)
    b.caja("luz", -0.055, 0.055, -0.305, -0.195, -0.27, -0.11, var=0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.caja("hierro", sx * 0.06 - 0.01, sx * 0.06 + 0.01, -0.25 + sy * 0.06 - 0.01, -0.25 + sy * 0.06 + 0.01, -0.28, -0.1, var=0)
    b.caja("hierro", -0.075, 0.075, -0.325, -0.175, -0.3, -0.275, var=0)


def letrero(b, color):
    """Letrero colgante con el color del oficio; origen en el anclaje sobre el plano exterior, mirando a -Y."""
    b.caja("hierro", -0.05, 0.05, -0.02, 0.0, -0.12, 0.06, var=0)
    b.caja("hierro", -0.018, 0.018, -0.62, 0.0, 0.0, 0.035, var=0)
    b.box("hierro", (0.03, 0.26, 0.03), (0, -0.11, -0.1), rot=(45, 0, 0), var=0)
    for y in (-0.5, -0.24):
        b.caja("hierro", -0.01, 0.01, y - 0.01, y + 0.01, -0.07, 0.0, var=0)
    b.caja("madera", -0.025, 0.025, -0.58, -0.16, -0.42, -0.07, bisel=0.012, var=0)
    for sx in (-1, 1):
        b.box(color, (0.012, 0.17, 0.17), (sx * 0.028, -0.37, -0.245), rot=(45, 0, 0), var=0)


# ---------------------------------------------------------------- tejado

def tejado(b, w, L, z0, grados=38, vuelo=0.4, vuelo_y=0.35, hastial="yeso", lados=(-1, 1), oculo=True, tejas=(-1, 1)):
    """A dos aguas, cumbrera a lo largo de Y local. w = luz del muro en X, L = largo en Y.
    `lados`: faldones con tablero; `tejas`: faldones con teja. Devuelve la altura de la cumbrera."""
    R = b.r
    p = math.radians(grados)
    alto = (w / 2) * math.tan(p)
    zc = z0 + alto
    if len(lados) == 2:
        for sy in (-1, 1):
            # El hastial sigue el plano exterior del muro y baja hasta su coronación.
            b.push(pos=(0, sy * L / 2, 0), rz=0 if sy < 0 else 180)
            b.prisma(hastial, [(-w / 2, Z_MURO), (w / 2, Z_MURO), (w / 2, z0), (0, zc), (-w / 2, z0)], 0.0, PARED, var=0)
            b.caja("madera", -w / 2 - 0.02, w / 2 + 0.02, -0.045, 0.03, Z_MURO - 0.02, Z_MURO + 0.2, bisel=0.02, var=0)
            if oculo:
                zo = z0 + alto * 0.42
                b.cil("vidrio", 0.2, 0.1, (0, 0.03, zo), lados=16, rot=(90, 0, 0), var=0)
                for i in range(8):
                    a0 = i * math.pi / 4 + 0.03
                    a1 = (i + 1) * math.pi / 4 - 0.03
                    pts = [(math.cos(a0) * 0.17, zo + math.sin(a0) * 0.17), (math.cos(a0) * 0.33, zo + math.sin(a0) * 0.33),
                           (math.cos(a1) * 0.33, zo + math.sin(a1) * 0.33), (math.cos(a1) * 0.17, zo + math.sin(a1) * 0.17)]
                    b.prisma(PIEDRAS, pts, -0.04, 0.05, bisel=0.02, var=0.07)
            b.pop()
    lp = (w / 2 + vuelo) / math.cos(p)
    for s in (-1, 1):
        cx = s * (w / 2 + vuelo) / 2
        cz = zc - (w / 2 + vuelo) / 2 * math.tan(p)
        if s in lados:
            b.box("madera2", (lp, L + 2 * vuelo_y - 0.06, 0.06), (cx, 0, cz + 0.035), rot=(0, s * grados, 0), var=0)
        n = max(3, int(L / 0.8))
        for i in range(n + 1):
            y = -L / 2 - vuelo_y + 0.1 + i * (L + 2 * vuelo_y - 0.2) / n
            tc = lp / 2 + 0.07      # los cabios no llegan a cruzarse en la cumbrera
            b.box(["madera", "madera3"], (lp + 0.06, 0.11, 0.13), (s * tc * math.cos(p), y, zc - tc * math.sin(p) - 0.07),
                  rot=(0, s * grados, 0), bisel=0.02)
        if s in lados:
            for y in (-L / 2 - vuelo_y + 0.03, L / 2 + vuelo_y - 0.03):
                b.box("madera", (lp + 0.12, 0.07, 0.2), (cx + s * 0.05, y, cz - 0.03), rot=(0, s * grados, 0), bisel=0.02, var=0)
    b.box("madera", (0.14, L + 2 * vuelo_y + 0.16, 0.16), (0, 0, zc - 0.12), bisel=0.02)
    tl, tw = 0.46, 0.36
    paso = tl * 0.72
    nf = int(math.ceil((lp + 0.05) / paso))
    nc = int(math.ceil((L + 2 * vuelo_y + 0.1) / tw))
    tw = (L + 2 * vuelo_y + 0.1) / nc
    for s in tejas:
        for f in range(nf):
            t = lp + 0.06 - tl / 2 - f * paso
            if t < tl * 0.3:
                continue
            for c in range(nc + (f % 2)):
                y = -L / 2 - vuelo_y - 0.05 + (c + 0.5 - 0.5 * (f % 2)) * tw
                ancho = tw - 0.015
                if f % 2 and (c == 0 or c == nc):
                    ancho = tw / 2 - 0.015
                    y += (tw / 4 if c == 0 else -tw / 4)
                levanta = R.uniform(0, 0.012)
                x = s * t * math.cos(p)
                z = zc - t * math.sin(p) + 0.12 + levanta
                col = TEJAS
                b.box(col, (tl, ancho, 0.07), (x, y, z),
                      rot=(R.uniform(-0.8, 0.8), s * (grados - 7) + R.uniform(-1.2, 1.2), R.uniform(-1.2, 1.2)), var=0.035, bisel=0.012)
    if len(tejas) == 2:
        n = int((L + 2 * vuelo_y + 0.2) / 0.42)
        for i in range(n):
            y = -L / 2 - vuelo_y - 0.08 + (i + 0.5) * (L + 2 * vuelo_y + 0.16) / n
            b.cil(TEJA_CUMBRERA, 0.16, 0.44, (0, y, zc + 0.14), lados=8, rot=(90, 0, 0), r2=0.13, var=0.03)
    return zc


def chimenea(b, alto=1.7, lado=0.34, colores=None):
    R = b.r
    colores = colores or PIEDRAS + ["ladrillo", "ladrillo2"]
    z = 0
    m = lado / 2 - 0.01
    while z < alto:
        for dx, dy in ((-m, -m), (m, -m), (m, m), (-m, m)):
            b.box(colores, (lado, lado, 0.2), (dx + R.uniform(-.012, .012), dy + R.uniform(-.012, .012), z + 0.1),
                  bisel=0.03, var=0.07, deform=0.008)
        z += 0.205
    b.box("piedra3", (lado * 2 + 0.18, lado * 2 + 0.18, 0.12), (0, 0, alto + 0.05), bisel=0.035)
    b.box("piedra2", (lado * 2 - 0.08, lado * 2 - 0.08, 0.1), (0, 0, alto + 0.15), bisel=0.03)
    for dx in (-0.14, 0.14):
        b.cil("teja3", 0.1, 0.3, (dx, 0, alto + 0.33), lados=7, r2=0.075)


# ---------------------------------------------------------------- atrezo

def barril(b, pos):
    b.push(pos=pos)
    for i, (r1, r2) in enumerate(((0.26, 0.31), (0.31, 0.31), (0.31, 0.26))):
        b.cil("madera", r1, 0.24, (0, 0, 0.12 + i * 0.24), lados=10, r2=r2)
    for z in (0.14, 0.58):
        b.cil("hierro", 0.315, 0.05, (0, 0, z), lados=10)
    b.cil("madera2", 0.24, 0.02, (0, 0, 0.725), lados=10)
    b.pop()


def caja_madera(b, pos, t=0.55, rz=0):
    b.push(pos=pos, rz=rz)
    b.box("madera3", (t, t, t), (0, 0, t / 2), bisel=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box("madera2", (0.07, 0.07, t + 0.04), (sx * t / 2, sy * t / 2, t / 2))
    for z in (0.04, t - 0.04):
        for sg in (-1, 1):
            b.box("madera", (t, 0.05, 0.07), (0, sg * t / 2, z))
            b.box("madera", (0.05, t, 0.07), (sg * t / 2, 0, z))
    b.pop()


def mata(b, pos, n=5):
    R = b.r
    for i in range(n):
        b.box(["hierba_oscura", "hoja_clara", "hoja"], (0.05, 0.02, R.uniform(0.18, 0.34)),
              (pos[0] + R.uniform(-.08, .08), pos[1] + R.uniform(-.08, .08), pos[2] + 0.1),
              rot=(R.uniform(-22, 22), R.uniform(-22, 22), R.uniform(0, 360)))


def mato(b, pos, r=0.45, flores=False):
    R = b.r
    for i in range(5):
        a = i * 2.4
        d = r * 0.55 * (i > 0)
        b.bola(HOJAS, r * R.uniform(0.6, 0.85), (pos[0] + math.cos(a) * d, pos[1] + math.sin(a) * d, pos[2] + r * 0.45 + R.uniform(0, r * 0.3)),
               esc=(1, 1, 0.8), luz_z=True)
    if flores:
        for i in range(7):
            b.box(["flor", "flor2", "blanco"], (0.08, 0.08, 0.08),
                  (pos[0] + R.uniform(-r, r) * 0.8, pos[1] + R.uniform(-r, r) * 0.8, pos[2] + r * R.uniform(0.75, 1.0)), rot=(30, 20, 40), var=0.03)


# ---------------------------------------------------------------- capas de edificio

def capa_losa(nombre, w, d, con_cuerdas):
    b = Det(nombre, "losa")
    R = b.r
    b.box("piedra2", (w, d, LOSA - 0.03), (0, 0, (LOSA - 0.03) / 2), bisel=0.03, var=0)
    n = int(d / 0.34)
    for i in range(n):
        y = -d / 2 + 0.1 + (i + 0.5) * (d - 0.2) / n
        b.box(["madera", "madera3", "madera3"], (w - 0.2, (d - 0.2) / n - 0.012, 0.04), (0, y, LOSA - 0.02), var=0.06)
    if con_cuerdas:
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.box("madera", (0.08, 0.08, 0.75), (sx * (w / 2 - 0.04), sy * (d / 2 - 0.04), LOSA + 0.3), rot=(R.uniform(-5, 5), R.uniform(-5, 5), 0), bisel=0.015)
        z = LOSA + 0.52
        for sy in (-1, 1):
            b.box("yeso3", (w - 0.1, 0.03, 0.03), (0, sy * (d / 2 - 0.04), z), var=0)
        for sx in (-1, 1):
            b.box("yeso3", (0.03, d - 0.1, 0.03), (sx * (w / 2 - 0.04), 0, z), var=0)
        # acopio: sillares y tablones
        for i in range(7):
            b.box(PIEDRAS, (R.uniform(0.4, 0.6), 0.34, 0.3), (-w / 2 + 0.6 + (i % 3) * 0.5 + (i // 3) * 0.2, -d / 2 + 0.55 + R.uniform(-.04, .04), LOSA + 0.15 + (i // 3) * 0.3),
                  rot=(0, 0, R.uniform(-8, 8)), bisel=0.04, var=0.07, deform=0.012)
        for i in range(5):
            b.box(["madera", "madera3"], (0.24, 2.0, 0.07), (w / 2 - 0.5 - (i % 3) * 0.26, 0.1 + R.uniform(-.1, .1), LOSA + 0.04 + (i // 3) * 0.075), rot=(0, 0, R.uniform(-4, 4)), bisel=0.012)
    return b.formas


def huecos_de(w, d):
    """Huecos de cada fachada: {fachada: [(tipo, x)]}, con x a lo largo de la fachada vista desde fuera."""
    def sin_puerta(L):
        return [("ventana", 0.0)] if L < 5 else [("ventana", -1.15), ("ventana", 1.15)]
    if PLANTAS == 2:
        # La casa de dos plantas (3x2): escalera contra el fondo, chimenea en el lateral derecho.
        return {"frente": [("ventana_lisa", -1.6), ("puerta", 0.0), ("ventana_lisa", 1.6),
                           ("ventana_lisa", -1.65, 1), ("ventana", 0.0, 1), ("ventana_lisa", 1.65, 1)],
                "fondo": [("ventana_lisa", 1.65), ("ventana", -0.9, 1), ("ventana_lisa", 1.65, 1)],
                "izquierda": [("ventana", 0.0), ("ventana", 0.0, 1)],
                "derecha": [("ventana_lisa", -1.15), ("ventana_lisa", -1.0, 1)]}
    frente = [("puerta", 0.0)]
    if w > 5:
        frente = [("ventana_lisa", -1.6), ("puerta", 0.0), ("ventana_lisa", 1.6)]
    return {"fondo": sin_puerta(w), "izquierda": sin_puerta(d), "derecha": sin_puerta(d), "frente": frente}


def _postes(w, d):
    """Postes (x, y, lado): esquinas, jambas de la puerta y el centro de las fachadas largas. Todos quedan dentro del muro."""
    ex, ey = w / 2 - PARED / 2, d / 2 - PARED / 2
    p = [(sx * ex, sy * ey, PILAR) for sx in (-1, 1) for sy in (-1, 1)]
    p += [(-0.73, ey, 0.16), (0.73, ey, 0.16)]
    if w > 5:
        p.append((0.0, -ey, PILAR))
    if d > 5:
        p += [(-ex, 0.0, PILAR), (ex, 0.0, PILAR)]
    return p


# Escalera de la casa de dos plantas: contra el muro del fondo, sube hacia +X.
ESCALERA_PELDANOS = 14
ESCALERA_HUELLA = 0.24
ESCALERA_ANCHO = 0.84


def escalera_medidas(w, d):
    """(xs, xt, y0, y1, hx0): primer peldaño, llegada arriba, fondo y borde de la escalera, inicio del hueco."""
    xi, yi = w / 2 - PARED, d / 2 - PARED
    xs = -xi + 0.25
    xt = xs + (ESCALERA_PELDANOS - 1) * ESCALERA_HUELLA
    return xs, xt, -yi, -yi + ESCALERA_ANCHO, xs + 0.26


def forjado_y_escalera(b, w, d):
    """Suelo de la planta de arriba (vigas y tablas, con el hueco de la escalera), la escalera y las barandillas."""
    xi, yi = w / 2 - PARED, d / 2 - PARED
    zp = Z_PLANTA
    xs, xt, y0, y1, hx0 = escalera_medidas(w, d)
    sube = (zp - LOSA) / ESCALERA_PELDANOS
    zv0, zv1 = zp - 0.25, zp - 0.05
    # brochales a los lados del hueco (fuera de él) y zoquete a lo largo
    for x in (hx0 - 0.07, xt + 0.07):
        b.caja("madera2", x - 0.07, x + 0.07, -yi, y1 + 0.14, zv0, zv1, bisel=0.015, var=0)
    b.caja("madera2", hx0 - 0.14, xt + 0.14, y1, y1 + 0.14, zv0, zv1, bisel=0.015, var=0)
    for x in (-1.45, -0.7, 0.05, 1.55):
        dentro = hx0 - 0.14 < x < xt + 0.14
        b.caja("madera2", x - 0.07, x + 0.07, y1 + 0.14 if dentro else -yi, yi, zv0, zv1, bisel=0.015, var=0)
    # tablas, en hiladas a lo largo de X; las que pisan el hueco se cortan
    filas = [(y0 + i * (y1 - y0) / 3, y0 + (i + 1) * (y1 - y0) / 3) for i in range(3)]
    k = 8
    filas += [(y1 + i * (yi - y1) / k, y1 + (i + 1) * (yi - y1) / k) for i in range(k)]
    for fy0, fy1 in filas:
        tramos = [(-xi, hx0), (xt, xi)] if fy1 <= y1 + 1e-6 else [(-xi, xi)]
        for a, c in tramos:
            if c - a > 0.05:
                b.caja(["madera", "madera3"], a, c, fy0 + 0.006, fy1 - 0.006, zv1, zp, var=0.05)
    # escalera: de obra encalada, con huella de madera
    for i in range(1, ESCALERA_PELDANOS):
        xa = xs + (i - 1) * ESCALERA_HUELLA
        top = LOSA + i * sube
        b.caja("yeso2", xa, xa + ESCALERA_HUELLA, y0, y1 - 0.05, LOSA, top - 0.04, var=0)
        b.caja(["madera", "madera3"], xa - 0.03, xa + ESCALERA_HUELLA, y0, y1 - 0.05, top - 0.04, top, bisel=0.01, var=0.04)
    # Tabique de yeso por el lado abierto desde el quinto peldaño hasta la llegada: la escalera queda
    # en caja y los cuatro primeros peldaños, libres para subir de lado (al pie de la escalera está el muro).
    # Murete de yeso por el lado abierto: arranca en el quinto peldaño, sube con la escalera a la
    # altura de un pasamanos y, donde toca el suelo de arriba, la cierra hasta la llegada.
    xp = xs + 4 * ESCALERA_HUELLA
    pend = sube / ESCALERA_HUELLA
    za = LOSA + 4 * sube + 0.9
    xk = xp + (zv0 - za) / pend
    # a ras del zoquete y del brochal de la llegada, que no quede rendija en el techo
    yt0, yt1 = y1 - 0.05, y1 + 0.14
    b.prisma("yeso", [(xp, LOSA), (xt + 0.14, LOSA), (xt + 0.14, zv0), (xk, zv0), (xp, za)], yt0, yt1, var=0)
    # albardilla de madera sobre el tramo inclinado
    largo = math.hypot(xk - xp, zv0 - za)
    ang = math.degrees(math.atan2(zv0 - za, xk - xp))
    b.box("madera", (largo, yt1 - yt0 + 0.04, 0.05), ((xp + xk) / 2, (yt0 + yt1) / 2, (za + zv0) / 2 + 0.02),
          rot=(0, -ang, 0), bisel=0.012, var=0)
    # alacena bajo la escalera: puertecilla de tablas con herrajes, sobre el murete
    ax0, ax1, az1 = xt - 0.2 - 0.62, xt - 0.2, LOSA + 1.42
    yp = yt1
    for i in range(4):
        t0 = ax0 + i * (ax1 - ax0) / 4
        b.caja(["madera", "madera3"], t0 + 0.004, t0 + (ax1 - ax0) / 4 - 0.004, yp, yp + 0.03, LOSA + 0.02, az1, bisel=0.006, var=0.04)
    b.caja("madera2", ax0 - 0.05, ax1 + 0.05, yp, yp + 0.02, az1, az1 + 0.06, var=0)
    for sx in (ax0 - 0.05, ax1 + 0.01):
        b.caja("madera2", sx, sx + 0.04, yp, yp + 0.02, LOSA, az1, var=0)
    for z in (LOSA + 0.3, az1 - 0.3):
        b.caja("hierro", ax0 + 0.04, ax0 + 0.3, yp + 0.03, yp + 0.042, z - 0.025, z + 0.025, var=0)
    b.caja("hierro", ax1 - 0.1, ax1 - 0.07, yp + 0.03, yp + 0.06, LOSA + 0.78, LOSA + 0.86, var=0)
    # zanca de los peldaños que quedan a la vista
    zz = LOSA + 4 * sube
    b.prisma("madera", [(xs, LOSA), (xp, LOSA), (xp, zz), (xs, LOSA + sube - 0.04)], y1 - 0.05, y1, var=0)
    # barandilla arriba, a lo largo del hueco y en su cabeza
    def barandilla(xa, ya, xb, yb):
        L = math.hypot(xb - xa, yb - ya)
        ux, uy = (xb - xa) / L, (yb - ya) / L
        npost = max(2, round(L / 0.7) + 1)
        for i in range(npost):
            t = i / (npost - 1)
            x, y = xa + ux * L * t, ya + uy * L * t
            b.caja("madera3", x - 0.04, x + 0.04, y - 0.04, y + 0.04, zp, zp + 0.98, bisel=0.01, var=0)
        nb = int(L / 0.14)
        for i in range(1, nb):
            t = i / nb
            x, y = xa + ux * L * t, ya + uy * L * t
            b.caja("madera", x - 0.016, x + 0.016, y - 0.016, y + 0.016, zp + 0.1, zp + 0.9, var=0)
        mx, my = (xa + xb) / 2, (ya + yb) / 2
        rz = math.degrees(math.atan2(uy, ux))
        b.box("madera", (L, 0.1, 0.06), (mx, my, zp + 0.93), rot=(0, 0, rz), bisel=0.015, var=0)
        b.box("madera2", (L, 0.05, 0.05), (mx, my, zp + 0.075), rot=(0, 0, rz), var=0)
    barandilla(hx0 + 0.06, y1 + 0.05, xt - 0.04, y1 + 0.05)
    barandilla(hx0 - 0.05, y0 + 0.05, hx0 - 0.05, y1 + 0.05)


def capa_estructura(nombre, w, d, con_andamio):
    b = Det(nombre, "estructura")
    if PLANTAS == 2:
        forjado_y_escalera(b, w, d)
    for x, y, lado in _postes(w, d):
        b.box(["madera", "madera2"], (lado, lado, ALTO - 0.01), (x, y, LOSA + ALTO / 2), bisel=0.02, var=0.03)
    zv = Z_MURO + PILAR / 2
    for sy in (-1, 1):
        b.box("madera2", (w - 2 * PARED, PILAR, PILAR), (0, sy * (d / 2 - PARED / 2), zv), bisel=0.025, var=0)
    tramos = max(2, round((w - PARED) / 1.7))
    for i in range(tramos + 1):
        x = -(w / 2 - PARED / 2) + i * (w - PARED) / tramos
        # las vigas de los hastiales no asoman; las intermedias enseñan la cabeza bajo el alero
        largo = d - 0.02 if i in (0, tramos) else d + 0.1
        b.box("madera", (0.16, largo, 0.16), (x, 0, zv), bisel=0.02, var=0.03)
    if con_andamio:
        for sx in (-1, 1):
            for sy in (-1, 1):
                b.box("madera3", (0.09, 0.8, 0.09), (sx * (w / 2 - PARED / 2), sy * (d / 2 - PARED / 2 - 0.3), Z_MURO - 0.28), rot=(sy * 45, 0, 0), var=0.03)
        x = -w / 2 - 0.5
        for sy in (-1, 1):
            for dx in (-0.3, 0.3):
                b.box("madera3", (0.08, 0.08, ALTO + 0.3), (x + dx, sy * d / 4, (ALTO + 0.3) / 2), bisel=0.015)
            b.box("madera", (0.06, 0.06, 1.6), (x, sy * d / 4, 0.9), rot=(0, 28, 0))
        # andamios: una plataforma por planta, cada una con su barandal y su escalera de mano
        niveles = [(1.6, d / 4 + 0.5)] if PLANTAS == 1 else [(1.6, d / 4 + 0.5), (Z_PLANTA + 0.45, -(d / 4 + 0.5))]
        for k, (zt, ye) in enumerate(niveles):
            for i in range(3):
                b.box(["madera", "madera3"], (0.21, d / 2 + 0.6, 0.05), (x - 0.23 + i * 0.23, 0, zt), var=0.06)
            b.box("madera3", (0.06, d / 2 + 0.3, 0.06), (x - 0.3, 0, zt + 0.9))
            zb = 0.0 if k == 0 else niveles[k - 1][0] + 0.025
            for sy in (-1, 1):
                b.box("madera", (0.05, 0.05, 2.0), (x + 0.55, ye + sy * 0.2, zb + 0.95), rot=(0, -14, 0))
            for i in range(5):
                b.box("madera3", (0.04, 0.4, 0.04), (x + 0.78 - i * 0.09, ye, zb + 0.25 + i * 0.36))
    return b.formas


def _fachada(b, L, off, huecos, lateral, esq=(True, True), jardineras=(), luces=(), libres=()):
    """Un muro a lo largo de X local con el plano exterior en y = -off y el exterior hacia -Y.
    Los huecos son reales: el muro se construye con paños alrededor de ellos."""
    T = PARED
    ye = -off
    xa, xb = (-L / 2 + T, L / 2 - T) if lateral else (-L / 2, L / 2)
    vanos = []      # (x0, x1, z0, z1, tipo, planta)
    for h in sorted(huecos, key=lambda h: (h[2] if len(h) > 2 else 0, h[1])):
        tipo, x = h[0], h[1]
        planta = h[2] if len(h) > 2 else 0
        if tipo == "puerta":
            m = PUERTA_LUZ + 0.02
            vanos.append((x - m, x + m, LOSA, Z_MURO, tipo, 0))
        else:
            a = 0.35 if tipo == "ventana" else 0.3
            z0 = VENTANA_Z if planta == 0 else Z_PLANTA + VENTANA_ALTA
            vanos.append((x - a, x + a, z0, z0 + VENTANA_ALTO, tipo, planta))
    # El muro se levanta por bandas, una por planta, con sus huecos en cada una.
    bandas = [(LOSA, Z_MURO)] if PLANTAS == 1 else [(LOSA, Z_PLANTA - 0.25), (Z_PLANTA - 0.25, Z_MURO)]
    for f, (zb0, zb1) in enumerate(bandas):
        x = xa
        for x0, x1, z0, z1, tipo, planta in vanos:
            if planta != f:
                continue
            b.caja("yeso", x, x0, ye, ye + T, zb0, zb1, var=0)
            if tipo == "puerta":
                m, zs = PUERTA_LUZ + 0.02, PUERTA_ARRANQUE
                cx = (x0 + x1) / 2
                arco = [(cx + math.cos(math.pi * (1 - i / 16)) * m, zs + math.sin(math.pi * (1 - i / 16)) * m) for i in range(17)]
                b.prisma("yeso", [(x0, zb1)] + arco + [(x1, zb1)], ye, ye + T, var=0)
            else:
                b.caja("yeso", x0, x1, ye, ye + T, zb0, z0, var=0)
                b.caja("yeso", x0, x1, ye, ye + T, z1, zb1, var=0)
            x = x1
        b.caja("yeso", x, xb, ye, ye + T, zb0, zb1, var=0)

    cortes = [(v[0] - 0.3, v[1] + 0.3) for v in vanos if v[4] == "puerta"]
    zocalo(b, L, off, lateral, esq, cortes)
    if IMPOSTA:
        # Faja de piedra entre plantas: en las fachadas de frente y fondo llega a la arista; en los
        # laterales se queda entre ellas, así se tocan sin solaparse.
        sale = HILADAS[IMPOSTA_HILADA][2]
        i0, i1 = (-L / 2, L / 2) if lateral else (-L / 2 - sale, L / 2 + sale)
        if not lateral:
            i0 = i0 if esq[0] else -L / 2
            i1 = i1 if esq[1] else L / 2
        b.caja("piedra3", i0, i1, ye - sale, ye, IMPOSTA[0], IMPOSTA[1], bisel=0.015, var=0.02)

    # rodapié interior
    ri0, ri1 = -L / 2 + T + (0.03 if lateral else 0.0), L / 2 - T - (0.03 if lateral else 0.0)
    tramos = [(ri0, ri1)]
    for c0, c1 in list(cortes) + list(libres):
        tramos = [t for a, bb in tramos for t in ((a, min(bb, c0)), (max(a, c1), bb)) if t[1] - t[0] > 0.05]
    for a, bb in tramos:
        b.caja("madera2", a, bb, ye + T, ye + T + 0.025, LOSA, LOSA + 0.12, var=0)

    n = 0
    for x0, x1, z0, z1, tipo, _ in vanos:
        cx = (x0 + x1) / 2
        if tipo == "puerta":
            b.push(pos=(cx, ye, 0))
            portada(b)
            b.pop()
        else:
            b.push(pos=(cx, ye, (z0 + z1) / 2))
            ventana(b, ancho=x1 - x0, contras=tipo == "ventana", jardinera=n in jardineras, luz=n in luces)
            b.pop()
            n += 1


def capa_paredes(nombre, w, d, parcial, letrero_color=None):
    """Muros de yeso con huecos reales, zócalo y esquinas de sillería, ventanas con contraventanas y puerta de arco.
    La puerta está en la fachada +Y. Si `parcial`, solo el fondo y el lateral izquierdo."""
    b = Det(nombre, "paredes")
    huecos = huecos_de(w, d)
    dos = PLANTAS == 2
    b.push(rz=0)
    # el rodapié no corre por detrás de la escalera ni del hogar
    libres_fondo = ()
    if dos:
        xs, xt = escalera_medidas(w, d)[:2]
        libres_fondo = ((xs - 0.03, xt + 0.03),)
    _fachada(b, w, d / 2, huecos["fondo"], False, esq=(True, not parcial), jardineras=() if dos else (0,), luces=(0,),
             libres=libres_fondo)
    b.pop()
    b.push(rz=270)
    _fachada(b, d, w / 2, huecos["izquierda"], True, esq=(not parcial, True),
             jardineras=(1,) if dos or d > 5 else (), luces=(0,) if dos else ())
    b.pop()
    if parcial:
        esquinas(b, w, d, cuales=((-1, -1),))
        return b.formas
    b.push(rz=90)
    _fachada(b, d, w / 2, huecos["derecha"], True, jardineras=(0,), luces=(1,), libres=((-0.62, 0.62),) if dos else ())
    if dos:
        # pendón azul de Nova Lavinia en el lateral de la chimenea, como en la hoja de referencia
        b.push(pos=(0.9, -w / 2, Z_MURO - 0.3))
        pendon(b, largo=1.2, ancho=0.46)
        b.pop()
    b.pop()
    b.push(rz=180)
    _fachada(b, w, d / 2, huecos["frente"], False, luces=(0, 1), jardineras=(3,) if dos else ())
    xf = PUERTA_LUZ + 0.44
    b.push(pos=(0.95 if dos else xf, -d / 2, 2.2 if dos else 2.3))
    farol(b)
    b.pop()
    if letrero_color and not dos:
        b.push(pos=(-xf, -d / 2, 2.32 if dos else 2.52))
        letrero(b, letrero_color)
        b.pop()
    b.pop()
    esquinas(b, w, d)
    return b.formas


def capa_tejado(nombre, w, d, parcial, chimenea_grande=False):
    """Tejado a dos aguas de teja roja con la cumbrera a lo largo de X, hastiales con entramado y chimenea.
    Si `parcial`, solo el faldón del fondo tiene teja y el del frente enseña los cabios."""
    b = Det(nombre, "tejado")
    z0 = LOSA + ALTO + PILAR
    grados = 38 if d < 5 else 33
    b.push(rz=90)
    if parcial:
        zc = tejado(b, d, w, z0, grados=grados, lados=(-1,), tejas=(-1,))
    else:
        zc = tejado(b, d, w, z0, grados=grados)
    b.pop()
    if not parcial:
        cx, cy = w / 2 - 0.83, -d / 2 + 0.6
        if PLANTAS == 2:
            cx, cy = w / 2 - PARED - 0.25, 0.0
        base = zc - abs(cy) * math.tan(math.radians(grados)) - 0.45
        b.push(pos=(cx, cy, base))
        if chimenea_grande:
            chimenea(b, alto=2.6, lado=0.46)
        else:
            chimenea(b, alto=1.9)
        b.pop()
    return b.formas
