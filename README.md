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

Vor jedem Einsatz liegt erst der Stadtplan offen, dann die Waffenkammer. Neues Spiel: Prolog, Stadtplan, Waffenkammer, Einsatz. Nach einem Ergebnis: Ergebnis-Bildschirm, Stadtplan, Waffenkammer, Einsatz. Nach "Spiel laden" geht es direkt in den Einsatz. Der Stadtplan (`game/CityPlan.cs`, Bildschirm `CityPlanScreen`) zeigt die Viertel aus `data/stadtplan.json`. Viertel der Brandt-Werke sind grün schraffiert, eroberte tragen das Rot des Hammers als volle Fläche mit dem Stempel HAMMER HÄLT, so erkennt man den Besitzer auch ohne Farbe. Die Farben der Story stehen als Konstanten in `CityPlanScreen` (`BrandtColor`, `HammerColor` und ihre Ränder). Das gewählte Viertel pulsiert ruhig, ein frisch erobertes färbt sich beim ersten Anzeigen um. Pfeiltasten oder ein Klick auf den Plan wählen, Enter oder "Einsatz annehmen" nimmt das Viertel, gesperrte Viertel und solche ohne Auftrag nennen den Grund. Esc führt zum Bericht zurück, aus der Waffenkammer zurück auf den Plan. Woher die Viertel kommen, weiß nur `CityPlanSource`: Es liest `Main.City` und die Abfragen `State`, `Owner`, `CanSelect`, `TrySelect` und `Selected` der Kampagne. Die Karte lädt `game/assets/stadtplan/plan.png`, die Masken `viertel_1.png` bis `viertel_5.png` und `plan.json` (Platz für Namen und Stempel, Umriss für die Klickprüfung). Nummer n dort ist das n-te Viertel der Kampagne. Solange die Dateien fehlen, zeichnet der Plan Platzhalterflächen. Das prüft `stadtplan.txt` mit `--menu --fresh`.

Die Waffenkammer (`game/Armory.cs`): links die Akten mit je vier Plätzen, in der Mitte die Kiste der Truppe, rechts der Katalog aus `data/waffen.json`. Einen Agenten wählen, dann einen Gegenstand anklicken, darunter stehen Werte, Aktentext und die Handgriffe Kaufen, In die Kiste, Ausrüsten, Ablegen und Verkaufen (halber Preis). Esc führt zurück zum Stadtplan. Beine, Herz, Arzt und Anwerben bleiben auf dem Ergebnis-Bildschirm. Das prüft `waffenkammer.txt` mit `--menu --fresh`.

Im Einsatz steht links eine feste Spalte (`game/Hud.cs`, Breite `Hud.ColumnWidth`), die Spielfläche beginnt rechts davon. Oben vier Agentenfelder mit Bild, Nummer, Leben und Zustand, ein Klick wählt wie die Tasten 1 bis 4. Darunter das Inventar des gewählten Agenten: Ein Klick auf einen Platz nimmt die Waffe in die Hand (wie E), legt den Verband an (wie H) oder macht den Rauchtopf scharf, der nächste Linksklick in die Welt wirft ihn, ein Rechtsklick nimmt das zurück. Unten das Peilgerät: Es schlägt alle `Hud.PeilSeconds` Sekunden aus (Startschalter `--peil=2.5`), Agenten gelb, Wachen rot, Zivilisten blau, Kreuz für das Ziel, Viereck für den Fluchtwagen. Was es zeigt, regelt allein `Hud.PeilRule`, derzeit alle lebenden Wachen in 14 Feldern Umkreis. Zivilisten kommen aus `Main.Passersby.Positions`, das Brustbild wählt `Main.AgentPortrait` aus `game/assets/hud` (ein Bild je Agent, Rekruten bekommen `portrait_rekrut`, angeschlagen und niedergegangen wird es abgedunkelt). Die Hilfezeile teilt `Hud.FitHelp` bei Platzmangel in zwei etwa gleich lange Zeilen. Das prüft `hud_spalte.txt` mit `--menu --script-writes --peil=1.5` und der Akte `waffen_akte.json`.

Wer aussetzt, angeschlagen oder in der Krankenstube, steht im Aktenschrank unter seiner Akte und im Einsatz durchgestrichen in den freien Agentenfeldern der Spalte. Das prüft `akte_aussetzen.txt` mit `--menu --script-writes` in einem eigenen `APPDATA`-Ordner, in den vorher `tests/prueflaeufe/akte_angeschlagen.json` als `Godot/app_userdata/Messingstadt/kampagne.json` kopiert wird.

