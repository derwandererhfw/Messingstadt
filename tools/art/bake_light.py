"""Berechnet die Lichtkarten des Spiels mit dem Pathtracer in tools/Messingstadt.LightBake.

Aufruf: python tools/art/bake_light.py   (erst nach generate_concept.py, dauert etwa eine Minute)
Schreibt game/assets/light/: je Sprite <name>_a.png (Licht gesamt) und <name>_b.png (Anteile fürs Flackern)
sowie grid.png, das Lichtgitter für bewegte Figuren. Werte sind Wurzeln von Licht / MAX_LIGHT.
"""
import json
import random
import shutil
import sys
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image

import os

HOME = Path(__file__).resolve().parents[2]
ROOT = Path(os.environ.get("MESSINGSTADT_BAKE_ROOT", HOME))  # anderer Stand zum Vergleichen, etwa ein ausgecheckter älterer
ASSETS = ROOT / "game" / "assets"
OUT = ASSETS / "light"
WORK = ROOT / "work" / "light"

STRIDE = 2
MAX_LIGHT = 4.0
Z_SCALE = 39.2  # Bildpixel je Feld Höhe: Isometrie mit 30 Grad Blickwinkel
SKY = (0.135, 0.145, 0.20)  # halb so hell wie zuvor: Die Nacht ist dunkel, die Lichter tragen das Bild. Fürs Schleichen gilt RULES_SKY
VOID_LIGHT = (0.17, 0.185, 0.275)  # außerhalb der Karte: der gemalte Nachthimmel, mit dem Himmelslicht abgedunkelt
LAMP_COLOR = (1.0, 0.74, 0.42)  # elektrische Laternen des Kesselviertels. Fürs Schleichen gilt diese Farbe auf jeder Karte
# Laternen je Karte, nur fürs Bild: Farbe, Art und Reichweite in Feldern. Steht im Manifest unter "sources" für Lichtkegel und Dunst.
MAP_LAMPS = {
    # scale: Stärke im Bild gegenüber den Regeln. cut: Bei so vielen Feldern Abstand endet das Licht im Bild weich, 0 = nie.
    "": dict(color=LAMP_COLOR, kind="elektrisch", reach=4.2, scale=1.6, cut=4.2),
    "nordwall": dict(color=(0.90, 0.95, 0.56), kind="gas", reach=5.0, scale=1.3, cut=5.0),  # Gasglühlicht: grünlich-gelb und etwas schwächer im Umkreis
}
FIRE_REACH = 3.0
MOON_SPEC = 6.0  # Nässeglanz auf Dächern: Der Mond spiegelt sich in Flecken, wo die Fläche zur Kamera vermittelt
RIM_LIGHT = 2.6  # Kantenlicht: Die obere Umrisskante von Häusern und Aufbauten fängt den Mond, so stark wie das Mondlicht mal diesem Wert
# Lichtteppiche: Hinter jedem erleuchteten Fenster der unteren Geschosse hängt eine Lampe, ihr Licht fällt durch die Öffnung
# aufs Pflaster, das Fensterkreuz wirft Schatten. Dauerhaft brennende Fenster gehen fest ins Licht ein, schaltende (toggle)
# bekommen einen Lichtflicken, siehe "patches" im Manifest.
CARPET_POWER = 6.0
CARPET_REACH = 4.4
CARPET_MAX_SILL = 70  # Fenster, deren Unterkante höher liegt, werfen ihr Licht zu weit und zu schwach
CARPET_DEPTH = 0.55  # so viele Felder hinter der Wand hängt die Lampe
NEON_COLOR, NEON_POWER, NEON_REACH = (1.0, 0.16, 0.12), 2.6, 6.5
WELD_COLOR, WELD_POWER, WELD_REACH = (0.62, 0.8, 1.0), 5.0, 5.5
RAIL_GLOSS = 1.5  # Schienenköpfe sind blank: Sie werfen das Licht, das sie trifft, um diesen Faktor stärker zurück
RAIL_GLOSS_CAP = 0.85  # aber nie über diesen Lichtwert hinaus: Eine Schiene leuchtet nicht heller als das Pflaster unter der Laterne
MAP_RAILS = {"": ("x", (8.42, 8.58, 9.42, 9.58)), "nordwall": ("y", (10.8, 11.2))}
RIM_KINDS = ("", "prop", "gate", "boiler")
FIRE_COLOR = (1.0, 0.45, 0.15)
LAMP_POWER = 4.2
FIRE_POWER = 0.5  # gedämpft: Die Kessel sollen glimmen, nicht den Hof überstrahlen
RULES_FIRE_POWER = 0.7  # fürs Schleichen (light_01.json) zählt das Feuer weiter mit der alten Stärke, darauf ist die Balance abgestimmt
# Leuchtmittel hinter den Fenstern: Licht nach außen, Einfärbung der Scheibe, Anteil unter allen erleuchteten Fenstern.
WINDOW_TYPES = {
    "gluehlampe": dict(color=(1.0, 0.76, 0.46), power=0.014, pane=(1.2, 1.05, 0.78), weight=4),
    "gas": dict(color=(1.0, 0.62, 0.3), power=0.008, pane=(0.95, 0.68, 0.38), weight=3),
    "funzel": dict(color=(1.0, 0.68, 0.38), power=0.003, pane=(0.52, 0.38, 0.24), weight=2),
    "kalt": dict(color=(0.75, 0.86, 1.0), power=0.008, pane=(0.8, 0.95, 1.2), weight=1),
    "kerze": dict(color=(1.0, 0.55, 0.25), power=0.003, pane=(0.68, 0.42, 0.2), weight=1),
    "dunkel": dict(color=(0, 0, 0), power=0.0, pane=(1.0, 1.0, 1.0), weight=2),
    "halle": dict(color=(0.95, 0.88, 0.6), power=0.0016, pane=(0.72, 0.68, 0.44), weight=0),
}
SWITCHING_SHARE = 0.18  # dieser Anteil geht im Spiel an und aus und gibt deshalb kein vorberechnetes Licht ab
DARK_HALL_SHARE = 0.3
HALO_STRENGTH = 0.5
HALO_ROUNDS = 5
SCREEN_WINDOWS = 3  # Fenster mit flimmerndem Bildfunk, ihr Licht kommt im Spiel dazu
SPEC_STRENGTH = 1.6
PUDDLE_SPEC = 7.0
STUTTER_LAMP = (5.6, 10.5)
FLASH_DIRECTION = (0.72, -0.28, 0.62)  # Blitz: von der Seite und hoch, damit Schatten quer über die Straßen fallen
FLASH_AMBIENT = 0.2
VOID_FLASH = 0.3
BACKDROP_FLASH = 1.3  # die Kulisse ist dunkler gemalt, beim Blitz soll die Kartengrenze trotzdem nicht auffallen
MOON_DIRECTION = (-0.45, -0.25, 0.85)  # von hinten und oben: Nur Dächer und Oberseiten sehen ihn, nicht Fassaden und Straßen
MOON_COLOR = (0.252, 0.282, 0.372)  # auf 60 % gesenkt, als die Nacht dunkler wurde. Trifft nur Dächer und Oberseiten, zählt nicht fürs Schleichen.
FOG_SIZE = 256
EMISSIVE = ((255, 232, 160), (226, 170, 90))
EMISSIVE_REACH = 45
EMISSIVE_LIGHT = 1.05
GRID_RES = 4
GRID_Z = 22
TONE_KNEE = 0.28
BLUR_RADIUS = 2

