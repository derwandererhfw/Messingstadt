"""Berechnet die Lichtkarten des Spiels mit dem Pathtracer in tools/Messingstadt.LightBake.

Aufruf: python tools/art/bake_light.py   (erst nach generate_concept.py, dauert etwa eine Minute)
Schreibt game/assets/light/: je Sprite <name>_a.png (Licht gesamt) und <name>_b.png (Anteile fürs Flackern)
sowie grid.png, das Lichtgitter für bewegte Figuren. Werte sind Wurzeln von Licht / MAX_LIGHT.
"""
import json
import shutil
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "game" / "assets"
OUT = ASSETS / "light"
WORK = ROOT / "work" / "light"

STRIDE = 2
MAX_LIGHT = 4.0
Z_SCALE = 39.2  # Bildpixel je Feld Höhe: Isometrie mit 30 Grad Blickwinkel
SKY = (0.27, 0.29, 0.40)
VOID_LIGHT = (0.34, 0.37, 0.55)  # außerhalb der Karte bleibt der gemalte Nachthimmel wie bisher
LAMP_COLOR = (1.0, 0.74, 0.42)
FIRE_COLOR = (1.0, 0.45, 0.15)
WINDOW_COLOR = (1.0, 0.72, 0.42)
LAMP_POWER = 4.2
FIRE_POWER = 0.7
WINDOW_POWER = 0.02
SPEC_STRENGTH = 1.6
PUDDLE_SPEC = 7.0
STUTTER_LAMP = (5.6, 10.5)
EMISSIVE = ((255, 232, 160), (226, 170, 90))
EMISSIVE_REACH = 45
EMISSIVE_LIGHT = 1.05
GRID_RES = 4
GRID_Z = 22
TONE_KNEE = 0.28
BLUR_RADIUS = 2


def is_emissive(rgba):
    rgb = rgba[..., :3].astype(np.int32)
    hit = np.zeros(rgb.shape[:2], bool)
    for color in EMISSIVE:
        hit |= (np.abs(rgb - np.array(color)).sum(axis=2) < EMISSIVE_REACH)
    return hit & (rgba[..., 3] > 128)


def half_mask(rgba, emissive_allowed):
    """Maske in halber Auflösung: 0 durchsichtig, 1 Fläche, 2 leuchtet selbst."""
    h, w = rgba.shape[:2]
    hw, hh = (w + 1) // 2, (h + 1) // 2
    mask = np.zeros((hh, hw), np.uint8)
    solid = rgba[..., 3] > 16
    glow = is_emissive(rgba) if emissive_allowed else np.zeros((h, w), bool)
    for y in range(hh):
        for x in range(hw):
            if solid[y * 2:y * 2 + 2, x * 2:x * 2 + 2].any():
                mask[y, x] = 2 if glow[y * 2:y * 2 + 2, x * 2:x * 2 + 2].sum() >= 2 else 1
    return mask


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
    manifest = json.loads((ASSETS / "manifest.json").read_text(encoding="utf-8"))
    WORK.mkdir(parents=True, exist_ok=True)
    if OUT.exists():
        shutil.rmtree(OUT)
    OUT.mkdir(parents=True)

    ground = np.array(Image.open(ASSETS / "ground.png").convert("RGBA"))
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
        boxes.append({"x0": x0, "y0": y0, "x1": x1, "y1": y1, "z0": t["z0"], "z1": z1, "albedo": mean_albedo(rgba)})
        sprites.append({"name": Path(t["file"]).stem, "box": len(boxes) - 1, "left": t["px"], "top": t["py"], "kind": t["kind"]})
        images.append(rgba)

    with open(WORK / "scene.bin", "wb") as f:
        for sprite, rgba in zip(sprites, images):
            # Lampengläser und Feuerluken sind eigene Lichtquellen, sie zählen nicht als Fenster.
            allowed = sprite.get("kind", "") not in ("lamp", "boiler") and sprite["box"] >= 0
            mask = half_mask(rgba, allowed)
            sprite["w"], sprite["h"] = mask.shape[1], mask.shape[0]
            f.write(mask.tobytes())

    lights = []
    for x, y, z in manifest["lights"]:
        stutter = abs(x - STUTTER_LAMP[0]) < 0.01 and abs(y - STUTTER_LAMP[1]) < 0.01
        lights.append({"x": x, "y": y, "z": z, "color": "lamp", "power": LAMP_POWER, "layer": 1 if stutter else 0, "hooded": True})
    for x, y, z in manifest["fireLights"]:
        lights.append({"x": x, "y": y, "z": z, "color": "fire", "power": FIRE_POWER, "layer": 2, "hooded": False})

    scene = {
        "ox": manifest["ox"], "oy": manifest["oy"], "zScale": Z_SCALE, "mapSize": 20, "stride": STRIDE,
        "sky": SKY, "groundAlbedo": (groundless.mean(axis=0)[:3] / 255).tolist(),
        "lampColor": LAMP_COLOR, "fireColor": FIRE_COLOR, "windowColor": WINDOW_COLOR, "windowPower": WINDOW_POWER,
        "specStrength": SPEC_STRENGTH, "puddleSpec": PUDDLE_SPEC,
        "boxes": boxes, "lights": lights, "sprites": sprites, "puddles": manifest["puddles"],
        "gridRes": GRID_RES, "gridZ": GRID_Z,
    }
    (WORK / "scene.json").write_text(json.dumps(scene), encoding="utf-8")

    subprocess.run(["dotnet", "run", "-c", "Release", "--project", str(ROOT / "tools" / "Messingstadt.LightBake"), "--", str(WORK)], check=True)

    data = np.fromfile(WORK / "light.bin", dtype="<f4")
    at = 0
    for sprite, rgba in zip(sprites, images):
        h, w = sprite["h"], sprite["w"]
        block = data[at:at + w * h * 9].reshape(h, w, 9)
        at += w * h * 9
        void = block[..., 0] < 0
        block[void] = 0
        mask = half_mask(rgba, False) > 0
        light = tone(block[..., :3] + blur(block[..., 3:6], mask))
        light[void] = VOID_LIGHT
        deltas = np.stack([block[..., 6], block[..., 7], block[..., 8]], axis=-1)
        # Rand auffüllen, damit beim Hochrechnen keine dunkle Kante aus leeren Punkten entsteht.
        light = fill_empty(light, mask)
        deltas = fill_empty(deltas, mask)
        size = (rgba.shape[1], rgba.shape[0])
        a = np.array(Image.fromarray(encode(light)).resize(size, Image.BILINEAR))
        b = np.array(Image.fromarray(encode(deltas)).resize(size, Image.BILINEAR))
        glow = is_emissive(rgba) if sprite["box"] >= 0 else np.zeros(rgba.shape[:2], bool)
        a[glow] = encode(np.array([EMISSIVE_LIGHT] * 3))
        b[glow] = 0
        Image.fromarray(a).save(OUT / f"{sprite['name']}_a.png", optimize=True)
        Image.fromarray(b).save(OUT / f"{sprite['name']}_b.png", optimize=True)

    size = 20 * GRID_RES
    grid = np.fromfile(WORK / "grid.bin", dtype="<f4").reshape(size, size, 3)
    inside = grid[..., 0] < 0
    grid[inside] = 0
    grid = fill_empty(tone(grid), ~inside)
    Image.fromarray(encode(grid)).save(OUT / "grid.png")
    print(f"Lichtkarten in {OUT}")


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
