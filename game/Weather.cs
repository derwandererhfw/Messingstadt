using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Wetter und Luft: Gewitter mit vorberechnetem Blitzlicht, Bodennebel, der vom Licht der Stadt lebt,
    /// und Lichtkegel unter den Laternen, in denen der Regen aufleuchtet. Reine Darstellung.
    /// </summary>
    public partial class Weather : Node2D
    {
        private const float VisibleBoltShare = 0.65f;
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
        };
        private const int ConePoints = 14;
        private const float ConeHeight = 52f;
        private const float ConeRadius = 46f;
        private const float ConeAlpha = 0.26f;

        // Nebel: Der Shader liest die Lichtkarte des Bodens, die Schwaden leuchten also genau dort, wo Licht fällt.
        private const string FogShaderCode = @"
shader_type canvas_item;
uniform sampler2D noise_map : filter_linear, repeat_enable;
uniform sampler2D ground_light : filter_nearest;
uniform vec3 night;
uniform vec2 origin;
uniform vec2 size;
uniform float lightning = 0.0;
void fragment() {
    vec2 px = UV * size;
    float a = (px.x - origin.x) / 32.0, b = (px.y - origin.y) / 16.0;
    float mx = (a + b) / 2.0, my = (b - a) / 2.0;
    float edge = smoothstep(0.0, 1.5, mx) * smoothstep(0.0, 1.5, my) * smoothstep(20.0, 18.5, mx) * smoothstep(20.0, 18.5, my);
    vec2 iso = vec2(px.x, px.y * 2.0) / 512.0;
    float n = texture(noise_map, iso + vec2(TIME * 0.012, TIME * 0.004)).r * 0.6
        + texture(noise_map, iso * 1.9 - vec2(TIME * 0.018, -TIME * 0.009)).r * 0.4;
    float amount = smoothstep(0.46, 0.78, n) * 0.5 * edge;
    vec3 l = texture(ground_light, UV).rgb;
    l = 4.0 * l * l + lightning * vec3(0.5, 0.58, 0.72);
    COLOR = vec4((l * vec3(0.82, 0.86, 0.95) + vec3(0.03, 0.035, 0.05)) / night, amount);
}";

        private sealed class Cone
        {
            public Polygon2D Shape = null!;
            public bool Stutters;
        }

        private readonly Random _rng = new();
        private readonly List<Cone> _cones = new();
        private ShaderMaterial _fog = null!;
        private LightningBolt _bolt = null!;
        private bool _boltShown;
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
        public Vector2 GroundSize;
        public Vector2 GroundOrigin;
        public Color Night;
        public Color LampColor;

        /// <summary>Prüflauf: hält den Blitz auf dieser Stärke, negativ heißt aus.</summary>
        public float HeldFlash = -1f;

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
            AddChild(new Sprite2D { Texture = GroundLight, Centered = false, ZIndex = 0, Material = _fog });
            var boltLayer = new CanvasLayer { Layer = 1 };
            AddChild(boltLayer);
            _bolt = new LightningBolt { Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
            boltLayer.AddChild(_bolt);
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
                _cones.Add(new Cone { Shape = shape, Stutters = stutters });
                AddChild(new CpuParticles2D
                {
                    Material = additive, Position = foot - new Vector2(0, ConeHeight - 6), ZIndex = shape.ZIndex,
                    Amount = 12, Lifetime = 0.45f, Preprocess = 0.5f, EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                    EmissionRectExtents = new Vector2(ConeRadius * 0.55f, 4f), Direction = new Vector2(0.15f, 1f), Spread = 3f,
                    Gravity = Vector2.Zero, InitialVelocityMin = 90f, InitialVelocityMax = 120f,
                    ScaleAmountMin = 1f, ScaleAmountMax = 1f, Color = new Color(glow, 0.55f),
                });
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
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
                        _bolt.Strike();
                        _boltShown = true;
                    }
                }
            }
            _flash = HeldFlash >= 0f ? HeldFlash : Mathf.Max(0f, _flash - _flash * FlashFadePerSecond * dt - 0.2f * dt);
            Game.SetLightning(_flash);
            if (HeldFlash >= 0f && !_boltShown)
            {
                _bolt.Strike();
                _boltShown = true;
            }
            _bolt.SetStrength(_boltShown ? Mathf.Clamp(_flash * 1.3f - 0.2f, 0f, 1f) : 0f);
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
                cone.Shape.Modulate = new Color(1f, 1f, 1f, cone.Stutters ? Game.StutterLevel : Game.UnrestLevel);
            }
        }

        private void StartStorm()
        {
            _nextStorm = _time + Rand(MinStormPause, MaxStormPause);
            // Zwei bis vier schnelle Entladungen, wie echte Blitze flackern.
            int strokes = _rng.Next(2, 5);
            float at = _time;
            for (int i = 0; i < strokes; i++)
            {
                _strokes.Add(at);
                at += Rand(0.05f, 0.22f);
            }
            float delay = Rand(MinThunderDelay, MaxThunderDelay);
            _thunderAt = _time + delay;
            _thunderSound = $"thunder_{_rng.Next(3)}";
            // Je später der Donner, desto weiter weg und leiser.
            _thunderVolume = Mathf.Lerp(-9f, -20f, (delay - MinThunderDelay) / (MaxThunderDelay - MinThunderDelay));
        }
    }
}
