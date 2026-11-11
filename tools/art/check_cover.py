"""Prüft an den fertigen Bildern einer Karte, welche freien Felder die Kamera verdeckt, Dächer und Aufbauten eingeschlossen.

Aufruf: python tools/art/check_cover.py 02
Gegenstück zu tools/level/scene_to_map.py, das dieselbe Frage nur für die Quader des Grundrisses beantwortet. Weichen
die Zahlen ab, verdeckt ein Dach oder Aufbau mehr als geplant.
"""
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
HW, HH = 32, 16
FIGURE = 40


def main():
    nr = sys.argv[1] if len(sys.argv) > 1 else "02"
    scene = json.loads((ROOT / "data" / f"scene_{nr}.json").read_text(encoding="utf-8"))
    assets = ROOT / "game" / "assets" / scene["id"]
    manifest = json.loads((assets / "manifest.json").read_text(encoding="utf-8"))
    solids = json.loads((ROOT / "data" / f"map_{nr}.json").read_text(encoding="utf-8"))["solids"]
    w, h = scene["width"], scene["height"]
    things = [(t, Image.open(assets / "things" / t["file"]).getchannel("A").load(), Image.open(assets / "things" / t["file"]).size) for t in manifest["things"]]

    def blocked(x, y):
        return any(s["x0"] < x + 1 and x < s["x1"] and s["y0"] < y + 1 and y < s["y1"] for s in solids)

    def covered(cx, cy, z):
        sx = manifest["ox"] + (cx - cy) * HW
        sy = manifest["oy"] + (cx + cy) * HH - z
        for t, alpha, size in things:
            if cx < t["x1"] and cy < t["y1"] and not (cx >= t["x0"] and cy >= t["y0"]):
                px, py = int(sx - t["px"]), int(sy - t["py"])
                if 0 <= px < size[0] and 0 <= py < size[1] and alpha[px, py] > 128:
                    return True
        return False

    full = feet = 0
    print("# gesperrt, . frei und sichtbar, f Füße verdeckt, X Figur ganz verdeckt")
    for y in range(h):
        row = ""
        for x in range(w):
            if blocked(x, y):
                row += "#"
                continue
            low, high = covered(x + 0.5, y + 0.5, 2), covered(x + 0.5, y + 0.5, FIGURE - 1)
            row += "X" if low and high else "f" if low else "."
            full += low and high
            feet += low and not high
        print(f"{y:3d} {row}")
    print(f"{full} freie Felder ganz verdeckt, {feet} teilweise")


if __name__ == "__main__":
    main()
