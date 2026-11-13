using System;
using System.Collections.Generic;
using System.Linq;

namespace Messingstadt.Core
{
    public sealed partial class Unit
    {
        /// <summary>Gefangener, der noch in der Zelle sitzt. Wachen beachten ihn nicht, er rührt sich nicht.</summary>
        public bool Captive;

        /// <summary>Tür seiner Zelle, -1 ohne Zelle.</summary>
        public int CellDoor = -1;

        /// <summary>Takte, die ein Agent schon an der offenen Zelle steht.</summary>
        public int FreeProgress;

        /// <summary>Wem ein Befreiter folgt, -1 wenn niemandem.</summary>
        public int FollowId = -1;

        /// <summary>Der Befreite wartet, wo er steht, bis ihn wieder jemand mitnimmt.</summary>
        public bool Waiting;

        /// <summary>Türen, zu denen die Einheit den Schlüssel trägt.</summary>
        public readonly List<int> Keys = new();

        public bool IsPrisoner => Faction == Faction.Prisoner;
    }

    /// <summary>Ein Schlüsselbund am Boden, etwa neben dem niedergeschlagenen Schließer.</summary>
    public sealed class DroppedKeys
    {
        public int X, Y;
        public List<int> Doors = new();
    }

    public sealed partial class World
    {
        public const int FreeTicks = 40;
        // Wer vor der Tür einer zwei Felder tiefen Zelle steht, erreicht den Gefangenen am hinteren Ende.
        public const int FreeRange = 22 * TileUnits / 10;
        public const int PrisonerSpeed = 100;
        public const int KeyDoorStep = 4;
        private const int FollowStartRange = 3 * TileUnits / 2;
        private const int FollowStopRange = 11 * TileUnits / 10;
        private const int AdoptRange = 3 * TileUnits;
        private const int FollowRepathTicks = 8;
        private const int BoardRange = 3 * TileUnits;

        /// <summary>True in einem Einsatz der Art Befreien: Es gibt Gefangene statt eines Gegenstands.</summary>
        public bool IsRescue { get; }

        public List<DroppedKeys> KeysOnGround { get; } = new();

        /// <summary>So nah muss jemand vom Trupp einem Bewohner kommen, damit er ruft, mit freier Sicht.</summary>
        public const int ResidentSeesRange = 5 * TileUnits / 2;

        /// <summary>So weit hört eine Wache den Ruf eines Bewohners.</summary>
        public const int ResidentHeardRange = 9 * TileUnits;

        /// <summary>Haltung der Stadt in diesem Einsatz, aus den Missionsdaten.</summary>
        public CityStance Stance { get; }

        /// <summary>
        /// Rufe der Bewohner: wann, wer und welche Wache nachsehen geht (-1, wenn keine in Hörweite war). Für Darstellung und Ton.
        /// Steht die Stadt gegen den Trupp, gehen zwei Wachen, dann stehen zwei Einträge mit demselben Takt in der Liste.
        /// </summary>
        public List<(int Tick, string Resident, int GuardId)> ResidentCalls { get; } = new();

        private readonly List<ResidentData> _residents = new();

        /// <summary>
        /// Ein Bewohner, der nicht hilft, ruft einmal, wenn ein Agent oder Befreiter nah an ihm vorbeikommt. Hilft die Stadt, ruft niemand.
        /// Schweigt sie, geht die nächste ahnungslose Wache in Hörweite an die Stelle und sieht nach, steht sie gegen den Trupp, gehen zwei.
        /// Der Einsatz gilt damit als bemerkt, nicht als laut.
        /// </summary>
        private void ResidentsWatch()
        {
            for (int i = _residents.Count - 1; i >= 0; i--)
            {
                var r = _residents[i];
                int rx = ToUnits(r.X), ry = ToUnits(r.Y);
                var seen = Units.Find(u => u.Alive && (u.Faction == Faction.Agent || (u.IsPrisoner && !u.Captive))
                    && DistanceTo(u, rx, ry) <= ResidentSeesRange && HasSight(rx / TileUnits, ry / TileUnits, u.TileX, u.TileY));
                if (seen == null)
                {
                    continue;
                }
                _residents.RemoveAt(i);
                _everNoticed = true;
                int sent = 0;
                for (int n = Stance == CityStance.Against ? 2 : 1; n > 0; n--)
                {
                    Unit? nearest = null;
                    foreach (var g in Units)
                    {
                        if (g.Alive && g.Faction == Faction.Guard && !g.Bribed && !g.Type.Bites && g.GuardState == GuardState.Patrol
                            && DistanceTo(g, rx, ry) <= ResidentHeardRange && (nearest == null || DistanceTo(g, rx, ry) < DistanceTo(nearest, rx, ry)))
                        {
                            nearest = g;
                        }
                    }
                    if (nearest == null)
                    {
                        break;
                    }
                    ResidentCalls.Add((Tick, r.Id, nearest.Id));
                    nearest.LastKnownX = seen.X;
                    nearest.LastKnownY = seen.Y;
                    StartInvestigating(nearest);
                    sent++;
                }
                if (sent == 0)
                {
                    ResidentCalls.Add((Tick, r.Id, -1));
                }
            }
        }

