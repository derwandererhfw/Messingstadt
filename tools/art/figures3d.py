"""Rendert das Figurenblatt aus einem kleinen 3D-Modell mit Gelenken.

Jede Figur besteht aus runden Körperteilen (Kapseln und Kugeln), die Pose kommt aus einem Gelenkmodell.
Gerendert wird mit Strahlen im selben Blickwinkel wie die Stadt, dann auf Pixelgröße verkleinert.
Aufruf: python tools/art/figures3d.py   (etwa eine Minute)

Blatt: je Figur fünf Zeilen für die Blickrichtungen S, SO, O, NO, N (Westen entsteht durch Spiegeln).
Spalten: 0-7 gehen, 8-15 rennen, 16-19 stehen, 20-21 zielen, 22-29 zielend gehen, 30-37 und 38-45 sterben.
"""
import json
import math
import multiprocessing
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_concept import C, SPECS  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
CELL_W, CELL_H = 48, 50
FOOT_X, FOOT_Y = 24, 46
SUPER = 2
VIEWS = (90.0, 45.0, 0.0, -45.0, -90.0)  # Blickrichtung am Boden: 90 ist zur Kamera, 0 nach rechts im Bild
TO_CAMERA = np.array((0.0, math.cos(math.radians(30)), math.sin(math.radians(30))))
UP = np.array((0.0, -0.5, math.cos(math.radians(30))))
RIGHT = np.array((1.0, 0.0, 0.0))
LIGHT = np.array((-0.35, 0.3, 0.88)) / np.linalg.norm((-0.35, 0.3, 0.88))
SMOOTH = 0.4
HIP, THIGH, SHIN = 23.0, 12.0, 11.0
FALL_FRAMES = 8

# Teile der Figur, je ein Stoff
COAT, COAT_D, COAT_L, TROUSERS, SHOE, SKIN, HAIR, HAT, GUN, SHIRT = range(10)

# Blatt: Spalte zu Bewegung und Phase
FRAMES = (
    [("walk", i / 8) for i in range(8)]
    + [("run", i / 8) for i in range(8)]
    + [("idle", i / 4) for i in range(4)]
    + [("aim", 0.0), ("recoil", 0.0)]
    + [("aimwalk", i / 8) for i in range(8)]
    + [("fallA", i / (FALL_FRAMES - 1)) for i in range(FALL_FRAMES)]
    + [("fallB", i / (FALL_FRAMES - 1)) for i in range(FALL_FRAMES)]
)


def palette(spec):
    co_d, co, co_l = spec["coat"]
    return {
        COAT: C[co], COAT_D: C[co_d], COAT_L: C[co_l], TROUSERS: C[spec["legs"]], SHOE: C["black"],
        SKIN: C["skin"], HAIR: C[spec["hair"]], HAT: C[spec["hat_c"]], GUN: C["soot"], SHIRT: C[spec["shirt"]],
    }


class Pose:
    """Gelenke in Modelleinheiten: x nach vorn, y zur Seite, z nach oben. Die Füße stehen bei z = 0."""

    def __init__(self):
        self.parts = []

    def capsule(self, a, b, r1, r2, material):
        self.parts.append((np.array(a, float), np.array(b, float), r1, r2, material))

    def ball(self, p, r, material):
        self.capsule(p, p, r, r, material)


def leg_points(side, swing, knee):
    """Hüfte zu Knie zu Knöchel. swing nach vorn, knee beugt nach hinten."""
    hip = np.array((0.0, side * 3.0, 0.0))
    kx, kz = math.sin(swing) * THIGH, -math.cos(swing) * THIGH
    ax = kx + math.sin(swing - knee) * SHIN
    az = kz - math.cos(swing - knee) * SHIN
    return hip, hip + (kx, 0, kz), hip + (ax, 0, az), swing - knee


