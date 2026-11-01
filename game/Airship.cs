using System;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Luftschiff der Brandt-Werke: zieht über die Stadt, sein Suchscheinwerfer streift durch die Straßen.
    /// Bisher reine Stimmung, die Wachen wissen nichts davon. Eigener Zufall, keine Spielregeln.
    /// </summary>
    public partial class Airship : Node2D
    {
        private const float Altitude = 175f;
        private const float StartX = -13f;
        private const float EndX = 34f;
        private const float TilesPerSecond = 0.42f;
        private const float FirstPassSeconds = 12f;
        private const float MinPause = 45f;
        private const float MaxPause = 110f;
        private const float SweepTiles = 3.2f;
        private const float BeamAlpha = 0.13f;
        private const float PoolEnergy = 0.75f;
        private const int FlyZ = 3600;
        private const int BeamZ = 3450;
        private static readonly Vector2 GondolaOffset = new(0, 40);

        private readonly Random _rng = new();
        private Sprite2D _ship = null!;
        private Sprite2D _shade = null!;
        private Sprite2D _pool = null!;
        private Polygon2D _beam = null!;
        private Sprite2D _blink = null!;
        private AudioStreamPlayer2D _drone = null!;
        private float _x = StartX;
        private float _y;
        private float _wait = FirstPassSeconds;
        private float _time;

        public Main Game = null!;
        public Texture2D ShipTexture = null!;
        public Texture2D Glow = null!;
        public Color Night;

        /// <summary>Prüflauf: hält das Schiff über diesem Feld.</summary>
        public Vector2? HeldAt;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        public override void _Ready()
        {
            var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            var multiply = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Sub };
            _shade = new Sprite2D { Texture = Glow, ZIndex = BeamZ - 1, Material = multiply, Scale = new Vector2(1.1f, 0.5f), Modulate = new Color(0.1f, 0.1f, 0.1f) };
            AddChild(_shade);
            var white = new Color(0.9f / Night.R, 0.95f / Night.G, 1f / Night.B);
            _pool = new Sprite2D { Texture = Glow, ZIndex = BeamZ, Material = additive, Scale = new Vector2(0.5f, 0.25f), Modulate = new Color(white, PoolEnergy) };
            AddChild(_pool);
            _beam = new Polygon2D
            {
                ZIndex = BeamZ, Material = additive,
                Polygon = new Vector2[3],
                VertexColors = new[] { new Color(white, BeamAlpha), new Color(white, BeamAlpha * 0.15f), new Color(white, BeamAlpha * 0.15f) },
            };
            AddChild(_beam);
            _ship = new Sprite2D { Texture = ShipTexture, ZIndex = FlyZ, Modulate = new Color(1.25f, 1.25f, 1.35f) };
            AddChild(_ship);
            var dot = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
            dot.Fill(Colors.White);
            _blink = new Sprite2D { Texture = ImageTexture.CreateFromImage(dot), ZIndex = FlyZ + 1 };
            AddChild(_blink);
            _drone = Game.Sound.AddMovingLoop("airship", -11f, 1500f);
            ShowShip(false);
        }

        private void ShowShip(bool show)
        {
            _ship.Visible = show;
            _shade.Visible = show;
            _pool.Visible = show;
            _beam.Visible = show;
            _blink.Visible = show;
            if (show != _drone.Playing)
            {
                if (show)
                {
                    _drone.Play();
                }
                else
                {
                    _drone.Stop();
                }
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            if (HeldAt is { } held)
            {
                _x = held.X;
                _y = held.Y;
                _wait = 0f;
                ShowShip(true);
            }
            if (_wait > 0f)
            {
                _wait -= dt;
                if (_wait <= 0f)
                {
                    _x = StartX;
                    _y = Rand(3f, 15f);
                    ShowShip(true);
                }
                return;
            }
            _x += HeldAt == null ? TilesPerSecond * dt : 0f;
            if (_x > EndX)
            {
                _wait = Rand(MinPause, MaxPause);
                ShowShip(false);
                return;
            }
            var ground = Tile(_x, _y);
            _ship.Position = ground - new Vector2(0, Altitude);
            _shade.Position = ground;
            _drone.Position = _ship.Position;
            // Der Scheinwerfer sucht in einer langsamen Schleife unter dem Schiff.
            float sx = _x + Mathf.Sin(_time * 0.37f) * SweepTiles;
            float sy = _y + Mathf.Sin(_time * 0.23f + 1f) * SweepTiles;
            var pool = Tile(sx, sy);
            _pool.Position = pool;
            var from = _ship.Position + GondolaOffset;
            var across = new Vector2(46f, 0f);
            _beam.Polygon = new[] { from, pool - across, pool + across };
            bool lit = _time % 2f < 0.12f;
            _blink.Position = _ship.Position + new Vector2(-96, 22);
            _blink.Modulate = lit ? new Color(4f, 0.6f, 0.5f) : new Color(0.4f, 0.1f, 0.1f);
        }

        private Vector2 Tile(float x, float y) => Game.ToScreen((int)(x * World.TileUnits), (int)(y * World.TileUnits));
    }
}
