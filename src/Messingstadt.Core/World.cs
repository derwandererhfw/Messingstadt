using System;
using System.Collections.Generic;

namespace Messingstadt.Core
{
    public enum Faction
    {
        Agent,
        Guard,
    }

    public enum MissionState
    {
        Running,
        Won,
        Lost,
    }

    public sealed class Unit
    {
        public int Id;
        public string Name = "";
        public string Sprite = "";
        public Faction Faction;
        public int X, Y;
        public int Hp, MaxHp;
        public int Cooldown;
        public int TargetId = -1;
        public bool Alerted;
        public bool Moving;
        public bool CarriesItem;
        public int WalkTick;
        public int FacingX, FacingY = 1;
        public int LastFiredTick = -1000;
        public readonly List<(int X, int Y)> Path = new();
        public readonly List<(int X, int Y)> Patrol = new();
        public int PatrolIndex;
        public int RepathTick;

        public bool Alive => Hp > 0;
        public int TileX => X / World.TileUnits;
        public int TileY => Y / World.TileUnits;
    }

    /// <summary>Ein abgegebener Schuss, nur für die Darstellung.</summary>
    public readonly record struct ShotEvent(int FromX, int FromY, int ToX, int ToY, bool Hit, int Tick);

    /// <summary>Der gesamte Spielzustand. Vollständig deterministisch: gleicher Seed und gleiche Befehle, gleicher Ablauf.</summary>
    public sealed class World
    {
        public const int TickRate = 20;
        public const int TileUnits = 1000;

        private const int AgentHp = 100;
        private const int GuardHp = 60;
        private const int AgentSpeed = 120;
        private const int GuardPatrolSpeed = 80;
        private const int GuardAlertSpeed = 110;
        private const int WeaponRange = 7 * TileUnits;
        private const int WeaponCooldown = 14;
        private const int AgentDamage = 16;
        private const int GuardDamage = 8;
        private const int HitChanceBase = 85;
        private const int HitChancePerTile = 5;
        private const int SightRange = 7 * TileUnits;
        private const int AgentSightRange = 10 * TileUnits;
        private const int AlertRadius = 8 * TileUnits;
        private const int PickupRadius = 700;
        private const int RepathInterval = 20;

        private uint _rng;
        private readonly (int X0, int Y0, int X1, int Y1) _exit;

        public GameMap Map { get; }
        public List<Unit> Units { get; } = new();
        public List<ShotEvent> Shots { get; } = new();
        public int Tick { get; private set; }
        public MissionState State { get; private set; } = MissionState.Running;
        public int ItemX { get; private set; }
        public int ItemY { get; private set; }
        public bool ItemTaken { get; private set; }
        public string ItemName { get; }
        public (int X0, int Y0, int X1, int Y1) Exit => _exit;

        public World(MapData mapData, MissionData mission)
        {
            Map = mapData.Build();
            _rng = (uint)mission.Seed | 1u;
            ItemName = mission.ItemName;
            ItemX = ToUnits(mission.Item[0]);
            ItemY = ToUnits(mission.Item[1]);
            _exit = (ToUnits(mission.Exit.X0), ToUnits(mission.Exit.Y0), ToUnits(mission.Exit.X1), ToUnits(mission.Exit.Y1));
            foreach (var a in mission.Agents)
            {
                AddUnit(a, Faction.Agent, AgentHp);
            }
            foreach (var g in mission.Guards)
            {
                AddUnit(g, Faction.Guard, GuardHp);
            }
        }

        private static int ToUnits(double tiles) => (int)Math.Round(tiles * TileUnits);

        private void AddUnit(UnitData d, Faction faction, int hp)
        {
            var u = new Unit
            {
                Id = Units.Count, Name = d.Name, Sprite = d.Sprite, Faction = faction,
                X = ToUnits(d.X), Y = ToUnits(d.Y), Hp = hp, MaxHp = hp,
            };
            foreach (var p in d.Patrol)
            {
                u.Patrol.Add(((int)p[0], (int)p[1]));
            }
            Units.Add(u);
        }

        private uint NextRandom()
        {
            // xorshift32
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return _rng;
        }

        // ---------- Befehle ----------

        /// <summary>Schickt eine Einheit zu einem Feld. Zielauswahl und Feuer bleiben automatisch.</summary>
        public bool OrderMove(int unitId, int tileX, int tileY)
        {
            var u = Units[unitId];
            if (!u.Alive || State != MissionState.Running)
            {
                return false;
            }
            var path = Map.FindPath(u.TileX, u.TileY, tileX, tileY);
            if (path == null)
            {
                return false;
            }
            u.Path.Clear();
            u.Path.AddRange(path);
            u.TargetId = -1;
            return true;
        }

