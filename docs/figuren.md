# Figuren: Blatt, Paletten und Passanten

Das Figurenblatt entsteht mit `python tools/art/figures3d.py` (rund 25 Minuten) und wird mit
`python tools/art/check_figures.py <ordner>` geprüft. Die Gestalten stehen in `tools/art/generate_concept.py`:
`AGENTS` (Greta, Otto, Emil, Hanna), `SPECS` (Wachen, Hund, Arbeiter) und `CIVILIANS` (Passanten).

## Dateien

- `figures.png`: je Bildpunkt eine Palettennummer. Nie roh zeichnen, sondern über `Main.NewFigureSprite()`.
- `figures_palette.png`: je Zeile 256 Farben. Erst eine Zeile je Figur, dahinter die Farbvarianten.
- `figures_mirror.png`: farbig und viermal kleiner, für Spiegelungen (`Main.FigureMirror`). Ohne Varianten.
- `figures.json`: `names` (Zeilen), `columns` (Bewegungen der Einheiten), `stride`, `has`, `variants`, `civil`.

## Zeichnen

```csharp
var sprite = Game.NewFigureSprite();                 // Palettenshader, scharf-bilinear
sprite.RegionRect = Game.CivilFrame("Zivil_Frau", view, "walk", frame);
Game.SetFigureVariant(sprite, "Zivil_Frau", "gruen"); // "" ist die Grundfarbe
sprite.Position = Game.ToScreen(x, y) - Game.FigureFoot;
sprite.FlipH = nachWesten;                           // view 0 bis 4: S, SO, O, NO, N
sprite.Modulate = Game.FigureLight(x, y, 0.3f);      // Licht am Ort
```

Die Blickrichtung berechnet `Main.UpdateUnits` aus der Laufrichtung, dort kann man die Rechnung abschauen.
Wer `frame` nach dem Weg weiterschaltet, gleitet nicht: `frame = weg / Game.CivilStride(name, "walk") * Game.CivilFrames("walk")`,
der Weg in Spieleinheiten (1.000 je Feld).

## Passanten

Zeilen: `Zivil_Arbeiter` (Henkelmann), `Zivil_Frau` (Kopftuch, Einkaufsnetz), `Zivil_Herr` (Melone, Stockschirm),
`Zivil_Junge` (Zeitungsjunge, kleiner), `Zivil_Trinker` (ohne Hut, Flasche, schwankt). `Main.Civilians` listet sie.

| Variante | Figuren |
|---|---|
| `blau`, `braun` | `Zivil_Arbeiter` |
| `gruen`, `grau` | `Zivil_Frau` |
| `grau` | `Zivil_Herr`, `Zivil_Trinker` |
| `braun` | `Zivil_Junge` |
| `rekrut` | alle Zeilen der Agenten, setzt das Spiel selbst |

| Bewegung | Bilder | Hinweis |
|---|---|---|
| `walk` | 12 | nach Weg weiterschalten |
| `run` | 8 | Wegrennen, nach Weg weiterschalten |
| `idle` | 8 | Warten, etwa 1,6 Bilder je Sekunde |
| `smoke` | 6 | Hand zum Mund und zurück, etwa 3 Bilder je Sekunde, dazwischen `idle` |
| `read` | 4 | Zeitung lesen, etwa 1 Bild je Sekunde |
| `startle` | 3 | Erschrecken, einmal abspielen, etwa 10 Bilder je Sekunde |
| `cower` | 5 | Bilder 0 und 1 gehen in die Hocke, 2 bis 4 sind die Schleife |

Passanten sind grauer und dunkler als Agenten und Wachen und haben keinen Lichtsaum. Sterben gibt es für sie nicht.
Beim Lesen und Kauern verschwindet, was sie in der Hand tragen.
