"""
Arte de la interfaz nueva (portada, HUD y menú de construcción). Lo deja en
Assets/Nostos/Resources/UI:

- Lineas/: iconos de trazo blanco (se tiñen en Unity): continuar, nueva, ajustes, salir,
  construir, a pie, ayuda, menú, objetivo, cerrar, vivienda, produccion, servicios, camino,
  colonos, guardar, cargar.
- Adornos/: estrella de ocho puntas, columna jónica, flecha del minimapa.
- Hojas/: hojas de olivo y de roble y un pétalo, para el viento de la portada.
- Fondos/plano.png: la hoja del cuaderno de Nicias (papel, cuadrícula y bocetos de la nave)
  que hace de fondo del menú de construcción.
- Retratos/nicias_medallon.png: el retrato de Nicias recortado en círculo sobre el cielo.
  Necesita Retratos/nicias.png (lo hace generar_miniaturas.py).
- Mapa/valle.png: el valle visto desde arriba para el minimapa (lo hace este script con bpy
  si está disponible; si no, se salta).

Uso: python3 Pipeline/generar_ui_menu.py
"""
import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UI = os.path.join(RAIZ, "Assets", "Nostos", "Resources", "UI")
S = 4  # se dibuja a 4x y se reduce para suavizar


def carpeta(nombre):
    ruta = os.path.join(UI, nombre)
    os.makedirs(ruta, exist_ok=True)
    return ruta


# ------------------------------------------------------------------ iconos de trazo

class Trazo:
    """Iconos de línea en blanco sobre transparente, en una rejilla de 48 unidades."""

    def __init__(self, lado=96):
        self.lado = lado
        self.k = lado * S / 48.0
        self.img = Image.new("L", (lado * S, lado * S), 0)
        self.d = ImageDraw.Draw(self.img)
        self.g = 3.2  # grosor en unidades

    def p(self, x, y):
        return (x * self.k, y * self.k)

    def linea(self, puntos, cerrar=False, grosor=None):
        pts = [self.p(x, y) for x, y in puntos]
        if cerrar:
            pts.append(pts[0])
        w = int((grosor or self.g) * self.k)
        self.d.line(pts, fill=255, width=w, joint="curve")
        r = w / 2
        for x, y in pts:
            self.d.ellipse([x - r, y - r, x + r, y + r], fill=255)

    def lleno(self, puntos, color=255):
        self.d.polygon([self.p(x, y) for x, y in puntos], fill=color)

    def circulo(self, cx, cy, r, lleno=False, grosor=None, color=255):
        caja = [self.p(cx - r, cy - r), self.p(cx + r, cy + r)]
        if lleno:
            self.d.ellipse(caja, fill=color)
        else:
            self.d.ellipse(caja, outline=color, width=int((grosor or self.g) * self.k))

    def arco(self, cx, cy, r, a0, a1, grosor=None):
        caja = [self.p(cx - r, cy - r), self.p(cx + r, cy + r)]
        self.d.arc(caja, a0, a1, fill=255, width=int((grosor or self.g) * self.k))

    def caja(self, x0, y0, x1, y1, radio=3, lleno=False, grosor=None, color=255):
        c = [self.p(x0, y0), self.p(x1, y1)]
        if lleno:
            self.d.rounded_rectangle(c, radius=radio * self.k, fill=color)
        else:
            self.d.rounded_rectangle(c, radius=radio * self.k, outline=color, width=int((grosor or self.g) * self.k))

    def guardar(self, ruta):
        alfa = self.img.resize((self.lado, self.lado), Image.LANCZOS)
        out = Image.new("RGBA", alfa.size, (255, 255, 255, 0))
        out.putalpha(alfa)
        out.save(ruta)


