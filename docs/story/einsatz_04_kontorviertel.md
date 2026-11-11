# Einsatz 4: Das Schließfach (Kontorviertel, Überzeugen)

Stand 2026-11-11. Bauvorlage in derselben Form wie `einsatz_02_nordwall.md`. Rahmen ist KANON: Kontorviertel, Überzeugen, Prokurist Albert Dorn schließt Valks Schließfach auf, darin das Zweitbuch mit den Klarnamen, Sperling ist Rehm. Karte, Gegner, Regeln und Texte sind VORSCHLAG der Story. Karte 20 × 20, Norden ist y = 0.

## 1. Geschichte

### Was vorher geschah

- Am Güterbahnhof hat der Werkschutz den Trupp erwartet (Einsatz 3). Jemand hat geredet.
- Im Kontobuch stehen Zahlungen an einen „Sperling“, ohne Namen. Die Kontorbank führt für Brandt ein zweites Buch mit den richtigen Namen. Es liegt in Direktor Valks Schließfach (KANON).
- Emil kennt die Bank von innen und kennt Albert Dorn: Prokurist, zwanzig Jahre im Haus, hat den zweiten Schlüssel und drei Monate Mietschulden bei der eigenen Bank (VORSCHLAG). Ist Emil tot, weiß Kolb das aus einer Akte.

### Was auf dem Spiel steht

Ohne das zweite Buch bleibt Sperling ein Deckname, und der Trupp läuft beim nächsten Einsatz wieder in eine vorbereitete Wache. Aufbrechen lässt sich das Fach nicht, ohne dass das Viertel aufwacht. Es geht nur mit Dorn.

### Was der Trupp erfährt

- Sperling ist Julius Rehm, der Vorsitzende des Roten Hammers (KANON).
- Dorn sagt, Valk habe den Notstand schon verbucht, als Geschäft. Damit weiß der Trupp, wozu Brandt den Streik braucht (VORSCHLAG, die Frage steht in `steuerung.md` unter den offenen Entscheidungen).

### Wie es weitergeht

Rehm erfährt binnen eines Tages, dass das Buch fehlt. Er verrät das Versteck in der Altstadt, Einsatz 5.

### Ersatzwege

| Fall | Folge |
|---|---|
| Emil tot oder nicht dabei | Überzeugen geht auch ohne Buchhalter, dauert aber länger und ist zu hören. Kolbs Auftrag nennt Emil nicht |
| Dorn stirbt | Das Fach bleibt zu, der Einsatz ist verloren. Wachen schießen nicht auf ihn |
| Niederlage | Die Bank merkt nicht, worum es ging. Das Buch liegt noch im Fach, der Einsatz steht wieder offen |

## 2. Der Ort

Der Platz vor dem Hauptsitz der Kontorbank. Das hellste Bild der Kampagne.

| Was | Felder | Beschreibung |
|---|---|---|
| Kanal | x 0–3, y 0–9 und y 12–20 | Wasser, nicht begehbar, sperrt keine Sicht |
| Kanalbrücke | x 0–3, y 9–12 | Steinbrücke mit Geländer. Hier wartet der Wagen |
| Uferweg | x 3–6, y 2–9 | Westlich der Bank, schwach beleuchtet |
| Kontorbank | x 6–14, y 2–8 | Sandstein, Säulenportal nach Süden bei (10, 8), Kuppel. Kein Fenster im Erdgeschoss ohne Gitter |
| Kassengasse | x 14–16, y 2–9 | Durchfahrt zwischen Bank und Notariat, dunkel. Hier liegt der Hintereingang |
| Notariat | x 16–19, y 2–8 | Geschäftshaus, im Souterrain der Dienstboteneingang zur Gasse |
| Bankstraße | y 9–12, x 3–20 | Breit, glattes Pflaster, vier elektrische Kandelaber |
| Geschäftshaus | x 4–8, y 13–18 | Juwelier mit Rollgitter, Schaufenster erleuchtet |
| Hotel Kontor | x 11–17, y 13–18 | Vordach, Drehtür, Page davor |
| Durchfahrt | x 8–11, y 13–20 | Zwischen Geschäftshaus und Hotel, dunkel. Start |

