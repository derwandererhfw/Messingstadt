# Spielkonzept

## Welt

Messingstadt, 1927 in einer erfundenen Zeitlinie. Dieselmotoren und Dampfkessel haben sich nie aus den
Städten zurückgezogen. Die Stadt gehört drei Mächten: den Brandt-Werken, der Kontorbank und der
Stadtwache, die offiziell neutral ist. Dagegen steht die Gewerkschaft „Roter Hammer“.

Der Spieler führt eine verdeckte Abteilung der Gewerkschaft. Die Mittel sind Diebstahl, Sabotage,
Befreiung und, wenn es nicht anders geht, Gewalt. Figuren, Konflikt und Verlauf der Kampagne stehen in `story.md`.

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
| Wachhund | 35 | Biss aus 0,8 Feldern, 12 Schaden | Wittert auf 2,5 Felder ringsum und in jedem Licht, sehr schnell, schlägt an. Sein Biss macht den Einsatz nicht laut. Folgt alarmiert 20 s alten Spuren | nicht käuflich |
| Hundeführer | 60 | Pistole, 7 Felder, 10 Schaden | Führt einen Hund an der Leine | 300, der Hund ist dann mit ruhig |
| Laufbursche | 15 | keine | Bemerkt er jemanden, rennt er zur nächsten Wache und meldet es | 50, auch auf der Flucht |

Die Art steht in der Mission je Gegner. Fährt die Maus über einen sichtbaren Gegner, zeigt das Spiel seine Art und ob er käuflich ist. Ein Hund an der Leine nennt in der Mission seinen Führer unter `handler`.

Hund und Laufbursche beschießen Agenten nur auf Befehl, denn der Schuss macht den Einsatz laut. Ohne Befehl tritt ein Agent nach dem Hund, der ihn anfällt: 9 Schaden je Sekunde, leise, aber der Hund beißt in der Zeit weiter. Das kostet etwa die Hälfte der Lebenspunkte. Der Schuss dauert kürzer und alarmiert alle.

Fällt der Führer, läuft der Hund auf den nächsten Agenten los. Sobald einer von beiden kämpft, ist der Hund von der Leine. Auf bestochene Wachen und den Hund eines bestochenen Führers gibt es keinen Angriffsbefehl, ein Rechtsklick darauf bleibt ohne Wirkung. Beim Überfahren steht dann „bestochen“.

Der Laufbursche ist schneller als ein gehender Agent und langsamer als ein rennender. Ein Treffer stoppt ihn. Emil rennt ihm von selbst nach, wenn er ihn bestechen soll, die Puste reicht aber nur für ein kurzes Stück. Kommt die Meldung an, geht die Wache zum gemeldeten Ort, alle Wachen im Umkreis werden alarmiert und die Verstärkung rückt an. Die Wertung bleibt ohne Schuss leise.

### Namen, Schicksal und Erinnerung

Jede Wache kann in der Mission unter `person` ihren vollen Namen tragen, etwa „Hermann Krüger“. Gerufen und beim Überfahren gezeigt wird der Rufname `Unit.CallName`: der Nachname, oder was die Mission unter `call` nennt, etwa „Kurt“ für den Laufburschen. Totenbuch und Zeitung nehmen den vollen Namen. `name` bleibt der Posten, an dem Schlüssel und Hundeführer hängen. Verstärkung bekommt ihre Namen aus `reinforcements.persons`. Der Name steht beim Überfahren vor der Art.

Wachen rufen Namen, ohne dass sich am Ablauf etwas ändert: Begegnen sich zwei auf dem Rundgang auf 1,5 Felder, nennt eine die andere, höchstens alle 30 s je Wache. Fällt eine Wache mit Namen, ruft ihn der nächste Kollege, der sie auf bis zu 8 Felder sieht, einmal. Niedergeschlagene lösen keinen Ruf aus. Die Rufe stehen in `World.Calls`.

Am Ende sagt `World.GuardOutcomes()` für jede Wache, was aus ihr wurde: ahnungslos, aufgeschreckt, gekauft, niedergeschlagen oder tot. Niedergeschlagene sind nicht tot.

Die Kampagne gibt dem nächsten Einsatz in `MissionData.Memory` mit, wie die Stadt nach der Blutschuld zum Trupp steht (hilft, schweigt, gegen den Trupp) und welche Wachen der Trupp schon kennt. Daraus folgt:

| Fall | Wirkung | Decke |
|---|---|---|
| Die Wache wurde früher gekauft | 25 % Nachlass, bemerkt 50 % langsamer. Mit Gretas Nachlass gilt der größere, nicht beide | keine |
| Die Wache hat überlebt, und die Stadt steht gegen den Trupp | nimmt kein Geld, bemerkt 25 % schneller | höchstens 2 Wachen |
| Die Stadt steht gegen den Trupp | Wachen mit `afterBlood` gehen mit, als zweiter Mann einer Streife | höchstens 1 |
| Die Stadt hilft dem Trupp | Wachen mit `atEase` bemerken 50 % langsamer | höchstens 2 |

Mehr als die Decke kostet die Blutschuld im nächsten Einsatz nie, egal wie viele gefallen sind. Gemessen im Nordwall (20 × 20, Groll auf Schließer und Hundeführer): Mit zwei zusätzlichen Streifen fiel „Erst kämpfen, dann befreien“ von 46 % auf 1 %, mit einer auf 15 %, mit Maschinenpistolen von 98 % auf 77 %. Der leise Weg ohne Geld blieb bei 100 %. Darum eine. Ohne `Memory` gilt nichts davon.

Unabhängig vom letzten Einsatz kann die Mission eine Wache mit `noBribe` unbestechlich machen und mit `noticeScale` wachsamer oder nachlässiger (150 heißt 50 % langsamer). `returning` nennt eine Wache aus einem früheren Einsatz, die den Posten übernimmt, wenn sie noch lebt. Wer im Totenbuch steht, rückt auch nicht mehr als Verstärkung an, der Nächste auf der Liste kommt. Die Kampagne füllt `Memory` vor jedem Einsatz aus dem Totenbuch (`GuardMemoryData.From`).

### Telefonist und Kaserne

Nennt `reinforcements.phone` den Posten eines Telefonisten, hängt die Verstärkung am Telefon. Der Telefonist ist unbewaffnet, hat 40 Lebenspunkte und ist für 100 käuflich. Bemerkt er jemanden oder läuft der Alarm, wählt er 3 s lang und bleibt dabei am Apparat.

Am Telefon kann auch ein anderer Posten stehen, etwa der Wachhabende. Mit `phoneAt` steht der Apparat auf einem eigenen, begehbaren Feld: Bei Alarm lässt der Posten alles stehen, geht dorthin und wählt erst dort. Wer ihn auf dem Weg aufhält, hat denselben Vorteil wie mit gekapptem Draht. Führt kein Weg zum Apparat, ruft er von seinem Platz aus an.

