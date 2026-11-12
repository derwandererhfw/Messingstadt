using System;
using Godot;

namespace Messingstadt
{
    /// <summary>Regen über dem sichtbaren Bildausschnitt. Stärke und Schräge kommen vom Wetter. Reine Darstellung.</summary>
    public partial class RainLayer : Node2D
    {
        private const int MaxDrops = 420;
        private const float DrizzleSpeed = 300f;
        private const float ShowerSpeed = 400f;
        private const float WindDrift = 2.2f;
        private const float BaseDrift = 22f;
        private const float DrizzleLength = 7f;
        private const float ShowerLength = 12f;

        private readonly Random _rng = new();
        private readonly Vector2[] _drops = new Vector2[MaxDrops];
        private Vector2[] _lines = Array.Empty<Vector2>();
        private int _active;

        public Main Game = null!;

        public override void _Ready()
        {
            for (int i = 0; i < MaxDrops; i++)
            {
                _drops[i] = new Vector2((float)_rng.NextDouble(), (float)_rng.NextDouble());
            }
        }

        public override void _Process(double delta)
        {
            Visible = !AirTest.Off("regen");
            var size = GetViewSize();
            float rain = Game.Climate.RainStrength;
            _active = (int)(MaxDrops * rain);
            var velocity = new Vector2(BaseDrift + Game.Climate.Wind * WindDrift, Mathf.Lerp(DrizzleSpeed, ShowerSpeed, rain));
            var step = new Vector2(velocity.X / size.X, velocity.Y / size.Y) * (float)delta;
            for (int i = 0; i < _active; i++)
            {
                _drops[i] += step;
                if (_drops[i].Y > 1f || _drops[i].X > 1f)
                {
                    // Bei Wind kommen die Tropfen auch von der Seite ins Bild.
                    bool fromSide = _rng.NextDouble() < step.X / (step.X + step.Y);
                    _drops[i] = fromSide ? new Vector2(0f, (float)_rng.NextDouble()) : new Vector2((float)_rng.NextDouble(), 0f);
                }
            }
            if (_lines.Length != _active * 2)
            {
                _lines = new Vector2[_active * 2];
            }
            var origin = GetCanvasTransform().AffineInverse() * Vector2.Zero;
            var tail = velocity.Normalized() * Mathf.Lerp(DrizzleLength, ShowerLength, rain);
            for (int i = 0; i < _active; i++)
            {
                var p = origin + _drops[i] * size;
                _lines[i * 2] = p;
                _lines[i * 2 + 1] = p + tail;
            }
            QueueRedraw();
        }

        private Vector2 GetViewSize()
        {
            var scale = GetCanvasTransform().Scale;
            return GetViewportRect().Size / scale;
        }

        public override void _Draw()
        {
            if (_lines.Length > 0)
            {
                // Halb so breit wie ein einfacher Bildpunkt, so fein wie die Linien der Stadt in doppelter Auflösung.
                DrawMultiline(_lines, new Color(0.8f, 0.86f, 1f, 0.62f), 0.7f);
            }
        }
    }
}
