import bpy, bmesh, math, random, sys
from mathutils import Vector, Euler, Matrix

R = random.Random(7)
C = {
 "yeso": (232, 218, 190), "yeso2": (220, 202, 170), "yeso3": (240, 228, 204),
 "piedra": (176, 168, 160), "piedra2": (150, 144, 142), "piedra3": (196, 186, 172), "piedra4": (132, 124, 132),
 "teja": (196, 96, 62), "teja2": (214, 118, 70), "teja3": (172, 78, 56), "teja4": (226, 140, 84), "teja5": (150, 70, 60),
 "madera": (122, 82, 52), "madera2": (98, 64, 42), "madera3": (150, 106, 66),
 "azul": (58, 96, 178), "azul2": (44, 76, 150), "azul3": (84, 124, 200), "blanco": (236, 236, 228),
 "vidrio": (40, 52, 84), "luz": (255, 206, 120), "hierro": (60, 58, 66),
 "hoja": (104, 142, 70), "hoja2": (132, 162, 78), "hoja3": (80, 118, 66), "flor": (226, 190, 70), "flor2": (206, 84, 80),
 "hierba": (150, 162, 78), "hierba2": (128, 148, 70), "tierra": (176, 142, 100), "tierra2": (160, 126, 90),
 "ladrillo": (170, 104, 80), "ladrillo2": (150, 90, 72),
}
def lin(c): 
    c /= 255.0
    return c/12.92 if c <= 0.04045 else ((c+0.055)/1.055)**2.4

class B:
    def __init__(s):
        s.bm = bmesh.new(); s.col = s.bm.loops.layers.float_color.new("Col"); s.M = Matrix.Identity(4); s.pila = []
    def push(s, pos=(0,0,0), rz=0):
        s.pila.append(s.M.copy()); s.M = s.M @ Matrix.Translation(pos) @ Matrix.Rotation(math.radians(rz), 4, 'Z')
    def pop(s): s.M = s.pila.pop()
    def _fin(s, nuevos, color, var):
        if isinstance(color, (list, tuple)) and isinstance(color[0], str): color = R.choice(color)
        rgb = C[color] if isinstance(color, str) else color
        k = 1 + R.uniform(-var, var); t = R.uniform(-var, var) * 0.5
        rgba = (lin(min(255, rgb[0]*k*(1+t))), lin(min(255, rgb[1]*k)), lin(min(255, rgb[2]*k*(1-t))), 1)
        for v in nuevos: v.co = s.M @ v.co
        for f in {f for v in nuevos for f in v.link_faces}:
            f.smooth = False
            for l in f.loops: l[s.col] = rgba
    def box(s, color, tam, c, rot=(0,0,0), bisel=0.025, var=0.05, deform=0.0):
        a = set(s.bm.verts); bmesh.ops.create_cube(s.bm, size=1.0)
        nv = [v for v in s.bm.verts if v not in a]
        for v in nv:
            v.co = Vector((v.co.x*tam[0], v.co.y*tam[1], v.co.z*tam[2]))
            if deform: v.co += Vector((R.uniform(-deform, deform), R.uniform(-deform, deform), R.uniform(-deform, deform)))
        if bisel and min(tam) > bisel*3:
            bmesh.ops.bevel(s.bm, geom=list({e for v in nv for e in v.link_edges}), offset=bisel, segments=1, affect='EDGES', profile=0.5)
            nv = [v for v in s.bm.verts if v not in a]
        g = Euler([math.radians(x) for x in rot], 'XYZ').to_matrix(); cc = Vector(c)
        for v in nv: v.co = g @ v.co + cc
        s._fin(nv, color, var)
    def cil(s, color, r, h, c, lados=10, rot=(0,0,0), r2=None, var=0.05):
        a = set(s.bm.verts)
        bmesh.ops.create_cone(s.bm, cap_ends=True, segments=lados, radius1=r, radius2=r if r2 is None else r2, depth=h)
        nv = [v for v in s.bm.verts if v not in a]
        g = Euler([math.radians(x) for x in rot], 'XYZ').to_matrix(); cc = Vector(c)
        for v in nv: v.co = g @ v.co + cc
        s._fin(nv, color, var)
    def bola(s, color, r, c, esc=(1,1,1), ruido=0.18, var=0.06, sub=1):
        a = set(s.bm.verts); bmesh.ops.create_icosphere(s.bm, subdivisions=sub, radius=r)
        nv = [v for v in s.bm.verts if v not in a]; cc = Vector(c)
        for v in nv:
            v.co *= 1 + R.uniform(-ruido, ruido)
            v.co = Vector((v.co.x*esc[0], v.co.y*esc[1], v.co.z*esc[2])) + cc
        s._fin(nv, color, var)
    def prisma(s, color, pts, y0, y1, var=0.03):
        """Polígono (x, z) extruido de y0 a y1."""
        va = [s.bm.verts.new((x, y0, z)) for x, z in pts]; vb = [s.bm.verts.new((x, y1, z)) for x, z in pts]
        n = len(pts); s.bm.faces.new(va); s.bm.faces.new(vb[::-1])
        for i in range(n): s.bm.faces.new((va[i], vb[i], vb[(i+1) % n], va[(i+1) % n]))
        s._fin(va+vb, color, var)

