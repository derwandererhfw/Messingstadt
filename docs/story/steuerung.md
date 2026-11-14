# Steuerung der Story

## Ziel

Eine zusammenhängende Geschichte über sechs Einsätze, deren Texte im Spiel (Aufträge, Telegramme, Zeitung, Gespräche, Akten) zueinander und zur Spielwelt passen. Grundlage ist `../story.md`.

## Stand

| Was | Stand |
|---|---|
| Story-Grundlage `../story.md` | Freigegeben, in `main` |
| Zeitungstexte je Mission (`headlines`) | Für Einsatz 1 fertig, in `main` |
| Berufe auf der Akte (`role`) | Für Einsatz 1 eingetragen, Commit e7e194c auf `claude/story-62427b` |
| Arbeitsumgebung (Skill, dieser Ordner) | Angelegt am 2026-11-06 |
| Stadtplan mit fünf Vierteln | Im Spiel, Texte aus `stadtplan.md` |
| Einsatz 2 | Wird auf 30 × 30 vergrößert („Der lange Rückweg“), Vorlage seit 2026-11-12 in Abschnitt 8 angeglichen, Namen und Rufe in `texte/wachen.md`. Der Kern ist gebaut, in `main` (`data/mission_02.json`). Texte der Vorlage wörtlich übernommen, Zurufe der Befreiten werden noch nicht angezeigt |
| Einsatz 3 bis 6 | Bauvorlagen `einsatz_03_gleisviertel.md` bis `einsatz_06_anzeiger.md`, Stand 2026-11-11. Noch nicht gebaut |
| Grafikthemen der Viertel | `grafikthemen.md`, Stand 2026-11-11 |

## Entscheidungsprotokoll