| Fall | Erster Mann | Anzahl |
|---|---|---|
| Anruf kommt durch | `phoneDelay` nach dem Anruf, sonst 1 s | alle |
| Kein Anruf: Draht gekappt, Telefonist gekauft, niedergeschlagen oder tot, auch noch während er wählt | `lateDelay` nach dem Alarm, sonst doppelt so spät wie `firstDelay` | `lateCount`, sonst die Hälfte, mindestens einer |
| Kein Telefon in der Mission | `firstDelay` nach dem Alarm | alle |

Der Draht ist eine Tür mit `wire`: Jeder kann ihn kappen, nach den Zeiten für Türen, das Feld bleibt gesperrt. `beds` begrenzt die Kaserne: Mehr rücken nie an, auch nicht mit Fahndung. Schuss, Bote und Telefonist lösen denselben Alarm aus, er läuft nur einmal.

Für Anzeige und Ton: `World.Phone` nennt den Stand (`Idle`, `Fetching` auf dem Weg zum Apparat, `Dialing`, `Called`, `Silent`), `World.PhoneSince` den Takt des letzten Wechsels, `World.TicksUntilCall` die Zeit bis zum fertigen Anruf, `World.PhoneSpot` den Platz des Apparats, `World.PhoneOperatorId` den Mann am Telefon, `World.WireCut` den gekappten Draht und `World.ReinforcementLimit`, wie viele höchstens anrücken.

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
| Greta, Truppführerin | Kennt die Stadtwache | Ist sie dabei, nimmt jede Wache nur den halben Preis. Dabei heißt: Sie lebt, ist im Einsatz und steht höchstens 5 Felder vom Bestechenden oder sieht die Wache selbst auf höchstens 8 Felder. Es zählt der Augenblick, in dem das Geld übergeben wird |

#### Gretas Nachlass

Entscheidung des Nutzers: halber Preis, wenn Greta dabei ist. Die Regel gilt in allen Einsätzen (`World.LeaderBacks`, `World.BribePrice(wache, bestechender)`). Sie hängt an der Person: Wer Greta nach ihrem Tod ersetzt, kennt die Wache nicht. Setzt sie aus oder liegt sie in der Krankenstube, ist sie nicht im Einsatz, und es gilt der volle Preis. Der Text am Mauszeiger über einer Wache nennt den Preis, der gerade gilt („käuflich für 150, Greta ist dabei“ oder „käuflich für 300, mit Greta in der Nähe 150“). Zwei Schalter für Messungen: `LeaderDiscountPercent` (0 schaltet ab) und `LeaderDiscountUses` (so oft je Einsatz).

Gemessen mit `--mission=mission_02 --greta`, je 300 Läufe, Kasse 600. Greta muss dafür mit durch die Gasse, drei statt zwei queren die Wallstraße:

| Spielweise | Bestechung | bleibt | genau | 0,5 s spät | 1 s spät | 2 s spät |
|---|---|---|---|---|---|---|
| Otto und Emil, Greta bleibt am Start (voller Preis) | 500 | 100 | 100 % leise | 100 % leise | 100 % Siege, 52 % leise | 75 % Siege |
| Otto, Emil und Greta, voller Preis (ohne die Regel) | 500 | 100 | 100 % leise | 8 % Siege | 5 % | 2 % |
| Otto, Emil und Greta, halber Preis | 250 | 350 | 100 % leise | 9 % Siege | 5 % | 2 % |
| dasselbe, ein Drittel Nachlass | 335 | 265 | 100 % leise | 9 % | 5 % | 3 % |
| dasselbe, halber Preis nur für die erste Wache | 350 | 250 | 100 % leise | 8 % | 5 % | 2 % |
| Otto allein mit Dietrich und Totschläger | 0 | 400 | 100 % leise | 100 % leise | 52 % | 26 % |

- Der halbe Preis kippt den Einsatz nicht. Er spart 250, kostet aber die Nachsicht: Zu dritt ist das Fenster auf der Wallstraße so eng, dass schon 0,5 s Verspätung fast immer auffliegt. In 272 von 300 Läufen bemerkt die Wallstreife 2 die Letzte, die quert.
- Die Fassungen mit weniger Nachlass ändern am Ausgang nichts, nur an der Kasse. Deshalb bleibt es beim halben Preis.
- Absicherung: Fliegt der Weg mit Greta auf und der Trupp kämpft, gewinnt er mit einer Schrotflinte vom gesparten Geld in 54 bis 63 % der Läufe, ohne Nachlass mit Pistolen in 49 bis 62 %. Eine Maschinenpistole (600) ist neben der Bestechung auch zum halben Preis nicht bezahlbar.
- Greta im dunklen Durchgang stehen lassen und den Hundeführer von dort „sehen“ geht nicht auf: Er bemerkt Emil in der Gasse, ehe er nah genug ist. Gemessen 0 % Siege.
- Otto allein behält seinen Grund: der billigste Weg, der 0,5 s verzeiht. Otto und Emil zu zweit bleiben der sicherste. Mit Greta wird es billig und knapp.
- Einsatz 1: Das Werkzeug hat dort keine Spielweise mit Bestechung, die gewinnt. Der leise Weg (Greta schleicht allein) braucht kein Geld. Geändert sind nur die Preise: Wachmann und Schütze 100 statt 200, Offizier 200 statt 400, Hundeführer 150 statt 300, Laufbursche 25 statt 50.
- Im Spiel gespielt: `greta_nachlass.txt`, leise gewonnen mit 250 Bestechung. `einsatz2_leise.txt` prüft den vollen Preis, wenn Greta am Start bleibt.

### Ausrüstung

Jeder Agent hat 4 Plätze. Zur Grundausstattung gehört die Pistole, dazu kommt die Kiste der Truppe für Gekauftes, das gerade niemand trägt. Der Verkauf bringt die Hälfte des Preises zurück. Wer stirbt, verliert seine Ausrüstung, wer in die Krankenstube kommt, behält sie. Verbrauchtes ist nach dem Einsatz weg. Die Daten stehen in `data/waffen.json`, die Schnittstelle für die Waffenkammer in `docs/ausruestung.md`.

