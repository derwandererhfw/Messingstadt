"""Rendert das Figurenblatt aus einem kleinen 3D-Modell mit Gelenken.

Jede Figur besteht aus runden Körperteilen (Kapseln, Kugeln und flachen Scheiben), die Pose kommt aus einem
Gelenkmodell. Beim Gehen führen die Füße: Ein Fuß am Boden wandert gleichmäßig nach hinten, genau so schnell,
wie die Figur im Spiel vorankommt. Knie und Ellbogen ergeben sich daraus. So gleiten die Füße nicht.
Gerendert wird mit Strahlen im selben Blickwinkel wie die Stadt, in doppelter Auflösung: Das Spiel zeigt
die Figuren halb so groß, mit der Kamera ergibt das scharfe Figuren vor der pixeligeren Stadt.
Aufruf: python tools/art/figures3d.py   (rund zehn Minuten)

Blatt: je Figur fünf Zeilen für die Blickrichtungen S, SO, O, NO, N (Westen entsteht durch Spiegeln).
Welche Spalten zu welcher Bewegung gehören, steht in figures.json unter "columns", die Schrittlänge je
Bewegung unter "stride" in Spieleinheiten pro Zyklus.
"""
import json
import math
import multiprocessing
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_concept import C, SPECS, bleed  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
RES = 2
WORLD_W, WORLD_H = 56, 54
CELL_W, CELL_H = WORLD_W * RES, WORLD_H * RES
WORLD_FOOT_X, WORLD_FOOT_Y = 28, 50
FOOT_X, FOOT_Y = WORLD_FOOT_X * RES, WORLD_FOOT_Y * RES
SHADE_STEPS = 12
SUPER = 2
VIEWS = (90.0, 45.0, 0.0, -45.0, -90.0)  # Blickrichtung am Boden: 90 ist zur Kamera, 0 nach rechts im Bild
TO_CAMERA = np.array((0.0, math.cos(math.radians(30)), math.sin(math.radians(30))))
UP = np.array((0.0, -0.5, math.cos(math.radians(30))))
RIGHT = np.array((1.0, 0.0, 0.0))
LIGHT = np.array((-0.35, 0.3, 0.88)) / np.linalg.norm((-0.35, 0.3, 0.88))
SMOOTH = 0.4
HIP, THIGH, SHIN = 23.0, 12.0, 11.0
REACH = 0.985 * (THIGH + SHIN)  # So weit streckt sich ein Bein höchstens, ganz durchgedrückt sieht es steif aus.
ANKLE = 1.6  # Höhe des Knöchels über dem Boden bei flachem Fuß
FOOT = 3.6
UPPER_ARM, FOREARM = 8.5, 8.0
SHOULDER_Z = 38.5 - HIP
WAIST = 25.0 - HIP
# Ein Feld hat 1.000 Spieleinheiten, seine Kante ist im Bild 32 × 16 Pixel lang. Eine Modelleinheit ist ein Pixel.
WORLD_PER_UNIT = 1000 / (32 * math.sqrt(2))

# Teile der Figur, je ein Stoff
COAT, COAT_D, COAT_L, TROUSERS, SHOE, SKIN, HAIR, HAT, GUN, SHIRT, TRIM, WOOD, LINEN = range(13)
CAPSULE, DISC = 0, 1
# sawn: doppelläufige Flinte mit gekürzten Läufen, smg: Maschinenpistole mit Trommel, club: Totschläger
GUN_LENGTH = {"pistol": 4.2, "shotgun": 11.0, "rifle": 16.0, "sawn": 9.5, "smg": 11.0}
LONG_GUNS = ("shotgun", "rifle", "sawn", "smg")

# Bewegungen in Spaltenreihenfolge und ihre Zahl an Bildern
MOTIONS = (
    ("walk", 12), ("run", 8), ("idle", 8), ("look", 6), ("raise", 3), ("aim", 2), ("recoil", 3), ("aimwalk", 10),
    ("fallA", 8), ("fallB", 8), ("strike", 5), ("throw", 5), ("bandage", 6),
)
FRAMES = [(motion, i, n) for motion, n in MOTIONS for i in range(n)]

# Gangarten. stride: Weg pro Zyklus aus zwei Schritten. duty: Anteil des Zyklus, den ein Fuß am Boden steht.
# Unter 0,5 hat das Rennen eine Flugphase. Die Figuren sind im Spiel flott unterwegs, daher lange Schritte.
GAITS = {
    "walk": dict(stride=42.0, duty=0.55, lift=3.2, width=3.1, height=23.6, bounce=0.0, yaw=0.24, twist=0.75,
                 sway=0.55, lean=0.06, arm=0.5, elbow=0.32, kick=0.0, trail=0.0, ankle=ANKLE),
    "run": dict(stride=74.0, duty=0.36, lift=8.5, width=2.3, height=22.4, bounce=1.1, yaw=0.34, twist=0.9,
                sway=0.3, lean=0.22, arm=0.95, elbow=1.45, kick=5.0, trail=1.8, ankle=ANKLE),
}
GAITS["aimwalk"] = GAITS["walk"]
DOG_GAITS = {
    # Trab im Schritt: jedes Bein einzeln, diagonal versetzt.
    "walk": dict(stride=18.0, duty=0.55, lift=2.2, height=13.0, bounce=0.0, ankle=0.95, flex=0.3, roll=0.0,
                 offsets=(("HL", 0.0), ("FL", 0.25), ("HR", 0.5), ("FR", 0.75))),
    # Galopp: Hinterbeine fast gemeinsam, dann die Vorderbeine, dazwischen fliegt der Hund. Der Rücken arbeitet mit.
    "run": dict(stride=44.0, duty=0.32, lift=3.6, height=12.6, bounce=0.6, ankle=0.95, flex=1.8, roll=0.0,
                offsets=(("HL", 0.0), ("HR", 0.09), ("FL", 0.5), ("FR", 0.6))),
}
DOG_GAITS["aimwalk"] = DOG_GAITS["walk"]
DOG_LEG = 5.5
DOG_SHIFT = -3.5  # Der Hund steht etwas zurückgesetzt, damit Rute und Schnauze beim Sprung in die Zelle passen.


def palette(spec):
    co_d, co, co_l = spec["coat"]
    return {
        COAT: C[co], COAT_D: C[co_d], COAT_L: C[co_l], TROUSERS: C[spec["legs"]], SHOE: C["black"],
        SKIN: C["skin"], HAIR: C[spec["hair"]], HAT: C[spec["hat_c"]], GUN: C["soot"], SHIRT: C[spec["shirt"]],
        TRIM: C[spec.get("trim", co_l)], WOOD: C["wood_d"], LINEN: C["paper"],
    }


class Pose:
    """Gelenke in Modelleinheiten: x nach vorn, y zur Seite (links positiv), z nach oben. Die Füße stehen bei z = 0."""

    def __init__(self):
        self.parts = []

    def capsule(self, a, b, r1, r2, material):
        self.parts.append((np.array(a, float), np.array(b, float), r1, r2, material, CAPSULE))

    def ball(self, p, r, material):
        self.capsule(p, p, r, r, material)

    def disc(self, center, axis, radius, thickness, material):
        """Flache runde Scheibe, etwa eine Hutkrempe. axis zeigt senkrecht aus der Scheibe."""
        center = np.array(center, float)
        axis = np.array(axis, float)
        self.parts.append((center, center + axis / np.linalg.norm(axis), radius, thickness, material, DISC))

    def move(self, offset):
        self.parts = [(a + offset, b + offset, r1, r2, m, k) for a, b, r1, r2, m, k in self.parts]

    def lowest(self):
        return min(part_low(part) for part in self.parts)


