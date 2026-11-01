"""Erzeugt den Grafikentwurf für Messingstadt prozedural mit Pillow (zweite Fassung).

Alles hier ist eigenständig entworfen: eigene Palette, Gebäude, Maschinen und Figuren.
Es werden keine Dateien, Werte oder Grafiken aus anderen Spielen verwendet.

Fassaden entstehen als flache Bilder und werden danach in die Isometrie gekippt.

Ausgabe in assets/concept/: figuren_v3_x4.png, szene_v3.png, szene_v3_x2.png

Aufruf (aus dem Projektordner):
    python tools/art/generate_concept.py
"""
import json
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[2] / "assets" / "concept"
RNG = random.Random(1923)
WEAR = random.Random(1931)  # eigener Zufall für die Abnutzung, damit RNG die Fenster nicht verschiebt
AMBIENT = {"smoke": [], "steam": [], "fires": [], "puddles": [], "lights": [], "fireLights": []}  # Bildpositionen für die Animationen im Spiel
HW, HH = 32, 16  # halbe Tile-Breite und -Höhe

# Palette: Abenddämmerung statt Nacht, deutlich heller als die erste Fassung
C = {
    "sky": (52, 56, 78), "black": (20, 20, 28), "soot": (48, 48, 60), "iron": (76, 80, 94), "steel": (112, 118, 130),
    "ash": (150, 152, 156), "paper": (226, 216, 190),
    "brick_d": (104, 54, 48), "brick": (146, 74, 58), "brick_l": (178, 102, 76), "mortar": (116, 92, 84),
    "stone_d": (112, 106, 104), "stone": (146, 138, 128), "stone_l": (178, 168, 150),
    "green_d": (54, 84, 78), "green": (76, 112, 100), "green_l": (106, 142, 124),
    "cobble_d": (72, 70, 80), "cobble": (98, 96, 104), "cobble_l": (122, 120, 124),
    "tar": (70, 68, 78), "tar_l": (88, 86, 94), "gravel": (104, 100, 104),
    "brass_d": (132, 90, 36), "brass": (200, 150, 62), "brass_l": (244, 208, 118),
    "gas": (255, 232, 160), "gas_d": (226, 170, 90), "glass": (120, 160, 170), "glass_d": (66, 92, 106),
    "wood_d": (72, 50, 40), "wood": (112, 78, 54), "awn_r": (160, 56, 50), "awn_w": (214, 202, 172),
    "skin": (178, 146, 122), "skin_d": (122, 94, 80), "hair": (84, 64, 52),
    "navy_d": (40, 50, 80), "navy": (58, 72, 112), "navy_l": (84, 102, 146),
    "olive_d": (66, 72, 44), "olive": (94, 102, 62), "olive_l": (124, 132, 84),
    "wine_d": (92, 32, 46), "wine": (136, 50, 62), "wine_l": (176, 78, 84),
    "grey_d": (64, 68, 80), "grey": (92, 98, 112), "grey_l": (124, 130, 142),
    "cream": (222, 208, 176),
    # Der braune Mantel der Agenten: gedeckt, aber warm genug, um nachts von den grauen Wachen abzustechen.
    "coat_d": (82, 58, 40), "coat": (126, 90, 60), "coat_l": (158, 120, 84), "felt": (100, 72, 50),
    # Dienstkleidung der Wachen: schmutziges Feldgrau statt Bläulichgrau
    "uni_d": (50, 54, 56), "uni": (78, 84, 84), "uni_l": (106, 112, 108),
    # Abnutzung
    "rust_d": (74, 44, 32), "rust": (122, 68, 38), "rust_l": (156, 92, 50), "grime": (42, 38, 36),
    "dirt": (56, 48, 42), "moss": (62, 76, 46), "puddle": (34, 40, 56), "plaster": (150, 138, 120),
}


def blend(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c)


def mix(a, b, f):
    return tuple(int(x + (y - x) * f) for x, y in zip(a, b))


def hash01(ix, iy, seed=0):
    h = (ix * 374761393 + iy * 668265263 + seed * 2147483647) & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    return ((h ^ (h >> 16)) & 0xffff) / 65535


