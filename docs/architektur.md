# Architektur

## Grundsatz

Die Spiellogik kennt die Engine nicht. Godot zeigt an und nimmt Eingaben entgegen.

```
Eingabe (Godot) -> Befehle -> World.Tick() -> Zustand -> Darstellung (Godot)
```

## Teile

- **`Messingstadt.Core`:** reines C#, keine Godot-Typen. Fester Takt von 20 Hz, Positionen als Ganzzahlen
  (1.000 Einheiten pro Feld), eigener Zufallsgenerator. Gleicher Startwert und gleiche Befehle ergeben
  denselben Ablauf. Das prüfen die Tests über eine Prüfsumme des Zustands.
- **`data/`:** Karten und Missionen als JSON, unabhängig von der Engine.
- **`game/`:** Godot-Projekt. Zeichnet Boden, Objekte und Figuren, wandelt Klicks in Befehle um.
- **`tools/art/`:** erzeugt Boden, Objektgrafiken samt Liste (`game/assets/manifest.json`) und Figurenblatt.
  Die Liste enthält Standfläche und Zeichenreihenfolge, die Karte die gesperrten Felder. Dazu kommt die Kulisse
  `backdrop.png` jenseits der Karte. `generate_concept.py --city` lässt das langsame Figurenrendern aus.
  Das Figurenblatt kommt aus `figures3d.py`. Welche Spalten zu welcher Bewegung gehören und wie lang ein
  Doppelschritt ist, steht in `figures.json`. `game/FigureAnimator.cs` liest das und schaltet Gehen und Rennen
  nach zurückgelegtem Weg weiter, damit die Füße nicht gleiten. `check_figures.py` prüft Gleiten und Zellränder
  und legt Kontaktbögen und GIFs je Bewegung ab.
  `figures.png` enthält je Bildpunkt nur eine Palettennummer, die Farben stehen je Figur in einer Zeile von
  `figures_palette.png`. Der Figurenshader in `Main.cs` schlägt sie nach, das spart drei Viertel des Texturspeichers.
  Für Spiegelungen gibt es `figures_mirror.png`, farbig und viermal kleiner. Die vier Agenten stehen als `AGENTS` in
  `generate_concept.py`, je Waffe mit eigener Zeile. Figuren-Sprites legt man über `Main.NewFigureSprite()` an.
  Passanten, Farbvarianten und die Schnittstelle zum Zeichnen beschreibt `docs/figuren.md`.
  Die Lichtwerte in `data/light_01.json` kommen aus `bake_light.py`: Der Mond dort trifft nur Oberseiten von Objekten
  und zählt nicht fürs Schleichen.
- **Doppelte Auflösung:** `game/assets/hd/` enthält Boden und alle Objekte zusätzlich doppelt so fein,
  deckungsgleich mit den einfachen Bildern und mit schon aufgefüllten Rändern. Das Spiel zeigt sie mit halber
  Skalierung an, wie das Figurenblatt, und prüft die Verdeckung an ihnen. Welche Dateien es gibt, steht unter `hd` in
  `manifest.json`. Die Kulisse bleibt einfach aufgelöst. Die einfachen Bilder bleiben die Grundlage für Licht und
  Pfützen. Fassaden entstehen über `DualDraw` in beiden Auflösungen zugleich, der feine Boden mit numpy (`ground_np`).
  Alle Stadtbilder werden scharf-bilinear gezeichnet (`sharp_uv` im Lichtshader): innerhalb eines Grafikpunkts wie
  nearest, an seinem Rand über einen Bildschirmpunkt weich. Die Lichtkarten der Objekte (`light/<nr>_a/_b/_w.png`)
  haben die Größe der feinen Bilder: Gebacken wird in einfacher Auflösung, Selbstleuchten und Fensterkennung kommen
  Bildpunkt für Bildpunkt aus dem feinen Bild. Das Bodenlicht bleibt einfach.

- **Dächer:** Fassaden sind flache Bilder, die Dächer dagegen echte Körper (`tools/art/roofs.py`): Prismen aus
  Ebenen, exakt mit dem Sehstrahl geschnitten. Mansarde, Sattel-, Walm- und Sheddach, Gauben, Schornsteine und Luken
  entstehen so in beiden Auflösungen gleich. Das Dach gehört zum Bild seines Hauses. Aufbauten sind eigene Objekte und
  werden gegen das Dach geschnitten: Was der First verdeckt, fehlt in ihrem Bild. Die Ebenen stehen je Haus unter `roof`
  in `manifest.json`, der Lichtbaker rechnet mit denselben Flächen. Grundfläche und Höhe `z1` eines Hauses bleiben die
  des Quaders bis zur Traufe: Kollision, Sichtlinien und Schatten kennen die Dächer nicht.