| Gegenstand | Preis | Werte | Besonderheit |
|---|---|---|---|
| Pistole | 100 | 16 Schaden, 7 Felder, alle 0,7 s, 10 Schuss, 0,8 s Nachladen | Grundausstattung, auf 8 Felder zu hören |
| Schrotflinte | 350 | 45 Schaden, 4 Felder, alle 0,8 s, 2 Schuss, 1,5 s Nachladen | +15 auf Treffer, aber 10 weniger je Feld. Auf 10 Felder zu hören |
| Maschinenpistole | 600 | 12 Schaden, 6 Felder, alle 0,2 s, 20 Schuss, 2 s Nachladen | Streut (-5), auf 10 Felder zu hören |
| Totschläger | 120 | 25 Schaden, 1,6 Felder, alle 0,8 s | Lautlos, macht den Einsatz nicht laut. Eine Wache, die noch nicht kämpft, geht sofort zu Boden, außer dem Werkschutz |
| Dietrich | 80 | | Schlosser öffnen in 1 statt 2 s, alle anderen in 4 s |
| Rauchtopf | 60 | Wurf 6 Felder, Wolke 1,5 Felder, 12 s | Niemand sieht hindurch, hinein oder heraus, auch nicht im Lageplan. Einmal |
| Verbandszeug | 75 | 40 Lebenspunkte | 3 s still stehen, jeder Befehl bricht ab, kein Feuer dabei. Einmal |

Im Einsatz wechselt E die Waffe der Gewählten (0,5 s), G wirft den Rauchtopf auf das Feld unter der Maus, H legt den Verband an. Die Akte zeigt die Waffe in der Hand und die Schüsse im Magazin. Ein leeres Magazin lädt von selbst nach. Die Pistole der ersten Fassung ohne Nachladen gilt nur noch, wenn eine Mission keinen Katalog hat, etwa in alten Tests. Maschinenpistole und Schrotflinte klingen anders, der Totschläger ist nur als dumpfer Treffer aus der Nähe zu hören. Der Rauch ist vorläufig ein grauer Ballen ohne eigene Grafik.

Gemessen mit `--waffen` im Balance-Werkzeug, je 300 Läufe, alle Agenten gleich ausgerüstet. Wer unter die Hälfte fällt, legt den Verband an, sobald ihn keine kämpfende Wache sieht:

| Ausrüstung | Kosten Trupp | Alle angreifen | Feuer bündeln | Zum Buch gehen |
|---|---|---|---|---|
| Pistole ohne Nachladen (alte Regel) | | 69 % | 71 % | 44 % |
| Pistole, 10 Schuss | 400 | 64 % | 68 % | 44 % |
| Pistole und Verband | 700 | 66 % | 68 % | 43 % |
| Schrotflinte | 1400 | 72 % | 88 % | 81 % |
| Maschinenpistole | 2400 | 83 % | 91 % | 90 % |
| Gemischt: MP, Schrot und Dietrich, Pistole und Verband, Schrot | 1555 | 53 % | 64 % | 70 % |

Gemischte Trupps mit `--preis`: Die Pistole bleibt als zweite Waffe, gewechselt wird wie ein Spieler mit E. Die teure Waffe kommt in die Hand, wenn sie so weit reicht wie die der meisten im Trupp, sonst erst, wenn ein Gegner nah ist. Wer allein kürzer reicht, steht beim Bündeln vorn und fällt zuerst. Feuer bündeln, ohne Zukauf 68 %: eine Schrotflinte 73 %, zwei 78 %, vier 88 %. Eine MP 69 %, vier 91 %. Je Mark ist die Schrotflinte die bessere Wahl, die volle MP-Ausrüstung verzeiht dafür mehr, auch beim bloßen Losrennen.

Kampagne mit Einkauf (`--einkauf`, Feuer bündeln, je 100 Kampagnen mit 6 Einsätzen): Wer das Startgeld sofort ausgibt, kann nach der ersten Niederlage weder Arzt noch Anwerben bezahlen und verliert öfter (Verbände 26 %, Schrotflinte 26 % statt 36 %). Mit 250 Rückhalt für das Anwerben kauft man mit 300 Startgeld kaum etwas, die Siege bleiben bei 36 bis 37 %. Erst mit 600 Startgeld lohnt der Einkauf: sofort eine Schrotflinte, 250 zurück, dann 49 %. Seit dem Stadtplan beginnt ein neues Spiel deshalb mit 600.

Totschläger: Mit `--knueppel-plan` sucht das Werkzeug wie beim Schleichplan für Greta die Zeitfenster, in denen ein Angriffsbefehl eine Wache lautlos niederschlägt, ohne dass eine andere kämpft. Bei 1,2 Feldern Schlagweite ging das nur beim Offizier, alle anderen Wachen bemerken Greta auf 2 Felder auch von hinten und werden von einer zweiten Wache gedeckt. Bei 1,6 Feldern gehen Offizier und Schütze, danach bündelt der Trupp das Feuer: 79 % Sieg mit Klicks bis 0,25 s daneben, 74 % bis 0,5 s, statt 68 % ohne Knüppel. Bei 1,8 Feldern gingen fünf Wachen, aber nur mit Klicks auf den Takt genau. Die Spielweise ohne Plan (`--knueppel`, Greta lauert im Versteck und schlägt zu, wenn die Wache sie vor dem Umdrehen nicht mehr sieht) schlägt niemanden lautlos nieder, ihre Ergebnisse taugen nicht als Maß für die Waffe.

In der ersten Mission führt die Hintertür der Lagerhalle zu einem Durchgang zur Querstraße und zur
Lohnkasse (300). Beute zählt nur, wenn der Trupp entkommt. Papiere fälschen ist noch nicht gebaut.

### Auftragsart Befreien

Einsatz 2 „Die Wachstube“ im Nordwall (`data/mission_02.json`, Grundriss und Abweichungen von der Story-Vorlage in `docs/einsatz_02_grundriss.md`). Statt eines Gegenstands gibt es Gefangene.

| Regel | Wert |
|---|---|
| Gefangener | 60 Lebenspunkte, 2,0 Felder/s, rennt und kämpft nicht. Sitzt hinter einer Zellentür |
| Befreien | Die Zelle ist offen und ein Agent steht 2 s still auf höchstens 2,2 Felder. Danach folgt der Gefangene ihm |
| Folgen | Er läuft dem Agenten nach und bleibt gut ein Feld vor ihm stehen. Rechtsklick auf ihn: Er folgt dem ersten gewählten Agenten, ein zweiter Rechtsklick lässt ihn warten |
| Führer fällt | Der Befreite bleibt stehen, bis ein Agent auf 3 Felder herankommt, und folgt dann diesem |
| Wachen | Übersehen, wer in der Zelle sitzt. Befreite bemerken sie wie Agenten, das macht den Einsatz „Leise“. Sie schießen zuerst auf Agenten: Ein Befreiter zählt wie 3 Felder weiter weg |
| Einsteigen | Steht der Führer im Wagenbereich, suchen sich die Befreiten dort selbst ein freies Feld |
| Sieg | Alle lebenden Gefangenen und ein Agent stehen 1 s im Wagenbereich. Wer noch in der Zelle sitzt, verhindert den Sieg |
| Niederlage | Alle Agenten oder alle Gefangenen sind tot. Stirbt nur einer, geht es weiter |
| Schlüssel | Eine Wache kann Schlüssel zu Türen tragen. Wer sie besticht, bekommt sie, fällt sie, liegen sie am Boden und der erste Agent dort nimmt sie. Mit Schlüssel geht eine Tür in 0,5 s auf, auch ohne Schlosser |
| Verstärkung | Kann eine andere Art sein, in Einsatz 2 der Werkschutz |
| Kampagne | Merkt sich je Gefangenem, ob er lebend herauskam (`MissionRecord.Prisoners`, `Campaign.PrisonerAlive`) |