PIEDRAS = ["piedra", "piedra2", "piedra3", "piedra", "piedra3"]
TEJAS = ["teja", "teja", "teja2", "teja2", "teja3", "teja4", "teja5"]

def fila_piedras(b, x0, x1, y, z, alto, fondo, eje='x'):
    """Hilada de sillares irregulares de x0 a x1 (eje local x), cara exterior en y."""
    x = x0
    while x < x1 - 0.05:
        w = min(R.uniform(0.38, 0.8), x1 - x)
        if x1 - (x + w) < 0.25: w = x1 - x
        h = alto * R.uniform(0.86, 1.0)
        b.box(PIEDRAS, (w - 0.02, fondo * R.uniform(0.85, 1.1), h), (x + w/2, y, z + h/2), bisel=0.04, var=0.07, deform=0.012)
        x += w

def muro(b, w, d, h, zocalo=0.55):
    """Cuerpo de yeso con zócalo de sillares, esquineras y manchas. Centrado en el origen local, base en z=0."""
    b.box("yeso", (w, d, h), (0, 0, h/2), bisel=0.03, var=0.015)
    for rz, L, off in ((0, w, d/2), (180, w, d/2), (90, d, w/2), (270, d, w/2)):
        b.push(rz=rz)
        fila_piedras(b, -L/2 - 0.06, L/2 + 0.06, -off - 0.03, 0, zocalo, 0.2)
        fila_piedras(b, -L/2 - 0.03, L/2 + 0.03, -off - 0.01, zocalo * 0.92, 0.16, 0.12)
        # desconchones con ladrillo de piedra visto
        for _ in range(int(L * 1.1)):
            px = R.uniform(-L/2 + 0.7, L/2 - 0.7); pz = R.uniform(zocalo + 0.5, h - 0.4)
            for i in range(R.randint(1, 3)):
                b.box(["piedra3", "yeso2", "piedra"], (R.uniform(0.28, 0.42), 0.05, 0.17), (px + i*0.2 - 0.2 + R.uniform(-.05,.05), -off - 0.005, pz + (i % 2)*0.19), bisel=0.02, var=0.05)
        for _ in range(int(L * 0.6)):
            b.box(["yeso2", "yeso3"], (R.uniform(0.5, 1.1), 0.03, R.uniform(0.3, 0.7)), (R.uniform(-L/2 + 0.8, L/2 - 0.8), -off - 0.002, R.uniform(zocalo + 0.5, h - 0.5)), bisel=0.012, var=0.02)
        b.pop()
    # esquineras alternas
    for sx in (-1, 1):
        for sy in (-1, 1):
            z = zocalo + 0.12; i = 0
            while z < h - 0.3:
                hh = R.uniform(0.3, 0.4); largo = R.uniform(0.5, 0.66); corto = R.uniform(0.3, 0.38)
                tx, ty = (largo, corto) if i % 2 else (corto, largo)
                b.box(PIEDRAS, (tx, ty, hh - 0.025), (sx * (w/2 + 0.035 - tx/2), sy * (d/2 + 0.035 - ty/2), z + hh/2), bisel=0.035, var=0.07, deform=0.01)
                z += hh; i += 1

