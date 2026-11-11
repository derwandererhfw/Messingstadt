"""Titelbild: Blick eine nasse Straße hinunter auf die Brandt-Werke, nachts im Regen.

Schwarzweiß mit Blau als einziger Farbe (Noir-Styleguide, Abschnitt 4: Blau gehört den Mächtigen). Blau leuchten nur
die Bogenlampen in den Werkfenstern, der Schriftzug und die Suchscheinwerfer über dem Rauch. Laternen und Fenster
der Mietshäuser bleiben weiß. Die linke Bildhälfte bleibt ruhig und dunkel, dort steht das Menü.

Die Straße wird als einfache Szene mit Strahlen berechnet: Pflaster, zwei Häuserzeilen, die Werkhalle mit Schloten.
Das nasse Pflaster spiegelt die Szene, gerechnet als Strahl von einem Auge unter dem Boden. Gerendert wird doppelt
so fein und dann auf das Pixelraster der Spielgrafik gebracht: ein Bildpunkt der Stadt sind zwei Pixel auf dem Schirm.

Aufruf: python tools/art/title.py   (etwa eine halbe Minute)
Schreibt game/assets/title/title.png (1920 × 1080) und title_logo.png (durchsichtiger Grund).
"""
import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_concept import text, text_size  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "game" / "assets" / "title"
FONTS = ROOT / "game" / "assets" / "fonts"

SCREEN_W, SCREEN_H = 1920, 1080
PIXEL = 2  # so groß wie ein Bildpunkt der Stadt im Spiel
SUPER = 2
W, H = SCREEN_W // PIXEL * SUPER, SCREEN_H // PIXEL * SUPER
VX, VY = W * 0.64, H * 0.62  # Fluchtpunkt: Die Werke stehen rechts der Mitte, links bleibt Platz fürs Menü
FOCAL = W * 0.72
EYE = 1.4  # Augenhöhe in Metern, leicht von unten: Die Werke sollen drohen

KERB, FACADE = 5.5, 8.5  # halbe Straßenbreite bis zur Bordsteinkante und bis zu den Hauswänden
FLOOR, BAY = 3.4, 3.0
WORKS_Z, WORKS_HALF, HALL = 92.0, 46.0, 15.0
CHIMNEYS = ((-15.0, 1.8, 36.0), (-5.0, 2.1, 41.0), (12.0, 1.7, 33.0))  # x, halbe Breite, Höhe
SIGN = (-8.0, 8.0, 18.6, 21.3)  # x0, x1, unten, oben
LAMP_HEIGHT, LAMP_POWER = 4.3, 7.0
LAMPS = [(6.4, z) for z in (7.0, 19.0, 31.0, 43.0, 55.0)] + [(-6.4, z) for z in (26.0, 50.0)]
BEAMS = ((-12.0, -0.5), (-2.0, 0.18), (14.0, 0.62))  # Fuß auf dem Hallendach (x) und Neigung gegen die Senkrechte
FOG = 0.05
ACCENT = np.array((0.36, 0.6, 1.0))
BLACK_POINT, CONTRAST = 0.05, 1.3
SEED = 1927


def value_noise(rng, cells_x, cells_y, w=W, h=H):
    """Weiches Rauschen zwischen 0 und 1 aus einem groben Zufallsgitter."""
    grid = (rng.random((cells_y, cells_x)) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(grid).resize((w, h), Image.BICUBIC), np.float32) / 255


def hash2(a, b, seed):
    h = (a.astype(np.int64) * 374761393 + b.astype(np.int64) * 668265263 + seed * 2147483647) & 0xffffffff
    h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
    return ((h ^ (h >> 16)) & 0xffff) / 65535.0


