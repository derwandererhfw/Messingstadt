using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Pulverdampf: Nach jedem Schuss hängt Rauch in der Luft, zieht mit dem Wind und leuchtet unter den Laternen auf.
    /// Im Schauer verfliegt er schneller. Reine Darstellung, die Wachen sehen durch ihn hindurch.
    /// </summary>
    public partial class GunSmoke : Node2D
    {
        private const int MaxPuffs = 70;
        private const int PuffsPerShot = 2;
        private const float PuffSeconds = 6.5f;
        private const float MuzzleHeight = 22f;
        private const float MuzzleForward = 7f;
        private const float StartSize = 6f;
        private const float EndSize = 28f;
        private const float PuffAlpha = 0.42f;
        private const float WindShare = 0.7f;
        private const float RiseSpeed = 4f;
        private const float RainAge = 1.2f;
        private const float MinLight = 0.32f;
        private const float LightShare = 0.9f;

        private sealed class Puff
        {
            public Vector2 Pos;
            public Vector2 Velocity;
            public float Age;
        }

        private readonly List<Puff> _puffs = new();
        private readonly RandomNumberGenerator _rng = new();
        private World? _seenWorld;
        private int _lastShotTick;
        private Texture2D _disc = null!;

        public Main Game = null!;

        public override void _Ready()
        {
            ZIndex = 3000;
            LightMask = 0;
            _disc = MakeDisc();
        }

        /// <summary>Weiche Scheibe, gemeinsam für Rauch und Atem.</summary>
        public static Texture2D MakeDisc()
        {
            const int S = 32;
            var image = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float d = new Vector2((x + 0.5f) / S * 2f - 1f, (y + 0.5f) / S * 2f - 1f).Length();
                    float a = Mathf.Clamp(1f - d, 0f, 1f);
                    image.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            return ImageTexture.CreateFromImage(image);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            var world = Game.World;
            if (world != _seenWorld)
            {
                _seenWorld = world;
                _lastShotTick = 0;
                _puffs.Clear();
            }
            int newest = _lastShotTick;
            for (int i = world.Shots.Count - 1; i >= 0 && world.Shots[i].Tick > _lastShotTick; i--)
            {
                var shot = world.Shots[i];
                newest = Mathf.Max(newest, shot.Tick);
                var from = Game.ToScreen(shot.FromX, shot.FromY);
                var aim = (Game.ToScreen(shot.ToX, shot.ToY) - from).Normalized();
                for (int p = 0; p < PuffsPerShot; p++)
                {
                    _puffs.Add(new Puff
                    {
                        Pos = from - new Vector2(0f, MuzzleHeight) + aim * MuzzleForward,
                        Velocity = aim * _rng.RandfRange(4f, 16f) + new Vector2(_rng.RandfRange(-3f, 3f), _rng.RandfRange(-3f, 3f)),
                    });
                }
            }
            _lastShotTick = newest;
            if (_puffs.Count > MaxPuffs)
            {
                _puffs.RemoveRange(0, _puffs.Count - MaxPuffs);
            }
            float wind = Game.Climate.Wind * WindShare;
            float aging = dt * (1f + Game.Climate.RainStrength * RainAge);
            foreach (var p in _puffs)
            {
                p.Age += aging;
                p.Pos += (p.Velocity + new Vector2(wind, -RiseSpeed)) * dt;
                p.Velocity *= 1f - Mathf.Min(1f, dt * 0.8f);
            }
            bool any = _puffs.Count > 0;
            _puffs.RemoveAll(p => p.Age >= PuffSeconds);
            if (any)
            {
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            foreach (var p in _puffs)
            {
                float f = p.Age / PuffSeconds;
                var tile = Game.ScreenToMap(p.Pos + new Vector2(0f, MuzzleHeight));
                var light = Game.FigureLight(Units(tile.X), Units(tile.Y), MinLight);
                float alpha = PuffAlpha * (1f - f) * Mathf.Min(1f, f * 8f + 0.3f);
                float size = Mathf.Lerp(StartSize, EndSize, Mathf.Sqrt(f));
                DrawTextureRect(_disc, new Rect2(p.Pos - new Vector2(size, size * 0.8f), new Vector2(size * 2f, size * 1.6f)), false,
                    new Color(light.R * LightShare, light.G * LightShare, light.B * LightShare, alpha));
            }
        }

        private static int Units(float tile) => (int)(Mathf.Clamp(tile, 0.1f, 19.9f) * World.TileUnits);
    }
}
