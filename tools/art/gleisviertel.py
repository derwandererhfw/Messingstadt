"""Bausteine des Gleisviertels (Einsatz 3, Güterbahnhof Ost): weit, flach, Teerholz, Schotter, Ruß.

Gebaut wird aus data/scene_03.json über build_map.py, das dieses Modul für die Karte "gleisviertel" aufruft.
Die Höhen sind Vorgaben der Regeln: Güterwagen höchstens 52, Baracken und Bansen höchstens 38.
"""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

import generate_concept as g
import roofs as R

HW = g.HW

PALETTE = {
    # rußiger gelber Ziegel statt Klinker, Teerholz, kaum Stein
    "brick_d": (74, 64, 44), "brick": (112, 98, 64), "brick_l": (136, 120, 80), "mortar": (66, 60, 50),
    "stone_d": (70, 68, 66), "stone": (96, 94, 90), "stone_l": (122, 118, 110),
    "cobble_d": (48, 48, 52), "cobble": (70, 70, 74), "cobble_l": (90, 90, 92),
    "wood_d": (40, 34, 30), "wood": (60, 50, 40), "iron": (50, 52, 56), "soot": (34, 34, 38),
    "wagon_d": (66, 36, 30), "wagon": (96, 52, 40), "wagon_l": (120, 70, 52),
    "board_d": (50, 54, 48), "board": (70, 76, 66), "tarp": (84, 82, 66), "tarp_d": (58, 58, 48),
    "ballast_d": (52, 48, 46), "ballast": (84, 78, 72), "cinder": (62, 60, 64), "coal": (26, 26, 30), "mud": (74, 64, 54),
    "signal_r": (196, 44, 32), "signal_g": (60, 150, 96),
}
THEME = {
    "palette": PALETTE,
    "seed": 1903,
    "backdrop_keep": lambda x, y, w, d: y + d > 0.5,  # nördlich des Damms liegt freies Feld
    "perches": [(6.0, 5.95, 70), (11.4, 5.95, 70), (20.0, 5.95, 90), (12.0, 21.95, 37), (22.0, 21.95, 37)],
}


SITE = Path(__file__).resolve().parents[2] / "game" / "assets" / "sites" / "gleisviertel.json"
DAM_SIGNALS = [tuple(q[:3]) for q in json.loads(SITE.read_text(encoding="utf-8")).get("signals", ()) if q[3] == "strecke"] if SITE.exists() else []


def col(name):
    return np.array(g.C[name], np.float64)


# ---------- Dachmaterialien ----------

def planks_np(f, base="wood", dark="wood_d"):
    u, _v = f.wall_uv()
    stripe = (np.floor(u / f.res) % 5) < 1
    return np.where(stripe[:, None], g._c(dark)[None, :], g._c(base)[None, :] * (0.85 + 0.25 * R.hash01(np.floor(u / f.res / 5), 3, 7))[:, None])


def mat_shed(f):
    """Teerpappe in Bahnen, die Giebeldreiecke aus Brettern."""
    if f.kind != "plane":
        return planks_np(f)
    seam = (f.fx * 2.0) % 1 < 0.05
    felt = g._c("soot")[None, :] * (0.9 + 0.5 * R.noise(f.fx * 5, f.fy * 9, 31))[:, None]
    felt = np.where((R.noise(f.fx * 1.7, f.fy * 2.3, 32) > 0.66)[:, None], felt * 0.7 + g._c("puddle")[None, :] * 0.3, felt)
    return np.where(seam[:, None], g._c("black")[None, :], felt)


def mat_coal(f):
    """Kohle: schwarze Brocken, hier und da eine blanke Bruchfläche."""
    lump = R.hash01(np.floor(f.fx * 13), np.floor(f.fy * 13), 41)
    c = g._c("coal")[None, :] * (0.55 + 1.0 * lump)[:, None]
    return np.where((lump > 0.94)[:, None], g._c("grey_l")[None, :] * 0.8, c)


def mat_tank(f):
    """Genieteter Eisenbehälter: Blechschüsse mit Nietreihen, Rostfahnen."""
    if f.kind == "plane":
        return g._c("iron")[None, :] * (0.7 + 0.4 * R.noise(f.fx * 6, f.fy * 6, 44))[:, None]
    u, v = f.wall_uv()
    u, v = u / f.res, v / f.res
    c = g._c("iron")[None, :] * (0.62 + 0.3 * R.noise(u / 11, v / 30, 45))[:, None]
    c = np.where((R.noise(u / 4, v / 70, 46) > 0.64)[:, None], c * 0.7 + g._c("rust_d")[None, :] * 0.3, c)  # Rostfahnen
    hoop = np.floor(v) % 14
    c = np.where((hoop == 0)[:, None], g._c("black")[None, :], c)
    c = np.where((hoop == 1)[:, None], g._c("iron")[None, :] * 1.1, c)
    rivet = (hoop == 1) & (np.floor(u) % 5 == 2)
    return np.where(rivet[:, None], g._c("steel")[None, :] * 0.9, c)


def mat_track(f):
    """Dammkrone: Schotter mit dem Gleis der Hauptstrecke."""
    if f.kind != "plane":
        return g.brick_np(*f.wall_uv(), f.res)
    return track_np(f.fx, f.fy, [f.prism.y0 + 0.5 + 0.45 * (f.prism.y1 - f.prism.y0 - 1)])


def mat_wagon_roof(f):
    if f.kind != "plane":
        return g._c("wagon_d")[None, :] * np.ones((len(f.fx), 1))
    rib = (f.fx * 4.0) % 1 < 0.08
    c = g._c("soot")[None, :] * (1.0 + 0.4 * R.noise(f.fx * 6, f.fy * 10, 52))[:, None]
    return np.where(rib[:, None], g._c("black")[None, :], c)


def mat_tarp(f):
    """Plane über der Ladung: Falten, Flickstellen."""
    fold = R.noise(f.fx * 7, f.fy * 3, 55)
    c = g._c("tarp")[None, :] * (0.7 + 0.5 * fold)[:, None]
    return np.where((np.abs((f.fx * 2.0) % 1 - 0.5) < 0.03)[:, None], g._c("tarp_d")[None, :], c)


def mat_wood_top(f):
    stripe = (f.fy * 7.0) % 1 < 0.12
    c = g._c("wood")[None, :] * (0.8 + 0.4 * R.hash01(np.floor(f.fy * 7), np.floor(f.fx * 2), 58))[:, None]
    return np.where(stripe[:, None], g._c("black")[None, :], c)


def mat_sheet_roof(f):
    return g.mat_sheet(f, (64, 66, 70), 0.9) if f.kind == "plane" else planks_np(f)


# ---------- Boden ----------

