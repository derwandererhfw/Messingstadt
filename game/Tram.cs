using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Die stehende Straßenbahn lebt noch: Das Innenlicht hat einen Wackelkontakt, und der Schaffner geht als Schatten
    /// hinter den Scheiben auf und ab. Bei Schüssen geht das Licht aus, und er duckt sich weg. Reine Darstellung.
    /// </summary>
    public partial class Tram : Node2D
    {
        // So sind die Fenster in tram() in tools/art/generate_concept.py gezeichnet: Bildpunkte auf der langen Wand.
        private const float TramHeight = 40f;
        private const float MinLength = 3f;
        private const float MaxWidth = 1f;
        private const float FirstWindowU = 5f;
        private const float WindowStepU = 15f;
        private const float WindowWidthU = 10f;
        private const float WindowTopV = 8f;
        private const float WindowBottomV = 22f;
        private const float WallU = 32f;

        private const float GlowAlpha = 0.2f;
        private const float StutterDarkAlpha = 0.45f;
        private const float LightFadePerSecond = 1.5f;
        private const float HushAtWhichLightGoesOut = 0.25f;
        private const float MinStutterPause = 5f;
        private const float MaxStutterPause = 18f;
        private const float WalkSpeedU = 7f;
        private const float MinWalkSeconds = 3f;
        private const float MaxWalkSeconds = 9f;
        private const float MinRestSeconds = 2f;
        private const float MaxRestSeconds = 8f;
        private const float BodyHalfU = 3.5f;
        private const float HeadHalfU = 2f;
        private const float ShoulderV = 14f;
        private const float HeadTopV = 10f;
        private const float BobV = 0.6f;
        private const float BobPerSecond = 5f;
        private const float BehindViewFadePerSecond = 3f;
        private static readonly Color Warm = new(1f, 0.74f, 0.4f);
        private static readonly Color Shadow = new(0.03f, 0.025f, 0.03f);

        private readonly Random _rng = new();
        private readonly List<float> _windows = new();
        private bool _exists;
        private float _wallX, _y0, _y1;
        private float _light = 1f;
        private float _stutterUntil;
        private float _nextStutter = 4f;
        private float _conductorU;
        private float _direction = 1f;
        private float _walkLeft;
        private float _restLeft = 1f;
        private float _visible = 1f;
        private float _time;
        private Rect2 _area;
        private AudioStreamPlayer2D? _hum;

        public Main Game = null!;
        public string ManifestPath = "";

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public override void _Ready()
        {
            LightMask = 0;
            using var manifest = JsonDocument.Parse(File.ReadAllText(ManifestPath));
            foreach (var t in manifest.RootElement.GetProperty("things").EnumerateArray())
            {
                float x0 = t.GetProperty("x0").GetSingle(), y0 = t.GetProperty("y0").GetSingle();
                float x1 = t.GetProperty("x1").GetSingle(), y1 = t.GetProperty("y1").GetSingle();
                if (MapSite.Current.Tram && Mathf.IsEqualApprox(t.GetProperty("z1").GetSingle(), TramHeight) && x1 - x0 < MaxWidth && y1 - y0 > MinLength)
                {
                    _exists = true;
                    _wallX = x1;
                    _y0 = y0;
                    _y1 = y1;
                    // Direkt über der Bahn, aber unter allem, was davor steht.
                    ZIndex = Game.PerchZ((x0 + x1) / 2f, (y0 + y1) / 2f, TramHeight) - 19;
                    _area = new Rect2(t.GetProperty("px").GetSingle(), t.GetProperty("py").GetSingle(), (x1 - x0 + y1 - y0) * WallU, TramHeight + (x1 - x0 + y1 - y0) * 16f);
                    break;
                }
            }
            if (!_exists)
            {
                SetProcess(false);
                return;
            }
            float length = (_y1 - _y0) * WallU;
            _hum = Game.Sound.AddTramHum(_area.GetCenter());
            for (float u = FirstWindowU; u < length - 12f; u += WindowStepU)
            {
                _windows.Add(u);
            }
            _conductorU = length * 0.6f;
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            float length = (_y1 - _y0) * WallU;
            bool hushed = Game.Windows.Hush > HushAtWhichLightGoesOut;
            if (_time > _nextStutter)
            {
                _stutterUntil = _time + Rand(0.2f, 0.9f);
                _nextStutter = _stutterUntil + Rand(MinStutterPause, MaxStutterPause);
            }
            float wanted = hushed ? 0f : _time < _stutterUntil && _rng.NextDouble() < 0.6 ? 0.1f : 1f;
            // Beim Stottern springt das Licht, beim Ausschalten und Wiederkommen geht es weich.
            _light = _time < _stutterUntil && !hushed ? wanted : Mathf.MoveToward(_light, wanted, LightFadePerSecond * dt);
            if (_hum != null)
            {
                _hum.VolumeDb = SoundScape.TramHumAt(_light);
            }
            if (_restLeft > 0f)
            {
                _restLeft -= dt;
                if (_restLeft <= 0f)
                {
                    _walkLeft = Rand(MinWalkSeconds, MaxWalkSeconds);
                    _direction = _rng.NextDouble() < 0.5 ? -1f : 1f;
                }
            }
            else
            {
                _conductorU += _direction * WalkSpeedU * dt;
                if (_conductorU < FirstWindowU || _conductorU > length - 12f)
                {
                    _conductorU = Mathf.Clamp(_conductorU, FirstWindowU, length - 12f);
                    _direction = -_direction;
                }
                _walkLeft -= dt;
                if (_walkLeft <= 0f)
                {
                    _restLeft = Rand(MinRestSeconds, MaxRestSeconds);
                }
            }
            // Steht ein Agent hinter der Bahn, ist sie im Bild aufgeschnitten. Dann darf hier nichts in der Luft hängen.
            _visible = Mathf.MoveToward(_visible, AgentBehind() ? 0f : 1f, BehindViewFadePerSecond * dt);
            QueueRedraw();
        }

        private bool AgentBehind()
        {
            foreach (var u in Game.World.Units)
            {
                if (u.Faction != Faction.Agent || !u.Alive || u.X / (float)World.TileUnits >= _wallX)
                {
                    continue;
                }
                var body = new Rect2(Game.ToScreen(u.X, u.Y) + new Vector2(-12f, -46f), new Vector2(24f, 48f));
                if (body.Intersects(_area))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Helligkeit des Innenlichts von 0 bis 1, für Spiegelbilder.</summary>
        public float Glow => _exists ? _light : 0f;

        /// <summary>Mitte jeder Scheibe und der Fuß der Wand darunter, im Bild.</summary>
        public IEnumerable<(Vector2 Light, float FootY)> PaneLights()
        {
            if (!_exists)
            {
                yield break;
            }
            foreach (float u in _windows)
            {
                var middle = u + WindowWidthU / 2f;
                yield return (Wall(middle, (WindowTopV + WindowBottomV) / 2f), Wall(middle, TramHeight).Y);
            }
        }

        // Punkt auf der langen Wand: u läuft vom vorderen Ende nach hinten, v von der Oberkante nach unten.
        private Vector2 Wall(float u, float v) => Game.ToScreen((int)(_wallX * World.TileUnits), (int)((_y1 - u / WallU) * World.TileUnits)) - new Vector2(0f, TramHeight - v);

        private Color Lit(Color c, float a)
        {
            var night = Game.NightColor;
            return new Color(c.R / night.R, c.G / night.G, c.B / night.B, a * _visible);
        }

        private void Pane(float u0, float u1, float v0, float v1, Color color)
        {
            DrawColoredPolygon(new[] { Wall(u0, v0), Wall(u1, v0), Wall(u1, v1), Wall(u0, v1) }, color);
        }

        public override void _Draw()
        {
            if (!_exists || _visible <= 0f)
            {
                return;
            }
            var glow = Lit(Warm, GlowAlpha * _light);
            var dark = Lit(Shadow, StutterDarkAlpha * (1f - _light));
            foreach (float u in _windows)
            {
                Pane(u, u + WindowWidthU, WindowTopV, WindowBottomV, dark);
                Pane(u, u + WindowWidthU, WindowTopV, WindowBottomV, glow);
            }
            // Im Dunkeln sieht man den Schaffner nicht mehr.
            if (_light < 0.3f)
            {
                return;
            }
            bool walking = _restLeft <= 0f;
            float bob = walking ? Mathf.Sin(_time * BobPerSecond) * BobV : 0f;
            var figure = Lit(Shadow, 0.8f * _light);
            foreach (float u in _windows)
            {
                // Nur in den Scheiben sichtbar, die Wand dazwischen verdeckt ihn.
                Clipped(u, _conductorU - BodyHalfU, _conductorU + BodyHalfU, ShoulderV + bob, WindowBottomV, figure);
                Clipped(u, _conductorU - HeadHalfU, _conductorU + HeadHalfU, HeadTopV + bob, ShoulderV + bob, figure);
            }
        }

        private void Clipped(float window, float from, float to, float top, float bottom, Color color)
        {
            float u0 = Mathf.Max(from, window), u1 = Mathf.Min(to, window + WindowWidthU);
            float v0 = Mathf.Max(top, WindowTopV), v1 = Mathf.Min(bottom, WindowBottomV);
            if (u1 > u0 && v1 > v0)
            {
                Pane(u0, u1, v0, v1, color);
            }
        }
    }
}