In Einsatz 2 trägt der Schließer die Schlüssel zu beiden Zellen und der Türposten den zur Pforte. Die Texte im Telegrammstreifen und die Gespräche der Wachen stehen in der Missionsdatei (`rescueTexts`, `chatter`).

Gemessen mit `dotnet run -c Release --project tools/Messingstadt.Balance -- --mission=mission_02`, je 300 Läufe, Kasse 600:

| Spielweise | Einkauf | Siege | Tote Agenten | Wertung |
|---|---|---|---|---|
| Alle gehen zur Pforte, Otto öffnet | | 4 % | 3,78 | Laut |
| Otto allein, der Rest wartet | | 0 % | 3,92 | Laut |
| Erst kämpfen, dann befreien | | 46 % | 2,55 | Laut |
| Laut durchs Hoftor | | 40 % | 2,67 | Laut |
| Laut durchs Hoftor mit Rauchtopf im Postengang | 60 | 40 % | 2,65 | Laut |
| Direkt zum Hoftor, ohne vorher zu kämpfen, mit oder ohne Rauchtopf | | 0 % | 3,99 | Laut |
| Erst kämpfen, eine Maschinenpistole | 600 | 84 % | 0,84 | Laut |
| Erst kämpfen, Schrotflinte, Rauchtopf, zwei Verbände | 560 | 70 % | 1,86 | Laut |
| Erst kämpfen, vier Schrotflinten | 1400 | 19 % | 2,98 | Laut |
| Erst kämpfen, vier Maschinenpistolen | 2400 | 98 % | 0,17 | Laut |
| Leise ohne Geld: Otto allein nach Lageplan, Totschläger | 120 | 100 % | 0 | Leise |
| Dasselbe, Klicks bis 0,5 s zu spät | 120 | 14 % | 0,90 | 91 % Laut |
| Otto allein mit Totschläger und Dietrich | 200 | 100 % | 0 | Leise |
| Dasselbe, Klicks bis 0,5 s zu spät | 200 | 100 % | 0 | Leise |
| Dasselbe, Klicks bis 0,75 s zu spät | 200 | 69 % | | Leise oder verloren |
| Dasselbe, Klicks bis 1 s zu spät | 200 | 52 % | | Leise oder verloren |
| Dasselbe, Klicks bis 1,5 s zu spät | 200 | 35 % | | 23 % Leise |
| Dasselbe, Klicks bis 2 s zu spät | 200 | 26 % | | 12 % Leise |
| Leise mit Geld: Otto und Emil nach Lageplan, Hundeführer und Schließer gekauft | 500 Bestechung | 100 % | 0 | Leise |
| Dasselbe, Klicks bis 0,5 s zu spät | 500 Bestechung | 100 % | 0 | Leise |
| Dasselbe, Klicks bis 1 s zu spät | 500 Bestechung | 100 % | 0,01 | 52 % Leise, 47 % Laut |
| Dasselbe, Klicks bis 1,5 s zu spät | 500 Bestechung | 100 % | 0 | 36 % Leise, 64 % Laut |
| Dasselbe, Klicks bis 2 s zu spät | 500 Bestechung | 75 % | 0,92 | 21 % Leise, 79 % Laut |

In jedem Sieg kamen beide Gefangenen lebend heraus. Was die Zahlen sagen:

- Wer ohne Plan hineingeht, verliert.
- Der offene Kampf kostet mit der Grundausstattung den halben Trupp. Eine einzige Maschinenpistole hebt ihn von 48 auf 82 %. Schrotflinten allein taugen hier nicht, die Wege sind zu lang.
- Leise ohne Geld: Der Hundeführer geht auf jedem Rundgang ein Stück am Fuß des Walls nach Westen, bis (1, 2), und ist so lange aus der Gasse. Vorher drehte er nur in der Gasse, und Otto wurde in 271 von 300 Läufen an der Pforte erwischt, sobald der Klick bis 0,5 s zu spät kam (9 % Siege). Jetzt verzeiht der Weg mit Dietrich 0,5 s ganz und 1 s zur Hälfte. Ohne Dietrich, mit 2 s an der Pforte, geht er nur bei genauen Klicks. Eng ist danach die Querung der Wallstraße, wie beim Weg mit Geld.
- Der Weg mit Geld bleibt der sichere: halb so lang (63 gegen 121 s, Otto allein wartet 81 s auf sein Fenster), und wer zu spät klickt, gewinnt meist trotzdem laut, während Otto allein dann fällt.
- Leise mit Geld ist der verlässliche Weg und kostet 500.
- „Unentdeckt“ erreicht keine gemessene Spielweise: Wer gekauft oder niedergeschlagen wird, hat den Trupp vorher bemerkt.

- Der Rauchtopf im Postengang ändert am Hoftor nichts (41 gegen 40 %): Das Gefecht entscheidet sich auf der Wallstraße gegen Streifen, Türposten und Verstärkung, nicht am Wallposten.
- Wo es eng wird, zeigt `--wer <Takte>`: Es zählt, welche Wache Otto zuerst bemerkt und wo er dann steht.

Im Spiel mit echten Wachen gespielt sind beide Wege. Laut (`einsatz2_laut.txt`): vier Maschinenpistolen, 13 Wachen ausgeschaltet, ein Agent gefallen, beide Gefangenen im Wagen; mit sechs verschobenen Startzeiten sechs Siege mit keinem oder einem Gefallenen. Leise (`einsatz2_leise.txt`): Otto und Emil kaufen Hundeführer und Schließer, Wertung leise, niemand fällt, alle zehn Wachen stehen noch.

Stellschrauben für den leisen Weg, gemessen mit `--schrauben` vor der Änderung am Hundeführer. Die Leitung hat entschieden, keine davon einzubauen:

| Stellschraube | Spielweise | Kosten | genau | 0,5 s spät | 1 s spät |
|---|---|---|---|---|---|
| A: Schließer kostet 350, das Geld reicht nur für einen | Hundeführer kaufen, Schließer niederschlagen | 420 | 100 % leise | 100 % leise | 52 % leise |
| | Schließer kaufen, Hund zu zweit abpassen | 430 | kein Fenster | | |
| B: Zweiter Wachmann im Hof | vier Spielweisen | | 0 % Siege | 0 % Siege | |
| C: Laufbursche im Hof, unbewaffnet, meldet, kostet 50 | alle drei kaufen | 550 | 100 % leise | 100 % leise | 52 % leise |
| | Hundeführer und Bursche kaufen, Schließer niederschlagen | 470 | 100 % leise | 100 % leise | 52 % leise |
| | Bursche nicht beachtet | 500 | 0 % Siege | 0 % Siege | 0 % Siege |

