"""Schreibt aus dem Grundriss data/scene_<nr>.json die Regelkarte data/map_<nr>.json und prüft, was die Kamera verdeckt.

Aufruf: python tools/level/scene_to_map.py 02
Vorläufig, bis generate_concept.py die Karte je Szene selbst schreibt.
"""
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HH = 16  # halbe Feldhöhe im Bild, wie in generate_concept.py
FIGURE = 40  # Höhe einer Figur im Bild


def main():
    nr = sys.argv[1] if len(sys.argv) > 1 else "02"
    scene = json.loads((ROOT / "data" / f"scene_{nr}.json").read_text(encoding="utf-8"))
    w, h = scene["width"], scene["height"]
    solids = [{"x0": b["x0"], "y0": b["y0"], "x1": b["x1"], "y1": b["y1"], "opaque": b.get("opaque", True)}
              for b in scene["buildings"] + scene["walls"]]
    for p in scene["props"]:
        if p["solid"]:
            x, y = int(p["x"]), int(p["y"])
            solids.append({"x0": x + 0.3, "y0": y + 0.3, "x1": x + 0.7, "y1": y + 0.7, "opaque": p["opaque"]})
    if "--check" not in sys.argv:
        (ROOT / "data" / f"map_{nr}.json").write_text(json.dumps({"width": w, "height": h, "solids": solids}, indent=1), encoding="utf-8")

    blocked = [[False] * w for _ in range(h)]
    for s in solids:
        for y in range(int(s["y0"]), math.ceil(s["y1"])):
            for x in range(int(s["x0"]), math.ceil(s["x1"])):
                blocked[y][x] = True
    tall = [(b["x0"], b["y0"], b["x1"], b["y1"], b["height"]) for b in scene["buildings"] + scene["walls"]]

    def hidden(x, y):
        """0 sichtbar, 1 nur die Füße verdeckt, 2 auch der Kopf einer Figur."""
        worst = 0
        for x0, y0, x1, y1, height in tall:
            # Ein Punkt in Höhe z über dem Feld liegt im Bild dort, wo der Boden bei (x + k, y + k) liegt, k = z / (2 * HH).
            # Verdeckt ist er, wenn ein Haus davor steht, das dort noch höher ist.
            for z, level in ((0, 1), (FIGURE, 2)):
                k = 0.25
                while k <= (height - z) / (2 * HH) + 1e-9:
                    if x0 <= x + 0.5 + k < x1 and y0 <= y + 0.5 + k < y1:
                        worst = max(worst, level)
                        break
                    k += 0.25
        return worst

    print("# gesperrt, . frei und sichtbar, f Füße verdeckt, X Figur ganz verdeckt")
    print("    " + "".join(str(x % 10) for x in range(w)))
    full = feet = 0
    for y in range(h):
        row = ""
        for x in range(w):
            if blocked[y][x]:
                row += "#"
            else:
                v = hidden(x, y)
                full += v == 2
                feet += v == 1
                row += ".fX"[v]
        print(f"{y:3} {row}")
    print(f"{full} freie Felder ganz verdeckt, {feet} teilweise")


if __name__ == "__main__":
    main()
