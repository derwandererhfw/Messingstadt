# Spielkonzept

## Welt

Messingstadt, 1927 in einer erfundenen Zeitlinie. Dieselmotoren und Dampfkessel haben sich nie aus den
Städten zurückgezogen. Die Stadt gehört drei Mächten: den Brandt-Werken, der Kontorbank und der
Stadtwache, die offiziell neutral ist. Dagegen steht die Gewerkschaft „Roter Hammer“.

Der Spieler führt eine verdeckte Abteilung der Gewerkschaft. Die Mittel sind Diebstahl, Sabotage,
Befreiung und, wenn es nicht anders geht, Gewalt.

## Was das Spiel ausmachen soll

- Kleine Teams mit klaren Rollen statt anonymer Einheiten.
- Die Stadt lebt weiter: Straßenbahnen, Schichtwechsel, Wachen mit Rundgängen.
- Beute und Ruf statt reiner Eroberung. Zwischen den Einsätzen wird die Gewerkschaft aufgebaut:
  Mitglieder gewinnen, Betriebe organisieren, Streiks auslösen.
- Lärm hat Folgen: Schüsse alarmieren die Umgebung, stille Wege bleiben möglich.

## Prototyp

Einsatz „Das Kontobuch“: Der Trupp beginnt am Südende der Straße, holt das Kontobuch und erreicht den
Fluchtwagen am Ostende der Querstraße.

| Regel | Wert |
|---|---|
| Spieltakt | 20 Schritte pro Sekunde, fest |
| Agent | 100 Lebenspunkte, 2,4 Felder/s, mit dem Kontobuch 2,0 Felder/s |
| Wache | 60 Lebenspunkte, 1,6 Felder/s auf Rundgang, 2,2 im Alarm |
| Pistole | Reichweite 7 Felder, alle 0,7 s, Agent 16 Schaden, Wache 10 Schaden |
| Trefferchance | 85 %, je Feld Entfernung 5 Punkte weniger, Ziel in Deckung 25 weniger, Schütze in Bewegung 15 weniger, mindestens 10 % |
| Deckung | Ein Hindernis direkt neben dem Ziel in Richtung des Schützen |
| Sicht ahnungslose Wache | Kegel von 90° in Blickrichtung, im Umkreis von 2 Feldern auch hinter ihr. Die Reichweite hängt vom Licht beim Ziel ab: 3,5 Felder im Dunkeln, bis 5 Felder im Hellen |
| Licht beim Schleichen | Helligkeit aus dem Pathtracer (`data/light_01.json`). Im Dunkeln dauert das Bemerken 60 % länger, im hellen Licht 20 % kürzer. Lageplan und Sichtkegel rechnen damit, die Akte zeigt „im Dunkeln“ oder „im Licht“ |
| Sicht alarmierte Wache | 7 Felder rundum |
| Sicht allgemein | Gebäude verdecken, Sicht gilt immer in beide Richtungen |
| Posten | Wachen ohne Rundgang drehen sich alle 3 s um 90° |
| Kontobuch aufnehmen | 3 s im Stehen, wer weggeht, fängt von vorn an |
| Einsteigen | Der Träger muss 1 s im Wagenbereich stehen |
| Rennen | Doppelklick. Tempo plus 1,25 Felder/s je Stufe Beine. Wer rennt, schießt nicht und ist auf 4 statt 2 Felder zu hören |
| Puste | 4 s plus 2 s je Stufe Herz. Erholung alle 3, 2 oder 1 Takte je nach Herz. Neustart ab 1 s Puste |
| Startwerte der Agenten | Greta Beine 2, Herz 2. Otto Beine 1, Herz 3. Emil Beine 1, Herz 1. Hanna Beine 3, Herz 1 |
| Deckung der Agenten | Stehende Agenten gehen unter Feuer selbst in Deckung, höchstens 2 Felder vom befohlenen Standort, nicht beim Aufnehmen oder Einsteigen |
| Bemerken | 0,4 s plus 0,1 s je Feld Entfernung, bereits alarmierte Wachen 0,2 s |
| Alarm | Jeder Schuss ist im Umkreis von 8 Feldern zu hören, je Feld 0,15 s später |
| Verstärkung | 6 s nach dem ersten Schuss und dann alle 4 s eine Wache nördlich vom Fluchtwagen, höchstens 3. Sie sichert den Wagen, nicht den Ort des Schusses |
| Wertung | Unentdeckt: nie bemerkt. Leise: bemerkt, aber kein Schuss. Laut: Schüsse gefallen |
| Abbrechen (R) | Vor dem ersten Schuss frei. Danach nur nach Warnung, und der Trupp gilt als gefallen. Fenster schließen zählt genauso |

