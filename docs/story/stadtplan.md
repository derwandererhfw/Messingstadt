# Stadtplan

Stand 2026-11-11. Auftrag des Nutzers über die Hauptsession: Messingstadt hat fünf Viertel. Anfangs gehören alle den Brandt-Werken, nach einem erfolgreichen Einsatz hält der Rote Hammer das Viertel. Die Daten dazu stehen in `stadtplan-vorlage.json`, die Kampagne übernimmt sie nach `data/stadtplan.json`.

Kennzeichen wie im Skill `messingstadt-story`. Viertel 1 ist IM SPIEL. Namen und Auftragsarten der Viertel 2 bis 5 sind KANON (freigegeben 2026-11-11), die Auftragstexte und Hinweise für Level sind Ausblick und VORSCHLAG.

## Was der Plan zeigt

Der Plan liegt bei Martha Kolb im Keller. Er zeigt nicht, wem die Häuser gehören, sondern wem die Leute folgen. Grün heißt: Hier gilt Brandts Wort, die Wache schaut weg, der Werkschutz kommt, wenn er gerufen wird. Rot heißt: Hier hält der Hammer, hier gibt es Türen, die aufgehen (ABLEITUNG aus `../story.md`, Ausgangslage).

Die Viertel öffnen sich in fester Reihenfolge, weil jeder Einsatz auf dem Wissen des vorigen aufbaut (`wissen.md`). Sind alle fünf gehalten, öffnet sich der sechste Einsatz „Der Anzeiger“.

## Die fünf Viertel

| Kennung | Name | Wer wohnt dort, was steht dort | Warum es Brandt wichtig ist |
|---|---|---|---|
| `viertel_1` | Kesselviertel | Arbeiterfamilien in Mietshäusern vor dem Werkstor. Dazu die Zweigstelle der Bank, das Hotel Adler, das Lager, der Fabrikhof und die Haltestelle der Linie 7 | Hier liegen seine Kessel und seine Lohnkasse. Wer das Viertel vor dem Tor verliert, verliert die Schicht |
| `viertel_2` | Nordwall | Beamte, Wachleute und ihre Familien am alten Stadtwall. Wachstube Nord, Arrestzellen, Amtsgericht | Die Wache sperrt für ihn weg, wen er nennt. Ohne den Nordwall muss er seine Gegner selbst holen |
| `viertel_3` | Gleisviertel | Rangierer, Lokführer, Schlafgänger in Baracken zwischen den Gleisen. Güterbahnhof Ost, Stellwerk, Kohlenlager | Über die Gleise kommen Kohle und Streikbrecher. Steht der Bahnhof, stehen die Werke von allein |
| `viertel_4` | Kontorviertel | Prokuristen, Anwälte, Herrschaften mit Dienstboten. Hauptsitz der Kontorbank, Börsensaal, Villen am Kanal | Hier liegt sein Kredit und hier liegen seine Geheimnisse, in Valks Schließfächern |
| `viertel_5` | Altstadt | Handwerker, Wäscherinnen, Drucker in engen Gassen. Fischmarkt, Druckhaus des Anzeigers, die Wäscherei mit der Krankenstube im Keller | Hier sitzt der Hammer. Brandt will die Altstadt nicht haben, er will sie abriegeln |

Viertel 1 stimmt mit dem vorhandenen Einsatz überein (IM SPIEL, `data/mission_01.json`, `tools/art/generate_concept.py`). Die Linie 7 nennt die Hauptsession, im Kanon stand sie bisher nicht.

## Aufträge

Ton: Aktenvermerk von Kolb, unterschrieben mit „K.“. Kein Text nennt einen Agenten.

### viertel_1 Kesselviertel: holen

- Einsatz: „Das Kontobuch“, Dienstag, 22. November 1927, 2:40 Uhr (IM SPIEL)
- Auftrag auf dem Plan: `Kontobuch der Brandt-Werke liegt bis zum Morgen am Mietshaus. Holen und zum Wagen in der Querstraße bringen. K.`
- Auf dem Spiel: `Ohne das Buch ist das Schwarzgeld ein Gerücht. Mit ihm ist es ein Beweis.`