- **Schrift an Häusern:** `paint_text` zeichnet Buchstaben aus Strichen, in doppelter Auflösung glatt. Damit entstehen
  gemalte Fassadenschrift, Ladenschilder, Schmierereien und die Leuchtreklame, deren Buchstaben einzeln schalten.

- **Straße und Kleinkram:** Den Boden rechnet `ground_np` in jeder Auflösung aus denselben Regeln: Fahrbahn mit
  Rinnstein, Gullys und Schienen, Gehwege aus Platten zwischen Mosaikstreifen, um jedes Haus ein Pflasterstreifen.
  Kleinkram (`street_props`) sperrt nichts: Er steht höchstens ein Viertelfeld vor einer Wand oder auf Feldern, die
  Kessel und Kisten ohnehin sperren. Der Fahrdraht ist ein Objekt ohne Breite, damit niemand eine Wand aus ihm liest.
  Durchbrochenes wie Fahrräder hat die Art `open` und wirft im Lichtbaker keinen Schatten, übriger Kleinkram (`prop`) nur im Bild.

- **Schleichlicht bleibt von der Grafik getrennt:** `data/light_01.json` rechnet der Lichtbaker in einem eigenen
  Durchgang. Dort gelten die alte Feuerstärke (`RULES_FIRE_POWER`), die Rückstrahlung aus `rules_albedo.json` und das
  Fensterlicht aus `rules_windows.json`, Kleinkram wirft dort keinen Schatten, der Mond zählt nicht. Neue Farben, Fenster
  oder gedämpftes Glühen ändern so nur das Bild. Wer das Schleichlicht bewusst ändern will, erneuert diese Dateien.

- **Stadtplan zur Einsatzwahl:** `tools/art/stadtplan.py` zeichnet `game/assets/stadtplan/plan.png` (1280 × 900) und
  je Viertel eine deckungsgleiche Maske `viertel_1.png` bis `viertel_5.png`, weiß mit Deckkraft, zum Einfärben im Code.
  `plan.json` nennt Kartenfeld, Ankerpunkte für Namen und Stempel und den Umriss jedes Viertels. Fluss, Bahn und eine
  Hauptstraße sind die Grenzen. Nachbarn teilen sich dieselben Kurvenpunkte, deshalb bleibt zwischen den Masken keine Lücke.

- **Titelbild in Ebenen:** `tools/art/title_scene.py` baut die Straße zum Werkstor aus den Fassaden des Spiels, in die
  Fluchtlinie gekippt. In `game/assets/title/` liegen `title.png` (fertig zusammengesetzt), die Ebenen
  `title_sky/_factory/_street/_left/_right/_front.png`, auf ihren Inhalt zugeschnitten, und die Leuchtebenen
  `title_*_lit.png` mit allen Fenstern und der Leuchtschrift. `title.json` nennt Lage und Tiefe der Ebenen, Fenster,
  Schild, Tor, Schlote, Laternen, Pfützen und den Weg der Arbeiter. Alles außer Rot ist entsättigt.

- **Weitere Karten:** `python tools/art/build_map.py <nr>` baut eine Karte aus dem Grundriss `data/scene_<nr>.json` der
  Level-Sitzung und dem Grafikthema ihres Viertels (`THEMES`: Farben der Baustoffe, Boden, Kulisse; je Gebäudeart ein
  Baumeister). Geschrieben wird nach `game/assets/maps/<id>/` mit denselben Dateinamen wie im Kesselviertel, dazu
  `data/light_<nr>.json`. Das Kesselviertel bleibt in `game/assets/` und entsteht weiter in `generate_concept.py`.
  Dächer steigen dort von Norden und Westen höchstens so steil wie die Blickrichtung, damit sie nicht mehr verdecken
  als die Quader des Grundrisses. `tools/art/check_cover.py <nr>` prüft das an den fertigen Bildern. Das Schleichlicht
  einer neuen Karte wird beim ersten Lichtbake festgehalten (`rules_albedo_<nr>.json`, `rules_windows_<nr>.json`).

## Zeichenreihenfolge

Statische Objekte sind beim Export vorsortiert. Eine Figur wird vor dem ersten Objekt eingereiht, das
vor ihr steht. Damit verdecken Gebäude Figuren korrekt, ohne die Objekte in jedem Bild neu zu sortieren.

## Möglicher Port nach Unreal

Ein späterer Wechsel bleibt möglich, ist aber kein Knopfdruck. Unreal nutzt C++, `Messingstadt.Core`
müsste also neu geschrieben werden.

| Bleibt nutzbar | Muss neu entstehen |
|---|---|
| JSON-Daten und Grafiken | Spiellogik in C++ |
| Regeln und Zahlenwerte | Darstellung und Bedienung |
| Tests als Beschreibung des Verhaltens | Build und Auslieferung |

Isometrische Pixelgrafik gehört nicht zu Unreals Stärken. Sinnvoll wird der Wechsel vor allem bei einem
Umstieg auf 3D. Eine Entscheidung ist dafür jetzt nicht nötig.
