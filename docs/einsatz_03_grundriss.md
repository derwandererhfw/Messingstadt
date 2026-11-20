# Einsatz 3: Grundriss des Güterbahnhofs Ost

Stand 2026-11-20, Schritt 1 von 2. Quelle für Regeln, Story und Grafik ist `data/scene_03.json`, erzeugt von `python tools/level/build_gleisviertel.py`. `python tools/level/scene_to_map.py 03` schreibt daraus die Regelkarte `data/map_03.json` und zeigt je Feld, was die Kamera verdeckt. Vorlage der Story: `docs/story/einsatz_03_gleisviertel.md`, dort für 20 × 20 geschrieben. Kartenordner für die Bilder: `game/assets/maps/gleisviertel/`.

Noch nicht gebaut: Mission, Regeln für Sabotieren, Uhr, Schotter, Messung. Das ist Schritt 2. Posten, Zeiten und Preise unten sind der Plan, gemessen ist davon nichts. `data/light_03.json` ist ein vorläufiges Licht nur aus den Laternen (`provisional`), bis die Grafik die Karte backt.

## Karte

30 × 30 Felder, Norden ist y = 0. Rechtecke gelten von x0, y0 bis ausschließlich x1, y1. `#` gesperrt, `.` frei, `f` frei, aber die Füße einer Figur sind im Bild verdeckt.

```
    012345678901234567890123456789
  0 ##############################   Bahndamm
  1 ##############################
  2 ...f###################.......   Schuppen x 5–14, Wasserturm x 15–17, Stellwerk x 19–23
  3 ...f##########.########.......
  4 ....##########...######.......
  5 .....#########.....####.......
  6 ......#.......................   Rampe (y 6), Stellwerkstür bei (21, 5)
  7 ...fffff...f#.................   Ladestraße, Ziel im Westen x 0–3
  8 ....fffff...ffff#.......#.#...
  9 .....#####...##fff............   Gleis 1
 10 .....ffff.......###..fff......   Gleis 2, Weiche bei (27,5, 10,5)
 11 ......ffff..ffff......fff.....   Gleis 3, ganz frei
 12 .fff...####..ffff..fff.###....   Gleis 4
 13 ..fff..ffff...####..fff.ff....   Gleis 5
 14 #..###..ffff.........###.ff...   Gleis 6
 15 .........####.............##.#   Gleis 7
 16 ffffff..#ffffffffffffffffffff.   Bahnweg
 17 #######...######f########f####   Bretterzaun, Pforten bei x 16 und x 25, Kohlenweg x 7–10
 18 ff.fff....ffff....fffff....f##
 19 ###f###...f####...f#####....##   Kohlenbansen, Baracke A, Baracke B, Wärterbude
 20 ###f###...f####...f#####......
 21 ###f###....####....#####......
 22 ###f###.......................
 23 ###.###.........#.............   Barackenhof
 24 .........#...ffffff...........
 25 .............f######..........   Speisebaracke
 26 .............f######..........
 27 ..............######..........
 28 ..............................
 29 ..............................   Start x 5–9
```

Kein freies Feld ist ganz verdeckt. Bei 125 Feldern stehen nur die Füße hinter einer Wagenkante, einem Zaun oder einer Baracke.

## Orte

| Was | Art | Felder | Höhe | Wozu |
|---|---|---|---|---|
| Bahndamm | bahndamm | x 0–30, y 0–2 | 56 | Schließt das Bild nach Norden, oben die Hauptstrecke |
| Güterschuppen | gueterschuppen | x 5–14, y 2–6 | 70 | Teerholz, Rampe nach Süden. Darin die Lohngelder |
| Wasserturm | wasserturm | x 15–17, y 2–4 | 140 | Das Höchste im Bild, am Nordrand. Für die Regeln gesperrt |
| Stellwerk | stellwerk | x 19–23, y 2–6 | 90 | Erstes Sabotageziel, Tür nach Süden |
| Laderampe | Fläche | x 5–14, y 6 | – | Gang des Schützen, im Licht der Bogenlampe |
| Ladestraße | Pflaster | x 0–30, y 7–9 | – | Zwischen Rampe und Gleisen, hell |
| Gleisfeld | Schotter | x 0–30, y 9–16 | – | Sieben Gleise bei y 9 bis 15 |
| Güterwagen | gueterwagen | zehn Gruppen, siehe unten | 52 | Sperren Weg und Sicht |
| Bahnweg | Schlacke | x 0–30, y 16 | – | Dunkler Weg südlich der Gleise |
| Bretterzaun | bretterzaun | y 17, x 0–7 und x 10–30 | 34 | Trennt Gleisfeld und Baracken, blickdicht |
| Kohlenweg | Schlacke | x 7–10, y 17–30 | – | Vom Start zum Gleisfeld, immer offen |
| Kohlenlager | kohlenbansen | x 0–3 und x 4–7, je y 19–24 | 36 | Zwei Bansen, dazwischen eine Karrengasse |
| Baracke A | baracke | x 11–15, y 19–22 | 38 | Rangierer, Helferstelle |
| Baracke B | baracke | x 19–24, y 19–22 | 38 | Heizer und Putzer, Helferstelle |
| Speisebaracke | baracke | x 14–20, y 25–28 | 38 | Dunkel, schließt den Barackenhof nach Süden |
| Wärterbude | huette | x 28–30, y 18–20 | 34 | Streckenwärter, hält zur Bahn |