        /// <summary>
        /// Sitzt er noch im Gebäude? Die Zelle liegt hinter der Fassade: Solange die Tür zu ist und solange er auf einem Feld der
        /// Zelle steht, zeigt das Spiel ihn nicht, sonst klebte er außen an der Wand. Er erscheint, wenn er vor die Tür tritt.
        /// </summary>
        public bool HiddenInCell(Unit u)
        {
            if (!u.IsPrisoner || u.CellDoor < 0 || !u.Alive)
            {
                return false;
            }
            var door = Doors[u.CellDoor];
            return !door.Open || door.Tiles.Contains((u.TileX, u.TileY));
        }

        /// <summary>Kurzer Name eines Gefangenen, wie er im Telegramm und an der Zellentür steht.</summary>
        public string PrisonerShort(Unit u) => _prisonerShort.GetValueOrDefault(u.Id, u.Name);

        private readonly Dictionary<int, string> _prisonerShort = new();
        private RescueTextData _rescueTexts = new();

        /// <summary>
        /// Zeile für den Telegrammstreifen in einem Einsatz der Art Befreien. Die Texte kommen aus der Mission, {NAME} ist der kurze Name
        /// eines Gefangenen.
        /// </summary>
        public string RescueLine()
        {
            var all = Prisoners.ToList();
            var living = all.Where(p => p.Alive).ToList();
            var dead = all.Find(p => !p.Alive);
            var free = living.Where(p => !p.Captive).ToList();
            string Named(string text, Unit? p) => text.Replace("{NAME}", p == null ? "" : _prisonerShort[p.Id]);
            if (EscapeProgress > 0)
            {
                return _rescueTexts.Boarding;
            }
            if (living.Exists(p => p.Captive && p.CellDoor >= 0 && !Doors[p.CellDoor].Open && Doors[p.CellDoor].Progress > 0))
            {
                return _rescueTexts.Opening;
            }
            if (dead != null && living.Count > 0)
            {
                return Named(_rescueTexts.OneDead, dead);
            }
            if (free.Count > 0)
            {
                return free.Count == living.Count ? _rescueTexts.AllFree : Named(_rescueTexts.OneFree, free[0]);
            }
            return _rescueTexts.Start;
        }

        /// <summary>Alle Gefangenen, auch die toten und die noch eingesperrten.</summary>
        public IEnumerable<Unit> Prisoners => Units.Where(u => u.IsPrisoner);

        /// <summary>Wachen sind die eine Seite, Agenten und befreite Gefangene die andere. Wer noch in der Zelle sitzt, ist für niemanden ein Ziel.</summary>
        private static bool Hostile(Unit a, Unit b) =>
            (a.Faction == Faction.Guard) != (b.Faction == Faction.Guard) && !a.Captive && !b.Captive;

        private void AddPrisoners(MissionData mission)
        {
            if (mission.RescueTexts != null)
            {
                _rescueTexts = mission.RescueTexts;
            }
            foreach (var p in mission.Prisoners)
            {
                AddUnit(new UnitData { Name = p.Name, Sprite = p.Sprite, X = p.X, Y = p.Y, Items = new List<string>() }, Faction.Prisoner, Math.Max(1, p.Hp));
                var u = Units[^1];
                u.Captive = true;
                u.CellDoor = Doors.FindIndex(d => d.Name == p.Door);
                _prisonerShort[u.Id] = p.Short.Length > 0 ? p.Short : p.Name.ToUpperInvariant();
                if (p.Door.Length > 0 && u.CellDoor < 0)
                {
                    throw new ArgumentException($"Unbekannte Zelle {p.Door}");
                }
            }
        }