        /// <summary>Befiehlt den Angriff. Die Einheit geht auf das Ziel zu, bis sie es treffen kann.</summary>
        public void OrderAttack(int unitId, int targetId)
        {
            var u = Units[unitId];
            if (u.Alive && Units[targetId].Alive && State == MissionState.Running)
            {
                u.TargetId = targetId;
                u.RepathTick = 0;
            }
        }

        // ---------- Takt ----------

        public void Step()
        {
            if (State != MissionState.Running)
            {
                return;
            }
            Tick++;
            foreach (var u in Units)
            {
                if (!u.Alive)
                {
                    continue;
                }
                if (u.Faction == Faction.Guard)
                {
                    ThinkGuard(u);
                }
                else
                {
                    ThinkAgent(u);
                }
                Move(u);
                Fight(u);
            }
            CheckMission();
        }

        private void ThinkAgent(Unit a)
        {
            if (a.TargetId < 0)
            {
                return;
            }
            var target = Units[a.TargetId];
            if (!target.Alive)
            {
                a.TargetId = -1;
                a.Path.Clear();
                return;
            }
            if (CanSee(a, target, WeaponRange))
            {
                a.Path.Clear();
            }
            else if (Tick >= a.RepathTick)
            {
                a.RepathTick = Tick + RepathInterval;
                var path = Map.FindPath(a.TileX, a.TileY, target.TileX, target.TileY);
                a.Path.Clear();
                if (path != null)
                {
                    a.Path.AddRange(path);
                }
            }
        }

        private void ThinkGuard(Unit g)
        {
            Unit? seen = NearestVisibleEnemy(g, SightRange);
            if (seen != null && !g.Alerted)
            {
                Alert(g);
            }
            if (g.Alerted)
            {
                if (seen == null || Distance(g, seen) > WeaponRange - TileUnits)
                {
                    Unit? chase = seen ?? NearestLivingEnemy(g);
                    if (chase != null && Tick >= g.RepathTick)
                    {
                        g.RepathTick = Tick + RepathInterval;
                        var path = Map.FindPath(g.TileX, g.TileY, chase.TileX, chase.TileY);
                        g.Path.Clear();
                        if (path != null)
                        {
                            g.Path.AddRange(path);
                        }
                    }
                }
                else
                {
                    g.Path.Clear();
                }
                return;
            }
            if (g.Path.Count == 0 && g.Patrol.Count > 0)
            {
                g.PatrolIndex = (g.PatrolIndex + 1) % g.Patrol.Count;
                var (px, py) = g.Patrol[g.PatrolIndex];
                var path = Map.FindPath(g.TileX, g.TileY, px, py);
                if (path != null)
                {
                    g.Path.AddRange(path);
                }
            }
        }

        private void Alert(Unit source)
        {
            foreach (var u in Units)
            {
                if (u.Alive && u.Faction == Faction.Guard && DistanceTo(u, source.X, source.Y) <= AlertRadius)
                {
                    u.Alerted = true;
                }
            }
        }

        private void Move(Unit u)
        {
            u.Moving = false;
            if (u.Path.Count == 0)
            {
                return;
            }
            int speed = u.Faction == Faction.Agent ? AgentSpeed : (u.Alerted ? GuardAlertSpeed : GuardPatrolSpeed);
            var (tx, ty) = u.Path[0];
            int wx = tx * TileUnits + TileUnits / 2, wy = ty * TileUnits + TileUnits / 2;
            int dx = wx - u.X, dy = wy - u.Y;
            int dist = (int)Math.Sqrt((double)dx * dx + (double)dy * dy);
            if (dist <= speed)
            {
                u.X = wx;
                u.Y = wy;
                u.Path.RemoveAt(0);
            }
            else
            {
                u.X += dx * speed / dist;
                u.Y += dy * speed / dist;
            }
            if (dx != 0 || dy != 0)
            {
                u.FacingX = dx;
                u.FacingY = dy;
            }
            u.Moving = true;
            u.WalkTick++;
        }

        private void Fight(Unit u)
        {
            if (u.Cooldown > 0)
            {
                u.Cooldown--;
                return;
            }
            Unit? target = u.TargetId >= 0 ? Units[u.TargetId] : null;
            if (target != null && !target.Alive)
            {
                u.TargetId = -1;
                target = null;
            }
            if (target != null && !CanSee(u, target, WeaponRange))
            {
                // Befohlenes Ziel noch nicht in Reichweite: unterwegs nur auf alarmierte Gegner feuern.
                target = NearestVisibleEnemy(u, WeaponRange);
                if (target != null && target.Alerted)
                {
                    Shoot(u, target);
                }
                return;
            }
            target ??= NearestVisibleEnemy(u, WeaponRange);
            // Agenten bleiben ruhig, bis die Wachen alarmiert sind oder der Spieler ein Ziel vorgibt.
            if (target == null || (u.Faction == Faction.Guard && !u.Alerted))
            {
                return;
            }
            if (u.Faction == Faction.Agent && u.TargetId < 0 && !target.Alerted)
            {
                return;
            }
            Shoot(u, target);
        }

