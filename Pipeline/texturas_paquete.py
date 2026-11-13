"""YA NO SE USA (Toni, 2026-11-13): las texturas del paquete van tal cual.

Texturas de "Lowpoly Textures Pack" pasadas a los colores del juego.

Las texturas del paquete son pintadas a mano y muy saturadas (agua cian, tierra naranja). Aquí se
quedan con su dibujo (las líneas del agua, las grietas de la tierra, los adoquines) pero se
pintan con unos pocos colores de la paleta, en escalones, para que peguen con el resto del valle.

Salida en Assets/Nostos/Arte/Texturas:
- agua_rio.png      (de Water_A)            el río y la cascada
- camino_tierra.png (de Summer_ForestRoad)  la tierra pisada bajo las losas de los caminos
- plaza_adoquin.png (de Summer_Rocks)       el empedrado de la plaza de la hoguera

Uso: python3 Pipeline/texturas_paquete.py
"""
import os

from PIL import Image, ImageFilter

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ORIGEN = os.path.join(RAIZ, "Assets", "LowpolyTexturesPack", "Textures")
DESTINO = os.path.join(RAIZ, "Assets", "Nostos", "Arte", "Texturas")

# (archivo del paquete, salida, escalones: (hasta qué parte de los píxeles, de oscuro a claro, color))
TEXTURAS = [
    ("Water_A.png", "agua_rio.png", [(0.80, (74, 134, 194)), (0.88, (88, 146, 202)), (1.01, (140, 188, 224))]),
    ("Summer_ForestRoad.png", "camino_tierra.png",
     [(0.14, (114, 90, 68)), (0.6, (132, 104, 78)), (1.01, (146, 116, 86))]),
    ("Summer_Rocks.png", "plaza_adoquin.png",
     [(0.18, (120, 98, 76)), (0.36, (140, 132, 132)), (0.62, (150, 144, 142)), (1.01, (166, 158, 150))]),
]


def recolorear(origen, escalones, lado=1024):
    im = Image.open(origen).convert("RGB").resize((lado, lado), Image.LANCZOS)
    # Un poco de suavizado para que los escalones no salgan con ruido de píxel.
    gris = im.convert("L").filter(ImageFilter.MedianFilter(5))
    # Los escalones van por reparto de píxeles y no por luminancia: así cada textura usa todos
    # sus colores aunque sea muy clara o muy oscura.
    histograma = gris.histogram()
    total = float(sum(histograma))
    acumulado, hasta_valor = 0, []
    for valor in range(256):
        acumulado += histograma[valor]
        hasta_valor.append(acumulado / total)
    tabla_r, tabla_g, tabla_b = [], [], []
    for valor in range(256):
        l = hasta_valor[valor] - histograma[valor] / total / 2
        color = next(c for hasta, c in escalones if l < hasta)
        tabla_r.append(color[0])
        tabla_g.append(color[1])
        tabla_b.append(color[2])
    return Image.merge("RGB", (gris.point(tabla_r), gris.point(tabla_g), gris.point(tabla_b)))


def main():
    os.makedirs(DESTINO, exist_ok=True)
    for archivo, salida, escalones in TEXTURAS:
        ruta = os.path.join(ORIGEN, archivo)
        if not os.path.exists(ruta):
            print(f"  falta {ruta}")
            continue
        recolorear(ruta, escalones).save(os.path.join(DESTINO, salida))
        print(f"  Texturas/{salida}")


if __name__ == "__main__":
    main()
