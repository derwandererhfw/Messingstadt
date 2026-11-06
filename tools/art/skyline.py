"""Was über den Häusern hinter der Karte liegt: Laternen und die Hochbahn, dazu die Karte für Smog und Hochöfen.

Die Häuser selbst zeichnet export_backdrop in generate_concept.py. Laternen und Bahn stehen in deren Straßen,
beim Verschieben der Blöcke also hier nachziehen. Schreibt game/assets/skyline.png, skyline_fx.png und skyline.json.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ASSETS = Path(__file__).resolve().parents[2] / "game" / "assets"
HW, HH = 32, 16
MAP_TILES = 20
MARGIN_X, MARGIN_TOP, MARGIN_BOTTOM = 900, 500, 600
GROUND_W, GROUND_H, MAP_OX, MAP_OY = 1480, 960, 740, 260
FX_DIVISOR = 8

# Farben stehen so im fertigen Bild, der Shader rechnet die Nacht heraus.
HAZE = (25, 27, 42)
LAMP = (235, 180, 105)
POST = (14, 14, 18)
PIER = (17, 17, 22)
RAIL = (46, 44, 48)
POOL = (62, 42, 21)
POOL_RADIUS = 62
LAMP_HEIGHT = 46
LAMP_FADE_TILES = 30.0

# Die Bahn fährt auf der Allee vor der Karte und verschwindet an beiden Enden in einem Haus.
VIADUCT_X = 22.0
VIADUCT_Z = 30
TRAIN_RANGE = (-1.2, 23.8)
PIER_TILES = 2.5
OPEN_STREET_Y = (6.0, 14.5)
ROOF_LINE_Z = 22

# Hochöfen weit hinter den Häusern: Dort glimmt der Smog. Feld x, y und Größe des Scheins in Bildpixeln.
FURNACES = [(9.0, -26.0, 300), (-26.0, 10.0, 240), (40.0, -12.0, 260)]

# In den Straßen zwischen den Blöcken, so gesetzt, dass kein Haus davor steht.
LAMPS = [
    (2, -0.5), (8, -0.5), (14, -0.5), (18, -0.5), (-0.5, 2), (-0.5, 8), (-0.5, 14), (-0.5, 18),
    (6.5, -3), (6.5, -6), (6.5, -10), (6.5, -14), (6.5, -18), (6.5, -23),
    (-3, 8), (-6, 8), (-10, 8), (-14, 8), (-18, 8), (-23, 8),
    (21.6, 7), (21.6, 13.5), (26, 8), (29, 8), (33, 8), (37, 8),
    (7, 21.6), (11.5, 21.6), (8, 26), (8, 29), (8, 33), (8, 37),
]


def screen(ox, oy, x, y, z=0.0):
    return ox + (x - y) * HW, oy + (x + y) * HH - z


def outside_tiles(x, y):
    return max(-x, -y, x - MAP_TILES, y - MAP_TILES)


def add_pools(pixels, ox, oy):
    """Lichtinseln unter den Laternen: Sie addieren sich zum Bild darunter und decken nichts ab."""
    height, width = pixels.shape[:2]
    for x, y in LAMPS:
        px, py = screen(ox, oy, x, y)
        strength = max(0.0, 1.0 - outside_tiles(x, y) / LAMP_FADE_TILES)
        x0, x1 = max(0, int(px - POOL_RADIUS)), min(width, int(px + POOL_RADIUS))
        y0, y1 = max(0, int(py - POOL_RADIUS / 2)), min(height, int(py + POOL_RADIUS / 2))
        ys, xs = np.mgrid[y0:y1, x0:x1]
        glow = np.exp(-(((xs - px) / (POOL_RADIUS * 0.5)) ** 2 + ((ys - py) / (POOL_RADIUS * 0.25)) ** 2))
        pixels[y0:y1, x0:x1, :3] += glow[..., None] * np.array(POOL, np.float32) * strength


def draw_lamps(draw, ox, oy):
    for x, y in LAMPS:
        strength = max(0.25, 1.0 - outside_tiles(x, y) / LAMP_FADE_TILES)
        foot, head = screen(ox, oy, x, y), screen(ox, oy, x, y, LAMP_HEIGHT)
        draw.line([foot, head], fill=POST + (255,))
        color = tuple(int(c * strength) for c in LAMP) + (255,)
        draw.rectangle((head[0] - 1, head[1] - 2, head[0] + 1, head[1]), fill=color)


def draw_viaduct(draw, ox, oy):
    y = TRAIN_RANGE[0] + 0.4
    while y < TRAIN_RANGE[1]:
        # Vor den Häusern ragt nur die Oberkante der Stützen über das Dach, in der offenen Straße steht die ganze Stütze.
        low = 0 if OPEN_STREET_Y[0] < y < OPEN_STREET_Y[1] else ROOF_LINE_Z
        draw.line([screen(ox, oy, VIADUCT_X, y, low), screen(ox, oy, VIADUCT_X, y, VIADUCT_Z)], fill=PIER + (255,), width=4)
        y += PIER_TILES
    ends = (TRAIN_RANGE[0], TRAIN_RANGE[1])
    draw.line([screen(ox, oy, VIADUCT_X, e, VIADUCT_Z) for e in ends], fill=PIER + (255,), width=4)
    draw.line([screen(ox, oy, VIADUCT_X, e, VIADUCT_Z + 2) for e in ends], fill=RAIL + (255,), width=1)


def effect_map(width, height, ox, oy):
    """Grobe Karte für den Shader: rot der Schein der Hochöfen, grün die Entfernung zur Karte, blau der Takt des Scheins."""
    w, h = width // FX_DIVISOR, height // FX_DIVISOR
    ys, xs = np.mgrid[0:h, 0:w]
    px, py = xs * FX_DIVISOR, ys * FX_DIVISOR
    a, b = (px - ox) / HW, (py - oy) / HH
    away = np.maximum.reduce([-(a + b) / 2, -(b - a) / 2, (a + b) / 2 - MAP_TILES, (b - a) / 2 - MAP_TILES])
    far = np.clip((away - 6.0) / 36.0, 0, 1)
    glow = np.zeros((h, w), np.float32)
    phase = np.zeros((h, w), np.float32)
    for i, (x, y, r) in enumerate(FURNACES):
        fx, fy = screen(ox, oy, x, y)
        d = np.hypot(px - fx, (py - fy) * 1.5) / r
        one = np.exp(-d * d * 2.0)
        phase = np.where(one > glow, i / len(FURNACES), phase)
        glow = np.maximum(glow, one)
    rgb = np.dstack((glow, far * far * (3 - 2 * far), phase))
    Image.fromarray((rgb * 255).astype(np.uint8), "RGB").save(ASSETS / "skyline_fx.png", optimize=True)


def main():
    width = GROUND_W + 2 * MARGIN_X
    height = GROUND_H + MARGIN_TOP + MARGIN_BOTTOM
    ox, oy = MAP_OX + MARGIN_X, MAP_OY + MARGIN_TOP
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw_viaduct(draw, ox, oy)
    draw_lamps(draw, ox, oy)
    pixels = np.array(img, np.float32)
    add_pools(pixels, ox, oy)
    Image.fromarray(np.clip(pixels, 0, 255).astype(np.uint8), "RGBA").save(ASSETS / "skyline.png", optimize=True)
    effect_map(width, height, ox, oy)
    data = {
        "marginX": MARGIN_X, "marginTop": MARGIN_TOP, "haze": HAZE,
        "viaductX": VIADUCT_X, "viaductZ": VIADUCT_Z, "trainFrom": TRAIN_RANGE[0], "trainTo": TRAIN_RANGE[1],
    }
    (ASSETS / "skyline.json").write_text(json.dumps(data), encoding="utf-8")
    print(f"skyline.png {width}x{height}, {len(LAMPS)} Laternen")


if __name__ == "__main__":
    main()
