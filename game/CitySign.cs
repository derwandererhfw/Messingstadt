using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Wie die Stadt zum Trupp steht, als Zeichen ohne Zahl und ohne Text: eine kleine Silhouette mit Fenstern.
    /// Alle Fenster brennen: Die Stadt hilft. Wenige: Sie schweigt. Keines: Sie steht gegen den Trupp.
    /// Die Stufe kommt aus CityMood.Of, der einen Stelle, die weiß, woran sie hängt.
    /// </summary>
    public partial class CitySign : Control
    {
        // Häuser von links nach rechts: Breite und Höhe in Bildpunkten, dazu wie viele Fenster nebeneinander und übereinander.
        private static readonly (int Width, int Height, int Columns, int Rows)[] Houses =
        {
            (24, 17, 2, 2), (18, 26, 2, 3), (28, 20, 3, 2), (12, 31, 1, 4), (24, 12, 2, 1), (18, 25, 2, 3), (28, 18, 3, 2),
        };

        // Bei "schweigt" brennt nur jedes so vielte Fenster.
        private const int SilentEvery = 4;
        private static readonly Color Lit = new(1f, 0.84f, 0.32f);
        private static readonly Color Dark = new(0.22f, 0.20f, 0.17f);

        private CityMood.Stance _stance;

        public CityMood.Stance Stance
        {
            get => _stance;
            set
            {
                _stance = value;
                QueueRedraw();
            }
        }

        /// <summary>Für den Prüflauf: wie viele Fenster brennen und wie viele es gibt.</summary>
        public string Lights
        {
            get
            {
                int all = 0, lit = 0;
                foreach (var h in Houses)
                {
                    for (int i = 0; i < h.Columns * h.Rows; i++)
                    {
                        lit += Burns(all++) ? 1 : 0;
                    }
                }
                return $"{lit}/{all}";
            }
        }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            float width = 0f;
            foreach (var h in Houses)
            {
                width += h.Width + 1;
            }
            CustomMinimumSize = new Vector2(width, 32f);
        }

        private bool Burns(int window) => _stance switch
        {
            CityMood.Stance.Helps => true,
            CityMood.Stance.Silent => window % SilentEvery == 1,
            _ => false,
        };

        public override void _Draw()
        {
            float x = 0f, ground = Size.Y;
            int window = 0;
            foreach (var h in Houses)
            {
                DrawRect(new Rect2(x, ground - h.Height, h.Width, h.Height), PaperUi.Ink);
                float stepX = h.Width / (float)h.Columns, stepY = (h.Height - 3f) / h.Rows;
                const float pane = 4f;
                for (int row = 0; row < h.Rows; row++)
                {
                    for (int col = 0; col < h.Columns; col++)
                    {
                        var at = new Vector2(x + col * stepX + (stepX - pane) / 2f, ground - h.Height + 3f + row * stepY);
                        DrawRect(new Rect2(at, new Vector2(pane, pane)), Burns(window++) ? Lit : Dark);
                    }
                }
                x += h.Width + 1;
            }
        }
    }
}