def noise(x, y, seed=0):
    """Weiches Rauschen zwischen 0 und 1, für Flecken und Verfärbungen."""
    ix, iy = int(x // 1), int(y // 1)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    top = hash01(ix, iy, seed) * (1 - fx) + hash01(ix + 1, iy, seed) * fx
    bottom = hash01(ix, iy + 1, seed) * (1 - fx) + hash01(ix + 1, iy + 1, seed) * fx
    return top * (1 - fy) + bottom * fy


class Scene:
    def __init__(self, tiles):
        self.tiles = tiles
        self.w = (tiles * 2) * HW + 200
        self.h = (tiles * 2) * HH + 320
        self.ox, self.oy = self.w // 2, 260
        self.img = Image.new("RGBA", (self.w, self.h), C["sky"] + (255,))
        self.px = self.img.load()

    def to_screen(self, mx, my, z=0.0):
        return self.ox + (mx - my) * HW, self.oy + (mx + my) * HH - z

    def to_map(self, sx, sy, z=0.0):
        a, b = (sx - self.ox) / HW, (sy - self.oy + z) / HH
        return (a + b) / 2, (b - a) / 2

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = tuple(c) + (255,)

    def add(self, x, y, r, g, b):
        if 0 <= x < self.w and 0 <= y < self.h:
            o = self.px[x, y]
            if o[3] == 0:  # beim Export einzelner Objekte: heller Dampf auf durchsichtigem Grund
                self.px[x, y] = (150, 150, 156, 150)
                return
            self.px[x, y] = (min(255, o[0] + r), min(255, o[1] + g), min(255, o[2] + b), 255)

    def shade(self, x, y, f):
        if 0 <= x < self.w and 0 <= y < self.h:
            o = self.px[x, y]
            self.px[x, y] = blend(o[:3], f) + (255,)

    def rect(self, x, y, w, h, c):
        for j in range(int(y), int(y + h)):
            for i in range(int(x), int(x + w)):
                self.put(i, j, c)


# ---------- Kleine Pixelschrift, 3 × 5 ----------
FONT = {
    "A": "010101111101101", "B": "110101110101110", "C": "011100100100011", "D": "110101101101110",
    "E": "111100110100111", "F": "111100110100100", "G": "011100101101011", "H": "101101111101101",
    "I": "111010010010111", "K": "101101110101101", "L": "100100100100111", "M": "101111111101101",
    "N": "110101101101101", "O": "010101101101010", "P": "110101110100100", "R": "110101110101101",
    "S": "011100010001110", "T": "111010010010010", "U": "101101101101111", "W": "101101111111101",
    "&": "010101010101011", " ": "000000000000000",
}


def text_size(t, s=1):
    return (len(t) * 4 - 1) * s, 5 * s


def text(d, x, y, t, color, s=1, worn=0.0):
    for ch in t:
        pat = FONT[ch]
        for i, bit in enumerate(pat):
            if bit == "1" and WEAR.random() >= worn:
                px, py = x + (i % 3) * s, y + (i // 3) * s
                d.rectangle((px, py, px + s - 1, py + s - 1), fill=color)
        x += 4 * s


# ---------- Fassaden als flache Bilder ----------
def wall(w, h, material):
    img = Image.new("RGBA", (w, h), C[material[0]] + (255,))
    d = ImageDraw.Draw(img)
    if material[0] == "brick":
        for v in range(h):
            row = v // 4
            for u in range(w):
                if v % 4 == 3 or (u + (row % 2) * 4) % 8 == 7:
                    img.putpixel((u, v), C["mortar"] + (255,))
                elif RNG.random() < 0.05:
                    img.putpixel((u, v), C[RNG.choice(["brick_d", "brick_l"])] + (255,))
    elif material[0] == "stone":
        for v in range(0, h, 10):
            d.line((0, v, w, v), fill=C["stone_d"])
            for u in range(0 if (v // 10) % 2 else 12, w, 24):
                d.line((u, v, u, v + 9), fill=C["stone_d"])
    elif material[0] == "green":
        for u in range(0, w, 6):
            d.line((u, 0, u, h), fill=C["green_d"])
            d.line((u + 2, 0, u + 2, h), fill=C["green_l"])
    return img, d


def window(d, x, y, w=11, h=17, lit=False, frame="stone_l"):
    d.rectangle((x - 1, y - 2, x + w, y + h + 1), fill=C[frame])
    d.rectangle((x, y, x + w - 1, y + h), fill=C["gas_d"] if lit else C["glass_d"])
    state = WEAR.random()
    if not lit and state < 0.16:
        # Mit Brettern vernagelt
        d.rectangle((x, y, x + w - 1, y + h), fill=C["black"])
        for v in range(y + 1, y + h, 4):
            d.rectangle((x - 2, v, x + w + 1, v + 2), fill=C[WEAR.choice(["wood", "wood_d"])])
        return
    if not lit and state < 0.34:
        # Zerbrochene Scheibe: schwarzes Loch mit gezacktem Rand
        for v in range(y, y + h // 2 + 3):
            d.line((x, v, x + WEAR.randint(1, w - 2), v), fill=C["black"])
    d.line((x + w // 2, y, x + w // 2, y + h), fill=C["wood_d"])
    d.line((x, y + h // 2, x + w - 1, y + h // 2), fill=C["wood_d"])
    if lit:
        # Vergilbte Vorhänge, nur ein Spalt Licht
        d.rectangle((x, y, x + w // 3, y + h), fill=C["wood"])
        d.rectangle((x + w // 3 + 1, y + h // 2 + 1, x + w - 1, y + h), fill=C["gas"])
    else:
        d.point((x + 1, y + 1), fill=C["glass"])
    for _ in range(3):
        d.point((x + WEAR.randint(-1, w), y + h + 1), fill=C["grime"])


def door(d, x, base, w=16, h=36, c="wood", lamp=True):
    d.rectangle((x - 2, base - h - 3, x + w + 1, base), fill=C["stone_l"])
    d.rectangle((x, base - h, x + w - 1, base), fill=C[c])
    d.line((x + w // 2, base - h, x + w // 2, base), fill=C["wood_d"])
    d.rectangle((x + 2, base - h + 3, x + w // 2 - 2, base - h + 11), fill=C["gas_d"])
    d.rectangle((x + w // 2 + 2, base - h + 3, x + w - 3, base - h + 11), fill=C["gas_d"])
    d.point((x + w // 2 - 2, base - h // 2), fill=C["brass_l"])
    d.point((x + w // 2 + 2, base - h // 2), fill=C["brass_l"])
    # Abgewetzte Farbe und eine eingeschlagene Scheibe
    for _ in range(w * h // 18):
        u, v = WEAR.randint(x, x + w - 1), WEAR.randint(base - h, base)
        d.line((u, v, u, v + WEAR.randint(0, 3)), fill=C[WEAR.choice(["wood_d", "grime", "plaster"])])
    if WEAR.random() < 0.6:
        d.rectangle((x + 2, base - h + 3, x + w // 2 - 2, base - h + 11), fill=C["black"])
    if lamp:
        lit_lamp = WEAR.random() < 0.5
        d.rectangle((x + w // 2 - 2, base - h - 9, x + w // 2 + 2, base - h - 5), fill=C["gas_d" if lit_lamp else "soot"])
        d.line((x + w // 2 - 2, base - h - 10, x + w // 2 + 2, base - h - 10), fill=C["brass_d"])


def shop(d, x, base, w, name, awn=("awn_r", "awn_w")):
    d.rectangle((x, base - 40, x + w, base), fill=C["wood_d"])
    d.rectangle((x + 3, base - 28, x + w - 3, base - 8), fill=C["gas"])
    for u in range(x + 3, x + w - 3, 10):
        d.line((u, base - 28, u, base - 8), fill=C["wood"])
    d.rectangle((x + 3, base - 7, x + w - 3, base - 1), fill=C["wood"])
    for u in range(x, x + w, 6):
        d.rectangle((u, base - 39, u + 2, base - 31), fill=C[awn[0]])
        d.rectangle((u + 3, base - 39, u + 5, base - 31), fill=C[awn[1]])
        # Zerrissene Markise: Fetzen hängen unterschiedlich tief
        d.rectangle((u, base - 39 + WEAR.randint(4, 8), u + 5, base - 31), fill=C["wood_d"])
    tw, _ = text_size(name)
    d.rectangle((x + w // 2 - tw // 2 - 2, base - 48, x + w // 2 + tw // 2 + 2, base - 41), fill=C["black"])
    text(d, x + w // 2 - tw // 2, base - 47, name, C["brass_l"], worn=0.15)


def rust_patches(d, x0, y0, x1, y1):
    """Rost in Flecken, mit Rostfahnen unter den Rohren."""
    vertical = y1 - y0 > x1 - x0
    for pos in range(y0 if vertical else x0, y1 if vertical else x1, 3):
        if WEAR.random() < 0.45:
            size = WEAR.randint(1, 6)
            c = C[WEAR.choice(["rust", "rust", "rust_d", "rust_l"])]
            if vertical:
                d.rectangle((x0, pos, x1, pos + size), fill=c)
            else:
                d.rectangle((pos, y0, pos + size, y1), fill=c)
                if WEAR.random() < 0.3:
                    d.line((pos, y1, pos, y1 + WEAR.randint(3, 14)), fill=C["rust_d"])


def poster(d, x, y):
    """Abgerissenes Plakat, Reste von alten Plakaten darunter."""
    d.rectangle((x - 2, y + 3, x + 11, y + 15), fill=C["plaster"])
    w, h = WEAR.randint(9, 13), WEAR.randint(13, 18)
    d.rectangle((x, y, x + w, y + h), fill=C["paper"])
    d.rectangle((x + 2, y + 2, x + w - 2, y + 7), fill=C[WEAR.choice(["awn_r", "soot", "wine"])])
    for v in range(y + 9, y + h - 1, 2):
        d.line((x + 2, v, x + w - WEAR.randint(2, 4), v), fill=C["ash"])
    for u in range(x, x + w + 1):
        d.line((u, y + h - WEAR.randint(0, 6), u, y + h), fill=C["plaster"])


def weather(tex):
    """Gibt eine verschmutzte Kopie einer Fassade zurück: Ruß, Feuchte, Rostfahnen, Risse, Putzschäden."""
    w, h = tex.size
    if h < 24 or w < 8:
        return tex
    out = tex.copy()
    pix = out.load()
    seed = WEAR.randrange(1 << 16)
    damp = 14 + int(noise(0.5, 0.5, seed) * 12)
    for v in range(h):
        for u in range(w):
            r, g, b, a = pix[u, v]
            if not a:
                continue
            f = 0.86
            n = noise(u / 15, v / 15, seed)
            if n > 0.55:
                f *= 1 - (n - 0.55) * 0.95
            if v < h * 0.22:
                f *= 0.7 + 0.3 * v / (h * 0.22)
            edge = h - v - noise(u / 6, 0.5, seed + 1) * 12
            if edge < damp:
                f *= 0.58 + 0.42 * max(0, edge) / damp
            if hash01(u, v, seed) > 0.93:
                f *= 0.82
            pix[u, v] = mix(blend((r, g, b), f), C["grime"], 0.12) + (a,)

    def darken(u, v, f, tint=None):
        if 0 <= u < w and 0 <= v < h and pix[u, v][3]:
            c = blend(pix[u, v][:3], f)
            pix[u, v] = (mix(c, tint, 0.3) if tint else c) + (255,)

    # Regenfahnen von Simsen und Dachkante
    for _ in range(w // 5):
        u, v = WEAR.randrange(w), WEAR.randint(0, int(h * 0.7))
        tint = C["rust"] if WEAR.random() < 0.3 else None
        for k in range(WEAR.randint(8, 46)):
            darken(u, v + k, 0.82 - 0.2 * (1 - k / 46), tint)
            if k % 9 == 8 and WEAR.random() < 0.4:
                u += WEAR.choice((-1, 1))
    # Risse
    for _ in range(max(1, w * h // 5000)):
        u, v = WEAR.randrange(w), WEAR.randrange(h)
        for _ in range(WEAR.randint(10, 34)):
            darken(u, v, 0.42)
            v += 1
            u += WEAR.choice((-1, 0, 0, 1))
    # Abgeplatzter Putz und fehlende Steine
    for _ in range(max(1, w * h // 3500)):
        u0, v0 = WEAR.randrange(w), WEAR.randrange(h)
        c = C[WEAR.choice(["plaster", "plaster", "grime", "dirt"])]
        for v in range(v0, v0 + WEAR.randint(2, 5)):
            for u in range(u0 + WEAR.randint(0, 1), u0 + WEAR.randint(3, 8)):
                if 0 <= u < w and 0 <= v < h and pix[u, v][3]:
                    pix[u, v] = mix(pix[u, v][:3], c, 0.75) + (255,)
    return out


def ledge(d, w, y, c="stone_l"):
    d.rectangle((0, y, w, y + 1), fill=C[c])
    d.line((0, y + 2, w, y + 2), fill=C["soot"])


def pipe_v(d, x, y0, y1, c="brass"):
    d.rectangle((x, y0, x + 3, y1), fill=C[c])
    d.line((x + 3, y0, x + 3, y1), fill=C["brass_d"])
    d.line((x, y0, x, y1), fill=C["brass_l"])
    for v in range(y0 + 6, y1, 18):
        d.rectangle((x - 1, v, x + 4, v + 1), fill=C["iron"])
    rust_patches(d, x, y0, x + 3, y1)


def pipe_h(d, x0, x1, y):
    d.rectangle((x0, y, x1, y + 3), fill=C["steel"])
    d.line((x0, y, x1, y), fill=C["ash"])
    d.line((x0, y + 3, x1, y + 3), fill=C["iron"])
    for u in range(x0 + 6, x1, 22):
        d.rectangle((u, y - 1, u + 1, y + 4), fill=C["iron"])
    rust_patches(d, x0, y, x1, y + 3)


def tenement(w, h, name=None, material=("brick",), door_at=None, lit_rate=0.45):
    img, d = wall(w, h, material)
    d.rectangle((0, 0, w, 3), fill=C["stone_l"])
    floors = (h - 6) // 42
    for f in range(floors):
        base = h - f * 42
        if f:
            ledge(d, w, base)
        for u in range(10, w - 16, 26):
            if f == 0:
                continue
            window(d, u, base - 32, lit=RNG.random() < lit_rate)
    if door_at is not None:
        door(d, door_at, h - 1)
    if name:
        tw, th = text_size(name, 2)
        d.rectangle((w // 2 - tw // 2 - 4, h - 44 - th, w // 2 + tw // 2 + 4, h - 42), fill=C["black"])
        text(d, w // 2 - tw // 2, h - 43 - th + 1, name, C["brass_l"], 2, worn=0.08)
    return img


# ---------- Figuren, 22 × 40 Pixel ----------
# Realistische Proportionen: Der Kopf ist rund ein Siebtel der Körperhöhe. hem = Unterkante der Jacke oder des Mantels.
SPECS = {
    "Agent": dict(coat=("coat_d", "coat", "coat_l"), legs="iron", hat="fedora", hat_c="felt", shirt="ash", hair="hair", hem=31, belt=21),
    "Wachmann": dict(coat=("uni_d", "uni", "uni_l"), legs="uni", hat="peak", hat_c="soot", shirt="uni_d", hair="hair", hem=26, belt=19),
}


def figure(s, ox, oy, spec, direction, step=0, aim=False):
    """Figur 22 × 40. ox, oy = linke obere Ecke. step: 0 Stand, 1–3 Gehphasen. aim: Schießhaltung."""
    co_d, co, co_l = spec["coat"]
    legs, hem, belt = spec["legs"], spec["hem"], spec["belt"]
    R = lambda x, y, w, h, c: s.rect(ox + x, oy + y, w, h, C[c])  # noqa: E731
    if direction in ("s", "n"):
        lift_l, lift_r = {0: (0, 0), 1: (2, 0), 2: (0, 0), 3: (0, 2)}[step]
        R(7, hem, 3, 38 - hem - lift_l, legs)
        R(12, hem, 3, 38 - hem - lift_r, legs)
        R(9, hem, 1, 38 - hem - lift_l, "black")
        R(14, hem, 1, 38 - hem - lift_r, "black")
        R(6, 38 - lift_l, 4, 2, "black")
        R(12, 38 - lift_r, 4, 2, "black")
        # Arme hängen eng am Körper, gegengleich zu den Beinen
        R(4, 12, 2, 12 - lift_r, co)
        R(4, 24 - lift_r, 2, 2, "skin_d")
        if not aim:
            R(16, 12, 2, 12 - lift_l, co_d)
            R(16, 24 - lift_l, 2, 2, "skin_d")
        # Rumpf mit hängenden Schultern, der Mantel fällt nach unten leicht weiter
        R(7, 10, 8, 1, co)
        R(6, 11, 10, hem - 11, co)
        R(5, hem - 6, 12, 6, co)
        R(6, 11, 1, hem - 11, co_l)
        R(13, 11, 3, hem - 11, co_d)
        R(15, hem - 6, 2, 6, co_d)
        R(6, belt, 10, 1, co_d if spec["hat"] == "fedora" else "black")
        R(11, 13, 1, hem - 13, co_d)
        if direction == "s":
            R(10, 10, 2, 3, spec["shirt"])
            R(9, 10, 1, 3, co_l)
            R(12, 10, 1, 3, co_d)
        R(10, 8, 2, 2, "skin_d" if direction == "s" else spec["hair"])
        if direction == "s":
            R(9, 4, 4, 5, "skin")
            R(12, 4, 1, 5, "skin_d")
            # Die Krempe wirft Schatten über die Augen
            R(9, 4, 4, 2, "skin_d")
            R(9, 5, 1, 1, "black")
            R(12, 5, 1, 1, "black")
        else:
            R(9, 4, 4, 5, spec["hair"])
            # Hochgeschlagener Kragen und helle Schulterkante: Auch von hinten bleibt es ein Mensch.
            R(8, 9, 6, 2, co_l)
            R(13, 11, 1, 8, co)
        if aim and direction == "s":
            # Der Arm zeigt auf den Betrachter: verkürzt, die Waffe vor der Brust
            R(16, 12, 2, 3, co_d)
            R(11, 13, 6, 2, co_d)
            R(9, 13, 2, 2, "skin_d")
            R(9, 12, 2, 1, "black")
            R(10, 11, 1, 1, "iron")
        elif aim:
            # Von hinten sieht man nur den angehobenen Ellbogen
            R(16, 11, 2, 3, co_d)
            R(17, 10, 2, 2, co_d)
    else:
        spread = 2 if aim else {0: 1, 1: 3, 2: 0, 3: 3}[step]
        near = 1 if step != 3 else -1
        # hinteres Bein dunkler, beide sichtbar
        R(10 - spread * near, hem, 3, 38 - hem, "black")
        R(10 - spread * near, 38, 4, 2, "black")
        R(10 + spread * near, hem, 3, 38 - hem, legs)
        R(12 + spread * near, hem, 1, 38 - hem, "black")
        R(10 + spread * near, 38, 4, 2, "black")
        R(9, 10, 5, 1, co)
        R(8, 11, 6, hem - 11, co)
        R(7, hem - 6, 8, 6, co)
        R(8, 11, 1, hem - 11, co_d)
        R(13, 11, 1, hem - 11, co_l)
        R(8, belt, 6, 1, co_d if spec["hat"] == "fedora" else "black")
        R(13, 10, 1, 3, spec["shirt"])
        swing = {0: 0, 1: 2, 2: 0, 3: -2}[step]
        if not aim:
            R(10 + swing, 12, 2, 12, co_d)
            R(10 + swing, 24, 2, 2, "skin_d")
        R(10, 8, 3, 2, "skin_d")
        R(10, 4, 4, 5, "skin")
        R(10, 4, 1, 5, spec["hair"])
        R(10, 4, 4, 2, "skin_d")
        R(13, 5, 1, 1, "black")
        R(14, 6, 1, 1, "skin_d")
        if aim:
            # Ausgestreckter Arm mit Pistole auf Schulterhöhe
            R(12, 11, 7, 2, co_d)
            R(12, 11, 7, 1, co)
            R(19, 11, 1, 2, "skin_d")
            R(20, 11, 2, 1, "black")
            R(20, 12, 1, 1, "iron")
    # Kopfbedeckung
    hat, c = spec["hat"], spec["hat_c"]
    side = direction == "e"
    if hat == "fedora":
        R(8, 0, 6, 3, c)
        R(8, 2, 6, 1, "black")
        R(6, 3, 10, 1, c)
        if side:
            R(15, 4, 1, 1, c)
    elif hat == "peak":
        R(8, 0, 6, 4, c)
        R(7, 1, 8, 1, c)
        R(10, 1, 2, 1, "ash")
        if side:
            R(14, 3, 2, 1, "black")
        elif direction == "s":
            R(8, 3, 6, 1, "black")


def figures_sheet():
    frames = [("s", 0), ("e", 0), ("n", 0), ("e", 1), ("e", 2), ("e", 3), ("s", 1), ("s", 3)]
    s = Scene(1)
    s.w, s.h = 28 * len(frames) + 8, 46 * len(SPECS) + 4
    s.img = Image.new("RGBA", (s.w, s.h), C["stone"] + (255,))
    s.px = s.img.load()
    for row, spec in enumerate(SPECS.values()):
        for col, (d, step) in enumerate(frames):
            figure(s, 4 + col * 28, 4 + row * 46, spec, d, step)
    return s.img


# ---------- Objekte in der Welt ----------
class Thing:
    def __init__(self, x0, y0, x1, y1, z0=0, z1=10, draw=None, solid=False, opaque=False):
        self.x0, self.y0, self.x1, self.y1, self.z0, self.z1, self.draw = x0, y0, x1, y1, z0, z1, draw
        self.solid, self.opaque = solid, opaque
        self.kind = ""


def behind(a, b):
    """True, wenn a vor b gezeichnet werden muss."""
    eps = 0.001
    if a.x1 <= b.x0 + eps and not b.x1 <= a.x0 + eps and not b.y1 <= a.y0 + eps:
        return True
    if a.y1 <= b.y0 + eps and not b.y1 <= a.y0 + eps and not b.x1 <= a.x0 + eps:
        return True
    overlap = a.x0 < b.x1 and b.x0 < a.x1 and a.y0 < b.y1 and b.y0 < a.y1
    return overlap and a.z1 <= b.z0 + eps


def sort_things(things):
    n = len(things)
    after = [set() for _ in range(n)]
    indeg = [0] * n
    for i in range(n):
        for j in range(n):
            if i != j and behind(things[i], things[j]):
                after[i].add(j)
                indeg[j] += 1
    left = set(range(n))
    order = []
    while left:
        ready = [i for i in left if indeg[i] == 0] or list(left)
        i = min(ready, key=lambda k: things[k].x0 + things[k].y0 + things[k].x1 + things[k].y1 + things[k].z0 * 0.001)
        order.append(things[i])
        left.remove(i)
        for j in after[i]:
            indeg[j] -= 1
    return order


def shear(s, tex, sx, sy, slope, light=1.0):
    pix = tex.load()
    for u in range(tex.width):
        off = int(u * slope) if slope > 0 else -int(u * -slope)
        for v in range(tex.height):
            c = pix[u, v]
            if c[3]:
                s.put(sx + u, sy + off + v, blend(c[:3], light))


def top_face(s, x0, y0, x1, y1, z, color_at):
    ax, _ = s.to_screen(x0, y1, z)
    bx, _ = s.to_screen(x1, y0, z)
    _, ty = s.to_screen(x0, y0, z)
    _, by = s.to_screen(x1, y1, z)
    for sy in range(int(ty), int(by) + 1):
        for sx in range(int(ax), int(bx) + 1):
            fx, fy = s.to_map(sx, sy, z)
            if x0 <= fx < x1 and y0 <= fy < y1:
                c = color_at(fx - x0, fy - y0, x1 - x0, y1 - y0)
                if c:
                    s.put(sx, sy, c)


def plain_tex(w, h, c, c2=None):
    img = Image.new("RGBA", (max(1, w), max(1, h)), C[c] + (255,))
    if c2:
        ImageDraw.Draw(img).line((0, 0, w, 0), fill=C[c2])
    return img


def box(s, x0, y0, x1, y1, z0, h, left=None, right=None, top=None, base="iron"):
    wl, wr = int((x1 - x0) * HW), int((y1 - y0) * HW)
    left = weather(left or plain_tex(wl, int(h), base, "ash"))
    right = weather(right or plain_tex(wr, int(h), base, "ash"))
    west = s.to_screen(x0, y1, z0 + h)
    south = s.to_screen(x1, y1, z0 + h)
    shear(s, left, int(west[0]), int(west[1]), 0.5, 1.0)
    shear(s, right, int(south[0]), int(south[1]), -0.5, 0.74)
    top_face(s, x0, y0, x1, y1, z0 + h, top or (lambda a, b, w, d: C["steel"]))


def roof_color(a, b, w, d):
    edge = min(a, b, w - a, d - b) < 0.12
    seed = int(w * 13 + d * 7)
    n = noise(a * 3.2, b * 3.2, seed)
    if edge:
        c = C["stone"] if (int(a * 12) + int(b * 12)) % 2 else C["stone_d"]
        return blend(c, 0.55 + 0.4 * n)
    seam = (a * 2) % 1 < 0.05
    c = C["tar_l"] if seam else (C["gravel"] if RNG.random() < 0.07 else C["tar"])
    # Geflickte Dachpappe in schiefen Rechtecken
    if hash01(int(a * 2.5), int(b * 2.5), seed) > 0.86:
        c = mix(c, C["soot"], 0.6)
    fine = noise(a * 14, b * 14, seed + 1)
    if n > 0.74 and fine > 0.3:
        return mix(c, C["puddle"], 0.75)  # stehendes Wasser
    if n < 0.27 and fine > 0.5:
        return mix(c, C["moss"], 0.45)  # nur Sprenkel, keine zusammenhängenden Flächen
    return blend(c, 0.66 + 0.34 * n)


def building(x0, y0, w, d, h, left, right, s, things, roof_items=()):
    def draw():
        box(s, x0, y0, x0 + w, y0 + d, 0, h, left, right, roof_color)
    things.append(Thing(x0, y0, x0 + w, y0 + d, 0, h, draw, solid=True, opaque=True))
    for item in roof_items:
        item(x0, y0, w, d, h, s, things)


def chimney(fx, fy, hc=26, smoke=True):
    def add(x0, y0, w, d, h, s, things):
        x, y = x0 + fx * w, y0 + fy * d
        if smoke:
            cx, cy = s.to_screen(x + 0.16, y + 0.16, h + hc)
            AMBIENT["smoke"].append([int(cx), int(cy) - 2])
        def draw():
            tl, _ = wall(10, hc, ("brick",))
            tr, _ = wall(10, hc, ("brick",))
            ImageDraw.Draw(tl).rectangle((0, 0, 10, 3), fill=C["stone_l"])
            ImageDraw.Draw(tr).rectangle((0, 0, 10, 3), fill=C["stone_l"])
            box(s, x, y, x + 0.32, y + 0.32, h, hc, tl, tr, lambda a, b, w2, d2: C["black"])
        things.append(Thing(x, y, x + 0.32, y + 0.32, h, h + hc, draw))
    return add


def skylight(fx, fy):
    def add(x0, y0, w, d, h, s, things):
        x, y = x0 + fx * w, y0 + fy * d
        def draw():
            box(s, x, y, x + 0.9, y + 0.6, h, 6, base="brass_d",
                top=lambda a, b, w2, d2: C["gas"] if (int(a * 6) + 0) % 2 and b > 0.06 else C["brass_d"])
        things.append(Thing(x, y, x + 0.9, y + 0.6, h, h + 6, draw))
    return add


def vent(fx, fy):
    def add(x0, y0, w, d, h, s, things):
        x, y = x0 + fx * w, y0 + fy * d
        def draw():
            box(s, x, y, x + 0.34, y + 0.34, h, 10, base="steel", top=lambda a, b, w2, d2: C["iron"])
        things.append(Thing(x, y, x + 0.34, y + 0.34, h, h + 10, draw))
    return add


def water_tank(fx, fy):
    def add(x0, y0, w, d, h, s, things):
        x, y = x0 + fx * w, y0 + fy * d
        def draw():
            cx, cy = s.to_screen(x + 0.4, y + 0.4, h)
            for lx in (-9, 8):
                s.rect(cx + lx, cy - 16, 2, 16, C["iron"])
            cylinder(s, cx, cy - 12, 12, 24, ("wood_d", "wood", "brass"), bands=True)
        things.append(Thing(x, y, x + 0.8, y + 0.8, h, h + 40, draw))
    return add


def roof_sign(word, fy=0.92):
    def add(x0, y0, w, d, h, s, things):
        y = y0 + fy * d
        tw, th = text_size(word, 3)
        tex = Image.new("RGBA", (tw + 8, th + 12), (0, 0, 0, 0))
        dr = ImageDraw.Draw(tex)
        for u in range(2, tw + 6, 12):
            dr.rectangle((u, th + 4, u + 1, th + 11), fill=C["iron"])
        dr.rectangle((0, th + 4, tw + 7, th + 5), fill=C["iron"])
        text(dr, 4, 1, word, C["brass"], 3, worn=0.14)
        def draw():
            west = s.to_screen(x0 + 0.3, y + 0.05, h + tex.height)
            shear(s, tex, int(west[0]), int(west[1]), 0.5, 1.0)
        things.append(Thing(x0 + 0.3, y, x0 + 0.3 + tex.width / HW, y + 0.05, h, h + tex.height, draw))
    return add


def cylinder(s, cx, cy, r, h, cols, rivets=False, bands=False):
    """Stehender Zylinder. cx, cy = Mitte der Deckfläche."""
    d, m, l = cols
    for x in range(-r, r + 1):
        rel = x / r
        depth = (1 - rel * rel) ** 0.5 * r / 2
        c = C[l] if rel < -0.45 else (C[m] if rel < 0.35 else C[d])
        for y in range(int(cy + depth), int(cy + depth + h)):
            s.put(cx + x, y, c)
        if bands:
            for by in (h // 4, 3 * h // 4):
                s.put(cx + x, int(cy + depth + by), C["iron"])
        if rivets and x % 6 == 0:
            for ry in range(4, h, 8):
                s.put(cx + x, int(cy + depth + ry), C["brass_l"])
    for y in range(-r // 2, r // 2 + 1):
        half = int(r * (1 - (2 * y / r) ** 2) ** 0.5) if r else 0
        for x in range(-half, half + 1):
            s.put(cx + x, cy + y, C[d] if abs(x) > half - 2 or abs(2 * y) > r - 2 else C[m])


def puff(s, cx, cy, r):
    """Dampf als gerastertes, halb durchsichtiges Wölkchen."""
    for y in range(int(cy - r), int(cy + r)):
        for x in range(int(cx - r), int(cx + r)):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5 / r
            if d < 1 and (x + y) % 2 == 0 and RNG.random() > d * 0.6:
                s.add(x, y, 60, 60, 62)


def boiler(x, y, s, things):
    top = s.to_screen(x + 0.9, y + 0.9, 62)
    AMBIENT["smoke"].append([int(top[0]), int(top[1]) - 4])
    AMBIENT["fires"].append([int(top[0]) + 8, int(top[1]) + 46])
    # Die Feuerluke sitzt zur Kamera hin, knapp vor dem Kern des Kessels
    AMBIENT["fireLights"].append([x + 1.5, y + 1.5, 14])

    def draw():
        cx, cy = top
        s.rect(cx - 24, cy + 44, 48, 22, C["iron"])
        cylinder(s, cx, cy, 22, 62, ("grey_d", "steel", "ash"), rivets=True, bands=True)
        # Rostfahnen laufen die Kesselwand hinab
        for _ in range(26):
            bx = WEAR.randint(-20, 20)
            depth = (1 - (bx / 22) ** 2) ** 0.5 * 11
            start = int(cy + depth) + WEAR.randint(1, 40)
            c = C[WEAR.choice(["rust", "rust_d", "grime", "rust_l"])]
            for by in range(start, min(start + WEAR.randint(4, 26), int(cy + depth + 62))):
                s.put(cx + bx, by, c)
        # Manometer
        s.rect(cx - 12, cy + 26, 9, 9, C["brass_d"])
        s.rect(cx - 11, cy + 27, 7, 7, C["paper"])
        s.put(cx - 8, cy + 30, C["wine"])
        s.put(cx - 7, cy + 29, C["wine"])
        # Feuerluke
        s.rect(cx + 2, cy + 40, 13, 13, C["black"])
        s.rect(cx + 4, cy + 42, 9, 9, C["gas_d"])
        s.rect(cx + 6, cy + 44, 5, 5, C["gas"])
    things.append(Thing(x, y, x + 1.8, y + 1.8, 0, 90, draw, solid=True, opaque=True))
    things[-1].kind = "boiler"


def overhead_pipe(x0, y0, x1, y1, z, s, things):
    def draw():
        box(s, x0, y0, x1, y1, z, 5, base="rust_d", top=lambda a, b, w, d: C["brass_d"] if noise(a * 9, b * 9, 5) > 0.62 else C["rust"])
    things.append(Thing(x0, y0, x1, y1, z, z + 5, draw))


def post(x, y, z, s, things):
    def draw():
        box(s, x, y, x + 0.12, y + 0.12, 0, z, base="iron", top=lambda a, b, w, d: C["steel"])
    things.append(Thing(x, y, x + 0.12, y + 0.12, 0, z, draw))


def tram(x, y, length, s, things):
    def draw():
        w, h = int(length * HW), 40
        tex, d = Image.new("RGBA", (w, h), C["wine"] + (255,)), None
        d = ImageDraw.Draw(tex)
        d.rectangle((0, 0, w, 3), fill=C["brass_d"])
        d.rectangle((0, 30, w, h), fill=C["soot"])
        d.rectangle((0, 28, w, 29), fill=C["rust_l"])
        for u in range(5, w - 12, 15):
            d.rectangle((u, 8, u + 10, 22), fill=C["gas_d"] if WEAR.random() < 0.45 else C["glass_d"])
            d.line((u + 5, 8, u + 5, 22), fill=C["wood_d"])
        rust_patches(d, 0, 24, w - 1, 28)
        text(d, 10, 23, "LINIE ", C["brass_l"]) if False else None
        front = Image.new("RGBA", (int(0.8 * HW), h), C["wine"] + (255,))
        fd = ImageDraw.Draw(front)
        fd.rectangle((0, 0, front.width, 3), fill=C["brass_d"])
        fd.rectangle((3, 8, front.width - 4, 20), fill=C["glass"])
        fd.rectangle((0, 30, front.width, h), fill=C["soot"])
        fd.rectangle((front.width // 2 - 3, 24, front.width // 2 + 2, 29), fill=C["gas"])
        box(s, x, y, x + 0.8, y + length, 0, h, front, tex,
            lambda a, b, w2, d2: C["steel"] if abs(a - w2 / 2) > 0.1 else C["iron"])
    things.append(Thing(x, y, x + 0.8, y + length, 0, 40, draw, solid=True, opaque=True))


def getaway_truck(x, y, s, things):
    """Der Fluchtwagen: kurzer Diesel-Lieferwagen, eigener Entwurf."""
    def draw():
        side = Image.new("RGBA", (int(1.4 * HW), 34), C["navy"] + (255,))
        d = ImageDraw.Draw(side)
        d.rectangle((0, 0, side.width, 2), fill=C["navy_l"])
        d.rectangle((0, 25, side.width, 33), fill=C["soot"])
        d.rectangle((0, 22, side.width, 23), fill=C["brass"])
        d.rectangle((4, 6, 16, 16), fill=C["glass"])
        rust_patches(d, 0, 18, side.width - 1, 21)
        for wx in (8, side.width - 12):
            d.ellipse((wx - 4, 25, wx + 4, 33), fill=C["black"])
            d.rectangle((wx - 1, 28, wx + 1, 30), fill=C["brass"])
        front = Image.new("RGBA", (int(0.8 * HW), 34), C["navy"] + (255,))
        fd = ImageDraw.Draw(front)
        fd.rectangle((0, 0, front.width, 2), fill=C["navy_l"])
        fd.rectangle((3, 6, front.width - 4, 15), fill=C["glass"])
        fd.rectangle((0, 22, front.width, 33), fill=C["soot"])
        fd.rectangle((2, 23, 5, 26), fill=C["gas"])
        fd.rectangle((front.width - 6, 23, front.width - 3, 26), fill=C["gas"])
        box(s, x, y, x + 0.8, y + 1.4, 0, 34, front, side, lambda a, b, w, dd: C["navy_l"])
    things.append(Thing(x, y, x + 0.8, y + 1.4, 0, 34, draw, solid=True, opaque=False))


def lamp(x, y, s, things, glows):
    glows.append((x, y))
    AMBIENT["lights"].append([x, y, 55])
    def draw():
        sx, sy = s.to_screen(x, y)
        sx, sy = int(sx), int(sy)
        s.rect(sx - 2, sy - 3, 5, 3, C["soot"])
        s.rect(sx - 1, sy - 50, 2, 48, C["iron"])
        s.rect(sx - 4, sy - 60, 8, 10, C["brass_d"])
        s.rect(sx - 3, sy - 59, 6, 8, C["gas"])
        s.rect(sx - 4, sy - 63, 8, 3, C["soot"])
        s.rect(sx - 7, sy - 46, 14, 1, C["iron"])
    things.append(Thing(x - 0.05, y - 0.05, x + 0.05, y + 0.05, 0, 60, draw))
    things[-1].kind = "lamp"


def person(x, y, name, direction, step, s, things):
    def draw():
        sx, sy = s.to_screen(x, y)
        sx, sy = int(sx), int(sy)
        for py in range(-3, 4):
            for px in range(-9, 10):
                if (px / 9) ** 2 + (py / 3) ** 2 < 1 and (px + py) % 2 == 0:
                    s.shade(sx + px, sy + py, 0.6)
        figure(s, sx - 11, sy - 39, SPECS[name], direction, step)
    things.append(Thing(x - 0.1, y - 0.1, x + 0.1, y + 0.1, 0, 40, draw))


def crate(x, y, s, things):
    def draw():
        tex = plain_tex(12, 12, "wood", "brass_d")
        ImageDraw.Draw(tex).rectangle((5, 0, 6, 12), fill=C["wood_d"])
        box(s, x, y, x + 0.4, y + 0.4, 0, 12, tex, tex, lambda a, b, w, d: C["wood"])
    things.append(Thing(x, y, x + 0.4, y + 0.4, 0, 12, draw, solid=True, opaque=False))


# ---------- Boden ----------
def ground_color(fx, fy, solids):
    n = 20
    if not (0 <= fx < n and 0 <= fy < n):
        return None
    tx, ty = int(fx), int(fy)
    road_y = 9 <= fy < 12
    road_x = 8 <= fx < 10
    if road_x or road_y:
        cell = (int(fx * 7), int(fy * 7))
        r = random.Random(cell[0] * 131 + cell[1] * 977)
        c = r.choice(["cobble", "cobble", "cobble_l", "cobble"])
        if (fx * 7) % 1 < 0.12 or (fy * 7) % 1 < 0.12:
            c = "cobble_d"
        if road_x and not road_y:
            for rail in (8.42, 8.58, 9.42, 9.58):
                if abs(fx - rail) < 0.018:
                    return C["rust_l"] if noise(fy * 4, rail, 9) > 0.45 else C["rust"]
                if abs(fx - rail - 0.03) < 0.012:
                    return C["black"]
        # Herausgebrochene Steine und nasser Dreck in den Rinnen
        if hash01(cell[0], cell[1], 3) < 0.06:
            return C["dirt"]
        n = noise(fx * 1.6, fy * 1.6, 11)
        gutter = road_y and not road_x and min(fy - 9, 12 - fy) < 0.3
        return blend(C[c], (0.55 if gutter else 0.68) + 0.4 * n)
    if 6 <= fx < 12 or 7 <= fy < 14:
        kerb = fx >= 7.88 and fx < 8 or 10 <= fx < 10.12 or 8.88 <= fy < 9 or 12 <= fy < 12.12
        n = noise(fx * 1.4, fy * 1.4, 21)
        if kerb:
            return blend(C["stone"], 0.55 + 0.4 * n)
        cx, cy = int(fx * 2), int(fy * 2)
        ax, ay = (fx * 2) % 1, (fy * 2) % 1
        roll = hash01(cx, cy, 7)
        if roll < 0.05 and noise(fx * 9, fy * 9, 22) > 0.35:
            return C["dirt"]  # Platte weggebrochen, mit unregelmäßigem Rand
        edge = ax < 0.05 or ay < 0.05
        tone = 0.8 + 0.2 * hash01(cx, cy, 8)
        c = C["stone_d"] if edge else blend(C["stone"], tone)
        # Gesprungene Platte: Der Sprung läuft schräg und krumm, nicht von Ecke zu Ecke.
        if roll < 0.3:
            slope = 0.35 + 1.3 * hash01(cx, cy, 9)
            line = ay - slope * ax - (hash01(cx, cy, 10) - 0.4) + (noise(fx * 16, fy * 16, 23) - 0.5) * 0.35
            if abs(line) < 0.022:
                c = C["grime"]
        return blend(c, 0.56 + 0.42 * n)
    # Hof
    cell = (int(fx * 3), int(fy * 3))
    r = random.Random(cell[0] * 31 + cell[1] * 17)
    c = C[r.choice(["tar_l", "tar", "gravel"])] if (fx * 3) % 1 > 0.05 and (fy * 3) % 1 > 0.05 else C["tar"]
    n = noise(fx * 1.8, fy * 1.8, 31)
    if n < 0.27 and noise(fx * 12, fy * 12, 32) > 0.5:
        return mix(c, C["moss"], 0.45)
    return blend(c, 0.56 + 0.45 * n)


def scatter_dirt(s, solids):
    """Pfützen, Ölflecken, Müll und Unkraut, nur auf den Boden gemalt und ohne Einfluss auf die Spiellogik."""
    def free(x, y):
        return 0 <= x < 20 and 0 <= y < 20 and not any(a <= x < c and b <= y < d for a, b, c, d in solids)

    def blot(x, y, rx, ry, color, strength):
        cx, cy = s.to_screen(x, y)
        seed = WEAR.randrange(1 << 16)
        for j in range(int(cy - ry), int(cy + ry) + 1):
            for i in range(int(cx - rx), int(cx + rx) + 1):
                d = (((i - cx) / rx) ** 2 + ((j - cy) / ry) ** 2) ** 0.5
                if d + noise(i / 5, j / 3, seed) * 0.6 < 1 and 0 <= i < s.w and 0 <= j < s.h:
                    s.px[i, j] = mix(s.px[i, j][:3], color, strength) + (255,)

    for _ in range(34):  # Pfützen
        x, y = WEAR.uniform(0, 20), WEAR.uniform(0, 20)
        if free(x, y):
            rx = WEAR.randint(10, 30)
            blot(x, y, rx, rx * 0.5, C["puddle"], 0.8)
            px, py = s.to_screen(x, y)
            AMBIENT["puddles"].append([int(px), int(py), rx])
    for _ in range(36):  # Ölflecken
        x, y = WEAR.uniform(0, 20), WEAR.uniform(0, 20)
        if free(x, y):
            rx = WEAR.randint(5, 13)
            blot(x, y, rx, rx * 0.5, C["black"], 0.5)
    for _ in range(420):  # Papier, Scherben, Dreck
        x, y = WEAR.uniform(0, 20), WEAR.uniform(0, 20)
        if free(x, y):
            sx, sy = s.to_screen(x, y)
            c = C[WEAR.choice(["ash", "plaster", "wood_d", "grime", "rust", "glass_d"])]
            s.rect(sx, sy, WEAR.randint(1, 3), WEAR.randint(1, 2), c)
    for _ in range(70):  # Unkraut in den Fugen
        x, y = WEAR.uniform(0, 20), WEAR.uniform(0, 20)
        if free(x, y):
            sx, sy = s.to_screen(x, y)
            for k in range(WEAR.randint(2, 4)):
                s.rect(sx + WEAR.randint(-2, 2), sy - WEAR.randint(0, 3), 1, WEAR.randint(1, 3), C["moss"])
    # Müllhaufen an den Hauswänden
    for a, b, c, d in solids:
        for _ in range(int((c - a) + (d - b))):
            x, y = (WEAR.uniform(a, c), d + WEAR.uniform(0.03, 0.3)) if WEAR.random() < 0.5 else (c + WEAR.uniform(0.03, 0.3), WEAR.uniform(b, d))
            if free(x, y):
                sx, sy = s.to_screen(x, y)
                for k in range(WEAR.randint(4, 14)):
                    s.rect(sx + WEAR.randint(-6, 6), sy + WEAR.randint(-3, 3), WEAR.randint(1, 3), WEAR.randint(1, 2),
                           C[WEAR.choice(["wood_d", "grime", "dirt", "plaster", "soot", "rust_d"])])


def draw_ground(s, solids, glows):
    for sy in range(s.h):
        for sx in range(s.w):
            fx, fy = s.to_map(sx + 0.5, sy + 0.5)
            c = ground_color(fx, fy, solids)
            if c:
                s.put(sx, sy, c)
    scatter_dirt(s, solids)
    # Kontaktschatten an Gebäudekanten
    for x0, y0, x1, y1 in solids:
        for sy in range(s.h):
            for sx in range(s.w):
                fx, fy = s.to_map(sx + 0.5, sy + 0.5)
                if not (0 <= fx < 20 and 0 <= fy < 20):
                    continue
                dx = max(x0 - fx, 0, fx - x1)
                dy = max(y0 - fy, 0, fy - y1)
                dist = (dx * dx + dy * dy) ** 0.5
                if 0 < dist < 0.5:
                    s.shade(sx, sy, 0.62 + 0.38 * dist / 0.5)
    # Der Lichtschein der Laternen entsteht im Spiel, hier würde er nur als Punktmuster stören.


def build_scene(with_people=True):
    s = Scene(20)
    things, glows = [], []
    for points in AMBIENT.values():
        points.clear()
    solids = [(1, 1, 6, 6), (0, 7, 6, 9), (1, 12, 6, 17), (12, 1, 18, 6), (12, 12, 17, 18)]

    # Links: Mietshaus mit Laden, Bank aus Stein, Hotel mit Dachschild
    left = tenement(160, 128, door_at=30)
    shop_d = ImageDraw.Draw(left)
    shop(shop_d, 60, 127, 76, "KOHLE")
    right = tenement(160, 128, door_at=70)
    pipe_v(ImageDraw.Draw(right), 12, 4, 127)
    poster(ImageDraw.Draw(left), 8, 96)
    poster(ImageDraw.Draw(right), 100, 96)
    text(ImageDraw.Draw(right), 110, 114, "STREIK", C["paper"], 1, worn=0.2)
    building(1, 1, 5, 5, 128, left, right, s, things,
             [chimney(0.2, 0.2), chimney(0.65, 0.3, 20, False), water_tank(0.35, 0.55), vent(0.8, 0.8), skylight(0.1, 0.72)])

    bank_l = tenement(192, 84, "BANK", ("stone",), lit_rate=0.7)
    door(ImageDraw.Draw(bank_l), 88, 83, 18, 38, "brass_d", False)
    for u in range(16, 180, 34):
        ImageDraw.Draw(bank_l).rectangle((u, 44, u + 5, 83), fill=C["stone_l"])
    bank_r = tenement(64, 84, material=("stone",), lit_rate=0.7)
    building(0, 7, 6, 2, 84, bank_l, bank_r, s, things, [vent(0.2, 0.3), vent(0.5, 0.3), skylight(0.7, 0.2)])

    hotel_l = tenement(160, 170, "HOTEL", door_at=72, lit_rate=0.6)
    hotel_r = tenement(160, 170, door_at=30, lit_rate=0.6)
    pipe_v(ImageDraw.Draw(hotel_r), 120, 6, 169)
    poster(ImageDraw.Draw(hotel_l), 20, 138)
    poster(ImageDraw.Draw(hotel_l), 110, 136)
    poster(ImageDraw.Draw(hotel_r), 62, 138)
    text(ImageDraw.Draw(hotel_r), 80, 142, "BRANDT RAUS", C["paper"], 1, worn=0.25)
    building(1, 12, 5, 5, 170, hotel_l, hotel_r, s, things,
             [roof_sign("HOTEL ADLER"), chimney(0.75, 0.3, 24, False), vent(0.2, 0.3), water_tank(0.5, 0.2)])

    # Rechts: Fabrik aus Grünblech mit Rohren, Kessel und Maschinenhalle
    fab_l, fd = wall(192, 110, ("green",))
    fd.rectangle((0, 0, 192, 3), fill=C["green_l"])
    for u in range(14, 170, 44):
        fd.rectangle((u, 16, u + 28, 44), fill=C["gas_d"])
        for k in range(u, u + 28, 7):
            fd.line((k, 16, k, 44), fill=C["iron"])
    fd.rectangle((60, 66, 112, 109), fill=C["iron"])
    for k in range(60, 112, 6):
        fd.line((k, 66, k, 109), fill=C["soot"])
    pipe_h(fd, 0, 191, 52)
    pipe_v(fd, 130, 52, 109)
    pipe_v(fd, 24, 52, 109)
    tw, th = text_size("BRANDT WERKE", 2)
    fd.rectangle((96 - tw // 2 - 4, 56, 96 + tw // 2 + 4, 58 + th), fill=C["black"])
    text(fd, 96 - tw // 2, 57, "BRANDT WERKE", C["brass_l"], 2, worn=0.08)
    fab_r, fr = wall(160, 110, ("green",))
    fr.rectangle((0, 0, 160, 3), fill=C["green_l"])
    pipe_h(fr, 0, 159, 30)
    pipe_v(fr, 40, 30, 109)
    text(fr, 62, 74, "STREIK", C["paper"], 2, worn=0.3)
    building(12, 1, 6, 5, 110, fab_l, fab_r, s, things,
             [chimney(0.15, 0.25, 54), chimney(0.4, 0.25, 54), skylight(0.1, 0.65), skylight(0.45, 0.65), skylight(0.72, 0.65), vent(0.8, 0.2)])

    # Lager unten rechts
    lag_l, ld = wall(160, 70, ("brick",))
    ld.rectangle((0, 0, 160, 3), fill=C["stone_l"])
    # Die Tür sitzt genau vor dem Durchgang in Spalte 14 der Karte.
    door(ld, 66, 69, 28, 40, "wood", True)
    text(ld, 104, 30, "LAGER", C["plaster"], 2, worn=0.2)
    poster(ld, 14, 46)
    poster(ld, 38, 48)
    lag_r, lr = wall(192, 70, ("brick",))
    lr.rectangle((0, 0, 192, 3), fill=C["stone_l"])
    pipe_h(lr, 0, 191, 20)
    for u in range(20, 170, 40):
        window(lr, u, 30, 14, 18, lit=u % 3 == 0)
    text(lr, 44, 56, "STREIK", C["paper"], 1, worn=0.2)
    building(12, 12, 5, 6, 70, lag_l, lag_r, s, things, [vent(0.3, 0.3), chimney(0.7, 0.6, 22)])

    # Maschinen im Hof vor der Fabrik
    boiler(12.3, 6.4, s, things)
    boiler(14.6, 6.4, s, things)
    overhead_pipe(14.1, 7.2, 14.6, 7.34, 50, s, things)
    overhead_pipe(6.0, 7.5, 12.3, 7.64, 62, s, things)
    getaway_truck(19.0, 9.3, s, things)
    for px in (7.0, 8.6, 10.4, 11.8):
        post(px, 7.51, 62, s, things)
    for cx, cy in ((12.2, 8.5), (12.7, 8.4), (12.3, 8.0), (16.2, 8.2), (16.7, 8.5)):
        crate(cx, cy, s, things)

    # Straße
    tram(8.1, 2.6, 4.2, s, things)
    for lx, ly in ((7.5, 2.0), (7.5, 5.5), (10.5, 3.5), (10.5, 6.9), (7.5, 13.5), (10.5, 13.5), (5.6, 10.5), (12.6, 10.5)):
        lamp(lx, ly, s, things, glows)

    # Figuren
    if with_people:
        person(10.5, 8.4, "Agent", "e", 0, s, things)
        person(10.2, 8.9, "Agent", "e", 1, s, things)
        person(10.9, 8.9, "Agent", "s", 0, s, things)
        person(10.6, 9.5, "Wachmann", "s", 1, s, things)
        person(7.2, 6.2, "Wachmann", "n", 0, s, things)
        person(6.7, 9.8, "Agent", "e", 3, s, things)
        person(11.2, 6.1, "Agent", "s", 3, s, things)

    draw_ground(s, solids, glows)
    # Gullydeckel, aus denen im Spiel Dampf aufsteigt
    for mx, my in ((9.0, 6.4), (4.0, 10.6), (14.6, 10.7)):
        cx, cy = s.to_screen(mx, my)
        for j in range(-5, 6):
            for i in range(-10, 11):
                d = (i / 10) ** 2 + (j / 5) ** 2
                if d < 1:
                    s.put(int(cx) + i, int(cy) + j, C["iron"] if d > 0.6 else (C["black"] if (i + j) % 3 else C["soot"]))
        AMBIENT["steam"].append([int(cx), int(cy)])
    return s, sort_things(things)


def export_game():
    """Schreibt die Grafiken für das Godot-Projekt und die Kartendaten."""
    root = Path(__file__).resolve().parents[2]
    assets = root / "game" / "assets"
    (assets / "things").mkdir(parents=True, exist_ok=True)
    s, things = build_scene(with_people=False)
    s.img.save(assets / "ground.png")
    manifest, solids = [], []
    for i, t in enumerate(things):
        s.img = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
        s.px = s.img.load()
        t.draw()
        bbox = s.img.getbbox()
        if not bbox:
            continue
        name = f"{i:03d}.png"
        s.img.crop(bbox).save(assets / "things" / name)
        manifest.append({"file": name, "px": bbox[0], "py": bbox[1], "x0": t.x0, "y0": t.y0, "x1": t.x1, "y1": t.y1, "z0": t.z0, "z1": t.z1, "kind": t.kind})
        if t.solid:
            solids.append({"x0": t.x0, "y0": t.y0, "x1": t.x1, "y1": t.y1, "opaque": t.opaque})
    (assets / "manifest.json").write_text(json.dumps({"ox": s.ox, "oy": s.oy, "things": manifest, **AMBIENT}), encoding="utf-8")
    (root / "data" / "map_01.json").write_text(json.dumps({"width": 20, "height": 20, "solids": solids}, indent=1), encoding="utf-8")

    export_figures(assets)

    # Kontobuch
    book = Image.new("RGBA", (14, 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(book)
    d.polygon([(7, 0), (13, 3), (7, 6), (1, 3)], fill=C["wine_l"])
    d.polygon([(1, 3), (7, 6), (7, 9), (1, 6)], fill=C["wine_d"])
    d.polygon([(7, 6), (13, 3), (13, 6), (7, 9)], fill=C["paper"])
    d.point((7, 3), fill=C["brass_l"])
    book.save(assets / "book.png")
    print(f"{len(manifest)} Objekte, {len(solids)} Blockaden exportiert nach {assets}")


def fallen(sheet, cx, cy, spec):
    """Liegende Figur in einer Blutlache: Kopf links, ein Arm abgespreizt, die Mütze daneben."""
    co_d, co, co_l = spec["coat"]
    d = ImageDraw.Draw(sheet.img)
    P = lambda pts: [(cx + x, cy + y) for x, y in pts]  # noqa: E731
    d.ellipse(P([(0, 31), (19, 41)]), fill=(70, 10, 10, 235))
    d.ellipse(P([(4, 34), (13, 40)]), fill=(96, 14, 12, 235))
    d.polygon(P([(16, 33), (23, 34), (23, 37), (16, 37)]), fill=C[spec["legs"]])
    d.line(P([(16, 37), (23, 37)]), fill=C["black"])
    d.rectangle(P([(22, 33), (23, 35)]), fill=C["black"])
    d.polygon(P([(10, 28), (13, 28), (13, 32), (10, 32)]), fill=C[co_d])
    d.rectangle(P([(10, 26), (12, 27)]), fill=C["skin_d"])
    d.polygon(P([(6, 31), (17, 30), (18, 37), (8, 38)]), fill=C[co])
    d.line(P([(6, 31), (17, 30)]), fill=C[co_l])
    d.line(P([(8, 38), (18, 37)]), fill=C[co_d])
    d.line(P([(12, 31), (13, 37)]), fill=C[co_d])
    d.ellipse(P([(1, 31), (6, 36)]), fill=C["skin_d"])
    d.line(P([(2, 31), (6, 31)]), fill=C[spec["hair"]])
    d.ellipse(P([(17, 27), (22, 30)]), fill=C[spec["hat_c"]])


def export_figures(assets):
    """Figurenblatt: je Figur eine Zeile, Spalten s0–s3, e0–e3, n0–n3, Schießhaltung s, e, n, zuletzt gefallen. Feld 24 × 42."""
    names = list(SPECS)
    sheet = Scene(1)
    sheet.w, sheet.h = 24 * 16, 42 * len(names)
    sheet.img = Image.new("RGBA", (sheet.w, sheet.h), (0, 0, 0, 0))
    sheet.px = sheet.img.load()
    frames = [(d_i * 4 + step, d, step, False) for d_i, d in enumerate(("s", "e", "n")) for step in range(4)]
    frames += [(12 + d_i, d, 0, True) for d_i, d in enumerate(("s", "e", "n"))]
    for row, name in enumerate(names):
        for column, d, step, aim in frames:
            cell_x, cell_y = column * 24, row * 42
            # Weicher Schatten am Boden, damit die Figur im Bild steht und nicht schwebt
            for j in range(38, 42):
                half = {38: 6, 39: 9, 40: 9, 41: 6}[j]
                for i in range(12 - half, 12 + half):
                    sheet.px[cell_x + i, cell_y + j] = (0, 0, 0, 120)
            figure(sheet, 1 + cell_x, 1 + cell_y, SPECS[name], d, step, aim)
        fallen(sheet, 15 * 24, row * 42, SPECS[name])
    sheet.img.save(assets / "figures.png")
    (assets / "figures.json").write_text(json.dumps({"names": names, "w": 24, "h": 42}), encoding="utf-8")


def main():
    if "--figures" in sys.argv:
        export_figures(Path(__file__).resolve().parents[2] / "game" / "assets")
        print("Nur das Figurenblatt neu erzeugt")
        return
    OUT.mkdir(parents=True, exist_ok=True)
    fig = figures_sheet()
    fig.resize((fig.width * 4, fig.height * 4), Image.NEAREST).save(OUT / "figuren_v3_x4.png")
    s, things = build_scene()
    for t in things:
        t.draw()
    img = s.img
    img.save(OUT / "szene_v3.png")
    cx = img.width // 2
    crop = img.crop((cx - 420, 330, cx + 420, 930))
    crop.resize((crop.width * 2, crop.height * 2), Image.NEAREST).save(OUT / "szene_v3_x2.png")
    print("Entwurf geschrieben nach", OUT, img.size)
    export_game()


if __name__ == "__main__":
    main()