def track_np(fx, fy, centres, switch=None):
    """Schotterbett mit Gleisen: Schwellen quer, zwei Schienen. centres: Lage der Gleismitten in y."""
    stone = R.hash01(np.floor(fx * 21), np.floor(fy * 21), 61)
    out = col("ballast")[None, :] * (0.55 + 0.75 * stone)[:, None]
    out = np.where((R.noise(fx * 1.3, fy * 2.6, 62) > 0.6)[:, None], out * 0.62, out)  # Öl und Ruß
    lines = [np.full(fx.shape, c) for c in centres]
    if switch:
        x0, x1, ya, yb = switch
        t = np.clip((fx - x0) / (x1 - x0), 0, 1)
        lines.append(np.where((fx >= x0) & (fx <= x1), ya + (yb - ya) * (3 * t * t - 2 * t ** 3), -99.0))
    for c in lines:
        off = fy - c
        sleeper = (np.abs(off) < 0.36) & ((fx * 3.2) % 1 < 0.34)
        out = np.where(sleeper[:, None], col("wood_d")[None, :] * (0.8 + 0.5 * R.hash01(np.floor(fx * 3.2), np.round(c * 10), 63))[:, None], out)
        between = np.abs(off) < 0.2
        out = np.where((between & ~sleeper)[:, None], out * 0.72, out)
        for rail in (-0.21, 0.21):
            out = np.where((np.abs(off - rail - 0.035) < 0.02)[:, None], col("black")[None, :], out)
            out = np.where((np.abs(off - rail) < 0.022)[:, None], col("steel")[None, :] * 0.8, out)
    return out


def ground_from(scene):
    size = scene["width"]
    centres = [t + 0.5 for t in scene.get("tracks", ())]
    weiche = next((p for p in scene["props"] if p["kind"] == "weichenbock"), None)
    switch = (weiche["x"] - 4.5, weiche["x"], weiche["y"] + 1.0, weiche["y"]) if weiche else None

    def ground(fx, fy, _solids=()):
        n = R.noise(fx * 1.5, fy * 1.5, 11)
        speck = R.hash01(np.floor(fx * 26), np.floor(fy * 26), 12)
        cinder = col("cinder")[None, :] * (0.7 + 0.7 * speck)[:, None]
        cinder = np.where((R.noise(fx * 2.1, fy * 2.1, 13) > 0.64)[:, None], cinder * 0.5 + col("puddle")[None, :] * 0.5, cinder)
        out = cinder
        for st in scene["streets"]:
            m = (fx >= st["x0"]) & (fx < st["x1"]) & (fy >= st["y0"]) & (fy < st["y1"])
            kind = st["kind"]
            if kind == "schotter":
                m = (fy >= st["y0"]) & (fy < st["y1"])  # die Gleise laufen über den Kartenrand hinaus
                out = np.where(m[:, None], track_np(fx, fy, centres, switch), out)
            elif kind == "bohlen":  # Überweg: Bohlen zwischen und neben den Schienen, die Schienen bleiben frei
                along_y = st["y1"] - st["y0"] > st["x1"] - st["x0"]
                a = fx if along_y else fy
                plank = col("wood")[None, :] * (0.75 + 0.4 * R.hash01(np.floor(a * 4), np.floor((fy if along_y else fx) * 0.8), 66))[:, None]
                plank = np.where(((a * 4) % 1 < 0.1)[:, None], col("black")[None, :], plank)
                rail = np.zeros(fx.shape, bool)
                for c in centres:
                    rail |= (np.abs(np.abs(fy - c) - 0.21) < 0.05)
                out = np.where((m & ~rail)[:, None], plank, out)
            elif kind == "kopfsteinpflaster":
                m = (fy >= st["y0"]) & (fy < st["y1"])
                row = np.floor(fx * g.COBBLE_ROWS)
                shift = fy * g.COBBLE_STONES + 0.5 * (row % 2)
                joint = ((fx * g.COBBLE_ROWS) % 1 < 0.17) | (shift % 1 < 0.1)
                sett = (col("cobble") * 0.6 + col("cobble_l") * 0.4)[None, :] * (0.8 + 0.3 * R.hash01(row, np.floor(shift), 6))[:, None]
                sett = np.where(joint[:, None], col("cobble_d")[None, :], sett)
                sett = np.where((R.noise(fx * 0.9, fy * 2.6, 14) > 0.6)[:, None], sett * 0.4 + col("puddle")[None, :] * 0.6, sett)
                rut = (np.abs(fy - st["y0"] - 0.7) < 0.06) | (np.abs(fy - st["y0"] - 1.3) < 0.06)  # Karrenspuren
                sett = np.where(rut[:, None], sett * 0.7, sett)
                out = np.where(m[:, None], sett, out)
            elif kind == "holz":  # Laderampe: Bohlen quer, vorn ein Kantbalken mit Eisenband
                plank = col("wood")[None, :] * (0.8 + 0.5 * R.hash01(np.floor(fx * 5), 2, 67))[:, None]
                plank = np.where(((fx * 5) % 1 < 0.09)[:, None], col("black")[None, :], plank)
                plank = np.where((fy > st["y1"] - 0.14)[:, None], col("iron")[None, :] * 0.9, plank)
                out = np.where(m[:, None], plank, out)
            elif kind == "kohlenstaub":
                dust = col("coal")[None, :] * (0.7 + 0.9 * speck)[:, None]
                dust = np.where((speck > 0.95)[:, None], col("grey")[None, :] * 0.7, dust)
                out = np.where(m[:, None], dust, out)
            elif kind in ("erde", "hof"):  # festgetretener Lehm mit Schlacke, Pfützen, Karrenspuren. Kein Halm
                mud = col("mud")[None, :] * (0.7 + 0.5 * R.noise(fx * 6, fy * 6, 71))[:, None]
                mud = np.where((speck > 0.86)[:, None], col("cinder")[None, :], mud)
                mud = np.where((R.noise(fx * 1.9, fy * 1.9, 72) > 0.66)[:, None], mud * 0.45 + col("puddle")[None, :] * 0.55, mud)
                out = np.where(m[:, None], mud, out)
        out = out * (0.6 + 0.42 * n)[:, None]
        return np.clip(out, 0, 255).astype(np.uint8)
    return ground


# ---------- Fassaden ----------

def boards(w, h, base="wood", dark="wood_d", step=5):
    """Waagerechte Stülpschalung, geteert und fleckig."""
    tex = g.canvas(max(1, w), h, g.C[base])
    d = g.draw_on(tex)
    for v in range(0, h, step):
        d.rectangle((0, v, w, v), fill=g.C[dark])
    for u in range(14, w, 38):
        d.shade((u, 0, u + 9 + (u * 7) % 13, h), 0.78)
    d.shade((0, h - 5, w, h), 0.66)
    return tex, d