def part_low(part):
    a, b, r1, r2, _m, kind = part
    if kind == DISC:
        axis = b - a
        return a[2] - r1 * math.sqrt(max(0.0, 1 - axis[2] ** 2)) - r2
    return min(a[2] - r1, b[2] - r2)


def ramp(u, start, end):
    """Weicher Übergang von 0 nach 1 zwischen start und end."""
    x = min(1.0, max(0.0, (u - start) / (end - start)))
    return x * x * (3 - 2 * x)


def lerp(a, b, f):
    return a + (b - a) * f


def turn_z(v, angle):
    c, s = math.cos(angle), math.sin(angle)
    return np.array((v[0] * c - v[1] * s, v[0] * s + v[1] * c, v[2]))


def unit(v):
    v = np.asarray(v, float)
    return v / max(np.linalg.norm(v), 1e-6)


def two_bone(root, target, l1, l2, bend):
    """Zwei Glieder von root zu target. bend zeigt, wohin das mittlere Gelenk ausweicht. Gibt Gelenk und Ende."""
    d = target - root
    dist = float(np.linalg.norm(d))
    direction = d / max(dist, 1e-6)
    dist = min(max(dist, abs(l1 - l2) + 1e-3), l1 + l2 - 1e-3)
    end = root + direction * dist
    cos_a = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
    side = np.asarray(bend, float) - direction * float(np.dot(bend, direction))
    side = unit(side)
    joint = root + direction * (cos_a * l1) + side * (math.sqrt(max(0.0, 1 - cos_a * cos_a)) * l1)
    return joint, end


def foot_path(u, g):
    """Lage eines Fußes im Gangzyklus, u von 0 bis 1 ab dem Aufsetzen. Gibt x, Höhe des Knöchels und Fußwinkel.

    Am Boden wandert der Fuß gleichmäßig nach hinten. Gegen Ende hebt sich die Ferse, der Ballen bleibt stehen.
    """
    half = g["stride"] * g["duty"] / 2
    ground = g["ankle"]
    if u < g["duty"]:
        s = u / g["duty"]
        x = half - 2 * half * s
        roll = ramp(s, 0.62, 1.0) * g.get("roll", 0.75)
        heel = 0.12 * (1 - ramp(s, 0.0, 0.18))
        # Um den Ballen gedreht: Der Knöchel steigt, die Zehen bleiben am selben Fleck.
        return x + FOOT * (1 - math.cos(roll)), ground + FOOT * math.sin(roll), heel - roll
    s = (u - g["duty"]) / (1 - g["duty"])
    # Der Fuß ist vorn, bevor er sinkt. Sonst setzte er im Vorschwingen auf und schliffe über den Boden.
    ease = 0.5 - 0.5 * math.cos(math.pi * min(1.0, s * 1.15))
    roll = g.get("roll", 0.75)
    start_x = -half + FOOT * (1 - math.cos(roll))
    x = lerp(start_x, half, ease) - g.get("kick", 0.0) * math.sin(math.pi * s) * (1 - s)
    lift = g["lift"] * math.sin(math.pi * s)
    z = ground + FOOT * math.sin(roll) * (1 - s) ** 2 + lift
    return x, z, lerp(-roll, 0.15, s)


def hip_point(pelvis, side, yaw):
    return pelvis + turn_z((0.0, side * 3.0, 0.0), yaw)


def pelvis_height(desired, feet, pelvis_xy, yaw):
    """So hoch wie gewünscht, aber kein Bein darf sich überstrecken."""
    z = desired
    for side, (ankle, _angle) in feet.items():
        hip = hip_point(np.array((pelvis_xy[0], pelvis_xy[1], 0.0)), side, yaw)
        dx, dy = ankle[0] - hip[0], ankle[1] - hip[1]
        z = min(z, ankle[2] + math.sqrt(max(1.0, REACH * REACH - dx * dx - dy * dy)))
    return z


def new_rig():
    return dict(pelvis=np.zeros(3), hip_yaw=0.0, twist=0.0, roll=0.0, lean=0.0, breath=0.0, head_yaw=0.0,
                head_pitch=0.0, legs={}, arms={}, gun=None, coat={1: 0.0, -1: 0.0}, trail=0.0, carry=True)


def legs_from_feet(rig, feet):
    """Knie per Gelenkrechnung: Das Knie beugt nach vorn."""
    for side, (ankle, angle) in feet.items():
        hip = hip_point(rig["pelvis"], side, rig["hip_yaw"])
        knee, end = two_bone(hip, ankle, THIGH, SHIN, turn_z((1.0, 0.0, 0.0), rig["hip_yaw"]))
        rig["legs"][side] = (hip, knee, end, angle)
        rig["coat"][side] = math.atan2(knee[0] - hip[0], hip[2] - knee[2])


def gait_rig(g, phase):
    rig = new_rig()
    feet = {}
    for side, offset in ((1, 0.0), (-1, 0.5)):
        x, z, angle = foot_path((phase + offset) % 1.0, g)
        feet[side] = (np.array((x, side * g["width"], z)), angle)
    spread = feet[1][0][0] - feet[-1][0][0]
    rig["hip_yaw"] = -g["yaw"] * spread / (g["stride"] * g["duty"])
    rig["twist"] = -g["twist"] * rig["hip_yaw"]
    sway = g["sway"] * math.cos(math.tau * (phase - g["duty"] / 2))
    desired = g["height"] - g["bounce"] * math.cos(2 * math.tau * (phase - g["duty"] / 2))
    rig["pelvis"] = np.array((0.0, sway, pelvis_height(desired, feet, (0.0, sway), rig["hip_yaw"])))
    rig["lean"] = g["lean"] + 0.025 * math.cos(2 * math.tau * (phase - g["duty"] / 2))
    legs_from_feet(rig, feet)
    # Die Arme schwingen gegen die Beine: Steht das linke Bein vorn, ist der linke Arm hinten.
    swing = math.cos(math.tau * (phase - 0.04))
    for side in (1, -1):
        arm = -side * g["arm"] * swing
        rig["arms"][side] = ("fk", arm, g["elbow"] + 0.25 * max(0.0, arm))
    # Der Mantel folgt den Beinen etwas verspätet.
    lag = new_rig()
    lag_feet = {}
    for side, offset in ((1, 0.0), (-1, 0.5)):
        x, z, angle = foot_path((phase + offset - 0.07) % 1.0, g)
        lag_feet[side] = (np.array((x, side * g["width"], z)), angle)
    lag["pelvis"] = rig["pelvis"]
    legs_from_feet(lag, lag_feet)
    rig["coat"] = {side: 0.55 * lag["coat"][side] for side in (1, -1)}
    rig["trail"] = g["trail"]
    return rig


