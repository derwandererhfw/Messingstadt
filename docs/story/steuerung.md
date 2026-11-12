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

## Offene Entscheidungen

| Frage | Wer entscheidet | Folgen |
|---|---|---|
| Stadtplan: Kann ein Viertel an Brandt zurückfallen? Wie sieht der Notstand auf dem Plan aus? | Nutzer mit Kampagne | Vorschläge in `stadtplan.md`, Abschnitt Offen |
| Je Einsatz volle oder kleine Fassung der neuen Regel: Gefangene (2), Sabotageziele mit Zeitdruck (3), Zielperson überzeugen (4), Zielperson ausschalten (5), Entscheidung und Halten (6) | Nutzer mit Mechanik | Jeweils Abschnitt 4 der Bauvorlage. Die kleine Fassung kommt ohne neue Regel aus, macht den Einsatz aber dem ersten ähnlicher |
| Neue Gegnerart Werkschutzführer (Rademacher) | Nutzer mit Gegner | `einsatz_05_altstadt.md`, Abschnitt 3 |
| Texte, Namen und Orte der Bauvorlagen für Einsatz 2 bis 6 | Nutzer | Stehen als VORSCHLAG in den Bauvorlagen und werden am gebauten Einsatz abgenommen |
| Schlusssequenz teils im Präsens | Nutzer mit Sequenzen | `einsatz_06_anzeiger.md`, Abschnitt 5 |
| Eigene Fähigkeiten für Greta und Hanna? | Nutzer mit Mechanik | Vorschlag Story: Greta fälscht Papiere, Hanna bringt Beute schneller weg |
| Wann erfährt der Trupp vom Plan mit dem Notstand? | Nutzer | Siehe `wissen.md`, betrifft Einsatz 2 bis 4 |
| Welche Währung gilt in Messingstadt? | Nutzer | Im Spiel stehen nur Zahlen (200, 300). Ein Arbeiter sagt „Groschen“ |

## Nächste Schritte

1. Einbau der Dauertod-Texte durch Kampagne und Menüs begleiten.
2. Einsatz 2 „Die Wachstube“ wird gebaut. Danach Sätze mit Spielhinweis gegen die Karte prüfen, `{prisoners}` in `Headlines.cs` einbauen und die Texte in die Missionsdatei eintragen.
3. Recherche zum Zeitkolorit für die offenen Fragen in `kanon.md`.
