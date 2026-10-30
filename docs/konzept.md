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
| Pistole | Reichweite 7 Felder, alle 0,7 s, Agent 12 Schaden, Wache 10 Schaden |
| Trefferchance | 85 %, je Feld Entfernung 5 Punkte weniger, Ziel in Deckung 25 weniger, Schütze in Bewegung 15 weniger, mindestens 10 % |
| Deckung | Ein Hindernis direkt neben dem Ziel in Richtung des Schützen |
| Sicht ahnungslose Wache | Kegel von 90° und 5 Feldern in Blickrichtung, im Umkreis von 2 Feldern auch hinter ihr |
| Sicht alarmierte Wache | 7 Felder rundum |
| Sicht allgemein | Gebäude verdecken, Sicht gilt immer in beide Richtungen |
| Posten | Wachen ohne Rundgang drehen sich alle 3 s um 90° |
| Kontobuch aufnehmen | 3 s im Stehen, wer weggeht, fängt von vorn an |
| Bemerken | 0,4 s plus 0,1 s je Feld Entfernung, bereits alarmierte Wachen 0,2 s |
| Alarm | Jeder Schuss ist im Umkreis von 8 Feldern zu hören, je Feld 0,15 s später |
| Verstärkung | 6 s nach dem ersten Schuss und dann alle 4 s eine Wache nördlich vom Fluchtwagen, höchstens 4 |
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

### Stand der Spielweisen

Gemessen in der Spiellogik über je 300 Zufallsfolgen mit `tools/Messingstadt.Balance`:

| Spielweise | Siege | Niederlagen | Tote Agenten | Lebenspunkte übrig | Dauer | Wertung |
|---|---|---|---|---|---|---|
| Alle rennen zum Buch, dann zum Wagen | 97 % | 2 % | 1,71 | 117 von 400 | 18 s | Laut |
| Erst alle Wachen angreifen | 62 % | 38 % | 2,33 | 88 von 400 | 36 s | Laut |
| Greta schleicht allein, Klicks auf ±0,5 s genau | 100 % | 0 % | 0 | 400 von 400 | 66 s | Unentdeckt |
| Greta schleicht allein, Klicks auf ±1 s genau | 90 % | 9 % | 0,09 | | 66 s | meist Unentdeckt |

Der Schleichplan, einmal im Spiel mit echten Klicks gespielt und als Test abgesichert:

| Zeit | Klick auf Feld | Zeitfenster | Zweck |
|---|---|---|---|
| 10 s | 6,15 | 20 s | Versteck südlich der Straße |
| 15 s | 6,5 | 2,6 s | über die Straße ins Versteck in der Gasse |
| 30 s | 6,3 | 5,2 s | zum Buch, 3 s aufnehmen |
| 34 s | 6,5 | 1,7 s | zurück ins Versteck |
| 41 s | 6,14 | 2,6 s | zurück über die Straße |
| 55 s | 18,18 | 20 s | hinter der Lagerhalle nach Osten |
| 63 s | 18,11 | 2,7 s | zum Fluchtwagen |

Rennen gewinnt also meist, kostet aber fast die Hälfte des Trupps. Die Wertung hat noch keine Folgen
über den Einsatz hinaus, das kommt erst mit einer Kampagne.

Gewonnen ist der Einsatz, wenn ein Agent das Kontobuch trägt und den Fluchtwagen erreicht. Verloren
ist er, wenn alle Agenten ausgeschaltet sind.

## Bewusst noch nicht drin

Basisbau, Ausrüstung, mehrere Waffen, Fahrzeuge, Stockwerke, Sound, Speichern.