def stand_rig(shift=0.0, stance="relaxed"):
    """Stehen. shift verlagert das Gewicht: positiv aufs linke Bein, das rechte Knie gibt dann etwas nach."""
    rig = new_rig()
    if stance == "long":
        feet = {1: (np.array((3.0, 3.6, ANKLE)), 0.05), -1: (np.array((-2.8, -3.2, ANKLE)), -0.02)}
    elif stance == "pistol":
        feet = {1: (np.array((1.6, 3.4, ANKLE)), 0.05), -1: (np.array((-1.4, -3.4, ANKLE)), 0.0)}
    else:
        feet = {1: (np.array((0.8, 3.3, ANKLE)), 0.06), -1: (np.array((-0.5, -3.3, ANKLE)), 0.0)}
    pelvis_xy = (0.1, 0.9 * shift)
    rig["pelvis"] = np.array((pelvis_xy[0], pelvis_xy[1], pelvis_height(23.5 - 0.3 * abs(shift), feet, pelvis_xy, 0.0)))
    legs_from_feet(rig, feet)
    # Über dem Standbein steht die Hüfte höher. Das andere Knie beugt sich dafür.
    for side in (1, -1):
        hip, knee, ankle, angle = rig["legs"][side]
        hip = hip + (0, 0, side * 0.45 * shift)
        knee, ankle = two_bone(hip, ankle, THIGH, SHIN, (1.0, 0.0, 0.0))
        rig["legs"][side] = (hip, knee, ankle, angle)
        rig["coat"][side] = 0.5 * math.atan2(knee[0] - hip[0], hip[2] - knee[2])
    rig["roll"] = -0.05 * shift
    return rig


def aim_pose(rig, spec, amount, kick=0.0, push=0.0):
    """Waffe heben: amount 0 hängt sie, 1 liegt sie im Anschlag. kick reißt die Mündung hoch, push den Oberkörper."""
    gun = spec.get("gun", "pistol")
    if gun == "none" or amount <= 0:
        return
    e = ramp(amount, 0.0, 1.0)
    rig["carry"] = False
    if gun == "pistol":
        rig["twist"] += 0.2 * e
        shoulder = arm_root(rig, -1)
        pitch = lerp(-1.45, kick, e)
        length = lerp(15.6, 16.3, e) - 3.2 * math.sin(math.pi * e)
        hand = shoulder + length * np.array((math.cos(pitch), 0.12 * e, math.sin(pitch))) - (push, 0, 0)
        direction = unit(lerp(np.array((0.35, 0.0, -1.0)), np.array((math.cos(kick + 0.03), 0.02, math.sin(kick + 0.03))), e))
        rig["arms"][-1] = ("ik", hand, (-0.3, -0.8, -1.0))
        rig["gun"] = (hand + direction * 0.6, direction, GUN_LENGTH[gun])
        return
    if gun == "club":
        # Totschläger: aus der hängenden Hand auf Schulterhöhe gehoben, bereit zum Schlag
        rig["twist"] += 0.25 * e
        shoulder = arm_root(rig, -1)
        hang = shoulder + turn_z((1.0, -0.6, -15.6), rig["twist"])
        ready = shoulder + turn_z((3.5 - push, -0.8, 3.0), rig["twist"])
        rig["arms"][-1] = ("ik", lerp(hang, ready, e), (0.0, -0.8, -1.0))
        rig["club"] = unit(turn_z(lerp(np.array((0.3, 0.0, -1.0)), np.array((-0.45, 0.0, 1.0)), e), rig["twist"]))
        return
    length = GUN_LENGTH[gun]
    carry_butt, carry_dir, _length, _right, _left = carry_pose(rig, spec)
    shoulder = arm_root(rig, -1)
    aim_dir = np.array((math.cos(kick), 0.0, math.sin(kick)))
    if gun == "smg":
        # Maschinenpistole aus der Hüfte: vor dem Bauch, beide Hände daran
        rig["twist"] -= 0.15 * e
        aim_butt = upper(rig, (2.6 - push, -2.6, WAIST + 5.0))
    else:
        # Flinte und Gewehr in die Schulter
        rig["twist"] -= 0.38 * e
        aim_butt = shoulder + np.array((1.2 - push, 1.6, 0.6))
    butt = lerp(carry_butt, aim_butt, e)
    direction = unit(lerp(carry_dir, aim_dir, e))
    rig["gun"] = (butt, direction, length)
    grip = 0.22 if gun in ("sawn", "smg") else 0.3
    rig["arms"][-1] = ("ik", butt + direction * length * grip, (0.0, -0.6, -1.0))
    rig["arms"][1] = ("ik", butt + direction * length * 0.62, (0.0, 0.7, -1.0))


def carry_pose(rig, spec):
    """Lange Waffe beim Stehen und Gehen: Schaft, Richtung, Länge und wo die rechte und linke Hand greifen."""
    gun = spec.get("gun", "pistol")
    bulk = rig_bulk(spec)
    if gun == "sawn":
        # Mit beiden Händen an der Hüfte, die Läufe zeigen schräg zu Boden.
        butt = upper(rig, (2.6 * bulk, -2.4, WAIST + 3.4))
        return butt, unit(turn_z((0.88, 0.1, -0.36), rig["twist"])), GUN_LENGTH[gun], 0.2, 0.62
    if gun == "smg":
        # Vor dem Bauch, die Trommel unten, die Mündung leicht gesenkt.
        butt = upper(rig, (2.4 * bulk, -2.4, WAIST + 3.6))
        return butt, unit(turn_z((0.95, 0.15, -0.2), rig["twist"])), GUN_LENGTH[gun], 0.2, 0.62
    butt = upper(rig, (3.3 * bulk, -4.5, WAIST + 1.5))
    end = upper(rig, (3.0 * bulk, 3.5, SHOULDER_Z + rig["breath"] + (2.5 if gun == "rifle" else 0.5)))
    return butt, unit(end - butt), float(np.linalg.norm(end - butt)), 0.25, 0.8


def draw_gun(p, gun, start, direction, length):
    """Waffe vom Schaft aus entlang direction."""
    side = np.cross(direction, (0.0, 0.0, 1.0))
    side = unit(side) if np.linalg.norm(side) > 1e-3 else np.array((0.0, 1.0, 0.0))
    down = unit(np.cross(side, direction))
    if gun == "sawn":
        # Kolben aus Holz, darauf zwei kurze Läufe nebeneinander
        p.capsule(start, start + direction * 3.6, 1.2, 0.9, WOOD)
        for s in (0.6, -0.6):
            p.capsule(start + direction * 3.2 + side * s, start + direction * length + side * s, 0.62, 0.62, GUN)
        return
    if gun == "smg":
        # Holzschaft, Gehäuse, vorn ein Griff und unten die runde Trommel
        p.capsule(start, start + direction * 3.2, 1.0, 0.8, WOOD)
        p.capsule(start + direction * 2.8, start + direction * length, 0.9, 0.6, GUN)
        p.disc(start + direction * 5.4 - down * 1.6, side, 2.2, 0.8, GUN)
        p.capsule(start + direction * 8.0, start + direction * 8.0 - down * 2.0, 0.6, 0.6, WOOD)
        return
    if gun == "carry":
        p.capsule(start, start + direction * length, 0.85, 0.75, GUN)
        return
    p.capsule(start, start + direction * length, 0.95 if gun == "shotgun" else 0.8, 0.8, GUN)


def rig_bulk(spec):
    return spec.get("bulk", 1.0)


def upper(rig, v):
    """Punkt am Oberkörper relativ zum Becken. Die Drehung wächst von der Hüfte zu den Schultern."""
    x, y, z = v
    f = min(1.0, max(0.0, z / SHOULDER_Z))
    t = lerp(rig["hip_yaw"], rig["twist"], f)
    x, y = x * math.cos(t) - y * math.sin(t), x * math.sin(t) + y * math.cos(t)
    r = rig["roll"] * f
    y, z = y * math.cos(r) - z * math.sin(r), y * math.sin(r) + z * math.cos(r)
    lean = rig["lean"]
    return rig["pelvis"] + np.array((x + z * math.sin(lean), y, z * math.cos(lean)))


