# Noir-Styleguide für Messingstadt

Verbindliche Erzähl- und Bildregeln für Zwischensequenzen und längere Erzähltexte. Gilt für Prosa, Panel-Sequenzen und Bildaufträge. Was in der Welt gilt, steht weiter in `../story.md` und `kanon.md`. Dieser Styleguide regelt nur, **wie** erzählt wird, nicht **was**.

Stand 2026-11-06. Vom Nutzer freigegeben am 2026-11-06 (KANON für die Regeln dieses Styleguides). Die offenen Fragen in Abschnitt 9 sind damit nicht entschieden, Empfehlungen dort bleiben VORSCHLAG. Kennzeichen wie im Skill `messingstadt-story`: KANON, IM SPIEL, ABLEITUNG, VORSCHLAG, OFFEN.

## 1. Grundlage

Messingstadt ist Dieselpunk 1927 und ausdrücklich kein Syndicate-Nachbau (KANON, `../story.md`, Ton). Die Erzählweise leiht sich Techniken, keine Inhalte:

| Quelle | Was wir übernehmen | Was nicht |
|---|---|---|
| Film Noir der 1940er | Hartes Licht, Schatten als Handlung, Erzählstimme aus dem Off | Femme fatale als Schablone, Detektivbüro |
| Hardboiled-Prosa | Kurze Sätze, Verben statt Adjektive, Dialog mit Subtext | Zynismus als Pose, Sprüche |
| Neo-Noir | Moralische Grauzonen, keine saubere Auflösung | Gewalt als Schauwert |
| Graphic Novels | Panelrhythmus, Bildpausen, Großaufnahmen als Satzzeichen | Splash-Seiten ohne Erzählzweck |
| Schwarzweiß mit Farbakzent | Eine Farbe trägt eine Bedeutung | Konkrete Figuren, Panels oder Kompositionen aus Sin City oder anderen Werken |

Sin City dient nur als Beispiel für abstrakte Techniken: Silhouette vor hellem Grund, Farbe als einziger Akzent, Regen als Grafik. Keine Figur, kein Bild, kein Dialog wird nachgebaut.

## 2. Literarisches Regelwerk

### 2.1 Perspektive

- Jede Szene hat genau eine Perspektive. Wechsel nur an einem Szenen- oder Panelbruch, nie mitten im Absatz.
- Erlaubt sind Ich-Erzählung und personale Er/Sie-Perspektive. Kein allwissender Erzähler.
- Die Perspektivfigur weiß nur, was sie laut `wissen.md` bis zu diesem Zeitpunkt wissen kann. Die Erzählung darf dem Spieler nichts verraten, was die Figur nicht sieht. Dramatische Ironie entsteht nur über das Bild (siehe 3.6), nie über den Erzähltext.
- Wer erzählt, ist eine offene Entscheidung (siehe 9, Frage 1). Bis dahin gilt: personale Perspektive einer Figur, die in der Szene sicher dabei ist.
- Kein Text setzt voraus, dass ein bestimmter Agent lebt oder dabei ist (KANON, `kanon.md`, Kontinuitätsregeln). Eine Szene mit Perspektive eines Agenten braucht eine Ersatzfassung oder steht vor dem ersten Einsatz.

### 2.2 Innerer Monolog

- Höchstens drei Sätze am Stück. Danach ein Bild, eine Handlung oder ein Dialogsatz.
- Der Monolog beobachtet und urteilt knapp. Er erklärt keine Welt, keine Vorgeschichte, keine Gefühle beim Namen.
- Gedacht wird in der Sprache der Figur: Greta denkt in Dienstplänen und Rundgängen, Otto in Werkzeug und Schicht, Emil in Zahlen und Zeilen, Hanna in Wegen und Wut (ABLEITUNG aus `../story.md`, Der Trupp).
- Im Satz steht Präsens, in der Rückblende Präteritum.

### 2.3 Dialog

