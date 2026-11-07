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

        private readonly Dictionary<string, (int Start, int Count)> _columns = new();
        private readonly Dictionary<string, Dictionary<string, float>> _strides = new();
        private readonly Dictionary<string, float> _biteReach = new();
        private readonly Dictionary<int, State> _states = new();

        private sealed class State
        {
            public float Distance;
            public Vector2 LastFoot;
            public bool HasFoot;
            public float Aim;
            public float FallStarted = -1f;
        }

        /// <summary>Erste Spalte und Bildzahl für Kleintiere und Passanten, die das Blatt mitbenutzen.</summary>
        public static int IdleFirst { get; private set; }
        public static int IdleCount { get; private set; } = 1;
        public static int WalkFirst { get; private set; }
        public static int WalkCount { get; private set; } = 1;

        public void Load(JsonElement root)
        {
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

        private float Stride(Unit u, string motion) =>
            _strides.TryGetValue(u.Sprite, out var table) && table.TryGetValue(motion, out float stride) ? stride : DefaultStride;

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
            bool wants = WantsAim(world, u);
            s.Aim = Mathf.Clamp(s.Aim + (wants ? delta / RaiseSeconds : -delta / LowerSeconds), 0f, 1f);
            int sinceShot = world.Tick - u.LastFiredTick;
            if (u.Moving)
            {
                // Ein alarmierter Hund rennt, er kennt kein Gehen mehr. Der Laufbursche auch.
                bool running = u.Sprinting || ((u.Type.Bites || u.Type.Reports) && u.Alerted);
                string motion = running ? "run" : s.Aim > 0.5f && !u.Type.Bites ? "aimwalk" : "walk";
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