def arm_root(rig, side):
    return upper(rig, (0.1, side * 5.9, SHOULDER_Z + rig["breath"] - 0.9))


def body(spec, rig, dying=False):
    """Baut die Figur aus der Haltung rig."""
    p = Pose()
    bulk = rig_bulk(spec)
    gun = spec.get("gun", "pistol")
    shoulder_z = SHOULDER_Z + rig["breath"]

    def up(v):
        return upper(rig, v)

    # Beine und Schuhe
    for side in (1, -1):
        h, k, a, foot_angle = rig["legs"][side]
        p.capsule(h, k, 2.2, 1.8, TROUSERS)
        p.capsule(k, a, 1.8, 1.35, TROUSERS)
        forward = turn_z((1.0, 0.0, 0.0), rig["hip_yaw"] * 0.5)
        toe = a + forward * (FOOT * math.cos(max(foot_angle, -0.8))) + (0, 0, FOOT * math.sin(max(foot_angle, -0.8)) - 0.2)
        p.capsule(a, toe, 1.7, 1.45, SHOE)

    # Rumpf: zwei Hälften nebeneinander ergeben einen runden Oberkörper mit Schultern.
    for side in (1, -1):
        for depth in (1.1, -1.1):
            p.capsule(up((depth * bulk, side * 1.7 * bulk, WAIST)), up((depth * bulk + 0.2, side * 2.9 * bulk, shoulder_z - 1.2)), 2.5 * bulk, 2.6 * bulk, COAT)
    # Mantel oder Jacke unter dem Gürtel. Seitenbahnen folgen den Beinen, Bahnen vorn und hinten schließen die Lücke.
    hem = max(4.0, 25.0 - (40 - spec["hem"]) * 1.15)
    pelvis = rig["pelvis"]
    trail = rig["trail"]
    flare = trail * 0.5
    hem_r = (1.6 + hem * 0.09) * bulk
    for side in (1, -1):
        swing = rig["coat"][side]
        for depth in (1.0, -1.2):
            end = pelvis + turn_z((depth + math.sin(swing) * hem * 0.8 - trail, side * (2.5 + flare * 0.3), WAIST - hem + flare), rig["hip_yaw"])
            p.capsule(up((depth, side * 1.7 * bulk, WAIST)), end, 2.4 * bulk, hem_r, COAT_D if side < 0 else COAT)
    middle = 0.5 * (rig["coat"][1] + rig["coat"][-1])
    for depth, spread in ((1.5, rig["coat"][1] - rig["coat"][-1]), (-1.7, rig["coat"][-1] - rig["coat"][1])):
        # Schwingt das linke Bein vor, rückt die vordere Bahn mit, die hintere bleibt mittig.
        reach = math.sin(middle) * hem * 0.8 + 0.06 * abs(spread) * hem * (1 if depth > 0 else -1)
        end = pelvis + turn_z((depth + reach - trail * 1.2, 0.0, WAIST - hem + flare * 1.4), rig["hip_yaw"])
        p.capsule(up((depth, 0.0, WAIST)), end, 2.3 * bulk, hem_r * 0.95, COAT)
    # Gürtel und Kragen
    p.capsule(up((0.4, 2.1 * bulk, WAIST + 0.3)), up((0.4, -2.1 * bulk, WAIST + 0.3)), 3.05 * bulk, 3.05 * bulk, COAT_D)
    p.capsule(up((0, 2.1, shoulder_z + 0.6)), up((0, -2.1, shoulder_z + 0.6)), 1.5, 1.5, TRIM)
    p.ball(up((1.9 * bulk, 0, shoulder_z - 1.4)), 1.05, SHIRT)
    if bulk > 1.0:
        # Panzerplatte auf der Brust und Schulterstücke beim Werkschutz
        p.capsule(up((2.6, 2.0, WAIST + 3.0)), up((2.6, -2.0, WAIST + 3.0)), 2.2, 2.2, TRIM)
        p.capsule(up((2.6, 2.0, shoulder_z - 3.0)), up((2.6, -2.0, shoulder_z - 3.0)), 2.0, 2.0, TRIM)
        for side in (1, -1):
            p.ball(up((0.1, side * 6.4, shoulder_z - 0.2)), 2.4, TRIM)
    if spec.get("strap"):
        # Lederriemen quer über die Brust
        p.capsule(up((2.0 * bulk, 2.9, shoulder_z - 1.0)), up((2.2 * bulk, -3.2, WAIST + 0.5)), 0.65, 0.65, TRIM)
    if spec.get("satchel"):
        # Umhängetasche an der Hüfte
        p.capsule(up((-1.6, -5.1, WAIST - 2.6)), up((1.9, -5.1, WAIST - 2.6)), 2.7, 2.7, TRIM)
    if gun in LONG_GUNS and rig["carry"] and not dying:
        # Lange Waffe getragen, beide Hände daran
        butt, direction, length, right, left = carry_pose(rig, spec)
        draw_gun(p, gun if gun in ("sawn", "smg") else "carry", butt, direction, length)
        rig["arms"][-1] = ("ik", butt + direction * length * right, (0.0, -0.6, -1.0))
        rig["arms"][1] = ("ik", butt + direction * length * left, (0.0, 0.7, -1.0))
    if rig["gun"] is not None:
        start, direction, length = rig["gun"]
        draw_gun(p, gun, start, direction, length)

    # Kopf und Kopfbedeckung. Der Kopf dreht sich mit den Schultern und dazu um sich selbst.
    neck = up((0.2, 0, shoulder_z + 1.5))
    head = up((0.5, 0, shoulder_z + 5.0))
    yaw = rig["twist"] + rig["head_yaw"]
    pitch = rig["head_pitch"] - rig["lean"] * 0.4

    def hv(v):
        x, y, z = v
        x, z = x * math.cos(pitch) - z * math.sin(pitch), x * math.sin(pitch) + z * math.cos(pitch)
        return head + turn_z((x, y, z), yaw)

    up_axis = hv((0, 0, 1)) - head
    p.capsule(neck, head, 1.4, 1.4, SKIN)
    p.ball(head, 2.9, SKIN)
    p.ball(hv((-1.0, 0, 0.4)), 2.7, HAIR)
    if spec["hat"] == "helmet":
        # Stahlhelm: runde Kappe mit umlaufendem Rand
        p.ball(hv((-0.1, 0, 1.3)), 3.5, HAT)
        p.disc(hv((0, 0, 0.6)), up_axis, 4.1, 0.55, HAT)
    elif spec["hat"] == "fedora":
        # Breite, glatte Krempe, darauf der schmalere Hutkopf
        brim = hv((0, 0, 1.5))
        p.disc(brim, up_axis, 5.2, 0.5, HAT)
        p.capsule(hv((0, 0, 1.9)), hv((0, 0, 4.3)), 2.7, 2.2, COAT_D)
    else:
        top = hv((-0.2, 0, 2.4))
        p.disc(hv((-0.2, 0, 2.6)), up_axis, 3.3, 0.9, HAT)
        p.ball(top + up_axis * 0.9, 2.6, HAT)
        p.capsule(hv((2.7, 1.5, 1.4)), hv((2.7, -1.5, 1.4)), 0.8, 0.8, SHOE)

    # Arme: entweder Winkel an Schulter und Ellbogen oder die Hand an einem Zielpunkt.
    joints = {}
    for side in (1, -1):
        shoulder = arm_root(rig, side)
        mode = rig["arms"].get(side, ("fk", 0.04, 0.18))
        sleeve = COAT_D if side < 0 else COAT_L
        if mode[0] == "ik":
            _, hand, bend = mode
            elbow_p, hand = two_bone(shoulder, np.asarray(hand, float), UPPER_ARM, FOREARM, turn_z(np.asarray(bend, float), rig["twist"]))
            sleeve = COAT_L
        else:
            _, arm, elbow = mode
            elbow_p = shoulder + turn_z((math.sin(arm) * UPPER_ARM, side * 0.9, -math.cos(arm) * UPPER_ARM), rig["twist"])
            fore = arm + elbow
            hand = elbow_p + turn_z((math.sin(fore) * FOREARM, side * 0.2, -math.cos(fore) * FOREARM), rig["twist"])
        p.capsule(shoulder, elbow_p, 1.8, 1.5, sleeve)
        p.capsule(elbow_p, hand, 1.5, 1.2, sleeve)
        p.ball(hand, 1.25, SKIN)
        joints[side] = (elbow_p, hand)
        if side < 0 and gun == "club" and not dying and not rig.get("busy"):
            # Lederknüppel in der rechten Hand, hängend oder erhoben
            direction = rig["club"] if rig.get("club") is not None else unit(hand - elbow_p)
            p.capsule(hand - direction * 0.8, hand + direction * 6.5, 0.75, 1.05, SHOE)
        if side < 0 and rig.get("can"):
            # Rauchtopf: kleine Blechdose in der Wurfhand
            p.capsule(hand + (0, 0, -0.4), hand + (0, 0, 1.2), 1.15, 1.15, GUN)
        if side < 0 and rig.get("bandage"):
            p.ball(hand, 1.0, LINEN)
        if spec.get("leash") and side > 0 and not dying and mode[0] == "fk":
            # Aufgerolltes Ende der Leine in der freien Hand
            p.capsule(hand + (0.4, 0.4, -0.5), hand + (1.2, 1.2, -4.5), 0.75, 0.55, TRIM)
    if rig.get("bandage") and 1 in joints:
        # Binde um den linken Unterarm
        elbow_p, hand = joints[1]
        p.capsule(lerp(elbow_p, hand, 0.3), lerp(elbow_p, hand, 0.7), 1.7, 1.55, LINEN)
    return p


