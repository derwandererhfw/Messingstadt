# Alleinstellungsmerkmal

Stand 2026-11-12. Ergebnis des Gesprächs zwischen dem Nutzer und der Session „Alleinstellung“. Leitsatz und Auswahl der Mechaniken hat der Nutzer entschieden. Zahlen, Etappen und Einzelheiten der Regeln sind VORSCHLAG und gehen über die Hauptsession an die Fachsessions. Dieses Dokument ändert weder Code noch Daten.

## Leitsatz

> **Eine Stadt, die keinen Toten vergisst, weder deine noch ihre.**

Was das heißt: In Messingstadt stirbt niemand namenlos. Jede Wache hat einen Namen und jemanden, dem sie fehlt. Jeder eigene Agent hat eine Akte, die im Schrank bleibt. Die Stadt merkt sich beides und zeigt es im nächsten Einsatz auf der Straße, nicht erst im Abspann.

## Was der Nutzer vorgegeben hat

| Frage | Antwort |
|---|---|
| Was hat am Vorbild gereizt? | Die düstere Stimmung |
| Was soll ein Spieler nach einer Stunde erzählen? | „Die Stadt merkt sich alles“ und „Mein Otto ist tot“ |
| Was darf das Spiel nicht sein? | Kein Lesespiel, kein Verwaltungsspiel |
| Wie groß darf der Umbau werden? | Mittel: ein bis zwei neue Regeln auf dem Bestand, Story und sechs Einsätze bleiben |

Daraus folgen zwei Leitplanken für alles Weitere:

- **Kein Lesespiel.** Das Gedächtnis der Stadt zeigt sich in Namen, Stempeln, Türen, Lichtern und einzelnen Sätzen. Keine neuen langen Texte, keine neuen Sequenzen dafür.
- **Kein Verwaltungsspiel.** Kein neuer Bildschirm mit Zahlen und Reglern. Der Spieler steuert das Gedächtnis der Stadt nur durch sein Verhalten im Einsatz.

## Bestandsaufnahme

### Schon eigen

- Das Thema: Streik 1927, eine Stadt, die einem Konzern gehört. Der Trupp sind Schlosser, Buchhalter, Kurierin und eine entlassene Wachtmeisterin, keine Soldaten.
- Die Wachen sind arme Leute, keine Monster. Sie frieren, schimpfen und nehmen Geld.
- Die Zeitung nach jedem Einsatz druckt die Sicht der Wache und zählt die Toten.
- Papier als Bildsprache: Akten, Stempel, Telegramme, Stadtplan.
- Das Finale ohne richtiges Ende, mit einer leisen und einer blutigen Fassung.

### Austauschbar

- Der Kern im Einsatz: Sichtkegel, Schleichen im Dunkeln, Verstärkung nach dem Schuss. Das gehört zum Genre der Echtzeit-Taktik.
- Waffen kaufen, Werte ausbauen, Viertel einfärben.
- Wertung leise, laut, unentdeckt als Lohnzuschlag.

### Halb genutzt

- **Rückhalt** ist bisher eine Zahl, die Beiträge bringt. Auf der Straße merkt man ihn erst im Finale.
- **Tote Wachen** werden gezählt, wirken aber nur über Fahndung und im Finale.
- **Passanten, Fenster, Streikposten** reagieren auf Schüsse, aber nicht darauf, wer der Spieler bisher war.
- **Endgültiger Tod und Krankenstube** sind gebaut. Die Toten verschwinden danach aus dem Spiel.
- **Gespräche der Wachen** kennen den Verlauf der Kampagne nicht.

Der Leitsatz setzt genau hier an: Er verbindet, was schon da ist, und macht es im Einsatz spürbar.

## Geprüfte Richtungen