Güterwagen (Gleis, x von–bis): Gleis 1 x 5–10 und x 13–15 · Gleis 2 x 16–19 · Gleis 4 x 7–11 und x 23–26 · Gleis 5 x 14–18 · Gleis 6 x 3–6 und x 21–24 · Gleis 7 x 9–13 und x 26–28. Gleis 3 (y 11) bleibt ganz frei.

Einzelne Deckung, versperrt den Weg, nicht die Sicht: Signal (16,5, 8,2), Wasserkran (24,5, 8,5), Karren (12,5, 7,5), Kisten auf der Rampe (6,5, 6,5), Schwellenstapel (8,5, 16,5) und (26,5, 8,5), zwei Prellböcke, Pumpe (9,5, 24,5), Feuertonne (16,5, 23,5).

## Start, Ziele, Zielzone

| Was | Wo | Anmerkung |
|---|---|---|
| Start | x 5–9, y 29 | Am Südrand vor dem Kohlenweg, dunkel |
| Stellwerk, Hebelbank | (21,5, 4,5) hinter der Tür [21, 5], [21, 4] | 6 s, mit dem Schlosser 3 s. Arbeitsplatz ist die Türschwelle, wie bei den Zellen im Nordwall |
| Einfahrweiche | (27,5, 10,5) | 4 s, frei im Licht der Weichenlaterne |
| Zielzone | x 0–3, y 6–9 | Wagen am Bahnübergang, unter der Schranke |
| Beute | (9,5, 4,5) hinter dem Schuppentor [9, 5], [9, 4] | Lohngelder der Bahnmeisterei, 300 |
| Verstärkung | (29,5, 12,5) | Werkschutz vom Ostende der Gleise |

Die Reihenfolge der beiden Ziele ist frei. Die Ziele liegen im Nordosten und Osten, die Zielzone im Westen: Der Rückweg führt die ganze Länge des Bahnhofs entlang, entweder im Licht über die Ladestraße oder im Dunkeln über den Bahnweg und den Bahnübergang.

## Türen

Vier Türen, weniger als im Nordwall (elf).

| Tür | Felder | Marke | Wozu |
|---|---|---|---|
| Stellwerk | [21, 5], [21, 4] | (21,5, 6,5) | Zur Hebelbank. Schlüssel beim Stellwerksposten |
| Schuppentor | [9, 5], [9, 4] | (9,5, 6,5) | Zur Beute, mitten auf der hellen Rampe |
| Zaunpforte der Rangierer | [16, 17] | (16,5, 18,5) | Helfertür `rangierer`. Kürzester Weg vom Barackenhof ins Gleisfeld |
| Zaunpforte der Heizer | [25, 17] | (25,5, 18,5) | Helfertür `heizer`. Kürzester Weg zur Weiche |

## Baracken als Helferstellen

Wie die Türen der Stadt im Nordwall: Hilft die Stadt (`Campaign.Stance`, Blutschuld 0 bis 1), brennt im Südfenster der Baracke eine Kerze und die Zaunpforte steht offen. Sonst ist die Pforte zu und braucht Schlosser oder Dietrich.

| Helfer | Kerze | Tür |
|---|---|---|
| `rangierer` | Baracke A, Südfenster (13,0, 22,0) | Zaunpforte der Rangierer |
| `heizer` | Baracke B, Südfenster (21,5, 22,0) | Zaunpforte der Heizer |

Zwei Bewohner helfen nie und rufen, sobald die Stadt schweigt (Regel wie im Nordwall):

| Wer | Wo | Trifft |
|---|---|---|
| `streckenwaerter` | Wärterbude, Fenster nach Westen (28,0, 19,0) | den Weg durch die Zaunpforte der Heizer |
| `bahnmeister` | Westgiebel des Schuppens (5,0, 4,5) | den Hof nördlich der Zielzone |

## Schotter