def build(spec, motion, phase):
    p = Pose()
    t = phase * math.tau
    swing_size, knee_size, lean, arm_swing, elbow = {
        "walk": (0.5, 0.75, 0.04, 0.42, 0.28),
        "aimwalk": (0.42, 0.65, 0.05, 0.3, 0.28),
        "run": (0.95, 1.55, 0.22, 0.9, 1.45),
    }.get(motion, (0.0, 0.0, 0.0, 0.0, 0.18))
    moving = motion in ("walk", "run", "aimwalk")
    swing_l = swing_size * math.sin(t) if moving else 0.0
    swing_r = -swing_l
    # Das Knie beugt sich, wenn das Bein nach vorn schwingt, und streckt sich vor dem Aufsetzen.
    knee_l = knee_size * max(0.0, math.sin(t + 1.2)) if moving else 0.03
    knee_r = knee_size * max(0.0, math.sin(t + 1.2 + math.pi)) if moving else 0.03
    dying = fall_pose(motion, phase) if motion.startswith("fall") else None
    if dying:
        swing_l, swing_r, knee_l, knee_r = dying["swing_l"], dying["swing_r"], dying["knee_l"], dying["knee_r"]
        lean = dying["lean"]
    breath = 0.35 * math.sin(t) if motion == "idle" else 0.0
    weight = 0.5 * math.sin(t) if motion == "idle" else 0.0

    hip_l, knee_pl, ankle_l, angle_l = leg_points(1, swing_l, knee_l)
    hip_r, knee_pr, ankle_r, angle_r = leg_points(-1, swing_r, knee_r)
    # Becken so hoch, dass der tiefere Fuß den Boden berührt: Daraus entsteht von selbst das Wippen beim Gehen.
    pelvis_z = -min(ankle_l[2], ankle_r[2]) + 1.6
    pelvis = np.array((0.0, weight, pelvis_z))

    def body(v):
        return pelvis + np.array(v, float)

    def lean_up(v):
        x, y, z = v
        return pelvis + np.array((x + z * math.sin(lean), y, z * math.cos(lean)))

    # Beine und Schuhe
    for hip, knee, ankle, foot_angle in ((hip_l, knee_pl, ankle_l, angle_l), (hip_r, knee_pr, ankle_r, angle_r)):
        h, k, a = body(hip), body(knee), body(ankle)
        p.capsule(h, k, 2.2, 1.8, TROUSERS)
        p.capsule(k, a, 1.8, 1.35, TROUSERS)
        toe = a + (3.6 * math.cos(max(foot_angle, -0.5)), 0, 3.6 * math.sin(max(foot_angle, -0.5)) - 0.2)
        p.capsule(a, toe, 1.7, 1.45, SHOE)

    # Rumpf: zwei Hälften nebeneinander ergeben einen runden Oberkörper mit Schultern.
    waist = 25.0 - HIP
    shoulder_z = 38.5 - HIP + breath
    for side in (1, -1):
        for depth in (1.1, -1.1):
            p.capsule(lean_up((depth, side * 1.7, waist)), lean_up((depth + 0.2, side * 2.9, shoulder_z - 1.2)), 2.5, 2.6, COAT)
    # Mantel oder Jacke unter dem Gürtel: folgt den Oberschenkeln und schwingt beim Gehen.
    hem_z = (40 - spec["hem"]) * 1.15
    hem = max(4.0, 25.0 - hem_z)
    for side, swing in ((1, swing_l), (-1, swing_r)):
        for depth in (1.0, -1.2):
            skirt_end = body((depth + math.sin(swing) * hem * 0.8, side * 2.5, waist - hem))
            p.capsule(lean_up((depth, side * 1.7, waist)), skirt_end, 2.4, 1.6 + hem * 0.09, COAT_D if side < 0 else COAT)
    # Gürtel und Kragen
    p.capsule(lean_up((0.4, 2.1, waist + 0.3)), lean_up((0.4, -2.1, waist + 0.3)), 3.05, 3.05, COAT_D)
    p.capsule(lean_up((0, 2.1, shoulder_z + 0.6)), lean_up((0, -2.1, shoulder_z + 0.6)), 1.5, 1.5, COAT_L)
    p.ball(lean_up((1.9, 0, shoulder_z - 1.4)), 1.05, SHIRT)

    # Kopf und Kopfbedeckung
    neck = lean_up((0.2, 0, shoulder_z + 1.5))
    head = lean_up((0.5, 0, shoulder_z + 5.0))
    p.capsule(neck, head, 1.4, 1.4, SKIN)
    p.ball(head, 2.9, SKIN)
    p.ball(head + (-1.0, 0, 0.4), 2.7, HAIR)
    if spec["hat"] == "fedora":
        # Breite Krempe aus einem Ring flacher Kugeln, darauf der schmalere Hutkopf
        brim = head + (0, 0, 1.5)
        for i in range(12):
            a = i / 12 * math.tau
            p.ball(brim + (math.cos(a) * 4.6, math.sin(a) * 4.6, 0), 0.75, HAT)
            p.ball(brim + (math.cos(a) * 2.6, math.sin(a) * 2.6, 0.1), 0.85, HAT)
        p.capsule(brim + (0, 0, 0.4), head + (0, 0, 4.3), 2.7, 2.2, COAT_D)
    else:
        top = head + (-0.2, 0, 2.4)
        for i in range(8):
            a = i / 8 * math.tau
            p.ball(top + (math.cos(a) * 2.5, math.sin(a) * 2.5, 0.5), 1.0, HAT)
        p.ball(top + (0, 0, 0.7), 2.6, HAT)
        p.capsule(head + (2.7, 1.5, 1.4), head + (2.7, -1.5, 1.4), 0.8, 0.8, SHOE)

    # Arme. Die rechte Hand führt die Waffe, sobald gezielt wird.
    aiming = motion in ("aim", "recoil", "aimwalk")
    kick = 0.32 if motion == "recoil" else 0.0
    for side, swing in ((1, -swing_l), (-1, -swing_r)):
        shoulder = lean_up((0.1, side * 5.9, shoulder_z - 0.9))
        if aiming and side < 0:
            direction = np.array((math.cos(kick), 0.08, math.sin(kick)))
            elbow_p = shoulder + direction * 8.5
            hand = elbow_p + direction * 8.0
            p.capsule(shoulder, elbow_p, 1.8, 1.5, COAT_L)
            p.capsule(elbow_p, hand, 1.5, 1.2, COAT_L)
            p.ball(hand, 1.25, SKIN)
            p.capsule(hand + direction * 0.6, hand + direction * 4.2, 0.9, 0.8, GUN)
            continue
        arm = swing * arm_swing / max(swing_size, 0.01) if moving else 0.04 * math.sin(t + side)
        if dying:
            arm = dying["arm_l"] if side > 0 else dying["arm_r"]
            elbow = dying["elbow_l"] if side > 0 else dying["elbow_r"]
        elbow_p = shoulder + (math.sin(arm) * 8.5, side * 0.9, -math.cos(arm) * 8.5)
        fore = arm + elbow
        hand = elbow_p + (math.sin(fore) * 8.0, side * 0.2, -math.cos(fore) * 8.0)
        p.capsule(shoulder, elbow_p, 1.8, 1.5, COAT_D if side < 0 else COAT_L)
        p.capsule(elbow_p, hand, 1.5, 1.2, COAT_D if side < 0 else COAT_L)
        p.ball(hand, 1.25, SKIN)

    if dying:
        collapse(p, dying)
    return p