| Richtung | Idee | Ergebnis |
|---|---|---|
| A. Die Stadt führt Buch | Was der Spieler in einem Viertel tut, spürt er im nächsten Einsatz: Türen, Lichter, Passanten helfen oder verraten | **Gewählt**, mit B verbunden |
| B. Jeder Tote hat einen Namen | Wachen haben Namen und fehlen später jemandem. Eigene Tote bleiben sichtbar | **Gewählt**, mit A verbunden |
| C. Die Stadt als Werkzeug | Schichthorn, Straßenbahn, Donner und Luftschiff werden Spielregel, etwa Lärm als Deckung | Nicht gewählt. Stärkt die Stimmung, macht das Spiel aber besser und nicht einzigartig. Lärm als Deckung kennen andere Schleichspiele. Bleibt als mögliche spätere Zutat |
| D. Der Streik läuft ab | Die Streikkasse leert sich täglich, Hunger frisst den Rückhalt | Verworfen. Wird schnell ein Verwaltungsspiel, das der Nutzer ausgeschlossen hat |

Einordnung, nur zum Genre: Dass eine Welt auf Töten reagiert, gibt es in anderen Spielen, ebenso den schmerzhaften Tod eigener Leute. Eigen wird Messingstadt durch die Verbindung: Beide Seiten haben Namen, es ist dieselbe Stadt, und die Folge steht nicht in einer Zahl, sondern an der nächsten Tür.

## Die vier Mechaniken

Der Nutzer hat alle vier gewählt. Sie bauen aufeinander auf: Erst braucht es Namen, dann können Wachen und Stadt darauf antworten.

### 1. Das Totenbuch

**Regel:** Jede Wache hat einen Namen. Fällt sie, merkt sich die Kampagne Name, Einsatz und Viertel. Die eigenen Toten stehen im selben Buch.

**Im Spiel:**

- Fährt die Maus über eine Wache, steht neben Art und Preis ihr Name.
- Die Zeitung nennt die Toten des Einsatzes mit Namen statt nur als Zahl. Bei vielen Toten die ersten Namen und „und weitere“.
- Im Aktenschrank liegt ein Blatt „Totenbuch“: links die eigenen, rechts die der Wache. Ein Blatt, keine Bedienung.
- Das Finale greift darauf zu: Die Titelseite der blutigen Kampagne druckt diese Namen. Die Vorlage in `story/einsatz_06_anzeiger.md` sieht das mit `{n}` schon vor.

**Schon da:** Zähler für tote Wachen je Einsatz, Zeitung mit `{dead}`, Aktenschrank, Stempel GEFALLEN.

**Offen für Mechanik:** Ob der Totschläger tötet oder bewusstlos schlägt. Für den Leitsatz muss klar sein: Niedergeschlagene stehen nicht im Totenbuch. Einsatz 5 unterscheidet bei Rademacher schon tot und bewusstlos.

### 2. Die Wache vergisst nicht

**Regel:** Die Wachen eines Einsatzes wissen, was im Totenbuch steht.

**Im Spiel (Zahlen sind VORSCHLAG):**

- Hat der Spieler im letzten Einsatz Wachen getötet, kostet Bestechen mehr, und ab einer Schwelle nehmen einzelne Wachen gar kein Geld mehr.
- Hat er bisher niemanden getötet, wird Bestechen billiger, und einzelne Wachen schauen von selbst weg. Das Finale tut das schon mit den Hofwachen.
- Wachen nennen die Toten in ihren Gesprächen, ein Satz, mit Namen aus dem Totenbuch: „Krüger hatte drei Kinder.“
- Eine Wache, die einen Einsatz überlebt hat, kann im nächsten wieder Dienst haben. Wer sie gekauft hat, kennt ihren Preis.

**Schon da:** Bestechen mit Preisen je Gegnerart, Fahndung, Gespräche in der Missionsdatei, „bestochen“ als Zustand. Der Nachlass beim Bestechen mit Greta, an dem die Level-Session gerade arbeitet, passt dazu: Greta war Wachtmeisterin, die Wachen kennen sie.

**Abgrenzung zur Fahndung:** Die Fahndung bleibt, was sie ist, nämlich mehr Verstärkung. Neu ist die Haltung der einzelnen Wache.

### 3. Türen der Stadt

**Regel:** Der Rückhalt wirkt im Einsatz. Die Leute im Viertel helfen dem Trupp oder stellen sich gegen ihn.

