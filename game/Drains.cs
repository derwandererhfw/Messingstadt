using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Wohin das Wasser der Straße läuft und was aus den Kellern steigt: Im Rinnstein ziehen Lichtpunkte zum nächsten
    /// Straßenablauf, aus den Kellerrosten an den Hauswänden steigt dünner Dampf. Reine Darstellung.
    /// </summary>
    public partial class Drains : Node2D
    {
        // Abläufe, Roste und Fahrbahnen kommen je Karte aus MapSite. Ohne Fahrbahnen gibt es keinen Rinnstein.
        private const float MapMin = 0.3f;
        private const float Reach = 4f;
        private const float GlintsPerTile = 1.1f;
        private const float GlintTiles = 0.09f;
        private const float FlowTilesPerSecond = 0.9f;
        private const float GlintAlpha = 0.42f;
        private const float MinLight = 0.14f;
        private const float MinFlow = 0.05f;
        private static readonly Color WaterTint = new(1.05f, 1.15f, 1.4f);
        private static readonly Color SteamColor = new(2f, 2f, 2.1f, 0.15f);
        private const int SteamAmount = 7;
        private const float SteamSeconds = 2.4f;

        private sealed class Run
        {
            public Vector2 From;
            public Vector2 To;
            public float Phase;
            public int Glints;
        }

        private readonly Random _rng = new();
        private readonly List<Run> _runs = new();
        private readonly List<CpuParticles2D> _steam = new();
        private float _time;

        public Main Game = null!;

        private Vector2 Screen(Vector2 tile) => Game.ToScreen((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits));

        public override void _Ready()
        {
            ZIndex = 0;
            var site = MapSite.Current;
            foreach (var (tile, alongY) in site.Road == null ? new List<(Vector2, bool)>() : site.Gullies)
            {
                float x = tile.X, y = tile.Y;
                float RoadX0 = site.Road!.X0, RoadX1 = site.Road.X1, RoadY0 = site.Road.Y0, RoadY1 = site.Road.Y1;
                // Der Rinnstein endet an der Kreuzung und am Kartenrand.
                float at = alongY ? y : x;
                float low = alongY ? (y < RoadY0 ? MapMin : RoadY1) : (x < RoadX0 ? MapMin : RoadX1);
                float high = alongY ? (y < RoadY0 ? RoadY0 : MapSite.Tiles.Y - MapMin) : (x < RoadX0 ? RoadX0 : MapSite.Tiles.X - MapMin);
                foreach (float end in new[] { Mathf.Max(low, at - Reach), Mathf.Min(high, at + Reach) })
                {
                    float length = Mathf.Abs(end - at);
                    if (length < 0.5f)
                    {
                        continue;
                    }
                    _runs.Add(new Run
                    {
                        From = alongY ? new Vector2(x, end) : new Vector2(end, y), To = new Vector2(x, y),
                        Phase = (float)_rng.NextDouble(), Glints = Mathf.CeilToInt(length * GlintsPerTile),
                    });
                }
            }
            var puff = new GradientTexture2D
            {
                Gradient = new Gradient { Offsets = new[] { 0f, 0.6f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0f) } },
                Width = 16, Height = 16, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
            var ramp = new Gradient
            {
                Offsets = new[] { 0f, 0.3f, 1f },
                Colors = new[] { new Color(SteamColor, 0f), SteamColor, new Color(SteamColor, 0f) },
            };
            var growth = new Curve();
            growth.AddPoint(new Vector2(0f, 0.3f));
            growth.AddPoint(new Vector2(1f, 1f));
            foreach (var grate in site.Grates)
            {
                var middle = grate.GetCenter();
                var steam = new CpuParticles2D
                {
                    Texture = puff, TextureFilter = TextureFilterEnum.Linear, Position = Screen(middle), ZIndex = Game.DepthZ(middle.X, middle.Y) + 3,
                    Amount = SteamAmount, Lifetime = SteamSeconds, Preprocess = SteamSeconds, Randomness = 0.6f,
                    EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle, EmissionRectExtents = new Vector2(6f, 2f),
                    Direction = new Vector2(0.2f, -1f), Spread = 18f, Gravity = new Vector2(3f, -3f),
                    InitialVelocityMin = 5f, InitialVelocityMax = 10f, ScaleAmountMin = 0.55f, ScaleAmountMax = 1.1f,
                    ScaleAmountCurve = growth, ColorRamp = ramp, LightMask = 0,
                };
                AddChild(steam);
                _steam.Add(steam);
            }
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            var drift = new Vector2(3f + Game.Climate.Wind * 0.05f, -3f);
            foreach (var steam in _steam)
            {
                steam.Gravity = drift;
            }
            QueueRedraw();
        }

        public override void _Draw()
        {
            float flow = Game.Climate.RoofFlow;
            if (flow < MinFlow)
            {
                return;
            }
            foreach (var run in _runs)
            {
                var way = run.To - run.From;
                float length = way.Length();
                var step = way / length * GlintTiles;
                for (int i = 0; i < run.Glints; i++)
                {
                    // Jeder Lichtpunkt wandert vom Ende des Rinnsteins zum Ablauf und taucht dort weg.
                    float s = (_time * FlowTilesPerSecond / length + run.Phase + i / (float)run.Glints + 0.37f * Mathf.Sin(i * 12.9f)) % 1f;
                    var tile = run.From + way * s;
                    var light = Game.FigureLight((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits), MinLight);
                    float alpha = GlintAlpha * flow * Mathf.Sqrt(Mathf.Sin(s * Mathf.Pi));
                    DrawLine(Screen(tile), Screen(tile + step), new Color(light.R * WaterTint.R, light.G * WaterTint.G, light.B * WaterTint.B, alpha), 0.6f);
                }
            }
            foreach (var (tile, _) in MapSite.Current.Road == null ? new List<(Vector2, bool)>() : MapSite.Current.Gullies)
            {
                float x = tile.X, y = tile.Y;
                // Am Ablauf strudelt es.
                var at = Screen(new Vector2(x, y));
                var light = Game.FigureLight((int)(x * World.TileUnits), (int)(y * World.TileUnits), MinLight);
                for (int i = 0; i < 3; i++)
                {
                    float a = _time * 5f + i * Mathf.Tau / 3f + x;
                    var p = at + new Vector2(Mathf.Cos(a) * 2.5f, Mathf.Sin(a) * 1.2f);
                    DrawRect(new Rect2(p, new Vector2(0.7f, 0.7f)), new Color(light.R * WaterTint.R, light.G * WaterTint.G, light.B * WaterTint.B, 0.7f * flow));
                }
            }
        }
    }
}