def ventana(b, ancho=0.72, alto=1.05, contras=True, jardinera=False, luz=False):
    """En el origen local, mirando a -Y, pegada a la pared (y=0), z = centro."""
    b.box("luz" if luz else "vidrio", (ancho, 0.06, alto), (0, 0.0, 0), bisel=0, var=0.03)
    m = 0.075
    for sx in (-1, 1): b.box("madera2", (m, 0.1, alto + m), (sx * ancho/2, -0.03, 0), bisel=0.015)
    b.box("madera2", (ancho + m, 0.1, m), (0, -0.03, -alto/2), bisel=0.015)
    b.box("madera3", (0.045, 0.07, alto), (0, -0.03, 0), bisel=0.01); b.box("madera3", (ancho, 0.07, 0.045), (0, -0.03, alto*0.12), bisel=0.01)
    # dintel de tres dovelas y alféizar
    for i, dx in enumerate((-0.36, 0, 0.36)):
        b.box(PIEDRAS, (0.36 if i != 1 else 0.3, 0.16, 0.2 if i != 1 else 0.25), (dx * (ancho + 0.3) / 1.02 / 1.0 * 1.0, -0.05, alto/2 + 0.14), rot=(0, -dx*14, 0), bisel=0.03, var=0.07)
    b.box("piedra3", (ancho + 0.36, 0.24, 0.1), (0, -0.09, -alto/2 - 0.07), bisel=0.03)
    for sx in (-1, 1): b.box("piedra2", (0.09, 0.2, 0.1), (sx * (ancho/2 + 0.05), -0.07, -alto/2 - 0.16), bisel=0.02)
    if contras:
        for sx in (-1, 1):
            ang = sx * R.uniform(8, 22); cw = ancho/2 + 0.02
            b.push(pos=(sx * (ancho/2 + m/2 + 0.01), -0.07, 0), rz=ang)
            for k in range(3):
                b.box(["azul", "azul2", "azul3"], (cw/3 - 0.008, 0.045, alto), (sx * (cw/6 + k*cw/3), 0, 0), bisel=0.01, var=0.06)
            for zz in (-alto*0.3, alto*0.3): b.box("azul2", (cw, 0.03, 0.07), (sx * cw/2, -0.035, zz), bisel=0.01)
            b.pop()
    if jardinera:
        z = -alto/2 - 0.03
        b.box("madera", (ancho + 0.2, 0.26, 0.2), (0, -0.3, z + 0.08), bisel=0.02)
        for sx in (-1, 1): b.box("madera2", (0.06, 0.28, 0.08), (sx * ancho*0.4, -0.27, z - 0.05), rot=(-30, 0, 0), bisel=0.01)
        n = 5
        for i in range(n):
            x = (i/(n-1) - 0.5) * ancho
            b.bola(["hoja", "hoja2", "hoja3"], 0.13, (x, -0.3, z + 0.24 + R.uniform(0, .05)), ruido=0.25)
            b.bola(["flor", "flor2", "blanco"], 0.05, (x + R.uniform(-.06, .06), -0.36, z + 0.34 + R.uniform(0, .06)), ruido=0.1)

