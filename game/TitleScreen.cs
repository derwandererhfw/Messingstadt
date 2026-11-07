using System;
using System.IO;
using System.Linq;
using Godot;

namespace Messingstadt
{
    /// <summary>
    /// Startbildschirm: Titel und Einträge links auf dunklem Grund, dahinter Titelbild und bewegte Szene, dazu das Titelthema.
    /// Alle drei Zutaten sind freiwillig. Fehlen sie, liegt die Stadt stark abgedunkelt dahinter und es läuft nur der Regen.
    /// Titelbild: game/assets/title/title_bg.png (tools/art/title.py). Szene: eine Klasse Messingstadt.TitleAmbience (Control), wird per Reflexion gesucht.
    /// Thema: SoundScape.SetTitleMusic blendet music_title über und drückt die Einsatzmusik weg.
    /// </summary>
    public partial class TitleScreen : Control
    {
        private const string ImagePath = "res://assets/title/title_bg.png";
        private const string ThemePath = "res://assets/audio/music_title.wav";
        private const string AmbienceType = "Messingstadt.TitleAmbience";
        private const float ShadeWithoutImage = 0.82f;
        private const float ShadeWithImage = 0.25f;
        private const float ColumnWidth = 720f;
        public static readonly Color Brass = new(0.82f, 0.68f, 0.38f);
        public static readonly Color Ash = new(0.66f, 0.63f, 0.56f);

        private Texture2D? _image;
        private Control? _ambience;
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
            _image = LoadImage();
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
        }

        private void BuildColumn()
        {
            // Links ein dunkler Streifen hinter der Schrift, damit sie auf jedem Titelbild lesbar bleibt.
            var band = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f), MouseFilter = MouseFilterEnum.Ignore, AnchorBottom = 1f, OffsetRight = ColumnWidth };
            AddChild(band);
            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore, AnchorBottom = 1f, OffsetRight = ColumnWidth };
            margin.AddThemeConstantOverride("margin_left", 96);
            margin.AddThemeConstantOverride("margin_right", 24);
            AddChild(margin);
            var center = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            center.AddThemeConstantOverride("separation", 6);
            margin.AddChild(center);
            center.AddChild(PaperUi.Text("MESSINGSTADT", 64, Brass, PaperUi.Serif));
            center.AddChild(PaperUi.Text("1927. Die Stadt gehört den Brandt-Werken. Noch.", 17, Ash, PaperUi.Typewriter));
            center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40), MouseFilter = MouseFilterEnum.Ignore });
            Entries = new VBoxContainer();
            Entries.AddThemeConstantOverride("separation", 4);
            center.AddChild(Entries);
            center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40), MouseFilter = MouseFilterEnum.Ignore });
            center.AddChild(PaperUi.Text("Pfeiltasten und Enter, oder die Maus", 12, new Color(Ash, 0.55f), PaperUi.Typewriter));
            band.AddChild(new ColorRect { Color = new Color(PaperUi.StampRed, 0.45f), AnchorBottom = 1f, AnchorLeft = 1f, AnchorRight = 1f, OffsetLeft = -2, MouseFilter = MouseFilterEnum.Ignore });
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
            if (_ambience == null && Type.GetType(AmbienceType) is { } type && typeof(Control).IsAssignableFrom(type))
            {
                _ambience = (Control)Activator.CreateInstance(type)!;
                type.GetProperty("Backdrop")?.SetValue(_ambience, _image);
                _ambience.MouseFilter = MouseFilterEnum.Ignore;
                AddChild(_ambience);
                // Über Titelbild und Schatten, unter Streifen und Schrift
                MoveChild(_ambience, _image == null ? 1 : 2);
                _ambience.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            }
        }

        private void Closed()
        {
            Game.Sound.SetTitleMusic(false);
            _ambience?.QueueFree();
            _ambience = null;
        }

        private static Texture2D? LoadImage()
        {
            string path = ProjectSettings.GlobalizePath(ImagePath);
            return File.Exists(path) ? ImageTexture.CreateFromImage(Image.LoadFromFile(path)) : null;
        }

        /// <summary>Für den Prüflauf: welche der drei freiwilligen Zutaten da sind.</summary>
        public string Parts => string.Join(",", new[]
        {
            _image != null ? "bild" : "", Type.GetType(AmbienceType) != null ? "szene" : "", File.Exists(ProjectSettings.GlobalizePath(ThemePath)) ? "thema" : "",
        }.Where(p => p.Length > 0));
    }
}