- Ein Satz pro Redebeitrag, selten zwei. Niemand hält Reden (KANON, `../story.md`, Ton).
- Jeder Dialog hat Subtext: Was gesagt wird, ist nie ganz das, worum es geht. Wer droht, redet über das Wetter. Wer Angst hat, redet über Lohn.
- Inquit-Formeln nur „sagt“ oder gar keine. Kein „zischt“, „knurrt“, „haucht“.
- Keine Philosophie, keine Sentenzen. Ein trockener Satz ist erlaubt, ein Witz nicht (KANON, Ton).
- Wörter der Zeit, nichts nach 1927 (KANON, Ton). Kein „okay“, keine Funkgeräte.

### 2.4 Satzrhythmus und Wortwahl

- Grundtakt kurz: Subjekt, Verb, Objekt. Ein langer Satz nur, wenn er Spannung trägt, und danach ein sehr kurzer.
- Verben vor Adjektiven. Höchstens ein Adjektiv pro Substantiv.
- Metaphern sparsam: höchstens eine pro Szene, und sie kommt aus der Welt der Figur (Kessel, Lohntüte, Akte), nie aus der Literatur.
- Konkret statt allgemein: „drei Groschen“ statt „wenig Geld“, „Kurt, der Laufbursche“ statt „ein Junge“ (Kurt: IM SPIEL, `game/Chatter.cs`).
- Kein Pathos über die Gewerkschaft (KANON, Ton).

### 2.5 Spannung

- Informationsdosierung: Pro Szene wird höchstens eine neue Tatsache gesichert. Alles andere bleibt Andeutung.
- Der Leser erfährt Dinge gleichzeitig mit der Perspektivfigur, nie vorher durch den Text.
- Schnitt vor der Auflösung. Eine Szene endet auf einer Handlung oder einem Bild, nicht auf einer Erklärung.
- Gewalt kostet etwas (KANON, Ton). Sie wird kurz gezeigt und lange nachgewirkt.

### 2.6 Fragmentierung und Rückblende

- Fragmente sind erlaubt, wenn jedes Fragment für sich ein klares Bild oder eine klare Handlung hat.
- Rückblenden sind kurz, höchstens eine pro Szene, und immer an ein Ding im Jetzt gebunden (ein Geruch, eine Narbe, ein Stempel).
- Rückblenden stehen im Präteritum und sind im Bild farblich markiert (siehe 4, Rot).
- Rückblenden zeigen nur, was die Figur erlebt hat. Sie verraten keine späteren Wendungen.

### 2.7 Unzuverlässige Wahrnehmung

- Erlaubt: Die Figur irrt sich, übersieht etwas oder will etwas nicht sehen. Der Text lügt nicht, die Figur schon.
- Der Irrtum muss später auflösbar sein und darf keiner freigegebenen Tatsache widersprechen.
- Beispiel aus dem Kanon: Der Trupp hält Brandts Sturheit für den Grund des endlosen Streiks (KANON, `wissen.md`, vor Einsatz 1). Erzähltexte vor Einsatz 4 dürfen diese Sicht ungebrochen zeigen.

### 2.8 Moralische Grauzonen

- Keine Seite ist sauber. Der Hammer kämpft für das Richtige und ist trotzdem käuflich (KANON, Ton).
- Wachen sind arme Leute, keine Monster (KANON, Kontinuitätsregeln). Jede Wache, die näher gezeigt wird, bekommt einen menschlichen Zug.
- Der Trupp entscheidet unter Druck und zahlt dafür. Keine Szene belohnt Gewalt ohne Preis.

## 3. Visuelles Regelwerk

### 3.1 Grundästhetik

- Schwarzweiß als Grundbild. Grautöne sind erlaubt, aber das Bild lebt von Schwarz und Weiß.
- Extreme Hell-Dunkel-Kontraste: Ein Bild hat eine Lichtquelle, die man versteht (Laterne, Fenster, Scheinwerfer, Feuer).
- Regen, Dampf und Rauch sind grafische Elemente: weiße Linien und Flächen auf Schwarz.

