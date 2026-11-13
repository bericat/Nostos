"""
Genera lo que necesita la interfaz de Nostos y lo deja en Assets/Nostos/Resources/UI:

- Iconos de los 14 recursos y de la interfaz (confianza, nave, tributo, sol, luna, mochila),
  facetados como el resto del arte: dos o tres tonos por cara y un contorno oscuro.
- Fuentes: Cinzel (letras de inscripción romana, para títulos) y Alegreya Sans (texto),
  las dos con licencia SIL OFL, que permite incluirlas en el juego.

Uso: python3 generar_ui.py [carpeta_de_fuentes]
La carpeta de fuentes debe contener Cinzel[wght].ttf, AlegreyaSans-*.ttf y los OFL.txt.
"""
import math
import os
import shutil
import sys

from PIL import Image, ImageDraw, ImageFilter

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESTINO = os.path.join(RAIZ, "Assets", "Nostos", "Resources", "UI")
LADO = 256
FINAL = 96
TINTA = (29, 21, 16, 255)
GROSOR = 6


def hexa(c, a=255):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), a)


def mezcla(c, k):
    """k>0 aclara hacia blanco, k<0 oscurece hacia negro."""
    r, g, b, a = c
    if k >= 0:
        return (int(r + (255 - r) * k), int(g + (255 - g) * k), int(b + (255 - b) * k), a)
    k = -k
    return (int(r * (1 - k)), int(g * (1 - k)), int(b * (1 - k)), a)