# Fensterlicht, wie es die Spielregeln kennen: aus work/light/windows.json eines Laufs übernommen, nur bei gewollter Änderung neu
RULES_WINDOWS = Path(__file__).with_name("rules_windows.json")

# Himmelslicht der Spielregeln. Das Bild darf dunkler werden (SKY, MOON_COLOR), das Schleichlicht rechnet weiter hiermit.
RULES_SKY = (0.27, 0.29, 0.40)


def trial(name, value):
    """Probewerte für Vergleichsbilder: MS_<NAME> in der Umgebung skaliert oder ersetzt eine Konstante, ohne die Datei zu ändern."""
    raw = os.environ.get("MS_" + name)
    if raw is None:
        return value
    if "," in raw:
        return tuple(float(v) for v in raw.split(","))
    return tuple(v * float(raw) for v in value) if isinstance(value, tuple) else value * float(raw)


SKY, VOID_LIGHT, MOON_COLOR = trial("SKY", SKY), trial("SKY", VOID_LIGHT), trial("MOON", MOON_COLOR)
SPEC_STRENGTH, PUDDLE_SPEC = trial("SPEC", SPEC_STRENGTH), trial("SPEC", PUDDLE_SPEC)
MOON_SPEC, RIM_LIGHT = trial("MOON_SPEC", MOON_SPEC), trial("RIM", RIM_LIGHT)
for _t in WINDOW_TYPES.values():
    _t["power"] = trial("WINDOW", _t["power"])



def is_emissive(rgba):
    rgb = rgba[..., :3].astype(np.int32)
    hit = np.zeros(rgb.shape[:2], bool)
    for color in EMISSIVE:
        hit |= (np.abs(rgb - np.array(color)).sum(axis=2) < EMISSIVE_REACH)
    return hit & (rgba[..., 3] > 128)


def half_mask(rgba, glow):
    """Maske in halber Auflösung: 0 durchsichtig, 1 Fläche, ab 2 leuchtet selbst (2 + Leuchtmittel)."""
    h, w = rgba.shape[:2]
    hw, hh = (w + 1) // 2, (h + 1) // 2
    mask = np.zeros((hh, hw), np.uint8)
    solid = rgba[..., 3] > 16
    for y in range(hh):
        for x in range(hw):
            if solid[y * 2:y * 2 + 2, x * 2:x * 2 + 2].any():
                block = glow[y * 2:y * 2 + 2, x * 2:x * 2 + 2]
                mask[y, x] = 1 + int(block.max()) if (block > 0).sum() >= 2 else 1
    return mask


