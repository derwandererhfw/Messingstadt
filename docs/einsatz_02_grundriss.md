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
| Wallstreife 1 | Rundgang (3, 10) – (17, 10) | Rundgang (0, 11) – (13, 11) | Auf Reihe 10 stehen jetzt Türposten und Laternen. Die Strecke liegt so, dass die Querung bei x 6 bis 7 in ihrer Mitte ist, sonst blieb dort kein Fenster von einer Sekunde |
| Wallstreife 2 | Rundgang (17, 12) – (3, 12) | Rundgang (13, 12) – (0, 12) | Ebenso, weiter gegenläufig |
| Westliche Laterne der Wallstraße | (4,5, 10,5) | (2,5, 10,5) | Die Querung liegt jetzt im Halbdunkel. Mit der Laterne daneben fiel der leise Weg schon bei 0,25 s Verspätung auf 16 % |
| Schlüssel zur Pforte | niemand | Der Türposten trägt ihn | Ohne Schlosser und Dietrich kam man sonst nicht in den Hof zum Schließer |
| Laterne der Wachstube | (10,5, 9,6) | (11,5, 10,1) über der Haupttür | Folgt der Tür |
| Lampe im Arresthof | (10,5, 4,8) | (10,5, 6,2) über den Zellentüren | Folgt den Zellen |
| Beamtenhäuser | y 13–18, vier Geschosse (128) | y 14–18, zwei Geschosse (84), davor ein Feld Vorgarten | Sie verdeckten die Wallstraße |
| Pforte zur Gerichtsgasse, Bild | Tür in der Westseite der Hofmauer, die die Kamera nie sieht | Lücke in der Mauer bei [7, 7] mit einem Tor aus Eisenblech, etwas niedriger als die Mauer (`gates` im Grundriss) | Der leise Weg führt hier durch, die Stelle muss lesbar sein. In der Südseite läge sie an der hellen Wallstraße. Blech statt Gitter, weil die Regeln die Sicht dort sperren |
| Hundeführer mit Hund | Rundgang (5, 9) – (5, 2) – (6, 2) – (6, 9) | Rundgang (5, 9) – (5, 2) – (1, 2) – (6, 2) – (6, 9): auf jeder Runde ein Stück am Fuß des Walls nach Westen | Otto allein wurde sonst schon bei 0,25 s Verspätung an der Pforte erwischt. Das Stück am Wall liegt hinter dem Amtsgericht, zu sehen ist er dort nur im Lageplan |
| Kohlenkiste | nicht vorhanden | (8,5, 8,5) im Arresthof, versperrt den Weg, nicht die Sicht | Deckung im Hof |

Unverändert: Stadtwall, Amtsgericht, Gerichtsgasse, Kaserne, Remise, Wallstraße, Wallgasse, Start, Ziel, Verstärkung, die mittlere und die östliche Laterne der Wallstraße, Wallposten, Bote.

## Sätze mit Spielhinweis

Alle acht Sätze aus der Vorlage stimmen weiter: Die Pforte liegt an der Gerichtsgasse, am Hoftor steht der Mann vom Werk, der Schütze steht im Osten am Wall, der Hund geht die Gasse ab. „Am Wall steht einer mit Gewehr“ gilt, sein Rundgang ist unverändert (14, 2) – (19, 2). Der Satz „Auf der Wallstraße laufen wir uns alle zwei Minuten über den Weg“ fehlt in der Missionsdatei: Bei 13 Feldern Rundgang begegnen sich die Streifen überschlagen etwa alle 8 Sekunden.

## Was verdeckt bleibt

43 freie Felder sind ganz verdeckt, alle am Rand: der Fuß des Walls westlich der Wachstube, der Westrand hinter dem Amtsgericht, der Postengang zwischen Wachstube und Kaserne nördlich von y 7 und der Streifen direkt vor den Beamtenhäusern und der Remise. Der Wallposten läuft im verdeckten Teil am Wall, sichtbar ist er nur am Ostende. Hof, Zellentüren, Pforte, Hoftor, Schreibstube, Gerichtsgasse ab y 4 und die Wallstraße sind frei.

## Der lange Rückweg: 30 × 30

Entscheidung des Nutzers: Level 2 wird größer und abwechslungsreicher. `python tools/level/grow_nordwall.py` baut den Grundriss aus dem Kern von 20 × 20, der Feld für Feld oben links bleibt. Noch gilt für das Spiel die alte Karte `data/map_02.json`, bis Mission und Messung nachgezogen sind.

