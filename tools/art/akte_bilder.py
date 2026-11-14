"""Bilder für Waffenkammer und Ausbauten: lavierte Werkstattzeichnungen auf Karteikarten, wie aus einer Akte von 1927.

Je Gegenstand aus data/waffen.json ein Bild, je Ausbau (Beine, Arme, Herz) und Stufe eines. Eigene Entwürfe, gezeichnet aus Flächen und Linien.
Aufruf: python tools/art/akte_bilder.py [--sheet=<Datei.png>]   (wenige Sekunden)
Ergebnis: game/assets/akte/*.png, 256 × 192 Bildpunkte, gedacht für die Anzeige in halber (128 × 96) und viertel Größe (64 × 48),
dazu game/assets/akte/zuordnung.json: Kennung -> Datei. Mit --sheet entsteht ein Übersichtsblatt aller Bilder.
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game" / "assets" / "akte"
FONTS = ROOT / "game" / "assets" / "fonts"
W, H = 256, 192
SUPER = 4
UPGRADE_LEVELS = 3  # Campaign.MaxLevel
UPGRADES = {"legs": "beine", "arms": "arme", "heart": "herz"}  # Kennung im Spiel -> Name in der Datei

PAPER = (200, 184, 148)
INK = (36, 30, 26)
RED = (140, 30, 22)
STEEL, STEEL_L, STEEL_D = (86, 88, 90), (136, 138, 134), (54, 55, 58)
WOOD, WOOD_L, WOOD_D = (104, 68, 40), (146, 102, 62), (70, 44, 28)
LEATHER, LEATHER_L, LEATHER_D = (110, 72, 46), (150, 106, 70), (66, 42, 30)
BRASS, BRASS_L, BRASS_D = (178, 140, 62), (222, 190, 108), (118, 88, 38)
TIN, TIN_L, TIN_D = (132, 134, 126), (176, 178, 168), (86, 88, 84)
CLOTH, CLOTH_D = (224, 216, 196), (176, 166, 144)
TROUSERS, SHIRT = (70, 66, 64), (198, 188, 164)
RUBBER = (44, 38, 36)
RUST = (128, 70, 40)
FACE = (226, 218, 196)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def light(c, t=0.35):
    return mix(c, (255, 246, 220), t)


def dark(c, t=0.35):
    return mix(c, (18, 14, 12), t)


class Pen:
    """Zeichnet in Kartenkoordinaten (256 × 192) auf ein vergrößertes Bild. at() verschiebt, dreht und skaliert."""

    def __init__(self, img, m=(1, 0, 0, 0, 1, 0), k=1.0):
        self.img, self.d, self.m, self.k = img, ImageDraw.Draw(img), m, k

    def at(self, x, y, deg=0.0, k=1.0):
        a, b, c, d, e, f = self.m
        co, si = math.cos(math.radians(deg)) * k, math.sin(math.radians(deg)) * k
        return Pen(self.img, (a * co + b * si, -a * si + b * co, a * x + b * y + c, d * co + e * si, -d * si + e * co, d * x + e * y + f), self.k * k)

    def pts(self, points):
        a, b, c, d, e, f = self.m
        return [((a * x + b * y + c) * SUPER, (d * x + e * y + f) * SUPER) for x, y in points]

    def line(self, points, col=INK, w=1.3, closed=False):
        p = self.pts(points)
        if closed:
            p = p + p[:2]
        self.d.line(p, fill=col + (255,), width=max(1, int(round(w * self.k * SUPER))), joint="curve")
        r = w * self.k * SUPER / 2
        if not closed and r > 1.5:
            for x, y in (p[0], p[-1]):
                self.d.ellipse((x - r, y - r, x + r, y + r), fill=col + (255,))

    def poly(self, points, fill, line=INK, w=1.3):
        if fill is not None:
            self.d.polygon(self.pts(points), fill=fill + (255,))
        if line is not None:
            self.line(points, line, w, closed=True)

    @staticmethod
    def ring(cx, cy, rx, ry, a0=0.0, a1=360.0, n=40):
        return [(cx + rx * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + ry * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]

    def oval(self, cx, cy, rx, ry, fill, line=INK, w=1.3):
        self.poly(self.ring(cx, cy, rx, ry)[:-1], fill, line, w)

    def arc(self, cx, cy, rx, ry, a0, a1, col=INK, w=1.3):
        self.line(self.ring(cx, cy, rx, ry, a0, a1), col, w)

    def box(self, x0, y0, x1, y1, fill, line=INK, w=1.3, r=0.0):
        if r <= 0:
            return self.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], fill, line, w)
        points = []
        for cx, cy, a in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
            points += self.ring(cx, cy, r, r, a, a + 90, 6)
        self.poly(points, fill, line, w)

    def bar(self, x0, y0, x1, y1, col, r=0.0, w=1.3, upright=False):
        """Rundes Stück Metall oder Holz: oben (stehend: links) ein heller Streifen, unten ein dunkler."""
        self.box(x0, y0, x1, y1, col, None, r=r)
        if upright:
            t = (x1 - x0)
            self.box(x0 + t * 0.12, y0 + r, x0 + t * 0.34, y1 - r, light(col, 0.3), None)
            self.box(x0 + t * 0.7, y0 + r, x1, y1 - r, dark(col, 0.3), None)
        else:
            t = (y1 - y0)
            self.box(x0 + r, y0 + t * 0.12, x1 - r, y0 + t * 0.34, light(col, 0.3), None)
            self.box(x0 + r, y0 + t * 0.7, x1 - r, y1, dark(col, 0.3), None)
        self.box(x0, y0, x1, y1, None, INK, w, r)

    def dashes(self, points, col=INK, w=0.9, on=3.0, off=3.0):
        """Naht: gestrichelte Linie entlang eines Linienzugs."""
        run, draw = 0.0, True
        for (ax, ay), (bx, by) in zip(points, points[1:]):
            length = math.hypot(bx - ax, by - ay)
            t = 0.0
            while t < length:
                step = min((on if draw else off) - run, length - t)
                if draw:
                    self.line([(ax + (bx - ax) * t / length, ay + (by - ay) * t / length), (ax + (bx - ax) * (t + step) / length, ay + (by - ay) * (t + step) / length)], col, w)
                t += step
                run += step
                if run >= (on if draw else off) - 1e-6:
                    run, draw = 0.0, not draw

    def gear(self, cx, cy, r, teeth, fill, w=1.2, hole=0.3):
        points = []
        for i in range(teeth * 4):
            rr = r if i % 4 in (0, 1) else r * 0.8
            a = 2 * math.pi * (i + 0.5) / (teeth * 4)
            points.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        self.poly(points, fill, INK, w)
        self.oval(cx, cy, r * 0.56, r * 0.56, light(fill, 0.2), INK, w * 0.8)
        self.oval(cx, cy, r * hole, r * hole, dark(fill, 0.5), INK, w * 0.8)

    def rivets(self, points, col=BRASS_L, r=1.5):
        for x, y in points:
            self.oval(x, y, r, r, col, INK, 0.7)

    def gauge(self, cx, cy, r, needle=40.0, rim=BRASS):
        """Druckmesser: Messingrand, helles Zifferblatt, roter Bereich, Zeiger."""
        self.oval(cx, cy, r, r, rim, INK, 1.2)
        self.oval(cx, cy, r * 0.78, r * 0.78, FACE, INK, 0.8)
        self.arc(cx, cy, r * 0.6, r * 0.6, -60, 10, RED, r * 0.16)
        for a in range(-210, 31, 40):
            ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
            self.line([(cx + ca * r * 0.52, cy + sa * r * 0.52), (cx + ca * r * 0.72, cy + sa * r * 0.72)], INK, 0.7)
        a = math.radians(needle - 210)
        self.line([(cx, cy), (cx + math.cos(a) * r * 0.62, cy + math.sin(a) * r * 0.62)], INK, 1.1)
        self.oval(cx, cy, r * 0.12, r * 0.12, INK, None)

    def wheel(self, cx, cy, r, col=STEEL, spokes=5, turn=0.0):
        """Handrad oder Schwungrad mit Speichen."""
        self.oval(cx, cy, r, r, col, INK, 1.2)
        self.oval(cx, cy, r * 0.74, r * 0.74, dark(PAPER, 0.25), INK, 0.9)
        for i in range(spokes):
            a = math.radians(turn + 360 * i / spokes)
            self.line([(cx, cy), (cx + math.cos(a) * r * 0.78, cy + math.sin(a) * r * 0.78)], col, r * 0.2)
        self.oval(cx, cy, r * 0.24, r * 0.24, light(col, 0.2), INK, 0.9)


def bezier(p0, p1, p2, p3, n=26):
    points = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        points.append(tuple(u ** 3 * p0[j] + 3 * u * u * t * p1[j] + 3 * u * t * t * p2[j] + t ** 3 * p3[j] for j in range(2)))
    return points


def tube(p, points, col=RUBBER, w=5.0):
    """Schlauch: dunkle Linie mit hellem Glanzstrich."""
    p.line(points, INK, w + 1.6)
    p.line(points, col, w)
    p.line([(x - w * 0.18, y - w * 0.18) for x, y in points], light(col, 0.35), w * 0.22)


def font(name, size):
    return ImageFont.truetype(str(FONTS / name), int(size * SUPER))


def text(img, words, x, y, size, col=INK, deg=0.0, face="CourierPrime-Bold.ttf", alpha=1.0, frame=False):
    """Schrift oder Stempel, um den Mittelpunkt (x, y) gedreht."""
    f = font(face, size)
    box = f.getbbox(words)
    pad = int(3 * SUPER)
    tile = Image.new("RGBA", (box[2] - box[0] + pad * 2, box[3] - box[1] + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(tile)
    d.text((pad - box[0], pad - box[1]), words, font=f, fill=col + (255,))
    if frame:
        d.rectangle((2, 2, tile.width - 3, tile.height - 3), outline=col + (255,), width=max(1, int(0.9 * SUPER)))
    if alpha < 1:
        tile.putalpha(tile.getchannel("A").point(lambda v: int(v * alpha)))
    tile = tile.rotate(deg, expand=True, resample=Image.BICUBIC)
    img.alpha_composite(tile, (int(x * SUPER - tile.width / 2), int(y * SUPER - tile.height / 2)))


# ---------- Gegenstände der Waffenkammer ----------

def trigger_guard(p, cx, cy, rx, ry):
    p.arc(cx, cy, rx, ry, 0, 180, INK, 4.0)
    p.arc(cx, cy, rx, ry, 0, 180, STEEL_L, 2.0)


def draw_pistole(p, img):
    p = p.at(4, 0)
    p.bar(34, 66, 54, 78, STEEL_D, 1)  # Mündung
    p.poly([(150, 90), (198, 88), (216, 150), (208, 157), (172, 157), (164, 150)], STEEL_D)  # Griffstück
    p.poly([(158, 98), (194, 96), (206, 146), (177, 148)], WOOD)  # Griffschale, geriffelt
    for i in range(8):
        p.line([(160 + i * 4.6, 99), (178 + i * 3.8, 147)], WOOD_D, 0.7)
    for i in range(6):
        p.line([(160 + i * 3.2, 104 + i * 8), (196 + i * 1.8, 100 + i * 8)], WOOD_D, 0.6)
    p.rivets([(180, 104), (190, 140)], STEEL_L, 1.8)
    p.box(170, 157, 210, 162, STEEL, r=1)  # Magazinboden
    trigger_guard(p, 132, 98, 18, 15)
    p.poly([(136, 94), (141, 94), (137, 109), (132, 107)], STEEL_L)  # Abzug
    p.poly([(68, 84), (198, 84), (202, 92), (152, 96), (146, 100), (74, 93)], STEEL_D)  # Rahmen
    p.bar(50, 59, 198, 85, STEEL, 2)  # Verschluss
    p.box(104, 62, 130, 71, dark(STEEL_D, 0.3), INK, 0.9)  # Auswurffenster
    for x in range(168, 193, 4):
        p.line([(x, 63), (x + 2, 82)], INK, 0.9)
    p.poly([(56, 59), (60, 54), (64, 59)], STEEL_D, INK, 1.0)  # Korn
    p.box(184, 55, 191, 59, STEEL_D, INK, 1.0)  # Kimme
    p.poly([(197, 62), (207, 55), (212, 61), (203, 70)], STEEL_L)  # Hahn
    p.oval(120, 90, 2.2, 2.2, STEEL_L, INK, 0.8)
    p.line([(58, 80), (160, 80)], dark(STEEL, 0.4), 0.8)
    p.oval(200, 166, 4, 4, None, INK, 1.4)  # Fangriemenöse


def draw_schrotflinte(p, img):
    p = p.at(-6, 4).at(-13, -9, 0, 1.1)
    p.poly([(176, 66), (200, 70), (228, 94), (242, 128), (233, 139), (213, 134), (196, 108), (176, 99)], WOOD)  # abgesägter Schaft
    p.poly([(186, 74), (200, 76), (222, 96), (214, 100), (198, 84)], WOOD_L, None)
    p.poly([(233, 139), (242, 128), (238, 126), (229, 137)], light(WOOD_L, 0.3), INK, 0.8)  # rohe Sägekante
    for i in range(5):  # Wickel aus Isolierband am Griff
        p.line([(203 + i * 4.2, 110 + i * 4.6), (216 + i * 4.2, 100 + i * 4.6)], CLOTH_D, 2.6)
        p.line([(203 + i * 4.2, 110 + i * 4.6), (216 + i * 4.2, 100 + i * 4.6)], INK, 0.5)
    trigger_guard(p, 160, 100, 17, 13)
    p.poly([(156, 98), (160, 98), (157, 110), (153, 108)], STEEL_L)
    p.poly([(166, 98), (170, 98), (168, 109), (164, 107)], STEEL_L)
    p.poly([(68, 86), (130, 86), (130, 99), (78, 97)], WOOD)  # Vorderschaft
    p.line([(76, 92), (128, 93)], WOOD_D, 0.8)
    p.bar(28, 62, 130, 74, STEEL, 1)  # zwei gekürzte Läufe
    p.bar(28, 74, 130, 86, STEEL, 1)
    p.oval(29, 68, 2.6, 4.6, INK, None)
    p.oval(29, 80, 2.6, 4.6, INK, None)
    p.poly([(27, 61), (31, 61), (31, 87), (27, 87)], light(STEEL_L, 0.2), INK, 0.8)  # blanke Sägekante
    p.poly([(128, 58), (168, 58), (177, 70), (177, 97), (128, 99)], STEEL_L)  # Basküle
    p.arc(150, 80, 12, 9, 200, 340, INK, 0.8)
    p.arc(150, 84, 8, 6, 20, 160, INK, 0.8)
    p.oval(136, 92, 2, 2, STEEL, INK, 0.8)
    for x in (158, 167):  # Hähne
        p.poly([(x, 59), (x + 5, 45), (x + 11, 47), (x + 8, 59)], STEEL_D)
    p.line([(150, 57), (176, 60)], STEEL_D, 2.6)  # Verschlusshebel
    p.oval(34, 59, 1.6, 1.6, BRASS_L, INK, 0.8)  # Korn


def draw_maschinenpistole(p, img):
    p = p.at(-2, -6)
    p.poly([(150, 88), (193, 82), (246, 102), (248, 136), (234, 139), (198, 113), (184, 114), (180, 132), (166, 134), (164, 104)], WOOD)  # Schaft mit Griff
    p.poly([(196, 92), (240, 108), (242, 126), (200, 104)], WOOD_L, None)
    p.poly([(244, 101), (249, 103), (250, 137), (245, 139)], STEEL_D, INK, 1.0)  # Kolbenkappe
    trigger_guard(p, 152, 98, 14, 12)
    p.poly([(153, 96), (157, 96), (154, 107), (150, 105)], STEEL_L)
    p.poly([(121, 98), (141, 98), (138, 158), (120, 158)], STEEL)  # Stangenmagazin
    p.poly([(124, 100), (129, 100), (127, 156), (123, 156)], light(STEEL, 0.3), None)
    for y in range(108, 156, 10):
        p.line([(121, y), (140, y)], INK, 0.7)
    p.box(118, 86, 144, 100, STEEL_D, r=1)  # Magazinschacht
    p.bar(10, 71, 24, 80, STEEL_D, 1)  # Mündung
    p.bar(22, 65, 114, 86, STEEL, 2)  # Kühlmantel mit Löchern
    for i, x in enumerate(range(30, 110, 11)):
        p.oval(x, 71, 2.6, 2.6, dark(STEEL_D, 0.5), INK, 0.6)
        p.oval(x + 5.5, 80, 2.6, 2.6, dark(STEEL_D, 0.5), INK, 0.6)
    p.bar(112, 61, 190, 89, STEEL_D, 2)  # Gehäuse
    p.box(120, 66, 172, 70, dark(STEEL_D, 0.5), INK, 0.7)  # Spannschlitz
    p.line([(150, 68), (150, 58)], STEEL_L, 3.0)
    p.oval(150, 56, 4.2, 4.2, STEEL_L)  # Spanngriff
    p.bar(188, 59, 196, 91, STEEL, 1)  # Verschlusskappe
    p.poly([(28, 65), (31, 58), (35, 65)], STEEL_D, INK, 1.0)
    p.box(176, 56, 184, 61, STEEL_D, INK, 1.0)
    p.oval(66, 88, 3, 3, None, INK, 1.4)  # Riemenöse
    p.rivets([(126, 93), (136, 93)], STEEL_L, 1.3)


def draw_totschlaeger(p, img):
    loop = bezier((-86, 0), (-150, -34), (-150, 44), (-88, 4), 30)
    q = p.at(132, 100, 27)
    q.line(loop, INK, 4.6)
    q.line(loop, LEATHER, 3.0)
    q.poly([(-90, -6), (22, -8), (34, -15), (34, 15), (22, 8), (-90, 6)], LEATHER)  # Griff
    q.poly([(-88, -4), (20, -6), (20, -2), (-88, -1)], LEATHER_L, None)
    for x in range(-84, 20, 7):  # Flechtung
        q.line([(x, -6), (x + 6, 6)], LEATHER_D, 0.9)
        q.line([(x + 6, -6), (x, 6)], LEATHER_D, 0.9)
    q.bar(-94, -8, -86, 8, LEATHER_D, 2)
    q.oval(66, 0, 40, 20, LEATHER_D)  # Kopf mit Bleikern
    q.poly(Pen.ring(60, -6, 30, 9, 190, 350, 20), dark(LEATHER, 0.1), None)
    q.dashes(Pen.ring(66, 0, 34, 15, 0, 360, 48), LEATHER_L, 1.0, 3.2, 3.0)
    q.dashes([(30, 0), (104, 0)], LEATHER_L, 1.0, 3.2, 3.0)
    q.line([(30, -13), (30, 13)], INK, 2.2)
    q.line([(35, -15), (35, 15)], LEATHER_L, 1.0)
    q.line([(84, -14), (92, -6)], dark(LEATHER_D, 0.4), 0.8)  # Schrammen
    q.line([(74, 10), (86, 13)], dark(LEATHER_D, 0.4), 0.8)


def draw_dietrich(p, img):
    p.poly([(30, 54), (218, 40), (228, 142), (40, 154)], LEATHER)  # aufgerollte Ledertasche
    p.poly([(38, 60), (212, 47), (216, 100), (42, 112)], LEATHER_L, None)
    p.dashes([(35, 59), (215, 45), (224, 138), (44, 150), (35, 59)], LEATHER_D, 0.9)
    tips = (
        [(0, 0), (0, -6), (5, -10)],  # Haken
        [(0, 0), (-3, -4), (3, -8), (-3, -12), (0, -16)],  # Schlange
        [(0, 0), (0, -8), (-5, -8), (-5, -3)],  # Bart
        [(0, 0), (4, -5), (0, -10)],  # Raute
        [(0, 0), (0, -12)],  # Nadel
        [(0, 0), (0, -5), (6, -5), (6, -11)],  # Stufe
    )
    for i, tip in enumerate(tips):
        x, top = 62 + i * 26, 46 + (i * 7) % 16
        slope = -0.075 * (x - 62)
        q = p.at(x, 114 + slope, (i - 2.5) * 2.2)
        q.line([(0, 0), (0, top - 114)], INK, 3.6)
        q.line([(0, 0), (0, top - 114)], STEEL_L, 2.0)
        tip_points = [(tx, top - 114 + ty) for tx, ty in tip]
        q.line(tip_points, INK, 3.2)
        q.line(tip_points, STEEL_L, 1.7)
        q.box(-3.2, -26, 3.2, 4, STEEL, INK, 1.0, 1)  # Heft
        q.line([(-1.4, -24), (-1.4, 2)], light(STEEL, 0.35), 0.8)
    p.poly([(41, 114), (217, 101), (228, 142), (40, 154)], LEATHER_D)  # Steckfach
    p.dashes([(45, 119), (215, 106)], LEATHER_L, 0.9)
    for i in range(7):
        x = 49 + i * 26
        p.dashes([(x, 119 - 0.075 * (x - 49)), (x + 2, 149 - 0.075 * (x - 49))], LEATHER_L, 0.8, 2.4, 2.4)
    p.line([(226, 92), (246, 96)], INK, 6.0)  # Riemen mit Schnalle
    p.line([(226, 92), (246, 96)], LEATHER, 4.2)
    p.box(232, 88, 240, 100, None, INK, 2.4, 1)
    p.box(232, 88, 240, 100, None, BRASS_L, 1.0, 1)
    p.line([(50, 70), (50, 88), (62, 88)], INK, 3.4)  # Spanner
    p.line([(50, 70), (50, 88), (62, 88)], STEEL_L, 1.8)


def draw_rauchtopf(p, img):
    cx, top, bottom, r = 122, 66, 158, 44
    fuse = bezier((cx, top - 8), (cx + 4, 30), (cx + 44, 52), (cx + 52, 22), 30)
    p.oval(cx, bottom, r, 11, TIN_D)
    p.box(cx - r, top, cx + r, bottom, TIN, None)
    p.box(cx - r + 6, top, cx - r + 22, bottom, TIN_L, None)
    p.box(cx + r - 26, top, cx + r, bottom, TIN_D, None)
    p.box(cx + r - 10, top, cx + r, bottom, dark(TIN_D, 0.3), None)
    band = [(cx - r, 88), (cx + r, 88), (cx + r, 130), (cx - r, 130)]  # Papierbanderole
    p.poly(band, (196, 176, 124), None)
    p.box(cx + r - 26, 88, cx + r, 130, dark((196, 176, 124), 0.3), None)
    p.box(cx - r, 132, cx + r, 139, RED, None)
    p.box(cx + r - 26, 132, cx + r, 139, dark(RED, 0.35), None)
    for y in (88, 130, 132, 139):
        p.line([(cx - r, y), (cx + r, y)], INK, 0.8)
    for y, col in ((76, dark(TIN_D, 0.2)), (150, dark(TIN_D, 0.2))):  # Sicken
        p.line([(cx - r, y), (cx + r, y)], col, 1.2)
    for x, y, a, b in ((cx - 30, 146, 9, 5), (cx + 8, 72, 7, 3), (cx - 12, 152, 5, 3), (cx + 30, 148, 6, 4)):  # Rost
        p.oval(x, y, a, b, RUST, None)
    p.line([(cx - r, top), (cx - r, bottom)], INK, 1.3)
    p.line([(cx + r, top), (cx + r, bottom)], INK, 1.3)
    p.arc(cx, bottom, r, 11, 0, 180, INK, 1.3)
    p.oval(cx, top, r, 11, TIN_L)  # Deckel
    p.oval(cx, top, r - 6, 8, TIN, INK, 0.8)
    p.oval(cx, top - 2, 9, 3.4, BRASS_D, INK, 1.0)  # Zündertülle
    p.line(fuse, INK, 3.8)
    p.line(fuse, (150, 126, 84), 2.2)
    p.dashes(fuse, WOOD_D, 0.8, 2.0, 2.4)
    end = fuse[-1]
    for dx, dy in ((-3, -5), (1, -6), (4, -4)):  # ausgefranstes Ende
        p.line([end, (end[0] + dx, end[1] + dy)], (150, 126, 84), 0.9)
    text(img, "RAUCH", cx - 5, 109, 17, INK, 0, "OldStandard-Bold.ttf", 0.9)
    p.line([(cx + 28, 94), (cx + 36, 124)], dark(TIN_D, 0.3), 0.8)


def draw_verbandszeug(p, img):
    p.box(200, 86, 242, 152, TIN, r=9)  # Flachmann mit Korn
    p.box(205, 92, 216, 146, TIN_L, None, r=5)
    p.box(226, 96, 242, 146, TIN_D, None, r=6)
    p.box(200, 86, 242, 152, None, INK, 1.3, 9)
    p.bar(212, 72, 230, 86, STEEL_D, 2)
    p.line([(204, 116), (238, 116)], dark(TIN_D, 0.2), 0.9)
    p.bar(170, 58, 186, 74, (88, 48, 24), 1, upright=True)  # Jodflasche
    p.bar(167, 46, 189, 60, WOOD_L, 2)
    p.box(154, 72, 198, 152, (98, 54, 24), r=5)
    p.box(158, 76, 166, 148, (150, 92, 44), None, r=3)
    p.box(186, 78, 198, 148, (62, 34, 18), None, r=4)
    p.box(154, 72, 198, 152, None, INK, 1.3, 5)
    p.box(158, 98, 194, 132, FACE, INK, 0.9)
    text(img, "JOD", 176, 112, 13, INK, 0, "OldStandard-Bold.ttf")
    p.line([(162, 124), (190, 124)], RED, 1.6)
    ribbon = [(96, 138), (150, 150), (196, 164), (228, 160), (230, 170), (194, 176), (144, 164), (92, 150)]  # loses Ende der Binde
    p.poly(ribbon, CLOTH)
    p.line([(100, 144), (150, 157), (196, 170), (226, 166)], CLOTH_D, 0.8)
    p.box(52, 86, 124, 140, CLOTH, None)  # Rolle Mullbinde
    p.box(52, 122, 124, 140, CLOTH_D, None)
    p.box(52, 90, 124, 100, light(CLOTH, 0.5), None)
    for x in range(60, 124, 7):
        p.line([(x, 87), (x, 139)], CLOTH_D, 0.6)
    for y in range(94, 140, 9):
        p.line([(54, y), (124, y)], CLOTH_D, 0.5)
    p.line([(52, 86), (124, 86)], INK, 1.3)
    p.line([(52, 140), (124, 140)], INK, 1.3)
    p.arc(124, 113, 14, 27, -90, 90, INK, 1.3)
    p.oval(52, 113, 15, 27, CLOTH)
    for k in (0.78, 0.56, 0.34):
        p.oval(52, 113, 15 * k, 27 * k, None, CLOTH_D, 0.8)
    p.oval(52, 113, 2.6, 4.6, CLOTH_D, INK, 0.7)
    p.arc(108, 160, 9, 4, 150, 380, INK, 1.2)  # Sicherheitsnadel
    p.line([(100, 162), (122, 158)], STEEL_L, 1.2)


# ---------- Ausbauten der Agenten ----------

def strap(p, a, b, width=9.0, col=LEATHER, buckle=None):
    """Lederriemen von a nach b, mit Naht und auf Wunsch einer Schnalle (Anteil der Strecke)."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.hypot(dx, dy)
    nx, ny = -dy / length * width / 2, dx / length * width / 2
    p.poly([(a[0] + nx, a[1] + ny), (b[0] + nx, b[1] + ny), (b[0] - nx, b[1] - ny), (a[0] - nx, a[1] - ny)], col, INK, 1.1)
    p.dashes([(a[0] + nx * 0.55, a[1] + ny * 0.55), (b[0] + nx * 0.55, b[1] + ny * 0.55)], light(col, 0.3), 0.6, 2.0, 2.0)
    if buckle is not None:
        cx, cy = a[0] + dx * buckle, a[1] + dy * buckle
        q = p.at(cx, cy, -math.degrees(math.atan2(dy, dx)))
        q.box(-4, -width / 2 - 2, 4, width / 2 + 2, None, INK, 2.6, 1)
        q.box(-4, -width / 2 - 2, 4, width / 2 + 2, None, BRASS_L, 1.2, 1)
        q.line([(-4, 0), (4, 0)], STEEL_L, 1.0)