class Lienzo:
    """Dibuja caras planas; cada cara lleva su propio filete oscuro, pintado en el momento para que
    lo que queda delante tape las líneas de lo que queda detrás."""

    def __init__(self):
        self.img = Image.new("RGBA", (LADO, LADO), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def cara(self, puntos, color, contorno=True):
        self.d.polygon(puntos, fill=color)
        if contorno:
            self.d.line(list(puntos) + [puntos[0]], fill=mezcla(color, -0.55), width=3, joint="curve")

    def circulo(self, cx, cy, r, color, contorno=True):
        self.elipse([cx - r, cy - r, cx + r, cy + r], color, contorno)

    def elipse(self, caja, color, contorno=True):
        if contorno:
            self.d.ellipse(caja, fill=color, outline=mezcla(color, -0.55), width=3)
        else:
            self.d.ellipse(caja, fill=color)

    def linea(self, puntos, color, ancho):
        self.d.line(puntos, fill=color, width=ancho, joint="curve")

    def guardar(self, nombre, con_contorno=True):
        img = self.img
        if con_contorno:
            # Contorno exterior: la silueta dilatada en tinta, debajo del dibujo.
            alfa = img.split()[3].point(lambda v: 255 if v > 40 else 0)
            silueta = alfa.filter(ImageFilter.MaxFilter(GROSOR * 2 + 1))
            fondo = Image.new("RGBA", img.size, TINTA)
            fondo.putalpha(silueta)
            fondo.alpha_composite(img)
            img = fondo
        img = img.resize((FINAL, FINAL), Image.LANCZOS)
        img.save(os.path.join(DESTINO, "Iconos", nombre + ".png"))
        return img


# --------------------------------------------------------------------- recursos

def madera():
    L = Lienzo()
    corteza = hexa("8a5a2e")
    for (y, x0) in ((150, 40), (100, 62)):
        # Tronco tumbado: lado con dos facetas y la cara del corte a la izquierda.
        L.cara([(x0 + 20, y - 34), (x0 + 175, y - 34), (x0 + 175, y), (x0 + 20, y)], mezcla(corteza, 0.18))
        L.cara([(x0 + 20, y), (x0 + 175, y), (x0 + 175, y + 34), (x0 + 20, y + 34)], mezcla(corteza, -0.15))
        L.elipse([x0 + 150, y - 34, x0 + 200, y + 34], mezcla(corteza, -0.25))
        L.elipse([x0 - 6, y - 36, x0 + 46, y + 36], hexa("e2b97f"))
        L.elipse([x0 + 8, y - 20, x0 + 32, y + 20], hexa("c99559"), contorno=False)
    L.guardar("madera")


def mineral():
    L = Lienzo()
    base = hexa("8d8f9c")
    p = {"a": (48, 170), "b": (80, 92), "c": (148, 58), "d": (210, 104), "e": (222, 176), "f": (150, 212), "g": (88, 210),
         "m": (138, 134)}
    L.cara([p["a"], p["b"], p["m"], p["g"]], mezcla(base, -0.1))
    L.cara([p["b"], p["c"], p["m"]], mezcla(base, 0.35))
    L.cara([p["c"], p["d"], p["m"]], mezcla(base, 0.15))
    L.cara([p["d"], p["e"], p["f"], p["m"]], mezcla(base, -0.3))
    L.cara([p["m"], p["f"], p["g"]], mezcla(base, -0.45))
    # Vetas de cobre y cristal.
    L.cara([(110, 110), (124, 102), (128, 118)], hexa("d98b4a"), contorno=False)
    L.cara([(170, 150), (186, 140), (184, 160)], hexa("7fd0dc"), contorno=False)
    L.guardar("mineral")


def tablones():
    L = Lienzo()
    base = hexa("c8915a")
    for i, y in enumerate((178, 138, 98)):
        dx = (-14, 10, -4)[i]
        x0, x1 = 34 + dx, 222 + dx
        L.cara([(x0 + 18, y - 22), (x1 + 18, y - 22), (x1, y), (x0, y)], mezcla(base, 0.3))
        L.cara([(x0, y), (x1, y), (x1, y + 24), (x0, y + 24)], mezcla(base, -0.08 * i))
        L.cara([(x1, y), (x1 + 18, y - 22), (x1 + 18, y + 2), (x1, y + 24)], mezcla(base, -0.35))
        L.linea([(x0 + 40, y + 12), (x0 + 100, y + 12)], mezcla(base, -0.3), 3)
    L.guardar("tablones")


def lingotes():
    L = Lienzo()
    base = hexa("a3abb5")

    def barra(cx, cy):
        w, h, t = 66, 34, 20
        top = [(cx - w + t, cy - h), (cx + w - t, cy - h), (cx + w, cy), (cx - w, cy)]
        L.cara(top, mezcla(base, 0.35))
        L.cara([(cx - w, cy), (cx + w, cy), (cx + w - 6, cy + 30), (cx - w + 6, cy + 30)], mezcla(base, -0.2))

    barra(84, 170)
    barra(172, 170)
    barra(128, 112)
    L.guardar("lingotes")


def chapa():
    L = Lienzo()
    base = hexa("b9c1c9")
    sup = [(40, 120), (150, 64), (220, 112), (110, 170)]
    L.cara(sup, mezcla(base, 0.2))
    L.cara([(40, 120), (110, 170), (110, 186), (40, 136)], mezcla(base, -0.25))
    L.cara([(110, 170), (220, 112), (220, 128), (110, 186)], mezcla(base, -0.45))
    L.linea([(70, 118), (150, 78)], mezcla(base, 0.6), 5)
    for (x, y) in ((70, 122), (150, 80), (192, 114), (112, 156)):
        L.circulo(x, y, 7, mezcla(base, -0.35), contorno=False)
    L.guardar("chapa")


def comida():
    L = Lienzo()
    hoja = hexa("6f9a4a")
    L.cara([(128, 40), (186, 66), (150, 96)], mezcla(hoja, 0.2))
    L.cara([(128, 40), (150, 96), (112, 92)], mezcla(hoja, -0.2))
    L.linea([(140, 70), (128, 112)], hexa("5b3d22"), 6)
    baya = hexa("c2413b")
    for (x, y, r) in ((96, 150, 40), (164, 150, 40), (128, 196, 38), (128, 120, 32)):
        L.circulo(x, y, r, baya)
        L.cara([(x - r * 0.55, y - r * 0.2), (x - r * 0.1, y - r * 0.7), (x + r * 0.1, y - r * 0.25)], mezcla(baya, 0.45), contorno=False)
    L.guardar("comida")


def raciones():
    L = Lienzo()
    cuenco = hexa("a0643a")
    L.elipse([36, 112, 220, 160], hexa("e0a54a"))
    L.cara([(36, 136), (220, 136), (196, 196), (152, 214), (104, 214), (60, 196)], mezcla(cuenco, -0.05))
    L.cara([(60, 196), (104, 214), (152, 214), (196, 196), (176, 204), (80, 204)], mezcla(cuenco, -0.35), contorno=False)
    L.linea([(56, 150), (200, 150)], mezcla(cuenco, 0.3), 5)
    for (x, y) in ((96, 124), (140, 128), (170, 120)):
        L.circulo(x, y, 9, hexa("d0583f"), contorno=False)
    vapor = (241, 230, 208, 220)
    for x in (100, 140):
        L.linea([(x, 96), (x - 10, 76), (x + 4, 58), (x - 6, 38)], vapor, 8)
    L.guardar("raciones")


def cobre():
    L = Lienzo()
    base = hexa("c46a35")
    p = [(56, 150), (96, 84), (166, 70), (214, 128), (184, 196), (100, 204)]
    m = (138, 140)
    tonos = (0.25, 0.5, 0.1, -0.3, -0.45, -0.1)
    for i in range(6):
        L.cara([p[i], p[(i + 1) % 6], m], mezcla(base, tonos[i]))
    L.cara([(112, 98), (136, 92), (124, 112)], (255, 230, 190, 230), contorno=False)
    L.guardar("cobre")


def cristal():
    L = Lienzo()
    base = hexa("72cbd9")

    def punta(x, y, w, h, ang):
        c, s = math.cos(ang), math.sin(ang)

        def r(px, py):
            return (x + px * c - py * s, y + px * s + py * c)
        L.cara([r(-w, 0), r(0, -h), r(0, 0)], mezcla(base, 0.45))
        L.cara([r(0, -h), r(w, 0), r(0, 0)], mezcla(base, -0.05))
        L.cara([r(-w, 0), r(0, 0), r(0, h * 0.25)], mezcla(base, -0.3))
        L.cara([r(0, 0), r(w, 0), r(0, h * 0.25)], mezcla(base, -0.5))

    punta(82, 190, 26, 110, -0.45)
    punta(178, 196, 24, 100, 0.42)
    punta(128, 200, 34, 160, 0.0)
    L.guardar("cristal")


def agua():
    L = Lienzo()
    base = hexa("4f9bd9")
    pts = []
    for i in range(41):
        t = i / 40 * 2 * math.pi
        # Gota: un círculo con la parte de arriba estirada en punta.
        x = 128 + 70 * math.sin(t)
        y = 160 - 70 * math.cos(t)
        if math.cos(t) > 0:
            k = math.cos(t) ** 2
            x = 128 + (x - 128) * (1 - k * 0.85)
            y = y - k * 56
        pts.append((x, y))
    L.cara(pts, base)
    L.cara([(128, 34), (128, 230), (78, 200), (60, 160), (84, 110)], mezcla(base, 0.18), contorno=False)
    L.cara([(92, 150), (104, 122), (110, 156), (100, 186)], (235, 248, 255, 230), contorno=False)
    L.guardar("agua")


def cable():
    L = Lienzo()
    cobre_ = hexa("d0773d")
    for i, r in enumerate((86, 66, 46)):
        L.d.ellipse([128 - r, 128 - r * 0.8, 128 + r, 128 + r * 0.8], outline=TINTA, width=26)
        L.d.ellipse([128 - r, 128 - r * 0.8, 128 + r, 128 + r * 0.8], outline=mezcla(cobre_, 0.1 * i), width=16)
    L.linea([(214, 128), (236, 200)], TINTA, 24)
    L.linea([(214, 128), (236, 200)], cobre_, 14)
    L.guardar("cable")


def bobinas():
    L = Lienzo()
    madera_ = hexa("8a5a2e")
    cobre_ = hexa("d0773d")
    L.elipse([40, 54, 108, 202], mezcla(madera_, -0.2))
    L.cara([(74, 70), (182, 70), (182, 186), (74, 186)], cobre_)
    for y in range(80, 186, 14):
        L.linea([(78, y), (178, y)], mezcla(cobre_, -0.3), 4)
    L.linea([(78, 92), (178, 92)], mezcla(cobre_, 0.4), 5)
    L.elipse([148, 54, 216, 202], mezcla(madera_, 0.1))
    L.elipse([170, 112, 194, 144], mezcla(madera_, -0.4), contorno=False)
    L.guardar("bobinas")


def lentes():
    L = Lienzo()
    laton = hexa("c9a24a")
    L.circulo(128, 128, 92, laton)
    L.circulo(128, 128, 70, hexa("9fd8e6"))
    L.cara([(84, 104), (122, 66), (138, 80), (98, 122)], (245, 252, 255, 235), contorno=False)
    L.cara([(150, 176), (176, 150), (182, 158), (158, 184)], (245, 252, 255, 160), contorno=False)
    for a in range(0, 360, 45):
        x = 128 + 82 * math.cos(math.radians(a))
        y = 128 + 82 * math.sin(math.radians(a))
        L.circulo(x, y, 6, mezcla(laton, -0.4), contorno=False)
    L.guardar("lentes")


def nucleos():
    L = Lienzo()
    base = hexa("3fa7a0")
    hexag = [(128 + 92 * math.cos(math.radians(a)), 128 + 92 * math.sin(math.radians(a))) for a in range(30, 390, 60)]
    tonos = (-0.1, -0.35, -0.2, 0.2, 0.35, 0.1)
    for i in range(6):
        L.cara([hexag[i], hexag[(i + 1) % 6], (128, 128)], mezcla(base, tonos[i]))
    L.circulo(128, 128, 34, hexa("f4e08a"))
    L.circulo(128, 128, 18, (255, 252, 230, 255), contorno=False)
    L.guardar("nucleos")


def herramientas():
    """Martillo y formón cruzados."""
    L = Lienzo()
    madera_ = hexa("b07a45")
    hierro = hexa("8f97a3")

    def palo(x0, y0, x1, y1, ancho, color):
        dx, dy = x1 - x0, y1 - y0
        n = math.hypot(dx, dy)
        nx, ny = -dy / n * ancho / 2, dx / n * ancho / 2
        L.cara([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)], color)

    # Formón: mango de madera abajo a la derecha, hoja de hierro arriba a la izquierda.
    palo(206, 206, 140, 140, 30, madera_)
    palo(140, 140, 62, 62, 20, hierro)
    L.cara([(62 - 10, 62 + 10), (62 + 10, 62 - 10), (44, 44)], mezcla(hierro, 0.3))
    # Martillo: mango abajo a la izquierda, cabeza arriba a la derecha.
    palo(50, 210, 160, 100, 24, mezcla(madera_, -0.1))
    cabeza = [(126, 54), (206, 134), (178, 162), (98, 82)]
    L.cara(cabeza, hierro)
    L.cara([(126, 54), (206, 134), (196, 144), (116, 64)], mezcla(hierro, 0.35))
    L.guardar("herramientas")


