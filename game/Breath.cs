using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Kälte, die man sieht: Alle Figuren atmen Wölkchen aus, nach dem Rennen schneller. Wachen im Dunkeln stecken sich
    /// manchmal eine Zigarette an, das verrät, wo sie wirklich stehen. Reine Darstellung, ihre Aufmerksamkeit bleibt gleich.
    /// </summary>
    public partial class Breath : Node2D
    {
        private const float MouthHeight = 30f;
        private const float MouthForward = 4f;
        private const float RestSeconds = 3.4f;
        private const float WalkSeconds = 2.4f;
        private const float WindedSeconds = 1f;
        private const float WindedAfterSprint = 6f;
        private const float PuffSeconds = 1.25f;
        private const float StartSize = 3f;
        private const float EndSize = 9f;
        private const float PuffAlpha = 0.9f;
        private const float MinLight = 0.75f;
        private const float MinSmokePause = 15f;
        private const float MaxSmokePause = 60f;
        private const float MinSmokeSeconds = 12f;
        private const float MaxSmokeSeconds = 22f;
        private const float FlareSeconds = 0.6f;
        private const float DragPeriodSeconds = 4.5f;
        private const float DragSeconds = 1f;
        private const float HeldSmokeStart = 2.5f;
        private const float ViewMargin = 60f;

        private sealed class Puff
        {
            public Vector2 Pos;
            public Vector2 Velocity;
            public float Age;
            public Color Light;
        }

        /// <summary>Eine Figur mit ihrem Atem. Eigene Tiefe, damit Häuser davor auch den Atem verdecken.</summary>
        private sealed partial class Mouth : Node2D
        {
            private readonly List<Puff> _puffs = new();
            private readonly Random _rng = new();
            private Sprite2D _flare = null!;
            private float _nextBreath;
            private float _winded;
            private float _nextSmoke;
            private float _smokeLeft;
            private float _flareLeft;
            private float _time;
            private Vector2 _mouth;
            private bool _drewLast;

            public Main Game = null!;
            public Unit Unit = null!;
            public Texture2D Disc = null!;
            public Texture2D Glow = null!;
            public bool HeldSmoke;

            private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

            public override void _Ready()
            {
                LightMask = 0;
                _nextBreath = Rand(0f, RestSeconds);
                _nextSmoke = HeldSmoke ? HeldSmokeStart : Rand(MinSmokePause / 2f, MaxSmokePause);
                _flare = new Sprite2D
                {
                    Texture = Glow, Visible = false, LightMask = 0,
                    Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
                };
                AddChild(_flare);
            }

            public override void _Process(double delta)
            {
                float dt = (float)delta;
                _time += dt;
                var foot = Game.UnitPos(Unit);
                var tile = Game.ScreenToMap(foot);
                ZIndex = Game.DepthZ(tile.X, tile.Y) + 1;
                float across = Unit.FacingX - Unit.FacingY, toward = Unit.FacingX + Unit.FacingY;
                var facing = new Vector2(across, toward * 0.5f).Normalized();
                _mouth = foot + new Vector2(0f, -MouthHeight) + facing * MouthForward;
                if (Unit.Sprinting)
                {
                    _winded = WindedAfterSprint;
                }
                _winded = Mathf.Max(0f, _winded - dt);
                _nextBreath -= dt;
                if (_nextBreath <= 0f)
                {
                    _nextBreath = (_winded > 0f ? WindedSeconds : Unit.Moving ? WalkSeconds : RestSeconds) * Rand(0.8f, 1.2f);
                    if (InView(foot))
                    {
                        _puffs.Add(new Puff
                        {
                            Pos = _mouth, Velocity = facing * Rand(5f, 9f) + new Vector2(0f, -3f),
                            Light = Game.FigureLight(Units(tile.X), Units(tile.Y), MinLight),
                        });
                    }
                }
                float wind = Game.Climate.Wind * 0.5f;
                foreach (var p in _puffs)
                {
                    p.Age += dt;
                    p.Pos += (p.Velocity + new Vector2(wind, -3f)) * dt;
                }
                _puffs.RemoveAll(p => p.Age >= PuffSeconds);
                UpdateSmoking(dt);
                // Ohne Wölkchen und Glut gibt es nichts zu zeichnen, das spart bei vielen Figuren am meisten.
                if (_puffs.Count > 0 || _smokeLeft > 0f || _drewLast)
                {
                    _drewLast = _puffs.Count > 0 || _smokeLeft > 0f;
                    QueueRedraw();
                }
            }

            private bool InView(Vector2 at)
            {
                var canvas = GetCanvasTransform();
                var origin = canvas.AffineInverse() * Vector2.Zero;
                var size = GetViewportRect().Size / canvas.Scale;
                return new Rect2(origin, size).Grow(ViewMargin).HasPoint(at);
            }

            private void UpdateSmoking(float dt)
            {
                bool calm = Unit.Faction == Faction.Guard && !Unit.Bribed && Unit.GuardState == GuardState.Patrol;
                if (!calm)
                {
                    _smokeLeft = 0f;
                    _flareLeft = 0f;
                }
                else if (_smokeLeft > 0f)
                {
                    _smokeLeft -= dt;
                }
                else
                {
                    _nextSmoke -= dt;
                    bool dark = Game.World.LightLevelAt(Unit.X, Unit.Y) <= World.DarkLevel;
                    if (_nextSmoke <= 0f && (dark || HeldSmoke))
                    {
                        _smokeLeft = Rand(MinSmokeSeconds, MaxSmokeSeconds);
                        _flareLeft = FlareSeconds;
                        Game.PlayScenery("match", Game.UnitPos(Unit), -14f, 260f, 0.08f);
                        _nextSmoke = Rand(MinSmokePause, MaxSmokePause);
                    }
                }
                _flareLeft = Mathf.Max(0f, _flareLeft - dt);
                float phase = _time % DragPeriodSeconds;
                float drag = phase < DragSeconds ? Mathf.Sin(phase / DragSeconds * Mathf.Pi) : 0f;
                float flare = _flareLeft / FlareSeconds;
                var night = Game.NightColor;
                _flare.Visible = _smokeLeft > 0f;
                _flare.Position = _mouth;
                _flare.Scale = Vector2.One * (0.022f + 0.012f * drag + 0.13f * flare);
                _flare.Modulate = new Color(1f / night.R, (0.5f + 0.35f * flare) / night.G, (0.16f + 0.4f * flare) / night.B, 0.55f + 0.2f * drag + 0.25f * flare);
            }

            public override void _Draw()
            {
                foreach (var p in _puffs)
                {
                    float f = p.Age / PuffSeconds;
                    float size = Mathf.Lerp(StartSize, EndSize, f);
                    DrawTextureRect(Disc, new Rect2(ToLocal(p.Pos) - new Vector2(size, size), new Vector2(size * 2f, size * 2f)), false,
                        new Color(p.Light.R, p.Light.G, p.Light.B, PuffAlpha * (1f - f)));
                }
                if (_smokeLeft > 0f)
                {
                    DrawRect(new Rect2(ToLocal(_mouth), new Vector2(1f, 1f)), new Color(3.2f, 1.2f, 0.4f));
                }
            }

            private static int Units(float tile) => (int)(Mathf.Clamp(tile, 0.1f, 19.9f) * World.TileUnits);
        }

        private readonly Dictionary<int, Mouth> _mouths = new();
        private World? _seenWorld;
        private Texture2D _disc = null!;

        public Main Game = null!;
        public Texture2D Glow = null!;

        /// <summary>Prüflauf: Alle ruhigen Wachen rauchen kurz nach dem Start, auch im Licht.</summary>
        public bool HeldSmoke;

        public override void _Ready()
        {
            _disc = GunSmoke.MakeDisc();
        }

        public override void _Process(double delta)
        {
            var world = Game.World;
            if (world != _seenWorld)
            {
                _seenWorld = world;
                foreach (var mouth in _mouths.Values)
                {
                    mouth.QueueFree();
                }
                _mouths.Clear();
            }
            foreach (var u in world.Units)
            {
                bool known = _mouths.TryGetValue(u.Id, out var mouth);
                if (u.Alive && !known)
                {
                    var added = new Mouth { Game = Game, Unit = u, Disc = _disc, Glow = Glow, HeldSmoke = HeldSmoke };
                    AddChild(added);
                    _mouths[u.Id] = added;
                }
                else if (!u.Alive && known)
                {
                    mouth!.QueueFree();
                    _mouths.Remove(u.Id);
                }
            }
        }
    }
}