def build(spec, motion, i, n):
    if spec.get("dog"):
        return build_dog(motion, i, n)
    if motion.startswith("fall"):
        p = build_fall(spec, motion, i / (n - 1))
    else:
        p = body(spec, human_rig(spec, motion, i, n))
    scale = spec.get("scale", 1.0)
    if scale != 1.0:
        p.parts = [(a * scale, b * scale, r1 * scale, r2 * scale, m, k) for a, b, r1, r2, m, k in p.parts]
    return p


# Rückstoß je Bild: Mündung hoch, Oberkörper zurück. Lange Waffen schlagen stärker in die Schulter.
RECOIL = ((0.3, 1.3, 0.09), (0.14, 0.7, 0.05), (0.05, 0.25, 0.015))
RECOIL_FORCE = {"pistol": 1.0, "shotgun": 1.7, "rifle": 1.4, "sawn": 1.9, "smg": 0.45, "club": 0.0, "none": 0.0}
# Schlag mit dem Totschläger: ausholen, oben, Treffer, durchgezogen, zurück. Hand relativ zur rechten Schulter,
# dazu Richtung des Knüppels, Drehung der Schultern und Vorlage.
STRIKE = (
    ((-1.5, -1.0, 6.5), (-0.5, 0.0, 1.0), 0.35, -0.06),
    ((-2.5, -0.8, 7.5), (-0.9, 0.0, 0.3), 0.42, -0.08),
    ((8.0, -0.5, 1.0), (0.7, 0.0, -0.7), -0.25, 0.18),
    ((8.5, -0.3, -4.5), (0.3, 0.0, -1.0), -0.3, 0.22),
    ((4.0, -0.8, -10.0), (0.2, 0.0, -1.0), -0.1, 0.08),
)
# Wurf: zurück ausholen, hoch, loslassen, nachschwingen, zurück. Die Dose ist in den ersten beiden Bildern in der Hand.
THROW = (
    ((-6.0, -1.5, 1.0), 0.5, -0.06, True),
    ((-5.0, -1.0, 5.5), 0.45, -0.08, True),
    ((9.0, -0.5, 5.0), -0.3, 0.15, False),
    ((10.0, 0.0, -2.0), -0.4, 0.22, False),
    ((4.0, -1.0, -9.0), -0.15, 0.08, False),
)
LOOK = (0.3, 0.72, 0.8, 0.35, -0.5, -0.22)


def human_rig(spec, motion, i, n):
    gun = spec.get("gun", "pistol")
    phase = i / n
    stance = "long" if gun in LONG_GUNS else "pistol"
    if motion == "strike":
        return strike_rig(i)
    if motion == "throw":
        return throw_rig(i)
    if motion == "bandage":
        return bandage_rig(phase)
    if motion in ("walk", "run"):
        return gait_rig(GAITS[motion], phase)
    if motion == "aimwalk":
        rig = gait_rig(GAITS["walk"], phase)
        rig["twist"] *= 0.3
        rig["lean"] = 0.03
        aim_pose(rig, spec, 1.0)
        return rig
    if motion == "idle":
        # Fünf Sekunden: zweimal atmen, das Gewicht wandert einmal hin und her.
        t = phase * math.tau
        rig = stand_rig(0.8 * math.sin(t))
        rig["breath"] = 0.4 * math.sin(2 * t)
        rig["head_yaw"] = 0.06 * math.sin(t + 0.6)
        rig["head_pitch"] = 0.03 * math.sin(2 * t)
        for side in (1, -1):
            rig["arms"][side] = ("fk", 0.05 + 0.035 * math.sin(t + side) + 0.02 * rig["breath"], 0.2 + 0.03 * side * math.sin(t))
        return rig
    if motion == "look":
        rig = stand_rig(0.25)
        rig["head_yaw"] = LOOK[i]
        rig["twist"] = 0.22 * LOOK[i]
        rig["head_pitch"] = -0.04 * abs(LOOK[i])
        for side in (1, -1):
            rig["arms"][side] = ("fk", 0.05, 0.2)
        return rig
    rig = stand_rig(0.0, stance if gun != "none" else "relaxed")
    for side in (1, -1):
        rig["arms"][side] = ("fk", 0.06, 0.2)
    if motion == "raise":
        aim_pose(rig, spec, (i + 1) / (n + 1))
    elif motion == "aim":
        rig["breath"] = 0.25 * i
        aim_pose(rig, spec, 1.0, kick=0.015 * i)
    elif motion == "recoil":
        kick, push, back = RECOIL[i]
        force = RECOIL_FORCE[gun]
        rig["lean"] = -back * force
        aim_pose(rig, spec, 1.0, kick=kick * min(force, 1.3), push=push * force)
    if gun in ("pistol", "none", "club"):
        # Die freie Hand hängt locker etwas vor dem Körper.
        rig["arms"][1] = ("fk", 0.15, 0.35)
    return rig


def strike_rig(i):
    hand, club, twist, lean = STRIKE[i]
    rig = stand_rig(0.0, "long")
    rig["twist"], rig["lean"] = twist, lean
    rig["carry"] = False
    shoulder = arm_root(rig, -1)
    rig["arms"][-1] = ("ik", shoulder + turn_z(hand, twist), (-0.3, -1.0, -0.6))
    rig["arms"][1] = ("fk", 0.55, 0.9)
    rig["club"] = unit(turn_z(club, twist))
    return rig