A und C machen den Weg teurer, aber nicht riskanter: Das Wagnis liegt in den zwei Querungen der Wallstraße, nicht im Hof. B macht ihn mit den gemessenen Spielweisen unmöglich. Eine Uhr („Um sechs kommt der Werkschutz“) bräuchte eine neue Regel und ist nicht gemessen.

Die Zellen liegen im Gebäude. Wer auf einem Feld seiner Zelle steht, wird nicht gezeichnet (`World.HiddenInCell`), sonst klebte er außen an der Fassade. Solange die Tür zu ist, steht sein kurzer Name neben dem Schloss. Ein Befreiter bleibt nie in der Zelle stehen, auch wenn sein Führer direkt vor der Tür wartet, sondern tritt auf das nächste freie Feld davor.

#### Niedergeschlagen ist nicht tot

Der Totschläger schlägt eine ahnungslose Wache nieder. Sie liegt, lebt und steht in diesem Einsatz nicht wieder auf, sieht, hört und meldet nichts und verliert ihre Schlüssel. Im Kern ist sie aus dem Spiel (`Alive` ist false) und trägt `Unit.KnockedOut`. Wirklich tot ist nur, für wen `Unit.Dead` gilt. Schlagzeile, Fahndung und Totenbuch zählen nur Tote, die Wertung bleibt leise. Wer auf einen Niedergeschlagenen schießt und trifft, tötet ihn: Er ist dann ein Toter wie jeder andere, und der Schuss macht den Einsatz laut. Mit dem Totschläger weiterschlagen tötet nicht. Findet eine andere Wache einen Niedergeschlagenen, geschieht in den Regeln nichts, wie bei einem Toten auch: Wachen bemerken bisher keine Liegenden. Der Ruf „Wach auf!“ wäre eine neue Regel und ist nicht gebaut.

#### Türen der Stadt

Eine Tür mit `helper` in der Mission steht zu Beginn offen, wenn der Bewohner hilft. Ob er hilft, entscheidet `Campaign.HouseHelps(helper, mission)`, `Campaign.Prepare` trägt die Helfer in `MissionData.Helpers` ein. `openAtSupport` verlangt zusätzlich einen Rückhalt und ist bisher überall 0. Sonst braucht die Tür Schlosser, Dietrich oder Schlüssel wie jede andere. In Einsatz 2: Pforte des Totengräbers, Hoftür der Witwe, Kellerluke der Kneipe.

Bewohner, die nicht helfen, stehen als `residents` in der Mission (Kennung und Feld). Ob sie rufen, hängt an der Haltung der Stadt, `Campaign.Prepare` trägt `Campaign.Stance` als `MissionData.Stance` ein:

| Haltung | Blutschuld | Bewohner |
|---|---|---|
| Stadt hilft | 0 bis 1 | Niemand ruft, auch wer nicht hilft, schaut weg. |
| Stadt schweigt | 2 bis 3 | Wer jemanden vom Trupp oder einen Befreiten auf 2,5 Felder mit freier Sicht sieht, ruft einmal. Die nächste ahnungslose Wache in 9 Feldern Hörweite geht an die Stelle und sieht nach. |
| Stadt gegen den Trupp | 4 bis 6 | Wie zuvor, aber die zwei nächsten Wachen gehen. |

Jeder Bewohner ruft höchstens einmal je Einsatz. Ein Ruf macht den Einsatz bemerkt, nicht laut. Gekaufte Wachen, Hunde und Wachen, die schon suchen oder kämpfen, gehen nicht. Ist keine Wache in Hörweite, verhallt der Ruf. Für Bild und Ton steht jeder Ruf in `World.ResidentCalls` (Takt, Kennung, Wache oder -1). In Einsatz 2: Gerichtsdiener am Portal des Amtsgerichts, Frau des Wachmanns am Osthof, Kasernenkoch an der Küchentür. Ohne Kampagne, also auch im Messwerkzeug, hilft die Stadt. Die Rückwege sind mit rufenden Bewohnern noch nicht gemessen.

#### Der lange Rückweg, Messung auf 30 × 30

Gemessen mit `--mission=mission_02 --wege`, je 300 Läufe, Kasse 600. 15 Gegner statt 10, der Wagen steht im Kohlenhof. Die Wegsuche bricht Gleichstände nach Feldnummern, die an der Kartenbreite hängen: Der Kern ist Feld für Feld gleich, die Figuren nehmen aber zwischen gleich langen Wegen andere Felder, deshalb ist alles neu gemessen.

| Rückweg | Spielweise | Türen zu (kein Helfer) | Helfertüren offen |
|---|---|---|---|
| Friedhof und Böschung | Otto und Emil mit Geld (500) | 100 % leise, 108 s | 100 % leise, 90 s |
| | Otto allein mit Dietrich | 100 % leise, 158 s | 100 % leise, 158 s |
| Beamtenhöfe | Otto und Emil mit Geld | 100 % leise, 175 s | genau 100 % leise, 124 s. Bis 0,5 s spät 14 % Siege, bis 1 s 37 % |
| | Otto allein mit Dietrich | 100 % leise, 220 s | 100 % leise, 185 s |
| Friedhofsgang ohne Tür | Otto und Emil mit Geld | 39 % Siege, immer laut | ebenso |
| | Otto allein mit Dietrich | 99 % Siege, immer laut | ebenso |
| Laut, Trupp zusammen | Pistolen, eine MP, Schrot, vier MP | 0 % Siege, der Trupp fällt in 22 bis 33 s | |

Was die Zahlen sagen, und was sie nicht sagen:

- Die leisen Wege tragen. Friedhof ist der sichere, lange Weg. Die Höfe sind kürzer, sobald die Helfer öffnen, aber empfindlich: Die Tür zum Osthof liegt in der Mittelgasse des Nachtwächters.
- Der Friedhofsgang ohne Tür ist die Falle: An seinem Ausgang steht eine Laterne, und die Südstreife läuft daran vorbei.
- Der laute Weg ist kaputt. Wer am Start schießt, hat binnen Sekunden Südstreife, Nachtwächter, Türposten, Wachhabenden, beide neuen Posten und die Verstärkung auf sich. Ziel war 40 bis 50 % mit Grundausstattung. Das ist offen und hängt an der Regel für Telefon und Verstärkung, die die Gegner-Session baut.
- Die Zeiten der leisen Wege sind lang, weil die Mess-Spielweise vor jedem Wegstück wartet, bis der Lageplan es ganz frei zeigt, höchstens 40 s. Ein Spieler geht früher.
- Im Spiel nachgestellt ist bisher nur der Hinweg bis zur Befreiung (`einsatz2_leise.txt`, `greta_nachlass.txt`) und der ganze Ablauf mit ruhiggestellten Wachen (`einsatz2_befreien.txt`). Der Heimweg mit echten Wachen wurde im Spiel auf beiden Wegen am Ende bemerkt.