| Datum | Entscheidung | Wer |
|---|---|---|
| 2026-11-03 | Story-Grundlage aus dem Vorschlag freigegeben: Streik, Brandt braucht den Notstand, Verrat durch Rehm, sechs Einsätze, Ende mit Wahl | Nutzer |
| 2026-11-03 | „Gefallen“ heißt schwer verwundet, Krankenstube, Rückkehr auf Stufe 1 | Nutzer, mit der Grundlage |
| 2026-11-03 | Absender aller Aufträge und Telegramme ist Martha Kolb, Unterschrift „K.“ | Nutzer, mit der Grundlage |
| 2026-11-03 | Etappe 1 committen, mit `main` zusammenführen, pushen | Nutzer über die Hauptsession |
| 2026-11-06 | Arbeitsweise des „Story Studio“ für Messingstadt übernommen, ohne Syndicate-Bezug | Nutzer |
| 2026-11-06 | Dauertod beim zweiten Fall. Das Spiel erklärt das selbst | Nutzer |
| 2026-11-06 | Texte zum Dauertod freigegeben: Ärztin Ilse Marquardt, Akte, Ergebnisfenster, Telegramme, Namen für Neue (`texte/dauertod.md`) | Nutzer |
| 2026-11-06 | Auch bei einer Niederlage stirbt nur, wer zum zweiten Mal fällt. Alle anderen kommen in die Krankenstube | Nutzer |
| 2026-11-06 | Noir-Styleguide (`NOIR_STYLE_GUIDE.md`) und Skill `messingstadt-noir` freigegeben, „Freigeben“. Gilt für Sequenzen und längere Erzähltexte. Kommt mit Branch `claude/sequenzen-95d8ec` | Nutzer über die Sequenzen-Session |
| 2026-11-06 | Kolb erzählt die Sequenzen aus dem Off, Ich-Form, Präteritum. Innerer Monolog nur für sie, Agenten von außen. Sie verrät nichts über `wissen.md` hinaus | Nutzer über die Sequenzen-Session („1. Kolb“) |
| 2026-11-06 | Farbdramaturgie gilt nur für Sequenzen, nicht für die Spielgrafik im Einsatz | Nutzer über die Sequenzen-Session („2. nur Sequenzen“) |
| 2026-11-06 | Fahne und Stempel des Hammers bleiben in Sequenzen schwarzweiß, bis er Blut kostet. Menüs, Auftrag und Akte bleiben rot | Nutzer über die Sequenzen-Session („3. ja“) |
| 2026-11-06 | Keine Prosa-Fassung (Roman, Begleitheft) | Nutzer über die Sequenzen-Session („5. Nein“) |
| 2026-11-06 | Prolog „Drei Wochen“ vor Einsatz 1 freigegeben (`texte/prolog.md`, Branch `claude/sequenzen-95d8ec`). Panel 7: Hanna „Ich nehm die Pistole trotzdem mit.“, Greta „Nimm sie. Und lass sie stecken.“ Kolbs Zimmer ohne Farbakzent. Nicht wiederholbar, weil Panel 6 alle vier nennt | Nutzer über die Sequenzen-Session („Ja, freigeben“) |
| 2026-11-07 | Ersetzt „nicht wiederholbar“: Der Prolog läuft bei jedem „Neuen Spiel“, nicht beim Fortsetzen und nicht bei jedem Programmstart. Ein neues Spiel beginnt mit allen vieren, Panel 6 stimmt also immer | Nutzer über die Hauptsession („jedes „Neue Spiel“, also meine Empfehlung“) |
| 2026-11-11 | Stadtplan zur Einsatzwahl: fünf Viertel, anfangs alle bei den Brandt-Werken, nach einem gelungenen Einsatz hält der Hammer das Viertel. Inhalt in `stadtplan.md` | Nutzer über die Hauptsession |
| 2026-11-11 | Einsatz 4 wird Überzeugen (ein Prokurist schließt Valks Schließfach auf), Einsatz 5 wird Ausschalten (Führer des Werkschutzes, der die Altstadt abriegelt). „Der Anzeiger“ bleibt sechster Einsatz | Nutzer über die Hauptsession („Ja, beide ändern“) |
| 2026-11-11 | Namen freigegeben: Kesselviertel, Nordwall, Gleisviertel, Kontorviertel, Altstadt, Fischmarkt, Prokurist Albert Dorn, Werkschutzführer Gustav Rademacher | Nutzer („Ja, freigeben“) |
| 2026-11-11 | Auftrag: Story weiterschreiben. Zuerst Einsatz 2 als Bauvorlage, dann Grafikthema je Viertel, dann Einsatz 3 bis 5 und Finale | Nutzer über die Hauptsession |
| 2026-11-12 | Leitbild: „Eine Stadt, die keinen Toten vergisst, weder deine noch ihre.“ Vier Mechaniken: Totenbuch, die Wache vergisst nicht, Türen der Stadt, Narben. Inhalt in `../alleinstellung.md` | Nutzer mit der Session Alleinstellung |
| 2026-11-12 | Die Regel „Tote werden nicht mehr genannt“ gilt nur noch für Erzähltexte. Akte, Totenbuch und Zeitung nennen die Toten | Hauptsession auf Grundlage des gewählten Leitbilds, keine wörtliche Freigabe des Nutzers |
| 2026-11-12 | Einsatz 2 wird größer, 30 × 30: „Der lange Rückweg“ mit Friedhof der Namenlosen, Beamtenhöfen und Exerzierplatz. Drei Helfer mit Kerze im Fenster. Der Totschläger schlägt bewusstlos | Nutzer über die Hauptsession |
| 2026-11-12 | Bestechen kostet die Hälfte, wenn Greta in Rufweite ist | Gebaut laut Hauptsession |
| 2026-11-13 | Blutschuld 0 bis 6 für die ganze Stadt, Stufen hilft, schweigt, gegen den Trupp. Helfer hängen an „hilft“, die Wirtin mit lebender Greta auch an „schweigt“. Regel in `kanon.md` | Nutzer über die Hauptsession („Ja, so bauen“) |
| 2026-11-13 | Verstärkung in Einsatz 2 sind Wachleute aus der Kaserne, der Werkschutz tritt dort nicht auf. Der Satz zur Kerze bleibt in Reserve | Hauptsession |
| 2026-11-13 | Zeitung mit Namen: Die Story baut sie in `Headlines.cs`, sobald die Kampagne die Abfrage auf das Totenbuch meldet. Felder `person`, `rank`, `age` trägt Level ein | Hauptsession |
| 2026-11-13 | Zeitung: tote Wachen mit Namen und Anrede (gebaut in `Headlines.cs`). Eigene Tote bleiben Blutspuren und stehen nur in Akte und Totenbuch. Der Nutzer kann das später ändern | Hauptsession, auf Empfehlung der Story |
| 2026-11-13 | Feld `person` trägt den vollen Namen, der Rufname ist das letzte Wort, `call` überschreibt ihn bei den Jungen | Hauptsession |
| 2026-11-13 | Blutschuld geglättet: Der erste Tote einer Nacht ist frei, jeder weitere 1, höchstens 3 je Nacht. Stufen unverändert | Kampagne über die Hauptsession, keine wörtliche Freigabe des Nutzers |
| 2026-11-14 | Ein Viertel fällt nicht an Brandt zurück | Nutzer über die Hauptsession („Nein“) |
| 2026-11-14 | Einsatz 3 bis 6 bekommen die volle Fassung ihrer neuen Regel: Sabotageziele mit Zeitdruck, Überzeugen, Ausschalten, Entscheidung und Halten. Die kleinen Fassungen sind aus den Bauvorlagen gestrichen | Nutzer über die Hauptsession („volle Fassung“) |
| 2026-11-14 | Neue Gegnerart Werkschutzführer (Rademacher) | Nutzer über die Hauptsession („ja“) |
| 2026-11-14 | Texte, Namen und Orte der Bauvorlagen für Einsatz 2 bis 6 freigegeben. Lesart der Hauptsession: Die Vorschläge gelten, abgenommen wird weiter am gebauten Einsatz | Nutzer über die Hauptsession („ja“), Lesart von der Hauptsession |
| 2026-11-14 | Die Schlusssequenz steht teils im Präsens | Nutzer über die Hauptsession („ja“) |
| 2026-11-14 | Greta und Hanna bekommen eigene Fähigkeiten. Vorschlag der Story war: Greta fälscht Papiere, Hanna bringt Beute schneller weg. Wie das mit Gretas halbem Preis zusammenpasst, steht in `faehigkeiten.md` | Nutzer über die Hauptsession („ja“) |
| 2026-11-14 | Der Trupp erfährt vom Plan mit dem Notstand so, wie `wissen.md` es empfahl: Andeutung durch Gretas alte Kollegen in Einsatz 2, Gewissheit aus dem Zweitbuch in Einsatz 4 (Dorns Satz) | Nutzer über die Hauptsession („wie empfohlen“) |
| 2026-11-14 | Die Währung heißt Groschen | Nutzer über die Hauptsession („Groschen“) |