### 3.2 Licht und Schatten

- Licht erzählt, wer Macht hat. Wer im Licht steht, ist sichtbar und angreifbar. Wer im Schatten steht, handelt.
- Gegenlicht macht Figuren zu Silhouetten. Das ist der Normalfall für den Trupp in braunen Mänteln.
- Streifenlicht (Jalousie, Gitter, Zaun) zeigt Gefangenschaft oder Entscheidung.

### 3.3 Silhouette

- Jede Figur muss als Umriss erkennbar sein: Hut, Mütze, Kragen, Haltung.
- Gesichter zeigen wir selten und dann nah. Martha Kolb zeigen wir nie im Gesicht (KANON, `../story.md`, Der Trupp).
- Brandt erscheint nie selbst, nur über Werkschutz, Luftschiff und Geld (KANON, Kontinuitätsregeln).

### 3.4 Perspektive und Einstellung

| Einstellung | Wirkung | Einsatz |
|---|---|---|
| Totale von oben | Die Stadt ist größer als jeder Mensch | Eröffnung, Übergang, Ohnmacht |
| Untersicht | Bedrohung, Macht | Werkschutz, Luftschiff, Fabrik |
| Halbnah auf Augenhöhe | Gespräch unter Gleichen | Trupp untereinander, Arbeiter |
| Großaufnahme | Satzzeichen, Entscheidung | Hände, Geld, Stempel, Waffe, Augen |
| Schräge Kamera | Etwas stimmt nicht | Verrat, Verwundung, Panik |

### 3.5 Panelgrößen

- Breites Panel über die ganze Seite: Ort, Zeit, Atmosphäre.
- Hohes, schmales Panel: Fall, Flucht, Treppenhaus, Isolation.
- Kleine Panels in Folge: Zeit dehnt sich, eine Handlung in Schritten.
- Einschub (Panel im Panel): Detail, das die Figur in diesem Moment bemerkt.
- Höchstens eine große Seite ohne Text pro Sequenz.

### 3.6 Symbolik

Wiederkehrende Dinge mit fester Bedeutung (VORSCHLAG):

| Ding | Bedeutung |
|---|---|
| Umschlag | Geld, das die Hand wechselt, ohne dass jemand fragt woher |
| Luftschiff | Brandts Blick über der Stadt |
| Feuerfass | Was die Streikenden zusammenhält |
| Stempel und Akte | Abteilung K, Ordnung im Verborgenen |
| Regen | Deckung für den Trupp, Kälte für alle |
| Uhr, Uhrzeit | Druck, der Morgen kommt |

Dramatische Ironie geht nur über diese Bilder. Beispiel: Ein Umschlag wechselt ohne Gesichter die Hand. Der Spieler ahnt etwas, der Text sagt nichts.

### 3.7 Bildpause

- Eine Bildpause ist ein Panel oder eine Einstellung ohne Text und ohne Handlung: Regen auf Pflaster, eine leere Gasse, ein Telegramm auf dem Tisch.
- Nach jeder Gewalt und vor jeder Entscheidung steht eine Bildpause.
- Im Spiel ist eine Bildpause ein Abschnitt ohne Text von 1,5 bis 3 Sekunden.

### 3.8 Prosa oder Panel

| Schreibe als Prosa, wenn | Schreibe als Panel-Sequenz, wenn |
|---|---|
| Die Szene von innerem Erleben lebt | Die Szene von Bewegung und Raum lebt |
| Zeit gerafft wird (Tage, Wege, Warten) | Zeit gedehnt wird (Sekunden vor einem Schuss) |
| Erinnerung und Gedanke im Vordergrund stehen | Ein Bild mehr sagt als ein Satz (Umschlag, Blut, Stempel) |
| Die Figur etwas begreift | Mehrere Figuren gleichzeitig handeln |
| Der Text ohne Bild auskommt (Roman, Akte, Brief) | Die Szene als Zwischensequenz ins Spiel geht |

