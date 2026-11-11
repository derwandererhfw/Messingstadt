using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Leben über dem Startbildschirm. Ein steter, müder Strom von Arbeitern zieht als Schattenrisse die Straße hinunter
    /// zum Werkstor, vor dem Licht des Tors und der Laternen. Die Linie 7 quert vor dem Werk, am Fahrdraht sprühen
    /// Funken. In den Häusern geht Licht an und aus, die Reklame flackert, eine Laterne pendelt, Wäsche weht. Rauch,
    /// Dampf und Atem ziehen, im Werk zuckt Schweißlicht, am Tor glüht der Ofen. Der Regen kommt in Böen, es tropft von
    /// den Traufen, in den Pfützen laufen Ringe, und ab und zu zeigt ein Wetterleuchten die ganze Straße. Dazwischen
    /// kleine Ereignisse: ein Wachmann mit Hund, ein Luftschiff, eine Katze, ein Flugblatt im Wind.
    /// Alles liegt in Ebenen über dem stehenden Titelbild. Wo was liegt, steht in TitleLayout. Links, wo das Menü steht,
    /// bewegt sich nur der Regen.
    /// </summary>
    public partial class TitleAmbience : Control
    {
        private const string LayoutPath = "res://assets/title/title.json";

        // Regen und Wetter
        private const int Drops = 190;
        private const float FallSpeed = 540f;
        private const float CalmDrift = 70f;
        private const float GustDrift = 330f;
        private const float DropLength = 14f;
        private const float RainAlpha = 0.2f;
        private const float GustRainBoost = 0.7f;
        private const float GustChangePerSecond = 0.5f;
        private const float MinStormPause = 18f;
        private const float MaxStormPause = 40f;
        private const float FirstStorm = 12f;
        private const float FlashFadePerSecond = 5f;
        private const float SkyFlashAlpha = 0.34f;
        private const float StreetFlashAlpha = 0.2f;
        private const float EdgeDark = 0.6f;
        private const float OverallDark = 0.1f;
        private static readonly Color RainColor = new(0.78f, 0.84f, 1f);
        private static readonly Color LampColor = new(0.95f, 0.9f, 0.82f);
        private static readonly Color FlashColor = new(0.62f, 0.7f, 0.95f);
        private static readonly Color SmokeColor = new(0.46f, 0.49f, 0.57f);
        private static readonly Color SteamColor = new(0.62f, 0.66f, 0.74f);
        private static readonly Color FogColor = new(0.5f, 0.56f, 0.7f);

        // Rauch, Dampf, Atem
        private const float SmokePerSecond = 2.4f;
        private const float SmokeSeconds = 11f;
        private const float SmokeAlpha = 0.17f;
        private const float SteamPerSecond = 2.2f;
        private const float SteamSeconds = 4.5f;
        private const float SteamAlpha = 0.13f;
        private const float BreathZ = 2.4f;
        private const float BreathSeconds = 1.3f;
        private const int FogBanks = 5;
        private const float FogAlpha = 0.06f;
        private const float FogDrift = 9f;

        // Arbeiter
        private const float MinStartZ = 0.6f;
        private const float MaxStartZ = 0.74f;
        private const float GateFadeZ = 0.7f;
        private const float MinPace = 0.125f;
        private const float MaxPace = 0.16f;
        private const float StepsPerZ = 15f;
        private const float MinGroupPause = 0.9f;
        private const float MaxGroupPause = 4.2f;
        private const float LullChance = 0.12f;
        private const int MaxWorkers = 46;
        private const float BikeShare = 0.12f;
        private const float MirrorAlpha = 0.3f;
        private const float MirrorSquash = 0.62f;
        private const float SimpleFromZ = 3.2f;
        private const float MirrorUntilZ = 2.6f;
        private const float RimShift = 0.014f;
        private static readonly Color Shadow = new(0.012f, 0.014f, 0.02f);
        private static readonly Color FarShadow = new(0.03f, 0.036f, 0.05f);
        private static readonly Color Rim = new(0.2f, 0.22f, 0.28f);

        // Licht, vor dem die Gestalten stehen
        private const float PoolAlpha = 0.17f;
        private const float PoolWidth = 360f;
        private const float ForecourtAlpha = 0.16f;
        private static readonly Color ForecourtColor = new(0.55f, 0.66f, 0.9f);
        private const float GlintAlpha = 0.08f;
        private const float SwingPixels = 7f;
        private const float SwingPerSecond = 1.1f;

        // Fenster, Reklame, Werk
        private const float MinToggleSeconds = 9f;
        private const float MaxToggleSeconds = 45f;
        private const float SwitchSeconds = 0.18f;
        private const float OffAlpha = 0.94f;
        private const float PasserChancePerSecond = 0.03f;
        private const float PasserShade = 0.7f;
        private const float StartDarkShare = 0.3f;
        private const float MaskFromLight = 0.2f;
        private const float MaskFullLight = 0.42f;
        private static readonly Color DarkGlass = new(0.075f, 0.078f, 0.088f);
        private static readonly Color WeldColor = new(0.6f, 0.78f, 1f);
        private const float WeldAlpha = 0.6f;
        private const float WeldHalo = 260f;
        private const float MinWeldPause = 3f;
        private const float MaxWeldPause = 9f;
        private static readonly Color FurnaceColor = new(1f, 0.46f, 0.16f);
        private const float FurnaceRest = 0.14f;
        private const float FurnaceSurge = 0.42f;
        private const float FurnaceRadius = 120f;
        private const float MinSurgePause = 10f;
        private const float MaxSurgePause = 24f;
        private const float SurgeSeconds = 4f;

        // Straßenbahn
        private const float TramSpeed = 62f;
        private const float FirstTram = 7f;
        private const float MinTramPause = 34f;
        private const float MaxTramPause = 62f;
        private static readonly Color TramBody = new(0.03f, 0.03f, 0.035f);
        private static readonly Color TramLight = new(0.82f, 0.74f, 0.58f);
        private static readonly Color SparkColor = new(0.7f, 0.85f, 1f);

        // Kleine Ereignisse
        private const float FirstEvent = 4f;
        private const float MinEventPause = 9f;
        private const float MaxEventPause = 20f;
        private const float AirshipSeconds = 46f;
        private const float GuardSeconds = 26f;
        private const float CatSeconds = 2.6f;
        private const float LeafletSeconds = 7f;
        private const int Drips = 10;
        private const float RingsPerSecond = 3.2f;
        private const float RingSeconds = 1.1f;

        private sealed class Puff
        {
            public Vector2 Pos;
            public Vector2 Drift;
            public float Age;
            public float Life;
            public float Size;
            public float Grow;
            public Color Color;
        }

        private sealed class Worker
        {
            public float Z;
            public float Lane;
            public float EndLane;
            public float Pace;
            public float Phase;
            public float Tall;
            public float Stoop;
            public bool Pail;
            public bool Bike;
            public float NextBreath;
        }

        private sealed class Pane
        {
            public Rect2 Area;
            public bool On = true;
            public float Dark;
            public float NextToggle;
            public float PasserFrom = -10f;
            public float PasserSeconds = 1f;
            public bool Flickers;
            public Texture2D? Mask;
        }

        private sealed class Drip
        {
            public int Eave;
            public float Fall = -1f;
            public float Wait;
            public float Shift;
        }

        private sealed class Ring
        {
            public Vector2 At;
            public float Age;
            public float Size;
        }

        /// <summary>Eine Ebene der Szene. Die Reihenfolge der Ebenen entscheidet, was vor wem liegt.</summary>
        private sealed partial class Layer : Control
        {
            public Action<CanvasItem> Paint = _ => { };

            public override void _Draw() => Paint(this);
        }

        private readonly Random _rng = new();
        private readonly Vector2[] _drops = new Vector2[Drops];
        private readonly Vector2[] _lines = new Vector2[Drops * 2];
        private readonly List<Puff> _puffs = new();
        private readonly List<float> _strokes = new();
        private readonly List<Worker> _workers = new();
        private readonly List<Pane> _panes = new();
        private readonly List<Drip> _drips = new();
        private readonly List<Ring> _rings = new();
        private readonly float[] _fog = new float[FogBanks];
        private readonly List<Layer> _layers = new();
        private TitleLayout _at = new();
        private Pane? _sign;
        private Texture2D _round = null!;
        private Texture2D _sky = null!;
        private Texture2D _edge = null!;
        private float _time;
        private float _flash;
        private float _nextStorm = FirstStorm;
        private float _gust;
        private float _gustUntil;
        private float _nextGust = 5f;
        private float _stutterUntil;
        private float _lamp = 1f;
        private float _smokeDebt;
        private float _steamDebt;
        private float _ringDebt;
        private float _nextGroup = 0.5f;
        private float _weldUntil;
        private float _nextWeld = 2f;
        private int _weldPane;
        private float _weld;
        private float _nextSurge = 9f;
        private float _surgeStart = -100f;
        private float _furnace = FurnaceRest;
        private float _signOutUntil;
        private float _nextSignTrouble = 6f;
        private float _tramX = float.NaN;
        private int _tramWay = 1;
        private float _nextTram = FirstTram;
        private float _spark;
        private float _nextEvent = FirstEvent;
        private int _lastEvent = -1;
        private float _airshipFrom = -100f;
        private float _guardFrom = -100f;
        private float _catFrom = -100f;
        private float _leafletFrom = -100f;
        private float _leafletY;

        /// <summary>Wie stark alles wirkt, von 0 (aus) bis 1. Kleiner, wenn der Hintergrund schon unruhig ist.</summary>
        public float Strength = 1f;

        /// <summary>Früher die Laterne der Szene, anteilig. Bleibt für alte Einbindungen, die Laternen stehen jetzt in TitleLayout.</summary>
        public Vector2 LampAt = new(0.16f, 0.4f);

        /// <summary>Früher die Orte des Rauchs, anteilig. Bleibt für alte Einbindungen, die Schlote stehen jetzt in TitleLayout.</summary>
        public List<Vector2> SteamAt = new();

        /// <summary>Prüflauf: hält den Blitz auf dieser Stärke, negativ heißt aus.</summary>
        public float HeldFlash = -1f;

        /// <summary>
        /// Die Szene gehört zum Titelbild und sitzt auf dessen Bildpunkten. Ohne Titelbild abschalten, dann bleiben Regen
        /// und Wetterleuchten.
        /// </summary>
        public bool Scene = true;

        /// <summary>
        /// Das Titelbild selbst. Daraus entstehen Masken in Fensterform, damit ein Fenster, das ausgeht, genau seine
        /// Scheibe verdunkelt. Ohne Bild wird das Feld um das Fenster abgedeckt.
        /// </summary>
        public Texture2D? Picture;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        // Das Titelbild füllt die Fläche und wird dabei mittig beschnitten. So liegt ein Bildpunkt des Bilds im Fenster.
        private float ImageScale => Mathf.Max(Size.X / _at.Size.X, Size.Y / _at.Size.Y);

        private Vector2 OnScreen(Vector2 pixel) => pixel * ImageScale + (Size - _at.Size * ImageScale) / 2f;

        private Vector2 OnScreen(float x, float y) => OnScreen(new Vector2(x, y));

        private Rect2 OnScreen(Rect2 area) => new(OnScreen(area.Position), area.Size * ImageScale);

        private float Strong => Mathf.Clamp(Strength, 0f, 1f);

        // Füße eines Arbeiters im Bild und seine Größe, aus Tiefe und seitlichem Abstand.
        private Vector2 Ground(float z, float lane) => new(_at.Vanish.X + lane / z, _at.Vanish.Y + _at.GroundDrop / z);

        private float DepthOf(float groundY) => _at.GroundDrop / Mathf.Max(1f, groundY - _at.Vanish.Y);

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _at = TitleLayout.Load(ProjectSettings.GlobalizePath(LayoutPath));
            for (int i = 0; i < Drops; i++)
            {
                _drops[i] = new Vector2((float)_rng.NextDouble(), (float)_rng.NextDouble());
            }
            _round = new GradientTexture2D
            {
                Gradient = new Gradient { Offsets = new[] { 0f, 0.4f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0f) } },
                Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
            // Der Blitz hellt den Himmel oben auf, nach unten wird er schwächer, wo Häuser und Menü stehen.
            _sky = new GradientTexture2D
            {
                Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.1f) } },
                Width = 4, Height = 64, FillFrom = new Vector2(0.5f, 0f), FillTo = new Vector2(0.5f, 1f),
            };
            // Zum Rand hin versinkt das Bild im Schwarz, die Mitte mit dem Werk bleibt offen.
            _edge = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Offsets = new[] { 0f, 0.45f, 1f },
                    Colors = new[] { new Color(0f, 0f, 0f, OverallDark), new Color(0f, 0f, 0f, OverallDark + 0.07f), new Color(0f, 0f, 0f, OverallDark + EdgeDark * (1f - OverallDark)) },
                },
                Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.62f, 0.56f), FillTo = new Vector2(1.25f, 0.56f),
            };
            // Von hinten nach vorn: abgedunkelte Scheiben, Licht, die Gestalten davor, Dunkel und Regen, zuletzt Blitz und Funken.
            AddLayer(PaintGlass, add: false);
            AddLayer(PaintLight, add: true);
            AddLayer(PaintStreet, add: false);
            AddLayer(PaintVeil, add: false);
            AddLayer(PaintFlash, add: true);
            for (int i = 0; i < FogBanks; i++)
            {
                _fog[i] = Rand(0f, 1f);
            }
            if (!Scene)
            {
                return;
            }
            using var picture = Picture?.GetImage();
            if (picture != null && picture.IsCompressed())
            {
                picture.Decompress();
            }
            int flickers = _at.Windows.Count > 0 ? _rng.Next(_at.Windows.Count) : -1;
            for (int i = 0; i < _at.Windows.Count; i++)
            {
                // Ein Teil der Fenster ist anfangs dunkel und geht erst später an.
                bool on = _rng.NextDouble() >= StartDarkShare;
                _panes.Add(new Pane
                {
                    Area = _at.Windows[i], On = on, Dark = on ? 0f : 1f, NextToggle = Rand(3f, MaxToggleSeconds), Flickers = i == flickers,
                    Mask = picture == null ? null : MakeMask(picture, _at.Windows[i].Grow(1f)),
                });
            }
            _sign = new Pane { Area = _at.Sign, Mask = picture == null ? null : MakeMask(picture, _at.Sign) };
            for (int i = 0; i < Drips; i++)
            {
                _drips.Add(new Drip { Eave = i % Mathf.Max(1, _at.Eaves.Count), Wait = Rand(0f, 3f), Shift = Rand(-14f, 14f) });
            }
            // Die Straße ist schon bevölkert, wenn das Bild erscheint.
            for (float z = 1f; z < _at.GateZ; z += Rand(0.25f, 0.8f))
            {
                AddGroup(z);
            }
        }

        private void AddLayer(Action<CanvasItem> paint, bool add)
        {
            var layer = new Layer { Paint = paint, MouseFilter = MouseFilterEnum.Ignore };
            if (add)
            {
                layer.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            }
            AddChild(layer);
            layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _layers.Add(layer);
        }

        /// <summary>Wo das Bild im Feld hell ist, ist Scheibe: Die Maske deckt nur dort.</summary>
        private Texture2D MakeMask(Image picture, Rect2 area)
        {
            float factor = picture.GetWidth() / _at.Size.X;
            int x0 = (int)(area.Position.X * factor), y0 = (int)(area.Position.Y * factor);
            int w = Mathf.Max(1, (int)(area.Size.X * factor)), h = Mathf.Max(1, (int)(area.Size.Y * factor));
            var mask = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int px = Mathf.Clamp(x0 + x, 0, picture.GetWidth() - 1), py = Mathf.Clamp(y0 + y, 0, picture.GetHeight() - 1);
                    float light = picture.GetPixel(px, py).Luminance;
                    mask.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp((light - MaskFromLight) / (MaskFullLight - MaskFromLight), 0f, 1f)));
                }
            }
            return ImageTexture.CreateFromImage(mask);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            UpdateWind(dt);
            UpdateRain(dt);
            UpdateStorm(dt);
            if (Scene)
            {
                UpdateLamp(dt);
                UpdateWorkers(dt);
                UpdatePanes(dt);
                UpdateWorks(dt);
                UpdateTram(dt);
                UpdateEvents();
                UpdateWater(dt);
                UpdateSmoke(dt);
            }
            foreach (var layer in _layers)
            {
                layer.QueueRedraw();
            }
        }

        /// <summary>Böen: Der Wind frischt auf, der Regen legt sich schräg, Rauch und Wäsche gehen mit.</summary>
        private void UpdateWind(float dt)
        {
            if (_time > _nextGust)
            {
                _gustUntil = _time + Rand(2f, 5f);
                _nextGust = _gustUntil + Rand(4f, 11f);
            }
            _gust = Mathf.MoveToward(_gust, _time < _gustUntil ? 1f : 0f, GustChangePerSecond * dt);
        }

        private void UpdateRain(float dt)
        {
            var size = Size;
            if (size.X <= 0f || size.Y <= 0f)
            {
                return;
            }
            float drift = Mathf.Lerp(CalmDrift, GustDrift, _gust);
            var step = new Vector2(drift / size.X, FallSpeed / size.Y) * dt;
            var tail = new Vector2(drift, FallSpeed).Normalized() * DropLength;
            for (int i = 0; i < Drops; i++)
            {
                _drops[i] += step;
                if (_drops[i].Y > 1f || _drops[i].X > 1f)
                {
                    _drops[i] = new Vector2((float)_rng.NextDouble() * 1.4f - 0.4f, 0f);
                }
                var p = _drops[i] * size;
                _lines[i * 2] = p;
                _lines[i * 2 + 1] = p + tail;
            }
        }

        /// <summary>Zwei bis vier schnelle Entladungen weit hinter der Stadt. Für einen Augenblick liegt die Straße offen da.</summary>
        private void UpdateStorm(float dt)
        {
            if (_time > _nextStorm)
            {
                _nextStorm = _time + Rand(MinStormPause, MaxStormPause);
                float at = _time;
                for (int i = _rng.Next(2, 5); i > 0; i--)
                {
                    _strokes.Add(at);
                    at += Rand(0.06f, 0.25f);
                }
            }
            for (int i = _strokes.Count - 1; i >= 0; i--)
            {
                if (_time >= _strokes[i])
                {
                    _flash = Mathf.Max(_flash, Rand(0.6f, 1f));
                    _strokes.RemoveAt(i);
                }
            }
            _flash = HeldFlash >= 0f ? HeldFlash : Mathf.Max(0f, _flash - _flash * FlashFadePerSecond * dt - 0.2f * dt);
        }

        /// <summary>Leises Unruhigsein, dazu ab und zu ein Wackelkontakt, bei dem das Licht kurz aussetzt.</summary>
        private void UpdateLamp(float dt)
        {
            if (_time > _stutterUntil && _rng.NextDouble() < 0.25 * dt)
            {
                _stutterUntil = _time + Rand(0.2f, 0.7f);
            }
            float unrest = 0.92f + 0.05f * Mathf.Sin(_time * 3f) + 0.03f * Mathf.Sin(_time * 11f);
            _lamp = _time < _stutterUntil && _rng.NextDouble() < 0.55 ? 0.15f : unrest;
        }

        /// <summary>Schichtwechsel: einzeln und in kleinen Gruppen, dicht hintereinander, ab und zu reißt der Zug kurz ab.</summary>
        private void UpdateWorkers(float dt)
        {
            if (_time > _nextGroup && _workers.Count < MaxWorkers)
            {
                _nextGroup = _time + (_rng.NextDouble() < LullChance ? Rand(MaxGroupPause, MaxGroupPause * 2.2f) : Rand(MinGroupPause, MaxGroupPause));
                AddGroup(Rand(MinStartZ, MaxStartZ));
            }
            foreach (var w in _workers)
            {
                w.Z += w.Pace * dt;
                if (w.Z < BreathZ && _time > w.NextBreath)
                {
                    // Atem in der kalten Luft, nur vorn zu sehen.
                    w.NextBreath = _time + Rand(2.2f, 3.4f);
                    var head = Ground(w.Z, LaneOf(w)) - new Vector2(-_at.FigureHeight / w.Z * 0.1f, _at.FigureHeight / w.Z * w.Tall * 0.86f);
                    _puffs.Add(new Puff
                    {
                        Pos = head, Drift = new Vector2(10f + 30f * _gust, -9f), Life = BreathSeconds, Size = 9f / w.Z + 4f, Grow = 1.6f,
                        Color = new Color(SteamColor, 0.16f),
                    });
                }
            }
            _workers.RemoveAll(w => w.Z >= _at.GateZ);
            // Ferne zuerst zeichnen, Nahe darüber.
            _workers.Sort((a, b) => b.Z.CompareTo(a.Z));
        }

        private float LaneOf(Worker w)
        {
            float progress = Mathf.Clamp((w.Z - MinStartZ) / (_at.GateZ - MinStartZ), 0f, 1f);
            return Mathf.Lerp(w.Lane, w.EndLane, Mathf.SmoothStep(0f, 1f, progress));
        }

        private void AddGroup(float z)
        {
            int count = _rng.Next(10) switch { < 4 => 1, < 8 => 2, _ => 3 };
            float lane = Rand(_at.StartLaneMin, _at.StartLaneMax), end = Rand(-_at.GateLane, _at.GateLane), pace = Rand(MinPace, MaxPace);
            bool bike = count == 1 && _rng.NextDouble() < BikeShare * 3f;
            for (int i = 0; i < count; i++)
            {
                _workers.Add(new Worker
                {
                    Z = z + i * Rand(0.03f, 0.09f), Lane = lane + i * Rand(35f, 70f), EndLane = end + i * Rand(20f, 45f),
                    // Wer zusammen geht, hält fast denselben Schritt.
                    Pace = pace * Rand(0.97f, 1.03f), Phase = Rand(0f, Mathf.Tau), Tall = Rand(0.9f, 1.06f), Stoop = Rand(0.5f, 1f),
                    Pail = !bike && _rng.Next(3) > 0, Bike = bike, NextBreath = _time + Rand(0f, 3f),
                });
            }
        }

        private void UpdatePanes(float dt)
        {
            foreach (var pane in _panes)
            {
                if (_time > pane.NextToggle)
                {
                    pane.On = !pane.On;
                    pane.NextToggle = _time + Rand(MinToggleSeconds, MaxToggleSeconds);
                }
                pane.Dark = Mathf.MoveToward(pane.Dark, pane.On ? 0f : 1f, dt / SwitchSeconds);
                if (pane.On && _time > pane.PasserFrom + pane.PasserSeconds && _rng.NextDouble() < PasserChancePerSecond * dt)
                {
                    pane.PasserFrom = _time;
                    pane.PasserSeconds = Rand(0.9f, 1.8f);
                }
            }
            // Die Reklame über dem Werk hat einen Wackelkontakt: Mal zuckt sie, mal fällt sie für einen Atemzug aus.
            if (_time > _nextSignTrouble)
            {
                _signOutUntil = _time + (_rng.Next(4) == 0 ? Rand(0.8f, 1.6f) : Rand(0.25f, 0.6f));
                _nextSignTrouble = _signOutUntil + Rand(5f, 14f);
            }
            if (_sign != null)
            {
                _sign.Dark = _time < _signOutUntil && _rng.NextDouble() < 0.6 ? Rand(0.55f, 1f) : 0f;
            }
        }

        /// <summary>Im Werk wird geschweißt, und ab und zu geht eine Ofentür auf.</summary>
        private void UpdateWorks(float dt)
        {
            if (_time > _nextWeld)
            {
                _weldUntil = _time + Rand(0.6f, 2.4f);
                _nextWeld = _weldUntil + Rand(MinWeldPause, MaxWeldPause);
                _weldPane = _rng.Next(Mathf.Max(1, _at.WorksPanes.Count));
            }
            _weld = _time < _weldUntil && _rng.NextDouble() < 0.5 ? Rand(0.5f, 1f) : Mathf.MoveToward(_weld, 0f, dt * 9f);
            if (_time > _nextSurge)
            {
                _surgeStart = _time;
                _nextSurge = _time + SurgeSeconds + Rand(MinSurgePause, MaxSurgePause);
            }
            bool surging = _time - _surgeStart < SurgeSeconds;
            float breathe = FurnaceRest * (1f + 0.25f * Mathf.Sin(_time * 1.3f) + 0.12f * Mathf.Sin(_time * 5.1f));
            _furnace = Mathf.MoveToward(_furnace, surging ? FurnaceSurge * Rand(0.85f, 1f) : breathe, dt * 0.4f);
        }

        /// <summary>Die Linie 7 quert vor dem Werk. Am Fahrdraht springen Funken, ihr Licht fällt auf Pflaster und Fassaden.</summary>
        private void UpdateTram(float dt)
        {
            _spark = Mathf.MoveToward(_spark, 0f, dt * 7f);
            if (float.IsNaN(_tramX))
            {
                if (_time > _nextTram)
                {
                    _tramWay = _rng.Next(2) == 0 ? 1 : -1;
                    _tramX = _tramWay > 0 ? _at.TramFrom - _at.TramLength : _at.TramTo;
                }
                return;
            }
            _tramX += _tramWay * TramSpeed * dt;
            if (_rng.NextDouble() < 1.6 * dt)
            {
                _spark = Rand(0.6f, 1f);
            }
            if (_tramX > _at.TramTo || _tramX < _at.TramFrom - _at.TramLength)
            {
                _tramX = float.NaN;
                _nextTram = _time + Rand(MinTramPause, MaxTramPause);
            }
        }

        /// <summary>In Abständen geschieht eine Kleinigkeit, nie zweimal dieselbe hintereinander.</summary>
        private void UpdateEvents()
        {
            if (_time < _nextEvent)
            {
                return;
            }
            _nextEvent = _time + Rand(MinEventPause, MaxEventPause);
            int pick;
            do
            {
                pick = _rng.Next(4);
            }
            while (pick == _lastEvent);
            _lastEvent = pick;
            switch (pick)
            {
                case 0:
                    _guardFrom = _time;
                    break;
                case 1:
                    _airshipFrom = _time;
                    break;
                case 2:
                    _catFrom = _time;
                    break;
                default:
                    _leafletFrom = _time;
                    _leafletY = Rand(760f, 900f);
                    break;
            }
        }

        /// <summary>Tropfen von den Traufen und Ringe in den Pfützen.</summary>
        private void UpdateWater(float dt)
        {
            foreach (var d in _drips)
            {
                if (_at.Eaves.Count == 0)
                {
                    break;
                }
                var eave = _at.Eaves[d.Eave];
                if (d.Fall >= 0f)
                {
                    d.Fall += dt;
                    if (eave.Y + 480f * d.Fall * d.Fall >= eave.Z)
                    {
                        d.Fall = -1f;
                        d.Wait = Rand(0.3f, 2.6f);
                        _rings.Add(new Ring { At = new Vector2(eave.X + d.Shift, eave.Z), Size = 9f });
                    }
                }
                else
                {
                    d.Wait -= dt;
                    if (d.Wait <= 0f)
                    {
                        d.Fall = 0f;
                        d.Eave = _rng.Next(_at.Eaves.Count);
                        d.Shift = Rand(-16f, 16f);
                    }
                }
            }
            _ringDebt += RingsPerSecond * (1f + _gust) * dt;
            while (_ringDebt >= 1f && _at.Puddles.Count > 0)
            {
                _ringDebt -= 1f;
                var puddle = _at.Puddles[_rng.Next(_at.Puddles.Count)];
                _rings.Add(new Ring { At = new Vector2(puddle.X + Rand(-puddle.Z, puddle.Z), puddle.Y + Rand(-puddle.Z, puddle.Z) * 0.18f), Size = Rand(12f, 26f) * puddle.Y / 900f });
            }
            foreach (var r in _rings)
            {
                r.Age += dt;
            }
            _rings.RemoveAll(r => r.Age >= RingSeconds);
        }

        /// <summary>Rauch aus den Schloten, Dampf aus den Gullys. Beides zieht mit dem Wind und wird im Blitz hell.</summary>
        private void UpdateSmoke(float dt)
        {
            float wind = 14f + 46f * _gust;
            _smokeDebt += SmokePerSecond * _at.Chimneys.Count * dt;
            while (_smokeDebt >= 1f && _at.Chimneys.Count > 0)
            {
                _smokeDebt -= 1f;
                var at = _at.Chimneys[_rng.Next(_at.Chimneys.Count)];
                _puffs.Add(new Puff
                {
                    Pos = at + new Vector2(Rand(-7f, 7f), 0f), Drift = new Vector2(wind, -15f), Life = SmokeSeconds, Size = Rand(18f, 30f), Grow = 3.2f,
                    Color = new Color(SmokeColor, SmokeAlpha),
                });
            }
            _steamDebt += SteamPerSecond * _at.Gullies.Count * dt;
            while (_steamDebt >= 1f && _at.Gullies.Count > 0)
            {
                _steamDebt -= 1f;
                var at = _at.Gullies[_rng.Next(_at.Gullies.Count)];
                _puffs.Add(new Puff
                {
                    Pos = at + new Vector2(Rand(-10f, 10f), 0f), Drift = new Vector2(wind * 0.5f, -19f), Life = SteamSeconds, Size = Rand(10f, 17f), Grow = 2.6f,
                    Color = new Color(SteamColor, SteamAlpha),
                });
            }
            foreach (var p in _puffs)
            {
                p.Age += dt;
                p.Pos += (p.Drift + new Vector2(6f * Mathf.Sin(_time * 0.7f + p.Size), 0f)) * dt;
            }
            _puffs.RemoveAll(p => p.Age >= p.Life);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Ebene 1: abgedunkelte Scheiben, Reklame, Wäsche, Luftschiff. Alles, was zur Kulisse gehört.
        // ------------------------------------------------------------------------------------------------------------
        private void PaintGlass(CanvasItem c)
        {
            if (!Scene)
            {
                return;
            }
            float scale = ImageScale;
            DrawAirship(c, scale);
            foreach (var pane in _panes)
            {
                float dark = pane.Dark;
                if (pane.Flickers && pane.On && Mathf.Sin(_time * 0.9f) > 0.2f && _rng.NextDouble() < 0.4)
                {
                    dark = Rand(0.4f, 0.95f);
                }
                var area = OnScreen(pane.Area.Grow(1f));
                Cover(c, pane, area, dark);
                float passing = (_time - pane.PasserFrom) / pane.PasserSeconds;
                if (pane.On && passing is >= 0f and < 1f)
                {
                    // Breite Fenster: Der Schatten wandert quer durch. Schmale: Das ganze Fenster wird kurz dunkler.
                    float band = area.Size.X > 12f * scale ? 0.45f : 1f;
                    float x = area.Position.X + area.Size.X * (1f - band) * passing;
                    c.DrawRect(new Rect2(x, area.Position.Y + area.Size.Y * 0.25f, area.Size.X * band, area.Size.Y * 0.75f),
                        new Color(DarkGlass, PasserShade * Mathf.Sin(passing * Mathf.Pi)));
                }
            }
            if (_sign != null)
            {
                Cover(c, _sign, OnScreen(_sign.Area), _sign.Dark);
            }
            // Wäsche auf der Leine: Die Tücher hängen schwer und heben sich in der Böe.
            Vector2 from = OnScreen(_at.LineFrom), to = OnScreen(_at.LineTo);
            c.DrawLine(from, to, new Color(0.1f, 0.1f, 0.11f, 0.8f), 1f);
            for (int i = 0; i < 4; i++)
            {
                var top = from.Lerp(to, 0.16f + 0.2f * i);
                float lift = (0.12f + 0.75f * _gust) * (0.7f + 0.3f * Mathf.Sin(_time * 5f + i * 1.7f));
                float w = (10f + 3f * (i % 2)) * scale, h = (20f - 3f * (i % 3)) * scale;
                var swing = new Vector2(lift * h * 0.9f, -lift * h * 0.3f);
                c.DrawPolygon(new[] { top, top + new Vector2(w, 0f), top + new Vector2(w, h) + swing, top + new Vector2(0f, h) + swing * 0.8f },
                    new[] { new Color(0.2f, 0.2f, 0.22f, 0.9f) });
            }
        }

        private void Cover(CanvasItem c, Pane pane, Rect2 area, float dark)
        {
            if (dark <= 0.01f)
            {
                return;
            }
            if (pane.Mask != null)
            {
                c.DrawTextureRect(pane.Mask, area, false, new Color(DarkGlass, OffAlpha * dark));
            }
            else
            {
                c.DrawRect(area, new Color(DarkGlass, OffAlpha * dark));
            }
        }

        /// <summary>Hinter den Schloten zieht langsam ein Luftschiff durch den Dunst, ein einzelnes Licht am Bug.</summary>
        private void DrawAirship(CanvasItem c, float scale)
        {
            float t = (_time - _airshipFrom) / AirshipSeconds;
            if (t is < 0f or > 1f)
            {
                return;
            }
            var at = OnScreen(Mathf.Lerp(_at.AirshipFrom, _at.AirshipTo, t), _at.AirshipY - 10f * Mathf.Sin(t * Mathf.Pi));
            float fade = Mathf.Sin(t * Mathf.Pi);
            var hull = new Vector2(96f, 21f) * scale;
            c.DrawSetTransform(at, 0f, new Vector2(hull.X, hull.Y));
            c.DrawCircle(Vector2.Zero, 1f, new Color(0.035f, 0.04f, 0.055f, 0.9f * fade));
            c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            c.DrawRect(new Rect2(at + new Vector2(-18f, 17f) * scale, new Vector2(34f, 7f) * scale), new Color(0.03f, 0.033f, 0.045f, 0.9f * fade));
            if (_time % 1.6f < 0.25f)
            {
                c.DrawRect(new Rect2(at + new Vector2(hull.X * 0.9f, 4f * scale), new Vector2(2f, 2f) * scale), new Color(0.9f, 0.3f, 0.25f, fade));
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Ebene 2, aufhellend: das Licht, vor dem die Gestalten stehen. Liegt unter ihnen, damit sie schwarz bleiben.
        // ------------------------------------------------------------------------------------------------------------
        private void PaintLight(CanvasItem c)
        {
            if (!Scene)
            {
                return;
            }
            float scale = ImageScale, strong = Strong;
            // Der Vorplatz des Werks liegt im Schein der hohen Fenster. Davor zeichnet sich der Zug ab.
            var gate = OnScreen(_at.Gate);
            Disc(c, gate + new Vector2(0f, 26f * scale), new Vector2(330f, 70f) * scale, new Color(ForecourtColor, ForecourtAlpha * strong));
            Disc(c, gate + new Vector2(0f, 150f * scale), new Vector2(240f, 170f) * scale, new Color(ForecourtColor, ForecourtAlpha * 0.5f * strong));
            // Ofenschein am Tor und auf dem Pflaster davor.
            Disc(c, gate, new Vector2(FurnaceRadius * 1.5f, FurnaceRadius) * scale, new Color(FurnaceColor, _furnace * strong));
            Disc(c, gate + new Vector2(0f, 120f * scale), new Vector2(FurnaceRadius, FurnaceRadius * 1.5f) * scale, new Color(FurnaceColor, _furnace * 0.5f * strong));
            if (_weld > 0.01f && _at.WorksPanes.Count > 0)
            {
                // Das Schweißlicht steht im Fenster, wirft Schein auf Dunst und Fassaden und zuckt im nassen Pflaster nach.
                var pane = OnScreen(_at.WorksPanes[_weldPane]);
                c.DrawRect(pane, new Color(WeldColor, WeldAlpha * _weld * strong));
                Disc(c, pane.GetCenter(), new Vector2(WeldHalo, WeldHalo * 0.8f) * scale, new Color(WeldColor, 0.3f * _weld * strong));
                Disc(c, pane.GetCenter() + new Vector2(0f, 190f * scale), new Vector2(WeldHalo * 1.3f, WeldHalo * 0.7f) * scale, new Color(WeldColor, 0.16f * _weld * strong));
                c.DrawRect(new Rect2(pane.Position.X, pane.End.Y + 100f * scale, pane.Size.X, pane.Size.Y * 1.1f), new Color(WeldColor, 0.2f * _weld * strong));
            }
            for (int i = 0; i < _at.Lamps.Count; i++)
            {
                var (head, foot, mirror) = _at.Lamps[i];
                float z = DepthOf(foot.Y);
                // Die pendelnde Laterne nimmt ihren Lichtkreis mit.
                float swing = i == _at.SwingingLamp ? Mathf.Sin(_time * SwingPerSecond) * (0.5f + 0.5f * _gust) : 0f;
                float level = i == _at.SwingingLamp ? _lamp : 0.94f + 0.06f * Mathf.Sin(_time * 2.1f + i);
                var pool = OnScreen(foot + new Vector2(swing * SwingPixels * 6f, 4f));
                float width = PoolWidth / z * scale;
                Disc(c, pool, new Vector2(width, width * 0.24f), new Color(LampColor, PoolAlpha * level * strong));
                Disc(c, OnScreen(head + new Vector2(swing * SwingPixels, 0f)), new Vector2(60f, 60f) / z * 2.4f * scale, new Color(LampColor, 0.2f * level * strong));
                // Träge Spiegelung: Der Lichtstreifen im Pflaster schwillt langsam an und ab und wandert kaum merklich.
                float swell = 0.5f + 0.5f * Mathf.Sin(_time * (0.35f + 0.07f * i) + i * 1.9f);
                Disc(c, OnScreen(mirror + new Vector2(2.5f * Mathf.Sin(_time * 0.5f + i) + swing * SwingPixels * 3f, 0f)),
                    new Vector2(9f, (mirror.Y - foot.Y) * 0.45f) * scale, new Color(LampColor, GlintAlpha * (0.4f + swell) * level * strong));
            }
            if (!float.IsNaN(_tramX))
            {
                // Das Wagenlicht wandert über Pflaster und Fassaden.
                var middle = OnScreen(Mathf.Clamp(_tramX + _at.TramLength / 2f, _at.TramFrom, _at.TramTo), _at.TramGround);
                float inside = TramShowing();
                Disc(c, middle + new Vector2(0f, 46f * scale), new Vector2(260f, 90f) * scale, new Color(TramLight, 0.16f * inside * strong));
                Disc(c, middle - new Vector2(0f, 70f * scale), new Vector2(330f, 150f) * scale, new Color(TramLight, 0.07f * inside * strong));
                Disc(c, middle - new Vector2(0f, (_at.TramHeight + 30f) * scale), new Vector2(420f, 300f) * scale, new Color(SparkColor, 0.22f * _spark * inside * strong));
            }
        }

        private void Disc(CanvasItem c, Vector2 middle, Vector2 half, Color color) =>
            c.DrawTextureRect(_round, new Rect2(middle - half, half * 2f), false, color);

        /// <summary>Wie viel von der Bahn im Ausschnitt der Querstraße zu sehen ist, von 0 bis 1.</summary>
        private float TramShowing()
        {
            float from = Mathf.Max(_tramX, _at.TramFrom), to = Mathf.Min(_tramX + _at.TramLength, _at.TramTo);
            return Mathf.Clamp((to - from) / (_at.TramLength * 0.6f), 0f, 1f);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Ebene 3: was auf der Straße ist. Dunst, Rauch, die Bahn, der Zug der Arbeiter und die kleinen Ereignisse.
        // ------------------------------------------------------------------------------------------------------------
        private void PaintStreet(CanvasItem c)
        {
            if (!Scene)
            {
                return;
            }
            float scale = ImageScale, strong = Strong;
            for (int i = 0; i < FogBanks; i++)
            {
                // Dunstbänke treiben in Straßenhöhe langsam nach rechts, nur rechts vom Menü.
                float travel = (_fog[i] + _time * FogDrift * (1f + 2f * _gust) / 1300f * (0.7f + 0.15f * i)) % 1f;
                var half = new Vector2(310f + 45f * i, 60f + 13f * i) * scale;
                var at = OnScreen(_at.QuietUntil + travel * (_at.Size.X - _at.QuietUntil + 300f), 610f + 34f * i);
                Disc(c, at, half, new Color(FogColor, FogAlpha * Mathf.Sin(travel * Mathf.Pi) * strong));
            }
            foreach (var p in _puffs)
            {
                float f = p.Age / p.Life;
                float radius = p.Size * (1f + p.Grow * f) * scale;
                // Im Wetterleuchten steht der Rauch für einen Augenblick hell vor dem Himmel.
                var color = new Color(p.Color.R + _flash * 0.3f, p.Color.G + _flash * 0.32f, p.Color.B + _flash * 0.4f, p.Color.A * strong * Mathf.Min(1f, f * 5f) * (1f - f));
                Disc(c, OnScreen(p.Pos), new Vector2(radius, radius), color);
            }
            DrawWater(c, scale);
            bool tramDrawn = float.IsNaN(_tramX);
            float tramZ = DepthOf(_at.TramGround);
            bool guardDrawn = false;
            foreach (var w in _workers)
            {
                if (!guardDrawn && w.Z < _at.GateZ - 0.5f)
                {
                    DrawGuard(c, scale);
                    guardDrawn = true;
                }
                if (!tramDrawn && w.Z < tramZ)
                {
                    DrawTram(c, scale);
                    tramDrawn = true;
                }
                DrawWorker(c, w, scale);
            }
            c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            if (!guardDrawn)
            {
                DrawGuard(c, scale);
            }
            if (!tramDrawn)
            {
                DrawTram(c, scale);
            }
            DrawCat(c, scale);
            DrawLeaflet(c, scale);
        }

        private void DrawWater(CanvasItem c, float scale)
        {
            foreach (var r in _rings)
            {
                float f = r.Age / RingSeconds;
                c.DrawSetTransform(OnScreen(r.At), 0f, new Vector2(1f, 0.3f));
                c.DrawArc(Vector2.Zero, r.Size * (0.2f + f) * scale, 0f, Mathf.Tau, 20, new Color(0.62f, 0.68f, 0.82f, 0.3f * (1f - f)), 1f);
            }
            c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            foreach (var d in _drips)
            {
                if (d.Fall < 0f || _at.Eaves.Count == 0)
                {
                    continue;
                }
                var eave = _at.Eaves[d.Eave];
                float y = eave.Y + 480f * d.Fall * d.Fall;
                var at = OnScreen(eave.X + d.Shift, y);
                c.DrawLine(at - new Vector2(0f, Mathf.Min(10f, 3f + 40f * d.Fall) * scale), at, new Color(0.7f, 0.76f, 0.9f, 0.5f), 1f);
            }
        }

        /// <summary>
        /// Ein Arbeiter von hinten, als Schattenriss: schwerer Gang, runde Schultern, der Kopf gesenkt. Gezeichnet in
        /// Einheiten seiner Größe, die Füße im Ursprung. Ein schmaler Lichtsaum vom Werk her löst ihn vom Dunkel.
        /// </summary>
        private void DrawWorker(CanvasItem c, Worker w, float scale)
        {
            var foot = OnScreen(Ground(w.Z, LaneOf(w)));
            float height = _at.FigureHeight / w.Z * w.Tall * scale;
            var color = Shadow.Lerp(FarShadow, Mathf.Clamp((w.Z - 1f) / 5f, 0f, 1f));
            float alpha = Mathf.Clamp((_at.GateZ - w.Z) / GateFadeZ, 0f, 1f);
            float step = w.Z * StepsPerZ + w.Phase;
            if (w.Z > SimpleFromZ)
            {
                // Weit hinten ist die Gestalt nur noch ein paar Bildpunkte hoch: Der Umriss genügt.
                c.DrawSetTransform(foot, 0f, new Vector2(height, height));
                DrawOutline(c, step, new Color(color, alpha));
                return;
            }
            if (w.Z < MirrorUntilZ)
            {
                c.DrawSetTransform(foot, 0f, new Vector2(height, -height * MirrorSquash));
                DrawOutline(c, step, new Color(color, alpha * MirrorAlpha));
            }
            // Der Saum liegt auf der Seite zum Werk hin, also zur Straßenmitte.
            float side = foot.X > OnScreen(_at.Vanish).X ? -1f : 1f;
            c.DrawSetTransform(foot + new Vector2(side * RimShift * height, -RimShift * 0.6f * height), 0f, new Vector2(height, height));
            DrawOutline(c, step, new Color(Rim, alpha * 0.55f));
            c.DrawSetTransform(foot, 0f, new Vector2(height, height));
            if (w.Bike)
            {
                DrawBike(c, step, new Color(color, alpha));
            }
            DrawFigure(c, w, step, new Color(color, alpha));
        }

        private static void DrawOutline(CanvasItem c, float step, Color color)
        {
            float lean = 0.02f * Mathf.Sin(step);
            c.DrawPolygon(new[]
            {
                new Vector2(-0.1f + lean, 0f), new Vector2(0.1f + lean, 0f), new Vector2(0.14f, -0.76f), new Vector2(0.05f, -0.82f),
                new Vector2(0.06f, -0.93f), new Vector2(-0.06f, -0.93f), new Vector2(-0.05f, -0.82f), new Vector2(-0.14f, -0.76f),
            }, new[] { color });
        }

        /// <summary>Das Fahrrad wird geschoben: zwei Räder schräg von hinten, der Rahmen, der Lenker in der Hand.</summary>
        private static void DrawBike(CanvasItem c, float step, Color color)
        {
            float roll = step * 0.6f;
            foreach (float y in new[] { -0.02f, -0.09f })
            {
                var hub = new Vector2(0.27f, y - 0.19f);
                c.DrawArc(hub, 0.19f, 0f, Mathf.Tau, 14, color, 0.022f);
                c.DrawLine(hub + new Vector2(Mathf.Cos(roll), Mathf.Sin(roll)) * 0.18f, hub - new Vector2(Mathf.Cos(roll), Mathf.Sin(roll)) * 0.18f, color, 0.012f);
            }
            c.DrawLine(new Vector2(0.27f, -0.21f), new Vector2(0.25f, -0.55f), color, 0.03f);
            c.DrawLine(new Vector2(0.25f, -0.55f), new Vector2(0.15f, -0.56f), color, 0.03f);
            c.DrawRect(new Rect2(0.2f, -0.52f, 0.12f, 0.04f), color);
        }

        private static void DrawFigure(CanvasItem c, Worker w, float step, Color color)
        {
            float swing = Mathf.Sin(step), sway = 0.012f * Mathf.Sin(step), bob = 0.012f * Mathf.Abs(Mathf.Cos(step));
            float stoop = 0.035f * w.Stoop;
            var hip = new Vector2(sway, -0.47f + bob);
            var one = new[] { color };
            // Beine: Der Fuß, der nach vorn schwingt, hebt sich ein wenig.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = swing * side;
                var foot = new Vector2(side * 0.07f + s * 0.012f, -Mathf.Max(0f, s) * 0.035f);
                c.DrawPolygon(new[]
                {
                    hip + new Vector2(side * 0.05f - 0.05f, 0f), hip + new Vector2(side * 0.05f + 0.05f, 0f),
                    foot + new Vector2(0.032f, 0f), foot + new Vector2(-0.032f, 0f),
                }, one);
            }
            // Rumpf mit runden Schultern, die Jacke hängt.
            var shoulder = new Vector2(sway * 1.4f, -0.8f + bob + stoop);
            c.DrawPolygon(new[]
            {
                hip + new Vector2(-0.105f, 0.04f), hip + new Vector2(0.105f, 0.04f), shoulder + new Vector2(0.14f, 0.035f),
                shoulder + new Vector2(0.075f, -0.03f), shoulder + new Vector2(-0.075f, -0.03f), shoulder + new Vector2(-0.14f, 0.035f),
            }, one);
            // Arme hängen, die Hände pendeln kaum.
            for (int side = -1; side <= 1; side += 2)
            {
                var top = shoulder + new Vector2(side * 0.132f, 0.025f);
                var hand = new Vector2(top.X + side * 0.012f - swing * side * 0.012f, -0.5f + bob);
                c.DrawPolygon(new[] { top + new Vector2(-0.028f, 0f), top + new Vector2(0.028f, 0f), hand + new Vector2(0.022f, 0f), hand + new Vector2(-0.022f, 0f) }, one);
                if (side > 0 && w.Pail)
                {
                    // Der Henkelmann.
                    c.DrawRect(new Rect2(hand.X - 0.035f, hand.Y, 0.07f, 0.085f), color);
                }
            }
            // Der Kopf sinkt zwischen die Schultern, darauf die Schiebermütze.
            var head = shoulder + new Vector2(0f, -0.1f + stoop * 0.9f);
            c.DrawPolygon(new[]
            {
                head + new Vector2(-0.028f, 0.09f), head + new Vector2(0.028f, 0.09f), head + new Vector2(0.056f, 0.02f), head + new Vector2(0.066f, -0.03f),
                head + new Vector2(0.06f, -0.062f), head + new Vector2(-0.06f, -0.062f), head + new Vector2(-0.066f, -0.03f), head + new Vector2(-0.056f, 0.02f),
            }, one);
        }

        /// <summary>Die Bahn fährt quer vor dem Werk und ist nur zwischen den Häuserzeilen zu sehen.</summary>
        private void DrawTram(CanvasItem c, float scale)
        {
            if (float.IsNaN(_tramX))
            {
                return;
            }
            c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            float top = _at.TramGround - _at.TramHeight;
            Clipped(c, new Rect2(_tramX, top, _at.TramLength, _at.TramHeight), TramBody);
            Clipped(c, new Rect2(_tramX + 8f, top - 5f, _at.TramLength - 16f, 6f), TramBody);
            float light = 0.85f + 0.15f * Mathf.Sin(_time * 23f) * Mathf.Sin(_time * 7f);
            for (float x = 10f; x < _at.TramLength - 22f; x += 24f)
            {
                Clipped(c, new Rect2(_tramX + x, top + 12f, 17f, 22f), new Color(TramLight, 0.82f * light));
            }
            // Stirnlampe in Fahrtrichtung und die Liniennummer als heller Fleck.
            float front = _tramWay > 0 ? _tramX + _at.TramLength - 5f : _tramX + 1f;
            Clipped(c, new Rect2(front, top + 40f, 4f, 5f), new Color(1f, 0.95f, 0.85f));
            // Stromabnehmer bis zum Fahrdraht.
            float pole = _tramX + _at.TramLength * 0.5f;
            if (pole > _at.TramFrom && pole < _at.TramTo)
            {
                Vector2 foot = OnScreen(pole, top - 5f), tip = OnScreen(pole - _tramWay * 14f, top - 30f);
                c.DrawLine(foot, tip, TramBody, 1.5f);
            }
        }

        private void Clipped(CanvasItem c, Rect2 area, Color color)
        {
            float from = Mathf.Max(area.Position.X, _at.TramFrom), to = Mathf.Min(area.End.X, _at.TramTo);
            if (to > from)
            {
                c.DrawRect(OnScreen(new Rect2(from, area.Position.Y, to - from, area.Size.Y)), color);
            }
        }

        /// <summary>Ein Wachmann geht mit dem Hund vor dem Tor auf und ab, klein und schwarz vor dem Ofenschein.</summary>
        private void DrawGuard(CanvasItem c, float scale)
        {
            float t = (_time - _guardFrom) / GuardSeconds;
            if (t is < 0f or > 1f)
            {
                return;
            }
            // Hinüber, kurz stehen, zurück.
            float across = t < 0.42f ? t / 0.42f : t < 0.58f ? 1f : 1f - (t - 0.58f) / 0.42f;
            bool walking = t is < 0.42f or > 0.58f;
            float z = _at.GateZ - 0.7f;
            var foot = OnScreen(Ground(z, Mathf.Lerp(1050f, -700f, across)));
            float height = _at.FigureHeight / z * 1.08f * scale;
            float way = t < 0.5f ? -1f : 1f;
            var color = new Color(Shadow, Mathf.Sin(Mathf.Clamp(t * 12f, 0f, 1f) * Mathf.Pi / 2f) * Mathf.Clamp((1f - t) * 12f, 0f, 1f));
            c.DrawSetTransform(foot, 0f, new Vector2(height, height));
            DrawOutline(c, walking ? _time * 7f : 0f, color);
            // Mütze mit Schirm, damit er sich von den Arbeitern unterscheidet, und der Hund an der kurzen Leine.
            c.DrawRect(new Rect2(-0.09f, -0.97f, 0.18f, 0.05f), color);
            var dog = new Vector2(way * 0.42f, 0f);
            float trot = walking ? Mathf.Sin(_time * 14f) * 0.02f : 0f;
            c.DrawRect(new Rect2(dog.X - 0.17f, -0.3f + trot, 0.34f, 0.14f), color);
            c.DrawRect(new Rect2(dog.X + way * 0.15f - 0.06f, -0.4f + trot, 0.12f, 0.12f), color);
            c.DrawRect(new Rect2(dog.X - 0.15f, -0.17f, 0.04f, 0.17f), color);
            c.DrawRect(new Rect2(dog.X + 0.11f, -0.17f, 0.04f, 0.17f), color);
            c.DrawLine(new Vector2(way * 0.1f, -0.5f), new Vector2(dog.X, -0.3f), color, 0.02f);
            c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        /// <summary>Eine Katze huscht vorn über den Gehweg und ist schon wieder weg.</summary>
        private void DrawCat(CanvasItem c, float scale)
        {
            float t = (_time - _catFrom) / CatSeconds;
            if (t is < 0f or > 1f)
            {
                return;
            }
            var at = OnScreen(Mathf.Lerp(_at.Size.X + 40f, 1480f, t), 842f - 26f * t);
            float size = 30f * scale, leap = Mathf.Abs(Mathf.Sin(t * 26f));
            var color = new Color(Shadow, Mathf.Clamp((1f - t) * 5f, 0f, 1f));
            c.DrawSetTransform(at - new Vector2(0f, leap * 5f * scale), 0f, new Vector2(size, size));
            c.DrawRect(new Rect2(-0.5f, -0.42f, 1f, 0.3f), color);
            c.DrawRect(new Rect2(-0.72f, -0.62f, 0.3f, 0.28f), color);
            c.DrawPolygon(new[] { new Vector2(-0.7f, -0.62f), new Vector2(-0.62f, -0.78f), new Vector2(-0.55f, -0.62f) }, new[] { color });
            c.DrawLine(new Vector2(0.5f, -0.36f), new Vector2(0.86f, -0.66f - 0.1f * leap), color, 0.07f);
            c.DrawLine(new Vector2(-0.4f, -0.14f), new Vector2(-0.5f - 0.2f * leap, 0f), color, 0.08f);
            c.DrawLine(new Vector2(0.4f, -0.14f), new Vector2(0.5f + 0.2f * leap, 0f), color, 0.08f);
            c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        /// <summary>Ein Flugblatt treibt im Wind über die Straße, überschlägt sich und bleibt vor dem Menü liegen.</summary>
        private void DrawLeaflet(CanvasItem c, float scale)
        {
            float t = (_time - _leafletFrom) / LeafletSeconds;
            if (t is < 0f or > 1f)
            {
                return;
            }
            float x = Mathf.Lerp(_at.Size.X + 30f, _at.QuietUntil + 120f, 1f - Mathf.Pow(1f - t, 2f));
            float hop = Mathf.Abs(Mathf.Sin(t * 9f)) * 46f * (1f - t);
            var at = OnScreen(x, _leafletY - hop);
            float turn = _time * 9f * (1f - t);
            // Von der Seite gesehen wird das Blatt beim Überschlagen schmal und wieder breit.
            var half = new Vector2(8f, 2f + 7f * Mathf.Abs(Mathf.Sin(turn))) * scale;
            c.DrawSetTransform(at, Mathf.Sin(turn) * 0.5f, Vector2.One);
            c.DrawRect(new Rect2(-half, half * 2f), new Color(0.42f, 0.42f, 0.4f, 0.85f * Mathf.Clamp((1f - t) * 4f, 0f, 1f)));
            c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Ebene 4: Dunkel zum Rand und der Regen davor.
        // ------------------------------------------------------------------------------------------------------------
        private void PaintVeil(CanvasItem c)
        {
            float strong = Strong;
            if (Scene)
            {
                c.DrawTextureRect(_edge, new Rect2(Vector2.Zero, Size), false, new Color(1f, 1f, 1f, strong));
            }
            c.DrawMultiline(_lines, new Color(RainColor, RainAlpha * strong * (1f + GustRainBoost * _gust + _flash)), 1f);
        }

        // ------------------------------------------------------------------------------------------------------------
        // Ebene 5, aufhellend: Wetterleuchten und Funken. Liegt über allem und zeigt für einen Augenblick die ganze Straße.
        // ------------------------------------------------------------------------------------------------------------
        private void PaintFlash(CanvasItem c)
        {
            float strong = Strong;
            if (_flash > 0.01f)
            {
                c.DrawTextureRect(_sky, new Rect2(Vector2.Zero, Size), false, new Color(FlashColor, SkyFlashAlpha * _flash * strong));
                if (Scene)
                {
                    // Die Straße selbst: rechts vom Menü hell, zum Menü hin läuft das Licht aus.
                    var quiet = OnScreen(_at.QuietUntil, 0f);
                    c.DrawRect(new Rect2(quiet.X, 0f, Size.X - quiet.X, Size.Y), new Color(FlashColor, StreetFlashAlpha * _flash * strong));
                    Disc(c, new Vector2(quiet.X, Size.Y * 0.6f), new Vector2(Size.X * 0.22f, Size.Y * 0.7f), new Color(FlashColor, StreetFlashAlpha * 0.6f * _flash * strong));
                }
            }
            if (!Scene || float.IsNaN(_tramX) || _spark <= 0.02f)
            {
                return;
            }
            float pole = _tramX + _at.TramLength * 0.5f - _tramWay * 14f;
            if (pole <= _at.TramFrom || pole >= _at.TramTo)
            {
                return;
            }
            float scale = ImageScale;
            var tip = OnScreen(pole, _at.TramGround - _at.TramHeight - 30f);
            Disc(c, tip, new Vector2(26f, 26f) * scale * _spark, new Color(SparkColor, 0.9f * _spark));
            for (int i = 0; i < 7; i++)
            {
                // Die Funken fallen in kurzen Bögen vom Draht.
                float a = i * 0.9f + _time * 3f, far = (1f - _spark) * 46f + 6f;
                var at = tip + new Vector2(Mathf.Cos(a) * far, Mathf.Abs(Mathf.Sin(a)) * far * 1.3f) * scale;
                c.DrawRect(new Rect2(at, new Vector2(2f, 2f) * scale), new Color(1f, 0.92f, 0.75f, _spark));
            }
        }
    }
}
