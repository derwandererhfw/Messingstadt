---
name: messingstadt-story
description: Arbeitsablauf für Story und Spieltexte von Messingstadt (isometrisches Taktikspiel, Dieselpunk 1927, kein Syndicate-Nachbau). Verwenden bei allem, was Welt, Fraktionen, die Agenten Greta, Otto, Emil und Hanna, den Verlauf der Kampagne oder Texte im Spiel betrifft, also Aufträge, Telegramme, Zeitung, Gespräche der Wachen und Akten. Auch verwenden bei Recherche zum Zeitkolorit 1927, bei Faktenprüfung, Figurenentwicklung, Weltbeschreibung, Kampagnen- und Missionsplanung, beim Schreiben oder Überarbeiten von Texten und bei der Kontinuitätsprüfung.
---

# Story-Arbeit an Messingstadt

Dieser Skill steuert die Story-Arbeit. Er ändert nichts an Spielregeln, Sequenz-Technik, Grafik oder Ton. Diese Bereiche haben eigene Sessions, Wünsche an sie gehen über den Nutzer.

## Wo was steht

| Datei | Inhalt |
|---|---|
| `docs/story.md` | Freigegebene Grundlage: Ausgangslage, Mächte, Trupp, Verlauf, Ende, Ton, Textsorten |
| `docs/story/steuerung.md` | Status, Entscheidungsprotokoll, offene Entscheidungen, nächste Schritte |
| `docs/story/kanon.md` | Was gilt, mit Kennzeichnung und Quelle. Widersprüche, offene Fragen, Kontinuitätsregeln |
| `docs/story/wissen.md` | Was der Trupp vor und nach jedem Einsatz weiß |
| `docs/story/texte/` | Entwürfe und Redaktionsnotizen zu Spieltexten |
| `docs/konzept.md` | Spielregeln. Lesen, nicht ändern, außer in Absprache |

Wo die Texte im Spiel liegen, steht in `docs/story/texte/README.md`.

## Verbindliche Regeln

1. Keine ganzen Einsätze, Szenen oder Textblöcke ungefragt schreiben.
2. Planung und Ausführung trennen: erst Plan vorlegen, nach Freigabe ausführen.
3. Bei wichtigen kreativen Entscheidungen gezielt fragen, eine Frage nach der anderen, mit Empfehlung.
4. Grundlegende Entscheidungen (Figuren, Wendepunkte, Ende, Ton) erst nach Freigabe des Nutzers umsetzen.
5. Freigaben mit Datum in `docs/story/steuerung.md` eintragen.
6. Freigegebenes nie stillschweigend ändern. Bei Bedarf Änderung vorschlagen und begründen.
7. Vor jedem neuen Einsatz `docs/story/kanon.md`, `docs/story/wissen.md` und den Abschnitt in `docs/story.md` lesen.
8. Informationslage beachten: Figuren wissen nur, was laut `docs/story/wissen.md` bis dahin bekannt ist. Das gilt auch für Wachen, Arbeiter und die Zeitung.
9. Figuren, Zeitlinie, Technik der Zeit und Ereignisse gegen die Prüfliste halten (`references/pruefliste.md`).
10. Konflikte mit Spielregeln, Daten oder anderen Bereichen melden, nicht selbst auflösen.
11. Bei Problemen Lösungen vorschlagen, statt Festgelegtes zu überschreiben.
12. Jede Aussage kennzeichnen: gesichert, offen oder erfunden (Kennzeichen unten).

Selbstständig erlaubt: Tippfehler, Sätze mit Spielhinweis an geänderte Karten anpassen, Texte gegen Kanon prüfen, Dokumentation nachziehen. Commit und Push nur nach Zustimmung.

## Kennzeichen

| Kennzeichen | Bedeutung | Quelle angeben |
|---|---|---|
| KANON | Vom Nutzer freigegeben | Datei und Abschnitt, bei Entscheidungen Datum |
| IM SPIEL | So in Daten oder Code umgesetzt | Datei, möglichst mit Zeile |
| HISTORISCH | Reale Tatsache der Zeit um 1927, belegt | Titel, Autor oder Herausgeber, Jahr, Fundort |
| ABLEITUNG | Folgt plausibel aus Gesichertem | Woraus sie folgt |
| VORSCHLAG | Eigene Idee, nicht freigegeben | wer, wann |
| OFFEN | Ungeklärt | was zur Klärung fehlt |

Nichts als HISTORISCH eintragen, was nicht belegt ist. Wikis und Foren sind Hinweise, keine Belege. Widersprüche dokumentieren, nicht durch Vermutung glätten.

## Syndicate

Messingstadt ist ausdrücklich kein Syndicate-Nachbau. Syndicate wird nicht recherchiert und nicht als Vorlage benutzt. Taucht etwas auf, das nach Syndicate klingt (Konzernagenten, Implantate, Überzeugen von Menschenmengen, Eroberung einer Weltkarte), darauf hinweisen und eine eigene Lösung vorschlagen.

## Arbeitsmodi

Jeder Modus ist in `references/modi.md` beschrieben: Recherche, Faktenprüfung, Figurenentwicklung, Weltbeschreibung, Kampagnenplanung, Missionsdesign, Texte schreiben, Dialogüberarbeitung, Kontinuitätsprüfung, Redaktion. Den passenden Modus nennen, bevor die Arbeit beginnt.

## Abschluss einer Arbeit

- `docs/story/steuerung.md` und betroffene Dateien nachziehen.
- Bei geänderten Spieltexten `dotnet test tests/Messingstadt.Core.Tests` laufen lassen.
- Ehrlich sagen, was nicht im Spiel geprüft wurde.
- Für deutsche Texte gilt der Skill `vermenschlichen`, falls vorhanden.
