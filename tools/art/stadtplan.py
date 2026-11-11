"""Zeichnet den Stadtplan zur Einsatzwahl: Messingstadt von oben als gedruckter Plan von 1927 auf altem Papier.

Aufruf (aus dem Projektordner):
    python tools/art/stadtplan.py [vorschau.png]

Schreibt nach game/assets/stadtplan/: plan.png (Grundkarte), viertel_1.png bis viertel_5.png (Masken, weiß mit
Deckkraft, deckungsgleich) und plan.json (Größe, Kartenfeld, je Viertel Ankerpunkte und Umriss). Mit einem Dateinamen
entsteht zusätzlich eine Vorschau mit zwei Beispielfarben über den Masken.

Die Stadt ist frei erfunden. Fluss, Bahn und eine Hauptstraße teilen sie in fünf Viertel:
1 Kesselviertel (Brandt-Werke, Viertel des ersten Einsatzes, Linie 7), 2 Nordwall (alter Wall mit Graben), 3 Gleisviertel
(Güterbahnhof Ost), 4 Kontorviertel (Banken und Geschäftshäuser am Hafen), 5 Altstadt (enge Gassen). Die Namen vergibt die Story.
"""
import json
import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

import roofs as R

OUT = Path(__file__).resolve().parents[2] / "game" / "assets" / "stadtplan"
W, H = 1280, 900
S = 2  # gezeichnet wird doppelt so groß und dann verkleinert, das glättet alle Linien
FRAME = (46, 46, 1234, 854)
INK = (33, 28, 26)  # Tinte und Papier wie in der Menüführung: #211C1A auf #AD9E7A
PAPER = (173, 158, 122)
WATER = (120, 132, 128)
TRAM = (134, 44, 36)
PENCIL = (40, 40, 46)
RNG = random.Random(1927)

RIVER = [(418, 46), (436, 150), (486, 262), (566, 356), (676, 430), (800, 474), (948, 508), (1100, 560), (1234, 612)]
RAIL_WEST = [(46, 566), (190, 556), (330, 520), (470, 440), (566, 356)]
RAIL_EAST = [(566, 356), (700, 290), (860, 232), (1040, 186), (1234, 150)]
MAIN = [(800, 474), (792, 600), (772, 730), (762, 854)]
TRAM_LINE = [(46, 300), (160, 318), (290, 300), (400, 262), (486, 262), (600, 214), (760, 150), (900, 110), (1040, 104)]
LABELS = {1: ((800, 112), (640, 176)), 2: ((214, 196), (300, 440)), 3: ((1040, 424), (1150, 510)), 4: ((1010, 704), (1130, 790)), 5: ((420, 660), (610, 790))}
# Je Viertel: Winkel des Straßenrasters, Blockmaß längs und quer, Straßenbreite, jede wievielte Straße breit ist, Art
STYLE = {
    1: (-0.36, 92, 60, 6.5, 3, "works"),
    2: (0.22, 46, 34, 5.0, 4, "mixed"),
    3: (-0.30, 74, 40, 5.5, 4, "sheds"),
    4: (0.30, 56, 42, 6.5, 3, "banks"),
    5: (-0.08, 29, 22, 3.4, 7, "oldtown"),
}


def spline(points, steps=14):
    """Glatte Kurve durch die Punkte (Catmull-Rom)."""
    p = [points[0]] + list(points) + [points[-1]]
    out = []
    for i in range(1, len(p) - 2):
        for j in range(steps):
            t = j / steps
            out.append(tuple(0.5 * ((2 * p[i][k]) + (-p[i - 1][k] + p[i + 1][k]) * t
                                    + (2 * p[i - 1][k] - 5 * p[i][k] + 4 * p[i + 1][k] - p[i + 2][k]) * t * t
                                    + (-p[i - 1][k] + 3 * p[i][k] - 3 * p[i + 1][k] + p[i + 2][k]) * t ** 3) for k in (0, 1)))
    out.append(tuple(points[-1]))
    return out


def scaled(points):
    return [(x * S, y * S) for x, y in points]


def stroke(draw, points, width, fill):
    pts = scaled(points)
    draw.line(pts, fill=fill, width=int(width * S), joint="curve")
    r = width * S / 2
    for x, y in (pts[0], pts[-1]):
        draw.ellipse((x - r, y - r, x + r, y + r), fill=fill)


def dashed(draw, points, width, fill, dash, gap):
    run, on, last = 0.0, True, points[0]
    seg = [last]
    for p in points[1:]:
        d = math.dist(last, p)
        while run + d >= (dash if on else gap):
            t = ((dash if on else gap) - run) / d
            mid = (last[0] + (p[0] - last[0]) * t, last[1] + (p[1] - last[1]) * t)
            if on:
                draw.line(scaled(seg + [mid]), fill=fill, width=int(width * S))
            seg, last, d, run, on = [mid], mid, math.dist(mid, p), 0.0, not on
        run += d
        seg.append(p)
        last = p
    if on and len(seg) > 1:
        draw.line(scaled(seg), fill=fill, width=int(width * S))


