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
| Einsatz 2 bis 6 | Nur als Verlauf in `../story.md`, keine Texte |

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

## Offene Entscheidungen

| Frage | Wer entscheidet | Folgen |
|---|---|---|
| Stadtplan: Kann ein Viertel an Brandt zurückfallen? Wie sieht der Notstand auf dem Plan aus? | Nutzer mit Kampagne | Vorschläge in `stadtplan.md`, Abschnitt Offen |
| Eigene Fähigkeiten für Greta und Hanna? | Nutzer mit Mechanik | Vorschlag Story: Greta fälscht Papiere, Hanna bringt Beute schneller weg |
| Wann erfährt der Trupp vom Plan mit dem Notstand? | Nutzer | Siehe `wissen.md`, betrifft Einsatz 2 bis 4 |
| Welche Währung gilt in Messingstadt? | Nutzer | Im Spiel stehen nur Zahlen (200, 300). Ein Arbeiter sagt „Groschen“ |

## Nächste Schritte

1. Einbau der Dauertod-Texte durch Kampagne und Menüs begleiten.
2. Einsatz 2 „Die Wachstube“ im Modus Missionsdesign planen und vorlegen.
3. Recherche zum Zeitkolorit für die offenen Fragen in `kanon.md`.