def blocks(side, rng):
    """Häuserzeile als Liste (von, bis, Höhe). Zwischen manchen Häusern liegt eine finstere Einfahrt."""
    out, z = [], 0.5
    while z < WORKS_Z - 6:
        length = rng.uniform(9, 16)
        height = rng.uniform(10, 15) + (2 if side > 0 else 0)
        out.append((z, min(z + length, WORKS_Z - 6), height))
        z += length + (rng.uniform(2.2, 3.2) if rng.random() < 0.45 else 0.0)
    return out


def roof_line(x):
    """Sheddach der Werkhalle: Zähne von fünf Metern Breite."""
    return HALL + 3.0 * ((x + WORKS_HALF) % 5.0) / 5.0


def sign_mask():
    """Schriftzug auf dem Hallendach in der Pixelschrift des Spiels, als Maske über (x, Höhe)."""
    word = "BRANDT WERKE"
    tw, th = text_size(word, 1)
    img = Image.new("L", (tw + 2, th + 2), 0)
    text(ImageDraw.Draw(img), 1, 1, word, 255, 1, worn=0.0)
    return np.asarray(img, np.float32) / 255


def facade(side, z, height, lit_rate):
    """Hauswand an der Stelle z und Höhe height: Grauwert und Selbstleuchten (Fenster)."""
    bay = np.floor(z / BAY)
    floor = np.floor(height / FLOOR)
    u, v = (z % BAY) / BAY, (height % FLOOR) / FLOOR
    brick = 0.24 + 0.06 * hash2(np.floor(z * 5), np.floor(height * 9), 3 + side)
    gray = brick * (0.9 + 0.1 * hash2(bay, floor, 5 + side))
    ledge = (v < 0.06) & (floor >= 1)
    gray = np.where(ledge, 0.42, gray)
    window = (u > 0.3) & (u < 0.7) & (v > 0.28) & (v < 0.78) & (floor >= 1)
    shop = (floor == 0) & (u > 0.12) & (u < 0.88) & (v > 0.2) & (v < 0.8)
    lit = window & (hash2(bay, floor, 11 + side) < lit_rate)
    gray = np.where(window | shop, 0.035, gray)
    # Erleuchtete Zimmer: hell zur Mitte des Fensters, oben dunkler, manchmal ein Vorhang
    room = 0.55 + 0.35 * hash2(bay, floor, 17 + side) - 0.25 * (v - 0.28)
    curtain = hash2(bay, floor, 23 + side) < 0.4
    room = np.where(curtain & (np.abs(u - 0.5) > 0.12), room * 0.45, room)
    glow = np.where(lit, room, 0.0)
    return gray, glow


def lamp_light(px, py, pz, normal_x=None):
    """Licht der Laternen auf einer Fläche: am Boden von oben, an den Wänden von der Seite."""
    total = np.zeros_like(px)
    for lx, lz in LAMPS:
        dx, dy, dz = lx - px, LAMP_HEIGHT - py, lz - pz
        d2 = dx * dx + dy * dy + dz * dz
        cos = (dy if normal_x is None else dx * normal_x) / np.sqrt(d2)
        total += LAMP_POWER * np.clip(cos, 0, 1) / np.maximum(d2, 0.5)
    return total


