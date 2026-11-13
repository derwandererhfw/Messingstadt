"""Erweitert den Grundriss des Nordwalls von 20 × 20 auf 30 × 30 Felder: „Der lange Rückweg“.

Aufruf: python tools/level/grow_nordwall.py
Liest data/scene_02.json und schreibt es neu. Der Kern (y 0 bis 12, x 0 bis 19) bleibt Feld für Feld, wie er ist.
Das Skript ist wiederholbar: Es wirft alles weg, was es selbst angelegt hat, und baut es neu.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FILE = ROOT / "data" / "scene_02.json"
SIZE = 30
KEEP_BUILDINGS = {"stadtwall", "amtsgericht", "wachstube", "wachlokal", "kaserne", "beamtenhaus_ost", "beamtenhaus_a"}
KEEP_WALLS = {"hofmauer_west_nord", "hofmauer_west_sued", "hofmauer_ost"}
KEEP_PROPS = {"kohlenkiste"}
KEEP_DOORS = {"Pforte zur Gerichtsgasse", "Hoftor", "Zelle 1", "Zelle 2", "Schreibstube"}
KEEP_STREETS = {"wallstrasse", "gerichtsgasse", "arresthof", "postengang"}
KEEP_GATES = {"pforte"}


def wall_run(name, kind, height, tiles, doors):
    """Mauer aus einzelnen Feldern, an den Türfeldern unterbrochen. Zusammenhängende Stücke werden zu Rechtecken."""
    tiles = [t for t in tiles if t not in doors]
    pieces, run = [], []
    for t in tiles:
        if run and (abs(t[0] - run[-1][0]) + abs(t[1] - run[-1][1])) != 1:
            pieces.append(run)
            run = []
        run.append(t)
    if run:
        pieces.append(run)
    out = []
    for i, piece in enumerate(pieces):
        xs, ys = [t[0] for t in piece], [t[1] for t in piece]
        out.append({"id": f"{name}_{i + 1}", "kind": kind, "x0": min(xs), "y0": min(ys), "x1": max(xs) + 1, "y1": max(ys) + 1, "height": height, "opaque": True})
    return out


def main():
    scene = json.loads(FILE.read_text(encoding="utf-8"))
    scene["width"] = scene["height"] = SIZE
    scene["note"] = ("Grundriss von Einsatz 2, „Der lange Rückweg“, 30 × 30 Felder. Der Kern von 20 × 20 (Wall, Wachstube, Arresthof, Wachlokal, "
                     "Amtsgericht, Kaserne, Gerichtsgasse, Wallstraße, Pforte, Hoftor, Zellen, Beamtenhaus Ost mit dem Durchgang) liegt unverändert oben links. "
                     "Neu: Friedhof der Namenlosen im Westen, Beamtenviertel mit zwei Hinterhöfen im Süden, Exerzierplatz und Remise im Osten. "
                     "Felder wie in map_01.json, Norden ist y = 0. Rechtecke gelten von x0,y0 bis ausschließlich x1,y1. Höhen in Pixeln wie in generate_concept.py. "
                     "Regel für alle Karten: Zielorte, Türen mit Spielbedeutung und Hauptwege liegen nie hinter hohen Gebäuden. "
                     "Erzeugt von tools/level/grow_nordwall.py, Abweichungen und Orte in docs/einsatz_02_grundriss.md.")

    buildings = [b for b in scene["buildings"] if b["id"] in KEEP_BUILDINGS]
    for b in buildings:
        if b["id"] == "stadtwall":
            b["x1"] = SIZE
        if b["id"] == "kaserne":
            b["text"] = "Mannschaftshaus der Stadtwache, einige Fenster hell. Die Kasernentür geht nach Osten auf den Exerzierplatz, von dort kommt die Verstärkung"
            b["doors"] = [{"side": "ost", "at": 2.5, "kind": "kasernentuer", "door": ""}]
        if b["id"] in ("beamtenhaus_ost", "beamtenhaus_a"):
            b["id"] = "beamtenhaus_a"
            b["text"] = ("Zweigeschossiges Mietshaus der Beamten, davor ein Feld Vorgarten. Westlich der dunkle Durchgang zur Wallstraße, "
                         "nach Süden der Hinterhof. Im Erdgeschoss zum Hof wohnt die Witwe eines Wachmanns, ihr Fenster mit Kerze")
            b["doors"] = [{"side": "sued", "at": 2.0, "kind": "hoftuer_haus", "door": ""}]
    buildings += [
        {"id": "beamtenhaus_b", "kind": "mietshaus", "x0": 17, "y0": 14, "x1": 21, "y1": 18, "height": 84, "roof": "mansarde", "lit": 0.3,
         "text": "Zweites Mietshaus, hier wohnen Wachleute. Zum Hof hin die Wohnung der Frau eines Wachmanns",
         "doors": [{"side": "sued", "at": 2.0, "kind": "hoftuer_haus", "door": ""}]},
        {"id": "hinterhaus", "kind": "hinterhaus", "x0": 9, "y0": 22, "x1": 13, "y1": 26, "height": 48, "roof": "pult", "lit": 0.15,
         "text": "Niedriges Hinterhaus mit Waschküche und Remisen, schließt den Westhof nach Süden. Haustür zur Südgasse"},
        {"id": "kneipe", "kind": "kneipe", "x0": 17, "y0": 22, "x1": 20, "y1": 26, "height": 48, "roof": "sattel", "lit": 0.5, "name": "ZUM WALLGRABEN",
         "text": "Eckkneipe der Wachleute und Beamten, Tür und Laterne zur Südgasse. An der Ostseite der Kellerhals mit der Luke zum Osthof",
         "doors": [{"side": "sued", "at": 1.5, "kind": "haupttuer", "door": ""}]},
        {"id": "remise", "kind": "remise", "x0": 24, "y0": 14, "x1": 29, "y1": 18, "height": 56, "roof": "pult", "lit": 0.0,
         "text": "Offener Schuppen der Wache am Rand des Kohlenhofs, darin der graue, vergitterte Gefangenenwagen als Requisit. Öffnung nach Süden"},
        {"id": "totengraeberhuette", "kind": "huette", "x0": 0, "y0": 14, "x1": 2, "y1": 16, "height": 40, "roof": "pult", "lit": 0.0,
         "text": "Bretterhütte des Totengräbers in der Nordwestecke des Friedhofs, ein kleines Fenster nach Süden"},
    ]
    scene["buildings"] = buildings

    doors = [d for d in scene["doors"] if d["name"] in KEEP_DOORS]
    new_doors = [
        {"name": "Friedhofspforte", "tiles": [[4, 13]], "mark": [4.5, 12.5], "note": "gegenüber der Gerichtsgasse, an der Wallstraße"},
        {"name": "Pforte des Totengräbers", "tiles": [[5, 17]], "mark": [6.5, 17.5], "helper": "totengraeber",
         "note": "vom dunklen Durchgang auf den Friedhof. Steht offen, wenn der Totengräber hilft"},
        {"name": "Südpforte des Friedhofs", "tiles": [[2, 25]], "mark": [2.5, 26.5], "note": "hinaus auf die Südgasse"},
        {"name": "Hoftür der Witwe", "tiles": [[8, 20]], "mark": [7.5, 20.5], "helper": "witwe",
         "note": "vom Durchgang in den Westhof. Steht offen, wenn die Witwe hilft"},
        {"name": "Hoftür am Osthof", "tiles": [[16, 20]], "mark": [15.5, 20.5], "note": "von der Mittelgasse in den Osthof"},
        {"name": "Kellerluke der Kneipe", "tiles": [[20, 22]], "mark": [20.5, 21.5], "helper": "wirtin",
         "note": "vom Osthof durch den Kellerhals zur Südgasse. Steht offen, wenn die Wirtin hilft: nur wenn Greta lebt oder in Einsatz 1 niemand getötet wurde"},
    ]
    scene["doors"] = doors + new_doors
    door_tiles = {tuple(d["tiles"][0]) for d in new_doors}

    walls = [w for w in scene["walls"] if w["id"] in KEEP_WALLS]
    walls += wall_run("friedhofsmauer_nord", "friedhofsmauer", 30, [(x, 13) for x in range(0, 6)], door_tiles)
    walls += wall_run("friedhofsmauer_ost", "friedhofsmauer", 30, [(5, y) for y in range(14, 25)], door_tiles)
    walls += wall_run("friedhofsmauer_sued", "friedhofsmauer", 30, [(x, 25) for x in range(0, 6)], door_tiles)
    walls += wall_run("hofmauer_westhof", "hofmauer", 34, [(8, y) for y in range(18, 22)], door_tiles)
    walls += wall_run("hofmauer_mittelgasse", "hofmauer", 34, [(x, 22) for x in range(13, 17)], door_tiles)
    walls += wall_run("hofmauer_osthof", "hofmauer", 34, [(16, y) for y in range(18, 22)], door_tiles)
    walls += wall_run("hofmauer_ost_aussen", "hofmauer", 34, [(21, y) for y in range(18, 26)], door_tiles)
    # Bretterzaun quer durch den Kohlenhof: Der Posten an der Remise sieht den Wagen dahinter nicht.
    walls += wall_run("bretterzaun", "bretterzaun", 34, [(x, 20) for x in range(24, 30)], door_tiles)
    scene["walls"] = walls

    gates = [g for g in scene.get("gates", []) if g["id"] in KEEP_GATES]
    kinds = {"Friedhofspforte": "gittertor", "Pforte des Totengräbers": "holzpforte", "Südpforte des Friedhofs": "gittertor",
             "Hoftür der Witwe": "holztor", "Hoftür am Osthof": "holztor", "Kellerluke der Kneipe": "kellerluke"}
    for d in new_doors:
        x, y = d["tiles"][0]
        gates.append({"id": "tor_" + d["name"].lower().replace(" ", "_").replace("ü", "ue").replace("ä", "ae"), "door": d["name"], "kind": kinds[d["name"]],
                      "x0": x, "y0": y, "x1": x + 1, "y1": y + 1, "height": 24,
                      "text": "Lücke in der Mauer mit niedrigem Tor, von oben als Lücke zu erkennen. Blickdicht für die Regeln, gesperrt über die Tür der Mission."})
    scene["gates"] = gates

    props = [p for p in scene["props"] if p["id"] in KEEP_PROPS]
    props += [
        {"id": "gefangenenwagen", "kind": "gefangenenwagen", "x": 26.5, "y": 16, "solid": False, "opaque": False, "note": "steht in der Remise, nur Bild"},
        {"id": "waeschereiwagen", "kind": "fluchtwagen", "x": 28, "y": 23.5, "solid": False, "opaque": False, "note": "Wagen der Wäscherei im Kohlenhof, Ziel"},
        {"id": "telefon", "kind": "telefon", "x": 13.5, "y": 9.3, "solid": False, "opaque": False, "note": "Apparat in der Schreibstube, an der Wand zur Kaserne"},
        {"id": "brunnen", "kind": "brunnen", "x": 10.5, "y": 19.5, "solid": True, "opaque": False, "note": "Pumpe im Westhof, Deckung"},
        {"id": "faesser", "kind": "kiste", "x": 19.5, "y": 18.5, "solid": True, "opaque": False, "note": "Bierfässer im Osthof, Deckung"},
        {"id": "kohlenhaufen", "kind": "kiste", "x": 25.5, "y": 23.5, "solid": True, "opaque": False, "note": "Kohlenhaufen im Kohlenhof, Deckung vor dem Wagen"},
        {"id": "fahnenmast", "kind": "mast", "x": 25.5, "y": 6.5, "solid": True, "opaque": False, "note": "Mitte des Exerzierplatzes"},
        {"id": "baum_1", "kind": "baum", "x": 1.5, "y": 18.5, "solid": True, "opaque": False, "note": "kahle Linde auf dem Friedhof"},
        {"id": "baum_2", "kind": "baum", "x": 3.5, "y": 21.5, "solid": True, "opaque": False},
        {"id": "baum_3", "kind": "baum", "x": 2.5, "y": 23.5, "solid": True, "opaque": False},
    ]
    for i, (x, y) in enumerate([(0.5, 17.5), (2.5, 17.5), (3.5, 15.5), (0.5, 20.5), (2.5, 20.5), (4.5, 19.5), (1.5, 22.5), (4.5, 23.5), (0.5, 24.5)]):
        props.append({"id": f"grabkreuz_{i + 1}", "kind": "grabkreuz", "x": x, "y": y, "solid": False, "opaque": False,
                      "note": "Holzkreuz ohne Namen, nur eine Nummer" if i == 0 else ""})
    scene["props"] = props

    lamps = [lamp for lamp in scene["lamps"] if lamp["x"] < 20 and lamp["y"] < 13]
    lamps += [
        {"x": 22.5, "y": 10.5, "group": "wallstrasse"}, {"x": 28.5, "y": 10.5, "group": "wallstrasse"},
        {"x": 21.5, "y": 3.5, "group": "exerzierplatz", "kind": "bogenlampe"}, {"x": 28.5, "y": 3.5, "group": "exerzierplatz", "kind": "bogenlampe"},
        {"x": 21.5, "y": 8.5, "group": "exerzierplatz", "kind": "bogenlampe"}, {"x": 28.5, "y": 8.5, "group": "exerzierplatz", "kind": "bogenlampe"},
        {"x": 19.3, "y": 5.5, "group": "kaserne", "kind": "wandlampe"},
        {"x": 18.5, "y": 26.3, "group": "kneipe", "kind": "wandlampe"},
        {"x": 26.5, "y": 18.3, "group": "remise", "kind": "wandlampe"},
        {"x": 8.5, "y": 26.6, "group": "suedgasse"},
    ]
    scene["lamps"] = lamps

    streets = [s for s in scene["streets"] if s["id"] in KEEP_STREETS]
    for s in streets:
        if s["id"] == "wallstrasse":
            s["x1"] = SIZE
    streets += [
        {"id": "durchgang", "x0": 6, "y0": 13, "x1": 9, "y1": 18, "kind": "gasse", "note": "dunkler Durchgang zwischen Friedhofsmauer und Beamtenhaus, wie bisher"},
        {"id": "friedhofsgang", "x0": 6, "y0": 18, "x1": 8, "y1": 26, "kind": "gasse", "note": "schmal, an der Friedhofsmauer entlang zur Südgasse"},
        {"id": "friedhof", "x0": 0, "y0": 14, "x1": 5, "y1": 25, "kind": "erde", "note": "Friedhof der Namenlosen: flach, kein Licht, Kreuze mit Nummern, drei kahle Bäume"},
        {"id": "mittelgasse", "x0": 13, "y0": 13, "x1": 17, "y1": 22, "kind": "gasse", "note": "zwischen den Beamtenhäusern, im Süden zugemauert, nach Westen offen zum Westhof"},
        {"id": "westhof", "x0": 9, "y0": 18, "x1": 13, "y1": 22, "kind": "hof", "note": "Hinterhof mit Pumpe und Wäscheleinen"},
        {"id": "osthof", "x0": 17, "y0": 18, "x1": 21, "y1": 22, "kind": "hof", "note": "Hinterhof der Kneipe mit Fässern"},
        {"id": "kellerhals", "x0": 20, "y0": 22, "x1": 21, "y1": 26, "kind": "gasse", "note": "ein Feld breit, zwischen Kneipe und Außenmauer"},
        {"id": "holzplatz", "x0": 13, "y0": 23, "x1": 17, "y1": 26, "kind": "hof", "note": "Sackgasse zur Südgasse, Versteck"},
        {"id": "suedgasse", "x0": 0, "y0": 26, "x1": 30, "y1": 28, "kind": "kopfsteinpflaster", "note": "Wohnstraße, kaum Licht"},
        {"id": "boeschung", "x0": 0, "y0": 28, "x1": 30, "y1": 30, "kind": "erde", "note": "Böschung zum alten Wallgraben, begehbar, dunkel"},
        {"id": "ostgasse", "x0": 22, "y0": 13, "x1": 24, "y1": 26, "kind": "gasse", "note": "von der Wallstraße zum Kohlenhof"},
        {"id": "kohlenhof", "x0": 24, "y0": 18, "x1": 30, "y1": 26, "kind": "hof", "note": "offener Hof hinter der Remise, hier wartet der Wagen"},
        {"id": "exerzierplatz", "x0": 20, "y0": 2, "x1": 30, "y1": 10, "kind": "kies", "note": "weit, leer, von vier Bogenlampen grell beleuchtet"},
    ]
    scene["streets"] = streets

    marks = [m for m in scene["marks"] if m["kind"] in ("gefangener", "beute")]
    marks += [
        {"kind": "start", "x0": 9, "y0": 29, "x1": 13, "y1": 30},
        {"kind": "ziel", "x0": 27, "y0": 22, "x1": 30, "y1": 25},
        {"kind": "verstaerkung", "x": 19.5, "y": 5.5, "note": "Kasernentür. Kommt nur, wenn das Telefon in der Schreibstube durchkommt"},
        {"kind": "telefon", "x": 13.5, "y": 9.5, "note": "Apparat in der Schreibstube"},
        {"kind": "draht", "points": [[13.9, 9.4], [14.5, 9.0], [14.5, 8.3], [15.0, 7.8]],
         "note": "Telefondraht von der Schreibstube über den Postengang zur Kaserne, auf Kopfhöhe an Haken. Stelle zum Kappen bei (14,5, 8,5)"},
        {"kind": "helferfenster", "helper": "witwe", "x": 10.5, "y": 18.0, "note": "Erdgeschoss Beamtenhaus A zum Westhof, Kerze im Fenster"},
        {"kind": "helferfenster", "helper": "totengraeber", "x": 1.0, "y": 16.0, "note": "Hütte des Totengräbers, Kerze im Fenster"},
        {"kind": "helferfenster", "helper": "wirtin", "x": 20.0, "y": 24.0, "note": "Ostwand der Kneipe über dem Kellerhals, Kerze im Fenster"},
        {"kind": "bewohner", "who": "gerichtsdiener", "x": 3.0, "y": 9.5, "helps": "nie", "note": "wohnt im Amtsgericht, Portal"},
        {"kind": "bewohner", "who": "frau_des_wachmanns", "x": 19.0, "y": 18.2, "helps": "nie", "note": "Beamtenhaus B zum Osthof, die Frau eines Wachmanns aus dem Totenbuch"},
        {"kind": "bewohner", "who": "kasernenkoch", "x": 17.0, "y": 8.3, "helps": "nie", "note": "Küchentür der Kaserne nach Süden"},
    ]
    scene["marks"] = marks

    FILE.write_text(json.dumps(scene, indent=1, ensure_ascii=False), encoding="utf-8")
    print(f"{FILE.name}: {SIZE} × {SIZE}, {len(buildings)} Gebäude, {len(walls)} Mauerstücke, {len(scene['doors'])} Türen")


if __name__ == "__main__":
    main()