#### Nordwall mit Telefon und Kaserne, Messung vom 13.11.2026

Echte Daten aus `data/mission_02.json`: Stadtwache aus der Kasernentür, der Wachhabende ruft an, erster Mann 8 s nach Alarm, dann alle 5 s, drei Mann, drei Betten. Ohne Anruf ein Mann nach 16 s. Je 300 Läufe, Sieg in Prozent.

Warum die früheren Zahlen bei 0 % lagen: Die Pläne im Messwerkzeug passten nicht zur großen Karte. Der laute Bot schoss vom Start aus auf die Südstreife und lief dann der nächsten Wache nach, bis er an der Ecke Ostgasse und Wallstraße im Licht stand, acht Felder vor der Kasernentür. „Leise mit Geld“ nahm ohne Angabe den Friedhofsgang, auf dem die Südstreife den Trupp immer bemerkt. Jetzt gilt ohne Angabe der Friedhof, und für den lauten Weg gibt es den Plan „Laut aus einer Stellung“ (`--stellung`): an einen Platz gehen, dort das Feuer eröffnen, die Wachen kommen lassen, nach 15 s Ruhe die Gefangenen holen.

| Laut aus der Stellung | Pistolen | vier MP | vier MP + Verband |
|---|---|---|---|
| Durchgang (7, 15) | 13 | 82 | 76 |
| Friedhofsgang (7, 20) | 38 | 69 | 74 |
| Südgasse West (4, 27) | 15 | 52 | 58 |
| Start an der Böschung (10, 29) | 7 | 43 | 57 |
| Der Wache nachlaufen, alter Plan | 0 | 14 | 14 |

| Verstärkung, vier MP | Durchgang | Friedhofsgang | Südgasse West | Start |
|---|---|---|---|---|
| Draht gekappt: ein Mann nach 16 s | 88 | 79 | 68 | 70 |
| drei Mann, alle 5 s (Daten) | 82 | 69 | 52 | 43 |
| vier Mann, vier Betten | 80 | 67 | 37 | 21 |
| fünf Mann, fünf Betten | 80 | 66 | 16 | 5 |
| fünf Mann, alle 3 s | 78 | 67 | 15 | 5 |
| sechs Mann, alle 3 s | 76 | 66 | 6 | 1 |

Die Verstärkung trifft nur, wer im Offenen steht. In den zwei engen Gängen kommen die Wachen einzeln, dort ändert die Zahl der Betten fast nichts. Die Daten bleiben darum bei drei Mann. Wer die engen Gänge schwerer haben will, braucht eine andere Schraube als die Kaserne, etwa einen zweiten Ankunftsort im Süden.

Das Schleichlicht `data/light_02.json` ist am 13.11.2026 neu gebacken (`python tools/art/bake_light.py --map=nordwall --nr=02`) und folgt jetzt dem Grundriss: Die Laterne im Südwesten steht bei (8,5, 26,6), der Bretterzaun am Kohlenhof wirft Schatten. Sturmlaterne am Wagen und Kohlenkorb sind reine Bildlichter, am Wagen bleibt es bei 14 bis 18 Punkten. Die Tabelle ist mit diesem Licht gemessen. Gegenüber dem alten Licht änderten sich nur die zwei späten Zeilen mit Geld (1,5 s: 67 auf 45, 2 s: 39 auf 27), alles andere blieb gleich.

| Leise, Rückweg über den Friedhof | Sieg | davon leise |
|---|---|---|
| Otto und Emil mit Geld, bis 1 s spät | 100 | 100 |
| dasselbe, 1,5 s spät | 45 | 45 |
| dasselbe, 2 s spät | 27 | 27 |
| Otto allein mit Dietrich, bis 0,5 s spät | 100 | 100 |
| Totschläger ohne Dietrich, Klick genau | 100 | 100 |
| dasselbe, bis 0,5 s spät | 8 | 8 |
| Rückweg über die Höfe, mit Geld oder allein, Klick genau | 100 | 100 |
| Höfe mit offenen Helfertüren, mit Geld, 0,5 s spät | 14 | 9 |
| Abkürzung Friedhofsgang, mit Geld | 39 | 0 |

Nicht gemessen: Stadt gegen den Trupp und rufende Bewohner mit den echten Daten. Der Bot schießt genauer zusammen als ein Mensch, die lauten Zahlen sind eher eine Obergrenze.

### Mehrere Einsätze

Jeder Einsatz hat eine Missionsdatei, eine Regelkarte, eine Lichtkarte und einen Grafikordner: `data/mission_02.json` nennt `mapId`, `lightId` und `scene`, die Stadt liegt in `game/assets/maps/<scene>/` mit demselben Aufbau wie die erste. Welcher Einsatz gespielt wird, bestimmt das gewählte Viertel im Stadtplan. Hat es eine andere Stadt, lädt das Spiel die Szene neu und setzt mit derselben Akte fort. Der Grundriss `data/scene_02.json` ist die gemeinsame Quelle: `tools/level/scene_to_map.py` schreibt daraus die Regelkarte und zeigt, was die Kamera verdeckt, `tools/level/placeholder_city.py` baut eine schlichte Platzhalterstadt samt Licht. Zielorte, Türen mit Spielbedeutung und Hauptwege dürfen nie hinter hohen Gebäuden liegen.

### Kampagne

Die Logik steht in `src/Messingstadt.Core/Campaign.cs`. Sie liest am Ende eines Einsatzes nur, was die Spielregeln ergeben haben: Wertung, Lebenspunkte und Stürze der Agenten, tote und bestochene Wachen, Beute.

