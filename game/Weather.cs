using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Wetter und Luft: Nieselregen, Schauer und Pausen mit Böen, Gewitter mit vorberechnetem Blitzlicht,
    /// Bodennebel, der vom Licht der Stadt lebt, und Lichtkegel unter den Laternen. Reine Darstellung.
    /// </summary>
    public partial class Weather : Node2D
    {
        private const float VisibleBoltShare = 0.65f;
        private const float NearStrikeShare = 0.6f;
        private const float MinChimneyHeight = 130f;
        private const float TelegramTiles = 44f;
        private const float MaxChimneyWidth = 0.6f;
        private const int BoltNearZ = 3700;
        private const int BoltFarZ = -1;
        private const float BoltTopMargin = 60f;
        private const float NearThunderDelay = 0.3f;
        private const float NearThunderVolume = -7f;
        private const float EmberSeconds = 2.6f;
        private const float StrikeGlowSeconds = 0.7f;
        private const float DrizzleRain = 0.3f;
        private const float ShowerRain = 1f;
        private const float LullRain = 0.03f;
        private const float RainChangePerSecond = 0.07f;
        private const float RoofFlowPerSecond = 0.1f;
        private const float MinPhaseSeconds = 25f;
        private const float MaxPhaseSeconds = 65f;
        private const float CalmWind = 2f;
        private const float RainWind = 4f;
        private const float GustWind = 34f;
        private const float GustChangePerSecond = 1.6f;
        private const float MinStormPause = 35f;
        private const float MaxStormPause = 95f;
        private const float FirstStormSeconds = 18f;
        private const float FlashFadePerSecond = 7f;
        private const float MinThunderDelay = 0.6f;
        private const float MaxThunderDelay = 4.5f;
        private const float MinRumblePause = 18f;
        private const float MaxRumblePause = 50f;
        private const float RumbleQuietDb = -24f;
        private const float RumbleLoudDb = -14f;
        private const float MinCityPause = 14f;
        private const float MaxCityPause = 38f;
        private static readonly (string Sound, float Volume)[] CitySounds =
        {
            ("dog", -26f), ("dog", -29f), ("siren", -31f), ("train", -27f), ("factory_horn", -29f),
            ("tram", -29f), ("foghorn", -29f), ("church_bell", -30f), ("shutter", -18f), ("distant_shots", -16f),
        };
        private const int ConePoints = 14;
        private const float ConeHeight = 52f;
        private const float ConeRadius = 46f;
        private const float ConeAlpha = 0.3f;
        private const float ConeRainBoost = 0.7f;
        // Hof um die Laterne.
        private const float HaloScale = 0.5f;
        private const float HaloAlpha = 0.32f;

        // Nebel: Der Shader liest die Lichtkarte des Bodens, die Schwaden leuchten also genau dort, wo Licht fällt.
        private const string FogShaderCode = @"
shader_type canvas_item;
uniform sampler2D noise_map : filter_linear, repeat_enable;
uniform sampler2D ground_light : filter_nearest;
uniform vec3 night;
uniform vec2 origin;
uniform vec2 size;
uniform float lightning = 0.0;
uniform float density = 1.0;
uniform float wind_shift = 0.0;
uniform float air = 1.0;
void fragment() {
    vec2 px = UV * size;
    float a = (px.x - origin.x) / 32.0, b = (px.y - origin.y) / 16.0;
    float mx = (a + b) / 2.0, my = (b - a) / 2.0;
    float edge = smoothstep(0.0, 1.5, mx) * smoothstep(0.0, 1.5, my) * smoothstep(20.0, 18.5, mx) * smoothstep(20.0, 18.5, my);
    vec2 iso = vec2(px.x, px.y * 2.0) / 512.0;
    float n = texture(noise_map, iso + vec2(TIME * 0.012 + wind_shift, TIME * 0.004)).r * 0.6
        + texture(noise_map, iso * 1.9 - vec2(TIME * 0.018 - wind_shift * 1.4, -TIME * 0.009)).r * 0.4;
    // Am Kartenrand steht der Smog dicht und verwischt die Grenze zur Stadt dahinter.
    float inside = min(min(mx, my), min(20.0 - mx, 20.0 - my));
    float rim = smoothstep(1.3, 0.0, abs(inside + 0.2));
    float amount = max(smoothstep(0.46, 0.78, n) * 0.5 * density * edge, rim * (0.3 + 0.45 * n));
    vec3 l = texture(ground_light, UV).rgb;
    l = (1.0 - step(inside, 0.0)) * 4.0 * l * l + lightning * vec3(0.5, 0.58, 0.72);
    COLOR = vec4(mix(l * vec3(0.82, 0.86, 0.95) + vec3(0.03, 0.035, 0.05), vec3(0.086, 0.098, 0.17), rim * 0.8) / night, amount * air);
}";

        /// <summary>
        /// Regenfäden im Licht einer Laterne: Nur im Kegel sieht man die Tropfen fallen, dicht unter der Lampe am hellsten.
        /// </summary>
        private sealed partial class ConeRain : Node2D
        {
            private const int Threads = 24;
            private const float FallsPerSecond = 2.3f;
            private const float ThreadLength = 8f;
            private const float ThreadAlpha = 0.75f;
            private const float SplashAlpha = 0.6f;

            public Weather Weather = null!;
            public Color Glow;
            public bool Stutters;

            public override void _Draw()
            {
                float rain = Weather.RainStrength;
                float lamp = Stutters ? Weather.Game.StutterLevel : Weather.Game.UnrestLevel;
                // mehr und hellere Fäden, damit der Regen im Licht aufblitzt.
                float boost = AirTest.Light ? 1.6f : 1f;
                int count = Mathf.CeilToInt(Threads * boost * Mathf.Clamp(rain * 1.5f, 0f, 1f));
                var slant = new Vector2(Weather.Wind * 0.006f, 1f);
                for (int i = 0; i < count; i++)
                {
                    // Feste Zufallswerte je Faden: Platz quer im Kegel, Tiefe und Versatz im Fall.
                    float side = Mathf.Sin(i * 12.9898f) * 43758.547f;
                    side = (side - Mathf.Floor(side)) * 2f - 1f;
                    float deep = Mathf.Sin(i * 78.233f) * 12543.21f;
                    deep = deep - Mathf.Floor(deep);
                    float fall = (Weather._time * FallsPerSecond * (0.85f + 0.3f * deep) + deep * 7f) % 1f;
                    float ground = ConeHeight + (deep - 0.5f) * ConeRadius * 0.7f;
                    // Der Faden fällt senkrecht und wird erst sichtbar, wo er in den Kegel eintritt.
                    float top = Mathf.Abs(side) * ground * 0.9f;
                    var at = new Vector2(side * ConeRadius * 0.85f, top + fall * (ground - top));
                    float alpha = Mathf.Min(1f, ThreadAlpha * boost * lamp * (1f - side * side) * (1f - 0.55f * fall));
                    if (fall > 0.9f)
                    {
                        // Unten zerplatzt der Tropfen auf dem Pflaster.
                        float f = (fall - 0.9f) / 0.1f;
                        var hit = new Vector2(side * ConeRadius * 0.85f, ground);
                        var color = new Color(Glow, SplashAlpha * lamp * (1f - side * side) * (1f - f));
                        DrawRect(new Rect2(hit + new Vector2(-1f - 2f * f, -1.5f * f), new Vector2(0.7f, 0.7f)), color);
                        DrawRect(new Rect2(hit + new Vector2(1f + 2f * f, -1.5f * f), new Vector2(0.7f, 0.7f)), color);
                        continue;
                    }
                    DrawLine(at, at + slant * ThreadLength, new Color(Glow, alpha), AirTest.Light ? 0.8f : 0.6f);
                }
            }
        }

        private sealed class Cone
        {
            public Polygon2D Shape = null!;
            public Sprite2D? Halo;
            public bool Stutters;
        }

        private readonly Random _rng = new();
        private readonly List<Cone> _cones = new();
        private ShaderMaterial _fog = null!;
        private LightningBolt _bolt = null!;
        private LightningBolt _farBolt = null!;
        private LightningBolt? _liveBolt;
        private (Vector2 Top, Vector2 Bottom, bool Front, bool Hit)? _strikePath;
        private readonly List<(Vector2 Top, Vector2 Tile)> _chimneys = new();
        private readonly List<ConeRain> _coneRain = new();
        private Sprite2D _strikeGlow = null!;
        private Sprite2D _ember = null!;
        private float _strikeGlowLeft;
        private float _emberLeft;
        private bool _boltShown;
        private float _nextPhase = 40f;
        private float _rainTarget = DrizzleRain;
        private float _gust;
        private float _gustUntil;
        private float _nextGust = 2f;
        private float _windShift;
        private float _nextStorm = FirstStormSeconds;
        private float _nextRumble = 7f;
        private float _nextCity = 12f;
        private float _time;
        private float _flash;
        private readonly List<float> _strokes = new();
        private float _thunderAt = -1f;
        private string _thunderSound = "";
        private float _thunderVolume;

        public Main Game = null!;
        public Texture2D GroundLight = null!;
        public Texture2D FogNoise = null!;
        public Texture2D Glow = null!;
        public string ManifestPath = "";
        public Vector2 GroundSize;
        public Vector2 GroundOrigin;
        public Color Night;
        public Color LampColor;

        /// <summary>Prüflauf: hält den Blitz auf dieser Stärke, negativ heißt aus.</summary>
        public float HeldFlash = -1f;

        /// <summary>Prüflauf: hält den Regen auf dieser Stärke, negativ heißt Wetterverlauf.</summary>
        public float HeldRain = -1f;

        /// <summary>Regenstärke von 0 (trocken) bis 1 (Schauer). Auch für Geräusche gedacht.</summary>
        public float RainStrength { get; private set; } = DrizzleRain;

        /// <summary>Was gerade von den Dächern läuft: folgt dem Regen mit Verzögerung.</summary>
        public float RoofFlow { get; private set; } = DrizzleRain;

        /// <summary>Wind in Bildpunkten pro Sekunde nach rechts, in Böen stark. Auch für Geräusche gedacht.</summary>
        public float Wind { get; private set; } = CalmWind;

        /// <summary>Feld, von dem das Blitzlicht kommt. Figuren werfen im Blitz ihre Schatten davon weg.</summary>
        public Vector2 FlashFrom { get; private set; } = new(-30f, -30f);

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public override void _Ready()
        {
            _fog = new ShaderMaterial { Shader = new Shader { Code = FogShaderCode } };
            _fog.SetShaderParameter("noise_map", FogNoise);
            _fog.SetShaderParameter("ground_light", GroundLight);
            _fog.SetShaderParameter("night", Night);
            _fog.SetShaderParameter("origin", GroundOrigin);
            _fog.SetShaderParameter("size", GroundSize);
            // Die Nebelfläche hat die Maße des Bodens und liegt direkt darauf, unter den Gebäuden.
            _fog.SetShaderParameter("air", AirTest.Scale);
            AddChild(new Sprite2D { Texture = GroundLight, Centered = false, ZIndex = 0, Material = _fog, Visible = !AirTest.Off("nebel") });
            var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            // Der nahe Strahl trifft einen Schornstein und liegt vorn, der ferne geht hinter den Häusern der Karte nieder.
            _bolt = new LightningBolt { Material = additive, Night = Night, ZIndex = BoltNearZ };
            AddChild(_bolt);
            _farBolt = new LightningBolt { Material = additive, Night = Night, ZIndex = BoltFarZ };
            AddChild(_farBolt);
            _strikeGlow = new Sprite2D { Texture = Glow, Material = additive, ZIndex = BoltNearZ, Scale = new Vector2(0.22f, 0.22f), Visible = false };
            AddChild(_strikeGlow);
            _ember = new Sprite2D { Texture = Glow, Material = additive, ZIndex = BoltNearZ, Scale = new Vector2(0.05f, 0.05f), Visible = false };
            AddChild(_ember);
            ReadChimneys();
        }

        private void ReadChimneys()
        {
            using var manifest = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(ManifestPath));
            foreach (var t in manifest.RootElement.GetProperty("things").EnumerateArray())
            {
                float x0 = t.GetProperty("x0").GetSingle(), y0 = t.GetProperty("y0").GetSingle();
                float x1 = t.GetProperty("x1").GetSingle(), y1 = t.GetProperty("y1").GetSingle();
                float z1 = t.GetProperty("z1").GetSingle();
                if (x1 - x0 <= MaxChimneyWidth && y1 - y0 <= MaxChimneyWidth && z1 >= MinChimneyHeight)
                {
                    var tile = new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f);
                    var foot = Game.ToScreen((int)(tile.X * Messingstadt.Core.World.TileUnits), (int)(tile.Y * Messingstadt.Core.World.TileUnits));
                    _chimneys.Add((foot - new Vector2(0f, z1), tile));
                }
            }
        }

        /// <summary>Regen und Wind von Sekunde zu Sekunde. Eigener Zufall, die Spielregeln kennen kein Wetter.</summary>
        private void UpdateClimate(float dt)
        {
            if (HeldRain >= 0f)
            {
                _rainTarget = HeldRain;
                RainStrength = HeldRain;
                RoofFlow = HeldRain;
            }
            else if (_time > _nextPhase)
            {
                _nextPhase = _time + Rand(MinPhaseSeconds, MaxPhaseSeconds);
                float[] phases = { DrizzleRain, ShowerRain, LullRain };
                float next;
                do
                {
                    next = phases[_rng.Next(phases.Length)];
                }
                while (Mathf.IsEqualApprox(next, _rainTarget));
                _rainTarget = next;
            }
            RainStrength = Mathf.MoveToward(RainStrength, _rainTarget, RainChangePerSecond * dt);
            RoofFlow = Mathf.MoveToward(RoofFlow, RainStrength, RoofFlowPerSecond * dt);
            if (_time > _nextGust)
            {
                _gustUntil = _time + Rand(1.5f, 3.5f);
                _nextGust = _gustUntil + Rand(3f, 8f);
            }
            _gust = Mathf.MoveToward(_gust, _time < _gustUntil ? 1f : 0f, GustChangePerSecond * dt);
            Wind = Mathf.Lerp(CalmWind + RainWind * RainStrength, GustWind, _gust);
            _windShift -= Wind * dt * 0.0012f;
            _fog.SetShaderParameter("wind_shift", _windShift);
            // Starker Regen drückt den Nebel nieder, in den Pausen steigt er.
            _fog.SetShaderParameter("density", Mathf.Lerp(1.5f, 0.75f, RainStrength));
            foreach (var rain in _coneRain)
            {
                rain.QueueRedraw();
            }
        }

        /// <summary>Wohin der Blitz dieses Gewitters fährt: auf einen Schornstein im Bild oder hinter die Häuser in die Stadt.</summary>
        private void PickStrike()
        {
            var canvas = GetCanvasTransform();
            var origin = canvas.AffineInverse() * Vector2.Zero;
            var size = GetViewportRect().Size / canvas.Scale;
            // Oben liegt das Telegramm, dahinter sähe man den Einschlag nicht.
            var view = new Rect2(origin + new Vector2(24f, TelegramTiles), size - new Vector2(48f, TelegramTiles + 24f));
            float topY = origin.Y - BoltTopMargin;
            _strikePath = null;
            var visible = _chimneys.FindAll(c => view.HasPoint(c.Top));
            if (visible.Count > 0 && (HeldFlash >= 0f || _rng.NextDouble() < NearStrikeShare))
            {
                var (top, tile) = visible[_rng.Next(visible.Count)];
                _strikePath = (new Vector2(top.X + Rand(-0.14f, 0.14f) * size.X, topY), top, true, true);
                FlashFrom = tile;
                return;
            }
            for (int tries = 0; tries < 12; tries++)
            {
                var point = origin + new Vector2(Rand(0.1f, 0.9f) * size.X, Rand(0.15f, 0.6f) * size.Y);
                var map = Game.ScreenToMap(point);
                if (map.X < -0.5f || map.Y < -0.5f || map.X > 20.5f || map.Y > 20.5f)
                {
                    // Hinter der Karte verdeckt sie den Strahl, davor fährt er vor allem nieder.
                    bool front = map.X > 20.5f || map.Y > 20.5f;
                    _strikePath = (new Vector2(point.X + Rand(-0.14f, 0.14f) * size.X, topY), point, front, false);
                    FlashFrom = map;
                    return;
                }
            }
            FlashFrom = Game.ScreenToMap(origin + size * new Vector2(Rand(0f, 1f), 0f));
        }

        private void ShowBolt()
        {
            if (_strikePath is not { } path)
            {
                return;
            }
            _liveBolt = path.Front ? _bolt : _farBolt;
            _liveBolt.Strike(path.Top, path.Bottom);
            _boltShown = true;
            if (path.Hit)
            {
                HitChimney(path.Bottom);
            }
        }

        /// <summary>Am getroffenen Schornstein sprühen Funken, die Spitze glüht noch etwas nach.</summary>
        private void HitChimney(Vector2 at)
        {
            _strikeGlow.Position = at;
            _strikeGlowLeft = StrikeGlowSeconds;
            _ember.Position = at;
            _emberLeft = EmberSeconds;
            var sparks = new CpuParticles2D
            {
                Position = at, ZIndex = BoltNearZ, Amount = 30, Lifetime = 0.9f, OneShot = true, Explosiveness = 0.95f,
                Direction = Vector2.Up, Spread = 80f, Gravity = new Vector2(Wind, 260f),
                InitialVelocityMin = 30f, InitialVelocityMax = 110f, ScaleAmountMin = 1f, ScaleAmountMax = 1f,
                Color = new Color(3.6f, 2.6f, 1.4f),
            };
            AddChild(sparks);
            sparks.Emitting = true;
            sparks.Finished += sparks.QueueFree;
        }

        private void UpdateStrikeGlow(float dt)
        {
            _strikeGlowLeft = Mathf.Max(0f, _strikeGlowLeft - dt);
            _strikeGlow.Visible = _strikeGlowLeft > 0f;
            _strikeGlow.Modulate = new Color(0.7f / Night.R, 0.8f / Night.G, 1f / Night.B, _strikeGlowLeft / StrikeGlowSeconds * 0.8f);
            _emberLeft = Mathf.Max(0f, _emberLeft - dt);
            _ember.Visible = _emberLeft > 0f;
            _ember.Modulate = new Color(1f / Night.R, 0.42f / Night.G, 0.12f / Night.B, _emberLeft / EmberSeconds);
        }

        /// <summary>Erst aufrufen, wenn die Tiefe der Objekte feststeht.</summary>
        public void AddLampCones(IEnumerable<double[]> lamps, Vector2 stutterLamp)
        {
            var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            var glow = new Color(LampColor.R / Night.R, LampColor.G / Night.G, LampColor.B / Night.B);
            foreach (var lamp in lamps)
            {
                int ux = (int)(lamp[0] * Messingstadt.Core.World.TileUnits), uy = (int)(lamp[1] * Messingstadt.Core.World.TileUnits);
                var foot = Game.ToScreen(ux, uy);
                var points = new Vector2[ConePoints + 1];
                var colors = new Color[ConePoints + 1];
                points[0] = foot - new Vector2(0, ConeHeight);
                colors[0] = new Color(glow, ConeAlpha);
                for (int i = 0; i < ConePoints; i++)
                {
                    float a = Mathf.Pi * 0.08f + i / (ConePoints - 1f) * Mathf.Pi * 0.84f;
                    points[i + 1] = foot + new Vector2(Mathf.Cos(a) * ConeRadius, Mathf.Sin(a) * ConeRadius * 0.5f);
                    colors[i + 1] = new Color(glow, 0f);
                }
                var shape = new Polygon2D
                {
                    Polygon = points, VertexColors = colors, Material = additive,
                    ZIndex = Game.DepthZ((float)lamp[0], (float)lamp[1]) + 1,
                };
                AddChild(shape);
                bool stutters = Mathf.Abs((float)lamp[0] - stutterLamp.X) < 0.01f && Mathf.Abs((float)lamp[1] - stutterLamp.Y) < 0.01f;
                shape.Visible = !AirTest.Off("kegel");
                Sprite2D? halo = null;
                if (AirTest.Light)
                {
                    // Die feuchte Luft um die Lampe leuchtet mit. Ein weicher Hof, im Regen größer und heller.
                    halo = new Sprite2D
                    {
                        Texture = Glow, Material = additive, Position = foot - new Vector2(0, ConeHeight + 2f), Scale = Vector2.One * HaloScale,
                        ZIndex = shape.ZIndex, LightMask = 0, Modulate = new Color(glow, HaloAlpha),
                    };
                    AddChild(halo);
                }
                _cones.Add(new Cone { Shape = shape, Halo = halo, Stutters = stutters });
                var rain = new ConeRain
                {
                    Weather = this, Material = additive, Position = foot - new Vector2(0, ConeHeight), ZIndex = shape.ZIndex,
                    Glow = glow, Stutters = stutters, LightMask = 0,
                };
                AddChild(rain);
                _coneRain.Add(rain);
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            UpdateClimate(dt);
            UpdateStrikeGlow(dt);
            if (_time > _nextStorm)
            {
                StartStorm();
            }
            // Fernes Grollen ohne Blitz: Das Gewitter zieht irgendwo hinter den Fabriken vorbei.
            if (_time > _nextRumble)
            {
                _nextRumble = _time + Rand(MinRumblePause, MaxRumblePause);
                Game.Sound.PlayEverywhere($"thunder_{_rng.Next(3)}", Rand(RumbleQuietDb, RumbleLoudDb));
            }
            // Die Stadt hinter den Häusern: Hund, Sirene, Güterzug, jeweils leise aus der Ferne.
            if (_time > _nextCity)
            {
                _nextCity = _time + Rand(MinCityPause, MaxCityPause);
                var (sound, volume) = CitySounds[_rng.Next(CitySounds.Length)];
                Game.Sound.PlayEverywhere(sound, volume);
            }
            for (int i = _strokes.Count - 1; i >= 0; i--)
            {
                if (_time >= _strokes[i])
                {
                    _flash = Mathf.Max(_flash, Rand(0.7f, 1.25f));
                    _strokes.RemoveAt(i);
                    // Etwa jeder dritte Schlag bleibt im Wolkenband verborgen und erhellt nur die Stadt.
                    if (_rng.NextDouble() < VisibleBoltShare)
                    {
                        ShowBolt();
                    }
                }
            }
            _flash = HeldFlash >= 0f ? HeldFlash : Mathf.Max(0f, _flash - _flash * FlashFadePerSecond * dt - 0.2f * dt);
            Game.SetLightning(_flash);
            if (HeldFlash >= 0f && !_boltShown && _time > 0.2f)
            {
                PickStrike();
                ShowBolt();
            }
            float strength = _boltShown ? Mathf.Clamp(_flash * 1.3f - 0.2f, 0f, 1f) : 0f;
            _bolt.SetStrength(_liveBolt == _bolt ? strength : 0f);
            _farBolt.SetStrength(_liveBolt == _farBolt ? strength : 0f);
            if (_flash < 0.05f)
            {
                _boltShown = false;
            }
            _fog.SetShaderParameter("lightning", _flash);
            if (_thunderAt >= 0f && _time >= _thunderAt)
            {
                _thunderAt = -1f;
                Game.Sound.PlayEverywhere(_thunderSound, _thunderVolume);
                Game.ShakeCamera(Mathf.Clamp((_thunderVolume + 22f) / 16f, 0.15f, 0.6f));
            }
            foreach (var cone in _cones)
            {
                // Im Regen streut die nasse Luft das Licht, der Kegel steht dann deutlicher.
                float level = cone.Stutters ? Game.StutterLevel : Game.UnrestLevel;
                cone.Shape.Modulate = new Color(1f, 1f, 1f, level * (1f + ConeRainBoost * RainStrength));
                if (cone.Halo != null)
                {
                    cone.Halo.Modulate = new Color(cone.Halo.Modulate, HaloAlpha * level * (0.7f + 0.6f * RainStrength));
                    cone.Halo.Scale = Vector2.One * HaloScale * (1f + 0.25f * RainStrength);
                }
            }
        }

        private void StartStorm()
        {
            _nextStorm = _time + Rand(MinStormPause, MaxStormPause);
            PickStrike();
            // Zwei bis vier schnelle Entladungen, wie echte Blitze flackern.
            int strokes = _rng.Next(2, 5);
            float at = _time;
            for (int i = 0; i < strokes; i++)
            {
                _strokes.Add(at);
                at += Rand(0.05f, 0.22f);
            }
            float delay = Rand(MinThunderDelay, MaxThunderDelay);
            _thunderSound = $"thunder_{_rng.Next(3)}";
            // Je später der Donner, desto weiter weg und leiser. Ein Treffer im Bild kracht fast sofort.
            _thunderVolume = Mathf.Lerp(-9f, -20f, (delay - MinThunderDelay) / (MaxThunderDelay - MinThunderDelay));
            if (_strikePath is { Hit: true })
            {
                delay = NearThunderDelay;
                _thunderVolume = NearThunderVolume;
            }
            _thunderAt = _time + delay;
        }
    }
}