def pane_pixels(pane, sprite_left, sprite_top, rgba):
    """Bildpunkte eines Fensters oder Buchstabens innerhalb seines Sprites. Dunkle Sprossen bleiben ausgespart."""
    h, w = rgba.shape[:2]
    pixels = []
    for u in range(pane["u"], pane["u"] + pane["w"]):
        off = int(u * pane["slope"]) if pane["slope"] > 0 else -int(u * -pane["slope"])
        for v in range(pane["v"], pane["v"] + pane["h"]):
            x, y = pane["x"] + u - sprite_left, pane["y"] + off + v - sprite_top
            if 0 <= x < w and 0 <= y < h and rgba[y, x, 3] > 128 and rgba[y, x, 0] >= 70:
                pixels.append((y, x))
    return pixels


def pane_area(pane, sprite_left, sprite_top, rgba):
    """Alle deckenden Bildpunkte eines Fensters, auch die dunklen Sprossen. In doppelter Auflösung sind die Sprossen
    dünner, dort wird ein Teil dieser Fläche zu Glas."""
    h, w = rgba.shape[:2]
    pixels = []
    for u in range(pane["u"], pane["u"] + pane["w"]):
        off = int(u * pane["slope"]) if pane["slope"] > 0 else -int(u * -pane["slope"])
        for v in range(pane["v"], pane["v"] + pane["h"]):
            x, y = pane["x"] + u - sprite_left, pane["y"] + off + v - sprite_top
            if 0 <= x < w and 0 <= y < h and rgba[y, x, 3] > 128:
                pixels.append((y, x))
    return pixels


def upscale(values, factor):
    return np.repeat(np.repeat(values, factor, axis=0), factor, axis=1)


def resize_float(values, size):
    """Weiches Hochrechnen eines Felds mit Fließkommawerten, je Kanal."""
    channels = [np.asarray(Image.fromarray(values[..., c].astype(np.float32), "F").resize(size, Image.BILINEAR)) for c in range(values.shape[-1])]
    return np.stack(channels, axis=-1)


def write_maps(name, rgba, light, deltas, ids, pane_light, halo, box, rim=False, rails=None):
    """Schreibt Licht (_a), Anteile fürs Flackern (_b) und Fensterkennung (_w) in der Größe des angezeigten Bilds."""
    size = (rgba.shape[1], rgba.shape[0])
    a = np.array(Image.fromarray(encode(light)).resize(size, Image.BILINEAR))
    if rails is not None:
        # Blanke Schienenköpfe: in der Auflösung des Bilds, damit der Glanz so schmal bleibt wie die Schiene.
        axis, lines, ox, oy = rails
        k = rgba.shape[0] / light.shape[0] / STRIDE
        ys, xs = np.mgrid[0:rgba.shape[0], 0:rgba.shape[1]]
        ua, ub = ((xs + 0.5) / k - ox) / 32, ((ys + 0.5) / k - oy) / 16
        across = (ua + ub) / 2 if axis == "x" else (ub - ua) / 2
        head = np.zeros(across.shape, bool)
        for line in lines:
            head |= np.abs(across - line) < 0.02
        level = (a.astype(np.float32) / 255) ** 2 * MAX_LIGHT
        level[head] = np.maximum(level[head], np.minimum(level[head] * RAIL_GLOSS, RAIL_GLOSS_CAP))
        a = encode(level)
    if rim and RIM_LIGHT > 0:
        # Kantenlicht in der Auflösung des angezeigten Bilds: ein Punkt breit voll, der darunter halb.
        solid = rgba[..., 3] > 128
        above = np.vstack([np.zeros((1, solid.shape[1]), bool), solid[:-1]])
        edge = solid & ~above
        second = solid & above & np.vstack([np.zeros((1, solid.shape[1]), bool), edge[:-1]])
        level = (a.astype(np.float32) / 255) ** 2 * MAX_LIGHT
        moon = np.array(MOON_COLOR, np.float32) * RIM_LIGHT
        level[edge] = np.maximum(level[edge], moon)
        level[second] = np.maximum(level[second], moon * 0.5)
        a = encode(level)
    b = np.array(Image.fromarray(encode(deltas), "RGBA").resize(size, Image.BILINEAR))
    glow = (is_emissive(rgba) if box >= 0 else np.zeros(rgba.shape[:2], bool)) | (ids > 0)
    a[glow] = encode(np.array([EMISSIVE_LIGHT] * 3))
    panes = ids > 0
    a[panes] = encode(pane_light[panes])
    if halo is not None:
        # Schein auf Rahmen und Wand rund ums Fenster. Nur um Fenster, die dauerhaft leuchten.
        lit = (a[~panes].astype(np.float32) / 255) ** 2 * MAX_LIGHT + halo[~panes] * HALO_STRENGTH
        a[~panes] = encode(lit)
    b[glow] = 0
    Image.fromarray(a).save(OUT / f"{name}_a.png", optimize=True)
    Image.fromarray(b).save(OUT / f"{name}_b.png", optimize=True)
    if ids.any():
        Image.fromarray(ids).convert("RGB").save(OUT / f"{name}_w.png")


