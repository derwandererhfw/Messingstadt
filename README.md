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

Grafik neu erzeugen: `python tools/art/generate_concept.py`

## Steuerung

| Eingabe | Wirkung |
|---|---|
| Linksklick auf Agent | auswählen, mit Umschalt zur Auswahl hinzufügen |
| Linksklick auf Boden | Auswahl dorthin schicken |
| Doppelklick auf Boden | Auswahl rennt dorthin, kostet Puste, dabei kein Feuer |
| Rechtsklick auf Wache | angreifen, die Auswahl rückt bis in Reichweite vor. Nur Emil gewählt: bestechen |
| Rechtsklick auf Türmarke | Otto öffnet die Tür |
| `1`–`4`, Leertaste | Agent wählen, alle wählen |
| Pfeiltasten, WASD | Kamera |
| `F` | Kamera auf die Auswahl |
| `Q` halten | Lageplan: bewachte Felder, Wege der Wachen, Wegvorschau. Ein Klick wartet, bis der Weg frei ist |
| `P` | Pause |
| `R` | Einsatz abbrechen. Nach dem ersten Schuss zweimal drücken, dann gilt der Trupp als gefallen |

## Grafik und Licht neu berechnen

Nach Änderungen an der Stadt, dauert etwa zwei Minuten. Der erste Befehl rendert auch die Figuren, für Figuren allein reicht `python tools/art/figures3d.py`.

```bash
python tools/art/generate_concept.py
python tools/art/bake_light.py
```

## Aufbau

| Pfad | Inhalt |
|---|---|
| `src/Messingstadt.Core` | Spiellogik in reinem C#, ohne Godot |
| `tests/Messingstadt.Core.Tests` | Tests der Spiellogik |
| `data/` | Missions- und Kartendaten als JSON |
| `game/` | Godot-Projekt: Darstellung und Eingabe |
| `tools/art/` | Grafikerzeugung |
| `tools/Messingstadt.Balance` | misst die Spielweisen einer Mission |
| `docs/` | Konzept und Architektur |
