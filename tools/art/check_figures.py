"""Prüft das Figurenblatt und legt Bildfolgen zum Ansehen ab.

Aufruf: python tools/art/check_figures.py <ordner>

1. Gleiten: Für Gehen, Rennen und zielend Gehen wird am Modell gemessen, wie weit ein Fuß am Boden wandert,
   wenn die Figur so vorankommt, wie das Spiel sie mit der Schrittlänge aus figures.json bewegt. Null heißt:
   Der Fuß steht still auf dem Pflaster.
2. Rand: Zellen, in denen die Figur an den Rand stößt und abgeschnitten würde.
3. Bildfolgen: je Figur ein Kontaktbogen und je Bewegung ein GIF aus dem fertigen Blatt.
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
import figures3d as f  # noqa: E402

ASSETS = f.ROOT / "game" / "assets"
Image.MAX_IMAGE_PIXELS = None  # Das Blatt ist groß, aber unser eigenes.
CONTACT = 0.25  # So nah am Boden gilt ein Schuh oder eine Pfote als aufgesetzt, darüber hebt er gerade ab.


def slip(name, motion, meta):
    """Größte Wanderung eines aufgesetzten Fußes über aufeinanderfolgende Bilder, in Weltpixeln."""
    spec = f.SPECS[name]
    start, n = meta["columns"][motion]
    stride = meta["stride"][name][motion] / f.WORLD_PER_UNIT
    worst = 0.0
    previous = {}
    for k in range(n + 1):
        i = k % n
        travel = stride * k / n
        parts = f.build(spec, motion, i, n).parts
        contacts = {}
        for index, part in enumerate(parts):
            if part[4] == f.SHOE and f.part_low(part) < CONTACT:
                a, b = part[0], part[1]
                contacts[index] = max(a[0], b[0]) + travel
        for index, x in contacts.items():
            if index in previous:
                worst = max(worst, abs(x - previous[index]))
        previous = contacts
    return worst


def cell(sheet, meta, name, view, column):
    """Eine Zelle in Farbe. Das Blatt enthält Nummern, die Farben stehen je Figur in einer Zeile der Palette."""
    numbers, palette = sheet
    w, h = meta["w"], meta["h"]
    figure = meta["names"].index(name)
    row = figure * meta["views"] + view
    return Image.fromarray(palette[figure][numbers[row * h:(row + 1) * h, column * w:(column + 1) * w]], "RGBA")


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("figurenpruefung")
    out.mkdir(parents=True, exist_ok=True)
    meta = json.loads((ASSETS / "figures.json").read_text(encoding="utf-8"))
    data = np.array(Image.open(ASSETS / "figures.png"))
    sheet = (data, np.array(Image.open(ASSETS / "figures_palette.png").convert("RGBA")))

    print("Gleiten (Weltpixel, höchstens):")
    failed = 0
    for name in meta["names"]:
        values = {motion: slip(name, motion, meta) for motion in meta["stride"][name]}
        print(f"  {name:13s} " + "  ".join(f"{m} {v:.2f}" for m, v in values.items()))
        failed += sum(v > 0.5 for v in values.values())

    print("Abgeschnitten:")
    w, h = meta["w"], meta["h"]
    clipped = []
    for r in range(data.shape[0] // h):
        for c in range(data.shape[1] // w):
            numbers = data[r * h:(r + 1) * h, c * w:(c + 1) * w]
            block = (numbers > 0) & (numbers <= f.FIGURE_COLORS)
            if block[:, 0].any() or block[:, -1].any() or block[0, :].any():
                name = meta["names"][r // meta["views"]]
                motion = next(m for m, (s, k) in meta["columns"].items() if s <= c < s + k)
                clipped.append(f"{name} Blick {r % meta['views']} {motion} {c - meta['columns'][motion][0]}")
    print("  keine" if not clipped else "\n".join("  " + x for x in clipped))

    for name in meta["names"]:
        rows = []
        for view in range(meta["views"]):
            rows.append([cell(sheet, meta, name, view, c) for c in range(data.shape[1] // w)])
        board = Image.new("RGBA", (len(rows[0]) * w, len(rows) * h), (70, 72, 74, 255))
        draw = ImageDraw.Draw(board)
        for y, row in enumerate(rows):
            for x, image in enumerate(row):
                board.alpha_composite(image, (x * w, y * h))
        for motion, (s, k) in meta["columns"].items():
            draw.line([(s * w, 0), (s * w, board.height)], fill=(140, 140, 90))
            draw.text((s * w + 3, 3), motion, fill=(255, 255, 160))
        board.save(out / f"blatt_{name}.png")
        civil = name in meta["civil"]["names"]
        for motion, (s, k) in (meta["civil"]["columns"] if civil else meta["columns"]).items():
            if not civil and not f.applies(f.SPECS[name], motion):
                continue
            frames = []
            for c in range(s, s + k):
                strip = Image.new("RGBA", (w * 3, h), (70, 72, 74, 255))
                for j, view in enumerate((0, 2, 4)):
                    strip.alpha_composite(cell(sheet, meta, name, view, c), (j * w, 0))
                frames.append(strip.resize((w * 6, h * 2), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
            duration = 600 if motion.startswith(("idle", "look")) else 120 if motion.startswith("fall") else 80
            frames[0].save(out / f"{name}_{motion}.gif", save_all=True, append_images=frames[1:], duration=duration, loop=0)
    print(f"Bildfolgen in {out}")
    sys.exit(1 if failed or clipped else 0)


if __name__ == "__main__":
    main()