def puerta(b, ancho=1.0, alto=1.55):
    """Puerta de arco, mirando a -Y, base en z=0."""
    r = ancho/2
    b.box("vidrio", (ancho + 0.1, 0.08, alto), (0, 0.02, alto/2), bisel=0)
    b.cil("vidrio", r + 0.05, 0.08, (0, 0.02, alto), lados=16, rot=(90, 0, 0))
    n = 5
    for i in range(n):
        x = -r + (i + 0.5) * ancho/n
        h = alto + math.sqrt(max(0, r*r - x*x)) - 0.02
        b.box(["azul", "azul2", "azul3", "azul"], (ancho/n - 0.012, 0.07, h), (x, -0.03, h/2), bisel=0.012, var=0.06)
    for zz in (0.35, alto - 0.1): 
        b.box("hierro", (ancho - 0.06, 0.03, 0.07), (0, -0.075, zz), bisel=0.008)
    b.bola("luz", 0.045, (r - 0.16, -0.1, alto*0.55), ruido=0)
    # jambas y dovelas
    for sx in (-1, 1):
        z = 0; i = 0
        while z < alto - 0.05:
            hh = min(R.uniform(0.3, 0.42), alto - z)
            b.box(PIEDRAS, (0.3 + (i % 2)*0.1, 0.24, hh - 0.02), (sx * (r + 0.17 + (i % 2)*0.05), -0.06, z + hh/2), bisel=0.035, var=0.07, deform=0.01)
            z += hh; i += 1
    nd = 9
    for i in range(nd):
        a = math.pi * (i + 0.5) / nd; rr = r + 0.2
        clave = i == nd // 2
        b.box("piedra3" if clave else PIEDRAS, (0.36 if clave else 0.3, 0.26 if not clave else 0.32, math.pi*rr/nd * (1.02 if not clave else 1.15)),
              (math.cos(a) * rr, -0.07, alto + math.sin(a) * rr), rot=(0, 90 - math.degrees(a) + 90, 0), bisel=0.035, var=0.07)
    # escalones
    b.box("piedra", (ancho + 1.0, 0.75, 0.16), (0, -0.4, 0.06), bisel=0.04, deform=0.012)
    b.box("piedra3", (ancho + 0.5, 0.42, 0.16), (0, -0.25, 0.2), bisel=0.04, deform=0.012)

def estandarte(b, largo=1.3, ancho=0.5):
    """Cuelga de un asta que sale de la pared; origen = arranque del asta, mirando a -Y."""
    b.cil("madera2", 0.03, 0.75, (0, -0.33, 0), lados=6, rot=(90, 0, 0)); b.bola("luz", 0.055, (0, -0.72, 0), ruido=0)
    b.box("hierro", (0.04, 0.3, 0.04), (0, -0.16, -0.14), rot=(45, 0, 0), bisel=0)
    b.push(pos=(0, -0.5, -0.04), rz=90)
    b.box("azul", (ancho, 0.03, largo), (0, 0, -largo/2), bisel=0.008, var=0.03)
    b.prisma("azul", [(-ancho/2, -largo + 0.01), (ancho/2, -largo + 0.01), (0, -largo - 0.28)], -0.015, 0.015)
    for sy in (-1, 1):
        b.box("blanco", (ancho, 0.012, 0.05), (0, sy*0.016, -0.12), bisel=0); b.box("blanco", (ancho, 0.012, 0.05), (0, sy*0.016, -largo + 0.1), bisel=0)
        b.box("blanco", (0.2, 0.012, 0.2), (0, sy*0.016, -largo*0.5), rot=(0, 45, 0), bisel=0)
        b.box("azul", (0.1, 0.014, 0.1), (0, sy*0.017, -largo*0.5), rot=(0, 45, 0), bisel=0)
    b.pop()

