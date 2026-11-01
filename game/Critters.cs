using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Was sonst noch in der Stadt lebt: Ratten an den Hauswänden, Tauben auf Dächern und ein rauchender Arbeiter.
    /// Sie fliehen vor Schüssen und Menschen, beeinflussen das Spiel aber nicht. Reine Darstellung mit eigenem Zufall.
    /// </summary>
    public partial class Critters : Node2D
    {
        private const int CellW = 16;
        private const int CellH = 12;
        private const int RatRow = 0;
        private const int PigeonRow = 1;
        private const int RatCount = 7;
        private const float RatSpeed = 3.2f;
        private const float RatFleeSpeed = 5.5f;
        private const float RatSpookTiles = 1.8f;
        private const float RatSearchTiles = 3.5f;
        private const float RatMinLight = 0.34f;
        private const float PigeonMinLight = 0.42f;
        private const float PigeonSpookTiles = 2.5f;
        private const float PigeonFlightSeconds = 4.5f;
        private const float PigeonLandSeconds = 3f;
        private const float ShotHeardTiles = 9f;
        private const float WorkerSpookTiles = 1.6f;
        private const float WorkerWalkSpeed = 1.1f;
        private const float WorkerHomeSeconds = 45f;
        private const float DragPeriodSeconds = 6.5f;
        private const float DragSeconds = 1.3f;
        private const int FlyingZ = 3500;

        // Sitzplätze der Tauben: Feld x, y und Höhe in Bildpixeln, jeweils eine kleine Gruppe.
        private static readonly (float X, float Y, float Z)[] Perches =
        {
            (5.95f, 2.6f, 128), (5.95f, 3.2f, 128), (5.95f, 3.7f, 128), (5.95f, 4.4f, 128),
            (2.6f, 16.95f, 170), (3.3f, 16.95f, 170), (3.9f, 16.95f, 170),
            (13f, 17.95f, 70), (13.7f, 17.95f, 70), (14.3f, 17.95f, 70), (16.2f, 17.95f, 70),
            (17.95f, 3f, 110), (17.95f, 3.7f, 110), (17.95f, 4.5f, 110),
        };

        private sealed class Rat
        {
            public Sprite2D Sprite = null!;
            public float X, Y, TargetX, TargetY;
            public float Wait;
            public bool Fleeing;
            public float Stride;
        }

        private sealed class Pigeon
        {
            public Sprite2D Sprite = null!;
            public float X, Y, Z;
            public Vector2 Seat;
            public Vector2 Away;
            public float Airborne = -1f;
            public float BackIn;
            public float Peck;
        }

        private readonly Random _rng = new();
        private readonly List<Rat> _rats = new();
        private readonly List<Pigeon> _pigeons = new();
        private readonly List<(int X, int Y)> _wallTiles = new();
        private Sprite2D _worker = null!;
        private Sprite2D _ember = null!;
        private CpuParticles2D _workerSmoke = null!;
        private PointLight2D _emberLight = null!;
        private float _workerX, _workerY;
        private float _workerAway = -1f;
        private bool _workerLeaving;
        private World? _seenWorld;
        private int _lastShotTick;
        private float _time;

        public Main Game = null!;
        public Texture2D Sheet = null!;
        public Texture2D Glow = null!;
        public List<Vector2> Gullies = new();
        public Vector2 WorkerSpot;
        public Vector2 WorkerDoor;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        private Sprite2D NewSprite(Texture2D texture, int w, int h)
        {
            var sprite = new Sprite2D { Texture = texture, RegionEnabled = true, RegionRect = new Rect2(0, 0, w, h), Centered = false };
            AddChild(sprite);
            return sprite;
        }

        public override void _Ready()
        {
            var map = Game.World.Map;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    bool nearWall = map.IsBlocked(x + 1, y) || map.IsBlocked(x - 1, y) || map.IsBlocked(x, y + 1) || map.IsBlocked(x, y - 1);
                    if (!map.IsBlocked(x, y) && nearWall)
                    {
                        _wallTiles.Add((x, y));
                    }
                }
            }
            for (int i = 0; i < RatCount; i++)
            {
                var (tx, ty) = _wallTiles[_rng.Next(_wallTiles.Count)];
                var rat = new Rat { Sprite = NewSprite(Sheet, CellW, CellH), X = tx + 0.5f, Y = ty + 0.5f, Wait = Rand(0f, 5f) };
                rat.TargetX = rat.X;
                rat.TargetY = rat.Y;
                _rats.Add(rat);
            }
            foreach (var (x, y, z) in Perches)
            {
                var seat = Game.ToScreen((int)(x * World.TileUnits), (int)(y * World.TileUnits)) - new Vector2(0, z);
                var pigeon = new Pigeon
                {
                    Sprite = NewSprite(Sheet, CellW, CellH), X = x, Y = y, Z = z, Seat = seat, Peck = Rand(0f, 6f),
                };
                pigeon.Sprite.FlipH = _rng.NextDouble() < 0.5;
                pigeon.Sprite.ZIndex = Game.PerchZ(x, y, z);
                _pigeons.Add(pigeon);
            }
            AddWorker();
        }

        private void AddWorker()
        {
            var (figures, region) = Game.FigureFrame("Arbeiter", 0, Main.IdleFirstColumn);
            _worker = NewSprite(figures, (int)region.Size.X, (int)region.Size.Y);
            _worker.RegionRect = region;
            _workerX = WorkerSpot.X;
            _workerY = WorkerSpot.Y;
            var dot = Image.CreateEmpty(2, 1, false, Image.Format.Rgba8);
            dot.Fill(Colors.White);
            dot.SetPixel(1, 0, new Color(1f, 1f, 1f, 0f));
            _ember = new Sprite2D { Texture = ImageTexture.CreateFromImage(dot), Centered = false };
            AddChild(_ember);
            _emberLight = new PointLight2D
            {
                Texture = Glow, TextureScale = 0.045f, Color = new Color(1f, 0.45f, 0.15f),
                RangeZMin = -1, RangeZMax = 4096,
            };
            AddChild(_emberLight);
            var puff = new GradientTexture2D
            {
                Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0f) } },
                Width = 8, Height = 8, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
            _workerSmoke = new CpuParticles2D
            {
                Texture = puff, TextureFilter = TextureFilterEnum.Nearest, Amount = 5, Lifetime = 3f, Preprocess = 3f,
                Direction = new Vector2(0.3f, -1f), Spread = 12f, Gravity = new Vector2(2f, -2f),
                InitialVelocityMin = 3f, InitialVelocityMax = 6f,
                ScaleAmountMin = 0.5f, ScaleAmountMax = 1.1f, Color = new Color(1.8f, 1.8f, 1.9f, 0.16f),
            };
            AddChild(_workerSmoke);
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            var world = Game.World;
            if (world != _seenWorld)
            {
                _seenWorld = world;
                _lastShotTick = 0;
                ResetWorker();
            }
            Vector2? shot = null;
            if (world.Shots.Count > 0 && world.Shots[^1].Tick > _lastShotTick)
            {
                _lastShotTick = world.Shots[^1].Tick;
                shot = new Vector2(world.Shots[^1].FromX, world.Shots[^1].FromY) / World.TileUnits;
            }
            foreach (var rat in _rats)
            {
                UpdateRat(rat, world, shot, dt);
            }
            foreach (var pigeon in _pigeons)
            {
                UpdatePigeon(pigeon, world, shot, dt);
            }
            UpdateWorker(world, shot, dt);
        }

        private static bool Near(World world, float x, float y, float tiles)
        {
            foreach (var u in world.Units)
            {
                if (u.Alive && Mathf.Abs(u.X / (float)World.TileUnits - x) < tiles && Mathf.Abs(u.Y / (float)World.TileUnits - y) < tiles)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateRat(Rat rat, World world, Vector2? shot, float dt)
        {
            bool spooked = Near(world, rat.X, rat.Y, RatSpookTiles)
                || (shot is { } s && new Vector2(rat.X, rat.Y).DistanceTo(s) < ShotHeardTiles);
            if (!rat.Sprite.Visible)
            {
                // Im Gully: Taucht später an einem anderen Gully wieder auf.
                rat.Wait -= dt;
                if (rat.Wait <= 0f && Gullies.Count > 0)
                {
                    var (x, y) = Gullies[_rng.Next(Gullies.Count)] is var g ? (g.X, g.Y) : default;
                    if (!Near(world, x, y, RatSpookTiles * 2f))
                    {
                        rat.X = rat.TargetX = x;
                        rat.Y = rat.TargetY = y;
                        rat.Fleeing = false;
                        rat.Sprite.Visible = true;
                        rat.Wait = Rand(0.5f, 2f);
                    }
                }
                return;
            }
            if (spooked && !rat.Fleeing && Gullies.Count > 0)
            {
                var pos = new Vector2(rat.X, rat.Y);
                var best = Gullies[0];
                foreach (var g in Gullies)
                {
                    if (pos.DistanceTo(g) < pos.DistanceTo(best))
                    {
                        best = g;
                    }
                }
                rat.TargetX = best.X;
                rat.TargetY = best.Y;
                rat.Fleeing = true;
                rat.Wait = 0f;
            }
            var to = new Vector2(rat.TargetX - rat.X, rat.TargetY - rat.Y);
            float speed = rat.Fleeing ? RatFleeSpeed : RatSpeed;
            bool running = rat.Wait <= 0f && to.Length() > 0.03f;
            if (running)
            {
                var step = to.LimitLength(speed * dt);
                rat.X += step.X;
                rat.Y += step.Y;
                rat.Stride += dt * 18f;
                // Im Bild läuft die Ratte nach rechts, wenn x wächst oder y fällt.
                rat.Sprite.FlipH = to.X - to.Y < 0f;
            }
            else if (rat.Fleeing)
            {
                rat.Sprite.Visible = false;
                rat.Wait = Rand(14f, 30f);
                return;
            }
            else if (rat.Wait > 0f)
            {
                rat.Wait -= dt;
            }
            else
            {
                PickRatTarget(rat, world);
                rat.Wait = Rand(0.6f, 4f);
            }
            var foot = Game.ToScreen((int)(rat.X * World.TileUnits), (int)(rat.Y * World.TileUnits));
            rat.Sprite.Position = foot - new Vector2(CellW / 2, CellH - 1);
            rat.Sprite.ZIndex = Game.DepthZ(rat.X, rat.Y);
            rat.Sprite.Modulate = Game.FigureLight((int)(rat.X * World.TileUnits), (int)(rat.Y * World.TileUnits), RatMinLight);
            int column = running ? (int)rat.Stride % 2 : 2;
            rat.Sprite.RegionRect = new Rect2(column * CellW, RatRow * CellH, CellW, CellH);
        }

        private void PickRatTarget(Rat rat, World world)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                var (x, y) = _wallTiles[_rng.Next(_wallTiles.Count)];
                float tx = x + Rand(0.15f, 0.85f), ty = y + Rand(0.15f, 0.85f);
                if (new Vector2(tx - rat.X, ty - rat.Y).Length() < RatSearchTiles
                    && world.Map.HasLineOfSight((int)rat.X, (int)rat.Y, x, y))
                {
                    rat.TargetX = tx;
                    rat.TargetY = ty;
                    return;
                }
            }
        }

        private void UpdatePigeon(Pigeon p, World world, Vector2? shot, float dt)
        {
            var light = Game.FigureLight((int)(Mathf.Min(p.X, 19.9f) * World.TileUnits), (int)(Mathf.Min(p.Y, 19.9f) * World.TileUnits), PigeonMinLight);
            if (p.Airborne < 0f)
            {
                bool spooked = (shot is { } s && new Vector2(p.X, p.Y).DistanceTo(s) < ShotHeardTiles)
                    || (p.BackIn <= 0f && Near(world, p.X, p.Y, PigeonSpookTiles));
                p.BackIn = Mathf.Max(0f, p.BackIn - dt);
                if (spooked)
                {
                    p.Airborne = 0f;
                    p.Away = new Vector2(Rand(-240f, 240f), Rand(-300f, -200f));
                    p.Sprite.FlipH = p.Away.X < 0f;
                    p.Sprite.ZIndex = FlyingZ;
                }
                p.Peck -= dt;
                if (p.Peck < -0.35f)
                {
                    p.Peck = Rand(2f, 9f);
                }
                p.Sprite.Position = p.Seat - new Vector2(CellW / 2, CellH - 1);
                p.Sprite.RegionRect = new Rect2((p.Peck < 0f ? 1 : 0) * CellW, PigeonRow * CellH, CellW, CellH);
                p.Sprite.Modulate = light;
                p.Sprite.Visible = true;
                return;
            }
            p.Airborne += dt;
            float total = PigeonFlightSeconds + p.BackIn;
            float t;
            if (p.Airborne < PigeonFlightSeconds)
            {
                t = p.Airborne / PigeonFlightSeconds;
                p.Sprite.Visible = true;
            }
            else if (p.Airborne < PigeonFlightSeconds + WaitAway)
            {
                p.Sprite.Visible = false;
                return;
            }
            else
            {
                t = 1f - (p.Airborne - PigeonFlightSeconds - WaitAway) / PigeonLandSeconds;
                p.Sprite.Visible = true;
                p.Sprite.FlipH = p.Away.X > 0f;
                if (t <= 0f)
                {
                    p.Airborne = -1f;
                    p.BackIn = Rand(6f, 12f);
                    p.Sprite.ZIndex = Game.PerchZ(p.X, p.Y, p.Z);
                    return;
                }
            }
            float flap = Mathf.Sin(_time * 22f + p.Seat.X);
            p.Sprite.Position = p.Seat + p.Away * t - new Vector2(CellW / 2, CellH - 1) + new Vector2(0, flap * 1.5f);
            p.Sprite.RegionRect = new Rect2((flap > 0f ? 2 : 3) * CellW, PigeonRow * CellH, CellW, CellH);
            p.Sprite.Modulate = light;
        }

        private const float WaitAway = 22f;

        private void ResetWorker()
        {
            _workerX = WorkerSpot.X;
            _workerY = WorkerSpot.Y;
            _workerLeaving = false;
            _workerAway = -1f;
            ShowWorker(true);
        }

        private void ShowWorker(bool show)
        {
            _worker.Visible = show;
            _ember.Visible = show;
            _emberLight.Visible = show;
            _workerSmoke.Visible = show;
        }

        private void UpdateWorker(World world, Vector2? shot, float dt)
        {
            if (_workerAway >= 0f)
            {
                _workerAway += dt;
                if (_workerAway > WorkerHomeSeconds && !Near(world, WorkerSpot.X, WorkerSpot.Y, WorkerSpookTiles * 2f))
                {
                    ResetWorker();
                }
                return;
            }
            if (!_workerLeaving && (Near(world, _workerX, _workerY, WorkerSpookTiles)
                || (shot is { } s && new Vector2(_workerX, _workerY).DistanceTo(s) < ShotHeardTiles)))
            {
                _workerLeaving = true;
            }
            // Spalten im Figurenblatt: Stehen mit leichtem Atmen, beim Gehen zur Tür der Blick nach Westen.
            var region = Game.FigureFrame("Arbeiter", 0, Main.IdleFirstColumn + (int)(_time * 1.6f) % 4).Region;
            if (_workerLeaving)
            {
                var to = WorkerDoor - new Vector2(_workerX, _workerY);
                if (to.Length() < 0.05f)
                {
                    _workerAway = 0f;
                    ShowWorker(false);
                    return;
                }
                var step = to.LimitLength(WorkerWalkSpeed * dt);
                _workerX += step.X;
                _workerY += step.Y;
                region = Game.FigureFrame("Arbeiter", 2, Main.WalkFirstColumn + (int)(_time * 14f) % 8).Region;
            }
            _worker.RegionRect = region;
            _worker.FlipH = _workerLeaving;
            int ux = (int)(_workerX * World.TileUnits), uy = (int)(_workerY * World.TileUnits);
            var foot = Game.ToScreen(ux, uy);
            _worker.Position = foot - Game.FigureFoot;
            int z = Game.DepthZ(_workerX, _workerY);
            _worker.ZIndex = z;
            _worker.Modulate = Game.FigureLight(ux, uy, RatMinLight + 0.1f);
            // Glut an der Zigarette: Beim Zug wird sie hell, und etwas Rauch steigt auf.
            float phase = _time % DragPeriodSeconds;
            float drag = phase < DragSeconds ? Mathf.Sin(phase / DragSeconds * Mathf.Pi) : 0f;
            var mouth = foot + new Vector2(0, -35);
            _ember.Position = mouth;
            _ember.ZIndex = z + 1;
            float glow = 0.55f + 0.45f * drag;
            _ember.Modulate = new Color(3f * glow, (1f + drag * 1.2f) * glow, (0.3f + drag * 0.5f) * glow);
            _emberLight.Position = mouth;
            _emberLight.Energy = 0.1f + 0.45f * drag;
            _workerSmoke.Position = mouth;
            _workerSmoke.ZIndex = z + 1;
            _workerSmoke.Visible = !_workerLeaving;
        }
    }
}
