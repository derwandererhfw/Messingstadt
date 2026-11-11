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
        // Lage wie GULLIES und GRATES in tools/art/generate_concept.py. Ändert die Grafik sie dort, hier nachziehen.
        private static readonly (float X, float Y, bool AlongY)[] Gullies =
        {
            (8.11f, 3.4f, true), (8.11f, 13.3f, true), (9.89f, 16.4f, true), (9.89f, 7.6f, true),
            (4.3f, 9.15f, false), (17.3f, 9.15f, false), (14.5f, 11.85f, false), (6.9f, 11.85f, false),
        };
        private static readonly (float X0, float Y0, float X1, float Y1)[] Grates =
        {
            (2.2f, 6.03f, 2.7f, 6.2f), (4.4f, 6.03f, 4.9f, 6.2f), (6.03f, 2.6f, 6.2f, 3.1f), (2.0f, 17.03f, 2.5f, 17.2f), (6.03f, 13.2f, 6.2f, 13.7f),
            (6.03f, 15.6f, 6.2f, 16.1f), (13.0f, 18.03f, 13.5f, 18.2f), (17.03f, 14.2f, 17.2f, 14.7f), (18.03f, 2.2f, 18.2f, 2.7f),
        };
        // Die Fahrbahnen: längs x zwischen y 9 und 12, längs y zwischen x 8 und 10. In der Kreuzung gibt es keinen Rinnstein.
        private const float RoadX0 = 8f, RoadX1 = 10f, RoadY0 = 9f, RoadY1 = 12f;
        private const float MapMin = 0.3f, MapMax = 19.7f;
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
            foreach (var (x, y, alongY) in Gullies)
            {
                // Der Rinnstein endet an der Kreuzung und am Kartenrand.
                float at = alongY ? y : x;
                float low = alongY ? (y < RoadY0 ? MapMin : RoadY1) : (x < RoadX0 ? MapMin : RoadX1);
                float high = alongY ? (y < RoadY0 ? RoadY0 : MapMax) : (x < RoadX0 ? RoadX0 : MapMax);
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
            foreach (var (x0, y0, x1, y1) in Grates)
            {
                var middle = new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f);
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
            foreach (var (x, y, _) in Gullies)
            {
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
