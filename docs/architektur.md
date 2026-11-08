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
  Die Lichtwerte in `data/light_01.json` kommen aus `bake_light.py`: Der Mond dort trifft nur Oberseiten von Objekten
  und zählt nicht fürs Schleichen.

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