# --------------------------------------------------------------------- interfaz

def confianza():
    """Corona de laurel: en Lacio se premia con laurel."""
    L = Lienzo()
    hoja = hexa("7c9a4c")
    for lado in (-1, 1):
        for i in range(7):
            # De abajo (cerca del lazo) hacia arriba, por cada lado de la corona.
            t = math.radians(-66 + i * 20)
            cx = 128 + lado * 74 * math.cos(t)
            cy = 128 - 74 * math.sin(t)
            # La hoja apunta a lo largo de la corona, hacia arriba.
            tx, ty = lado * -math.sin(t), -math.cos(t)
            largo = 62 - i * 2.5
            dx, dy = tx * largo, ty * largo
            nx, ny = -dy * 0.3, dx * 0.3
            base = (cx - dx * 0.35, cy - dy * 0.35)
            punta = (cx + dx * 0.65, cy + dy * 0.65)
            medio = (cx + dx * 0.1, cy + dy * 0.1)
            L.cara([base, (medio[0] + nx, medio[1] + ny), punta], mezcla(hoja, 0.28))
            L.cara([base, (medio[0] - nx, medio[1] - ny), punta], mezcla(hoja, -0.22))
    L.cara([(108, 206), (128, 194), (148, 206), (128, 226)], hexa("c9a04a"))
    L.guardar("confianza")


