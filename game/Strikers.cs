using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Streikposten vor dem Lager: drei Arbeiter wärmen sich an einer Feuertonne, das Feuer wirft zuckende
    /// Schatten. Bei Schüssen laufen sie davon, die Tonne brennt weiter. Reine Darstellung.
    /// </summary>
    public partial class Strikers : Node2D
    {
        private const float WalkAwaySpeed = 2.4f;
        private const float ShotHeardTiles = 10f;
        private const float ShadowTiles = 1.5f;
        private const float ShadowHalfWidth = 0.13f;
        private const float ShadowAlpha = 0.6f;
        private const float FlameLightScale = 0.55f;
        private const float FlameEnergy = 1.9f;

        private static readonly Vector2 BarrelTile = new(13.25f, 18.7f);
        private static readonly Vector2[] Spots = { new(12.65f, 18.55f), new(13.85f, 18.6f), new(13.4f, 19.3f) };
        private static readonly Vector2 BannerTile = new(12.35f, 18.02f);

        // Blick zur Tonne: Blatt-Zeile der Blickrichtung und ob gespiegelt wird.
        private static readonly (int View, bool Flip)[] Facing = { (2, false), (2, true), (4, false) };

        private sealed class Striker
        {
            public Sprite2D Sprite = null!;
            public Vector2 Tile;
            public bool Gone;
            public int Index;
        }

        private readonly Random _rng = new();
        private readonly List<Striker> _people = new();
        private PointLight2D _fire = null!;
        private World? _seenWorld;
        private int _lastShotTick;
        private bool _fleeing;
        private float _time;
        private Vector2 _flicker;

        public Main Game = null!;
        public Texture2D Glow = null!;
        public Texture2D BarrelTexture = null!;
        public Texture2D BannerTexture = null!;

        public override void _Ready()
        {
            var foot = Screen(BarrelTile);
            AddChild(new Sprite2D
            {
                Texture = BarrelTexture, Centered = false, Position = foot - new Vector2(10, 24),
                ZIndex = Game.DepthZ(BarrelTile.X, BarrelTile.Y), Modulate = new Color(0.55f / 0.34f, 0.5f / 0.37f, 0.5f / 0.55f),
            });
            AddChild(new Sprite2D
            {
                Texture = BannerTexture, Centered = false, Position = Screen(BannerTile) - new Vector2(0, BannerTexture.GetHeight() - 22),
                ZIndex = Game.DepthZ(BannerTile.X, BannerTile.Y), Modulate = new Color(0.8f, 0.76f, 0.72f),
            });
            _fire = new PointLight2D
            {
                Texture = Glow, TextureScale = FlameLightScale, Color = new Color(1f, 0.5f, 0.2f), Energy = FlameEnergy,
                RangeZMin = -1, RangeZMax = 4096, Position = foot - new Vector2(0, 26),
                // Nur auf Boden und Häuser: Figuren hellt das Gitter auf, hier würden sie grell orange.
                RangeItemCullMask = 2,
            };
            AddChild(_fire);
            var puff = new GradientTexture2D
            {
                Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) } },
                Width = 8, Height = 8, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
            AddChild(new CpuParticles2D
            {
                Texture = puff, TextureFilter = TextureFilterEnum.Nearest, Position = foot - new Vector2(0, 20),
                ZIndex = Game.DepthZ(BarrelTile.X, BarrelTile.Y) + 1,
                Amount = 16, Lifetime = 0.7f, Preprocess = 1f, EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = new Vector2(3.5f, 0.5f), Direction = Vector2.Up, Spread = 10f, Gravity = new Vector2(2f, -14f),
                InitialVelocityMin = 12f, InitialVelocityMax = 20f, ScaleAmountMin = 0.5f, ScaleAmountMax = 1f,
                ColorRamp = new Gradient
                {
                    Offsets = new[] { 0f, 0.5f, 1f },
                    Colors = new[] { new Color(3f, 2.2f, 0.9f, 0.95f), new Color(2.8f, 1f, 0.3f, 0.7f), new Color(1.2f, 0.4f, 0.2f, 0f) },
                },
            });
            Game.Sound.AddLoop("fire", foot, -19f, 240f);
            for (int i = 0; i < Spots.Length; i++)
            {
                var sprite = new Sprite2D { Texture = Game.FigureFrame("Arbeiter", 0, 0).Sheet, RegionEnabled = true, Centered = false };
                AddChild(sprite);
                var person = new Striker { Sprite = sprite, Tile = Spots[i], Index = i };
                _people.Add(person);
                Game.AddWorkerVoice(() => person.Gone ? null : Screen(person.Tile));
            }
        }

        private Vector2 Screen(Vector2 tile) => Game.ToScreen((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits));

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            var world = Game.World;
            if (world != _seenWorld)
            {
                _seenWorld = world;
                _lastShotTick = 0;
                _fleeing = false;
                foreach (var p in _people)
                {
                    p.Tile = Spots[p.Index];
                    p.Gone = false;
                    p.Sprite.Visible = true;
                }
            }
            if (world.Shots.Count > 0 && world.Shots[^1].Tick > _lastShotTick)
            {
                _lastShotTick = world.Shots[^1].Tick;
                var at = new Vector2(world.Shots[^1].FromX, world.Shots[^1].FromY) / World.TileUnits;
                _fleeing |= at.DistanceTo(BarrelTile) < ShotHeardTiles;
            }
            // Feuer zuckt: Helligkeit und Lage springen unruhig, damit auch die Schatten leben.
            float wobble = Mathf.Sin(_time * 11f) * Mathf.Sin(_time * 17f + 1f);
            _fire.Energy = FlameEnergy * (0.82f + 0.18f * wobble + 0.08f * (float)_rng.NextDouble());
            _flicker = new Vector2(Mathf.Sin(_time * 9f), Mathf.Cos(_time * 13f)) * 0.06f;
            foreach (var p in _people)
            {
                UpdatePerson(p, dt);
            }
            QueueRedraw();
        }

        private void UpdatePerson(Striker p, float dt)
        {
            if (p.Gone)
            {
                return;
            }
            var (view, flip) = Facing[p.Index];
            int column = Main.IdleFirstColumn + (int)(_time * 1.6f + p.Index * 1.7f) % 4;
            if (_fleeing)
            {
                // Weg nach Westen aus dem Bild, jeder ein Stück versetzt.
                var target = new Vector2(8.5f - p.Index, 19.3f);
                var to = target - p.Tile;
                if (to.Length() < 0.1f)
                {
                    p.Gone = true;
                    p.Sprite.Visible = false;
                    return;
                }
                p.Tile += to.LimitLength(WalkAwaySpeed * dt);
                view = 2;
                flip = true;
                column = Main.WalkFirstColumn + (int)(_time * 16f + p.Index) % 8;
            }
            p.Sprite.RegionRect = Game.FigureFrame("Arbeiter", view, column).Region;
            p.Sprite.FlipH = flip;
            int ux = (int)(p.Tile.X * World.TileUnits), uy = (int)(p.Tile.Y * World.TileUnits);
            p.Sprite.Position = Game.ToScreen(ux, uy) - Game.FigureFoot;
            p.Sprite.ZIndex = Game.DepthZ(p.Tile.X, p.Tile.Y);
            p.Sprite.Modulate = Game.FigureLight(ux, uy, 0.34f);
        }

        // Schatten vom Feuer weg auf den Boden. Liegt mit dem Knoten unter den Figuren.
        public override void _Draw()
        {
            foreach (var p in _people)
            {
                if (p.Gone)
                {
                    continue;
                }
                var away = p.Tile - BarrelTile - _flicker;
                var dir = away.Normalized();
                var side = new Vector2(-dir.Y, dir.X) * ShadowHalfWidth;
                var far = p.Tile + dir * ShadowTiles;
                var dark = new Color(0f, 0f, 0f, ShadowAlpha);
                var clear = new Color(0f, 0f, 0f, 0f);
                DrawPolygon(new[] { Screen(p.Tile + side), Screen(p.Tile - side), Screen(far - side * 1.6f), Screen(far + side * 1.6f) }, new[] { dark, dark, clear, clear });
            }
        }
    }
}