def iconos_linea():
    dst = carpeta("Lineas")

    t = Trazo()
    t.lleno([(15, 9), (39, 24), (15, 39)])
    t.guardar(os.path.join(dst, "continuar.png"))

    # Nueva partida: una hoja con la esquina doblada y una estrella pequeña.
    t = Trazo()
    t.linea([(11, 6), (29, 6), (37, 14), (37, 42), (11, 42)], cerrar=True)
    t.linea([(29, 6), (29, 14), (37, 14)])
    t.linea([(24, 22), (24, 34)])
    t.linea([(18, 28), (30, 28)])
    t.guardar(os.path.join(dst, "nueva.png"))

    # Ajustes: rueda dentada.
    t = Trazo()
    dientes = []
    for i in range(16):
        a = i / 16 * 2 * math.pi
        r = 19 if i % 2 == 0 else 15
        for da in (-0.17, 0.17):
            dientes.append((24 + r * math.cos(a + da), 24 + r * math.sin(a + da)))
    t.linea(dientes, cerrar=True, grosor=2.8)
    t.circulo(24, 24, 6.5)
    t.guardar(os.path.join(dst, "ajustes.png"))

    # Salir: puerta con flecha.
    t = Trazo()
    t.linea([(28, 12), (28, 6), (10, 6), (10, 42), (28, 42), (28, 36)])
    t.linea([(18, 24), (42, 24)])
    t.linea([(35, 17), (42, 24), (35, 31)])
    t.guardar(os.path.join(dst, "salir.png"))

    # Construir: martillo.
    t = Trazo()
    t.lleno([(14, 8), (34, 8), (38, 12), (38, 18), (34, 18), (31, 15), (17, 15), (14, 18), (10, 14)])
    t.linea([(24, 16), (24, 42)], grosor=5)
    t.guardar(os.path.join(dst, "construir.png"))

    # A pie: huella de un pie descalzo (planta y dedos).
    t = Trazo()
    t.d.ellipse([t.p(14, 16), t.p(32, 44)], fill=255)
    t.d.ellipse([t.p(18, 30), t.p(30, 46)], fill=255)
    for cx, cy, r in ((16.5, 10.5, 3.6), (23, 7, 3.2), (28.5, 7.5, 2.8), (33, 10, 2.4), (36, 14, 2.1)):
        t.circulo(cx, cy, r, lleno=True)
    t.guardar(os.path.join(dst, "apie.png"))

    t = Trazo()
    t.circulo(24, 24, 18)
    t.arco(24, 19, 6.5, 180, 360 + 60)
    t.linea([(27.2, 24.5), (24, 28), (24, 30)])
    t.circulo(24, 36, 2.2, lleno=True)
    t.guardar(os.path.join(dst, "ayuda.png"))

    t = Trazo()
    for y in (13, 24, 35):
        t.linea([(9, y), (39, y)])
    t.guardar(os.path.join(dst, "menu.png"))

    # Objetivo: rombo con otro dentro.
    t = Trazo()
    t.linea([(24, 4), (44, 24), (24, 44), (4, 24)], cerrar=True)
    t.lleno([(24, 14), (34, 24), (24, 34), (14, 24)])
    t.guardar(os.path.join(dst, "objetivo.png"))

    t = Trazo()
    t.linea([(12, 12), (36, 36)], grosor=4)
    t.linea([(36, 12), (12, 36)], grosor=4)
    t.guardar(os.path.join(dst, "cerrar.png"))

    # Vivienda: casa con tejado a dos aguas.
    t = Trazo()
    t.linea([(5, 23), (24, 7), (43, 23)])
    t.linea([(11, 19), (11, 41), (37, 41), (37, 19)])
    t.caja(20, 28, 28, 41, radio=1)
    t.guardar(os.path.join(dst, "vivienda.png"))

    # Producción: yunque.
    t = Trazo()
    t.lleno([(6, 14), (36, 14), (42, 18), (36, 22), (32, 22), (28, 28), (32, 34), (16, 34), (20, 28), (14, 22), (10, 22)])
    t.caja(10, 37, 38, 42, radio=1.5, lleno=True)
    t.guardar(os.path.join(dst, "produccion.png"))

    # Servicios: ánfora.
    t = Trazo()
    perfil = [(19, 6), (29, 6), (28, 12), (34, 18), (35, 28), (30, 38), (24, 43), (18, 38), (13, 28), (14, 18), (20, 12)]
    t.linea(perfil, cerrar=True)
    t.arco(14, 16, 5, 90, 270)
    t.arco(34, 16, 5, 270, 450)
    t.linea([(16, 24), (32, 24)], grosor=2.2)
    t.guardar(os.path.join(dst, "servicios.png"))

    # Camino: losas en perspectiva.
    t = Trazo()
    t.linea([(16, 6), (32, 6), (42, 42), (6, 42)], cerrar=True)
    t.linea([(13, 18), (35, 18)], grosor=2.4)
    t.linea([(9, 30), (39, 30)], grosor=2.4)
    t.linea([(24, 6), (24, 42)], grosor=2.4)
    t.guardar(os.path.join(dst, "camino.png"))

    # Colonos: dos bustos.
    t = Trazo()
    t.circulo(17, 15, 6, lleno=True)
    t.d.pieslice([t.p(5, 24), t.p(29, 50)], 180, 360, fill=255)
    t.circulo(33, 13, 5.5, lleno=True)
    t.d.pieslice([t.p(23, 21), t.p(43, 45)], 180, 360, fill=255)
    t.guardar(os.path.join(dst, "colonos.png"))

    # Guardar: tablilla de cera con estilo.
    t = Trazo()
    t.caja(8, 8, 40, 40, radio=4)
    t.linea([(14, 18), (34, 18)], grosor=2.4)
    t.linea([(14, 25), (34, 25)], grosor=2.4)
    t.linea([(14, 32), (26, 32)], grosor=2.4)
    t.guardar(os.path.join(dst, "guardar.png"))

    # Cargar: flecha circular.
    t = Trazo()
    t.arco(24, 24, 15, 40, 330)
    t.lleno([(36, 3), (40, 17), (27, 13)])
    t.guardar(os.path.join(dst, "cargar.png"))


