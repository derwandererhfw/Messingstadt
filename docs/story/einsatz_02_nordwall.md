# Einsatz 2: Die Wachstube (Nordwall, Befreien)

Stand 2026-11-11. Bauvorlage der Story-Session für Level, Mechanik, Grafik, Ton und Sequenzen. Auftrag des Nutzers über die Hauptsession. Rahmen (Viertel, Auftragsart, wer befreit wird) ist KANON. Karte, Gegner, Regeln und Texte in dieser Datei sind VORSCHLAG der Story, bis der Nutzer sie am gebauten Einsatz abnimmt. Alle Koordinaten sind Felder auf einer Karte von 20 × 20 wie in `data/map_01.json`, Norden ist y = 0. Der gebaute Grundriss steht in `../einsatz_02_grundriss.md` und geht bei Abweichungen vor. Diese Vorlage ist am 2026-11-11 daran angeglichen: Die Wachstube steht am Wall, der Arresthof liegt südlich davor, weil die Schrägansicht sonst Hof und Zellen verdeckt hätte.

## 1. Geschichte

### Was vorher geschah

- In der Nacht auf den 22. November holt der Trupp das Kontobuch (KANON).
- Am 23. November druckt Anton Weiß in der Altstadt die ersten Seiten daraus als Flugblatt. Am Nachmittag holt ihn die Stadtwache von der Presse weg (VORSCHLAG).
- Paul Roth, Hannas Bruder, sitzt schon länger. Die Wache hat ihn als Streikposten abgeholt (KANON, im Spiel sagt ein Arbeiter: „Den jungen Roth haben sie auch geholt.“).
- Beide sitzen im Arrest der Wachstube Nord. Um sechs Uhr früh soll der Werkschutz sie übernehmen. Dafür gibt es kein Gesetz, nur eine Unterschrift von Hauptmann Pieper (VORSCHLAG, folgt aus „käuflich“ in `../story.md`).

### Wer befreit wird und warum

| Wer | Warum er wichtig ist | Im Einsatz |
|---|---|---|
| Anton Weiß, Drucker, Mitte fünfzig | Ohne ihn druckt der Hammer nichts mehr. Im Finale steht er im Druckhaus an der Maschine | Langsam, trocken, klagt nicht |
| Paul Roth, siebzehn, Streikposten | Er ist einer von uns, und er ist Hannas Bruder. Er hat beim Verhör etwas gesehen | Geschwächt, hustet, ohne Schuhe |

### Was auf dem Spiel steht

Übernimmt der Werkschutz die beiden, verschwinden sie im Werk. Dann hat der Hammer keinen Drucker, und jeder Streikposten weiß, dass die Wache Leute an Brandt ausliefert.

### Was der Trupp erfährt

Paul sagt am Wagen: Beim Verhör saß einer vom Werkschutz mit am Tisch und stellte die Fragen. Die Wache arbeitet also schon für Brandt. Das Wort „Notstand“ fällt in diesem Einsatz nicht. Wer Sperling ist, weiß weiter niemand (`wissen.md`).

### Wie es weitergeht

Weiß druckt in der nächsten Nacht die Seiten aus dem Kontobuch. Die Stadt redet. Brandt antwortet mit dem Zug voller Streikbrecher, Einsatz 3. Den Hinweis auf den Zug gibt Rehm (KANON).

### Ersatzwege

| Fall | Folge für spätere Texte |
|---|---|
| Hanna tot oder nicht dabei | Paul fragt nach ihr und bekommt keine Antwort. Kein Text setzt Hanna voraus |
| Greta tot oder nicht dabei | Die Sätze, in denen Wachleute sie erkennen, entfallen |
| Weiß stirbt im Einsatz | Sein Lehrling setzt weiter, langsamer. Im Finale steht der Lehrling an der Maschine |
| Paul stirbt im Einsatz | Seine Aussage fehlt. Dass der Werkschutz mitverhört, erfährt der Trupp dann erst von Weiß |
| Beide tot | Der Einsatz ist verloren |

## 2. Der Ort

