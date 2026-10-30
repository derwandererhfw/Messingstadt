using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Bewegte Umgebung: Rauch, Dampf, flackerndes Licht, Regenringe und Papier im Wind.
    /// Reine Darstellung mit eigenem Zufall. Die Spiellogik bleibt davon unberührt und deterministisch.
    /// </summary>
    public partial class Ambience : Node2D
    {
        private const int HighZ = 3000;
        private const int PaperCount = 7;
        private const float RipplesPerSecond = 2.2f;
        private const float RippleSeconds = 0.8f;
        private const int FlashLights = 6;

        private sealed class Ripple
        {
            public Vector2 Pos;
            public float Radius;
            public float Age;
        }

        private sealed class Paper
        {
            public Vector2 Pos;
            public float Phase;
        }

        private readonly Random _rng = new();
        private readonly List<Ripple> _ripples = new();
        private readonly List<Paper> _papers = new();
        private readonly List<PointLight2D> _fireLights = new();
        private readonly List<PointLight2D> _flashLights = new();
        private float _time;
        private float _stutterUntil;
        private float _gustUntil;
        private float _nextGust = 2f;
        private float _rippleDebt;

        public List<Vector2> Smoke = new();
        public List<Vector2> Steam = new();
        public List<Vector2> Fires = new();
        public List<(Vector2 Pos, float Radius)> Puddles = new();
        public List<PointLight2D> Lamps = new();
        public Main Game = null!;
        public Texture2D Glow = null!;
        public float LampEnergy;
        public Rect2 Area;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public override void _Ready()
        {
            foreach (var p in Smoke)
            {
                // Farben über 1: Die Nachtabdunkelung multipliziert sie wieder auf normale Helligkeit herunter.
                AddPlume(p, amount: 30, life: 5.5f, speed: 11f, size: 1.5f, new Color(1.7f, 1.7f, 1.75f, 0.3f));
            }
            foreach (var p in Steam)
            {
                AddPlume(p, amount: 18, life: 2.8f, speed: 11f, size: 1f, new Color(2.3f, 2.3f, 2.4f, 0.28f));
            }
            foreach (var p in Fires)
            {
                var light = new PointLight2D
                {
                    Texture = Glow, TextureScale = 0.3f, Color = new Color(1f, 0.45f, 0.15f), Energy = 1f,
                    RangeZMin = -1, RangeZMax = 4096, Position = p,
                };
                AddChild(light);
                _fireLights.Add(light);
            }
            for (int i = 0; i < FlashLights; i++)
            {
                var flash = new PointLight2D
                {
                    Texture = Glow, TextureScale = 0.4f, Color = new Color(1f, 0.8f, 0.5f), Energy = 1.5f,
                    RangeZMin = -1, RangeZMax = 4096, Visible = false,
                };
                AddChild(flash);
                _flashLights.Add(flash);
            }
            for (int i = 0; i < PaperCount; i++)
            {
                _papers.Add(new Paper
                {
                    Pos = new Vector2(Rand(Area.Position.X, Area.End.X), Rand(Area.Position.Y, Area.End.Y)),
                    Phase = Rand(0f, Mathf.Tau),
                });
            }
        }

        private void AddPlume(Vector2 pos, int amount, float life, float speed, float size, Color color)
        {
            var ramp = new Gradient
            {
                Offsets = new[] { 0f, 0.25f, 1f },
                Colors = new[] { new Color(color, 0f), color, new Color(color, 0f) },
            };
            var growth = new Curve();
            growth.AddPoint(new Vector2(0f, 0.35f));
            growth.AddPoint(new Vector2(1f, 1f));
            // Kleines rundes Wölkchen ohne Glättung, damit der Rauch zur Pixelgrafik passt.
            var puff = new GradientTexture2D
            {
                Gradient = new Gradient { Offsets = new[] { 0f, 0.6f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f) } },
                Width = 16, Height = 16, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
            AddChild(new CpuParticles2D
            {
                Texture = puff,
                TextureFilter = TextureFilterEnum.Nearest,
                Position = pos,
                ZIndex = HighZ,
                Amount = amount,
                Lifetime = life,
                Preprocess = life,
                Direction = new Vector2(0.25f, -1f),
                Spread = 14f,
                Gravity = new Vector2(3f, -2f),
                InitialVelocityMin = speed * 0.7f,
                InitialVelocityMax = speed * 1.3f,
                ScaleAmountMin = size,
                ScaleAmountMax = size * 1.8f,
                ScaleAmountCurve = growth,
                ColorRamp = ramp,
            });
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            FlickerLamps(dt);
            for (int i = 0; i < _fireLights.Count; i++)
            {
                _fireLights[i].Energy = 0.8f + 0.35f * Mathf.Sin(_time * 9f + i * 2f) * Mathf.Sin(_time * 14f + i);
            }
            UpdateFlashes();
            UpdateRipples(dt);
            UpdatePapers(dt);
            QueueRedraw();
        }

        /// <summary>Jeder Schuss erhellt für einen Augenblick die Umgebung des Schützen.</summary>
        private void UpdateFlashes()
        {
            var world = Game.World;
            int used = 0;
            for (int i = world.Shots.Count - 1; i >= 0 && used < _flashLights.Count; i--)
            {
                var shot = world.Shots[i];
                if (world.Tick - shot.Tick > 1)
                {
                    break;
                }
                _flashLights[used].Position = Game.ToScreen(shot.FromX, shot.FromY) + new Vector2(0, -20);
                _flashLights[used].Visible = true;
                used++;
            }
            for (int i = used; i < _flashLights.Count; i++)
            {
                _flashLights[i].Visible = false;
            }
        }

        private void FlickerLamps(float dt)
        {
            // Eine Laterne hat einen Wackelkontakt, die übrigen brennen nur leicht unruhig.
            if (_time > _stutterUntil && _rng.NextDouble() < 0.3 * dt)
            {
                _stutterUntil = _time + Rand(0.25f, 0.7f);
            }
            for (int i = 0; i < Lamps.Count; i++)
            {
                float e = 1f + 0.05f * Mathf.Sin(_time * (3f + i * 0.7f) + i * 1.7f) + 0.025f * Mathf.Sin(_time * 11f + i);
                if (i == 4 && _time < _stutterUntil)
                {
                    e = _rng.NextDouble() < 0.5 ? 0.12f : 1f;
                }
                Lamps[i].Energy = LampEnergy * e;
            }
        }

        private void UpdateRipples(float dt)
        {
            _rippleDebt += RipplesPerSecond * Puddles.Count * dt / 4f;
            while (_rippleDebt >= 1f && Puddles.Count > 0)
            {
                _rippleDebt -= 1f;
                var (pos, radius) = Puddles[_rng.Next(Puddles.Count)];
                _ripples.Add(new Ripple
                {
                    Pos = pos + new Vector2(Rand(-radius * 0.6f, radius * 0.6f), Rand(-radius * 0.25f, radius * 0.25f)),
                    Radius = Mathf.Min(radius * 0.35f, 7f),
                });
            }
            foreach (var r in _ripples)
            {
                r.Age += dt;
            }
            _ripples.RemoveAll(r => r.Age >= RippleSeconds);
        }

        private void UpdatePapers(float dt)
        {
            if (_time > _nextGust)
            {
                _gustUntil = _time + Rand(1.5f, 3.5f);
                _nextGust = _gustUntil + Rand(3f, 8f);
            }
            float wind = _time < _gustUntil ? 34f : 2f;
            foreach (var p in _papers)
            {
                p.Pos += new Vector2(wind, wind * 0.32f + Mathf.Sin(_time * 5f + p.Phase) * wind * 0.5f) * dt;
                if (!Area.HasPoint(p.Pos))
                {
                    p.Pos = new Vector2(Area.Position.X, Rand(Area.Position.Y, Area.End.Y));
                }
            }
        }

        public override void _Draw()
        {
            var pts = new Vector2[13];
            foreach (var r in _ripples)
            {
                float f = r.Age / RippleSeconds;
                float radius = 1f + r.Radius * f;
                for (int i = 0; i <= 12; i++)
                {
                    float a = i / 12f * Mathf.Tau;
                    pts[i] = r.Pos + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.5f);
                }
                DrawPolyline(pts, new Color(1.9f, 2.1f, 2.5f, 0.75f * (1f - f)), 1f);
            }
            foreach (var p in _papers)
            {
                bool flat = Mathf.Sin(_time * 7f + p.Phase) > 0f;
                DrawRect(new Rect2(p.Pos, flat ? new Vector2(3, 1) : new Vector2(2, 2)), new Color(1.7f, 1.6f, 1.4f));
            }
        }
    }
}