def tejado(b, w, L, z0, grados=38, vuelo=0.4, vuelo_y=0.35, hastial="yeso"):
    """A dos aguas, cumbrera a lo largo de Y local. w = ancho del muro en X, L = largo en Y. Devuelve altura de cumbrera."""
    p = math.radians(grados); alto = (w/2) * math.tan(p); zc = z0 + alto
    b.prisma(hastial, [(-w/2, z0 - 0.02), (w/2, z0 - 0.02), (0, zc)], -L/2, L/2, var=0.015)
    # tablero bajo las tejas, cabios y vigas del alero
    lp = (w/2 + vuelo) / math.cos(p)
    for s in (-1, 1):
        cx = s * (w/2 + vuelo)/2; cz = zc - (w/2 + vuelo)/2 * math.tan(p)
        b.box("madera2", (lp, L + 2*vuelo_y - 0.06, 0.07), (cx, 0, cz + 0.03), rot=(0, s*grados, 0), bisel=0.01)
        n = max(3, int(L / 0.7))
        for i in range(n + 1):
            y = -L/2 - vuelo_y + 0.1 + i * (L + 2*vuelo_y - 0.2) / n
            b.box(["madera", "madera3"], (lp + 0.1, 0.11, 0.13), (cx + s*0.04, y, cz - 0.07), rot=(0, s*grados, 0), bisel=0.02)
        # tablas del hastial
        for y in (-L/2 - vuelo_y + 0.03, L/2 + vuelo_y - 0.03):
            b.box("madera", (lp + 0.12, 0.07, 0.2), (cx + s*0.05, y, cz - 0.03), rot=(0, s*grados, 0), bisel=0.02)
    b.box("madera", (0.14, L + 2*vuelo_y + 0.16, 0.16), (0, 0, zc - 0.12), bisel=0.02)
    # tejas
    tl, tw = 0.46, 0.34; paso = tl * 0.72
    nf = int(math.ceil((lp + 0.05) / paso)); nc = int(math.ceil((L + 2*vuelo_y + 0.1) / tw))
    tw = (L + 2*vuelo_y + 0.1) / nc
    for s in (-1, 1):
        for f in range(nf):
            t = lp + 0.06 - tl/2 - f * paso
            if t < tl*0.3: continue
            for c in range(nc + (f % 2)):
                y = -L/2 - vuelo_y - 0.05 + (c + 0.5 - 0.5*(f % 2)) * tw
                ancho = tw - 0.015
                if f % 2 and (c == 0 or c == nc):
                    ancho = tw/2 - 0.015; y += (tw/4 if c == 0 else -tw/4)
                if R.random() < 0.012: continue
                levanta = R.uniform(0, 0.012) + (0.03 if R.random() < 0.05 else 0)
                x = s * t * math.cos(p); z = zc - t * math.sin(p) + 0.12 + levanta
                col = "hoja3" if R.random() < 0.012 else TEJAS
                b.box(col, (tl, ancho, 0.07), (x, y, z), rot=(R.uniform(-2, 2), s*(grados - 7) + R.uniform(-2.5, 2.5), R.uniform(-3, 3)), bisel=0.02, var=0.08)
    # cumbrera
    n = int((L + 2*vuelo_y + 0.2) / 0.42)
    for i in range(n):
        y = -L/2 - vuelo_y - 0.08 + (i + 0.5) * (L + 2*vuelo_y + 0.16) / n
        b.cil(TEJAS, 0.16, 0.44, (0, y, zc + 0.14 + R.uniform(0, .015)), lados=6, rot=(90 + R.uniform(-3, 3), 0, 0), r2=0.13, var=0.08)
    return zc

def chimenea(b, alto=1.7):
    z = 0
    while z < alto:
        for i, (dx, dy) in enumerate(((-.16, -.16), (.16, -.16), (.16, .16), (-.16, .16))):
            g = (z > 0 and int(z*10) % 2)
            b.box(PIEDRAS + ["ladrillo", "ladrillo2"], (0.34, 0.34, 0.2), (dx + R.uniform(-.012, .012), dy + R.uniform(-.012, .012), z + 0.1), rot=(0, 0, 0), bisel=0.03, var=0.07, deform=0.008)
        z += 0.205
    b.box("piedra3", (0.86, 0.86, 0.12), (0, 0, alto + 0.05), bisel=0.035)
    b.box("piedra2", (0.6, 0.6, 0.1), (0, 0, alto + 0.15), bisel=0.03)
    for dx in (-0.14, 0.14): b.cil("teja3", 0.1, 0.3, (dx, 0, alto + 0.33), lados=7, r2=0.075)

def barril(b, pos):
    b.push(pos=pos)
    for i, (r1, r2) in enumerate(((0.26, 0.31), (0.31, 0.31), (0.31, 0.26))): b.cil("madera", r1, 0.24, (0, 0, 0.12 + i*0.24), lados=10, r2=r2)
    for z in (0.14, 0.58): b.cil("hierro", 0.315, 0.05, (0, 0, z), lados=10)
    b.cil("madera2", 0.24, 0.02, (0, 0, 0.725), lados=10)
    b.pop()

