using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Spuren auf dem nassen Pflaster: Fußabdrücke, die langsam verschwinden, und Blutstropfen verletzter Figuren.
    /// Reine Darstellung, Wachen folgen den Spuren nicht.
    /// </summary>
    public partial class Traces : Node2D
    {
        private const int MaxMarks = 260;
        private const float FootprintSeconds = 8f;
        private const float BloodSeconds = 45f;
        private const float WoundedShare = 0.6f;
        private const float StepSide = 2.2f;
        private const float FootprintAlpha = 0.42f;

        private readonly struct Mark
        {
            public readonly Vector2 Pos;
            public readonly float Born;
            public readonly bool Blood;

            public Mark(Vector2 pos, float born, bool blood)
            {
                Pos = pos;
                Born = born;
                Blood = blood;
            }
        }

        private readonly List<Mark> _marks = new();
        private readonly Dictionary<int, int> _steps = new();
        private World? _world;
        private float _time;

        public Main Game = null!;

        public override void _Process(double delta)
        {
            _time += (float)delta;
            var world = Game.World;
            if (world != _world)
            {
                _world = world;
                _marks.Clear();
                _steps.Clear();
            }
            foreach (var u in world.Units)
            {
                if (u.Alive && u.Moving)
                {
                    AddSteps(u);
                }
            }
            _marks.RemoveAll(m => _time - m.Born > (m.Blood ? BloodSeconds : FootprintSeconds));
            if (_marks.Count > MaxMarks)
            {
                _marks.RemoveRange(0, _marks.Count - MaxMarks);
            }
            QueueRedraw();
        }

        private void AddSteps(Unit u)
        {
            int count = Game.StepsTaken(u);
            if (_steps.TryGetValue(u.Id, out int before) && before != count)
            {
                var pos = Game.ToScreen(u.X, u.Y);
                float across = u.FacingX - u.FacingY, toward = u.FacingX + u.FacingY;
                var side = new Vector2(toward, -across * 0.5f).Normalized() * (count % 2 == 0 ? StepSide : -StepSide);
                _marks.Add(new Mark(pos + side, _time, false));
                if (u.Hp < u.MaxHp * WoundedShare && count % 2 == 0)
                {
                    _marks.Add(new Mark(pos - side * 0.5f, _time, true));
                }
            }
            _steps[u.Id] = count;
        }

        public override void _Draw()
        {
            var shape = new Vector2[6];
            foreach (var m in _marks)
            {
                float life = (_time - m.Born) / (m.Blood ? BloodSeconds : FootprintSeconds);
                var color = m.Blood
                    ? new Color(0.62f, 0.04f, 0.03f, 0.85f * (1f - life))
                    : new Color(0f, 0f, 0f, FootprintAlpha * (1f - life));
                float w = m.Blood ? 1.6f : 2.4f, h = m.Blood ? 0.9f : 1.2f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.Tau;
                    shape[i] = m.Pos + new Vector2(Mathf.Cos(a) * w, Mathf.Sin(a) * h);
                }
                DrawColoredPolygon(shape, color);
            }
        }
    }
}
