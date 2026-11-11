using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Leben hinter den Scheiben: Fenster gehen an und aus, jemand läuft vorbei, in manchen flimmert der Bildfunk,
    /// und das Hotelschild hat einen Wackelkontakt. Bei Schüssen löscht die Nachbarschaft ihr Licht und lugt hinaus.
    /// Jedes Fenster und jeder Buchstabe hat in der Grafik eine Kennung (Lichtkarte _w), sein Zustand steht in einer
    /// kleinen Textur, die der Shader liest. Reine Darstellung, das Licht für die Wachen bleibt unverändert.
    /// </summary>
    public partial class WindowLife : Node
    {
        public const int MaxPanes = 256;

        private const float SomeoneHomeShare = 0.8f;
        private const float MinToggleSeconds = 25f;
        private const float MaxToggleSeconds = 120f;
        private const float MinPictureSeconds = 2f;
        private const float MaxPictureSeconds = 7f;
        private const float PictureBlendPerSecond = 2.5f;
        private const float ScreenLightEnergy = 0.5f;
        private const float PasserChancePerSecond = 0.02f;
        private const float BrokenLetterStutterPerSecond = 0.5f;
        private const float SignDropoutPerSecond = 0.03f;
        private const float HushRadius = 520f;
        private const float HushSpreadSpeed = 280f;
        private const float HushShare = 0.85f;
        private const float PeekShare = 0.4f;
        private const float MinQuietSeconds = 30f;
        private const float MaxQuietSeconds = 60f;
        private static readonly Color PeekPane = new(0.26f, 0.17f, 0.09f, 1f);
        private static readonly Color DarkPane = new(0.09f, 0.12f, 0.16f, 1f);
        private static readonly Color PasserPane = new(0.13f, 0.09f, 0.06f, 1f);
        // Zugezogener Vorhang: Das Licht dahinter brennt weiter, nach außen dringt nur ein matter, warmer Schein.
        private static readonly Color CurtainPane = new(0.3f, 0.17f, 0.1f, 1f);
        private const float ShadowLeadSeconds = 0.7f;
        private static readonly Color CandleColor = new(0.62f, 0.36f, 0.15f);
        private static readonly Color NeonColor = new(1f, 0.28f, 0.2f, 1f);
        private static readonly Color DeadLetter = new(0.28f, 0.2f, 0.16f, 1f);
        private static readonly Color Unchanged = new(0f, 0f, 0f, 0f);

        private enum PaneKind
        {
            Window,
            Toggle,
            Weld,
            Dark,
            Candle,
            Screen,
            Letter,
        }

        private sealed class Pane
        {
            public int Id;
            public PaneKind Kind;
            public Vector2 Center;
            public bool On = true;
            public float NextToggle;
            public float PasserUntil;
            public Color Picture;
            public Color PictureTarget;
            public float NextCut;
            public PointLight2D? Light;
            public CpuParticles2D? Sparks;
            public bool Crackling;
            public bool StaysLit;
            public float DarkAt = -1f;
            public float BackAt;
            public float PeekAt = -1f;
            public float PeekUntil;
            public float CurtainAt = -1f;
        }

        private readonly Random _rng = new();
        private readonly List<Pane> _panes = new();
        private readonly Image _states = Image.CreateEmpty(MaxPanes, 1, false, Image.Format.Rgba8);
        private PointLight2D? _signLight;
        private Pane? _brokenLetter;
        private float _time;
        private float _stutterUntil;
        private float _dropoutUntil;
        private float _nextStutter;
        private World? _seenWorld;
        private int _lastShotTick;
        private bool _alarmRunning;
        private int _hushCount;
        private int _hushable;

        public Main? Game;

        /// <summary>Wie still die Nachbarschaft gerade ist, von 0 bis 1. Auch für Geräusche gedacht.</summary>
        public float Hush => _hushable == 0 ? 0f : (float)_hushCount / _hushable;

        public ImageTexture States { get; }

        /// <summary>Art und Lage aller Fenster und Buchstaben, etwa für Geräusche.</summary>
        public List<(string Kind, Vector2 Center)> PaneSpots { get; } = new();

        /// <summary>Kennung, Art und Lage aller Fenster und Buchstaben, etwa für die Spiegelung im Pflaster.</summary>
        public IEnumerable<(int Id, string Kind, Vector2 Center)> AllPanes
        {
            get
            {
                for (int i = 0; i < _panes.Count; i++)
                {
                    yield return (_panes[i].Id, PaneSpots[i].Kind, _panes[i].Center);
                }
            }
        }

        /// <summary>Wie ein Fenster gerade aussieht. Alpha 0 heißt: wie im vorberechneten Bild.</summary>
        public Color PaneLook(int id) => _states.GetPixel(id, 0);

        /// <summary>Wo das Schweißlicht der Halle steht und wie hell es gerade ist. Energie 0, wenn nicht geschweißt wird.</summary>
        public (Vector2 At, float Energy) WeldLight
        {
            get
            {
                var weld = _panes.Find(p => p.Kind == PaneKind.Weld && p.Light != null);
                return weld == null ? (Vector2.Zero, 0f) : (weld.Light!.Position, weld.Light.Energy);
            }
        }

        /// <summary>Mitte des Leuchtschilds, null ohne Schild.</summary>
        public Vector2? SignCenter
        {
            get
            {
                var letters = PaneSpots.FindAll(p => p.Kind == "sign");
                if (letters.Count == 0)
                {
                    return null;
                }
                var sum = Vector2.Zero;
                foreach (var (_, center) in letters)
                {
                    sum += center;
                }
                return sum / letters.Count;
            }
        }

        public Texture2D Glow = null!;
        public Node2D LightParent = null!;
        public SoundScape? Sound;

        public WindowLife()
        {
            States = ImageTexture.CreateFromImage(_states);
        }

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        /// <summary>Meldet ein Fenster oder einen Buchstaben an. kind: window, tv oder sign.</summary>
        public void AddPane(int id, string kind, Vector2 center)
        {
            PaneSpots.Add((kind, center));
            var pane = new Pane
            {
                Id = id, Center = center,
                Kind = kind switch
                {
                    "tv" => PaneKind.Screen,
                    "sign" => PaneKind.Letter,
                    "dark" => PaneKind.Dark,
                    "candle" => PaneKind.Candle,
                    "toggle" => PaneKind.Toggle,
                    "weld" => PaneKind.Weld,
                    _ => PaneKind.Window,
                },
                On = _rng.NextDouble() < SomeoneHomeShare,
                NextToggle = Rand(MinToggleSeconds, MaxToggleSeconds),
                StaysLit = _rng.NextDouble() >= HushShare,
            };
            _panes.Add(pane);
        }

        public override void _Ready()
        {
            var letters = new List<Pane>();
            var signCenter = Vector2.Zero;
            foreach (var pane in _panes)
            {
                if (pane.Kind == PaneKind.Screen)
                {
                    // Das bläuliche Flimmern fällt durchs Fenster auf Wand und Boden davor.
                    pane.On = true;
                    pane.Light = new PointLight2D
                    {
                        Texture = Glow, TextureScale = 0.34f, Color = new Color(0.5f, 0.68f, 1f),
                        RangeZMin = -1, RangeZMax = 4096, Position = pane.Center + new Vector2(0, 8),
                    };
                    LightParent.AddChild(pane.Light);
                }
                else if (pane.Kind == PaneKind.Weld)
                {
                    pane.Light = new PointLight2D
                    {
                        Texture = Glow, TextureScale = 0.5f, Color = new Color(0.55f, 0.75f, 1f), Energy = 0f,
                        RangeZMin = -1, RangeZMax = 4096, Position = pane.Center + new Vector2(0, 12),
                    };
                    LightParent.AddChild(pane.Light);
                    pane.Sparks = new CpuParticles2D
                    {
                        Position = pane.Center + new Vector2(0, 12), ZIndex = 3000, Emitting = false, Amount = 24, Lifetime = 0.7f,
                        Direction = new Vector2(0.2f, 1f), Spread = 35f, Gravity = new Vector2(0f, 240f),
                        InitialVelocityMin = 20f, InitialVelocityMax = 70f, ScaleAmountMin = 1f, ScaleAmountMax = 1f,
                        Color = new Color(4f, 3f, 1.6f),
                    };
                    LightParent.AddChild(pane.Sparks);
                }
                else if (pane.Kind == PaneKind.Letter)
                {
                    letters.Add(pane);
                    signCenter += pane.Center;
                }
            }
            if (letters.Count > 0)
            {
                _brokenLetter = letters[letters.Count / 2 + 1];
                _signLight = new PointLight2D
                {
                    Texture = Glow, TextureScale = 0.6f, Color = new Color(1f, 0.3f, 0.22f), Energy = 0.55f,
                    RangeZMin = -1, RangeZMax = 4096, Position = signCenter / letters.Count + new Vector2(0, 14),
                };
                LightParent.AddChild(_signLight);
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            UpdateSign(dt);
            WatchForTrouble();
            _hushCount = 0;
            _hushable = 0;
            foreach (var pane in _panes)
            {
                var look = pane.Kind switch
                {
                    PaneKind.Screen => UpdateScreen(pane, dt),
                    PaneKind.Dark => DarkPane,
                    PaneKind.Weld => UpdateWeld(pane),
                    PaneKind.Candle => UpdateCandle(pane),
                    PaneKind.Letter => pane == _brokenLetter && _time < _stutterUntil && _rng.NextDouble() < 0.5 || _time < _dropoutUntil ? DeadLetter : NeonColor,
                    _ => UpdateWindow(pane, dt),
                };
                _states.SetPixel(pane.Id, 0, Hushable(pane) ? Hushed(pane, look) : look);
            }
            States.Update(_states);
        }

        // Geschweißt wird weiter, und das Schild brennt weiter: Nur hinter den gewöhnlichen Fenstern geht das Licht aus.
        private static bool Hushable(Pane pane) => pane.Kind is PaneKind.Window or PaneKind.Toggle or PaneKind.Screen or PaneKind.Candle;

        /// <summary>Hört die Nachbarschaft Schüsse oder Alarm? Beides lässt dort das Licht ausgehen.</summary>
        private void WatchForTrouble()
        {
            if (Game == null)
            {
                return;
            }
            var world = Game.World;
            if (world != _seenWorld)
            {
                _seenWorld = world;
                _lastShotTick = 0;
                _alarmRunning = false;
                foreach (var pane in _panes)
                {
                    pane.DarkAt = -1f;
                    pane.PeekAt = -1f;
                    pane.CurtainAt = -1f;
                }
            }
            if (world.Shots.Count > 0 && world.Shots[^1].Tick > _lastShotTick)
            {
                var shot = world.Shots[^1];
                _lastShotTick = shot.Tick;
                Alarmed(Game.ToScreen(shot.FromX, shot.FromY));
            }
            bool running = false;
            foreach (var u in world.Units)
            {
                if (u.Alive && u.AlarmTick > 0)
                {
                    running = true;
                    if (!_alarmRunning)
                    {
                        Alarmed(Game.ToScreen(u.AlarmX, u.AlarmY));
                    }
                    break;
                }
            }
            _alarmRunning = running;
        }

        private void Alarmed(Vector2 at)
        {
            foreach (var pane in _panes)
            {
                if (!Hushable(pane))
                {
                    continue;
                }
                float distance = pane.Center.DistanceTo(at);
                if (pane.StaysLit)
                {
                    // Wer das Licht anlässt, zieht wenigstens den Vorhang zu.
                    if (pane.CurtainAt >= 0f)
                    {
                        pane.BackAt = Mathf.Max(pane.BackAt, _time + Rand(MinQuietSeconds, MaxQuietSeconds));
                    }
                    else if (distance < HushRadius && pane.Kind is PaneKind.Window or PaneKind.Toggle)
                    {
                        pane.CurtainAt = _time + distance / HushSpreadSpeed + Rand(0.6f, 2.5f);
                        pane.BackAt = _time + Rand(MinQuietSeconds, MaxQuietSeconds);
                    }
                    continue;
                }
                if (pane.DarkAt >= 0f)
                {
                    // Wer schon im Dunkeln sitzt, wartet länger, solange es weiter knallt.
                    pane.BackAt = Mathf.Max(pane.BackAt, _time + Rand(MinQuietSeconds, MaxQuietSeconds));
                }
                else if (distance < HushRadius)
                {
                    pane.DarkAt = _time + distance / HushSpreadSpeed + Rand(0.1f, 1.2f);
                    pane.BackAt = _time + Rand(MinQuietSeconds, MaxQuietSeconds);
                    pane.PeekAt = _rng.NextDouble() < PeekShare ? pane.DarkAt + Rand(2f, 7f) : -1f;
                    pane.PeekUntil = pane.PeekAt + Rand(1.2f, 3f);
                }
            }
        }

        private Color Hushed(Pane pane, Color look)
        {
            _hushable++;
            if (pane.CurtainAt >= 0f)
            {
                if (_time >= pane.BackAt)
                {
                    pane.CurtainAt = -1f;
                    return look;
                }
                // Ein dunkles Fenster bleibt dunkel. Sonst tritt erst jemand ans Fenster, dann fällt der Vorhang.
                return look == DarkPane || _time < pane.CurtainAt - ShadowLeadSeconds ? look : _time < pane.CurtainAt ? PasserPane : CurtainPane;
            }
            if (pane.DarkAt < 0f)
            {
                return look;
            }
            if (_time < pane.DarkAt)
            {
                // Kurz bevor das Licht ausgeht, steht ein Schatten am Fenster.
                return _time >= pane.DarkAt - ShadowLeadSeconds && look != DarkPane ? PasserPane : look;
            }
            if (_time >= pane.BackAt)
            {
                pane.DarkAt = -1f;
                return look;
            }
            _hushCount++;
            if (pane.Light != null)
            {
                pane.Light.Energy = 0f;
            }
            return pane.PeekAt >= 0f && _time >= pane.PeekAt && _time < pane.PeekUntil ? PeekPane : DarkPane;
        }

        private Color UpdateWindow(Pane pane, float dt)
        {
            if (pane.Kind == PaneKind.Toggle && _time > pane.NextToggle)
            {
                pane.On = !pane.On;
                pane.NextToggle = _time + Rand(MinToggleSeconds, MaxToggleSeconds);
            }
            if (pane.Kind == PaneKind.Toggle && !pane.On)
            {
                return DarkPane;
            }
            if (_time > pane.PasserUntil && _rng.NextDouble() < PasserChancePerSecond * dt)
            {
                pane.PasserUntil = _time + Rand(0.8f, 2.2f);
            }
            return _time < pane.PasserUntil ? PasserPane : Unchanged;
        }

        /// <summary>Hinter den Hallenfenstern wird geschweißt: Serien grellblauer Blitze, dazwischen lange Pausen.</summary>
        private Color UpdateWeld(Pane pane)
        {
            if (pane.Sparks == null || pane.Light == null)
            {
                return Unchanged;
            }
            if (_time > pane.NextCut)
            {
                bool start = _time > pane.PasserUntil;
                pane.NextCut = start ? _time + Rand(1.2f, 3.5f) : _time + Rand(7f, 22f);
                pane.PasserUntil = start ? pane.NextCut : 0f;
            }
            bool welding = _time < pane.PasserUntil;
            bool arc = welding && _rng.NextDouble() < 0.55;
            pane.Sparks.Emitting = welding;
            pane.Light.Energy = arc ? Rand(0.5f, 1.1f) : 0f;
            if (Sound != null && welding != pane.Crackling)
            {
                pane.Crackling = welding;
                if (welding)
                {
                    Sound.Play("weld", pane.Center, -17f, 500f, 0.05f);
                }
            }
            return arc ? new Color(0.72f, 0.86f, 1f, 1f) : Unchanged;
        }

        /// <summary>Kerzenlicht: unruhig, aber langsam, und nie ganz aus.</summary>
        private Color UpdateCandle(Pane pane)
        {
            float flame = 0.8f + 0.14f * Mathf.Sin(_time * 3.1f + pane.Id) + 0.06f * Mathf.Sin(_time * 7.7f + pane.Id * 2f);
            return new Color(CandleColor.R * flame, CandleColor.G * flame, CandleColor.B * flame, 1f);
        }

        /// <summary>Gedämpftes Bild, das alle paar Sekunden weich wechselt, dazu ein kaum sichtbares Zittern der Röhre.</summary>
        private Color UpdateScreen(Pane pane, float dt)
        {
            if (_time > pane.NextCut)
            {
                float level = Rand(0.2f, 0.5f);
                pane.PictureTarget = new Color(level * Rand(0.55f, 0.72f), level * Rand(0.75f, 0.88f), level);
                pane.NextCut = _time + Rand(MinPictureSeconds, MaxPictureSeconds);
            }
            pane.Picture = pane.Picture.Lerp(pane.PictureTarget, Mathf.Min(1f, PictureBlendPerSecond * dt));
            var c = pane.Picture * (0.98f + 0.02f * Mathf.Sin(_time * 18f + pane.Id));
            if (pane.Light != null)
            {
                pane.Light.Energy = c.B * ScreenLightEnergy;
            }
            return new Color(c.R, c.G, c.B, 1f);
        }

        private void UpdateSign(float dt)
        {
            if (_time > _nextStutter)
            {
                _stutterUntil = _time + Rand(0.4f, 1.4f);
                _nextStutter = _stutterUntil + Rand(0f, 2f / BrokenLetterStutterPerSecond);
            }
            if (_time > _dropoutUntil && _rng.NextDouble() < SignDropoutPerSecond * dt)
            {
                _dropoutUntil = _time + Rand(0.1f, 0.5f);
            }
            if (_signLight != null)
            {
                _signLight.Energy = _time < _dropoutUntil ? 0.05f : 0.55f;
            }
        }
    }
}