Gemischte Szenen sind erlaubt. Dann steht die Prosa zwischen den Panels und nie im Panel.

## 4. Farbdramaturgie

Schwarzweiß ist die Regel. Farbe ist die Ausnahme und hat immer einen Grund. Pro Bild höchstens eine Akzentfarbe. Zwei Farben in einem Bild nur, wenn genau dieser Zusammenstoß die Aussage ist.

| Farbe | Bedeutung | Beispiele in Messingstadt | Status |
|---|---|---|---|
| Schwarz | Macht, Angst, Geheimnisse | Werkschutz in der Nacht, Brandts Geld, die Wache im Dunkeln | VORSCHLAG nach Vorgabe des Nutzers |
| Weiß | Erkenntnis, Wahrheit, Isolation | Papier des Kontobuchs, Licht in der Zelle, die Seite in der Druckerei | VORSCHLAG |
| Rot | Gewalt, Blut, persönliche Erinnerung | Blut auf dem Pflaster, Rückblenden, Hannas Erinnerung an ihren Bruder Paul | VORSCHLAG |
| Blau | Kontrolle, Überwachung, Technik der Mächtigen | Suchscheinwerfer des Luftschiffs, Bogenlampen der Brandt-Werke, Bildfunk, Kontorbank-Tinte | VORSCHLAG, angepasst an 1927 |
| Bernstein | Wärme, Zusammenhalt, Zuhause | Feuerfass der Streikposten, Küchenfenster, Glut einer geteilten Zigarette | VORSCHLAG, Erweiterung |

Regeln:

- Rot ist selten. Rot erscheint nur, wenn Blut fließt oder eine Figur sich an einen Verlust erinnert. Rückblenden haben einen roten Stich.
- Das Rot im Namen des Roten Hammers ist kein Freibrief: Fahne und Stempel des Hammers bleiben schwarzweiß, bis die Gewerkschaft Blut kostet. Dann dürfen sie rot werden. So zeigt die Farbe, dass auch der Hammer an Gewalt hängt (VORSCHLAG).
- Blau gehört den Mächtigen. Der Trupp hat nie blaues Licht auf sich, außer wenn er gesehen wird.
- Bernstein ist das einzige warme Licht, das den Leuten selbst gehört. Wenn es erlischt, ist etwas verloren.
- Die braunen Mäntel des Trupps (KANON, Konzept) bleiben ein gedämpftes Braun, keine Akzentfarbe. Sie heben den Trupp ab, ohne eine Aussage zu tragen.
- Weiß ist Licht und Papier, nicht Farbe. Weiße Flächen in schwarzer Umgebung isolieren eine Figur.

## 5. Verbindung zur Welt von Messingstadt

Der Prompt des Nutzers nennt Themen aus der Syndicate-Welt. Messingstadt übernimmt keine Syndicate-Lore (KANON, Skill `messingstadt-story`, Abschnitt Syndicate). Die Themen werden in die eigene Welt übersetzt:

| Thema im Prompt | Entsprechung in Messingstadt | Kennzeichen |
|---|---|---|
| Megakonzerne und Konzernherrschaft | Brandt-Werke und Kontorbank. Brandt will den Notstand, die Bank kauft die Schulden der Mietshäuser | KANON, `../story.md`, Ausgangslage |
| Agenten | Vier Leute der Abteilung K, Arbeiter und Entlassene, keine Profis | KANON, Der Trupp |
| Kybernetische Modifikationen | Keine. An ihre Stelle treten Narben und Krankenstube. Wer zweimal fällt, stirbt (KANON, `texte/dauertod.md`). Den Ausbau von Beinen und Herz erzählen wir als Training, nicht als Technik (ABLEITUNG aus `../konzept.md`) | KANON und ABLEITUNG |
| Gedankenkontrolle | Keine Technik dafür. An ihre Stelle treten Propaganda des Anzeigers, Lohntüte, Schulden, Angst vor dem Notstand | ABLEITUNG aus `../story.md` |
| Gesellschaftliche Ungleichheit | Sechzehn-Stunden-Schichten, Mieten, hungernde Familien, Wachleute als Schwäger der Streikenden | KANON, Mächte |
| Gewalt als wirtschaftliches Instrument | Brandt braucht einen blutigen Streik für den Notstand. Tote Wachen kosten Rückhalt in den Vierteln | KANON, Ausgangslage und Ende |
| Technik ohne moralischen Fortschritt | Luftschiff, Bildfunk, Fließband, Bogenlampen. Die Technik dient den Mächtigen | ABLEITUNG |