Der lange Auftrag im Einstieg bleibt, wie er in `data/mission_01.json` steht.

### viertel_2 Nordwall: befreien

- Einsatz: „Die Wachstube“, Donnerstag, 24. November 1927, 4:10 Uhr
- Auftrag: `Zwei von uns sitzen in der Wachstube Nord: Paul Roth und der Drucker Anton Weiß. Am Morgen übergibt die Wache sie dem Werkschutz. Vorher raus mit beiden. K.`
- Auf dem Spiel: `Ohne Weiß druckt der Hammer kein Blatt mehr. Und was der Werkschutz mit Gefangenen macht, steht in keiner Zeitung.`
- Für Level: Zellen, Dienstplan mit Schichtwechsel, viele käufliche Wachleute, ein Offizier. Bestechen soll sich lohnen.

### viertel_3 Gleisviertel: sabotieren

- Einsatz: „Der Güterbahnhof“, Sonntag, 27. November 1927, 23:15 Uhr
- Auftrag: `Um Mitternacht rollt ein Zug mit dreihundert Streikbrechern in den Güterbahnhof Ost. Stellwerk und Einfahrweiche lahmlegen. Der Zug bleibt draußen. K.`
- Auf dem Spiel: `Kommen sie an, laufen am Montag die Maschinen. Dann war alles umsonst.`
- Für Level: zwei Ziele (Stellwerk, Weiche) statt eines Gegenstands, Zeitdruck bis zur Einfahrt, Hundeführer zwischen den Gleisen. Die Wachen sind auffällig gut vorbereitet.

### viertel_4 Kontorviertel: überzeugen

- Einsatz: „Das Schließfach“, Mittwoch, 30. November 1927, 1:30 Uhr
- Auftrag: `Prokurist Dorn hat den Schlüssel zu Valks Schließfach und drei Monate Mietschulden. Bringt ihn dazu, aufzuschließen. Mit Geld oder mit Worten. K.`
- Auf dem Spiel: `Im Fach liegt das zweite Buch, das mit den richtigen Namen. Ohne Dorn kommt keiner hinein, ohne dass die halbe Stadt es hört.`
- Für Level: Die Zielperson ist ein Mensch, kein Gegenstand. Der Buchhalter überzeugt ihn wie beim Bestechen, nur teurer und aus der Nähe. Danach geht Dorn mit zum Fach und muss lebend ankommen. Werkschutz in der Bank, nicht käuflich.

### viertel_5 Altstadt: ausschalten

- Einsatz: „Die Falle“, Freitag, 2. Dezember 1927, 3:00 Uhr
- Auftrag: `Der Werkschutz riegelt die Altstadt ab. Sein Führer Rademacher steht am Fischmarkt und gibt die Befehle. Fällt er aus, fällt die Sperre. Wie, ist eure Sache. K.`
- Auf dem Spiel: `Hinter der Sperre liegen die Krankenstube, die Bücher und alle, die uns bisher die Tür aufgemacht haben.`
- Für Level: Der Trupp beginnt eingeschlossen im eigenen Viertel. Ziel ist eine Person in der Mitte vieler gepanzerter Gegner. „Ausschalten“ heißt tot oder bewusstlos, beides zählt. Die Zeitung unterscheidet es.

## Was sich am freigegebenen Verlauf ändert

`../story.md` nennt für Einsatz 4 einen Einbruch und für Einsatz 5 eine Flucht. Der Auftrag verlangt Abwechslung: holen, sabotieren, befreien, überzeugen, ausschalten. Deshalb der Vorschlag:

| Einsatz | Bisher (KANON) | Vorschlag | Was gleich bleibt |
|---|---|---|---|
| 4 | Einbruch ins Schließfach | Überzeugen: Prokurist Dorn schließt auf | Das Zweitbuch, Emils alter Arbeitsplatz, „Sperling ist Rehm“ |
| 5 | Flucht aus dem Versteck | Ausschalten: Rademacher, Führer des Werkschutzes | Rehms Verrat, die Altstadt als Falle, der Notstand in derselben Nacht |