Kandelaber: (5,5, 10,5), (9,5, 10,5), (13,5, 10,5), (17,5, 10,5). Portal: zwei Lampen bei (8,5, 8,6) und (11,5, 8,6). Dunkel sind nur die Kassengasse, die Durchfahrt im Süden und der Uferweg nördlich von y 5.

### Türen

| Tür | Felder | Marke | Wozu |
|---|---|---|---|
| Hintereingang der Bank | [13, 5], [12, 5], [11, 5] | (14,5, 5,5) | Schließfach mit dem Zweitbuch bei (11,5, 5,5). Geht nur mit Dorn auf, nicht mit Schlosser oder Dietrich |
| Seitentür des Notariats | [16, 4], [17, 4] | (15,5, 4,5) | Handkasse des Notariats bei (17,5, 4,5), 300. Gewöhnliches Schloss |

### Start und Ziel

- Start: Durchfahrt im Süden, x 8,5 bis 10,5 und y 18,5 bis 19,5.
- Ziel: Wagen auf der Kanalbrücke, Zone x 0–3, y 9–12.
- Dorn steht bei (15,5, 7,5) in der Kassengasse vor dem Dienstboteneingang und raucht.
- Verstärkung: Werkschutz vom Ostende der Bankstraße, (19,5, 10,5). Erste Welle 6 s nach dem ersten Schuss oder einer Meldung, alle 4 s, 4 Mann.

## 3. Gegner

Alle aus dem Bestand. Die Bank hat sich Werkschutz von Brandt geliehen.

| Name | Art | Wo | Verhalten | Käuflich |
|---|---|---|---|---|
| Portalwache links | Werkschutz | (8,5, 8,5), Blick nach Süden | Posten | nein |
| Portalwache rechts | Werkschutz | (11,5, 8,5), Blick nach Süden | Posten | nein |
| Straßenstreife 1 | Wachmann | Bankstraße | Rundgang (4, 10) – (18, 10) | 200 |
| Straßenstreife 2 | Wachmann | Bankstraße | Rundgang (18, 11) – (4, 11), gegenläufig | 200 |
| Nachtwächter | Wachmann | Kassengasse | Rundgang (14, 9) – (14, 2) – (15, 2) – (15, 9). Kommt alle halbe Minute an Dorn vorbei | 200 |
| Revierführer | Offizier | Vor dem Notariat | Rundgang (16, 9) – (19, 9) | 400 |
| Uferstreife | Hundeführer mit Hund | Uferweg | Rundgang (4, 2) – (4, 8) – (5, 8) – (5, 2) | 300 |
| Page | Laufbursche | (14, 12,5), vor dem Hotel | Steht, schaut über die Straße | 50 |

Acht Gegner und ein Hund. Die Gefahr ist das Licht: Auf der Bankstraße bemerken Wachen den Trupp schneller und weiter (Regel aus `../konzept.md`).

### Albert Dorn

Kein Gegner. Unbewaffnet, 40 Lebenspunkte, alarmiert niemanden. Er bleibt stehen, bis ihn jemand überzeugt. Fallen Schüsse, kauert er sich hin und rührt sich nicht vom Fleck.

## 4. Ablauf

### Was der Spieler tun muss

1. Über die Bankstraße in die Kassengasse.
2. Dorn überzeugen.
3. Dorn zum Hintereingang begleiten, zwei Felder weiter. Er schließt auf.
4. Das Zweitbuch aus dem Schließfach nehmen.
5. Mit dem Buch zur Kanalbrücke.

### Leise

- Die Bankstraße ist zwölf Felder Licht. Die Streifen laufen gegenläufig, der Page schaut zu. Den Pagen kaufen oder die Straße an ihrem Ostende queren, wo das Vordach des Hotels die Sicht des Pagen nimmt (beim Bau prüfen).
- In der Kassengasse läuft der Nachtwächter. Kaufen, niederschlagen oder das Gespräch mit Dorn in seine Runde legen.
- Überzeugen mit Geld ist still. Überzeugen mit Worten ist zu hören.
- Der Rückweg mit dem Buch führt über die ganze Bankstraße nach Westen, oder durch die Kassengasse nach Norden und über den Uferweg, am Hund vorbei.