Die Werte sind erste Annahmen und werden beim Spielen angepasst.

### Gegnertypen

| Art | Lebenspunkte | Waffe | Besonderheit | Bestechung |
|---|---|---|---|---|
| Wachmann | 60 | Pistole, 7 Felder, 10 Schaden | | 200 |
| Offizier | 70 | Pistole, 7 Felder, 12 Schaden | Bemerkt 30 % schneller, ruft beim Kampf Alarm auch ohne Schuss | 400 |
| Werkschutz | 110 | Schrotflinte, 4 Felder, 18 Schaden, langsame Schussfolge | Gepanzert, im Alarm langsam | nicht käuflich |
| Schütze | 50 | Gewehr, 11 Felder, 18 Schaden, sehr langsame Schussfolge | Sieht im Alarm 11 Felder weit | 200 |
| Wachhund | 35 | Biss aus 1,2 Feldern, 12 Schaden | Wittert auf 2,5 Felder ringsum und in jedem Licht, sehr schnell, schlägt an. Sein Biss macht den Einsatz nicht laut | nicht käuflich |

Die Art steht in der Mission je Gegner. Fährt die Maus über einen sichtbaren Gegner, zeigt das Spiel seine Art und ob er käuflich ist.

### Verhalten der Wachen

Eine Wache ist immer in einem von fünf Zuständen. Sichtbare Wachen tragen im Spiel eine farbige Marke.

| Zustand | Verhalten | Marke |
|---|---|---|
| Rundgang | läuft ihre Strecke ab | keine |
| Bemerken | bleibt stehen und schaut, schießt noch nicht | gelb |
| Kampf | feuert und sucht ein freies Feld mit Sicht, bevorzugt in Deckung | rot |
| Nachsehen | geht zur letzten bekannten Position oder zum Lärm | orange |
| Absuchen | wartet dort 5 s und kehrt dann zum Rundgang zurück | orange |

Wachen kennen nur, was sie selbst sehen oder hören. Wer beschossen wird, geht sofort in den Kampf.
Nach 2 s ohne Sichtkontakt wechselt eine kämpfende Wache zum Nachsehen. Sichtbare ahnungslose Wachen
zeigen ihren Sichtkegel, auch ohne Sichtkontakt, dann aber schwächer. Agenten sehen 10 Felder weit.

### Lageplan

Wer Q hält, sieht, wie oft die Wachen jedes Feld im Blick haben (grün: nie, gelb bis rot: immer öfter), wo sie entlanggehen und den Weg zum Mauszeiger. Die Vorschau sagt, ob der Weg gerade frei ist oder in wie vielen Sekunden. Ein Klick im Lageplan schickt die Auswahl los, sobald er frei ist. Grundlage ist eine Probe der Mission ohne Agenten (`WatchMap`). Sie stimmt, bis eine Wache etwas bemerkt, danach gibt es keine Vorhersage mehr.

Eine Gruppe verteilt sich beim Gehen auf eine Linie quer zur Blickrichtung, damit keine Figur im Bild
hinter einer anderen steht. Der Träger des Kontobuchs bekommt dabei genau das angeklickte Feld.

### Fähigkeiten

| Agent | Fähigkeit | Regel |
|---|---|---|
| Otto, Schlosser | Tür öffnen | Rechtsklick auf die Türmarke, 2 s neben der Tür stehen. Danach kann jeder hindurch. Die Sicht bleibt gesperrt |
| Emil, Buchhalter | Bestechen | Rechtsklick auf eine Wache, wenn nur Emil gewählt ist. 200 aus der Kasse, wirkt ab 2 Feldern Abstand, nur solange die Wache nicht kämpft. Sie ignoriert danach Agenten und Alarm, bis jemand auf sie schießt |

In der ersten Mission führt die Hintertür der Lagerhalle zu einem Durchgang zur Querstraße und zur
Lohnkasse (300). Beute zählt nur, wenn der Trupp entkommt. Papiere fälschen ist noch nicht gebaut.

### Kampagne

| Regel | Wert |
|---|---|
| Startgeld | 300 |
| Lohn pro Sieg | 300, bei Leise 450, bei Unentdeckt 600 |
| Ausbau | 150 von Stufe 1 auf 2, 300 von Stufe 2 auf 3 |
| Niederlage | Kein Lohn, Beute verloren, ausgegebenes Geld bleibt ausgegeben |
| Gefallene Agenten | Kommen zurück, Beine und Herz fallen auf Stufe 1, auch bei einer Niederlage |

