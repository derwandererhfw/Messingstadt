# Dauertod im Spiel erklären

Stand 2026-11-06. Vom Nutzer freigegeben am 2026-11-06, übergeben an Kampagne und Menüs, noch nicht im Spiel. Entscheidung des Nutzers: Wer zweimal fällt, ist tot, auch bei einer Niederlage nur dann. Das Spiel erklärt es selbst.

Die Texte erklären die Regel in der Welt, nicht als Spielregel: Eine Ärztin flickt jeden einmal zusammen, ein zweites Mal nicht. Wer sie wo einbaut, entscheiden Kampagne und Menüs. Hier stehen nur die Texte.

## Die Ärztin

Ilse Marquardt, Ärztin ohne Zulassung, seit ihr die Kasse die Praxis gesperrt hat. Behandelt im Keller einer Wäscherei in der Altstadt. Kurz angebunden, nie grausam.

Satz, wenn ein Agent zum ersten Mal zurückkommt (in der Kampagne-Anzeige „Arzt“ oder im Ergebnisfenster):

> Einmal flick ich jeden zusammen. Ein zweites Mal hält das keiner aus.

Weitere Sätze zur Auswahl, falls die Anzeige wechseln soll:

> Die Narbe bleibt. Und beim nächsten Mal bleibst du.
> Geh langsam die ersten Wochen. Ich mein das ernst.

## Akte

| Zustand | Stempel | Zeile unter dem Beruf (höchstens 18 Zeichen) |
|---|---|---|
| Nie gefallen | keiner | wie bisher |
| Einmal gefallen, in der Krankenstube | KRANKENSTUBE | — |
| Einmal gefallen, wieder im Einsatz | keiner | `einmal geflickt` (15) |
| Zweimal gefallen | GEFALLEN | — |

„Einmal geflickt“ ist der Hinweis für den Spieler: Diese Figur hat keinen Versuch mehr.

## Ergebnisfenster nach dem Einsatz

Ersetzt den heutigen Satz „Verwundete kommen aus der Krankenstube zurück, aber Beine und Herz fallen auf Stufe 1.“ in `game/Main.cs`.

| Fall | Text |
|---|---|
| Erster Fall | `{Name} liegt in der Krankenstube. Beine und Herz fallen auf Stufe 1. Ein zweites Mal geht das nicht gut.` |
| Zweiter Fall | `{Name} ist tot.` |
| Allgemeiner Hinweis, einmal je Kampagne | `Wer fällt, kommt einmal aus der Krankenstube zurück. Wer zweimal fällt, bleibt liegen.` |

## Telegramme

Kolb nennt keine Namen. Die Akte zeigt, wen es getroffen hat.

Nach einem Einsatz mit einem Toten:

```
AN ABTEILUNG K STOP EINER VON EUCH IST NICHT MEHR AUFGEWACHT STOP BEGRABEN OHNE NAMEN STOP MACHT WEITER STOP K
```

Mit mehreren Toten:

```
AN ABTEILUNG K STOP ZU VIELE GRAEBER FUER EINE WOCHE STOP MACHT WEITER STOP K
```

Wenn Neue kommen:

```
AN ABTEILUNG K STOP NEUE LEUTE UNTERWEGS STOP SIE WISSEN WOFUER STOP BRINGT SIE ZURUECK STOP K
```

## Namen für Neue

Die Kampagne schickt Neue für freie Berufe. Namensvorrat, in dieser Reihenfolge vergeben:

| Name | Beruf | Herkunft in einem Satz |
|---|---|---|
| Karl Hoffmann | Schlosser | Hat zwanzig Jahre Kessel repariert, bis Brandt ihn aussperrte |
| Frieda Kranz | Buchhalterin | Führte die Kasse einer Konsumgenossenschaft |
| Willi Saß | Schlosser | Lehrling bei Otto, falls der noch lebt, sonst einfach Lehrling |
| Erwin Pohl | Buchhalter | Lohnschreiber im Kontor der Brandt-Werke, entlassen wegen Streikgeld |
| Lotte Becker | Kurierin | Zeitungsjunge gewesen, nur dass sie ein Mädchen ist |
| Max Kühn | Truppführer | Früher Gewerkschaftsordner bei Versammlungen |

Willis Satz wird nur verwendet, wenn Otto lebt, sonst nur „Lehrling“.

## Offen

- Ob die Ärztin eine Figur im Menü wird oder nur ein Satz bleibt, entscheiden Menüs und Kampagne.
