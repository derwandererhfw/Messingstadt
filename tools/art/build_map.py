"""Baut die Grafik einer weiteren Karte aus ihrem Grundriss und dem Grafikthema ihres Viertels.

Aufruf (aus dem Projektordner):
    python tools/art/build_map.py 02            Bilder, Manifest, Kulisse und Licht
    python tools/art/build_map.py 02 --no-bake  ohne Lichtbake
    python tools/art/build_map.py 02 --preview=bild.png   nur ein Übersichtsbild, nichts wird geschrieben

Liest data/scene_<nr>.json (Level: Gebäude, Mauern, Requisiten, Laternen, Straßen) und schreibt nach
game/assets/maps/<id>/ dieselben Dateien wie generate_concept.py für das Kesselviertel: ground.png, things/, hd/,
manifest.json, backdrop.png, nach dem Lichtbake light/ und panes.json, dazu data/light_<nr>.json. Die Regelkarte
data/map_<nr>.json schreibt weiter tools/level/scene_to_map.py, hier wird nur geprüft, dass sie zu den Bildern passt.

Ein Thema (THEMES) legt fest, woraus ein Viertel gebaut ist: Farben der Baustoffe, Boden, Kulisse. Wie ein Gebäude
aussieht, entscheidet seine Art ("kind") im Grundriss, je Thema gibt es dafür einen Baumeister. Das Kesselviertel
entsteht weiter in generate_concept.py und wird von hier aus nicht angefasst.

Dächer bleiben hier flach geneigt: Von Norden und Westen steigen sie höchstens so steil wie die Blickrichtung, damit
kein Dach mehr verdeckt als der Quader, mit dem die Level-Sitzung die Sicht geplant hat.
"""
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

import generate_concept as g
import roofs as R

ROOT = Path(__file__).resolve().parents[2]
HW = g.HW

THEMES = {
    "nordwall": {
        # Behördenviertel: dunkler Klinker, grauer Granit, schwarzes Gusseisen. Kein Werksgrün, keine Reklame.
        "palette": {
            "brick_d": (56, 38, 35), "brick": (80, 52, 44), "brick_l": (98, 64, 52), "mortar": (60, 50, 47),
            "stone_d": (84, 82, 80), "stone": (108, 106, 102), "stone_l": (134, 132, 126),
            "cobble_d": (60, 60, 68), "cobble": (84, 84, 92), "cobble_l": (106, 106, 112),
            "wood_d": (52, 44, 40), "wood": (78, 64, 52), "iron": (48, 50, 56), "soot": (36, 38, 42),
        },
        "seed": 1880,
        "backdrop_keep": lambda x, y, w, d: y + d > 0.5,  # nördlich des Walls liegt keine Stadt mehr
        # Sitzplätze der Tauben: Feld x, y und Höhe in Bildpunkten, jeweils auf einer Dachkante, die zur Kamera zeigt.
        # Die Höhe ist die Traufe des Hauses darunter, daran findet das Spiel die richtige Zeichentiefe.
        "perches": [(3.0, 1.95, 60), (3.6, 1.95, 60), (12.4, 1.95, 60), (17.2, 1.95, 60), (9.0, 9.95, 40), (9.6, 9.95, 40),
                    (18.95, 5.0, 120), (18.95, 5.6, 120), (16.0, 16.95, 56), (4.95, 4.4, 150), (4.95, 5.0, 150)],
    },
}


# ---------- Dächer ----------
def mat_low(f, tint=(1.0, 1.0, 1.0), cap=None):
    if f.kind != "plane":
        return g.brick_np(*f.wall_uv(), f.res)
    if f.plane == len(f.prism.planes) - 1:
        return (cap or g.mat_tar)(f)
    return g.mat_slate(f) * np.array(tint)[None, :]


def mat_zinc(f):
    """Zinkblech in Bahnen, matt und hell: die Kappe über dem Mannschaftshaus."""
    return g.mat_sheet(f, (104, 110, 118), 0.35)


def roof_low(cap=22, k=26, tint=(1.0, 1.0, 1.0), top=None):
    """Flaches Walmdach aus Schiefer, obenauf Teerpappe, Kupfer oder Zink. Steigt von jeder Kante mit k Bildpunkten je Feld."""
    def make(x0, y0, x1, y1, h):
        planes = [R.rise_from(e, a, h, k) for e, a in (("x0", x0), ("x1", x1), ("y0", y0), ("y1", y1))] + [R.flat(h + cap)]
        return [R.Prism(x0, y0, x1, y1, h, planes, lambda f: mat_low(f, tint, top), "low")]
    return make


def mat_turret(f):
    """Dachreiter: verschieferter Kasten, nach Süden und Osten je ein erleuchtetes Zifferblatt."""
    p = f.prism
    if f.kind == "plane":
        return g.mat_copper(f)
    col = g.mat_slate(f) * 0.9
    if f.kind not in ("x1", "y1"):
        return col
    u = (p.y1 - f.fy) * HW if f.kind == "x1" else (f.fx - p.x0) * HW
    half = (p.x1 - p.x0) * HW / 2
    du, dv = u - half, f.z - (p.top() - half - 1)
    r = np.hypot(du, dv)
    col = np.where((r < half - 1.5)[:, None], g._c("stone_l")[None, :], col)
    face = r < half - 3
    col = np.where(face[:, None], np.array((236, 222, 176.0))[None, :], col)
    hands = face & (((np.abs(du) < 0.6) & (dv > 0) & (dv < half - 5)) | ((np.abs(dv + du * 0.3) < 0.6) & (du > 0) & (du < half - 6)))
    marks = face & (r > half - 4.6) & ((np.abs(du) < 0.7) | (np.abs(dv) < 0.7))
    col = np.where((hands | marks)[:, None], g._c("black")[None, :], col)
    return col, face & ~hands & ~marks


def with_turret(roof, fx, fy, size=0.9, lift=54, spire=24):
    """Setzt einen Dachreiter mit Kupferhelm aufs Dach. Er gehört zum Bild des Hauses und wirft im Schleichlicht keinen Schatten."""
    def make(x0, y0, x1, y1, h):
        prisms = roof(x0, y0, x1, y1, h)
        x, y = x0 + fx * (x1 - x0), y0 + fy * (y1 - y0)
        top = h + lift
        k = spire / (size / 2)
        helm = R.Prism(x - 0.06, y - 0.06, x + size + 0.06, y + size + 0.06, top,
                       [R.rise_from(e, a, top, k) for e, a in (("x0", x - 0.06), ("x1", x + size + 0.06), ("y0", y - 0.06), ("y1", y + size + 0.06))],
                       g.mat_copper, "helm")
        return prisms + [R.block(x, y, x + size, y + size, h, top, mat_turret, "turret"), helm]
    return make