        private void Shoot(Unit u, Unit target)
        {
            u.Cooldown = WeaponCooldown;
            u.FacingX = target.X - u.X;
            u.FacingY = target.Y - u.Y;
            u.LastFiredTick = Tick;
            int chance = HitChanceBase - HitChancePerTile * Distance(u, target) / TileUnits;
            bool hit = NextRandom() % 100 < (uint)Math.Max(chance, 10);
            Shots.Add(new ShotEvent(u.X, u.Y, target.X, target.Y, hit, Tick));
            if (Shots.Count > 64)
            {
                Shots.RemoveAt(0);
            }
            Alert(u);
            if (hit)
            {
                target.Hp = Math.Max(0, target.Hp - (u.Faction == Faction.Agent ? AgentDamage : GuardDamage));
                if (target.Faction == Faction.Guard)
                {
                    target.Alerted = true;
                    Alert(target);
                }
                if (!target.Alive)
                {
                    target.Path.Clear();
                    target.Moving = false;
                    if (target.CarriesItem)
                    {
                        target.CarriesItem = false;
                        ItemTaken = false;
                        ItemX = target.X;
                        ItemY = target.Y;
                    }
                }
            }
        }

        private void CheckMission()
        {
            bool anyAgent = false;
            foreach (var u in Units)
            {
                if (u.Faction != Faction.Agent || !u.Alive)
                {
                    continue;
                }
                anyAgent = true;
                if (!ItemTaken && DistanceTo(u, ItemX, ItemY) <= PickupRadius)
                {
                    ItemTaken = true;
                    u.CarriesItem = true;
                }
                if (u.CarriesItem && u.X >= _exit.X0 && u.X < _exit.X1 && u.Y >= _exit.Y0 && u.Y < _exit.Y1)
                {
                    State = MissionState.Won;
                    return;
                }
            }
            if (!anyAgent)
            {
                State = MissionState.Lost;
            }
        }

        // ---------- Abfragen ----------

        public static int Distance(Unit a, Unit b) => DistanceTo(a, b.X, b.Y);

        private static int DistanceTo(Unit a, int x, int y)
        {
            double dx = a.X - x, dy = a.Y - y;
            return (int)Math.Sqrt(dx * dx + dy * dy);
        }

        public bool CanSee(Unit a, Unit b, int range) =>
            Distance(a, b) <= range && Map.HasLineOfSight(a.TileX, a.TileY, b.TileX, b.TileY);

        /// <summary>True, wenn ein lebender Agent diese Einheit gerade sehen kann.</summary>
        public bool IsSpottedByAgents(Unit target)
        {
            foreach (var a in Units)
            {
                if (a.Alive && a.Faction == Faction.Agent && CanSee(a, target, AgentSightRange))
                {
                    return true;
                }
            }
            return false;
        }

        private Unit? NearestVisibleEnemy(Unit u, int range)
        {
            Unit? best = null;
            int bestDist = int.MaxValue;
            foreach (var o in Units)
            {
                if (o.Alive && o.Faction != u.Faction && Distance(u, o) < bestDist && CanSee(u, o, range))
                {
                    best = o;
                    bestDist = Distance(u, o);
                }
            }
            return best;
        }

        private Unit? NearestLivingEnemy(Unit u)
        {
            Unit? best = null;
            int bestDist = int.MaxValue;
            foreach (var o in Units)
            {
                if (o.Alive && o.Faction != u.Faction && Distance(u, o) < bestDist)
                {
                    best = o;
                    bestDist = Distance(u, o);
                }
            }
            return best;
        }

        /// <summary>Prüfsumme des Zustands für Determinismus-Tests.</summary>
        public ulong StateHash()
        {
            ulong h = 14695981039346656037UL;
            void Mix(long v)
            {
                h ^= (ulong)v;
                h *= 1099511628211UL;
            }
            Mix(Tick);
            Mix(_rng);
            Mix((long)State);
            foreach (var u in Units)
            {
                Mix(u.X);
                Mix(u.Y);
                Mix(u.Hp);
                Mix(u.Cooldown);
                Mix(u.Path.Count);
            }
            return h;
        }
    }
}
