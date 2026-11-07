using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Startbildschirm: Schriftzug und Einträge links auf dunklem Grund, dahinter das Titelbild mit Regen und Laternenflackern
    /// (TitleAmbience), dazu das Titelthema aus SoundScape. Titelbild und Schriftzug kommen aus tools/art/title.py.
    /// Fehlen die Bilder, liegt die Stadt stark abgedunkelt dahinter und der Titel steht in Messingschrift da.
    /// </summary>
    public partial class TitleScreen : Control
    {
        private const string ImagePath = "res://assets/title/title_bg.png";
        private const string LogoPath = "res://assets/title/title_logo.png";
        private const float ShadeWithoutImage = 0.82f;
        private const float ShadeWithImage = 0.15f;
        private const float ColumnWidth = 720f;
        private const float LogoWidth = 600f;
        public static readonly Color Brass = new(0.82f, 0.68f, 0.38f);
        public static readonly Color Ash = new(0.66f, 0.63f, 0.56f);

        // Laterne und Schlote im Titelbild, in Bildpunkten von title_bg.png (1920 x 1080).
        // Die rechte Laterne, damit links hinter dem Menü Ruhe bleibt.
        private static readonly Vector2 ImageLamp = new(1694, 458);
        private static readonly Vector2[] ImageSmoke = { new(1157, 62), new(1422, 192) };

        private Texture2D? _image;
        private TitleAmbience? _ambience;
        private Control _column = null!;

        public Main Game = null!;

        /// <summary>Hier legen die Menüs die Einträge ab.</summary>
        public VBoxContainer Entries { get; private set; } = null!;

        /// <summary>Titel und Einträge zeigen, oder nur den Hintergrund, solange ein Blatt darüber liegt.</summary>
        public bool ShowColumn
        {
            set => _column.Visible = value;
        }

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;
            _image = LoadTexture(ImagePath, false);
            if (_image != null)
            {
                var backdrop = new TextureRect
                {
                    Texture = _image, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Linear,
                };
                AddChild(backdrop);
                backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            }
            var shade = new ColorRect
            {
                Color = new Color(0f, 0f, 0f, _image == null ? ShadeWithoutImage : ShadeWithImage), MouseFilter = MouseFilterEnum.Ignore,
            };
            AddChild(shade);
            shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            BuildColumn();
            Resized += PlaceAmbience;
        }

        private void BuildColumn()
        {
            // Ohne Titelbild ein dunkler Streifen hinter der Schrift. Das Titelbild ist links ohnehin dunkel und ruhig.
            if (_image == null)
            {
                var band = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f), MouseFilter = MouseFilterEnum.Ignore, AnchorBottom = 1f, OffsetRight = ColumnWidth };
                AddChild(band);
                band.AddChild(new ColorRect { Color = new Color(PaperUi.StampRed, 0.45f), AnchorBottom = 1f, AnchorLeft = 1f, AnchorRight = 1f, OffsetLeft = -2, MouseFilter = MouseFilterEnum.Ignore });
            }
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore, AnchorBottom = 1f, OffsetRight = ColumnWidth };
            margin.AddThemeConstantOverride("margin_left", 72);
            margin.AddThemeConstantOverride("margin_right", 24);
            AddChild(margin);
            var center = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            center.AddThemeConstantOverride("separation", 6);
            margin.AddChild(center);
            var logo = LoadTexture(LogoPath, true);
            if (logo != null)
            {
                center.AddChild(new TextureRect
                {
                    Texture = logo, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                    CustomMinimumSize = new Vector2(LogoWidth, LogoWidth * logo.GetHeight() / logo.GetWidth()),
                    SizeFlagsHorizontal = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore,
                    TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                });
            }
            else
            {
                center.AddChild(PaperUi.Text("MESSINGSTADT", 64, Brass, PaperUi.Serif));
            }
            // Der Schriftzug trägt die Jahreszahl schon, dann steht sie im Untertitel nicht noch einmal.
            var subtitle = PaperUi.Text(logo != null ? "Die Stadt gehört den Brandt-Werken. Noch." : "1927. Die Stadt gehört den Brandt-Werken. Noch.", 17, Ash, PaperUi.Typewriter);
            subtitle.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            var indent = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            indent.AddThemeConstantOverride("margin_left", logo != null ? 18 : 0);
            indent.AddChild(subtitle);
            center.AddChild(indent);
            center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40), MouseFilter = MouseFilterEnum.Ignore });
            // So breit wie der längste Eintrag, damit der Wahlbalken nicht über das Bild läuft.
            Entries = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
            Entries.AddThemeConstantOverride("separation", 4);
            center.AddChild(Entries);
            center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40), MouseFilter = MouseFilterEnum.Ignore });
            center.AddChild(PaperUi.Text("Pfeiltasten und Enter, oder die Maus", 12, new Color(Ash, 0.55f), PaperUi.Typewriter));
            _column = margin;
        }

        /// <summary>Eintrag in der Liste: schlichte Schrift, gewählt mit rotem Strich und Messing. Ausgegraut, wenn noch nichts dahinter liegt.</summary>
        public static Button Entry(string text, bool enabled)
        {
            var button = new Button
            {
                Text = enabled ? text : $"{text}  (bald)", Disabled = !enabled, Alignment = HorizontalAlignment.Left,
                FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None, TextureFilter = TextureFilterEnum.Linear,
            };
            var plain = new StyleBoxEmpty { ContentMarginLeft = 14, ContentMarginTop = 4, ContentMarginBottom = 4 };
            var chosen = new StyleBoxFlat
            {
                BgColor = new Color(0f, 0f, 0f, 0.35f), BorderColor = PaperUi.StampRed, BorderWidthLeft = 3,
                ContentMarginLeft = 14, ContentMarginTop = 4, ContentMarginBottom = 4,
            };
            button.AddThemeStyleboxOverride("normal", plain);
            button.AddThemeStyleboxOverride("disabled", plain);
            button.AddThemeStyleboxOverride("hover", chosen);
            button.AddThemeStyleboxOverride("pressed", chosen);
            button.AddThemeStyleboxOverride("focus", chosen);
            button.AddThemeFontOverride("font", PaperUi.Typewriter);
            button.AddThemeFontSizeOverride("font_size", 22);
            button.AddThemeColorOverride("font_color", Ash);
            button.AddThemeColorOverride("font_hover_color", Brass);
            button.AddThemeColorOverride("font_focus_color", Brass);
            button.AddThemeColorOverride("font_pressed_color", Brass);
            button.AddThemeColorOverride("font_disabled_color", new Color(Ash, 0.32f));
            button.MouseEntered += () =>
            {
                if (!button.Disabled)
                {
                    button.GrabFocus();
                }
            };
            return button;
        }

        public override void _Notification(int what)
        {
            if (what == NotificationVisibilityChanged)
            {
                if (IsVisibleInTree())
                {
                    Opened();
                }
                else
                {
                    Closed();
                }
            }
        }

        private void Opened()
        {
            Game.Sound.SetTitleMusic(true);
            if (_ambience != null)
            {
                return;
            }
            _ambience = new TitleAmbience { Strength = _image == null ? 0.6f : 1f };
            AddChild(_ambience);
            // Über Titelbild und Schatten, unter Streifen und Schrift
            MoveChild(_ambience, _image == null ? 1 : 2);
            PlaceAmbience();
        }

        private void Closed()
        {
            Game.Sound.SetTitleMusic(false);
            _ambience?.QueueFree();
            _ambience = null;
        }

        /// <summary>
        /// Laterne und Dampf sitzen auf denselben Stellen im Titelbild, auch wenn das Fenster ein anderes Seitenverhältnis hat
        /// und vom Bild etwas abgeschnitten wird. Ohne Bild bleiben die Vorgaben der Szene.
        /// </summary>
        private void PlaceAmbience()
        {
            if (_ambience == null || _image == null || Size.X <= 0f || Size.Y <= 0f)
            {
                return;
            }
            _ambience.LampAt = ImageToScreen(ImageLamp);
            _ambience.SteamAt = ImageSmoke.Select(ImageToScreen).ToList();
        }

        /// <summary>Bildpunkt im Titelbild als Anteil der Fläche, mit derselben Rechnung wie KeepAspectCovered.</summary>
        private Vector2 ImageToScreen(Vector2 pixel)
        {
            var image = _image!.GetSize();
            float scale = Mathf.Max(Size.X / image.X, Size.Y / image.Y);
            var offset = (Size - image * scale) / 2f;
            return (pixel * scale + offset) / Size;
        }

        private static Texture2D? LoadTexture(string resPath, bool mipmaps)
        {
            string path = ProjectSettings.GlobalizePath(resPath);
            if (!File.Exists(path))
            {
                return null;
            }
            var image = Image.LoadFromFile(path);
            if (mipmaps)
            {
                image.GenerateMipmaps();
            }
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>Für den Prüflauf: welche Bilder der Startbildschirm gefunden hat und ob die Szene läuft.</summary>
        public string Parts => string.Join(",", new List<string>
        {
            _image != null ? "bild" : "", File.Exists(ProjectSettings.GlobalizePath(LogoPath)) ? "schriftzug" : "",
            _ambience != null ? "szene" : "",
        }.Where(p => p.Length > 0));
    }
}
