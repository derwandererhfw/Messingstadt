using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Wo der Regen hinläuft: Fallrohre an den Hausecken, Tropfen von Dachkanten und Trägern, Aufspritzer auf dem Pflaster.
    /// Alles nimmt sein Licht aus dem Lichtgitter, im Dunkeln bleibt es also fast unsichtbar. Reine Darstellung.
    /// </summary>
    public partial class Water : Node2D
    {
        private const float MinFootprint = 6f;
        private const float MinHeight = 60f;
        private const float BeamMinLength = 5f;
        private const float BeamMaxDepth = 0.3f;
        private const float BeamMaxHeight = 80f;
        private const float MinLight = 0.1f;
        private const int SplashesAtFullRain = 46;
        private const float SplashSeconds = 0.14f;
        private const float StreamLight = 0.42f;
        private const float StreamBaseAlpha = 0.16f;
        private const float GlintLength = 5f;
        private const float GlintSpacing = 37f;
        private const float GlintAlpha = 0.5f;
        private const float StreamSpeed = 150f;
        private const float DripLight = 0.6f;
        private const float DripGravity = 620f;
        private const int DripsPerWall = 9;
        private const int DripsPerBeam = 14;
        private static readonly Color WaterTint = new(1.05f, 1.15f, 1.4f);

        /// <summary>Eine Hauswand mit ihren Kanten. Eigene Tiefe, damit Dinge davor das Wasser verdecken.</summary>
        private sealed partial class Wall : Node2D
        {
            private sealed class Drip
            {
                public Vector2 Top;
                public float FootY;
                public float Fall;
                public float Wait;
                public Color Light;
            }

            private readonly List<(Vector2 Top, Vector2 Foot, Color Light)> _streams = new();
            private readonly List<(Vector2 From, Vector2 To, float Height)> _edges = new();
            private readonly List<Drip> _drips = new();
            private readonly Random _rng = new();
            private float _time;

            public Main Game = null!;

            private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

            public void AddStream(Vector2 top, Vector2 foot, Color light) => _streams.Add((top, foot, light));

            public void AddEdge(Vector2 from, Vector2 to, float height) => _edges.Add((from, to, height));

            public void MakeDrips(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    _drips.Add(new Drip { Wait = Rand(0f, 2f), Fall = -1f });
                }
            }

            public override void _Process(double delta)
            {
                float dt = (float)delta;
                _time += dt;
                float flow = Game.Climate.RoofFlow;
                foreach (var d in _drips)
                {
                    if (d.Fall >= 0f)
                    {
                        d.Fall += dt;
                        if (d.Top.Y + DripGravity * d.Fall * d.Fall / 2f >= d.FootY)
                        {
                            d.Fall = -1f;
                            d.Wait = Rand(0.2f, 2.2f) / Mathf.Max(0.08f, flow);
                        }
                    }
                    else if (flow > 0.04f && _edges.Count > 0)
                    {
                        d.Wait -= dt;
                        if (d.Wait <= 0f)
                        {
                            var (from, to, height) = _edges[_rng.Next(_edges.Count)];
                            var foot = from.Lerp(to, Rand(0.03f, 0.97f));
                            d.Top = foot - new Vector2(0f, height);
                            d.FootY = foot.Y;
                            d.Fall = 0f;
                            var tile = Game.ScreenToMap(foot);
                            d.Light = Game.FigureLight(Units(tile.X), Units(tile.Y), MinLight);
                        }
                    }
                }
                QueueRedraw();
            }

            public override void _Draw()
            {
                float flow = Game.Climate.RoofFlow;
                if (flow <= 0.02f)
                {
                    return;
                }
                foreach (var (top, foot, light) in _streams)
                {
                    // Ein dünner, dunkler Faden, an dem einzelne Lichtpunkte herablaufen. Das Rohr selbst bleibt Hintergrund.
                    var color = new Color(light.R * WaterTint.R * StreamLight, light.G * WaterTint.G * StreamLight, light.B * WaterTint.B * StreamLight, GlintAlpha * flow);
                    float height = foot.Y - top.Y;
                    DrawLine(top, foot, new Color(color, StreamBaseAlpha * flow), 1f);
                    float y = (_time * StreamSpeed + top.X) % GlintSpacing - GlintSpacing;
                    int glint = 0;
                    while (y < height)
                    {
                        // Unregelmäßige Abstände, sonst sähe es nach einer gestrichelten Linie aus.
                        float at = y + Mathf.Sin(glint * 2.4f + top.X) * GlintSpacing * 0.35f;
                        float from = Mathf.Max(0f, at), to = Mathf.Min(height, at + GlintLength);
                        if (to > from)
                        {
                            DrawLine(top + new Vector2(0f, from), top + new Vector2(0f, to), color, 1f);
                        }
                        y += GlintSpacing;
                        glint++;
                    }
                    // Unten spritzt es auseinander.
                    for (int i = 0; i < 4; i++)
                    {
                        float phase = (_time * 2.6f + i * 0.27f) % 1f;
                        float side = i % 2 == 0 ? 1f : -1f;
                        var at = foot + new Vector2(side * (2f + i) * phase * 2f, -(4f + i) * Mathf.Sin(phase * Mathf.Pi) * flow);
                        DrawRect(new Rect2(at, new Vector2(1f, 1f)), new Color(color, color.A * (1f - phase)));
                    }
                }
                foreach (var d in _drips)
                {
                    if (d.Fall >= 0f)
                    {
                        float y = d.Top.Y + DripGravity * d.Fall * d.Fall / 2f;
                        float tail = Mathf.Min(5f, DripGravity * d.Fall * 0.02f + 1f);
                        DrawLine(new Vector2(d.Top.X, y - tail), new Vector2(d.Top.X, y), new Color(d.Light.R * WaterTint.R * DripLight, d.Light.G * WaterTint.G * DripLight, d.Light.B * WaterTint.B * DripLight, 0.75f), 1f);
                    }
                }
            }

            private static int Units(float tile) => (int)(Mathf.Clamp(tile, 0.1f, 19.9f) * World.TileUnits);
        }

        private sealed class Splash
        {
            public Vector2 Pos;
            public float Age;
            public Color Light;
        }

        private readonly Random _rng = new();
        private readonly List<Splash> _splashes = new();
        private float _debt;

        public Main Game = null!;
        public string ManifestPath = "";

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private Vector2 Tile(float x, float y) => Game.ToScreen((int)(x * World.TileUnits), (int)(y * World.TileUnits));

        public override void _Ready()
        {
            ZIndex = 0;
            using var manifest = JsonDocument.Parse(File.ReadAllText(ManifestPath));
            foreach (var t in manifest.RootElement.GetProperty("things").EnumerateArray())
            {
                float x0 = t.GetProperty("x0").GetSingle(), y0 = t.GetProperty("y0").GetSingle();
                float x1 = t.GetProperty("x1").GetSingle(), y1 = t.GetProperty("y1").GetSingle();
                float z1 = t.GetProperty("z1").GetSingle();
                float w = x1 - x0, d = y1 - y0;
                bool house = w * d >= MinFootprint && z1 >= MinHeight;
                bool beam = w >= BeamMinLength && d <= BeamMaxDepth && z1 <= BeamMaxHeight;
                if (house)
                {
                    AddHouse(x0, y0, x1, y1, z1);
                }
                else if (beam)
                {
                    AddBeam(x0, x1, y1, z1);
                }
            }
        }

        private void AddHouse(float x0, float y0, float x1, float y1, float z1)
        {
            var wall = new Wall { Game = this.Game, ZIndex = Game.DepthZ(x1 + 0.05f, y1 + 0.05f) };
            AddChild(wall);
            // Fallrohre an der vorderen Ecke und an der Ecke zur rechten Seite.
            foreach (var (x, y) in new[] { (x1, y1), (x1, y0) })
            {
                var foot = Tile(x, y);
                var light = Game.FigureLight((int)(Mathf.Clamp(x + 0.1f, 0.1f, 19.9f) * World.TileUnits), (int)(Mathf.Clamp(y + 0.1f, 0.1f, 19.9f) * World.TileUnits), MinLight);
                wall.AddStream(foot - new Vector2(0f, z1), foot, light);
                Game.Sound.AddGutter(foot);
            }
            wall.AddEdge(Tile(x0, y1), Tile(x1, y1), z1);
            wall.AddEdge(Tile(x1, y1), Tile(x1, y0), z1);
            wall.MakeDrips(DripsPerWall);
        }

        private void AddBeam(float x0, float x1, float y1, float z1)
        {
            var wall = new Wall { Game = this.Game, ZIndex = Game.DepthZ(x1, y1 + 0.05f) };
            AddChild(wall);
            wall.AddEdge(Tile(x0, y1), Tile(x1, y1), z1);
            wall.MakeDrips(DripsPerBeam);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            var canvas = GetCanvasTransform();
            var origin = canvas.AffineInverse() * Vector2.Zero;
            var size = GetViewportRect().Size / canvas.Scale;
            _debt += Game.Climate.RainStrength * SplashesAtFullRain * dt;
            while (_debt >= 1f)
            {
                _debt -= 1f;
                var pos = origin + new Vector2(Rand(0f, size.X), Rand(0f, size.Y));
                var tile = Game.ScreenToMap(pos);
                if (tile.X > 0f && tile.Y > 0f && tile.X < 20f && tile.Y < 20f)
                {
                    _splashes.Add(new Splash { Pos = pos, Light = Game.FigureLight((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits), MinLight) });
                }
            }
            foreach (var s in _splashes)
            {
                s.Age += dt;
            }
            _splashes.RemoveAll(s => s.Age >= SplashSeconds);
            QueueRedraw();
        }

        // Liegt auf Höhe des Bodens: Dächer und Wände verdecken die Aufspritzer.
        public override void _Draw()
        {
            foreach (var s in _splashes)
            {
                float f = s.Age / SplashSeconds;
                var color = new Color(s.Light.R * WaterTint.R, s.Light.G * WaterTint.G, s.Light.B * WaterTint.B, 0.85f * (1f - f));
                float spread = 1f + 2f * f;
                DrawRect(new Rect2(s.Pos + new Vector2(-spread, -f * 2f), new Vector2(1f, 1f)), color);
                DrawRect(new Rect2(s.Pos + new Vector2(spread, -f * 2f), new Vector2(1f, 1f)), color);
            }
        }
    }
}
