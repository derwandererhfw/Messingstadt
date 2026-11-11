"""Baut das Titelbild in Ebenen: eine Straße des Kesselviertels bei Nacht, am Ende die Brandt-Werke.

Aufruf (aus dem Projektordner):
    python tools/art/title_scene.py

Die Häuser sind dieselben wie im Spiel: Die Fassaden kommen aus generate_concept.py (Putz, Gesimse, Erker, Balkone,
Fallrohre, Läden) und werden in die Fluchtlinie der Straße gekippt. Geschrieben wird nach game/assets/title/:
title.png (fertig zusammengesetzt), die Ebenen title_sky/_factory/_street/_left/_right/_front.png, die Leuchtebenen
title_factory_lit/_left_lit/_right_lit.png (nur erleuchtete Fenster und die Leuchtschrift) und title.json mit den
Lagedaten für die Animation. Den Schriftzug title_logo.png zeichnet weiter title.py.

Maße der Szene in Metern: x quer zur Straße (Mitte 0, Hauswände bei ±10), y nach oben, z in die Tiefe.
"""
import json
import math
import random
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

import generate_concept as g
import roofs as R

OUT = Path(__file__).resolve().parents[2] / "game" / "assets" / "title"
W, H = 1920, 1080
VX, HY = 1260, 600  # Fluchtpunkt: rechts der Mitte, damit links Platz für Titel und Menü bleibt
FOCAL = 1000.0
EYE = 1.6
WALL = 10.0  # halbe Straßenbreite bis zur Hauswand
PPM = 24  # Bildpunkte je Meter in den feinen Fassaden des Spiels
END = 58.0  # hier enden die Häuserzeilen, dahinter liegt der Vorplatz des Werks
WORKS = 120.0  # Tiefe der Werksfront
HAZE = np.array((26.0, 28.0, 40.0))
WARM = np.array((1.0, 0.72, 0.42))
LAMPS = [(8.0, 14.0), (8.0, 30.0), (8.0, 46.0), (-8.0, 30.0), (-8.0, 50.0), (13.0, 72.0), (-13.0, 72.0), (7.0, 112.0), (-7.0, 112.0)]
LAMP_Y = 4.75
PUDDLES = [(3.2, 9.5, 2.6, 1.6), (-1.0, 15.0, 2.2, 1.3), (5.5, 22.0, 2.4, 1.8), (-4.5, 27.0, 2.0, 1.5), (1.5, 40.0, 3.0, 2.4), (-6.5, 12.0, 1.6, 1.0),
           (6.8, 48.0, 2.2, 2.2), (-2.5, 64.0, 3.0, 3.0)]
WALK = [(3.4, 6.0), (2.8, 14.0), (1.9, 28.0), (1.2, 48.0), (0.6, 80.0), (0.0, 118.0)]
RNG = random.Random(1927)


def sx(x, z):
    return VX + FOCAL * x / z


def sy(y, z):
    return HY - FOCAL * (y - EYE) / z


def reseed(seed):
    for name in ("RNG", "WEAR", "DETAIL", "LIT_ROOMS"):
        getattr(g, name).seed(seed * 7919 + hash(name) % 1000 if False else seed * 7919 + len(name))


def homography(src, dst):
    """3 × 3-Matrix, die die vier Punkte src auf dst abbildet."""
    rows, rhs = [], []
    for (u, v), (x, y) in zip(src, dst):
        rows.append([u, v, 1, 0, 0, 0, -x * u, -x * v])
        rows.append([0, 0, 0, u, v, 1, -y * u, -y * v])
        rhs += [x, y]
    h = np.linalg.solve(np.array(rows, float), np.array(rhs, float))
    return np.append(h, 1).reshape(3, 3)


def warp(tex, quad):
    """Kippt ein flaches Bild auf das Viereck quad (links oben, rechts oben, rechts unten, links unten) im Titelbild."""
    w, h = tex.size
    forward = homography([(0, 0), (w, 0), (w, h), (0, h)], quad)
    back = np.linalg.inv(forward)
    back /= back[2, 2]
    return tex.transform((W, H), Image.PERSPECTIVE, tuple(back.ravel()[:8]), Image.BICUBIC), forward


def project(matrix, u, v):
    x, y, k = matrix @ np.array([u, v, 1.0])
    return x / k, y / k