def caja_madera(b, pos, t=0.55, rz=0):
    b.push(pos=pos, rz=rz)
    b.box("madera3", (t, t, t), (0, 0, t/2), bisel=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1): b.box("madera2", (0.07, 0.07, t + 0.04), (sx*t/2, sy*t/2, t/2), bisel=0.012)
    for z in (0.04, t - 0.04):
        for rzz in (0, 90):
            for sg in (-1, 1): b.box("madera", ((t, 0.05, 0.07) if rzz == 0 else (0.05, t, 0.07)), ((0, sg*t/2, z) if rzz == 0 else (sg*t/2, 0, z)), bisel=0.01)
    b.pop()

def arbusto(b, pos, r=0.45, flores=False):
    for i in range(5):
        a = i * 2.4; d = r * 0.55 * (i > 0)
        b.bola(["hoja", "hoja2", "hoja3"], r * R.uniform(0.6, 0.85), (pos[0] + math.cos(a)*d, pos[1] + math.sin(a)*d, pos[2] + r*0.45 + R.uniform(0, r*0.3)), esc=(1, 1, 0.8))
    if flores:
        for i in range(7): b.bola(["flor", "flor2", "blanco"], 0.05, (pos[0] + R.uniform(-r, r)*0.8, pos[1] + R.uniform(-r, r)*0.8, pos[2] + r*R.uniform(0.7, 1.0)), ruido=0.1)

def mata(b, pos):
    for i in range(5):
        a = R.uniform(0, 360)
        b.box(["hierba2", "hoja2", "hoja"], (0.05, 0.02, R.uniform(0.18, 0.34)), (pos[0] + R.uniform(-.08, .08), pos[1] + R.uniform(-.08, .08), 0.1), rot=(R.uniform(-22, 22), R.uniform(-22, 22), a), bisel=0)

def porche(b, w=2.6, fondo=1.6, alto=1.95, caida=8):
    """Cobertizo con toldo a rayas; origen en el centro de la pared, mirando a -Y."""
    yc = -(0.4 + fondo + 0.25) / 2; largo = fondo + 0.25 - 0.4 + 0.1
    zt = alto + 0.2
    for sx in (-1, 1):
        b.box("madera", (0.14, 0.14, alto), (sx * (w/2 - 0.1), -fondo + 0.1, alto/2), bisel=0.025)
        b.box("piedra", (0.3, 0.3, 0.18), (sx * (w/2 - 0.1), -fondo + 0.1, 0.09), bisel=0.04)
        b.box("madera2", (0.5, 0.09, 0.09), (sx * (w/2 - 0.32), -fondo + 0.1, alto - 0.22), rot=(0, sx*45, 0), bisel=0.015)
        b.box("madera", (0.1, fondo + 0.1, 0.1), (sx * (w/2 - 0.1), -fondo/2 - 0.05, zt - 0.03), rot=(caida, 0, 0), bisel=0.02)
    b.box("madera", (w + 0.3, 0.14, 0.14), (0, -fondo + 0.1, alto), bisel=0.025)
    n = 8; tw = (w + 0.3) / n
    for i in range(n):
        x = -w/2 - 0.15 + (i + 0.5) * tw
        b.box("azul" if i % 2 else "blanco", (tw - 0.004, largo, 0.035), (x, yc, zt + 0.06 + R.uniform(0, .006)), rot=(caida, 0, 0), bisel=0.008, var=0.03)
        b.box("azul" if i % 2 else "blanco", (tw - 0.004, 0.03, 0.22), (x, yc - largo/2, zt - 0.14), bisel=0.008, var=0.03)

