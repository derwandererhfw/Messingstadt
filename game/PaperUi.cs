using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Oberfläche wie aus den Zwanzigern: oben ein Telegramm, unten vergilbte Personalakten der Agenten.
    /// Nur Anzeige, ein Klick auf eine Akte wählt den Agenten aus.
    /// </summary>
    public partial class PaperUi : Control
    {
        public static readonly Color PaperColor = new(0.68f, 0.62f, 0.48f);
        public static readonly Color PaperDark = new(0.5f, 0.45f, 0.34f);
        public static readonly Color Ink = new(0.13f, 0.11f, 0.1f);
        public static readonly Color StampRed = new(0.55f, 0.1f, 0.08f);
        private static readonly Color Selected = new(0.95f, 0.78f, 0.35f);
        private const int CardWidth = 152;
        private const int CardHeight = 92;
        private const int CardGap = 6;
        private const int LevelDots = 3;
        private const int CardsFromBottom = 120;

        public static readonly SystemFont Typewriter = new()
        {
            FontNames = new[] { "Courier New", "Courier", "Liberation Mono", "monospace" }, FontWeight = 600,
        };

        public static readonly SystemFont Serif = new()
        {
            FontNames = new[] { "Georgia", "Times New Roman", "Liberation Serif", "serif" }, FontWeight = 700,
        };

        private sealed class Card
        {
            public ColorRect Edge = null!;
            public Label Name = null!;
            public Control Alive = null!;
            public readonly List<ColorRect> LegsPips = new();
            public readonly List<ColorRect> HeartPips = new();
            public ColorRect Health = null!;
            public ColorRect Breath = null!;
            public Label Status = null!;
            public Label Stamp = null!;
        }

        private readonly List<Card> _cards = new();
        private Label _telegram = null!;
        private Label _info = null!;
        private Control _cardRow = null!;

        public Main Game = null!;

        public static Label Text(string text, int size, Color color, Font font)
        {
            var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeFontOverride("font", font);
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }

        public static StyleBoxFlat PaperBox(Color color) => new()
        {
            BgColor = color, ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14,
            BorderColor = new Color(0.2f, 0.16f, 0.12f), BorderWidthBottom = 3, BorderWidthRight = 3, BorderWidthLeft = 1, BorderWidthTop = 1,
        };

        public static void InkButton(Button button)
        {
            var box = new StyleBoxFlat { BgColor = Ink, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 };
            var hover = new StyleBoxFlat { BgColor = StampRed, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 };
            var off = new StyleBoxFlat { BgColor = PaperDark, ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4 };
            button.AddThemeStyleboxOverride("normal", box);
            button.AddThemeStyleboxOverride("pressed", box);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("disabled", off);
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.AddThemeFontOverride("font", Typewriter);
            button.AddThemeFontSizeOverride("font_size", 14);
            button.AddThemeColorOverride("font_color", PaperColor);
            button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.8f));
            button.AddThemeColorOverride("font_disabled_color", new Color(0.3f, 0.26f, 0.2f));
        }

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            AddChild(Strip(14, 12, new Color(0, 0, 0, 0.5f)));
            AddChild(Strip(10, 8, PaperColor));
            var head = Text("TELEGRAMM", 13, StampRed, Serif);
            head.Position = new Vector2(20, 12);
            AddChild(head);
            _telegram = Text("", 15, Ink, Typewriter);
            _telegram.Position = new Vector2(122, 11);
            AddChild(_telegram);
            _info = Text("", 13, Ink, Typewriter);
            _info.Position = new Vector2(122, 35);
            AddChild(_info);
            // Die Akten bleiben unten links, egal wie groß das Fenster ist.
            _cardRow = new Control
            {
                AnchorTop = 1f, AnchorBottom = 1f, OffsetTop = -CardsFromBottom, OffsetBottom = -CardsFromBottom + CardHeight,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            AddChild(_cardRow);
            for (int i = 0; i < 4; i++)
            {
                AddCard(i);
            }
        }

        /// <summary>Papierstreifen für das Telegramm: läuft über die ganze Breite des Bildes.</summary>
        private static ColorRect Strip(int left, int top, Color color) => new()
        {
            AnchorRight = 1f, OffsetLeft = left, OffsetTop = top, OffsetRight = -28 + left, OffsetBottom = top + 52,
            Color = color, MouseFilter = MouseFilterEnum.Ignore,
        };

        private void AddCard(int index)
        {
            int x = 12 + index * (CardWidth + CardGap), y = 0;
            var root = new Control { Position = new Vector2(x, y), Size = new Vector2(CardWidth, CardHeight), MouseFilter = MouseFilterEnum.Stop };
            root.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
                {
                    Game.SelectAgent(index, click.ShiftPressed);
                }
            };
            _cardRow.AddChild(root);
            var card = new Card();
            card.Edge = new ColorRect { Position = new Vector2(-2, -2), Size = new Vector2(CardWidth + 4, CardHeight + 4), MouseFilter = MouseFilterEnum.Ignore };
            root.AddChild(card.Edge);
            root.AddChild(new ColorRect { Size = new Vector2(CardWidth, CardHeight), Color = PaperColor, MouseFilter = MouseFilterEnum.Ignore });
            // Reiter und Klammer wie bei einer Akte
            root.AddChild(new ColorRect { Position = new Vector2(CardWidth - 22, 4), Size = new Vector2(16, 3), Color = new Color(0.32f, 0.3f, 0.3f), MouseFilter = MouseFilterEnum.Ignore });
            card.Name = Text("", 14, Ink, Typewriter);
            card.Name.Position = new Vector2(6, 2);
            root.AddChild(card.Name);
            card.Status = Text("", 11, new Color(Ink, 0.75f), Typewriter);
            card.Status.Position = new Vector2(6, 19);
            root.AddChild(card.Status);
            // Was nur Lebende brauchen. Bei Gefallenen steht dort der Stempel, sonst würde er etwas überdecken.
            card.Alive = new Control { Size = new Vector2(CardWidth, CardHeight), MouseFilter = MouseFilterEnum.Ignore };
            root.AddChild(card.Alive);
            AddLevelRow(card.Alive, card.LegsPips, "Beine", 34);
            AddLevelRow(card.Alive, card.HeartPips, "Herz", 49);
            card.Alive.AddChild(new ColorRect { Position = new Vector2(6, 66), Size = new Vector2(CardWidth - 12, 8), Color = PaperDark, MouseFilter = MouseFilterEnum.Ignore });
            card.Health = new ColorRect { Position = new Vector2(7, 67), Size = new Vector2(CardWidth - 14, 6), Color = new Color(0.4f, 0.12f, 0.08f), MouseFilter = MouseFilterEnum.Ignore };
            card.Alive.AddChild(card.Health);
            card.Alive.AddChild(new ColorRect { Position = new Vector2(6, 77), Size = new Vector2(CardWidth - 12, 6), Color = PaperDark, MouseFilter = MouseFilterEnum.Ignore });
            card.Breath = new ColorRect { Position = new Vector2(7, 78), Size = new Vector2(CardWidth - 14, 4), Color = new Color(0.15f, 0.22f, 0.32f), MouseFilter = MouseFilterEnum.Ignore };
            card.Alive.AddChild(card.Breath);
            card.Stamp = Text("GEFALLEN", 21, StampRed, Serif);
            card.Stamp.Position = new Vector2(14, 50);
            card.Stamp.RotationDegrees = -11f;
            card.Stamp.Visible = false;
            root.AddChild(card.Stamp);
            _cards.Add(card);
        }

        private static void AddLevelRow(Control parent, List<ColorRect> pips, string label, int y)
        {
            var text = Text(label, 12, Ink, Typewriter);
            text.Position = new Vector2(6, y - 3);
            parent.AddChild(text);
            for (int i = 0; i < LevelDots; i++)
            {
                var pip = new ColorRect { Position = new Vector2(56 + i * 14, y + 1), Size = new Vector2(10, 9), MouseFilter = MouseFilterEnum.Ignore };
                parent.AddChild(pip);
                pips.Add(pip);
            }
        }

        private static void ShowLevel(List<ColorRect> pips, int level)
        {
            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].Color = i < level ? Ink : PaperDark;
            }
        }

        public void Refresh(World world, HashSet<int> selected, string telegram, string info)
        {
            _telegram.Text = telegram;
            _info.Text = info;
            for (int i = 0; i < _cards.Count && i < world.Units.Count; i++)
            {
                var u = world.Units[i];
                var card = _cards[i];
                card.Name.Text = $"{i + 1} {u.Name.ToUpperInvariant()}";
                string skill = u.Skill == World.SkillLocksmith ? "Schlosser" : u.Skill == World.SkillBookkeeper ? "Buchhalter" : "Agent";
                string doing = u.CarriesItem ? "trägt das Buch" : u.Sprinting ? "rennt" : skill;
                string light = Game.LightWord(u);
                card.Status.Text = !u.Alive ? skill : light.Length > 0 ? $"{doing}, {light}" : doing;
                ShowLevel(card.LegsPips, u.Legs);
                ShowLevel(card.HeartPips, u.Heart);
                card.Alive.Visible = u.Alive;
                float width = CardWidth - 14;
                card.Health.Size = new Vector2(width * u.Hp / u.MaxHp, 6);
                card.Breath.Size = new Vector2(width * u.Stamina / World.MaxStamina(u), 4);
                card.Stamp.Visible = !u.Alive;
                card.Edge.Color = selected.Contains(u.Id) ? Selected : new Color(0f, 0f, 0f, 0.45f);
            }
        }
    }
}