# ------------------------------------------------------------------ adornos

def estrella(radio_ext, radio_int, puntas, lado):
    img = Image.new("L", (lado * S, lado * S), 0)
    d = ImageDraw.Draw(img)
    c = lado * S / 2
    pts = []
    for i in range(puntas * 2):
        a = i / (puntas * 2) * 2 * math.pi - math.pi / 2
        r = (radio_ext if i % 2 == 0 else radio_int) * lado * S / 2
        pts.append((c + r * math.cos(a), c + r * math.sin(a)))
    d.polygon(pts, fill=255)
    alfa = img.resize((lado, lado), Image.LANCZOS)
    out = Image.new("RGBA", (lado, lado), (255, 255, 255, 0))
    out.putalpha(alfa)
    return out


def columna():
    """Columna jónica de frente, maciza, blanca (se tiñe)."""
    an, al = 96, 112
    img = Image.new("L", (an * S, al * S), 0)
    d = ImageDraw.Draw(img)
    k = S

    def r(x0, y0, x1, y1):
        d.rectangle([x0 * k, y0 * k, x1 * k, y1 * k], fill=255)

    # ábaco y volutas
    r(10, 6, 86, 12)
    d.rounded_rectangle([18 * k, 13 * k, 78 * k, 22 * k], radius=4 * k, fill=255)
    for cx in (20, 76):
        d.ellipse([(cx - 10) * k, 12 * k, (cx + 10) * k, 32 * k], fill=255)
        d.ellipse([(cx - 5) * k, 17 * k, (cx + 5) * k, 27 * k], fill=0)
        d.ellipse([(cx - 2) * k, 20 * k, (cx + 2) * k, 24 * k], fill=255)
    # equino
    r(28, 30, 68, 34)
    # fuste con estrías (huecos finos)
    d.polygon([(30 * k, 35 * k), (66 * k, 35 * k), (68 * k, 96 * k), (28 * k, 96 * k)], fill=255)
    for i in range(1, 6):
        x = 30 + i * 6
        d.line([((x) * k, 38 * k), ((x - (i - 3) * 0.3) * k, 93 * k)], fill=0, width=int(1.8 * k))
    # basa
    r(24, 96, 72, 100)
    d.rounded_rectangle([20 * k, 100 * k, 76 * k, 105 * k], radius=2 * k, fill=255)
    r(16, 106, 80, 110)
    alfa = img.resize((an, al), Image.LANCZOS)
    out = Image.new("RGBA", (an, al), (255, 255, 255, 0))
    out.putalpha(alfa)
    return out