def draw_beine(p, img, level):
    """Bein von der Seite, Fuß nach links. Stufe 1 Lederschiene, Stufe 2 Federwerk aus Messing, Stufe 3 Kolbenbein."""
    p = p.at(-4, 2)
    thigh = [(100, 6), (164, 6), (154, 70), (112, 72)]
    shin = [(114, 70), (152, 70), (143, 150), (120, 150)]
    boot = [(117, 140), (146, 140), (148, 166), (152, 176), (82, 176), (80, 167), (96, 160), (116, 156)]
    p.poly(thigh, TROUSERS)
    p.poly([(106, 8), (120, 8), (124, 68), (116, 70)], light(TROUSERS, 0.2), None)
    if level < 3:
        p.poly(shin, TROUSERS)
        p.poly([(118, 72), (128, 72), (128, 148), (123, 148)], light(TROUSERS, 0.2), None)
        p.poly(boot, LEATHER_D)
        p.poly([(86, 168), (100, 162), (118, 160), (118, 166), (90, 172)], light(LEATHER_D, 0.25), None)
        for y in (146, 152, 158):
            p.line([(119, y), (128, y - 1)], LEATHER_L, 0.9)
        p.box(80, 176, 152, 182, dark(LEATHER_D, 0.5), r=1)
        p.box(138, 176, 152, 186, dark(LEATHER_D, 0.5), r=1)
    else:
        # Kolbenbein: Schienbein als genietete Messingschale, Fuß aus Stahl mit Blattfeder
        p.poly([(98, 178), (150, 176), (154, 182), (94, 185)], STEEL_D)  # Blattfeder
        p.poly([(80, 176), (120, 160), (148, 160), (152, 176), (84, 180)], STEEL)
        p.poly([(86, 174), (120, 162), (130, 162), (96, 176)], STEEL_L, None)
        p.poly([(80, 176), (96, 169), (98, 179), (84, 180)], BRASS, INK, 1.0)  # Zehenkappe
        p.oval(134, 158, 8, 8, BRASS)  # Knöchelgelenk
        p.oval(134, 158, 3, 3, STEEL_D, INK, 0.8)
        p.poly([(112, 74), (150, 74), (144, 150), (122, 152)], BRASS)
        p.poly([(116, 78), (126, 78), (130, 148), (125, 149)], BRASS_L, None)
        p.poly([(140, 78), (149, 78), (143, 148), (138, 148)], BRASS_D, None)
        p.line([(113, 100), (148, 100)], INK, 0.9)
        p.line([(117, 126), (146, 126)], INK, 0.9)
        p.rivets([(117, 80), (146, 80), (118, 95), (146, 95), (120, 106), (145, 106), (121, 121), (144, 121), (123, 132), (143, 132), (124, 146), (142, 146)], BRASS_L, 1.4)
    # Schiene: zwei Stahlstäbe mit Kniegelenk, Ledermanschetten
    bar_col = STEEL_L if level == 1 else BRASS
    if level < 3:
        p.poly([(131, 22), (137, 22), (136, 72), (130, 72)], bar_col, INK, 1.0)
        p.poly([(130, 72), (136, 72), (134, 160), (129, 160)], bar_col, INK, 1.0)
        p.line([(132, 160), (132, 178), (112, 180)], INK, 3.2)  # Steigbügel
        p.line([(132, 160), (132, 178), (112, 180)], bar_col, 1.6)
        strap(p, (104, 34), (162, 30), 12, LEATHER, 0.3)
        strap(p, (113, 96), (152, 94), 10, LEATHER, 0.3)
        strap(p, (118, 134), (147, 132), 9, LEATHER, 0.3)
    else:
        strap(p, (102, 26), (163, 22), 12, LEATHER, 0.25)
        strap(p, (106, 52), (158, 50), 11, LEATHER, 0.25)
        p.poly([(130, 14), (138, 14), (137, 72), (129, 72)], BRASS, INK, 1.0)
    if level == 1:
        p.oval(133, 72, 7.5, 7.5, STEEL)
        p.oval(133, 72, 2.4, 2.4, STEEL_D, INK, 0.8)
        p.poly([(110, 62), (126, 60), (128, 84), (112, 84)], LEATHER_L, INK, 1.0)  # Kniekappe aus Leder
        p.dashes([(113, 65), (124, 63), (125, 81), (114, 81), (113, 65)], LEATHER_D, 0.6, 2, 2)
    if level >= 2:
        # Federwerk hinter der Wade: Schraubenfeder zwischen Kniehebel und Ferse
        top, foot = (174, 76), (172, 154) if level == 2 else (164, 150)
        if level == 2:
            p.line([(134, 72), top], INK, 4.4)
            p.line([(134, 72), top], BRASS, 2.6)
            p.line([(146, 160), foot], INK, 4.4)
            p.line([(146, 160), foot], BRASS, 2.6)
            coil = []
            for i in range(19):
                t = i / 18
                coil.append((top[0] + (foot[0] - top[0]) * t + (12 if i % 2 else -12) * (0 < i < 18), top[1] + 6 + (foot[1] - top[1] - 12) * t))
            p.line([top] + coil + [foot], INK, 5.2)
            p.line([top] + coil + [foot], STEEL_L, 3.0)
            p.oval(top[0], top[1], 3.4, 3.4, BRASS_L, INK, 0.9)
            p.oval(foot[0], foot[1], 3.4, 3.4, BRASS_L, INK, 0.9)
            p.poly([(108, 60), (127, 58), (130, 86), (110, 86)], BRASS, INK, 1.1)  # Kniekappe aus Messing
            p.poly([(111, 62), (117, 61), (119, 84), (113, 84)], BRASS_L, None)
            p.rivets([(112, 64), (124, 62), (114, 82), (126, 82)], BRASS_L, 1.3)
            p.poly([(106, 90), (122, 89), (125, 148), (114, 148)], BRASS, INK, 1.1)  # Schienbeinplatte
            p.poly([(109, 92), (114, 92), (118, 146), (115, 146)], BRASS_L, None)
            p.gear(134, 72, 17, 10, BRASS, 1.2)
        else:
            # Druckkessel am Oberschenkel, Rohr, Arbeitszylinder mit Kolbenstange zur Ferse
            pipe = bezier((186, 48), (196, 62), (176, 66), (168, 80), 16)
            p.line(pipe, INK, 4.6)
            p.line(pipe, BRASS, 2.8)
            p.bar(158, 18, 206, 46, BRASS_D, 13)
            p.line([(170, 19), (170, 45)], INK, 0.8)
            p.line([(194, 19), (194, 45)], INK, 0.8)
            p.rivets([(166, 24), (166, 40), (198, 24), (198, 40)], BRASS_L, 1.3)
            p.bar(178, 10, 186, 19, STEEL, 1)  # Ventil
            p.line([(174, 9), (190, 9)], INK, 3.4)
            p.line([(174, 9), (190, 9)], RED, 1.8)
            p.gauge(212, 30, 11, 150)
            p.line([(165, 128), (150, 162)], INK, 4.2)  # Kolbenstange
            p.line([(165, 128), (150, 162)], STEEL_L, 2.4)
            p.bar(157, 78, 173, 130, STEEL, 3, upright=True)
            p.bar(155, 76, 175, 84, BRASS, 2)
            p.bar(155, 124, 175, 132, BRASS, 2)
            p.oval(150, 162, 3.6, 3.6, BRASS_L, INK, 0.9)
            p.line([(137, 104), (157, 104)], INK, 3.6)
            p.line([(137, 104), (157, 104)], BRASS, 2.0)
            p.gear(133, 72, 15, 11, BRASS, 1.2)
            p.oval(133, 72, 4, 4, STEEL_D, INK, 0.8)
            p.poly([(104, 56), (124, 54), (128, 90), (108, 92)], BRASS, INK, 1.1)  # Kniepanzer
            p.poly([(108, 58), (114, 57), (117, 90), (111, 90)], BRASS_L, None)
            p.rivets([(109, 60), (121, 58), (112, 88), (124, 87)], BRASS_L, 1.4)


