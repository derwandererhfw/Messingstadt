using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Dunst mit der Entfernung: dünne Schleier quer durch die Stadt, jeder zwischen die Objekte einsortiert. Was hinter
    /// einem Schleier steht, verblasst ein wenig, was davor steht, bleibt klar. Je weiter hinten, desto mehr Schleier
    /// liegen davor. Am Boden steht der Dunst dichter als in der Höhe. Reine Darstellung, Sicht und Licht der Regeln
    /// bleiben unberührt.
    /// </summary>
    public partial class Haze : Node2D
    {
        // Linien gleicher Entfernung liegen in der Schrägansicht waagerecht: x + y = Tiefe.
        private const float FirstDepth = 7f;
        private const float DepthStep = 7.5f;
        private const int Sheets = 4;
        private const float SheetAlpha = 0.042f;
        private const float GroundMistAlpha = 0.04f;
        private const float RampPixels = 110f;
        private const float MistTopPixels = 190f;
        private const float RowPixels = 16f;
        private const float Left = -1000f;
        private const float Right = 2500f;
        private const float Top = -560f;
        private const float RainBoost = 0.2f;
        private static readonly Color HazeColor = new(0.3f, 0.36f, 0.56f);

        private readonly Sheet[] _sheets = new Sheet[Sheets];

        private sealed partial class Sheet : Node2D
        {
            public float[] Rows = System.Array.Empty<float>();
            public float[] Alphas = System.Array.Empty<float>();
            public Color Tint;

            public override void _Draw()
            {
                for (int r = 0; r + 1 < Rows.Length; r++)
                {
                    Color low = new(Tint, Alphas[r]), high = new(Tint, Alphas[r + 1]);
                    DrawPolygon(
                        new[] { new Vector2(Left, Rows[r]), new Vector2(Right, Rows[r]), new Vector2(Right, Rows[r + 1]), new Vector2(Left, Rows[r + 1]) },
                        new[] { low, low, high, high });
                }
            }
        }

        public Main Game = null!;

        public override void _Ready()
        {
            var night = Game.NightColor;
            var tint = new Color(HazeColor.R / night.R, HazeColor.G / night.G, HazeColor.B / night.B);
            float originY = Game.ToScreen(0, 0).Y;
            for (int i = 0; i < Sheets; i++)
            {
                float depth = FirstDepth + i * DepthStep;
                float line = originY + depth * RowPixels;
                // Von der Linie am Boden weich ansteigend, darüber eine Bank aus Bodendunst, in der Höhe nur der Schleier.
                float[] rows = { line, line - RampPixels, line - MistTopPixels, Top };
                float[] alphas = { 0f, SheetAlpha + GroundMistAlpha, SheetAlpha, SheetAlpha };
                _sheets[i] = new Sheet { Rows = rows, Alphas = alphas, Tint = tint, ZIndex = Game.BehindLineZ(depth), LightMask = 0 };
                AddChild(_sheets[i]);
            }
        }

        public override void _Process(double delta)
        {
            // Im Schauer hängt mehr Wasser in der Luft, im Blitz leuchtet der Dunst kurz auf.
            float strength = 1f + RainBoost * Game.Climate.RainStrength + 0.8f * Mathf.Min(1f, Game.Lightning);
            foreach (var sheet in _sheets)
            {
                sheet.Modulate = new Color(1f, 1f, 1f, strength);
            }
        }
    }
}