def carpet_source(pane, thing, ox, oy):
    """Lampe hinter einem Fenster und seine Öffnung in Feldern, aus der Lage des Fensters im Bild. None, wenn es zu hoch liegt."""
    left = pane["x"] + pane["u"]
    if pane["slope"] > 0:  # Südwand: nach rechts im Bild wächst x
        c = thing["y1"]
        a0 = (left - ox) / 32 + c
        a1 = a0 + pane["w"] / 32
        top = oy + (a0 + c) * 16 - (pane["y"] + int(pane["u"] * pane["slope"]) + pane["v"])
        axis = 1
    else:  # Ostwand: nach rechts im Bild sinkt y
        c = thing["x1"]
        a1 = c - (left - ox) / 32
        a0 = a1 - pane["w"] / 32
        top = oy + (c + a1) * 16 - (pane["y"] - int(pane["u"] * -pane["slope"]) + pane["v"])
        axis = 0
    sill = top - pane["h"]
    if sill > CARPET_MAX_SILL or sill < 2:
        return None
    mid = (a0 + a1) / 2
    lamp = (top + 7) / Z_SCALE
    pos = [c - CARPET_DEPTH, mid, lamp] if axis == 0 else [mid, c - CARPET_DEPTH, lamp]
    return {"type": "window", "pos": pos, "axis": axis, "c": c, "a0": a0, "a1": a1, "z0": sill / Z_SCALE, "z1": top / Z_SCALE,
            "bar": max(0.025, pane["w"] / 32 * 0.07)}


def room_light(u, v, source):
    """Das Licht im Zimmer kommt von einer Stelle und fällt zum Rand der Scheibe hin ab."""
    return 1.3 - 0.7 * min(1.0, ((u - source[0]) ** 2 + (v - source[1]) ** 2) ** 0.5)


def mean_albedo(rgba):
    seen = rgba[rgba[..., 3] > 128][:, :3]
    return (seen.mean(axis=0) / 255).tolist() if len(seen) else [0.3, 0.3, 0.3]


def blur(values, mask):
    """Glättet das Rauschen des Umwegelichts, ohne über den Rand des Sprites zu verschmieren."""
    k = np.array([1, 4, 6, 4, 1], np.float32) / 16
    weight = mask.astype(np.float32)
    result = values * weight[..., None]

    def smooth(a, axis):
        pad = [(0, 0)] * a.ndim
        pad[axis] = (2, 2)
        return sum(w * np.roll(np.pad(a, pad), i - 2, axis=axis) for i, w in enumerate(k)).take(range(2, a.shape[axis] + 2), axis=axis)

    for axis in (0, 1):
        result = smooth(result, axis)
        weight = smooth(weight, axis)
    return result / np.maximum(weight[..., None], 1e-4)


def tone(light):
    """Sanfte Schulter: Die Mitte der Lichtkegel läuft nicht weiß aus."""
    lum = light.mean(axis=-1, keepdims=True)
    return light / (1 + TONE_KNEE * np.maximum(lum - 1, 0))


def encode(light):
    return (np.sqrt(np.clip(light / MAX_LIGHT, 0, 1)) * 255 + 0.5).astype(np.uint8)