def draw_arme(p, img, level):
    """Unterarm und Hand von der Seite, Hand nach links. Stufe 1 Lederschiene, Stufe 2 Federzug mit Handgelenk, Stufe 3 Messingarm mit Stütze und Uhrwerk."""
    skin, skin_l, skin_d = (198, 164, 134), (222, 192, 162), (150, 116, 92)
    p = p.at(128, 98, -7).at(-128, -100)
    # Hemdsärmel, aufgekrempelt
    p.poly([(196, 70), (262, 62), (262, 140), (198, 130)], SHIRT)
    p.poly([(188, 68), (204, 68), (206, 132), (190, 132)], light(SHIRT, 0.35), INK, 1.1)
    p.line([(214, 72), (216, 128)], dark(SHIRT, 0.3), 0.9)
    p.line([(236, 68), (238, 134)], dark(SHIRT, 0.3), 0.9)
    arm = [(92, 86), (190, 72), (190, 128), (92, 114)]
    hand = [(92, 86), (70, 84), (50, 88), (30, 92), (24, 98), (30, 104), (52, 104), (56, 110), (36, 112), (32, 118), (40, 122), (66, 120), (92, 114)]
    thumb = [(84, 86), (72, 70), (62, 64), (56, 68), (62, 76), (68, 88)]
    if level < 3:
        p.poly(arm, skin)
        p.poly([(96, 88), (188, 76), (188, 86), (96, 95)], skin_l, None)
        p.poly([(96, 106), (188, 118), (188, 126), (96, 112)], skin_d, None)
        p.poly(arm, None)
        p.poly(thumb, skin)
        p.poly(hand, skin)
        p.poly([(34, 94), (70, 87), (88, 89), (88, 94), (40, 99)], skin_l, None)
        p.line([(30, 104), (52, 104)], INK, 0.9)
        p.line([(52, 104), (62, 100)], INK, 0.8)
    if level == 1:
        # Lederschiene: Manschette über den halben Unterarm, drei Riemen, oben ein Stahlstab
        p.poly([(104, 82), (182, 71), (182, 129), (104, 116)], LEATHER)
        p.poly([(108, 85), (178, 75), (178, 86), (108, 94)], LEATHER_L, None)
        p.poly([(104, 82), (182, 71), (182, 129), (104, 116)], None)
        p.dashes([(108, 86), (178, 76), (178, 124), (108, 112), (108, 86)], LEATHER_D, 0.8, 2.4, 2.4)
        for x in (118, 143, 168):
            strap(p, (x, 68 + (182 - x) * 0.13), (x, 131 - (182 - x) * 0.15), 9, LEATHER_D, 0.3)
        p.poly([(98, 78), (186, 66), (186, 72), (98, 84)], STEEL_L, INK, 1.1)
        p.rivets([(104, 80), (182, 69)], STEEL, 1.8)
    elif level == 2:
        # Federzug: kürzere Manschette, darüber eine starke Schraubenfeder vom Ellbogen zum Gelenk am Handgelenk, Zugdraht zu den Fingern
        p.poly([(124, 79), (184, 71), (184, 129), (124, 119)], LEATHER)
        p.poly([(128, 82), (180, 75), (180, 86), (128, 92)], LEATHER_L, None)
        p.poly([(124, 79), (184, 71), (184, 129), (124, 119)], None)
        for x in (138, 168):
            strap(p, (x, 68 + (184 - x) * 0.13), (x, 131 - (184 - x) * 0.16), 10, LEATHER_D, 0.3)
        p.poly([(60, 84), (92, 82), (94, 118), (64, 118)], BRASS, INK, 1.1)  # Handrückenplatte
        p.poly([(64, 87), (90, 85), (90, 92), (66, 94)], BRASS_L, None)
        p.rivets([(66, 90), (88, 88), (68, 113), (88, 113)], BRASS_L, 1.4)
        p.line([(40, 96), (62, 90)], INK, 2.4)  # Zugdraht
        p.line([(40, 96), (62, 90)], STEEL_L, 1.0)
        p.bar(174, 50, 190, 74, BRASS_D, 3, upright=True)  # Federbock am Ellbogen
        a, b = (176, 58), (104, 66)
        coil = [(a[0] + (b[0] - a[0]) * i / 16, a[1] + (b[1] - a[1]) * i / 16 + (9 if i % 2 else -9) * (0 < i < 16)) for i in range(17)]
        p.line(coil, INK, 5.0)
        p.line(coil, STEEL_L, 2.8)
        p.gear(98, 96, 20, 10, BRASS, 1.3)  # Gelenk am Handgelenk
        p.oval(98, 96, 5, 5, STEEL_D, INK, 0.9)
        p.line([(104, 66), (100, 82)], INK, 4.4)
        p.line([(104, 66), (100, 82)], BRASS, 2.6)
    else:
        # Messingarm: genietete Schale, Fingerplatten, oben das Uhrwerk, unten die ausklappbare Stütze
        strut = [(186, 132), (120, 166), (60, 126)]
        p.line(strut, INK, 6.4)
        p.line(strut, STEEL_L, 4.0)
        p.oval(120, 166, 6, 6, BRASS, INK, 1.1)
        p.poly([(110, 168), (130, 168), (136, 180), (104, 180)], STEEL_D, INK, 1.1)  # Auflage
        p.poly(thumb, BRASS_D)
        p.poly(hand, BRASS)
        p.poly([(34, 94), (70, 87), (88, 89), (88, 94), (40, 99)], BRASS_L, None)
        for x in (44, 58, 72):
            p.line([(x, 89 - (x - 44) * 0.08), (x + 2, 104)], INK, 0.9)
        p.line([(30, 104), (52, 104)], INK, 0.9)
        p.oval(60, 126, 4.5, 4.5, STEEL, INK, 0.9)
        p.poly(arm, BRASS)
        p.poly([(96, 88), (188, 75), (188, 86), (96, 96)], BRASS_L, None)
        p.poly([(96, 105), (188, 117), (188, 127), (96, 112)], BRASS_D, None)
        p.poly(arm, None)
        for x in (124, 158):
            p.line([(x, 82 - (x - 92) * 0.14), (x, 114 + (x - 92) * 0.14)], INK, 0.9)
        p.rivets([(x, 84 - (x - 92) * 0.14 + 5) for x in range(100, 188, 12)] + [(x, 113 + (x - 92) * 0.14 - 5) for x in range(100, 188, 12)], BRASS_L, 1.4)
        p.oval(92, 100, 15, 17, BRASS_D)  # Handgelenk
        p.oval(92, 100, 6, 7, STEEL_D, INK, 0.9)
        p.bar(182, 64, 196, 136, BRASS_D, 3, upright=True)  # Ellbogenmanschette
        # Uhrwerk: große Dose auf dem Arm mit zwei Zahnrädern und Aufziehschlüssel
        p.gear(166, 56, 17, 11, BRASS, 1.2)
        p.box(112, 44, 164, 82, BRASS, r=6)
        p.box(116, 48, 126, 78, BRASS_L, None, r=3)
        p.box(152, 50, 164, 78, BRASS_D, None, r=4)
        p.box(112, 44, 164, 82, None, INK, 1.3, 6)
        p.oval(138, 63, 15, 15, dark(BRASS_D, 0.45), INK, 1.0)  # Fenster ins Werk
        p.gear(133, 60, 8, 8, BRASS_L, 0.9)
        p.gear(145, 68, 6, 7, STEEL_L, 0.9)
        p.rivets([(117, 49), (159, 49), (117, 77), (159, 77)], BRASS_L, 1.4)
        p.line([(138, 44), (138, 32)], INK, 3.6)
        p.line([(138, 44), (138, 32)], STEEL_L, 2.0)
        p.poly([(138, 32), (126, 26), (124, 18), (134, 20), (138, 26), (142, 20), (152, 18), (150, 26)], STEEL_L, INK, 1.1)  # Flügelschlüssel