Das ganze Gleisfeld, x 0–30, y 9–16, ist Schotter: Schritte sind dort lauter (Regel in Schritt 2). Zwei Übergänge aus Bohlen sind leise:

| Übergang | Felder | Haken |
|---|---|---|
| Bahnübergang | x 0–3, y 9–16 | Am Westrand, führt in die Zielzone. Ohne Deckung, die Schrankenlampe steht bei (3,5, 8,5) |
| Bohlenweg | x 20, y 9–16 | Führt gerade auf die Stellwerkstür zu, im Blick des Stellwerkspostens |

Alles andere ist leise: Ladestraße, Rampe, Bahnweg, Kohlenweg, Barackenhof.

## Licht

| Laterne | Wo | Art |
|---|---|---|
| Bogenlampe über der Rampe | (9,5, 6,6) | hell, weit |
| Lampe über der Stellwerkstür | (21,5, 6,8) | Wandlampe |
| Weichenlaterne | (27,5, 10,5) | Laterne am Boden |
| Signal | (16,5, 8,2) | schwach |
| Schrankenlampe | (3,5, 8,5) | schwach |

Hell sind die Ladestraße und die nördlichen Gleise. Südlich von Gleis 4 ist es dunkel, Bahnweg, Kohlenweg und Barackenhof haben kein Schleichlicht. Kerzen und Funzeln der Baracken sind reine Bildlichter.

## Posten und Streifen

Acht Gegner und ein Hund, zwei davon Werkschutz. Namen liefert die Story.

| Posten | Art | Wo | Verhalten | Käuflich |
|---|---|---|---|---|
| Stellwerksposten | Werkschutz | (21,5, 6,5), Blick Süd | Steht vor der Tür, trägt den Schlüssel. Sieht den Bohlenweg hinunter | nein |
| Stellwerksrunde | Werkschutz | Osthof | Rundgang (18, 7) – (25, 7) – (25, 4) – (25, 7) | nein |
| Rampenschütze | Schütze | Rampe | Rundgang (7, 6) – (13, 6). Sieht über die Gleise, nicht durch Wagen | ja |
| Gleisstreife mit Hund | Hundeführer, Hund | Gleis 3 | Rundgang (2, 11) – (26, 11), die ganze Länge | ja |
| Südstreife | Wachmann | Bahnweg | Rundgang (9, 16) – (27, 16) | ja |
| Weichenposten | Wachmann | (27,5, 9,5), Blick West | Steht an der Weiche | ja |
| Aufsicht | Offizier | (16,5, 7,5) am Signal | Steht, dreht sich | ja, teuer |
| Lampenjunge | Laufbursche | Barackenhof | Rundgang (10, 18) – (24, 18) südlich des Zauns | ja |

## Wege

### Leise

1. **Über die Wagen.** Kohlenweg hinauf, über den Bahnweg, dann von Wagengruppe zu Wagengruppe nach Nordosten: Gleis 7 x 9–13, Gleis 5 x 14–18, Gleis 2 x 16–19. Jede Lücke quert Gleis 3, auf dem der Hund läuft, und liegt auf Schotter. Braucht keine Tür.
2. **Durch den Barackenhof.** Kohlenweg, dann hinter dem Zaun nach Osten und durch eine Zaunpforte. Leise bis zum Zaun, aber der Lampenjunge geht dort, und ohne Helfer sind die Pforten zu. Die Pforte der Heizer führt in vier Feldern zur Weiche.
3. **Am Stellwerk** führt kein leiser Weg am Posten vorbei, solange er steht: kaufen geht nicht, der Totschläger fällt keinen Werkschutz. Bleiben Rauchtopf, Weglocken oder der Moment, in dem die Runde im Osthof ist.

### Laut

1. **Erst die Weiche, dann das Stellwerk.** Die Verstärkung kommt vom Ostende. Wer zuerst im Osten steht, hat sie vor sich und nicht im Rücken.
2. **Stellung an den Wagen von Gleis 2** (x 16–19): letzte Deckung vor dem Stellwerk, acht Felder vor der Tür.
3. **Rückweg nach Westen** über den Bahnweg und den Bahnübergang. Über die Ladestraße ist es kürzer, aber der Schütze steht auf der Rampe.

### Woran sich der Bahnhof vom Nordwall unterscheidet

| | Nordwall | Güterbahnhof |
|---|---|---|
| Sicht | kurz, Mauern und Höfe | lang entlang der Gleise, quer dazu gesperrt durch Wagen |
| Deckung | Mauern, Gassen | Wagen, Stapel, Dunkelheit im Süden |
| Türen | elf | vier |
| Boden | überall leise | Schotter laut, zwei leise Übergänge |
| Druck | keiner | Uhr |
| Rückweg | drei Wege durch Höfe | einmal die ganze Länge zurück |