def districts():
    """Umrisse der fünf Viertel. Nachbarn teilen sich dieselben Kurvenpunkte, die Masken stoßen lückenlos aneinander."""
    x0, y0, x1, y1 = FRAME
    river, west, east, main = spline(RIVER), spline(RAIL_WEST), spline(RAIL_EAST), spline(MAIN)
    upper = river[:river.index((566, 356)) + 1]
    middle = river[river.index((566, 356)):river.index((800, 474)) + 1]
    lower = river[river.index((800, 474)):]
    return {
        1: upper[::-1] + [(x1, y0)] + east[::-1],
        2: [(x0, y0)] + upper + west[::-1],
        3: east + lower[::-1] + middle[::-1],
        4: lower + [(x1, y1)] + main[::-1],
        5: west + middle + main + [(x0, y1)],
    }


def paper(rng):
    """Vergilbtes, fleckiges Papier: Wolken, Fasern, Stockflecken, zum Rand hin dunkler."""
    ys, xs = np.mgrid[0:H * S, 0:W * S] / S
    tone = 0.86 + 0.2 * R.noise(xs / 190, ys / 190, 3) + 0.1 * R.noise(xs / 46, ys / 46, 4) + 0.05 * R.noise(xs / 7, ys / 7, 5)
    tone += 0.04 * (R.hash01(np.floor(xs * S), np.floor(ys * S), 6) - 0.5)
    fibre = R.noise(xs / 2.2, ys / 60, 7) > 0.8
    tone -= 0.03 * fibre
    edge = np.minimum.reduce([xs, ys, W - xs, H - ys])
    tone *= 0.62 + 0.38 * np.clip(edge / 90, 0, 1) ** 0.6
    img = np.array(PAPER, np.float64)[None, None, :] * tone[..., None]
    damp = np.clip(R.noise(xs / 130, ys / 110, 8) * 2.4 - 1.35, 0, 1)  # alte Wasserflecken, bräunlich mit dunklem Rand
    ring = np.clip(1 - np.abs(damp - 0.12) / 0.07, 0, 1) * (damp > 0.02)
    img = img * (1 - 0.16 * damp[..., None]) * (1 - 0.14 * ring[..., None])
    img[..., 2] *= 1 - 0.1 * damp
    for _ in range(170):  # Stockflecken
        cx, cy, r = rng.uniform(0, W), rng.uniform(0, H), rng.uniform(1.5, 6)
        spot = np.clip(1 - np.hypot(xs - cx, ys - cy) / r, 0, 1) ** 1.5
        img = img * (1 - 0.3 * spot[..., None]) + np.array((96, 62, 30.0))[None, None, :] * (0.3 * spot[..., None])
    return img, xs, ys