def throw_rig(i):
    hand, twist, lean, can = THROW[i]
    rig = stand_rig(0.0, "long")
    rig["twist"], rig["lean"] = twist, lean
    rig["carry"] = False
    rig["busy"] = True
    rig["can"] = can
    shoulder = arm_root(rig, -1)
    rig["arms"][-1] = ("ik", shoulder + turn_z(hand, twist), (-0.4, -1.0, -0.5))
    rig["arms"][1] = ("fk", 1.25, 0.25) if i < 2 else ("fk", 0.45, 0.7)
    return rig


def bandage_rig(phase):
    """Kniend: das rechte Knie am Boden, die rechte Hand wickelt die Binde um den linken Unterarm."""
    rig = new_rig()
    rig["pelvis"] = np.array((-0.5, 0.0, 13.6))
    feet = {1: (np.array((6.5, 3.4, ANKLE)), 0.0), -1: (np.array((-10.0, -3.3, 2.2)), -0.8)}
    legs_from_feet(rig, feet)
    rig["coat"] = {1: 0.9, -1: 0.0}
    rig["lean"] = 0.3
    rig["head_pitch"] = 0.35
    rig["carry"] = False
    rig["busy"] = True
    rig["bandage"] = True
    t = phase * math.tau
    rig["arms"][1] = ("ik", upper(rig, (8.0, 2.0, 8.0)), (0.0, 1.0, -1.0))
    rig["arms"][-1] = ("ik", upper(rig, (8.2, 1.0, 8.6)) + np.array((0.0, 1.9 * math.cos(t), 1.9 * math.sin(t))), (0.0, -1.0, -1.0))
    rig["breath"] = 0.2 * math.sin(2 * t)
    return rig


def leg_points(pelvis, side, swing, knee):
    """Für das Fallen: Hüfte zu Knie zu Knöchel über Winkel. swing nach vorn, knee beugt nach hinten."""
    hip = pelvis + (0.0, side * 3.0, 0.0)
    kx, kz = math.sin(swing) * THIGH, -math.cos(swing) * THIGH
    ax = kx + math.sin(swing - knee) * SHIN
    az = kz - math.cos(swing - knee) * SHIN
    return hip, hip + (kx, 0, kz), hip + (ax, 0, az), swing - knee


def fall_pose(motion, u):
    """Haltung beim Sterben. Erst zuckt der Körper, dann geben die Knie nach, zuletzt sackt er zusammen.

    fallA sinkt nach vorn auf die Knie und aufs Gesicht, fallB dreht sich weg und fällt auf die Seite.
    Die Arme fangen nichts ab: Wer getroffen ist, stützt sich nicht mehr auf.
    """
    jolt = ramp(u, 0.0, 0.14) * (1 - ramp(u, 0.14, 0.4))
    buckle = ramp(u, 0.12, 0.62)
    drop = ramp(u, 0.5, 1.0)
    if motion == "fallA":
        return dict(
            swing_l=(0.1 + 0.85 * buckle) * (1 - drop), swing_r=(-0.1 + 0.5 * buckle) * (1 - drop),
            # Beim Aufschlag strecken sich die Beine ganz, sonst ragten die Füße in die Luft.
            knee_l=2.15 * buckle * (1 - drop), knee_r=1.75 * buckle * (1 - drop),
            lean=(-0.24 * jolt + 0.8 * buckle) * (1 - drop), arm_l=-0.5 * jolt + 0.3 * buckle - 0.75 * drop,
            arm_r=-0.35 * jolt + 0.15 * buckle - 0.5 * drop, elbow_l=0.25 + 0.9 * buckle - 0.6 * drop,
            elbow_r=0.25 + 0.5 * buckle, pitch=math.radians(86) * drop, roll=math.radians(10) * drop, yaw=math.radians(18) * drop,
        )
    return dict(
        swing_l=0.05 + 0.75 * buckle, swing_r=-0.05 + 0.2 * buckle, knee_l=1.9 * buckle, knee_r=0.75 * buckle,
        lean=-0.3 * jolt + 0.32 * buckle, arm_l=1.1 * jolt + 0.1 * buckle, arm_r=-0.9 * jolt + 1.1 * drop,
        elbow_l=0.2 + 0.9 * buckle, elbow_r=0.15,
        pitch=math.radians(12) * drop, roll=-math.radians(84) * drop, yaw=math.radians(35) * buckle,
    )


def build_fall(spec, motion, u):
    dying = fall_pose(motion, u)
    rig = new_rig()
    rig["lean"] = dying["lean"]
    swings = {1: (dying["swing_l"], dying["knee_l"]), -1: (dying["swing_r"], dying["knee_r"])}
    low = min(leg_points(np.zeros(3), side, s, k)[2][2] for side, (s, k) in swings.items())
    rig["pelvis"] = np.array((0.0, 0.0, -low + ANKLE))
    for side, (s, k) in swings.items():
        rig["legs"][side] = leg_points(rig["pelvis"], side, s, k)
        rig["coat"][side] = 0.75 * s
    rig["arms"][1] = ("fk", dying["arm_l"], dying["elbow_l"])
    rig["arms"][-1] = ("fk", dying["arm_r"], dying["elbow_r"])
    p = body(spec, rig, dying=True)
    collapse(p, dying)
    return p


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

    p.parts = [(turn(a), turn(b), r1, r2, m, k) for a, b, r1, r2, m, k in p.parts]
    # Auf die Mitte der Ausdehnung rücken, nicht auf den Schwerpunkt: Sonst ragen die Beine aus der Zelle.
    low = np.min([np.minimum(a[:2], b[:2]) - r for a, b, r, _r2, _m, _k in p.parts], axis=0)
    high = np.max([np.maximum(a[:2], b[:2]) + r for a, b, r, _r2, _m, _k in p.parts], axis=0)
    center = (low + high) / 2
    p.move(np.array((-center[0], -center[1], -p.lowest())))


# ---------- Hund ----------
DOG_LEGS = {"FL": (5.0, 1), "FR": (5.0, -1), "HL": (-8.0, 1), "HR": (-8.0, -1)}