def trace(x, k, eye):
    """Strahl durch die Bildspalte x mit Höhe(z) = eye + k * z. Liefert Tiefe, Grauwert, Blau und Selbstleuchten.

    Für das Spiegelbild im Pflaster ist eye negativ: Der Strahl kommt aus dem Boden und gilt erst darüber.
    """
    a = (x - VX) / FOCAL
    n = x.shape[0]
    depth = np.full(n, np.inf, np.float32)
    gray = np.zeros(n, np.float32)
    blue = np.zeros(n, np.float32)
    glow = np.zeros(n, np.float32)
    emerge = np.where(eye < 0, -eye / np.maximum(k, 1e-6), 0.0)

    # Pflaster
    if eye > 0:
        zg = np.where(k < 0, -eye / np.minimum(k, -1e-6), np.inf)
        xg = a * zg
        hit = np.isfinite(zg) & (np.abs(xg) < FACADE) & (zg < WORKS_Z)
        depth[hit] = zg[hit]
        gray[hit] = -1.0  # Boden wird später schattiert, er braucht das Spiegelbild

    # Häuserzeilen
    zs = FACADE / np.maximum(np.abs(a), 1e-6)
    hs = eye + k * zs
    for side, rows, lit_rate in ((1, RIGHT, 0.34), (-1, LEFT, 0.12)):
        on_side = (np.sign(a) == side) & (zs < depth) & (zs > emerge) & (zs < WORKS_Z) & (hs >= 0)
        for z0, z1, top in rows:
            hit = on_side & (zs >= z0) & (zs < z1) & (hs < top)
            if not hit.any():
                continue
            g, e = facade(side, zs[hit], hs[hit], lit_rate)
            cornice = hs[hit] > top - 0.5
            g = np.where(cornice, 0.36, g)
            light = 0.006 + 0.6 * lamp_light(np.full(g.shape, side * FACADE), hs[hit], zs[hit], -side)
            gray[hit] = g * light + e
            glow[hit] = e
            depth[hit] = zs[hit]
        # Einfahrten zwischen den Häusern: nur Schwärze
        gap = on_side & ~np.isfinite(depth) & (hs < 12) & (zs < WORKS_Z - 6)
        depth[gap] = zs[gap]
        gray[gap] = 0.012

    # Werkhalle mit Schloten und Schriftzug
    xw, hw = a * WORKS_Z, eye + k * WORKS_Z
    free = ~np.isfinite(depth) & (hw >= 0)
    hall = free & (np.abs(xw) < WORKS_HALF) & (hw < roof_line(xw))
    bay, band = np.floor((xw + WORKS_HALF) / 3.2), (xw + WORKS_HALF) % 3.2
    windows = hall & (band > 0.7) & (band < 2.5) & (hw > 3.5) & (hw < 10.5) & (hash2(bay, bay * 0, 31) < 0.8)
    gray[hall] = 0.05
    flicker = 0.75 + 0.25 * hash2(bay, np.floor(hw * 2), 37)
    blue[windows] = 0.75 * flicker[windows]
    gray[windows] = 0.12
    depth[hall] = WORKS_Z
    x0, x1, s0, s1 = SIGN
    on_sign = free & (xw >= x0) & (xw < x1) & (hw >= s0) & (hw < s1)
    if on_sign.any():
        mask = SIGN_MASK
        mx = ((xw[on_sign] - x0) / (x1 - x0) * mask.shape[1]).astype(int).clip(0, mask.shape[1] - 1)
        my = ((s1 - hw[on_sign]) / (s1 - s0) * mask.shape[0]).astype(int).clip(0, mask.shape[0] - 1)
        letters = mask[my, mx] > 0.5
        idx = np.flatnonzero(on_sign)
        blue[idx[letters]] = 1.25
        gray[idx[letters]] = 0.35
        depth[idx[letters]] = WORKS_Z - 0.5
        frame = idx[~letters & (hw[on_sign] < s0 + 0.35)]
        gray[frame] = 0.04
        depth[frame] = WORKS_Z - 0.5
    for cx, half, top in CHIMNEYS:
        stack = free & (np.abs(xw - cx) < half * (1 - 0.18 * hw / top)) & (hw < top)
        ring = stack & (((hw % 9.0) < 0.5) | (hw > top - 1.2))
        gray[stack] = 0.04
        gray[ring] = 0.09
        depth[stack] = WORKS_Z - 1
    return depth, gray, blue, glow


def screen_y(height, z, eye=EYE):
    return VY + (eye - height) * FOCAL / z


def screen_x(x, z):
    return VX + x * FOCAL / z