## Offene Entscheidungen

| Frage | Wer entscheidet | Folgen |
|---|---|---|
| Wie sieht der Notstand nach Einsatz 5 auf dem Stadtplan aus? | Nutzer mit Kampagne | Vorschlag in `stadtplan.md`, Abschnitt Offen: Stempel `NOTSTAND` über dem roten Plan |
| Geglättete Blutschuld: Der erste Tote einer Nacht ist frei | Nutzer | Gebaut von der Kampagne. Freigegeben hatte er: je Toter 1, höchstens 3 |
| Eigene Tote in der Zeitung nur als Blutspuren | Nutzer | Entscheidung der Hauptsession, Sätze mit Namen liegen als Reserve in `einsatz_02_nordwall.md` |
| Tote werden außerhalb von Erzähltexten genannt | Nutzer | Entscheidung der Hauptsession auf Grundlage des Leitbilds |
| Werte und Grenzen der Fähigkeiten von Greta und Hanna | Nutzer mit Mechanik | `faehigkeiten.md` |
| Zahlen im Spiel ohne Währung: Soll „Groschen“ angezeigt werden? | Nutzer mit Menüs | Fundstellen unter Nächste Schritte |

## Nächste Schritte

1. Einsatz 3 „Der Güterbahnhof“ wird gebaut. Danach Sätze mit Spielhinweis gegen Karte und Messung prüfen.
2. Namen für die Wachen der Einsätze 3 bis 6, in der Form von `texte/wachen.md`.
3. Titelseite des Finales aus dem Totenbuch, wenn Einsatz 6 gebaut wird.
4. Recherche zum Zeitkolorit für die offenen Fragen in `kanon.md`.

### Zahlen ohne Währung im Spiel

Geprüft am 2026-11-14, nichts geändert. Die Anzeige gehört Menüs und Hauptsession.

| Wo | Text |
|---|---|
| `game/Armory.cs` | „Kasse {Zahl}“, „Preis {Zahl}   Verkauf {Zahl}“ |
| `game/CityPlan.cs` | „Belohnung: {Zahl}“ |
| `game/Main.cs` | „Lohn {Zahl}“, „Kasse {Zahl}“, „Beitrag {Zahl} je Einsatz“, „KASSE {Zahl}“ im Einsatz, Preis beim Bestechen |

In den Texten der Story steht Geld nur als Wort („Groschen“ bei Kurt und beim Boten) oder als Zahl in Regeln für andere Sessions. Kein Text nennt eine andere Währung.
