# Kanon

Was in der Welt von Messingstadt gilt, mit Kennzeichen und Quelle. Kennzeichen siehe Skill `messingstadt-story`.

## Namen

| Name | Wer oder was | Kennzeichen | Quelle |
|---|---|---|---|
| Messingstadt | Die Stadt, 1927, erfundene Zeitlinie | KANON | `../konzept.md`, Welt |
| Brandt-Werke | Fabrik und Macht, Luftschiff, Werkschutz | KANON | `../konzept.md`, `../story.md` |
| Konrad Brandt | Herr der Werke, „der Alte“ | KANON | `../story.md`, Mächte |
| Kontorbank, Direktor Heinrich Valk | Bank, kauft Schulden der Mietshäuser | KANON | `../story.md`, Mächte |
| Stadtwache, Hauptmann Lorenz Pieper | Offiziell neutral, schlecht bezahlt | KANON | `../story.md`, Mächte |
| Roter Hammer, Julius Rehm | Gewerkschaft, Vorsitzender | KANON | `../story.md`, Mächte |
| Abteilung K, Martha Kolb | Verdeckte Abteilung, schreibt Aufträge und Telegramme | KANON | `../story.md`, Der Trupp |
| Greta Lenz | Truppführerin, früher Wachtmeisterin | KANON | `../story.md`, `data/mission_01.json` (`role`) |
| Otto Brenner | Schlosser | KANON | wie oben |
| Emil Sander | Buchhalter, früher Kontorbank | KANON | wie oben |
| Hanna Roth | Kurierin, Bruder Paul in Haft | KANON | wie oben |
| Anton Weiß | Drucker, in Haft | KANON | `../story.md`, Einsatz 2 |
| Ilse Marquardt | Ärztin der Krankenstube, erklärt den Dauertod | KANON, freigegeben 2026-11-06 | `texte/dauertod.md` |
| Karl Hoffmann, Frieda Kranz, Willi Saß, Erwin Pohl, Lotte Becker, Max Kühn | Namen für Neue | KANON, freigegeben 2026-11-06 | `texte/dauertod.md` |
| Kesselviertel, Nordwall, Gleisviertel, Kontorviertel, Altstadt | Die fünf Viertel des Stadtplans | KANON, freigegeben 2026-11-11 | `stadtplan.md` |
| Albert Dorn | Prokurist der Kontorbank, Einsatz 4 | KANON, freigegeben 2026-11-11 | `stadtplan.md` |
| Gustav Rademacher | Führer des Werkschutzes, Einsatz 5 | KANON, freigegeben 2026-11-11 | `stadtplan.md` |
| Linie 7 | Straßenbahn im Kesselviertel | IM SPIEL laut Hauptsession 2026-11-11, von Story nicht nachgeprüft | `stadtplan.md` |
| Wachstube Nord, Amtsgericht, Gerichtsgasse, Wallstraße, Wallgasse, Arresthof | Orte im Nordwall, Einsatz 2 | VORSCHLAG Story 2026-11-11 (Wachstube Nord ist KANON) | `einsatz_02_nordwall.md` |
| Güterbahnhof Ost mit Stellwerk, Einfahrweiche, Bahnübergang | Orte im Gleisviertel, Einsatz 3 | VORSCHLAG Story 2026-11-11 (Güterbahnhof Ost ist KANON) | `einsatz_03_gleisviertel.md` |
| Kassengasse, Bankstraße, Notariat, Hotel Kontor, Kanalbrücke | Orte im Kontorviertel, Einsatz 4 | VORSCHLAG Story 2026-11-11 | `einsatz_04_kontorviertel.md` |
| Färbergasse, Druckgasse, Kapelle, Westtor, Papierhof | Orte in der Altstadt, Einsatz 5 und 6 | VORSCHLAG Story 2026-11-11 (Fischmarkt und Druckhaus sind KANON) | `einsatz_05_altstadt.md`, `einsatz_06_anzeiger.md` |
| Lehrling des Druckers, ohne Namen | Ersatz für Weiß im Finale | VORSCHLAG Story 2026-11-11 | `einsatz_06_anzeiger.md` |
| Messingstädter Anzeiger | Zeitung, druckt die Sicht der Wache | KANON | `src/Messingstadt.Core/Headlines.cs` |
| Hotel Adler | Hotel an der Querstraße | IM SPIEL | `tools/art/generate_concept.py` (Dachschild) |
| Kurt | Laufbursche der Wache, in einem Arbeiter-Satz | IM SPIEL | `game/Chatter.cs` |
| Laufbursche, vierzehn Jahre | Alter in der Zeitung | IM SPIEL, VORSCHLAG Story 2026-11-03 | `src/Messingstadt.Core/Headlines.cs` |