        private void GiveKeys(MissionData mission)
        {
            for (int i = 0; i < mission.Guards.Count; i++)
            {
                foreach (string name in mission.Guards[i].Keys)
                {
                    int door = Doors.FindIndex(d => d.Name == name);
                    if (door < 0)
                    {
                        throw new ArgumentException($"Unbekannte Tür {name}");
                    }
                    Units[mission.Agents.Count + i].Keys.Add(door);
                }
            }
        }

        // ---------- Befehle ----------

        /// <summary>Rechtsklick auf einen Befreiten: Er folgt diesem Agenten. Folgt er ihm schon, wartet er stattdessen.</summary>
        public bool OrderPrisoner(int prisonerId, int agentId)
        {
            var p = Units[prisonerId];
            var a = Units[agentId];
            if (!p.Alive || !p.IsPrisoner || p.Captive || !a.Alive || a.Faction != Faction.Agent || State != MissionState.Running)
            {
                return false;
            }
            if (p.FollowId == agentId && !p.Waiting)
            {
                p.Waiting = true;
                p.Path.Clear();
            }
            else
            {
                p.FollowId = agentId;
                p.Waiting = false;
                p.RepathTick = 0;
            }
            return true;
        }

        // ---------- Regeln ----------

        /// <summary>Wer einen Schlüssel zur Tür hat, schließt sie in einer halben Sekunde auf.</summary>
        private bool HasKey(Unit u, int doorId) => u.Keys.Contains(doorId);

        /// <summary>Was das Bestechen dieser Wache kostet. Hier säße ein Nachlass, etwa wenn eine bestimmte Agentin dabei ist.</summary>
        public int BribePrice(Unit guard) => guard.Type.BribeCost;

        /// <summary>Nimmt diese Wache überhaupt Geld? Manche Arten nie, und einzelne Leute auch nicht.</summary>
        public bool Bribable(Unit guard) => guard.Type.BribeCost > 0 && !guard.NoBribe;

        /// <summary>Rufweite der Truppführerin: So nah am Bestechenden gilt sie als dabei.</summary>
        public const int LeaderRange = 5 * TileUnits;

        /// <summary>So weit sieht sie eine Wache und gilt damit ebenfalls als dabei.</summary>
        public const int LeaderSight = 8 * TileUnits;

        /// <summary>Nachlass in Prozent, wenn die Truppführerin dabei ist. 0 schaltet die Regel ab.</summary>
        public int LeaderDiscountPercent { get; set; } = 50;

        /// <summary>So oft gilt der Nachlass je Einsatz, -1 heißt immer.</summary>
        public int LeaderDiscountUses { get; set; } = -1;

        public int LeaderDiscountsUsed { get; private set; }

        /// <summary>Kann der Nachlass in diesem Einsatz noch gelten, wenn die Truppführerin herankommt?</summary>
        public bool LeaderBacksLater => LeaderDiscountPercent > 0 && (LeaderDiscountUses < 0 || LeaderDiscountsUsed < LeaderDiscountUses)
            && Units.Exists(l => l.Alive && l.Faction == Faction.Agent && l.Skill == SkillLeader);

        /// <summary>
        /// Ist die Truppführerin bei dieser Bestechung dabei? Sie lebt, ist im Einsatz und steht in Rufweite des Bestechenden
        /// oder sieht die Wache selbst. Wer aussetzt oder in der Krankenstube liegt, ist nicht im Einsatz und zählt nicht.
        /// </summary>
        public bool LeaderBacks(Unit briber, Unit guard)
        {
            if (LeaderDiscountPercent <= 0 || (LeaderDiscountUses >= 0 && LeaderDiscountsUsed >= LeaderDiscountUses))
            {
                return false;
            }
            return Units.Exists(l => l.Alive && l.Faction == Faction.Agent && l.Skill == SkillLeader
                && (Distance(l, briber) <= LeaderRange || CanSee(l, guard, LeaderSight)));
        }

        /// <summary>Was diese Wache jetzt von diesem Agenten nimmt, mit dem Nachlass der Truppführerin, wenn sie dabei ist.</summary>
        public int BribePrice(Unit guard, Unit briber) =>
            LeaderBacks(briber, guard) ? guard.Type.BribeCost * (100 - LeaderDiscountPercent) / 100 : guard.Type.BribeCost;

