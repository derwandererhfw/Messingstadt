using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Die Wolke eines Rauchtopfs: Qualm quillt aus einer Blechdose am Boden, breitet sich aus und steht dann als dichte,
    /// träge Wolke. Sie liegt genau über dem Kreis, in dem die Regel die Sicht sperrt, ein dunkler Fleck am Boden zeigt
    /// seinen Rand. Reine Darstellung: Welche Fläche sperrt und wie lange, bestimmt allein World.Smokes.
    /// </summary>
    public partial class SmokePot : Node2D
    {
        // Ein Kreis auf dem Boden wird in der Schrägansicht zur Ellipse: so viele Bildpunkte je Feld Halbmesser.
        private const float PixelsPerTileAcross = 45.25f;
        private const int Puffs = 40;
        private const float SpreadSeconds = 1.1f;
        private const float FadeSeconds = 3f;
        private const float PuffAlpha = 0.4f;
        private const float PuffShare = 0.5f;
        private const float MaxHeight = 44f;
        private const float GroundShade = 0.3f;
        private const float RimAlpha = 0.22f;
        private const float MinLight = 0.42f;
        private const float CanSeenInSmoke = 0.5f;
        private const float MaxLight = 1f;
        private const float WispsPerSecond = 9f;
        private const float WispSeconds = 1.4f;
        private static readonly Color SmokeGrey = new(0.86f, 0.86f, 0.85f);
        private static readonly Color Tin = new(0.34f, 0.33f, 0.3f);
        private static readonly Color TinDark = new(0.16f, 0.15f, 0.14f);
        private static readonly Color Band = new(0.62f, 0.5f, 0.24f);
        private static readonly Color Ember = new(1f, 0.55f, 0.2f);

        private sealed class Puff
        {
            public Vector2 Home;
            public float Height;
            public float Size;
            public float Phase;
            public float Shade;
        }

        private sealed class Wisp
        {
            public Vector2 Pos;
            public float Age;
            public float Drift;
        }

        /// <summary>Eine Wolke mit eigener Tiefe: Was vor ihrer Mitte steht, verdeckt sie.</summary>
        private sealed partial class Cloud : Node2D
        {
            public SmokePot Owner_ = null!;
            public SmokeCloud Rule;
            public float Age;
            public bool Seen;
            public readonly List<Puff> Puffs = new();
            public readonly List<Wisp> Wisps = new();
            public float WispDebt;

            public override void _Draw() => Owner_.DrawCloud(this);
        }

        private readonly Random _rng = new();
        private readonly Dictionary<(int, int, int), Cloud> _clouds = new();
        private Texture2D _disc = null!;
        private float _time;

        public Main Game = null!;

        /// <summary>Prüflauf: hält auf diesem Feld eine Wolke, wie sie der Rauchtopf aus data/waffen.json legt.</summary>
        public Vector2? TestAt;
        private const float TestRadiusTiles = 1.5f;
        private const float TestSeconds = 600f;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public override void _Ready()
        {
            LightMask = 0;
            _disc = MakeBall();
        }

        /// <summary>Ballen mit dichtem Kern und weichem Rand. Die Scheibe aus GunSmoke ist für dicken Qualm zu dünn.</summary>
        private static Texture2D MakeBall()
        {
            const int S = 48;
            var image = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float d = new Vector2((x + 0.5f) / S * 2f - 1f, (y + 0.5f) / S * 2f - 1f).Length();
                    image.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0.3f, d)));
                }
            }
            return ImageTexture.CreateFromImage(image);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            if (TestAt is Vector2 at && Game.World.Smokes.Count == 0)
            {
                Game.World.Smokes.Add(new SmokeCloud((int)(at.X * World.TileUnits), (int)(at.Y * World.TileUnits),
                    (int)(TestRadiusTiles * World.TileUnits), Game.World.Tick + (int)(TestSeconds * World.TickRate)));
            }
            foreach (var cloud in _clouds.Values)
            {
                cloud.Seen = false;
            }
            foreach (var s in Game.World.Smokes)
            {
                var key = (s.X, s.Y, s.UntilTick);
                if (!_clouds.TryGetValue(key, out var cloud))
                {
                    cloud = AddCloud(s);
                    _clouds[key] = cloud;
                }
                cloud.Seen = true;
                cloud.Age += dt;
                UpdateWisps(cloud, dt);
                cloud.QueueRedraw();
            }
            foreach (var key in new List<(int, int, int)>(_clouds.Keys))
            {
                if (!_clouds[key].Seen)
                {
                    _clouds[key].QueueFree();
                    _clouds.Remove(key);
                }
            }
        }

        private Cloud AddCloud(SmokeCloud s)
        {
            float tileX = s.X / (float)World.TileUnits, tileY = s.Y / (float)World.TileUnits;
            var cloud = new Cloud { Owner_ = this, Rule = s, Position = Game.ToScreen(s.X, s.Y), ZIndex = Game.DepthZ(tileX, tileY) + 14, LightMask = 0 };
            float across = s.Radius / (float)World.TileUnits * PixelsPerTileAcross;
            for (int i = 0; i < Puffs; i++)
            {
                // Gleichmäßig über die Kreisfläche verteilt. Der Ballen reicht mit seinem Rand bis an den Rand der Regel.
                float size = across * PuffShare * Rand(0.8f, 1.15f);
                float reach = Mathf.Sqrt((float)_rng.NextDouble()) * Mathf.Max(0f, across - size * 0.75f);
                float angle = Rand(0f, Mathf.Tau);
                // Die meisten Ballen liegen flach über dem Boden und decken die Fläche, der Rest türmt sich in der Mitte.
                float lift = _rng.NextDouble() < 0.55 ? Rand(2f, 9f) : Rand(9f, MaxHeight) * (1f - 0.6f * reach / Mathf.Max(1f, across));
                cloud.Puffs.Add(new Puff
                {
                    Home = new Vector2(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach * 0.5f),
                    Height = lift, Size = size, Phase = Rand(0f, Mathf.Tau),
                    // Unten liegt der Qualm im eigenen Schatten, oben fängt er das Licht.
                    Shade = 0.5f + 0.6f * lift / MaxHeight + Rand(-0.06f, 0.06f),
                });
            }
            // Von hinten nach vorn zeichnen, sonst liegen ferne Ballen über nahen.
            cloud.Puffs.Sort((a, b) => (a.Home.Y + a.Height * 0.3f).CompareTo(b.Home.Y + b.Height * 0.3f));
            AddChild(cloud);
            return cloud;
        }

        /// <summary>Aus der Dose steigt weiter Qualm nach.</summary>
        private void UpdateWisps(Cloud cloud, float dt)
        {
            cloud.WispDebt += WispsPerSecond * dt;
            while (cloud.WispDebt >= 1f)
            {
                cloud.WispDebt -= 1f;
                cloud.Wisps.Add(new Wisp { Pos = new Vector2(Rand(-1.5f, 1.5f), -6f), Drift = Rand(-5f, 5f) });
            }
            foreach (var w in cloud.Wisps)
            {
                w.Age += dt;
                w.Pos += new Vector2(w.Drift + Game.Climate.Wind * 0.02f, -17f) * dt;
            }
            cloud.Wisps.RemoveAll(w => w.Age >= WispSeconds);
        }

        private void DrawCloud(Cloud cloud)
        {
            var world = Game.World;
            var night = Game.NightColor;
            float across = cloud.Rule.Radius / (float)World.TileUnits * PixelsPerTileAcross;
            float left = Mathf.Clamp((cloud.Rule.UntilTick - world.Tick) / (float)World.TickRate / FadeSeconds, 0f, 1f);
            float spread = 1f - Mathf.Pow(1f - Mathf.Clamp(cloud.Age / SpreadSeconds, 0f, 1f), 2f);
            // Der Qualm nimmt das Licht seines Ortes an, im Dunkeln bleibt er als fahler Schemen sichtbar.
            var light = Game.FigureLight(cloud.Rule.X, cloud.Rule.Y, MinLight);
            // Rauch leuchtet nicht selbst: Auch dicht unter einer Laterne bleibt er grau statt weiß.
            float brightest = Mathf.Max(light.R * night.R, Mathf.Max(light.G * night.G, light.B * night.B));
            float limit = Mathf.Min(1f, MaxLight / Mathf.Max(0.01f, brightest));
            var grey = new Color(SmokeGrey.R * light.R * limit, SmokeGrey.G * light.G * limit, SmokeGrey.B * light.B * limit);

            // Schatten am Boden: genau die Fläche, die nach der Regel die Sicht sperrt.
            cloud.DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.5f));
            cloud.DrawCircle(Vector2.Zero, across * spread, new Color(0f, 0f, 0f, GroundShade * left * spread));
            cloud.DrawArc(Vector2.Zero, across * spread, 0f, Mathf.Tau, 48, new Color(grey, RimAlpha * left), 0.8f, true);
            cloud.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

            foreach (var w in cloud.Wisps)
            {
                float f = w.Age / WispSeconds;
                float r = 3f + 7f * f;
                cloud.DrawTextureRect(_disc, new Rect2(w.Pos - new Vector2(r, r), new Vector2(r * 2f, r * 2f)), false, new Color(grey, 0.5f * (1f - f) * left));
            }
            foreach (var p in cloud.Puffs)
            {
                // Träges Wogen: Jeder Ballen kreist ein wenig um seinen Platz und atmet.
                var sway = new Vector2(Mathf.Sin(_time * 0.5f + p.Phase), Mathf.Cos(_time * 0.37f + p.Phase * 1.7f) * 0.5f) * across * 0.06f;
                float breathe = 1f + 0.08f * Mathf.Sin(_time * 0.8f + p.Phase * 2.3f);
                var at = (p.Home + sway) * spread - new Vector2(0f, p.Height * spread);
                float r = p.Size * breathe * (0.35f + 0.65f * spread);
                var color = new Color(grey.R * p.Shade, grey.G * p.Shade, grey.B * p.Shade, PuffAlpha * left * (0.4f + 0.6f * spread));
                // Etwas flacher als breit, wie eine Wolke, die am Boden liegt.
                cloud.DrawTextureRect(_disc, new Rect2(at - new Vector2(r, r * 0.72f), new Vector2(r * 2f, r * 1.44f)), false, color);
                if (p.Height > 9f)
                {
                    // Die Kuppe eines hohen Ballens fängt das Licht von oben.
                    float top = r * 0.55f;
                    var bright = new Color(grey.R * (p.Shade + 0.3f), grey.G * (p.Shade + 0.3f), grey.B * (p.Shade + 0.32f), color.A * 0.7f);
                    cloud.DrawTextureRect(_disc, new Rect2(at + new Vector2(-top * 0.8f, -r * 0.5f - top * 0.5f), new Vector2(top * 2f, top * 1.3f)), false, bright);
                }
            }
            DrawCan(cloud, night, Mathf.Lerp(1f, CanSeenInSmoke, spread));
        }

        /// <summary>Die Blechdose mit Messingband, oben glimmt die Zündung. Im dichten Qualm ist sie nur noch zu ahnen.</summary>
        private void DrawCan(Cloud cloud, Color night, float seen)
        {
            Color Lit(Color c, float a = 1f) => new(c.R / night.R, c.G / night.G, c.B / night.B, a * seen);
            cloud.DrawRect(new Rect2(-2.5f, -6f, 5f, 6f), Lit(TinDark));
            cloud.DrawRect(new Rect2(-2.5f, -6f, 2f, 6f), Lit(Tin));
            cloud.DrawRect(new Rect2(-2.5f, -3.5f, 5f, 1f), Lit(Band));
            float glow = 0.6f + 0.4f * Mathf.Sin(_time * 13f) * Mathf.Sin(_time * 7f);
            cloud.DrawRect(new Rect2(-1f, -7f, 2f, 1f), Lit(Ember, glow));
        }
    }
}
