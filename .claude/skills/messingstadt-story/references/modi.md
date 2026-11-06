# Arbeitsmodi

Jeder Modus hat Auslöser, Ablauf und Ergebnis. Was freigegeben werden muss, ist markiert.

## Recherche

Für Zeitkolorit um 1927: Wörter, Berufe, Löhne, Technik, Arbeitskämpfe, Alltag. Nicht für Syndicate.

1. Frage genau aufschreiben und in `docs/story/kanon.md` unter „Offene Fragen“ eintragen.
2. Quellen in dieser Reihenfolge: Primärquellen der Zeit, Fachliteratur, Museen und Archive, seriöse Nachschlagewerke. Wikis nur als Hinweis.
3. Ergebnis als HISTORISCH mit Quelle eintragen, sonst OFFEN lassen.
4. Kurz zusammenfassen, keine langen Zitate.

Vor dem Herunterladen von Dateien Name und Quelle nennen und fragen.

## Faktenprüfung

1. Aussage in einen Text- oder Planentwurf zerlegen.
2. Jede Aussage gegen `docs/story/kanon.md`, `docs/story.md`, `docs/konzept.md` und die Spieldaten halten.
3. Ergebnis je Aussage: stimmt (mit Quelle), widerspricht (mit Fundstelle), offen.
4. Widersprüche in `docs/story/kanon.md` eintragen und dem Nutzer melden.

## Figurenentwicklung

1. Bestehendes aus `docs/story.md` und `docs/story/kanon.md` sammeln.
2. Lücken als Fragen formulieren, mit Empfehlung.
3. Nach Freigabe in `docs/story.md` ergänzen, Entscheidung in `docs/story/steuerung.md`.

Grenzen: Startwerte (Beine, Herz, Fähigkeit) gehören zur Mechanik. Kein Text darf voraussetzen, dass ein bestimmter Agent lebt oder dabei ist.

## Weltbeschreibung

Mächte, Orte, Alltag. Neue Orte nur mit Bezug auf einen geplanten Einsatz. Ortsnamen auf Karten und Schildern stimmen mit `tools/art/generate_concept.py` ab (Grafik-Session).

## Kampagnenplanung

1. Verlauf aus `docs/story.md` und Wissensstand aus `docs/story/wissen.md` lesen.
2. Änderungen als Vorschlag mit Folgen für spätere Einsätze vorlegen.
3. Freigabe abwarten. Danach `docs/story.md`, `docs/story/wissen.md`, `docs/story/steuerung.md` nachziehen.
4. Anforderungen an Kampagne, Level, Sequenzen als Liste für den Nutzer.

## Missionsdesign

Story-Seite eines Einsatzes: Auftrag, Ziel in der Geschichte, wer was erfährt, Schlagzeilen, Ort und Zeit. Karte, Gegner und Regeln baut die Level-Session.

1. Prüfliste durchgehen.
2. Plan mit Auftragstext, Zielen, Schlagzeilen je Ausgang und Wissenszuwachs vorlegen.
3. Nach Freigabe in die Missionsdatei eintragen (Felder `briefing`, `place`, `time`, `headlines`).

## Texte schreiben

Entwurf zuerst in `docs/story/texte/`, nicht direkt ins Spiel. Längen: Auftrag drei bis vier Sätze, Zeitungszeile ein bis zwei Sätze, Gesprächssatz höchstens einer, Akte höchstens 18 Zeichen. Nach Freigabe übernehmen.

## Dialogüberarbeitung

Sätze in `game/Chatter.cs` prüfen: passt der Ton, stimmt der Spielhinweis zur Karte, wiederholt sich etwas, verrät der Satz mehr, als die Figur wissen kann. Sätze mit Spielhinweis bleiben mit Kommentar `// Stimmt: ...` markiert.

## Kontinuitätsprüfung

Alle Texte eines Einsatzes oder der ganzen Kampagne gegen `references/pruefliste.md` halten. Ergebnis als Liste mit Fundstelle und Vorschlag. Nichts selbst ändern, was freigegeben ist.

## Redaktion

Sprache, Länge, Einheitlichkeit. Umlaute in Telegrammen ausschreiben (UE, OE, AE), sonst normale Rechtschreibung. Typografische Anführungszeichen „so“. Änderungen als Liste vorher-nachher vorlegen, bei freigegebenen Texten nur nach Zustimmung übernehmen.