Beides hat der Nutzer am 2026-11-11 freigegeben („Ja, beide ändern“), `../story.md` ist nachgezogen.

Neue Namen (KANON, freigegeben 2026-11-11): Kesselviertel, Nordwall, Gleisviertel, Kontorviertel, Fischmarkt, Prokurist Albert Dorn, Werkschutzführer Gustav Rademacher.

## Farben

Alle Töne außer dem dunklen Rand des Hammers gibt es schon im Spiel.

| Seite | Ton | Hex | Woher | Wofür auf dem Plan |
|---|---|---|---|---|
| Brandt-Werke | Werksgrün dunkel | `#36544E` | Blech der Fabrik, Mantel des Werkschutzes (`green_d` in `tools/art/generate_concept.py`) | Rand und Schraffur |
| Brandt-Werke | Werksgrün | `#4C7064` | Blech der Fabrik (`green`) | Fläche |
| Brandt-Werke | Creme | `#DED0B0` | Schriftzug „BRANDT WERKE“ auf dem Hallendach (`cream`) | Schrift auf Grün |
| Roter Hammer | Stempelrot | `#8C1A14` | `PaperUi.StampRed`, alle Stempel und Kopfzeilen des Hammers | Fläche und Stempel |
| Roter Hammer | Stempelrot dunkel | `#5E120E` | neu, aus dem Stempelrot abgedunkelt | Rand |
| Roter Hammer | Bannerrot | `#A03832` | Schrift auf dem STREIK-Banner (`awn_r`) | Schrift und kleine Marken |

Begründung: Brandt ist im Spiel schon grün (Fabrik, Werkschutz) und der Hammer schon rot (Stempel, Banner). Kaltes Grün gegen warmes Rot trennt sich auf dem vergilbten Papier (`#AD9E7A`) klar, und beide sind dunkel genug für den düsteren Look.

Zwei Hinweise für Menüs und Grafik:

- Wer Rot und Grün schlecht unterscheidet, sieht beide Töne ähnlich dunkel. Deshalb Brandt-Viertel schraffiert zeichnen und Hammer-Viertel voll mit Stempel. Dann trägt die Form die Aussage mit.
- Messing (`#C8963E`) bleibt der Titel der Stadt und gehört keiner Seite. Blau bleibt in den Sequenzen Brandts Überwachung (`NOIR_STYLE_GUIDE.md`, Abschnitt 4) und kommt auf dem Plan nicht vor.

## Texte für den Bildschirm

| Stelle | Text |
|---|---|
| Überschrift | `ROTER HAMMER   STADTPLAN` |
| Stempel neben der Überschrift | `VERTRAULICH` |
| Unterzeile | `Stand der Viertel, November 1927` |
| Viertel in Brandts Hand | `BRANDT-WERKE` |
| Stempel für ein erobertes Viertel | `HAMMER HÄLT` |
| Gesperrtes Viertel | je Viertel ein fertiger Satz, siehe Vorlage (`locked`), etwa `Kein Weg hinein. Erst den Nordwall halten.` |
| Offenes Viertel | `Einsatz bereit` |
| Viertel ohne fertigen Einsatz | `Kein Auftrag. K. meldet sich.` |
| Knopf | `Einsatz annehmen` |
| Alle fünf gehalten | `Die Stadt hält. Eine Nacht noch. K.` |

Der Satz für gesperrte Viertel steht ausgeschrieben in der Vorlage, weil der Artikel wechselt („das Kesselviertel“, „den Nordwall“).

## Offen

- Entschieden: Ein Viertel fällt nicht an Brandt zurück. Einmal gehalten bleibt gehalten (Nutzer, 2026-11-14: „Nein“).
- Wie der Notstand nach Einsatz 5 auf dem Plan aussieht. Vorschlag: Der Plan bleibt rot, aber über allem liegt der Stempel `NOTSTAND`. Die Leute halten zum Hammer, die Straße gehört trotzdem dem Werkschutz.