Was nach Syndicate klingt (Konzernagenten, Implantate, Menschen umprogrammieren, Weltkarte), wird gemeldet und durch eine eigene Lösung ersetzt.

## 6. Szenenformat

Jede Szene folgt diesem Gerüst. Felder in eckigen Klammern sind optional. Das Format passt für Roman, Comic und Zwischensequenz.

```markdown
## KAPITEL: <Titel>

**Ort, Zeit:** <Ort>. <Wochentag, Datum 1927, Uhrzeit>
**Perspektive:** <Figur>, <Ich | personal>
**Wissensstand:** vor/nach Einsatz <n> (laut wissen.md)
**Farbe:** <Grundton>, Akzent <Farbe> für <Grund>

### Szene
<Ein bis drei Sätze: Was ist der Raum, was ist die Lage. Für Bild und Text.>

### Prosa
<Erzähltext in der gewählten Perspektive.>

> *Innerer Monolog, kursiv, höchstens drei Sätze.*

**<Name>:** „Dialogsatz.“

### [Panels]
| Nr. | Größe | Einstellung | Bild | Text | Farbe |
|---|---|---|---|---|---|
| 1 | breit | Totale von oben | <Was man sieht, Licht, Silhouetten> | <Erzählkasten oder leer> | <Akzent oder keiner> |

### [Bildpause]
<Was zu sehen ist, ohne Text.>

### Prüfung
<Ergebnis der Checkliste aus Abschnitt 8, kurz.>
```

Regeln für die Felder:

- **Ort, Zeit** stehen wie in den Missionsdaten (`place`, `time`, IM SPIEL `data/mission_01.json`).
- **Bild** beschreibt Licht, Lichtquelle, Silhouetten und Blickrichtung, nicht Gefühle.
- **Text** im Panel ist Erzählkasten, Sprechblase oder Geräusch. Höchstens 20 Wörter pro Panel.
- **Farbe** nennt nur den Akzent und seinen Grund aus Abschnitt 4.

### 6.1 Umsetzung im Spiel

Zwischensequenzen liegen als Daten in `data/sequences/*.json` (IM SPIEL, Sequenzen-Session). Ein Panel entspricht einem Abschnitt (`beat`): Einstellung wird zur Kamerafahrt (`camera`), Bildpause zu einem Abschnitt ohne `show`, Erzählkasten zu `card` oder `paper`, Telegramm zu `telegram`. Schwarzweiß, Farbakzente und Comic-Panels kann der Player heute noch nicht. Das ist eine eigene Etappe der Sequenzen-Session (OFFEN, siehe 9).

## 7. Arbeitsablauf

1. `../story.md`, `kanon.md`, `wissen.md` und diesen Styleguide lesen.
2. Szenenplan vorlegen: Kapitel, Ort, Zeit, Perspektive, Wissensstand, Farbe, neue Tatsache. Freigabe abwarten (KANON, Skill `messingstadt-story`, Regeln 1 und 2).
3. Szene im Format aus Abschnitt 6 schreiben, als Entwurf in `texte/`.
4. Checkliste aus Abschnitt 8 durchgehen und das Ergebnis unter die Szene schreiben.
5. Für Spieltexte zusätzlich die Prüfliste des Skills `messingstadt-story` (`references/pruefliste.md`).

## 8. Checkliste