def street_grid(xs, ys, zone, river_gap):
    """Straßenraster je Viertel als Druckfarbe zwischen 0 (Papier) und 1 (volle Farbe), dazu die Parkflächen."""
    ink = np.zeros(xs.shape)
    park = np.zeros(xs.shape, bool)
    wx = (R.noise(xs / 150, ys / 150, 21) - 0.5) * 34
    wy = (R.noise(xs / 150, ys / 150, 22) - 0.5) * 34
    for d, (angle, bw, bh, street, wide, kind) in STYLE.items():
        m = zone == d
        ca, sa = math.cos(angle), math.sin(angle)
        ax, ay = wx, wy
        if kind == "oldtown":
            # Gewachsene Gassen: Das Raster ist stark verbogen und in sich verdreht
            ax = wx * 2.2 + (R.noise(xs / 46, ys / 46, 23) - 0.5) * 36
            ay = wy * 2.2 + (R.noise(xs / 46, ys / 46, 24) - 0.5) * 36
        u = (xs + ax) * ca + (ys + ay) * sa
        v = -(xs + ax) * sa + (ys + ay) * ca
        col, row = np.floor(u / bw), np.floor(v / bh)
        fu, fv = u - col * bw, v - row * bh
        su = np.where(col % wide == 0, street * 1.9, street)
        sv = np.where(row % wide == 0, street * 1.9, street)
        block = (fu > su) & (fv > sv)
        pick = R.hash01(col, row, 30 + d)
        inner_u, inner_v = fu - su, fv - sv
        bu, bv = bw - su, bh - sv
        rim = np.minimum.reduce([inner_u, inner_v, bu - inner_u, bv - inner_v])
        hatch = (np.floor((xs + ys) * S) % (3 * S) == 0)
        cross = (np.floor((xs - ys) * S) % (3 * S) == 0)
        line = rim < 0.9
        if kind == "oldtown":
            # Altstadt: kleine, fast volle Parzellen, kaum Höfe
            court = (rim > 5.5) & (pick > 0.5)
            fill = np.where(court, 0.0, np.where(hatch | cross, 0.82, 0.42))
            empty = pick > 0.965
        elif kind == "tenements":
            # Mietskasernen: Randbebauung dicht schraffiert, innen enge Höfe mit Quergebäuden
            court = (rim > 7.5) & (np.floor(inner_u / 9) % 2 == 0)
            fill = np.where(court, 0.0, np.where(hatch | cross, 0.78, 0.36))
            empty = pick > 0.95
        elif kind == "mixed":
            court = (rim > 8.5)
            fill = np.where(court, np.where(hatch & (pick > 0.5), 0.3, 0.0), np.where(hatch, 0.72, 0.3))
            empty = pick > 0.9
        elif kind == "banks":
            # Geschlossene, voll gedruckte Blöcke mit Lichthof: Banken, Kontore, Ämter
            court = (rim > 11) & (pick > 0.35)
            fill = np.where(court, 0.0, 0.8)
            empty = pick > 0.93
        elif kind == "works":
            # Werkshallen: große Körper mit Sheddach-Streifen, dazwischen offene Höfe
            hall = (rim > 3.5) & (np.floor(inner_u / 31) % 2 == 0) | ((rim > 3.5) & (pick < 0.45))
            fill = np.where(hall, np.where(np.floor(inner_v * S / 2.4) % (2 * S) == 0, 0.9, 0.62), 0.0)
            # Zwischen den Hallen stehen Mietshäuser, Bank und Hotel: geschlossene Blöcke mit Hof
            homes = (pick > 0.56) & (pick <= 0.9)
            fill = np.where(homes, np.where(rim > 8, 0.0, np.where(hatch, 0.74, 0.34)), fill)
            empty = pick > 0.9
            line = rim < 1.3
        else:
            # Schuppen und Lagerplätze zwischen den Gleisen
            shed = (np.floor(inner_v / 13) % 2 == 0) & (rim > 2.5)
            fill = np.where(shed, np.where(hatch, 0.85, 0.5), 0.0)
            empty = pick > 0.86
        fill = np.where(line, 0.95, fill)
        green = empty & (pick > 0.972) & block
        fill = np.where(empty & ~line, np.where(green, 0.0, 0.08 * (R.hash01(np.floor(xs / 3), np.floor(ys / 3), 9) > 0.7)), fill)
        ink = np.where(m & block & ~river_gap, fill, ink)
        park |= m & green & ~river_gap & (rim > 1.5)
    return ink, park