        private int PayBribe(Unit guard, Unit briber)
        {
            int price = BribePrice(guard, briber);
            LeaderDiscountsUsed += price < guard.Type.BribeCost ? 1 : 0;
            return price;
        }

        /// <summary>Eine bestochene Wache gibt ihre Schlüssel dem, der gezahlt hat.</summary>
        private void HandOverKeys(Unit from, Unit to)
        {
            foreach (int door in from.Keys)
            {
                if (!to.Keys.Contains(door))
                {
                    to.Keys.Add(door);
                }
            }
            from.Keys.Clear();
        }

        /// <summary>Fällt, wer Schlüssel trägt, liegen sie neben ihm. Der erste Agent, der hinkommt, nimmt sie.</summary>
        private void DropKeys(Unit u)
        {
            if (u.Keys.Count > 0)
            {
                KeysOnGround.Add(new DroppedKeys { X = u.X, Y = u.Y, Doors = new List<int>(u.Keys) });
                u.Keys.Clear();
            }
        }

        private void PickUpKeys()
        {
            for (int i = KeysOnGround.Count - 1; i >= 0; i--)
            {
                var keys = KeysOnGround[i];
                var taker = Units.Find(u => u.Alive && u.Faction == Faction.Agent && DistanceTo(u, keys.X, keys.Y) <= PickupRadius);
                if (taker != null)
                {
                    foreach (int door in keys.Doors)
                    {
                        if (!taker.Keys.Contains(door))
                        {
                            taker.Keys.Add(door);
                        }
                    }
                    KeysOnGround.RemoveAt(i);
                }
            }
        }

        private void ThinkPrisoner(Unit p)
        {
            if (p.Captive)
            {
                TryFree(p);
                return;
            }
            Unit? leader = p.FollowId >= 0 ? Units[p.FollowId] : null;
            if (leader != null && !leader.Alive)
            {
                // Der Führer ist gefallen: stehen bleiben, bis ein anderer Agent herankommt.
                p.FollowId = -1;
                p.Path.Clear();
                leader = null;
            }
            if (leader == null)
            {
                // Auch hier ohne Sichtprüfung: Er kann noch in der offenen Zelle stehen.
                leader = NearestAgent(p, AdoptRange, needSight: false);
                if (leader == null)
                {
                    return;
                }
                p.FollowId = leader.Id;
                p.Waiting = false;
            }
            if (p.Waiting)
            {
                return;
            }
            if (leader.Path.Count == 0 && (InExit(leader) || (NearExit(leader) && AgentAboard())))
            {
                Board(p, leader);
                return;
            }
            int distance = Distance(p, leader);
            if (distance <= FollowStopRange && HiddenInCell(p))
            {
                // Nicht in der Zelle stehen bleiben, auch wenn der Führer direkt vor der Tür wartet: heraus auf das nächste freie Feld.
                if (p.Path.Count == 0 && Tick >= p.RepathTick)
                {
                    p.RepathTick = Tick + FollowRepathTicks;
                    StepOutOfCell(p);
                }
            }
            else if (distance <= FollowStopRange)
            {
                p.Path.Clear();
                if (TileIsTaken(p, p.TileX, p.TileY))
                {
                    StepAside(p, null);
                }
            }
            else if (distance > FollowStartRange && Tick >= p.RepathTick)
            {
                p.RepathTick = Tick + FollowRepathTicks;
                WalkTo(p, leader.TileX, leader.TileY);
            }
        }

