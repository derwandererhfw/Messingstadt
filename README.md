# Messingstadt (Arbeitstitel)

Isometrische Echtzeit-Taktik in einer eigenen Dieselpunk-Metropole der 1920er Jahre. Ein kleiner Trupp
arbeitet für eine Gewerkschaft gegen Konzerne, Banken und ihre Werkschützer.

Eigenständiges Spiel: eigene Welt, Figuren, Grafik, Missionen und Regeln. Dieses Projekt verwendet
keine Daten, Grafiken oder Zahlenwerte aus der Syndicate-Analyse im Nachbarordner.

## Stand

Spielbarer Prototyp: eine Karte, vier Agenten mit eigenen Stärken, Wachen, ein Missionsziel, Geld und Ausbau zwischen den Einsätzen.

## Starten

Voraussetzung: Godot 4.7.2 .NET (`C:\Users\user\.tools\godot\`) und .NET SDK 9.

Spielen: Doppelklick auf `Spielen.bat`.

Branches testen: Doppelklick auf `Branch-Hub.bat`. Das Fenster listet alle Branches mit Ort, Datum und Abstand zu `main`.
- **Spielen** (oder Doppelklick auf die Zeile) holt den Commit-Stand in die Prüfkopie `work\hub`, baut und startet ihn. `main` und die Ordner der Sessions bleiben unberührt.
- **Arbeitsstand spielen** startet eine Session direkt aus ihrem Ordner, mit nicht committeten Änderungen.
- **Tests**, **Im Godot-Editor**, **Commits zeigen**, **Ordner öffnen** und **Von GitHub holen** tun, was sie sagen.
- Spielstand, Einstellungen und Einstieg werden vor jedem Test gesichert und danach zurückgespielt, außer der Haken „Spielstand behalten“ ist gesetzt.

Die folgenden Befehle gelten für PowerShell im Projektordner.

Tests:

```powershell
dotnet test tests/Messingstadt.Core.Tests
```

Nach Änderungen am Code neu bauen:

```powershell
& "C:\Users\user\.tools\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --path game --build-solutions --headless --quit
```

Falls das hängen bleibt: `dotnet build game/Messingstadt.csproj`

Spielweisen messen, optional mit Feldern als Argument, etwa `-- 6,9 6,8`, für die Anzeige, welche Wache wann hinschaut:

```powershell
dotnet run -c Release --project tools/Messingstadt.Balance
```

Kampagne über mehrere Einsätze messen: dieselbe Zeile mit `-- --kampagne`.

Den Ergebnis-Bildschirm mit Krankenstube, Arzt und den Stempeln KRANKENSTUBE und GEFALLEN prüft `tests/prueflaeufe/kampagne_akten.txt`, aufgerufen mit `--menu --fresh --script=../tests/prueflaeufe/kampagne_akten.txt`.

"Spiel laden" auf dem Startbildschirm setzt eine belegte Akte fort, mit dem Übergang statt des Prologs. Leere Akten stehen ausgegraut da. Solange nur der Startbildschirm offen ist, wird nichts gespeichert, und "Neues Spiel" fragt nur nach, wenn in der Akte eine Kampagne liegt, die schon etwas erlebt hat. Das prüfen `erststart_laden.txt` (`--menu --script-writes` in einem leeren `APPDATA`-Ordner) und danach im selben Ordner `laden_fortsetzen.txt` (`--menu --intro --script-writes`).

Wer aussetzt, angeschlagen oder in der Krankenstube, steht im Aktenschrank unter seiner Akte und im Einsatz auf einem Zettel neben den Agentenakten. Das prüft `akte_aussetzen.txt` mit `--menu --script-writes` in einem eigenen `APPDATA`-Ordner, in den vorher `tests/prueflaeufe/akte_angeschlagen.json` als `Godot/app_userdata/Messingstadt/kampagne.json` kopiert wird.

Waffenwechsel, Rauchtopf und Verband prüft `waffen_wechsel.txt`, genauso aufgerufen, nur mit `tests/prueflaeufe/waffen_akte.json` als Spielstand.

Grafik neu erzeugen: `python tools/art/generate_concept.py`

## Steuerung

| Eingabe | Wirkung |
|---|---|
| Linksklick auf Agent | auswählen, mit Umschalt zur Auswahl hinzufügen |
| Linksklick auf Boden | Auswahl dorthin schicken |
| Doppelklick auf Boden | Auswahl rennt dorthin, kostet Puste, dabei kein Feuer |
| Rechtsklick auf Wache | angreifen, die Auswahl rückt bis in Reichweite vor. Nur Emil gewählt: bestechen |
| Rechtsklick auf Türmarke | Otto öffnet die Tür, mit Dietrich auch andere |
| `E` | nächste Waffe für die Auswahl |
| `G` | Rauchtopf auf das Feld unter der Maus |
| `H` | Verband anlegen, 3 s still stehen |
| `1`–`4`, Leertaste | Agent wählen, alle wählen |
| Pfeiltasten, WASD | Kamera |
| `F` | Kamera auf die Auswahl |
| `Q` halten | Lageplan: bewachte Felder, Wege der Wachen, Wegvorschau. Ein Klick wartet, bis der Weg frei ist |
| `P` | Pause |
| `M` | Ton an und aus |
| `F11` | Vollbild an und aus. Das Fenster lässt sich auch frei ziehen, Oberfläche und Bild passen sich an, kleinstes Fenster 1024x640 |
| `Esc` | Pausenmappe mit Einstellungen und Bedienung, im Menü eine Ebene zurück |
| `R` | Einsatz abbrechen, mit Rückfrage. Nach dem ersten Schuss gilt der Trupp dann als gefallen |

In den Menüs wählen Pfeiltasten und Maus dasselbe, Enter oder Klick bestätigt. Lautstärken, Ton, Vollbild und Bildwackeln liegen getrennt vom Spielstand in `user://einstellungen.json`. Unter „Akten“ liegen drei Kampagnen: `user://kampagne.json` ist Akte 1, dazu `kampagne_2.json` und `kampagne_3.json`.

## Prüfläufe für die Bedienung

`--script=<datei>` schickt Tasten und Klicks durch den echten Eingabeweg und vergleicht den Zustand, die Befehle stehen in `game/TestScript.cs`. Dazu passen `--menu` (Startbildschirm auch im Prüflauf) und `--fresh` (ohne Spielstand). Prüfläufe schreiben nichts in die Dateien des Spielers. Nur `--script-writes` tut das und gehört in einen eigenen `APPDATA`-Ordner.

Prüfläufe zeigen keine Zwischensequenzen. `--intro` schaltet sie ein, `--seq=<id>` beginnt mit einer bestimmten aus `data/sequences`, `--seq-time=<s>` springt darin vor. Die Abläufe in `tests/prueflaeufe/sequenz_*.txt` prüfen Überspringen, Weiterschalten, Abbruch und den Weg vom Startbildschirm, etwa:

```powershell
& "C:\Users\user\.tools\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --path game -- --intro --fresh --script=$PWD/tests/prueflaeufe/sequenz_abbruch.txt
```

Die zweite Zeile jedes Ablaufs nennt seinen Aufruf. `sequenz_menue.txt` braucht `--menu --fresh --intro`, `sequenz_weiter.txt` statt `--intro` den Schalter `--seq=einstieg`, `sequenz_fortsetzen.txt` einen eigenen APPDATA-Ordner, in dem schon eine Akte liegt. Der Pfad zum Skript muss absolut sein, weil Godot im Ordner `game` startet. Jedes neue Spiel beginnt mit dem Prolog (`data/sequences/prolog.json`) und danach der Kamerafahrt mit dem Auftrag, `--fresh` gilt dabei als neues Spiel. Wer eine Akte fortsetzt, sieht nur den Übergang.

## Grafik und Licht neu berechnen

Nach Änderungen an der Stadt, dauert etwa zwei Minuten. Der erste Befehl rendert auch die Figuren, für Figuren allein reicht `python tools/art/figures3d.py`.

```bash
python tools/art/generate_concept.py
python tools/art/bake_light.py
```

Laternen, Hochbahn und Smog über den Häusern hinter der Karte stehen in deren Straßen, nach dem Verschieben der Blöcke also auch das:

```bash
python tools/art/skyline.py
```

Die Geräusche entstehen unabhängig davon:

```bash
python tools/art/generate_audio.py
```

`python tools/art/check_audio.py --neu` misst Pegel, Spektrum und Schleifen und vergleicht alles Byte für Byte mit einem neuen Lauf. `python tools/art/hoerproben.py` schreibt Hörproben mit den Pegeln des Spiels nach `work/hoerproben`. Im Prüflauf hält `--music=ruhe|dunkel|verdacht|gefecht|titel` eine Musiklage fest.

## Aufbau

| Pfad | Inhalt |
|---|---|
| `src/Messingstadt.Core` | Spiellogik in reinem C#, ohne Godot |
| `tests/Messingstadt.Core.Tests` | Tests der Spiellogik |
| `data/` | Missions- und Kartendaten als JSON |
| `game/` | Godot-Projekt: Darstellung und Eingabe |
| `tools/art/` | Grafikerzeugung |
| `game/assets/fonts` | Courier Prime und Old Standard, beide unter der SIL Open Font License, Quelle `github.com/google/fonts` |
| `tools/Messingstadt.Balance` | misst die Spielweisen einer Mission |
| `docs/` | Konzept und Architektur |