def draw_herz(p, img, level):
    """Brust von vorn mit Riemengeschirr. Stufe 1 Uhrwerk, Stufe 2 Pumpe mit Druckmesser, Stufe 3 Zwillingspumpe mit Schwungrad."""
    p = p.at(-38, -40, 0, 1.3)  # nah heran: Der Apparat soll die Karte füllen
    torso = [(26, 192), (36, 74), (62, 40), (104, 26), (152, 26), (194, 40), (220, 74), (230, 192)]
    p.poly(torso, SHIRT, INK, 1.2)
    p.poly([(104, 26), (152, 26), (146, 0), (110, 0)], (196, 164, 136), INK, 1.2)  # Hals
    p.poly([(104, 26), (128, 52), (152, 26), (140, 22), (128, 40), (116, 22)], light(SHIRT, 0.4), INK, 0.9)  # Kragen
    p.line([(128, 52), (128, 192)], dark(SHIRT, 0.3), 0.9)
    for y in (70, 96, 122, 148, 174):
        p.oval(128, y, 1.6, 1.6, CLOTH_D, INK, 0.5)
    p.line([(60, 60), (52, 192)], dark(SHIRT, 0.25), 0.8)
    p.line([(196, 60), (204, 192)], dark(SHIRT, 0.25), 0.8)
    if level >= 2:  # Schläuche über die Schulter, hinter den Riemen
        tube(p, bezier((150, 84), (176, 60), (186, 44), (190, 20)), RUBBER, 5.0)
        tube(p, bezier((104, 88), (84, 66), (74, 46), (72, 22)), (96, 40, 30), 5.0)
    if level >= 3:
        tube(p, bezier((128, 130), (132, 156), (170, 160), (186, 192)), RUBBER, 5.0)
        p.poly([(40, 60), (66, 38), (98, 34), (92, 56), (58, 76)], BRASS, INK, 1.1)  # Schulterplatte
        p.poly([(48, 60), (68, 44), (88, 40), (86, 46), (60, 62)], BRASS_L, None)
        p.rivets([(48, 64), (66, 44), (90, 40), (86, 54)], BRASS_L, 1.4)
    strap(p, (64, 42), (200, 170), 13, LEATHER, 0.2)
    strap(p, (192, 42), (56, 170), 13, LEATHER, 0.82)
    strap(p, (32, 118), (224, 118), 12, LEATHER_D, 0.9)
    if level == 1:
        # Uhrwerk: runde Dose wie eine große Taschenuhr, Zifferblatt, Aufziehschlüssel
        p.oval(126, 106, 23, 23, BRASS)
        p.arc(126, 106, 20, 20, 150, 250, BRASS_L, 2.4)
        p.arc(126, 106, 20, 20, -30, 70, BRASS_D, 2.4)
        p.oval(126, 106, 16, 16, FACE, INK, 1.0)
        for a in range(0, 360, 30):
            ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
            p.line([(126 + ca * 12, 106 + sa * 12), (126 + ca * 15, 106 + sa * 15)], INK, 0.7)
        p.line([(126, 106), (126, 94)], INK, 1.2)
        p.line([(126, 106), (134, 111)], INK, 1.2)
        p.oval(126, 106, 1.6, 1.6, INK, None)
        p.line([(149, 106), (160, 106)], INK, 3.2)
        p.line([(149, 106), (160, 106)], STEEL_L, 1.6)
        p.poly([(160, 106), (166, 96), (174, 98), (172, 106), (174, 114), (166, 116)], STEEL_L, INK, 1.1)  # Flügelschlüssel
    elif level == 2:
        # Pumpe: größeres Gehäuse, Druckmesser, Handrad, zwei Anschlüsse
        p.bar(98, 78, 110, 96, STEEL, 2, upright=True)
        p.bar(144, 74, 156, 92, STEEL, 2, upright=True)
        p.box(92, 86, 162, 136, BRASS, r=7)
        p.box(96, 90, 106, 132, BRASS_L, None, r=4)
        p.box(148, 92, 162, 132, BRASS_D, None, r=5)
        p.box(92, 86, 162, 136, None, INK, 1.3, 7)
        p.line([(92, 124), (162, 124)], INK, 0.8)
        p.rivets([(98, 92), (156, 92), (98, 130), (112, 130), (126, 130), (142, 130), (156, 130)], BRASS_L, 1.4)
        p.gauge(120, 106, 14, 80)
        p.wheel(176, 112, 17, STEEL, 5, 20)
        p.bar(56, 98, 94, 124, STEEL, 3)  # liegender Pumpenzylinder
        p.bar(52, 95, 62, 127, BRASS_D, 2, upright=True)
        p.bar(86, 95, 94, 127, BRASS_D, 2, upright=True)
        p.box(140, 98, 152, 104, dark(BRASS_D, 0.4), INK, 0.8)  # Schauglas
    else:
        # Zwillingspumpe: zwei Zylinder, Schwungrad, großer Druckmesser, Ölglas
        p.box(74, 80, 180, 146, BRASS, r=8)
        p.box(78, 84, 90, 142, BRASS_L, None, r=4)
        p.box(164, 86, 180, 142, BRASS_D, None, r=6)
        p.box(74, 80, 180, 146, None, INK, 1.3, 8)
        p.line([(74, 132), (180, 132)], INK, 0.8)
        p.rivets([(80, 86), (174, 86)] + [(x, 139) for x in range(82, 176, 13)], BRASS_L, 1.4)
        for x in (84, 104):  # Zylinder mit Kolbenstangen
            p.line([(x + 8, 70), (x + 8, 82)], INK, 3.6)
            p.line([(x + 8, 70), (x + 8, 82)], STEEL_L, 2.0)
            p.bar(x, 82, x + 16, 128, STEEL, 3, upright=True)
            p.bar(x - 2, 80, x + 18, 88, BRASS_D, 2)
            p.bar(x - 2, 122, x + 18, 130, BRASS_D, 2)
        p.line([(86, 70), (118, 70)], INK, 4.0)  # Joch
        p.line([(86, 70), (118, 70)], STEEL, 2.4)
        p.gauge(146, 96, 16, 150)
        p.box(132, 116, 144, 130, (120, 140, 96), INK, 0.9, 2)  # Ölglas
        p.box(132, 123, 144, 130, (90, 84, 40), None)
        p.box(132, 116, 144, 130, None, INK, 0.9, 2)
        p.wheel(190, 122, 19, STEEL, 6, 12)
        p.oval(190, 122, 3, 3, BRASS_L, INK, 0.8)
        p.line([(122, 70), (176, 108)], INK, 3.0)  # Pleuel zum Schwungrad
        p.line([(122, 70), (176, 108)], STEEL_L, 1.6)
        p.oval(176, 108, 2.6, 2.6, BRASS_L, INK, 0.8)
        p.bar(152, 64, 164, 80, STEEL, 2, upright=True)  # Sicherheitsventil
        p.line([(148, 63), (168, 63)], INK, 3.4)
        p.line([(148, 63), (168, 63)], RED, 1.8)