**Im Spiel:**

- Hoher Rückhalt, wenig Blut: Bewohner lassen eine Hoftür offen, die sonst der Schlosser öffnen müsste. Ein Fenster geht aus und macht einen Weg dunkel. Ein Passant bleibt stehen und nickt in eine Richtung.
- Niedriger Rückhalt oder viel Blut: Die Türen sind zu. Ein Passant, der den Trupp sieht, läuft zur nächsten Wache, wie heute der Laufbursche.
- Je Einsatz zwei bis drei solche Stellen, fest im Grundriss markiert. Der Spieler sieht beim ersten Blick auf die Karte, ob die Stadt mit ihm ist.

**Schon da:** Rückhalt 0 bis 5, Passanten mit Erschrecken und Kauern, Fenster, die ausgehen, Türen mit Marken, Licht als Schleichregel, der Laufbursche als Melder. Einsatz 5 und 6 nennen in den Bauvorlagen schon Bewohner, die Riegel offen lassen, und Arbeiter, die Gassen füllen.

**Offen für Kampagne:** Ob der Rückhalt eine Zahl für die ganze Stadt bleibt oder je Viertel zählt. Je Viertel wäre stimmiger, ist aber mehr Umbau.

### 4. Narben

**Regel:** Wer aus der Krankenstube kommt, ist nicht mehr derselbe. Wer stirbt, bleibt sichtbar.

**Im Spiel:**

- Der Sturz hinterlässt eine Narbe, die man sieht und spürt: ein Vermerk in der Akte, ein Merkmal am Aktenfoto, eine kleine dauerhafte Schwäche. Heute fallen Beine und Herz auf Stufe 1, lassen sich aber wieder hochkaufen.
- Die Akte eines Toten bleibt mit Stempel im Schrank und steht im Totenbuch.
- Der Neue übernimmt den Beruf, kann ihn aber schlechter. Er öffnet Türen langsamer oder besticht teurer, bis er Einsätze überlebt hat.

**Schon da:** Krankenstube, „angeschlagen“, endgültiger Tod, Anwerben, feste Namen für Neue, Stempel KRANKENSTUBE und GEFALLEN, abgedunkelte Aktenfotos.

**Widerspruch zum Kanon, zu klären mit Story:** `story.md` sagt, Tote werden später nicht mehr genannt, außer in Kolbs Nachruf. Der Leitsatz verlangt das Gegenteil für Akte und Totenbuch. Vorschlag: Die Regel gilt weiter für Erzähltexte, die an einer Figur hängen. Akte, Totenbuch und Zeitung dürfen die Toten nennen.

## Was das für die Bereiche bedeutet

| Bereich | Folgen |
|---|---|
| Story | Namen für alle Wachen der sechs Einsätze, kurze Sätze der Wachen über Tote, Fassungen der Zeitung mit Namen. Kanonregel zu den Toten anpassen. Keine neuen langen Texte |
| Kampagne | Totenbuch im Spielstand: Name, Seite, Einsatz, Viertel. Abfragen für „Tote im letzten Einsatz“ und „Tote gesamt“. Entscheidung Rückhalt je Viertel oder für die Stadt. Überlebende Wachen wiedererkennen. Alte Spielstände übernehmen |
| Mechanik und Level | Preis und Käuflichkeit einer Wache hängen am Totenbuch. Helfertüren, Fenster und Passanten als Stellen im Grundriss, geschaltet über Rückhalt. Tot und bewusstlos trennen. Lehrzeit für Neue |
| Gegner | Name je Wache in den Missionsdaten. Passant als Melder nach dem Muster des Laufburschen |
| Menüs | Name beim Überfahren einer Wache. Blatt „Totenbuch“ im Aktenschrank. Narbe in der Akte. Akte der Toten bleibt liegen |
| Grafik und Animation | Aktenfoto mit Narbe. Offene Hoftür, Bewohner in der Tür, Passant, der nickt oder wegläuft. Stempel für das Totenbuch |
| Atmosphäre | Fenster, die für den Trupp ausgehen, statt nur nach Schüssen. Die Stadt zeigt beim Einstieg, wie sie zum Trupp steht |
| Ton | Tür, die leise aufgeht. Fenster, das zuschlägt. Ruf nach der Wache. Stille, wenn ein Name fällt |
| Sequenzen | Zeitung nach dem Einsatz mit Namen. Titelseite des Finales aus dem Totenbuch. Sonst keine neuen Sequenzen |
| Spieltest und Balance | Prüfen, dass der laute Weg spielbar bleibt und nicht in eine Abwärtsspirale führt. Die Fahndung hatte dieses Problem schon einmal. Blut soll kosten, aber nicht die Kampagne beenden |

