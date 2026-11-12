using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Bewegte Umgebung: Rauch und Dampf im Wind, flackerndes Licht, Regenringe und Papier.
    /// Reine Darstellung mit eigenem Zufall. Die Spiellogik bleibt davon unberührt und deterministisch.
    /// </summary>
    public partial class Ambience : Node2D
    {
        private const int HighZ = 3000;
        private const int PaperCount = 7;
        private const float RipplesPerSecond = 2.2f;
        private const float RippleSeconds = 0.8f;
        private const int FlashLights = 6;
        // Mündungsfeuer als Licht auf Boden, Wänden und Figuren.
        private const int FlashTicks = 3;
        private const float FlashEnergy = 0.95f;
        private const float FlashScale = 0.75f;
        private const float ShotgunEnergy = 1.6f;
        private const float ShotgunScale = 1.35f;
        private const float MinValveSeconds = 16f;
        private const float MaxValveSeconds = 45f;
        private const float PlumeWindShare = 0.55f;
        private const float PlumeDriftX = 3f;
        private const float PlumeLift = -2f;

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
        private readonly List<PointLight2D> _flashLights = new();
        private float _nextGasDip = 2f;
        private float _gasDipFrom = -10f;
        private float _gasDipSeconds = 0.2f;
        private float _gasDipDepth;
        private readonly List<CpuParticles2D> _plumes = new();
        private float _time;
        private float _stutterUntil;
        private float _rippleDebt;
        private float _nextValve = 9f;

        public List<Vector2> Smoke = new();
        public List<Vector2> Steam = new();
        public List<Vector2> Fires = new();
        public List<(Vector2 Pos, float Radius)> Puddles = new();

        /// <summary>Überdruckventile auf den Kesseln.</summary>
        public List<Vector2> Valves = new();
        public Main Game = null!;
        public Texture2D Glow = null!;
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
            for (int i = 0; i < FlashLights; i++)
            {
                var flash = new PointLight2D
                {
                    Texture = Glow, TextureScale = 0.4f, Color = new Color(1f, 0.72f, 0.4f), Energy = 0.3f,
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
            var plume = new CpuParticles2D
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
            };
            AddChild(plume);
            _plumes.Add(plume);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            var plumeDrift = new Vector2(PlumeDriftX + Game.Climate.Wind * PlumeWindShare, PlumeLift);
            foreach (var plume in _plumes)
            {
                plume.Gravity = plumeDrift;
            }
            FlickerLights(dt);
            UpdateValves();
            UpdateFlashes();
            UpdateRipples(dt);
            UpdatePapers(dt);
            QueueRedraw();
        }

        /// <summary>Ab und zu bläst ein Kessel zischend Dampf ab.</summary>
        private void UpdateValves()
        {
            if (_time < _nextValve || Valves.Count == 0)
            {
                return;
            }
            _nextValve = _time + Rand(MinValveSeconds, MaxValveSeconds);
            var at = Valves[_rng.Next(Valves.Count)];
            var burst = new CpuParticles2D
            {
                Position = at, ZIndex = HighZ, Amount = 40, Lifetime = 1.6f, OneShot = true, Explosiveness = 0.45f,
                Direction = new Vector2(0.3f, -1f), Spread = 16f, Gravity = new Vector2(8f, 12f),
                // Die Größe muss hier stehen: Die Skalierung der Node wirkt ohne lokale Koordinaten nicht auf die Partikel.
                InitialVelocityMin = 45f, InitialVelocityMax = 85f, ScaleAmountMin = 0.16f, ScaleAmountMax = 0.4f,
                Texture = Glow, TextureFilter = TextureFilterEnum.Linear,
                Color = new Color(2.4f, 2.4f, 2.5f, 0.09f),
            };
            AddChild(burst);
            burst.Emitting = true;
            burst.Finished += burst.QueueFree;
            Game.PlayScenery("steam_burst", at, -11f, 900f, 0.08f);
        }

        /// <summary>Jeder Schuss erhellt für einen Augenblick die Umgebung des Schützen.</summary>
        private void UpdateFlashes()
        {
            var world = Game.World;
            int used = 0;
            for (int i = world.Shots.Count - 1; i >= 0 && used < _flashLights.Count; i--)
            {
                var shot = world.Shots[i];
                int age = world.Tick - shot.Tick;
                bool bright = AirTest.Light && !AirTest.Off("muendung");
                if (age > (bright ? FlashTicks : 1))
                {
                    break;
                }
                _flashLights[used].Position = Game.ToScreen(shot.FromX, shot.FromY) + new Vector2(0, -20);
                _flashLights[used].Visible = true;
                if (bright)
                {
                    // Der Schuss reißt die Umgebung für einen Augenblick warm aus dem Dunkel und glimmt kurz nach.
                    // Die Schrotflinte blitzt breiter und heller als Pistole und Maschinenpistole.
                    bool shotgun = shot.Weapon == "schrotflinte";
                    float fade = 1f - age / (FlashTicks + 1f);
                    _flashLights[used].Energy = FlashEnergy * (shotgun ? ShotgunEnergy : 1f) * fade * fade;
                    _flashLights[used].TextureScale = FlashScale * (shotgun ? ShotgunScale : 1f);
                }
                used++;
            }
            for (int i = used; i < _flashLights.Count; i++)
            {
                _flashLights[i].Visible = false;
            }
        }

        private void FlickerLights(float dt)
        {
            // Eine Laterne hat einen Wackelkontakt, die übrigen brennen nur leicht unruhig.
            if (_time > _stutterUntil && _rng.NextDouble() < 0.3 * dt)
            {
                _stutterUntil = _time + Rand(0.25f, 0.7f);
            }
            float unrest = 1f + 0.05f * Mathf.Sin(_time * 3f) + 0.025f * Mathf.Sin(_time * 11f);
            float gas = AirTest.Off("gas") ? 0f : MapSite.Current.LampUnrest;
            if (gas > 0f)
            {
                // Gaslicht brennt unruhig: Die Flamme wogt, zuckt im Zug und sackt ab und zu kurz zusammen.
                if (_time > _nextGasDip)
                {
                    _nextGasDip = _time + Rand(1.5f, 6f);
                    _gasDipFrom = _time;
                    _gasDipSeconds = Rand(0.12f, 0.35f);
                    _gasDipDepth = Rand(0.12f, 0.3f);
                }
                float dip = _time < _gasDipFrom + _gasDipSeconds ? Mathf.Sin((_time - _gasDipFrom) / _gasDipSeconds * Mathf.Pi) * _gasDipDepth : 0f;
                unrest = 1f + gas * (0.07f * Mathf.Sin(_time * 4.1f) + 0.05f * Mathf.Sin(_time * 9.7f + 1.3f) + 0.035f * Mathf.Sin(_time * 23f) - dip);
            }
            float stutter = _time < _stutterUntil && _rng.NextDouble() < 0.5 ? 0.12f : unrest;
            float fire = 1f + 0.35f * Mathf.Sin(_time * 9f) * Mathf.Sin(_time * 14f);
            Game.SetFlicker(stutter, unrest, fire);
        }

        private void UpdateRipples(float dt)
        {
            // Bei Niesel wie gehabt, im Schauer kochen die Pfützen, in der Pause fällt kaum ein Tropfen.
            _rippleDebt += RipplesPerSecond * Puddles.Count * dt / 4f * Game.Climate.RainStrength * 3.3f;
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
            float wind = Game.Climate.Wind;
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
            var pts = new Vector2[25];
            foreach (var r in _ripples)
            {
                float f = r.Age / RippleSeconds;
                float radius = 1f + r.Radius * f;
                for (int i = 0; i <= 24; i++)
                {
                    float a = i / 24f * Mathf.Tau;
                    pts[i] = r.Pos + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.5f);
                }
                DrawPolyline(pts, new Color(1.9f, 2.1f, 2.5f, 0.85f * (1f - f)), 0.5f, true);
            }
            foreach (var p in _papers)
            {
                bool flat = Mathf.Sin(_time * 7f + p.Phase) > 0f;
                DrawRect(new Rect2(p.Pos, flat ? new Vector2(3, 1) : new Vector2(2, 2)), new Color(1.7f, 1.6f, 1.4f));
            }
        }
    }
}