def ramp(u, start, end):
    """Weicher Übergang von 0 nach 1 zwischen start und end."""
    x = min(1.0, max(0.0, (u - start) / (end - start)))
    return x * x * (3 - 2 * x)


def fall_pose(motion, u):
    """Haltung beim Sterben. Erst zuckt der Körper, dann geben die Knie nach, zuletzt sackt er zusammen.

    fallA sinkt nach vorn auf die Knie und aufs Gesicht, fallB dreht sich weg und fällt auf die Seite.
    """
    jolt = ramp(u, 0.0, 0.14) * (1 - ramp(u, 0.14, 0.4))
    buckle = ramp(u, 0.12, 0.62)
    drop = ramp(u, 0.5, 1.0)
    if motion == "fallA":
        return dict(
            swing_l=(0.1 + 0.85 * buckle) * (1 - drop), swing_r=(-0.1 + 0.5 * buckle) * (1 - 0.8 * drop),
            # Beim Aufschlag strecken sich die Beine wieder, sonst ragten die Füße in die Luft.
            knee_l=2.15 * buckle * (1 - 0.92 * drop), knee_r=1.75 * buckle * (1 - 0.8 * drop),
            lean=(-0.24 * jolt + 0.8 * buckle) * (1 - 0.95 * drop), arm_l=-0.5 * jolt + 0.55 * buckle + 0.9 * drop, arm_r=-0.35 * jolt + 0.25 * buckle + 1.5 * drop,
            elbow_l=0.25 + 0.7 * buckle, elbow_r=0.25 + 0.3 * buckle,
            pitch=math.radians(88) * drop, roll=math.radians(10) * drop, yaw=0.0,
        )
    return dict(
        swing_l=0.05 + 0.75 * buckle, swing_r=-0.05 + 0.2 * buckle, knee_l=1.9 * buckle, knee_r=0.75 * buckle,
        lean=-0.3 * jolt + 0.32 * buckle, arm_l=1.1 * jolt + 0.1 * buckle, arm_r=-0.9 * jolt + 2.0 * drop,
        elbow_l=0.2 + 0.9 * buckle, elbow_r=0.15,
        pitch=math.radians(12) * drop, roll=-math.radians(84) * drop, yaw=math.radians(50) * buckle,
    )