| Regel | Wert |
|---|---|
| Startgeld | 600 |
| Lohn pro Sieg | Grundlohn des Viertels, im ersten 300. Dazu 150 bei Leise, 300 bei Unentdeckt |
| Ausbau | Beine, Herz und Arme, je Stufe 1 bis 3. 150 von Stufe 1 auf 2, 300 von Stufe 2 auf 3 |
| Arme | Die ruhige Hand, in den Daten `arms`. Je Stufe über der ersten 3 Punkte Trefferchance beim Schießen und 10 % weniger Zeit beim Nachladen und beim Waffenwechsel. Der Nahschlag mit dem Totschläger bleibt unberührt, damit der leise Weg nicht leichter wird. Wer fällt, verliert die Stufen wie bei Beinen und Herz, Neue beginnen auf Stufe 1 |
| Niederlage | Kein Lohn, Beute verloren, ausgegebenes Geld bleibt ausgegeben |
| Erster Sturz | Krankenstube: Beine und Herz fallen auf Stufe 1, der Agent setzt einen Einsatz aus. Gilt auch bei einer Niederlage |
| Zweiter Sturz | Tot. Der Platz bleibt leer, bis jemand angeworben wird |
| Angeschlagen | Wer unter der Hälfte der Lebenspunkte entkommt, setzt einen Einsatz aus |
| Arzt | 100, der Agent ist sofort wieder einsatzbereit, auch aus der Krankenstube |
| Anwerben | 250 für einen leeren Platz. Der Rekrut beginnt auf Stufe 1 und übernimmt die Fähigkeit des Platzes |
| Neue | In fester Reihenfolge nach der Fähigkeit des Platzes: Karl Hoffmann, Frieda Kranz, Willi Saß, Erwin Pohl (Schlosser und Buchhalter), Lotte Becker, Max Kühn (ohne Fähigkeit). Danach „Rekrut“ mit Nummer. Texte von Story in `CampaignTexts` |
| Notbesetzung | Sind weniger als 2 Agenten einsatzbereit, kommen Rekruten umsonst, danach Ruhende trotzdem mit. Die Kampagne hängt nie fest |
| Abbrechen nach dem ersten Schuss | Jeder im Trupp gilt als gestürzt |
| Fahndung 0 bis 4 | Unentdeckt −1, Leise mit Bestechung −1, Laut +1, ab 3 toten Wachen nochmal +1. Je angefangene zwei Stufen eine Wache mehr in der Verstärkung |
| Rückhalt 0 bis 5 | Sieg +1, Niederlage −1. Nach jedem zählenden Einsatz 50 Beitrag je Stufe, auch nach einer Niederlage |
| Merker | Je Einsatz Versuche und für den Sieg Wertung, tote und bestochene Wachen, tote Agenten und Beute. Für die Story |
| Totenbuch | Jeder zählende Einsatz trägt die toten Wachen und die toten Agenten mit Name, Einsatz, Viertel und Nacht ein. Niedergeschlagene, Verwundete und Hunde stehen nicht darin. Tot bleibt tot: Wer einen Einsatz wiederholt, trägt denselben Namen nicht zweimal ein. Dazu merkt sich die Kampagne den letzten Stand jeder Wache (tot, gekauft, niedergeschlagen, ahnungslos, überlebt) und die toten Wachen der letzten Nacht je Viertel. Abfragen in `Campaign.Ledger.cs` |
| Blutschuld 0 bis 6 | Gilt für die ganze Stadt. Der erste Tote einer Nacht ist frei, jeder weitere erhöht sie um 1, höchstens um 3 je Nacht. Eine Nacht ohne tote Wache tilgt 2. Bei 0 bis 1 hilft die Stadt, bei 2 bis 3 schweigt sie, ab 4 stellt sie sich gegen den Trupp (`Campaign.BloodDebt`, `Campaign.Stance`). Niedergeschlagene und Gekaufte zählen nicht. Reihen über sechs Nächte: ein Toter je Nacht 0, 0, 0, 0, 0, 0. Zwei je Nacht 1, 2, 3, 4, 5, 6. Drei je Nacht 2, 4, 6, 6, 6, 6. Vier oder mehr je Nacht 3, 6, 6, 6, 6, 6. Einmal laut, dann leise 3, 1, 0, 0, 0, 0. Abwechselnd laut und leise 3, 1, 4, 2, 5, 3 |
| Helfende Häuser | Jedes Haus hilft, solange die Stadt hilft. Die Wirtin hilft auch, wenn die Stadt schweigt, solange Greta lebt (`Campaign.HouseHelps`) |
| Stadtviertel | Fünf Viertel aus `data/stadtplan.json`, am Anfang gehören alle den Brandt-Werken. Ein Sieg im gewählten Viertel gibt es dem Roten Hammer. Wählbar ist ein Viertel, wenn es einen Einsatz hat und seine Voraussetzungen erobert sind. Bisher hat nur das erste einen Einsatz, die anderen sind sichtbar, aber gesperrt. Eroberte Viertel bleiben spielbar |

Ein Abbruch vor dem ersten Schuss zählt nicht: Nur das Geld für Bestechungen bleibt ausgegeben.

Der Stand wird im Benutzerordner des Spiels gespeichert, je Akte eine Datei (`kampagne.json`, `kampagne_2.json`, `kampagne_3.json`, Version 7). Ältere Stände werden übernommen: Kasse, Einsätze und Stufen bleiben. Stände ohne Versionsnummer bekommen gesunde Agenten, Stände vor der Waffenkammer eine Pistole je Agent, und wer vor dem Stadtplan schon einen Einsatz gewonnen hat, hält das erste Viertel. Stände vor dem dritten Ausbau haben Arme auf Stufe 1. Stände vor dem Totenbuch haben ein leeres Buch, nur die eigenen Toten stehen ohne Einsatz darin. Unlesbare oder neuere Stände werden verworfen und je Akte als `kampagne*.alt.json` beiseitegelegt. Es gibt bisher nur eine Mission, „Nächster Einsatz“ spielt sie erneut.

Der Stadtplan ist das Modell für die Einsatzwahl (`CityMap.cs`, Abfragen in `Campaign`: `Owner`, `State`, `CanSelect`, `TrySelect`, `Selected`). Der Bildschirm dazu fehlt noch, bis dahin wählt das Spiel das erste offene Viertel selbst. Namen und Texte in `data/stadtplan.json` stammen wörtlich aus `docs/story/stadtplan-vorlage.json`: Kesselviertel, Nordwall, Gleisviertel, Kontorviertel, Altstadt. Jedes Viertel sperrt das nächste und bringt im Feld `locked` seinen Satz für „gesperrt“ mit. Ein sechster Einsatz nach allen fünf Vierteln ist vorgesehen, aber nicht gebaut: Er passt als weiterer Eintrag mit mehreren Voraussetzungen ins Modell. Ein höherer Beitrag je erobertem Viertel ist nur ein Vorschlag und nicht gebaut.

Gemessen mit `tools/Messingstadt.Balance -- --kampagne`, je 100 Kampagnen mit 6 Einsätzen derselben Mission, 600 Startgeld, ohne Einkauf. Zwischen den Einsätzen erst Arzt, dann Anwerben, der Rest geht in den Ausbau:

| Spielweise | Siege | Tote je Kampagne | Einsätze ohne vollen Trupp | Kasse am Ende |
|---|---|---|---|---|
| Feuer bündeln | 36 % | 6,7 | 29 % | 162 |
| Zum Buch gehen | 33 % | 7,5 | 31 % | 191 |
| Alle angreifen | 32 % | 6,7 | 33 % | 135 |
| Stehen und schießen | 0 % | 8,0 | 66 % | 0 |