Der Nordwall ist das Viertel am alten Stadtwall. Zu sehen ist der Platz hinter dem Amtsgericht.

| Was | Felder | Beschreibung |
|---|---|---|
| Stadtwall | x 0–20, y 0–2 | Backsteinmauer über die ganze Breite, oben Wehrgang, unten zugemauerte Bögen. Nicht begehbar, sperrt die Sicht |
| Amtsgericht | x 1–5, y 3–9 | Großer dunkler Bau mit Freitreppe nach Süden. Kein Fenster brennt |
| Gerichtsgasse | x 5–7, y 2–9 | Schmal, ohne Laterne. Führt von der Wallstraße zum Wall |
| Wachstube Nord, der Arrest | x 7–14, y 2–6 | Hauptbau am Wall. Zwei Zellentüren nach Süden in den Hof |
| Arresthof | x 8–13, y 6–9 | Vor der Wachstube. Eine Lampe über den Zellentüren, sonst dunkel. Eine Kohlenkiste bei (8,5, 8,5) als Deckung |
| Wachlokal | x 7–14, y 9–10 | Niedriger Vorbau zur Wallstraße mit Haupttür, Schild und Laterne. Schließt den Hof nach Süden |
| Hofmauer West | x 7–8, y 6–9 | Niedrig. Die Pforte zur Gerichtsgasse ist eine Lücke in der Mauer mit einem Tor aus Eisenblech (so gebaut, Stand 2026-11-11) |
| Hofmauer Ost | x 13–14, y 6–9 | Niedrig. Darin das Hoftor |
| Kaserne der Wache | x 15–19, y 3–8 | Mannschaftshaus, einige Fenster hell |
| Remise | x 15–19, y 13–17 | Offener Schuppen mit dem Gefangenenwagen, grau, vergittert. Requisit, fährt nicht |
| Wallstraße | y 10–12, ganze Breite | Kopfsteinpflaster, drei Laternen, Straßenbahnschienen ohne Bahn in dieser Nacht |
| Beamtenhaus West | x 2–6, y 14–18 | Zwei Geschosse, davor ein Feld Vorgarten |
| Beamtenhaus Ost | x 9–13, y 14–18 | Ebenso. Dazwischen ein Durchgang, x 6–9 |
| Wallgasse | x 0–2, y 13–20 | Sackgasse am Westrand. Hier wartet der Wagen der Wäscherei |

Laternen: (2,5, 10,5), (10,5, 10,5), (16,5, 10,5) auf der Wallstraße, (11,5, 10,1) über der Haupttür des Wachlokals, (10,5, 6,2) über den Zellentüren im Arresthof. Die Gerichtsgasse, der Fuß des Walls und der Durchgang zwischen den Beamtenhäusern sind dunkel. Die westliche Laterne stand in der ersten Vorlage bei x 4,5. Die Level-Session hat sie nach der Messung versetzt, damit die Querung zur Gerichtsgasse im Dunkeln liegt.

### Türen

Alle fünf sind verschlossen. Regeln wie in `../konzept.md`: Schlosser 2 s, mit Dietrich 1 s, alle anderen nur mit Dietrich in 4 s. Dazu kommen Schlüssel, so gebaut am 2026-11-11: Der Türposten trägt den Schlüssel zur Pforte, der Schließer die Schlüssel zu beiden Zellen. Wer einen der beiden besticht, bekommt seine Schlüssel und schließt selbst auf, 0,5 s je Tür. So ist der Einsatz auch ohne Schlosser und Dietrich lösbar.

| Tür | Felder | Marke | Wozu |
|---|---|---|---|
| Pforte zur Gerichtsgasse | [7, 7] | (6,5, 7,5) | Leiser Zugang zum Arresthof |
| Hoftor | [13, 7] | (14,5, 7,5) | Östlicher Zugang, bewacht |
| Zelle 1 | [9, 5], [9, 4] | (9,5, 6,5) | Anton Weiß steht bei (9,5, 4,5) |
| Zelle 2 | [11, 5], [11, 4] | (11,5, 6,5) | Paul Roth steht bei (11,5, 4,5) |
| Schreibstube | [13, 9] | (13,5, 10,5) | Im Ostende des Wachlokals. Bußgeldkasse bei (13,5, 9,5), 300 |