def dog_frame(motion, i, n):
    """Haltung des Hundes: Füße, Höhe vorn und hinten, Kopf, Kiefer, Rute."""
    phase = i / n
    t = phase * math.tau
    state = dict(lunge=0.0, jaw=0.08, head=(7.5, 6.5), head_yaw=0.0, tail=0.0, ears=0.0, flex=0.0, breath=0.0)
    feet = {}
    if motion in DOG_GAITS:
        g = DOG_GAITS[motion]
        for leg, offset in g["offsets"]:
            x, z, _angle = foot_path((phase + offset) % 1.0, g)
            base, side = DOG_LEGS[leg]
            feet[leg] = np.array((base + x, side * 2.5, z))
        flex = g["flex"] * math.cos(math.tau * (phase - 0.3))
        state["flex"] = flex
        bounce = g["bounce"] * math.cos(2 * math.tau * phase)
        state["front"] = g["height"] + bounce
        state["hind"] = g["height"] - bounce
        state["tail"] = 0.8 * math.sin(2 * t)
        if motion == "run":
            state["head"] = (8.0, 4.0 + 0.8 * math.sin(t))
            state["jaw"] = 0.3
            state["ears"] = -0.6
        return feet, state
    for leg, (base, side) in DOG_LEGS.items():
        feet[leg] = np.array((base + (0.4 if leg[0] == "F" else -0.3), side * 2.6, 0.95))
    state["front"] = state["hind"] = 13.2
    if motion == "idle":
        # Hecheln, die Rute pendelt ruhig, das Gewicht verlagert sich kaum merklich.
        state["jaw"] = 0.28 + 0.1 * math.sin(4 * t)
        state["breath"] = 0.35 * math.sin(4 * t)
        state["tail"] = 0.45 * math.sin(t)
        state["head"] = (7.5, 6.5 + 0.3 * math.sin(t))
    elif motion == "look":
        state["head_yaw"] = LOOK[i] * 0.9
        state["ears"] = 0.3
        state["tail"] = 0.2
    elif motion in ("raise", "aim"):
        # Ducken vor dem Sprung: vorn tief, Kopf gesenkt, Ohren angelegt, Lefzen hoch.
        a = (i + 1) / (n + 1) if motion == "raise" else 1.0
        state["front"] = 13.2 - 2.8 * a
        state["hind"] = 13.2 - 1.2 * a
        state["head"] = (lerp(7.5, 8.5, a), lerp(6.5, 2.2, a))
        state["jaw"] = lerp(0.1, 0.4 + 0.08 * i * (motion == "aim"), a)
        state["ears"] = -0.8 * a
        state["tail"] = -0.4 * a
    elif motion == "recoil":
        # Sprung: Vorderbeine heben ab, der Hals streckt sich, der Fang schnappt zu und lässt wieder los.
        lunge = (6.5, 8.5, 3.5)[i]
        state["lunge"] = lunge
        state["front"] = (14.0, 13.0, 12.6)[i]
        state["hind"] = (12.2, 11.8, 12.8)[i]
        state["head"] = (9.5, (3.2, 2.6, 3.8)[i])
        state["jaw"] = (0.8, 0.05, 0.3)[i]
        state["ears"] = -0.9
        state["tail"] = 0.3
        for leg in ("FL", "FR"):
            base, side = DOG_LEGS[leg]
            feet[leg] = np.array((base + lunge + (1.5, 0.5, 0.0)[i], side * 2.6, (3.5, 1.4, 0.95)[i]))
    return feet, state


def build_dog(motion, i, n):
    """Schäferhund: Rumpf, Kopf mit Ohren und Kiefer, Rute und vier Beine, die Füße am Boden geführt."""
    if motion.startswith("fall"):
        return build_dog_fall(motion, i / (n - 1))
    feet, s = dog_frame(motion, i, n)
    return dog_body(feet, s)


def dog_body(feet, s, limp=0.0):
    p = Pose()
    lunge, flex = s["lunge"], s["flex"]
    front_x, hind_x = 5.0 + lunge + flex, -8.0 + lunge * 0.55 - flex
    # Höhe vorn und hinten so, dass sich kein Bein überstreckt.
    reach = 2 * DOG_LEG - 0.15
    front_z, hind_z = s["front"], s["hind"]
    for leg, foot in feet.items():
        top_x = front_x if leg[0] == "F" else hind_x
        dx, dy = foot[0] - top_x, foot[1] - math.copysign(2.6, foot[1])
        limit = foot[2] + math.sqrt(max(1.0, reach * reach - dx * dx - dy * dy)) + 1.5
        if leg[0] == "F":
            front_z = min(front_z, limit)
        else:
            hind_z = min(hind_z, limit)
    chest = np.array((front_x, 0.0, front_z + 1.5 + s["breath"] * 0.3))
    rump = np.array((hind_x, 0.0, hind_z))
    p.capsule(chest, rump, 4.3 + s["breath"] * 0.15, 3.5, COAT)
    p.capsule(chest + (0, 0, 2.0), rump + (0, 0, 2.0), 2.6, 2.2, COAT_D)
    hx, hz = s["head"]
    yaw = s["head_yaw"]
    neck = chest + (3.0, 0, 3.0 * (1 - limp))
    head = chest + turn_z((hx, 0, hz * (1 - limp) - 2.5 * limp), yaw)
    p.capsule(neck, head, 2.7, 2.3, COAT)
    forward = unit(turn_z((1.0, 0.0, 0.0), yaw) + np.array((0, 0, -0.1 * limp)))
    jaw = s["jaw"]
    # Oberkiefer mit Nase, Unterkiefer klappt nach unten auf
    p.capsule(head, head + forward * 4.5 + (0, 0, -1.0), 1.7, 1.0, COAT_L)
    p.ball(head + forward * 5.0 + (0, 0, -0.9), 0.85, SHOE)
    lower = head + (0, 0, -1.1)
    jaw_dir = unit(forward * math.cos(jaw) + np.array((0, 0, -math.sin(jaw))))
    p.capsule(lower, lower + jaw_dir * 4.0, 1.2, 0.75, COAT_L)
    side_dir = turn_z((0.0, 1.0, 0.0), yaw)
    ear_tilt = s["ears"]
    for side in (1, -1):
        root = head - forward * 1.0 + side_dir * side * 1.6 + (0, 0, 1.5)
        tip = root + side_dir * side * 0.3 + forward * (-0.4 + 2.2 * min(0.0, ear_tilt) * 0.6) + (0, 0, 2.7 * (1 + 0.5 * min(0.0, ear_tilt)))
        p.capsule(root, tip, 1.0, 0.45, COAT_D)
    wag = s["tail"]
    tail_end = rump + (-10, wag * 3, 3 + 2 * wag - 4.0 * limp)
    p.capsule(rump + (-3, 0, 1), tail_end, 1.2, 0.6, COAT_D)
    for leg, foot in feet.items():
        base, side = DOG_LEGS[leg]
        top_x = front_x if leg[0] == "F" else hind_x
        top = np.array((top_x, side * 2.6, (front_z if leg[0] == "F" else hind_z) - 1.5))
        # Vorn knickt das Handgelenk nach vorn aus, hinten steht das Sprunggelenk nach hinten.
        knee, paw = two_bone(top, foot, DOG_LEG, DOG_LEG, (1.0 if leg[0] == "F" else -1.0, 0.0, 0.0))
        material = COAT_D if side < 0 else COAT
        p.capsule(top, knee, 1.6, 1.15, material)
        p.capsule(knee, paw, 1.15, 0.9, material)
        p.capsule(paw, paw + (1.4, 0, 0), 0.95, 0.9, SHOE)
    p.move(np.array((DOG_SHIFT, 0.0, 0.0)))
    return p


def build_dog_fall(motion, u):
    """Der Hund knickt ein: erst die Beine, dann sinkt er auf die Brust und rollt kraftlos auf die Seite."""
    feet, s = dog_frame("idle", 0, 8)
    buckle = ramp(u, 0.05, 0.5)
    roll = ramp(u, 0.4, 0.88)
    first = "H" if motion == "fallA" else "F"
    for leg, (base, side) in DOG_LEGS.items():
        # Beim Umfallen lassen die Beine los und liegen leicht angewinkelt neben dem Körper.
        relaxed = np.array((base + (2.5 if leg[0] == "F" else -2.0), side * 3.5, 4.0))
        feet[leg] = lerp(feet[leg], relaxed, roll)
    early, late = ramp(u, 0.02, 0.35), buckle
    s["front"] = 13.2 - 6.5 * (late if first == "H" else early)
    s["hind"] = 13.2 - 7.0 * (early if first == "H" else late)
    s["jaw"] = 0.25 + 0.2 * roll
    s["tail"] = -0.3 * buckle
    s["ears"] = -0.5 * buckle
    p = dog_body(feet, s, limp=roll)
    p.move(np.array((-DOG_SHIFT, 0.0, 0.0)))
    collapse(p, dict(pitch=0.0, roll=(1 if motion == "fallA" else -1) * math.radians(82) * roll, yaw=0.0))
    p.move(np.array((DOG_SHIFT, 0.0, 0.0)))
    return p


