using System;
using System.Collections.Generic;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Der sichtbare Blitzstrahl: eine gezackte, verästelte Linie von oben durchs Bild.
    /// Jeder Schlag bekommt eine neue Form. Liegt in einer eigenen Ebene über der Stadt.
    /// </summary>
    public partial class LightningBolt : Node2D
    {
        private const int Subdivisions = 7;
        private const float Jaggedness = 0.17f;
        private const int MinBranches = 2;
        private const int MaxBranches = 5;

        private readonly Random _rng = new();
        private readonly List<Vector2[]> _trunks = new();
        private readonly List<Vector2[]> _branches = new();
        private float _strength;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        /// <summary>Linie zwischen zwei Punkten, immer wieder in der Mitte zur Seite versetzt: So entstehen die Zacken.</summary>
        private Vector2[] Zigzag(Vector2 from, Vector2 to, int rounds)
        {
            var points = new List<Vector2> { from, to };
            float spread = from.DistanceTo(to) * Jaggedness;
            for (int round = 0; round < rounds; round++)
            {
                var next = new List<Vector2>(points.Count * 2);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    var side = (b - a).Orthogonal().Normalized();
                    next.Add(a);
                    next.Add((a + b) / 2f + side * Rand(-spread, spread));
                }
                next.Add(points[^1]);
                points = next;
                spread *= 0.55f;
            }
            return points.ToArray();
        }

        /// <summary>Formt einen neuen Strahl. Er trifft irgendwo hinter der Stadt, nicht auf die Figuren.</summary>
        public void Strike()
        {
            _trunks.Clear();
            _branches.Clear();
            var view = GetViewportRect().Size;
            var top = new Vector2(Rand(0.15f, 0.85f) * view.X, -20f);
            var bottom = new Vector2(top.X + Rand(-0.18f, 0.18f) * view.X, Rand(0.38f, 0.62f) * view.Y);
            var trunk = Zigzag(top, bottom, Subdivisions);
            _trunks.Add(trunk);
            int branches = _rng.Next(MinBranches, MaxBranches + 1);
            for (int i = 0; i < branches; i++)
            {
                var start = trunk[_rng.Next(trunk.Length / 6, trunk.Length * 4 / 5)];
                float length = Rand(0.08f, 0.24f) * view.Y;
                var end = start + new Vector2(Rand(-1f, 1f) * length, Rand(0.4f, 1f) * length);
                _branches.Add(Zigzag(start, end, Subdivisions - 2));
            }
            QueueRedraw();
        }

        /// <summary>Helligkeit des Strahls gerade jetzt, von 0 bis 1.</summary>
        public void SetStrength(float strength)
        {
            if (!Mathf.IsEqualApprox(strength, _strength))
            {
                _strength = strength;
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            if (_strength <= 0.01f)
            {
                return;
            }
            foreach (var line in _branches)
            {
                DrawPolyline(line, new Color(0.55f, 0.65f, 1f, 0.16f * _strength), 3f);
                DrawPolyline(line, new Color(0.85f, 0.9f, 1f, 0.6f * _strength), 1f);
            }
            foreach (var line in _trunks)
            {
                // Weicher Schein außen, heißer Kern innen. Die Linie bleibt dünn, wie ein echter Blitz.
                DrawPolyline(line, new Color(0.45f, 0.55f, 1f, 0.13f * _strength), 9f);
                DrawPolyline(line, new Color(0.65f, 0.75f, 1f, 0.35f * _strength), 4f);
                DrawPolyline(line, new Color(0.97f, 0.98f, 1f, _strength), 1.6f);
            }
        }
    }
}
