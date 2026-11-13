"""Busca caras coplanarias que se solapan (parpadeo en Unity) entre piezas distintas de un modelo."""
import sys, math, collections
sys.path.insert(0, "/mnt/project-files/nostos-f0/Pipeline")
import bpy, bmesh
import generar_kit as g
from mathutils import Vector

def base(n):
    t = Vector((1, 0, 0)) if abs(n.x) < 0.9 else Vector((0, 1, 0))
    u = n.cross(t).normalized(); return u, n.cross(u)

def solape(P, Q):
    """Penetración mínima entre dos polígonos convexos 2D (<=0 si no se solapan)."""
    m = 1e9
    for A in (P, Q):
        for i in range(len(A)):
            e = A[(i + 1) % len(A)] - A[i]
            if e.length < 1e-7: continue
            ax = Vector((-e.y, e.x)).normalized()
            p = [v.dot(ax) for v in P]; q = [v.dot(ax) for v in Q]
            m = min(m, min(max(p), max(q)) - max(min(p), min(q)))
            if m <= 0: return m
    return m

def comprobar(nombre):
    todos = g.modelos()
    objetos = next(grp[nombre] for grp in todos.values() if nombre in grp)
    caras = []
    for obj, cont in objetos.items():
        formas = cont["formas"] if isinstance(cont, dict) else cont
        org = Vector(cont["origen"]) if isinstance(cont, dict) else Vector((0, 0, 0))
        bm = bmesh.new(); uv = bm.loops.layers.uv.new("UVMap")
        for i, f in enumerate(formas):
            antes = set(bm.faces)
            g._anadir_forma(bm, uv, f)
            nuevas = [c for c in bm.faces if c not in antes]
            for c in nuevas:
                c.normal_update()
            bmesh.ops.recalc_face_normals(bm, faces=nuevas)
            for c in nuevas:
                c.normal_update()
                n = c.normal.copy()
                if n.length < 0.5: continue
                pts = [v.co + org for v in c.verts]
                caras.append(((obj, i, f["tipo"], f["color"]), n, n.dot(pts[0]), pts, tuple(round(x, 4) for x in c.loops[0][uv].uv)))
        bm.free()
    grupos = collections.defaultdict(list)
    for c in caras:
        _, n, dd, _, _ = c
        grupos[(round(n.x, 2), round(n.y, 2), round(n.z, 2), round(dd * 500))].append(c)
    malos = collections.Counter(); ejemplos = {}
    claves = list(grupos)
    for k in claves:
        lista = grupos[k]
        # también el cubo vecino en distancia
        vec = lista + grupos.get((k[0], k[1], k[2], k[3] + 1), [])
        for i in range(len(lista)):
            a = lista[i]
            for j in range(i + 1, len(vec)):
                bb = vec[j]
                if a[0][:2] == bb[0][:2]: continue
                if abs(a[2] - bb[2]) > 0.0015 or a[1].dot(bb[1]) < 0.999: continue
                u, v = base(a[1])
                P = [Vector((p.dot(u), p.dot(v))) for p in a[3]]; Q = [Vector((p.dot(u), p.dot(v))) for p in bb[3]]
                s = solape(P, Q)
                if s > 0.004:
                    mismo = a[4] == bb[4]
                    clave = (a[0][0], a[0][3], bb[0][0], bb[0][3], "igual" if mismo else "DISTINTO")
                    malos[clave] += 1
                    c0 = sum(a[3], Vector()) / len(a[3]); c1 = sum(bb[3], Vector()) / len(bb[3])
                    ejemplos.setdefault(clave, (tuple(round(x, 3) for x in c0), tuple(round(x, 3) for x in c1), tuple(round(x, 2) for x in a[1]), round(s, 3), a[0][1], bb[0][1], a[0][2], bb[0][2]))
    print(f"== {nombre}: {len(caras)} caras, {sum(malos.values())} solapes coplanarios")
    for k, n in sorted(malos.items(), key=lambda t: (t[0][4] != "DISTINTO", -t[1])):
        print("  ", n, k, "ej. centro/normal/penetración:", ejemplos[k])

for n in sys.argv[1:]:
    comprobar(n)