def main():
    """Ohne Argumente das Kesselviertel. Mit --map=<id> --nr=<nr> eine weitere Karte aus game/assets/maps/<id>/."""
    global ASSETS, OUT, WORK, RULES_WINDOWS
    args = dict(a[2:].split("=", 1) for a in sys.argv[1:] if a.startswith("--") and "=" in a)
    light_file = ROOT / "data" / "light_01.json"
    albedo_file = Path(__file__).with_name("rules_albedo.json")
    if "map" in args:
        ASSETS = ROOT / "game" / "assets" / "maps" / args["map"]
        OUT = ASSETS / "light"
        WORK = ROOT / "work" / f"light_{args['map']}"
        light_file = ROOT / "data" / f"light_{args['nr']}.json"
        albedo_file = Path(__file__).with_name(f"rules_albedo_{args['nr']}.json")
        RULES_WINDOWS = Path(__file__).with_name(f"rules_windows_{args['nr']}.json")
    manifest = json.loads((ASSETS / "manifest.json").read_text(encoding="utf-8"))
    WORK.mkdir(parents=True, exist_ok=True)
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)

    ground = np.array(Image.open(ASSETS / "ground.png").convert("RGBA"))
    # Objekte mit feiner Fassung bekommen ihre Lichtkarten in deren Größe. Der Boden bleibt einfach, Wetter und
    # nasser Boden lesen sein Licht in Weltkoordinaten.
    fine_images = {Path(f).stem: np.array(Image.open(ASSETS / "hd" / "things" / f).convert("RGBA"))
                   for f in manifest.get("hd", {}).get("things", [])}
    # Fürs Schleichlicht strahlen Boden und Objekte so zurück wie vor dem Umbau der Grafik: Neue Farben ändern nur das Bild.
    # Eine neue Karte hat noch keinen festgehaltenen Stand: Der erste Lauf legt ihn an, danach gilt er.
    frozen = json.loads(albedo_file.read_text()) if albedo_file.exists() else {"ground": None, "boxes": {}}
    fresh = {"ground": None, "boxes": {}}
    boxes, sprites, images = [], [], []
    groundless = ground[(np.abs(ground[..., :3].astype(int) - ground[0, 0, :3].astype(int)).sum(axis=2) > 8)]
    sprites.append({"name": "ground", "box": -1, "left": 0, "top": 0})
    images.append(ground)
    for t in manifest["things"]:
        rgba = np.array(Image.open(ASSETS / "things" / t["file"]).convert("RGBA"))
        x0, y0, x1, y1, z1 = t["x0"], t["y0"], t["x1"], t["y1"], t["z1"]
        if t["kind"] == "boiler":
            # Der Quader umschließt den runden Kessel nur grob. Fürs Licht zählt der Kern.
            x0, y0, x1, y1, z1 = x0 + 0.4, y0 + 0.4, x1 - 0.4, y1 - 0.4, 62
        if t["kind"] == "lamp":
            z1 = 50
        albedo = mean_albedo(rgba)
        key = ",".join(f"{t[k]:.2f}" for k in ("x0", "y0", "x1", "y1", "z0"))
        fresh["boxes"][key] = [round(v, 5) for v in albedo]
        boxes.append({"x0": x0, "y0": y0, "x1": x1, "y1": y1, "z0": t["z0"], "z1": z1, "albedo": albedo,
                      "rulesAlbedo": frozen["boxes"].get(key, albedo), "roof": t.get("roof", []),
                      # Kleinkram wirft im Bild Schatten, fürs Schleichlicht aber keinen. Durchbrochenes wie Fahrräder nie.
                      "ghost": {"prop": 1, "open": 2}.get(t["kind"], 0)})
        sprites.append({"name": Path(t["file"]).stem, "box": len(boxes) - 1, "left": t["px"], "top": t["py"], "kind": t["kind"], "thing": t})
        images.append(rgba)

    # Jedes erleuchtete Fenster und jeder Buchstabe bekommt eine Kennung für das Spiel.
    pane_list = []
    for sprite, thing in zip(sprites[1:], manifest["things"]):
        for pane in thing["panes"]:
            pane_list.append((sprite, pane))
    # Bildfunk reihum auf die Häuser verteilt, damit das Flimmern nicht an einer Stelle sitzt.
    by_house = {}
    for i, (sprite, pane) in enumerate(pane_list):
        if pane["kind"] == "window" and not pane["type"]:
            by_house.setdefault(sprite["name"], []).append(i + 1)
    picker = random.Random(1927)
    houses = sorted(by_house)
    picker.shuffle(houses)
    screens = set()
    for turn in range(SCREEN_WINDOWS):
        screens.add(picker.choice([w for w in by_house[houses[turn % len(houses)]] if w not in screens]))
    panes_out = []
    welding_placed = False
    extras, groups, signs = [], [], {}
    type_names = list(WINDOW_TYPES)
    weights = [WINDOW_TYPES[name]["weight"] for name in type_names]
    for sprite, rgba in zip(sprites, images):
        sprite["area"] = np.zeros(rgba.shape[:2], np.uint8)
        sprite["area_light"] = np.zeros(rgba.shape[:2] + (3,), np.float32)
        sprite["ids"] = np.zeros(rgba.shape[:2], np.uint8)
        sprite["lit"] = np.zeros(rgba.shape[:2], np.uint8)
        sprite["pane_light"] = np.zeros(rgba.shape[:2] + (3,), np.float32)
        sprite["halo"] = np.zeros(rgba.shape[:2] + (3,), np.float32)
    sprite_rgba = {id(sprite): rgba for sprite, rgba in zip(sprites, images)}
    for index, (sprite, pane) in enumerate(pane_list):
        pane_id = index + 1
        pixels = pane_pixels(pane, sprite["left"], sprite["top"], sprite_rgba[id(sprite)])
        kind = pane["kind"]
        switching = False
        if kind == "window":
            type_name = "bildfunk" if pane_id in screens else pane["type"] or picker.choices(type_names, weights)[0]
            if pane["type"] and picker.random() < DARK_HALL_SHARE:
                type_name = "dunkel"
            kind = "tv" if pane_id in screens else {"kerze": "candle", "dunkel": "dark"}.get(type_name, "window")
            switching = kind == "window" and not pane["type"] and picker.random() < SWITCHING_SHARE
            if switching:
                kind = "toggle"
            if pane["type"] and type_name != "dunkel" and not welding_placed:
                kind = "weld"
                welding_placed = True
        source = (picker.uniform(0.25, 0.75), picker.uniform(0.1, 0.55))
        # Fläche samt Sprossen, mit dem Licht des Zimmers, für die feine Fensterkennung
        for y, x in pane_area(pane, sprite["left"], sprite["top"], sprite_rgba[id(sprite)]):
            sprite["area"][y, x] = pane_id
            sprite["area_light"][y, x] = EMISSIVE_LIGHT
            if pane["kind"] == "window" and pane_id not in screens:
                u = (x + sprite["left"] - pane["x"] - pane["u"]) / pane["w"]
                v = (y + sprite["top"] - pane["y"] - pane["v"] - (int(pane["u"] * pane["slope"]) if pane["slope"] > 0 else -int(pane["u"] * -pane["slope"]))) / pane["h"]
                sprite["area_light"][y, x] = np.array(WINDOW_TYPES[type_name]["pane"]) * room_light(u, v, source)
        for y, x in pixels:
            sprite["ids"][y, x] = pane_id
            sprite["pane_light"][y, x] = EMISSIVE_LIGHT
            # Bildfunk und Schild leuchten im Spiel selbst, sie zählen nicht zum vorberechneten Licht.
            if pane["kind"] == "window" and pane_id not in screens:
                u = (x + sprite["left"] - pane["x"] - pane["u"]) / pane["w"]
                v = (y + sprite["top"] - pane["y"] - pane["v"] - (int(pane["u"] * pane["slope"]) if pane["slope"] > 0 else -int(pane["u"] * -pane["slope"]))) / pane["h"]
                sprite["pane_light"][y, x] = np.array(WINDOW_TYPES[type_name]["pane"]) * room_light(u, v, source)
                if WINDOW_TYPES[type_name]["power"] > 0 and not switching:
                    sprite["lit"][y, x] = type_names.index(type_name) + 1
                    sprite["halo"][y, x] = WINDOW_TYPES[type_name]["color"]
                    sprite["halo"][y, x] *= WINDOW_TYPES[type_name]["pane"][1]
        # Zusatzquellen fürs Bild, siehe CARPET_POWER
        thing = sprite["thing"]
        if pane["kind"] == "sign":
            signs.setdefault(id(thing), (thing, []))[1].append(pane_id)
        elif pane["kind"] == "window" and kind in ("window", "toggle", "weld") and pane_id not in screens:
            source = carpet_source(pane, thing, manifest["ox"], manifest["oy"])
            strength = WINDOW_TYPES[type_name]["power"] / 0.014
            if source and (kind != "window" or strength > 0):
                source.update(group=-1, color=WINDOW_TYPES[type_name]["color"], power=CARPET_POWER * strength, reach=CARPET_REACH)
                if kind == "toggle":
                    source.update(group=len(groups), color=WINDOW_TYPES["gluehlampe"]["color"], power=CARPET_POWER)
                    groups.append({"kind": "fenster", "panes": [pane_id], "color": source["color"]})
                elif kind == "weld":
                    source.update(group=len(groups), color=WELD_COLOR, power=WELD_POWER, reach=WELD_REACH)
                    groups.append({"kind": "schweiss", "panes": [pane_id], "color": WELD_COLOR})
                extras.append(source)
        if pixels:
            ys, xs = zip(*pixels)
            panes_out.append({"id": pane_id, "kind": kind, "x": sprite["left"] + sum(xs) / len(xs), "y": sprite["top"] + sum(ys) / len(ys)})
    (ASSETS / "panes.json").write_text(json.dumps(panes_out), encoding="utf-8")

    with open(WORK / "scene.bin", "wb") as f:
        for sprite, rgba in zip(sprites, images):
            # Lampengläser und Feuerluken sind eigene Lichtquellen, sie zählen nicht als Fenster.
            allowed = sprite.get("kind", "") not in ("lamp", "boiler") and sprite["box"] >= 0
            glow = sprite["lit"].copy()
            if allowed:
                glow[is_emissive(rgba) & (sprite["ids"] == 0)] = 1
            mask = half_mask(rgba, glow)
            sprite["w"], sprite["h"] = mask.shape[1], mask.shape[0]
            f.write(mask.tobytes())

    for thing, ids in signs.values():
        # Leuchtschrift: eine Reihe roter Lichtpunkte vor dem Schild, alle Buchstaben zusammen ein Lichtflicken
        count = 6
        for i in range(count):
            f = (i + 0.5) / count
            extras.append({"type": "point", "group": len(groups), "color": NEON_COLOR, "power": NEON_POWER / count, "reach": NEON_REACH,
                           "pos": [thing["x0"] + (thing["x1"] - thing["x0"]) * f, thing["y1"] + 0.12, (thing["z0"] + thing["z1"]) / 2 / Z_SCALE]})
        groups.append({"kind": "neon", "panes": ids, "color": NEON_COLOR})
    rails = MAP_RAILS.get(args.get("map", ""))
    rails = rails and (rails[0], rails[1], manifest["ox"], manifest["oy"])
    lamp = dict(MAP_LAMPS.get(args.get("map", ""), MAP_LAMPS[""]))
    lamp["color"] = trial("LAMP_COLOR", lamp["color"])
    # Für Lichtkegel, Dunst und Flackern im Spiel: jede Quelle mit Ort, Höhe in Bildpunkten, Farbe, Art und Reichweite in Feldern.
    manifest["lampColor"] = list(lamp["color"])
    manifest["sources"] = [{"x": x, "y": y, "z": z, "kind": lamp["kind"], "color": list(lamp["color"]), "reach": lamp["reach"]} for x, y, z in manifest["lights"]]
    manifest["sources"] += [{"x": x, "y": y, "z": z, "kind": "feuer", "color": list(FIRE_COLOR), "reach": FIRE_REACH} for x, y, z in manifest["fireLights"]]
    (ASSETS / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    lights = []
    for x, y, z in manifest["lights"]:
        stutter = abs(x - STUTTER_LAMP[0]) < 0.01 and abs(y - STUTTER_LAMP[1]) < 0.01
        lights.append({"x": x, "y": y, "z": z, "color": "lamp", "power": LAMP_POWER, "layer": 1 if stutter else 0, "hooded": True})
    for x, y, z in manifest["fireLights"]:
        lights.append({"x": x, "y": y, "z": z, "color": "fire", "power": FIRE_POWER, "layer": 2, "hooded": False})

    scene = {
        "ox": manifest["ox"], "oy": manifest["oy"], "zScale": Z_SCALE, "mapSize": 20, "stride": STRIDE,
        "sky": SKY, "rulesSky": RULES_SKY, "groundAlbedo": (groundless.mean(axis=0)[:3] / 255).tolist(),
        "lampColor": LAMP_COLOR, "lampPicture": lamp["color"], "lampScale": trial("LAMP", lamp["scale"]), "lampReach": float(os.environ.get("MS_CUT", lamp["cut"])), "fireColor": FIRE_COLOR, "moonSpec": MOON_SPEC, "extras": extras, "extraGroups": len(groups),
        "windowTypes": [{"color": t["color"], "power": t["power"]} for t in WINDOW_TYPES.values()],
        "specStrength": SPEC_STRENGTH, "puddleSpec": PUDDLE_SPEC,
        "flashDirection": FLASH_DIRECTION, "flashAmbient": FLASH_AMBIENT,
        "moonDirection": MOON_DIRECTION, "moonColor": MOON_COLOR,
        "boxes": boxes, "lights": lights, "puddles": manifest["puddles"],
        "sprites": [{k: s[k] for k in ("box", "left", "top", "w", "h")} for s in sprites],
        "gridRes": GRID_RES, "gridZ": GRID_Z, "rulesFireScale": RULES_FIRE_POWER / FIRE_POWER,
        "rulesGroundAlbedo": frozen["ground"] or (groundless.mean(axis=0)[:3] / 255).tolist(),
        **({"rulesWindows": json.loads(RULES_WINDOWS.read_text())} if RULES_WINDOWS.exists() else {}),
    }
    (WORK / "scene.json").write_text(json.dumps(scene), encoding="utf-8")

    subprocess.run(["dotnet", "run", "-c", "Release", "--project", str(HOME / "tools" / "Messingstadt.LightBake"), "--", str(WORK)], check=True)

    data = np.fromfile(WORK / "light.bin", dtype="<f4")
    at = 0
    channels = 10 + len(groups)
    canvas_h, canvas_w = images[0].shape[:2]
    patches = [np.zeros((canvas_h, canvas_w, 3), np.float32) for _ in groups]
    for sprite, rgba in zip(sprites, images):
        h, w = sprite["h"], sprite["w"]
        block = data[at:at + w * h * channels].reshape(h, w, channels)
        at += w * h * channels
        # Lichtflicken: Was diese Gruppe auf dem Sprite beleuchtet, mit seiner Farbe. Spätere Sprites verdecken frühere.
        top, left = sprite["top"], sprite["left"]
        y0, x0 = max(0, top), max(0, left)
        y1, x1 = min(canvas_h, top + rgba.shape[0]), min(canvas_w, left + rgba.shape[1])
        if groups and y1 > y0 and x1 > x0:
            part = rgba[y0 - top:y1 - top, x0 - left:x1 - left]
            seen = part[..., 3] > 128
            extra = resize_float(np.maximum(block[..., 10:], 0), (rgba.shape[1], rgba.shape[0]))[y0 - top:y1 - top, x0 - left:x1 - left]
            for g, group in enumerate(groups):
                lit = part[..., :3] / 255.0 * extra[..., g:g + 1] * np.array(group["color"])
                patches[g][y0:y1, x0:x1][seen] = lit[seen]
        void = block[..., 0] < 0
        block[void] = 0
        mask = half_mask(rgba, np.zeros(rgba.shape[:2], np.uint8)) > 0
        light = tone(block[..., :3] + blur(block[..., 3:6], mask))
        light[void] = VOID_LIGHT
        deltas = np.stack([block[..., 6], block[..., 7], block[..., 8], block[..., 9]], axis=-1)
        deltas[void, 3] = VOID_FLASH
        # Rand auffüllen, damit beim Hochrechnen keine dunkle Kante aus leeren Punkten entsteht.
        light = fill_empty(light, mask)
        deltas = fill_empty(deltas, mask)
        halo = None
        if sprite["halo"].any():
            halo = sprite["halo"]
            everywhere = np.ones(halo.shape[:2], bool)
            for _ in range(HALO_ROUNDS):
                halo = blur(halo, everywhere)
        fine = fine_images.get(sprite["name"])
        if fine is None:
            write_maps(sprite["name"], rgba, light, deltas, sprite["ids"], sprite["pane_light"], halo, sprite["box"], sprite.get("kind") in RIM_KINDS, rails if sprite["box"] < 0 else None)
            continue
        # Feines Bild: Licht weich hochgerechnet, Selbstleuchten und Fenster aber Bildpunkt für Bildpunkt aus dem feinen
        # Bild. Glas ist dort, wo die Fensterfläche hell ist, also auch neben den nun dünneren Sprossen.
        factor = fine.shape[0] // rgba.shape[0]
        area = upscale(sprite["area"], factor)
        bright = (fine[..., 3] > 128) & (fine[..., 0] >= 70)
        ids = np.where(bright, area, 0).astype(np.uint8)
        size = (fine.shape[1], fine.shape[0])
        write_maps(sprite["name"], fine, light, deltas, ids, upscale(sprite["area_light"], factor),
                   None if halo is None else resize_float(halo, size), sprite["box"], sprite.get("kind") in RIM_KINDS, rails if sprite["box"] < 0 else None)

    for old in OUT.glob("patch_*.png"):
        old.unlink()
    listed = []
    for g, (group, patch) in enumerate(zip(groups, patches)):
        ys, xs = np.nonzero(patch.max(axis=2) > 0.012)
        if not len(ys):
            continue
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        Image.fromarray((np.clip(patch[y0:y1, x0:x1], 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT / f"patch_{g:02d}.png", optimize=True)
        listed.append({"file": f"light/patch_{g:02d}.png", "px": int(x0), "py": int(y0), **group, "color": list(group["color"])})
    manifest["patches"] = listed
    (ASSETS / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    print(f"{len(extras)} Zusatzquellen, {len(listed)} Lichtflicken")

    size = 20 * GRID_RES
    grid = np.fromfile(WORK / "grid.bin", dtype="<f4").reshape(size, size, 7)
    inside = grid[..., 0] < 0
    grid[inside] = 0
    filled = fill_empty(np.concatenate([tone(grid[..., :3]), grid[..., 3:4]], axis=-1), ~inside)
    Image.fromarray(encode(filled), "RGBA").save(OUT / "grid.png")
    # Für die Spielregeln: Helligkeit in Prozent, im Stand von Figuren gemessen. Eigenes Licht, siehe RULES_FIRE_POWER.
    rules = fill_empty(tone(grid[..., 4:7]), ~inside)
    levels = (rules.mean(axis=-1) * 100).round().astype(int).ravel().tolist()
    light_file.write_text(json.dumps({"cells": GRID_RES, "size": size, "levels": levels}), encoding="utf-8")
    if not albedo_file.exists():
        fresh["ground"] = [round(v, 5) for v in (groundless.mean(axis=0)[:3] / 255).tolist()]
        albedo_file.write_text(json.dumps(fresh, indent=0))
    if not RULES_WINDOWS.exists():
        shutil.copy(WORK / "windows.json", RULES_WINDOWS)
    # Die Kulisse hat kein eigenes Licht, sondern trägt überall das des Nachthimmels.
    Image.fromarray(encode(np.array([[VOID_LIGHT]]))).save(OUT / "backdrop_a.png")
    Image.fromarray(encode(np.array([[(0, 0, 0, BACKDROP_FLASH)]])), "RGBA").save(OUT / "backdrop_b.png")
    fog_noise()
    print(f"Lichtkarten in {OUT}")


def fog_noise():
    """Weiches, kachelbares Rauschen für den Bodennebel. Im Frequenzraum gefiltert, deshalb ohne Nahtstellen."""
    rng = np.random.default_rng(1927)
    spectrum = np.fft.rfft2(rng.standard_normal((FOG_SIZE, FOG_SIZE)))
    fy = np.fft.fftfreq(FOG_SIZE)[:, None]
    fx = np.fft.rfftfreq(FOG_SIZE)[None, :]
    shaped = spectrum * np.exp(-((fx ** 2 + fy ** 2) ** 0.5) / 0.018)
    field = np.fft.irfft2(shaped, (FOG_SIZE, FOG_SIZE))
    field = (field - field.min()) / (field.max() - field.min())
    Image.fromarray((field * 255).astype(np.uint8)).convert("RGB").save(OUT / "fog.png")


def fill_empty(values, mask):
    """Überträgt Werte aus gefüllten Punkten nach außen auf leere Nachbarn."""
    result = values.copy()
    known = mask.copy()
    for _ in range(6):
        if known.all():
            break
        pad = np.pad(result, ((1, 1), (1, 1), (0, 0)))
        known_pad = np.pad(known, 1).astype(np.float32)
        total = np.zeros_like(result)
        count = np.zeros(known.shape, np.float32)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ky, kx = 1 + dy, 1 + dx
            k = known_pad[ky:ky + known.shape[0], kx:kx + known.shape[1]]
            total += pad[ky:ky + known.shape[0], kx:kx + known.shape[1]] * k[..., None]
            count += k
        fresh = ~known & (count > 0)
        result[fresh] = total[fresh] / count[fresh][..., None]
        known = known | fresh
    return result


if __name__ == "__main__":
    main()
