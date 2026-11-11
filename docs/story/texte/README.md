# Spieltexte

Messingstadt hat kein Manuskript im Sinne eines Romans. Die Geschichte steckt in den Texten des Spiels. Dieser Ordner nimmt Entwürfe auf, bevor sie ins Spiel kommen, eine Datei je Einsatz (`einsatz_02.md` usw.), angelegt erst, wenn die Arbeit daran beginnt.

## Wo die Texte im Spiel liegen

| Text | Datei | Feld oder Stelle |
|---|---|---|
| Titel, Auftrag, Ort, Zeit | `data/mission_XX.json` | `title`, `briefing`, `place`, `time` |
| Schlagzeilen je Ausgang | `data/mission_XX.json` | `headlines` |
| Berufe auf der Akte | `data/mission_XX.json` | `role` je Agent |
| Kopfzeile und Ablauf der Sequenzen | `data/sequences/*.json` | `head`, `body` |
| Telegramme bei Niederlage und Abbruch | `src/Messingstadt.Core/Sequence.cs` | Rückfalltexte, je Mission überschreibbar (siehe Sequenzen-Session) |
| Allgemeine Schlagzeilen, Platzhalter | `src/Messingstadt.Core/Headlines.cs` | Rückfall, wenn die Mission nichts angibt |
| Gespräche und Rufe | `game/Chatter.cs` | Listen `GuardTalk`, `WorkerTalk`, Rufe |
| Telegrammstreifen im Einsatz, Ergebnisfenster | `game/Main.cs` | kurze feste Sätze |

Nach jeder Änderung an Missionsdateien oder Core-Texten `dotnet test tests/Messingstadt.Core.Tests` laufen lassen.

## Ablauf für einen Entwurf

1. Entwurf in `einsatz_XX.md`, gegliedert nach den Stellen oben.
2. Prüfliste aus dem Skill durchgehen, Ergebnis unter den Entwurf schreiben.
3. Freigabe einholen, dann ins Spiel übernehmen.
4. Im Entwurf vermerken, was übernommen wurde und wann.

## Redaktionsnotizen

- Maschinenpistole in `data/waffen.json`: Der Text nennt ein Trommelmagazin, das Magazin fasst aber 20 Schuss. Vorschlag: `Zwanzig Schuss im Stangenmagazin, über einen Hafenarbeiter beschafft. Streut, frisst Patronen und weckt das ganze Viertel.` Noch nicht übernommen, die Datei gehört der Ausrüstung. Die Figur trägt laut `docs/ausruestung.md` eine sichtbare Trommel, das müsste die Grafik angleichen.

- Der Rückzugstext (Schlagzeile und Telegramm) erscheint heute nie, siehe `../kanon.md`.
- Der Auftrag von Einsatz 1 ist länger als früher. Ob er auf das Papier im Einstieg passt, wurde im Spiel nicht geprüft.
