# Ausrüstung: Schnittstelle für die Waffenkammer

Regeln und Werte stehen in `docs/konzept.md` unter „Ausrüstung“. Hier steht, was ein Bildschirm zum Kaufen, Verkaufen und Verteilen braucht. Alles liegt in `Messingstadt.Core` und kommt ohne Godot aus.

## Katalog

`data/waffen.json`, geladen mit `Loader.ParseCatalog(json)`. Im Spiel liegt der Katalog fertig in `Main.Catalog`.

`ItemCatalog`
- `List<ItemDef> Items`: in der Reihenfolge der Datei, so kann sie die Waffenkammer auch zeigen
- `ItemDef? Get(string id)`
- `int SellPrice(string id)`: was ein Verkauf zurückbringt, `Campaign.SellPercent` (50) Prozent des Preises
- `static string[] StarterKit`: Grundausstattung, derzeit `pistole`

`ItemDef` (Zeiten in Sekunden, Reichweiten in Feldern, wie in der Datei)

| Feld | Bedeutung |
|---|---|
| `Id`, `Name`, `ShortName` | Kennung, voller Name, kurzer Name für die Akte im Einsatz |
| `Kind` | `ItemKind.Firearm`, `Melee`, `Lockpick`, `Smoke`, `Medkit`. In der Datei als `kind`: schusswaffe, nahkampf, dietrich, rauch, verband |
| `IsWeapon` | Schusswaffe oder Nahkampf, also mit E wählbar |
| `Price` | Kaufpreis |
| `Damage`, `Range`, `Cooldown` | Schaden je Treffer, Reichweite, Sekunden zwischen zwei Schüssen. `1 / Cooldown` ist die Feuerrate |
| `Noise` | Hörweite des Schusses in Feldern, 0 heißt lautlos |
| `Accuracy`, `Falloff` | Punkte auf die Trefferchance, Punkte weniger je Feld |
| `Magazine`, `Reload` | Schuss bis zum Nachladen (0 ohne), Sekunden zum Nachladen |
| `Charges` | Anwendungen bei Verbrauchsgut, danach ist der Gegenstand weg |
| `Heal`, `Duration`, `Radius` | Verband: Lebenspunkte und Sekunden zum Anlegen. Rauch: Wurfweite steht in `Range`, Halbmesser und Dauer der Wolke |
| `Text` | Aktentext für die Waffenkammer |

## Kampagne

`Campaign`
- `const int ItemSlots = 4`: Plätze je Agent
- `List<string> Stash`: die Kiste der Truppe, Kennungen, Doppeltes erlaubt
- `AgentRecord.Items`: Kennungen in der Reihenfolge der Plätze, höchstens `ItemSlots`
- `bool TryBuy(ItemCatalog, string itemId, string? agent = null)`: mit Agent in seinen freien Platz, sonst in die Kiste. Scheitert ohne Geld, bei voller Tasche, bei Toten und bei unbekannter Kennung
- `bool TrySell(ItemCatalog, string itemId, string? agent = null)`: aus den Plätzen des Agenten oder, ohne Agent, aus der Kiste
- `bool Equip(string agent, string itemId)`: Kiste zum Agenten
- `bool Unequip(string agent, int slot)`: Platz des Agenten in die Kiste

Nach jedem Kauf, Verkauf oder Tausch speichert die Anzeige den Stand wie bisher. Mit `Roster()` bekommt man die Agenten in Platzreihenfolge, `AgentRecord.Ready` sagt, wer mitgeht. Ausrüsten lassen sich auch Aussetzende, nur Tote nicht.

Folgen eines Einsatzes, alles in `Finish` und `Abandon`:
- Verbrauchtes (Rauchtopf, Verband) ist danach weg.
- Wer stirbt, verliert alles, was er trug. Die Kiste bleibt.
- Wer in die Krankenstube kommt, behält seine Sachen.

Ab Spielstand Version 3 gibt es die Ausrüstung. Ältere Stände bekommen beim Laden für jeden Lebenden eine Pistole, Tote nichts, die Kiste ist leer. Ein neuer Rekrut kommt mit Pistole.

## Im Einsatz

`MissionData.Catalog` setzt das Spiel vor dem Einsatz. `Campaign.Prepare` schreibt die Ausrüstung in `UnitData.Items`.

`Unit`
- `List<CarriedItem> Items`, je Gegenstand `Def`, `Ammo` (Schuss im Magazin), `Charges`
- `ActiveItem`, `Weapon` (Platz der Waffe in der Hand, -1 ohne)
- `ReloadTicks` (lädt nach, wenn größer 0), `BandageTicks` (legt den Verband an), `LastStrikeTick` (letzter Schlag)

`World`
- `bool CycleWeapon(int unitId)`, `bool SelectWeapon(int unitId, int slot)`: Wechsel kostet 0,5 s, angefangenes Nachladen bricht ab
- `bool OrderThrowSmoke(int unitId, int tileX, int tileY)`, `bool OrderUseMedkit(int unitId)`
- `List<SmokeCloud> Smokes`, `List<StrikeEvent> Strikes` für die Darstellung. `ShotEvent.Weapon` nennt die Waffe des Schützen
- `static List<string> ItemIds(Unit)`, `bool HasItem(Unit, ItemKind)`

Tasten: E nächste Waffe für alle Gewählten, G Rauchtopf auf das Feld unter der Maus, H Verband. Die Hilfezeile nennt sie nur, wenn jemand sie braucht.

## Haltungen und Bewegungen je Waffe

Bisher halten alle Figuren die Pistole. Was je Gegenstand sinnvoll wäre, nach Wichtigkeit:

| Gegenstand | Haltung und Bewegung |
|---|---|
| Schrotflinte | Mit beiden Händen an der Hüfte oder Schulter, Gehen mit gesenktem Lauf. Rückstoß reißt Oberkörper und Lauf deutlich hoch. Nachladen: Flinte abknicken, zwei Patronen einlegen, zuklappen |
| Maschinenpistole | Beidhändig vor dem Bauch, das gerade Stangenmagazin (zwanzig Schuss) ragt nach unten. Dauerfeuer als kurzes Zittern statt eines Rückstoßes je Schuss. Nachladen: Magazin nach unten herausziehen, neues aus der Tasche einsetzen |
| Totschläger | Locker am Arm hängend beim Gehen. Schlag von oben, kurz ausholen. Schleichhaltung: geduckt, Knüppel hinter dem Rücken |
| Pistole | Wie bisher. Dazu Nachladen: Magazin aus dem Griff, neues hinein |
| Rauchtopf | Wurf mit kurzem Ausholen, danach Rauch aus einer Dose am Boden |
| Verbandszeug | Kniend oder an die Wand gelehnt, 3 s Binde um den Arm wickeln |
| Dietrich | Kniend am Schloss, wie Otto heute, nur mit Hakensatz |
| Ohne Waffe | Hände leer, beim Angriffsbefehl nichts |

Für die Darstellung reichen dafür `ActiveItem.Def.Id`, `ReloadTicks`, `BandageTicks`, `LastFiredTick` und `LastStrikeTick`.
