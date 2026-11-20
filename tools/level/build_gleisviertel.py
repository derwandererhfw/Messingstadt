"""Schreibt den Grundriss von Einsatz 3, Güterbahnhof Ost im Gleisviertel, nach data/scene_03.json.

Aufruf: python tools/level/build_gleisviertel.py   (danach python tools/level/scene_to_map.py 03)
Der Grundriss ist die Vorlage für Regelkarte, Mission, Story und Grafik. Orte und Gründe stehen in docs/einsatz_03_grundriss.md.
Kameraregel: Hohes steht am Nordrand, wichtige Türen gehen nach Süden, nichts Wichtiges liegt hinter hohen Gebäuden.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
W = H = 30
TRACKS = range(9, 16)  # sieben Gleise, y 9 bis 15, von Ost nach West durch das ganze Bild


def rect(id_, kind, x0, y0, x1, y1, height, **more):
    return {"id": id_, "kind": kind, "x0": x0, "y0": y0, "x1": x1, "y1": y1, "height": height, **more}


buildings = [
    rect("bahndamm", "bahndamm", 0, 0, 30, 2, 56, roof="flach", lit=0.0,
         text="Stützmauer aus Klinker unter dem Damm der Hauptstrecke, oben ein Geländer und Signalmasten. Schließt das Bild nach Norden"),
    rect("gueterschuppen", "gueterschuppen", 5, 2, 14, 6, 70, roof="sattel", name="GÜTERABFERTIGUNG OST", lit=0.1,
         text="Langer Schuppen aus Teerholz, Laderampe nach Süden über die ganze Länge. Das Schuppentor liegt in der Mitte der Rampe"),
    rect("wasserturm", "wasserturm", 15, 2, 17, 4, 140, roof="kessel", lit=0.0,
         text="Wasserturm auf vier Beinen, das Höchste im Bild, deshalb am Nordrand. Unten offen gezeichnet, für die Regeln gesperrt"),
    rect("stellwerk", "stellwerk", 19, 2, 23, 6, 90, roof="walm", name="STELLWERK OST", lit=0.6,
         text="Ziegelsockel, oben die Glaskanzel mit Licht. Tür nach Süden, die Hebelbank steht gleich dahinter"),
    rect("kohlenbansen_west", "kohlenbansen", 0, 19, 3, 24, 36, roof="offen", lit=0.0,
         text="Bansen aus Schwellenholz mit Kohlenhaufen, niedrig"),
    rect("kohlenbansen_ost", "kohlenbansen", 4, 19, 7, 24, 36, roof="offen", lit=0.0,
         text="Zweiter Bansen, dazwischen eine Gasse für die Karren"),
    rect("baracke_a", "baracke", 11, 19, 15, 22, 38, roof="flach", lit=0.2,
         text="Schlafbaracke der Rangierer, ein Geschoss, Ofenrohr. Tür und Fenster nach Süden zum Barackenhof"),
    rect("baracke_b", "baracke", 19, 19, 24, 22, 38, roof="flach", lit=0.2,
         text="Schlafbaracke der Heizer und Putzer, ein Geschoss. Tür und Fenster nach Süden"),
    rect("speisebaracke", "baracke", 14, 25, 20, 28, 38, roof="flach", lit=0.0,
         text="Speisebaracke, um diese Zeit dunkel. Schließt den Barackenhof nach Süden"),
    rect("waerterbude", "huette", 28, 18, 30, 20, 34, roof="pult", lit=0.3,
         text="Bude des Streckenwärters am Ostende des Zauns, ein Fenster nach Westen"),
]

# Abgestellte Güterwagen: sperren Weg und Sicht. Gleis 3 (y 11) bleibt ganz frei, dort läuft der Hund die Länge des Bahnhofs ab.
WAGONS = [(9, 5, 10), (9, 13, 15), (10, 16, 19), (12, 7, 11), (12, 23, 26), (13, 14, 18), (14, 3, 6), (14, 21, 24), (15, 9, 13), (15, 26, 28)]
walls = [rect(f"wagen_{y}_{x0}", "gueterwagen", x0, y, x1, y + 1, 52, opaque=True) for y, x0, x1 in WAGONS]
# Bretterzaun zwischen Gleisfeld und Baracken, mit zwei Pforten. Westlich davon bleibt der Kohlenweg offen.
walls += [
    rect("zaun_1", "bretterzaun", 10, 17, 16, 18, 34, opaque=True),
    rect("zaun_2", "bretterzaun", 17, 17, 25, 18, 34, opaque=True),
    rect("zaun_3", "bretterzaun", 26, 17, 30, 18, 34, opaque=True),
    rect("zaun_kohle", "bretterzaun", 0, 17, 7, 18, 34, opaque=True),
    # Niedrige Anbauten in den Hofecken am Nordrand: Dort stünde eine Figur ganz hinter Schuppen, Turm oder Stellwerk.
    rect("holzlege", "holzstapel", 4, 2, 5, 5, 24, opaque=False),
    rect("turmkasten", "holzstapel", 14, 2, 15, 3, 24, opaque=False),
    rect("kohlenkasten", "kohlenkasten", 17, 2, 19, 5, 24, opaque=False),
]

gates = [
    {"id": "tor_stellwerk", "door": "Stellwerk", "kind": "eisentuer", "x0": 21, "y0": 4, "x1": 22, "y1": 6, "height": 40,
     "text": "Eisentür im Ziegelsockel, nach Süden"},
    {"id": "tor_schuppen", "door": "Schuppentor", "kind": "schiebetor", "x0": 9, "y0": 4, "x1": 10, "y1": 6, "height": 44,
     "text": "Schiebetor aus Brettern in der Mitte der Rampe"},
    {"id": "tor_rangierer", "door": "Zaunpforte der Rangierer", "kind": "holzpforte", "x0": 16, "y0": 17, "x1": 17, "y1": 18, "height": 24,
     "text": "Lücke im Bretterzaun mit niedriger Pforte, von oben als Lücke zu erkennen"},
    {"id": "tor_heizer", "door": "Zaunpforte der Heizer", "kind": "holzpforte", "x0": 25, "y0": 17, "x1": 26, "y1": 18, "height": 24,
     "text": "Zweite Pforte am Ostende, kurzer Weg zur Einfahrweiche"},
]

doors = [
    {"name": "Stellwerk", "tiles": [[21, 5], [21, 4]], "mark": [21.5, 6.5]},
    {"name": "Schuppentor", "tiles": [[9, 5], [9, 4]], "mark": [9.5, 6.5]},
    {"name": "Zaunpforte der Rangierer", "tiles": [[16, 17]], "mark": [16.5, 18.5], "helper": "rangierer"},
    {"name": "Zaunpforte der Heizer", "tiles": [[25, 17]], "mark": [25.5, 18.5], "helper": "heizer"},
]

props = [
    {"id": "hebelbank", "kind": "hebelbank", "x": 21.5, "y": 4.5, "solid": False, "opaque": False, "note": "Sabotageziel im Stellwerk, 6 s"},
    {"id": "weichenbock", "kind": "weichenbock", "x": 27.5, "y": 10.5, "solid": False, "opaque": False, "note": "Sabotageziel am Ostende, 4 s, im Licht der Weichenlaterne"},
    {"id": "signal", "kind": "signal", "x": 16.5, "y": 8.2, "solid": True, "opaque": False, "note": "Formsignal an der Ladestraße, die Aufsicht steht daneben"},
    {"id": "schranke", "kind": "schranke", "x": 3.5, "y": 8.5, "solid": False, "opaque": False, "note": "Schrankenbaum oben, hier wartet der Wagen"},
    {"id": "fluchtwagen", "kind": "fluchtwagen", "x": 1.5, "y": 7.5, "solid": False, "opaque": False, "note": "Lieferwagen am Bahnübergang, Ziel"},
    {"id": "lohnkasse", "kind": "kasse", "x": 9.5, "y": 4.5, "solid": False, "opaque": False, "note": "Lohngelder der Bahnmeisterei im Schuppen"},
    {"id": "wasserkran", "kind": "wasserkran", "x": 24.5, "y": 8.5, "solid": True, "opaque": False, "note": "Wasserkran östlich des Stellwerks, Deckung"},
    {"id": "prellbock_1", "kind": "prellbock", "x": 29.5, "y": 15.5, "solid": True, "opaque": False, "note": "Ende von Gleis 7"},
    {"id": "prellbock_2", "kind": "prellbock", "x": 0.5, "y": 14.5, "solid": True, "opaque": False, "note": "Stumpfgleis im Westen"},
    {"id": "schwellenstapel_1", "kind": "schwellenstapel", "x": 8.5, "y": 16.5, "solid": True, "opaque": False, "note": "Deckung am Kohlenweg"},
    {"id": "schwellenstapel_2", "kind": "schwellenstapel", "x": 26.5, "y": 8.5, "solid": True, "opaque": False, "note": "Deckung auf der Ladestraße vor der Weiche"},
    {"id": "karren", "kind": "karren", "x": 12.5, "y": 7.5, "solid": True, "opaque": False, "note": "Gepäckkarren auf der Ladestraße"},
    {"id": "kisten", "kind": "kiste", "x": 6.5, "y": 6.5, "solid": True, "opaque": False, "note": "Kistenstapel auf der Rampe"},
    {"id": "feuertonne", "kind": "feuertonne", "x": 16.5, "y": 23.5, "solid": True, "opaque": False, "note": "Feuertonne im Barackenhof, nur Glut"},
    {"id": "pumpe", "kind": "brunnen", "x": 9.5, "y": 24.5, "solid": True, "opaque": False, "note": "Pumpe am Kohlenweg"},
]

lamps = [
    {"x": 9.5, "y": 6.6, "group": "rampe", "kind": "bogenlampe"},
    {"x": 21.5, "y": 6.8, "group": "stellwerk", "kind": "wandlampe"},
    {"x": 27.5, "y": 10.5, "group": "weiche"},
    {"x": 16.5, "y": 8.2, "group": "signal", "kind": "wandlampe"},
    {"x": 3.5, "y": 8.5, "group": "schranke", "kind": "wandlampe"},
]

streets = [
    {"id": "gleisfeld", "x0": 0, "y0": 9, "x1": 30, "y1": 16, "kind": "schotter", "rails": True,
     "note": "Sieben Gleise bei y 9 bis 15. Schotter: Schritte sind lauter"},
    {"id": "bahnuebergang", "x0": 0, "y0": 9, "x1": 3, "y1": 16, "kind": "bohlen", "note": "Bohlen über alle Gleise am Westrand, leise"},
    {"id": "bohlenweg", "x0": 20, "y0": 9, "x1": 21, "y1": 16, "kind": "bohlen",
     "note": "Bohlenweg für die Gepäckkarren, leise, aber gerade auf die Stellwerkstür zu"},
    {"id": "ladestrasse", "x0": 0, "y0": 7, "x1": 30, "y1": 9, "kind": "kopfsteinpflaster", "note": "Zwischen Rampe und Gleisen, im Licht der Bogenlampe"},
    {"id": "rampe", "x0": 5, "y0": 6, "x1": 14, "y1": 7, "kind": "holz", "note": "Laderampe, hier geht der Schütze"},
    {"id": "bahnmeisterhof", "x0": 0, "y0": 2, "x1": 5, "y1": 7, "kind": "hof"},
    {"id": "turmhof", "x0": 14, "y0": 2, "x1": 19, "y1": 7, "kind": "hof"},
    {"id": "osthof", "x0": 23, "y0": 2, "x1": 30, "y1": 7, "kind": "schlacke"},
    {"id": "bahnweg", "x0": 0, "y0": 16, "x1": 30, "y1": 17, "kind": "schlacke", "note": "Dunkler Weg südlich der Gleise"},
    {"id": "kohlenweg", "x0": 7, "y0": 17, "x1": 10, "y1": 30, "kind": "schlacke", "note": "Vom Start zum Gleisfeld, immer offen"},
    {"id": "kohlenlager", "x0": 0, "y0": 18, "x1": 7, "y1": 25, "kind": "kohlenstaub"},
    {"id": "barackenhof", "x0": 10, "y0": 18, "x1": 30, "y1": 25, "kind": "erde"},
    {"id": "suedrand", "x0": 0, "y0": 25, "x1": 30, "y1": 30, "kind": "erde"},
]

marks = [
    {"kind": "start", "x0": 5, "y0": 29, "x1": 9, "y1": 30},
    {"kind": "ziel", "x0": 0, "y0": 6, "x1": 3, "y1": 9, "note": "Wagen am Bahnübergang"},
    {"kind": "sabotage", "name": "Stellwerk", "x": 21.5, "y": 4.5, "seconds": 6, "note": "Hebelbank. Der Schlosser braucht die halbe Zeit"},
    {"kind": "sabotage", "name": "Weiche", "x": 27.5, "y": 10.5, "seconds": 4, "note": "Einfahrweiche verkeilen"},
    {"kind": "beute", "name": "Lohngelder der Bahnmeisterei", "x": 9.5, "y": 4.5},
    {"kind": "verstaerkung", "x": 29.5, "y": 12.5, "note": "Werkschutz vom Ostende der Gleise"},
    {"kind": "helferfenster", "helper": "rangierer", "x": 13.0, "y": 22.0, "note": "Baracke A, Fenster nach Süden, Kerze"},
    {"kind": "helferfenster", "helper": "heizer", "x": 21.5, "y": 22.0, "note": "Baracke B, Fenster nach Süden, Kerze"},
    {"kind": "bewohner", "who": "streckenwaerter", "x": 28.0, "y": 19.0, "helps": "nie", "note": "Wärterbude, Fenster nach Westen. Er hält zur Bahn"},
    {"kind": "bewohner", "who": "bahnmeister", "x": 5.0, "y": 4.5, "helps": "nie", "note": "Schreibstube im Westgiebel des Schuppens, Fenster zum Hof am Bahnübergang"},
]

scene = {
    "id": "gleisviertel",
    "mission": "mission_03",
    "width": W,
    "height": H,
    "note": "Grundriss von Einsatz 3, Güterbahnhof Ost, 30 × 30 Felder. Norden ist y = 0. Rechtecke gelten von x0,y0 bis ausschließlich x1,y1. "
            "Höhen in Pixeln wie in generate_concept.py. Erzeugt von tools/level/build_gleisviertel.py, Orte und Gründe in docs/einsatz_03_grundriss.md.",
    "tracks": list(TRACKS),
    "buildings": buildings,
    "walls": walls,
    "gates": gates,
    "props": props,
    "doors": doors,
    "lamps": lamps,
    "streets": streets,
    "marks": marks,
}

(ROOT / "data" / "scene_03.json").write_text(json.dumps(scene, ensure_ascii=False, indent=1), encoding="utf-8")
print("data/scene_03.json geschrieben")
