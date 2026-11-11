using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Prüfhilfe mit --passanten: stellt alle Passanten mit ihren Farbvarianten neben den Trupp und spielt ihre
    /// Bewegungen der Reihe nach ab. Das Verhalten der Passanten im Spiel gehört nicht hierher.
    /// </summary>
    public partial class CivilPreview : Node2D
    {
        private static readonly string[] Motions = { "idle", "walk", "smoke", "read", "startle", "cower", "run" };
        private const float SecondsPerMotion = 1.5f;
        private const float FramesPerSecond = 6f;

        private readonly System.Collections.Generic.List<(Sprite2D Sprite, string Name, int View)> _shown = new();
        private float _time;

        public Main Game = null!;

        public override void _Ready()
        {
            ZIndex = 3500;
            var first = Game.World.Units[0];
            int column = 0;
            foreach (string name in Game.Civilians)
            {
                var variants = new System.Collections.Generic.List<string> { "" };
                variants.AddRange(Game.FigureVariants(name));
                for (int row = 0; row < variants.Count; row++)
                {
                    var sprite = Game.NewFigureSprite();
                    Game.SetFigureVariant(sprite, name, variants[row]);
                    int x = first.X + (column - 3) * 1100 + row * 900, y = first.Y + (column - 3) * -1100 + row * 900 + 1800;
                    sprite.Position = Game.ToScreen(x, y) - Game.FigureFoot;
                    sprite.Modulate = Game.FigureLight(x, y, 0.5f);
                    AddChild(sprite);
                    _shown.Add((sprite, name, 1 + row % 2));
                }
                column++;
            }
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            string motion = Motions[(int)(_time / SecondsPerMotion) % Motions.Length];
            foreach (var (sprite, name, view) in _shown)
            {
                sprite.RegionRect = Game.CivilFrame(name, view, motion, (int)(_time * FramesPerSecond));
            }
        }
    }
}