def collapse(p, pose):
    """Dreht den ganzen Körper um die Füße, legt ihn auf den Boden und rückt ihn zum Standort."""
    cp, sp = math.cos(pose["pitch"]), math.sin(pose["pitch"])
    cr, sr = math.cos(pose["roll"]), math.sin(pose["roll"])
    cy, sy = math.cos(pose["yaw"]), math.sin(pose["yaw"])

    def turn(q):
        x, y, z = q
        x, z = x * cp + z * sp, -x * sp + z * cp
        y, z = y * cr - z * sr, y * sr + z * cr
        return np.array((x * cy - y * sy, x * sy + y * cy, z))

    moved = [(turn(a), turn(b), r1, r2, m) for a, b, r1, r2, m in p.parts]
    low = min(min(a[2] - r1, b[2] - r2) for a, b, r1, r2, _ in moved)
    center = np.mean([a[:2] for a, *_ in moved], axis=0)
    shift = np.array((-center[0] * 0.7, -center[1] * 0.7, -low))
    p.parts = [(a + shift, b + shift, r1, r2, m) for a, b, r1, r2, m in moved]


def sdf(points, parts):
    """Abstand zur Figur je Punkt und welches Teil am nächsten ist. Weiche Vereinigung, damit keine Kanten entstehen."""
    all_d = np.empty((len(parts), len(points)), np.float32)
    for i, (a, b, r1, r2, _) in enumerate(parts):
        ba = b - a
        pa = points - a
        length = float(ba @ ba)
        h = np.clip((pa @ ba) / length, 0.0, 1.0) if length > 1e-6 else np.zeros(len(points))
        all_d[i] = np.linalg.norm(pa - h[:, None] * ba, axis=1) - (r1 + (r2 - r1) * h)
    low = all_d.min(axis=0)
    soft = low - SMOOTH * np.log(np.exp(-(all_d - low) / SMOOTH).sum(axis=0))
    return soft, all_d.argmin(axis=0)