def nave():
    L = Lienzo()
    blanco = hexa("e8ecef")
    L.cara([(128, 30), (170, 120), (128, 150)], mezcla(blanco, 0.2))
    L.cara([(128, 30), (128, 150), (86, 120)], mezcla(blanco, -0.12))
    L.cara([(86, 120), (128, 150), (128, 200), (92, 186)], mezcla(blanco, -0.25))
    L.cara([(170, 120), (164, 186), (128, 200), (128, 150)], mezcla(blanco, -0.05))
    L.cara([(92, 150), (40, 206), (98, 194)], hexa("4f7fb0"))
    L.cara([(164, 150), (216, 206), (158, 194)], mezcla(hexa("4f7fb0"), -0.2))
    L.circulo(128, 104, 14, hexa("72cbd9"))
    L.cara([(108, 200), (148, 200), (128, 236)], hexa("f0a040"), contorno=False)
    L.guardar("nave")


def tributo():
    """Moneda de Calcis: plata fría con una columna griega."""
    L = Lienzo()
    plata = hexa("d9dee4")
    L.circulo(128, 128, 92, mezcla(plata, -0.15))
    L.circulo(128, 128, 74, plata)
    L.cara([(96, 86), (160, 86), (152, 98), (104, 98)], mezcla(plata, -0.4), contorno=False)
    for x in (108, 128, 148):
        L.cara([(x - 6, 100), (x + 6, 100), (x + 6, 160), (x - 6, 160)], mezcla(plata, -0.35), contorno=False)
    L.cara([(96, 162), (160, 162), (164, 172), (92, 172)], mezcla(plata, -0.4), contorno=False)
    L.guardar("tributo")