        private void StepOutOfCell(Unit p)
        {
            var cell = Doors[p.CellDoor].Tiles;
            (int X, int Y)? best = null;
            int bestSteps = int.MaxValue;
            foreach (var (cx, cy) in cell)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (Map.IsBlocked(x, y) || cell.Contains((x, y)) || TileIsTaken(p, x, y))
                        {
                            continue;
                        }
                        int steps = Map.FindPath(p.TileX, p.TileY, x, y)?.Count ?? int.MaxValue;
                        if (steps < bestSteps)
                        {
                            best = (x, y);
                            bestSteps = steps;
                        }
                    }
                }
            }
            if (best != null)
            {
                WalkTo(p, best.Value.X, best.Value.Y);
            }
        }

        /// <summary>Steht der Führer im Wagenbereich, sucht sich der Befreite dort selbst ein freies Feld, statt neben der Zone stehen zu bleiben.</summary>
        private void Board(Unit p, Unit leader)
        {
            if (InExit(p) && (p.Path.Count > 0 || !TileIsTaken(p, p.TileX, p.TileY)) || Tick < p.RepathTick)
            {
                return;
            }
            p.RepathTick = Tick + FollowRepathTicks;
            (int X, int Y)? best = null;
            int bestSteps = int.MaxValue;
            for (int y = _exit.Y0 / TileUnits; y * TileUnits < _exit.Y1; y++)
            {
                for (int x = _exit.X0 / TileUnits; x * TileUnits < _exit.X1; x++)
                {
                    if (Map.IsBlocked(x, y) || TileIsTaken(p, x, y) || (x == leader.TileX && y == leader.TileY))
                    {
                        continue;
                    }
                    int steps = Map.FindPath(p.TileX, p.TileY, x, y)?.Count ?? int.MaxValue;
                    if (steps < bestSteps)
                    {
                        best = (x, y);
                        bestSteps = steps;
                    }
                }
            }
            if (best != null)
            {
                WalkTo(p, best.Value.X, best.Value.Y);
            }
        }

        /// <summary>Ist die Zelle offen und steht ein Agent still neben dem Gefangenen, folgt er ihm nach FreeTicks.</summary>
        private void TryFree(Unit p)
        {
            // Ohne Sichtprüfung: Die Türfelder sperren die Sicht auch offen, die offene Tür genügt.
            Unit? helper = p.CellDoor < 0 || Doors[p.CellDoor].Open ? NearestAgent(p, FreeRange, standing: true, needSight: false) : null;
            p.FreeProgress = helper != null ? p.FreeProgress + 1 : 0;
            if (helper != null && p.FreeProgress >= FreeTicks)
            {
                p.Captive = false;
                p.FreeProgress = 0;
                p.FollowId = helper.Id;
            }
        }

        private Unit? NearestAgent(Unit p, int range, bool standing = false, bool needSight = true)
        {
            Unit? best = null;
            foreach (var a in Units)
            {
                if (a.Alive && a.Faction == Faction.Agent && (!standing || a.Path.Count == 0) && Distance(p, a) <= range && (!needSight || CanSee(p, a, range))
                    && (best == null || Distance(p, a) < Distance(p, best)))
                {
                    best = a;
                }
            }
            return best;
        }

        private bool InExit(Unit u) => u.X >= _exit.X0 && u.X < _exit.X1 && u.Y >= _exit.Y0 && u.Y < _exit.Y1;

        /// <summary>
        /// Steht er höchstens drei Felder neben dem Wagen? Wer einen Trupp zum Wagen schickt, bekommt nicht jeden in das Feld.
        /// Ein Befreiter steigt trotzdem ein, wenn sein Führer daneben hält und schon ein Agent im Wagen steht.
        /// </summary>
        private bool NearExit(Unit u) =>
            u.X >= _exit.X0 - BoardRange && u.X < _exit.X1 + BoardRange && u.Y >= _exit.Y0 - BoardRange && u.Y < _exit.Y1 + BoardRange;

        private bool AgentAboard() => Units.Exists(a => a.Alive && a.Faction == Faction.Agent && a.Path.Count == 0 && InExit(a));

        /// <summary>
        /// Befreien: Gewonnen ist, wenn alle lebenden Gefangenen und ein Agent im Wagen stehen. Stirbt einer, geht es weiter,
        /// sterben alle, ist der Einsatz verloren.
        /// </summary>
        private void CheckRescue(bool anyAgent)
        {
            int alive = 0, aboard = 0;
            foreach (var p in Units)
            {
                if (p.IsPrisoner && p.Alive)
                {
                    alive++;
                    aboard += !p.Captive && !p.Moving && InExit(p) ? 1 : 0;
                }
            }
            if (alive == 0)
            {
                State = MissionState.Lost;
                return;
            }
            bool escort = anyAgent && Units.Exists(u => u.Alive && u.Faction == Faction.Agent && !u.Moving && InExit(u));
            EscapeProgress = aboard == alive && escort ? EscapeProgress + 1 : 0;
            if (EscapeProgress >= EscapeTicks)
            {
                State = MissionState.Won;
            }
        }
    }
}