# ---------- Rendern ----------
def sdf(points, parts):
    """Abstand zur Figur je Punkt und welches Teil am nächsten ist. Weiche Vereinigung, damit keine Kanten entstehen."""
    all_d = np.empty((len(parts), len(points)), np.float32)
    for i, (a, b, r1, r2, _m, kind) in enumerate(parts):
        pa = points - a
        if kind == DISC:
            axis = b - a
            along = pa @ axis
            across = np.linalg.norm(pa - along[:, None] * axis, axis=1)
            all_d[i] = np.hypot(np.maximum(across - r1, 0.0), along) - r2
            continue
        ba = b - a
        length = float(ba @ ba)
        h = np.clip((pa @ ba) / length, 0.0, 1.0) if length > 1e-6 else np.zeros(len(points))
        all_d[i] = np.linalg.norm(pa - h[:, None] * ba, axis=1) - (r1 + (r2 - r1) * h)
    low = all_d.min(axis=0)
    soft = low - SMOOTH * np.log(np.exp(-(all_d - low) / SMOOTH).sum(axis=0))
    return soft, all_d.argmin(axis=0)


def world_parts(spec, motion, i, n, view):
    pose = build(spec, motion, i, n)
    yaw = math.radians(view)
    forward = np.array((math.cos(yaw), math.sin(yaw), 0.0))
    side = np.array((-math.sin(yaw), math.cos(yaw), 0.0))
    parts = []
    for a, b, r1, r2, material, kind in pose.parts:
        world = [q[0] * forward + q[1] * side + np.array((0, 0, q[2])) for q in (a, b)]
        parts.append((world[0], world[1], r1, r2, material, kind))
    return parts


def render(args):
    name, view, column = args
    spec = SPECS[name]
    motion, i, n = FRAMES[column]
    parts = world_parts(spec, motion, i, n, view)

    w, h = CELL_W * SUPER, CELL_H * SUPER
    xs = (np.arange(w) + 0.5) / (SUPER * RES) - WORLD_FOOT_X
    ys = (np.arange(h) + 0.5) / (SUPER * RES) - WORLD_FOOT_Y
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
        normal = np.stack([sdf(pts + np.eye(3)[k] * e, parts)[0] - sdf(pts - np.eye(3)[k] * e, parts)[0] for k in range(3)], axis=1)
        normal /= np.maximum(np.linalg.norm(normal, axis=1, keepdims=True), 1e-5)
        light = np.clip(normal @ LIGHT, 0, 1)
        facing = np.clip(normal @ TO_CAMERA, 0, 1)
        # Licht von oben, dunkler zum Rand hin: Das macht die Figur rund. In Stufen, damit es Pixelgrafik bleibt.
        shade = (0.58 + 0.62 * light) * (0.68 + 0.32 * facing)
        shade = np.round(shade * SHADE_STEPS) / SHADE_STEPS
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
    return name, view, column, ground_marks(out, motion, i / max(n - 1, 1), parts)


def ground_marks(figure, motion, phase, parts):
    """Weicher Schatten unter den Füßen. Beim Fallen wächst stattdessen eine Blutlache unter dem Körper, längs zu ihm."""
    h, w = figure.shape[:2]
    under = np.zeros((h, w, 4), np.uint8)
    ys, xs = np.mgrid[0:h, 0:w]
    if motion.startswith("fall"):
        grow = max(0.0, phase - 0.55) / 0.45
        if grow > 0:
            points = np.array([q[:2] for a, b, *_ in parts for q in (a, b)])
            center = points.mean(axis=0)
            spread = np.cov((points - center).T)
            values, vectors = np.linalg.eigh(spread)
            along, across = vectors[:, 1], vectors[:, 0]
            length = 2.0 * math.sqrt(max(values[1], 1e-3))
            # Vom Bildpunkt zurück auf den Boden: x wie im Bild, die Tiefe ist im Bild halb so lang.
            gx = (xs + 0.5) / RES - WORLD_FOOT_X - center[0]
            gy = 2 * ((ys + 0.5) / RES - WORLD_FOOT_Y) - center[1]
            u = (gx * along[0] + gy * along[1]) / ((2 + 0.75 * length) * grow + 1e-3)
            v = (gx * across[0] + gy * across[1]) / ((2.5 + 0.25 * length) * grow + 1e-3)
            d = u * u + v * v
            under[d < 1] = (72, 10, 10, 225)
            under[d < 0.45] = (96, 14, 12, 225)
    else:
        d = ((xs - FOOT_X) / (9.0 * RES)) ** 2 + ((ys - FOOT_Y) / (3.2 * RES)) ** 2
        under[d < 1] = (0, 0, 0, 105)
        under[d < 0.45] = (0, 0, 0, 140)
    solid = figure[..., 3] > 0
    under[solid] = figure[solid]
    return under


def columns():
    table, start = {}, 0
    for motion, n in MOTIONS:
        table[motion] = [start, n]
        start += n
    return table


def strides():
    """Weg pro Zyklus in Spieleinheiten. Das Spiel schaltet die Bilder nach zurückgelegtem Weg weiter."""
    table = {}
    for name, spec in SPECS.items():
        gaits = DOG_GAITS if spec.get("dog") else GAITS
        scale = spec.get("scale", 1.0)
        table[name] = {motion: round(gaits[motion]["stride"] * scale * WORLD_PER_UNIT) for motion in ("walk", "run", "aimwalk")}
    return table


def bite_reach():
    """Wie weit die Schnauze im Sprung vor dem Standort des Hundes ist, in Spieleinheiten."""
    table = {}
    for name, spec in SPECS.items():
        if spec.get("dog"):
            lunge = build_dog("recoil", 1, 3)
            table[name] = round(max(max(a[0] + r1, b[0] + r2) for a, b, r1, r2, _m, _k in lunge.parts) * WORLD_PER_UNIT)
    return table


def main():
    names = list(SPECS)
    tasks = [(name, view, column) for name in names for view in VIEWS for column in range(len(FRAMES))]
    sheet = np.zeros((CELL_H * len(names) * len(VIEWS), CELL_W * len(FRAMES), 4), np.uint8)
    with multiprocessing.Pool() as pool:
        for name, view, column, cell in pool.imap_unordered(render, tasks, chunksize=8):
            row = names.index(name) * len(VIEWS) + VIEWS.index(view)
            sheet[row * CELL_H:(row + 1) * CELL_H, column * CELL_W:(column + 1) * CELL_W] = cell
    assets = ROOT / "game" / "assets"
    # Das Spiel zeichnet scharf-bilinear wie die Stadt: Ränder bekommen die Farbe ihrer Nachbarn, sonst säumt sie Schwarz.
    bleed(Image.fromarray(sheet), rounds=2).save(assets / "figures.png", optimize=True)
    (assets / "figures.json").write_text(json.dumps({
        "names": names, "w": CELL_W, "h": CELL_H, "footX": FOOT_X, "footY": FOOT_Y, "views": len(VIEWS), "res": RES,
        "columns": columns(), "stride": strides(), "biteReach": bite_reach(),
    }), encoding="utf-8")
    print(f"{len(tasks)} Figurenbilder gerendert")


if __name__ == "__main__":
    main()
