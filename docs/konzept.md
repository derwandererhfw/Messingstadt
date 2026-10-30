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
| Sicht ahnungslose Wache | Kegel von 90° und 5 Feldern in Blickrichtung, im Umkreis von 2 Feldern auch hinter ihr |
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

Die Werte sind erste Annahmen und werden beim Spielen angepasst.

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
zeigen ihren Sichtkegel. Agenten sehen 10 Felder weit, der Spieler kann also vorausplanen.

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
| Gefallene Agenten | Kommen zurück, Beine und Herz fallen auf Stufe 1 |

Der Stand wird im Benutzerordner des Spiels gespeichert (`kampagne.json`). Es gibt bisher nur eine
Mission, „Nächster Einsatz“ spielt sie erneut.

### Umgebung

Die Stadt bewegt sich auch ohne Zutun: Rauch aus Schornsteinen und Kesseln, Dampf aus Gullys, Nieselregen
mit Ringen in den Pfützen, flackernde Laternen (eine mit Wackelkontakt), flackernde Kesselfeuer und
Papierfetzen in Windböen. Das ist reine Darstellung mit eigenem Zufall in `game/Ambience.cs` und
`game/RainLayer.cs`. Die Spiellogik bleibt deterministisch, und die Animation läuft auch in der Pause weiter.

### Schüsse

Nach einem Schuss hält die Figur die Waffe kurz aufs Ziel, auch im Weitergehen, mit kurzem Rückstoß. Dazu kommen Mündungsfeuer, das die Umgebung erhellt, Leuchtspur sowie Blut bei Treffern und Staub bei Fehlschüssen. Alles ist reine Darstellung, nur die Schussrichtung merkt sich die Logik.

### Stand der Spielweisen

Gemessen in der Spiellogik über je 300 Zufallsfolgen mit `tools/Messingstadt.Balance`:

| Spielweise | Siege | Niederlagen | Tote Agenten | Lebenspunkte übrig | Dauer | Wertung |
|---|---|---|---|---|---|---|
| Alle rennen zum Buch, dann zum Wagen | 82 % | 18 % | 2,15 | 79 von 400 | 19 s | Laut |
| Jeder greift die nächste Wache an, dann Buch und Wagen | 92 % | 7 % | 1,10 | 176 von 400 | 36 s | Laut |
| Alle schießen dieselbe Wache aus, dann Buch und Wagen | 78 % | 21 % | 1,58 | 147 von 400 | 34 s | Laut |
| Alle sprinten den ganzen Weg zum Buch und zum Wagen | 29 % | 70 % | 3,43 | 14 von 400 | 15 s | Laut |
| Auf die Straße stellen und kämpfen lassen, dann Buch und Wagen | 61 % | 35 % | 2,62 | 77 von 400 | 47 s | Laut |
| Hinterhalt: einer lockt, alle warten zwischen den Häusern | 15 % | 84 % | 3,71 | 18 von 400 | 29 s | Laut |
| Greta schleicht allein, Klicks auf ±0,5 s genau | 100 % | 0 % | 0 | 400 von 400 | 83 s | Unentdeckt |
| Greta schleicht allein, Klicks auf ±1 s genau | 90 % | 9 % | 0,09 | | 83 s | meist Unentdeckt |

Der Schleichplan ist als Test abgesichert. Eine frühere Fassung mit Klick 7 bei 63 s und ohne Einsteigen wurde im Spiel mit echten Klicks gewonnen:

| Zeit | Klick auf Feld | Zeitfenster | Zweck |
|---|---|---|---|
| 10 s | 6,15 | 20 s | Versteck südlich der Straße |
| 15 s | 6,5 | 2,6 s | über die Straße ins Versteck in der Gasse |
| 30 s | 6,3 | 5,2 s | zum Buch, 3 s aufnehmen |
| 34 s | 6,5 | 1,7 s | zurück ins Versteck |
| 41 s | 6,14 | 2,6 s | zurück über die Straße |
| 55 s | 18,18 | 20 s | hinter der Lagerhalle nach Osten |
| 79 s | 18,11 | 1,4 s | zum Fluchtwagen, 1 s einsteigen |

Rennen ist also riskant und kostet mehr als die Hälfte des Trupps, wer aktiv kämpft, fährt besser. Die Wertung hat noch keine Folgen
über den Einsatz hinaus, das kommt erst mit einer Kampagne.

Gewonnen ist der Einsatz, wenn ein Agent das Kontobuch trägt und den Fluchtwagen erreicht. Verloren
ist er, wenn alle Agenten ausgeschaltet sind.

## Bewusst noch nicht drin

Basisbau, Ausrüstung, mehrere Waffen, Fahrzeuge, Stockwerke, Sound, Speichern.