### Laut

- Fällt ein Schuss, bevor Dorn aufgeschlossen hat, kauert er. Überzeugen dauert dann doppelt so lang.
- Die Portalwachen kommen um die Ecke in die Kassengasse. Rauchtopf an die Mündung der Gasse.
- Mit dem Buch ist der Träger langsam. Der kürzeste Weg zur Brücke liegt im Feuer der Portalwachen.

### Was jeder beiträgt

| Wer | Beitrag |
|---|---|
| Emil, Buchhalter | Überzeugt Dorn mit Geld, schnell und still. Kennt ihn von früher, das steht in den Zurufen |
| Otto, Schlosser | Seitentür des Notariats für die Handkasse. Gegen das Schließfach hilft er nicht |
| Hanna, Kurierin | Zieht eine Streife oder den Revierführer weg. Trägt das Buch nicht schneller, ist aber als Erste an der Brücke, um zu sichern |
| Greta, Truppführerin | In der Mechanik keine Sonderrolle. Ist sie dabei und gilt der Vorschlag aus Einsatz 2, nimmt der Nachtwächter die Hälfte |
| Totschläger | Nachtwächter, Streifen, Revierführer. Nicht die Portalwachen |
| Rauchtopf | Mündung der Kassengasse oder die Mitte der Bankstraße |

### Überzeugen als Regel

**Volle Fassung, neu für die Mechanik:**

- Die Mission nennt eine Zielperson mit Preis und Dauer.
- Mit Geld: Rechtsklick des Buchhalters wie beim Bestechen, 400 aus der Kasse, 2 s. Still.
- Mit Worten: Jeder Agent, 10 s daneben stehen, kostet nichts. Dorn redet dabei, zu hören auf 3 Felder. Der Buchhalter braucht dafür nur 5 s.
- Überzeugt folgt Dorn dem Agenten bis zu seiner Tür und öffnet sie in 3 s. Danach geht er in den Dienstboteneingang und ist weg.
- Die Tür des Schließfachs lässt sich auf keine andere Weise öffnen.
- Fällt die Kasse unter 400 und kein Agent kann stehen bleiben, bleibt der Weg mit Worten. Der Einsatz ist also immer lösbar.

**Kleine Fassung:**

- Dorn ist eine bestechbare Figur ohne Waffe (400). Ist er bestochen, steht seine Tür offen. Das ist dieselbe Regel wie beim Schließer in Einsatz 2: Ein bestochener Schlüsselträger öffnet seine Türen.
- Ohne Buchhalter ist diese Fassung nicht lösbar.

Ganz ohne neue Regel wäre der Einsatz wieder ein Einbruch. Dann stimmt die Auftragsart auf dem Stadtplan nicht mehr.

## 5. Texte

### Mission

| Feld | Text |
|---|---|
| `title` | Das Schließfach |
| `place` | Kontorviertel, hinter der Kontorbank |
| `time` | Mittwoch, 30. November 1927, 1:30 Uhr |
| `itemName` | Zweitbuch |
| `briefing` | In Valks Schließfach liegt ein zweites Buch, das mit den richtigen Namen. Aufbrechen hört die halbe Stadt. Prokurist Dorn hat den Schlüssel und Schulden bei der eigenen Bank, er raucht nachts in der Kassengasse. Bringt ihn dazu, aufzuschließen, mit Geld oder mit Worten. Ihm passiert nichts. Wagen auf der Kanalbrücke. K. |
| `loot.name` | Handkasse des Notariats |

### Einsatzziel im Telegrammstreifen