Die Zahlen sind niedriger als vor den Gegnertypen und Waffen (damals 69 % für „Alle angreifen“), weil der Einsatz seither härter geworden ist. Laute Spielweisen enden immer bei Fahndung 4. Mit einer Wache mehr je Stufe Fahndung und früherer Verstärkung war es eine Abwärtsspirale, deshalb ist die Folge schwächer.

Arme, gemessen mit `tools/Messingstadt.Balance -- --arme` (je 300 Läufe, alle vier Agenten auf derselben Stufe, vier Maschinenpistolen):

| Einsatz und Spielweise | Stufe 1 | Stufe 2 | Stufe 3 |
|---|---|---|---|
| Kesselviertel, Feuer bündeln | 91 % | 98 % | 99 % |
| Kesselviertel, alle angreifen | 83 % | 95 % | 98 % |
| Kesselviertel, Feuer bündeln mit Pistolen | 68 % | 77 % | 84 % |
| Nordwall, laut, Trupp zusammen | 14 % | 29 % | 44 % |
| Nordwall, Otto allein mit Dietrich | 99 % | 99 % | 99 % |

Stufe 1 entspricht den Werten vor dem Ausbau. Mit 6 Punkten und 20 % wäre der Nordwall von 14 auf 66 % gestiegen, das war zu viel.

### Umgebung

Die Stadt bewegt sich auch ohne Zutun: Rauch aus Schornsteinen und Kesseln, Dampf aus Gullys, Nieselregen
mit Ringen in den Pfützen, flackernde Laternen (eine mit Wackelkontakt), flackernde Kesselfeuer und
Papierfetzen in Windböen. Das ist reine Darstellung mit eigenem Zufall in `game/Ambience.cs` und
`game/RainLayer.cs`. Die Spiellogik bleibt deterministisch, und die Animation läuft auch in der Pause weiter.

### Eintauchen

Was den Spieler in die Welt ziehen soll, alles reine Darstellung:

| Was | Wie |
|---|---|
| Zwischensequenzen | Laufen in der Spielgrafik und stehen als Daten in `data/sequences`: Kamerafahrt, Schwarzblende, Titel, Ortskarte, Auftrag, Telegramm, Zeitung und Ton. Bei jedem neuen Spiel Kolbs Prolog und der Einstieg, beim Fortsetzen und vor jedem weiteren Anlauf ein kurzer Übergang mit Ort und Uhrzeit. Nach dem Sieg die Zeitung, nach Niederlage oder Abbruch ein Telegramm von K. und die Zeitung, nach dem Rückzug ohne Schuss nur ein Telegramm. Esc überspringt, Leertaste oder Klick schreibt fertig und geht weiter. Einsätze können eigene Bild-Sequenzen haben (`sequenceBefore`, `sequenceAfter` in der Missionsdatei): vor dem ersten Antreten und nach dem ersten Sieg, einmal je Akte. Abschnitte können an Bedingungen hängen (`when`), etwa wer lebend herauskam. Die letzte Seite nach einem Sieg zeigt das Totenbuch der Nacht, links die eigenen Toten, rechts die der Wache. Der Spielstand ist vorher verbucht, Sequenzen schreiben nichts (`src/Messingstadt.Core/Sequence.cs`, `game/SequencePlayer.cs`) |
| Oberfläche | Oben ein Telegramm, unten vergilbte Personalakten mit Stempel. Ein Klick auf eine Akte wählt den Agenten (`game/PaperUi.cs`) |
| Filmlook | Dunkle Bildränder, feines Filmkorn, leichte Farbstimmung (`game/FilmLook.cs`) |
| Gespräche | Wachen und Arbeiter reden kurze Sätze, Wachen rufen im Alarm. Manche Sätze verraten etwas über die Rundgänge (`game/Chatter.cs`) |
| Kamera | Folgt weich, atmet leicht, zittert bei Donner und nahen Schüssen, rückt heran, wenn ein Agent fällt |
| Ton | Hund, Sirene, Güterzug und Schichthorn in der Ferne, Radio hinter einem Fenster. Herzschlag, wenn eine Wache gerade bemerkt. Im Dunkeln wird die Musik dünner |
| Zeitung | Nach jedem Einsatz eine Schlagzeile, die zum Verlauf passt: unentdeckt, leise, blutig, verloren, aufgegeben, ohne Schuss zurückgezogen. Die Texte stehen je Mission unter `headlines`, `Headlines` in der Spiellogik füllt die Platzhalter |
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
| Musik | Vier Lagen im selben Takt: Ruhe, Dunkel beim Schleichen, Verdacht, Gefecht. Sie blenden je nach Lage ineinander |
| Überall | Regen und Wind folgen dem Wetter, Böen rauschen in den Mitten. Ferne Stadt: Hund, Sirene, Güterzug, Schichthorn, Straßenbahn, Nebelhorn, Kirchenglocke, Fensterladen, ferne Schüsse |
| Raum | Der Hall wächst, je enger die Hindernisse um die Kamera stehen. In der Gasse klingt der Regen dumpfer |
| Am Ort, nur aus der Nähe | Laternen, Dampf aus Gullys, Kesselfeuer und Maschinen, Dieselmotor des Fluchtwagens, Bildfunk, Leuchtschild, Radio und Klavier hinter Fenstern, Regenrinnen je nach Dachablauf, Lampenbrummen in der Straßenbahn. Radio, Bildfunk und Klavier verstummen, wenn nach Schüssen die Fenster ausgehen |
| Figuren | Dumpfe Schritte, Wachen nur aus der Nähe, Agenten etwas weiter, laut beim Rennen. Schüsse mit Echo, Treffer, Querschläger, Fallen. Ein Streichholz, wenn eine Wache sich eine anzündet |
| Einsatz | Trillerpfeife, wenn eine Wache in den Kampf geht, Alarmglocke beim Anrücken der Verstärkung, Dietrich und Tür, Blättern im Kontobuch, Münzen, Abschlussakkord |
| Tiere | Tauben flattern auf, Ratten quieken |
| Wetter und Stadt | Donner, Motoren des Luftschiffs, Dampfstöße, Schweißen, Feuertonne |

Dazu kommt leise Musik: langsame dunkle Flächen in d-Moll mit einem sparsamen Thema, eine eigene Komposition. Solange Wachen alarmiert sind, blendet sich eine treibende Bassfigur ein.

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

Rennen ist also riskant und kostet mehr als die Hälfte des Trupps, wer aktiv kämpft, fährt besser. Über den Einsatz
hinaus wirkt die Wertung über Lohn und Fahndung, siehe Kampagne.

Gewonnen ist der Einsatz, wenn ein Agent das Kontobuch trägt und den Fluchtwagen erreicht. Verloren
ist er, wenn alle Agenten ausgeschaltet sind.

## Bewusst noch nicht drin

Basisbau, Fahrzeuge, Stockwerke. Die Waffenkammer als Bildschirm baut die Session Menüs.