### Start und Ziel

- Start: Durchgang zwischen den Beamtenhäusern, vier Agenten bei x 6,5 bis 8,5 und y 18,5 bis 19,5, im Dunkeln.
- Ziel: Wagen in der Wallgasse, Zone x 0–2, y 17–20.
- Verstärkung: Werkschutz vom Ostende der Wallstraße, (19,5, 11,5). Erste Welle 8 s nach dem ersten Schuss oder einer Meldung, dann alle 5 s, 3 Mann. Das ist der Transport, der früher kommt als geplant.

## 3. Gegner

Alle aus dem Bestand. Die Stadtwache ist die Polizei von Messingstadt, eine neue Art braucht es nicht.

| Name | Art | Wo | Verhalten | Käuflich |
|---|---|---|---|---|
| Wallstreife 1 | Wachmann | Wallstraße | Rundgang (0, 11) – (13, 11) | 200 |
| Wallstreife 2 | Wachmann | Wallstraße | Rundgang (13, 12) – (0, 12), läuft gegenläufig | 200 |
| Türposten | Wachmann | (11,5, 10,5), Blick nach Süden, vor der Haupttür | Posten, dreht sich. Trägt den Schlüssel zur Pforte | 200 |
| Wachhabender | Offizier | (14,5, 9,5), Blick nach Süden, an der Ecke neben der Schreibstube | Posten. Sieht die Straße und den Gang zum Hoftor | 400 |
| Schließer | Wachmann | Arresthof | Rundgang (8, 7) – (12, 7). Trägt die Schlüssel zu beiden Zellen | 200 |
| Hundeführer mit Hund | Hundeführer, Wachhund | Gerichtsgasse | Rundgang (5, 9) – (5, 2) – (6, 2) – (6, 9) | 300 |
| Wallposten | Schütze | Am Fuß des Walls, Ostseite | Rundgang (14, 2) – (19, 2). Sieht durch den Postengang zwischen Wachstube und Kaserne zum Hoftor. Für die Kamera meist verdeckt | 200 |
| Bote | Laufbursche | Zwischen Remise und Gericht | Rundgang (16, 12) – (6, 12) | 50 |
| Mann vom Werk | Werkschutz | (14,5, 7,5), vor dem Hoftor, Blick nach Süden | Posten. Wartet auf die Übergabe | nein |

Neun Gegner und ein Hund, etwa wie in Einsatz 1. Der Mann vom Werk, der Wachhabende an der Ecke und der Schütze machen das Hoftor teuer. Die Gerichtsgasse ist der leise Weg, dort stört nur der Hund.

Die Wallstreifen liefen in der ersten Vorlage von x 3 bis 17. Die Level-Session hat sie nach der Messung auf x 0 bis 13 gelegt: Vorher blieb an der Querung kein Fenster von einer Sekunde.

## 4. Ablauf

### Was der Spieler tun muss

1. Über die Wallstraße in die Gerichtsgasse.
2. Durch die Pforte in den Arresthof.
3. Beide Zellen öffnen.
4. Beide Gefangenen aus dem Hof bringen.
5. Zurück über die Wallstraße zum Wagen in der Wallgasse.

Der Rückweg ist der schwere Teil: Die Gefangenen sind langsam, und die Wallstraße liegt im Licht.

### Leise

- Die Wallstreifen laufen gegenläufig. Der Lageplan zeigt das Fenster, in dem die Straße frei ist. In der Geschichte ist das Gretas alter Dienstplan.
- Der Hund wittert in der Gasse jeden. Entweder kauft der Buchhalter den Hundeführer, oder der Trupp wartet am Südende, bis beide am Wall wenden.
- Otto öffnet Pforte und Zellen. Ohne ihn holt sich der Trupp die Schlüssel: erst vom Türposten (Pforte), dann vom Schließer (Zellen), gekauft oder mit dem Totschläger. Der Schließer geht im Hof auf und ab: bestechen, mit dem Totschläger niederschlagen oder seinen Rundgang abpassen.
- Der Bote läuft die Wallstraße ab. Sieht er den Trupp mit den Gefangenen, rennt er zum Türposten. Hanna ist die Einzige, die ihn sicher einholt. Für 50 schweigt er.