def adornos():
    dst = carpeta("Adornos")
    estrella(1.0, 0.36, 4, 96).save(os.path.join(dst, "estrella.png"))
    estrella(1.0, 0.42, 8, 96).save(os.path.join(dst, "estrella8.png"))
    columna().save(os.path.join(dst, "columna.png"))
    # Flecha del minimapa: punta de lanza.
    lado = 64
    img = Image.new("L", (lado * S, lado * S), 0)
    d = ImageDraw.Draw(img)
    k = S
    d.polygon([(32 * k, 4 * k), (54 * k, 56 * k), (32 * k, 44 * k), (10 * k, 56 * k)], fill=255)
    alfa = img.resize((lado, lado), Image.LANCZOS)
    out = Image.new("RGBA", (lado, lado), (255, 255, 255, 0))
    out.putalpha(alfa)
    out.save(os.path.join(dst, "flecha.png"))
    # Punto suave (motas de luz, sombras).
    lado = 64
    img = Image.new("L", (lado, lado), 0)
    for y in range(lado):
        for x in range(lado):
            dd = math.hypot(x - 31.5, y - 31.5) / 31.5
            img.putpixel((x, y), int(255 * max(0.0, 1 - dd) ** 2))
    out = Image.new("RGBA", (lado, lado), (255, 255, 255, 0))
    out.putalpha(img)
    out.save(os.path.join(dst, "mota.png"))


# ------------------------------------------------------------------ hojas