Jede Frage mit Ja beantworten können, sonst überarbeiten.

**Perspektive**
- [ ] Hat die Szene genau eine Perspektive, und bleibt sie bis zum Schnitt?
- [ ] Weiß die Perspektivfigur nur, was sie laut `wissen.md` wissen kann?
- [ ] Setzt die Szene keinen bestimmten lebenden Agenten voraus, oder gibt es eine Ersatzfassung?

**Innerer Monolog**
- [ ] Höchstens drei Sätze am Stück?
- [ ] Beobachtet er, statt zu erklären?
- [ ] Klingt er nach der Figur (Beruf, Herkunft)?

**Dialog**
- [ ] Hat jeder Wortwechsel Subtext?
- [ ] Ein Satz pro Beitrag, keine Rede, keine Sentenz?
- [ ] Nur „sagt“ oder keine Inquit-Formel?
- [ ] Nichts, was nach 1927 klingt?

**Bild**
- [ ] Hat jedes Panel eine erkennbare Lichtquelle?
- [ ] Ist die Komposition nachvollziehbar: Wer steht wo, wohin geht der Blick?
- [ ] Passt die Panelgröße zur Funktion (3.5)?
- [ ] Steht nach Gewalt und vor einer Entscheidung eine Bildpause?

**Farbe**
- [ ] Höchstens eine Akzentfarbe pro Bild?
- [ ] Hat jeder Farbakzent eine Funktion aus Abschnitt 4?
- [ ] Ist Rot an Blut, Gewalt oder Erinnerung gebunden?

**Spannung**
- [ ] Höchstens eine neue gesicherte Tatsache?
- [ ] Endet die Szene auf Handlung oder Bild statt auf Erklärung?
- [ ] Ist die Szene frei von unnötigen Erklärungen und Vorgeschichte?

**Welt**
- [ ] Passt alles zu `../story.md` und `kanon.md`?
- [ ] Wachen menschlich, Hammer nicht verklärt, Brandt unsichtbar, Kolb ohne Gesicht?
- [ ] Nichts, was nach Syndicate klingt?

## 9. Offene Fragen

| Nr. | Frage | Wer entscheidet | Empfehlung |
|---|---|---|---|
| 1 | Wer erzählt die Zwischensequenzen? Kolb aus dem Off, wechselnde Agenten oder personale Perspektive ohne Ich | Nutzer mit Story | Kolb als Stimme aus dem Off: Sie überlebt jeden Ausgang, man sieht sie nie, und sie weiß mehr, als sie sagt. Das ergibt eine unzuverlässige Erzählerin, ohne den Kanon zu brechen. Achtung: Sie wusste von Rehm (KANON, Ende). Ihre Erzählung darf das bis zum letzten Telegramm nur verschweigen, nie leugnen. Ihre Sätze passen zu `wissen.md`: Sie weiß mehr als der Trupp, verrät davon vor Einsatz 4 aber nichts (Hinweis Story, 2026-11-06) |
| 2 | Gilt die Farbdramaturgie auch für die Spielgrafik im Einsatz oder nur für Sequenzen | Nutzer mit Grafik und Atmosphäre | Nur für Sequenzen. Die Spielgrafik bleibt farbig und warm |
| 3 | Wird Rot für Fahne und Stempel des Roten Hammers zurückgehalten (Abschnitt 4) | Nutzer mit Story | Ja. Heute stehen im Spiel rote Stempel auf Auftrag und Akte (IM SPIEL, `game/PaperUi.cs`). Für Sequenzen schwarzweiß, im Menü bleibt es, wie es ist |
| 4 | Wie entstehen Bilder für Panel-Sequenzen | Nutzer mit Grafik | OFFEN. Higgsfield hat kein Guthaben, ein Blender-Weg wurde am 2026-11-06 verworfen |
| 5 | Kommt eine Prosa-Fassung (Roman, Begleitheft) überhaupt vor oder nur Zwischensequenzen | Nutzer | OFFEN |
