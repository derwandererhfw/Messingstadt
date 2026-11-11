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
        private static readonly Color SmokeColor = new(0.56f, 0.58f, 0.66f);
        private static readonly Color SteamColor = new(0.62f, 0.66f, 0.74f);
        private static readonly Color FogColor = new(0.5f, 0.56f, 0.7f);

        // Rauch, Dampf, Atem
        private const float SmokePerSecond = 2.4f;
        private const float SmokeSeconds = 13f;
        private const float SmokeAlpha = 0.26f;
        private const float MinSmokePixels = 26f;
        private const float SteamPerSecond = 2.2f;
        private const float SteamSeconds = 4.5f;
        private const float SteamAlpha = 0.13f;
        private const float BreathSeconds = 1.3f;
        private const int FogBanks = 5;
        private const float FogAlpha = 0.06f;
        private const float FogDrift = 9f;

        // Arbeiter
        private const float MinPace = 0.95f;
        private const float MaxPace = 1.2f;
        private const float StepsPerMeter = 4.2f;
        private const float LaneMeters = 1.5f;
        private const float GateFade = 0.06f;
        // Brennweite als Anteil der Bildbreite. Kleiner heißt weiterer Winkel: Die Straße wird kürzer, der Zug rückt schneller vor.
        private const float FocalShare = 0.42f;
        private const float StartBefore = 0.72f;
        private const float MinGroupPause = 0.7f;
        private const float MaxGroupPause = 3.2f;
        private const float LullChance = 0.12f;
        private const int MaxWorkers = 80;
        private const float BikeShare = 0.035f;
        private const float MirrorAlpha = 0.3f;
        private const float MirrorSquash = 0.62f;
        private const float SimpleBelowPixels = 77f;
        private const float MirrorAbovePixels = 95f;
        private const float BreathAbovePixels = 103f;
        private const float RimShift = 0.014f;
        private static readonly Color Shadow = new(0.012f, 0.014f, 0.02f);
        private static readonly Color FarShadow = new(0.03f, 0.036f, 0.05f);
        private static readonly Color Rim = new(0.2f, 0.22f, 0.28f);

        // Licht, vor dem die Gestalten stehen
        private const float PoolAlpha = 0.17f;
        private const float PoolMeters = 2.6f;
        private const float HaloMeters = 1.05f;
        // Die Kamera atmet: Das Bild driftet kaum merklich, nahe Ebenen weiter als ferne. Nur mit Ebenen aus title.json.
        private const float DriftPixels = 11f;
        private const float BaseZoom = 1.024f;
        private const float BreatheZoom = 0.006f;
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
        // Klänge der Ton-Session, bewusst unter deren Vorschlägen gehalten: Das Titelthema muss darüber gut hörbar bleiben.
        private const float CrowdDb = -17f;
        private const float CrowdQuietDb = -40f;
        private const float CrowdReach = 600f;
        private const float TramDb = -24f;
        private const float SparkDb = -15f;
        private const float SparkSoundPause = 1.3f;
        private const float HornDb = -24f;
        private const float FirstHorn = 3f;
        private const float MinHornPause = 70f;
        private const float MaxHornPause = 130f;
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
            public float Depth;
            public float Meters;
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
            public int Layer = -1;
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
        private readonly List<Pane> _letters = new();
        private readonly List<Texture2D?> _images = new();
        private readonly List<Texture2D?> _lit = new();
        private float _depth = 0.5f;
        private int _deadLetter = -1;
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
        private AudioStreamPlayer2D? _crowd;
        private float _nextSparkSound;
        private float _nextHorn = FirstHorn;

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

        /// <summary>
        /// Die Geräusche des Spiels. Mit ihnen hört man die Menge gehen, die Bahn fahren, die Funken knistern und ab und zu
        /// das Schichthorn, alles leise unter dem Titelthema. Ohne bleibt die Szene stumm.
        /// </summary>
        public SoundScape? Sound;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        // Das Titelbild füllt die Fläche und wird dabei mittig beschnitten. Mit Ebenen kommt die Kamera dazu: leicht
        // vergrößert, damit beim Driften kein Rand frei wird, und je Ebene nach ihrer Tiefe verschoben.
        private float FitScale => Mathf.Max(Size.X / _at.Size.X, Size.Y / _at.Size.Y);

        private float Zoom => _at.Layered ? BaseZoom + BreatheZoom * Mathf.Sin(_time * 0.09f) : 1f;

        private float ImageScale => FitScale * Zoom;

        private Vector2 Drift => _at.Layered ? new Vector2(Mathf.Sin(_time * 0.11f), 0.4f * Mathf.Sin(_time * 0.07f + 1f)) * DriftPixels : Vector2.Zero;

        /// <summary>Ein Bildpunkt des Titelbilds im Fenster, für die Ebene, deren Tiefe gerade in _depth steht.</summary>
        private Vector2 OnScreen(Vector2 pixel)
        {
            var middle = _at.Size / 2f;
            return Size / 2f + (pixel - middle + Drift * (_depth - 0.5f) * 2f) * ImageScale;
        }

        private Vector2 OnScreen(float x, float y) => OnScreen(new Vector2(x, y));

        private Rect2 OnScreen(Rect2 area) => new(OnScreen(area.Position), area.Size * ImageScale);

        private float Strong => Mathf.Clamp(Strength, 0f, 1f);

        private float Depth(string layer, float fallback) => _at.DepthOf(layer, fallback);

        // Der Maßstab am Ort (Bildpunkte je Meter für Aufrechtes) fällt mit der Entfernung: Maßstab = Brennweite / Tiefe.
        // Daraus ergibt sich, wie weit ein Punkt des Wegs weg ist, und die Arbeiter können in Metern gehen.
        private float Focal => _at.Size.X * FocalShare;

        private float NearDepth => Focal / _at.Walk[0].Scale;

        private float GateDepth => Focal / _at.Walk[^1].Scale;

        /// <summary>Fußpunkt und Maßstab in dieser Entfernung auf dem Weg. Vor dem ersten Punkt läuft der Weg gerade weiter aus dem Bild.</summary>
        private (Vector2 At, float Scale) Path(float depth)
        {
            float scale = Focal / Mathf.Max(0.1f, depth);
            int last = _at.Walk.Count - 1, i = 0;
            while (i < last - 1 && _at.Walk[i + 1].Scale > scale)
            {
                i++;
            }
            var (a, big) = _at.Walk[i];
            var (b, small) = _at.Walk[i + 1];
            // Entlang einer Geraden am Boden wandert der Fußpunkt gleichmäßig mit dem Maßstab.
            return (a + (b - a) * ((big - scale) / Mathf.Max(0.001f, big - small)), scale);
        }

        private float Progress(Worker w) => Mathf.Clamp((w.Depth - NearDepth) / (GateDepth - NearDepth), 0f, 1f);

        /// <summary>Füße eines Arbeiters im Bild und sein Maßstab. Vorn streut der Zug, zum Tor hin läuft er zusammen.</summary>
        private (Vector2 Foot, float Scale) Stand(Worker w)
        {
            var (at, scale) = Path(w.Depth);
            return (at + new Vector2(Mathf.Lerp(w.Lane, w.EndLane, Progress(w)) * scale, 0f), scale);
        }

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
            string folder = ProjectSettings.GlobalizePath(LayoutPath).GetBaseDir();
            foreach (var layer in _at.Layers)
            {
                _images.Add(LoadImage(folder.PathJoin(layer.File)));
                _lit.Add(layer.Lit == null ? null : LoadImage(folder.PathJoin(layer.Lit)));
            }
            using var picture = _at.Layered ? null : Picture?.GetImage();
            if (picture != null && picture.IsCompressed())
            {
                picture.Decompress();
            }
            int flickers = _at.Windows.Count > 0 ? _rng.Next(_at.Windows.Count) : -1;
            for (int i = 0; i < _at.Windows.Count; i++)
            {
                var (area, layer) = _at.Windows[i];
                // Ein Teil der Fenster ist anfangs dunkel und geht erst später an.
                bool on = _rng.NextDouble() >= StartDarkShare;
                _panes.Add(new Pane
                {
                    Area = area, On = on, Dark = on ? 0f : 1f, NextToggle = Rand(3f, MaxToggleSeconds), Flickers = i == flickers,
                    Mask = picture == null ? null : MakeMask(picture, area.Grow(1f)), Layer = _at.Layers.FindIndex(l => l.Name == layer),
                });
            }
            int factory = _at.Layers.FindIndex(l => l.Name == "factory");
            _sign = new Pane { Area = _at.Sign, Mask = picture == null ? null : MakeMask(picture, _at.Sign), Layer = factory };
            foreach (var letter in _at.Letters)
            {
                _letters.Add(new Pane { Area = letter, Layer = factory });
            }
            // Ein Buchstabe der Leuchtschrift ist hinüber und fällt immer wieder aus.
            _deadLetter = _letters.Count > 0 ? _rng.Next(_letters.Count) : -1;
            for (int i = 0; i < Drips; i++)
            {
                _drips.Add(new Drip { Eave = i % Mathf.Max(1, _at.Eaves.Count), Wait = Rand(0f, 3f), Shift = Rand(-14f, 14f) });
            }
            // Die Straße ist schon bevölkert, wenn das Bild erscheint.
            for (float depth = NearDepth; depth < GateDepth; depth += Rand(0.8f, 3.4f))
            {
                AddGroup(depth);
            }
        }

        private static Texture2D? LoadImage(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                GD.PushWarning($"Ebene des Titelbilds fehlt: {path}");
                return null;
            }
            var image = Image.LoadFromFile(path);
            image.GenerateMipmaps();
            return ImageTexture.CreateFromImage(image);
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
                AddGroup(NearDepth * Rand(StartBefore, StartBefore + 0.08f));
            }
            foreach (var w in _workers)
            {
                // Gleichmäßiger Schritt in Metern: Im Bild werden die Gestalten zum Tor hin langsamer und kleiner.
                w.Depth += w.Pace * dt;
                w.Meters += w.Pace * dt;
                float scale = Path(w.Depth).Scale;
                if (PersonPixels(w, scale) > BreathAbovePixels && _time > w.NextBreath)
                {
                    // Atem in der kalten Luft, nur vorn zu sehen.
                    w.NextBreath = _time + Rand(2.2f, 3.4f);
                    var (foot, _) = Stand(w);
                    _puffs.Add(new Puff
                    {
                        Pos = foot + new Vector2(0.18f, -0.86f * w.Tall * TitleLayout.PersonMeters) * scale, Drift = new Vector2(10f + 30f * _gust, -9f),
                        Life = BreathSeconds, Size = 0.07f * scale + 3f, Grow = 1.6f, Color = new Color(SteamColor, 0.16f),
                    });
                }
            }
            float gate = GateDepth;
            _workers.RemoveAll(w => w.Depth >= gate);
            // Ferne zuerst zeichnen, Nahe darüber.
            _workers.Sort((a, b) => b.Depth.CompareTo(a.Depth));
        }

        /// <summary>Höhe der Gestalt in Bildpunkten des Titelbilds.</summary>
        private static float PersonPixels(Worker w, float scale) => TitleLayout.PersonMeters * w.Tall * scale;



        private void AddGroup(float depth)
        {
            int count = _rng.Next(10) switch { < 4 => 1, < 8 => 2, _ => 3 };
            float lane = Rand(-LaneMeters, LaneMeters), end = Rand(-LaneMeters, LaneMeters) * 0.5f, pace = Rand(MinPace, MaxPace);
            bool bike = count == 1 && _rng.NextDouble() < BikeShare * 3f;
            for (int i = 0; i < count; i++)
            {
                _workers.Add(new Worker
                {
                    Depth = depth + i * Rand(0.5f, 1.4f), Lane = lane + i * Rand(0.5f, 0.9f), EndLane = end + i * Rand(0.4f, 0.7f),
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
            bool trouble = _time < _signOutUntil;
            if (_sign != null)
            {
                _sign.Dark = trouble && _letters.Count == 0 && _rng.NextDouble() < 0.6 ? Rand(0.55f, 1f) : 0f;
            }
            for (int i = 0; i < _letters.Count; i++)
            {
                // Der kaputte Buchstabe zuckt bei jeder Störung und bleibt manchmal ganz weg, die anderen flackern nur selten mit.
                bool hit = trouble && (i == _deadLetter || _rng.NextDouble() < 0.04);
                _letters[i].Dark = hit && _rng.NextDouble() < 0.7 ? 1f : i == _deadLetter && Mathf.Sin(_time * 0.21f) > 0.86f ? 1f : 0f;
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
                    Sound?.PlayEverywhere("tram", TramDb);
                }
                return;
            }
            _tramX += _tramWay * TramSpeed * dt;
            if (_rng.NextDouble() < 1.6 * dt)
            {
                _spark = Rand(0.6f, 1f);
                if (_time > _nextSparkSound && TramShowing() > 0.2f)
                {
                    // Nicht jeder Funke knistert, sonst übertönt die Bahn die Musik.
                    _nextSparkSound = _time + SparkSoundPause;
                    Sound?.PlayEverywhere("tram_spark", SparkDb);
                }
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
            if (_time > _nextHorn)
            {
                // Das Schichthorn ruft den Zug zum Tor: gleich zu Beginn und dann in langen Abständen.
                _nextHorn = _time + Rand(MinHornPause, MaxHornPause);
                Sound?.PlayEverywhere("factory_horn", HornDb);
            }
            UpdateCrowdSound();
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
                    _leafletY = Path(NearDepth * Rand(1.1f, 2.2f)).At.Y;
                    break;
            }
        }

        /// <summary>Die Schritte der Menge laufen als Schleife mit und werden mit dem Zug dichter und dünner.</summary>
        private void UpdateCrowdSound()
        {
            if (Sound == null)
            {
                return;
            }
            if (_crowd == null)
            {
                _crowd = Sound.AddMovingLoop("crowd_steps", CrowdQuietDb, CrowdReach);
                _crowd.Play();
            }
            // Der Spieler hängt an der Welt des Spiels: Er bleibt in der Bildmitte, damit er überall gleich klingt.
            if (GetViewport().GetCamera2D() is Camera2D camera)
            {
                _crowd.GlobalPosition = camera.GetScreenCenterPosition();
            }
            float full = Mathf.Clamp(_workers.Count / 30f, 0f, 1f);
            _crowd.VolumeDb = Mathf.MoveToward(_crowd.VolumeDb, Mathf.Lerp(CrowdQuietDb, CrowdDb, full) + Mathf.LinearToDb(Mathf.Max(0.01f, Strong)), 0.5f);
        }

        public override void _ExitTree()
        {
            // Mit dem Startbildschirm verstummt die Menge.
            _crowd?.QueueFree();
            _crowd = null;
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
                var (middle, radius) = _at.Puddles[_rng.Next(_at.Puddles.Count)];
                _rings.Add(new Ring { At = middle + new Vector2(Rand(-radius.X, radius.X), Rand(-radius.Y, radius.Y)) * 0.8f, Size = Rand(0.2f, 0.42f) * radius.X });
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
            _smokeDebt += SmokePerSecond * _at.Stacks.Count * dt;
            while (_smokeDebt >= 1f && _at.Stacks.Count > 0)
            {
                _smokeDebt -= 1f;
                var stack = _at.Stacks[_rng.Next(_at.Stacks.Count)];
                _puffs.Add(new Puff
                {
                    Pos = new Vector2(stack.X + Rand(-0.25f, 0.25f) * stack.Z, stack.Y), Drift = new Vector2(wind, -15f), Life = SmokeSeconds,
                    Size = Rand(0.9f, 1.4f) * Mathf.Max(stack.Z, MinSmokePixels), Grow = 3.6f,
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
            if (_at.Layered)
            {
                // Von hinten: Himmel, das Luftschiff davor, dann das Werk mit seinen Lichtern und die Straße.
                PaintLayer(c, "sky");
                _depth = Depth("sky", 0f) * 0.5f + Depth("factory", 0.2f) * 0.5f;
                DrawAirship(c, scale);
                PaintLayer(c, "factory");
                PaintLayer(c, "street");
                return;
            }
            _depth = 0.5f;
            DrawAirship(c, scale);
            foreach (var pane in _panes)
            {
                DrawPane(c, pane, scale);
            }
            if (_sign != null)
            {
                Cover(c, _sign, OnScreen(_sign.Area), _sign.Dark);
            }
            if (!_at.OwnLaundry)
            {
                return;
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

        /// <summary>Zeichnet eine Ebene des Titelbilds und darüber ihre Fenster, soweit sie gerade leuchten.</summary>
        private void PaintLayer(CanvasItem c, string name)
        {
            int index = _at.Layers.FindIndex(l => l.Name == name);
            if (index < 0)
            {
                return;
            }
            var layer = _at.Layers[index];
            _depth = layer.Depth;
            if (_images[index] is Texture2D image)
            {
                c.DrawTextureRect(image, OnScreen(layer.Area), false);
            }
            if (_lit[index] == null)
            {
                return;
            }
            float scale = ImageScale;
            foreach (var pane in _panes)
            {
                if (pane.Layer == index)
                {
                    DrawPane(c, pane, scale);
                }
            }
            if (name != "factory")
            {
                return;
            }
            if (_letters.Count == 0 && _sign != null)
            {
                Light(c, _sign, 1f - _sign.Dark);
            }
            foreach (var letter in _letters)
            {
                Light(c, letter, 1f - letter.Dark);
            }
        }

        /// <summary>Ein Fenster in seinem jetzigen Zustand: an, aus, flackernd oder mit einem Schatten, der vorbeigeht.</summary>
        private void DrawPane(CanvasItem c, Pane pane, float scale)
        {
            float dark = pane.Dark;
            if (pane.Flickers && pane.On && Mathf.Sin(_time * 0.9f) > 0.2f && _rng.NextDouble() < 0.4)
            {
                dark = Rand(0.4f, 0.95f);
            }
            var area = OnScreen(pane.Area.Grow(1f));
            if (pane.Layer >= 0)
            {
                // Mit Ebenen liegt das Fenster dunkel im Bild und wird aus der Leuchtebene eingeschaltet.
                Light(c, pane, 1f - dark);
            }
            else
            {
                Cover(c, pane, area, dark);
            }
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

        /// <summary>Zeichnet den Ausschnitt eines Fensters oder Buchstabens aus der Leuchtebene über seine Ebene.</summary>
        private void Light(CanvasItem c, Pane pane, float level)
        {
            if (level <= 0.01f || pane.Layer < 0 || _lit[pane.Layer] is not Texture2D lit)
            {
                return;
            }
            var layer = _at.Layers[pane.Layer].Area;
            // Die Leuchtebene ist wie ihre Ebene zugeschnitten: Der Ausschnitt zählt ab deren Ecke, in deren Auflösung.
            var factor = lit.GetSize() / layer.Size;
            var source = new Rect2((pane.Area.Position - layer.Position) * factor, pane.Area.Size * factor);
            c.DrawTextureRectRegion(lit, OnScreen(pane.Area), source, new Color(1f, 1f, 1f, level));
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
            _depth = Depth("factory", 0.5f);
            // Der Vorplatz des Werks liegt im Schein der hohen Fenster. Davor zeichnet sich der Zug ab.
            var gate = OnScreen(_at.Gate - new Vector2(0f, 0.6f * _at.GateScale));
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
            _depth = Depth("front", 0.5f);
            for (int i = 0; i < _at.Lamps.Count; i++)
            {
                var (head, meter) = _at.Lamps[i];
                Vector2 foot = _at.LampFoot(i), mirror = _at.LampMirror(i);
                // Die pendelnde Laterne nimmt ihren Lichtkreis mit.
                float swing = i == _at.SwingingLamp ? Mathf.Sin(_time * SwingPerSecond) * (0.5f + 0.5f * _gust) : 0f;
                float level = i == _at.SwingingLamp ? _lamp : 0.94f + 0.06f * Mathf.Sin(_time * 2.1f + i);
                float width = PoolMeters * meter * scale;
                Disc(c, OnScreen(foot + new Vector2(swing * SwingPixels * 6f, 4f)), new Vector2(width, width * 0.24f), new Color(LampColor, PoolAlpha * level * strong));
                float halo = HaloMeters * meter * scale;
                Disc(c, OnScreen(head + new Vector2(swing * SwingPixels, 0f)), new Vector2(halo, halo), new Color(LampColor, 0.2f * level * strong));
                // Träge Spiegelung: Der Lichtstreifen im Pflaster schwillt langsam an und ab und wandert kaum merklich.
                float swell = 0.5f + 0.5f * Mathf.Sin(_time * (0.35f + 0.07f * i) + i * 1.9f);
                Disc(c, OnScreen(mirror + new Vector2(2.5f * Mathf.Sin(_time * 0.5f + i) + swing * SwingPixels * 3f, 0f)),
                    new Vector2(9f, (mirror.Y - foot.Y) * 0.45f) * scale, new Color(LampColor, GlintAlpha * (0.4f + swell) * level * strong));
            }
            _depth = Depth("factory", 0.5f);
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
            _depth = Depth("factory", 0.5f);
            for (int i = 0; i < FogBanks; i++)
            {
                // Dunstbänke treiben in Straßenhöhe langsam nach rechts, nur rechts vom Menü.
                float travel = (_fog[i] + _time * FogDrift * (1f + 2f * _gust) / 1300f * (0.7f + 0.15f * i)) % 1f;
                var half = new Vector2(310f + 45f * i, 60f + 13f * i) * scale;
                var at = OnScreen(_at.QuietUntil + travel * (_at.Size.X - _at.QuietUntil + 300f), _at.Gate.Y - 58f + 34f * i);
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
            _depth = Depth("street", 0.5f);
            DrawWater(c, scale);
            // Die Bahn fährt vor dem Werk und hinter den Häuserzeilen: Mit Ebenen verdecken die Häuser sie von selbst.
            _depth = Depth("factory", 0.5f) * 0.5f + Depth("street", 0.5f) * 0.5f;
            DrawTram(c, scale);
            DrawGuard(c, scale);
            if (_at.Layered)
            {
                PaintLayer(c, "left");
                PaintLayer(c, "right");
            }
            float far = Depth("factory", 0.5f), near = Depth("front", 0.5f);
            foreach (var w in _workers)
            {
                // Der Zug kommt von vorn und geht in die Tiefe: Seine Ebene wandert mit.
                _depth = Mathf.Lerp(near, far, Progress(w));
                DrawWorker(c, w, scale);
            }
            c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            _depth = near;
            DrawCat(c, scale);
            DrawLeaflet(c, scale);
            if (_at.Layered)
            {
                PaintLayer(c, "front");
            }
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
            var (at, meter) = Stand(w);
            var foot = OnScreen(at);
            float tall = PersonPixels(w, meter), height = tall * scale;
            float progress = Progress(w);
            var color = Shadow.Lerp(FarShadow, progress);
            float alpha = Mathf.Clamp((1f - progress) / GateFade, 0f, 1f);
            float step = w.Meters * StepsPerMeter + w.Phase;
            if (tall < SimpleBelowPixels)
            {
                // Weit hinten ist die Gestalt nur noch ein paar Bildpunkte hoch: Der Umriss genügt.
                c.DrawSetTransform(foot, 0f, new Vector2(height, height));
                DrawOutline(c, step, new Color(color, alpha));
                return;
            }
            if (tall > MirrorAbovePixels)
            {
                c.DrawSetTransform(foot, 0f, new Vector2(height, -height * MirrorSquash));
                DrawOutline(c, step, new Color(color, alpha * MirrorAlpha));
            }
            // Der Saum liegt auf der Seite zum Tor hin.
            float side = at.X > _at.Gate.X ? -1f : 1f;
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
            float top = _at.TramGround - _at.TramHeight, unit = _at.TramHeight / 64f;
            Clipped(c, new Rect2(_tramX, top, _at.TramLength, _at.TramHeight), TramBody);
            Clipped(c, new Rect2(_tramX + 8f * unit, top - 5f * unit, _at.TramLength - 16f * unit, 6f * unit), TramBody);
            float light = 0.85f + 0.15f * Mathf.Sin(_time * 23f) * Mathf.Sin(_time * 7f);
            for (float x = 10f * unit; x < _at.TramLength - 22f * unit; x += 24f * unit)
            {
                Clipped(c, new Rect2(_tramX + x, top + 12f * unit, 17f * unit, 22f * unit), new Color(TramLight, 0.82f * light));
            }
            // Stirnlampe in Fahrtrichtung.
            float front = _tramWay > 0 ? _tramX + _at.TramLength - 5f * unit : _tramX + unit;
            Clipped(c, new Rect2(front, top + 40f * unit, 4f * unit, 5f * unit), new Color(1f, 0.95f, 0.85f));
            // Stromabnehmer bis zum Fahrdraht.
            float pole = _tramX + _at.TramLength * 0.5f;
            if (pole > _at.TramFrom && pole < _at.TramTo)
            {
                Vector2 foot = OnScreen(pole, top - 5f * unit), tip = OnScreen(pole - _tramWay * 14f * unit, top - 30f * unit);
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
            var (spot, meter) = Path(GateDepth * 0.9f);
            var foot = OnScreen(new Vector2(_at.Gate.X + Mathf.Lerp(4.5f, -3f, across) * meter, spot.Y));
            float height = TitleLayout.PersonMeters * 1.08f * meter * scale;
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
            var (spot, meter) = Path(NearDepth * 1.25f);
            var at = OnScreen(Mathf.Lerp(_at.Size.X + 40f, spot.X - 1.5f * meter, t), spot.Y - 0.2f * meter * t);
            float size = 0.3f * meter * scale, leap = Mathf.Abs(Mathf.Sin(t * 26f));
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
                    _depth = 0.5f;
                    var quiet = OnScreen(_at.QuietUntil, 0f);
                    c.DrawRect(new Rect2(quiet.X, 0f, Size.X - quiet.X, Size.Y), new Color(FlashColor, StreetFlashAlpha * _flash * strong));
                    Disc(c, new Vector2(quiet.X, Size.Y * 0.6f), new Vector2(Size.X * 0.22f, Size.Y * 0.7f), new Color(FlashColor, StreetFlashAlpha * 0.6f * _flash * strong));
                }
            }
            if (!Scene || float.IsNaN(_tramX) || _spark <= 0.02f)
            {
                return;
            }
            float unit = _at.TramHeight / 64f;
            float pole = _tramX + _at.TramLength * 0.5f - _tramWay * 14f * unit;
            if (pole <= _at.TramFrom || pole >= _at.TramTo)
            {
                return;
            }
            float scale = ImageScale * unit;
            _depth = Depth("factory", 0.5f) * 0.5f + Depth("street", 0.5f) * 0.5f;
            var tip = OnScreen(pole, _at.TramGround - _at.TramHeight - 30f * unit);
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