Das Blatt "Totenbuch" (`Menus.ShowLedger`) liegt im Aktenschrank neben jeder belegten Akte und in der Pausenmappe: links die Toten des Trupps, rechts die der Wache, je Zeile Name, Viertel und der Stempel GEFALLEN. Je Seite stehen `Menus.LedgerRows` Namen, der Rest wird mit einem Knopf geblättert. Am Mauszeiger über einer Wache steht `Main.GuardLine`: Name, Art, Preis. Woher der Name kommt, weiß nur `Main.GuardName`. Was ein Agent besonders kann, liefert `Main.AgentTrait` für das Inventarblatt der Spalte, bei Greta den Nachlass beim Bestechen, den auch die Hilfezeile nennt. Im Kopf des Stadtplans zeigt `CitySign` als kleine Silhouette, wie die Stadt zum Trupp steht: alle Fenster hell (hilft), wenige (schweigt), keines (gegen den Trupp). Die Stufe kommt aus `CityMood.Of`, der Schalter `--stimmung=` setzt sie für Prüfläufe. Hat ein Einsatz ein Telefon, zeichnet `Overlay.DrawPhone` am Apparat (`World.PhoneSpot`) ein Zeichen: blass in Ruhe, gelb blinkend auf dem Weg dorthin, rot mit Balken beim Wählen, rot nach dem Anruf, durchgestrichen ohne Leitung. Das Telegramm meldet sechs Sekunden lang `Main.PhoneCalledText` oder `Main.PhoneCutText`. Den gekappten Draht prüft `telefon_draht_tot.txt` (`--fresh --mission=mission_02 --sound-log --look=14,9`) mit dem Prüfbefehl `alarm`, der über `World.SoundAlarm` die Kaserne alarmiert, auch wenn `calm` die Wachen gekauft hat. Sprechzeilen der Welt beginnen rechts der Spalte (`Main.HudColumn`) und brechen bei Überbreite in zwei Zeilen um. Das prüfen `totenbuch.txt` (`--menu --fresh`, ein Einsatz ohne Tote, einer mit einer toten Wache, Prüfbefehl `fellguard <n>`) und `totenbuch_blaettern.txt` (`--menu --script-writes` mit `totenbuch_akte.json` als Spielstand).

Waffenwechsel, Rauchtopf und Verband prüft `waffen_wechsel.txt`, genauso aufgerufen, nur mit `tests/prueflaeufe/waffen_akte.json` als Spielstand.

Einsatz 2 prüfen `einsatz2_befreien.txt` (`--fresh --mission=mission_02`: Pforte, Zellen, Folgen, Warten, Einsteigen) und `einsatz_wechsel.txt` (`--menu --fresh`: Sieg im Kesselviertel, Nordwall wählen, Stadt und Einsatz laden). `einsatz2_laut.txt` spielt den lauten Weg mit echten Wachen, sein Aufruf steht in der zweiten Zeile. `--mission=<kennung>` startet einen Einsatz direkt.

Grundriss eines Einsatzes ändern: `data/scene_02.json` bearbeiten, dann `python tools/level/scene_to_map.py 02` (Regelkarte, Verdeckung) und `python tools/level/placeholder_city.py 02` (Platzhalterstadt und Licht, etwa eine Minute).

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

`--script=<datei>` schickt Tasten und Klicks durch den echten Eingabeweg und vergleicht den Zustand, die Befehle stehen in `game/TestScript.cs`. Dazu passen `--menu` (Startbildschirm auch im Prüflauf) und `--fresh` (ohne Spielstand). Im Prüflauf zählt jedes Bild als ein Sechzigstel Spielzeit, gleich wie schnell der Rechner zeichnet: Dieselben Befehle geben denselben Ablauf, und am Ende steht die Prüfsumme der Spielwelt in der Ausgabe. `waittick <n>` wartet auf einen Takt der Spielwelt. Prüfläufe schreiben nichts in die Dateien des Spielers. Nur `--script-writes` tut das und gehört in einen eigenen `APPDATA`-Ordner.

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

`python tools/art/check_audio.py --neu` misst Pegel, Spektrum und Schleifen und vergleicht alles Byte für Byte mit einem neuen Lauf. `python tools/art/hoerproben.py` schreibt Hörproben mit den Pegeln des Spiels nach `work/hoerproben`, `python tools/art/hoerproben_vergleich.py` stellt alte und neue Klänge hintereinander. Im Prüflauf hält `--music=ruhe|dunkel|verdacht|gefecht|titel` eine Musiklage fest.

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
