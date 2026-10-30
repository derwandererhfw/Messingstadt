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
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parents[2] / "assets" / "concept"
RNG = random.Random(1923)
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
    "skin": (226, 176, 138), "skin_d": (180, 126, 100), "hair": (58, 40, 34),
    "navy_d": (40, 50, 80), "navy": (58, 72, 112), "navy_l": (84, 102, 146),
    "olive_d": (66, 72, 44), "olive": (94, 102, 62), "olive_l": (124, 132, 84),
    "wine_d": (92, 32, 46), "wine": (136, 50, 62), "wine_l": (176, 78, 84),
    "grey_d": (64, 68, 80), "grey": (92, 98, 112), "grey_l": (124, 130, 142),
    "cream": (222, 208, 176),
}


def blend(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c)


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


def text(d, x, y, t, color, s=1):
    for ch in t:
        pat = FONT[ch]
        for i, bit in enumerate(pat):
            if bit == "1":
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
    d.rectangle((x, y, x + w - 1, y + h), fill=C["gas"] if lit else C["glass_d"])
    d.line((x + w // 2, y, x + w // 2, y + h), fill=C["wood_d"])
    d.line((x, y + h // 2, x + w - 1, y + h // 2), fill=C["wood_d"])
    if not lit:
        d.point((x + 1, y + 1), fill=C["glass"])
        d.point((x + 2, y + 2), fill=C["glass"])


def door(d, x, base, w=16, h=36, c="wood", lamp=True):
    d.rectangle((x - 2, base - h - 3, x + w + 1, base), fill=C["stone_l"])
    d.rectangle((x, base - h, x + w - 1, base), fill=C[c])
    d.line((x + w // 2, base - h, x + w // 2, base), fill=C["wood_d"])
    d.rectangle((x + 2, base - h + 3, x + w // 2 - 2, base - h + 11), fill=C["gas_d"])
    d.rectangle((x + w // 2 + 2, base - h + 3, x + w - 3, base - h + 11), fill=C["gas_d"])
    d.point((x + w // 2 - 2, base - h // 2), fill=C["brass_l"])
    d.point((x + w // 2 + 2, base - h // 2), fill=C["brass_l"])
    if lamp:
        d.rectangle((x + w // 2 - 2, base - h - 9, x + w // 2 + 2, base - h - 5), fill=C["gas"])
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
    tw, _ = text_size(name)
    d.rectangle((x + w // 2 - tw // 2 - 2, base - 48, x + w // 2 + tw // 2 + 2, base - 41), fill=C["black"])
    text(d, x + w // 2 - tw // 2, base - 47, name, C["brass_l"])


def ledge(d, w, y, c="stone_l"):
    d.rectangle((0, y, w, y + 1), fill=C[c])
    d.line((0, y + 2, w, y + 2), fill=C["soot"])


def pipe_v(d, x, y0, y1, c="brass"):
    d.rectangle((x, y0, x + 3, y1), fill=C[c])
    d.line((x + 3, y0, x + 3, y1), fill=C["brass_d"])
    d.line((x, y0, x, y1), fill=C["brass_l"])
    for v in range(y0 + 6, y1, 18):
        d.rectangle((x - 1, v, x + 4, v + 1), fill=C["iron"])


def pipe_h(d, x0, x1, y):
    d.rectangle((x0, y, x1, y + 3), fill=C["steel"])
    d.line((x0, y, x1, y), fill=C["ash"])
    d.line((x0, y + 3, x1, y + 3), fill=C["iron"])
    for u in range(x0 + 6, x1, 22):
        d.rectangle((u, y - 1, u + 1, y + 4), fill=C["iron"])


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
        text(d, w // 2 - tw // 2, h - 43 - th + 1, name, C["brass_l"], 2)
    return img


# ---------- Figuren, 22 × 40 Pixel ----------
SPECS = {
    "Agentin": dict(coat=("wine_d", "wine", "wine_l"), legs="soot", hat="cloche", hat_c="black", shirt="cream", hair="black"),
    "Schlosser": dict(coat=("navy_d", "navy", "navy_l"), legs="iron", hat="cap", hat_c="soot", shirt="ash", hair="hair"),
    "Buchhalter": dict(coat=("olive_d", "olive", "olive_l"), legs="soot", hat="bowler", hat_c="black", shirt="paper", hair="hair"),
    "Wachmann": dict(coat=("grey_d", "grey", "grey_l"), legs="soot", hat="peak", hat_c="brass_d", shirt="cream", hair="hair"),
}


def figure(s, ox, oy, spec, direction, step=0):
    """Figur 22 × 40. ox, oy = linke obere Ecke. step: 0 Stand, 1–3 Gehphasen."""
    co_d, co, co_l = spec["coat"]
    legs = spec["legs"]
    R = lambda x, y, w, h, c: s.rect(ox + x, oy + y, w, h, C[c])  # noqa: E731
    if direction in ("s", "n"):
        lift_l, lift_r = {0: (0, 0), 1: (2, 0), 2: (0, 0), 3: (0, 2)}[step]
        R(6, 27, 4, 11 - lift_l, legs)
        R(12, 27, 4, 11 - lift_r, legs)
        R(9, 27, 1, 11 - lift_l, "black")
        R(15, 27, 1, 11 - lift_r, "black")
        R(5, 38 - lift_l, 5, 2, "black")
        R(12, 38 - lift_r, 5, 2, "black")
        # Mantel
        R(5, 13, 12, 13, co)
        R(4, 24, 14, 4, co)
        R(5, 13, 2, 13, co_l)
        R(15, 13, 2, 13, co_d)
        R(16, 24, 2, 4, co_d)
        swing_l, swing_r = {0: (0, 0), 1: (2, 0), 2: (0, 0), 3: (0, 2)}[step]
        R(2, 14, 3, 11 - swing_r, co)
        R(2, 14, 1, 11 - swing_r, co_l)
        R(17, 14, 3, 11 - swing_l, co_d)
        R(2, 25 - swing_r, 3, 2, "skin")
        R(17, 25 - swing_l, 3, 2, "skin_d")
        if direction == "s":
            R(9, 13, 4, 5, spec["shirt"])
            R(10, 14, 2, 7, "black" if spec["shirt"] != "ash" else "soot")
            R(8, 13, 1, 7, co_l)
            R(13, 13, 1, 7, co_d)
            R(13, 21, 1, 1, "brass_l")
            R(13, 24, 1, 1, "brass_l")
        else:
            R(10, 15, 1, 12, co_d)
        R(8, 11, 6, 3, "skin_d" if direction == "s" else spec["hair"])
        if direction == "s":
            R(7, 4, 8, 8, "skin")
            R(14, 4, 1, 8, "skin_d")
            R(9, 7, 1, 2, "black")
            R(12, 7, 1, 2, "black")
            R(10, 10, 2, 1, "skin_d")
        else:
            R(7, 4, 8, 8, spec["hair"])
    else:
        spread = {0: 2, 1: 5, 2: 1, 3: 5}[step]
        near = 1 if step != 3 else -1
        # hinteres Bein dunkler, beide sichtbar
        R(10 - spread * near, 27, 4, 11, "black")
        R(9 - spread * near, 38, 6, 2, "black")
        R(10 + spread * near, 27, 4, 11, legs)
        R(13 + spread * near, 27, 1, 11, "black")
        R(10 + spread * near, 38, 6, 2, "black")
        R(7, 13, 10, 13, co)
        R(6, 24, 12, 4, co)
        R(7, 13, 2, 13, co_d)
        R(15, 13, 2, 13, co_l)
        R(15, 13, 2, 5, spec["shirt"])
        swing = {0: 0, 1: 4, 2: 0, 3: -4}[step]
        R(10 + swing, 14, 4, 11, co_l)
        R(10 + swing, 14, 1, 11, co_d)
        R(10 + swing, 25, 4, 2, "skin")
        R(10, 11, 5, 3, "skin_d")
        R(8, 4, 8, 8, "skin")
        R(8, 4, 3, 8, spec["hair"])
        R(14, 7, 1, 2, "black")
        R(16, 8, 1, 2, "skin")
        R(13, 10, 2, 1, "skin_d")
    # Kopfbedeckung
    hat, c = spec["hat"], spec["hat_c"]
    front = direction == "e"
    if hat == "cap":
        R(6, 1, 10, 4, c)
        R(5, 3, 12, 2, c)
        R(8, 1, 4, 1, "iron")
        if front:
            R(15, 4, 5, 1, "black")
        elif direction == "s":
            R(6, 5, 10, 1, "black")
    elif hat == "bowler":
        R(7, 0, 8, 4, c)
        R(5, 4, 12, 1, c)
        R(7, 3, 8, 1, "wine_d")
    elif hat == "cloche":
        R(6, 1, 10, 5, c)
        R(5, 5, 3, 4, c)
        R(14, 5, 3, 4, c)
        R(6, 5, 10, 1, "brass")
        R(14, 2, 1, 2, "brass_l")
    elif hat == "peak":
        R(6, 0, 10, 4, c)
        R(5, 1, 12, 2, c)
        R(10, 1, 2, 2, "brass_l")
        R(6, 4, 10, 1, "black")
        if front:
            R(15, 4, 4, 1, "black")


def figures_sheet():
    frames = [("s", 0), ("e", 0), ("n", 0), ("e", 1), ("e", 2), ("e", 3), ("s", 1), ("s", 3)]
    s = Scene(1)
    s.w, s.h = 28 * len(frames) + 8, 46 * 4 + 4
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
    left = left or plain_tex(wl, int(h), base, "ash")
    right = right or plain_tex(wr, int(h), base, "ash")
    west = s.to_screen(x0, y1, z0 + h)
    south = s.to_screen(x1, y1, z0 + h)
    shear(s, left, int(west[0]), int(west[1]), 0.5, 1.0)
    shear(s, right, int(south[0]), int(south[1]), -0.5, 0.74)
    top_face(s, x0, y0, x1, y1, z0 + h, top or (lambda a, b, w, d: C["steel"]))


def roof_color(a, b, w, d):
    edge = min(a, b, w - a, d - b) < 0.12
    if edge:
        return C["stone_l"] if (int(a * 12) + int(b * 12)) % 2 else C["stone"]
    seam = (a * 2) % 1 < 0.05
    return C["tar_l"] if seam else (C["gravel"] if RNG.random() < 0.07 else C["tar"])


def building(x0, y0, w, d, h, left, right, s, things, roof_items=()):
    def draw():
        box(s, x0, y0, x0 + w, y0 + d, 0, h, left, right, roof_color)
    things.append(Thing(x0, y0, x0 + w, y0 + d, 0, h, draw, solid=True, opaque=True))
    for item in roof_items:
        item(x0, y0, w, d, h, s, things)


def chimney(fx, fy, hc=26, smoke=True):
    def add(x0, y0, w, d, h, s, things):
        x, y = x0 + fx * w, y0 + fy * d
        def draw():
            tl, _ = wall(10, hc, ("brick",))
            tr, _ = wall(10, hc, ("brick",))
            ImageDraw.Draw(tl).rectangle((0, 0, 10, 3), fill=C["stone_l"])
            ImageDraw.Draw(tr).rectangle((0, 0, 10, 3), fill=C["stone_l"])
            box(s, x, y, x + 0.32, y + 0.32, h, hc, tl, tr, lambda a, b, w2, d2: C["black"])
            if smoke:
                cx, cy = s.to_screen(x + 0.16, y + 0.16, h + hc)
                puff(s, cx, cy - 8, 7)
                puff(s, cx + 3, cy - 20, 10)
                puff(s, cx + 8, cy - 36, 13)
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
        text(dr, 4, 1, word, C["brass_l"], 3)
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
    def draw():
        cx, cy = s.to_screen(x + 0.9, y + 0.9, 62)
        s.rect(cx - 24, cy + 44, 48, 22, C["iron"])
        cylinder(s, cx, cy, 22, 62, ("grey_d", "steel", "ash"), rivets=True, bands=True)
        # Manometer
        s.rect(cx - 12, cy + 26, 9, 9, C["brass_d"])
        s.rect(cx - 11, cy + 27, 7, 7, C["paper"])
        s.put(cx - 8, cy + 30, C["wine"])
        s.put(cx - 7, cy + 29, C["wine"])
        # Feuerluke
        s.rect(cx + 2, cy + 40, 13, 13, C["black"])
        s.rect(cx + 4, cy + 42, 9, 9, C["gas_d"])
        s.rect(cx + 6, cy + 44, 5, 5, C["gas"])
        puff(s, cx + 2, cy - 18, 12)
        puff(s, cx - 6, cy - 34, 16)
    things.append(Thing(x, y, x + 1.8, y + 1.8, 0, 90, draw, solid=True, opaque=True))


def overhead_pipe(x0, y0, x1, y1, z, s, things):
    def draw():
        box(s, x0, y0, x1, y1, z, 5, base="brass_d", top=lambda a, b, w, d: C["brass_l"] if b < 0.07 or a < 0.07 else C["brass"])
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
        d.rectangle((0, 28, w, 29), fill=C["brass"])
        for u in range(5, w - 12, 15):
            d.rectangle((u, 8, u + 10, 22), fill=C["gas"])
            d.line((u + 5, 8, u + 5, 22), fill=C["wood_d"])
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
                    return C["steel"]
                if abs(fx - rail - 0.03) < 0.012:
                    return C["black"]
        return C[c]
    if 6 <= fx < 12 or 7 <= fy < 14:
        kerb = fx >= 7.88 and fx < 8 or 10 <= fx < 10.12 or 8.88 <= fy < 9 or 12 <= fy < 12.12
        if kerb:
            return C["stone_l"]
        edge = (fx * 2) % 1 < 0.05 or (fy * 2) % 1 < 0.05
        return C["stone_d"] if edge else C["stone"]
    # Hof
    cell = (int(fx * 3), int(fy * 3))
    r = random.Random(cell[0] * 31 + cell[1] * 17)
    return C[r.choice(["tar_l", "tar", "gravel"])] if (fx * 3) % 1 > 0.05 and (fy * 3) % 1 > 0.05 else C["tar"]


def draw_ground(s, solids, glows):
    for sy in range(s.h):
        for sx in range(s.w):
            fx, fy = s.to_map(sx + 0.5, sy + 0.5)
            c = ground_color(fx, fy, solids)
            if c:
                s.put(sx, sy, c)
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
    # Lichtschein
    for gx, gy in glows:
        cx, cy = s.to_screen(gx, gy)
        for y in range(int(cy - 48), int(cy + 48)):
            for x in range(int(cx - 96), int(cx + 96)):
                d = (((x - cx) / 96) ** 2 + ((y - cy) / 48) ** 2) ** 0.5
                if d < 1 and (x + y) % 2 == 0 and RNG.random() > d * 0.8:
                    s.add(x, y, 44, 34, 12)


def build_scene(with_people=True):
    s = Scene(20)
    things, glows = [], []
    solids = [(1, 1, 6, 6), (0, 7, 6, 9), (1, 12, 6, 17), (12, 1, 18, 6), (12, 12, 17, 18)]

    # Links: Mietshaus mit Laden, Bank aus Stein, Hotel mit Dachschild
    left = tenement(160, 128, door_at=30)
    shop_d = ImageDraw.Draw(left)
    shop(shop_d, 60, 127, 76, "KOHLE")
    right = tenement(160, 128, door_at=70)
    pipe_v(ImageDraw.Draw(right), 12, 4, 127)
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
    text(fd, 96 - tw // 2, 57, "BRANDT WERKE", C["brass_l"], 2)
    fab_r, fr = wall(160, 110, ("green",))
    fr.rectangle((0, 0, 160, 3), fill=C["green_l"])
    pipe_h(fr, 0, 159, 30)
    pipe_v(fr, 40, 30, 109)
    building(12, 1, 6, 5, 110, fab_l, fab_r, s, things,
             [chimney(0.15, 0.25, 54), chimney(0.4, 0.25, 54), skylight(0.1, 0.65), skylight(0.45, 0.65), skylight(0.72, 0.65), vent(0.8, 0.2)])

    # Lager unten rechts
    lag_l, ld = wall(160, 70, ("brick",))
    ld.rectangle((0, 0, 160, 3), fill=C["stone_l"])
    door(ld, 40, 69, 34, 40, "wood", True)
    text(ld, 90, 30, "LAGER", C["paper"], 2)
    lag_r, lr = wall(192, 70, ("brick",))
    lr.rectangle((0, 0, 192, 3), fill=C["stone_l"])
    pipe_h(lr, 0, 191, 20)
    for u in range(20, 170, 40):
        window(lr, u, 30, 14, 18, lit=u % 3 == 0)
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
        person(10.5, 8.4, "Agentin", "e", 0, s, things)
        person(10.2, 8.9, "Schlosser", "e", 1, s, things)
        person(10.9, 8.9, "Buchhalter", "s", 0, s, things)
        person(10.6, 9.5, "Wachmann", "s", 1, s, things)
        person(7.2, 6.2, "Wachmann", "n", 0, s, things)
        person(6.7, 9.8, "Buchhalter", "e", 3, s, things)
        person(11.2, 6.1, "Schlosser", "s", 3, s, things)

    draw_ground(s, solids, glows)
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
        manifest.append({"file": name, "px": bbox[0], "py": bbox[1], "x0": t.x0, "y0": t.y0, "x1": t.x1, "y1": t.y1})
        if t.solid:
            solids.append({"x0": t.x0, "y0": t.y0, "x1": t.x1, "y1": t.y1, "opaque": t.opaque})
    (assets / "manifest.json").write_text(json.dumps({"ox": s.ox, "oy": s.oy, "things": manifest}), encoding="utf-8")
    (root / "data" / "map_01.json").write_text(json.dumps({"width": 20, "height": 20, "solids": solids}, indent=1), encoding="utf-8")

    # Figurenblatt: je Figur eine Zeile, Spalten s0–s3, e0–e3, n0–n3. Feld 24 × 42.
    names = list(SPECS)
    sheet = Scene(1)
    sheet.w, sheet.h = 24 * 12, 42 * len(names)
    sheet.img = Image.new("RGBA", (sheet.w, sheet.h), (0, 0, 0, 0))
    sheet.px = sheet.img.load()
    for row, name in enumerate(names):
        for d_i, d in enumerate(("s", "e", "n")):
            for step in range(4):
                figure(sheet, 1 + (d_i * 4 + step) * 24, 1 + row * 42, SPECS[name], d, step)
    sheet.img.save(assets / "figures.png")
    (assets / "figures.json").write_text(json.dumps({"names": names, "w": 24, "h": 42}), encoding="utf-8")

    # Kontobuch
    book = Image.new("RGBA", (14, 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(book)
    d.polygon([(7, 0), (13, 3), (7, 6), (1, 3)], fill=C["wine_l"])
    d.polygon([(1, 3), (7, 6), (7, 9), (1, 6)], fill=C["wine_d"])
    d.polygon([(7, 6), (13, 3), (13, 6), (7, 9)], fill=C["paper"])
    d.point((7, 3), fill=C["brass_l"])
    book.save(assets / "book.png")
    print(f"{len(manifest)} Objekte, {len(solids)} Blockaden exportiert nach {assets}")


def main():
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