def sky(xs, ys, rng_fields):
    """Himmel in Bildkoordinaten: Smog, Schein der Werke hinter den Schloten, Rauchfahnen und Suchscheinwerfer."""
    smog, wisps, grain = rng_fields
    gray = 0.012 + 0.03 * smog[ys, xs]
    # Gegenlicht: Die Stadt glimmt über dem Horizont, am hellsten hinter den Werken
    gx = (xs - VX) / (W * 0.45)
    gy = (VY - ys) / (H * 0.42)
    gray += 0.3 * np.exp(-gx * gx * 2.2 - np.maximum(gy, 0) ** 1.3 * 3.0) * (0.6 + 0.6 * smog[ys, xs])
    blue = np.zeros_like(gray)
    for cx, half, top in CHIMNEYS:
        sx, sy = screen_x(cx, WORKS_Z), screen_y(top, WORKS_Z)
        rise = np.maximum(sy - ys, 0)
        centre = sx + 0.0016 * rise ** 1.9
        width = 6 + 0.33 * rise
        plume = np.exp(-((xs - centre) / width) ** 2) * (rise > 0) * np.exp(-rise / (H * 0.55))
        plume *= 0.55 + 0.6 * wisps[ys, xs]
        gray = gray * (1 - 0.55 * plume) + 0.2 * plume
    beam_sum = np.zeros_like(gray)
    for foot, tilt in BEAMS:
        ox, oy = screen_x(foot, WORKS_Z), screen_y(roof_line(foot) + 0.5, WORKS_Z)
        dx, dy = math.sin(tilt), -math.cos(tilt)
        t = (xs - ox) * dx + (ys - oy) * dy
        perp = np.abs((xs - ox) * dy - (ys - oy) * dx)
        width = 3 + 0.075 * np.maximum(t, 0)
        beam_sum += np.exp(-(perp / width) ** 2) * (t > 0) * np.exp(-np.maximum(t, 0) / (H * 1.4))
    # Der Strahl zeigt sich nur, wo Rauch und Dunst ihn auffangen.
    beam_sum *= 0.45 + 0.9 * smog[ys, xs] * (0.6 + 0.4 * wisps[ys, xs])
    blue += 0.38 * beam_sum
    gray += 0.05 * beam_sum
    return gray, blue