| Zustand | Text |
|---|---|
| Beginn | `DORN IN DER KASSENGASSE STOP ÜBERZEUGEN STOP ER SCHLIESST AUF STOP` |
| Beim Überzeugen | `DORN HÖRT ZU STOP STEHEN BLEIBEN STOP` |
| Überzeugt | `DORN KOMMT MIT STOP ZUM HINTEREINGANG STOP` |
| Tür offen | `FACH IST OFFEN STOP ZWEITBUCH HOLEN STOP` |
| Beim Aufnehmen | `ZWEITBUCH WIRD GESICHERT STOP STEHEN BLEIBEN STOP` |
| Buch gesichert | `ZWEITBUCH GESICHERT STOP ZUR KANALBRÜCKE STOP` |
| Dorn kauert | `DORN HAT ANGST STOP WEITERREDEN STOP` |
| Am Wagen | `EINSTEIGEN STOP STEHEN BLEIBEN STOP` |

### Dorn

| Wann | Text |
|---|---|
| Sieht den Trupp | „Ich habe kein Geld bei mir. Ich habe nie Geld bei mir.“ |
| Sieht Emil (nur wenn Emil dabei ist) | „Sander? Man hat Sie doch entlassen.“ |
| Beim Überzeugen mit Worten | „Zwanzig Jahre. Und die kündigen mir die Wohnung.“ / „Wissen Sie, was in dem Fach liegt? Ich schon.“ / „Leiser. Der Wächter kommt gleich wieder.“ |
| Überzeugt mit Geld | „Das reicht für die Miete. Und für eine Fahrkarte.“ |
| Überzeugt mit Worten | „Gut. Aber ich war heute Nacht nicht hier.“ |
| Schließt auf | „Fach neun. Valk hat den Notstand schon verbucht. Unter Erträge.“ |
| Schüsse | „Nicht schießen! Ich bin nur der Prokurist!“ |
| Geht ab | „Sagen Sie Ihrer Chefin, Dorn ist quitt.“ |

### Gespräche

Ohne Spielhinweis:

- „Hier fällt nicht mal Papier auf die Straße.“
- „Im Kesselviertel hungern sie. Hier putzen sie nachts die Türgriffe.“
- „Der Werkschutz vor einer Bank. Sag mir, wem die Bank gehört.“
- „Valk grüßt nicht. Der zählt.“
- „Der Dorn raucht jede Nacht da hinten. Zu Hause wartet wohl keiner.“
- „Für eine Nacht hier gibt es doppelten Sold. Fürs Maulhalten, sagen sie.“

Mit Spielhinweis, gegen die Karte oben geprüft:

- „Die zwei am Portal sind vom Werk. Die rühren sich nicht weg.“ (Posten, nicht käuflich)
- „Unter den Kandelabern siehst du eine Maus über die Straße laufen.“ (Licht)
- „In der Kassengasse ist es finster wie im Tresor.“ (dunkel)
- „Der Wächter dreht seine Runde in der Gasse. Alle halbe Minute.“ (Rundgang, Zeit beim Bau nachmessen)
- „Der Page vom Hotel sieht alles und sagt alles.“ (Laufbursche)
- „Am Ufer läuft einer mit Hund.“ (Hundeführer)

Bestochen:

- Nachtwächter: „Ich seh nach den Fenstern vom Notariat. Gründlich.“
- Page: „Ich hab die Drehtür geputzt. Die ganze Zeit.“
- Revierführer: „Ich gehe den Bericht schreiben. Der wird lang.“

### Schlagzeilen

| Fall | Kopf | Zeile |
|---|---|---|
| `unseen` | Prokurist der Kontorbank verschwunden | Albert Dorn, seit zwanzig Jahren im Haus, erschien am Morgen nicht zum Dienst. Die Bank teilt mit, es fehle nichts.{loot}{paid} |
| `quiet` | Nächtlicher Besuch an der Kontorbank | Ein Wächter will Gestalten in der Kassengasse gesehen haben. Die Bank teilt mit, es fehle nichts. Prokurist Dorn ist seit der Nacht verschwunden.{loot}{paid} |
| `loud` | Schüsse im Kontorviertel | Bewaffnete lieferten sich vor der Kontorbank ein Gefecht mit dem Werkschutz.{dead} Direktor Valk nennt es einen versuchten Raub. Was fehlt, sagt die Bank nicht.{fallen}{loot}{paid} |
| `lost` | Überfall auf die Kontorbank vereitelt | Der Werkschutz schlug in der Nacht Bewaffnete vor der Kontorbank zurück. Die Schließfächer blieben unberührt.{dead} |
| `abandoned` | Schießerei vor der Kontorbank | Bewaffnete zogen sich nach einem Gefecht mit dem Werkschutz zurück. Die Schließfächer blieben unberührt.{dead} |
| `retreat` | Unruhe im Kontorviertel | Ein Hotelgast meldete Gestalten in einer Durchfahrt. Die Wache fand nichts. |
| `loot` | | Auch die Handkasse des Notariats fehlt. |