### Laut

- Durch das Hoftor: von der Wallstraße in den Gang zwischen Wachlokal und Kaserne, am Wachhabenden vorbei. Rauchtopf in den Postengang gegen den Wallposten, dann der Mann vom Werk aus der Nähe, am besten mit der Schrotflinte. Der Totschläger hilft gegen ihn nicht.
- Nach dem ersten Schuss kommt der Werkschutz vom Ostende der Wallstraße. Der Rückweg zur Wallgasse führt dann durch die Gerichtsgasse und quer über die Straße, mit den Gefangenen im Schlepp.
- Ein Rauchtopf auf der Wallstraße deckt die Querung.

### Was jeder beiträgt

| Wer | Beitrag |
|---|---|
| Otto, Schlosser | Drei Schlösser auf dem leisen Weg. Ohne ihn braucht jede Tür einen Dietrich und 4 s |
| Emil, Buchhalter | Hundeführer, Schließer und Bote sind käuflich, für 550 ist der leise Weg fast frei. Türposten und Schließer geben dabei ihre Schlüssel her, zusammen 400 |
| Hanna, Kurierin | Fängt den Boten ab, lockt eine Wallstreife weg, ist als Erste am Wagen. Paul ist ihr Bruder |
| Greta, Truppführerin | Kennt die Wache. Vorschlag für die Mechanik: Schließer und Hundeführer nehmen von ihrem Trupp nur die Hälfte, wenn Greta dabei ist |
| Dietrich | Macht den Einsatz ohne Schlosser möglich |
| Totschläger | Schließer, Türposten und Wallstreifen gehen lautlos zu Boden. Nicht der Mann vom Werk |
| Rauchtopf | Deckt die Wallstraße auf dem Rückweg oder das Hoftor vor dem Schützen |

### Befreien als Regel

Empfohlen ist die volle Fassung. Die kleine Fassung kommt ohne neue Regel aus und passt zu allen Texten.

**Volle Fassung, neu für die Mechanik:**

- Die Mission nennt Gefangene: Name, Figur, Ort, Zelle.
- Ist die Zelle offen und steht ein Agent 2 s neben dem Gefangenen, folgt dieser ihm.
- Ein Gefangener geht 2,0 Felder pro Sekunde, rennt nicht, kämpft nicht und hat 60 Lebenspunkte. Wachen bemerken ihn wie einen Agenten.
- Gewonnen ist, wenn alle lebenden Gefangenen und ein Agent 1 s in der Zielzone stehen. Stirbt einer, geht der Einsatz weiter. Sterben beide, ist er verloren.
- Die Kampagne merkt sich je Gefangenem, ob er lebt. Spätere Texte fragen das ab.
- Wer Türposten oder Schließer besticht, bekommt deren Schlüssel und schließt selbst auf. So bleibt der Einsatz auch ohne Schlosser und ohne Dietrich lösbar. Auch ein Niedergeschlagener verliert seine Schlüssel (so gebaut, Stand 2026-11-11).

**Kleine Fassung, ohne neue Regel:**

- Beide Gefangenen stehen in einer Zelle, Felder [10, 5], [10, 4], und gelten zusammen als das Ziel der Mission: `itemName` „Gefangene“, `item` (10,5, 4,5).
- „Aufnehmen“ dauert 3 s, der Agent ist danach langsam wie mit dem Kontobuch. Zwei Figuren, die folgen, wären dann nur Darstellung.
- Ohne Schlosser und Dietrich ist diese Fassung nicht lösbar. Dann muss der Stadtplan den Dietrich vor dem Einsatz verlangen oder schenken.

## 5. Texte

### Mission