# ---------- Häuser ----------
def roof_strip(width, seed, columns, lit):
    """Mansarde über einer Fassade, flach gezeichnet in feinen Bildpunkten: Schiefer, Gauben, Schornsteine, schiefer First."""
    rng = random.Random(seed)
    roof, head = 104, 70
    img = Image.new("RGBA", (width, roof + head), (0, 0, 0, 0))
    px = img.load()
    sag = rng.uniform(2, 7)
    lean = rng.uniform(-3, 3)
    for u in range(width):
        top = head + int(sag * math.sin(math.pi * u / width) + lean * u / width + 2 * R.noise(np.array(u / 40.0), np.array(seed * 1.0), 3))
        for v in range(top, roof + head):
            course = (v - head) // 8
            tile = (u + (course % 2) * 6) // 12
            tone = 0.72 + 0.5 * float(R.hash01(np.array(tile), np.array(course), seed))
            c = (60 * tone, 64 * tone, 80 * tone)
            if (v - head) % 8 == 0 or (u + (course % 2) * 6) % 12 == 0:
                c = tuple(k * 0.55 for k in c)
            if v - top < 3:
                c = (38, 38, 46)  # Firstblech
            if v > roof + head - 6:
                c = (84, 88, 96) if v < roof + head - 2 else (34, 34, 42)  # Rinne
            px[u, v] = tuple(int(k) for k in c) + (255,)
    d = ImageDraw.Draw(img)
    panes = []
    for i, u in enumerate(columns):
        if (i + seed) % 2:
            continue
        x0, y0 = u - 6, head + 42
        d.polygon([(x0 - 4, y0), (x0 + 17, y0 - 16), (x0 + 38, y0)], fill=(52, 54, 66, 255))
        d.rectangle((x0, y0, x0 + 34, y0 + 52), fill=(118, 110, 96, 255))
        d.rectangle((x0 + 5, y0 + 6, x0 + 28, y0 + 44), fill=(226, 170, 90, 255) if lit else (40, 52, 60, 255))
        d.line((x0 + 16, y0 + 6, x0 + 16, y0 + 44), fill=(60, 44, 36, 255), width=2)
        d.line((x0 + 5, y0 + 20, x0 + 28, y0 + 20), fill=(60, 44, 36, 255), width=2)
        panes.append((x0 + 5, y0 + 6, 24, 39))
    for _ in range(max(1, width // 170)):
        u = rng.randint(20, max(21, width - 40))
        tall = rng.randint(40, 78)
        base = head + rng.randint(8, 30)
        d.rectangle((u, base - tall, u + 18, base + 10), fill=(104, 58, 48, 255))
        for v in range(base - tall, base + 10, 6):
            d.line((u, v, u + 18, v), fill=(72, 44, 40, 255))
        d.rectangle((u - 2, base - tall - 4, u + 20, base - tall), fill=(120, 112, 100, 255))
        d.rectangle((u + 5, base - tall - 12, u + 13, base - tall - 4), fill=(40, 34, 34, 255))
    return img, panes


def house(length, height, seed, **kind):
    """Eine Fassade mit Dach, dunkel und mit allen Fenstern erleuchtet. Gibt beide Bilder und die Fensterflächen zurück."""
    w, h = int(length * PPM / 2), height
    out = []
    panes = []
    for lit in (False, True):
        reseed(seed)
        g.DUAL = True
        tex = g.tenement(w, h, lit_rate=1.0 if lit else 0.0, **kind.get("tenement", {}))
        d = g.draw_on(tex)
        if kind.get("shop"):
            x, width, name = kind["shop"]
            g.shop(tex, d, x, h - 1, width, name)
        for u, v in kind.get("posters", ()):
            g.poster(d, u, v)
        if kind.get("scrawl"):
            u, v, word = kind["scrawl"]
            g.scrawl(tex, u, v, word, 8, seed)
        fine = g.weathered(tex).info["hd"]
        columns = [u * 2 for u in range(10, w - 16, 26)]
        strip, roof_panes = roof_strip(fine.width, seed, columns, lit)
        whole = Image.new("RGBA", (fine.width, fine.height + strip.height), (0, 0, 0, 0))
        whole.paste(strip, (0, 0))
        whole.paste(fine, (0, strip.height))
        out.append(whole)
        if lit:
            panes = [(u, v, pw, ph) for u, v, pw, ph in roof_panes]
            panes += [(u * 2, v * 2 + strip.height, pw * 2, ph * 2) for u, v, pw, ph, *_ in tex.info.get("panes", [])]
    return out[0], out[1], panes


def firewall(width_m, height_m, seed, words):
    """Brandmauer: nackter Ziegel, oben verrußt, mit Resten gemalter Reklame."""
    reseed(seed)
    g.DUAL = True
    tex, d = g.wall(int(width_m * PPM / 2), max(24, int(height_m * PPM / 2)), ("brick",))
    y = 6
    for word, size in words:
        if y + size < tex.height - 4:
            tw = len(word) * 4 * size / 6 * 0.8 + (len(word) - 1) * max(1.5, size / 6 * 1.3)
            g.paint_text(tex, max(2, (tex.width - tw) / 2), y, word, size, [(g.C["cream"], max(1.4, size / 7), 0, 0, 0.9, 0.55)], seed=seed)
        y += size + 8
    return g.weathered(tex).info["hd"]


LEFT = [  # Länge in Metern, Höhe in einfachen Bildpunkten (42 je Geschoss), Zutaten. Nach hinten werden die Häuser niedriger.
    (20, 212, dict(tenement=dict(render=(138, 130, 118), pipes=(6,), balconies=((2, 3), (6, 2))), posters=((30, 180),))),
    (14, 254, dict(tenement=dict(render=(160, 132, 90), bays=((1, 2, 4),), balconies=((4, 2), (4, 3)), door_at=40, name="HOTEL", corner_stones=True))),
    (18, 128, dict(tenement=dict(door_at=150, render=(132, 128, 120), pipes=(200,)), shop=(20, 70, "TABAK"))),
]
RIGHT = [
    (14, 170, dict(tenement=dict(door_at=60, pipes=(150,)), scrawl=(96, 142, "BRANDT RAUS"), posters=((20, 140), (128, 138)))),
    (16, 254, dict(tenement=dict(render=(140, 134, 122), balconies=((1, 2), (1, 3), (4, 4)), door_at=30, corner_stones=True), shop=(90, 76, "KOHLE"))),
    (18, 128, dict(tenement=dict(render=(150, 126, 100), bays=((3, 1, 1),), door_at=20), shop=(120, 70, "BROT"))),
]
ADS = [(("ADLER", 20), ("BIER", 16)), (("KOHLEN", 18), ("& KOKS", 14)), (("BRANDT", 20), ("STAHL", 18)), (("SEIFE", 18),)]


def light_at(x, y, z, side_normal=None):
    """Licht an einem Punkt der Szene: fahler Nachthimmel von oben, warme Laternen, ein Schein vom Werk."""
    light = np.array((0.21, 0.24, 0.33))[None, :] * (0.55 + 0.45 * np.clip(y / 18, 0, 1))[:, None]
    for lx, lz in LAMPS:
        d2 = (x - lx) ** 2 + (y - LAMP_Y) ** 2 + (z - lz) ** 2
        fall = 6.5 / (d2 + 2.5)
        if side_normal is not None:
            fall = fall * np.clip((lx - x) * side_normal / np.sqrt(d2 + 1e-6) + 0.25, 0.05, 1)
        fall = fall * np.where(y > LAMP_Y + 1.2, 0.35, 1.0)  # der Hut der Laterne schirmt nach oben ab
        light = light + WARM[None, :] * fall[:, None]
    glow = np.clip((z - 60) / 50, 0, 1) * 0.16
    return light + np.array((1.0, 0.42, 0.16))[None, :] * glow[:, None]


def grade(rgb, z, xs):
    """Dunst mit der Tiefe, und die linke Bildseite dunkler: Dort stehen Titel und Menü."""
    fog = (1 - np.exp(-z / 150))[:, None]
    out = rgb * (1 - fog) + HAZE[None, :] * fog
    calm = 0.68 + 0.32 * np.clip((xs - 250) / 700, 0, 1)
    return out * calm[:, None]


def side_layer(side, houses):
    """Eine Häuserzeile: Ebene und Leuchtebene, dazu die Fensterflächen im Bild."""
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    lit_canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    windows = []
    x = -WALL if side < 0 else WALL
    z = 6.0 if side < 0 else 10.0
    spans = []
    for index, (length, height, kind) in enumerate(houses):
        dark, lit, panes = house(length, height, 100 * (2 + side) + index, **kind)
        spans.append((z, z + length, dark, lit, panes))
        z += length
    for index in range(len(spans) - 1, -1, -1):  # von hinten nach vorn
        z0, z1, dark, lit, panes = spans[index]
        top = dark.height / PPM
        # Brandmauer des höheren Nachbarn dahinter, über dem eigenen Dach
        if index + 1 < len(spans):
            behind = spans[index + 1][2].height / PPM
            if behind > top + 2.5:
                wall = firewall(13, behind - top + 4.4, 300 + index + 10 * (side > 0), ADS[(index + (side > 0)) % len(ADS)])
                xa, xb = sorted((sx(x, z1), sx(x + side * 13, z1)))
                box = (int(xa), int(sy(behind - 4.4, z1)), int(xb), int(sy(top - 4.4, z1)))
                if box[2] > box[0] and box[3] > box[1]:
                    piece = wall.resize((box[2] - box[0], box[3] - box[1]), Image.LANCZOS)
                    sheet = Image.new("RGBA", (W, H), (0, 0, 0, 0))
                    sheet.paste(piece, (box[0], box[1]))
                    canvas.alpha_composite(sheet)
        near, far = (z0, z1) if side < 0 else (z1, z0)  # links wächst die Fassade nach hinten, rechts auf den Betrachter zu
        quad = [(sx(x, near), sy(top, near)), (sx(x, far), sy(top, far)), (sx(x, far), sy(0, far)), (sx(x, near), sy(0, near))]
        warped, matrix = warp(dark, quad)
        canvas.alpha_composite(warped)
        mask = Image.new("L", lit.size, 0)
        md = ImageDraw.Draw(mask)
        for u, v, pw, ph in panes:
            md.rectangle((u, v, u + pw - 1, v + ph - 1), fill=255)
            corners = [project(matrix, a, b) for a, b in ((u, v), (u + pw, v), (u + pw, v + ph), (u, v + ph))]
            bx0, by0 = min(c[0] for c in corners), min(c[1] for c in corners)
            bx1, by1 = max(c[0] for c in corners), max(c[1] for c in corners)
            if bx1 - bx0 >= 4 and by1 - by0 >= 6 and bx0 >= 0 and bx1 < W and by1 > 0 and by0 < H:
                windows.append({"layer": "left" if side < 0 else "right", "rect": [int(bx0), int(by0), int(math.ceil(bx1 - bx0)), int(math.ceil(by1 - by0))]})
        glow = lit.copy()
        glow.putalpha(mask)
        lit_canvas.alpha_composite(warp(glow, quad)[0])
    out = []
    for image, glowing in ((canvas, False), (lit_canvas, True)):
        arr = np.asarray(image).astype(np.float64)
        ys, xs = np.nonzero(arr[..., 3] > 0)
        depth = np.clip(FOCAL * WALL / np.maximum(np.abs(xs + 0.5 - VX), 1.0), 3, END + 8)
        height = EYE + (HY - (ys + 0.5)) * depth / FOCAL
        rgb = arr[ys, xs, :3]
        if glowing:
            rgb = rgb * 0.92
        else:
            rgb = rgb * light_at(np.full(len(xs), x), height, depth, -side) * 1.25
            grain = 0.9 + 0.2 * R.hash01(xs, ys, 5)  # feines Korn, damit vergrößerte Fassaden nicht glatt wirken
            rgb = rgb * grain[:, None]
        arr[ys, xs, :3] = np.clip(grade(rgb, depth, xs), 0, 255)
        out.append(Image.fromarray(arr.astype(np.uint8), "RGBA"))
    return out[0], out[1], windows


# ---------- Straße ----------
ROAD = 6.5  # halbe Fahrbahnbreite, außen der Rinnstein
RAILS = (-3.1, -1.9, 1.9, 3.1)


def paving(across, depth):
    """Farbe des Bodens: Kopfsteinpflaster mit Schienen und Rinnstein, Bordstein, Gehweg aus Platten und Mosaik.

    In der Ferne sind die Steine kleiner als ein Bildpunkt, dort geht das Muster in seinen Mittelwert über.
    """
    col = lambda name: np.array(g.C[name], np.float64)  # noqa: E731
    sharp = np.clip(0.2 * FOCAL * EYE / depth ** 2, 0, 1)  # Bildpunkte je Stein in der Tiefe
    wide = np.clip(0.2 * FOCAL / depth / 2, 0, 1)
    row = np.floor(depth / 0.2)
    stone = np.floor(across / 0.14 + 0.5 * (row % 2))
    joint = ((depth / 0.2) % 1 < 0.2) * sharp + ((across / 0.14 + 0.5 * (row % 2)) % 1 < 0.16) * wide * sharp
    tone = 0.84 + 0.32 * R.hash01(row, stone, 6) * sharp[...] + 0.16 * (1 - sharp)
    sett = (col("cobble") * 0.6 + col("cobble_l") * 0.4)[None, :] * tone[:, None]
    sett = sett * (1 - 0.5 * np.clip(joint, 0, 1))[:, None]
    worn = R.noise(across / 1.7, depth / 2.6, 13)
    sett = sett * (0.72 + 0.5 * worn)[:, None]
    lost = (R.noise(across / 0.9, depth / 1.1, 14) > 0.8) & (R.hash01(row, stone, 5) < 0.5)
    sett = np.where(lost[:, None], col("dirt")[None, :], sett)
    gutter = (np.abs(across) > ROAD - 0.5) & (np.abs(across) <= ROAD)
    sett = np.where(gutter[:, None], sett * 0.6, sett)
    for rail in RAILS:
        groove = np.abs(across - rail - 0.045) < 0.02
        head = np.abs(across - rail) < 0.03
        sett = np.where(groove[:, None], col("black")[None, :], sett)
        sett = np.where(head[:, None], (col("steel") * 0.45 + col("ash") * 0.55)[None, :], sett)
    # Gehweg
    slab_u, slab_v = across / 0.9, depth / 0.9
    edge = np.clip(((slab_u % 1 < 0.05) | (slab_v % 1 < 0.05)) * np.clip(0.9 * FOCAL * EYE / depth ** 2 / 6, 0, 1), 0, 1)
    slab = col("stone")[None, :] * (0.8 + 0.2 * R.hash01(np.floor(slab_u), np.floor(slab_v), 8))[:, None] * (1 - 0.4 * edge)[:, None]
    mos_u, mos_v = across / 0.11, depth / 0.11
    mosaic = np.array((128, 120, 110.0))[None, :] * (0.78 + 0.3 * R.hash01(np.floor(mos_u), np.floor(mos_v), 51) * sharp + 0.15 * (1 - sharp))[:, None]
    strip = (np.abs(across) < ROAD + 0.9) | (np.abs(across) > WALL - 0.7)
    walk = np.where(strip[:, None], mosaic, slab) * (0.66 + 0.4 * R.noise(across / 1.4, depth / 1.4, 21))[:, None]
    kerb = (np.abs(across) > ROAD) & (np.abs(across) <= ROAD + 0.22)
    walk = np.where(kerb[:, None], col("stone_l")[None, :] * 0.8, walk)
    drip = np.abs(across) > WALL - 0.12
    walk = np.where(drip[:, None], col("dirt")[None, :], walk)
    pavement = (np.abs(across) > ROAD) & (depth <= END)
    return np.where(pavement[:, None], walk, sett)


def street_layer():
    ys, xs = np.mgrid[int(HY) + 1:H, 0:W]
    ys, xs = ys.ravel(), xs.ravel()
    depth = FOCAL * EYE / (ys + 0.5 - HY)
    across = (xs + 0.5 - VX) * depth / FOCAL
    keep = ((np.abs(across) <= WALL + 0.02) | (depth > END)) & (depth <= WORKS + 0.5)
    ys, xs, depth, across = ys[keep], xs[keep], depth[keep], across[keep]
    albedo = paving(across, depth)
    wet = 0.35 + 0.65 * R.noise(across / 2.3, depth / 3.1, 11)
    pool = np.zeros(len(xs))
    for px, pz, rx, rz in PUDDLES:
        rim = np.hypot((across - px) / rx, (depth - pz) / rz) + (R.noise(across * 1.3, depth * 1.3, 12) - 0.5) * 0.5
        pool = np.maximum(pool, np.clip((1 - rim) * 4, 0, 1))
    rgb = albedo * light_at(across, np.zeros(len(xs)), depth) * 1.9
    rgb = rgb + np.array((20, 24, 36.0))[None, :] * (wet * np.clip(depth / 30, 0.25, 1))[:, None]  # der nasse Stein spiegelt den Himmel
    rgb = rgb * (1 - 0.55 * pool[:, None])
    # Nasses Pflaster spiegelt die Laternen als lange Lichtbahnen, in den Pfützen klar
    for lx, lz in LAMPS:
        mx, my = sx(lx, lz), sy(-LAMP_Y, lz)
        scale = FOCAL / lz
        streak = np.exp(-((xs - mx) / (0.22 * scale + 3)) ** 2) * np.exp(-((ys - my) / (2.4 * scale + 10)) ** 2)
        streak = streak * (ys > sy(0, lz))
        rgb = rgb + (WARM * 255)[None, :] * (streak * (0.35 * wet + 1.1 * pool))[:, None]
    rgb = rgb + np.array((34, 40, 58.0))[None, :] * (pool * 0.5)[:, None]
    arr = np.zeros((H, W, 4), np.uint8)
    arr[ys, xs, :3] = np.clip(grade(rgb, depth, xs), 0, 255)
    arr[ys, xs, 3] = 255
    puddles = [{"x": round(sx(px, pz)), "y": round(sy(0, pz)), "rx": round(FOCAL * rx / pz), "ry": max(2, round(FOCAL * EYE * rz / pz ** 2))} for px, pz, rx, rz in PUDDLES]
    return Image.fromarray(arr, "RGBA"), puddles


# ---------- Werk ----------
def works_layer():
    """Die Brandt-Werke quer am Ende der Straße: Mauer mit Gittertor, Halle mit Sheddach, Kessel, Schlote, Leuchtschrift."""
    k = 2
    scale = FOCAL / WORKS * k
    base = Image.new("RGBA", (W * k, H * k), (0, 0, 0, 0))
    lit = Image.new("RGBA", (W * k, H * k), (0, 0, 0, 0))
    d, dl = ImageDraw.Draw(base), ImageDraw.Draw(lit)
    px = lambda x: VX * k + x * scale  # noqa: E731
    py = lambda y: HY * k - (y - EYE) * scale  # noqa: E731
    rng = random.Random(77)

    def box(x0, y0, x1, y1, c, draw=d):
        draw.rectangle((px(x0), py(y1), px(x1), py(y0)), fill=tuple(int(v) for v in c) + (255,))

    brick, brick_d, steel, soot = (58, 36, 32), (40, 26, 26), (44, 48, 56), (20, 20, 26)
    windows, stacks = [], []
    # Ferne Silhouetten: Gasometer und eine zweite Halle
    box(-108, 0, -78, 15, (30, 30, 38))
    d.ellipse((px(-70), py(26), px(-48), py(-4)), fill=(32, 32, 40, 255))
    box(70, 0, 110, 13, (30, 30, 38))
    # Schlote: gemauert, nach oben schlanker, mit Eisenbändern
    for cx, half, tall in ((-31, 2.2, 52), (-13, 2.7, 66), (11, 2.4, 60), (29, 2.0, 46)):
        d.polygon([(px(cx - half), py(0)), (px(cx - half * 0.62), py(tall)), (px(cx + half * 0.62), py(tall)), (px(cx + half), py(0))], fill=brick + (255,))
        d.polygon([(px(cx + half * 0.1), py(0)), (px(cx + half * 0.1), py(tall)), (px(cx + half * 0.62), py(tall)), (px(cx + half), py(0))], fill=brick_d + (255,))
        for band in range(8, int(tall), 7):
            w = half * (1 - 0.38 * band / tall)
            d.line((px(cx - w), py(band), px(cx + w), py(band)), fill=soot + (255,), width=k)
        box(cx - half * 0.7, tall, cx + half * 0.7, tall + 1.2, (70, 62, 58))
        stacks.append({"x": round(px(cx) / k), "y": round(py(tall + 1.2) / k), "width": round(half * 1.3 * scale / k)})
    # Halle mit Sheddach: sieben Zähne, die steile Seite verglast
    hx0, hx1, hy1 = -44.0, 24.0, 24.0
    box(hx0, 0, hx1, hy1, brick)
    teeth = 7
    pitch = (hx1 - hx0) / teeth
    for i in range(teeth):
        a = hx0 + i * pitch
        d.polygon([(px(a), py(hy1)), (px(a + pitch), py(hy1 + 5.5)), (px(a + pitch), py(hy1))], fill=steel + (255,))
        d.polygon([(px(a + pitch - 1.1), py(hy1)), (px(a + pitch - 1.1), py(hy1 + 4.9)), (px(a + pitch), py(hy1 + 5.5)), (px(a + pitch), py(hy1))], fill=(28, 34, 40, 255))
        dl.polygon([(px(a + pitch - 1.0), py(hy1 + 0.4)), (px(a + pitch - 1.0), py(hy1 + 4.6)), (px(a + pitch - 0.15), py(hy1 + 5.0)), (px(a + pitch - 0.15), py(hy1 + 0.4))], fill=(226, 170, 90, 255))
        windows.append([a + pitch - 1.0, hy1 + 0.4, a + pitch - 0.15, hy1 + 5.0])
    box(hx0, hy1 - 0.6, hx1, hy1, (84, 76, 70))
    for i in range(12):  # hohe Hallenfenster mit Sprossen zwischen Lisenen
        a = hx0 + 3 + i * 5.3
        box(a - 1.3, 0, a - 0.9, hy1 - 0.6, brick_d)
        box(a, 9.0, a + 3.0, 20.2, (26, 30, 36))
        d.pieslice((px(a), py(21.7), px(a + 3.0), py(18.7)), 180, 360, fill=(26, 30, 36, 255))
        box(a + 0.15, 9.2, a + 2.85, 20.0, (226, 170, 90) if rng.random() < 0.8 else (150, 110, 60), dl)
        for bar in range(1, 5):
            dl.line((px(a + bar * 0.6), py(20.0), px(a + bar * 0.6), py(9.2)), fill=(40, 30, 26, 255), width=k)
        for bar in range(1, 7):
            dl.line((px(a + 0.15), py(9.2 + bar * 1.55), px(a + 2.85), py(9.2 + bar * 1.55)), fill=(40, 30, 26, 255), width=k)
        windows.append([a + 0.15, 9.2, a + 2.85, 20.0])
    # Kesselhaus: stehende Kessel mit Dom, Bühnen und Rohren
    for cx in (27.0, 36.0, 45.0):
        box(cx - 3.6, 0, cx + 3.6, 21, (50, 54, 62))
        box(cx + 0.8, 0, cx + 3.6, 21, (34, 36, 44))
        d.pieslice((px(cx - 3.6), py(24.5), px(cx + 3.6), py(17.5)), 180, 360, fill=(54, 58, 66, 255))
        for band in (5, 10, 15, 20):
            d.line((px(cx - 3.6), py(band), px(cx + 3.6), py(band)), fill=soot + (255,), width=k)
        box(cx - 1.0, 1.0, cx + 1.0, 3.0, soot)
        box(cx - 0.7, 1.3, cx + 0.7, 2.7, (255, 150, 60), dl)  # Feuertür
        windows.append([cx - 0.7, 1.3, cx + 0.7, 2.7])
    box(22, 12.5, 50, 13.0, soot)
    for post in range(22, 51, 4):
        d.line((px(post), py(13), px(post), py(14.4)), fill=soot + (255,), width=k)
    d.line((px(22), py(14.4), px(50), py(14.4)), fill=soot + (255,), width=k)
    box(20, 18.5, 27, 19.6, (60, 40, 30))
    box(-52, 9, -46, 10.2, (60, 40, 30))
    # Werksmauer mit Pfeilern und Stacheldraht, in der Mitte das Tor
    box(-120, 0, -6.0, 5.2, (50, 32, 30))
    box(6.0, 0, 120, 5.2, (50, 32, 30))
    for post in range(-118, 120, 8):
        if abs(post) > 7:
            box(post - 0.5, 0, post + 0.5, 5.8, (62, 40, 36))
    d.line((px(-120), py(5.9), px(-6), py(5.9)), fill=(16, 16, 20, 255), width=1)
    d.line((px(6), py(5.9), px(120), py(5.9)), fill=(16, 16, 20, 255), width=1)
    # Tor: zwei Pfeiler mit Laternen, Bogen aus Eisen, Gitterflügel
    for side in (-1, 1):
        box(side * 6.2 - 0.9, 0, side * 6.2 + 0.9, 8.2, (72, 60, 54))
        box(side * 6.2 - 1.1, 8.2, side * 6.2 + 1.1, 8.8, (92, 80, 70))
        box(side * 6.2 - 0.4, 8.8, side * 6.2 + 0.4, 9.9, (20, 20, 24))
        box(side * 6.2 - 0.3, 8.9, side * 6.2 + 0.3, 9.7, (255, 232, 160), dl)
        windows.append([side * 6.2 - 0.3, 8.9, side * 6.2 + 0.3, 9.7])
    d.arc((px(-5.3), py(10.4), px(5.3), py(4.0)), 180, 360, fill=(14, 14, 18, 255), width=3 * k)
    d.arc((px(-5.3), py(9.6), px(5.3), py(3.2)), 180, 360, fill=(14, 14, 18, 255), width=k)
    for bar in np.arange(-5.2, 5.21, 0.52):
        top = 6.3 + 2.6 * math.sqrt(max(0.0, 1 - (bar / 5.3) ** 2))
        d.line((px(bar), py(0), px(bar), py(top)), fill=(14, 14, 18, 255), width=k if abs(bar) > 0.1 else 2 * k)
    for rail in (0.5, 3.4, 6.0):
        d.line((px(-5.3), py(rail), px(5.3), py(rail)), fill=(14, 14, 18, 255), width=2 * k)
    # Leuchtschrift auf einem Gerüst über der Halle, wie die Reklame im Spiel
    g.DUAL = False
    height = 13
    gap = height / 6 * 1.5
    word = "BRANDT WERKE"
    tw = int(len(word) * 4 * height / 6 * 0.8 + (len(word) - 1) * gap)
    for target, layers in ((d, [((70, 20, 24), 2.6, 0, 0, 1.0, 0)]), (dl, g.NEON)):
        sign = Image.new("RGBA", (tw + 10, height + 8), (0, 0, 0, 0))
        spans = g.paint_text(sign, 5, 4, word, height, layers, gap=gap, seed=5)
        factor = 5.0 * scale / height  # die Buchstaben sind fünf Meter hoch
        big = sign.resize((int(sign.width * factor), int(sign.height * factor)), Image.LANCZOS)
        ox, oy = int(px(0) - big.width / 2), int(py(38.8))
        (base if target is d else lit).alpha_composite(big, (ox, oy))
    letters = [[round((ox + (u0 - 1) * factor) / k), round(oy / k), round((u1 - u0 + 2) * factor / k), round(big.height / k)] for u0, u1 in spans]
    sign_rect = [round(ox / k), round(oy / k), round(big.width / k), round(big.height / k)]
    half_sign = big.width / scale / 2
    box(-half_sign, 30.4, half_sign, 30.8, soot)
    box(-half_sign, 37.9, half_sign, 38.2, soot)
    for post in np.arange(-half_sign, half_sign + 0.1, 3.2):
        d.line((px(post), py(29.0), px(post), py(38.2)), fill=soot + (255,), width=k)
        d.line((px(post), py(30.6), px(post + 3.2), py(38.0)), fill=soot + (255,), width=1)
    # Struktur und Dunst: Mauerwerk bekommt Korn, alles versinkt im Rauch, unten glimmt es
    out = []
    for image, glowing in ((base, False), (lit, True)):
        small = image.resize((W, H), Image.LANCZOS)
        arr = np.asarray(small).astype(np.float64)
        ys, xs = np.nonzero(arr[..., 3] > 0)
        rgb = arr[ys, xs, :3]
        if not glowing:
            rgb = rgb * (0.78 + 0.44 * R.noise(xs / 7.0, ys / 3.0, 21))[:, None] * (0.9 + 0.2 * R.hash01(xs, ys, 22))[:, None]
            up = np.clip((HY - ys) / 420, 0, 1)
            ember = np.clip(1 - np.abs(HY - 30 - ys) / 150, 0, 1) * 0.5
            rgb = rgb * (1.25 - 0.55 * up)[:, None] + np.array((120, 46, 14.0))[None, :] * (ember * (0.3 + 0.5 * R.noise(xs / 90.0, ys / 60.0, 23)))[:, None]
            rgb = rgb * 0.7 + HAZE[None, :] * 0.3
        calm = 0.68 + 0.32 * np.clip((xs - 250) / 700, 0, 1)
        arr[ys, xs, :3] = np.clip(rgb * calm[:, None], 0, 255)
        out.append(Image.fromarray(arr.astype(np.uint8), "RGBA"))
    rects = [{"layer": "factory", "rect": [round(px(x0) / k), round(py(y1) / k), max(2, round((x1 - x0) * scale / k)), max(2, round((y1 - y0) * scale / k))]} for x0, y0, x1, y1 in windows]
    gate = {"rect": [round(px(-5.3) / k), round(py(8.9) / k), round(10.6 * scale / k), round(8.9 * scale / k)]}
    return out[0], out[1], rects, {"layer": "factory", "rect": sign_rect, "letters": letters}, gate, stacks


# ---------- Himmel ----------
def sky_layer():
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float64)
    t = np.clip(ys / HY, 0, 1.3)
    rgb = np.array((12, 14, 22.0))[None, None, :] * (1 - t[..., None]) + np.array((44, 38, 46.0))[None, None, :] * t[..., None]
    smog = R.noise(xs / 300, ys / 80, 31) * 0.6 + R.noise(xs / 90, ys / 34, 32) * 0.4
    rgb = rgb * (0.7 + 0.6 * smog[..., None])
    glow = np.exp(-((xs - VX) / 620) ** 2) * np.exp(-((ys - HY) / 330) ** 2)
    rgb = rgb + np.array((150, 62, 22.0))[None, None, :] * (glow * (0.35 + 0.5 * smog))[..., None]
    moon = np.exp(-(np.hypot(xs - 1560, ys - 150) / 150) ** 2) * (0.5 + 0.5 * smog)
    rgb = rgb + np.array((40, 46, 62.0))[None, None, :] * moon[..., None]
    rgb[ys > HY + 4] = HAZE * 0.7
    calm = 0.68 + 0.32 * np.clip((xs - 250) / 700, 0, 1)
    return Image.fromarray(np.clip(rgb * calm[..., None], 0, 255).astype(np.uint8), "RGB").convert("RGBA")


# ---------- Vordergrund ----------
def front_layer():
    """Laternen, Fahrdraht mit Querdrähten, Leitungen, Wäscheleinen, das rote Banner und eine Litfaßsäule."""
    k = 2
    img = Image.new("RGBA", (W * k, H * k), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pt = lambda x, y, z: (sx(x, z) * k, sy(y, z) * k)  # noqa: E731
    dark = (14, 14, 18, 255)

    def fade(z, c=(14, 14, 18)):
        f = 1 - math.exp(-z / 150)
        return tuple(int(v * (1 - f) + h * f) for v, h in zip(c, HAZE)) + (255,)

    def sagging(a, b, sag, steps=20):
        return [(a[0] + (b[0] - a[0]) * i / steps, a[1] + (b[1] - a[1]) * i / steps + math.sin(math.pi * i / steps) * sag) for i in range(steps + 1)]

    # Fahrdraht über beiden Gleisen, an Querdrähten zwischen den Häusern
    for track in (-2.5, 2.5):
        d.line([pt(track, 6.0, z) for z in (5.0, 12, 30, 60, WORKS - 8)], fill=fade(30), width=k)
    for z in range(12, int(END), 20):
        a, b = pt(-WALL, 8.4, z), pt(WALL, 8.4, z)
        d.line(sagging(a, b, FOCAL * 2.0 / z * k), fill=fade(z), width=max(1, int(k * 20 / z)))
        for track in (-2.5, 2.5):
            x, y = pt(track, 6.0, z)
            d.rectangle((x - k, y - 3 * k * 12 / z, x + k, y), fill=fade(z, (150, 140, 120)))
    # Telefon- und Lichtleitungen, schräg von Dach zu Dach
    for (xa, ya, za), (xb, yb, zb) in (((-WALL, 17, 22), (WALL, 15, 30)), ((-WALL, 14, 40), (WALL, 19, 34)), ((WALL, 13, 50), (-WALL, 12, 56))):
        for extra in (0.0, 0.5, 1.0):
            d.line(sagging(pt(xa, ya + extra, za), pt(xb, yb + extra, zb), FOCAL * 1.4 / za * k), fill=fade((za + zb) / 2), width=1)
    # Wäscheleinen quer über die Straße
    rng = random.Random(41)
    for z, y in ((30, 13.0),):
        line = sagging(pt(-WALL, y, z), pt(WALL, y, z), FOCAL * 0.7 / z * k, 40)
        d.line(line, fill=fade(z), width=1)
        s = FOCAL / z * k
        for i in range(4, 37, rng.choice((3, 4))):
            x, yy = line[i]
            if rng.random() < 0.75 and x > 860 * k:  # links bleibt der Himmel über dem Menü frei
                w, h = rng.uniform(0.5, 1.1) * s, rng.uniform(0.6, 1.3) * s
                tone = rng.choice(((66, 64, 60), (52, 54, 60), (74, 70, 60), (46, 50, 62)))
                d.polygon([(x, yy), (x + w, yy + 0.04 * s), (x + w * 0.95, yy + h), (x + 0.05 * w, yy + h * 0.94)], fill=fade(z * 1.6, tone))
    # Das Banner des Roten Hammers: der einzige kräftige Ton im Bild
    g.DUAL = False
    cloth = Image.new("RGBA", (300, 34), (140, 26, 20, 255))
    g.paint_text(cloth, 96, 9, "STREIK", 17, [((226, 214, 190), 3.0, 0, 0, 0.95, 0.25)], gap=6, seed=8)
    edge = cloth.load()
    for u in range(300):
        tear = int(3 * R.noise(np.array(u / 9.0), np.array(1.0), 51) + (5 if 200 < u < 214 else 0))
        for v in range(34 - tear, 34):
            edge[u, v] = (0, 0, 0, 0)
    bz = 38.0
    a, b = pt(-7.8, 9.4, bz), pt(7.8, 9.4, bz)
    big = cloth.resize((int(b[0] - a[0]), int(FOCAL * 1.75 / bz * k)), Image.LANCZOS)
    shade = np.asarray(big).astype(np.float64)
    fold = 0.62 + 0.3 * np.sin(np.arange(big.width) / 23.0)[None, :, None] * np.ones((big.height, 1, 1))
    shade[..., :3] *= fold
    big = Image.fromarray(shade.astype(np.uint8), "RGBA")
    for column in range(big.width):  # hängt in der Mitte durch
        drop = int(math.sin(math.pi * column / big.width) * FOCAL * 0.5 / bz * k)
        img.alpha_composite(big.crop((column, 0, column + 1, big.height)), (int(a[0]) + column, int(a[1]) + drop))
    d = ImageDraw.Draw(img)
    for end, wall in ((a, -WALL), (b, WALL)):
        d.line([pt(wall, 10.6, bz), end], fill=fade(bz), width=1)
        d.line([pt(wall, 8.4, bz), (end[0], end[1] + big.height)], fill=fade(bz), width=1)
    # Laternen wie im Spiel: Gusssockel, Mast, Glas im Messingrahmen, Dach
    lamps = []
    for lx, lz in sorted(LAMPS, key=lambda p: -p[1]):
        s = FOCAL / lz * k
        x, y0 = pt(lx, 0, lz)
        iron, brass = fade(lz, (30, 32, 40)), fade(lz, (96, 68, 30))
        d.rectangle((x - 0.2 * s, y0 - 0.5 * s, x + 0.2 * s, y0), fill=iron)
        d.rectangle((x - 0.13 * s, y0 - 1.0 * s, x + 0.13 * s, y0 - 0.5 * s), fill=iron)
        d.rectangle((x - 0.055 * s, y0 - 4.2 * s, x + 0.055 * s, y0 - 1.0 * s), fill=iron)
        d.line((x - 0.5 * s, y0 - 3.8 * s, x + 0.5 * s, y0 - 3.8 * s), fill=iron, width=max(1, int(0.05 * s)))
        d.polygon([(x - 0.2 * s, y0 - 4.3 * s), (x + 0.2 * s, y0 - 4.3 * s), (x + 0.33 * s, y0 - 5.2 * s), (x - 0.33 * s, y0 - 5.2 * s)], fill=(226, 170, 90, 255))
        d.polygon([(x - 0.12 * s, y0 - 4.36 * s), (x + 0.12 * s, y0 - 4.36 * s), (x + 0.22 * s, y0 - 5.14 * s), (x - 0.22 * s, y0 - 5.14 * s)], fill=(255, 232, 160, 255))
        for side in (-1, 1):
            d.line((x + side * 0.2 * s, y0 - 4.3 * s, x + side * 0.33 * s, y0 - 5.2 * s), fill=brass, width=max(1, int(0.04 * s)))
        d.line((x, y0 - 4.3 * s, x, y0 - 5.2 * s), fill=brass, width=max(1, int(0.03 * s)))
        d.polygon([(x - 0.42 * s, y0 - 5.2 * s), (x + 0.42 * s, y0 - 5.2 * s), (x + 0.12 * s, y0 - 5.55 * s), (x - 0.12 * s, y0 - 5.55 * s)], fill=iron)
        d.rectangle((x - 0.03 * s, y0 - 5.75 * s, x + 0.03 * s, y0 - 5.55 * s), fill=brass)
        lamps.append({"x": round(x / k), "y": round((y0 - 4.75 * s) / k), "scale": round(FOCAL / lz, 2)})
    # Litfaßsäule auf dem rechten Gehweg, mit abgerissenen Plakaten
    cz, cx, radius, tall = 17.0, 8.2, 0.62, 3.1
    s = FOCAL / cz * k
    x, y0 = pt(cx, 0, cz)
    for i in range(int(2 * radius * s)):
        u = i / (2 * radius * s) * 2 - 1
        tone = 0.35 + 0.65 * max(0.0, 1 - (u + 0.35) ** 2)
        col = tuple(int(v * tone) for v in (104, 96, 82))
        d.line((x - radius * s + i, y0 - tall * s, x - radius * s + i, y0), fill=col + (255,))
    for u0, v0, w, h, col in ((-0.5, 2.7, 0.5, 0.9, (150, 140, 116)), (0.02, 2.5, 0.42, 1.1, (122, 30, 24)), (-0.34, 1.6, 0.56, 0.8, (96, 104, 112)), (0.1, 1.2, 0.36, 0.7, (140, 126, 92))):
        tone = 0.45 + 0.55 * max(0.0, 1 - (u0 / radius + 0.2) ** 2)
        d.polygon([(x + u0 * s, y0 - v0 * s), (x + (u0 + w) * s, y0 - v0 * s), (x + (u0 + w) * s, y0 - (v0 - h * 0.9) * s), (x + (u0 + w * 0.4) * s, y0 - (v0 - h) * s),
                   (x + u0 * s, y0 - (v0 - h * 0.8) * s)], fill=tuple(int(v * tone) for v in col) + (255,))
    d.rectangle((x - (radius + 0.06) * s, y0 - 0.3 * s, x + (radius + 0.06) * s, y0), fill=(34, 34, 40, 255))
    d.polygon([(x - (radius + 0.1) * s, y0 - tall * s), (x + (radius + 0.1) * s, y0 - tall * s), (x + 0.2 * s, y0 - (tall + 0.55) * s), (x - 0.2 * s, y0 - (tall + 0.55) * s)], fill=(40, 42, 50, 255))
    d.ellipse((x - 0.12 * s, y0 - (tall + 0.75) * s, x + 0.12 * s, y0 - (tall + 0.5) * s), fill=(40, 42, 50, 255))
    small = img.resize((W, H), Image.LANCZOS)
    arr = np.asarray(small).astype(np.float64)
    calm = 0.68 + 0.32 * np.clip((np.arange(W) - 250) / 700, 0, 1)
    arr[..., :3] *= calm[None, :, None]
    return Image.fromarray(arr.astype(np.uint8), "RGBA"), lamps


def muted(image):
    """Nimmt die Farbe zurück, nur Rot bleibt kräftig: Banner, Plakate und die Leuchtschrift tragen den einzigen satten Ton."""
    arr = np.asarray(image).astype(np.float64)
    rgb = arr[..., :3]
    grey = (rgb * np.array((0.3, 0.52, 0.18))[None, None, :]).sum(axis=2, keepdims=True)
    red = np.clip((rgb[..., 0:1] - 1.55 * np.maximum(rgb[..., 1:2], rgb[..., 2:3])) / 40, 0, 1)
    keep = 0.42 + 0.58 * red
    arr[..., :3] = np.clip(grey + (rgb - grey) * keep, 0, 255)
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def cropped(image):
    box = image.getbbox() or (0, 0, 1, 1)
    return image.crop(box), box


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    sky = sky_layer()
    works, works_lit, works_windows, sign, gate, stacks = works_layer()
    street, puddles = street_layer()
    left, left_lit, left_windows = side_layer(-1, LEFT)
    right, right_lit, right_windows = side_layer(1, RIGHT)
    front, lamps = front_layer()
    layers = [("sky", sky, None, 0.0), ("factory", works, works_lit, 0.15), ("street", street, None, 0.5), ("left", left, left_lit, 0.6),
              ("right", right, right_lit, 0.6), ("front", front, None, 0.85)]
    windows = works_windows + left_windows + right_windows
    info = {"size": [W, H], "vanish": [VX, HY], "horizon": HY, "layers": [], "windows": windows, "sign": sign, "gate": gate, "stacks": stacks,
            "lamps": lamps, "puddles": puddles, "walk": [{"x": round(sx(x, z)), "y": round(sy(0, z)), "scale": round(FOCAL / z, 2)} for x, z in WALK]}
    whole = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    pick = random.Random(7)
    for name, image, lit, depth in layers:
        image = muted(image)
        lit = muted(lit) if lit is not None else None
        part, box = cropped(image)
        part.save(OUT / f"title_{name}.png", optimize=True)
        entry = {"name": name, "file": f"title_{name}.png", "lit": None, "x": box[0], "y": box[1], "w": part.width, "h": part.height, "depth": depth}
        whole.alpha_composite(image)
        if lit is not None:
            lit.crop(box).save(OUT / f"title_{name}_lit.png", optimize=True)
            entry["lit"] = f"title_{name}_lit.png"
            # Im fertigen Bild brennt nur ein Teil der Fenster, links kaum eines. Die Leuchtschrift brennt ganz.
            shown = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            for window in windows:
                if window["layer"] == name:
                    x, y, w, h = window["rect"]
                    if pick.random() < (0.1 if x < 760 else 0.34 if name != "factory" else 0.75):
                        shown.alpha_composite(lit.crop((x, y, x + w, y + h)), (x, y))
            if name == "factory":
                x, y, w, h = sign["rect"]
                shown.alpha_composite(lit.crop((x, y, x + w, y + h)), (x, y))
                halo = shown.filter(ImageFilter.GaussianBlur(9))
                whole.alpha_composite(halo)
            whole.alpha_composite(shown)
        info["layers"].append(entry)
    whole.convert("RGB").save(OUT / "title.png", optimize=True)
    (OUT / "title.json").write_text(json.dumps(info, ensure_ascii=False, indent=1), encoding="utf-8")
    print("Titelbild geschrieben:", OUT, len(windows), "Fenster")


if __name__ == "__main__":
    main()