def render():
    rng = np.random.default_rng(SEED)
    fields = (value_noise(rng, 14, 9), value_noise(rng, 60, 40), value_noise(rng, 200, 120))
    puddles = value_noise(rng, 26, 30)
    ripple = value_noise(rng, 240, 40)
    ys, xs = np.mgrid[0:H, 0:W]
    xs_f, ys_f = xs.astype(np.float32).ravel() + 0.5, ys.astype(np.float32).ravel() + 0.5
    k = -(ys_f - VY) / FOCAL
    depth, gray, blue, glow = trace(xs_f, k, EYE)
    gray, blue = gray.reshape(H, W), blue.reshape(H, W)
    depth = depth.reshape(H, W)

    open_sky = ~np.isfinite(depth)
    sg, sb = sky(xs[open_sky], ys[open_sky], fields)
    gray[open_sky], blue[open_sky] = sg, sb

    # Pflaster: Reihensteine quer zur Straße, Gehweg aus Platten, dazu das Spiegelbild der nassen Fläche
    ground = gray < -0.5
    zg = depth[ground]
    xg = (xs[ground] + 0.5 - VX) / FOCAL * zg
    road = np.abs(xg) < KERB
    row = np.floor(zg / 0.24)
    stone = np.floor(xg / 0.34 + 0.5 * (row % 2))
    joint = ((zg / 0.24) % 1 < 0.16) | ((xg / 0.34 + 0.5 * (row % 2)) % 1 < 0.1)
    slab = (np.floor(zg / 1.1), np.floor(xg / 1.1))
    slab_joint = ((zg / 1.1) % 1 < 0.05) | ((xg / 1.1) % 1 < 0.05)
    albedo = np.where(road, 0.2 + 0.07 * hash2(row, stone, 41), 0.28 + 0.04 * hash2(*slab, 43))
    albedo = np.where(road & joint, 0.07, albedo)
    albedo = np.where(~road & slab_joint, 0.1, albedo)
    albedo = np.where(np.abs(np.abs(xg) - KERB) < 0.14, 0.42, albedo)
    # Fernes Pflaster verschwimmt zu einem Ton, sonst flimmert es.
    far = np.clip((zg - 12) / 40, 0, 1)
    albedo = albedo * (1 - far) + 0.2 * far
    diffuse = albedo * (0.006 + lamp_light(xg, np.zeros_like(xg), zg))
    wet = np.clip(0.25 + 0.6 * (puddles[ground] - 0.5) * 3, 0.2, 0.9)
    wet = np.where(road & joint & (zg < 14), wet * 0.55, wet)
    # Spiegelbild: gleicher Strahl, nach oben geklappt. Die Riffelung der Steine zieht es in senkrechte Streifen.
    kr = -k.reshape(H, W)[ground] * (1 + 0.12 * (ripple[ground] - 0.5))
    rdepth, rgray, rblue, _ = trace(xs[ground].astype(np.float32) + 0.5, kr, -EYE)
    up = ~np.isfinite(rdepth)
    sky_y = np.clip((VY - kr * FOCAL).astype(int), 0, H - 1)
    rg2, rb2 = sky(xs[ground][up], sky_y[up], fields)
    rgray[up], rblue[up] = rg2, rb2
    gray[ground] = diffuse * (1 - wet) + rgray * wet * 0.6
    blue[ground] = rblue * wet * 0.7

    # Laternen: Mast, Kopf und Schein, im Pflaster gespiegelt als lange Streifen
    for lx, lz in sorted(LAMPS, key=lambda lamp: -lamp[1]):
        sx, top = screen_x(lx, lz), screen_y(LAMP_HEIGHT, lz)
        foot = screen_y(0, lz)
        size = 0.2 * FOCAL / lz
        pole = (np.abs(xs + 0.5 - sx) < max(0.6, 0.07 * FOCAL / lz)) & (ys > top) & (ys < foot) & (depth > lz)
        gray[pole] = 0.03
        blue[pole] = 0
        r2 = ((xs + 0.5 - sx) ** 2 + (ys + 0.5 - top) ** 2) / size ** 2
        head = (r2 < 1) & (depth > lz)
        gray[head] = 1.6
        gray += 0.22 * np.exp(-r2 / 12) + 0.05 * np.exp(-r2 / 160)
        mirror = screen_y(-LAMP_HEIGHT, lz)
        streak = np.exp(-((xs + 0.5 - sx) / (size * 1.1)) ** 2 - ((ys + 0.5 - mirror) / (size * 7)) ** 2)
        streak *= np.where(ground, 1.0, 0.0)
        streak[ground] *= 0.6 + 0.8 * ripple[ground]
        gray += 0.5 * streak

    # Dunst schluckt die Ferne
    haze = 1 - np.exp(-np.where(np.isfinite(depth), depth, 200) / 150)
    haze = np.where(open_sky, 0, haze)
    gray = gray * (1 - haze) + FOG * haze
    blue *= 1 - 0.6 * haze

    gray += rain(rng)
    # Vignette: Die Ecken sinken ab, die linke Seite etwas mehr, dort steht das Menü
    nx, ny = (xs / W - 0.58) / 0.62, (ys / H - 0.48) / 0.62
    gray *= np.clip(1 - 0.55 * (nx * nx + ny * ny), 0.25, 1)
    blue *= np.clip(1 - 0.4 * (nx * nx + ny * ny), 0.3, 1)
    return gray, blue


def rain(rng):
    layer = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(layer)
    for _ in range(1400):
        x, y = rng.uniform(-60, W), rng.uniform(-40, H)
        length = rng.uniform(10, 26)
        d.line((x, y, x + length * 0.22, y + length), fill=int(rng.uniform(14, 42)))
    return np.asarray(layer, np.float32) / 255


