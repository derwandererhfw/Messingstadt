# Einsatz 2: Grundriss und Abweichungen von der Vorlage

Quelle für Grafik und Regeln ist `data/scene_02.json`. `python tools/level/scene_to_map.py 02` schreibt daraus `data/map_02.json` und zeigt je Feld, was die Kamera verdeckt. Vorlage der Story: `docs/story/einsatz_02_nordwall.md`.

Regel für alle Karten, Entscheidung der Leitung: Zielorte, Türen mit Spielbedeutung und Hauptwege liegen nie hinter hohen Gebäuden. Ein Haus von 96 Pixeln verdeckt in der Schrägansicht drei Felder nördlich und westlich von sich.

## Gebäude

| Was | Art | Felder | Höhe |
|---|---|---|---|
| Stadtwall | stadtwall | x 0–20, y 0–2 | 60 |
| Amtsgericht | amtsbau | x 1–5, y 3–9 | 150 |
| Wachstube Nord, der Arrest | wachstube | x 7–14, y 2–6 | 96 |
| Wachlokal, Vorbau zur Straße | wachlokal | x 7–14, y 9–10 | 40 |
| Hofmauer West und Ost | hofmauer | x 7–8 und x 13–14, je y 6–9 | 34 |
| Kaserne | kaserne | x 15–19, y 3–8 | 120 |
| Remise | remise | x 15–19, y 13–17 | 56 |
| Beamtenhaus West und Ost | mietshaus | x 2–6 und x 9–13, je y 14–18 | 84 |

## Was sich gegenüber der Vorlage geändert hat

| Was | Vorlage | Jetzt | Grund |
|---|---|---|---|
| Wachstube | x 7–14, y 5–9, Front nach Süden | x 7–14, y 2–6 am Wall, Zellentüren nach Süden | Der Hof dahinter war verdeckt |
| Arresthof | x 8–13, y 2–5, zwischen Wachstube und Wall | x 8–13, y 6–9, vor der Wachstube | Sichtbar ohne Durchblick |
| Wachlokal | Teil der Wachstube | Eigener niedriger Vorbau x 7–14, y 9–10, schließt den Hof zur Straße | Der Hof braucht eine Südseite, die Wache eine Front zur Straße |
| Pforte zur Gerichtsgasse | [7, 3], Marke (6,5, 3,5) | [7, 7], Marke (6,5, 7,5) | Folgt dem Hof |
| Hoftor | [13, 3], Marke (14,5, 3,5) | [13, 7], Marke (14,5, 7,5) | Folgt dem Hof |
| Zelle 1, Anton Weiß | [9, 5], [9, 6], Marke (9,5, 4,5), er steht bei (9,5, 6,5) | [9, 5], [9, 4], Marke (9,5, 6,5), er steht bei (9,5, 4,5) | Tür zeigt jetzt nach Süden |
| Zelle 2, Paul Roth | [11, 5], [11, 6], Marke (11,5, 4,5), er steht bei (11,5, 6,5) | [11, 5], [11, 4], Marke (11,5, 6,5), er steht bei (11,5, 4,5) | Ebenso |
| Schreibstube | [13, 8], [13, 7], Marke (13,5, 9,5), Kasse bei (13,5, 7,5) | [13, 9], Marke (13,5, 10,5), Kasse bei (13,5, 9,5) | Liegt im Ostende des Wachlokals. Ein Feld, damit sie keinen dritten Weg in den Hof öffnet |
| Türposten | (10,5, 9,5), Blick Süd | (11,5, 10,5), Blick Süd, vor der Haupttür | Reihe 9 ist jetzt das Wachlokal |
| Wachhabender | (12,5, 9,5), Blick Süd | (14,5, 9,5), Blick Süd, an der Ecke neben der Schreibstube | Ebenso. Er sieht die Straße und den Gang zum Hoftor |
| Schließer | Rundgang (8, 3) – (12, 3) | Rundgang (8, 7) – (12, 7) | Folgt dem Hof |
| Mann vom Werk | (14,5, 3,5), Blick Ost | (14,5, 7,5), Blick Süd | Folgt dem Hoftor. Nach Osten sähe er nur die Kasernenwand |
| Wallstreife 1 | Rundgang (3, 10) – (17, 10) | Rundgang (3, 11) – (17, 11) | Auf Reihe 10 stehen jetzt Türposten und Laternen |
| Laterne der Wachstube | (10,5, 9,6) | (11,5, 10,1) über der Haupttür | Folgt der Tür |
| Lampe im Arresthof | (10,5, 4,8) | (10,5, 6,2) über den Zellentüren | Folgt den Zellen |
| Beamtenhäuser | y 13–18, vier Geschosse (128) | y 14–18, zwei Geschosse (84), davor ein Feld Vorgarten | Sie verdeckten die Wallstraße |
| Kohlenkiste | nicht vorhanden | (8,5, 8,5) im Arresthof, versperrt den Weg, nicht die Sicht | Deckung im Hof |

Unverändert: Stadtwall, Amtsgericht, Gerichtsgasse, Kaserne, Remise, Wallstraße, Wallgasse, Start, Ziel, Verstärkung, die drei Laternen der Wallstraße, Hundeführer mit Hund, Wallposten, Bote, Wallstreife 2.

## Sätze mit Spielhinweis

Alle acht Sätze aus der Vorlage stimmen weiter: Die Pforte liegt an der Gerichtsgasse, am Hoftor steht der Mann vom Werk, der Schütze steht im Osten am Wall, der Hund geht die Gasse ab. „Am Wall steht einer mit Gewehr“ gilt, sein Rundgang ist unverändert (14, 2) – (19, 2). Die Zeit der Wallstreifen messe ich nach dem Bau nach.

## Was verdeckt bleibt

43 freie Felder sind ganz verdeckt, alle am Rand: der Fuß des Walls westlich der Wachstube, der Westrand hinter dem Amtsgericht, der Postengang zwischen Wachstube und Kaserne nördlich von y 7 und der Streifen direkt vor den Beamtenhäusern und der Remise. Der Wallposten läuft im verdeckten Teil am Wall, sichtbar ist er nur am Ostende. Hof, Zellentüren, Pforte, Hoftor, Schreibstube, Gerichtsgasse ab y 4 und die Wallstraße sind frei.
