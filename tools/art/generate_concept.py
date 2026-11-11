"""Erzeugt den Grafikentwurf für Messingstadt prozedural mit Pillow (zweite Fassung).

Alles hier ist eigenständig entworfen: eigene Palette, Gebäude, Maschinen und Figuren.
Es werden keine Dateien, Werte oder Grafiken aus anderen Spielen verwendet.

Fassaden entstehen als flache Bilder und werden danach in die Isometrie gekippt.

Ausgabe in assets/concept/: figuren_v3_x4.png, szene_v3.png, szene_v3_x2.png

Aufruf (aus dem Projektordner):
    python tools/art/generate_concept.py
"""
import json
import math
import random
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

import roofs as R

OUT = Path(__file__).resolve().parents[2] / "assets" / "concept"
RNG = random.Random(1923)
LIT_ROOMS = random.Random(1933)  # was hinter erleuchteten Fenstern zu sehen ist
WEAR = random.Random(1931)  # eigener Zufall für die Abnutzung, damit RNG die Fenster nicht verschiebt
LITTER = random.Random(1941)
DETAIL = random.Random(1951)  # Schmuck und Schäden der Häuser, eigener Zufall: RNG entscheidet weiter, welche Fenster leuchten
AMBIENT = {"smoke": [], "steam": [], "fires": [], "puddles": [], "lights": [], "fireLights": []}  # Bildpositionen für die Animationen im Spiel
HW, HH = 32, 16  # halbe Tile-Breite und -Höhe
COBBLE_ROWS, COBBLE_STONES = 5, 3  # Pflastersteine je Feld
MANHOLES = ((9.0, 6.4), (4.0, 10.6), (14.6, 10.7))
BACKDROP_W, BACKDROP_H = 3400, 2200
HD = 2  # Anzeige in doppelter Auflösung: Stadt und Boden werden zusätzlich so fein gezeichnet, das Spiel halbiert sie
DUAL = False  # beim Export bekommt jede Fassade ihre Feinfassung, im Entwurf nicht
BACKDROP_STREETS = 9  # so viele Felder weit laufen die Straßen, bevor sie im Dunst verschwinden
BACKDROP_DARK = 0.62

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
    """Zeichenfläche der Stadt. ox, oy und alle Kartenmaße gelten in einfacher Auflösung, res vergrößert nur das Bild."""

    def __init__(self, tiles, res=1):
        self.tiles = tiles
        self.res = res
        self.w = ((tiles * 2) * HW + 200) * res
        self.h = ((tiles * 2) * HH + 320) * res
        self.ox, self.oy = ((tiles * 2) * HW + 200) // 2, 260
        self.img = Image.new("RGBA", (self.w, self.h), C["sky"] + (255,))
        self.px = self.img.load()

    def use(self, res):
        """Schaltet auf eine andere Auflösung um, für Zeichenfunktionen, die diese Szene schon kennen."""
        self.w, self.h = self.w // self.res * res, self.h // self.res * res
        self.res = res

    def to_screen(self, mx, my, z=0.0):
        return (self.ox + (mx - my) * HW) * self.res, (self.oy + (mx + my) * HH - z) * self.res

    def to_map(self, sx, sy, z=0.0):
        a, b = (sx / self.res - self.ox) / HW, (sy / self.res - self.oy + z) / HH
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
class DualDraw:
    """Zeichnet auf eine Fassade und zugleich auf ihre doppelt so große Fassung in img.info["hd"].

    Flächen werden vergrößert, Linien bleiben einen Bildpunkt dünn: Rahmen, Sprossen und Fugen werden dadurch feiner.
    Der Zufall läuft genau wie beim Zeichnen ohne Feinfassung, das einfache Bild bleibt gleich.
    """

    def __init__(self, img):
        self.img = img
        self.fine = img.info.get("hd")
        self.lo = ImageDraw.Draw(img)
        self.hi = ImageDraw.Draw(self.fine) if self.fine is not None else None

    def rectangle(self, box, fill=None):
        self.lo.rectangle(box, fill=fill)
        if not self.hi:
            return
        x0, y0, x1, y1 = box
        self.hi.rectangle((x0 * HD, y0 * HD, x1 * HD + HD - 1, y1 * HD + HD - 1), fill=fill)

    def line(self, xy, fill=None):
        self.lo.line(xy, fill=fill)
        if not self.hi:
            return
        x0, y0, x1, y1 = xy
        mid = HD // 2
        if y0 == y1:
            self.hi.line((min(x0, x1) * HD, y0 * HD + mid, max(x0, x1) * HD + HD - 1, y0 * HD + mid), fill=fill)
        elif x0 == x1:
            self.hi.line((x0 * HD + mid, min(y0, y1) * HD, x0 * HD + mid, max(y0, y1) * HD + HD - 1), fill=fill)
        else:
            self.hi.line((x0 * HD + mid, y0 * HD + mid, x1 * HD + mid, y1 * HD + mid), fill=fill)

    def point(self, xy, fill=None):
        self.lo.point(xy, fill=fill)
        if not self.hi:
            return
        x, y = xy
        self.hi.rectangle((x * HD, y * HD, x * HD + HD - 1, y * HD + HD - 1), fill=fill)

    def ellipse(self, box, fill=None):
        self.lo.ellipse(box, fill=fill)
        if not self.hi:
            return
        x0, y0, x1, y1 = box
        self.hi.ellipse((x0 * HD, y0 * HD, x1 * HD + HD - 1, y1 * HD + HD - 1), fill=fill)

    def fine_line(self, xy, fill):
        """Linie, die in der Feinfassung nur einen ihrer Bildpunkte breit ist: Sprossen, Gitterstäbe, Fugen."""
        if not self.hi:
            self.lo.line(xy, fill=fill)
            return
        self.lo.line(xy, fill=fill)
        x0, y0, x1, y1 = (v * HD + HD // 2 for v in xy)
        self.hi.line((x0, y0, x1, y1), fill=fill)

    def hd_only_line(self, xy, fill):
        """Linie nur in der Feinfassung, etwa eine zweite Sprosse, für die das einfache Bild keinen Platz hat."""
        if self.hi:
            x0, y0, x1, y1 = (v * HD + HD // 2 for v in xy)
            self.hi.line((x0, y0, x1, y1), fill=fill)

    def shade(self, box, f, tint=None, amount=0.3):
        """Dunkelt eine Fläche ab (f < 1) oder hellt sie auf, ohne die Zeichnung darunter zu verlieren."""
        x0, y0, x1, y1 = (int(v) for v in box)
        for img, k in ((self.img, 1), (self.fine, HD)):
            if img is None:
                continue
            p = img.load()
            w, h = img.size
            for y in range(max(0, y0 * k), min(h, (y1 + 1) * k)):
                for x in range(max(0, x0 * k), min(w, (x1 + 1) * k)):
                    c = p[x, y]
                    if c[3]:
                        n = blend(c[:3], f)
                        p[x, y] = (mix(n, tint, amount) if tint else n) + (c[3],)

    def streak(self, x, y0, y1, f0, f1=None, tint=None):
        """Senkrechte Fahne von y0 nach y1, die von f0 nach f1 ausläuft. Fein nur einen Bildpunkt breit."""
        f1 = f0 if f1 is None else f1
        for img, k in ((self.img, 1), (self.fine, HD)):
            if img is None:
                continue
            p = img.load()
            w, h = img.size
            u = int(x) * k + k // 2
            if not 0 <= u < w:
                continue
            span = max(1, (int(y1) - int(y0) + 1) * k)
            for j in range(span):
                y = int(y0) * k + j
                if 0 <= y < h and p[u, y][3]:
                    f = f0 + (f1 - f0) * j / span
                    n = blend(p[u, y][:3], f)
                    p[u, y] = (mix(n, tint, 0.3) if tint else n) + (p[u, y][3],)

    def erase(self, box):
        """Macht einen Bereich durchsichtig: abgeschlagene Ecken."""
        self.rectangle(box, fill=(0, 0, 0, 0))


def draw_on(img):
    return DualDraw(img)


# ---------- Schrift aus Strichen, für Schilder und Fassadenschrift ----------
# Raster 4 × 6, y nach unten. In doppelter Auflösung werden die Striche glatt gezeichnet und bleiben gut lesbar.
STROKES = {
    "A": [[(0, 6), (0, 2), (2, 0), (4, 2), (4, 6)], [(0, 3.6), (4, 3.6)]],
    "B": [[(0, 0), (0, 6), (3, 6), (4, 5), (4, 4), (3, 3), (0, 3)], [(0, 0), (3, 0), (4, 1), (4, 2), (3, 3)]],
    "C": [[(4, 1), (3, 0), (1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5)]],
    "D": [[(0, 0), (0, 6), (2.5, 6), (4, 4.5), (4, 1.5), (2.5, 0), (0, 0)]],
    "E": [[(4, 0), (0, 0), (0, 6), (4, 6)], [(0, 3), (3, 3)]],
    "F": [[(4, 0), (0, 0), (0, 6)], [(0, 3), (3, 3)]],
    "G": [[(4, 1), (3, 0), (1, 0), (0, 1), (0, 5), (1, 6), (3, 6), (4, 5), (4, 3.5), (2.4, 3.5)]],
    "H": [[(0, 0), (0, 6)], [(4, 0), (4, 6)], [(0, 3), (4, 3)]],
    "I": [[(2, 0), (2, 6)], [(1, 0), (3, 0)], [(1, 6), (3, 6)]],
    "K": [[(0, 0), (0, 6)], [(4, 0), (0, 3.6)], [(1.5, 2.4), (4, 6)]],
    "L": [[(0, 0), (0, 6), (4, 6)]],
    "M": [[(0, 6), (0, 0), (2, 3.2), (4, 0), (4, 6)]],
    "N": [[(0, 6), (0, 0), (4, 6), (4, 0)]],
    "O": [[(1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0)]],
    "P": [[(0, 6), (0, 0), (3, 0), (4, 1), (4, 2), (3, 3), (0, 3)]],
    "R": [[(0, 6), (0, 0), (3, 0), (4, 1), (4, 2), (3, 3), (0, 3)], [(2, 3), (4, 6)]],
    "S": [[(4, 1), (3, 0), (1, 0), (0, 1), (0, 2), (1, 3), (3, 3), (4, 4), (4, 5), (3, 6), (1, 6), (0, 5)]],
    "T": [[(0, 0), (4, 0)], [(2, 0), (2, 6)]],
    "U": [[(0, 0), (0, 5), (1, 6), (3, 6), (4, 5), (4, 0)]],
    "W": [[(0, 0), (1, 6), (2, 2.5), (3, 6), (4, 0)]],
    "Z": [[(0, 0), (4, 0), (0, 6), (4, 6)]],
    "7": [[(0, 0), (4, 0), (1.6, 6)]],
    "0": [[(1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (1, 6), (0, 5), (0, 1), (1, 0)]],
    "1": [[(1, 1), (2, 0), (2, 6)], [(1, 6), (3, 6)]],
    "2": [[(0, 1), (1, 0), (3, 0), (4, 1), (4, 2), (0, 6), (4, 6)]],
    "9": [[(4, 3), (1, 3), (0, 2), (0, 1), (1, 0), (3, 0), (4, 1), (4, 5), (3, 6), (1, 6)]],
    "&": [[(4, 6), (1, 2), (1, 1), (2, 0), (3, 1), (3, 2), (0, 4.5), (0, 5.2), (1, 6), (2.5, 6), (4, 4)]],
    " ": [],
}


def paint_text(img, x, y, t, height, layers, gap=None, condense=0.8, slant=0.0, jitter=0.0, seed=0):
    """Schrift aus Strichen auf eine Fassade oder ein Schild, in beiden Auflösungen.

    layers: Liste von (Farbe, Strichbreite, dx, dy, Deckkraft, Abnutzung), alles in einfachen Bildpunkten, von unten nach
    oben gezeichnet: etwa erst ein dunkler Schein, dann die Leuchtröhre. Gibt die Spannen der Buchstaben zurück.
    """
    scale = height / 6
    gap = max(1.5, scale * 1.3) if gap is None else gap
    cell = 4 * scale * condense
    spans, glyphs = [], []
    cx = x
    for i, ch in enumerate(t):
        strokes = []
        for line in STROKES[ch]:
            pts = []
            for j, (gx, gy) in enumerate(line):
                ox = (float(R.hash01(i * 31 + j, seed, 7)) - 0.5) * jitter
                oy = (float(R.hash01(i * 31 + j, seed, 9)) - 0.5) * jitter
                pts.append((cx + gx * scale * condense + (6 - gy) * scale * slant + ox, y + gy * scale + oy))
            strokes.append(pts)
        glyphs.append(strokes)
        if ch != " ":
            spans.append((int(cx), int(math.ceil(cx + cell + 6 * scale * slant))))
        cx += cell + gap
    for im, k in ((img, 1), (img.info.get("hd"), HD)):
        if im is None:
            continue
        arr = np.asarray(im).astype(np.float64)
        for color, width, dx, dy, opacity, worn in layers:
            mask = Image.new("L", im.size, 0)
            md = ImageDraw.Draw(mask)
            lw = max(1, int(round(width * k)))
            for strokes in glyphs:
                for pts in strokes:
                    q = [((px + dx) * k + (k - 1) / 2, (py + dy) * k + (k - 1) / 2) for px, py in pts]
                    md.line(q * 2 if len(q) == 1 else q, fill=255, width=lw, joint="curve")
                    if lw > 2:
                        r = lw / 2 - 0.5
                        for px, py in (q[0], q[-1]):
                            md.ellipse((px - r, py - r, px + r, py + r), fill=255)
            m = np.asarray(mask).astype(np.float64) / 255 * opacity
            if worn:
                yy, xx = np.mgrid[0:im.size[1], 0:im.size[0]] / k
                wear = R.noise(xx / 2.5, yy / 2.5, seed + 3) * 0.6 + R.noise(xx / 0.9, yy / 0.9, seed + 4) * 0.4
                m *= np.clip(1.5 - wear * 2.2 * worn - worn * 0.4, 0, 1)
            a0 = arr[..., 3:4] / 255
            mm = m[..., None]
            out_a = a0 + mm * (1 - a0)
            col = np.array(color, np.float64)
            arr[..., :3] = np.where(out_a > 0, (arr[..., :3] * a0 * (1 - mm) + col * mm) / np.maximum(out_a, 1e-6), arr[..., :3])
            arr[..., 3:4] = out_a * 255
        im.paste(Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8), "RGBA"))
    return spans


def canvas(w, h, color, hd=None):
    """Leere Fassade. Beim Export (DUAL) hängt eine doppelt so große Feinfassung daran."""
    color = tuple(color) + ((255,) if len(color) == 3 else ())
    img = Image.new("RGBA", (w, h), color)
    if DUAL if hd is None else hd:
        img.info["hd"] = Image.new("RGBA", (w * HD, h * HD), color)
    return img


def wall(w, h, material, hd=None):
    img = canvas(w, h, C[material[0]], hd)
    d = draw_on(img)
    if material[0] == "brick":
        speckles = []
        for v in range(h):
            row = v // 4
            for u in range(w):
                if v % 4 == 3 or (u + (row % 2) * 4) % 8 == 7:
                    img.putpixel((u, v), C["mortar"] + (255,))
                elif RNG.random() < 0.05:
                    img.putpixel((u, v), C[RNG.choice(["brick_d", "brick_l"])] + (255,))
                    speckles.append((u, v, img.getpixel((u, v))))
        if "hd" in img.info:
            fine_bricks(img.info["hd"], w, h, speckles)
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


def fine_bricks(hd, w, h, speckles):
    """Mauerwerk in doppelter Auflösung: dieselben Steine, aber nur einen Bildpunkt breite Fugen."""
    d = ImageDraw.Draw(hd)
    for u, v, c in speckles:
        d.rectangle((u * HD, v * HD, u * HD + HD - 1, v * HD + HD - 1), fill=c)
    for v in range(3, h, 4):
        d.line((0, v * HD + HD - 1, w * HD, v * HD + HD - 1), fill=C["mortar"])
    for v in range(h):
        row = v // 4
        for u in range(w):
            if v % 4 != 3 and (u + (row % 2) * 4) % 8 == 7:
                d.line((u * HD + HD - 1, v * HD, u * HD + HD - 1, v * HD + HD - 1), fill=C["mortar"])


def lit_room(d, x, y, w, h):
    """Blick in ein erleuchtetes Zimmer. Jedes Fenster sieht anders aus: Vorhänge, Rollo, Gardine oder offen."""
    style = LIT_ROOMS.choice(("curtain", "both", "blind", "net", "open", "plant"))
    mid_x, mid_y = x + w // 2, y + h // 2
    if style == "curtain":
        d.rectangle((x, y, x + w // 3, y + h), fill=C["wood"])
        d.rectangle((x + w // 3 + 1, mid_y + 1, x + w - 1, y + h), fill=C["gas"])
    elif style == "both":
        d.rectangle((x + 3, y, x + w - 4, y + h), fill=C["gas"])
        d.rectangle((x, y, x + 2, y + h), fill=C["wood"])
        d.rectangle((x + w - 3, y, x + w - 1, y + h), fill=C["wood"])
    elif style == "blind":
        d.rectangle((x, y, x + w - 1, y + 7), fill=C["wood"])
        for v in range(y + 1, y + 8, 2):
            d.line((x, v, x + w - 1, v), fill=C["brass_d"])
        d.rectangle((x, y + 8, x + w - 1, y + h), fill=C["gas"])
    elif style == "net":
        d.rectangle((x, y, x + w - 1, y + h), fill=C["gas"])
        for v in range(y, y + h + 1):
            for u in range(x + v % 2, x + w, 2):
                d.point((u, v), fill=C["gas_d"])
    elif style == "open":
        # Deckenlampe oben, darunter wird der Raum dunkler
        d.rectangle((x, y + h - 4, x + w - 1, y + h), fill=C["wood"])
        d.rectangle((mid_x - 2, y + 1, mid_x + 1, y + 3), fill=C["gas"])
    else:
        # Topfpflanze auf der Fensterbank
        d.rectangle((x, y, x + w - 1, y + h), fill=C["gas"])
        d.rectangle((x + 3, y + h - 3, x + 6, y + h), fill=C["wood_d"])
        # Kompakter Busch: Einzelne Blätter sähen auf diese Größe nach Insekten aus.
        d.rectangle((x + 2, y + h - 6, x + 7, y + h - 4), fill=C["wood_d"])
        d.rectangle((x + 3, y + h - 7, x + 6, y + h - 7), fill=C["wood_d"])
    sash(d, x, y, w, h)


def sash(d, x, y, w, h, c="wood_d"):
    """Kastenfenster: Mittelpfosten, Kämpfer unter dem Oberlicht, fein dazu je Flügel eine Quersprosse."""
    top = y + h // 3
    d.line((x, top, x + w - 1, top), fill=C[c])
    d.line((x + w // 2, top, x + w // 2, y + h), fill=C[c])
    d.hd_only_line((x, (top + y + h) // 2, x + w - 1, (top + y + h) // 2), fill=C[c])
    d.hd_only_line((x + w // 2, y, x + w // 2, top), fill=C[c])


def window(d, x, y, w=11, h=17, lit=False, frame="stone_l"):
    """Fenster in der Laibung: Sturz mit Schlussstein, Faschen, Fensterbank mit Schlagschatten, Kastenfenster dahinter."""
    fc = C[frame]
    d.rectangle((x - 1, y - 2, x + w, y + h + 1), fill=fc)
    d.rectangle((x - 2, y - 5, x + w + 1, y - 3), fill=fc)
    d.line((x - 2, y - 5, x + w + 1, y - 5), fill=blend(fc, 1.1))
    d.rectangle((x + w // 2 - 1, y - 6, x + w // 2 + 1, y - 2), fill=blend(fc, 1.06))
    d.rectangle((x, y, x + w - 1, y + h), fill=C["gas_d"] if lit else C["glass_d"])
    state = WEAR.random()
    if not lit and state < 0.16:
        # Mit Brettern vernagelt
        d.rectangle((x, y, x + w - 1, y + h), fill=C["black"])
        for v in range(y + 1, y + h, 4):
            d.rectangle((x - 2, v, x + w + 1, v + 2), fill=C[WEAR.choice(["wood", "wood_d"])])
            d.hd_only_line((x - 2, v + 2, x + w + 1, v + 2), fill=C["wood_d"])
        sill(d, x, y, w, h, fc)
        soot_plume(d, x, y, w, 0.7)
        return
    if not lit and state < 0.34:
        # Zerbrochene Scheibe: schwarzes Loch mit gezacktem Rand
        for v in range(y, y + h // 2 + 3):
            d.line((x, v, x + WEAR.randint(1, w - 2), v), fill=C["black"])
    if lit:
        lit_room(d, x, y, w, h)
    else:
        # Spiegelung des Himmels oben in der Scheibe
        d.line((x + 1, y + 1, x + 3, y + 1), fill=C["glass"])
        d.hd_only_line((x + 1, y + 2, x + 1, y + 4), fill=C["glass"])
        sash(d, x, y, w, h)
    # Laibung: Der Sturz wirft Schatten oben und links in die Öffnung.
    d.shade((x, y, x + w - 1, y), 0.55)
    d.shade((x, y + 1, x, y + h), 0.72)
    for _ in range(3):
        d.point((x + WEAR.randint(-1, w), y + h + 1), fill=C["grime"])
    sill(d, x, y, w, h, fc)
    soot_plume(d, x, y, w, 0.3 if lit else 0.45)


def sill(d, x, y, w, h, fc):
    """Fensterbank, ein Stück vorstehend, mit Schatten und Wasserfahnen darunter."""
    d.rectangle((x - 2, y + h + 1, x + w + 1, y + h + 2), fill=fc)
    d.line((x - 2, y + h + 1, x + w + 1, y + h + 1), fill=blend(fc, 1.12))
    d.shade((x - 1, y + h + 3, x + w + 1, y + h + 3), 0.62)
    d.shade((x, y + h + 4, x + w + 1, y + h + 4), 0.84)
    for u in (x - 1, x + w):
        if DETAIL.random() < 0.7:
            d.streak(u, y + h + 4, y + h + 4 + DETAIL.randint(6, 22), 0.74, 0.98)


def soot_plume(d, x, y, w, chance):
    """Rußfahne über dem Sturz: aus Ofenrohren und von alten Bränden, nach oben ausfransend."""
    if DETAIL.random() >= chance:
        return
    length = DETAIL.randint(8, 20)
    for j in range(length):
        half = (w + 3) / 2 * (1 - j / length * 0.6) + DETAIL.choice((-1, 0, 0, 1))
        mid = x + w / 2 + math.sin(j / 4) * 1.2
        d.shade((int(mid - half), y - 7 - j, int(mid + half), y - 7 - j), 0.6 + 0.4 * (j / length) ** 0.7, C["soot"], 0.25)


def door(d, x, base, w=16, h=36, c="wood", lamp=True):
    """Haustür mit Steingewände, Oberlicht, Stufe davor und einem Vordach aus Eisen und Glas."""
    d.rectangle((x - 2, base - h - 3, x + w + 1, base), fill=C["stone_l"])
    d.rectangle((x, base - h, x + w - 1, base), fill=C[c])
    d.line((x + w // 2, base - h, x + w // 2, base), fill=C["wood_d"])
    d.rectangle((x + 2, base - h + 3, x + w // 2 - 2, base - h + 11), fill=C["gas_d"])
    d.rectangle((x + w // 2 + 2, base - h + 3, x + w - 3, base - h + 11), fill=C["gas_d"])
    # Füllungen der Flügel
    for u0, u1 in ((x + 2, x + w // 2 - 2), (x + w // 2 + 2, x + w - 3)):
        d.fine_line((u0, base - h + 15, u1, base - h + 15), fill=C["wood_d"])
        d.fine_line((u0, base - 6, u1, base - 6), fill=C["wood_d"])
        d.hd_only_line((u0, base - h + 15, u0, base - 6), fill=C["wood_d"])
    d.point((x + w // 2 - 2, base - h // 2), fill=C["brass_l"])
    d.point((x + w // 2 + 2, base - h // 2), fill=C["brass_l"])
    # Abgewetzte Farbe und eine eingeschlagene Scheibe
    for _ in range(w * h // 18):
        u, v = WEAR.randint(x, x + w - 1), WEAR.randint(base - h, base)
        d.line((u, v, u, v + WEAR.randint(0, 3)), fill=C[WEAR.choice(["wood_d", "grime", "plaster"])])
    if WEAR.random() < 0.6:
        d.rectangle((x + 2, base - h + 3, x + w // 2 - 2, base - h + 11), fill=C["black"])
    # Die Tür liegt tiefer als die Wand: Schatten oben und links im Gewände
    d.shade((x, base - h, x + w - 1, base - h + 1), 0.55)
    d.shade((x, base - h + 2, x, base - 3), 0.7)
    # Stufe
    d.rectangle((x - 4, base - 2, x + w + 3, base), fill=C["stone"])
    d.line((x - 4, base - 2, x + w + 3, base - 2), fill=C["stone_l"])
    d.shade((x - 4, base, x + w + 3, base), 0.7)
    # Vordach mit zwei schrägen Streben
    top = base - h - 8
    d.rectangle((x - 5, top, x + w + 4, top + 1), fill=C["iron"])
    d.line((x - 5, top, x + w + 4, top), fill=C["steel"])
    d.hd_only_line((x - 4, top - 1, x + w + 3, top - 1), fill=C["glass"])
    d.line((x - 4, top + 2, x - 1, top + 5), fill=C["iron"])
    d.line((x + w + 3, top + 2, x + w, top + 5), fill=C["iron"])
    d.shade((x - 3, top + 2, x + w + 2, top + 4), 0.6)
    if lamp:
        lit_lamp = WEAR.random() < 0.5
        lx = x + w + 6
        d.line((lx - 2, base - h + 2, lx + 1, base - h + 2), fill=C["brass_d"])
        d.rectangle((lx, base - h + 3, lx + 3, base - h + 8), fill=C["gas_d" if lit_lamp else "soot"])
        d.line((lx, base - h + 3, lx + 3, base - h + 3), fill=C["brass_d"])


def shop(img, d, x, base, w, name, awn=("awn_r", "awn_w")):
    """Laden: Schaufenster mit Sprossen, zerrissene Markise, darüber das gemalte Ladenschild."""
    d.rectangle((x, base - 40, x + w, base), fill=C["wood_d"])
    d.rectangle((x + 3, base - 28, x + w - 3, base - 8), fill=C["gas"])
    for u in range(x + 3, x + w - 3, 10):
        d.line((u, base - 28, u, base - 8), fill=C["wood"])
    d.fine_line((x + 3, base - 21, x + w - 3, base - 21), fill=C["wood"])
    d.rectangle((x + 3, base - 7, x + w - 3, base - 1), fill=C["wood"])
    for u in range(x + 6, x + w - 6, 10):
        d.fine_line((u, base - 6, u + 6, base - 6), fill=C["wood_d"])
        d.fine_line((u, base - 2, u + 6, base - 2), fill=C["wood_d"])
    for u in range(x, x + w, 6):
        d.rectangle((u, base - 39, u + 2, base - 31), fill=C[awn[0]])
        d.rectangle((u + 3, base - 39, u + 5, base - 31), fill=C[awn[1]])
        # Zerrissene Markise: Fetzen hängen unterschiedlich tief
        d.rectangle((u, base - 39 + WEAR.randint(4, 8), u + 5, base - 31), fill=C["wood_d"])
    d.shade((x + 3, base - 28, x + w - 3, base - 26), 0.62)
    d.rectangle((x - 1, base - 52, x + w + 1, base - 41), fill=C["wine_d"])
    d.fine_line((x - 1, base - 52, x + w + 1, base - 52), fill=C["brass_d"])
    d.fine_line((x - 1, base - 41, x + w + 1, base - 41), fill=C["black"])
    tw = len(name) * 4 * 7 / 6 * 0.8 + (len(name) - 1) * 3
    paint_text(img, x + w / 2 - tw / 2, base - 50, name, 7, [(C["cream"], 1.3, 0, 0, 1.0, 0.3)], gap=3, seed=21)
    d.shade((x, base - 40, x + w, base - 40), 0.6)


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
    """Gibt eine verschmutzte Kopie einer Fassade zurück: Ruß, Feuchte, Rostfahnen, Risse, Putzschäden.

    Hat die Fassade eine Feinfassung, wird sie mit denselben Flecken verschmutzt, nur feiner aufgelöst.
    """
    w, h = tex.size
    if h < 24 or w < 8:
        return tex
    fine = tex.info.get("hd")
    out = tex.copy()
    pix = out.load()
    seed = WEAR.randrange(1 << 16)
    damp = 14 + int(noise(0.5, 0.5, seed) * 12)

    def grime(img, scale):
        p = img.load()
        for y in range(img.height):
            v = y / scale
            for x in range(img.width):
                r, g, b, a = p[x, y]
                if not a:
                    continue
                u = x / scale
                f = 0.86
                n = noise(u / 15, v / 15, seed)
                if n > 0.55:
                    f *= 1 - (n - 0.55) * 0.95
                if v < h * 0.22:
                    f *= 0.7 + 0.3 * v / (h * 0.22)
                edge = h - v - noise(u / 6, 0.5, seed + 1) * 12
                if edge < damp:
                    f *= 0.58 + 0.42 * max(0, edge) / damp
                if hash01(x, y, seed) > 0.93:
                    f *= 0.82
                p[x, y] = mix(blend((r, g, b), f), C["grime"], 0.12) + (a,)

    grime(out, 1)
    hd = None
    if fine:
        hd = fine.copy()
        grime(hd, HD)
        hd_pix = hd.load()
        out.info["hd"] = hd

    def darken(u, v, f, tint=None):
        if 0 <= u < w and 0 <= v < h and pix[u, v][3]:
            c = blend(pix[u, v][:3], f)
            pix[u, v] = (mix(c, tint, 0.3) if tint else c) + (255,)
            if hd:
                # Fein nur einen Bildpunkt breit: Regenfahnen und Risse werden zu Linien statt Blöcken.
                x = u * HD + HD // 2
                for y in range(v * HD, v * HD + HD):
                    c = blend(hd_pix[x, y][:3], f)
                    hd_pix[x, y] = (mix(c, tint, 0.3) if tint else c) + (255,)

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
                    if hd:
                        for y in range(v * HD, v * HD + HD):
                            for x in range(u * HD, u * HD + HD):
                                hd_pix[x, y] = mix(hd_pix[x, y][:3], c, 0.75) + (255,)
    return out


def ledge(d, w, y, c="stone_l"):
    """Gurtgesims zwischen den Geschossen, mit Lichtkante oben und Schlagschatten darunter."""
    d.rectangle((0, y, w, y + 1), fill=C[c])
    d.hd_only_line((0, y, w, y), fill=blend(C[c], 1.12))
    d.line((0, y + 2, w, y + 2), fill=C["soot"])
    d.shade((0, y + 3, w, y + 3), 0.8)


def cornice(d, w, c="stone_l", dentils=True):
    """Dachrinne und Hauptgesims am oberen Rand. Die Rinne hängt in der Mitte etwas durch."""
    d.rectangle((0, 2, w, 5), fill=C[c])
    d.line((0, 2, w, 2), fill=blend(C[c], 1.12))
    d.line((0, 6, w, 6), fill=C["soot"])
    if dentils:
        for u in range(1, w, 4):
            d.rectangle((u, 6, u + 1, 7), fill=blend(C[c], 0.9))
    d.shade((0, 8, w, 8), 0.68)
    d.shade((0, 9, w, 9), 0.86)
    for u in range(w):
        drop = 1 if 0.25 < u / w < 0.8 and math.sin(math.pi * u / w) > 0.75 else 0
        d.rectangle((u, drop, u, drop + 1), fill=C["steel"])
        d.point((u, drop + 2), fill=C["iron"])
    for u in range(5, w, 14):
        d.line((u, 0, u, 3), fill=C["iron"])


def plinth(d, w, h, height=9):
    """Sockel aus groben Quadern, unten vom Spritzwasser dunkel."""
    top = h - height
    d.rectangle((0, top, w, h), fill=C["stone_d"])
    d.line((0, top, w, top), fill=C["stone"])
    d.line((0, top - 1, w, top - 1), fill=C["stone_l"])
    for u in range(0, w, 14):
        d.fine_line((u, top + 1, u, h), fill=C["soot"])
    d.fine_line((0, top + 5, w, top + 5), fill=C["soot"])
    d.shade((0, h - 3, w, h), 0.72)


def downpipe(d, x, y0, y1):
    """Fallrohr aus Zinkblech mit Schellen, oben ein Schwanenhals von der Rinne, Rostfahnen an den Stößen."""
    d.line((x - 2, y0, x, y0 + 3), fill=C["steel"])
    d.rectangle((x, y0 + 3, x + 1, y1), fill=C["steel"])
    d.fine_line((x, y0 + 3, x, y1), fill=C["ash"])
    d.shade((x + 2, y0 + 4, x + 2, y1), 0.72)
    for v in range(y0 + 10, y1 - 4, 22):
        d.rectangle((x - 1, v, x + 2, v), fill=C["iron"])
        if DETAIL.random() < 0.5:
            d.streak(x + DETAIL.randint(0, 1), v + 1, v + DETAIL.randint(5, 16), 0.8, 1.0, C["rust"])
    if DETAIL.random() < 0.5:
        # Ein Stück fehlt, darunter läuft das Wasser seit Jahren die Wand hinab.
        v = DETAIL.randint(y0 + 30, max(y0 + 31, y1 - 40))
        gap = DETAIL.randint(5, 9)
        for u in (x, x + 1):
            d.streak(u, v, v + gap, 0.55)
        for u in range(x - 2, x + 5):
            d.streak(u, v + gap, v + gap + DETAIL.randint(14, 40), 0.7, 1.0, C["moss"])
    d.line((x, y1, x + 3, y1), fill=C["steel"])


def quoins(d, w, y0, y1, c="stone_l"):
    """Eckquader im Wechsel lang und kurz."""
    for i, v in enumerate(range(y0, y1 - 5, 7)):
        size = 6 if i % 2 else 4
        for u0 in (0, w - size):
            d.rectangle((u0, v, u0 + size - 1, v + 5), fill=blend(C[c], 0.94 + 0.1 * DETAIL.random()))
            d.fine_line((u0, v + 6, u0 + size - 1, v + 6), fill=C["soot"])


def chip_corners(d, w, h):
    """Die Hauskanten sind nicht mehr scharf: abgeschlagene Ziegel und ausgebrochener Putz."""
    for u in (0, w - 1):
        v = DETAIL.randint(12, 30)
        while v < h - 12:
            size = DETAIL.randint(1, 4)
            d.erase((u, v, u, v + size))
            d.shade((u + (1 if u == 0 else -1), v, u + (1 if u == 0 else -1), v + size), 0.7)
            v += DETAIL.randint(9, 40)


def patch(d, x, y, w, h, color):
    """Geflickte Stelle: frischer, glatter und mit hartem Rand."""
    d.rectangle((x, y, x + w, y + h), fill=color)
    d.fine_line((x, y, x + w, y), fill=blend(color, 1.12))
    d.fine_line((x, y + h, x + w, y + h), fill=blend(color, 0.7))
    d.fine_line((x + w, y, x + w, y + h), fill=blend(color, 0.8))


def balcony(d, x, y, w=11, h=17):
    """Balkon vor einem Fenster: Steinplatte auf zwei Konsolen, Eisengitter, tiefer Schatten darunter."""
    floor = y + h + 2
    d.shade((x - 5, floor + 2, x + w + 5, floor + 4), 0.52)
    d.shade((x - 4, floor + 5, x + w + 5, floor + 7), 0.78)
    d.rectangle((x - 6, floor, x + w + 5, floor + 1), fill=C["stone_l"])
    d.line((x - 6, floor + 2, x + w + 5, floor + 2), fill=C["stone_d"])
    for u in (x - 4, x + w + 2):
        d.rectangle((u, floor + 3, u + 1, floor + 5), fill=C["stone"])
    rail = floor - 9
    d.line((x - 6, rail, x + w + 5, rail), fill=C["iron"])
    d.hd_only_line((x - 6, floor - 1, x + w + 5, floor - 1), fill=C["iron"])
    for u in range(x - 6, x + w + 6, 2):
        d.fine_line((u, rail, u, floor - 1), fill=C["iron"] if u % 4 else C["black"])
    if DETAIL.random() < 0.6:
        # Wäsche über dem Geländer
        u = x + DETAIL.randint(-4, w - 2)
        d.rectangle((u, rail, u + DETAIL.randint(3, 5), rail + DETAIL.randint(4, 7)), fill=C[DETAIL.choice(["paper", "cream", "grey_l", "awn_w"])])


def bay(d, x0, x1, y0, y1, c):
    """Erker über mehrere Geschosse: heller Kasten mit Schattenseite rechts, Konsole darunter, Blechdach darüber."""
    d.shade((x1 + 1, y0 + 3, x1 + 4, y1 + 6), 0.6)
    d.rectangle((x0, y0, x1, y1), fill=c)
    d.rectangle((x1 - 2, y0, x1, y1), fill=blend(c, 0.72))
    d.fine_line((x0, y0, x0, y1), fill=blend(c, 1.12))
    # Blechdach
    d.rectangle((x0 - 1, y0 - 4, x1 + 1, y0 - 1), fill=C["steel"])
    d.line((x0 - 1, y0 - 4, x1 + 1, y0 - 4), fill=C["ash"])
    for u in range(x0 + 2, x1, 5):
        d.fine_line((u, y0 - 4, u, y0 - 1), fill=C["iron"])
    d.shade((x0, y0, x1, y0 + 1), 0.7)
    # Konsole in Stufen
    for i in range(4):
        d.rectangle((x0 + i * 2, y1 + 1 + i * 2, x1 - i * 2, y1 + 2 + i * 2), fill=blend(c, 0.86 - i * 0.07))
    d.shade((x0 + 6, y1 + 9, x1 - 6, y1 + 11), 0.6)


def plaster(img, color, seed, broken=0.15):
    """Verputzt eine Ziegelwand. Wo der Putz abgefallen ist, schauen die Ziegel durch, mit Schatten an der Bruchkante."""
    w, h = img.size
    for im, k in ((img, 1), (img.info.get("hd"), HD)):
        if im is None:
            continue
        arr = np.asarray(im).astype(np.float64)
        yy, xx = np.mgrid[0:h * k, 0:w * k] / k
        n = R.noise(xx / 17, yy / 17, seed) * 0.7 + R.noise(xx / 5, yy / 5, seed + 1) * 0.22 + R.noise(xx / 1.3, yy / 1.3, seed + 2) * 0.08
        # Unten am Sockel und unter der Traufe hält der Putz am schlechtesten.
        n += 0.12 * np.clip((yy - (h - 30)) / 30, 0, 1) + 0.08 * np.clip((14 - yy) / 14, 0, 1)
        gone = n > np.quantile(n, 1 - broken)
        tone = 0.9 + 0.2 * R.noise(xx / 22, yy / 9, seed + 5) - 0.06 * (R.hash01(np.floor(xx * k), np.floor(yy * k), seed) > 0.9)
        coat = np.array(color, np.float64)[None, None, :] * tone[..., None]
        dull = arr[..., :3] * 0.62 + np.array(C["mortar"], np.float64)[None, None, :] * 0.3
        out = np.where(gone[..., None], dull, coat)
        above = np.vstack([gone[:1], gone[:-1]])
        below = np.vstack([gone[1:], gone[-1:]])
        left = np.hstack([gone[:, :1], gone[:, :-1]])
        out[gone & (~above | ~left)] *= 0.55  # Die Putzkante wirft Schatten in die Fehlstelle
        out[~gone & below] *= 1.14  # Bruchkante unten fängt Licht
        arr[..., :3] = np.clip(out, 0, 255)
        im.paste(Image.fromarray(arr.astype(np.uint8), "RGBA"))


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


def scrawl(img, x, y, word, height, seed):
    """Parole, mit dem Pinsel an die Wand geschmiert: schief, ungleich dick, mit Farbnasen."""
    spans = paint_text(img, x, y, word, height, [(C["paper"], 1.5, 0, 0, 0.95, 0.2)], slant=0.12, jitter=1.1, seed=seed)
    d = draw_on(img)
    for u0, u1 in spans[::2]:
        m = (u0 + u1) // 2
        d.fine_line((m, y + height, m, y + height + 2 + (m + seed) % 4), fill=C["paper"])


def arched_window(d, x, y, w, h, bars=True):
    """Hohes Bogenfenster in Steingewände, vergittert: So sieht eine Bank von außen aus."""
    r = w // 2
    d.ellipse((x - 2, y - r - 2, x + w + 1, y + r + 2), fill=C["stone_l"])
    d.rectangle((x - 2, y, x + w + 1, y + h + 1), fill=C["stone_l"])
    d.ellipse((x, y - r, x + w - 1, y + r), fill=C["glass_d"])
    d.rectangle((x, y, x + w - 1, y + h), fill=C["glass_d"])
    d.rectangle((x + r - 1, y - r - 4, x + r + 1, y - r), fill=blend(C["stone_l"], 1.08))
    d.line((x, y, x + w - 1, y), fill=C["wood_d"])
    d.line((x + r, y - r, x + r, y + h), fill=C["wood_d"])
    d.hd_only_line((x + 1, y - r + 3, x + 3, y - r + 1), fill=C["glass"])
    d.shade((x, y - r, x + w - 1, y - r + 2), 0.6)
    d.shade((x, y - r + 3, x, y + h), 0.72)
    if bars:
        for u in range(x + 1, x + w - 1, 3):
            d.fine_line((u, y - r + 2, u, y + h), fill=C["black"])
        for v in (y + h // 3, y + 2 * h // 3):
            d.fine_line((x, v, x + w - 1, v), fill=C["black"])
    sill(d, x, y, w, h, C["stone_l"])


def frieze(img, d, w, word=None):
    """Glattes Band unter dem Gesims, darin die Inschrift, in den Stein gehauen."""
    d.rectangle((0, 10, w, 27), fill=blend(C["stone"], 1.06))
    ledge(d, w, 28)
    if word:
        tw = len(word) * 4 * 10 / 6 * 0.8 + (len(word) - 1) * 5
        paint_text(img, w / 2 - tw / 2, 14, word, 10, [(C["stone_l"], 1.6, 0.7, 0.7, 0.9, 0), (C["soot"], 1.6, 0, 0, 0.85, 0)], gap=5, seed=3)


def bank_front(img):
    d = draw_on(img)
    w = img.width
    frieze(img, d, w, "BANK")
    for u in range(16, 180, 34):
        d.shade((u + 6, 32, u + 7, 74), 0.7)
        d.rectangle((u, 32, u + 5, 75), fill=C["stone_l"])
        d.fine_line((u + 2, 34, u + 2, 73), fill=C["stone"])
        d.rectangle((u - 1, 30, u + 6, 32), fill=blend(C["stone_l"], 1.08))
        d.rectangle((u - 1, 73, u + 6, 75), fill=C["stone"])
    for u in (27, 61, 129, 163):
        arched_window(d, u, 46, 14, 22)
    door(d, 88, 83, 18, 38, "brass_d", False)
    downpipe(d, 186, 2, 78)


def bank_side(img):
    d = draw_on(img)
    frieze(img, d, img.width)
    for u in (10, 38):
        arched_window(d, u, 46, 14, 22)


def name_plate(img, d, cx, y, name, height=9):
    """Emailleschild: dunkle Tafel mit Messingrand und heller Schrift."""
    tw = len(name) * 4 * height / 6 * 0.8 + (len(name) - 1) * max(1.5, height / 6 * 1.3)
    x0, x1 = int(cx - tw / 2 - 5), int(cx + tw / 2 + 5)
    d.shade((x0 + 1, y + height + 5, x1 + 1, y + height + 6), 0.6)
    d.rectangle((x0, y - 4, x1, y + height + 4), fill=C["brass_d"])
    d.rectangle((x0 + 1, y - 3, x1 - 1, y + height + 3), fill=C["black"])
    paint_text(img, cx - tw / 2, y, name, height, [(C["brass_l"], 1.4, 0, 0, 1.0, 0.25)], seed=len(name))


def tenement(w, h, name=None, material=("brick",), door_at=None, lit_rate=0.65, render=None, corner_stones=False,
             balconies=(), bays=(), pipes=()):
    """Fassade eines Mietshauses. render: Putzfarbe über den Ziegeln. balconies: (Spalte, Geschoss). bays: (Spalte, von, bis)."""
    img, d = wall(w, h, material)
    if render:
        plaster(img, render, DETAIL.randrange(1 << 16))
        for _ in range(w * h // 5200):
            patch(d, DETAIL.randint(8, w - 30), DETAIL.randint(14, h - 40), DETAIL.randint(8, 18), DETAIL.randint(5, 12),
                  blend(mix(render, C["ash"], 0.4), 0.9 + 0.2 * DETAIL.random()))
    floors = (h - 6) // 42
    columns = list(range(10, w - 16, 26))
    plinth(d, w, h)
    for column, first, last in bays:
        u = columns[column]
        bay(d, u - 7, u + 26 + 11 + 7, h - last * 42 - 38, h - first * 42 - 8, blend(render or C["stone"], 1.08))
    for f in range(floors):
        base = h - f * 42
        if f:
            ledge(d, w, base)
            for column, first, last in bays:
                u = columns[column]
                if first <= f <= last:
                    d.rectangle((u - 7, base, u + 44, base + 1), fill=blend(render or C["stone"], 1.16))
        for i, u in enumerate(columns):
            if f == 0:
                continue
            lit = RNG.random() < lit_rate
            window(d, u, base - 32, lit=lit)
            if lit:
                img.info.setdefault("panes", []).append((u, base - 32, 11, 18))
            if (i, f) in balconies:
                balcony(d, u, base - 32)
    cornice(d, w)
    if corner_stones:
        quoins(d, w, 10, h - 10)
    for u in pipes:
        downpipe(d, u, 2, h - 3)
    if door_at is not None:
        door(d, door_at, h - 1)
    if name:
        name_plate(img, d, w // 2, h - 56, name)
    chip_corners(d, w, h)
    return img


# ---------- Figuren, 22 × 40 Pixel ----------
# Realistische Proportionen: Der Kopf ist rund ein Siebtel der Körperhöhe. hem = Unterkante der Jacke oder des Mantels.
SPECS = {
    "Agent": dict(coat=("coat_d", "coat", "coat_l"), legs="iron", hat="fedora", hat_c="felt", shirt="ash", hair="hair", hem=31, belt=21),
    "Wachmann": dict(coat=("uni_d", "uni", "uni_l"), legs="uni", hat="peak", hat_c="soot", shirt="uni_d", hair="hair", hem=26, belt=19),
    "Offizier": dict(coat=("soot", "iron", "steel"), legs="soot", hat="peak", hat_c="black", shirt="paper", hair="hair", hem=32, belt=20, trim="brass_l"),
    "Werkschutz": dict(coat=("soot", "green", "green_l"), legs="soot", hat="helmet", hat_c="steel", shirt="soot", hair="hair", hem=26, belt=20, bulk=1.32, gun="shotgun", trim="steel"),
    "Schuetze": dict(coat=("wood_d", "wood", "brass_d"), legs="iron", hat="peak", hat_c="wood_d", shirt="ash", hair="hair", hem=24, belt=19, gun="rifle"),
    "Hund": dict(coat=("soot", "wood_d", "wood"), legs="wood_d", hat="peak", hat_c="soot", shirt="soot", hair="soot", hem=26, belt=19, dog=True),
    "Arbeiter": dict(coat=("grey_d", "grey", "grey_l"), legs="iron", hat="peak", hat_c="grime", shirt="ash", hair="hair", hem=25, belt=20),
    "Hundefuehrer": dict(coat=("uni_d", "uni", "uni_l"), legs="uni", hat="peak", hat_c="wood_d", shirt="uni_d", hair="hair", hem=26, belt=19, trim="wood_d", strap=True, leash=True),
    "Laufbursche": dict(coat=("iron", "grey_d", "grey"), legs="wood_d", hat="peak", hat_c="wood", shirt="ash", hair="hair", hem=22, belt=20, trim="wood", gun="none", strap=True, satchel=True, scale=0.86),
}
# Die vier Agenten: braune, schäbige Mäntel in Abstufung, dazu je eigener Hut, eigene Statur und eigenes Beiwerk.
# Farben als RGB, damit die Palette der Stadt unberührt bleibt.
AGENTS = {
    # Truppführerin: Schlapphut mit dunklem Band, langer Mantel, weinroter Schal
    "Greta": dict(coat=((82, 58, 40), (126, 90, 60), (158, 120, 84)), legs="iron", hat="fedora", hat_c=(64, 48, 40), band=(30, 26, 26),
                  shirt="ash", hair="hair", hem=33, belt=21, scarf=(128, 46, 40), bulk=0.97),
    # Schlosser: breit, kurze dunkle Joppe, Schiebermütze, Werkzeugtasche
    "Otto": dict(coat=((50, 38, 30), (82, 60, 44), (110, 84, 62)), legs="soot", hat="peak", hat_c=(96, 88, 78), shirt="ash", hair="hair",
                 hem=25, belt=20, bulk=1.2, strap=True, satchel=True, trim=(60, 44, 34)),
    # Buchhalter: schmal, heller langer Mantel, Melone, weißer Kragen
    "Emil": dict(coat=((118, 94, 64), (162, 132, 92), (194, 164, 120)), legs="grey_d", hat="bowler", hat_c=(40, 36, 38), shirt="paper",
                 hair="hair", hem=33, belt=21, bulk=0.93),
    # Kurierin: klein, graubrauner kurzer Mantel, Baskenmütze, rotes Haar, Kuriertasche, heller Schal
    "Hanna": dict(coat=((64, 58, 42), (98, 90, 64), (128, 118, 88)), legs="iron", hat="beret", hat_c=(52, 44, 58), shirt="ash",
                  hair=(150, 84, 48), bun=True, hem=27, belt=20, scale=0.92, strap=True, satchel=True, trim=(150, 124, 86),
                  scarf=(168, 160, 140)),
}
def _worn(color):
    """Rostbrauner Ton für Rekruten: gleiche Helligkeit, röter. Grau sähe den Wachen zu ähnlich."""
    level = sum(color) / 3
    return (min(255, round(level * 1.3)), round(level * 0.86), round(level * 0.62))


# Rekruten tragen die Gestalt eines der vier, aber rostbraun. Das ist nur eine zweite Palettenzeile, kein eigenes Bild.
for _spec in AGENTS.values():
    _spec["variants"] = {"rekrut": dict(coat=tuple(_worn(c) for c in _spec["coat"]), scarf=(96, 100, 96))}

# Passanten, reine Kulisse: gedeckte Farben, kein Lichtsaum, keine Waffe. Farbvarianten stehen als weitere Palettenzeilen bereit.
CIVILIANS = {
    # Arbeiter mit Henkelmann
    "Zivil_Arbeiter": dict(coat=((44, 52, 66), (66, 78, 96), (88, 100, 118)), legs="soot", hat="peak", hat_c=(70, 66, 62), shirt="ash",
                           hair="hair", hem=25, belt=20, pail=True, trim=(96, 100, 108),
                           variants={"blau": dict(coat=((36, 48, 78), (54, 72, 110), (74, 94, 134))),
                                     "braun": dict(coat=((60, 46, 34), (90, 70, 50), (116, 94, 70)))}),
    # Frau mit Kopftuch und Einkaufsnetz, langer Rock
    "Zivil_Frau": dict(coat=((70, 50, 56), (104, 74, 82), (130, 98, 104)), legs="soot", hat="kerchief", hat_c=(150, 60, 52), shirt="ash",
                       hair="hair", hem=37, belt=20, bulk=0.95, scale=0.94, net=True, trim=(120, 104, 76),
                       variants={"gruen": dict(coat=((46, 62, 50), (70, 92, 74), (94, 116, 96)), hat_c=(64, 84, 130)),
                                 "grau": dict(coat=((60, 60, 66), (90, 90, 98), (116, 116, 124)), hat_c=(160, 128, 62))}),
    # Herr mit Melone, der geschlossene Schirm dient als Stock
    "Zivil_Herr": dict(coat=((34, 34, 40), (54, 54, 62), (76, 76, 86)), legs="soot", hat="bowler", hat_c=(30, 28, 30), shirt="paper",
                       hair="hair", hem=34, belt=21, umbrella=True,
                       variants={"grau": dict(coat=((70, 70, 74), (104, 104, 108), (130, 130, 134)), hat_c=(70, 68, 68))}),
    # Zeitungsjunge: klein, Mütze, ein Packen Zeitungen unter dem Arm
    "Zivil_Junge": dict(coat=((70, 74, 60), (102, 108, 88), (126, 132, 110)), legs="iron", hat="peak", hat_c=(96, 80, 60), shirt="ash",
                        hair="hair", hem=22, belt=20, scale=0.78, strap=True, papers=True, trim=(80, 60, 44),
                        variants={"braun": dict(coat=((66, 50, 38), (98, 76, 56), (124, 100, 76)))}),
    # Betrunkener: ohne Hut, Flasche in der Hand, schwankt
    "Zivil_Trinker": dict(coat=((58, 54, 44), (88, 82, 66), (112, 106, 88)), legs="iron", hat="none", hat_c="soot", shirt="ash",
                          hair=(120, 116, 108), hem=30, belt=21, bottle=True, drunk=True, band=(40, 70, 50),
                          variants={"grau": dict(coat=((52, 54, 58), (78, 82, 88), (102, 106, 112)))}),
}
# Mit anderer Waffe in der Hand gibt es eigene Zeilen im Figurenblatt. Das Spiel wählt nach Name und Waffe.
WEAPON_ROWS = {"": "pistol", "_Schrot": "sawn", "_MP": "smg", "_Knueppel": "club"}
# Was figures3d.py rendert: die Agenten zuerst, dann alle übrigen Figuren. "Agent" bleibt nur für das Konzeptblatt.
SHEET_SPECS = {name + suffix: dict(spec, gun=gun, agent=True) for name, spec in AGENTS.items() for suffix, gun in WEAPON_ROWS.items()}
SHEET_SPECS.update({name: spec for name, spec in SPECS.items() if name != "Agent"})
SHEET_SPECS.update({name: dict(spec, gun="none", civil=True) for name, spec in CIVILIANS.items()})


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
        self.panes = []
        self.hd = True  # zeichnet sich auch in doppelter Auflösung
        self.prisms = []  # Dachkörper, siehe roofs.py


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
    if s.res > 1:
        tex = tex.info.get("hd") or tex.resize((tex.width * s.res, tex.height * s.res), Image.NEAREST)
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
    img = canvas(max(1, w), max(1, h), C[c])
    if c2:
        draw_on(img).line((0, 0, w, 0), fill=C[c2])
    return img


def weathered(tex):
    """Verschmutzt eine Fassade einmal. Ein zweites Zeichnen, etwa in doppelter Auflösung, nimmt dieselbe Fassung."""
    if "weathered" not in tex.info:
        tex.info["weathered"] = weather(tex)
    return tex.info["weathered"]


def box(s, x0, y0, x1, y1, z0, h, left=None, right=None, top=None, base="iron"):
    wl, wr = int((x1 - x0) * HW), int((y1 - y0) * HW)
    left = weathered(left or plain_tex(wl, int(h), base, "ash"))
    right = weathered(right or plain_tex(wr, int(h), base, "ash"))
    west = s.to_screen(x0, y1, z0 + h)
    south = s.to_screen(x1, y1, z0 + h)
    shear(s, left, int(west[0]), int(west[1]), 0.5, 1.0)
    shear(s, right, int(south[0]), int(south[1]), -0.5, 0.74)
    top_face(s, x0, y0, x1, y1, z0 + h, top or (lambda a, b, w, d: C["steel"]))


def roof_color(a, b, w, d):
    """Dachpappe in Bahnen hinter einer hellen Mauerkrone. Das Licht dazu kommt vom Mond, siehe bake_light.py."""
    rim = min(a, b, w - a, d - b)
    seed = int(w * 13 + d * 7)
    n = noise(a * 3.2, b * 3.2, seed)
    # Der Zufall läuft genau wie früher mit: Davon hängt ab, welche Fenster leuchten, und damit das Licht im Spiel.
    gritty = rim >= 0.12 and (a * 2) % 1 >= 0.05 and RNG.random() < 0.07
    if rim < 0.15:
        c = C["stone_l"] if (int(a * 7) + int(b * 7)) % 2 else C["stone"]
        return blend(c, 0.72 + 0.34 * n)
    if rim < 0.23:
        return blend(C["soot"], 0.7 + 0.3 * n)  # Schatten innen an der Brüstung
    lane = int(a * 2)
    c = blend(mix(C["tar_l"], C["gravel"], 0.45), 0.84 + 0.3 * hash01(lane, seed, 5))
    if (a * 2) % 1 < 0.07:
        c = C["soot"]  # Naht zwischen zwei Bahnen
    elif gritty:
        c = blend(c, 1.12)
    # Geflickte Dachpappe in schiefen Rechtecken
    patch = hash01(int(a * 2.5), int(b * 2.5), seed)
    if patch > 0.84:
        c = mix(c, C["soot"], 0.55)
    elif patch < 0.08:
        c = mix(c, C["plaster"], 0.3)
    fine = noise(a * 14, b * 14, seed + 1)
    if n > 0.74 and fine > 0.3:
        return mix(c, C["puddle"], 0.75)  # stehendes Wasser
    if n < 0.27 and fine > 0.5:
        return mix(c, C["moss"], 0.45)  # nur Sprenkel, keine zusammenhängenden Flächen
    return blend(c, 0.74 + 0.34 * n)


# ---------- Dachflächen: Farbe je getroffenem Punkt, für roofs.render ----------
def _c(name):
    return np.array(C[name], np.float64)


def _grime(f, strength=0.3, seed=3):
    """Großflächige Verfärbung, damit keine Dachfläche gleichmäßig aussieht."""
    return 1 - strength + 2 * strength * R.noise(f.fx * 2.3, f.fy * 2.3 + f.z / 40, seed)


def mat_slate(f):
    """Schieferdeckung in versetzten Reihen. Einzelne Platten fehlen oder sind ersetzt, unten sitzt Moos."""
    u, v = f.slope_uv()
    k = f.res
    course = np.floor(v / (4 * k))
    uu = u + (course % 2) * 3 * k
    tile = np.floor(uu / (6 * k))
    pick = R.hash01(tile, course, 11)
    col = np.array((60, 64, 80), np.float64)[None, :] * (0.78 + 0.44 * R.hash01(tile, course, 12))[:, None]
    col = np.where((pick > 0.955)[:, None], _c("wood_d")[None, :] * 0.8, col)
    col = np.where(((pick < 0.05))[:, None], np.array((92, 96, 108.0))[None, :], col)
    joint = (np.floor(v) % (4 * k) == 0) | (np.floor(uu) % (6 * k) == 0)
    col = col * np.where(joint, 0.6, 1.0)[:, None]
    moss = (R.noise(f.fx * 5, f.fy * 5, 21) > 0.62) & (R.noise(u / k / 2.0, v / k / 2.0, 22) > 0.45)
    col = np.where(moss[:, None], col * 0.6 + _c("moss")[None, :] * 0.4, col)
    return col * _grime(f)[:, None]


def mat_tar(f):
    """Dachpappe in Bahnen mit Nähten, Flicken und stehendem Wasser."""
    k = f.res
    a, b = f.fx - f.prism.x0, f.fy - f.prism.y0
    lane = np.floor(a * 2)
    col = (_c("tar_l") * 0.55 + _c("gravel") * 0.45)[None, :] * (0.84 + 0.3 * R.hash01(lane, 3, 5))[:, None]
    seam = np.floor(a * HW * k) % (HW * k // 2) == 0
    col = np.where(seam[:, None], _c("soot")[None, :], col)
    spot = R.hash01(np.floor(a * 2.5), np.floor(b * 2.5), 8)
    col = np.where((spot > 0.84)[:, None], col * 0.45 + _c("soot")[None, :] * 0.55, col)
    col = np.where((spot < 0.08)[:, None], col * 0.7 + _c("plaster")[None, :] * 0.3, col)
    n = R.noise(a * 3.2, b * 3.2, 9)
    fine = R.noise(a * 14, b * 14, 10)
    col = np.where(((n > 0.74) & (fine > 0.3))[:, None], col * 0.25 + _c("puddle")[None, :] * 0.75, col)
    col = col * (1 + 0.12 * (R.hash01(np.floor(a * HW * k), np.floor(b * HW * k), 4) > 0.93))[:, None]
    return col * (0.74 + 0.34 * n)[:, None]


def mat_copper(f):
    """Kupferblech mit Stehfalzen, grün angelaufen, mit dunklen Laufspuren."""
    u, v = f.slope_uv()
    k = f.res
    col = np.array((86, 132, 116), np.float64)[None, :] * (0.8 + 0.4 * R.noise(u / k / 9, v / k / 5, 31))[:, None]
    bare = R.noise(f.fx * 4, f.fy * 4, 32) > 0.72
    col = np.where(bare[:, None], col * 0.5 + np.array((120, 84, 56.0))[None, :] * 0.5, col)
    streak = R.noise(u / k / 1.5, v / k / 30, 33) > 0.68
    col = col * np.where(streak, 0.78, 1.0)[:, None]
    seam = np.floor(u) % (7 * k) == 0
    col = col * np.where(seam, 0.62, 1.0)[:, None]
    lit = np.floor(u) % (7 * k) == 1
    return col * np.where(lit, 1.12, 1.0)[:, None]


def mat_sheet(f, base=(100, 106, 114), rust=1.0):
    """Wellblech: Wellen entlang des Gefälles, Tafeln überlappen, Rost läuft von den Stößen aus nach unten."""
    u, v = f.slope_uv()
    k = f.res
    wave = 0.88 + 0.16 * np.sin(u / k * 2 * math.pi / 3)
    col = np.array(base, np.float64)[None, :] * wave[:, None]
    lap = np.floor(v) % (13 * k) == 0
    col = col * np.where(lap, 0.62, 1.0)[:, None]
    panel = R.hash01(np.floor(u / k / 24), np.floor(v / k / 13), 43)
    col = col * (0.9 + 0.2 * panel)[:, None]
    field = R.noise(u / k / 16, v / k / 9, 41) * 0.7 + R.noise(u / k / 2.2, v / k / 11, 42) * 0.3
    rusty = np.clip((field - 0.6 + 0.1 * rust) * 4, 0, 1)[:, None] * min(1.0, rust)
    brown = (_c("rust") * 0.6 + _c("rust_d") * 0.4)[None, :] * wave[:, None]
    col = col * (1 - rusty * 0.85) + brown * rusty * 0.85
    return col * _grime(f, 0.2)[:, None]


def glazing(u, v, k, lit_share, seed, pane_w=7, pane_h=9):
    """Verglasung mit Eisensprossen. Licht kommt von einzelnen Lampen dahinter, deshalb leuchten Scheiben in Gruppen.

    Gibt Farbe und die Maske der selbstleuchtenden Punkte zurück.
    """
    cell_u, cell_v = np.floor(u / (pane_w * k)), np.floor(v / (pane_h * k))
    pick = R.hash01(cell_u, cell_v, seed)
    lamp = R.noise(cell_u / 3.5, cell_v / 2.0, seed + 2) + 0.25 * pick
    lit = (lamp > 1.02 - lit_share) & (lit_share > 0)
    depth = (v / k - cell_v * pane_h) / pane_h
    warm = _c("gas_d")[None, :] * (1 - depth * 0.25)[:, None] + _c("gas")[None, :] * (depth * 0.25)[:, None]
    dirt = 0.6 + 0.6 * R.noise(u / k / 3, v / k / 3, seed + 1)
    dark = (_c("glass_d") * 0.75 + _c("soot") * 0.25)[None, :] * dirt[:, None]
    col = np.where(lit[:, None], warm, dark)
    col = np.where((pick > 0.94)[:, None], _c("black")[None, :], col)
    patched = (pick > 0.86) & (pick <= 0.94)
    col = np.where(patched[:, None], _c("iron")[None, :] * 0.9, col)  # mit Blech geflickt
    bars = (np.floor(u) % (pane_w * k) == 0) | (np.floor(v) % (pane_h * k) == 0)
    col = np.where(bars[:, None], _c("iron")[None, :] * 0.7, col)
    glow = lit & ~bars & (pick <= 0.86)
    return col, glow


def brick_np(u, v, k, seed=0):
    """Mauerwerk wie in wall(), für Giebel und Schornsteine. u, v in Bildpunkten der Auflösung."""
    row = np.floor(v / (4 * k))
    mortar = (np.floor(v) % (4 * k) == 4 * k - 1) | (np.floor(u + (row % 2) * 4 * k) % (8 * k) == 8 * k - 1)
    stone = np.floor((u + (row % 2) * 4 * k) / (8 * k))
    pick = R.hash01(stone, row, seed)
    col = np.where((pick < 0.1)[:, None], _c("brick_d")[None, :], np.where((pick > 0.9)[:, None], _c("brick_l")[None, :], _c("brick")[None, :]))
    col = np.where(mortar[:, None], _c("mortar")[None, :], col)
    grime = 0.86 * (1 - np.clip(R.noise(u / k / 15, v / k / 15, seed + 1) - 0.55, 0, 1) * 0.95)
    return (col * grime[:, None]) * 0.88 + _c("grime")[None, :] * 0.12


def mat_mansard(f):
    if f.kind != "plane":
        return brick_np(*f.wall_uv(), f.res)
    if f.plane == len(f.prism.planes) - 1:
        return mat_tar(f)
    if f.plane >= 4:
        return mat_sheet(f, (92, 98, 110), 0.25)  # das flache Oberdach ist mit Zinkblech gedeckt
    col = mat_slate(f)
    # Unten an der Traufe sammelt sich der Dreck.
    low = np.clip(1 - (f.z - f.prism.z0) / 6, 0, 1)
    return col * (1 - 0.35 * low)[:, None]


def mat_shed(f):
    """Sheddach der Fabrik: Blech auf der flachen Seite, Glas mit Sprossen auf der steilen, Giebel wie die Wand."""
    k = f.res
    if f.kind == "plane":
        return mat_sheet(f, (84, 92, 96), 0.7)
    u, v = f.wall_uv()
    top = f.prism.top()
    if f.kind == "x1":
        vv = v + top * k
        col, glow = glazing(u, vv - 2 * k, k, 0.3, 51 + int(f.prism.x0 * 10), 5, 7)
        frame = (vv < 2 * k) | (f.z - f.prism.z0 < 2)
        col = np.where(frame[:, None], _c("iron")[None, :], col)
        return col, glow & ~frame
    un = np.floor(u / k) % 6
    col = np.where((un == 0)[:, None], _c("green_d")[None, :], np.where((un == 2)[:, None], _c("green_l")[None, :], _c("green")[None, :]))
    return col * 0.8 * _grime(f, 0.2)[:, None]


def mat_gable_brick(f):
    """Satteldach des Lagers: Wellblech mit einem Lichtband, der Giebel gemauert mit Ladeluke."""
    k = f.res
    p = f.prism
    if f.kind == "plane":
        col = mat_sheet(f)
        if f.normal[0] > 0:
            up = (f.z - p.z0) / max(1e-6, p.top() - p.z0)
            along = (f.fy - p.y0) / (p.y1 - p.y0)
            band = (up > 0.36) & (up < 0.62) & (np.abs(along - 0.5) < 0.36) & (np.floor(along * 9) % 3 != 2)
            u, v = f.slope_uv()
            g, glow = glazing(u, v, k, 0.0, 61, 5, 4)
            col = np.where(band[:, None], g, col)
        return col
    u, v = f.wall_uv()
    col = brick_np(u, v + p.z0 * k, k, 7)
    if f.kind == "y1":
        mid = (p.x1 - p.x0) * HW / 2
        uu, zz = u / k - mid, f.z - p.z0
        hatch = (np.abs(uu) < 7) & (zz > 6) & (zz < 22)
        planks = np.floor(u / k) % 3 == 0
        col = np.where(hatch[:, None], _c("wood")[None, :] * np.where(planks, 0.6, 1.0)[:, None], col)
        frame = (np.abs(uu) < 9) & (zz > 4) & (zz < 24) & ~hatch
        col = np.where(frame[:, None], _c("stone_l")[None, :], col)
        beam = (np.abs(uu) < 1.5) & (zz >= 24) & (zz < 30)
        col = np.where(beam[:, None], _c("iron")[None, :], col)
        # Verrußt unter dem First
        col = col * (1 - 0.3 * np.clip((zz - 26) / 14, 0, 1))[:, None]
    return col


def mat_hip(f):
    return mat_copper(f) if f.kind == "plane" else brick_np(*f.wall_uv(), f.res)


def mat_pediment(f):
    """Giebeldreieck der Bank: heller Stein mit Gesims an den Schrägen und einem runden Fenster."""
    if f.kind != "y1":
        return mat_copper(f) if f.kind == "plane" else _c("stone")[None, :] * np.ones((len(f.z), 1))
    p = f.prism
    mid = (p.x0 + p.x1) / 2
    edge = np.minimum.reduce([(d - a * f.fx - b * f.fy) / c - f.z for a, b, c, d, *_ in p.planes])
    zz = f.z - p.info["base"]
    col = _c("stone")[None, :] * (0.9 + 0.2 * R.noise(f.fx * 9, f.z / 4, 71))[:, None]
    col = np.where(((edge < 3.2) | (zz < 3))[:, None], _c("stone_l")[None, :], col)
    col = np.where((((edge >= 3.2) & (edge < 4.2)) | ((zz >= 3) & (zz < 4)))[:, None], _c("soot")[None, :], col)
    r = np.hypot((f.fx - mid) * HW, zz - 11)
    col = np.where((r < 5)[:, None], _c("stone_l")[None, :], col)
    col = np.where((r < 3.6)[:, None], _c("glass_d")[None, :], col)
    return col


def mat_chimney(f):
    p = f.prism
    k = f.res
    top = p.top()
    if f.kind == "plane":
        a, b = (f.fx - p.x0) / (p.x1 - p.x0), (f.fy - p.y0) / (p.y1 - p.y0)
        rim = np.minimum.reduce([a, b, 1 - a, 1 - b])
        return np.where((rim > 0.26)[:, None], _c("black")[None, :], _c("stone_l")[None, :] * 0.9)
    u, v = f.wall_uv()
    col = brick_np(u, v + top * k, k, int(p.x0 * 37 + p.y0 * 11))
    col = np.where((top - f.z < 3)[:, None], _c("stone_l")[None, :], col)
    col = np.where(((top - f.z >= 3) & (top - f.z < 4))[:, None], _c("soot")[None, :], col)
    return col * (0.62 + 0.38 * np.clip((top - f.z) / 12, 0, 1))[:, None]  # oben vom Rauch schwarz


def mat_vent(f):
    """Lüfterhaube aus Blech mit Lamellen an den Seiten."""
    p = f.prism
    if f.kind == "plane":
        return _c("steel")[None, :] * (0.8 + 0.2 * R.noise(f.fx * 30, f.fy * 30, 80))[:, None]
    down = p.top() - f.z
    col = _c("steel")[None, :] * np.ones((len(f.z), 1)) * 0.9
    slat = (np.floor(down * f.res) % (2 * f.res) == 0) & (down > 2)
    col = np.where(slat[:, None], _c("soot")[None, :], col)
    return np.where(((R.noise(f.fx * 40, f.z / 2, 82) > 0.74) & ~slat)[:, None], _c("rust")[None, :] * 0.8, col)


def mat_hatch(f):
    """Dachluke oder Oberlicht: Glas in einem Eisenrahmen, liegt auf der Dachschräge."""
    p = f.prism
    if f.kind != "plane":
        return _c("iron")[None, :] * np.ones((len(f.z), 1))
    k = f.res
    a, b = (f.fx - p.x0) * HW * k, (f.fy - p.y0) * HW * k
    col, glow = glazing(a - k, b - k, k, p.info.get("lit", 0.4), int(p.x0 * 53 + p.y0 * 17), 6, 6)
    wa, wb = (p.x1 - p.x0) * HW * k, (p.y1 - p.y0) * HW * k
    frame = (a < k) | (b < k) | (a > wa - 2 * k) | (b > wb - 2 * k)
    col = np.where(frame[:, None], _c("brass_d")[None, :], col)
    return col, glow & ~frame


def mat_dormer(f):
    """Gaube: Fenster in der Stirnseite, Wangen aus Zinkblech, kleines Schieferdach."""
    p = f.prism
    k = f.res
    front = "y1" if p.name == "dormer_y" else "x1"
    if f.kind == "plane":
        return mat_slate(f)
    if f.kind != front:
        return _c("steel")[None, :] * (0.7 + 0.2 * R.noise(f.fx * 20, f.z / 3, 91))[:, None]
    wall = p.info["wall"]
    if front == "y1":
        u, width = (f.fx - p.x0) * HW, (p.x1 - p.x0) * HW
    else:
        u, width = (p.y1 - f.fy) * HW, (p.y1 - p.y0) * HW
    down = wall - f.z
    col = _c("stone_l")[None, :] * (0.85 + 0.15 * R.noise(u, f.z / 2, 92))[:, None] * np.ones((len(u), 1))
    col = np.where((down < 0)[:, None], _c("wood")[None, :] * np.where(np.floor(u * k) % (2 * k) == 0, 0.7, 1.0)[:, None], col)
    glass = (u > 2.5) & (u < width - 2.5) & (down > 1.5) & (down < p.info["glass"])
    lit = p.info["lit"]
    pane = (_c("gas_d") if lit else _c("glass_d"))[None, :] * (1 - 0.3 * (down < 3.5))[:, None]
    bars = (np.abs(np.floor(u * k) - np.floor(width * k / 2)) < 1) | (np.abs(np.floor(down * k) - np.floor(5.5 * k)) < 1)
    pane = np.where(bars[:, None], _c("wood_d")[None, :], pane)
    col = np.where(glass[:, None], pane, col)
    glow = glass & ~bars & (down >= 3.5) if lit else np.zeros(len(u), bool)
    return col, glow


# ---------- Dachformen der Häuser ----------
def roof_mansard(dormers_y=(), dormers_x=(), steep=30, upper=6):
    """Mansarddach mit Gauben. dormers_y und dormers_x: Lage der Gauben entlang der linken und rechten Fassade in Feldern."""
    def make(x0, y0, x1, y1, h):
        prisms = [R.mansard(x0, y0, x1, y1, h, steep=steep, upper=upper, material=mat_mansard)]
        for along, facing in [(a, "y") for a in dormers_y] + [(a, "x") for a in dormers_x]:
            if facing == "y":
                p = R.dormer(x0 + along, x0 + along + 0.44, y1 - 0.1, 0.9, h, h + steep - 9, h + steep - 1, "y", mat_dormer)
            else:
                p = R.dormer(y1 - along - 0.44, y1 - along, x1 - 0.1, 0.9, h, h + steep - 9, h + steep - 1, "x", mat_dormer)
            p.info = {"wall": h + steep - 9, "glass": steep - 9 - 60 * 0.1 - 2, "lit": DETAIL.random() < 0.35}
            prisms.append(p)
        return prisms
    return make


def roof_gable(rise, ridge="y", sag=2.0, material=mat_gable_brick):
    def make(x0, y0, x1, y1, h):
        return [R.gable(x0, y0, x1, y1, h, rise, ridge, sag, material)]
    return make


def roof_shed(teeth, rise):
    def make(x0, y0, x1, y1, h):
        return R.sawtooth(x0, y0, x1, y1, h, teeth, rise, mat_shed, sag=1.2)
    return make


def roof_bank(rise=24, pediment_at=3.03, pediment_w=1.7, pediment_h=34):
    """Walmdach aus Kupfer, über dem Eingang ein Giebeldreieck."""
    def make(x0, y0, x1, y1, h):
        k = pediment_h / (pediment_w / 2)
        xm = x0 + pediment_at
        front = R.Prism(xm - pediment_w / 2, y1 - 0.8, xm + pediment_w / 2, y1, h,
                        [R.rise_from("x0", xm - pediment_w / 2, h, k), R.rise_from("x1", xm + pediment_w / 2, h, k)], mat_pediment, "pediment")
        front.info = {"base": h}
        return [R.hip(x0, y0, x1, y1, h, rise, 44, mat_hip), front]
    return make


def building(x0, y0, w, d, h, left, right, s, things, roof_items=(), roof=None):
    prisms = roof(x0, y0, x0 + w, y0 + d, h) if roof else []

    def draw():
        box(s, x0, y0, x0 + w, y0 + d, 0, h, left, right, (lambda a, b, w2, d2: None) if prisms else roof_color)
        if prisms:
            R.render(s, prisms)
    things.append(Thing(x0, y0, x0 + w, y0 + d, 0, h, draw, solid=True, opaque=True))
    house = things[-1]
    house.prisms = prisms
    # Erleuchtete Fenster mit ihrer Lage im Bild, damit das Spiel sie einzeln schalten kann
    west = s.to_screen(x0, y0 + d, h)
    south = s.to_screen(x0 + w, y0 + d, h)
    for tex, origin, slope in ((left, west, 0.5), (right, south, -0.5)):
        for u, v, pw, ph, *fixed in tex.info.get("panes", []):
            things[-1].panes.append({"x": int(origin[0]), "y": int(origin[1]), "slope": slope, "u": u, "v": v, "w": pw, "h": ph, "kind": "window", "type": fixed[0] if fixed else ""})
    for item in roof_items:
        item(x0, y0, w, d, h, s, things, house)


def roof_base(house, h, x, y, x1=None, y1=None):
    """Höhe des Dachs unter einem Aufbau: der höchste Punkt unter seiner Grundfläche."""
    if not house.prisms:
        return h
    points = [(x, y)] if x1 is None else [(x, y), (x1, y), (x, y1), (x1, y1), ((x + x1) / 2, (y + y1) / 2)]
    return max(R.height_at(house.prisms, px, py) or h for px, py in points)


def roof_block(size, height, material, kind_smoke=False):
    """Quader, der aus dem Dach ragt. Er wird mit dem Dach zusammen geschnitten: Was der First verdeckt, fehlt im Bild."""
    def make(fx, fy, hc=height, smoke=kind_smoke):
        def add(x0, y0, w, d, h, s, things, house):
            x, y = x0 + fx * w, y0 + fy * d
            top = roof_base(house, h, x, y, x + size, y + size) + hc
            body = R.block(x, y, x + size, y + size, h, top, material)
            if smoke:
                cx, cy = s.to_screen(x + size / 2, y + size / 2, top)
                AMBIENT["smoke"].append([int(cx), int(cy) - 2])

            def draw():
                R.render(s, house.prisms + [body], only={body})
            things.append(Thing(x, y, x + size, y + size, h, top, draw))
        return add
    return make


chimney = roof_block(0.32, 26, mat_chimney, True)
vent = roof_block(0.34, 10, mat_vent)


def skylight(fx, fy, lit=0.4, size=(0.9, 0.6)):
    """Oberlicht: Glas mit Sprossen in einem Rahmen, liegt auf der Dachfläche und folgt ihrer Neigung."""
    def add(x0, y0, w, d, h, s, things, house):
        x, y = x0 + fx * w, y0 + fy * d
        mx, my = x + size[0] / 2, y + size[1] / 2
        plane = R.flat(h + 5)
        for p in house.prisms:
            if p.height(mx, my) is not None and p.height(mx, my) >= roof_base(house, h, mx, my) - 0.01:
                plane = R.lifted(min((q for q in p.planes if q[2] > 0), key=lambda q: (q[3] - q[0] * mx - q[1] * my) / q[2])[:4], 4)
        hatch = R.Prism(x, y, x + size[0], y + size[1], h, [plane], mat_hatch, "hatch")
        hatch.info = {"lit": lit}

        def draw():
            R.render(s, house.prisms + [hatch], only={hatch})
        things.append(Thing(x, y, x + size[0], y + size[1], h, hatch.top(), draw))
    return add


def water_tank(fx, fy):
    def add(x0, y0, w, d, h, s, things, house):
        x, y = x0 + fx * w, y0 + fy * d
        base = roof_base(house, h, x + 0.4, y + 0.4)

        def draw():
            k = s.res
            cx, cy = s.to_screen(x + 0.4, y + 0.4, base)
            cx, cy = int(cx), int(cy)
            for lx in (-9, 8):
                s.rect(cx + lx * k, cy - 16 * k, 2 * k, 16 * k, C["iron"])
            # Kreuzstreben zwischen den Beinen, fein gezeichnet
            d2 = ImageDraw.Draw(s.img)
            d2.line((cx - 8 * k, cy - 2 * k, cx + 8 * k, cy - 14 * k), fill=C["iron"] + (255,), width=1)
            d2.line((cx - 8 * k, cy - 14 * k, cx + 8 * k, cy - 2 * k), fill=C["iron"] + (255,), width=1)
            cylinder(s, cx, cy - 12 * k - 24 * k, 12, 24, ("wood_d", "wood", "brass"), bands=True)
        things.append(Thing(x, y, x + 0.8, y + 0.8, base, base + 40, draw))
    return add


def clothesline(fx0, fy0, fx1, fy1):
    """Wäscheleine zwischen zwei Pfosten auf dem Dach."""
    def add(x0, y0, w, d, h, s, things, house):
        ax, ay, bx, by = x0 + fx0 * w, y0 + fy0 * d, x0 + fx1 * w, y0 + fy1 * d
        base = roof_base(house, h, (ax + bx) / 2, (ay + by) / 2)
        wash = [(DETAIL.uniform(0.1, 0.9), DETAIL.randint(3, 6), DETAIL.randint(5, 10),
                 DETAIL.choice(["paper", "cream", "grey_l", "awn_w", "navy_l", "wine_l"])) for _ in range(5)]

        def draw():
            k = s.res
            dr = ImageDraw.Draw(s.img)
            a, b = s.to_screen(ax, ay, base), s.to_screen(bx, by, base)
            for px, py in (a, b):
                dr.rectangle((px - k // 2, py - 15 * k, px + k // 2, py), fill=C["wood_d"] + (255,))
                dr.line((px - 2 * k, py - 14 * k, px + 2 * k, py - 14 * k), fill=C["wood_d"] + (255,))
            rope = lambda t: (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t - 14 * k + math.sin(math.pi * t) * 3 * k)  # noqa: E731
            pts = [rope(i / 24) for i in range(25)]
            dr.line(pts, fill=C["ash"] + (255,), width=1)
            for t, cw, ch, col in sorted(wash):
                px, py = rope(t)
                dr.rectangle((px, py, px + cw * k, py + ch * k), fill=C[col] + (255,))
                dr.line((px, py + ch * k, px + cw * k, py + ch * k), fill=blend(C[col], 0.7) + (255,))
                dr.line((px + cw * k, py, px + cw * k, py + ch * k), fill=blend(C[col], 0.8) + (255,))
        things.append(Thing(min(ax, bx), min(ay, by), max(ax, bx) + 0.05, max(ay, by) + 0.05, base, base + 16, draw))
    return add


def roof_mast(fx, fy, height=30):
    """Dachständer: Mast mit Querträgern und Isolatoren, die Drähte laufen durchhängend nach hinten aus dem Bild."""
    def add(x0, y0, w, d, h, s, things, house):
        x, y = x0 + fx * w, y0 + fy * d
        base = roof_base(house, h, x, y)

        def draw():
            k = s.res
            dr = ImageDraw.Draw(s.img)
            px, py = s.to_screen(x, y, base)
            dr.rectangle((px - k // 2, py - height * k, px + k // 2, py), fill=C["iron"] + (255,))
            dr.line((px - 5 * k, py, px, py - 9 * k), fill=C["iron"] + (255,))
            for i, up in enumerate((height - 2, height - 9)):
                ty = py - up * k
                dr.line((px - 7 * k, ty + 3 * k, px + 7 * k, ty - 3 * k), fill=C["iron"] + (255,), width=max(1, k // 2 + 1))
                for side in (-6, -3, 3, 6):
                    ix, iy = px + side * k, ty - side * k * 3 // 7
                    dr.rectangle((ix - k // 2, iy - 2 * k, ix + k // 2, iy), fill=C["paper"] + (255,))
                    # Draht: läuft nach hinten links weg und hängt durch
                    span = (70 + i * 14) * k
                    pts = [(ix - span * t, iy - 2 * k - span * t * 0.36 + math.sin(math.pi * min(1, t * 1.1)) * 7 * k) for t in (j / 16 for j in range(17))]
                    dr.line(pts, fill=C["soot"] + (255,), width=1)
        things.append(Thing(x - 0.1, y - 0.1, x + 0.1, y + 0.1, base, base + height, draw))
    return add


NEON = [((92, 22, 30), 3.6, 0, 0, 0.9, 0), ((226, 62, 52), 2.0, 0, 0, 1.0, 0), ((255, 186, 160), 0.8, 0, 0, 1.0, 0)]


def roof_sign(word, fx=0.18, fy=0.65, height=13):
    """Leuchtreklame auf einem Eisengerüst über dem Dach. Jeder Buchstabe ist eine eigene Röhre und wird einzeln geschaltet."""
    def add(x0, y0, w, d, h, s, things, house):
        x, y = x0 + fx * w, y0 + fy * d
        base = roof_base(house, h, x + 0.5, y)
        scale = height / 6
        gap = scale * 1.5
        tw = int(len(word) * 4 * scale * 0.8 + (len(word) - 1) * gap)
        legs = 10
        tex = canvas(tw + 10, height + 6 + legs, (0, 0, 0, 0))
        dr = draw_on(tex)
        top = height + 5
        # Gerüst: zwei Gurte, Pfosten und Kreuzstreben
        dr.rectangle((0, top, tw + 9, top), fill=C["iron"])
        dr.rectangle((0, top + legs - 1, tw + 9, top + legs - 1), fill=C["iron"])
        for u in range(1, tw + 8, 12):
            dr.rectangle((u, top, u, top + legs - 1), fill=C["iron"])
            dr.fine_line((u, top, min(tw + 9, u + 12), top + legs - 1), fill=C["soot"])
            dr.fine_line((u, top + legs - 1, min(tw + 9, u + 12), top), fill=C["soot"])
        # Rückwand aus Blechstreifen, damit die Röhren Halt haben
        dr.fine_line((2, 2 + height // 2, tw + 7, 2 + height // 2), fill=C["soot"])
        spans = paint_text(tex, 5, 3, word, height, NEON, gap=gap, seed=5)

        def draw():
            west = s.to_screen(x, y + 0.05, base + tex.height)
            shear(s, tex, int(west[0]), int(west[1]), 0.5, 1.0)
        things.append(Thing(x, y, x + tex.width / HW, y + 0.05, base, base + tex.height, draw))
        west = s.to_screen(x, y + 0.05, base + tex.height)
        for u0, u1 in spans:
            things[-1].panes.append({"x": int(west[0]), "y": int(west[1]), "slope": 0.5, "u": u0 - 2, "v": 0, "w": u1 - u0 + 4, "h": height + 5, "kind": "sign"})
    return add


def cylinder(s, cx, cy, r, h, cols, rivets=False, bands=False):
    """Stehender Zylinder. cx, cy = Mitte der Deckfläche, r und h in einfacher Auflösung. Bänder und Nieten bleiben fein."""
    d, m, l = cols
    k = s.res
    rr, hh = r * k, h * k
    for x in range(-rr, rr + 1):
        rel = x / rr
        depth = (1 - rel * rel) ** 0.5 * rr / 2
        c = C[l] if rel < -0.45 else (C[m] if rel < 0.35 else C[d])
        for y in range(int(cy + depth), int(cy + depth + hh)):
            s.put(cx + x, y, c)
        if bands:
            for by in (h // 4, 3 * h // 4):
                s.put(cx + x, int(cy + depth + by * k), C["iron"])
        if rivets and x % (6 * k) == 0:
            for ry in range(4, h, 8):
                s.put(cx + x, int(cy + depth + ry * k), C["brass_l"])
    for y in range(-rr // 2, rr // 2 + 1):
        half = int(rr * max(0.0, 1 - (2 * y / rr) ** 2) ** 0.5) if rr else 0
        for x in range(-half, half + 1):
            s.put(cx + x, cy + y, C[d] if abs(x) > half - 2 or abs(2 * y) > rr - 2 else C[m])


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
        k = s.res
        cx, cy = s.to_screen(x + 0.9, y + 0.9, 62)
        cx, cy = int(cx), int(cy)
        s.rect(cx - 24 * k, cy + 44 * k, 48 * k, 22 * k, C["iron"])
        cylinder(s, cx, cy, 22, 62, ("grey_d", "steel", "ash"), rivets=True, bands=True)
        # Rostfahnen laufen die Kesselwand hinab, fein nur einen Bildpunkt breit
        for _ in range(26):
            bx = WEAR.randint(-20, 20)
            depth = (1 - (bx / 22) ** 2) ** 0.5 * 11
            start = int(cy + depth * k) + WEAR.randint(1, 40) * k
            c = C[WEAR.choice(["rust", "rust_d", "grime", "rust_l"])]
            for by in range(start, min(start + WEAR.randint(4, 26) * k, int(cy + depth * k + 62 * k))):
                s.put(cx + bx * k, by, c)
        dr = ImageDraw.Draw(s.img)
        ink = lambda name, f=1.0: blend(C[name], f) + (255,)  # noqa: E731
        # Steigleiter an der linken Flanke
        for lx in (-19, -15):
            dr.line((cx + lx * k, cy + 12 * k, cx + lx * k, cy + 64 * k), fill=ink("soot"))
        for ry in range(14, 64, 4):
            dr.line((cx - 19 * k, cy + ry * k, cx - 15 * k, cy + ry * k), fill=ink("soot"))
        # Speiserohr mit Handrad
        dr.rectangle((cx + 15 * k, cy + 18 * k, cx + 16 * k + k - 1, cy + 58 * k), fill=ink("brass_d"))
        dr.line((cx + 15 * k, cy + 18 * k, cx + 15 * k, cy + 58 * k), fill=ink("brass"))
        dr.ellipse((cx + 12 * k, cy + 30 * k, cx + 19 * k, cy + 36 * k), outline=ink("wine"))
        dr.line((cx + 12 * k, cy + 33 * k, cx + 19 * k, cy + 33 * k), fill=ink("wine"))
        # Manometer: runde Messinguhr mit Zeiger statt eines weißen Vierecks
        dr.ellipse((cx - 12 * k, cy + 25 * k, cx - 3 * k, cy + 34 * k), fill=ink("brass_d"))
        dr.ellipse((cx - 11 * k, cy + 26 * k, cx - 4 * k, cy + 33 * k), fill=ink("paper", 0.72))
        dr.line((cx - 7.5 * k, cy + 29.5 * k, cx - 5.5 * k, cy + 27.5 * k), fill=ink("wine_d"))
        dr.line((cx - 7.5 * k, cy + 34 * k, cx - 7.5 * k, cy + 40 * k), fill=ink("brass_d"))
        # Feuertür: gusseiserner Rahmen, die Tür steht einen Spalt offen, dahinter die Glut hinter Roststäben
        dr.rectangle((cx + 1 * k, cy + 39 * k, cx + 14 * k + k - 1, cy + 53 * k + k - 1), fill=ink("black"))
        dr.rectangle((cx + 3 * k, cy + 42 * k, cx + 12 * k + k - 1, cy + 51 * k + k - 1), fill=ink("gas_d"))
        dr.rectangle((cx + 5 * k, cy + 45 * k, cx + 10 * k + k - 1, cy + 51 * k + k - 1), fill=ink("gas"))
        for bx in range(3, 13, 2):
            dr.rectangle((cx + bx * k, cy + 42 * k, cx + bx * k + max(0, k - 1), cy + 51 * k + k - 1), fill=ink("black"))
        dr.rectangle((cx + 3 * k, cy + 41 * k, cx + 12 * k + k - 1, cy + 43 * k), fill=ink("soot"))
        dr.rectangle((cx - 1 * k, cy + 40 * k, cx + 1 * k, cy + 52 * k + k - 1), fill=ink("iron"))
        for hy in (42, 50):
            dr.rectangle((cx - 1 * k, cy + hy * k, cx + 2 * k, cy + hy * k + k - 1), fill=ink("brass_d"))
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


WIRE_Z = 70  # Fahrdraht der Straßenbahn, über der Rohrbrücke


def mat_tram_roof(f):
    """Dach des Wagens: geteertes Segeltuch, in der Mitte der Laternenaufbau mit kleinen Lüftungsfenstern."""
    p = f.prism
    k = f.res
    if p.name == "lantern" and f.kind == "x1":
        u = (p.y1 - f.fy) * HW
        glass = (np.floor(u) % 9 >= 2) & (np.floor(u) % 9 <= 6) & (p.top() - f.z > 1.5) & (f.z - p.z0 > 1)
        lit = R.hash01(np.floor(u / 9), 3, 7) < 0.6
        col = np.where(glass[:, None], np.where(lit[:, None], _c("gas_d")[None, :], _c("glass_d")[None, :]), _c("wine_d")[None, :])
        return col, glass & lit
    if f.kind != "plane":
        return _c("wine_d")[None, :] * np.ones((len(f.z), 1))
    v = (f.fy - p.y0) * HW * k
    col = np.array((104, 104, 112), np.float64)[None, :] * (0.8 + 0.3 * R.noise(f.fx * 12, f.fy * 6, 95))[:, None]
    col = col * np.where(np.floor(v) % (16 * k) == 0, 0.7, 1.0)[:, None]
    return col * (0.9 + 0.2 * R.hash01(np.floor(f.fx * 64), np.floor(f.fy * 64), 96))[:, None]


def tram(x, y, length, s, things):
    made = []
    h = 40
    roof = [R.mansard(x, y, x + 0.8, y + length, h, steep=3, steep_k=26, upper=1.5, upper_k=6, material=mat_tram_roof),
            R.block(x + 0.3, y + 0.5, x + 0.52, y + length - 0.5, h, h + 8, mat_tram_roof, "lantern")]

    def textures():
        w = int(length * HW)
        tex = canvas(w, h, C["wine"])
        d = draw_on(tex)
        # Zweifarbig: oben creme mit dem Fensterband, unten weinrot
        d.rectangle((0, 4, w, 24), fill=blend(C["cream"], 0.86))
        d.rectangle((0, 0, w, 3), fill=C["soot"])
        d.fine_line((0, 3, w, 3), fill=C["brass_d"])
        d.rectangle((0, 31, w, h), fill=C["soot"])
        d.rectangle((0, 24, w, 25), fill=C["brass_d"])
        d.fine_line((0, 24, w, 24), fill=C["brass"])
        windows = []
        for u in range(5, w - 12, 15):
            lit = WEAR.random() < 0.45
            d.rectangle((u - 1, 7, u + 11, 23), fill=C["wood_d"])
            d.rectangle((u, 8, u + 10, 22), fill=C["gas_d"] if lit else C["glass_d"])
            d.fine_line((u, 12, u + 10, 12), fill=C["wood_d"])
            windows.append((u, lit))
        rust_patches(d, 0, 26, w - 1, 30)
        # Fahrgäste als Schattenriss, mit Hut, vor den hellen Scheiben
        for u, lit in windows:
            if lit and DETAIL.random() < 0.7:
                px = u + DETAIL.randint(2, 6)
                d.rectangle((px, 15, px + 3, 18), fill=C["black"])
                d.rectangle((px - 1, 19, px + 4, 22), fill=C["black"])
                d.fine_line((px - 1, 15, px + 4, 15), fill=C["black"])
            elif not lit:
                d.hd_only_line((u + 1, 9, u + 3, 9), fill=C["glass"])
        # Türen an beiden Enden mit Griffstange, davor das Trittbrett
        for u in (0, w - 5):
            d.rectangle((u, 6, u + 4, 30), fill=C["wine_d"])
            d.rectangle((u + 1, 8, u + 3, 18), fill=C["glass_d"])
            d.fine_line((u + 2 if u else u + 4, 14, u + 2 if u else u + 4, 28), fill=C["brass"])
            d.rectangle((u - 1 if u else 0, 31, u + 6, 32), fill=C["wood"])
            d.shade((u - 1 if u else 0, 33, u + 6, 34), 0.5)
        # Linienschild und Wagennummer
        d.rectangle((w // 2 - 13, 26, w // 2 + 13, 30), fill=C["black"])
        paint_text(tex, w // 2 - 11, 26.5, "LINIE 7", 4, [(C["brass_l"], 0.9, 0, 0, 1.0, 0)], gap=1.4, seed=3)
        # Fahrgestell: zwei Drehgestelle mit Rädern
        for wx in (20, w - 24):
            d.rectangle((wx - 8, 31, wx + 12, 35), fill=C["iron"])
            for off in (-4, 8):
                d.ellipse((wx + off - 4, 32, wx + off + 4, 40), fill=C["black"])
                d.ellipse((wx + off - 1, 35, wx + off + 1, 37), fill=C["steel"])
        d.shade((0, 4, w, 5), 0.7)
        front = canvas(int(0.8 * HW), h, C["wine"])
        fd = draw_on(front)
        fw = front.width
        fd.rectangle((0, 4, fw, 24), fill=blend(C["cream"], 0.86))
        fd.rectangle((0, 0, fw, 3), fill=C["soot"])
        # Zielschild über der Scheibe
        fd.rectangle((6, 4, fw - 7, 8), fill=C["black"])
        paint_text(front, fw / 2 - 1.3, 4.6, "7", 3.4, [(C["gas"], 0.9, 0, 0, 1.0, 0)], seed=4)
        fd.rectangle((2, 9, fw - 3, 21), fill=C["wood_d"])
        fd.rectangle((3, 10, fw - 4, 20), fill=C["glass"])
        fd.line((fw // 2, 10, fw // 2, 20), fill=C["wood_d"])
        fd.hd_only_line((5, 12, 9, 18), fill=blend(C["glass"], 1.25))
        fd.rectangle((0, 24, fw, 25), fill=C["brass_d"])
        # Scheinwerfer in Messingfassung
        fd.ellipse((fw // 2 - 4, 25, fw // 2 + 3, 31), fill=C["brass_d"])
        fd.ellipse((fw // 2 - 3, 26, fw // 2 + 2, 30), fill=C["gas"])
        fd.rectangle((0, 32, fw, h), fill=C["soot"])
        fd.rectangle((1, 32, fw - 2, 33), fill=C["iron"])
        fd.rectangle((fw // 2 - 2, 34, fw // 2 + 1, 36), fill=C["iron"])
        made.extend((front, tex))

    def draw():
        if not made:
            textures()
        box(s, x, y, x + 0.8, y + length, 0, h, made[0], made[1], lambda a, b, w2, d2: None)
        R.render(s, roof)
        # Stromabnehmer: Schere aus Rohren, oben das Schleifstück am Fahrdraht
        k = s.res
        dr = ImageDraw.Draw(s.img)
        ink = C["black"] + (255,)
        my = y + length * 0.42
        base_a, base_b = s.to_screen(x + 0.41, my - 0.35, h + 8), s.to_screen(x + 0.41, my + 0.35, h + 8)
        knee_a, knee_b = s.to_screen(x + 0.41, my - 0.55, h + 18), s.to_screen(x + 0.41, my + 0.55, h + 18)
        head = s.to_screen(x + 0.41, my, WIRE_Z - 1)
        for a, b in ((base_a, knee_b), (base_b, knee_a), (knee_a, head), (knee_b, head)):
            dr.line((a[0], a[1], b[0], b[1]), fill=ink, width=max(1, k // 2 + (k > 1)))
        bar_a, bar_b = s.to_screen(x + 0.2, my, WIRE_Z - 1), s.to_screen(x + 0.62, my, WIRE_Z - 1)
        dr.line((bar_a[0], bar_a[1], bar_b[0], bar_b[1]), fill=C["brass_d"] + (255,), width=k)
    things.append(Thing(x, y, x + 0.8, y + length, 0, 40, draw, solid=True, opaque=True))
    things[-1].prisms = roof


def overhead_wire(x, spans, s, things):
    """Fahrdraht über dem Gleis, an Querdrähten zwischen den Häusern aufgehängt. spans: (y, x links, x rechts, Höhe an der Wand)."""
    def draw():
        dr = ImageDraw.Draw(s.img)
        k = s.res
        ink = C["black"] + (255,)
        stops = [0.0] + [sp[0] for sp in spans] + [20.0]
        for a, b in zip(stops, stops[1:]):
            pts = []
            for i in range(25):
                t = i / 24
                pts.append(s.to_screen(x, a + (b - a) * t, WIRE_Z - math.sin(math.pi * t) * (b - a) * 0.45))
            dr.line([(px, py) for px, py in pts], fill=ink, width=1)
        for yy, xl, xr, zw in spans:
            pts = []
            for i in range(25):
                t = i / 24
                xx = xl + (xr - xl) * t
                near = 1 - abs(xx - x) / max(x - xl, xr - x)
                pts.append(s.to_screen(xx, yy, zw - (zw - WIRE_Z - 2) * near ** 0.8))
            dr.line([(px, py) for px, py in pts], fill=C["soot"] + (255,), width=1)
            for xx, zz in ((xl, zw),):  # rechts sitzt die Rosette an einer Wand, die man von hier nicht sieht
                px, py = s.to_screen(xx, yy, zz)
                dr.rectangle((px - k, py - k, px + k, py + k), fill=C["paper"] + (255,))
            px, py = s.to_screen(x, yy, WIRE_Z + 2)
            dr.rectangle((px - k, py, px + k, py + 2 * k), fill=C["paper"] + (255,))
    # Ohne Breite: Für alle, die aus dem Quader eine Wand lesen, steht hier nichts.
    things.append(Thing(x, 0.0, x, 20.0, WIRE_Z - 2, WIRE_Z + 10, draw))


def getaway_truck(x, y, s, things):
    """Der Fluchtwagen: kurzer Diesel-Lieferwagen, eigener Entwurf."""
    made = []

    def draw():
        if made:
            box(s, x, y, x + 0.8, y + 1.4, 0, 34, made[0], made[1], lambda a, b, w, dd: C["navy_l"])
            return
        side = canvas(int(1.4 * HW), 34, C["navy"])
        d = draw_on(side)
        d.rectangle((0, 0, side.width, 2), fill=C["navy_l"])
        d.rectangle((0, 25, side.width, 33), fill=C["soot"])
        d.rectangle((0, 22, side.width, 23), fill=C["brass"])
        d.rectangle((4, 6, 16, 16), fill=C["glass"])
        rust_patches(d, 0, 18, side.width - 1, 21)
        for wx in (8, side.width - 12):
            d.ellipse((wx - 4, 25, wx + 4, 33), fill=C["black"])
            d.rectangle((wx - 1, 28, wx + 1, 30), fill=C["brass"])
        front = canvas(int(0.8 * HW), 34, C["navy"])
        fd = draw_on(front)
        fd.rectangle((0, 0, front.width, 2), fill=C["navy_l"])
        fd.rectangle((3, 6, front.width - 4, 15), fill=C["glass"])
        fd.rectangle((0, 22, front.width, 33), fill=C["soot"])
        fd.rectangle((2, 23, 5, 26), fill=C["gas"])
        fd.rectangle((front.width - 6, 23, front.width - 3, 26), fill=C["gas"])
        made.extend((front, side))
        box(s, x, y, x + 0.8, y + 1.4, 0, 34, front, side, lambda a, b, w, dd: C["navy_l"])
    things.append(Thing(x, y, x + 0.8, y + 1.4, 0, 34, draw, solid=True, opaque=False))


def lamp(x, y, s, things, glows):
    glows.append((x, y))
    AMBIENT["lights"].append([x, y, 55])

    def draw():
        k = s.res
        sx, sy = s.to_screen(x, y)
        sx, sy = int(sx), int(sy)
        dr = ImageDraw.Draw(s.img)

        def part(x0, y0, x1, y1, c):
            a, b = int(round(x0 * k)), int(round(x1 * k))
            lo, hi = int(round(y0 * k)), int(round(y1 * k))
            dr.rectangle((sx + a, sy - max(hi, lo + 1), sx + max(b, a + 1) - 1, sy - lo - 1), fill=tuple(c) + (255,))

        # Sockel aus Gusseisen, gestuft und kanneliert
        part(-4, 0, 4, 2, C["soot"])
        part(-3, 2, 3, 6, C["iron"])
        part(-2.5, 6, 2.5, 7, C["soot"])
        part(-2, 7, 2, 12, blend(C["iron"], 1.1))
        part(-2.5, 12, 2.5, 13, C["soot"])
        for fx in (-1.5, 0.5):
            part(fx, 2.5, fx + 0.5, 5.5, C["soot"])
        # Mast, links eine Lichtkante
        part(-1, 13, 1, 49, C["iron"])
        part(-1, 13, -0.5, 49, C["steel"])
        part(-1.5, 30, 1.5, 31, C["soot"])
        # Leitersprosse für den Laternenanzünder
        part(-7, 45.5, 7, 46, C["iron"])
        part(-7.5, 45, -6.5, 46.5, C["soot"])
        part(6.5, 45, 7.5, 46.5, C["soot"])
        # Kelch unter der Laterne
        part(-1.5, 49, 1.5, 50, C["brass_d"])
        part(-2.5, 50, 2.5, 51, C["brass_d"])
        # Laterne: Glas, das sich nach oben weitet, in einem Rahmen aus Messing
        rows = 9 * k
        for i in range(rows):
            half = 3 + 1.8 * i / rows
            yy = 51 + i / k
            part(-half, yy, half, yy + 1 / k, C["gas_d"])
            part(-half + 1, yy, half - 1, yy + 1 / k, C["gas"])
            part(-half - 0.5, yy, -half + 0.5, yy + 1 / k, C["brass_d"])
            part(half - 0.5, yy, half + 0.5, yy + 1 / k, C["brass_d"])
        part(-0.25, 51, 0.25, 60, C["brass_d"])
        part(-3.5, 51, 3.5, 51.5, C["brass_d"])
        # Dach mit Abzug und Knauf
        part(-6, 60, 6, 61, C["soot"])
        part(-5, 61, 5, 62, C["black"])
        part(-3, 62, 3, 63, C["soot"])
        part(-1.5, 63, 1.5, 64.5, C["soot"])
        part(-0.5, 64.5, 0.5, 66, C["brass_d"])
    things.append(Thing(x - 0.05, y - 0.05, x + 0.05, y + 0.05, 0, 60, draw))
    things[-1].kind = "lamp"


# ---------- Kleinkram: steht dicht an Wänden oder auf ohnehin gesperrten Feldern und sperrt selbst nichts ----------
def mat_plain(color, top=None, bands=0):
    def material(f):
        if f.kind == "plane":
            c = np.array(top or blend(color, 1.1), np.float64)
        else:
            c = np.array(color, np.float64)
        col = c[None, :] * (0.85 + 0.3 * R.noise(f.fx * 25, f.fy * 25 + f.z / 3, 97))[:, None]
        if bands and f.kind != "plane":
            col = col * np.where(np.floor((f.z - f.prism.z0) * f.res) % (bands * f.res) == 0, 0.6, 1.0)[:, None]
        return col
    return material


def prop_box(x, y, w, d, h, material, s, things, z0=0):
    body = R.block(x, y, x + w, y + d, z0, z0 + h, material)

    def draw():
        R.render(s, [body])
    things.append(Thing(x, y, x + w, y + d, z0, z0 + h, draw))


def ash_can(x, y, s, things, lid=True):
    """Mülltonne aus verzinktem Blech mit Deckel."""
    def draw():
        k = s.res
        cx, cy = s.to_screen(x + 0.13, y + 0.13, 13)
        cx, cy = int(cx), int(cy)
        cylinder(s, cx, cy, 5, 13, ("grey_d", "grey", "grey_l"), bands=True)
        if lid:
            dr = ImageDraw.Draw(s.img)
            dr.ellipse((cx - 6 * k, cy - 4 * k, cx + 6 * k, cy + 2 * k), fill=C["grey_d"] + (255,))
            dr.ellipse((cx - 5 * k, cy - 4 * k, cx + 5 * k, cy + k), fill=C["grey_l"] + (255,))
            dr.rectangle((cx - k, cy - 4 * k, cx + k, cy - 3 * k), fill=C["iron"] + (255,))
    things.append(Thing(x, y, x + 0.26, y + 0.26, 0, 15, draw))


def barrel(x, y, s, things, cols=("rust_d", "rust", "rust_l")):
    def draw():
        cx, cy = s.to_screen(x + 0.15, y + 0.15, 16)
        cylinder(s, int(cx), int(cy), 6, 16, cols, bands=True)
    things.append(Thing(x, y, x + 0.3, y + 0.3, 0, 16, draw))


def hydrant(x, y, s, things):
    """Hydrant: kurzer roter Gusskörper mit Haube und seitlichem Abgang."""
    def draw():
        k = s.res
        sx, sy = s.to_screen(x, y)
        sx, sy = int(sx), int(sy)
        dr = ImageDraw.Draw(s.img)
        p = lambda x0, y0, x1, y1, c: dr.rectangle((sx + int(x0 * k), sy - max(int(y1 * k), int(y0 * k) + 1), sx + max(int(x1 * k), int(x0 * k) + 1) - 1, sy - int(y0 * k) - 1), fill=tuple(c) + (255,))  # noqa: E731
        p(-3, 0, 3, 1.5, C["soot"])
        p(-2, 1.5, 2, 9, C["wine"])
        p(-2, 1.5, -1, 9, C["wine_l"])
        p(1, 1.5, 2, 9, C["wine_d"])
        p(-2.5, 9, 2.5, 10, C["wine_d"])
        p(-1.5, 10, 1.5, 11.5, C["wine"])
        p(-0.5, 11.5, 0.5, 12.5, C["brass_d"])
        p(2, 5, 3.5, 7, C["brass_d"])
    things.append(Thing(x - 0.06, y - 0.06, x + 0.06, y + 0.06, 0, 12, draw))


def flat_prop(x, y, tex, s, things, along="x", kind=""):
    """Flaches Ding, das an einer Wand lehnt oder hängt: als Bild in die Wandebene gekippt."""
    def draw():
        if along == "x":
            west = s.to_screen(x, y, tex.height)
            shear(s, tex, int(west[0]), int(west[1]), 0.5, 1.0)
        else:
            south = s.to_screen(x, y + tex.width / HW, tex.height)
            shear(s, tex, int(south[0]), int(south[1]), -0.5, 0.8)
    if along == "x":
        things.append(Thing(x, y - 0.03, x + tex.width / HW, y + 0.03, 0, tex.height, draw))
    else:
        things.append(Thing(x - 0.03, y, x + 0.03, y + tex.width / HW, 0, tex.height, draw))
    things[-1].kind = kind


def bicycle_tex(color="soot"):
    """Fahrrad von der Seite, aus dünnen Linien: in doppelter Auflösung sind Speichen und Rahmen zu erkennen."""
    tex = canvas(30, 19, (0, 0, 0, 0))
    for im, k in ((tex, 1), (tex.info.get("hd"), HD)):
        if im is None:
            continue
        dr = ImageDraw.Draw(im)
        ink = C["black"] + (255,)
        frame = C[color] + (255,)
        for wx in (7, 22):
            dr.ellipse(((wx - 6) * k, 6 * k, (wx + 6) * k, 18 * k), outline=ink, width=max(1, k - 1))
            if k > 1:
                for ang in range(0, 180, 45):
                    ax, ay = math.cos(math.radians(ang)) * 5.5 * k, math.sin(math.radians(ang)) * 5.5 * k
                    dr.line((wx * k - ax, 12 * k - ay, wx * k + ax, 12 * k + ay), fill=C["iron"] + (255,))
        lines = [((7, 12), (12, 5)), ((12, 5), (20, 5)), ((20, 5), (22, 12)), ((12, 5), (15, 12)), ((15, 12), (7, 12)), ((15, 12), (20, 5)),
                 ((20, 5), (19, 2)), ((12, 5), (11, 3))]
        for (ax, ay), (bx, by) in lines:
            dr.line((ax * k, ay * k, bx * k, by * k), fill=frame, width=max(1, k - 1))
        dr.line((17 * k, 2 * k, 21 * k, 2 * k), fill=ink, width=k)
        dr.line((9 * k, 3 * k, 13 * k, 3 * k), fill=C["wood_d"] + (255,), width=k)
    return tex


def news_stand_tex():
    """Zeitungsaushang: Brett mit den Blättern des Tages, darunter eine Kiste mit dem Stapel."""
    tex = canvas(34, 30, (0, 0, 0, 0))
    d = draw_on(tex)
    d.rectangle((0, 0, 33, 19), fill=C["wood_d"])
    d.rectangle((1, 1, 32, 5), fill=C["wine_d"])
    paint_text(tex, 3, 1.6, "ZEITUNG", 3.4, [(C["cream"], 0.8, 0, 0, 1.0, 0)], gap=1.3, seed=9)
    for i, u in enumerate((2, 12, 22)):
        d.rectangle((u, 7, u + 8, 18), fill=blend(C["paper"], 0.82 + 0.06 * i))
        d.rectangle((u + 1, 8, u + 7, 9), fill=C["black"])
        for v in range(11, 18, 2):
            d.fine_line((u + 1, v, u + 7 - (v + i) % 3, v), fill=C["ash"])
    d.rectangle((3, 20, 4, 29), fill=C["wood_d"])
    d.rectangle((29, 20, 30, 29), fill=C["wood_d"])
    d.rectangle((8, 22, 26, 29), fill=C["wood"])
    d.rectangle((8, 21, 26, 22), fill=C["paper"])
    d.fine_line((8, 26, 26, 26), fill=C["wood_d"])
    return tex


def street_props(s, things):
    """Kleinkram der Straße. Alles hier steht höchstens ein Viertelfeld vor einer Wand oder auf Feldern, die Kessel und
    Kisten ohnehin sperren: Die Figuren laufen von Feldmitte zu Feldmitte und kommen daran vorbei."""
    crate_mat = mat_plain(C["wood"], C["wood"], 4)
    first = len(things)
    # Mülltonnen neben den Haustüren
    ash_can(6.03, 2.55, s, things)
    ash_can(6.03, 2.85, s, things, lid=False)
    ash_can(3.9, 17.03, s, things)
    ash_can(6.03, 14.6, s, things)
    # Fässer und Kisten im Fabrikhof, unterhalb der Kessel
    barrel(13.3, 8.32, s, things)
    barrel(13.65, 8.5, s, things, ("navy_d", "navy", "navy_l"))
    barrel(15.2, 8.35, s, things)
    prop_box(14.3, 8.4, 0.5, 0.4, 14, crate_mat, s, things)
    prop_box(14.38, 8.46, 0.34, 0.28, 9, crate_mat, s, things, z0=14)
    prop_box(15.6, 8.45, 0.4, 0.4, 11, crate_mat, s, things)
    # Kisten an der Lagerwand
    prop_box(17.02, 16.3, 0.24, 0.5, 12, crate_mat, s, things)
    prop_box(17.02, 16.85, 0.24, 0.36, 8, crate_mat, s, things)
    # Fahrräder lehnen an der Wand
    flat_prop(4.25, 6.06, bicycle_tex(), s, things, kind="open")
    flat_prop(6.06, 12.5, bicycle_tex("wine_d"), s, things, along="y", kind="open")
    flat_prop(14.9, 18.06, bicycle_tex("navy_d"), s, things, kind="open")
    # Zeitungsaushang am Mietshaus, zur Straße mit der Bahn hin
    flat_prop(6.06, 4.1, news_stand_tex(), s, things, along="y")
    # Hydranten am Bordstein
    hydrant(7.76, 12.3, s, things)
    hydrant(10.26, 6.72, s, things)
    hydrant(11.74, 8.74, s, things)
    for t in things[first:]:
        t.kind = t.kind or "prop"


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
    made = []

    def draw():
        if not made:
            made.append(plain_tex(12, 12, "wood", "brass_d"))
            draw_on(made[0]).rectangle((5, 0, 6, 12), fill=C["wood_d"])
        box(s, x, y, x + 0.4, y + 0.4, 0, 12, made[0], made[0], lambda a, b, w, d: C["wood"])
    things.append(Thing(x, y, x + 0.4, y + 0.4, 0, 12, draw, solid=True, opaque=False))


# ---------- Boden ----------
def scatter_dirt(s, solids):
    """Pfützen, Ölflecken, Müll und Unkraut, nur auf den Boden gemalt und ohne Einfluss auf die Spiellogik.

    Gibt die Flecken als Liste zurück, damit paint_dirt sie in jeder Auflösung gleich malen kann.
    """
    def free(x, y):
        return 0 <= x < 20 and 0 <= y < 20 and not any(a <= x < c and b <= y < d for a, b, c, d in solids)

    ops = []
    for _ in range(34):  # Pfützen
        x, y = WEAR.uniform(0, 20), WEAR.uniform(0, 20)
        if free(x, y):
            rx = WEAR.randint(10, 30)
            ops.append(("blot", x, y, rx, rx * 0.5, C["puddle"], 0.8, WEAR.randrange(1 << 16)))
            px, py = s.to_screen(x, y)
            AMBIENT["puddles"].append([int(px), int(py), rx])
    for _ in range(36):  # Ölflecken
        x, y = WEAR.uniform(0, 20), WEAR.uniform(0, 20)
        if free(x, y):
            rx = WEAR.randint(5, 13)
            ops.append(("blot", x, y, rx, rx * 0.5, C["black"], 0.5, WEAR.randrange(1 << 16)))
    for _ in range(46):  # Papier, Scherben, Dreck: vom Wind in kleine Haufen getrieben
        x, y = LITTER.uniform(0, 20), LITTER.uniform(0, 20)
        for k in range(LITTER.randint(3, 8)):
            px, py = x + LITTER.uniform(-0.22, 0.22), y + LITTER.uniform(-0.22, 0.22)
            if free(px, py):
                c = C[LITTER.choice(["ash", "plaster", "wood_d", "grime", "rust", "glass_d"])]
                ops.append(("rect", px, py, 0, 0, LITTER.randint(1, 3), LITTER.randint(1, 2), c))
    for _ in range(70):  # Unkraut in den Fugen
        x, y = WEAR.uniform(0, 20), WEAR.uniform(0, 20)
        if free(x, y):
            for k in range(WEAR.randint(2, 4)):
                dx, dy = WEAR.randint(-2, 2), -WEAR.randint(0, 3)
                ops.append(("weed", x, y, dx, dy, 1, WEAR.randint(1, 3), C["moss"]))
    # Müllhaufen an den Hauswänden
    for a, b, c, d in solids:
        for _ in range(int((c - a) + (d - b))):
            x, y = (WEAR.uniform(a, c), d + WEAR.uniform(0.03, 0.3)) if WEAR.random() < 0.5 else (c + WEAR.uniform(0.03, 0.3), WEAR.uniform(b, d))
            if free(x, y):
                for k in range(WEAR.randint(2, 7)):
                    dx, dy, w, h = WEAR.randint(-6, 6), WEAR.randint(-3, 3), WEAR.randint(1, 3), WEAR.randint(1, 2)
                    ops.append(("rect", x, y, dx, dy, w, h, C[WEAR.choice(["wood_d", "grime", "dirt", "plaster", "soot", "rust_d"])]))
    return ops


def paint_dirt(s, ops):
    r = s.res
    for op in ops:
        if op[0] == "blot":
            _, x, y, rx, ry, color, strength, seed = op
            cx, cy = s.to_screen(x, y)
            rx, ry = rx * r, ry * r
            for j in range(int(cy - ry), int(cy + ry) + 1):
                for i in range(int(cx - rx), int(cx + rx) + 1):
                    d = (((i - cx) / rx) ** 2 + ((j - cy) / ry) ** 2) ** 0.5
                    if d + noise(i / r / 5, j / r / 3, seed) * 0.6 < 1 and 0 <= i < s.w and 0 <= j < s.h:
                        s.px[i, j] = mix(s.px[i, j][:3], color, strength) + (255,)
        else:
            kind, x, y, dx, dy, w, h, c = op
            sx, sy = s.to_screen(x, y)
            # Halme bleiben fein einen Bildpunkt breit, alles andere wächst mit der Auflösung.
            s.rect(sx + dx * r, sy + dy * r, w if kind == "weed" else w * r, h * r, c)


def manhole(s, mx, my):
    """Kanaldeckel aus Gusseisen: Rahmen, Fuge, Waffelmuster und zwei Löcher für den Haken."""
    r = s.res
    cx, cy = s.to_screen(mx, my)
    for j in range(-5 * r, 5 * r + 1):
        for i in range(-10 * r, 10 * r + 1):
            u, v = i / (10 * r), j / (5 * r)
            d = math.hypot(u, v)
            if d >= 1:
                continue
            if d > 0.84:
                c = blend(C["iron"], 0.9 + 0.3 * (v < 0))
            elif d > 0.74:
                c = C["black"]
            elif math.hypot(abs(u) - 0.45, v) < 0.09:
                c = C["black"]
            else:
                waffle = (math.floor((u + v) * 4.5) + math.floor((u - v) * 4.5)) % 2
                c = C["iron"] if waffle else C["soot"]
            s.put(int(cx) + i, int(cy) + j, c)


RAILS = (8.42, 8.58, 9.42, 9.58)
# Straßenabläufe im Rinnstein: Feld x, y und die Richtung des Bordsteins
GULLIES = ((8.11, 3.4, "y"), (8.11, 13.3, "y"), (9.89, 16.4, "y"), (9.89, 7.6, "y"), (4.3, 9.15, "x"), (17.3, 9.15, "x"), (14.5, 11.85, "x"), (6.9, 11.85, "x"))
# Kellerroste und Kohlenklappen dicht an der Hauswand: x0, y0, x1, y1
GRATES = ((2.2, 6.03, 2.7, 6.2), (4.4, 6.03, 4.9, 6.2), (6.03, 2.6, 6.2, 3.1), (2.0, 17.03, 2.5, 17.2), (6.03, 13.2, 6.2, 13.7), (6.03, 15.6, 6.2, 16.1),
          (13.0, 18.03, 13.5, 18.2), (17.03, 14.2, 17.2, 14.7), (18.03, 2.2, 18.2, 2.7))


def hash01_np(ix, iy, seed=0):
    h = (ix.astype(np.int64) * 374761393 + iy.astype(np.int64) * 668265263 + seed * 2147483647) & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    return ((h ^ (h >> 16)) & 0xffff) / 65535


def noise_np(x, y, seed=0):
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    top = hash01_np(ix, iy, seed) * (1 - fx) + hash01_np(ix + 1, iy, seed) * fx
    bottom = hash01_np(ix, iy + 1, seed) * (1 - fx) + hash01_np(ix + 1, iy + 1, seed) * fx
    return top * (1 - fy) + bottom * fy


def ground_np(fx, fy, solids=()):
    """Farbe des Bodens für ganze Felder auf einmal, Ergebnis als (…, 3) uint8.

    Fahrbahn aus Reihenpflaster mit Rinnstein und Schienen, Gehwege aus Platten zwischen zwei Streifen Mosaikpflaster,
    Bordsteine, Höfe aus Teer. Um jedes Haus (solids) läuft ein gepflasterter Streifen, direkt an der Wand ein Traufstreifen.
    """
    col = lambda name: np.array(C[name], np.float64)  # noqa: E731

    def blend_np(c, f):
        return np.clip(np.floor(c * f[..., None]), 0, 255)

    def mix_np(a, b, f):
        return np.floor(a + (b - a) * f[..., None])

    def put(base, mask, c):
        return np.where(mask[..., None], c, base)

    shape = fx.shape
    full = lambda v: np.full(shape, v)  # noqa: E731
    road_y = (9 <= fy) & (fy < 12)
    road_x = (8 <= fx) & (fx < 10)
    road = road_x | road_y
    # Pflaster
    along, across = np.where(road_x, fy, fx), np.where(road_x, fx, fy)
    row = np.floor(along * COBBLE_ROWS)
    shift = across * COBBLE_STONES + 0.5 * (row % 2)
    stone = np.floor(shift)
    joint = ((along * COBBLE_ROWS) % 1 < 0.17) | (shift % 1 < 0.1)
    sett = blend_np(mix_np(col("cobble"), col("cobble_l"), hash01_np(row, stone, 6) * 0.5), 0.94 + 0.1 * hash01_np(row, stone, 3))
    c = np.where(joint[..., None], col("cobble_d"), sett)
    n = noise_np(fx * 1.6, fy * 1.6, 11)
    edge_y = np.minimum(fy - 9, 12 - fy)
    edge_x = np.minimum(fx - 8, 10 - fx)
    gutter = (road_y & ~road_x & (edge_y < 0.3)) | (road_x & ~road_y & (edge_x < 0.22))
    paved = blend_np(c, 0.68 + 0.4 * n)
    broken = (noise_np(fx * 2.3, fy * 2.3, 41) > 0.8) & (hash01_np(row, stone, 5) < 0.5)
    paved = put(paved, broken, col("dirt"))
    # Rinnstein: drei Reihen Steine längs zum Bordstein, dunkler, mit stehendem Wasser dicht an der Kante
    reach = np.where(road_x & ~road_y, edge_x / 0.22, edge_y / 0.3)
    lane = np.floor(reach * 3)
    g_joint = ((reach * 3) % 1 < 0.16) | ((along * 2.4 + 0.5 * (lane % 2)) % 1 < 0.07)
    g_stone = hash01_np(np.floor(along * 2.4 + 0.5 * (lane % 2)), lane + np.floor(across), 13)
    g = blend_np(np.where(g_joint[..., None], col("cobble_d") * 0.8, mix_np(col("cobble_d"), col("cobble"), g_stone * 0.7)), 0.58 + 0.34 * n)
    film = noise_np(along * 1.1, np.floor(across) * 3.0, 14) + 0.25 * noise_np(along * 5, across * 9, 15)
    wet = (reach < 0.5 + 0.5 * noise_np(along * 3, across, 16)) & (film > 0.58)
    g = put(g, wet, mix_np(g, col("puddle"), full(0.72)))
    g = put(g, wet & (hash01_np(np.floor(along * 60), np.floor(across * 60), 17) > 0.965), mix_np(col("puddle"), col("glass"), full(0.5)))
    paved = put(paved, gutter, g)
    for x, y, way in GULLIES:
        da, dc = (fy - y, fx - x) if way == "y" else (fx - x, fy - y)
        frame = (np.abs(da) < 0.19) & (np.abs(dc) < 0.1)
        slots = (np.abs(da) < 0.16) & (np.abs(dc) < 0.07)
        paved = put(paved, frame, blend_np(col("iron"), 0.75 + 0.3 * n))
        paved = put(paved, slots & ((da * 24) % 1 < 0.5), col("black"))
    # Schienen: Rillenschiene, der Kopf blank gefahren. Unter den Laternen fängt er das Licht.
    for rail in reversed(RAILS):
        flank = road_x & (np.abs(fx - rail) < 0.07)
        paved = put(paved, flank, blend_np(paved, full(0.84)))
        paved = put(paved, road_x & (np.abs(fx - rail - 0.03) < 0.012), col("black"))
        head = road_x & (np.abs(fx - rail) < 0.018)
        rusty = noise_np(fy * 4, full(rail), 9) > 0.8
        metal = np.where(rusty[..., None], col("rust_d"), mix_np(col("steel"), col("ash"), full(0.55)))
        paved = put(paved, head, metal)
        paved = put(paved, road_x & (np.abs(fx - rail + 0.006) < 0.005) & ~rusty, np.array((214, 216, 220.0)))
    # Gehweg
    walk = ~road & (((6 <= fx) & (fx < 12)) | ((7 <= fy) & (fy < 14)))
    n = noise_np(fx * 1.4, fy * 1.4, 21)
    cx, cy = np.floor(fx * 2), np.floor(fy * 2)
    ax, ay = (fx * 2) % 1, (fy * 2) % 1
    roll = hash01_np(cx, cy, 7)
    edge = (ax < 0.06) | (ay < 0.06)
    c = np.where(edge[..., None], col("stone_d"), blend_np(col("stone"), 0.91 + 0.09 * hash01_np(cx, cy, 8)))
    slope = 0.35 + 1.3 * hash01_np(cx, cy, 9)
    line = ay - slope * ax - (hash01_np(cx, cy, 10) - 0.4) + (noise_np(fx * 16, fy * 16, 23) - 0.5) * 0.35
    crack = (roll < 0.14) & (np.abs(line) < 0.04)
    c = put(c, crack, mix_np(c, col("grime"), full(0.65)))
    slabs = blend_np(c, 0.56 + 0.42 * n)
    slabs = put(slabs, (roll < 0.03) & (noise_np(fx * 9, fy * 9, 22) > 0.35), col("dirt"))
    # Mosaikpflaster: kleine Steine, unruhig verlegt, einzelne fehlen
    mu, mv = fx * 9 + 0.35 * np.sin(fy * 5), fy * 9 + 0.35 * np.sin(fx * 5)
    m_stone = hash01_np(np.floor(mu), np.floor(mv), 51)
    m_joint = (mu % 1 < 0.16) | (mv % 1 < 0.16)
    mosaic = mix_np(np.array((112, 106, 100.0)), np.array((146, 136, 122.0)), m_stone)
    mosaic = put(mosaic, m_joint, np.array((70, 66, 66.0)))
    mosaic = put(mosaic, (m_stone > 0.95) & (noise_np(fx * 3, fy * 3, 52) > 0.5), col("dirt"))
    mosaic = blend_np(mosaic, 0.58 + 0.42 * n)
    # Abstand zum nächsten Haus
    near = full(9.0)
    for x0, y0, x1, y1 in solids:
        gap = np.hypot(np.maximum.reduce([x0 - fx, np.zeros(shape), fx - x1]), np.maximum.reduce([y0 - fy, np.zeros(shape), fy - y1]))
        near = np.minimum(near, gap)
    kerb_gap = np.minimum.reduce([np.abs(fx - 7.88), np.abs(fx - 10.12), np.abs(fy - 8.88), np.abs(fy - 12.12)])
    slabs = put(slabs, (near < 0.36) | (kerb_gap < 0.3), mosaic)
    # Bordstein: Granit mit Stoßfugen. Zur Kamera hin ist die Stirn zu sehen, sonst nur die helle Oberkante.
    kerb = ((fx >= 7.88) & (fx < 8)) | ((10 <= fx) & (fx < 10.12)) | ((8.88 <= fy) & (fy < 9)) | ((12 <= fy) & (fy < 12.12))
    k_along = np.where(((fx >= 7.88) & (fx < 8)) | ((10 <= fx) & (fx < 10.12)), fy, fx)
    k = blend_np(col("stone"), 0.7 + 0.4 * n)
    k = put(k, (k_along * 1.7) % 1 < 0.03, blend_np(col("stone_d"), 0.5 + 0.3 * n))
    face = ((fx >= 7.965) & (fx < 8)) | ((fy >= 8.965) & (fy < 9))
    k = put(k, face, blend_np(col("stone_d"), 0.4 + 0.25 * n))
    lip = ((fx >= 7.94) & (fx < 7.965)) | ((fy >= 8.94) & (fy < 8.965)) | ((10 <= fx) & (fx < 10.025)) | ((12 <= fy) & (fy < 12.025))
    k = put(k, lip, blend_np(col("stone_l"), 0.8 + 0.3 * n))
    slabs = put(slabs, kerb, k)
    # Hof
    n = noise_np(fx * 1.8, fy * 1.8, 31)
    c = mix_np(col("tar"), col("gravel"), np.round(noise_np(fx * 3.1, fy * 3.1, 33) * 4) / 4 * 0.7)
    c = put(c, np.abs(noise_np(fx * 2.2, fy * 2.2, 34) - 0.5) < 0.012, col("soot"))
    yard = blend_np(c, (0.56 + 0.45 * n) * (0.97 + 0.06 * hash01_np(np.floor(fx * 28), np.floor(fy * 28), 35)))
    yard = put(yard, (n < 0.27) & (noise_np(fx * 12, fy * 12, 32) > 0.5), mix_np(c, col("moss"), full(0.45)))
    # Um die Häuser im Hof ein gepflasterter Streifen mit Kantenstein
    yard = put(yard, near < 0.62, mosaic)
    border = (near >= 0.55) & (near < 0.62)
    yard = put(yard, border, blend_np(col("stone"), 0.62 + 0.36 * n))
    yard = put(yard, border & (((fx + fy) * 1.9) % 1 < 0.04), blend_np(col("stone_d"), 0.5 + 0.3 * n))
    yard = put(yard, (near >= 0.62) & (near < 0.65), blend_np(yard, full(0.7)))
    out = np.where(road[..., None], paved, np.where(walk[..., None], slabs, yard))
    # Traufstreifen: Direkt an der Wand liegt kein Pflaster, sondern Dreck, Moos und Kies.
    drip = (near < 0.085 + 0.03 * noise_np(fx * 7, fy * 7, 61)) & ~road
    soil = mix_np(col("dirt"), col("moss"), np.clip(noise_np(fx * 6, fy * 6, 62) * 1.6 - 0.6, 0, 1) * 0.7)
    soil = blend_np(soil, 0.7 + 0.5 * hash01_np(np.floor(fx * 40), np.floor(fy * 40), 63))
    out = put(out, drip, soil)
    for x0, y0, x1, y1 in GRATES if len(solids) else ():
        inside = (fx >= x0) & (fx < x1) & (fy >= y0) & (fy < y1)
        long_x = x1 - x0 > y1 - y0
        bars = ((np.where(long_x, fx, fy) * 26) % 1 < 0.45)
        rim = (fx < x0 + 0.02) | (fx > x1 - 0.02) | (fy < y0 + 0.02) | (fy > y1 - 0.02)
        out = put(out, inside, np.where((bars & ~rim)[..., None], col("black"), blend_np(col("iron"), full(0.8))))
    return out.astype(np.uint8)


def paint_ground(s, solids, dirt, cut_out):
    """Boden in der Auflösung der Szene. cut_out: außerhalb der Karte durchsichtig statt himmelfarben."""
    ys, xs = np.mgrid[0:s.h, 0:s.w]
    a = ((xs + 0.5) / s.res - s.ox) / HW
    b = ((ys + 0.5) / s.res - s.oy) / HH
    fx, fy = (a + b) / 2, (b - a) / 2
    inside = (fx >= 0) & (fx < 20) & (fy >= 0) & (fy < 20)
    rgb = np.empty((s.h, s.w, 3), np.uint8)
    rgb[...] = C["sky"]
    rgb[inside] = ground_np(fx[inside], fy[inside], solids)
    s.img = Image.fromarray(np.dstack([rgb, np.full((s.h, s.w), 255, np.uint8)]), "RGBA").copy()
    s.px = s.img.load()
    paint_dirt(s, dirt)
    arr = np.asarray(s.img).copy()
    for x0, y0, x1, y1 in solids:
        dist = np.hypot(np.maximum.reduce([x0 - fx, np.zeros_like(fx), fx - x1]), np.maximum.reduce([y0 - fy, np.zeros_like(fy), fy - y1]))
        shade = inside & (dist > 0) & (dist < 0.5)
        arr[shade, :3] = np.clip(np.floor(arr[shade, :3] * (0.62 + 0.38 * dist[shade] / 0.5)[:, None]), 0, 255)
    if cut_out:
        arr[..., 3] = np.where(inside, 255, 0)
    s.img = Image.fromarray(arr, "RGBA").copy()
    s.px = s.img.load()


def draw_ground_fine(s, solids, dirt):
    """Boden in der Auflösung der Szene, Schmutz und Gullydeckel wie im einfachen Bild."""
    paint_ground(s, solids, dirt, True)
    for mx, my in MANHOLES:
        manhole(s, mx, my)


def bleed(img, rounds=4):
    """Füllt die Farbe durchsichtiger Bildpunkte am Rand mit der ihrer Nachbarn. Sonst zieht die weiche Überblendung
    im Spiel dort einen dunklen Saum. Die Deckkraft bleibt, wie sie ist."""
    arr = np.asarray(img).astype(np.float32)
    known = arr[..., 3] > 0
    rgb = arr[..., :3] * known[..., None]
    for _ in range(rounds):
        pad = np.pad(rgb, ((1, 1), (1, 1), (0, 0)))
        kpad = np.pad(known, 1).astype(np.float32)
        total = sum(pad[1 + dy:pad.shape[0] - 1 + dy, 1 + dx:pad.shape[1] - 1 + dx] * kpad[1 + dy:kpad.shape[0] - 1 + dy, 1 + dx:kpad.shape[1] - 1 + dx, None]
                    for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)))
        count = sum(kpad[1 + dy:kpad.shape[0] - 1 + dy, 1 + dx:kpad.shape[1] - 1 + dx] for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)))
        fresh = ~known & (count > 0)
        rgb[fresh] = total[fresh] / count[fresh][:, None]
        known = known | fresh
    out = np.dstack([rgb, arr[..., 3]]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def draw_ground(s, solids, glows, dirt=None):
    """Malt den Boden. Ohne dirt wird der Schmutz neu gestreut, sonst werden die übergebenen Flecken gemalt."""
    if dirt is None:
        dirt = scatter_dirt(s, solids)
    paint_ground(s, solids, dirt, False)
    # Der Lichtschein der Laternen entsteht im Spiel, hier würde er nur als Punktmuster stören.
    return dirt


def restore_random(point):
    """Setzt den Zufall auf den Stand, den er vor dem Umbau der Häuser an dieser Stelle hatte (rng_freeze.json).

    Davon hängt ab, welche Fenster leuchten und wo Pfützen und Dreck liegen, also auch das Licht fürs Schleichen.
    Neue Einzelheiten der Häuser nehmen deshalb DETAIL und lassen diese Folgen in Ruhe.
    """
    frozen = json.loads(Path(__file__).with_name("rng_freeze.json").read_text())[point]
    for name, (version, state, gauss) in frozen.items():
        globals()[name].setstate((version, tuple(state), gauss))


def build_scene(with_people=True):
    global DUAL
    DUAL = not with_people  # Feinfassungen braucht nur der Export
    s = Scene(20)
    things, glows = [], []
    for points in AMBIENT.values():
        points.clear()
    solids = [(1, 1, 6, 6), (0, 7, 6, 9), (1, 12, 6, 17), (12, 1, 18, 6), (12, 12, 17, 18)]

    # Links: verputztes Mietshaus mit Laden, Bank aus Stein, Hotel mit Leuchtreklame
    grey_render = (150, 142, 128)
    left = tenement(160, 128, door_at=30, render=grey_render, pipes=(4,), balconies=((3, 2), (4, 1)))
    shop_d = draw_on(left)
    shop(left, shop_d, 60, 127, 76, "KOHLE")
    right = tenement(160, 128, door_at=70, render=grey_render, pipes=(152,), balconies=((1, 2), (1, 1), (4, 2)))
    pipe_v(draw_on(right), 12, 10, 118)
    poster(draw_on(left), 8, 96)
    poster(draw_on(right), 100, 96)
    scrawl(right, 104, 110, "STREIK", 6, 3)
    building(1, 1, 5, 5, 128, left, right, s, things,
             [chimney(0.14, 0.18), chimney(0.82, 0.3, 20, False), water_tank(0.3, 0.3), vent(0.55, 0.45), skylight(0.3, 0.58, 0.0, (0.5, 0.4)),
              clothesline(0.5, 0.7, 0.72, 0.5), roof_mast(0.68, 0.3)],
             roof=roof_mansard((0.4, 1.9, 3.5), (0.5, 2.1, 3.7)))

    bank_l = tenement(192, 84, material=("stone",), lit_rate=0.7)
    bank_front(bank_l)
    bank_r = tenement(64, 84, material=("stone",), lit_rate=0.7)
    bank_side(bank_r)
    building(0, 7, 6, 2, 84, bank_l, bank_r, s, things, [vent(0.82, 0.42)], roof=roof_bank())

    ochre = (168, 138, 92)
    hotel_l = tenement(160, 170, "HOTEL", door_at=72, lit_rate=0.8, render=ochre, corner_stones=True, bays=((0, 1, 2),), pipes=(150,),
                       balconies=((4, 3), (4, 2), (4, 1)))
    hotel_r = tenement(160, 170, door_at=30, lit_rate=0.8, render=ochre, corner_stones=True, bays=((3, 2, 3),), balconies=((1, 3), (1, 1)))
    pipe_v(draw_on(hotel_r), 120, 10, 160)
    poster(draw_on(hotel_l), 20, 138)
    poster(draw_on(hotel_l), 110, 136)
    poster(draw_on(hotel_r), 62, 138)
    scrawl(hotel_r, 70, 139, "BRANDT RAUS", 7, 8)
    building(1, 12, 5, 5, 170, hotel_l, hotel_r, s, things,
             [roof_sign("HOTEL ADLER", 0.14, 0.75), chimney(0.82, 0.3, 24, False), chimney(0.14, 0.2, 22), vent(0.56, 0.4), water_tank(0.3, 0.3),
              roof_mast(0.68, 0.52, 24)],
             roof=roof_mansard((0.5, 1.6, 2.7, 3.8), (0.6, 1.7, 2.8, 3.9)))

    # Rechts: Fabrik aus Grünblech mit Rohren, Kessel und Maschinenhalle
    fab_l, fd = wall(192, 110, ("green",))
    cornice(fd, 192, "green_l", dentils=False)
    plinth(fd, 192, 110, 7)
    for u in range(14, 170, 44):
        fd.rectangle((u - 2, 14, u + 30, 46), fill=C["iron"])
        fd.rectangle((u, 16, u + 28, 44), fill=C["gas_d"])
        for k in range(u, u + 28, 7):
            fd.line((k, 16, k, 44), fill=C["iron"])
        for v in (25, 35):
            fd.fine_line((u, v, u + 28, v), fill=C["iron"])
        fd.shade((u, 16, u + 28, 17), 0.6)
        fd.rectangle((u - 3, 46, u + 31, 47), fill=C["green_l"])
        fd.shade((u - 2, 48, u + 31, 49), 0.65)
        fab_l.info.setdefault("panes", []).append((u, 16, 29, 29, "halle"))
    # Rolltor in einem Rahmen aus Winkeleisen
    fd.rectangle((58, 74, 114, 109), fill=C["soot"])
    fd.rectangle((60, 76, 112, 109), fill=C["iron"])
    for v in range(78, 109, 3):
        fd.fine_line((60, v, 112, v), fill=C["soot"])
    fd.shade((60, 76, 112, 78), 0.6)
    rust_patches(fd, 60, 100, 112, 108)
    pipe_h(fd, 0, 191, 52)
    pipe_v(fd, 130, 52, 102)
    pipe_v(fd, 24, 52, 102)
    # Firmenname direkt aufs Blech gemalt, die Farbe blättert
    tw = 12 * 4 * 2 * 0.8 + 11 * 2.6
    fd.shade((int(96 - tw / 2 - 5), 57, int(96 + tw / 2 + 5), 72), 0.62)
    paint_text(fab_l, 96 - tw / 2, 59, "BRANDT WERKE", 12, [(C["cream"], 2.0, 0, 0, 0.95, 0.36)], gap=2.6, seed=11)
    fab_r, fr = wall(160, 110, ("green",))
    cornice(fr, 160, "green_l", dentils=False)
    plinth(fr, 160, 110, 7)
    for u in (16, 108):
        fr.rectangle((u - 2, 50, u + 30, 82), fill=C["iron"])
        fr.rectangle((u, 52, u + 28, 80), fill=C["glass_d"])
        for k in range(u, u + 28, 7):
            fr.line((k, 52, k, 80), fill=C["iron"])
        for v in (61, 71):
            fr.fine_line((u, v, u + 28, v), fill=C["iron"])
        for _ in range(5):
            bu, bv = u + 7 * DETAIL.randint(0, 3), DETAIL.choice((52, 62, 72))
            fr.rectangle((bu + 1, bv + 1, bu + 6, bv + 8), fill=C["black"])
        fr.shade((u, 52, u + 28, 53), 0.6)
    pipe_h(fr, 0, 159, 30)
    pipe_v(fr, 40, 30, 102)
    downpipe(fr, 150, 2, 104)
    scrawl(fab_r, 56, 88, "STREIK", 10, 2)
    building(12, 1, 6, 5, 110, fab_l, fab_r, s, things,
             [chimney(0.12, 0.25, 54), chimney(0.42, 0.25, 54), vent(0.82, 0.2), roof_mast(0.72, 0.7, 26)],
             roof=roof_shed(5, 24))

    # Lager unten rechts
    lag_l, ld = wall(160, 70, ("brick",))
    cornice(ld, 160, dentils=False)
    plinth(ld, 160, 70, 7)
    # Die Tür sitzt genau vor dem Durchgang in Spalte 14 der Karte.
    door(ld, 66, 69, 28, 40, "wood", True)
    paint_text(lag_l, 104, 12, "LAGER", 13, [(C["paper"], 2.2, 0, 0, 0.92, 0.32)], seed=17)
    poster(ld, 14, 40)
    poster(ld, 38, 42)
    downpipe(ld, 4, 2, 64)
    chip_corners(ld, 160, 70)
    lag_r, lr = wall(192, 70, ("brick",))
    cornice(lr, 192, dentils=False)
    plinth(lr, 192, 70, 7)
    pipe_h(lr, 0, 191, 14)
    for u in range(20, 170, 40):
        window(lr, u, 30, 14, 18, lit=u % 3 == 0)
    patch(lr, 124, 26, 22, 14, blend(C["brick_l"], 0.9))
    scrawl(lag_r, 42, 54, "STREIK", 6, 5)
    downpipe(lr, 186, 2, 64)
    chip_corners(lr, 192, 70)
    building(12, 12, 5, 6, 70, lag_l, lag_r, s, things, [vent(0.3, 0.3), chimney(0.7, 0.6, 22)], roof=roof_gable(38))

    if not with_people:
        restore_random("yard")
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

    overhead_wire(8.5, ((3.2, 6.0, 12.0, 82), (14.5, 6.0, 12.0, 78)), s, things)
    street_props(s, things)

    # Figuren
    if with_people:
        person(10.5, 8.4, "Agent", "e", 0, s, things)
        person(10.2, 8.9, "Agent", "e", 1, s, things)
        person(10.9, 8.9, "Agent", "s", 0, s, things)
        person(10.6, 9.5, "Wachmann", "s", 1, s, things)
        person(7.2, 6.2, "Wachmann", "n", 0, s, things)
        person(6.7, 9.8, "Agent", "e", 3, s, things)
        person(11.2, 6.1, "Agent", "s", 3, s, things)

    s.dirt = draw_ground(s, solids, glows)
    s.solids, s.glows = solids, glows
    # Gullydeckel, aus denen im Spiel Dampf aufsteigt
    for mx, my in MANHOLES:
        manhole(s, mx, my)
        cx, cy = s.to_screen(mx, my)
        AMBIENT["steam"].append([int(cx), int(cy)])
    return s, sort_things(things)


def export_game():
    """Schreibt die Grafiken für das Godot-Projekt und die Kartendaten."""
    root = Path(__file__).resolve().parents[2]
    assets = root / "game" / "assets"
    (assets / "things").mkdir(parents=True, exist_ok=True)
    restore_random("export")
    DETAIL.seed(1951)
    s, things = build_scene(with_people=False)
    # Außerhalb der Karte bleibt der Boden durchsichtig, dort liegt die Umgebung.
    mask = Image.new("L", s.img.size)
    mask.putdata([255 if 0 <= fx < 20 and 0 <= fy < 20 else 0 for fx, fy in (s.to_map(x + 0.5, y + 0.5) for y in range(s.h) for x in range(s.w))])
    s.img.putalpha(mask)
    s.img.save(assets / "ground.png")
    backdrop = {"file": "backdrop.png", "px": s.ox - BACKDROP_W // 2, "py": s.oy + 20 * HH - BACKDROP_H // 2}
    manifest, solids, exported = [], [], []
    for i, t in enumerate(things):
        s.img = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
        s.px = s.img.load()
        t.draw()
        bbox = s.img.getbbox()
        if not bbox:
            continue
        name = f"{i:03d}.png"
        s.img.crop(bbox).save(assets / "things" / name)
        exported.append((t, name, bbox))
        manifest.append({"file": name, "px": bbox[0], "py": bbox[1], "x0": t.x0, "y0": t.y0, "x1": t.x1, "y1": t.y1, "z0": t.z0, "z1": t.z1, "kind": t.kind, "panes": t.panes,
                         **({"roof": [p.to_json() for p in t.prisms]} if t.prisms else {})})
        if t.solid:
            solids.append({"x0": t.x0, "y0": t.y0, "x1": t.x1, "y1": t.y1, "opaque": t.opaque})
    (assets / "manifest.json").write_text(json.dumps({"ox": s.ox, "oy": s.oy, "things": manifest, "backdrop": backdrop,
                                                "hd": {"res": HD, "ground": "hd/ground.png", "things": [n for t, n, _ in exported if t.hd]},
                                                **AMBIENT}), encoding="utf-8")
    (root / "data" / "map_01.json").write_text(json.dumps({"width": 20, "height": 20, "solids": solids}, indent=1), encoding="utf-8")

    export_critters(assets)
    if "--city" not in sys.argv:  # Die Figuren brauchen einige Minuten und hängen nicht an der Stadt.
        subprocess.run([sys.executable, str(Path(__file__).with_name("figures3d.py"))], check=True)
    subprocess.run([sys.executable, str(Path(__file__).with_name("props.py"))], check=True)

    export_backdrop(assets)

    # Kontobuch
    book = Image.new("RGBA", (14, 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(book)
    d.polygon([(7, 0), (13, 3), (7, 6), (1, 3)], fill=C["wine_l"])
    d.polygon([(1, 3), (7, 6), (7, 9), (1, 6)], fill=C["wine_d"])
    d.polygon([(7, 6), (13, 3), (13, 6), (7, 9)], fill=C["paper"])
    d.point((7, 3), fill=C["brass_l"])
    book.save(assets / "book.png")
    # Ganz zuletzt: Die feinen Fassungen verbrauchen Zufall, der vorher die einfachen Bilder verändert hätte.
    export_hd(assets, s, exported)
    print(f"{len(manifest)} Objekte, {len(solids)} Blockaden exportiert nach {assets}")


def export_hd(assets, s, exported):
    """Boden und die dafür vorbereiteten Objekte in doppelter Auflösung, deckungsgleich mit den einfachen Bildern."""
    out = assets / "hd"
    (out / "things").mkdir(parents=True, exist_ok=True)
    for old in (out / "things").glob("*.png"):
        old.unlink()
    global DUAL
    DUAL = True  # was erst beim Zeichnen entsteht, etwa Platzhalterflächen, bekommt so auch eine Feinfassung
    s.use(HD)
    draw_ground_fine(s, s.solids, s.dirt)
    bleed(s.img).save(out / "ground.png")
    for t, name, bbox in exported:
        if not t.hd:
            continue
        s.img = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
        s.px = s.img.load()
        t.draw()
        bleed(s.img.crop(tuple(v * HD for v in bbox))).save(out / "things" / name)
    s.use(1)


def export_backdrop(assets):
    """Die Stadt jenseits der Karte: Die Straßen laufen weiter und verlieren sich zwischen Häuserblocks im Dunst.

    Reine Kulisse ohne Lichtberechnung und ohne Einfluss aufs Spiel. Läuft zuletzt, weil sie den Zufall der Fassaden mitbenutzt.
    """
    global DUAL
    DUAL = False  # die Kulisse bleibt in einfacher Auflösung
    s = Scene(20)
    s.w, s.h = BACKDROP_W, BACKDROP_H
    s.ox, s.oy = BACKDROP_W // 2, BACKDROP_H // 2 - 20 * HH
    s.img = Image.new("RGBA", (s.w, s.h), C["sky"] + (255,))
    s.px = s.img.load()
    ys, xs = np.mgrid[0:s.h, 0:s.w]
    a, b = (xs + 0.5 - s.ox) / HW, (ys + 0.5 - s.oy) / HH
    fx, fy = (a + b) / 2, (b - a) / 2
    dist = np.maximum.reduce([-fx, fx - 20, -fy, fy - 20])
    street = (dist >= 0) & (dist < BACKDROP_STREETS)
    paving = np.floor(ground_np(fx[street], fy[street]) * BACKDROP_DARK)
    haze = ((dist[street] / BACKDROP_STREETS) ** 0.7)[:, None]
    arr = np.asarray(s.img).copy()
    arr[street, :3] = (paving + (np.array(C["sky"], np.float64) - paving) * haze).astype(np.uint8)
    s.img = Image.fromarray(arr, "RGBA")
    s.px = s.img.load()
    canvas = s.img
    things = []
    # Vor der Karte (x oder y über 20) bleiben die Blocks weit genug weg und flach, damit sie nichts verdecken.
    blocks = [(x, y, 6, 6, 170 if ring else 128) for ring, x in ((0, -7), (1, -15)) for y in (-15, -7, 0, 14.5, 21, 28)]
    blocks += [(x, y, 5.5 if x == 0 else 6, 6, 170 if ring else 128) for ring, y in ((0, -7), (1, -15)) for x in (0, 12.5, 19, 26)]
    blocks += [(24, y, 6, 6, 86) for y in (-7, 0, 14.5, 21, 28)] + [(x, 24, 5.5 if x == 0 else 6, 6, 86) for x in (-7, 0, 12.5, 19)]
    for x, y, w, d, h in blocks:
        material = ("stone",) if hash01(int(x), int(y), 4) < 0.3 else ("brick",)
        left = tenement(int(w * HW), h, material=material, lit_rate=0.22)
        right = tenement(int(d * HW), h, material=material, lit_rate=0.22)
        roof = roof_gable(34, "x" if hash01(int(x), int(y), 6) < 0.5 else "y", 1.0) if hash01(int(x), int(y), 5) < 0.4 else roof_mansard((1.0, 2.7, 4.4), (1.0, 2.7, 4.4))
        building(x, y, w, d, h, left, right, s, things, [chimney(0.2, 0.3, 24, False), vent(0.6, 0.6)], roof=roof)
    for t in sort_things(things):
        s.img = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
        s.px = s.img.load()
        t.draw()
        bbox = s.img.getbbox()
        if not bbox:
            continue
        part = s.img.crop(bbox)
        away = max(-t.x1, t.x0 - 20, -t.y1, t.y0 - 20)
        haze = min(0.85, 0.3 + away / 22)
        dimmed = Image.blend(part, Image.new("RGBA", part.size, (0, 0, 0, 255)), 0.28)
        fogged = Image.blend(dimmed, Image.new("RGBA", part.size, C["sky"] + (255,)), haze)
        canvas.paste(fogged, bbox[:2], part.getchannel("A"))
    canvas.save(assets / "backdrop.png", optimize=True)


def export_critters(assets):
    """Kleintiere, Feld 16 × 12. Zeile 0 Ratte: laufen, laufen, sitzen. Zeile 1 Taube: sitzen, picken, Flügel oben, Flügel unten."""
    sheet = Image.new("RGBA", (16 * 4, 12 * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(sheet)
    fur, fur_d, fur_l, skin = (104, 92, 82), (66, 58, 54), (136, 122, 106), (168, 120, 112)
    for column, lift in ((0, 0), (1, 1), (2, 0)):
        x = column * 16
        run = column < 2
        d.line((x + 1, 9 - lift, x + 4, 9), fill=skin)
        d.rectangle((x + 4, 7 - lift, x + 10, 10 - lift), fill=fur)
        d.line((x + 4, 7 - lift, x + 10, 7 - lift), fill=fur_l)
        d.line((x + 4, 10 - lift, x + 10, 10 - lift), fill=fur_d)
        d.rectangle((x + 11, 8 - lift, x + 13, 10 - lift), fill=fur)
        d.point((x + 14, 9 - lift), fill=skin)
        d.point((x + 12, 7 - lift), fill=fur_d)
        d.point((x + 13, 8 - lift), fill=(16, 12, 12))
        feet = (5, 9) if lift == 0 or not run else (7, 11)
        for foot in feet:
            d.point((x + foot, 11), fill=fur_d)
    grey, grey_d, grey_l, neck = (128, 132, 142), (82, 86, 98), (170, 172, 178), (92, 120, 110)
    y = 12

    def body(x, head_y, head_x=11):
        d.polygon([(x + 4, y + 7), (x + 7, y + 4), (x + 11, y + 5), (x + 11, y + 9), (x + 6, y + 10)], fill=grey)
        d.line((x + 6, y + 9, x + 10, y + 9), fill=grey_d)
        d.line((x + 2, y + 8, x + 5, y + 8), fill=grey_d)
        d.rectangle((x + head_x, y + head_y, x + head_x + 1, y + head_y + 2), fill=neck)
        d.point((x + head_x + 2, y + head_y + 1), fill=(216, 176, 96))

    body(0, 3)
    d.point((8, y + 11), fill=(150, 80, 70))
    body(16, 7, 12)
    d.point((16 + 8, y + 11), fill=(150, 80, 70))
    for column, wing in ((2, ((6, 6), (3, 0), (10, 2), (10, 6))), (3, ((6, 6), (2, 10), (9, 11), (10, 6)))):
        x = column * 16
        body(x, 4)
        d.polygon([(x + a, y + b) for a, b in wing], fill=grey_l)
    sheet.save(assets / "critters.png")


def main():
    if "--figures" in sys.argv:
        export_critters(Path(__file__).resolve().parents[2] / "game" / "assets")
        subprocess.run([sys.executable, str(Path(__file__).with_name("figures3d.py"))], check=True)
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