def finish(gray, blue):
    """Linearen Grauwert und Blau zusammenführen, auf das Pixelraster der Stadt bringen, Korn darüber."""
    rgb = gray[..., None] * np.array((0.97, 0.98, 1.0)) + blue[..., None] * ACCENT
    small = rgb.reshape(H // SUPER, SUPER, W // SUPER, SUPER, 3).mean(axis=(1, 3))
    rng = np.random.default_rng(SEED + 1)
    small += rng.normal(0, 0.0025, small.shape[:2])[..., None]
    # Harte Kennlinie: Schwarz bleibt schwarz, die Mitten sinken, das Licht behält seine Spitzen.
    tone = np.clip((np.clip(small, 0, 1) ** (1 / 2.2) - BLACK_POINT) / (1 - BLACK_POINT), 0, 1) ** CONTRAST
    encoded = (tone * 255 + 0.5).astype(np.uint8)
    return Image.fromarray(encoded).resize((SCREEN_W, SCREEN_H), Image.NEAREST)


def logo():
    """Schriftzug MESSINGSTADT mit Jahreszahl, abgenutzt wie gestempelt. Halb so groß gesetzt, dann verdoppelt."""
    title_font = ImageFont.truetype(str(FONTS / "OldStandard-Bold.ttf"), 78)
    year_font = ImageFont.truetype(str(FONTS / "CourierPrime-Bold.ttf"), 22)
    word, tracking = "MESSINGSTADT", 7
    widths = [title_font.getbbox(c)[2] - title_font.getbbox(c)[0] for c in word]
    width = sum(widths) + tracking * (len(word) - 1)
    w, h = width + 40, 150
    ink = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(ink)
    x = 20
    for c, cw in zip(word, widths):
        d.text((x - title_font.getbbox(c)[0], 14), c, font=title_font, fill=255)
        x += cw + tracking
    year = "1 9 2 7"
    yw = year_font.getbbox(year)[2]
    line_y = 124
    top, bottom = year_font.getbbox(year)[1], year_font.getbbox(year)[3]
    d.text(((w - yw) // 2, line_y - (top + bottom) // 2), year, font=year_font, fill=255)
    for x0, x1 in ((20, (w - yw) // 2 - 16), ((w + yw) // 2 + 16, w - 20)):
        d.rectangle((x0, line_y, x1, line_y + 1), fill=255)
    # Abnutzung: kleine Fehlstellen in der Farbe, als wäre der Stempel trocken gewesen
    rng = random.Random(SEED)
    pix = ink.load()
    for _ in range(int(w * h * 0.006)):
        px, py = rng.randrange(w), rng.randrange(h)
        if pix[px, py]:
            for dx, dy in ((0, 0), (1, 0), (0, 1))[:rng.randint(1, 3)]:
                if px + dx < w and py + dy < h:
                    pix[px + dx, py + dy] = 0
    paper = (222, 218, 208)
    shadow = Image.new("L", (w, h), 0)
    shadow.paste(ink, (3, 3))
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.paste((8, 8, 12, 200), mask=shadow)
    out.paste(paper + (255,), mask=ink)
    return out.resize((w * 2, h * 2), Image.NEAREST)


LEFT = blocks(-1, random.Random(SEED + 2))
RIGHT = blocks(1, random.Random(SEED + 3))
SIGN_MASK = sign_mask()


def main():
    """Schreibt den Schriftzug. Das Titelbild selbst baut inzwischen title_scene.py in Ebenen, mit den Häusern des Spiels.

    Die alte Straße in Schwarzweiß und Blau gibt es weiter mit dem Schalter --altes-bild, als title_alt.png zum Vergleich.
    """
    OUT.mkdir(parents=True, exist_ok=True)
    if "--altes-bild" in sys.argv:
        gray, blue = render()
        finish(gray, blue).save(OUT.parents[2] / "work" / "title_alt.png", optimize=True)
    logo().save(OUT / "title_logo.png", optimize=True)
    print(f"Schriftzug in {OUT}")


if __name__ == "__main__":
    main()