def small_window(tex, d, u, v, w=9, h=9, lit=False, fixed=None):
    d.rectangle((u - 1, v - 1, u + w, v + h), fill=g.C["wood_d"])
    d.rectangle((u, v, u + w - 1, v + h - 1), fill=g.C["gas_d"] if lit else g.C["glass_d"])
    d.fine_line((u + w // 2, v, u + w // 2, v + h - 1), fill=g.C["wood_d"])
    d.fine_line((u, v + h // 2, u + w - 1, v + h // 2), fill=g.C["wood_d"])
    if lit:
        tex.info.setdefault("panes", []).append((u, v, w, h, fixed or "funzel"))  # im Bahnhof brennen nur Funzeln


def iron_door(d, u, base, w=20, h=38):
    d.rectangle((u - 2, base - h - 2, u + w + 1, base), fill=g.C["stone_d"])
    d.rectangle((u, base - h, u + w - 1, base), fill=g.C["iron"])
    for v in (base - h + 6, base - 9):
        d.rectangle((u, v, u + w - 1, v + 1), fill=g.C["black"])
        for k in range(u + 2, u + w - 1, 4):
            d.point((k, v), fill=g.C["steel"])
    d.rectangle((u + w - 5, base - h // 2, u + w - 3, base - h // 2 + 3), fill=g.C["brass_d"])


def lettering(tex, d, cx, y, words, height=5):
    """Helle Tafel mit schwarzer Schrift. Ü wird aus U und zwei Punkten gesetzt."""
    plain = words.replace("Ü", "U")
    width = int(len(plain) * 4 * height / 6 * 0.8 + (len(plain) - 1) * max(1.5, height / 6 * 1.3)) + 10
    x0 = int(cx - width / 2)
    d.rectangle((x0, y - 3, x0 + width, y + height + 3), fill=g.C["black"])
    d.rectangle((x0 + 1, y - 2, x0 + width - 1, y + height + 2), fill=g.blend(g.C["paper"], 0.72))
    spans = g.paint_text(tex, x0 + 5, y, plain, height, [(g.C["black"], 1.0, 0, 0, 1.0, 0.15)])
    for i, ch in enumerate(words):
        if ch == "Ü" and i < len(spans):
            a0, a1 = spans[i]
            for u in (a0 + 0.7, a1 - 1.5):
                d.fine_line((u, y - 2, u, y - 2), fill=g.C["black"])


WAGON_KINDS = ("box", "coal", "box", "tarp")
WAGON_WALL = 42


def wagon_side(d, u0, u1, h, kind):
    """Ein Güterwagen von der Seite auf der Fassade: Kasten, Rahmen, zwei Achsen, Puffer."""
    body = h - 13
    d.rectangle((u0, body, u1 - 1, h), fill=g.blend(g.C["soot"], 0.55))  # Dunkel unter dem Wagen
    d.rectangle((u0 + 1, body, u1 - 2, body + 3), fill=g.C["iron"])  # Langträger
    d.rectangle((u0 + 1, body, u1 - 2, body), fill=g.C["steel"])
    for wx in (u0 + 9, u1 - 10):
        d.ellipse((wx - 5, h - 11, wx + 5, h - 1), fill=g.C["black"])
        d.ellipse((wx - 2, h - 8, wx + 2, h - 4), fill=g.C["iron"])
        d.rectangle((wx - 6, body + 3, wx + 6, body + 5), fill=g.C["black"])  # Federpaket
    for u in (u0, u1 - 2):
        d.rectangle((u, body + 1, u + 1, body + 4), fill=g.C["steel"])  # Puffer
    a, b = u0 + 2, u1 - 3
    if kind == "box":
        d.rectangle((a, 0, b, body - 1), fill=g.C["wagon"])
        for u in range(a, b, 4):
            d.fine_line((u, 1, u, body - 1), fill=g.C["wagon_d"])
        d.rectangle((a, 0, b, 1), fill=g.C["soot"])
        mid = (a + b) // 2
        d.rectangle((mid - 10, 3, mid + 10, body - 2), fill=g.C["wagon_l"])  # Schiebetür
        for u in range(mid - 10, mid + 11, 4):
            d.fine_line((u, 3, u, body - 2), fill=g.C["wagon_d"])
        d.line((mid - 10, 3, mid + 10, body - 2), fill=g.C["iron"])
        d.rectangle((mid - 12, 2, mid + 12, 3), fill=g.C["iron"])
        d.rectangle((mid - 12, body - 2, mid + 12, body - 1), fill=g.C["iron"])
        for u in (a, b - 1):
            d.rectangle((u, 0, u + 1, body - 1), fill=g.C["iron"])
        d.rectangle((a + 5, 8, a + 13, 9), fill=g.C["paper"])  # Anschrift in Kreide
        d.rectangle((a + 5, 12, a + 10, 12), fill=g.C["paper"])
        d.shade((a, body - 7, b, body - 1), 0.7)
    elif kind == "coal":
        d.rectangle((a, 0, b, 9), fill=g.C["coal"])  # Kohle bis über die Bordwand
        for u in range(a, b, 3):
            d.rectangle((u, (u * 5) % 4, u + 1, 9), fill=g.blend(g.C["coal"], 0.7 + ((u * 3) % 5) * 0.12))
        d.rectangle((a, 8, b, body - 1), fill=g.C["board"])
        for v in range(8, body, 6):
            d.rectangle((a, v, b, v), fill=g.C["board_d"])
        for u in range(a, b + 1, 11):
            d.rectangle((u, 7, u + 1, body - 1), fill=g.C["iron"])
        d.shade((a, 9, b, 12), 0.6, g.C["coal"], 0.5)
        d.shade((a, body - 6, b, body - 1), 0.7)
    else:  # Rungenwagen mit Plane
        d.rectangle((a, 0, b, body - 9), fill=g.C["tarp"])
        for u in range(a + 3, b, 9):
            d.line((u, 0, u + 4, body - 9), fill=g.C["tarp_d"])
        d.shade((a, 0, b, 3), 0.75)
        d.line((a, body - 13, b, body - 13), fill=g.C["paper"])  # Seil
        d.rectangle((a, body - 9, b, body - 1), fill=g.C["board_d"])
        for u in range(a, b + 1, 9):
            d.rectangle((u, body - 12, u + 1, body - 1), fill=g.C["iron"])


def wagon_end(w, h, kind):
    tex = g.canvas(w, h, g.blend(g.C["soot"], 0.55))
    d = g.draw_on(tex)
    body = h - 13
    base = {"box": "wagon", "coal": "board", "tarp": "tarp"}[kind]
    d.rectangle((2, 0 if kind != "coal" else 8, w - 3, body - 1), fill=g.blend(g.C[base], 0.8))
    if kind == "coal":
        d.rectangle((2, 2, w - 3, 8), fill=g.C["coal"])
    for u in (2, w // 2, w - 4):
        d.rectangle((u, 0 if kind != "coal" else 8, u + 1, body - 1), fill=g.C["iron"])
    d.rectangle((1, body, w - 2, body + 3), fill=g.C["iron"])
    for u in (6, w - 8):  # Pufferteller
        d.ellipse((u - 3, body - 1, u + 3, body + 5), fill=g.C["steel"])
        d.ellipse((u - 1, body + 1, u + 1, body + 3), fill=g.C["black"])
    d.rectangle((w // 2 - 1, body + 3, w // 2 + 1, body + 8), fill=g.C["black"])  # Zughaken
    return tex


def wagons(b, s, things):
    """Eine Wagengruppe als ein Körper: je rund zwei Felder ein Wagen, im Wechsel gedeckt, mit Kohle, unter Plane."""
    length = b["x1"] - b["x0"]
    count = max(1, round(length / 2))
    cuts = [b["x0"] + length * i / count for i in range(count + 1)]
    kinds = [WAGON_KINDS[(b["x0"] * 3 + b["y0"] + i) % len(WAGON_KINDS)] for i in range(count)]
    h = WAGON_WALL
    left = g.canvas(int(length * HW), h, g.blend(g.C["soot"], 0.55))
    d = g.draw_on(left)
    for i, kind in enumerate(kinds):
        wagon_side(d, int((cuts[i] - b["x0"]) * HW) + (1 if i else 0), int((cuts[i + 1] - b["x0"]) * HW) - (1 if i < count - 1 else 0), h, kind)
    right = wagon_end(int((b["y1"] - b["y0"]) * HW), h, kinds[-1])

    def roof(x0, y0, x1, y1, hh):
        prisms = []
        for i, kind in enumerate(kinds):
            a, c = cuts[i], cuts[i + 1]
            if kind == "box":
                prisms.append(R.gable(a, y0, c, y1, hh, 8, ridge="x", material=mat_wagon_roof))
            elif kind == "coal":
                prisms.append(R.hip(a, y0, c, y1, hh - 2, 7, 9, material=mat_coal))
            else:
                prisms.append(R.gable(a, y0, c, y1, hh, 7, ridge="x", material=mat_tarp))
        return prisms
    g.building(b["x0"], b["y0"], length, b["y1"] - b["y0"], h, left, right, s, things, roof=roof)


def building(b, s, things, B):
    """Ein Gebäude oder Hindernis des Gleisviertels. False, wenn die Art hier unbekannt ist."""
    w, depth, h = b["x1"] - b["x0"], b["y1"] - b["y0"], b["height"]
    wl, wr = int(w * HW), int(depth * HW)
    lit = b.get("lit", 0.0)
    kind = b["kind"]
    items = []
    if kind == "gueterwagen":
        wagons(b, s, things)
        things[-1].opaque = b.get("opaque", True)
        return True
    if kind == "bahndamm":
        left, d = g.wall(wl, h, ("brick",))
        for u in range(20, wl, 96):  # Strebepfeiler, dazwischen vermauerte Bögen und Entwässerungsrohre
            d.ellipse((u + 22, 14, u + 70, 54), fill=g.C["brick_d"])
            d.rectangle((u + 22, 34, u + 70, h - 6), fill=g.C["brick_d"])
            d.shade((u + 22, 14, u + 70, 18), 0.7)
            d.rectangle((u, 4, u + 9, h), fill=g.C["brick_l"])
            d.shade((u + 10, 6, u + 12, h), 0.6)
            d.rectangle((u + 44, 40, u + 47, 44), fill=g.C["black"])
            d.shade((u + 44, 45, u + 47, h), 0.6, g.C["rust"], 0.4)
        d.rectangle((0, 0, wl, 3), fill=g.C["stone"])
        d.shade((0, 4, wl, 6), 0.6)
        d.shade((0, h - 14, wl, h), 0.6, g.C["soot"], 0.5)
        right, dr = g.wall(wr, h, ("brick",))
        dr.rectangle((0, 0, wr, 3), fill=g.C["stone"])
        roof = lambda x0, y0, x1, y1, hh: [R.Prism(x0, y0, x1, y1, hh, [R.flat(hh + 2)], mat_track, "flat")]  # noqa: E731
        if b.get("id") == "bahndamm":  # Maste der Streckensignale: Ihr Licht schaltet das Spiel (game/assets/sites/gleisviertel.json, signals)
            iron = lambda f: g._c("iron")[None, :] * np.ones((len(f.fx), 1))  # noqa: E731
            for mx, my, mz in DAM_SIGNALS:
                items.append(g.roof_block(0.1, mz - h + 2, iron)((mx - 0.05 - b["x0"]) / w, (my - 0.05 - b["y0"]) / depth))
    elif kind == "gueterschuppen":
        h -= 18  # die Wand endet an der Traufe, darüber das Satteldach
        left = B.planks(wl, h)
        d = g.draw_on(left)
        d.rectangle((0, h - 10, wl, h), fill=g.C["brick_d"])  # Sockel in Rampenhöhe
        d.rectangle((0, h - 10, wl, h - 10), fill=g.C["iron"])
        d.rectangle((0, h - 46, wl, h - 45), fill=g.C["iron"])  # Laufschiene der Schiebetore
        gate = next((q for q in B.GATES if b["x0"] <= q["x0"] < b["x1"] and b["y0"] <= q["y0"] < b["y1"]), None)
        door_u = int((gate["x0"] - b["x0"] + 0.5) * HW) if gate else wl // 2
        for u in sorted({door_u, door_u - 96, door_u + 96}):
            if 20 < u < wl - 20:
                d.rectangle((u - 18, h - 45, u + 18, h - 10), fill=g.C["iron"])
                d.rectangle((u - 17, h - 44, u + 17, h - 10), fill=g.blend(g.C["wagon"], 0.9))
                for k in range(u - 17, u + 18, 5):
                    d.fine_line((k, h - 44, k, h - 10), fill=g.C["black"])
                d.line((u - 17, h - 44, u + 17, h - 10), fill=g.C["iron"])
                d.rectangle((u - 17, h - 28, u + 17, h - 27), fill=g.C["iron"])
                d.rectangle((u - 2, h - 26, u, h - 22), fill=g.C["brass_d"])
                d.rectangle((u - 13, h - 48, u - 11, h - 44), fill=g.C["steel"])  # Laufrollen
                d.rectangle((u + 11, h - 48, u + 13, h - 44), fill=g.C["steel"])
        small_window(left, d, door_u - 54, 16, 12, 10, lit=lit > 0)  # Schreibstube des Lademeisters, eine Funzel
        small_window(left, d, door_u + 136, 16, 12, 10)
        if b.get("name"):
            lettering(left, d, door_u + 48, 16, b["name"], 4)
        d.shade((0, 0, wl, 2), 0.6)
        right = B.planks(wr, h)
        dr = g.draw_on(right)
        dr.rectangle((0, h - 10, wr, h), fill=g.C["brick_d"])
        small_window(right, dr, wr // 2 - 6, 16, 12, 12)
        roof = lambda x0, y0, x1, y1, hh: [R.gable(x0, y0, x1, y1, hh, 18, ridge="x", sag=1.5, material=mat_shed)]  # noqa: E731
    elif kind == "wasserturm":
        shaft = h - 52
        left, d = g.wall(wl, shaft, ("brick",))
        right, dr = g.wall(wr, shaft, ("brick",))
        for face, width in ((d, wl), (dr, wr)):
            for u in (0, width - 9):  # Eckpfeiler
                face.rectangle((u, 0, u + 8, shaft), fill=g.C["brick_l"])
            face.rectangle((0, 0, width, 5), fill=g.C["stone"])  # Kragsteine unter dem Behälter
            for u in range(2, width, 8):
                face.rectangle((u, 6, u + 4, 9), fill=g.C["stone_d"])
            face.shade((0, 10, width, 14), 0.6)
            for v in (28, 58):
                face.rectangle((width // 2 - 2, v, width // 2 + 2, v + 12), fill=g.C["black"])
                face.rectangle((width // 2 - 3, v + 12, width // 2 + 3, v + 13), fill=g.C["stone_l"])
            face.shade((0, shaft - 16, width, shaft), 0.6, g.C["soot"], 0.5)
        iron_door(d, wl // 2 - 8, shaft - 1, 16, 28)
        d.shade((wl // 2 + 14, 20, wl // 2 + 17, shaft), 0.7, g.C["rust"], 0.5)  # Fallrohr zum Wasserkran

        def roof(x0, y0, x1, y1, hh):
            i = 0.2  # der Behälter steht eingerückt auf dem Schaft, ringsum bleibt ein Umgang
            a0, b0, a1, b1 = x0 + i, y0 + i, x1 - i, y1 - i
            cap = [R.rise_from(e, {"x0": a0, "x1": a1, "y0": b0, "y1": b1}[e], hh + 38, 20) for e in ("x0", "x1", "y0", "y1")]
            return [R.Prism(x0, y0, x1, y1, hh, [R.flat(hh + 2)], mat_tank, "flat"), R.block(a0, b0, a1, b1, hh + 2, hh + 38, mat_tank, "tank"),
                    R.Prism(a0, b0, a1, b1, hh + 38, cap, mat_tank, "cap")]
        h = shaft
    elif kind == "stellwerk":
        h -= 20
        left, d = g.wall(wl, h, ("brick",))
        right, dr = g.wall(wr, h, ("brick",))
        gate = next((q for q in B.GATES if b["x0"] <= q["x0"] < b["x1"] and b["y0"] <= q["y0"] < b["y1"]), None)
        door_u = int((gate["x0"] - b["x0"]) * HW) + 6 if gate else wl // 2 - 10
        for tex, face, width in ((left, d, wl), (right, dr, wr)):
            face.rectangle((0, 0, width, 30), fill=g.C["wood"])  # Kanzel aus Holz mit Fensterband
            for u in range(0, width, 4):
                face.fine_line((u, 0, u, 30), fill=g.C["wood_d"])
            for u in range(5, width - 14, 15):
                on = g.RNG.random() < lit
                face.rectangle((u - 1, 5, u + 12, 25), fill=g.C["wood_d"])
                face.rectangle((u, 6, u + 11, 24), fill=g.C["gas_d"] if on else g.C["glass_d"])
                face.fine_line((u + 5, 6, u + 5, 24), fill=g.C["wood_d"])
                face.fine_line((u, 14, u + 11, 14), fill=g.C["wood_d"])
                if on:
                    tex.info.setdefault("panes", []).append((u, 6, 12, 19))
            face.rectangle((0, 30, width, 33), fill=g.C["iron"])  # Schwelle der Kanzel auf Konsolen
            for u in range(4, width, 16):
                face.rectangle((u, 34, u + 2, 38), fill=g.C["iron"])
            face.shade((0, 34, width, 37), 0.6)
            face.shade((0, h - 12, width, h), 0.6, g.C["soot"], 0.5)
        if b.get("name"):
            g.name_plate(right, dr, wr // 2, 44, b["name"], 5)
        iron_door(d, door_u, h - 1, 20, 34)
        for u in (10, wl - 26):
            if abs(u - door_u) > 24:
                d.rectangle((u, h - 30, u + 9, h - 18), fill=g.C["black"])
                B.bars(d, u, h - 30, 9, 12)
        roof = lambda x0, y0, x1, y1, hh: [R.hip(x0, y0, x1, y1, hh, 20, 14, material=mat_shed)]  # noqa: E731
        items = [g.roof_block(0.2, 12, lambda f: g._c("iron")[None, :] * np.ones((len(f.fx), 1)), True)(0.72, 0.4)]
    elif kind == "kohlenbansen":
        h = 26
        tex_l, d = boards(wl, h, "wood_d", "black", 6)
        tex_r, dr = boards(wr, h, "wood_d", "black", 6)
        for face, width in ((d, wl), (dr, wr)):
            for u in range(0, width, 24):  # eingerammte Schienenstücke halten die Schwellenwand
                face.rectangle((u, 0, u + 2, h), fill=g.C["rust_d"])
            face.shade((0, 0, width, 4), 0.6, g.C["coal"], 0.6)
        left, right = tex_l, tex_r
        roof = lambda x0, y0, x1, y1, hh: [R.hip(x0, y0, x1, y1, hh - 3, 22, 9, material=mat_coal)]  # noqa: E731
    elif kind in ("baracke", "huette"):
        h = 30 if kind == "baracke" else 26
        left, d = boards(wl, h)
        right, dr = boards(wr, h)
        helper = next((n for n, (x, y) in B.HELPERS.items() if abs(y - b["y1"]) < 0.3 and b["x0"] <= x <= b["x1"]), None)
        spot = int((B.HELPERS[helper][0] - b["x0"]) * HW) if helper else None
        columns = list(range(10, wl - 14, 26))
        own = min(columns, key=lambda u: abs(u + 4 - spot)) if helper else None
        door_u = columns[len(columns) // 2] if own is None or columns[len(columns) // 2] != own else columns[0]
        for u in columns:
            if u == door_u:
                d.rectangle((u - 2, h - 25, u + 10, h - 1), fill=g.C["wood_d"])
                d.fine_line((u + 4, h - 25, u + 4, h - 1), fill=g.C["black"])
                d.rectangle((u - 3, h - 26, u + 11, h - 26), fill=g.C["iron"])
            elif u == own:
                g.RNG.random()
                small_window(left, d, u, 9, 9, 9, True, "kerze:" + helper)
            else:
                small_window(left, d, u, 9, 9, 9, g.RNG.random() < lit)
        for u in range(10, wr - 12, 30):
            small_window(right, dr, u, 9, 9, 9, g.RNG.random() < lit * 0.5)
        k = 2.0 if kind == "baracke" else 3.0
        roof = lambda x0, y0, x1, y1, hh: [R.Prism(x0, y0, x1, y1, hh, [R.rise_from("y0", y0, hh + 1, k)], mat_sheet_roof, "sheet")]  # noqa: E731
        items = [g.roof_block(0.14, 9, lambda f: g._c("iron")[None, :] * np.ones((len(f.fx), 1)), kind == "baracke" and lit > 0)(0.3, 0.45)]
    elif kind in ("holzstapel", "kohlenkasten"):
        left, d = boards(wl, h, "wood", "black", 4)
        right, dr = boards(wr, h, "wood", "black", 4)
        if kind == "holzstapel":
            for v in range(0, h, 4):  # Stirnholz der gestapelten Schwellen
                for u in range((v // 4 % 2) * 3, wl, 7):
                    d.rectangle((u, v + 1, u + 4, v + 3), fill=g.blend(g.C["wood"], 0.8 + ((u + v) % 5) * 0.1))
        top = mat_wood_top if kind == "holzstapel" else mat_coal
        roof = lambda x0, y0, x1, y1, hh: [R.Prism(x0, y0, x1, y1, hh, [R.flat(hh + 1)], lambda f: top(f) if f.kind == "plane" else planks_np(f), "flat")]  # noqa: E731
    else:
        return False
    g.building(b["x0"], b["y0"], w, depth, h, left, right, s, things, items, roof=roof)
    things[-1].opaque = b.get("opaque", True)
    return True


# ---------- Kleines ----------

def P(dr, c):
    return g.C[c] + (255,) if isinstance(c, str) else tuple(c) + (255,)


def paint_signal(dr, px, py, k):
    """Formsignal: Gittermast, roter Flügel mit weißem Rand, darunter die Laterne mit roter Blende."""
    dr.rectangle((px - 3 * k, py - 3 * k, px + 3 * k - 1, py), fill=P(dr, "stone_d"))
    dr.rectangle((px - 2 * k, py - 74 * k, px + 2 * k - 1, py - 3 * k), fill=P(dr, "iron"))
    dr.rectangle((px - 2 * k, py - 74 * k, px - k - 1, py - 3 * k), fill=P(dr, "steel"))
    for v in range(8, 70, 7):
        dr.line((px - 2 * k, py - v * k, px + 2 * k - 1, py - (v + 5) * k), fill=P(dr, "black"), width=max(1, k // 2))
    dr.polygon([(px, py - 72 * k), (px + 20 * k, py - 78 * k), (px + 20 * k, py - 73 * k), (px, py - 67 * k)], fill=P(dr, "signal_r"))
    dr.line((px + 15 * k, py - 76 * k, px + 15 * k, py - 72 * k), fill=P(dr, "paper"), width=max(1, k))
    dr.ellipse((px + 17 * k, py - 79 * k, px + 22 * k, py - 72 * k), fill=P(dr, "signal_r"), outline=P(dr, "paper"))
    dr.rectangle((px - 4 * k, py - 62 * k, px + 2 * k - 1, py - 56 * k), fill=P(dr, "black"))
    dr.rectangle((px - 3 * k, py - 61 * k, px - k, py - 58 * k), fill=P(dr, (200, 60, 40)))
    dr.line((px + k, py - 20 * k, px + 5 * k, py - 4 * k), fill=P(dr, "black"))  # Drahtzug


def paint_crane(dr, px, py, k):
    """Wasserkran: gusseiserne Säule, schwenkbarer Ausleger, Lederschlauch, Handrad."""
    dr.rectangle((px - 4 * k, py - 4 * k, px + 4 * k - 1, py), fill=P(dr, "stone_d"))
    dr.rectangle((px - 2 * k, py - 52 * k, px + 2 * k - 1, py - 4 * k), fill=P(dr, "iron"))
    dr.rectangle((px - 2 * k, py - 52 * k, px - k, py - 4 * k), fill=P(dr, "steel"))
    dr.rectangle((px - 3 * k, py - 30 * k, px + 3 * k - 1, py - 28 * k), fill=P(dr, "black"))
    dr.line((px, py - 52 * k, px - 22 * k, py - 58 * k), fill=P(dr, "iron"), width=3 * k)
    dr.line((px, py - 42 * k, px - 14 * k, py - 55 * k), fill=P(dr, "black"), width=max(1, k))
    dr.line((px - 22 * k, py - 58 * k, px - 23 * k, py - 44 * k), fill=P(dr, "wood_d"), width=2 * k)
    dr.ellipse((px + 2 * k, py - 24 * k, px + 8 * k, py - 18 * k), outline=P(dr, "rust"), width=max(1, k))
    dr.ellipse((px - 3 * k, py - 56 * k, px + 3 * k, py - 50 * k), fill=P(dr, "iron"))


def paint_switch(dr, px, py, k):
    """Weichenbock: Stellhebel mit Gewicht, darauf die weiße Weichenlaterne dicht über dem Boden."""
    dr.rectangle((px - 5 * k, py - 2 * k, px + 5 * k - 1, py), fill=P(dr, "wood_d"))
    dr.rectangle((px - k, py - 10 * k, px + k - 1, py - 2 * k), fill=P(dr, "iron"))
    dr.line((px, py - 4 * k, px + 10 * k, py - 12 * k), fill=P(dr, "iron"), width=max(1, k))
    dr.ellipse((px + 7 * k, py - 15 * k, px + 12 * k, py - 10 * k), fill=P(dr, "paper"))
    dr.pieslice((px + 7 * k, py - 15 * k, px + 12 * k, py - 10 * k), 90, 270, fill=P(dr, "black"))
    dr.rectangle((px - 3 * k, py - 17 * k, px + 3 * k - 1, py - 10 * k), fill=P(dr, "black"))
    dr.rectangle((px - 2 * k, py - 16 * k, px + 2 * k - 1, py - 11 * k), fill=(255, 232, 160, 255))
    dr.rectangle((px - 2 * k, py - 19 * k, px + 2 * k - 1, py - 17 * k), fill=P(dr, "iron"))


def paint_barrier(dr, px, py, k):
    """Schranke am Bahnübergang: Bock mit Gegengewicht, der Baum steht offen, rot und weiß."""
    dr.rectangle((px - 2 * k, py - 16 * k, px + 2 * k - 1, py), fill=P(dr, "iron"))
    dr.rectangle((px + 2 * k, py - 14 * k, px + 8 * k, py - 9 * k), fill=P(dr, "black"))
    for i in range(7):
        a = (px - i * 5 * k, py - 14 * k - i * 9 * k)
        b = (px - (i + 1) * 5 * k, py - 14 * k - (i + 1) * 9 * k)
        dr.line((a, b), fill=P(dr, "signal_r" if i % 2 else "paper"), width=2 * k)
    dr.rectangle((px - 3 * k, py - 24 * k, px + k, py - 19 * k), fill=P(dr, "black"))
    dr.rectangle((px - 2 * k, py - 23 * k, px, py - 20 * k), fill=P(dr, (200, 60, 40)))


def paint_buffer(dr, px, py, k):
    """Prellbock: aufgebogene Schienen, davor die Bohle."""
    for dx in (-7, 5):
        dr.line((px + dx * k, py, px + dx * k, py - 12 * k), fill=P(dr, "rust_d"), width=2 * k)
        dr.line((px + dx * k, py - 12 * k, px + (dx + 8) * k, py), fill=P(dr, "rust_d"), width=max(1, k))
    dr.rectangle((px - 10 * k, py - 15 * k, px + 9 * k, py - 10 * k), fill=P(dr, "wood_d"))
    for dx in (-8, -2, 4):
        dr.rectangle((px + dx * k, py - 15 * k, px + (dx + 3) * k - 1, py - 10 * k), fill=P(dr, "paper"))
    dr.rectangle((px - 10 * k, py - 10 * k, px + 9 * k, py - 10 * k), fill=P(dr, "black"))


def paint_sleepers(dr, px, py, k):
    """Schwellenstapel: kreuzweise geschichtet, ölgetränkt."""
    for row in range(4):
        v = py - row * 4 * k
        if row % 2 == 0:
            for dx in (-9, -3, 3):
                dr.rectangle((px + dx * k, v - 4 * k, px + (dx + 5) * k - 1, v - 1), fill=P(dr, "wood_d"))
                dr.rectangle((px + dx * k, v - 4 * k, px + (dx + 5) * k - 1, v - 4 * k), fill=P(dr, "wood"))
        else:
            dr.rectangle((px - 10 * k, v - 4 * k, px + 9 * k, v - 1), fill=P(dr, "wood"))
            dr.rectangle((px - 10 * k, v - k, px + 9 * k, v - 1), fill=P(dr, "black"))


def paint_cart(dr, px, py, k):
    """Sackkarre der Ladeschaffner: zwei Räder, Kiste darauf, Holme nach hinten."""
    dr.line((px - 9 * k, py - 14 * k, px + 6 * k, py - 3 * k), fill=P(dr, "wood"), width=2 * k)
    dr.rectangle((px - 4 * k, py - 16 * k, px + 5 * k, py - 7 * k), fill=P(dr, "wood_d"))
    dr.rectangle((px - 4 * k, py - 16 * k, px + 5 * k, py - 15 * k), fill=P(dr, "wood"))
    dr.line((px - 4 * k, py - 11 * k, px + 5 * k, py - 11 * k), fill=P(dr, "iron"))
    dr.ellipse((px + k, py - 8 * k, px + 9 * k, py), fill=P(dr, "black"))
    dr.ellipse((px + 4 * k, py - 5 * k, px + 6 * k, py - 3 * k), fill=P(dr, "iron"))


def paint_drum(dr, px, py, k):
    """Feuertonne: durchlöchertes Ölfass. Die Glut darin ist ein eigenes Bild."""
    dr.rectangle((px - 5 * k, py - 15 * k, px + 5 * k - 1, py), fill=P(dr, "rust_d"))
    dr.rectangle((px - 5 * k, py - 15 * k, px - 3 * k, py), fill=P(dr, "rust"))
    for v in (-11, -5):
        dr.rectangle((px - 5 * k, py + v * k, px + 5 * k - 1, py + v * k), fill=P(dr, "black"))
    for dx, dy in ((-2, -8), (2, -9), (0, -3), (3, -3)):
        dr.rectangle((px + dx * k, py + dy * k, px + dx * k + k - 1, py + dy * k + k - 1), fill=P(dr, (150, 52, 24)))
    dr.ellipse((px - 5 * k, py - 17 * k, px + 5 * k - 1, py - 13 * k), fill=P(dr, "black"))


def embers(x, y, z, s, things, B):
    """Glut oben in der Feuertonne: leuchtet nur im Bild."""
    B.PICTURE_LIGHTS.append({"x": x, "y": y, "z": z + 4, "kind": "kohlenkorb"})

    def draw():
        k = s.res
        px, py = s.to_screen(x, y, z)
        px, py = int(px), int(py)
        dr = ImageDraw.Draw(s.img)
        dr.ellipse((px - 4 * k, py - 2 * k, px + 4 * k - 1, py + k), fill=(226, 170, 90, 255))
        dr.ellipse((px - 2 * k, py - k, px + k, py), fill=(255, 232, 160, 255))
    things.append(g.Thing(x - 0.06, y - 0.06, x + 0.06, y + 0.06, z - 1, z + 3, draw))
    things[-1].kind = "glow"


def lamp_glass(x, y, z, kind, s, things, B, color):
    """Kleines Signallicht an einem Mast: zählt als Laterne der Karte, Art und Farbe kennt der Lichtbaker."""
    g.AMBIENT["lights"].append([x, y, z])
    B.LAMP_KINDS[(x, y)] = kind

    def draw():
        k = s.res
        px, py = s.to_screen(x, y, z)
        px, py = int(px), int(py)
        ImageDraw.Draw(s.img).rectangle((px - k, py - k, px + k - 1, py + k - 1), fill=tuple(color) + (255,))
    things.append(g.Thing(x - 0.04, y - 0.04, x + 0.04, y + 0.04, z - 2, z + 2, draw))
    things[-1].kind = "lamp"


def furnish(scene, s, things, glows, B):
    """Laternen und Requisiten des Bahnhofs."""
    seen = set()
    for lamp in scene["lamps"]:
        at = (lamp["x"], lamp["y"])
        if at in seen:
            continue
        seen.add(at)
        group = lamp.get("group", "")
        if lamp.get("kind") == "bogenlampe":
            B.arc_lamp(lamp["x"], lamp["y"], s, things, glows)
        elif group == "signal":
            lamp_glass(lamp["x"] - 0.05, lamp["y"] + 0.05, 59, "signal", s, things, B, (255, 120, 90))
        elif group == "schranke":
            lamp_glass(lamp["x"] - 0.05, lamp["y"] + 0.05, 21, "schranke", s, things, B, (255, 120, 90))
        elif group == "weiche":
            lamp_glass(lamp["x"], lamp["y"] + 0.05, 13, "weiche", s, things, B, (255, 244, 214))
        else:
            B.wall_lamp(lamp["x"], lamp["y"], 44, s, things)
    paints = {"signal": (paint_signal, 80), "wasserkran": (paint_crane, 60), "weichenbock": (paint_switch, 20), "schranke": (paint_barrier, 78),
              "prellbock": (paint_buffer, 16), "schwellenstapel": (paint_sleepers, 16), "karren": (paint_cart, 17), "brunnen": (B.paint_pump, 24),
              "feuertonne": (paint_drum, 17)}
    for prop in scene["props"]:
        kind = prop["kind"]
        if kind == "fluchtwagen":
            B.laundry_van(prop["x"] - 0.4, prop["y"] - 0.7, s, things, sign=None)
            things[-1].solid, things[-1].opaque = prop["solid"], prop["opaque"]
            B.storm_lantern(prop["x"] + 0.47, prop["y"] - 0.25, 27, s, things)  # das Ziel des Rückwegs muss von weitem zu finden sein
        elif kind == "kiste":
            g.crate(int(prop["x"]) + 0.3, int(prop["y"]) + 0.3, s, things)
            things[-1].solid, things[-1].opaque = prop["solid"], prop["opaque"]
        elif kind in paints:
            paint, height = paints[kind]
            B.post_thing(prop["x"], prop["y"], height, s, things, paint, solid=prop["solid"], kind="prop" if kind != "signal" and kind != "schranke" else "open")
            if kind == "feuertonne":
                embers(int(prop["x"]) + 0.5, int(prop["y"]) + 0.5, 15, s, things, B)
    inside = lambda q: any(b["x0"] <= q["x0"] < b["x1"] and b["y0"] <= q["y0"] < b["y1"] for b in scene["buildings"])  # noqa: E731
    for gate in scene.get("gates", ()):
        if not inside(gate):  # Türen in einer Hauswand sind auf die Fassade gemalt
            B.sheet_gate(gate, s, things, scene)


# ---------- Kulisse ----------

BACKDROP_REACH = 13  # so viele Felder weit läuft der Bahnhof über den Kartenrand, bevor er im Dunst verschwindet


def export_backdrop(assets, tiles, B):
    """Der Bahnhof jenseits der Karte: Gleise, Damm und Schuppen laufen weiter, fern im Westen und Norden die Stadt.

    Reine Kulisse ohne Lichtberechnung und ohne Einfluss aufs Spiel. Vor der Karte (Süden, Osten) bleibt alles flach.
    """
    g.DUAL = False
    s = g.Scene(tiles)
    s.w, s.h = g.backdrop_size(tiles)
    s.ox, s.oy = s.w // 2, s.h // 2 - tiles * g.HH
    s.img = Image.new("RGBA", (s.w, s.h), g.C["sky"] + (255,))
    s.px = s.img.load()
    ys, xs = np.mgrid[0:s.h, 0:s.w]
    a, b = (xs + 0.5 - s.ox) / HW, (ys + 0.5 - s.oy) / g.HH
    fx, fy = (a + b) / 2, (b - a) / 2
    dist = np.maximum.reduce([-fx, fx - tiles, -fy, fy - tiles])
    near = (dist >= 0) & (dist < BACKDROP_REACH)
    paving = np.floor(g.ground_np(fx[near], fy[near]) * 0.9)
    haze = ((dist[near] / BACKDROP_REACH) ** 1.3)[:, None]
    arr = np.asarray(s.img).copy()
    arr[near, :3] = (paving + (np.array(g.C["sky"], np.float64) - paving) * haze).astype(np.uint8)
    s.img = Image.fromarray(arr, "RGBA")
    s.px = s.img.load()
    canvas = s.img
    things = []

    def put(kind, x0, y0, x1, y1, height, **more):
        building(dict(kind=kind, x0=x0, y0=y0, x1=x1, y1=y1, height=height, **more), s, things, B)

    put("bahndamm", -24, 0, 0, 2, 56)
    put("bahndamm", tiles, 0, tiles + 24, 2, 56)
    for x0, x1, y in ((-13, -3, 10), (-9, -2, 12), (-15, -5, 13), (-8, -2, 15),
                      (tiles + 3, tiles + 11, 9), (tiles + 5, tiles + 15, 11), (tiles + 3, tiles + 9, 14)):
        put("gueterwagen", x0, y, x1, y + 1, 52)
    put("gueterschuppen", -13, 2, -3, 6, 78)
    put("gueterschuppen", tiles + 4, 2, tiles + 13, 6, 70)
    put("wasserturm", -5, -6, -3, -4, 132)
    for x in (-12, -5):
        put("baracke", x, 19, x + 5, 22, 38)
        put("kohlenbansen", x, 24, x + 4, 27, 34)
    for x in (3, 11, 22):  # südlich der Karte: Baracken und Kohlenlager, flach
        put("baracke", x, tiles + 3, x + 5, tiles + 6, 38)
    put("kohlenbansen", 16, tiles + 3, 20, tiles + 6, 34)
    for y in (19, 24):
        put("baracke", tiles + 3, y, tiles + 8, y + 3, 38)
    # Fern die Stadt: Mietshäuser im Westen und jenseits des Damms im Norden
    blocks = [(x, y, 6, 6, 170 if ring else 128) for ring, x in ((0, -21), (1, -29)) for y in (-15, -7, 1, 9, 17, 25)]
    blocks += [(x, y, 6, 6, 170 if ring else 128) for ring, y in ((0, -13), (1, -21)) for x in (-12, -4, 4, 12, 20, 28)]
    for x, y, w, d, h in blocks:
        material = ("stone",) if g.hash01(int(x), int(y), 4) < 0.3 else ("brick",)
        left = g.tenement(int(w * HW), h, material=material, lit_rate=0.1)
        right = g.tenement(int(d * HW), h, material=material, lit_rate=0.1)
        g.building(x, y, w, d, h, left, right, s, things, [g.chimney(0.2, 0.3, 24, False)], roof=g.roof_gable(34, "x" if g.hash01(int(x), int(y), 6) < 0.5 else "y", 1.0))
    for t in g.sort_things(things):
        s.img = Image.new("RGBA", (s.w, s.h), (0, 0, 0, 0))
        s.px = s.img.load()
        t.draw()
        bbox = s.img.getbbox()
        if not bbox:
            continue
        part = s.img.crop(bbox)
        away = max(-t.x1, t.x0 - tiles, -t.y1, t.y0 - tiles)
        haze = min(0.8, 0.12 + away / 26)
        dimmed = Image.blend(part, Image.new("RGBA", part.size, (0, 0, 0, 255)), 0.15)
        fogged = Image.blend(dimmed, Image.new("RGBA", part.size, g.C["sky"] + (255,)), haze)
        canvas.paste(fogged, bbox[:2], part.getchannel("A"))
    canvas.save(assets / "backdrop.png", optimize=True)