Die Bank leugnet in jedem Fall, dass etwas fehlt. Das Buch darf es nicht geben.

### Telegramme

| Fall | Text |
|---|---|
| `lost` | `AN ABTEILUNG K STOP BANK HAT GEHALTEN STOP BUCH LIEGT NOCH STOP DORN RAUCHT AUCH MORGEN STOP K` |
| `abandoned` | `AN ABTEILUNG K STOP ABGEBROCHEN STOP BUCH LIEGT NOCH STOP ABWARTEN STOP K` |
| `retreat` | `AN ABTEILUNG K STOP ZU HELL STOP RICHTIG SO STOP NEUER VERSUCH MORGEN NACHT STOP K` |

### Sequenz vor dem Einsatz

Kolbs Kästen:

1. „Im Kontobuch stand ein Sperling. Ein Vogel, der Geld bekam und keinen Namen hatte.“
2. „Die Bank schrieb alles zweimal auf. Einmal für die Prüfer, einmal für sich.“
3. „Das zweite Buch lag in Valks Fach. Den Schlüssel dazu trug ein Mann, der seine Miete nicht zahlen konnte.“
4. „Ich schickte den Trupp nicht zum Tresor. Ich schickte ihn zu Dorn.“

### Sequenz nach dem Einsatz

| Fall | Kolbs Kästen |
|---|---|
| Sieg | „Dorn nahm den Frühzug nach Süden. Ich habe ihm nie geschrieben.“ / „Das Buch lag um vier auf meinem Tisch. Sperling stand auf Seite neun.“ / „Julius Rehm. Vorsitzender.“ / „Ich las den Namen zweimal. Nicht, weil ich ihn nicht glauben konnte.“ |

Der letzte Kasten verschweigt, dass Kolb es wusste, und leugnet es nicht (KANON, `kanon.md`).

## 6. Was andere Bereiche dafür brauchen

| Bereich | Was |
|---|---|
| Level | Karte, Gegner, Türen, Start und Ziel nach Abschnitt 2 bis 4. Lohn 450 steht im Stadtplan |
| Mechanik | Zielperson überzeugen (volle Fassung) oder bestochener Schlüsselträger (kleine). Dorn als Figur ohne Waffe |
| Grafik | Kontorbank mit Portal und Kuppel, Notariat, Hotel Kontor, Geschäftshaus mit Juwelier, Kanal mit Brücke, Kandelaber. Figur Dorn: Mantel über dem Anzug, Kneifer, Zigarette. Thema in `grafikthemen.md` |
| Ton | Stille, ein Motor im Leerlauf, Klavier, Rollgitter, Wasser im Kanal, Schlüssel im Tresorschloss |
| Sequenzen | Szenenplan aus den Kästen. Rehms Name fällt hier zum ersten Mal |

## 7. Prüfung

| Punkt | Ergebnis |
|---|---|
| Wissen | Rehms Name steht erst in der Sequenz nach dem Sieg. Vorher sagt niemand ihn. Dass Kolb es wusste, bleibt verschwiegen |
| Figuren | Der Auftrag nennt Emil nicht. Dorns Satz zu Emil hat eine Bedingung |
| Zeit | 30. November 1927 ist ein Mittwoch. Drei Tage nach Einsatz 3 |
| Spiel | Alle Gegner aus dem Bestand. Neue Regel nur für das Überzeugen |
| Welt | Die Bank leiht sich Brandts Werkschutz. Valk und Brandt treten nicht auf |
| Offen gelassen | Dorns Satz zum Notstand hängt an der offenen Entscheidung, wann der Trupp davon erfährt. Wird anders entschieden, entfällt der zweite Satz beim Aufschließen |
