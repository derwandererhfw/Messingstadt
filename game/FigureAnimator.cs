using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Wählt das Bild aus dem Figurenblatt: Bewegung, Phase und Blick. Welche Spalten zu welcher Bewegung gehören
    /// und wie lang ein Doppelschritt ist, steht in figures.json, geschrieben von tools/art/figures3d.py.
    /// Gehen und Rennen schalten nach zurückgelegtem Weg weiter, nicht nach Zeit. So gleiten die Füße nicht.
    /// </summary>
    public sealed class FigureAnimator
    {
        private const float IdleFramesPerSecond = 1.6f;
        private const float LookFramesPerSecond = 2.2f;
        private const float LookWindowSeconds = 7f;
        private const int LookChancePercent = 40;
        private const float FallSeconds = 1f;
        private const float RaiseSeconds = 0.22f;
        private const float LowerSeconds = 0.35f;
        private const float AimFramesPerSecond = 1.5f;
        private const int AimHoldTicksStanding = 30;
        private const int AimHoldTicksWalking = 6;
        private const int RecoilTicksPerFrame = 2;
        private const int BiteTicksPerFrame = 2;
        private const float TargetRadiusUnits = 90f;
        private const float DefaultStride = 930f;
        private const int StrikeTicksPerFrame = 2;
        private const float ThrowFramesPerSecond = 11f;
        private const float BandageFramesPerSecond = 4f;
        private const float PickFramesPerSecond = 6f;

        private readonly Dictionary<string, (int Start, int Count)> _columns = new();
        private readonly Dictionary<string, Dictionary<string, float>> _strides = new();
        private readonly Dictionary<string, float> _biteReach = new();
        private readonly Dictionary<int, State> _states = new();
        private readonly HashSet<string> _names = new();

        private readonly Dictionary<string, HashSet<string>> _has = new();
        private readonly List<string> _agents = new();
        private readonly Dictionary<string, Dictionary<string, int>> _variants = new();
        private readonly Dictionary<string, (int Start, int Count)> _civilColumns = new();
        private readonly List<string> _civilians = new();

        /// <summary>Zeilen der Passanten im Figurenblatt, etwa Zivil_Arbeiter.</summary>
        public IReadOnlyList<string> Civilians => _civilians;

        // Agenten mit anderer Waffe in der Hand haben eigene Zeilen im Blatt, siehe AGENTS in generate_concept.py.
        private static readonly Dictionary<string, string> WeaponRows = new()
        {
            ["schrotflinte"] = "_Schrot", ["maschinenpistole"] = "_MP", ["totschlaeger"] = "_Knueppel",
        };

        private sealed class State
        {
            public float Distance;
            public Vector2 LastFoot;
            public bool HasFoot;
            public float Aim;
            public float FallStarted = -1f;
            public int Smokes = -1;
            public float ThrowStarted = -100f;
            public int ThrowX, ThrowY;
            public string Row = "";
            public string RowWeapon = "?";
        }

        /// <summary>Erste Spalte und Bildzahl für Kleintiere und Passanten, die das Blatt mitbenutzen.</summary>
        public static int IdleFirst { get; private set; }
        public static int IdleCount { get; private set; } = 1;
        public static int WalkFirst { get; private set; }
        public static int WalkCount { get; private set; } = 1;

        public void Load(JsonElement root)
        {
            foreach (var n in root.GetProperty("names").EnumerateArray())
            {
                _names.Add(n.GetString()!);
            }
            foreach (var c in root.GetProperty("columns").EnumerateObject())
            {
                _columns[c.Name] = (c.Value[0].GetInt32(), c.Value[1].GetInt32());
            }
            foreach (var figure in root.GetProperty("stride").EnumerateObject())
            {
                var table = new Dictionary<string, float>();
                foreach (var motion in figure.Value.EnumerateObject())
                {
                    table[motion.Name] = motion.Value.GetSingle();
                }
                _strides[figure.Name] = table;
            }
            if (root.TryGetProperty("biteReach", out var bite))
            {
                foreach (var b in bite.EnumerateObject())
                {
                    _biteReach[b.Name] = b.Value.GetSingle();
                }
            }
            foreach (var figure in root.GetProperty("has").EnumerateObject())
            {
                var motions = new HashSet<string>();
                foreach (var m in figure.Value.EnumerateArray())
                {
                    motions.Add(m.GetString()!);
                }
                _has[figure.Name] = motions;
                if (!figure.Name.Contains('_'))
                {
                    _agents.Add(figure.Name);
                }
            }
            foreach (var figure in root.GetProperty("variants").EnumerateObject())
            {
                var rows = new Dictionary<string, int>();
                foreach (var v in figure.Value.EnumerateObject())
                {
                    rows[v.Name] = v.Value.GetInt32();
                }
                _variants[figure.Name] = rows;
            }
            var civil = root.GetProperty("civil");
            foreach (var n in civil.GetProperty("names").EnumerateArray())
            {
                _civilians.Add(n.GetString()!);
            }
            foreach (var c in civil.GetProperty("columns").EnumerateObject())
            {
                _civilColumns[c.Name] = (c.Value[0].GetInt32(), c.Value[1].GetInt32());
            }
            (IdleFirst, IdleCount) = _columns["idle"];
            (WalkFirst, WalkCount) = _columns["walk"];
        }

        public void Clear() => _states.Clear();

        private State StateOf(Unit u)
        {
            if (!_states.TryGetValue(u.Id, out var s))
            {
                s = new State();
                _states[u.Id] = s;
            }
            return s;
        }

        /// <summary>
        /// Zeile im Blatt. Greta, Otto, Emil und Hanna haben eigene Zeilen, dazu je Waffe in der Hand eine weitere.
        /// Wer anders heißt, etwa ein Rekrut, bekommt nach seinem Namen eine der vier Gestalten.
        /// </summary>
        public string SpriteName(Unit u)
        {
            if (u.Faction != Faction.Agent || _agents.Count == 0)
            {
                return u.Sprite;
            }
            var s = StateOf(u);
            string weapon = u.ActiveItem?.Def.Id ?? "";
            if (s.RowWeapon != weapon)
            {
                string who = _names.Contains(u.Name) ? u.Name : _agents[(int)(Hash(u.Name.Length, u.Name.Length > 0 ? u.Name[0] : 0) % (uint)_agents.Count)];
                string row = WeaponRows.TryGetValue(weapon, out var suffix) ? who + suffix : who;
                s.Row = _names.Contains(row) ? row : who;
                s.RowWeapon = weapon;
            }
            return s.Row;
        }

        /// <summary>Ob die Figur keiner der vier ist, sondern ein Rekrut in geliehener Gestalt. Er trägt dann Feldgrau.</summary>
        public bool IsRecruit(Unit u) => u.Faction == Faction.Agent && !_names.Contains(u.Name);

        /// <summary>Zeile in der Palette für eine Farbvariante der Figur, -1 für die Grundfarbe.</summary>
        public int PaletteRow(string name, string variant) =>
            variant.Length > 0 && _variants.TryGetValue(name, out var rows) && rows.TryGetValue(variant, out int row) ? row : -1;

        /// <summary>Namen der Farbvarianten einer Figur, ohne die Grundfarbe.</summary>
        public IEnumerable<string> Variants(string name) => _variants.TryGetValue(name, out var rows) ? rows.Keys : System.Array.Empty<string>();

        /// <summary>Spalte für eine Bewegung der Passanten: walk, run, idle, smoke, startle, cower, read. frame läuft im Kreis.</summary>
        public int CivilColumn(string motion, int frame)
        {
            var (start, count) = _civilColumns[motion];
            return start + Mathf.PosMod(frame, count);
        }

        /// <summary>Bildzahl einer Bewegung der Passanten.</summary>
        public int CivilFrames(string motion) => _civilColumns[motion].Count;

        /// <summary>Weg je Zyklus in Spieleinheiten für walk oder run einer Figur. Wer danach weiterschaltet, gleitet nicht.</summary>
        public float StrideOf(string name, string motion) =>
            _strides.TryGetValue(name, out var table) && table.TryGetValue(motion, out float stride) ? stride : DefaultStride;

        /// <summary>Ob es diese Bewegung für die Figur im Blatt gibt. Manche gibt es nur für Agenten oder nur mit einer Waffe.</summary>
        private bool Has(Unit u, string motion) => _has.TryGetValue(SpriteName(u), out var motions) && motions.Contains(motion);

        private static bool AtLock(Unit u) => u.Faction == Faction.Agent && u.DoorTarget >= 0 && !u.Moving;

        private static bool Melee(Unit u) => u.ActiveItem?.Def.Kind == ItemKind.Melee;

        private static int SmokeCharges(Unit u)
        {
            int count = 0;
            foreach (var item in u.Items)
            {
                if (item.Def.Kind == ItemKind.Smoke)
                {
                    count += System.Math.Max(1, item.Charges);
                }
            }
            return count;
        }

        /// <summary>Wohin die Figur blickt: beim Zielen aufs Ziel, beim Wurf zur Wolke, sonst in Gehrichtung.</summary>
        public (int X, int Y) Look(World world, Unit u, float now)
        {
            var s = StateOf(u);
            if (u.Alive && AtLock(u) && u.DoorTarget < world.Doors.Count)
            {
                var door = world.Doors[u.DoorTarget];
                return (door.MarkX - u.X, door.MarkY - u.Y);
            }
            if (u.Alive && now - s.ThrowStarted < ThrowSeconds)
            {
                return (s.ThrowX - u.X, s.ThrowY - u.Y);
            }
            return Aiming(u) && !Melee(u) ? (u.AimX, u.AimY) : (u.FacingX, u.FacingY);
        }

        private float ThrowSeconds => _columns.TryGetValue("throw", out var c) ? c.Count / ThrowFramesPerSecond : 0f;

        /// <summary>Ein Rauchtopf weniger in der Tasche heißt: Er ist gerade geworfen worden. Die Wolke steht im Kern sofort.</summary>
        private void WatchThrow(World world, Unit u, State s, float now)
        {
            int smokes = SmokeCharges(u);
            if (s.Smokes > smokes && world.Smokes.Count > 0)
            {
                s.ThrowStarted = now;
                (s.ThrowX, s.ThrowY) = (world.Smokes[^1].X, world.Smokes[^1].Y);
            }
            s.Smokes = smokes;
        }

        private float Stride(Unit u, string motion) =>
            _strides.TryGetValue(SpriteName(u), out var table) && table.TryGetValue(motion, out float stride) ? stride : DefaultStride;

        private int Column(string motion, int frame)
        {
            var (start, count) = _columns[motion];
            return start + Mathf.Clamp(frame, 0, count - 1);
        }

        private int Cycle(string motion, float progress)
        {
            var (start, count) = _columns[motion];
            return start + Mathf.PosMod((int)(progress * count), count);
        }

        /// <summary>Weg seit dem letzten Bild aufsummieren, in Spieleinheiten. foot ist die geglättete Lage im Bild.</summary>
        public void Track(Unit u, Vector2 foot)
        {
            var s = StateOf(u);
            if (s.HasFoot)
            {
                // Zurück vom Bild auf die Karte: Eine Feldkante ist im Bild 32 × 16 Pixel.
                var d = foot - s.LastFoot;
                float a = d.X / 32f, b = d.Y / 16f;
                float x = (a + b) / 2f, y = (b - a) / 2f;
                s.Distance += Mathf.Sqrt(x * x + y * y) * World.TileUnits;
            }
            s.LastFoot = foot;
            s.HasFoot = true;
        }

        /// <summary>Gesetzte Schritte, damit das Geräusch zum Aufsetzen der Füße passt.</summary>
        public int StepsTaken(Unit u) => (int)(StateOf(u).Distance / (Stride(u, u.Sprinting ? "run" : "walk") / 2f));

        /// <summary>Ob die Figur die Waffe gerade hebt, hält oder senkt. Bestimmt auch die Blickrichtung.</summary>
        public bool Aiming(Unit u) => u.Alive && StateOf(u).Aim > 0f;

        private static bool WantsAim(World world, Unit u)
        {
            if (!u.Alive || u.Type.Reports)
            {
                return false;
            }
            int sinceShot = world.Tick - u.LastFiredTick;
            if (sinceShot <= (u.Moving ? AimHoldTicksWalking : AimHoldTicksStanding))
            {
                return true;
            }
            if (u.Moving)
            {
                return false;
            }
            // Wer ein Ziel hat oder im Gefecht steht, hebt die Waffe schon vor dem ersten Schuss.
            bool target = u.TargetId >= 0 && u.TargetId < world.Units.Count && world.Units[u.TargetId].Alive;
            return target || (u.Faction == Faction.Guard && u.GuardState == GuardState.Combat);
        }

        /// <summary>Spalte im Blatt für diese Figur jetzt. started meldet den Beginn eines Sturzes.</summary>
        public int Column(World world, Unit u, float delta, float now, out bool started)
        {
            var s = StateOf(u);
            started = false;
            if (!u.Alive)
            {
                if (s.FallStarted < 0f)
                {
                    s.FallStarted = now;
                    started = true;
                }
                // Nicht alle sterben gleich: Die Id bestimmt, ob die Figur nach vorn oder zur Seite sackt.
                string fall = u.Id % 2 == 0 ? "fallA" : "fallB";
                return Column(fall, (int)((now - s.FallStarted) / FallSeconds * _columns[fall].Count));
            }
            WatchThrow(world, u, s, now);
            if (u.BandageTicks > 0 && Has(u, "bandage"))
            {
                return Cycle("bandage", now * BandageFramesPerSecond / _columns["bandage"].Count);
            }
            int sinceStrike = world.Tick - u.LastStrikeTick;
            if (Has(u, "strike") && sinceStrike >= 0 && sinceStrike < _columns["strike"].Count * StrikeTicksPerFrame)
            {
                return Column("strike", sinceStrike / StrikeTicksPerFrame);
            }
            if (now - s.ThrowStarted < ThrowSeconds && Has(u, "throw"))
            {
                return Column("throw", (int)((now - s.ThrowStarted) * ThrowFramesPerSecond));
            }
            if (AtLock(u) && Has(u, "pick"))
            {
                return Cycle("pick", now * PickFramesPerSecond / _columns["pick"].Count);
            }
            if (u.ReloadTicks > 0 && !u.Moving && Has(u, "reload") && u.ActiveItem is { } loading)
            {
                float done = 1f - u.ReloadTicks / (float)System.Math.Max(1, loading.Def.ReloadTicks);
                return Column("reload", (int)(done * _columns["reload"].Count));
            }
            bool wants = WantsAim(world, u);
            s.Aim = Mathf.Clamp(s.Aim + (wants ? delta / RaiseSeconds : -delta / LowerSeconds), 0f, 1f);
            int sinceShot = world.Tick - u.LastFiredTick;
            if (u.Moving)
            {
                // Ein alarmierter Hund rennt, er kennt kein Gehen mehr. Der Laufbursche auch.
                bool running = u.Sprinting || ((u.Type.Bites || u.Type.Reports) && u.Alerted);
                // Wer mit dem Knüppel geht, schleicht: geduckt, den Knüppel hinter dem Rücken.
                bool sneaking = !running && s.Aim <= 0.5f && Melee(u) && Has(u, "sneak");
                string motion = running ? "run" : sneaking ? "sneak" : s.Aim > 0.5f && !u.Type.Bites ? "aimwalk" : "walk";
                return Cycle(motion, s.Distance / Stride(u, motion));
            }
            if (s.Aim >= 1f)
            {
                int ticksPerFrame = u.Type.Bites ? BiteTicksPerFrame : RecoilTicksPerFrame;
                if (sinceShot < _columns["recoil"].Count * ticksPerFrame)
                {
                    return Column("recoil", sinceShot / ticksPerFrame);
                }
                return Cycle("aim", now * AimFramesPerSecond / _columns["aim"].Count + u.Id * 0.37f);
            }
            if (s.Aim > 0f)
            {
                return Column("raise", (int)(s.Aim * _columns["raise"].Count));
            }
            // Ab und zu sieht sich eine Figur um. Fenster und Zufall hängen an der Id, damit nicht alle zugleich schauen.
            float window = now / LookWindowSeconds + u.Id * 0.618f;
            int slot = (int)window;
            float into = (window - slot) * LookWindowSeconds;
            float lookLength = _columns["look"].Count / LookFramesPerSecond;
            if ((int)(Hash(u.Id, slot) % 100) < LookChancePercent && into < lookLength)
            {
                return Column("look", (int)(into * LookFramesPerSecond));
            }
            return Cycle("idle", (now * IdleFramesPerSecond + u.Id * 2.7f) / _columns["idle"].Count);
        }

        /// <summary>Beim Biss springt der Hund das letzte Stück aufs Ziel zu. Versatz im Bild, die Spiellogik bleibt.</summary>
        public Vector2 BiteOffset(World world, Unit u, Main game)
        {
            if (!u.Alive || !u.Type.Bites || u.Moving || !_biteReach.TryGetValue(u.Sprite, out float reach))
            {
                return Vector2.Zero;
            }
            int sinceShot = world.Tick - u.LastFiredTick;
            int frames = _columns["recoil"].Count;
            if (sinceShot >= frames * BiteTicksPerFrame || u.AimX == 0 && u.AimY == 0)
            {
                return Vector2.Zero;
            }
            float dist = Mathf.Sqrt((float)u.AimX * u.AimX + (float)u.AimY * u.AimY);
            float gap = Mathf.Max(0f, dist - reach - TargetRadiusUnits);
            // Hin und wieder zurück: im mittleren Bild am weitesten.
            float t = (sinceShot + 1f) / (frames * BiteTicksPerFrame);
            float amount = gap * Mathf.Sin(Mathf.Pi * Mathf.Min(1f, t * 1.15f));
            var target = game.ToScreen(u.X + (int)(u.AimX / dist * amount), u.Y + (int)(u.AimY / dist * amount));
            return target - game.ToScreen(u.X, u.Y);
        }

        private static uint Hash(int a, int b)
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            return h ^ (h >> 15);
        }
    }
}