def casa(b):
    # cuerpo principal: 5 x 4.4, dos plantas, hastial al frente
    W, D, H = 4.8, 4.4, 4.5
    b.push(pos=(1.2, 0.2, 0))
    muro(b, W, D, H)
    b.box("madera", (W + 0.1, D + 0.1, 0.14), (0, 0, 2.5), bisel=0.02)   # imposta de madera entre plantas
    for x in (-W/2 + 0.5, -0.9, 0.9, W/2 - 0.5):
        b.box("madera2", (0.16, 0.3, 0.16), (x, -D/2 - 0.08, 2.42), bisel=0.02)
    zc = tejado(b, W, D, H)
    # fachada
    b.push(pos=(0.55, -D/2, 0)); puerta(b); b.pop()
    b.push(pos=(-1.35, -D/2, 1.5)); ventana(b, jardinera=True, luz=True); b.pop()
    for x in (-1.1, 1.1):
        b.push(pos=(x, -D/2, 3.45)); ventana(b, alto=0.95, jardinera=(x > 0)); b.pop()
    # óculo en el hastial
    b.push(pos=(0, -D/2, H + 0.75))
    b.cil("vidrio", 0.2, 0.08, (0, 0, 0), lados=10, rot=(90, 0, 0))
    for i in range(8):
        a = i * math.pi / 4 + 0.39
        b.box(PIEDRAS, (0.2, 0.14, 0.15), (math.cos(a)*0.28, -0.03, math.sin(a)*0.28), rot=(0, -math.degrees(a) + 90, 0), bisel=0.025, var=0.07)
    b.pop()
    b.push(pos=(1.95, -D/2, 3.1)); estandarte(b); b.pop()
    # farol junto a la puerta
    b.push(pos=(-0.35, -D/2, 2.0))
    b.box("hierro", (0.04, 0.3, 0.04), (0, -0.15, 0.12), bisel=0); b.box("hierro", (0.16, 0.16, 0.03), (0, -0.3, 0.08), bisel=0)
    b.box("luz", (0.11, 0.11, 0.16), (0, -0.3, -0.02), bisel=0.01, var=0); b.box("hierro", (0.15, 0.15, 0.03), (0, -0.3, -0.11), bisel=0)
    b.pop()
    # laterales y trasera
    for rz, off, L in ((90, W/2, D), (180, D/2, W)):
        b.push(rz=rz)
        for x in (-L/4, L/4):
            b.push(pos=(x, -off, 1.5)); ventana(b); b.pop()
            b.push(pos=(x, -off, 3.45)); ventana(b, alto=0.95); b.pop()
        b.pop()
    b.push(pos=(1.1, 0.9, 0)); 
    p = math.radians(38); zch = zc - 1.1 * math.tan(p) - 0.5
    b.push(pos=(0, 0, zch)); chimenea(b, 1.9); b.pop(); b.pop()
    b.pop()

    # ala baja a la izquierda, cumbrera a lo largo de X
    w2, d2, h2 = 3.4, 3.6, 2.5
    b.push(pos=(-2.9, 0.6, 0))
    muro(b, w2, d2, h2)
    b.push(rz=90); tejado(b, d2, w2 + 0.0, h2, grados=36, vuelo=0.45, vuelo_y=0.0 + 0.3); b.pop()
    b.push(pos=(-0.2, -d2/2, 0)); 
    b.push(pos=(0, 0, 1.4)); ventana(b, ancho=1.1, alto=0.9, contras=False, luz=True); b.pop()
    porche(b)
    # mostrador
    b.box("madera3", (1.7, 0.5, 0.08), (0, -0.75, 0.85), bisel=0.02)
    for sx in (-1, 1): b.box("madera2", (0.1, 0.44, 0.82), (sx*0.72, -0.75, 0.41), bisel=0.02)
    for i, col in enumerate(("flor2", "flor", "hoja2", "teja4")): b.bola(col, 0.09, (-0.5 + i*0.2, -0.75, 0.97), ruido=0.12)
    b.cil("teja", 0.12, 0.22, (0.55, -0.72, 1.0), lados=8, r2=0.09)
    b.pop()
    b.push(rz=270); b.push(pos=(0.4, -w2/2, 1.4)); ventana(b); b.pop(); b.pop()
    b.pop()

    # atrezo
    barril(b, (3.95, -1.75, 0)); barril(b, (4.25, -1.2, 0)); caja_madera(b, (-5.0, -1.6, 0), rz=14); caja_madera(b, (-5.05, -1.65, 0.55), t=0.42, rz=-20)
    arbusto(b, (3.5, -2.6, 0), 0.5, True); arbusto(b, (-1.0, -2.5, 0), 0.42); arbusto(b, (-5.3, 0.6, 0), 0.6, True); arbusto(b, (4.3, 1.2, 0), 0.55)
    for _ in range(40):
        a = R.uniform(0, 6.28); d = R.uniform(3.2, 6.8)
        x, y = math.cos(a)*d*1.15, math.sin(a)*d*0.8
        if abs(x - 1.75) < 1.2 and y < -2: continue
        mata(b, (x, y - 0.3, 0))
    for _ in range(9):
        a = R.uniform(0, 6.28); d = R.uniform(4, 6.5)
        b.bola(PIEDRAS, R.uniform(0.12, 0.3), (math.cos(a)*d*1.1, math.sin(a)*d*0.8 - 0.4, 0.05), esc=(1, 1, 0.6), ruido=0.2)