Nachnamen und Vornamen der Mächte stehen bisher nur in `../story.md`, nicht im Spiel.

## Einsatz 1 im Spiel

| Was | Inhalt | Quelle |
|---|---|---|
| Ort, Zeit | Querstraße, hinter dem Mietshaus. Dienstag, 22. November 1927, 2:40 Uhr | `data/mission_01.json` |
| Auftrag | Kontobuch am Mietshaus holen, am Morgen holt es ein Bote ab. Unterschrift „K.“ | `data/mission_01.json` |
| Beute | Lohnkasse der Brandt-Werke in der Lagerhalle | `data/mission_01.json` |
| Telegramme | Niederlage, Abbruch, Rückzug. Fester Text als Rückfall | `src/Messingstadt.Core/Sequence.cs` |
| Telegrammstreifen im Einsatz | Kurze Anweisungen mit STOP | `game/Main.cs` |
| Sequenzen | Einstieg, Übergang, Sieg, Niederlage, Rückzug | `data/sequences/*.json` |

## Widersprüche und Lücken

| Was | Fundstellen | Stand |
|---|---|---|
| Untertitel „Die Stadt gehört den Brandt-Werken“ gegen drei Mächte im Konzept | `data/sequences/einstieg.json`, `../konzept.md` | ABLEITUNG: Brandts Selbstbild, bewusst so belassen |
| Rückzug ohne Schuss: Schlagzeile und Telegramm erscheinen nie, weil der Einsatz dann neu beginnt | `src/Messingstadt.Core/Headlines.cs`, `src/Messingstadt.Core/Sequence.cs`, Auskunft Menüs 2026-11-03 | OFFEN: Texte bleiben als Rückfall |
| Maschinenpistole: Text nennt ein Trommelmagazin, die Waffe hat 20 Schuss. Die Figur zeigt laut `docs/ausruestung.md` ebenfalls eine Trommel | `data/waffen.json`, `docs/ausruestung.md` | OFFEN: Textvorschlag in `texte/README.md`, Bild ist Sache der Grafik |
| Währung ohne Namen, „Groschen“ in einem Satz | `game/Chatter.cs`, Kasse in `../konzept.md` | OFFEN |

## Offene Fragen zur Recherche

Ergebnisse werden als HISTORISCH mit Quelle eingetragen.

- Wie hießen Dienstgrade einer städtischen Polizei um 1927 (Wachtmeister, Hauptmann)?
- Welche Löhne und Mieten passen für Fabrikarbeiter um 1927?
- Wie lief ein Streik ab: Streikkasse, Streikposten, Streikbrecher, Aussperrung?
- Wie wurde ein Notstand rechtlich ausgerufen, und wer bekam dann Polizeigewalt?
- Wie sah ein Telegramm aus: Kosten je Wort, übliche Abkürzungen?

## Kontinuitätsregeln

- Kein Text setzt voraus, dass ein bestimmter Agent lebt oder dabei ist.
- Der erste Fall heißt schwer verwundet, der zweite tot, auch bei einer Niederlage (Nutzer, 2026-11-06). Tote werden später nicht mehr genannt.
- Keine Wendung hängt an einer einzelnen Figur, für jede gibt es einen Ersatzweg.
- Kolb nennt in Telegrammen keine Namen.
- Kolb erzählt die Sequenzen. Sie weiß mehr als der Trupp, sagt aber nichts über `wissen.md` hinaus und lügt nie.
- Die Zeitung druckt, was die Wache sagt. Sie weiß nichts von Abteilung K.
- Wachen sind arme Leute, keine Monster.
- Brandt erscheint nie selbst im Einsatz, nur über Werkschutz, Luftschiff und Geld.
- Die Kampagne läuft vom 22. November bis 5. Dezember 1927 in der Reihenfolge aus `../story.md`.
