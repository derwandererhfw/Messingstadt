using System;
using Godot;

namespace Messingstadt
{
    /// <summary>Nieselregen über dem sichtbaren Bildausschnitt. Reine Darstellung.</summary>
    public partial class RainLayer : Node2D
    {
        private const int Drops = 110;
        private const float FallSpeed = 300f;
        private const float DriftSpeed = 46f;
        private const float DropLength = 7f;

        private readonly Random _rng = new();
        private readonly Vector2[] _drops = new Vector2[Drops];

        public override void _Ready()
        {
            for (int i = 0; i < Drops; i++)
            {
                _drops[i] = new Vector2((float)_rng.NextDouble(), (float)_rng.NextDouble());
            }
        }

        public override void _Process(double delta)
        {
            var size = GetViewSize();
            var step = new Vector2(DriftSpeed / size.X, FallSpeed / size.Y) * (float)delta;
            for (int i = 0; i < Drops; i++)
            {
                _drops[i] += step;
                if (_drops[i].Y > 1f || _drops[i].X > 1f)
                {
                    _drops[i] = new Vector2((float)_rng.NextDouble() * 1.1f - 0.1f, 0f);
                }
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
            var origin = GetCanvasTransform().AffineInverse() * Vector2.Zero;
            var size = GetViewSize();
            var tail = new Vector2(DriftSpeed, FallSpeed).Normalized() * DropLength;
            var color = new Color(0.8f, 0.86f, 1f, 0.42f);
            foreach (var d in _drops)
            {
                var p = origin + d * size;
                DrawLine(p, p + tail, color, 1f);
            }
        }
    }
}
