---
name: messingstadt-noir
description: Erzähl- und Bildstil im Noir-Look für Messingstadt (Dieselpunk 1927, kein Syndicate-Nachbau). Verwenden beim Planen, Schreiben oder Prüfen von Zwischensequenzen, Prologen, Panel-Sequenzen, Bildaufträgen und längeren Erzähltexten in Hardboiled-Prosa mit Schwarzweiß und Farbakzenten. Auch verwenden bei Fragen zu Perspektive, innerem Monolog, Dialog mit Subtext, Panelgrößen, Licht und Schatten, Farbdramaturgie und beim Szenenformat. Nicht für kurze Spieltexte wie Telegramme, Zeitung oder Gespräche der Wachen, dafür gilt der Skill messingstadt-story.
---

# Noir-Stil für Messingstadt

Verbindliche Referenz ist `docs/story/NOIR_STYLE_GUIDE.md`. Vor jeder Arbeit ganz lesen. Bei Widerspruch zwischen diesem Skill und dem Styleguide gilt der Styleguide.

## Verhältnis zu anderen Regeln

| Was | Wo | Vorrang |
|---|---|---|
| Was in der Welt gilt: Figuren, Mächte, Verlauf, Ende | `docs/story.md`, `docs/story/kanon.md` | immer vor diesem Skill |
| Was Figuren wann wissen | `docs/story/wissen.md` | immer vor diesem Skill |
| Arbeitsweise, Freigaben, Kennzeichen | Skill `messingstadt-story` | gilt auch hier |
| Wie erzählt und gezeigt wird | `docs/story/NOIR_STYLE_GUIDE.md` | dieser Skill |
| Technik der Zwischensequenzen | `data/sequences/*.json`, `game/SequencePlayer.cs` | Sequenzen-Session |

Dieser Skill ändert keine Story-Inhalte. Er entscheidet nicht, was passiert, sondern wie es erzählt wird.

## Ablauf

1. `docs/story.md`, `docs/story/kanon.md`, `docs/story/wissen.md` und `docs/story/NOIR_STYLE_GUIDE.md` lesen.
2. Offene Fragen im Styleguide (Abschnitt 9) prüfen. Hängt die Szene an einer offenen Frage, zuerst fragen. Eine Frage, mit Empfehlung.
3. Szenenplan vorlegen: Kapitel, Ort und Zeit, Perspektive, Wissensstand, neue Tatsache, Farbakzent mit Grund, Prosa oder Panels. Freigabe abwarten.
4. Szene im Format aus Abschnitt 6 des Styleguides schreiben, als Entwurf in `docs/story/texte/`.
5. Checkliste aus Abschnitt 8 durchgehen, Ergebnis unter die Szene schreiben.
6. Was ins Spiel soll, als Wunsch an die Sequenzen-Session über den Nutzer, nicht selbst in `data/sequences` eintragen.

## Kurzregeln

Die vollständigen Regeln stehen im Styleguide. Diese Liste ist nur zum Merken.

- Erzählstimme ist Martha Kolb aus dem Off, Ich-Form, Präteritum, in Erzählkästen. Keine allwissende Erzählung.
- Kolb verschweigt Rehm bis zum letzten Telegramm, leugnet aber nie.
- Innerer Monolog nur für Kolb, höchstens drei Sätze am Stück. Agenten zeigen wir von außen.
- Dialog: ein Satz, Subtext, nur „sagt“.
- Kurze Sätze, Verben vor Adjektiven, höchstens eine Metapher pro Szene.
- Höchstens eine neue gesicherte Tatsache pro Szene. Schnitt vor der Erklärung.
- Nur in Sequenzen: Schwarzweiß, eine Akzentfarbe pro Bild. Rot für Blut und Erinnerung, Blau für die Kontrolle der Mächtigen, Bernstein für Wärme und Zusammenhalt. Das Rot des Hammers erst, wenn er Blut kostet.
- Nach Gewalt und vor Entscheidungen eine Bildpause.
- Kolb ohne Gesicht, Brandt unsichtbar, Wachen menschlich, Hammer nicht verklärt.
- Kein Text setzt einen bestimmten lebenden Agenten voraus.
- Nichts aus Syndicate oder Sin City übernehmen: keine Figuren, Panels, Dialoge, Kompositionen.

## Kennzeichen

Wie im Skill `messingstadt-story`: KANON, IM SPIEL, HISTORISCH, ABLEITUNG, VORSCHLAG, OFFEN. Neue Szenen sind VORSCHLAG, bis der Nutzer sie freigibt.

## Abschluss

- Ehrlich sagen, was geprüft wurde und was nicht.
- Commit und Push nur nach Zustimmung des Nutzers.
- Für deutsche Texte gilt der Skill `vermenschlichen`, falls vorhanden.