def rod(fx, fy, height=22, flag=False):
    """Blitzableiter oder Fahnenmast auf dem Dach: eine dünne Stange, am Mast hängt ein nasses Tuch in den Farben der Stadtwache."""
    def add(x0, y0, w, d, h, s, things, house):
        x, y = x0 + fx * w, y0 + fy * d
        base = g.roof_base(house, h, x, y)

        def draw():
            k = s.res
            dr = ImageDraw.Draw(s.img)
            px, py = s.to_screen(x, y, base)
            px, py = int(px), int(py)
            dr.rectangle((px, py - height * k, px + (k - 1 if flag else max(0, k - 2)), py), fill=g.C["steel" if flag else "iron"] + (255,))
            if flag:
                dr.rectangle((px - k, py - height * k - 2 * k, px + 2 * k - 1, py - height * k - 1), fill=g.C["brass"] + (255,))
                top = py - (height - 2) * k
                for i in range(7 * k):  # das Tuch hängt schlaff und wird nach unten schmaler
                    wide = int((9 - 5 * i / (7 * k)) * k * (0.8 + 0.2 * math.sin(i / k * 1.7)))
                    for j in range(wide):
                        stripe = (j * 3) // max(1, wide)
                        col = (g.C["navy_d"], g.C["paper"], g.C["navy_d"])[stripe]
                        s.img.putpixel((px + k + j, top + i + j // 3), tuple(int(c * (0.75 if (i // k + j // k) % 3 == 0 else 0.95)) for c in col) + (255,))
            else:
                dr.rectangle((px - k // 2, py - 2 * k, px + k // 2 + 1, py), fill=g.C["iron"] + (255,))
        things.append(g.Thing(x - 0.05, y - 0.05, x + 0.05, y + 0.05, base, base + height, draw))
        things[-1].kind = "open"
    return add


def roof_flat(rise=3, sheet=False):
    """Flachdach hinter einer niedrigen Brüstung, oder ein Blechdach, das nach Süden leicht ansteigt."""
    def make(x0, y0, x1, y1, h):
        if sheet:
            return [R.Prism(x0, y0, x1, y1, h, [R.rise_from("y0", y0, h + 1, 4)], lambda f: g.mat_sheet(f, (72, 76, 82), 0.5) if f.kind == "plane" else g.brick_np(*f.wall_uv(), f.res), "sheet")]
        return [R.Prism(x0, y0, x1, y1, h, [R.flat(h + rise)], lambda f: g.mat_tar(f) if f.kind == "plane" else g.brick_np(*f.wall_uv(), f.res), "flat")]
    return make


def mat_walk(f):
    """Wehrgang: grobe Steinplatten, an den Rändern Moos."""
    if f.kind != "plane":
        return g.brick_np(*f.wall_uv(), f.res, 5)
    a, b = f.fx * 3.1, f.fy * 3.1
    joint = ((a % 1) < 0.08) | ((b + 0.5 * np.floor(a)) % 1 < 0.08)
    col = g._c("stone")[None, :] * (0.62 + 0.3 * R.hash01(np.floor(a), np.floor(b + 0.5 * np.floor(a)), 3))[:, None]
    col = np.where(joint[:, None], g._c("soot")[None, :], col)
    moss = R.noise(f.fx * 3, f.fy * 6, 4) > 0.66
    return np.where(moss[:, None], col * 0.6 + g._c("moss")[None, :] * 0.4, col)


def roof_rampart(x0, y0, x1, y1, h):
    """Wehrgang auf dem Wall: Lauffläche, nach Süden eine Brustwehr mit Zinnen."""
    prisms = [R.Prism(x0, y0, x1, y1, h, [R.flat(h + 1)], mat_walk, "walk")]
    x = x0
    while x < x1 - 0.05:
        prisms.append(R.block(x, y1 - 0.2, min(x1, x + 0.72), y1, h, h + 11, mat_walk, "merlon"))
        x += 1.0
    prisms.append(R.block(x0, y1 - 0.2, x1, y1, h, h + 5, mat_walk, "parapet"))
    for tx in (5.0, 14.1):  # Wachtürme über den beiden Gassen, die auf den Wall stoßen. Nördlich davon liegt kein Feld mehr
        if x0 + tx + 1.5 < x1:
            prisms.append(R.block(x0 + tx, y0 + 0.2, x0 + tx + 1.5, y1, h, h + 30, mat_tower, "tower"))
            for cx, cy in ((0, 0.2), (1.1, 0.2), (0, y1 - y0 - 0.4), (1.1, y1 - y0 - 0.4)):
                prisms.append(R.block(x0 + tx + cx, y0 + cy, x0 + tx + cx + 0.4, y0 + cy + 0.4, h, h + 37, mat_walk, "merlon"))
    return prisms


def mat_tower(f):
    """Wachturm auf dem Wall: Backstein, oben ein Band aus Granit, nach Süden und Osten je eine dunkle Scharte."""
    p = f.prism
    if f.kind == "plane":
        return mat_walk(f)
    col = g.brick_np(*f.wall_uv(), f.res, 7)
    top = p.top()
    col = np.where((top - f.z < 3)[:, None], g._c("stone")[None, :], col)
    if f.kind in ("x1", "y1"):
        u = (p.y1 - f.fy) * HW if f.kind == "x1" else (f.fx - p.x0) * HW
        mid = ((p.y1 - p.y0) if f.kind == "x1" else (p.x1 - p.x0)) * HW / 2
        slit = (np.abs(u - mid) < 1.6) & (top - f.z > 9) & (top - f.z < 21)
        col = np.where(slit[:, None], g._c("black")[None, :], col)
    return col


# ---------- Fassaden des Nordwalls ----------
def office_front(w, h, lit_rate, barred=False, name=None, floors=None, door_at=None, force=(), helper=None):
    """Amtsfassade aus Klinker: Granitsockel, Gesimse, Rundbogenfenster. Erleuchtete Stuben haben schlichte Rechteckfenster."""
    img, d = g.wall(w, h, ("brick",))
    g.plinth(d, w, h, 11)
    top = 12
    if name:
        g.frieze(img, d, w, name)
        top = 30
    else:
        g.cornice(d, w)
    floors = floors or max(1, (h - top - 14) // 42)
    # helper: (Name, Stelle auf der Wand in Bildpunkten). Das nächste Fenster im Erdgeschoss ist seins: Dort steht die Kerze.
    columns = [u for u in range(9, w - 18, 24) if door_at is None or abs(u + 6 - door_at) >= 20]
    signal = min(columns, key=lambda u: abs(u + 6 - helper[1])) if helper and columns else None
    step = (h - top - 12) / floors
    for f in range(floors):
        base = int(h - 11 - f * step)
        if f:
            g.ledge(d, w, base)
        for col, u in enumerate(range(9, w - 18, 24)):
            y = base - int(step) + 14
            if f == 0 and door_at is not None and abs(u + 6 - door_at) < 20:
                continue
            if f == 0 and u == signal:
                g.RNG.random()
                g.window(d, u, y, 11, 17, lit=True)
                img.info.setdefault("panes", []).append((u, y, 11, 18, "kerze:" + helper[0]))
            elif (g.RNG.random() < lit_rate) | ((f, col) in force):  # force: (Geschoss, Spalte) brennt immer
                g.window(d, u, y, 11, 17, lit=True)
                img.info.setdefault("panes", []).append((u, y, 11, 18))
                if barred:
                    bars(d, u, y, 11, 17)
            else:
                g.arched_window(d, u, y + 6, 12, 14, bars=barred)
    return img, d


def bars(d, x, y, w, h):
    for u in range(x + 2, x + w - 1, 3):
        d.fine_line((u, y, u, y + h), fill=g.C["black"])
    d.fine_line((x, y + h // 2, x + w - 1, y + h // 2), fill=g.C["black"])


def cell_door(d, x, base):
    """Zellentür aus Eisen: genietet, mit Klappe und schwerem Riegel, in einem Gewände aus Granit."""
    d.rectangle((x - 3, base - 34, x + 15, base), fill=g.C["stone"])
    d.rectangle((x, base - 31, x + 12, base), fill=g.C["iron"])
    d.shade((x, base - 31, x + 12, base - 30), 0.6)
    for v in range(base - 28, base - 2, 6):
        for u in (x + 2, x + 10):
            d.point((u, v), fill=g.C["steel"])
    d.rectangle((x + 3, base - 25, x + 9, base - 20), fill=g.C["black"])
    for u in range(x + 4, x + 9, 2):
        d.fine_line((u, base - 25, u, base - 20), fill=g.C["steel"])
    d.rectangle((x + 1, base - 14, x + 11, base - 13), fill=g.C["black"])
    d.rectangle((x - 4, base - 2, x + 16, base), fill=g.C["stone_l"])
    d.streak(x + 1, base - 12, base - 3, 0.75, 1.0, g.C["rust"])


def portal(img, d, x, base, w=30, h=46):
    """Säulenportal mit Freitreppe: vier Stufen, zwei Säulenpaare, Gebälk und flacher Giebel."""
    for i in range(4):
        d.rectangle((x - 10 + i * 2, base - 2 - i * 2, x + w + 10 - i * 2, base - i * 2), fill=g.blend(g.C["stone_l"], 0.8 + 0.06 * i))
    top = base - 8
    d.rectangle((x + 6, top - h + 10, x + w - 6, top), fill=g.C["wood_d"])
    d.line((x + w // 2, top - h + 10, x + w // 2, top), fill=g.C["black"])
    d.shade((x + 6, top - h + 10, x + w - 6, top - h + 13), 0.55)
    for u in (x - 4, x + 1, x + w - 5, x + w):
        d.rectangle((u, top - h + 6, u + 3, top), fill=g.C["stone_l"])
        d.fine_line((u + 3, top - h + 6, u + 3, top), fill=g.C["stone_d"])
        d.rectangle((u - 1, top - h + 4, u + 4, top - h + 6), fill=g.C["stone_l"])
    d.rectangle((x - 7, top - h - 2, x + w + 6, top - h + 3), fill=g.C["stone_l"])
    d.line((x - 7, top - h + 4, x + w + 6, top - h + 4), fill=g.C["soot"])
    for i in range(7):
        d.rectangle((x - 6 + i * 3, top - h - 3 - i, x + w + 5 - i * 3, top - h - 3 - i), fill=g.C["stone"])


def clock(d, cx, cy, r=9):
    d.ellipse((cx - r - 2, cy - r - 2, cx + r + 2, cy + r + 2), fill=g.C["stone_l"])
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=g.blend(g.C["paper"], 0.62))
    for a in range(0, 360, 30):
        d.point((int(cx + math.cos(math.radians(a)) * (r - 2)), int(cy + math.sin(math.radians(a)) * (r - 2))), fill=g.C["black"])
    d.line((cx, cy, cx, cy - r + 3), fill=g.C["black"])
    d.line((cx, cy, cx + r - 5, cy + 2), fill=g.C["black"])


def notice_board(d, x, y):
    """Anschlagtafel: Verordnungen und Steckbriefe, sauber in Reih und Glied."""
    d.rectangle((x, y, x + 26, y + 17), fill=g.C["wood_d"])
    for i, u in enumerate((x + 2, x + 10, x + 18)):
        d.rectangle((u, y + 2, u + 6, y + 14), fill=g.blend(g.C["paper"], 0.74 - 0.05 * i))
        d.fine_line((u + 1, y + 4, u + 5, y + 4), fill=g.C["black"])
        for v in range(y + 7, y + 13, 2):
            d.fine_line((u + 1, v, u + 5, v), fill=g.C["ash"])


def prop_item(add):
    """Dachaufbau, der nur im Bild Schatten wirft: Das Schleichlicht bleibt, wie es war."""
    def wrapped(x0, y0, w, d, h, s, things, house):
        add(x0, y0, w, d, h, s, things, house)
        things[-1].kind = "prop"
    return wrapped


GATES = []  # Tore des Grundrisses, damit die Mauer daneben ihre Pfosten bekommt
HELPERS = {}  # Helferfenster des Grundrisses: Name -> (x, y)


def mat_post(f):
    return g._c("stone_l")[None, :] * (0.86 + 0.2 * R.noise(f.fx * 9, f.fy * 9 + f.z / 5, 21))[:, None]


def nordwall_building(b, s, things):
    """Ein Gebäude des Nordwalls nach seiner Art: zwei Fassaden, Dach und Aufbauten."""
    w, depth, h = b["x1"] - b["x0"], b["y1"] - b["y0"], b["height"]
    wl, wr = int(w * HW), int(depth * HW)
    lit = b.get("lit", 0.0)
    kind = b["kind"]
    items = []
    if kind == "stadtwall":
        left, d = g.wall(wl, h, ("brick",))
        for u in range(8, wl - 40, 64):  # zugemauerte Bögen zwischen Strebepfeilern
            d.ellipse((u + 10, 14, u + 46, 44), fill=g.C["brick_d"])
            d.rectangle((u + 10, 29, u + 46, h - 8), fill=g.C["brick_d"])
            for v in range(32, h - 8, 4):
                d.fine_line((u + 11, v, u + 45, v), fill=g.C["mortar"])
            d.shade((u + 10, 14, u + 46, 17), 0.7)
            d.rectangle((u - 4, 6, u + 3, h), fill=g.C["brick_l"])
            d.shade((u + 4, 8, u + 5, h), 0.6)
            d.rectangle((u + 26, 34, u + 29, 46), fill=g.C["black"])  # zugesetzte Scharte
        d.rectangle((0, 0, wl, 3), fill=g.C["stone"])
        d.shade((0, 4, wl, 5), 0.6)
        d.shade((0, h - 12, wl, h), 0.72, g.C["moss"], 0.35)
        right, dr = g.wall(wr, h, ("brick",))
        dr.rectangle((0, 0, wr, 3), fill=g.C["stone"])
        roof = roof_rampart
    elif kind == "amtsbau":
        door = (b.get("doors") or [{"at": w / 2}])[0]
        left, d = office_front(wl, h, lit, name=b.get("name"), door_at=int(door["at"] * HW))
        portal(left, d, int(door["at"] * HW) - 15, h - 1)
        clock(d, wl // 2, 42)
        right, dr = office_front(wr, h, lit)
        g.downpipe(dr, 6, 2, h - 4)
        # Kupferkappe und Dachreiter mit erleuchteter Uhr: das Gericht ist das Wahrzeichen des Viertels
        roof = with_turret(roof_low(24, 26, top=g.mat_copper), 0.39, 0.72)
        items = [g.chimney(0.7, 0.62, 16, False), prop_item(g.chimney(0.2, 0.3, 16, False)), rod(0.5, 0.795, 14)]
    elif kind == "wachstube":
        left, d = office_front(wl, h, lit, barred=True)
        for door in b.get("doors", ()):
            u = int(door["at"] * HW)
            d.rectangle((u - 12, h - 44, u + 12, h - 12), fill=g.C["brick"])  # das Fenster an dieser Stelle weicht der Zellentür
            cell_door(d, u - 6, h - 1)
        right, dr = office_front(wr, h, lit, barred=True)
        g.downpipe(d, wl - 8, 2, h - 4)
        roof = roof_low(18, 24, tint=(0.9, 0.96, 1.08))
        items = [g.chimney(0.8, 0.6, 14, False), rod(0.2, 0.5, 20)]
    elif kind == "wachlokal":
        left, d = g.wall(wl, h, ("brick",))
        g.plinth(d, wl, h, 6)
        d.rectangle((0, 0, wl, 3), fill=g.C["stone"])
        d.shade((0, 4, wl, 5), 0.62)
        doors = {door["kind"]: int(door["at"] * HW) for door in b.get("doors", ())}
        main, side = doors.get("haupttuer", wl // 2), doors.get("nebentuer", wl - 24)
        for u in range(10, wl - 20, 26):
            if abs(u + 5 - main) > 18 and abs(u + 5 - side) > 14 and not 20 < u < 52:
                on = g.RNG.random() < lit
                g.window(d, u, 13, 11, 15, lit=on)
                if on:
                    left.info.setdefault("panes", []).append((u, 13, 11, 16))
        g.door(d, main - 8, h - 1, 16, 26, "wood_d", False)
        d.rectangle((side - 6, h - 27, side + 6, h - 1), fill=g.C["stone"])
        d.rectangle((side - 5, h - 25, side + 5, h - 1), fill=g.C["wood_d"])
        d.point((side + 3, h - 12), fill=g.C["brass_l"])
        notice_board(d, 24, 12)
        if b.get("name"):
            g.name_plate(left, d, (main + side) // 2 - 2 if side - main > 90 else main - 62, 6, b["name"], 5)
        right, dr = g.wall(wr, h, ("brick",))
        dr.rectangle((0, 0, wr, 3), fill=g.C["stone"])
        roof = roof_flat(3)
    elif kind == "kaserne":
        left, d = office_front(wl, h, lit, door_at=wl // 2)
        g.door(d, wl // 2 - 8, h - 1, 16, 30, "wood_d", True)
        east = [door for door in b.get("doors", ()) if door["side"] == "ost"]
        right, dr = office_front(wr, h, lit, door_at=int((depth - east[0]["at"]) * HW) if east else None)
        if east:
            g.door(dr, int((depth - east[0]["at"]) * HW) - 8, h - 1, 16, 30, "wood_d", True)
        g.downpipe(dr, wr - 8, 2, h - 4)
        g.quoins(d, wl, 10, h - 12)
        g.name_plate(left, d, wl // 2, 8, "KASERNE", 6)
        # Zinkkappe, Schiefer mit warmem Stich, darauf eine Reihe Lüfter wie auf jedem Mannschaftshaus, an der Ecke die Fahne
        roof = roof_low(22, 24, tint=(1.12, 1.0, 0.88), top=mat_zinc)
        items = [g.chimney(0.25, 0.7, 16, False), g.chimney(0.75, 0.7, 16, False)] + [prop_item(g.vent(0.46, fy)) for fy in (0.2, 0.42, 0.64)]
        items.append(rod(0.9, 0.9, 40, flag=True))
    elif kind == "remise":
        left = g.canvas(wl, h, g.C["soot"])
        d = g.draw_on(left)
        d.rectangle((0, 0, wl, 7), fill=g.C["wood"])
        d.shade((0, 8, wl, 10), 0.6)
        for u in range(0, wl + 1, wl // 3):  # Pfosten zwischen drei offenen Fächern
            d.rectangle((max(0, u - 3), 7, min(wl - 1, u + 3), h), fill=g.C["wood"])
            d.fine_line((max(0, u - 3), 7, max(0, u - 3), h), fill=g.C["wood_d"])
        prison_van(d, wl // 3 + 6, h - 2)
        for u in range(wl * 2 // 3 + 8, wl - 8, 9):  # im dritten Fach Fässer und Geschirr
            d.rectangle((u, h - 16, u + 6, h - 2), fill=g.C["wood_d"])
        right = g.canvas(wr, h, g.C["wood"])
        dr = g.draw_on(right)
        for u in range(0, wr, 5):
            dr.fine_line((u, 0, u, h), fill=g.C["wood_d"])
        dr.rectangle((0, 0, wr, 3), fill=g.C["soot"])
        roof = roof_flat(sheet=True)
    elif kind in ("hofmauer", "friedhofsmauer"):
        stone = kind == "friedhofsmauer"  # Bruchstein mit Ziegelkrone und Moos statt Klinker
        left, d = g.wall(wl, h, ("stone",) if stone else ("brick",))
        right, dr = g.wall(wr, h, ("stone",) if stone else ("brick",))
        for face, width in ((d, wl), (dr, wr)):
            face.rectangle((0, 0, width, 3), fill=g.C["brick_d" if stone else "stone"])
            face.shade((0, 4, width, 5), 0.62)
            if stone:
                face.shade((0, h - 9, width, h), 0.75, g.C["moss"], 0.4)
        for door in b.get("doors", ()):
            if door["side"] == "ost":  # nur die Ostseite zeigt zur Kamera
                u = int((depth - door["at"]) * HW) - 11
                dr.rectangle((u - 3, 2, u + 25, h), fill=g.C["stone"])
                dr.rectangle((u, 6, u + 22, h), fill=g.C["black"])
                for k in range(u + 1, u + 22, 3):
                    dr.fine_line((k, 6, k, h), fill=g.C["iron"])
                dr.fine_line((u, 16, u + 22, 16), fill=g.C["iron"])
                dr.fine_line((u, h - 6, u + 22, h - 6), fill=g.C["iron"])
        roof = roof_flat(2)
        posts = []  # Granitpfosten an jedem Mauerende, an das ein Tor stößt: (x0, y0, x1, y1)
        for gate in GATES:
            if gate["x0"] == b["x0"] and gate["y0"] == b["y1"]:  # Tor südlich
                dr.rectangle((0, 0, 9, h), fill=g.C["stone_l"])
                posts.append((b["x0"], b["y1"] - 0.3, b["x1"], b["y1"]))
            if gate["x0"] == b["x0"] and gate["y1"] == b["y0"]:  # Tor nördlich
                dr.rectangle((wr - 10, 0, wr - 1, h), fill=g.C["stone_l"])
                posts.append((b["x0"], b["y0"], b["x1"], b["y0"] + 0.3))
            if gate["y0"] == b["y0"] and gate["x0"] == b["x1"]:  # Tor östlich
                d.rectangle((wl - 10, 0, wl - 1, h), fill=g.C["stone_l"])
                dr.rectangle((0, 0, wr - 1, h), fill=g.C["stone_l"])
                posts.append((b["x1"] - 0.3, b["y0"], b["x1"], b["y1"]))
            if gate["y0"] == b["y0"] and gate["x1"] == b["x0"]:  # Tor westlich
                d.rectangle((0, 0, 9, h), fill=g.C["stone_l"])
                posts.append((b["x0"], b["y0"], b["x0"] + 0.3, b["y1"]))
        if posts:
            plain = roof
            roof = lambda x0, y0, x1, y1, hh: plain(x0, y0, x1, y1, hh) + [R.block(*q, hh, hh + 7, mat_post, "post") for q in posts]  # noqa: E731
    elif kind == "huette":
        # Hütte des Totengräbers: Bretter, Teerpappe, ein kleines Fenster nach Süden, in dem die Kerze stehen kann
        left, right = planks(wl, h), planks(wr, h)
        d = g.draw_on(left)
        g.window(d, wl // 2 - 5, 12, 10, 12, lit=True, frame="wood_d")
        left.info.setdefault("panes", []).append((wl // 2 - 5, 12, 10, 13, "kerze:" + b.get("helper", "totengraeber")))
        d.rectangle((6, h - 26, 17, h - 1), fill=g.C["wood_d"])
        d.fine_line((11, h - 26, 11, h - 1), fill=g.C["black"])
        dr = g.draw_on(right)
        for u in (10, 20):  # Schaufel und Spaten lehnen an der Wand
            dr.fine_line((u, h - 22, u, h - 2), fill=g.C["wood"])
            dr.rectangle((u - 1, h - 6, u + 1, h - 2), fill=g.C["steel"])
        roof = roof_flat(sheet=True)
    elif kind == "hinterhaus":
        # Hinterhaus: niedrig, verputzt und fleckig, Pappdach. Hier wohnt, wer vorn die Miete nicht zahlen kann
        left, d = office_front(wl, h, lit, floors=1, door_at=wl // 2)
        g.door(d, wl // 2 - 7, h - 1, 14, 26, "wood_d", False)
        g.plaster(left, g.C["plaster"], 31, 0.35)
        right, dr = office_front(wr, h, lit, floors=1)
        g.plaster(right, g.C["plaster"], 32, 0.4)
        g.downpipe(dr, 6, 2, h - 4)
        roof = roof_flat(sheet=True)
        items = [g.chimney(0.7, 0.3, 12, False)]
    elif kind == "kneipe":
        door = (b.get("doors") or [{"at": w / 2}])[0]
        at = int(door["at"] * HW)
        left, d = g.wall(wl, h, ("brick",))
        g.plinth(d, wl, h, 6)
        d.rectangle((0, 0, wl, 3), fill=g.C["stone"])
        for u in (8, wl - 26):
            butzen(left, d, u, 16, 18, 16)
        g.door(d, at - 8, h - 1, 16, 26, "wood", True)
        if b.get("name"):
            g.name_plate(left, d, wl // 2, 5, b["name"], 5)
        right, dr = g.wall(wr, h, ("brick",))
        g.plinth(dr, wr, h, 6)
        dr.rectangle((0, 0, wr, 3), fill=g.C["stone"])
        spot = int((b["y1"] - HELPERS.get("wirtin", (0, b["y1"] - 2))[1]) * HW)
        columns = list(range(10, wr - 24, 30))
        own = min(columns, key=lambda u: abs(u + 9 - spot)) if "wirtin" in HELPERS else None  # das nächste Fenster ist das der Wirtin
        for u in columns:
            mine = u == own
            butzen(right, dr, u, 16, 18, 16, "kerze:wirtin" if mine else None)
        roof = roof_low(14, 22, tint=(1.1, 0.96, 0.9))
        items = [g.chimney(0.3, 0.5, 14, False)]
    else:  # Mietshaus der Beamten und alles Unbekannte: Klinker mit Granitbändern, schlicht
        # Restlicht für den Start im Durchgang: Im Westhaus brennt das Eckfenster zur Gasse, im Osthaus das zur Straße.
        # Fensterlicht zählt nur fürs Bild, das Schleichlicht kennt die Fenster aus rules_windows_<nr>.json.
        spot = next((n, int((x - b["x0"]) * HW)) for n, (x, y) in list(HELPERS.items()) + [(None, (0, 0))] if n is None or (abs(y - b["y1"]) < 0.3 and b["x0"] <= x <= b["x1"]))
        left, d = office_front(wl, h, lit, floors=2, door_at=wl // 2, helper=spot if spot[0] else None)
        g.door(d, wl // 2 - 8, h - 1, 16, 30, "wood", True)
        g.quoins(d, wl, 10, h - 12)
        g.downpipe(d, 8, 2, h - 4)
        right, dr = office_front(wr, h, lit, floors=2)
        g.quoins(dr, wr, 10, h - 12)
        roof = roof_low(20, 26)
        items = [g.chimney(0.72, 0.66, 14, False)]
    g.building(b["x0"], b["y0"], w, depth, h, left, right, s, things, items, roof=roof)
    things[-1].opaque = b.get("opaque", True)


def planks(w, h, base="wood", dark="wood_d"):
    tex = g.canvas(max(1, w), h, g.C[base])
    d = g.draw_on(tex)
    for u in range(0, w, 5):
        d.fine_line((u, 0, u, h), fill=g.C[dark])
    d.rectangle((0, 0, w, 2), fill=g.C[dark])
    d.shade((0, h - 5, w, h), 0.7)
    return tex


def butzen(img, d, x, y, w, h, fixed=None):
    """Kneipenfenster aus Butzenscheiben: kleine runde Gläser in Blei, dahinter warmes Licht."""
    d.rectangle((x - 2, y - 2, x + w + 1, y + h + 1), fill=g.C["wood_d"])
    d.rectangle((x, y, x + w - 1, y + h - 1), fill=g.C["gas_d"])
    for u in range(x, x + w, 3):
        d.fine_line((u, y, u, y + h - 1), fill=g.C["black"])
    for v in range(y, y + h, 3):
        d.fine_line((x, v, x + w - 1, v), fill=g.C["black"])
    img.info.setdefault("panes", []).append((x, y, w, h, fixed) if fixed else (x, y, w, h))


def gate_leaf(kind, w, h):
    """Blatt eines Tors nach seiner Art. Gitter sind durchbrochen, Blech und Holz blickdicht."""
    if kind == "gittertor":
        tex = g.canvas(w, h, (0, 0, 0, 0))
        d = g.draw_on(tex)
        for u in range(0, w, 3):
            d.fine_line((u, 2, u, h), fill=g.C["black"])
            d.fine_line((u, 0, u, 1), fill=g.C["steel"])  # Spitzen
        for v in (4, h - 6):
            d.rectangle((0, v, w - 1, v), fill=g.C["iron"])
        d.rectangle((w // 2 - 1, h // 2 - 1, w // 2 + 1, h // 2 + 2), fill=g.C["iron"])
        return tex
    if kind in ("holzpforte", "holztor", "kellerluke"):
        tex = planks(w, h, "wood" if kind != "kellerluke" else "wood_d", "wood_d" if kind != "kellerluke" else "black")
        d = g.draw_on(tex)
        for v in (5, h - 9):
            d.rectangle((0, v, w - 1, v + 1), fill=g.C["iron"])
        d.fine_line((w // 2, 0, w // 2, h), fill=g.C["black"])
        d.rectangle((w // 2 + 2, h // 2, w // 2 + 3, h // 2 + 2), fill=g.C["brass"])
        d.rectangle((0, 0, w - 1, 0), fill=g.C["paper"] if kind == "holzpforte" else g.C["wood"])
        return tex
    sheet = g.blend(g.C["steel"], 1.12)
    tex = g.canvas(w, h, sheet)
    d = g.draw_on(tex)
    d.rectangle((0, 0, w - 1, 1), fill=g.C["ash"])  # blanke Oberkante
    d.rectangle((0, h - 4, w - 1, h - 1), fill=g.C["wood_d"])
    for u in (0, w // 2, w - 1):  # Rahmen und Stoß der beiden Flügel
        d.fine_line((u, 0, u, h), fill=g.C["black"])
    for v in (5, h - 10):  # Bänder mit blanken Nietköpfen
        d.rectangle((1, v, w - 2, v + 2), fill=g.C["iron"])
        for u in range(3, w - 2, 4):
            d.rectangle((u, v + 1, u, v + 1), fill=g.C["paper"])
    d.rectangle((w // 2 - 7, 11, w // 2 + 7, 16), fill=g.C["paper"])  # Emailleschild
    d.rectangle((w // 2 + 2, h // 2 + 3, w // 2 + 4, h // 2 + 6), fill=g.C["black"])  # Schlosskasten
    return tex


def sheet_gate(gate, s, things, scene=None):
    """Tor in einer Mauerlücke: ein dünnes Blatt, niedriger als die Mauer, leicht hinter der Mauerflucht.

    Es sperrt kein Feld, das tut die Tür der Mission. Im Manifest trägt es den Namen seiner Tür (door), damit das Spiel
    das Blatt ausblenden kann, wenn die Tür offen steht.
    """
    h, kind = gate["height"], gate["kind"]
    blocks = (scene["buildings"] + scene["walls"]) if scene else []
    along_x = any(b["y0"] <= gate["y0"] < b["y1"] and (b["x1"] == gate["x0"] or b["x0"] == gate["x1"]) for b in blocks)
    opaque = kind != "gittertor"
    if along_x:  # die Mauer läuft von West nach Ost, das Blatt zeigt nach Süden
        w = gate["x1"] - gate["x0"]
        y = gate["y0"] + 0.36
        if opaque:
            g.building(gate["x0"], y, w, 0.12, h, gate_leaf(kind, int(w * HW), h), g.canvas(4, h, g.C["iron"]), s, things)
        else:
            g.flat_prop(gate["x0"], y + 0.06, gate_leaf(kind, int(w * HW), h), s, things)
    else:
        depth = gate["y1"] - gate["y0"]
        x = gate["x0"] + 0.36
        if opaque:
            g.building(x, gate["y0"], 0.12, depth, h, g.canvas(4, h, g.C["iron"]), gate_leaf(kind, int(depth * HW), h), s, things)
        else:
            g.flat_prop(x + 0.06, gate["y0"], gate_leaf(kind, int(depth * HW), h), s, things, along="y")
    things[-1].solid = False
    things[-1].kind = "gate" if opaque else "open"
    things[-1].door = gate.get("door", "")


# ---------- Kleines für Friedhof, Höfe und Exerzierplatz ----------
def post_thing(x, y, height, s, things, paint, solid=True, kind="open"):
    """Schmales Ding auf einem Feld: Sperrt es, dann wie im Grundriss die Mitte des Felds (0,3 bis 0,7)."""
    tx, ty = int(x), int(y)

    def draw():
        px, py = s.to_screen(tx + 0.5 if solid else x, ty + 0.5 if solid else y, 0)
        paint(ImageDraw.Draw(s.img), int(px), int(py), s.res)
    if solid:
        things.append(g.Thing(tx + 0.3, ty + 0.3, tx + 0.7, ty + 0.7, 0, height, draw, solid=True, opaque=False))
    else:
        things.append(g.Thing(x - 0.08, y - 0.08, x + 0.08, y + 0.08, 0, height, draw))
    things[-1].kind = kind


def paint_tree(seed):
    """Kahler Baum: Stamm, der sich dreimal gabelt. Durchbrochen, verdeckt also kaum etwas."""
    def paint(dr, px, py, k):
        rnd = __import__("random").Random(seed)

        def branch(x, y, angle, length, width):
            x1, y1 = x + math.cos(angle) * length * k, y - math.sin(angle) * length * k
            dr.line((x, y, x1, y1), fill=g.C["wood_d" if width > 1 else "black"] + (255,), width=max(1, int(width * k)))
            if length > 5:
                for turn in (-0.5, 0.45):
                    branch(x1, y1, angle + turn + rnd.uniform(-0.2, 0.2), length * rnd.uniform(0.62, 0.75), width * 0.6)
        branch(px, py, math.pi / 2 + rnd.uniform(-0.08, 0.08), 22, 3.2)
    return paint


def paint_cross(number):
    """Holzkreuz mit Blechtafel: eine Nummer statt eines Namens."""
    def paint(dr, px, py, k):
        dr.rectangle((px - k // 2, py - 16 * k, px + k - k // 2 - 1, py), fill=g.C["wood"] + (255,))
        dr.rectangle((px - 4 * k, py - 12 * k, px + 4 * k - 1, py - 11 * k), fill=g.C["wood"] + (255,))
        dr.rectangle((px - 2 * k, py - 10 * k, px + 2 * k - 1, py - 7 * k), fill=g.C["paper"] + (255,))
        for i in range(number % 3 + 1):  # die Nummer als Striche, lesbar ist sie in dieser Größe nicht
            dr.rectangle((px - 2 * k + (1 + i) * k, py - 9 * k, px - 2 * k + (1 + i) * k + max(0, k - 2), py - 8 * k), fill=g.C["black"] + (255,))
    return paint


def paint_mast(dr, px, py, k):
    dr.rectangle((px - 2 * k, py - 3 * k, px + 2 * k - 1, py), fill=g.C["stone"] + (255,))
    dr.rectangle((px - k // 2, py - 96 * k, px + k - k // 2 - 1, py - 3 * k), fill=g.C["paper"] + (255,))
    dr.rectangle((px - k, py - 98 * k, px + k - 1, py - 96 * k), fill=g.C["brass"] + (255,))
    for i in range(10 * k):  # die Fahne hängt nass am Mast
        wide = int((12 - 6 * i / (10 * k)) * k)
        for j in range(wide):
            col = (g.C["navy_d"], g.C["paper"], g.C["navy_d"])[(j * 3) // max(1, wide)]
            dr.point((px + k + j, py - 94 * k + i + j // 3), fill=tuple(int(c * (0.78 if (i // k + j // k) % 3 == 0 else 0.96)) for c in col) + (255,))


def paint_pump(dr, px, py, k):
    dr.rectangle((px - 6 * k, py - 4 * k, px + 6 * k - 1, py), fill=g.C["stone"] + (255,))  # Trog
    dr.rectangle((px - 5 * k, py - 4 * k, px + 5 * k - 1, py - 3 * k), fill=g.C["puddle"] + (255,))
    dr.rectangle((px - k, py - 22 * k, px + k - 1, py - 4 * k), fill=g.C["iron"] + (255,))
    dr.rectangle((px - 2 * k, py - 24 * k, px + 2 * k - 1, py - 22 * k), fill=g.C["black"] + (255,))
    dr.line((px + k, py - 20 * k, px + 7 * k, py - 14 * k), fill=g.C["iron"] + (255,), width=max(1, k))  # Schwengel
    dr.rectangle((px - 4 * k, py - 15 * k, px - k, py - 14 * k), fill=g.C["iron"] + (255,))  # Auslauf


def paint_coal(dr, px, py, k):
    for i, (dx, r) in enumerate(((-5, 5), (2, 7), (7, 4))):
        dr.ellipse((px + (dx - r) * k, py - r * k - 2 * k, px + (dx + r) * k, py + 2 * k), fill=(g.C["black"], g.C["soot"], g.C["black"])[i] + (255,))
    for dx, dy in ((-3, -5), (2, -8), (6, -3), (0, -2)):
        dr.rectangle((px + dx * k, py + dy * k, px + dx * k + k - 1, py + dy * k + k - 1), fill=g.C["grey_d"] + (255,))


def paint_barrels(dr, px, py, k):
    for dx in (-5, 4):
        dr.rectangle((px + (dx - 4) * k, py - 14 * k, px + (dx + 4) * k - 1, py), fill=g.C["wood"] + (255,))
        for v in (-12, -3):
            dr.rectangle((px + (dx - 4) * k, py + v * k, px + (dx + 4) * k - 1, py + v * k + k - 1), fill=g.C["iron"] + (255,))
        dr.ellipse((px + (dx - 4) * k, py - 16 * k, px + (dx + 4) * k - 1, py - 12 * k), fill=g.C["wood_d"] + (255,))


def arc_lamp(x, y, s, things, glows):
    """Bogenlampe des Exerzierplatzes: hoher Gittermast mit Ausleger, darunter die weiße Glocke. Elektrisch und grell."""
    glows.append((x, y))
    g.AMBIENT["lights"].append([x, y, 86])
    LAMP_KINDS[(x, y)] = "bogen"

    def draw():
        k = s.res
        px, py = s.to_screen(x, y, 0)
        px, py = int(px), int(py)
        dr = ImageDraw.Draw(s.img)
        dr.rectangle((px - 3 * k, py - 4 * k, px + 3 * k - 1, py), fill=g.C["stone_d"] + (255,))
        dr.rectangle((px - k, py - 96 * k, px + k - 1, py - 4 * k), fill=g.C["iron"] + (255,))
        for v in range(10, 92, 8):  # Gitterstreben
            dr.line((px - k, py - v * k, px + k - 1, py - (v + 6) * k), fill=g.C["black"] + (255,))
        dr.line((px, py - 96 * k, px + 9 * k, py - 100 * k), fill=g.C["iron"] + (255,), width=max(1, k))
        dr.rectangle((px + 8 * k, py - 100 * k, px + 9 * k, py - 93 * k), fill=g.C["iron"] + (255,))
        dr.ellipse((px + 5 * k, py - 93 * k, px + 12 * k, py - 85 * k), fill=(255, 236, 180, 255))
        dr.rectangle((px + 6 * k, py - 95 * k, px + 11 * k, py - 93 * k), fill=g.C["black"] + (255,))
    things.append(g.Thing(x - 0.05, y - 0.05, x + 0.05, y + 0.05, 0, 100, draw))
    things[-1].kind = "lamp"


LAMP_KINDS = {}  # (x, y) -> Art der Laterne, wenn sie nicht die übliche der Karte ist


def yard_things(yard, scene, s, things):
    """Was in einem Hinterhof herumsteht, ohne ein Feld zu sperren: Kohlenkasten und Abort an der Hauswand, eine Wäscheleine."""
    x0, y0, x1, y1 = yard["x0"], yard["y0"], yard["x1"], yard["y1"]
    if not any(b["y1"] == y0 and b["x0"] <= x0 and b["x1"] >= x1 for b in scene["buildings"]):
        return
    made = {}

    def box_of(name, bx, w, h, base):
        def draw():
            if name not in made:
                made[name] = (planks(int(w * HW), h, base), planks(8, h, base))
            g.box(s, bx, y0, bx + w, y0 + 0.25, 0, h, made[name][0], made[name][1], lambda a, b, w2, d2: g.C["wood_d"])
        things.append(g.Thing(bx, y0, bx + w, y0 + 0.25, 0, h, draw))
        things[-1].kind = "prop"
    box_of("kasten", x0 + 0.2, 0.7, 11, "wood_d")  # Kohlenkasten
    box_of("abort", x1 - 0.75, 0.55, 36, "wood")  # Abort mit Herz in der Tür
    line = g.canvas(int((x1 - x0 - 0.8) * HW), 34, (0, 0, 0, 0))
    d = g.draw_on(line)
    for u in (0, line.width - 2):
        d.rectangle((u, 4, u + 1, 33), fill=g.C["wood_d"])
    d.fine_line((0, 6, line.width - 1, 6), fill=g.C["black"])
    for u, w, c in ((10, 9, "paper"), (26, 6, "cream"), (line.width - 30, 11, "paper")):
        d.rectangle((u, 7, u + w, 7 + 8 + w // 3), fill=g.C[c])
        d.shade((u, 12, u + w, 7 + 8 + w // 3), 0.82)
    g.flat_prop(x0 + 0.4, y1 - 0.9, line, s, things, kind="open")


def wire(points, s, things):
    """Telefondraht: von Stütze zu Stütze, hängt leicht durch."""
    def draw():
        dr = ImageDraw.Draw(s.img)
        for (ax, ay), (bx, by) in zip(points, points[1:]):
            pts = []
            for i in range(9):
                t = i / 8
                sx, sy = s.to_screen(ax + (bx - ax) * t, ay + (by - ay) * t, 46 + 30 * (points.index((ax, ay)) + t) / len(points) - 3 * math.sin(math.pi * t))
                pts.append((sx, sy))
            dr.line(pts, fill=g.C["black"] + (255,), width=1)
    mx, my = points[len(points) // 2]
    things.append(g.Thing(mx - 0.02, my - 0.02, mx + 0.02, my + 0.02, 40, 80, draw))
    things[-1].kind = "open"


def laundry_van(x, y, s, things):
    """Der Fluchtwagen: Lieferwagen einer Wäscherei. Dunkles Führerhaus, heller Kastenaufbau mit Aufschrift und rotem Streifen."""
    cab, long, wide = 0.38, 1.4, 0.8
    made = {}

    def side_of(length, height, base):
        tex = g.canvas(int(length * HW), height, g.C[base])
        return tex, g.draw_on(tex)

    def wheel(d, wx, h):
        d.ellipse((wx - 4, h - 9, wx + 4, h - 1), fill=g.C["black"])
        d.rectangle((wx - 1, h - 6, wx + 1, h - 4), fill=g.C["grey_l"])

    def draw():
        if not made:
            body, d = side_of(long - cab, 40, "cream")
            w = body.width
            d.rectangle((0, 0, w - 1, 1), fill=g.C["paper"])
            for u in (0, w - 1):
                d.fine_line((u, 0, u, 30), fill=g.C["wood_d"])
            d.rectangle((0, 21, w - 1, 22), fill=(150, 34, 30))  # roter Streifen der Firma
            spans = g.paint_text(body, 2, 10, "WASCHEREI", 4.6, [(g.C["black"], 1.0, 0, 0, 1.0, 0.1)], gap=1.0, condense=0.72)
            a0, a1 = spans[1]
            for u in (a0 + 0.6, a1 - 1.4):  # die Punkte des Ä
                d.fine_line((u, 8, u, 8), fill=g.C["black"])
            d.rectangle((0, 30, w - 1, 39), fill=g.C["soot"])
            d.rectangle((0, 30, w - 1, 30), fill=g.C["wood_d"])
            wheel(d, 8, 40)
            back, bd = side_of(wide, 40, "cream")  # Rückwand, vom Führerhaus verdeckt bis auf den oberen Rand
            bd.rectangle((0, 0, back.width - 1, 1), fill=g.C["paper"])
            nose, nd = side_of(cab, 30, "navy")
            nd.rectangle((0, 0, nose.width - 1, 1), fill=g.C["navy_l"])
            nd.rectangle((2, 4, nose.width - 3, 13), fill=g.C["glass"])
            nd.rectangle((0, 21, nose.width - 1, 29), fill=g.C["soot"])
            wheel(nd, nose.width - 6, 30)
            front, fd = side_of(wide, 30, "navy")
            fd.rectangle((0, 0, front.width - 1, 1), fill=g.C["navy_l"])
            fd.rectangle((3, 4, front.width - 4, 13), fill=g.C["glass"])
            fd.fine_line((front.width // 2, 4, front.width // 2, 13), fill=g.C["navy_d"])
            fd.rectangle((7, 16, front.width - 8, 22), fill=g.C["black"])  # Kühlergrill
            for u in range(8, front.width - 8, 2):
                fd.fine_line((u, 16, u, 22), fill=g.C["grey"])
            fd.rectangle((0, 22, front.width - 1, 29), fill=g.C["soot"])
            fd.rectangle((2, 17, 5, 20), fill=g.C["gas_d"])
            fd.rectangle((front.width - 6, 17, front.width - 3, 20), fill=g.C["gas_d"])
            made.update(body=body, back=back, nose=nose, front=front)
        g.box(s, x, y, x + wide, y + long - cab, 0, 40, made["back"], made["body"], lambda a, b, w2, d2: g.blend(g.C["cream"], 0.9))
        g.box(s, x, y + long - cab, x + wide, y + long, 0, 30, made["front"], made["nose"], lambda a, b, w2, d2: g.C["navy_l"])
    things.append(g.Thing(x, y, x + wide, y + long, 0, 34, draw, solid=True, opaque=False))


def prison_van(d, x, base):
    """Der Gefangenenwagen in der Remise: grauer Kasten auf hohen Rädern, vergitterte Luke, Tür hinten."""
    d.rectangle((x, base - 26, x + 30, base - 6), fill=g.C["grey_d"])
    d.rectangle((x, base - 26, x + 30, base - 24), fill=g.C["grey"])
    d.rectangle((x + 4, base - 22, x + 12, base - 16), fill=g.C["black"])
    for u in range(x + 5, x + 12, 2):
        d.fine_line((u, base - 22, u, base - 16), fill=g.C["steel"])
    d.fine_line((x + 20, base - 24, x + 20, base - 7), fill=g.C["black"])
    for wx in (x + 6, x + 24):
        d.ellipse((wx - 5, base - 10, wx + 5, base), fill=g.C["black"])
        d.ellipse((wx - 1, base - 6, wx + 1, base - 4), fill=g.C["steel"])
    d.rectangle((x + 30, base - 12, x + 38, base - 11), fill=g.C["wood_d"])  # Deichsel


def wall_lamp(x, y, z, s, things):
    """Lampe im Drahtkorb an der Wand über einer Amtstür."""
    g.AMBIENT["lights"].append([x, y, z])

    def draw():
        k = s.res
        sx, sy = s.to_screen(x, y, z)
        sx, sy = int(sx), int(sy)
        dr = ImageDraw.Draw(s.img)
        dr.rectangle((sx - k, sy - 6 * k, sx + k - 1, sy - 3 * k), fill=g.C["iron"] + (255,))
        dr.rectangle((sx - 3 * k, sy - 3 * k, sx + 3 * k - 1, sy - 2 * k), fill=g.C["iron"] + (255,))
        dr.rectangle((sx - 2 * k, sy - 2 * k, sx + 2 * k - 1, sy + 3 * k), fill=g.C["gas"] + (255,))
        dr.rectangle((sx - 2 * k, sy - 2 * k, sx - 2 * k, sy + 3 * k), fill=g.C["black"] + (255,))
        dr.rectangle((sx + 2 * k - 1, sy - 2 * k, sx + 2 * k - 1, sy + 3 * k), fill=g.C["black"] + (255,))
        dr.rectangle((sx, sy - 2 * k, sx, sy + 3 * k), fill=g.C["gas_d"] + (255,))
        dr.rectangle((sx - 2 * k, sy + 3 * k, sx + 2 * k - 1, sy + 3 * k + k - 1), fill=g.C["black"] + (255,))
    things.append(g.Thing(x - 0.05, y - 0.05, x + 0.05, y + 0.05, z - 6, z + 6, draw))
    things[-1].kind = "lamp"


def fence_tex(length):
    """Niedriger Eisenzaun vor dem Vorgarten."""
    tex = g.canvas(length, 12, (0, 0, 0, 0))
    d = g.draw_on(tex)
    d.rectangle((0, 3, length - 1, 3), fill=g.C["black"])
    d.rectangle((0, 10, length - 1, 11), fill=g.C["stone_d"])
    for u in range(0, length, 3):
        d.fine_line((u, 0 if u % 12 == 0 else 2, u, 10), fill=g.C["black"])
    return tex


# ---------- Boden ----------
def ground_from(scene):
    """Bodenfunktion für g.paint_ground: Farbe je Feldkoordinate aus den Straßen und Höfen des Grundrisses."""
    solids = [(b["x0"], b["y0"], b["x1"], b["y1"]) for b in scene["buildings"] + scene["walls"]]
    gardens = [(b["x0"], b["y0"] - 1, b["x1"], b["y0"]) for b in scene["buildings"] if b["kind"] == "mietshaus"]
    graves = [(p["x"], p["y"]) for p in scene["props"] if p["kind"] == "grabkreuz"]
    open_graves = [(p["x"], p["y"]) for p in scene["props"] if p["kind"] == "offenes_grab"]
    size = scene["width"]

    def ground(fx, fy, _solids=()):
        col = lambda name: np.array(g.C[name], np.float64)  # noqa: E731
        shape = fx.shape
        n = R.noise(fx * 1.5, fy * 1.5, 11)
        # Grund überall: Granitplatten in Bahnen, dazwischen kleines Pflaster
        cx, cy = np.floor(fx * 2), np.floor(fy * 2 + 0.5 * (np.floor(fx * 2) % 2))
        edge = ((fx * 2) % 1 < 0.06) | ((fy * 2 + 0.5 * (np.floor(fx * 2) % 2)) % 1 < 0.06)
        slab = col("stone")[None, :] * (0.72 + 0.18 * R.hash01(cx, cy, 8))[..., None]
        slab = np.where(edge[..., None], col("stone_d") * 0.7, slab)
        mu, mv = fx * 9 + 0.3 * np.sin(fy * 5), fy * 9 + 0.3 * np.sin(fx * 5)
        small = (col("cobble_d") * 0.6 + col("cobble") * 0.4)[None, :] * (0.7 + 0.5 * R.hash01(np.floor(mu), np.floor(mv), 51))[..., None]
        small = np.where(((mu % 1 < 0.17) | (mv % 1 < 0.17))[..., None], col("soot") * 0.8, small)
        out = slab
        inside = (fx >= 0) & (fx < size) & (fy >= 0) & (fy < size)
        for st in scene["streets"]:
            m = (fx >= st["x0"]) & (fx < st["x1"]) & (fy >= st["y0"]) & (fy < st["y1"])
            if st["kind"] == "kopfsteinpflaster":
                along, across = (fx, fy) if st["x1"] - st["x0"] > st["y1"] - st["y0"] else (fy, fx)
                a0 = st["y0"] if along is fx else st["x0"]
                a1 = st["y1"] if along is fx else st["x1"]
                m = (across >= a0) & (across < a1)  # die Straße läuft über den Kartenrand hinaus weiter
                row = np.floor(along * g.COBBLE_ROWS)
                shift = across * g.COBBLE_STONES + 0.5 * (row % 2)
                joint = ((along * g.COBBLE_ROWS) % 1 < 0.17) | (shift % 1 < 0.1)
                sett = (col("cobble") * 0.6 + col("cobble_l") * 0.4)[None, :] * (0.88 + 0.2 * R.hash01(row, np.floor(shift), 6))[..., None]
                sett = np.where(joint[..., None], col("cobble_d"), sett)
                reach = np.minimum(across - a0, a1 - across)
                sett = sett * np.where(reach < 0.25, 0.78, 1.0)[..., None]
                wet = (reach < 0.2) & (R.noise(along * 1.1, across * 3, 14) > 0.58)
                sett = np.where(wet[..., None], sett * 0.3 + col("puddle") * 0.7, sett)
                if st.get("rails"):
                    mid = (a0 + a1) / 2
                    for rail in (mid - 0.2, mid + 0.2):
                        sett = np.where((np.abs(across - rail - 0.03) < 0.012)[..., None], col("black"), sett)
                        sett = np.where((np.abs(across - rail) < 0.018)[..., None], col("steel") * 0.45 + col("ash") * 0.55, sett)
                kerb = ((across >= a0 - 0.12) & (across < a0)) | ((across >= a1) & (across < a1 + 0.12))
                out = np.where(kerb[..., None], col("stone_l") * 0.82, out)
                out = np.where(m[..., None], sett, out)
            elif st["kind"] == "gasse":
                out = np.where(m[..., None], small, out)
            elif st["kind"] == "kies":  # Exerzierplatz: gestampfter heller Kies, vom Drill in Bahnen getreten
                grain = col("gravel")[None, :] * (0.92 + 0.5 * R.hash01(np.floor(fx * 16), np.floor(fy * 16), 71))[..., None]
                lane = (np.abs((fy * 1.0) % 2.0 - 1.0) < 0.18)
                grain = np.where(lane[..., None], grain * 0.86, grain)
                grain = np.where((R.noise(fx * 2.2, fy * 2.2, 72) > 0.7)[..., None], grain * 0.8 + col("puddle") * 0.2, grain)
                out = np.where(m[..., None], grain * 1.12, out)
            elif st["kind"] == "erde":  # Friedhof und Böschung: nasse Erde, Grasbüschel, auf dem Friedhof die Grabhügel
                earth = col("dirt")[None, :] * (0.8 + 0.6 * R.noise(fx * 7, fy * 7, 73))[..., None]
                earth = np.where((R.noise(fx * 3.1, fy * 3.1, 74) > 0.52)[..., None], earth * 0.5 + col("moss") * 0.6, earth)
                earth = np.where((R.hash01(np.floor(fx * 24), np.floor(fy * 24), 75) > 0.93)[..., None], col("moss") * 1.25, earth)
                for gx, gy in graves:
                    mound = ((fx - gx) / 0.3) ** 2 + ((fy - gy - 0.42) / 0.4) ** 2
                    earth = np.where((mound < 1)[..., None], col("dirt") * 1.5 * (1.1 - 0.35 * mound)[..., None], earth)
                for gx, gy in open_graves:
                    pit = (np.abs(fx - gx) < 0.28) & (np.abs(fy - gy) < 0.42)
                    heap = ((fx - gx - 0.55) / 0.22) ** 2 + ((fy - gy) / 0.4) ** 2 < 1
                    earth = np.where(heap[..., None], col("dirt") * 1.7, earth)
                    earth = np.where(pit[..., None], col("black") * 0.5, earth)
                out = np.where(m[..., None], earth, out)
            elif st["kind"] == "hof" and st["id"] != "arresthof":  # Hinterhof: Lehm, ein Weg aus Platten zur Hoftür
                clay = (col("dirt") * 0.5 + col("plaster") * 0.5)[None, :] * (0.55 + 0.35 * R.noise(fx * 5, fy * 5, 76))[..., None]
                clay = np.where((R.noise(fx * 2.4, fy * 2.4, 77) > 0.66)[..., None], clay * 0.55 + col("puddle") * 0.45, clay)
                way = (np.abs(fy - (st["y0"] + st["y1"]) / 2 - 0.5) < 0.32) & (((fx * 1.6) % 1) > 0.12)
                slab2 = col("stone")[None, :] * (0.7 + 0.2 * R.hash01(np.floor(fx * 1.6), 1, 78))[..., None]
                clay = np.where(way[..., None], slab2, clay)
                out = np.where(m[..., None], clay, out)
            elif st["kind"] == "hof":
                yard = col("stone_d")[None, :] * (0.7 + 0.25 * R.hash01(np.floor(fx * 3), np.floor(fy * 3), 9))[..., None]
                yard = np.where((((fx * 3) % 1 < 0.07) | ((fy * 3) % 1 < 0.07))[..., None], col("soot") * 0.7, yard)
                out = np.where(m[..., None], yard, out)
        for x0, y0, x1, y1 in gardens:  # Vorgarten: Erde und Gras, in der Mitte der Plattenweg zur Tür
            m = (fx >= x0) & (fx < x1) & (fy >= y0) & (fy < y1)
            path = np.abs(fx - (x0 + x1) / 2) < 0.3
            grass = col("moss")[None, :] * (0.6 + 0.5 * R.noise(fx * 9, fy * 9, 61))[..., None]
            grass = np.where((R.noise(fx * 4, fy * 4, 62) > 0.62)[..., None], col("dirt") * 0.9, grass)
            out = np.where((m & ~path)[..., None], grass, out)
        near = np.full(shape, 9.0)
        for x0, y0, x1, y1 in solids:
            gap = np.hypot(np.maximum.reduce([x0 - fx, np.zeros(shape), fx - x1]), np.maximum.reduce([y0 - fy, np.zeros(shape), fy - y1]))
            near = np.minimum(near, gap)
        drip = near < 0.08 + 0.03 * R.noise(fx * 7, fy * 7, 63)
        soil = col("dirt")[None, :] * (0.7 + 0.5 * R.hash01(np.floor(fx * 40), np.floor(fy * 40), 64))[..., None]
        soil = np.where((R.noise(fx * 6, fy * 6, 65) > 0.6)[..., None], soil * 0.6 + col("moss") * 0.4, soil)
        out = np.where((drip & inside)[..., None], soil, out)
        out = out * (0.6 + 0.42 * n)[..., None]
        return np.clip(out, 0, 255).astype(np.uint8)
    return ground


# ---------- Szene ----------
def build(scene, theme):
    g.C.update(theme["palette"])
    for i, name in enumerate(("RNG", "WEAR", "DETAIL", "LITTER", "LIT_ROOMS")):
        getattr(g, name).seed(theme["seed"] * 10 + i)
    g.DUAL = True
    g.MANHOLES = ()
    g.ground_np = ground_from(scene)
    for points in g.AMBIENT.values():
        points.clear()
    s = g.Scene(scene["width"])
    things, glows = [], []
    GATES[:] = scene.get("gates", [])
    HELPERS.clear()
    HELPERS.update({m["helper"]: (m["x"], m["y"]) for m in scene.get("marks", ()) if m["kind"] == "helferfenster"})
    LAMP_KINDS.clear()
    for b in scene["buildings"] + scene["walls"]:
        nordwall_building(b, s, things)
    for gate in scene.get("gates", ()):
        sheet_gate(gate, s, things, scene)
    seen = set()
    for lamp in scene["lamps"]:
        if (lamp["x"], lamp["y"]) in seen:  # doppelt eingetragene Lampen zählen einmal
            continue
        seen.add((lamp["x"], lamp["y"]))
        if lamp.get("kind") == "wandlampe":
            wall_lamp(lamp["x"], lamp["y"], {"wachstube": 30, "kneipe": 36, "remise": 40}.get(lamp["group"], 44), s, things)
        elif lamp.get("kind") == "bogenlampe":
            arc_lamp(lamp["x"], lamp["y"], s, things, glows)
        else:
            g.lamp(lamp["x"], lamp["y"], s, things, glows)
    for i, prop in enumerate(scene["props"]):
        x, y, kind = int(prop["x"]), int(prop["y"]), prop["kind"]
        if kind == "fluchtwagen":
            laundry_van(prop["x"] - 0.4, prop["y"] - 0.7, s, things)
            things[-1].solid, things[-1].opaque = prop["solid"], prop["opaque"]
        elif kind == "kiste" and "kohlenhaufen" in prop["id"]:
            post_thing(prop["x"], prop["y"], 12, s, things, paint_coal, kind="prop")
        elif kind == "kiste" and "faesser" in prop["id"]:
            post_thing(prop["x"], prop["y"], 16, s, things, paint_barrels, kind="prop")
        elif kind == "kiste":
            g.crate(x + 0.3, y + 0.3, s, things)
            things[-1].solid, things[-1].opaque = prop["solid"], prop["opaque"]
        elif kind == "baum":
            post_thing(prop["x"], prop["y"], 60, s, things, paint_tree(i))
        elif kind == "grabkreuz":
            post_thing(prop["x"], prop["y"] - 0.1, 16, s, things, paint_cross(i), solid=False)
        elif kind == "mast":
            post_thing(prop["x"], prop["y"], 98, s, things, paint_mast)
        elif kind == "brunnen":
            post_thing(prop["x"], prop["y"], 24, s, things, paint_pump)
    for st in scene["streets"]:
        if st["kind"] == "hof" and st["id"] in ("westhof", "osthof"):
            yard_things(st, scene, s, things)
    for mark in scene.get("marks", ()):
        if mark["kind"] == "draht":
            wire([tuple(q) for q in mark["points"]], s, things)
    for b in scene["buildings"]:
        if b["kind"] == "mietshaus":  # Zaun vor dem Vorgarten, mit Lücke am Weg
            half = int((b["x1"] - b["x0"]) * HW / 2) - 12
            g.flat_prop(b["x0"], b["y0"] - 0.95, fence_tex(half), s, things, kind="open")
            g.flat_prop((b["x0"] + b["x1"]) / 2 + 12 / HW, b["y0"] - 0.95, fence_tex(half), s, things, kind="open")
    solids = [(b["x0"], b["y0"], b["x1"], b["y1"]) for b in scene["buildings"] + scene["walls"]]
    s.dirt = g.draw_ground(s, solids, glows)
    s.solids, s.glows = solids, glows
    return s, g.sort_things(things)


def export(scene, theme, s, things, assets):
    (assets / "things").mkdir(parents=True, exist_ok=True)
    for old in (assets / "things").glob("*.png"):
        old.unlink()
    size = scene["width"]
    ys, xs = np.mgrid[0:s.h, 0:s.w]
    a, b = ((xs + 0.5) - s.ox) / g.HW, ((ys + 0.5) - s.oy) / g.HH
    fx, fy = (a + b) / 2, (b - a) / 2
    inside = (fx >= 0) & (fx < size) & (fy >= 0) & (fy < size)
    s.img.putalpha(Image.fromarray((inside * 255).astype(np.uint8), "L"))
    s.img.save(assets / "ground.png")
    bw, bh = g.backdrop_size(size)
    backdrop = {"file": "backdrop.png", "px": s.ox - bw // 2, "py": s.oy + size * g.HH - bh // 2}
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
        manifest.append({"file": name, "px": bbox[0], "py": bbox[1], "x0": t.x0, "y0": t.y0, "x1": t.x1, "y1": t.y1, "z0": t.z0, "z1": t.z1, "kind": t.kind, **({"door": t.door} if getattr(t, "door", "") else {}),
                         "panes": t.panes, **({"roof": [p.to_json() for p in t.prisms]} if t.prisms else {})})
        if t.solid:
            solids.append({"x0": t.x0, "y0": t.y0, "x1": t.x1, "y1": t.y1, "opaque": t.opaque})
    (assets / "manifest.json").write_text(json.dumps({"map": scene["id"], "size": size, "ox": s.ox, "oy": s.oy, "things": manifest, "backdrop": backdrop,
                                                      "perches": [list(p) for p in theme.get("perches", ())],
                                                      "lightKinds": [LAMP_KINDS.get((x, y), "") for x, y, _ in g.AMBIENT["lights"]],
                                                      "hd": {"res": g.HD, "ground": "hd/ground.png", "things": [n for t, n, _ in exported if t.hd]},
                                                      **g.AMBIENT}), encoding="utf-8")
    g.export_backdrop(assets, theme.get("backdrop_keep"), size)
    g.DUAL = True
    g.export_hd(assets, s, exported)
    return solids


def same_solids(mine, path):
    """Vergleicht die gesperrten Flächen der Bilder mit der Regelkarte der Level-Sitzung."""
    key = lambda r: tuple(round(r[k], 2) for k in ("x0", "y0", "x1", "y1")) + (bool(r["opaque"]),)  # noqa: E731
    theirs = json.loads(path.read_text(encoding="utf-8"))["solids"]
    a, b = sorted(map(key, mine)), sorted(map(key, theirs))
    if a != b:
        print("ACHTUNG: Bilder und", path.name, "weichen ab:", sorted(set(a) ^ set(b)))
    return a == b


def main():
    nr = next((a for a in sys.argv[1:] if not a.startswith("--")), "02")
    scene = json.loads((ROOT / "data" / f"scene_{nr}.json").read_text(encoding="utf-8"))
    theme = THEMES[scene["id"]]
    s, things = build(scene, theme)
    preview = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--preview=")), None)
    if preview:
        res = 2
        s.use(res)
        g.draw_ground_fine(s, s.solids, s.dirt)
        for t in things:
            t.draw()
        s.img.save(preview)
        print("Vorschau:", preview)
        return
    # Das Kesselviertel liegt direkt in game/assets, jede weitere Karte in game/assets/maps/<id>/. Was dort fehlt, nimmt der
    # Lader aus dem Kesselviertel: Deshalb entsteht der Ordner jedes Mal vollständig neu.
    assets = ROOT / "game" / "assets" / "maps" / scene["id"]
    if assets.exists():
        shutil.rmtree(assets)
    solids = export(scene, theme, s, things, assets)
    # Die Regelkarte schreibt allein das Werkzeug der Level-Sitzung, hier ohne sein vorläufiges Licht. Danach wird verglichen.
    map_file = ROOT / "data" / f"map_{nr}.json"
    subprocess.run([sys.executable, str(ROOT / "tools" / "level" / "scene_to_map.py"), nr, "--no-light"], check=True, stdout=subprocess.DEVNULL)
    print("Regelkarte passt zu den Bildern" if same_solids(solids, map_file) else "Regelkarte bitte prüfen")
    print(f"{len(things)} Objekte nach {assets}")
    if "--no-bake" not in sys.argv:
        subprocess.run([sys.executable, str(Path(__file__).with_name("bake_light.py")), f"--map={scene['id']}", f"--nr={nr}"], check=True)


if __name__ == "__main__":
    main()