def suelo(b):
    b.cil("hierba", 7.8, 0.3, (-0.3, -0.3, -0.15), lados=28, var=0.0)
    for _ in range(26):
        a = R.uniform(0, 6.28); d = R.uniform(1, 6.6)
        b.cil(["hierba2", "hierba"], R.uniform(0.5, 1.2), 0.02, (math.cos(a)*d - 0.3, math.sin(a)*d - 0.3, 0.004 + _*0.0006), lados=7, var=0.04)
    for i in range(9):
        y = -2.6 - i*0.55
        b.cil(["tierra", "tierra2"], R.uniform(0.6, 0.85), 0.03, (1.75 + math.sin(i*0.8)*0.35, y, 0.03 + i*0.002), lados=8, var=0.04)
    for i in range(12):
        b.box(PIEDRAS, (R.uniform(0.3, 0.5), R.uniform(0.3, 0.45), 0.08), (1.75 + R.uniform(-.5, .5), -2.9 - i*0.38, 0.05 + i*0.001), rot=(0, 0, R.uniform(0, 90)), bisel=0.02, deform=0.01)

def malla(nombre, fn):
    b = B(); fn(b)
    bmesh.ops.recalc_face_normals(b.bm, faces=b.bm.faces[:])
    bmesh.ops.triangulate(b.bm, faces=b.bm.faces[:])
    m = bpy.data.meshes.new(nombre); b.bm.to_mesh(m); n = len(b.bm.faces); b.bm.free()
    o = bpy.data.objects.new(nombre, m); bpy.context.scene.collection.objects.link(o)
    return o, n

if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    out = args[0]; muestras = int(args[1]) if len(args) > 1 else 48
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("V"); mat.use_nodes = True; nt = mat.node_tree
    bs = nt.nodes["Principled BSDF"]; at = nt.nodes.new("ShaderNodeVertexColor"); at.layer_name = "Col"
    nt.links.new(at.outputs[0], bs.inputs["Base Color"]); bs.inputs["Roughness"].default_value = 0.9
    o, n = malla("casa", casa); o.data.materials.append(mat); print("TRIS casa", n)
    o2, n2 = malla("suelo", suelo); o2.data.materials.append(mat)
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, -0.3))
    pl = bpy.context.object; mp = bpy.data.materials.new("F"); mp.use_nodes = True
    mp.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.045, 0.045, 0.06, 1); pl.data.materials.append(mp)
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); bpy.context.scene.collection.objects.link(cam)
    cam.location = (-13.5, -21, 13.5); d = Vector((-0.3, 0, 2.3)) - cam.location
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); cam.data.lens = 62
    sun = bpy.data.objects.new("s", bpy.data.lights.new("s", 'SUN')); sun.data.energy = 4.2; sun.data.color = (1.0, 0.9, 0.76); sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(52), 0, math.radians(-62)); bpy.context.scene.collection.objects.link(sun)
    w = bpy.data.worlds.new("w"); bpy.context.scene.world = w; w.use_nodes = True
    bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.42, 0.5, 0.85, 1); bg.inputs[1].default_value = 0.75
    s = bpy.context.scene; s.camera = cam; s.render.engine = 'CYCLES'; s.cycles.samples = muestras; s.cycles.device = 'CPU'
    try: s.cycles.use_denoising = True
    except Exception: pass
    s.view_settings.view_transform = 'Standard'; s.view_settings.look = 'None'
    s.render.resolution_x = 1600; s.render.resolution_y = 1000; s.render.filepath = out
    bpy.ops.render.render(write_still=True)