def render(args):
    name, view, column = args
    spec = SPECS[name]
    motion, phase = FRAMES[column]
    pose = build(spec, motion, phase)
    yaw = math.radians(view)
    forward = np.array((math.cos(yaw), math.sin(yaw), 0.0))
    side = np.array((-math.sin(yaw), math.cos(yaw), 0.0))
    parts = []
    for a, b, r1, r2, material in pose.parts:
        world = [q[0] * forward + q[1] * side + np.array((0, 0, q[2])) for q in (a, b)]
        parts.append((world[0], world[1], r1, r2, material))

    w, h = CELL_W * SUPER, CELL_H * SUPER
    xs = (np.arange(w) + 0.5) / SUPER - FOOT_X
    ys = (np.arange(h) + 0.5) / SUPER - FOOT_Y
    gx, gy = np.meshgrid(xs, ys)
    origin = gx.ravel()[:, None] * RIGHT - gy.ravel()[:, None] * UP + TO_CAMERA * 70
    position = origin.copy()
    alive = np.ones(len(position), bool)
    hit = np.zeros(len(position), bool)
    for _ in range(40):
        index = np.flatnonzero(alive)
        if len(index) == 0:
            break
        d, _nearest = sdf(position[index], parts)
        position[index] -= TO_CAMERA * d[:, None]
        done = d < 0.05
        hit[index[done]] = True
        too_far = np.linalg.norm(position[index] - origin[index], axis=1) > 140
        alive[index[done | too_far]] = False

    image = np.zeros((h, w, 4), np.float32)
    index = np.flatnonzero(hit)
    if len(index):
        pts = position[index]
        _d, nearest = sdf(pts, parts)
        e = 0.2
        normal = np.stack([sdf(pts + np.eye(3)[i] * e, parts)[0] - sdf(pts - np.eye(3)[i] * e, parts)[0] for i in range(3)], axis=1)
        normal /= np.maximum(np.linalg.norm(normal, axis=1, keepdims=True), 1e-5)
        light = np.clip(normal @ LIGHT, 0, 1)
        facing = np.clip(normal @ TO_CAMERA, 0, 1)
        # Licht von oben, dunkler zum Rand hin: Das macht die Figur rund. In Stufen, damit es Pixelgrafik bleibt.
        shade = (0.58 + 0.62 * light) * (0.68 + 0.32 * facing)
        shade = np.round(shade * 7) / 7
        colors = palette(spec)
        materials = np.array([colors[part[4]] for part in parts], np.float32)[nearest]
        rgb = materials * shade[:, None]
        rows, cols = np.unravel_index(index, (h, w))
        image[rows, cols, :3] = np.clip(rgb, 0, 255)
        image[rows, cols, 3] = 1.0
    alpha = image[..., 3].reshape(CELL_H, SUPER, CELL_W, SUPER).mean(axis=(1, 3))
    summed = (image[..., :3] * image[..., 3:]).reshape(CELL_H, SUPER, CELL_W, SUPER, 3).sum(axis=(1, 3))
    rgb = summed / np.maximum(alpha[..., None] * SUPER * SUPER, 1e-5)
    out = np.zeros((CELL_H, CELL_W, 4), np.uint8)
    solid = alpha >= 0.5
    out[solid, :3] = rgb[solid].astype(np.uint8)
    out[solid, 3] = 255
    return name, view, column, ground_marks(out, motion, phase)


def ground_marks(figure, motion, phase):
    """Weicher Schatten unter den Füßen, beim Fallen wächst stattdessen eine Blutlache."""
    h, w = figure.shape[:2]
    under = np.zeros((h, w, 4), np.uint8)
    ys, xs = np.mgrid[0:h, 0:w]
    if motion.startswith("fall"):
        grow = max(0.0, phase - 0.55) / 0.45
        if grow > 0:
            d = (((xs - FOOT_X) / (2 + 15 * grow)) ** 2 + ((ys - (FOOT_Y - 1)) / (1 + 5 * grow)) ** 2)
            under[d < 1] = (72, 10, 10, 225)
            under[d < 0.45] = (96, 14, 12, 225)
    else:
        d = ((xs - FOOT_X) / 9.0) ** 2 + ((ys - FOOT_Y) / 3.2) ** 2
        under[d < 1] = (0, 0, 0, 105)
        under[d < 0.45] = (0, 0, 0, 140)
    solid = figure[..., 3] > 0
    under[solid] = figure[solid]
    return under


def main():
    names = list(SPECS)
    tasks = [(name, view, column) for name in names for view in VIEWS for column in range(len(FRAMES))]
    sheet = np.zeros((CELL_H * len(names) * len(VIEWS), CELL_W * len(FRAMES), 4), np.uint8)
    with multiprocessing.Pool() as pool:
        for name, view, column, cell in pool.imap_unordered(render, tasks, chunksize=8):
            row = names.index(name) * len(VIEWS) + VIEWS.index(view)
            sheet[row * CELL_H:(row + 1) * CELL_H, column * CELL_W:(column + 1) * CELL_W] = cell
    assets = ROOT / "game" / "assets"
    Image.fromarray(sheet).save(assets / "figures.png", optimize=True)
    (assets / "figures.json").write_text(json.dumps({
        "names": names, "w": CELL_W, "h": CELL_H, "footX": FOOT_X, "footY": FOOT_Y, "views": len(VIEWS),
    }), encoding="utf-8")
    print(f"{len(tasks)} Figurenbilder gerendert")


if __name__ == "__main__":
    main()
