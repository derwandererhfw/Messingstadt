"""Platzhalterstadt für einen Einsatz, gebaut aus dem Grundriss data/scene_<nr>.json mit den Bausteinen von generate_concept.py.

Aufruf: python tools/level/placeholder_city.py 02
Schreibt game/assets/<id>/ (Boden, Objekte, Manifest, Feinfassungen, Lichtkarten) und data/light_<nr>.json.
Die Stadt des ersten Einsatzes bleibt unberührt: Generator und Lichtbake laufen in einem eigenen Arbeitsordner.
Vorläufig, bis die Grafik das Viertel selbst baut. Schlichte Fassaden, flache Dächer, keine Kulisse.
"""
import importlib
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "tools" / "art"
MATERIAL = {"stadtwall": "brick", "amtsbau": "stone", "wachstube": "brick", "wachlokal": "brick", "kaserne": "brick", "remise": "brick",
            "mietshaus": "brick", "hofmauer": "brick"}
PLAIN = {"stadtwall", "hofmauer", "remise"}  # ohne Fenster


def main():
    nr = sys.argv[1] if len(sys.argv) > 1 else "02"
    scene = json.loads((ROOT / "data" / f"scene_{nr}.json").read_text(encoding="utf-8"))
    work = ROOT / "work" / f"placeholder_{nr}"
    if work.exists():
        shutil.rmtree(work)
    (work / "tools" / "art").mkdir(parents=True)
    (work / "game" / "assets").mkdir(parents=True)
    (work / "data").mkdir()
    for extra in ART.glob("*.json"):
        shutil.copy(extra, work / "tools" / "art" / extra.name)

    sys.path.insert(0, str(ART))
    sys.argv = [sys.argv[0], "--city"]
    gc = importlib.import_module("generate_concept")
    # Alles, was der Generator schreibt, landet im Arbeitsordner statt in der Stadt des ersten Einsatzes.
    gc.__file__ = str(work / "tools" / "art" / "generate_concept.py")
    gc.subprocess = type("NoSubprocess", (), {"run": staticmethod(lambda *a, **k: None)})
    gc.RAILS, gc.GULLIES, gc.GRATES, gc.MANHOLES = (), (), (), ()

    def build_scene(with_people=True):
        gc.DUAL = not with_people
        s = gc.Scene(scene["width"])
        things, glows = [], []
        for points in gc.AMBIENT.values():
            points.clear()
        solids = []
        for b in scene["buildings"] + scene["walls"]:
            w, d, h = b["x1"] - b["x0"], b["y1"] - b["y0"], b["height"]
            material = (MATERIAL.get(b["kind"], "brick"),)
            faces = []
            for width in (w * 32, d * 32):
                if b["kind"] in PLAIN or h < 60:
                    tex, draw = gc.wall(width, h, material)
                    gc.plinth(draw, width, h, 5)
                else:
                    tex = gc.tenement(width, h, material=material, lit_rate=b.get("lit", 0.3))
                faces.append(tex)
            for door in b.get("doors", []):
                if door["side"] == "sued":
                    gc.door(gc.draw_on(faces[0]), int(door["at"] * 32 - 8), h - 1, 16, min(30, h - 6), "wood", door["kind"] == "haupttuer")
                elif door["side"] == "ost":
                    gc.door(gc.draw_on(faces[1]), int((d - door["at"]) * 32 - 8), h - 1, 16, min(30, h - 6), "wood", False)
            if b.get("name"):
                gc.name_plate(faces[0], gc.draw_on(faces[0]), w * 16, 6, b["name"], 7)
            gc.building(b["x0"], b["y0"], w, d, h, faces[0], faces[1], s, things)
            solids.append((b["x0"], b["y0"], b["x1"], b["y1"]))
        for p in scene["props"]:
            if p["kind"] == "fluchtwagen":
                gc.getaway_truck(p["x"] - 0.7, p["y"] - 0.7, s, things)
            elif p["solid"]:
                gc.crate(int(p["x"]) + 0.3, int(p["y"]) + 0.3, s, things)
        for lamp in scene["lamps"]:
            gc.lamp(lamp["x"], lamp["y"], s, things, glows)
        s.dirt = gc.draw_ground(s, solids, glows)
        s.solids, s.glows = solids, glows
        return s, gc.sort_things(things)

    gc.build_scene = build_scene
    gc.export_game()

    env = dict(os.environ, MESSINGSTADT_BAKE_ROOT=str(work))
    subprocess.run([sys.executable, str(ART / "bake_light.py")], check=True, env=env)

    target = ROOT / "game" / "assets" / scene["id"]
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True)
    assets = work / "game" / "assets"
    for name in ("ground.png", "manifest.json", "panes.json", "backdrop.png"):
        if (assets / name).exists():
            shutil.copy(assets / name, target / name)
    for name in ("things", "hd", "light"):
        shutil.copytree(assets / name, target / name)
    shutil.copy(work / "data" / "light_01.json", ROOT / "data" / f"light_{nr}.json")
    print(f"Platzhalterstadt in {target}, Licht in data/light_{nr}.json")


if __name__ == "__main__":
    main()