def write_plan(preview=None):
    rng = RNG
    base, xs, ys = paper(rng)
    areas = districts()
    zone_img = Image.new("L", (W * S, H * S), 0)
    zd = ImageDraw.Draw(zone_img)
    for d, poly in areas.items():
        zd.polygon(scaled(poly), fill=d)
    zone = np.asarray(zone_img)

    # Wasser, Bahn und Hauptstraße als Flächen, die das Raster freilassen
    river = spline(RIVER)
    basins = [[(1030, 548), (1022, 650), (1060, 654), (1070, 560)], [(1120, 580), (1112, 676), (1150, 680), (1160, 592)]]
    canal = spline([(486, 262), (380, 330), (250, 360), (140, 420), (46, 440)])
    water_img = Image.new("L", (W * S, H * S), 0)
    wd = ImageDraw.Draw(water_img)
    stroke(wd, river, 34, 255)
    stroke(wd, canal, 9, 255)
    for b in basins:
        wd.polygon(scaled(b), fill=255)
    water = (np.asarray(water_img) > 0) & (zone > 0)
    gap_img = Image.new("L", (W * S, H * S), 0)
    gd = ImageDraw.Draw(gap_img)
    stroke(gd, river, 46, 255)
    stroke(gd, canal, 17, 255)
    for b in basins:
        gd.polygon(scaled(b), fill=255)
        gd.line(scaled(b + b[:1]), fill=255, width=7 * S)
    rails = [spline(RAIL_WEST), spline(RAIL_EAST)]
    for line in rails:
        stroke(gd, line, 15, 255)
    stroke(gd, spline(MAIN), 13, 255)
    stroke(gd, spline(TRAM_LINE), 9, 255)
    stroke(gd, offset(canal, -15)[4:-2], 22, 255)
    stroke(gd, offset([q for q in river if q[0] >= 830], -42)[2:-3], 34, 255)
    gd.polygon(scaled([(1052, 196), (1234, 290), (1234, 384), (1090, 250)]), fill=255)
    yard = [(700, 300), (1010, 206), (1040, 268), (740, 372)]  # Güterbahnhof
    gd.polygon(scaled(yard), fill=255)
    title_box = (60, 762, 344, 842)
    gd.rectangle([v * S for v in title_box], fill=255)
    gap = np.asarray(gap_img) > 0

    ink, park = street_grid(xs, ys, zone, gap)
    # Ruhige Stellen für die Namen der Viertel: Dort ist der Druck blasser.
    for d, (label, _) in LABELS.items():
        calm = np.clip(1.25 - np.hypot((xs - label[0]) / 150, (ys - label[1]) / 34), 0, 1)
        ink *= 1 - 0.55 * np.minimum(calm * 1.6, 1)
    # Der Druck ist ungleichmäßig: mal satt, mal ausgelassen
    ink *= 0.72 + 0.4 * R.noise(xs / 60, ys / 60, 41)
    ink *= np.where(R.noise(xs / 1.3, ys / 1.3, 42) > 0.86, 0.55, 1.0)
    img = base.copy()
    img = np.where(park[..., None], img * np.array((0.8, 0.84, 0.72))[None, None, :], img)
    tree = park & (R.hash01(np.floor(xs / 4), np.floor(ys / 4), 43) > 0.6) & (np.hypot(xs % 4 - 2, ys % 4 - 2) < 1.1)
    ink = np.where(tree, 0.7, ink)
    wash = np.array(WATER, np.float64)[None, None, :] * (0.86 + 0.2 * R.noise(xs / 70, ys / 20, 44))[..., None]
    ripple = water & (np.floor(ys * S) % (5 * S) == 0) & (R.noise(xs / 9, ys / 3, 45) > 0.56)
    img = np.where(water[..., None], wash * 0.72 + img * 0.28, img)
    ink = np.where(ripple, 0.45, ink)
    img = img * (1 - ink[..., None]) + np.array(INK, np.float64)[None, None, :] * ink[..., None]

    # Alles Weitere wird mit der Feder darüber gezeichnet
    art = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    over = Image.new("RGBA", art.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    ink_a = INK + (235,)
    for bank in (17, -17):  # Uferlinien
        side = offset(river, bank)
        d.line(scaled(side), fill=ink_a, width=S + 1, joint="curve")
    for bank in (4.5, -4.5):
        d.line(scaled(offset(canal, bank)), fill=ink_a, width=S)
    for b in basins:
        d.line(scaled(b + b[:1]), fill=ink_a, width=S + 1)
        d.line(scaled([b[1], b[2]]), fill=ink_a, width=3 * S)
    # Hauptstraße und Straßenbahn
    for edge in (5, -5):
        d.line(scaled(offset(spline(MAIN), edge)), fill=INK + (200,), width=S)
    tram = spline(TRAM_LINE)
    d.line(scaled(tram), fill=TRAM + (235,), width=2 * S, joint="curve")
    for i in range(6, len(tram) - 4, 13):
        x, y = tram[i]
        d.ellipse(((x - 2.6) * S, (y - 2.6) * S, (x + 2.6) * S, (y + 2.6) * S), fill=TRAM + (255,), outline=INK + (255,))
    # Güterbahnhof: Gleisharfe, Schuppen, Drehscheibe
    for i in range(9):
        t = i / 8
        a = (724 + 20 * t, 318 + 44 * t)
        b = (1006 + 22 * t, 222 + 40 * t)
        d.line(scaled([(700, 322), a, b, (1052, 204)]), fill=ink_a, width=S)
    d.polygon(scaled([(800, 330), (930, 290), (938, 312), (808, 352)]), fill=INK + (215,))
    d.ellipse((716 * S, 344 * S, 738 * S, 366 * S), outline=ink_a, width=S + 1)
    d.line((719 * S, 361 * S, 735 * S, 349 * S), fill=ink_a, width=S)
    # Bahn: schwarze Linie mit hellen Feldern, an den Brücken Widerlager
    for line in rails:
        d.line(scaled(line), fill=INK + (255,), width=5 * S, joint="curve")
        dashed(d, line, 2.4, tuple(int(v * 0.96) for v in PAPER) + (255,), 9, 9)
    for x, y, w, h, a in ((300, 520, 46, 14, -0.3), (1100, 172, 50, 14, -0.22), (566, 356, 0, 0, 0)):
        if w:
            box = [(x + math.cos(a) * i * w / 2 - math.sin(a) * j * h / 2 + 10 * math.sin(a) * 0, y + math.sin(a) * i * w / 2 + math.cos(a) * j * h / 2 - 16)
                   for i, j in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            d.polygon(scaled(box), fill=INK + (255,))  # Bahnhöfe
    for (bx, by), ang in (((566, 356), -0.9), ((800, 474), 1.35), ((486, 262), 0.55)):  # Brücken
        ca, sa = math.cos(ang), math.sin(ang)
        for side in (-7, 7):
            p0 = (bx - ca * 25 - sa * side, by - sa * 25 + ca * side)
            p1 = (bx + ca * 25 - sa * side, by + sa * 25 + ca * side)
            d.line(scaled([p0, p1]), fill=INK + (255,), width=S + 1)
    # Alter Wall: gezackte Bastionen am Graben entlang, mit Böschungsstrichen, im Nordwall-Viertel
    wall_line = offset(canal, -15)[4:-2]
    zig = []
    for i, (x, y) in enumerate(wall_line):
        if i % 7 == 3:
            ax, ay = wall_line[max(0, i - 1)]
            bx, by = wall_line[min(len(wall_line) - 1, i + 1)]
            length = math.hypot(bx - ax, by - ay) or 1
            zig.append((x + (by - ay) / length * 15, y - (bx - ax) / length * 15))
        else:
            zig.append((x, y))
    d.line(scaled(zig), fill=INK + (255,), width=2 * S + 1, joint="curve")
    for (ax, ay), (bx, by) in zip(zig[::2], zig[1::2]):
        length = math.hypot(bx - ax, by - ay) or 1
        d.line(scaled([(ax, ay), (ax + (by - ay) / length * 6, ay - (bx - ax) / length * 6)]), fill=ink_a, width=S)
    d.rectangle((236 * S, 262 * S, 258 * S, 276 * S), fill=INK + (255,))  # Wachstube am Walltor
    d.line(scaled([(258, 262), (258, 250), (268, 254), (258, 258)]), fill=INK + (255,), width=S + 1)
    # Gleisfelder im Osten: Abstellgleise am Fluss entlang und eine zweite Harfe
    low = [q for q in river if q[0] >= 830]
    for k in range(6):
        d.line(scaled(offset(low, -(30 + k * 5))[2:-3]), fill=ink_a, width=S)
    for k in range(7):
        d.line(scaled([(1052, 204), (1100 + k * 4, 250 + k * 9), (1226, 300 + k * 12)]), fill=ink_a, width=S)
    # Gasometer, Kirche, Schornsteine der Werke
    for cx, cy, r in ((1000, 92, 13), (1034, 86, 10), (150, 700, 12), (640, 610, 9)):
        d.ellipse(((cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S), fill=tuple(PAPER) + (150,), outline=ink_a, width=S + 1)
        d.line(((cx - r) * S, cy * S, (cx + r) * S, cy * S), fill=ink_a, width=S)
        d.line((cx * S, (cy - r) * S, cx * S, (cy + r) * S), fill=ink_a, width=S)
    for cx, cy in ((250, 130), (560, 720), (1090, 420)):
        d.line(((cx - 5) * S, cy * S, (cx + 5) * S, cy * S), fill=INK + (255,), width=2 * S)
        d.line((cx * S, (cy - 7) * S, cx * S, (cy + 8) * S), fill=INK + (255,), width=2 * S)
    for cx, cy in ((560, 110), (690, 130), (760, 80), (880, 170), (620, 200)):
        d.ellipse(((cx - 3) * S, (cy - 3) * S, (cx + 3) * S, (cy + 3) * S), fill=INK + (255,))
        d.ellipse(((cx - 1) * S, (cy - 1) * S, (cx + 1) * S, (cy + 1) * S), fill=tuple(PAPER) + (255,))
    # Die Sieben der Linie im Kreis, wie auf Liniennetzen üblich
    small = ImageFont.load_default(size=9 * S)
    d.ellipse((672 * S, 196 * S, 688 * S, 212 * S), fill=tuple(PAPER) + (255,), outline=TRAM + (255,), width=S + 1)
    d.text((680 * S, 204 * S), "7", font=small, fill=TRAM + (255,), anchor="mm")
    # Was über das Kartenfeld hinausragt, fällt weg. Danach kommen Rahmen, Suchgitter, Titelfeld und Nordpfeil.
    x0, y0, x1, y1 = FRAME
    clip = Image.new("L", over.size, 0)
    ImageDraw.Draw(clip).rectangle((x0 * S, y0 * S, x1 * S, y1 * S), fill=255)
    over.putalpha(Image.fromarray(np.minimum(np.asarray(over.getchannel("A")), np.asarray(clip))))
    d = ImageDraw.Draw(over)
    d.rectangle((x0 * S, y0 * S, x1 * S, y1 * S), outline=INK + (255,), width=2 * S)
    d.rectangle(((x0 - 7) * S, (y0 - 7) * S, (x1 + 7) * S, (y1 + 7) * S), outline=INK + (230,), width=S)
    for i in range(9):
        x = x0 + (x1 - x0) * i / 8
        d.line((x * S, (y0 - 7) * S, x * S, y0 * S), fill=ink_a, width=S)
        d.line((x * S, y1 * S, x * S, (y1 + 7) * S), fill=ink_a, width=S)
        if i < 8:
            for yy in (y0 - 16, y1 + 17):
                d.text(((x + (x1 - x0) / 16) * S, yy * S), "ABCDEFGH"[i], font=small, fill=INK + (220,), anchor="mm")
    for j in range(7):
        y = y0 + (y1 - y0) * j / 6
        d.line(((x0 - 7) * S, y * S, x0 * S, y * S), fill=ink_a, width=S)
        d.line((x1 * S, y * S, (x1 + 7) * S, y * S), fill=ink_a, width=S)
        if j < 6:
            for xx in (x0 - 17, x1 + 17):
                d.text((xx * S, (y + (y1 - y0) / 12) * S), str(j + 1), font=small, fill=INK + (220,), anchor="mm")
    tx0, ty0, tx1, ty1 = title_box
    d.rectangle((tx0 * S, ty0 * S, tx1 * S, ty1 * S), fill=tuple(int(v * 0.97) for v in PAPER) + (236,), outline=INK + (255,), width=S + 1)
    d.rectangle(((tx0 + 4) * S, (ty0 + 4) * S, (tx1 - 4) * S, (ty1 - 4) * S), outline=INK + (200,), width=S)
    d.text(((tx0 + tx1) / 2 * S, (ty0 + 25) * S), "MESSINGSTADT", font=ImageFont.load_default(size=27 * S), fill=INK + (255,), anchor="mm")
    d.text(((tx0 + tx1) / 2 * S, (ty0 + 48) * S), "Amtlicher Stadtplan, Ausgabe 1927", font=ImageFont.load_default(size=10 * S), fill=INK + (235,), anchor="mm")
    for i in range(5):
        d.rectangle(((tx0 + 62 + i * 32) * S, (ty0 + 62) * S, (tx0 + 94 + i * 32) * S, (ty0 + 66) * S), fill=INK + (255,) if i % 2 == 0 else None, outline=INK + (255,))
    d.text(((tx0 + 36) * S, (ty0 + 64) * S), "1 : 25 000", font=ImageFont.load_default(size=8 * S), fill=INK + (235,), anchor="mm")
    nx, ny = 1196, 96
    d.polygon(scaled([(nx, ny - 26), (nx + 7, ny + 8), (nx, ny + 2), (nx - 7, ny + 8)]), fill=INK + (255,))
    d.polygon(scaled([(nx, ny + 2), (nx + 7, ny + 8), (nx, ny + 26)]), outline=INK + (255,))
    d.polygon(scaled([(nx, ny + 2), (nx - 7, ny + 8), (nx, ny + 26)]), outline=INK + (255,))
    d.ellipse(((nx - 16) * S, (ny - 16) * S, (nx + 16) * S, (ny + 16) * S), outline=INK + (170,), width=S)
    d.text((nx * S, (ny - 36) * S), "N", font=ImageFont.load_default(size=11 * S), fill=INK + (255,), anchor="mm")
    art = Image.alpha_composite(art, over)

    art = age(art, xs, ys, rng)
    plan = art.convert("RGB").resize((W, H), Image.LANCZOS)
    OUT.mkdir(parents=True, exist_ok=True)
    plan.save(OUT / "plan.png", optimize=True)

    info = {"size": [W, H], "frame": list(FRAME), "districts": []}
    masks = {}
    for dnum, poly in areas.items():
        # Aus der gemeinsamen Flächenkarte: Jeder Punkt gehört genau einem Viertel, an den Grenzen ergänzen sich die Masken zu voller Deckung.
        alpha = Image.fromarray(((zone == dnum) * 255).astype(np.uint8), "L").resize((W, H), Image.BOX)
        masks[dnum] = alpha
        Image.merge("LA", (Image.new("L", (W, H), 255), alpha)).save(OUT / f"viertel_{dnum}.png", optimize=True)
        box = alpha.getbbox()
        outline = [poly[0]] + [p for i, p in enumerate(poly[1:-1], 1) if i % 3 == 0 or p in ((566, 356), (800, 474))] + [poly[-1]]
        info["districts"].append({"id": dnum, "mask": f"viertel_{dnum}.png", "label": list(LABELS[dnum][0]), "stamp": list(LABELS[dnum][1]),
                                  "bounds": list(box), "polygon": [[round(x), round(y)] for x, y in outline]})
    (OUT / "plan.json").write_text(json.dumps(info, ensure_ascii=False, indent=1), encoding="utf-8")
    print("Stadtplan geschrieben:", OUT, plan.size)

    if preview:
        # So etwa sieht es im Spiel aus: zwei Beispielfarben halb durchsichtig über den Masken, Namen an den Ankern
        shown = plan.convert("RGBA")
        green, red = (0x4C, 0x70, 0x64), (0x8C, 0x1A, 0x14)
        yy, xx = np.mgrid[0:H, 0:W]
        stripes = ((xx + yy) % 9 < 4)
        for dnum, alpha in masks.items():
            a8 = np.asarray(alpha).astype(np.float64) / 255
            if dnum in (1, 4, 5):  # Brandt-Werke: schraffiert in Werksgrün
                layer = np.dstack([np.full((H, W, 3), green, np.uint8), (a8 * np.where(stripes, 0.8, 0.16) * 255).astype(np.uint8)])
            else:  # Roter Hammer: volle rote Fläche
                layer = np.dstack([np.full((H, W, 3), red, np.uint8), (a8 * 0.5 * 255).astype(np.uint8)])
            shown = Image.alpha_composite(shown, Image.fromarray(layer, "RGBA"))
        pd = ImageDraw.Draw(shown)
        for dnum, (label, stamp) in LABELS.items():
            name = {1: "KESSELVIERTEL", 2: "NORDWALL", 3: "GLEISVIERTEL", 4: "KONTORVIERTEL", 5: "ALTSTADT"}[dnum]
            pd.text(label, name, font=ImageFont.load_default(size=22), fill=(0x21, 0x1C, 0x1A, 255), anchor="mm", stroke_width=2, stroke_fill=(0xAD, 0x9E, 0x7A, 255))
            pd.ellipse((stamp[0] - 20, stamp[1] - 20, stamp[0] + 20, stamp[1] + 20), outline=(24, 18, 16, 255), width=2)
        shown.convert("RGB").save(preview)
        print("Vorschau:", preview)


def offset(points, distance):
    """Parallele zu einer Linie im Abstand distance."""
    out = []
    for i, (x, y) in enumerate(points):
        ax, ay = points[max(0, i - 1)]
        bx, by = points[min(len(points) - 1, i + 1)]
        length = math.hypot(bx - ax, by - ay) or 1
        out.append((x - (by - ay) / length * distance, y + (bx - ax) / length * distance))
    return out


def scribble(draw, points, rng, width=1.6, alpha=235, wobble=1.0):
    """Bleistiftstrich: leicht zittrig, zweimal nachgezogen, ungleich fest aufgedrückt."""
    for shift in range(2):
        pts = [(x + rng.uniform(-wobble, wobble), y + rng.uniform(-wobble, wobble)) for x, y in points]
        draw.line(scaled(pts), fill=PENCIL + (min(255, alpha) - 60 * shift,), width=max(2, int(width * S) - shift), joint="curve")


HAND = {
    "0": [[(1, 0), (3, 0), (4, 2), (4, 4), (3, 6), (1, 6), (0, 4), (0, 2), (1, 0)]], "1": [[(1, 1.5), (2.5, 0), (2.5, 6)]],
    "2": [[(0, 1), (2, 0), (4, 1.5), (0, 6), (4, 6)]], "3": [[(0, 0.5), (3, 0), (4, 1.5), (2, 3), (4, 4.5), (3, 6), (0, 5.5)]],
    "7": [[(0, 0), (4, 0), (1.5, 6)], [(1, 3), (3.5, 3)]], "?": [[(0, 1.5), (2, 0), (4, 1.5), (2, 3.6), (2, 4.6)], [(2, 5.8), (2.1, 6)]],
    "U": [[(0, 0), (0, 5), (2, 6), (4, 5), (4, 0)]], "h": [[(0, 0), (0, 6)], [(0, 4), (2, 3), (3.5, 4), (3.5, 6)]], "r": [[(0, 3), (0, 6)], [(0, 4), (2, 3), (3, 3.4)]],
    "!": [[(2, 0), (2, 4.2)], [(2, 5.7), (2.1, 6)]], "x": [[(0, 2), (4, 6)], [(4, 2), (0, 6)]], " ": [],
}


def handwriting(draw, x, y, word, size, rng, slant=0.25):
    for ch in word:
        for line in HAND[ch]:
            scribble(draw, [(x + (px + (6 - py) * slant) * size / 6, y + py * size / 6) for px, py in line], rng, 1.4, 240, 0.5)
        x += size * 0.82


def age(art, xs, ys, rng):
    """Gebrauchsspuren: Faltkanten mit abgeriebenem Druck, Kaffeerand, Bleistiftnotizen, Schmutz."""
    arr = np.asarray(art.convert("RGB")).astype(np.float64)
    paper = np.array(PAPER, np.float64)[None, None, :]
    # Der Plan war zweimal längs und einmal quer gefaltet
    for fold_x in (W / 3 + 6, 2 * W / 3 - 4):
        dist = np.abs(xs - fold_x - (R.noise(ys / 40, 1.0, 51) - 0.5) * 3)
        worn = np.clip(1 - dist / 5, 0, 1) * (0.5 + 0.5 * R.noise(ys / 9, fold_x, 52))
        arr = arr * (1 - 0.6 * worn[..., None]) + paper * 1.06 * (0.6 * worn[..., None])
        arr *= (1 - 0.3 * np.clip(1 - np.abs(dist - 1.2) / 1.0, 0, 1))[..., None]
        arr *= (1 - 0.07 * np.clip(1 - dist / 40, 0, 1) * (xs > fold_x))[..., None]
    dist = np.abs(ys - H / 2 - 8 - (R.noise(xs / 40, 2.0, 53) - 0.5) * 3)
    worn = np.clip(1 - dist / 5, 0, 1) * (0.5 + 0.5 * R.noise(xs / 9, 3.0, 54))
    arr = arr * (1 - 0.6 * worn[..., None]) + paper * 1.06 * (0.6 * worn[..., None])
    arr *= (1 - 0.3 * np.clip(1 - np.abs(dist - 1.2) / 1.0, 0, 1))[..., None]
    arr *= (1 - 0.06 * np.clip(1 - dist / 40, 0, 1) * (ys > H / 2))[..., None]
    for fold_x in (W / 3 + 6, 2 * W / 3 - 4):  # an den Kreuzungen der Falze ist das Papier durchgescheuert
        hole = np.clip(1 - np.hypot(xs - fold_x, (ys - H / 2 - 8) * 1.3) / (7 + 4 * R.noise(xs / 3, ys / 3, 55)), 0, 1)
        arr = arr * (1 - hole[..., None]) + np.array((58, 50, 44.0))[None, None, :] * hole[..., None]
    # Kaffeerand: ein Becher stand auf dem Plan, einmal verrutscht
    for cx, cy, r, strength in ((286, 640, 54, 0.62), (304, 650, 54, 0.3)):
        rad = np.hypot(xs - cx, (ys - cy) * 1.04) + (R.noise(xs / 14, ys / 14, 56) - 0.5) * 3
        ring = np.clip(1 - np.abs(rad - r) / (2.6 + 2 * R.noise(xs / 30, ys / 30, 57)), 0, 1)
        inside = (rad < r) * 0.09
        stain = (ring * strength + inside)[..., None]
        arr = arr * (1 - stain) + np.array((92, 58, 26.0))[None, None, :] * stain
    arr *= (0.9 + 0.14 * R.noise(xs / 300, ys / 240, 58))[..., None]  # Griffspuren, fleckig über alles
    out = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    notes = Image.new("RGBA", out.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(notes)
    # Notizen des Trupps: ein eingekreistes Haus, Wege, Uhrzeit, Fragezeichen, Kreuze
    circle = [(352 + math.cos(a) * 26 * (1 + 0.06 * math.sin(3 * a)), 372 + math.sin(a) * 18) for a in np.linspace(0.3, 6.9, 40)]
    scribble(d, circle, rng, 1.7, 240)
    scribble(d, [(376, 360), (420, 330), (452, 322)], rng, 1.6, 235)
    scribble(d, [(452, 322), (442, 320), (446, 330)], rng, 1.6, 235)
    handwriting(d, 458, 306, "23 Uhr", 15, rng)
    handwriting(d, 300, 398, "7 !", 12, rng)
    for cx, cy in ((690, 186), (912, 316), (1060, 610)):
        handwriting(d, cx, cy, "x", 12, rng, 0)
    handwriting(d, 930, 300, "?", 15, rng)
    scribble(d, [(560, 520), (600, 560), (640, 580), (700, 600), (760, 606)], rng, 1.5, 220, 1.4)
    scribble(d, [(760, 606), (750, 600), (752, 612)], rng, 1.5, 220)
    handwriting(d, 596, 588, "2 ?", 11, rng)
    for i in range(5):  # Strichliste am Rand
        scribble(d, [(1250 + i * 4, 430), (1249 + i * 4, 444)] if i < 4 else [(1246, 441), (1266, 432)], rng, 1.4, 230, 0.4)
    notes = notes.filter(ImageFilter.GaussianBlur(0.5))
    return Image.alpha_composite(out, notes)


if __name__ == "__main__":
    write_plan(sys.argv[1] if len(sys.argv) > 1 else None)