## Risiken

- **Abwärtsspirale.** Wer einmal laut war, hat teurere Wachen und geschlossene Türen und wird noch lauter. Gegenmittel: Die Stadt urteilt vor allem nach dem letzten Einsatz, nicht nach der Summe. Ein leiser Einsatz bringt Türen zurück. Nur das Totenbuch selbst vergisst nichts.
- **Textlast.** Namen und Sätze dürfen nicht zu Lesestoff werden. Ein Name, ein Satz, ein Stempel.
- **Unsichtbarkeit.** Wenn die Folge nur eine andere Zahl beim Bestechen ist, merkt sie keiner. Jede Folge braucht ein Bild oder einen Ton im Einsatz.
- **Strafe statt Wahl.** Der laute Weg muss ein gültiger Weg bleiben. Die Stadt bewertet nicht, sie antwortet.

## Reihenfolge der Umsetzung

| Etappe | Inhalt | Wer vor allem | Ergebnis für den Spieler |
|---|---|---|---|
| 1 | Namen und Totenbuch: Namen für die Wachen in Einsatz 1 und 2, Speichern in der Kampagne, Name beim Überfahren, Zeitung mit Namen, Blatt im Aktenschrank | Story, Kampagne, Menüs | Er sieht zum ersten Mal, wen er erschossen hat |
| 2 | Die Wache vergisst nicht: Preis und Käuflichkeit nach dem Totenbuch, Sätze mit Namen, Wegschauen bei leiser Kampagne | Mechanik, Gegner, Story, Balance | Einsatz 2 spielt sich anders, je nachdem, wie Einsatz 1 lief |
| 3 | Türen der Stadt: Helferstellen in Einsatz 1 und 2, Passant als Melder, Fenster für den Trupp | Level, Atmosphäre, Animation, Ton | Die Straße zeigt, ob die Stadt mit ihm ist |
| 4 | Narben: bleibende Folge des Sturzes, Akte der Toten, Lehrzeit der Neuen | Kampagne, Menüs, Grafik | Der Verlust eines Agenten bleibt sichtbar |
| 5 | Bauvorlagen der Einsätze 3 bis 6 nachziehen, Finale an das Totenbuch anschließen, Spieltest über die ganze Kampagne | Story, Level, Spieltest | Der Leitsatz gilt von der ersten bis zur letzten Nacht |

Etappe 1 ist klein und macht den Leitsatz sofort sichtbar. Etappe 2 und 3 sind die eigentlichen neuen Regeln und entsprechen dem mittleren Umbau. Etappe 4 baut fast nur auf Vorhandenem auf.

## Offene Entscheidungen

| Frage | Wer entscheidet |
|---|---|
| Tötet der Totschläger oder schlägt er bewusstlos? | Nutzer mit Mechanik |
| Rückhalt je Viertel oder für die ganze Stadt? | Nutzer mit Kampagne |
| Urteilt die Stadt nach dem letzten Einsatz oder nach der Summe? | Nutzer mit Balance |
| Dürfen Akte, Totenbuch und Zeitung die eigenen Toten nennen, entgegen der Regel in `story.md`? | Nutzer mit Story |
| Welche bleibende Folge hat eine Narbe genau? | Nutzer mit Kampagne |
| Kommt Richtung C (Stadt als Werkzeug) später als Zutat dazu? | Nutzer |