def sol():
    L = Lienzo()
    oro = hexa("f2b84b")
    for i in range(12):
        a = math.radians(i * 30)
        b1 = math.radians(i * 30 - 9)
        b2 = math.radians(i * 30 + 9)
        L.cara([(128 + 62 * math.cos(b1), 128 + 62 * math.sin(b1)), (128 + 104 * math.cos(a), 128 + 104 * math.sin(a)),
                (128 + 62 * math.cos(b2), 128 + 62 * math.sin(b2))], mezcla(oro, -0.1 if i % 2 else 0.15))
    L.circulo(128, 128, 60, oro)
    L.cara([(96, 110), (126, 80), (140, 92), (108, 124)], mezcla(oro, 0.5), contorno=False)
    L.guardar("sol")


def luna():
    L = Lienzo()
    base = hexa("dfe4f0")
    img = Image.new("L", (LADO, LADO), 0)
    di = ImageDraw.Draw(img)
    di.ellipse([40, 40, 216, 216], fill=255)
    di.ellipse([92, 20, 250, 178], fill=0)
    capa = Image.new("RGBA", (LADO, LADO), base)
    capa.putalpha(img)
    L.img.alpha_composite(capa)
    L.guardar("luna")


def mochila():
    L = Lienzo()
    cuero = hexa("9a6236")
    L.cara([(70, 70), (186, 70), (204, 212), (52, 212)], cuero)
    L.cara([(52, 212), (204, 212), (196, 226), (60, 226)], mezcla(cuero, -0.4))
    L.cara([(70, 70), (186, 70), (196, 120), (60, 120)], mezcla(cuero, 0.2))
    L.cara([(84, 140), (172, 140), (176, 196), (80, 196)], mezcla(cuero, -0.15))
    L.d.arc([92, 30, 164, 104], 180, 360, fill=TINTA, width=20)
    L.d.arc([92, 30, 164, 104], 180, 360, fill=mezcla(cuero, -0.2), width=10)
    L.cara([(120, 112), (136, 112), (136, 132), (120, 132)], hexa("c9a04a"))
    L.guardar("mochila")


def hoja_de_contacto(nombres):
    cols = 7
    filas = (len(nombres) + cols - 1) // cols
    hoja = Image.new("RGBA", (cols * 120 + 20, filas * 130 + 20), (35, 27, 21, 255))
    for i, n in enumerate(nombres):
        img = Image.open(os.path.join(DESTINO, "Iconos", n + ".png"))
        x = 20 + (i % cols) * 120
        y = 20 + (i // cols) * 130
        hoja.alpha_composite(img, (x, y))
    return hoja


def fuentes(origen):
    from fontTools.ttLib import TTFont
    from fontTools.varLib.instancer import instantiateVariableFont

    carpeta = os.path.join(DESTINO, "Fuentes")
    os.makedirs(carpeta, exist_ok=True)
    cinzel = os.path.join(origen, "Cinzel[wght].ttf")
    for peso, nombre in ((600, "Cinzel-SemiBold"), (800, "Cinzel-ExtraBold")):
        f = TTFont(cinzel)
        f = instantiateVariableFont(f, {"wght": peso})
        f.save(os.path.join(carpeta, nombre + ".ttf"))
    for n in ("AlegreyaSans-Regular", "AlegreyaSans-Medium", "AlegreyaSans-Bold"):
        shutil.copy(os.path.join(origen, n + ".ttf"), os.path.join(carpeta, n + ".ttf"))
    shutil.copy(os.path.join(origen, "Cinzel-OFL.txt"), os.path.join(carpeta, "Cinzel-OFL.txt"))
    shutil.copy(os.path.join(origen, "AlegreyaSans-OFL.txt"), os.path.join(carpeta, "AlegreyaSans-OFL.txt"))


RECURSOS = ["madera", "mineral", "tablones", "lingotes", "chapa", "comida", "raciones",
            "cobre", "cristal", "agua", "cable", "bobinas", "lentes", "nucleos", "herramientas"]
INTERFAZ = ["confianza", "nave", "tributo", "sol", "luna", "mochila"]

if __name__ == "__main__":
    os.makedirs(os.path.join(DESTINO, "Iconos"), exist_ok=True)
    for n in RECURSOS + INTERFAZ:
        globals()[n]()
    if len(sys.argv) > 1:
        fuentes(sys.argv[1])
    previa = os.path.join(RAIZ, "previa")
    os.makedirs(previa, exist_ok=True)
    hoja_de_contacto(RECURSOS + INTERFAZ).save(os.path.join(previa, "iconos_ui.png"))
    print("UI generada en", DESTINO)