## Abweichungen von der Vorlage

| Was | Vorlage (20 × 20) | Jetzt (30 × 30) | Grund |
|---|---|---|---|
| Gleise | vier, y 8–11 | sieben, y 9–15 | Mehr Tiefe für die Wagen, längere Sicht |
| Wagengruppen | drei | zehn | Deckung für ein breiteres Gleisfeld |
| Güterschuppen | x 2–9, y 2–6 | x 5–14, y 2–6 | Platz für die Zielzone im Westen |
| Wasserturm | x 10–12, y 2–5 | x 15–17, y 2–4 | Folgt dem Schuppen |
| Stellwerk | x 13–16, y 3–6 | x 19–23, y 2–6 | Direkt am Damm, damit dahinter kein verdeckter Gang bleibt |
| Stellwerkstür | [14, 5], [14, 4] | [21, 5], [21, 4] | Folgt dem Stellwerk |
| Schuppentor | [5, 5], [5, 4] | [9, 5], [9, 4] | Mitte der Rampe |
| Einfahrweiche | (18,5, 9,5) | (27,5, 10,5) | Ostende der größeren Karte |
| Zielzone | x 0–2, y 5–8 | x 0–3, y 6–9 | Vor dem Bahnübergang, frei sichtbar |
| Start | x 5,5–7,5, y 18,5–19,5 | x 5–9, y 29 | Südrand |
| Kohlenlager | x 1–5, y 13–17 | x 0–7, y 19–24 | Südlich des Zauns |
| Baracken | zwei | drei und eine Wärterbude | Helferstellen und ein Hof |
| Bretterzaun mit zwei Pforten | nicht vorhanden | y 17 | Türen der Stadt, zweiter leiser Weg |
| Bohlenweg und Bahnübergang | nicht vorhanden | x 20 und x 0–3 | Leise Übergänge mit Haken |
| Verstärkung | (19,5, 8,5) | (29,5, 12,5) | Ostende der Gleise |
| Gleisstreife | (2, 9) – (17, 9) | (2, 11) – (26, 11) | Gleis 3 ist das freie Gleis |
| Südstreife | (6, 12) – (15, 12) | (9, 16) – (27, 16) | Bahnweg |
| Lampenjunge | (8, 13) – (15, 13) | (10, 18) – (24, 18) | Südlich des Zauns |

## Sätze mit Spielhinweis

Gegen diesen Grundriss geprüft, noch nicht gegen eine Messung:

| Satz | Stimmt |
|---|---|
| „Im Stellwerk sitzen zwei vom Werk.“ | Ja, Posten und Runde |
| „Der auf der Rampe sieht bis zur Weiche. Nur nicht durch die Wagen.“ | Im Grundsatz ja. Ob er wirklich bis zur Weiche sieht, hängt an seiner Sichtweite, das klärt Schritt 2 |
| „Der Hund läuft von einem Ende zum andern.“ | Ja, Gleis 3 |
| „An der Weiche steht einer allein.“ | Ja |
| „Auf dem Schotter hört man jeden Schritt.“ | Ja, sobald die Regel gebaut ist |
| „Der Junge mit der Lampe meldet alles dem Erstbesten.“ | Ja, er geht jetzt hinter dem Zaun |

Der Auftragstext nennt den Start „hinter dem Kohlenlager“ und den Wagen „am Bahnübergang“, beides stimmt. Der Telegrammtext „WAGEN ALS DECKUNG“ meint die Güterwagen und kann mit dem Fluchtwagen verwechselt werden. Vorschlag an die Story: „GÜTERWAGEN ALS DECKUNG“.

## Für die Grafik

Neue Arten im Grundriss: `bahndamm`, `gueterschuppen`, `wasserturm`, `stellwerk`, `kohlenbansen`, `baracke`, `gueterwagen`, `holzstapel`, `kohlenkasten`, dazu die Requisiten `hebelbank`, `weichenbock`, `signal`, `schranke`, `wasserkran`, `prellbock`, `schwellenstapel`, `karren`, `kasse` und die Böden `schotter`, `bohlen`, `schlacke`, `kohlenstaub`, `holz`. Höhen sind Vorgaben der Regeln: Güterwagen höchstens 52, Baracken und Bansen höchstens 38, sonst verdecken sie Wege. Drei niedrige Anbauten am Nordrand (Holzlege, Turmkasten, Kohlenkasten, je 24 hoch) sperren Ecken, in denen eine Figur sonst ganz verdeckt stünde.