```
# gesperrt, . frei und sichtbar, f Füße verdeckt, X Figur ganz verdeckt
    012345678901234567890123456789
  0 ##############################
  1 ##############################
  2 XXXXfXX#######XXXX............
  3 X####XX#######X####...........
  4 X####.X#######X####...........
  5 X####.f#######X####...........
  6 X####.f#....f#X####......#....
  7 X####.ff....f#.####...........
  8 .####.f##ffff#................
  9 .......#######................
 10 ..............................
 11 ......ffff....ffff............
 12 fffff.fffff...fffff...fffff...
 13 ####f#ffXXXX..ffXXXX..fXXXXX..
 14 ##..f#ffX####.ffX####.fX#####.
 15 ##..f#.fX####..fX####.fX#####.
 16 ....f#..X####...X####..X#####.
 17 ....ff.f.####..f.####...#####.
 18 .#..f#.f#......f#..#f#........
 19 ....f#.f#.#....f#...f#........
 20 ....f#.ffff....fff..f#........
 21 ...#f#.f#fffffff#ffff#........
 22 ....f#.ff###########f#..#.....
 23 ..#.f#.ff####..ff###f#........
 24 fffff#..f####...f###f#........
 25 ##f###...####....###.#........
 26 ..............................
 27 ..............................
 28 ..............................
 29 ..............................
44 freie Felder ganz verdeckt, 110 teilweise
```

Hohes steht am Nord- und Westrand (Wall 60, Amtsgericht 150, Kaserne 120, Wachstube 96). Im Süden sind die Vorderhäuser 84 hoch, Hinterhaus und Kneipe 48, Mauern 30 bis 34. In beiden Höfen und auf allen Türmarken sind höchstens die Füße verdeckt. Ganz verdeckt bleiben nur der Fuß des Walls, der Westrand hinter dem Amtsgericht, der Postengang nördlich von y 7 und das Feld Vorgarten direkt nördlich der Vorderhäuser und der Remise.

### Orte

| Ort | Felder | Art, Höhe | Charakter |
|---|---|---|---|
| Kern: Wall, Amtsgericht, Gerichtsgasse, Wachstube, Arresthof, Wachlokal, Kaserne, Wallstraße | x 0–19, y 0–12 | unverändert | wie gemessen |
| Stadtwall | x 0–30, y 0–2 | 60 | jetzt über die ganze Breite |
| Exerzierplatz | x 20–30, y 2–10 | Kies, vier Bogenlampen, Fahnenmast bei (25,5, 6,5) | weit, leer, grell |
| Kasernentür | Ostseite der Kaserne bei (19,5, 5,5) | Wandlampe | hier kommt die Verstärkung heraus |
| Telefon und Draht | Apparat (13,5, 9,5) in der Schreibstube, Draht über den Postengang zur Kaserne, Stelle zum Kappen (14,5, 8,5) | | Regel baut die Gegner-Session |
| Wallstraße | x 0–30, y 10–12 | Laternen bei x 2,5, 10,5, 16,5, 22,5, 28,5 | hell, die Streifen |
| Friedhof der Namenlosen | x 0–5, y 14–25, Mauer ringsum 30 | Erde, neun Kreuze mit Nummern, drei kahle Bäume, Hütte des Totengräbers (0–2, 14–16), kein Licht | flach, dunkel, lang |
| Durchgang | x 6–9, y 13–18 | wie bisher zwischen Mauer und Beamtenhaus A | dunkel, hier wartet der Trupp auf das Fenster |
| Friedhofsgang | x 6–8, y 18–26 | Gasse | schmal, zur Südgasse |
| Beamtenhaus A | x 9–13, y 14–18 | 84 | die Witwe wohnt zum Hof |
| Westhof | x 9–13, y 18–22 | Hof mit Pumpe | eng, nach Osten offen zur Mittelgasse |
| Mittelgasse | x 13–17, y 13–22 | Gasse, im Süden zugemauert | von der Wallstraße in die Höfe |
| Beamtenhaus B | x 17–21, y 14–18 | 84 | hier wohnen Wachleute |
| Osthof | x 17–21, y 18–22 | Hof mit Fässern | Hinterhof der Kneipe |
| Hinterhaus | x 9–13, y 22–26 | 48 | schließt den Westhof |
| Holzplatz | x 13–17, y 23–26 | Sackgasse zur Südgasse | Versteck |
| Kneipe „Zum Wallgraben“ | x 17–20, y 22–26 | 48, Laterne zur Südgasse | Kellerhals an der Ostseite (x 20, y 22–26) |
| Ostgasse | x 22–24, y 13–26 | Gasse | von der Wallstraße zum Kohlenhof |
| Remise | x 24–29, y 14–18 | 56, Gefangenenwagen | Öffnung nach Süden, Wandlampe |
| Kohlenhof | x 24–30, y 18–26 | Hof, Kohlenhaufen bei (24,5, 22,5) | hier wartet der Wagen |
| Südgasse | x 0–30, y 26–28 | eine Laterne bei (10,5, 27,5) | Wohnstraße, kaum Licht |
| Böschung | x 0–30, y 28–30 | Erde | Start |