| Feld | Text |
|---|---|
| `title` | Die Wachstube |
| `place` | Nordwall, hinter dem Amtsgericht |
| `time` | Donnerstag, 24. November 1927, 4:10 Uhr |
| `briefing` | In der Wachstube Nord sitzen zwei von uns: Paul Roth und der Drucker Anton Weiß. Um sechs holt sie der Werkschutz ab, und was der mit Gefangenen macht, steht in keiner Akte. Holt beide vorher raus und bringt sie zum Wagen in der Wallgasse. Die Wache dort ist arm und müde. Redet, bevor ihr schießt. K. |
| `loot.name` | Bußgeldkasse |

Der kurze Auftrag auf dem Stadtplan steht schon in `data/stadtplan.json`.

### Einsatzziel im Telegrammstreifen

| Zustand | Text |
|---|---|
| Beginn | `GEFANGENE IM ARRESTHOF STOP PFORTE AN DER GERICHTSGASSE STOP DANN ZUM WAGEN IN DER WALLGASSE STOP` |
| Zelle wird geöffnet | `ZELLE WIRD GEÖFFNET STOP STEHEN BLEIBEN STOP` |
| Einer frei | `{NAME} FREI STOP DER ZWEITE FEHLT NOCH STOP` |
| Beide frei | `BEIDE FREI STOP ZUM WAGEN IN DER WALLGASSE STOP SIE SIND LANGSAM STOP` |
| Einer tot | `{NAME} TOT STOP DEN ANDEREN RAUSBRINGEN STOP` |
| Am Wagen | `EINSTEIGEN STOP STEHEN BLEIBEN STOP` |
| Kleine Fassung, beim Aufnehmen | `GEFANGENE WERDEN GEHOLT STOP STEHEN BLEIBEN STOP` |
| Kleine Fassung, danach | `GEFANGENE FREI STOP ZUM WAGEN IN DER WALLGASSE STOP` |

`{NAME}` ist WEISS oder ROTH.

### Gespräche der Wachen

Die Sätze in `game/Chatter.cs` verraten heute die Karte von Einsatz 1. Für Einsatz 2 braucht es eigene Listen je Mission. Wunsch an Level oder Sequenzen: ein Block `chatter` in der Missionsdatei mit `guards` und `others`. Die allgemeinen Sätze ohne Spielhinweis bleiben für alle Einsätze.

Ohne Spielhinweis:

- „Seit wann liefern wir Leute ans Werk?“
- „Pieper unterschreibt alles, was Brandt ihm hinlegt.“
- „Der Drucker hat die ganze Nacht kein Wort gesagt.“
- „Der Junge in Zelle zwei hustet seit Stunden.“
- „Mein Bruder steht am Feuerfass, und ich bewache seinesgleichen.“
- „Vom Wall zieht es wie aus einem Keller.“
- „Die Lenz hätte das nicht mitgemacht. Darum ist sie auch weg.“
- „Noch zwei Stunden, dann sind die zwei nicht mehr unser Ärger.“

Mit Spielhinweis, geprüft gegen die Karte oben. Wer die Karte ändert, prüft sie mit:

- „Um sechs kommt der Werkschutz und holt die zwei.“ (Verstärkung ist Werkschutz)
- „Der Schließer nimmt Geld. Hat er immer.“ (käuflich für 200)
- „Am Hoftor steht schon einer vom Werk. Mit dem redet keiner.“ (Werkschutz, nicht käuflich)
- „Die Pforte zur Gerichtsgasse ist zu. Aber da schaut nie einer nach.“ (verschlossen, kein Posten)
- „Der mit dem Hund geht die Gasse rauf und runter.“ (Rundgang des Hundeführers)
- „Am Wall steht einer mit Gewehr und friert.“ (Schütze im Osten)
- „Der Junge trägt Zettel zwischen Wache und Gericht.“ (Rundgang des Boten)
- „Auf der Wallstraße kommt immer gerade einer. Lange ist die nie leer.“ (gegenläufige Streifen, sie begegnen sich im gebauten Einsatz etwa alle 9 s)
- „Der an der Tür hat den Schlüssel zur Pforte am Gürtel.“ (Türposten)

Bestochen:

- Schließer: „Hier, der Bund. Zelle eins und zwei. Ich war die ganze Nacht am Ofen.“
- Türposten: „Der kleine ist für die Pforte. Ich hab ihn wohl verloren.“
- Hundeführer: „Ich geh mit dem Hund ums Gericht. Lange.“
- Bote: „Ich hab keinen gesehen. Krieg ich noch einen Groschen?“

Nur wenn Greta im Trupp ist und der Schließer sie bemerkt, ohne zu kämpfen:

- Schließer: „Lenz? Du traust dich was.“

### Zurufe der Befreiten

| Wann | Weiß | Paul |
|---|---|---|
| Zelle geht auf | „Na endlich. Ich dachte, ihr druckt ohne mich.“ | „Ist Hanna dabei?“ |
| Folgt | „Ich bin Drucker, kein Läufer.“ | „Langsam. Die haben mir die Schuhe genommen.“ |
| Wache bemerkt etwas | „Still jetzt.“ | „Nicht zurück. Bitte nicht zurück.“ |
| Schüsse | „Runter, Junge!“ | „Lasst mich nicht hier!“ |
| Am Wagen | „Steht die Maschine noch? Dann drucken wir morgen.“ | „Beim Verhör saß einer vom Werk mit am Tisch.“ |

Ist Hanna im Trupp und lebt, antwortet sie an der Zelle: „Komm, Paul. Reden später.“ Sonst bleibt seine Frage ohne Antwort.

### Schlagzeilen

| Fall | Kopf | Zeile |
|---|---|---|
| `unseen` | Arrest am Nordwall über Nacht leer | Zwei Gefangene sind aus der Wachstube Nord verschwunden, die Schlösser sind unversehrt. Die Wache hat keine Erklärung, der Werkschutz wartete am Morgen vergeblich.{prisoners}{loot}{paid} |
| `quiet` | Ausbruch am Nordwall: Wache sah Gestalten | Ein Wachmann will Schatten im Arresthof gesehen haben. Als er nachsah, standen beide Zellen offen.{prisoners}{loot}{paid} |
| `loud` | Schüsse an der Wachstube Nord | Bewaffnete haben in der Nacht Gefangene aus dem Arrest geholt.{dead} Hauptmann Pieper spricht von einem Angriff auf die Ordnung der Stadt.{prisoners}{fallen}{loot}{paid} |
| `lost` | Überfall auf die Wachstube gescheitert | Die Stadtwache meldet einen abgewehrten Angriff am Nordwall und mehrere Festnahmen. Die Verlegung der Gefangenen ist verschoben.{dead} |
| `abandoned` | Schießerei am Nordwall, Täter flüchtig | Bewaffnete griffen die Wachstube Nord an und zogen sich zurück. Die Verlegung der Gefangenen ist verschoben.{dead} |
| `retreat` | Unruhe am Nordwall | Anwohner meldeten Gestalten hinter dem Amtsgericht. Die Wache fand nichts. |
| `loot` | | Auch die Bußgeldkasse der Wachstube fehlt. |

Den Fall `unseen` erreicht im gebauten Einsatz keine gemessene Spielweise (Messung der Level-Session, 2026-11-11): Wer gekauft oder niedergeschlagen wird, hat den Trupp vorher bemerkt. Der Text bleibt als Rückfall stehen. Kein anderer Text des Einsatzes verspricht einen unentdeckten Ausgang.

Neuer Platzhalter `{prisoners}` für die volle Fassung: leer, wenn beide leben. Sonst „ Einer der Gefangenen kam dabei ums Leben.“ Den Einbau in `Headlines.cs` übernimmt die Level-Session (Absprache vom 2026-11-11).

„Verschoben“ steht dort, damit ein neuer Versuch nach einer Niederlage stimmt: Die beiden sitzen dann immer noch im Arrest.

### Telegramme

