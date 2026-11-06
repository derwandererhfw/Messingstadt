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

## Offene Entscheidungen

| Frage | Wer entscheidet | Folgen |
|---|---|---|
| Eigene Fähigkeiten für Greta und Hanna? | Nutzer mit Mechanik | Vorschlag Story: Greta fälscht Papiere, Hanna bringt Beute schneller weg |
| Wann erfährt der Trupp vom Plan mit dem Notstand? | Nutzer | Siehe `wissen.md`, betrifft Einsatz 2 bis 4 |
| Welche Währung gilt in Messingstadt? | Nutzer | Im Spiel stehen nur Zahlen (200, 300). Ein Arbeiter sagt „Groschen“ |

## Nächste Schritte

1. Einbau der Dauertod-Texte durch Kampagne und Menüs begleiten.
2. Einsatz 2 „Die Wachstube“ im Modus Missionsdesign planen und vorlegen.
3. Recherche zum Zeitkolorit für die offenen Fragen in `kanon.md`.
