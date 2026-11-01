"""Einzelne Grafiken, die nicht zur Stadt gehören: Luftschiff, Feuertonne und Transparent.

Das Luftschiff wird wie die Figuren aus runden Körpern gerendert (figures3d), im selben Blickwinkel.
Aufruf: python tools/art/props.py
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from figures3d import LIGHT, RIGHT, SUPER, TO_CAMERA, UP, sdf  # noqa: E402
from generate_concept import C, text, text_size  # noqa: E402

ASSETS = Path(__file__).resolve().parents[2] / "game" / "assets"
SHIP_W, SHIP_H = 300, 170
HULL_LENGTH = 190.0
HULL_SEGMENTS = 18
SHIP_YAW = math.radians(45)  # fliegt auf der Karte entlang +x, im Bild nach rechts unten
HULL_DARK, HULL_LIGHT = (70, 74, 84), (96, 100, 108)
WINDOW_COLOR = (255, 214, 130)


def hull_radius(u):
    """Zigarrenform: vorn rund, hinten spitz zulaufend."""
    return 34 * (max(0.0, 1 - (u * 2 - 0.85) ** 2 / 1.75)) ** 0.5 + 3


def ship_parts():
    forward = np.array((math.cos(SHIP_YAW), math.sin(SHIP_YAW), 0.0))
    side = np.array((-math.sin(SHIP_YAW), math.cos(SHIP_YAW), 0.0))
    up = np.array((0.0, 0.0, 1.0))
    parts = []

    def place(x, y, z):
        return forward * x + side * y + up * z

    # Hülle aus Abschnitten in zwei Tönen: Das ergibt die Spanten
    for i in range(HULL_SEGMENTS):
        u0, u1 = i / HULL_SEGMENTS, (i + 1) / HULL_SEGMENTS
        a = place(HULL_LENGTH * (u0 - 0.5), 0, 0)
        b = place(HULL_LENGTH * (u1 - 0.5), 0, 0)
        parts.append((a, b, hull_radius(u0), hull_radius(u1), HULL_DARK if i % 2 else HULL_LIGHT))
    # Leitwerk
    tail = -HULL_LENGTH * 0.42
    fin = (58, 40, 44)
    for y, z in ((0, 34), (0, -34), (30, 0), (-30, 0)):
        parts.append((place(tail, 0, 0), place(tail - 28, y, z), 7, 2.5, fin))
    # Gondel mit Fenstern und zwei Motoren
    gondola = (34, 34, 42)
    parts.append((place(34, 0, -40), place(-14, 0, -40), 8, 8, gondola))
    parts.append((place(30, 0, -30), place(-8, 0, -30), 3, 3, gondola))
    for i in range(5):
        parts.append((place(28 - i * 9, 7.5, -39), place(28 - i * 9, 7.5, -39), 1.8, 1.8, WINDOW_COLOR))
    for y in (24, -24):
        parts.append((place(-34, y, -24), place(-48, y, -24), 5, 3.5, gondola))
        parts.append((place(-34, y * 0.4, -14), place(-34, y, -24), 1.2, 1.2, gondola))
    return parts


def render_ship():
    w, h = SHIP_W * SUPER, SHIP_H * SUPER
    xs = (np.arange(w) + 0.5) / SUPER - SHIP_W / 2
    ys = (np.arange(h) + 0.5) / SUPER - SHIP_H / 2
    gx, gy = np.meshgrid(xs, ys)
    origin = gx.ravel()[:, None] * RIGHT - gy.ravel()[:, None] * UP + TO_CAMERA * 260
    parts = [(a, b, r1, r2, i) for i, (a, b, r1, r2, _) in enumerate(ship_parts())]
    colors = np.array([c for *_, c in ship_parts()], np.float32)
    position = origin.copy()
    alive = np.ones(len(position), bool)
    hit = np.zeros(len(position), bool)
    for _ in range(60):
        index = np.flatnonzero(alive)
        if len(index) == 0:
            break
        d, _near = sdf(position[index], parts)
        position[index] -= TO_CAMERA * d[:, None]
        done = d < 0.05
        hit[index[done]] = True
        alive[index[done | (np.linalg.norm(position[index] - origin[index], axis=1) > 540)]] = False
    image = np.zeros((h, w, 4), np.float32)
    index = np.flatnonzero(hit)
    pts = position[index]
    _d, nearest = sdf(pts, parts)
    normal = np.stack([sdf(pts + np.eye(3)[i] * 0.3, parts)[0] - sdf(pts - np.eye(3)[i] * 0.3, parts)[0] for i in range(3)], axis=1)
    normal /= np.maximum(np.linalg.norm(normal, axis=1, keepdims=True), 1e-5)
    shade = (0.45 + 0.75 * np.clip(normal @ LIGHT, 0, 1)) * (0.7 + 0.3 * np.clip(normal @ TO_CAMERA, 0, 1))
    shade = np.round(shade * 8) / 8
    rgb = colors[nearest] * shade[:, None]
    # Fenster leuchten selbst und bleiben von der Schattierung unberührt.
    glowing = np.all(colors[nearest] == np.array(WINDOW_COLOR, np.float32), axis=1)
    rgb[glowing] = colors[nearest][glowing]
    rows, cols = np.unravel_index(index, (h, w))
    image[rows, cols, :3] = np.clip(rgb, 0, 255)
    image[rows, cols, 3] = 1
    alpha = image[..., 3].reshape(SHIP_H, SUPER, SHIP_W, SUPER).mean(axis=(1, 3))
    summed = (image[..., :3] * image[..., 3:]).reshape(SHIP_H, SUPER, SHIP_W, SUPER, 3).sum(axis=(1, 3))
    color = summed / np.maximum(alpha[..., None] * SUPER * SUPER, 1e-5)
    out = np.zeros((SHIP_H, SHIP_W, 4), np.uint8)
    solid = alpha >= 0.5
    out[solid, :3] = color[solid].astype(np.uint8)
    out[solid, 3] = 255
    Image.fromarray(out).save(ASSETS / "airship.png")


def barrel():
    """Ölfass mit Rostflecken und Luftlöchern, aus denen die Glut scheint. Fuß bei (10, 24)."""
    img = Image.new("RGBA", (20, 26), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((3, 19, 16, 25), fill=C["soot"])
    d.rectangle((3, 6, 16, 22), fill=C["rust_d"])
    d.rectangle((3, 6, 6, 22), fill=C["grime"])
    d.rectangle((13, 6, 16, 22), fill=C["soot"])
    d.line((3, 10, 16, 10), fill=C["rust"])
    d.line((3, 17, 16, 17), fill=C["rust"])
    d.ellipse((3, 3, 16, 9), fill=C["soot"])
    d.ellipse((5, 4, 14, 8), fill=(235, 120, 40))
    d.ellipse((7, 5, 12, 7), fill=(255, 205, 110))
    for x, y in ((6, 13), (10, 12), (13, 14), (8, 19), (12, 20)):
        d.rectangle((x, y, x + 1, y + 1), fill=(240, 130, 45))
    img.save(ASSETS / "barrel.png")


def banner():
    """Bettlaken mit der Parole, an der Wand zwischen zwei Latten aufgehängt."""
    word = "STREIK"
    tw, th = text_size(word, 2)
    w, h = tw + 14, th + 16
    img = Image.new("RGBA", (w, h + 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((1, 0, 2, h + 11), fill=C["wood_d"])
    d.rectangle((w - 3, 0, w - 2, h + 11), fill=C["wood_d"])
    d.polygon([(3, 2), (w - 4, 1), (w - 4, h - 2), (w // 2, h + 1), (3, h)], fill=(156, 140, 118))
    text(d, 7, 9, word, C["awn_r"], 2, worn=0.0)
    # Schräg wie die Wand, an der es hängt: im Bild fällt sie nach rechts ab.
    sheared = Image.new("RGBA", (img.width, img.height + img.width // 2 + 1), (0, 0, 0, 0))
    for u in range(img.width):
        sheared.paste(img.crop((u, 0, u + 1, img.height)), (u, u // 2))
    sheared.save(ASSETS / "banner.png")


if __name__ == "__main__":
    render_ship()
    barrel()
    banner()
    print("Luftschiff, Tonne und Transparent erzeugt")
