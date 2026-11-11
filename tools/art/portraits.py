"""Brustbilder der Agenten für die Akten im HUD, wie Fotos aus einer Personalakte von 1927.

Dieselben Gestalten wie im Figurenblatt (AGENTS in generate_concept.py), nur näher und fast von vorn aufgenommen.
Aufruf: python tools/art/portraits.py   (wenige Sekunden)
Ergebnis: game/assets/hud/portrait_<name>.png, 192 × 256 Bildpunkte, gedacht für die Anzeige in halber Größe (96 × 128).
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import figures3d as f  # noqa: E402
from generate_concept import AGENTS  # noqa: E402

W, H = 192, 256
SUPER = 2
PIXELS_PER_UNIT = 7.0  # So groß erscheint eine Modelleinheit im Bild.
HEAD_AT = (0.5, 0.36)  # Wo der Kopf im Bild sitzt, als Anteil von Breite und Höhe
TILT = math.radians(4)  # Die Kamera schaut nur leicht von oben, anders als im Spiel.
TURN = math.radians(76)  # Die Figur steht fast frontal, eine Schulter etwas vor.
BORDER = 10
PAPER = np.array((226, 216, 190), np.float32)
SEPIA_DARK, SEPIA_LIGHT = np.array((38, 28, 20), np.float32), np.array((232, 214, 176), np.float32)
COLOR_LEFT = 0.3  # So viel von der Farbe der Kleidung bleibt im vergilbten Foto.

# Wer keiner der vier ist: Schiebermütze, rostbrauner Mantel wie die Rekruten im Spiel, kein Schal.
REKRUT = dict(AGENTS["Greta"], hat="peak", hat_c=(86, 78, 70), coat=AGENTS["Greta"]["variants"]["rekrut"]["coat"], bulk=1.05)
REKRUT.pop("scarf")
PEOPLE = {"greta": AGENTS["Greta"], "otto": AGENTS["Otto"], "emil": AGENTS["Emil"], "hanna": AGENTS["Hanna"], "rekrut": REKRUT}


def portrait(name, spec):
    spec = dict(spec, gun="none")
    rig = f.stand_rig(0.0)
    for side in (1, -1):
        rig["arms"][side] = ("fk", 0.04, 0.15)
    pose = f.body(spec, rig)
    # Der Mützenschirm säße im Foto über den Augen. Er rückt höher und wird schmaler.
    brow = f.upper(rig, (0.5, 0.0, f.SHOULDER_Z + 5.0))[2] + 1.0
    visor = [part for part in pose.parts if part[4] == f.SHOE and min(part[0][2], part[1][2]) > brow]
    pose.parts = [part for part in pose.parts if not any(part is v for v in visor)]
    for a, b, _r1, _r2, material, kind in visor:
        pose.parts.append((a + (0.3, 0, 0.75), b + (0.3, 0, 0.75), 0.4, 0.4, material, kind))
    # Das Gesicht gibt es nur hier: Im Spiel ist der Kopf wenige Bildpunkte groß, im Foto muss man jemanden erkennen.
    face = f.upper(rig, (0.5, 0.0, f.SHOULDER_Z + 5.0))
    for side in (1, -1):
        pose.ball(face + (2.62, side * 1.05, -0.15), 0.3, f.SHOE)
        pose.capsule(face + (2.78, side * 0.6, 0.45), face + (2.62, side * 1.6, 0.35), 0.16, 0.16, f.HAIR)
        pose.ball(face + (0.1, side * 2.9, -0.2), 0.75, f.SKIN)
    pose.capsule(face + (2.8, 0.0, -0.1), face + (3.25, 0.0, -0.85), 0.3, 0.42, f.SKIN)
    pose.capsule(face + (2.3, 0.5, -1.75), face + (2.3, -0.5, -1.75), 0.11, 0.11, f.COAT_D)
    scale = spec.get("scale", 1.0)
    forward = np.array((math.cos(TURN), math.sin(TURN), 0.0))
    side = np.array((-math.sin(TURN), math.cos(TURN), 0.0))
    parts = []
    for a, b, r1, r2, material, kind in pose.parts:
        world = [(q[0] * forward + q[1] * side + np.array((0, 0, q[2]))) * scale for q in (a, b)]
        parts.append((world[0], world[1], r1 * scale, r2 * scale, material, kind))

    to_camera = np.array((0.0, math.cos(TILT), math.sin(TILT)))
    up = np.array((0.0, -math.sin(TILT), math.cos(TILT)))
    right = np.array((1.0, 0.0, 0.0))
    head = np.array((0.0, 0.0, (rig["pelvis"][2] + f.SHOULDER_Z + 5.0) * scale))
    w, h = W * SUPER, H * SUPER
    xs = ((np.arange(w) + 0.5) / SUPER - W * HEAD_AT[0]) / PIXELS_PER_UNIT
    ys = ((np.arange(h) + 0.5) / SUPER - H * HEAD_AT[1]) / PIXELS_PER_UNIT
    gx, gy = np.meshgrid(xs, ys)
    origin = head + gx.ravel()[:, None] * right - gy.ravel()[:, None] * up + to_camera * 70
    position = origin.copy()
    alive = np.ones(len(position), bool)
    hit = np.zeros(len(position), bool)
    for _ in range(48):
        index = np.flatnonzero(alive)
        if len(index) == 0:
            break
        d, _nearest = f.sdf(position[index], parts)
        position[index] -= to_camera * d[:, None]
        done = d < 0.03
        hit[index[done]] = True
        alive[index[done | (np.linalg.norm(position[index] - origin[index], axis=1) > 140)]] = False

    # Atelierhintergrund: oben heller, zu den Ecken dunkler
    rng = np.random.default_rng(sum(map(ord, name)))
    fy, fx = np.mgrid[0:h, 0:w] / np.array((h, w))[:, None, None]
    back = 0.62 - 0.25 * fy + 0.1 * np.exp(-((fx - 0.5) ** 2 + (fy - 0.35) ** 2) / 0.08)
    tone = back.copy().ravel()
    tint = np.zeros((h * w, 3), np.float32)
    index = np.flatnonzero(hit)
    pts = position[index]
    _d, nearest = f.sdf(pts, parts)
    e = 0.15
    normal = np.stack([f.sdf(pts + np.eye(3)[k] * e, parts)[0] - f.sdf(pts - np.eye(3)[k] * e, parts)[0] for k in range(3)], axis=1)
    normal /= np.maximum(np.linalg.norm(normal, axis=1, keepdims=True), 1e-5)
    # Licht des Fotografen: von vorn links oben, weich
    lamp = np.array((-0.45, 0.6, 0.66))
    lamp /= np.linalg.norm(lamp)
    shade = 0.42 + 0.7 * np.clip(normal @ lamp, 0, 1) + 0.12 * np.clip(normal @ to_camera, 0, 1)
    colors = f.palette(spec)
    material = np.array([colors[part[4]] for part in parts], np.float32)[nearest]
    grey = material.mean(axis=1) / 255
    tone[index] = np.clip(grey * shade * 1.25, 0, 1)
    tint[index] = material / np.maximum(material.mean(axis=1, keepdims=True), 1) - 1

    tone = tone.reshape(h, w)
    tone = tone.reshape(H, SUPER, W, SUPER).mean(axis=(1, 3))
    tint = tint.reshape(H, SUPER, W, SUPER, 3).mean(axis=(1, 3))
    # Vergilbtes Foto: Helligkeit auf eine Sepia-Rampe, ein Rest Farbe, Vignette und Korn
    yy, xx = np.mgrid[0:H, 0:W]
    vignette = 1 - 0.35 * (((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2)
    tone = np.clip(tone * vignette + rng.normal(0, 0.025, tone.shape), 0, 1) ** 0.9
    image = SEPIA_DARK + (SEPIA_LIGHT - SEPIA_DARK) * tone[..., None]
    image *= 1 + COLOR_LEFT * tint
    # Weißer Fotorand, an den Kanten leicht abgegriffen
    edge = np.minimum.reduce([xx, yy, W - 1 - xx, H - 1 - yy])
    paper = PAPER * (0.94 + 0.06 * rng.random((H, W, 1)))
    image = np.where((edge < BORDER)[..., None], paper, image)
    image = np.where(((edge == BORDER) | (edge == BORDER + 1))[..., None], image * 0.7, image)
    out = np.dstack([np.clip(image, 0, 255), np.full((H, W), 255.0)]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def main():
    folder = f.ROOT / "game" / "assets" / "hud"
    folder.mkdir(parents=True, exist_ok=True)
    for name, spec in PEOPLE.items():
        portrait(name, spec).save(folder / f"portrait_{name}.png", optimize=True)
        print(f"portrait_{name}.png")


if __name__ == "__main__":
    main()
