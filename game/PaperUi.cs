using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Papier, Tinte und Schreibmaschine wie aus den Zwanzigern: Farben, Schriften und Bausteine für alle Blätter.
    /// Die Anzeige im Einsatz steht in Hud.cs.
    /// </summary>
    public partial class PaperUi : Control
    {
        public static readonly Color PaperColor = new(0.68f, 0.62f, 0.48f);
        public static readonly Color PaperDark = new(0.5f, 0.45f, 0.34f);
        public static readonly Color Ink = new(0.13f, 0.11f, 0.1f);
        public static readonly Color StampRed = new(0.55f, 0.1f, 0.08f);
        private static readonly Color Selected = new(0.95f, 0.78f, 0.35f);
        // Mitgelieferte Schriften unter der OFL, siehe game/assets/fonts. So sieht das Spiel überall gleich aus.
        public static readonly Font Typewriter = LoadFont("CourierPrime-Bold.ttf");
        public static readonly Font Serif = LoadFont("OldStandard-Bold.ttf");

        private static FontFile LoadFont(string file)
        {
            var font = new FontFile { Hinting = TextServer.Hinting.Light };
            font.LoadDynamicFont($"res://assets/fonts/{file}");
            return font;
        }

        public static Label Text(string text, int size, Color color, Font font)
        {
            // Die Welt ist Pixelgrafik, Schrift muss beim Strecken des Fensters aber weich bleiben, sonst zerfällt sie.
            var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Linear };
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
            // Was gewählt ist, trägt denselben goldenen Rand wie eine gewählte Akte. Die Maus wählt dasselbe wie die Tasten.
            button.AddThemeStyleboxOverride("focus", new StyleBoxFlat
            {
                DrawCenter = false, BorderColor = Selected, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            });
            button.AddThemeFontOverride("font", Typewriter);
            button.AddThemeFontSizeOverride("font_size", 14);
            button.AddThemeColorOverride("font_color", PaperColor);
            button.AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.8f));
            button.AddThemeColorOverride("font_focus_color", new Color(1f, 0.95f, 0.8f));
            button.AddThemeColorOverride("font_disabled_color", new Color(0.3f, 0.26f, 0.2f));
            button.TextureFilter = TextureFilterEnum.Linear;
            button.MouseEntered += () =>
            {
                if (!button.Disabled)
                {
                    button.GrabFocus();
                }
            };
        }

        /// <summary>Lautstärkeregler in Tinte: gefüllter Teil dunkel, Griff als schmaler Balken.</summary>
        public static void InkSlider(HSlider slider)
        {
            var track = new StyleBoxFlat { BgColor = PaperDark, ContentMarginTop = 4, ContentMarginBottom = 4 };
            var filled = new StyleBoxFlat { BgColor = Ink, ContentMarginTop = 4, ContentMarginBottom = 4 };
            var active = new StyleBoxFlat { BgColor = StampRed, ContentMarginTop = 4, ContentMarginBottom = 4 };
            slider.AddThemeStyleboxOverride("slider", track);
            slider.AddThemeStyleboxOverride("grabber_area", filled);
            slider.AddThemeStyleboxOverride("grabber_area_highlight", active);
            slider.AddThemeIconOverride("grabber", GrabberIcon(Ink));
            slider.AddThemeIconOverride("grabber_highlight", GrabberIcon(StampRed));
            slider.MouseEntered += () => slider.GrabFocus();
        }

        private static ImageTexture GrabberIcon(Color color)
        {
            var image = Image.CreateEmpty(8, 18, false, Image.Format.Rgba8);
            image.Fill(color);
            return ImageTexture.CreateFromImage(image);
        }
    }
}