def hoja(color, nervio, ancho, largo, punta=0.0, lobulos=0, lado=64):
    """Hoja vista de frente, apuntando hacia arriba, con nervio central."""
    img = Image.new("RGBA", (lado * S, lado * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = lado * S / 2
    L = largo * lado * S / 2
    W = ancho * lado * S / 2
    izq, der = [], []
    n = 40
    for i in range(n + 1):
        t = i / n
        y = c + L - 2 * L * t
        w = W * math.sin(math.pi * t) ** (0.8 + punta)
        if lobulos:
            w *= 0.82 + 0.18 * abs(math.sin(t * math.pi * lobulos))
        izq.append((c - w, y))
        der.append((c + w, y))
    contorno = izq + der[::-1]
    d.polygon(contorno, fill=color)
    # media hoja en sombra para que parezca facetada
    sombra = tuple(int(v * 0.82) for v in color[:3]) + (255,)
    d.polygon(der + [(c, c - L), (c, c + L)][::-1], fill=sombra)
    d.line([(c, c + L * 1.18), (c, c - L * 0.92)], fill=nervio, width=int(S * 1.6))
    img = img.resize((lado, lado), Image.LANCZOS)
    return img


def hojas():
    dst = carpeta("Hojas")
    random.seed(4)
    hoja((128, 150, 96, 255), (196, 206, 160, 255), 0.3, 0.86, punta=0.5).save(os.path.join(dst, "olivo.png"))
    hoja((168, 176, 120, 255), (222, 226, 180, 255), 0.28, 0.8, punta=0.6).save(os.path.join(dst, "olivo_claro.png"))
    hoja((204, 150, 70, 255), (240, 206, 140, 255), 0.42, 0.78, lobulos=4).save(os.path.join(dst, "roble.png"))
    hoja((236, 214, 222, 255), (250, 238, 240, 255), 0.4, 0.5, punta=-0.3).save(os.path.join(dst, "petalo.png"))


# ------------------------------------------------------------------ papel del cuaderno

def ruido(an, al, escala, semilla):
    random.seed(semilla)
    pequeno = Image.new("L", (max(2, an // escala), max(2, al // escala)))
    pequeno.putdata([random.randint(0, 255) for _ in range(pequeno.width * pequeno.height)])
    return pequeno.resize((an, al), Image.BICUBIC).filter(ImageFilter.GaussianBlur(escala / 3))


def plano():
    an, al = 1024, 640
    papel = Image.new("RGB", (an, al), (233, 220, 192))
    # manchas grandes y medianas
    for escala, fuerza, semilla in ((160, 22, 1), (40, 12, 2), (8, 6, 3)):
        r = ruido(an, al, escala, semilla)
        oscuro = Image.new("RGB", (an, al), (150, 118, 72))
        mascara = r.point(lambda v, f=fuerza: int(max(0, v - 128) * f / 127))
        papel = Image.composite(oscuro, papel, mascara)
    d = ImageDraw.Draw(papel, "RGBA")
    # fibras
    random.seed(9)
    for _ in range(1400):
        x, y = random.uniform(0, an), random.uniform(0, al)
        a = random.uniform(0, math.pi)
        l = random.uniform(3, 12)
        d.line([(x, y), (x + math.cos(a) * l, y + math.sin(a) * l)], fill=(120, 92, 58, random.randint(14, 34)), width=1)
    # cuadrícula de dibujante, en tinta sepia muy gastada
    tinta = (92, 70, 48)
    for x in range(0, an, 16):
        d.line([(x, 0), (x, al)], fill=tinta + ((26 if x % 128 == 0 else 11),), width=1)
    for y in range(0, al, 16):
        d.line([(0, y), (an, y)], fill=tinta + ((26 if y % 128 == 0 else 11),), width=1)

    # bocetos de la nave de Nicias: alzado del casco, círculos de compás, cotas
    boceto = Image.new("RGBA", (an, al), (0, 0, 0, 0))
    b = ImageDraw.Draw(boceto)
    trazo = (70, 52, 36, 70)
    fino = (70, 52, 36, 45)
    # casco (perfil) a la derecha, grande y cortado por el borde
    cx, cy = 820, 420
    casco = []
    for i in range(61):
        t = i / 60
        x = cx - 300 + 520 * t
        y = cy - 70 * math.sin(math.pi * t) ** 0.6 - 8 * t
        casco.append((x, y))
    quilla = []
    for i in range(61):
        t = 1 - i / 60
        x = cx - 300 + 520 * t
        y = cy + 60 * math.sin(math.pi * t) ** 0.9
        quilla.append((x, y))
    b.line(casco + quilla + [casco[0]], fill=trazo, width=2)
    for i in range(1, 9):
        x = cx - 300 + i * 58
        b.line([(x, cy - 66), (x, cy + 58)], fill=fino, width=1)
    b.line([(cx - 330, cy), (cx + 260, cy)], fill=fino, width=1)
    # aletas
    b.line([(cx - 240, cy - 40), (cx - 330, cy - 120), (cx - 180, cy - 64)], fill=trazo, width=2)
    b.line([(cx - 240, cy + 40), (cx - 320, cy + 116), (cx - 180, cy + 52)], fill=trazo, width=2)
    # cota
    yc = cy + 110
    b.line([(cx - 300, yc), (cx + 220, yc)], fill=trazo, width=1)
    for x in (cx - 300, cx + 220):
        b.line([(x, yc - 8), (x, yc + 8)], fill=trazo, width=1)
    # círculos de compás arriba a la izquierda
    for r, (x, y) in ((90, (150, 150)), (64, (230, 110)), (130, (190, 170))):
        b.ellipse([x - r, y - r, x + r, y + r], outline=fino, width=1)
    b.line([(40, 300), (420, 40)], fill=fino, width=1)
    # una columna en alzado, a medio dibujar
    x0, y0 = 520, 80
    b.rectangle([x0, y0, x0 + 60, y0 + 8], outline=fino)
    b.line([(x0 + 10, y0 + 8), (x0 + 12, y0 + 150)], fill=fino)
    b.line([(x0 + 50, y0 + 8), (x0 + 48, y0 + 150)], fill=fino)
    for i in range(1, 4):
        b.line([(x0 + 10 + i * 10, y0 + 12), (x0 + 10 + i * 10, y0 + 146)], fill=(70, 52, 36, 25))
    boceto = boceto.filter(ImageFilter.GaussianBlur(0.6))
    papel = papel.convert("RGBA")
    papel.alpha_composite(boceto)

    # bordes tostados
    vi = Image.new("L", (an, al), 0)
    dv = ImageDraw.Draw(vi)
    for i in range(40):
        dv.rounded_rectangle([i, i, an - 1 - i, al - 1 - i], radius=24, outline=int(150 * (1 - i / 40) ** 2))
    vi = vi.filter(ImageFilter.GaussianBlur(6))
    tostado = Image.new("RGBA", (an, al), (110, 74, 40, 255))
    tostado.putalpha(vi)
    papel.alpha_composite(tostado)
    papel.convert("RGB").save(os.path.join(carpeta("Fondos"), "plano.png"))


# ------------------------------------------------------------------ Calcis en el cielo

def calcis(lado=512):
    """El planeta de Calcis visto desde Lacio: disco pálido con regiones facetadas, iluminado de
    lado, con un halo de atmósfera. Se dibuja en el cielo (CieloCalcis.cs)."""
    import numpy as np
    random.seed(21)
    n = lado
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    c = (n - 1) / 2
    r = n * 0.42
    dx, dy = (xx - c) / r, (yy - c) / r
    d2 = dx * dx + dy * dy
    dentro = d2 <= 1.0
    dz = np.sqrt(np.clip(1 - d2, 0, 1))
    # regiones: Voronoi sobre la esfera proyectada
    semillas = [(random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(0, 1)) for _ in range(70)]
    tonos = [(232, 228, 242), (214, 206, 232), (196, 186, 220), (178, 168, 206), (240, 236, 246)]
    mejor = np.full((n, n), 9.0, dtype=np.float32)
    region = np.zeros((n, n), dtype=np.int32)
    for i, (sx, sy, sz) in enumerate(semillas):
        dist = (dx - sx) ** 2 + (dy - sy) ** 2 + (dz - sz) ** 2 * 0.6
        menor = dist < mejor
        mejor[menor] = dist[menor]
        region[menor] = i
    color = np.zeros((n, n, 3), dtype=np.float32)
    for i in range(len(semillas)):
        color[region == i] = tonos[i % len(tonos)]
    # luz desde arriba a la izquierda, terminador suave
    lx, ly, lz = -0.55, -0.45, 0.70
    norma = math.sqrt(lx * lx + ly * ly + lz * lz)
    lam = np.clip((dx * lx + dy * ly + dz * lz) / norma, 0, 1)
    luz = 0.38 + 0.62 * lam ** 0.7
    color *= luz[..., None]
    sombra = np.array([70, 74, 120], dtype=np.float32)
    k = np.clip(1 - lam * 2.5, 0, 1)[..., None] * 0.55
    color = color * (1 - k) + sombra * k
    alfa = np.where(dentro, 235.0, 0.0)
    # borde suave del disco y halo de atmósfera
    borde = np.clip((1.0 - np.sqrt(d2)) * r / 1.5, 0, 1)
    alfa = alfa * borde
    halo = np.clip(1 - (np.sqrt(d2) - 1) / 0.12, 0, 1) * (np.sqrt(d2) > 1)
    halo = halo ** 2 * 120
    color = np.where(dentro[..., None], color, np.array([225, 230, 255], dtype=np.float32))
    alfa = np.maximum(alfa, halo)
    img = np.dstack([np.clip(color, 0, 255), np.clip(alfa, 0, 255)]).astype(np.uint8)
    Image.fromarray(img, "RGBA").save(os.path.join(carpeta("Cielo"), "calcis.png"))


# ------------------------------------------------------------------ medallón

def medallon():
    origen = os.path.join(UI, "Retratos", "nicias.png")
    if not os.path.exists(origen):
        print("Falta Retratos/nicias.png: ejecuta antes generar_miniaturas.py")
        return
    lado = 256
    retrato = Image.open(origen).convert("RGBA")
    # Se acerca un poco y se sube para que la cara quede en el centro del círculo.
    w = retrato.width
    retrato = retrato.crop((int(w * 0.06), int(w * 0.14), int(w * 0.94), int(w * 1.02)))
    retrato = retrato.resize((lado, lado), Image.LANCZOS)
    fondo = Image.new("RGBA", (lado, lado))
    arriba, abajo = (122, 150, 214), (232, 196, 160)
    for y in range(lado):
        t = y / (lado - 1)
        c = tuple(int(arriba[i] + (abajo[i] - arriba[i]) * t) for i in range(3)) + (255,)
        ImageDraw.Draw(fondo).line([(0, y), (lado, y)], fill=c)
    fondo.alpha_composite(retrato)
    mascara = Image.new("L", (lado * S, lado * S), 0)
    ImageDraw.Draw(mascara).ellipse([0, 0, lado * S - 1, lado * S - 1], fill=255)
    mascara = mascara.resize((lado, lado), Image.LANCZOS)
    fondo.putalpha(mascara)
    fondo.save(os.path.join(UI, "Retratos", "nicias_medallon.png"))


if __name__ == "__main__":
    iconos_linea()
    adornos()
    hojas()
    plano()
    calcis()
    medallon()
    print("Arte de la interfaz en", UI)