Der Stand wird im Benutzerordner des Spiels gespeichert (`kampagne.json`). Es gibt bisher nur eine
Mission, „Nächster Einsatz“ spielt sie erneut.

### Umgebung

Die Stadt bewegt sich auch ohne Zutun: Rauch aus Schornsteinen und Kesseln, Dampf aus Gullys, Nieselregen
mit Ringen in den Pfützen, flackernde Laternen (eine mit Wackelkontakt), flackernde Kesselfeuer und
Papierfetzen in Windböen. Das ist reine Darstellung mit eigenem Zufall in `game/Ambience.cs` und
`game/RainLayer.cs`. Die Spiellogik bleibt deterministisch, und die Animation läuft auch in der Pause weiter.

### Eintauchen

Was den Spieler in die Welt ziehen soll, alles reine Darstellung:

| Was | Wie |
|---|---|
| Einstieg | Titel auf Schwarz, die Kamera sinkt auf den Trupp, eine Schreibmaschine tippt den Auftrag. Jede Taste beendet ihn, wer ihn kennt, sieht nur eine kurze Aufblende (`game/Intro.cs`) |
| Oberfläche | Oben ein Telegramm, unten vergilbte Personalakten mit Stempel. Ein Klick auf eine Akte wählt den Agenten (`game/PaperUi.cs`) |
| Filmlook | Dunkle Bildränder, feines Filmkorn, leichte Farbstimmung (`game/FilmLook.cs`) |
| Gespräche | Wachen und Arbeiter reden kurze Sätze, Wachen rufen im Alarm. Manche Sätze verraten etwas über die Rundgänge (`game/Chatter.cs`) |
| Kamera | Folgt weich, atmet leicht, zittert bei Donner und nahen Schüssen, rückt heran, wenn ein Agent fällt |
| Ton | Hund, Sirene, Güterzug und Schichthorn in der Ferne, Radio hinter einem Fenster. Herzschlag, wenn eine Wache gerade bemerkt. Im Dunkeln wird die Musik dünner |
| Zeitung | Nach jedem Einsatz eine Schlagzeile, die zum Verlauf passt: unentdeckt, leise, blutig, verloren, aufgegeben (`Headlines` in der Spiellogik) |
| Spuren | Nasse Fußabdrücke, die nach 8 Sekunden verschwinden, und Blutstropfen verletzter Figuren. Wachen folgen ihnen nicht (`game/Traces.cs`) |

### Figuren

Figuren werden aus einem kleinen 3D-Modell gerendert (`tools/art/figures3d.py`): runde Körperteile mit Gelenken, im selben Blickwinkel wie die Stadt, dann auf Pixelgröße verkleinert. Das Blatt hat acht Blickrichtungen, fünf gerendert und drei gespiegelt. Bewegungen: Gehen und Rennen mit je 8 Bildern, Stehen mit leichtem Atmen, Zielen mit Rückstoß, zielend Gehen und zwei Arten zu sterben: Erst zuckt der Körper, dann geben die Knie nach, dann sackt er nach vorn oder zur Seite zusammen, und eine Blutlache wächst. Die Schrittgeschwindigkeit hängt am Tempo, damit die Füße nicht rutschen. Zwischen den 20 Spieltakten pro Sekunde wird die Position im Bild geglättet, die Spiellogik bleibt davon unberührt.

### Leben in der Stadt

Reine Darstellung mit eigenem Zufall, ohne Einfluss auf die Spiellogik:

| Was | Verhalten |
|---|---|
| Fenster | Verschiedene Leuchtmittel: Glühlampe, Gaslicht, schwache Funzel, kaltes Licht, Kerze. Manche bleiben dunkel, einige gehen an und aus. Dahinter Vorhänge, Rollos, Gardinen oder eine Topfpflanze, das Licht fällt in der Scheibe von einer Stelle aus ab, und dauerhaft leuchtende Fenster werfen einen Schein auf die Wand. Ab und zu läuft jemand vorbei (`game/WindowLife.cs`). Die Fabrikfenster leuchten fahl, einige sind dunkel |
| Bildfunk | In drei Fenstern ein gedämpftes bläuliches Licht, das alle paar Sekunden weich wechselt und auch auf Wand und Boden davor fällt. In unserer Zeitlinie ein teures Gerät für wenige |
| Hotelschild | Leuchtet rot, ein Buchstabe hat einen Wackelkontakt, manchmal fällt das ganze Schild kurz aus |
| Ratten | Huschen an Hauswänden entlang, fliehen vor Menschen und Schüssen in den nächsten Gully (`game/Critters.cs`) |
| Tauben | Sitzen auf Dachkanten und picken, fliegen bei Schüssen oder nahen Menschen auf und kommen später zurück |
| Gewitter | Alle 35 bis 95 Sekunden Blitze: gezackte, verästelte Strahlen (`game/LightningBolt.cs`) mit vorberechneten Schatten, Figuren im Hausschatten bleiben dunkel. Der Donner kommt je nach Entfernung später und leiser. Dazwischen alle 18 bis 50 Sekunden fernes Grollen ohne Blitz (`game/Weather.cs`) |
| Bodennebel | Schwaden ziehen über den Boden und leuchten dort, wo Licht fällt, weil der Nebel die Lichtkarte des Bodens liest |
| Lichtkegel | Unter den Laternen steht ein Lichtkegel, in dem der Regen aufleuchtet |
| Luftschiff | Zieht in Abständen über die Stadt, sein Suchscheinwerfer streift durch die Straßen. Bisher ohne Einfluss auf die Wachen (`game/Airship.cs`) |
| Streikposten | Drei Arbeiter wärmen sich an einer Feuertonne vor dem Lager, das Feuer wirft zuckende Schatten. Bei Schüssen laufen sie weg (`game/Strikers.cs`) |
| Fabrik | Hinter einem Hallenfenster wird geschweißt, mit Funken und blauem Licht. Die Kessel blasen zischend Dampf ab |
| Arbeiter | Raucht vor der Seitentür des Hotels, geht bei Schüssen oder nahen Menschen hinein und kommt später wieder |

### Ton

Alle Geräusche werden per Synthese erzeugt (`tools/art/generate_audio.py`), es gibt keine fremden Aufnahmen. `game/SoundScape.cs` liest nur den Zustand der Spiellogik und ändert nichts daran.

| Art | Was |
|---|---|
| Überall | Regen und Wind |
| Am Ort, nur aus der Nähe | Laternen, Dampf aus Gullys, Kesselfeuer und Maschinen, Dieselmotor des Fluchtwagens, Bildfunk, Leuchtschild |
| Figuren | Dumpfe Schritte, Wachen nur aus der Nähe, Agenten etwas weiter, laut beim Rennen. Schüsse mit Echo, Treffer, Querschläger, Fallen |
| Einsatz | Trillerpfeife, wenn eine Wache in den Kampf geht, Alarmglocke beim Anrücken der Verstärkung, Dietrich und Tür, Blättern im Kontobuch, Münzen, Abschlussakkord |
| Tiere | Tauben flattern auf, Ratten quieken |
| Wetter und Stadt | Donner, Motoren des Luftschiffs, Dampfstöße, Schweißen, Feuertonne |

Dazu kommt leise Musik: langsame dunkle Flächen in d-Moll mit einem sparsamen Thema, eine eigene Komposition in der Stimmung des Originals. Solange Wachen alarmiert sind, blendet sich eine treibende Bassfigur ein.

Taste `M` schaltet den Ton aus.

### Licht

Das Licht von Häusern und Boden kommt aus einem Pathtracer (`tools/Messingstadt.LightBake`, Aufruf über `tools/art/bake_light.py`). Er rechnet jeden Bildpunkt über den Kamerastrahl auf ein 3D-Modell aus Quadern zurück und verfolgt von dort das Licht: direkt von Laternen, Feuerluken und erleuchteten Fenstern, dazu drei Umwege über Wände und Boden und der Nachthimmel. Das nasse Pflaster und die Pfützen spiegeln die Lampen Richtung Kamera. Das Ergebnis liegt je Sprite als Lichtkarte in `game/assets/light`.

Berechnet wird vorab, nicht in jedem Bild: Godot hat im 2D kein Pathtracing, und die Stadt steht still. Das Spiel multipliziert die Karten nur noch per Shader. Was sich bewegt, wird angenähert:

| Was | Wie |
|---|---|
| Figuren | Licht aus einem Gitter (4 Punkte je Feld), mit Schlagschatten von nahen Laternen. Agenten und entdeckte Wachen haben eine Mindesthelligkeit, damit sie lesbar bleiben, Wachen ohne Sichtkontakt bleiben dunkler |
| Flackern | Die Anteile von Laternen und Feuer stehen in einer zweiten Karte. Nur das direkte Licht flackert, das Umwegelicht bleibt gleich |
| Mündungsfeuer | Weiter ein einfaches 2D-Licht ohne Schatten |

