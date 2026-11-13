using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Was sich in Höfen und auf freien Plätzen im Wind bewegt: Nebelschwaden über dem Friedhof, Wäsche auf der Leine,
    /// eine Fahne am Mast. Die Orte nennt die Karte (mist, laundry, flags). Reine Darstellung: Die Schwaden nehmen
    /// keiner Wache und keinem Agenten die Sicht. Abschaltbar mit --ohne=schwaden,waesche,fahne.
    /// </summary>
    public partial class Yards : Node2D
    {
        private const float PuffsPerTile = 0.3f;
        private const float PuffAlpha = 0.22f;
        private const float PuffSeconds = 14f;
        private const float PuffDriftTiles = 0.16f;
        private const int Cloths = 5;
        private const float ClothDrop = 13f;
        private const float FlagLength = 20f;
        private const float FlagHeight = 12f;
        private const int FlagPieces = 6;
        private const float MinLight = 0.22f;
        private static readonly Color MistColor = new(0.5f, 0.56f, 0.7f);
        private static readonly Color[] ClothColors = { new(0.8f, 0.8f, 0.76f), new(0.62f, 0.66f, 0.74f), new(0.78f, 0.72f, 0.62f), new(0.7f, 0.7f, 0.7f) };
        private static readonly Color FlagRed = new(0.5f, 0.1f, 0.09f);
        private static readonly Color FlagDark = new(0.08f, 0.08f, 0.09f);
        private static readonly Color Cord = new(0.06f, 0.06f, 0.07f);

        private sealed class Puff
        {
            public Sprite2D Sprite = null!;
            public Rect2 Area;
            public Vector2 Tile;
            public float Age;
            public float Life;
            public float Size;
        }

        private sealed partial class Layer : Node2D
        {
            public Action<Layer> Paint = _ => { };

            public override void _Draw() => Paint(this);
        }

        private readonly Random _rng = new();
        private readonly List<Puff> _puffs = new();
        private readonly List<Layer> _moving = new();
        private Color _unNight;
        private float _time;

        public Main Game = null!;
        public Texture2D Glow = null!;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private Vector2 Map(Vector2 tile) => Game.ToScreen((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits));

        private int Depth(Vector2 tile) => Game.DepthZ(Mathf.Clamp(tile.X, 0.05f, MapSite.Tiles.X - 0.05f), Mathf.Clamp(tile.Y, 0.05f, MapSite.Tiles.Y - 0.05f)) + 1;

        private Color Lit(Color c, Vector2 tile, float alpha = 1f)
        {
            var light = Game.FigureLight(
                (int)(Mathf.Clamp(tile.X, 0.1f, MapSite.Tiles.X - 0.1f) * World.TileUnits), (int)(Mathf.Clamp(tile.Y, 0.1f, MapSite.Tiles.Y - 0.1f) * World.TileUnits), MinLight);
            return new Color(c.R * light.R, c.G * light.G, c.B * light.B, alpha);
        }

        private static bool OnMap(Vector2 tile) => tile.X >= 0f && tile.Y >= 0f && tile.X <= MapSite.Tiles.X && tile.Y <= MapSite.Tiles.Y;

        public override void _Ready()
        {
            var night = Game.NightColor;
            _unNight = new Color(1f / night.R, 1f / night.G, 1f / night.B);
            LightMask = 0;
            var site = MapSite.Current;
            var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            if (!AirTest.Off("schwaden"))
            {
                foreach (var area in site.Mist)
                {
                    if (!OnMap(area.Position) || !OnMap(area.End))
                    {
                        continue;
                    }
                    int count = Mathf.Max(3, Mathf.RoundToInt(area.Area * PuffsPerTile));
                    for (int i = 0; i < count; i++)
                    {
                        var puff = new Puff { Area = area, Sprite = new Sprite2D { Texture = Glow, Material = additive, LightMask = 0 } };
                        AddChild(puff.Sprite);
                        Respawn(puff);
                        puff.Age = Rand(0f, puff.Life);
                        _puffs.Add(puff);
                    }
                }
            }
            if (!AirTest.Off("waesche"))
            {
                foreach (var (from, to, height) in site.Laundry)
                {
                    if (OnMap(from) && OnMap(to))
                    {
                        var line = (from, to, height);
                        AddMoving(new Layer { ZIndex = Depth((from + to) / 2f), LightMask = 0, Paint = layer => PaintLaundry(layer, line.from, line.to, line.height) });
                    }
                }
            }
            if (!AirTest.Off("fahne"))
            {
                foreach (var (tile, height) in site.Flags)
                {
                    if (OnMap(tile))
                    {
                        var flag = (tile, height);
                        AddMoving(new Layer { ZIndex = Depth(tile), LightMask = 0, Paint = layer => PaintFlag(layer, flag.tile, flag.height) });
                    }
                }
            }
            if (_puffs.Count == 0 && _moving.Count == 0)
            {
                SetProcess(false);
            }
        }

        private void AddMoving(Layer layer)
        {
            AddChild(layer);
            _moving.Add(layer);
        }

        private void Respawn(Puff p)
        {
            p.Tile = new Vector2(Rand(p.Area.Position.X, p.Area.End.X), Rand(p.Area.Position.Y, p.Area.End.Y));
            p.Age = 0f;
            p.Life = PuffSeconds * Rand(0.7f, 1.4f);
            p.Size = Rand(0.8f, 1.4f);
            p.Sprite.ZIndex = Depth(p.Tile);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            // Der Wind schiebt die Schwaden im Bild nach rechts, in Böen schneller.
            float push = PuffDriftTiles * (0.5f + Game.Climate.Wind / 12f);
            var seen = ViewCull.Seen(this, 160f);
            foreach (var p in _puffs)
            {
                p.Age += dt;
                p.Tile += new Vector2(push, -push) * dt;
                if (p.Age > p.Life || !p.Area.Grow(0.6f).HasPoint(p.Tile))
                {
                    Respawn(p);
                }
                var at = Map(p.Tile) - new Vector2(0, 7f);
                p.Sprite.Visible = seen.HasPoint(at);
                if (!p.Sprite.Visible)
                {
                    continue;
                }
                float life = Mathf.Sin(p.Age / p.Life * Mathf.Pi);
                float breathe = 1f + 0.08f * Mathf.Sin(_time * 0.6f + p.Life);
                p.Sprite.Position = at;
                p.Sprite.Scale = new Vector2(p.Size * breathe, p.Size * 0.34f);
                p.Sprite.Modulate = new Color(MistColor.R * _unNight.R, MistColor.G * _unNight.G, MistColor.B * _unNight.B, PuffAlpha * life * AirTest.Scale / 0.35f);
            }
            foreach (var layer in _moving)
            {
                layer.QueueRedraw();
            }
        }

        private float Gust(float phase) => (0.35f + Game.Climate.Wind / 34f) * (Mathf.Sin(_time * 2.1f + phase) + 0.5f * Mathf.Sin(_time * 5.3f + phase * 1.7f));

        /// <summary>Eine Leine quer über den Hof mit ein paar Wäschestücken, die im Wind schwingen.</summary>
        private void PaintLaundry(Layer layer, Vector2 from, Vector2 to, float height)
        {
            var a = Map(from) - new Vector2(0, height);
            var b = Map(to) - new Vector2(0, height);
            var sag = new Vector2(0, 3f);
            layer.DrawPolyline(new[] { a, (a + b) / 2f + sag, b }, new Color(Cord.R * _unNight.R, Cord.G * _unNight.G, Cord.B * _unNight.B), 0.6f);
            for (int i = 0; i < Cloths; i++)
            {
                float t0 = (i + 0.35f) / (Cloths + 0.4f), t1 = t0 + 0.13f;
                var p0 = a.Lerp(b, t0) + sag * (1f - Mathf.Abs(2f * t0 - 1f));
                var p1 = a.Lerp(b, t1) + sag * (1f - Mathf.Abs(2f * t1 - 1f));
                float drop = ClothDrop * (0.7f + 0.3f * ((i * 7) % 3) / 2f);
                var sway = new Vector2(Gust(i * 1.9f) * 3.2f, 0f);
                var color = Lit(ClothColors[i % ClothColors.Length], from.Lerp(to, t0));
                color = new Color(color.R * _unNight.R, color.G * _unNight.G, color.B * _unNight.B);
                layer.DrawColoredPolygon(new[] { p0, p1, p1 + new Vector2(0, drop) + sway, p0 + new Vector2(0, drop) + sway * 0.8f }, color);
            }
        }

        /// <summary>Das Tuch am Fahnenmast. Den Mast selbst zeichnet die Stadt.</summary>
        private void PaintFlag(Layer layer, Vector2 tile, float height)
        {
            var top = Map(tile) - new Vector2(0, height);
            var red = Lit(FlagRed, tile);
            var dark = Lit(FlagDark, tile);
            red = new Color(red.R * _unNight.R, red.G * _unNight.G, red.B * _unNight.B);
            dark = new Color(dark.R * _unNight.R, dark.G * _unNight.G, dark.B * _unNight.B);
            float wind = 0.45f + Game.Climate.Wind / 34f;
            Vector2 Edge(int i, float down)
            {
                float along = i / (float)FlagPieces;
                // Am Mast fest, zum freien Ende hin schlägt das Tuch stärker. Bei wenig Wind hängt es durch.
                float wave = Mathf.Sin(_time * 6f - along * 5f) * 2.4f * along * wind;
                float droop = (1f - Mathf.Min(1f, wind)) * along * along * 8f;
                return top + new Vector2(along * FlagLength, down + wave + droop);
            }
            for (int i = 0; i < FlagPieces; i++)
            {
                layer.DrawColoredPolygon(new[] { Edge(i, 0f), Edge(i + 1, 0f), Edge(i + 1, FlagHeight), Edge(i, FlagHeight) }, red);
                layer.DrawColoredPolygon(new[] { Edge(i, FlagHeight * 0.4f), Edge(i + 1, FlagHeight * 0.4f), Edge(i + 1, FlagHeight * 0.6f), Edge(i, FlagHeight * 0.6f) }, dark);
            }
        }
    }
}