| Fall | Text |
|---|---|
| `lost` | `AN ABTEILUNG K STOP WACHSTUBE HAT GEHALTEN STOP VERLEGUNG VERSCHOBEN STOP WIR HABEN NOCH EINE NACHT STOP K` |
| `abandoned` | `AN ABTEILUNG K STOP ABGEBROCHEN STOP DIE ZWEI SITZEN NOCH STOP KOPF RUNTER STOP K` |
| `retreat` | `AN ABTEILUNG K STOP ZU VIELE AUGEN AM WALL STOP NEUER VERSUCH VOR SECHS STOP K` |

### Sequenz vor dem Einsatz

Kolb aus dem Off, Präteritum, nach `NOIR_STYLE_GUIDE.md`. Das sind nur ihre Kästen. Den Szenenplan mit Bildern legt die Sequenzen-Session dem Nutzer vor.

1. „Am Tag nach dem Buch holte die Wache Anton Weiß von seiner Presse. Die Farbe war noch nass.“
2. „Den jungen Roth hatten sie schon länger. Ich hatte die Tage gezählt.“
3. „Am Morgen sollte der Werkschutz beide übernehmen. Ein Gesetz dafür gab es nicht. Es gab Pieper, und Pieper unterschrieb.“
4. „Ich schickte den Trupp um vier. Um sechs wäre es zu spät gewesen.“

### Sequenz nach dem Einsatz

Nach der Zeitung, nur beim Sieg:

| Fall | Kolbs Kästen |
|---|---|
| Beide leben | „Weiß stand am Abend wieder an der Presse. Er fragte nicht, was es gekostet hatte.“ / „Der Junge sagte, beim Verhör habe einer vom Werk mit am Tisch gesessen. Ich schrieb es auf. Überrascht war ich nicht.“ |
| Weiß tot | „Weiß kam nicht zurück an seine Presse. Sein Lehrling setzte weiter. Langsamer, aber er setzte.“ / dann der Satz über den Jungen |
| Paul tot | „Weiß stand am Abend wieder an der Presse.“ / „Den Jungen brachten sie im Wagen zurück. Manche Nachrichten telegrafiert man nicht.“ |

## 6. Was andere Bereiche dafür brauchen

| Bereich | Was |
|---|---|
| Level | Karte, Gegner, Türen, Start und Ziel nach Abschnitt 2 bis 4. Lohn 350 steht im Stadtplan |
| Mechanik | Gefangene (volle Fassung) oder Entscheidung für die kleine. Schließer öffnet Zellen. Optional halber Preis mit Greta |
| Kampagne | Merker je Gefangenem: lebt, tot |
| Grafik | Stadtwall, Amtsgericht, Wachstube mit Arresthof und niedrigem Wachlokal, Kaserne, Remise mit Gefangenenwagen, Beamtenhäuser. Zwei Figuren: Weiß mit Schürze und grauem Haar, Paul schmal, in Hemd und Socken. Das Grafikthema des Viertels folgt in `grafikthemen.md` |
| Ton | Zellentür, Schlüsselbund, Husten, Glocke vom Amtsgericht zur vollen Stunde |
| Sequenzen | Szenenplan aus den Kästen oben. Block `chatter` je Mission, falls ihr die Missionsdaten lest |
| Story | Sätze mit Spielhinweis nach dem Bau gegen die Karte prüfen (erledigt am 2026-11-11 gegen `data/mission_02.json`, im Spiel nicht angesehen) |

## 7. Prüfung

| Punkt | Ergebnis |
|---|---|
| Wissen | Niemand nennt Sperling, Rehm oder Brandts Plan. „Notstand“ fällt nicht |
| Figuren | Kein Text setzt einen bestimmten Agenten voraus. Sätze mit Greta und Hanna haben eine Bedingung |
| Zeit | 24. November 1927 ist ein Donnerstag. Zwei Tage nach Einsatz 1 |
| Spiel | Alle Gegner aus dem Bestand, Preise aus `../konzept.md`. Neue Regel nur für Gefangene |
| Welt | Die Wache ist müde und käuflich, der Werkschutz nicht. Brandt tritt nicht auf |
| Offen gelassen | Ob ein Viertel zurückfallen kann und wie der Notstand auf dem Plan aussieht, berührt dieser Einsatz nicht |
