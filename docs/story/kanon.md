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
| Wachstube Nord, Wachlokal, Amtsgericht, Gerichtsgasse, Wallstraße, Wallgasse, Arresthof | Orte im Nordwall, Einsatz 2 | IM SPIEL als Grundriss (`../einsatz_02_grundriss.md`), Namen freigegeben 2026-11-14 | `einsatz_02_nordwall.md` |
| Güterbahnhof Ost mit Stellwerk, Einfahrweiche, Bahnübergang | Orte im Gleisviertel, Einsatz 3 | KANON, freigegeben 2026-11-14 | `einsatz_03_gleisviertel.md` |
| Kassenhof, Bankstraße, Pförtnerhaus, Hotel Kontor, Uferpromenade, Kanalbrücke | Orte im Kontorviertel, Einsatz 4 | KANON, freigegeben 2026-11-14 | `einsatz_04_kontorviertel.md` |
| Färbergasse, Druckgasse, Kapelle, Fischhalle, Westtor, Papierhof | Orte in der Altstadt, Einsatz 5 und 6 | KANON, freigegeben 2026-11-14 | `einsatz_05_altstadt.md`, `einsatz_06_anzeiger.md` |
| Lehrling des Druckers, ohne Namen | Ersatz für Weiß im Finale | KANON, freigegeben 2026-11-14 | `einsatz_06_anzeiger.md` |
| Namen der Wachen in Einsatz 1 und 2, Hunde Hasso und Greif, Bewohner des Nordwalls | Rufnamen im Spiel, Totenbuch | KANON, freigegeben 2026-11-14 | `texte/wachen.md` |
| Friedhof der Namenlosen, Beamtenhöfe, Exerzierplatz, Kasernenhof | Neue Orte im größeren Nordwall | Vom Nutzer gewählt über die Hauptsession 2026-11-12, Grundriss steht aus | `einsatz_02_nordwall.md` |
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
| Währung: Groschen (Nutzer, 2026-11-14). Im Spiel stehen Zahlen ohne Währung, siehe `steuerung.md`, Nächste Schritte | `game/Chatter.cs`, Kasse in `../konzept.md` | Entschieden, Anzeige nicht geändert |

## Offene Fragen zur Recherche

Ergebnisse werden als HISTORISCH mit Quelle eingetragen.

- Wie hießen Dienstgrade einer städtischen Polizei um 1927 (Wachtmeister, Hauptmann)?
- Welche Löhne und Mieten passen für Fabrikarbeiter um 1927?
- Wie lief ein Streik ab: Streikkasse, Streikposten, Streikbrecher, Aussperrung?
- Wie wurde ein Notstand rechtlich ausgerufen, und wer bekam dann Polizeigewalt?
- Wie sah ein Telegramm aus: Kosten je Wort, übliche Abkürzungen?

## Kontinuitätsregeln

- Kein Text setzt voraus, dass ein bestimmter Agent lebt oder dabei ist.
- Der erste Fall heißt schwer verwundet, der zweite tot, auch bei einer Niederlage (Nutzer, 2026-11-06). Erzähltexte nennen eigene Tote später nicht mehr. Akte, Totenbuch und Zeitung nennen die Toten beider Seiten, Wachen sprechen über ihre eigenen (Hauptsession auf Grundlage des Leitbilds, 2026-11-12).
- Keine Wendung hängt an einer einzelnen Figur, für jede gibt es einen Ersatzweg.
- Kolb nennt in Telegrammen keine Namen.
- Kolb erzählt die Sequenzen. Sie weiß mehr als der Trupp, sagt aber nichts über `wissen.md` hinaus und lügt nie.
- Die Zeitung druckt, was die Wache sagt. Sie weiß nichts von Abteilung K.
- Wachen sind arme Leute, keine Monster. Jede hat einen Namen, den man hört (`texte/wachen.md`).
- Der Totschläger schlägt bewusstlos. Niedergeschlagene stehen nicht im Totenbuch (Nutzer über die Hauptsession, 2026-11-12).
- Blutschuld, eine Zahl von 0 bis 6 für die ganze Stadt (Nutzer, 2026-11-13: „Ja, so bauen“): Der erste Tote einer Nacht ist frei, jeder weitere erhöht sie um 1, höchstens um 3 je Nacht. Eine Nacht ohne tote Wache tilgt 2. Diese Staffel hat die Kampagne am 2026-11-13 geglättet (Hauptsession). Freigegeben hatte der Nutzer: je Toter 1, höchstens 3. Stufen: hilft (0 bis 1), schweigt (2 bis 3), gegen den Trupp (4 bis 6). Im Spiel ohne Zahl und ohne Erklärtext.
- Die Zeitung nennt tote Wachen mit Namen. Von den eigenen Toten schreibt sie nur Blutspuren: Die Wache gibt die Namen der Täter nicht heraus, solange sie die anderen sucht (Hauptsession, 2026-11-13).
- Bewohner wissen nur, was in der Zeitung stand. Niemand ruft „Hammer“ oder „Abteilung K“.
- Die Währung heißt Groschen (Nutzer, 2026-11-14). Zahlen im Spiel sind Groschen.
- Ein Viertel, das der Hammer hält, fällt nicht an Brandt zurück (Nutzer, 2026-11-14).
- Werkschutzführer ist eine eigene Gegnerart, zuerst Rademacher in Einsatz 5 (Nutzer, 2026-11-14).
- Greta und Hanna haben je eine eigene Fähigkeit (Nutzer, 2026-11-14: „ja“). Inhalt in `faehigkeiten.md`, Regeln baut die Mechanik.
- Brandt erscheint nie selbst im Einsatz, nur über Werkschutz, Luftschiff und Geld.
- Die Kampagne läuft vom 22. November bis 5. Dezember 1927 in der Reihenfolge aus `../story.md`.