### Türen

| Tür | Feld | Marke | Helfer |
|---|---|---|---|
| Friedhofspforte | [4, 13] | (4,5, 12,5) an der Wallstraße, gegenüber der Gerichtsgasse | |
| Pforte des Totengräbers | [5, 17] | (6,5, 17,5) im Durchgang | Totengräber |
| Südpforte des Friedhofs | [2, 25] | (2,5, 26,5) | |
| Hoftür der Witwe | [8, 20] | (7,5, 20,5) im Friedhofsgang | Witwe |
| Hoftür am Osthof | [16, 20] | (15,5, 20,5) in der Mittelgasse | |
| Kellerluke der Kneipe | [20, 22] | (20,5, 21,5) im Osthof | Wirtin, nur wenn Greta lebt oder in Einsatz 1 niemand getötet wurde |

Helferfenster mit Kerze: Witwe (10,5, 18,0) zum Westhof, Totengräber (1,0, 16,0) an der Hütte, Wirtin (20,0, 24,0) an der Ostwand der Kneipe. Nie helfen: Gerichtsdiener (Portal des Amtsgerichts, 3,0, 9,5), die Frau eines Wachmanns (Beamtenhaus B zum Osthof, 19,0, 18,2), Kasernenkoch (Küchentür der Kaserne, 17,0, 8,3).

### Start, Ziel, drei Rückwege

Start auf der Böschung x 9–13, y 28–30. Wagen im Kohlenhof x 27–30, y 20–23. Hinein wie bisher: Friedhofsgang, Durchgang, Wallstraße queren, Gerichtsgasse, Pforte. Zweiter Anmarsch: Holzplatz ist Sackgasse, aber durch Kellerluke, Osthof und Mittelgasse kommt man von Süden an die Wallstraße.

| Rückweg | Verlauf | Länge ab Pforte oder Hoftor | Türen |
|---|---|---|---|
| Friedhof | Gerichtsgasse, Wallstraße queren, Durchgang, Pforte des Totengräbers, über den Friedhof, Südpforte, Südgasse nach Osten, Kohlenhof | etwa 55 Felder | zwei |
| Beamtenhöfe | Durchgang, Hoftür der Witwe, Westhof, Mittelgasse, Hoftür am Osthof, Osthof, Kellerluke, Kellerhals, Südgasse, Kohlenhof | etwa 45 Felder | drei |
| Exerzierplatz | Hoftor, Postengang, Wallstraße nach Osten am Platz entlang, Ostgasse, Kohlenhof | etwa 26 Felder | keine |

### Posten der neuen Teile

Namen stehen in der Mission im Feld `person`, der Posten bleibt in `name`.

| Posten (`name`) | Person | Wo | Verhalten |
|---|---|---|---|
| Friedhofsstreife | Buchholz | (3,5, 14,5) an der Friedhofspforte | Steht und schaut reihum, nimmt Geld. Als Streife über den Friedhof ließ er auf fünf Feldern Breite keinen Weg frei |
| Posten am Fahnenmast | Lindner | (24,5, 6,5), Blick Süd | Steht, nimmt kein Geld (`noBribe`) |
| Posten an der Remise | Roloff | (26,5, 18,5), Blick Süd | Steht, nimmt kein Geld. Ein Bretterzaun bei y 20 trennt ihn vom Wagen |
| Nachtwächter | Fenske | Mittelgasse | Rundgang (14, 17) – (14, 13) – (23, 13) – (14, 13), 16 s |
| Südstreife | Zander | Südgasse | Rundgang (3, 26) – (23, 26), 25 s |

Nachträglich geändert: Start auf der Böschung in der Reihe y 29, zwei Felder von der Südgasse, damit die Südstreife ihn nicht sofort sieht. Wagen x 27–30, y 22–25 hinter dem Bretterzaun (x 24–30, y 20). Laterne der Südgasse bei (8,5, 26,6) am Ausgang des Friedhofsgangs statt bei (10,5, 27,5).

### Geändert gegenüber dem Stand von 20 × 20

Beamtenhaus West weicht dem Friedhof, an seiner Stelle begrenzt die Friedhofsmauer den Durchgang. Die Remise wandert von (15–19, 13–17) nach Osten. Der Wagen steht nicht mehr in der Wallgasse, die es nicht mehr gibt. Die Verstärkung kommt aus der Kasernentür statt vom Ostrand der Wallstraße.
