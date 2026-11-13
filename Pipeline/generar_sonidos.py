"""Genera los sonidos de Nostos con numpy (sin grabaciones): pasos, golpes, ambiente.

Uso:  python3 Pipeline/generar_sonidos.py
Escribe WAV mono de 16 bits en Assets/Nostos/Sonido. Los ambientes hacen bucle sin corte.
"""
import os
import wave

import numpy as np

FRECUENCIA = 22050
RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESTINO = os.path.join(RAIZ, "Assets", "Nostos", "Sonido")
azar = np.random.default_rng(7)


def tiempo(segundos):
    return np.arange(int(FRECUENCIA * segundos)) / FRECUENCIA


def ruido(n):
    return azar.uniform(-1.0, 1.0, n)


def paso_bajo(senal, corte):
    """Filtro de un polo. corte en Hz."""
    a = np.exp(-2.0 * np.pi * corte / FRECUENCIA)
    salida = np.empty_like(senal)
    y = 0.0
    for i, x in enumerate(senal):
        y = (1.0 - a) * x + a * y
        salida[i] = y
    return salida


def paso_alto(senal, corte):
    return senal - paso_bajo(senal, corte)


def bucle(senal, fundido=0.5):
    """Funde el final con el principio para que el bucle no tenga corte."""
    n = int(FRECUENCIA * fundido)
    rampa = np.linspace(0.0, 1.0, n)
    cuerpo = senal[:-n].copy()
    cuerpo[:n] = cuerpo[:n] * rampa + senal[-n:] * (1.0 - rampa)
    return cuerpo


def guardar(nombre, senal, volumen=0.8):
    pico = np.max(np.abs(senal)) or 1.0
    datos = (senal / pico * volumen * 32767).astype("<i2")
    os.makedirs(DESTINO, exist_ok=True)
    with wave.open(os.path.join(DESTINO, nombre + ".wav"), "wb") as archivo:
        archivo.setnchannels(1)
        archivo.setsampwidth(2)
        archivo.setframerate(FRECUENCIA)
        archivo.writeframes(datos.tobytes())
    print(f"  Sonido/{nombre}.wav  ({len(senal) / FRECUENCIA:.1f} s)")


def paso():
    t = tiempo(0.16)
    return paso_bajo(ruido(len(t)), 700) * np.exp(-t * 38) + 0.5 * np.sin(2 * np.pi * 70 * t) * np.exp(-t * 45)


def golpe():
    t = tiempo(0.28)
    seco = paso_bajo(ruido(len(t)), 2500) * np.exp(-t * 55)
    cuerpo = np.sin(2 * np.pi * (180 - 200 * t) * t) * np.exp(-t * 22)
    return seco + 0.8 * cuerpo


def entrega():
    t = tiempo(0.4)
    a = np.sin(2 * np.pi * 110 * t) * np.exp(-t * 16)
    b = paso_bajo(ruido(len(t)), 1200) * np.exp(-t * 30)
    eco = np.concatenate([np.zeros(int(0.09 * FRECUENCIA)), (a + b)[: len(t) - int(0.09 * FRECUENCIA)]])
    return a + b + 0.4 * eco


def campana():
    t = tiempo(2.2)
    senal = np.zeros(len(t))
    for parcial, peso, caida in ((1.0, 1.0, 1.6), (2.0, 0.5, 2.4), (2.76, 0.35, 3.0), (5.4, 0.18, 4.5)):
        senal += peso * np.sin(2 * np.pi * 392 * parcial * t) * np.exp(-t * caida)
    return senal * np.minimum(1.0, t * 200)


def pagina():
    t = tiempo(0.09)
    return paso_alto(paso_bajo(ruido(len(t)), 3500), 500) * np.exp(-t * 60)


def viento():
    t = tiempo(9.0)
    base = paso_bajo(ruido(len(t)), 420)
    rafaga = 0.55 + 0.45 * np.sin(2 * np.pi * t / 4.5) * np.sin(2 * np.pi * t / 9.0 + 1.0)
    silbido = paso_alto(paso_bajo(ruido(len(t)), 1400), 700) * 0.25 * (0.5 + 0.5 * np.sin(2 * np.pi * t / 3.0))
    return bucle(base * rafaga + silbido)


def noche():
    t = tiempo(6.5)
    grillos = np.zeros(len(t))
    for tono, ritmo, fase in ((4200, 2.0, 0.0), (4650, 1.5, 0.4), (3900, 2.5, 0.7)):
        canto = (np.sin(2 * np.pi * ritmo * t + fase * 6.28) > 0.55) * (0.5 + 0.5 * np.sin(2 * np.pi * 38 * t))
        grillos += np.sin(2 * np.pi * tono * t) * canto
    fondo = paso_bajo(ruido(len(t)), 250) * 0.5
    return bucle(0.35 * grillos + fondo)


def fuego():
    t = tiempo(5.5)
    rumor = paso_bajo(ruido(len(t)), 350) * 0.6
    chasquidos = np.zeros(len(t))
    for _ in range(60):
        inicio = azar.integers(0, len(t) - 400)
        largo = azar.integers(60, 380)
        chasquidos[inicio : inicio + largo] += ruido(largo) * np.exp(-np.arange(largo) / (largo / 5.0)) * azar.uniform(0.3, 1.0)
    return bucle(rumor + paso_alto(chasquidos, 900) * 0.9)


def despegue():
    t = tiempo(7.0)
    sube = t / t[-1]
    grave = np.sin(2 * np.pi * np.cumsum(38 + 60 * sube ** 2) / FRECUENCIA)
    rugido = paso_bajo(ruido(len(t)), 600) * (0.3 + sube)
    return (grave * 0.7 + rugido) * np.minimum(1.0, t / 1.5) * np.minimum(1.0, (t[-1] - t) / 1.2)


def musica():
    """Un colchón lento en re menor para el título y el final. Hace bucle."""
    t = tiempo(16.0)
    senal = np.zeros(len(t))
    acordes = ((146.83, 174.61, 220.0), (130.81, 164.81, 196.0), (116.54, 146.83, 174.61), (110.0, 138.59, 164.81))
    tramo = len(t) // len(acordes)
    for i, acorde in enumerate(acordes):
        envolvente = np.zeros(len(t))
        inicio = i * tramo
        local = np.arange(tramo) / tramo
        envolvente[inicio : inicio + tramo] = np.sin(np.pi * local) ** 0.7
        for nota in acorde:
            for desafinado in (0.997, 1.0, 1.003):
                senal += envolvente * np.sin(2 * np.pi * nota * desafinado * t)
            senal += 0.3 * envolvente * np.sin(2 * np.pi * nota * 2 * t)
    return senal


def main():
    print("Sonidos de Nostos:")
    guardar("paso", paso(), 0.5)
    guardar("golpe", golpe(), 0.8)
    guardar("entrega", entrega(), 0.8)
    guardar("campana", campana(), 0.7)
    guardar("pagina", pagina(), 0.4)
    guardar("viento", viento(), 0.5)
    guardar("noche", noche(), 0.45)
    guardar("fuego", fuego(), 0.6)
    guardar("despegue", despegue(), 0.9)
    guardar("musica", musica(), 0.5)


if __name__ == "__main__":
    main()
