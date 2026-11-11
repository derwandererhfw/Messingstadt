using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Leben über dem Startbildschirm: Arbeiter ziehen als dunkle Gestalten die Straße hinunter zum Werkstor, in den
    /// Häusern geht hier und da ein Licht an oder aus, hinter den Werksfenstern zuckt Schweißlicht, am Tor glüht der
    /// Ofen. Dazu Regen, ziehender Dunst und Rauch, fernes Wetterleuchten und eine träge Spiegelung auf dem nassen
    /// Pflaster. Alles liegt als eigene Ebene über dem stehenden Titelbild und rechnet in dessen Bildpunkten
    /// (1920 x 1080, eingepasst wie KeepAspectCovered). Links, wo das Menü steht, bewegt sich nichts außer dem Regen.
    /// </summary>
    public partial class TitleAmbience : Control
    {
        private static readonly Vector2 ImageSize = new(1920f, 1080f);

        // Regen und Wetter
        private const int Drops = 170;
        private const float FallSpeed = 520f;
        private const float DriftSpeed = 90f;
        private const float DropLength = 13f;
        private const float RainAlpha = 0.2f;
        private const float MinStormPause = 22f;
        private const float MaxStormPause = 55f;
        private const float FirstStorm = 9f;
        private const float FlashFadePerSecond = 6f;
        private const float FlashAlpha = 0.24f;
        private const float LampRadius = 0.16f;
        private const float LampAlpha = 0.3f;
        private const float StutterChancePerSecond = 0.25f;
        private const int PuffsPerSecond = 3;
        private const float PuffSeconds = 9f;
        private const float PuffAlpha = 0.1f;
        private const int FogBanks = 5;
        private const float FogAlpha = 0.05f;
        private const float FogDrift = 9f;
        private const float EdgeDark = 0.62f;
        private const float OverallDark = 0.14f;
        private static readonly Color RainColor = new(0.78f, 0.84f, 1f);
        private static readonly Color LampColor = new(0.95f, 0.9f, 0.82f);
        private static readonly Color FlashColor = new(0.62f, 0.7f, 0.95f);
        private static readonly Color SteamColor = new(0.42f, 0.44f, 0.5f);
        private static readonly Color FogColor = new(0.5f, 0.56f, 0.7f);

        // Die Straße im Titelbild: Fluchtpunkt am Werkstor. Ein Arbeiter in der Tiefe z steht mit den Füßen auf
        // HorizonY + GroundDrop / z und ist FigureHeight / z Bildpunkte groß. z 1 ist vorn, das Tor liegt bei GateZ.
        private const float VanishX = 1215f;
        private const float HorizonY = 665f;
        private const float GroundDrop = 235f;
        private const float FigureHeight = 247f;
        private const float GateZ = 7.8f;
        private const float GateFadeZ = 0.9f;
        private const float MinStartZ = 0.6f;
        private const float MaxStartZ = 0.74f;
        private const float MinStartLane = 540f;
        private const float MaxStartLane = 900f;
        private const float GateLane = 150f;
        private const float MinPace = 0.125f;
        private const float MaxPace = 0.16f;
        private const float StepsPerZ = 15f;
        private const float MinGroupPause = 2.5f;
        private const float MaxGroupPause = 9f;
        private const int MaxWorkers = 26;
        private const float MirrorAlpha = 0.28f;
        private const float MirrorSquash = 0.62f;
        private const float SimpleFromZ = 3.2f;
        private const float MirrorUntilZ = 2.6f;
        private static readonly Color Shadow = new(0.012f, 0.014f, 0.02f);
        private static readonly Color FarShadow = new(0.075f, 0.09f, 0.125f);

        // Helle Fenster im Titelbild (x, y, Breite, Höhe), ausgemessen an title.png. Malt die Grafik das Bild neu,
        // müssen diese Felder und die Werksfenster darunter nachgezogen werden.
        private static readonly Rect2[] Windows =
        {
            new(808, 440, 18, 88), new(974, 328, 4, 52), new(1036, 488, 4, 40), new(1394, 512, 4, 34), new(1428, 402, 4, 38),
            new(1428, 478, 4, 42), new(1438, 384, 4, 44), new(1450, 368, 6, 46), new(1450, 458, 4, 46), new(1496, 304, 6, 56),
            new(1516, 160, 6, 64), new(1516, 394, 6, 60), new(1538, 154, 8, 34), new(1538, 498, 8, 64), new(1540, 254, 4, 56),
            new(1564, 92, 10, 56), new(1564, 344, 10, 74), new(1598, 66, 8, 30), new(1598, 164, 8, 80), new(1634, 440, 16, 88),
            new(1742, 380, 18, 110), new(1812, 330, 40, 136),
        };
        private static readonly Rect2[] WorksPanes = { new(1092, 534, 12, 104), new(1126, 534, 26, 104), new(1270, 534, 26, 104), new(1318, 534, 26, 104) };
        private static readonly Vector2 Gate = new(1215f, 668f);
        // Wo die Laternen sich im nassen Pflaster spiegeln: Mitte und halbe Höhe des Streifens.
        private static readonly (Vector2 At, float Half)[] Glints =
        {
            (new(884, 905), 70), (new(1048, 800), 46), (new(1388, 800), 40), (new(1436, 822), 46), (new(1512, 900), 70), (new(1692, 950), 80),
        };
        private const float MinToggleSeconds = 14f;
        private const float MaxToggleSeconds = 70f;
        private const float SwitchSeconds = 0.18f;
        private const float OffAlpha = 0.94f;
        private const float PasserChancePerSecond = 0.012f;
        private const float PasserShade = 0.6f;
        private const float StartDarkShare = 0.3f;
        private static readonly Color DarkGlass = new(0.075f, 0.078f, 0.088f);
        private const float MaskFromLight = 0.2f;
        private const float MaskFullLight = 0.42f;
        private static readonly Color WeldColor = new(0.6f, 0.78f, 1f);
        private const float WeldAlpha = 0.55f;
        private const float MinWeldPause = 4f;
        private const float MaxWeldPause = 13f;
        private static readonly Color FurnaceColor = new(1f, 0.46f, 0.16f);
        private const float FurnaceRest = 0.09f;
        private const float FurnaceSurge = 0.3f;
        private const float FurnaceRadius = 95f;
        private const float MinSurgePause = 14f;
        private const float MaxSurgePause = 32f;
        private const float SurgeSeconds = 4f;
        private const float GlintAlpha = 0.07f;

        private sealed class Puff
        {
            public Vector2 Pos;
            public float Age;
            public float Size;
        }

        private sealed class Worker
        {
            public float Z;
            public float StartZ;
            public float Lane;
            public float EndLane;
            public float Pace;
            public float Phase;
            public float Tall;
            public float Stoop;
            public bool Pail;
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

        private readonly Random _rng = new();
        private readonly Vector2[] _drops = new Vector2[Drops];
        private readonly Vector2[] _lines = new Vector2[Drops * 2];
        private readonly List<Puff> _puffs = new();
        private readonly List<float> _strokes = new();
        private readonly List<Worker> _workers = new();
        private readonly List<Pane> _panes = new();
        private readonly float[] _fog = new float[FogBanks];
        private Glow _glow = null!;
        private Texture2D _round = null!;
        private Texture2D _sky = null!;
        private Texture2D _edge = null!;
        private float _time;
        private float _flash;
        private float _nextStorm = FirstStorm;
        private float _stutterUntil;
        private float _lamp = 1f;
        private float _puffDebt;
        private float _nextGroup = 1.5f;
        private float _weldUntil;
        private float _nextWeld = 3f;
        private int _weldPane;
        private float _weld;
        private float _nextSurge = 11f;
        private float _surgeStart = -100f;
        private float _furnace = FurnaceRest;

        /// <summary>Wie stark alles wirkt, von 0 (aus) bis 1. Kleiner, wenn der Hintergrund schon unruhig ist.</summary>
        public float Strength = 1f;

        /// <summary>Wo die Laterne im Titelbild steht, anteilig. Negativ: keine Laterne.</summary>
        public Vector2 LampAt = new(0.16f, 0.4f);

        /// <summary>Wo Rauch aufsteigt, anteilig, etwa aus den Schloten im Titelbild.</summary>
        public List<Vector2> SteamAt = new() { new Vector2(0.78f, 0.86f), new Vector2(0.3f, 0.92f) };

        /// <summary>Prüflauf: hält den Blitz auf dieser Stärke, negativ heißt aus.</summary>
        public float HeldFlash = -1f;

        /// <summary>
        /// Arbeiter, Fenster, Werk und Spiegelung gehören zum Titelbild und sitzen auf dessen Bildpunkten. Ohne Titelbild
        /// abschalten, dann bleiben Regen, Wetterleuchten, Laterne und Rauch.
        /// </summary>
        public bool Scene = true;

        /// <summary>
        /// Das Titelbild selbst. Daraus entstehen Masken in Fensterform, damit ein Fenster, das ausgeht, genau seine
        /// Scheibe verdunkelt. Ohne Bild wird das Feld um das Fenster abgedeckt.
        /// </summary>
        public Texture2D? Picture;

        /// <summary>Die Leuchtebene liegt getrennt, damit Licht aufhellt statt zu überdecken.</summary>
        private sealed partial class Glow : Control
        {
            public TitleAmbience Owner_ = null!;

            public override void _Draw() => Owner_.DrawGlow(this);
        }

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        // Das Titelbild füllt die Fläche und wird dabei mittig beschnitten. So liegt ein Bildpunkt des Bilds im Fenster.
        private float ImageScale => Mathf.Max(Size.X / ImageSize.X, Size.Y / ImageSize.Y);

        private Vector2 OnScreen(Vector2 pixel) => pixel * ImageScale + (Size - ImageSize * ImageScale) / 2f;

        private Rect2 OnScreen(Rect2 area) => new(OnScreen(area.Position), area.Size * ImageScale);

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
            _glow = new Glow
            {
                Owner_ = this, MouseFilter = MouseFilterEnum.Ignore,
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            };
            AddChild(_glow);
            _glow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            for (int i = 0; i < FogBanks; i++)
            {
                _fog[i] = Rand(0f, 1f);
            }
            int flickers = _rng.Next(Windows.Length);
            using var picture = Picture?.GetImage();
            if (picture != null && picture.IsCompressed())
            {
                picture.Decompress();
            }
            for (int i = 0; i < Windows.Length; i++)
            {
                // Ein Teil der Fenster ist anfangs dunkel und geht erst später an.
                bool on = _rng.NextDouble() >= StartDarkShare;
                _panes.Add(new Pane
                {
                    Area = Windows[i], On = on, Dark = on ? 0f : 1f, NextToggle = Rand(4f, MaxToggleSeconds), Flickers = i == flickers,
                    Mask = picture == null ? null : MakeMask(picture, Windows[i].Grow(1f)),
                });
            }
            // Die Straße ist schon bevölkert, wenn das Bild erscheint.
            for (float z = 1.1f; z < GateZ; z += Rand(0.5f, 1.6f))
            {
                AddGroup(z);
            }
        }

        /// <summary>Wo das Bild im Feld hell ist, ist Scheibe: Die Maske deckt nur dort.</summary>
        private static Texture2D MakeMask(Image picture, Rect2 area)
        {
            float factor = picture.GetWidth() / ImageSize.X;
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
            UpdateRain(dt);
            UpdateStorm(dt);
            UpdateLamp(dt);
            UpdateSteam(dt);
            if (Scene)
            {
                UpdateWorkers(dt);
                UpdatePanes(dt);
                UpdateWorks(dt);
            }
            QueueRedraw();
            _glow.QueueRedraw();
        }

        private void UpdateRain(float dt)
        {
            var size = Size;
            if (size.X <= 0f || size.Y <= 0f)
            {
                return;
            }
            var step = new Vector2(DriftSpeed / size.X, FallSpeed / size.Y) * dt;
            var tail = new Vector2(DriftSpeed, FallSpeed).Normalized() * DropLength;
            for (int i = 0; i < Drops; i++)
            {
                _drops[i] += step;
                if (_drops[i].Y > 1f || _drops[i].X > 1f)
                {
                    _drops[i] = new Vector2((float)_rng.NextDouble() * 1.15f - 0.15f, 0f);
                }
                var p = _drops[i] * size;
                _lines[i * 2] = p;
                _lines[i * 2 + 1] = p + tail;
            }
        }

        /// <summary>Zwei bis vier schnelle Entladungen weit hinter der Stadt, ohne sichtbaren Strahl.</summary>
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
            if (_time > _stutterUntil && _rng.NextDouble() < StutterChancePerSecond * dt)
            {
                _stutterUntil = _time + Rand(0.2f, 0.7f);
            }
            float unrest = 0.92f + 0.05f * Mathf.Sin(_time * 3f) + 0.03f * Mathf.Sin(_time * 11f);
            _lamp = _time < _stutterUntil && _rng.NextDouble() < 0.55 ? 0.15f : unrest;
        }

        private void UpdateSteam(float dt)
        {
            _puffDebt += PuffsPerSecond * SteamAt.Count * dt;
            while (_puffDebt >= 1f && SteamAt.Count > 0)
            {
                _puffDebt -= 1f;
                var at = SteamAt[_rng.Next(SteamAt.Count)] * Size;
                _puffs.Add(new Puff { Pos = at + new Vector2(Rand(-8f, 8f), 0f), Size = Rand(16f, 30f) });
            }
            foreach (var p in _puffs)
            {
                p.Age += dt;
                p.Pos += new Vector2(11f + 6f * Mathf.Sin(_time * 0.7f + p.Size), -13f) * dt;
            }
            _puffs.RemoveAll(p => p.Age >= PuffSeconds);
        }

        /// <summary>Einzeln und in kleinen Gruppen, in unregelmäßigen Abständen: Schichtwechsel im Werk.</summary>
        private void UpdateWorkers(float dt)
        {
            if (_time > _nextGroup && _workers.Count < MaxWorkers)
            {
                // Manchmal reißt der Zug ab, dann kommt eine Weile niemand.
                _nextGroup = _time + (_rng.Next(5) == 0 ? Rand(MaxGroupPause, MaxGroupPause * 2f) : Rand(MinGroupPause, MaxGroupPause));
                AddGroup(Rand(MinStartZ, MaxStartZ));
            }
            foreach (var w in _workers)
            {
                w.Z += w.Pace * dt;
            }
            _workers.RemoveAll(w => w.Z >= GateZ);
            // Ferne zuerst zeichnen, Nahe darüber.
            _workers.Sort((a, b) => b.Z.CompareTo(a.Z));
        }

        private void AddGroup(float z)
        {
            int count = _rng.Next(10) switch { < 5 => 1, < 8 => 2, _ => 3 };
            float lane = Rand(MinStartLane, MaxStartLane), end = Rand(-GateLane, GateLane), pace = Rand(MinPace, MaxPace);
            for (int i = 0; i < count; i++)
            {
                _workers.Add(new Worker
                {
                    Z = z + i * Rand(0.03f, 0.09f), StartZ = MinStartZ, Lane = lane + i * Rand(35f, 70f), EndLane = end + i * Rand(20f, 45f),
                    // Wer zusammen geht, hält fast denselben Schritt.
                    Pace = pace * Rand(0.97f, 1.03f), Phase = Rand(0f, Mathf.Tau), Tall = Rand(0.9f, 1.06f), Stoop = Rand(0.5f, 1f),
                    Pail = _rng.Next(3) > 0,
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
        }

        /// <summary>Im Werk wird geschweißt, und ab und zu geht eine Ofentür auf.</summary>
        private void UpdateWorks(float dt)
        {
            if (_time > _nextWeld)
            {
                _weldUntil = _time + Rand(0.5f, 2.2f);
                _nextWeld = _weldUntil + Rand(MinWeldPause, MaxWeldPause);
                _weldPane = _rng.Next(WorksPanes.Length);
            }
            _weld = _time < _weldUntil && _rng.NextDouble() < 0.5 ? Rand(0.5f, 1f) : Mathf.MoveToward(_weld, 0f, dt * 9f);
            if (_time > _nextSurge)
            {
                _surgeStart = _time;
                _nextSurge = _time + SurgeSeconds + Rand(MinSurgePause, MaxSurgePause);
            }
            bool surging = _time - _surgeStart < SurgeSeconds;
            float breathe = FurnaceRest * (1f + 0.25f * Mathf.Sin(_time * 1.3f) + 0.12f * Mathf.Sin(_time * 5.1f));
            _furnace = Mathf.MoveToward(_furnace, surging ? FurnaceSurge * Rand(0.85f, 1f) : breathe, dt * 0.35f);
        }

        public override void _Draw()
        {
            float strength = Mathf.Clamp(Strength, 0f, 1f);
            if (Scene)
            {
                DrawPanes();
                DrawFog(strength);
            }
            foreach (var p in _puffs)
            {
                float f = p.Age / PuffSeconds;
                float radius = p.Size * (1f + 2.6f * f);
                float alpha = PuffAlpha * strength * Mathf.Min(1f, f * 5f) * (1f - f);
                DrawTextureRect(_round, new Rect2(p.Pos - new Vector2(radius, radius), new Vector2(radius * 2f, radius * 2f)), false, new Color(SteamColor, alpha));
            }
            if (Scene)
            {
                foreach (var w in _workers)
                {
                    DrawWorker(w);
                }
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                DrawTextureRect(_edge, new Rect2(Vector2.Zero, Size), false, new Color(1f, 1f, 1f, strength));
            }
            DrawMultiline(_lines, new Color(RainColor, RainAlpha * strength * (1f + _flash)), 1f);
        }

        /// <summary>Ein Fenster, das aus ist, wird mit dunklem Glas überdeckt. Geht jemand vorbei, zieht ein Schatten durch.</summary>
        private void DrawPanes()
        {
            foreach (var pane in _panes)
            {
                float dark = pane.Dark;
                if (pane.Flickers && pane.On && Mathf.Sin(_time * 0.9f) > 0.55f && _rng.NextDouble() < 0.35)
                {
                    dark = Rand(0.4f, 0.9f);
                }
                var area = OnScreen(pane.Area.Grow(1f));
                if (dark > 0.01f && pane.Mask != null)
                {
                    DrawTextureRect(pane.Mask, area, false, new Color(DarkGlass, OffAlpha * dark));
                }
                else if (dark > 0.01f)
                {
                    DrawRect(area, new Color(DarkGlass, OffAlpha * dark));
                }
                float passing = (_time - pane.PasserFrom) / pane.PasserSeconds;
                if (pane.On && passing is >= 0f and < 1f)
                {
                    // Breite Fenster: Der Schatten wandert quer durch. Schmale: Das ganze Fenster wird kurz dunkler.
                    float band = area.Size.X > 12f * ImageScale ? 0.45f : 1f;
                    float x = area.Position.X + (area.Size.X * (1f - band)) * passing;
                    DrawRect(new Rect2(x, area.Position.Y + area.Size.Y * 0.25f, area.Size.X * band, area.Size.Y * 0.75f),
                        new Color(DarkGlass, PasserShade * Mathf.Sin(passing * Mathf.Pi)));
                }
            }
        }

        /// <summary>Dunstbänke treiben in Straßenhöhe langsam nach rechts, nur rechts vom Menü.</summary>
        private void DrawFog(float strength)
        {
            float scale = ImageScale;
            for (int i = 0; i < FogBanks; i++)
            {
                float travel = (_fog[i] + _time * FogDrift / 1500f * (0.7f + 0.15f * i)) % 1f;
                float width = (620f + 90f * i) * scale, height = (120f + 26f * i) * scale;
                var at = OnScreen(new Vector2(700f + travel * 1500f, 610f + 34f * i));
                // An den Enden der Strecke blendet die Bank ein und aus.
                float fade = Mathf.Sin(travel * Mathf.Pi);
                DrawTextureRect(_round, new Rect2(at - new Vector2(width, height) / 2f, new Vector2(width, height)), false, new Color(FogColor, FogAlpha * fade * strength));
            }
        }

        /// <summary>
        /// Ein Arbeiter von hinten, als Schattenriss: schwerer Gang, runde Schultern, der Kopf gesenkt. Gezeichnet in
        /// Einheiten seiner Größe, die Füße im Ursprung. Darunter liegt sein Spiegelbild im nassen Pflaster.
        /// </summary>
        private void DrawWorker(Worker w)
        {
            float progress = Mathf.Clamp((w.Z - w.StartZ) / (GateZ - w.StartZ), 0f, 1f);
            float lane = Mathf.Lerp(w.Lane, w.EndLane, Mathf.SmoothStep(0f, 1f, progress));
            var foot = OnScreen(new Vector2(VanishX + lane / w.Z, HorizonY + GroundDrop / w.Z));
            float height = FigureHeight / w.Z * w.Tall * ImageScale;
            // In der Ferne verliert sich die Gestalt im Dunst, am Tor geht sie im Ofenschein auf.
            var color = Shadow.Lerp(FarShadow, Mathf.Clamp((w.Z - 1f) / 5f, 0f, 1f));
            float alpha = Mathf.Clamp((GateZ - w.Z) / GateFadeZ, 0f, 1f);
            float step = w.Z * StepsPerZ + w.Phase;
            if (w.Z > SimpleFromZ)
            {
                // Weit hinten ist die Gestalt nur noch ein paar Bildpunkte hoch: Rumpf, Beine und Kopf genügen.
                DrawSetTransform(foot, 0f, new Vector2(height, height));
                DrawFarFigure(step, new Color(color, alpha));
                return;
            }
            if (w.Z < MirrorUntilZ)
            {
                DrawSetTransform(foot, 0f, new Vector2(height, -height * MirrorSquash));
                DrawFarFigure(step, new Color(color, alpha * MirrorAlpha));
            }
            DrawSetTransform(foot, 0f, new Vector2(height, height));
            DrawFigure(w, step, new Color(color, alpha));
        }

        private void DrawFarFigure(float step, Color color)
        {
            float lean = 0.02f * Mathf.Sin(step);
            DrawPolygon(new[]
            {
                new Vector2(-0.1f + lean, 0f), new Vector2(0.1f + lean, 0f), new Vector2(0.14f, -0.76f), new Vector2(0.05f, -0.82f),
                new Vector2(0.06f, -0.93f), new Vector2(-0.06f, -0.93f), new Vector2(-0.05f, -0.82f), new Vector2(-0.14f, -0.76f),
            }, new[] { color });
        }

        private void DrawFigure(Worker w, float step, Color color)
        {
            float swing = Mathf.Sin(step), sway = 0.012f * Mathf.Sin(step), bob = 0.012f * Mathf.Abs(Mathf.Cos(step));
            float stoop = 0.035f * w.Stoop;
            var hip = new Vector2(sway, -0.47f + bob);
            // Beine: Der Fuß, der nach vorn schwingt, hebt sich ein wenig.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = swing * side;
                var foot = new Vector2(side * 0.07f + s * 0.012f, -Mathf.Max(0f, s) * 0.035f);
                DrawPolygon(new[]
                {
                    hip + new Vector2(side * 0.05f - 0.05f, 0f), hip + new Vector2(side * 0.05f + 0.05f, 0f),
                    foot + new Vector2(0.032f, 0f), foot + new Vector2(-0.032f, 0f),
                }, new[] { color });
            }
            // Rumpf mit runden Schultern, die Jacke hängt.
            var shoulder = new Vector2(sway * 1.4f, -0.8f + bob + stoop);
            DrawPolygon(new[]
            {
                hip + new Vector2(-0.105f, 0.04f), hip + new Vector2(0.105f, 0.04f), shoulder + new Vector2(0.14f, 0.035f),
                shoulder + new Vector2(0.075f, -0.03f), shoulder + new Vector2(-0.075f, -0.03f), shoulder + new Vector2(-0.14f, 0.035f),
            }, new[] { color });
            // Arme hängen, die Hände pendeln kaum.
            for (int side = -1; side <= 1; side += 2)
            {
                var top = shoulder + new Vector2(side * 0.132f, 0.025f);
                var hand = new Vector2(top.X + side * 0.012f - swing * side * 0.012f, -0.5f + bob);
                DrawPolygon(new[] { top + new Vector2(-0.028f, 0f), top + new Vector2(0.028f, 0f), hand + new Vector2(0.022f, 0f), hand + new Vector2(-0.022f, 0f) }, new[] { color });
                if (side > 0 && w.Pail)
                {
                    // Der Henkelmann.
                    DrawRect(new Rect2(hand.X - 0.035f, hand.Y, 0.07f, 0.085f), color);
                }
            }
            // Der Kopf sinkt zwischen die Schultern, darauf die Schiebermütze.
            var head = shoulder + new Vector2(0f, -0.1f + stoop * 0.9f);
            DrawPolygon(new[]
            {
                head + new Vector2(-0.028f, 0.09f), head + new Vector2(0.028f, 0.09f), head + new Vector2(0.056f, 0.02f), head + new Vector2(0.066f, -0.03f),
                head + new Vector2(0.06f, -0.062f), head + new Vector2(-0.06f, -0.062f), head + new Vector2(-0.066f, -0.03f), head + new Vector2(-0.056f, 0.02f),
            }, new[] { color });
        }

        private void DrawGlow(CanvasItem canvas)
        {
            float strength = Mathf.Clamp(Strength, 0f, 1f);
            var size = Size;
            if (LampAt.X >= 0f)
            {
                float radius = LampRadius * Mathf.Min(size.X, size.Y);
                var at = LampAt * size;
                canvas.DrawTextureRect(_round, new Rect2(at - new Vector2(radius, radius), new Vector2(radius * 2f, radius * 2f)), false,
                    new Color(LampColor, LampAlpha * _lamp * strength));
            }
            if (_flash > 0.01f)
            {
                canvas.DrawTextureRect(_sky, new Rect2(Vector2.Zero, size), false, new Color(FlashColor, FlashAlpha * _flash * strength));
            }
            if (!Scene)
            {
                return;
            }
            float scale = ImageScale;
            if (_weld > 0.01f)
            {
                // Das Schweißlicht steht im Fenster, strahlt in den Dunst davor und zuckt im nassen Pflaster nach.
                var pane = OnScreen(WorksPanes[_weldPane]);
                canvas.DrawRect(pane, new Color(WeldColor, WeldAlpha * _weld * strength));
                float halo = 130f * scale;
                canvas.DrawTextureRect(_round, new Rect2(pane.GetCenter() - new Vector2(halo, halo), new Vector2(halo * 2f, halo * 2f)), false,
                    new Color(WeldColor, 0.2f * _weld * strength));
                var mirror = new Rect2(pane.Position.X, pane.End.Y + 100f * scale, pane.Size.X, pane.Size.Y * 1.1f);
                canvas.DrawRect(mirror, new Color(WeldColor, 0.16f * _weld * strength));
            }
            // Ofenschein am Tor: Dorthin geht der Zug, davor stehen die Gestalten schwarz.
            var gate = OnScreen(Gate);
            var glow = new Vector2(FurnaceRadius * 1.5f, FurnaceRadius) * scale;
            canvas.DrawTextureRect(_round, new Rect2(gate - glow, glow * 2f), false, new Color(FurnaceColor, _furnace * strength));
            var floor = new Vector2(FurnaceRadius * 0.9f, FurnaceRadius * 1.6f) * scale;
            canvas.DrawTextureRect(_round, new Rect2(gate + new Vector2(-floor.X, 20f * scale), floor * 2f), false, new Color(FurnaceColor, _furnace * 0.45f * strength));
            // Träge Spiegelung: Die Lichtstreifen der Laternen im Pflaster schwellen langsam an und ab und wandern kaum merklich.
            for (int i = 0; i < Glints.Length; i++)
            {
                var (at, half) = Glints[i];
                float swell = 0.5f + 0.5f * Mathf.Sin(_time * (0.35f + 0.07f * i) + i * 1.9f);
                var middle = OnScreen(at + new Vector2(2.5f * Mathf.Sin(_time * 0.5f + i), 0f));
                var extent = new Vector2(9f, half) * scale;
                canvas.DrawTextureRect(_round, new Rect2(middle - extent, extent * 2f), false, new Color(LampColor, GlintAlpha * (0.4f + swell) * strength));
            }
        }
    }
}