# ---------- Karte, Papier, Ausgabe ----------

def card(draw, seed, number=None, stamp=None, stamp_at=(W - 32, H - 32)):
    """Zeichnet einen Gegenstand und legt ihn auf eine vergilbte Karteikarte: Schlagschatten, Flecken, Korn, Rand."""
    layer = Image.new("RGBA", (W * SUPER, H * SUPER), (0, 0, 0, 0))
    draw(Pen(layer), layer)
    obj = layer.resize((W, H), Image.LANCZOS)
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    paper = np.empty((H, W, 3), np.float32)
    paper[:] = PAPER
    # Wolkige Vergilbung und ein, zwei Flecken
    cloud = np.array(Image.fromarray((rng.random((H // 16 + 2, W // 16 + 2)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), np.float32) / 255
    paper *= (0.9 + 0.14 * cloud)[..., None]
    for _ in range(2):
        cx, cy, r = rng.uniform(20, W - 20), rng.uniform(20, H - 20), rng.uniform(18, 40)
        ring = np.exp(-((np.hypot(xx - cx, yy - cy) - r) / 2.2) ** 2) * 0.07 + (np.hypot(xx - cx, yy - cy) < r) * 0.035
        paper *= (1 - ring)[..., None] * np.array((1.0, 0.985, 0.95), np.float32)
    for y in range(28, H, 22):  # blasse Linien des Vordrucks
        paper[y] *= 0.955
    edge = np.minimum.reduce([xx, yy, W - 1 - xx, H - 1 - yy])
    paper *= (0.74 + 0.26 * np.clip(edge / 30, 0, 1))[..., None]
    alpha = np.array(obj.getchannel("A"), np.float32) / 255
    shadow = np.array(Image.fromarray((alpha * 255).astype(np.uint8)).transform((W, H), Image.AFFINE, (1, 0, -4, 0, 1, -5)).filter(ImageFilter.GaussianBlur(3.2)), np.float32) / 255
    paper *= (1 - 0.42 * shadow)[..., None]
    rgb = np.array(obj.convert("RGB"), np.float32)
    # Lavierung: Die Farbe liegt dünn auf dem Papier, das Korn scheint durch
    wash = rgb * (0.9 + 0.1 * paper / np.array(PAPER, np.float32))
    out = paper * (1 - alpha[..., None]) + wash * alpha[..., None]
    lum = out @ np.array((0.3, 0.59, 0.11), np.float32)
    sepia = np.array((34, 26, 20), np.float32) + (np.array((236, 220, 184), np.float32) - np.array((34, 26, 20), np.float32)) * (lum / 255)[..., None]
    out = out * 0.8 + sepia * 0.2
    out += rng.normal(0, 3.2, (H, W, 1)).astype(np.float32)
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).convert("RGBA")
    over = Image.new("RGBA", (W * SUPER, H * SUPER), (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    d.rectangle((3 * SUPER, 3 * SUPER, (W - 3) * SUPER - 1, (H - 3) * SUPER - 1), outline=INK + (150,), width=SUPER)
    if number:
        text(over, number, 9 + len(number) * 3.1, 13, 9.5, INK, 0, "CourierPrime-Regular.ttf", 0.7)
    if stamp:
        # Runder Stempel mit der Stufe als großer Ziffer: auch in 75 Bildpunkten Breite zu lesen
        cx, cy = stamp_at[0] * SUPER, stamp_at[1] * SUPER
        for r, width in ((23, 2.4), (19, 1.0)):
            d.ellipse((cx - r * SUPER, cy - r * SUPER, cx + r * SUPER, cy + r * SUPER), outline=RED + (215,), width=int(width * SUPER))
        text(over, stamp, stamp_at[0], stamp_at[1] - 1, 27, RED, 8, "OldStandard-Bold.ttf", 0.9)
    img.alpha_composite(over.resize((W, H), Image.LANCZOS))
    return img.convert("RGB")


ITEM_DRAWINGS = {
    "pistole": draw_pistole, "schrotflinte": draw_schrotflinte, "maschinenpistole": draw_maschinenpistole, "totschlaeger": draw_totschlaeger,
    "dietrich": draw_dietrich, "rauchtopf": draw_rauchtopf, "verbandszeug": draw_verbandszeug,
}
UPGRADE_DRAWINGS = {"legs": draw_beine, "arms": draw_arme, "heart": draw_herz}
ROMAN = ("I", "II", "III")


def sheet(entries, path):
    """Übersichtsblatt: jedes Bild mit Namen in Originalgröße, in halber und in viertel Größe."""
    cols, gap, label = 3, 14, 20
    cell_w, cell_h = W + 128 + 75 + gap * 3, H + label + gap
    rows = (len(entries) + cols - 1) // cols
    board = Image.new("RGB", (cols * cell_w + gap, rows * cell_h + gap + 30), (173, 158, 122))  # Papierfarbe des HUD
    d = ImageDraw.Draw(board)
    f = ImageFont.truetype(str(FONTS / "CourierPrime-Bold.ttf"), 14)
    d.text((gap, 8), "Akte: Waffenkammer und Ausbauten. Je Bild 256 x 192, daneben 128 x 96 und 75 x 56", font=f, fill=INK)
    for i, (name, img) in enumerate(entries):
        x, y = gap + (i % cols) * cell_w, 30 + gap + (i // cols) * cell_h
        d.text((x, y), name, font=f, fill=INK)
        board.paste(img, (x, y + label))
        board.paste(img.resize((128, 96), Image.LANCZOS), (x + W + gap, y + label))
        board.paste(img.resize((75, 56), Image.LANCZOS), (x + W + gap * 2 + 128, y + label))
    board.save(path)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    items = json.loads((ROOT / "data" / "waffen.json").read_text(encoding="utf-8"))["items"]
    missing = [i["id"] for i in items if i["id"] not in ITEM_DRAWINGS]
    if missing:
        sys.exit(f"Für diese Gegenstände aus data/waffen.json fehlt eine Zeichnung: {missing}")
    mapping = {"size": [W, H], "items": {}, "upgrades": {}}
    entries = []
    for n, item in enumerate(items):
        name = f"gegenstand_{item['id']}.png"
        img = card(ITEM_DRAWINGS[item["id"]], 1927 + n, f"Bl. {n + 1}")
        img.save(OUT / name)
        mapping["items"][item["id"]] = name
        entries.append((name, img))
    for u, (key, word) in enumerate(UPGRADES.items()):
        mapping["upgrades"][key] = {}
        for level in range(1, UPGRADE_LEVELS + 1):
            name = f"ausbau_{word}_{level}.png"
            img = card(lambda p, layer, k=key, lv=level: UPGRADE_DRAWINGS[k](p, layer, lv), 2700 + u * 10 + level, None, ROMAN[level - 1],
                       (32, 32) if key == "heart" else (W - 32, H - 32))
            img.save(OUT / name)
            mapping["upgrades"][key][str(level)] = name
            entries.append((name, img))
    (OUT / "zuordnung.json").write_text(json.dumps(mapping, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{len(entries)} Bilder nach {OUT}")
    target = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--sheet=")), None)
    if target:
        sheet(entries, target)
        print("Übersichtsblatt:", target)


if __name__ == "__main__":
    main()
