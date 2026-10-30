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

    /// <summary>Wie leise der Einsatz bisher lief: nie bemerkt, bemerkt ohne Schuss, Schüsse gefallen.</summary>
    public enum MissionRating
    {
        Unseen,
        Quiet,
        Loud,
    }

    /// <summary>Verhalten einer Wache: Rundgang, bemerken, kämpfen, nachsehen, absuchen.</summary>
    public enum GuardState
    {
        Patrol,
        Noticing,
        Combat,
        Investigate,
        Search,
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
        public GuardState GuardState;
        public int NoticeTicks;
        public int LastKnownX, LastKnownY;
        public int LastSeenTick;
        public int SearchUntilTick;
        public int AlarmTick;
        public int AlarmX, AlarmY;
        public bool Sidestepping;

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
        private const int AgentDamage = 12;
        private const int GuardDamage = 10;
        private const int HitChanceBase = 85;
        private const int HitChancePerTile = 5;
        private const int SightRange = 7 * TileUnits;
        private const int AgentSightRange = 10 * TileUnits;
        private const int AlertRadius = 8 * TileUnits;
        private const int AlarmDelayPerTile = 3;
        private const int NoticeBase = 8;
        private const int NoticePerTile = 2;
        private const int NoticeWhenAlert = 4;
        private const int LostSightTicks = 40;
        private const int SearchTicks = 100;
        private const int CoverPenalty = 25;
        private const int MovingPenalty = 15;
        private const int MinHitChance = 10;
        private const int FiringSearchRadius = 3;
        private const int CoverScore = 30;
        private const int PickupRadius = 700;
        private const int RepathInterval = 20;
        private const int CarrierSpeed = 100;
        public const int GuardCloseRange = 2 * TileUnits;
        private const int LookAroundTicks = 60;
        private const int SpreadRadius = 3;

        public const int PickupTicks = 60;
        public const int GuardConeRange = 5 * TileUnits;
        public const int GuardConeHalfDegrees = 45;

        // Erst eine Linie quer zur Blickrichtung, damit keine Figur im Bild hinter einer anderen steht.
        private static readonly (int X, int Y)[] Formation =
        {
            (0, 0), (1, -1), (-1, 1), (2, -2), (-2, 2), (1, 0), (0, 1), (-1, 0), (0, -1),
            (2, -1), (-1, 2), (1, -2), (-2, 1), (1, 1), (-1, -1), (2, 0), (0, 2),
        };

        private uint _rng;
        private readonly (int X0, int Y0, int X1, int Y1) _exit;
        private readonly ReinforcementData? _reinforcements;
        private int _reinforceTick;
        private int _reinforcedCount;
        private int _lastShotX, _lastShotY;
        private bool _everNoticed;
        private bool _everShot;

        public GameMap Map { get; }
        public List<Unit> Units { get; } = new();
        public List<ShotEvent> Shots { get; } = new();
        public int Tick { get; private set; }
        public MissionState State { get; private set; } = MissionState.Running;
        public int ItemX { get; private set; }
        public int ItemY { get; private set; }
        public bool ItemTaken { get; private set; }
        public int ItemProgress { get; private set; }
        public string ItemName { get; }
        public (int X0, int Y0, int X1, int Y1) Exit => _exit;
        public MissionRating Rating => _everShot ? MissionRating.Loud : _everNoticed ? MissionRating.Quiet : MissionRating.Unseen;

        /// <summary>Takte bis zur nächsten Verstärkung, -1 wenn keine unterwegs ist.</summary>
        public int TicksUntilReinforcement => _reinforceTick > 0 ? _reinforceTick - Tick : -1;

        public World(MapData mapData, MissionData mission)
        {
            Map = mapData.Build();
            _rng = (uint)mission.Seed | 1u;
            ItemName = mission.ItemName;
            _reinforcements = mission.Reinforcements;
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
            if (d.Facing is { Length: 2 })
            {
                u.FacingX = ToUnits(d.Facing[0]);
                u.FacingY = ToUnits(d.Facing[1]);
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

        /// <summary>Schickt mehrere Agenten zum Zielfeld und verteilt sie auf freie Felder in einer Linie.</summary>
        public bool OrderGroupMove(IEnumerable<int> unitIds, int tileX, int tileY)
        {
            if (Map.IsBlocked(tileX, tileY) || State != MissionState.Running)
            {
                return false;
            }
            var ids = new List<int>(unitIds);
            // Der Träger zuerst: Er bekommt genau das angeklickte Feld, etwa in der Fluchtzone.
            ids.Sort((a, b) => Units[a].CarriesItem != Units[b].CarriesItem ? (Units[a].CarriesItem ? -1 : 1) : a.CompareTo(b));
            var taken = new HashSet<(int, int)>();
            foreach (var o in Units)
            {
                if (o.Alive && o.Faction == Faction.Agent && !ids.Contains(o.Id))
                {
                    taken.Add(o.Path.Count > 0 ? o.Path[^1] : (o.TileX, o.TileY));
                }
            }
            bool any = false;
            foreach (int id in ids)
            {
                foreach (var (ox, oy) in Formation)
                {
                    int x = tileX + ox, y = tileY + oy;
                    if (!taken.Contains((x, y)) && !Map.IsBlocked(x, y) && OrderMove(id, x, y))
                    {
                        taken.Add((x, y));
                        any = true;
                        break;
                    }
                }
            }
            return any;
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
            SpawnReinforcement();
            CheckMission();
        }

        private void SpawnReinforcement()
        {
            if (_reinforcements == null || _reinforceTick == 0 || Tick < _reinforceTick)
            {
                return;
            }
            _reinforcedCount++;
            AddUnit(new UnitData
            {
                Name = $"Verstärkung {_reinforcedCount}", Sprite = _reinforcements.Sprite,
                X = _reinforcements.X, Y = _reinforcements.Y,
            }, Faction.Guard, GuardHp);
            var g = Units[^1];
            g.LastKnownX = _lastShotX;
            g.LastKnownY = _lastShotY;
            StartInvestigating(g);
            _reinforceTick = _reinforcedCount < _reinforcements.Count ? Tick + _reinforcements.Interval * TickRate : 0;
        }

        private void ThinkAgent(Unit a)
        {
            if (a.Path.Count == 0)
            {
                a.Sidestepping = false;
            }
            Unit? target = a.TargetId >= 0 ? Units[a.TargetId] : null;
            if (target != null && !target.Alive)
            {
                a.TargetId = -1;
                a.Path.Clear();
                target = null;
            }
            if (target != null)
            {
                if (CanSee(a, target, WeaponRange))
                {
                    if (!a.Sidestepping)
                    {
                        a.Path.Clear();
                    }
                }
                else if (Tick >= a.RepathTick)
                {
                    a.RepathTick = Tick + RepathInterval;
                    a.Sidestepping = false;
                    WalkTo(a, target.TileX, target.TileY);
                }
            }
            if (a.Path.Count == 0 && TileIsTaken(a, a.TileX, a.TileY))
            {
                StepAside(a, target);
            }
        }

        /// <summary>Geht zum nächsten freien Feld. Mit Ziel nur auf Felder, von denen aus es sichtbar bleibt.</summary>
        private void StepAside(Unit a, Unit? target)
        {
            for (int r = 1; r <= SpreadRadius; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = a.TileX + dx, y = a.TileY + dy;
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || Map.IsBlocked(x, y) || TileIsTaken(a, x, y))
                        {
                            continue;
                        }
                        if (target != null && !Map.HasLineOfSight(x, y, target.TileX, target.TileY))
                        {
                            continue;
                        }
                        WalkTo(a, x, y);
                        if (a.Path.Count > 0)
                        {
                            a.Sidestepping = true;
                            return;
                        }
                    }
                }
            }
        }

        private void ThinkGuard(Unit g)
        {
            // Nur wer ahnungslos ist, hat einen Sichtkegel. Alarmierte Wachen achten auf alle Seiten.
            Unit? seen = g.GuardState == GuardState.Patrol ? NearestNoticedEnemy(g) : NearestVisibleEnemy(g, SightRange);
            if (seen != null)
            {
                g.LastKnownX = seen.X;
                g.LastKnownY = seen.Y;
                g.LastSeenTick = Tick;
            }
            if (g.AlarmTick > 0 && Tick >= g.AlarmTick)
            {
                g.AlarmTick = 0;
                HearAlarm(g, seen);
            }
            switch (g.GuardState)
            {
                case GuardState.Patrol:
                    if (seen != null)
                    {
                        StartNoticing(g, seen, NoticeBase + NoticePerTile * Distance(g, seen) / TileUnits);
                    }
                    else
                    {
                        WalkPatrol(g);
                    }
                    break;
                case GuardState.Noticing:
                    if (seen == null)
                    {
                        StartInvestigating(g);
                    }
                    else if (--g.NoticeTicks <= 0)
                    {
                        StartCombat(g);
                    }
                    break;
                case GuardState.Combat:
                    ThinkCombat(g, seen);
                    break;
                case GuardState.Investigate:
                    if (seen != null)
                    {
                        StartNoticing(g, seen, NoticeWhenAlert);
                    }
                    else if (g.Path.Count == 0)
                    {
                        g.GuardState = GuardState.Search;
                        g.SearchUntilTick = Tick + SearchTicks;
                    }
                    break;
                case GuardState.Search:
                    if (seen != null)
                    {
                        StartNoticing(g, seen, NoticeWhenAlert);
                    }
                    else if (Tick >= g.SearchUntilTick)
                    {
                        g.GuardState = GuardState.Patrol;
                        g.Alerted = false;
                    }
                    break;
            }
        }

        private void HearAlarm(Unit g, Unit? seen)
        {
            if (g.GuardState == GuardState.Combat)
            {
                return;
            }
            if (g.GuardState == GuardState.Noticing && seen != null)
            {
                g.NoticeTicks = Math.Min(g.NoticeTicks, NoticeWhenAlert);
                return;
            }
            g.LastKnownX = g.AlarmX;
            g.LastKnownY = g.AlarmY;
            StartInvestigating(g);
        }

        private void WalkPatrol(Unit g)
        {
            if (g.Patrol.Count == 0)
            {
                // Posten bleiben stehen und schauen reihum in alle vier Richtungen.
                if (Tick % LookAroundTicks == 0)
                {
                    (g.FacingX, g.FacingY) = (-g.FacingY, g.FacingX);
                }
                return;
            }
            if (g.Path.Count == 0)
            {
                g.PatrolIndex = (g.PatrolIndex + 1) % g.Patrol.Count;
                var (px, py) = g.Patrol[g.PatrolIndex];
                WalkTo(g, px, py);
            }
        }

        private void WalkTo(Unit g, int tileX, int tileY)
        {
            g.Path.Clear();
            var path = Map.FindPath(g.TileX, g.TileY, tileX, tileY);
            if (path != null)
            {
                g.Path.AddRange(path);
            }
        }

        private void StartNoticing(Unit g, Unit seen, int ticks)
        {
            _everNoticed = true;
            g.GuardState = GuardState.Noticing;
            g.NoticeTicks = ticks;
            g.Path.Clear();
            g.FacingX = seen.X - g.X;
            g.FacingY = seen.Y - g.Y;
        }

        private void StartCombat(Unit g)
        {
            g.GuardState = GuardState.Combat;
            g.Alerted = true;
            g.LastSeenTick = Tick;
            g.RepathTick = 0;
        }

        private void StartInvestigating(Unit g)
        {
            g.GuardState = GuardState.Investigate;
            g.Alerted = true;
            WalkTo(g, g.LastKnownX / TileUnits, g.LastKnownY / TileUnits);
        }

        private void ThinkCombat(Unit g, Unit? seen)
        {
            if (seen == null)
            {
                if (Tick - g.LastSeenTick > LostSightTicks)
                {
                    StartInvestigating(g);
                }
                return;
            }
            if (g.Path.Count > 0 || Tick < g.RepathTick)
            {
                return;
            }
            g.RepathTick = Tick + RepathInterval;
            bool inRange = Distance(g, seen) <= WeaponRange - TileUnits;
            if (inRange && !TileIsTaken(g, g.TileX, g.TileY) && HasCoverFrom(g, seen))
            {
                return;
            }
            var spot = FindFiringTile(g, seen);
            if (spot != null)
            {
                WalkTo(g, spot.Value.X, spot.Value.Y);
            }
            else if (!inRange)
            {
                WalkTo(g, seen.TileX, seen.TileY);
            }
        }

        /// <summary>Sucht ein freies Feld in der Nähe mit Sicht auf das Ziel, bevorzugt in Deckung.</summary>
        private (int X, int Y)? FindFiringTile(Unit g, Unit target)
        {
            (int X, int Y)? best = null;
            int bestScore = int.MinValue;
            for (int dy = -FiringSearchRadius; dy <= FiringSearchRadius; dy++)
            {
                for (int dx = -FiringSearchRadius; dx <= FiringSearchRadius; dx++)
                {
                    int x = g.TileX + dx, y = g.TileY + dy;
                    if (Map.IsBlocked(x, y) || TileIsTaken(g, x, y) || !Map.HasLineOfSight(x, y, target.TileX, target.TileY))
                    {
                        continue;
                    }
                    int tx = target.TileX - x, ty = target.TileY - y;
                    int distSq = tx * tx + ty * ty;
                    if (distSq > 36 || distSq < 4)
                    {
                        continue;
                    }
                    int score = -4 * (Math.Abs(dx) + Math.Abs(dy));
                    if (TileHasCoverFrom(x, y, target.TileX, target.TileY))
                    {
                        score += CoverScore;
                    }
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = (x, y);
                    }
                }
            }
            return best;
        }

        /// <summary>True, wenn jemand von der eigenen Seite dort steht oder dorthin unterwegs ist. Auf dem eigenen Feld weicht die höhere Id.</summary>
        private bool TileIsTaken(Unit g, int x, int y)
        {
            bool ownTile = x == g.TileX && y == g.TileY;
            foreach (var o in Units)
            {
                if (o.Id == g.Id || !o.Alive || o.Faction != g.Faction)
                {
                    continue;
                }
                bool standing = o.Path.Count == 0 && o.TileX == x && o.TileY == y && (!ownTile || o.Id < g.Id);
                // Wer auf seinem Feld steht, wartet: Der Ankommende weicht aus, sonst wäre das Ziel nie erreichbar.
                bool heading = !ownTile && o.Path.Count > 0 && o.Path[^1] == (x, y);
                if (standing || heading)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Löst Alarm aus. Nahe Wachen reagieren früher, ferne später oder gar nicht.</summary>
        public void RaiseAlarm(int x, int y)
        {
            foreach (var u in Units)
            {
                if (!u.Alive || u.Faction != Faction.Guard || u.GuardState == GuardState.Combat || u.AlarmTick > 0)
                {
                    continue;
                }
                int dist = DistanceTo(u, x, y);
                if (dist <= AlertRadius)
                {
                    u.AlarmTick = Tick + 1 + AlarmDelayPerTile * dist / TileUnits;
                    u.AlarmX = x;
                    u.AlarmY = y;
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
            int speed = u.Faction == Faction.Agent
                ? (u.CarriesItem ? CarrierSpeed : AgentSpeed)
                : (u.Alerted ? GuardAlertSpeed : GuardPatrolSpeed);
            // Der Rest des Schritts zählt beim nächsten Feld weiter, sonst liefe jeder langsamer als sein Tempo.
            int remaining = speed;
            while (remaining > 0 && u.Path.Count > 0)
            {
                var (tx, ty) = u.Path[0];
                int wx = tx * TileUnits + TileUnits / 2, wy = ty * TileUnits + TileUnits / 2;
                int dx = wx - u.X, dy = wy - u.Y;
                int dist = (int)Math.Sqrt((double)dx * dx + (double)dy * dy);
                if (dist <= remaining)
                {
                    u.X = wx;
                    u.Y = wy;
                    u.Path.RemoveAt(0);
                    remaining -= dist;
                }
                else
                {
                    u.X += dx * remaining / dist;
                    u.Y += dy * remaining / dist;
                    remaining = 0;
                }
                if (dx != 0 || dy != 0)
                {
                    u.FacingX = dx;
                    u.FacingY = dy;
                }
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
            if (target == null || (u.Faction == Faction.Guard && u.GuardState != GuardState.Combat))
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
            _lastShotX = u.X;
            _lastShotY = u.Y;
            if (!_everShot)
            {
                _everShot = true;
                if (_reinforcements != null)
                {
                    _reinforceTick = Tick + _reinforcements.FirstDelay * TickRate;
                }
            }
            bool hit = NextRandom() % 100 < (uint)HitChance(u, target);
            Shots.Add(new ShotEvent(u.X, u.Y, target.X, target.Y, hit, Tick));
            if (Shots.Count > 64)
            {
                Shots.RemoveAt(0);
            }
            if (target.Faction == Faction.Guard && target.GuardState != GuardState.Combat)
            {
                // Wer beschossen wird, reagiert sofort und kennt die Richtung.
                target.LastKnownX = u.X;
                target.LastKnownY = u.Y;
                target.Path.Clear();
                StartCombat(target);
            }
            RaiseAlarm(u.X, u.Y);
            if (hit)
            {
                target.Hp = Math.Max(0, target.Hp - (u.Faction == Faction.Agent ? AgentDamage : GuardDamage));
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
            Unit? picker = null;
            foreach (var u in Units)
            {
                if (u.Faction != Faction.Agent || !u.Alive)
                {
                    continue;
                }
                anyAgent = true;
                // Auf demselben Feld immer erreichbar, auch wenn das Buch in einer Feldecke liegt.
                bool atItem = DistanceTo(u, ItemX, ItemY) <= PickupRadius
                    || (u.TileX == ItemX / TileUnits && u.TileY == ItemY / TileUnits);
                if (picker == null && !ItemTaken && !u.Moving && atItem)
                {
                    picker = u;
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
            // Das Aufnehmen braucht Zeit im Stand. Wer weggeht, fängt von vorn an.
            ItemProgress = picker != null ? ItemProgress + 1 : 0;
            if (picker != null && ItemProgress >= PickupTicks)
            {
                ItemTaken = true;
                ItemProgress = 0;
                picker.CarriesItem = true;
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

        /// <summary>Trefferchance in Prozent: Entfernung, Deckung des Ziels, Bewegung des Schützen.</summary>
        public int HitChance(Unit shooter, Unit target)
        {
            int chance = HitChanceBase - HitChancePerTile * Distance(shooter, target) / TileUnits;
            if (HasCoverFrom(target, shooter))
            {
                chance -= CoverPenalty;
            }
            if (shooter.Moving)
            {
                chance -= MovingPenalty;
            }
            return Math.Max(chance, MinHitChance);
        }

        /// <summary>True, wenn direkt neben der Einheit in Richtung des Gegners ein Hindernis steht.</summary>
        public bool HasCoverFrom(Unit u, Unit enemy) => TileHasCoverFrom(u.TileX, u.TileY, enemy.TileX, enemy.TileY);

        private bool TileHasCoverFrom(int x, int y, int enemyX, int enemyY)
        {
            int dx = Math.Sign(enemyX - x), dy = Math.Sign(enemyY - y);
            return (dx != 0 && Map.IsBlocked(x + dx, y))
                || (dy != 0 && Map.IsBlocked(x, y + dy))
                || (dx != 0 && dy != 0 && Map.IsBlocked(x + dx, y + dy));
        }

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

        /// <summary>Was eine ahnungslose Wache bemerkt: im Sichtkegel oder ganz nah, auch hinter ihr.</summary>
        private Unit? NearestNoticedEnemy(Unit g)
        {
            Unit? best = null;
            int bestDist = int.MaxValue;
            foreach (var o in Units)
            {
                if (o.Alive && o.Faction != g.Faction && Distance(g, o) < bestDist && CanSee(g, o, GuardConeRange)
                    && (Distance(g, o) <= GuardCloseRange || InCone(g, o)))
                {
                    best = o;
                    bestDist = Distance(g, o);
                }
            }
            return best;
        }

        private static bool InCone(Unit g, Unit o) => InCone(g, o.X, o.Y);

        private static bool InCone(Unit g, int x, int y)
        {
            // Halber Öffnungswinkel 45 Grad: Skalarprodukt >= Wurzel(0,5) * |Blick| * |Abstand|.
            long fx = g.FacingX, fy = g.FacingY, dx = x - g.X, dy = y - g.Y;
            long dot = fx * dx + fy * dy;
            return dot > 0 && 2 * dot * dot >= (fx * fx + fy * fy) * (dx * dx + dy * dy);
        }

        /// <summary>Die erste Wache, die einen Agenten auf diesem Feld bemerken würde. Für das Messwerkzeug.</summary>
        public Unit? WatcherOf(int tileX, int tileY)
        {
            int x = tileX * TileUnits + TileUnits / 2, y = tileY * TileUnits + TileUnits / 2;
            foreach (var g in Units)
            {
                if (g.Alive && g.Faction == Faction.Guard && DistanceTo(g, x, y) <= GuardConeRange
                    && Map.HasLineOfSight(g.TileX, g.TileY, tileX, tileY)
                    && (DistanceTo(g, x, y) <= GuardCloseRange || InCone(g, x, y)))
                {
                    return g;
                }
            }
            return null;
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
            Mix(ItemProgress);
            Mix(_reinforceTick);
            Mix(Units.Count);
            foreach (var u in Units)
            {
                Mix(u.X);
                Mix(u.Y);
                Mix(u.Hp);
                Mix(u.Cooldown);
                Mix(u.Path.Count);
                Mix((long)u.GuardState);
                Mix(u.NoticeTicks);
                Mix(u.AlarmTick);
            }
            return h;
        }
    }
}