Nach Änderungen an der Stadt erst `generate_concept.py`, dann `bake_light.py` laufen lassen. Das ändert auch `data/light_01.json` und damit die Spielregeln.

### Lesbarkeit

Sichtkegel, Lageplan und Beute liegen unter den Gebäuden, Dächer verdecken sie also. Das ist reine Darstellung.

### Schüsse

Nach einem Schuss hält die Figur die Waffe kurz aufs Ziel, auch im Weitergehen, mit kurzem Rückstoß. Dazu kommen Mündungsfeuer, das die Umgebung erhellt, Leuchtspur sowie Blut bei Treffern und Staub bei Fehlschüssen. Alles ist reine Darstellung, nur die Schussrichtung merkt sich die Logik.

### Stand der Spielweisen

Gemessen in der Spiellogik über je 300 Zufallsfolgen mit `tools/Messingstadt.Balance`:

| Spielweise | Siege | Niederlagen | Tote Agenten | Lebenspunkte übrig | Dauer | Wertung |
|---|---|---|---|---|---|---|
| Alle rennen zum Buch, dann zum Wagen | 46 % | 53 % | 2,71 | 43 von 400 | 19 s | Laut |
| Jeder greift die nächste Wache an, dann Buch und Wagen | 82 % | 18 % | 1,36 | 134 von 400 | 37 s | Laut |
| Alle schießen dieselbe Wache aus, dann Buch und Wagen | 81 % | 18 % | 1,56 | 140 von 400 | 39 s | Laut |
| Alle sprinten den ganzen Weg zum Buch und zum Wagen | 54 % | 46 % | 2,97 | 27 von 400 | 15 s | Laut |
| Auf die Straße stellen und kämpfen lassen, dann Buch und Wagen | 9 % | 90 % | 3,85 | 7 von 400 | 31 s | Laut |
| Hinterhalt: einer lockt, alle warten zwischen den Häusern | 2 % | 97 % | 3,97 | 1 von 400 | 30 s | Laut |
| Greta schleicht allein, Klicks auf ±0,5 s genau | 100 % | 0 % | 0 | 400 von 400 | 67 s | Unentdeckt |
| Greta schleicht allein, Klicks auf ±1 s genau | 90 % | 9 % | 0,09 | | 67 s | meist Unentdeckt |

Mit dem Lageplan ist kein vorab berechneter Zeitplan mehr nötig. Greta geht dabei von Versteck zu Versteck und startet, wenn die Vorschau den Weg frei zeigt (`--lageplan`, je 100 Läufe):

| Start | Unentdeckt | Dauer |
|---|---|---|
| Klick im Lageplan, Start genau bei frei | 94 % | 55 s |
| Eigener Klick, bis 0,25 s zu spät | 67 % | 119 s |
| Eigener Klick, bis 0,5 s zu spät | 59 % | 146 s |
| Eigener Klick, bis 1 s zu spät | 51 % | 161 s |

Der Schleichplan ist als Test abgesichert. Eine frühere Fassung mit Klick 7 bei 63 s und ohne Einsteigen wurde im Spiel mit echten Klicks gewonnen:

| Zeit | Klick auf Feld | Zeitfenster | Zweck |
|---|---|---|---|
| 10 s | 6,15 | 20 s | Versteck südlich der Straße |
| 15 s | 6,5 | 2,6 s | über die Straße ins Versteck in der Gasse |
| 30 s | 6,3 | 5,2 s | zum Buch, 3 s aufnehmen |
| 34 s | 6,5 | 1,7 s | zurück ins Versteck |
| 41 s | 6,14 | 2,6 s | zurück über die Straße |
| 55 s | 18,18 | 20 s | hinter der Lagerhalle nach Osten |
| 63 s | 18,11 | 2,7 s | zum Fluchtwagen, 1 s einsteigen |

Rennen ist also riskant und kostet mehr als die Hälfte des Trupps, wer aktiv kämpft, fährt besser. Die Wertung hat noch keine Folgen
über den Einsatz hinaus, das kommt erst mit einer Kampagne.

Gewonnen ist der Einsatz, wenn ein Agent das Kontobuch trägt und den Fluchtwagen erreicht. Verloren
ist er, wenn alle Agenten ausgeschaltet sind.

## Bewusst noch nicht drin

Basisbau, Ausrüstung, mehrere Waffen, Fahrzeuge, Stockwerke.
