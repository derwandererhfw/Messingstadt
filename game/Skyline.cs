using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Durchsichtige Ebene über den Häusern hinter der Karte: ziehender Smog, glimmende Hochöfen, Laternen (tools/art/skyline.py)
    /// und eine Hochbahn, die ab und zu vorbeifährt. Spielt für die Regeln keine Rolle.
    /// </summary>
    public partial class Skyline : Node2D
    {
        private const float TrainTilesPerSecond = 3.4f;
        private const float FirstTrainSeconds = 2f;
        private const float MinTrainPause = 25f;
        private const float MaxTrainPause = 70f;
        private const int Carriages = 5;
        private const float CarriageTiles = 2.6f;
        private const float CarriageGap = 0.15f;
        private const float CarriageHeight = 15f;
        private const int WindowsPerCarriage = 5;
        private const float RailHeight = 2f;
        private static readonly Color CarriageColor = new(0.05f, 0.05f, 0.07f);
        private static readonly Color CarriageWindow = new(0.72f, 0.54f, 0.3f);
        private static readonly Color TailLamp = new(0.8f, 0.14f, 0.1f);
        private static readonly Color Spark = new(0.75f, 0.85f, 1f);

        // Smog zieht langsam über die Stadt, je weiter weg, desto dichter. Die Hochöfen färben ihn von unten.
        private const string ShaderCode = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform sampler2D noise_map : filter_linear, repeat_enable;
uniform sampler2D fx_map : filter_linear;
uniform vec3 night;
uniform vec3 haze;
uniform vec2 origin;
uniform vec2 size;
uniform float lightning = 0.0;
void fragment() {
    vec2 px = UV * size;
    float a = (px.x - origin.x) / 32.0, b = (px.y - origin.y) / 16.0;
    float mx = (a + b) / 2.0, my = (b - a) / 2.0;
    if (mx > 0.0 && my > 0.0 && mx < 20.0 && my < 20.0) {
        discard;
    }
    vec4 t = texture(TEXTURE, UV);
    vec3 fx = texture(fx_map, UV).rgb;
    float n = texture(noise_map, vec2(px.x, px.y * 2.0) / 640.0 + vec2(TIME * 0.008, TIME * 0.003)).r;
    float smog = smoothstep(0.34, 0.76, n);
    float cover = smog * (0.06 + 0.6 * fx.g);
    // Je weiter draußen vor der Karte, desto dichter steht der Dunst: Die Stadt verliert sich, statt schwarz abzubrechen.
    float outside = max(max(-mx, -my), max(mx - 20.0, my - 20.0));
    float far = smoothstep(0.5, 9.0, outside) * (0.3 + 0.22 * n);
    vec3 c = (haze + lightning * vec3(0.3, 0.35, 0.48)) * cover;
    c += (vec3(0.2, 0.24, 0.38) + lightning * vec3(0.3, 0.35, 0.48)) * far * (1.0 - cover);
    cover = cover + far * (1.0 - cover);
    float breath = 0.74 + 0.2 * sin(TIME * 0.4 + fx.b * 12.0) + 0.06 * sin(TIME * 2.3 + fx.b * 20.0);
    c += vec3(0.5, 0.17, 0.04) * fx.r * breath * (0.3 + 0.7 * smog);
    // Pfosten und Bahn decken ab (Alpha), die Lichtinseln addieren sich nur (Farbe ohne Alpha).
    c = c * (1.0 - t.a) + t.rgb;
    COLOR = vec4(c / night, cover * (1.0 - t.a) + t.a);
}";

        private readonly Random _rng = new();
        private ShaderMaterial _material = null!;
        private float _viaductX;
        private float _viaductZ;
        private float _trainFrom;
        private float _trainTo;
        private float _trainHead;
        private float _wait = FirstTrainSeconds;
        private bool _running;
        private float _sparkLeft;
        private float _nextSpark;
        private float _lastLightning = -1f;

        public Main Game = null!;
        public Texture2D Backdrop = null!;
        public Texture2D Effects = null!;
        public Texture2D Noise = null!;
        public string DataPath = "";

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public override void _Ready()
        {
            using var json = JsonDocument.Parse(File.ReadAllText(DataPath));
            var root = json.RootElement;
            var margin = new Vector2(root.GetProperty("marginX").GetInt32(), root.GetProperty("marginTop").GetInt32());
            _viaductX = root.GetProperty("viaductX").GetSingle();
            _viaductZ = root.GetProperty("viaductZ").GetSingle();
            _trainFrom = root.GetProperty("trainFrom").GetSingle();
            _trainTo = root.GetProperty("trainTo").GetSingle();
            var haze = root.GetProperty("haze");
            _material = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
            _material.SetShaderParameter("noise_map", Noise);
            _material.SetShaderParameter("night", Game.NightColor);
            _material.SetShaderParameter("haze", new Color(haze[0].GetInt32() / 255f, haze[1].GetInt32() / 255f, haze[2].GetInt32() / 255f));
            _material.SetShaderParameter("origin", Game.ToScreen(0, 0) + margin);
            _material.SetShaderParameter("size", Backdrop.GetSize());
            _material.SetShaderParameter("fx_map", Effects);
            LightMask = 0;
            AddChild(new Sprite2D
            {
                Texture = Backdrop, Centered = false, Position = -margin, Material = _material, LightMask = 0,
                ShowBehindParent = true, TextureFilter = TextureFilterEnum.Nearest,
            });
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            if (Game.Lightning != _lastLightning)
            {
                _lastLightning = Game.Lightning;
                _material.SetShaderParameter("lightning", _lastLightning);
            }
            if (!_running)
            {
                _wait -= dt;
                if (_wait <= 0f)
                {
                    _running = true;
                    _trainHead = _trainFrom;
                }
                return;
            }
            _trainHead += TrainTilesPerSecond * dt;
            float tail = _trainHead - Carriages * (CarriageTiles + CarriageGap);
            if (tail > _trainTo)
            {
                _running = false;
                _wait = Rand(MinTrainPause, MaxTrainPause);
            }
            // Am Fahrdraht springt ab und zu ein Funke über.
            _sparkLeft -= dt;
            _nextSpark -= dt;
            if (_nextSpark <= 0f)
            {
                _nextSpark = Rand(0.8f, 4f);
                _sparkLeft = Rand(0.04f, 0.12f);
            }
            QueueRedraw();
        }

        private Vector2 Rail(float y, float z) => Game.ToScreen((int)(_viaductX * Messingstadt.Core.World.TileUnits), (int)(y * Messingstadt.Core.World.TileUnits)) - new Vector2(0, _viaductZ + RailHeight + z);

        private Color Lit(Color c, float a = 1f)
        {
            var night = Game.NightColor;
            return new Color(c.R / night.R, c.G / night.G, c.B / night.B, a);
        }

        public override void _Draw()
        {
            if (!_running)
            {
                return;
            }
            float flash = Mathf.Min(1f, Game.Lightning);
            var body = Lit(CarriageColor.Lerp(new Color(0.3f, 0.34f, 0.45f), flash * 0.6f));
            var pane = Lit(CarriageWindow);
            float step = CarriageTiles + CarriageGap;
            for (int i = 0; i < Carriages; i++)
            {
                float y0 = _trainHead - (i + 1) * step;
                float y1 = y0 + CarriageTiles;
                // An beiden Enden fährt der Zug in ein Haus, dort wird er abgeschnitten.
                float from = Mathf.Max(y0, _trainFrom), to = Mathf.Min(y1, _trainTo);
                if (to <= from)
                {
                    continue;
                }
                DrawColoredPolygon(new[] { Rail(from, 0f), Rail(to, 0f), Rail(to, CarriageHeight), Rail(from, CarriageHeight) }, body);
                for (int w = 0; w < WindowsPerCarriage; w++)
                {
                    // Immer dieselben Wagenfenster bleiben dunkel, das Muster soll beim Fahren nicht flimmern.
                    if ((i * 3 + w * 7) % 5 == 0)
                    {
                        continue;
                    }
                    float u0 = y0 + 0.15f + w * (CarriageTiles - 0.3f) / WindowsPerCarriage;
                    float u1 = u0 + 0.28f;
                    if (u0 < _trainFrom || u1 > _trainTo)
                    {
                        continue;
                    }
                    DrawColoredPolygon(new[] { Rail(u0, 5f), Rail(u1, 5f), Rail(u1, 11f), Rail(u0, 11f) }, pane);
                }
            }
            float end = _trainHead - Carriages * step;
            if (end > _trainFrom && end < _trainTo)
            {
                DrawRect(new Rect2(Rail(end, 4f) - new Vector2(1f, 1f), new Vector2(2f, 2f)), Lit(TailLamp));
            }
            float pole = _trainHead - step * 0.5f;
            if (_sparkLeft > 0f && pole > _trainFrom && pole < _trainTo)
            {
                var at = Rail(pole, CarriageHeight + 4f);
                DrawCircle(at, 7f, Lit(Spark, 0.16f));
                DrawCircle(at, 1.5f, Lit(Spark));
            }
        }
    }
}
