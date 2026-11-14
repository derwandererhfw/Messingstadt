using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Spielt eine Zwischensequenz aus data/sequences ab: Kamerafahrt in der Spielgrafik oder Bild-Panels, Schwarzblende, Papier und Ton.
    /// Esc überspringt alles, Klick, Leertaste oder Enter schreiben den Text fertig und gehen weiter.
    /// Liegt unter dem Filmbild, damit Korn und Ränder auch auf Titel und Papier liegen. Schreibt keine Dateien.
    /// </summary>
    public partial class SequencePlayer : CanvasLayer
    {
        private const float BarHeight = 56f;
        private const float BarSeconds = 0.6f;
        private static readonly Color TelegramColor = new(0.78f, 0.72f, 0.52f);
        private static readonly Color CaptionColor = new(0.9f, 0.87f, 0.79f);

        private SequenceRun _run = null!;
        private ColorRect _black = null!;
        private ColorRect _barTop = null!, _barBottom = null!;
        private Control _sheet = null!;
        private Control _picture = null!;
        private TextureRect? _zooming;
        private Control? _speech;
        private readonly Dictionary<string, Texture2D> _textures = new();
        private Label? _typed;
        private Label _hint = null!;
        private int _shownChanges = -1;
        private int _shownLetters;
        private float _age;
        private Vector2 _cameraFrom, _cameraTo;
        private bool _done;

        public Main Game = null!;
        public SequenceData Data = null!;
        public IReadOnlyDictionary<string, string> Texts = new Dictionary<string, string>();

        /// <summary>Wird genau einmal gerufen, wenn die Sequenz endet oder übersprungen wird.</summary>
        public Action? OnDone;

        public string Id => Data.Id;
        public int Beat => _run.Beat;

        public static SequenceData Load(string id)
        {
            string path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data", "sequences", id + ".json"));
            return SequenceData.Parse(File.ReadAllText(path));
        }

        public override void _Ready()
        {
            Layer = 1;
            _run = new SequenceRun(Data, Texts);
            _black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_black);
            _black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            // Bild-Panels liegen über der Schwarzblende. Die Blende dunkelt sie über ihre Helligkeit ab.
            _picture = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_picture);
            _picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _barTop = Bar(0f);
            _barBottom = Bar(1f);
            _sheet = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            AddChild(_sheet);
            _sheet.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _hint = PaperUi.Text("Leertaste: weiter   Esc: überspringen", 12, new Color(0.75f, 0.72f, 0.65f, 0.6f), PaperUi.Typewriter);
            _hint.AnchorLeft = 1f;
            _hint.AnchorRight = 1f;
            _hint.AnchorTop = 1f;
            _hint.AnchorBottom = 1f;
            _hint.OffsetLeft = -330f;
            _hint.OffsetTop = -34f;
            AddChild(_hint);
            Refresh();
        }

        private ColorRect Bar(float anchor)
        {
            var bar = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
            bar.AnchorRight = 1f;
            bar.AnchorTop = anchor;
            bar.AnchorBottom = anchor;
            AddChild(bar);
            return bar;
        }

        /// <summary>Für Prüfläufe: springt so weit vor, als wäre die Zeit vergangen.</summary>
        public void FastForward(float seconds)
        {
            for (float t = 0f; t < seconds && !_run.Done; t += 1f / 60f)
            {
                _run.Advance(1f / 60f);
            }
            _age = seconds;
            Refresh();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _age += dt;
            foreach (var sound in _run.Advance(dt))
            {
                Game.Sound.PlayEverywhere(sound.Name, (float)sound.Volume);
            }
            Refresh();
        }

        public override void _Input(InputEvent e)
        {
            if (_done)
            {
                return;
            }
            if (e is InputEventKey { Pressed: true, Echo: false } key)
            {
                switch (key.PhysicalKeycode)
                {
                    case Key.Escape:
                        _run.Skip();
                        break;
                    case Key.Space or Key.Enter or Key.KpEnter:
                        _run.Press();
                        break;
                    default:
                        // F11 und M gelten weiter, alles andere bleibt bis zum Einsatz liegen.
                        return;
                }
                GetViewport().SetInputAsHandled();
                Refresh();
            }
            else if (e is InputEventMouseButton { Pressed: true } mouse)
            {
                if (mouse.ButtonIndex == MouseButton.Left)
                {
                    _run.Press();
                    Refresh();
                }
                GetViewport().SetInputAsHandled();
            }
        }

        /// <summary>Bringt Bild und Kamera auf den Stand des Ablaufs.</summary>
        private void Refresh()
        {
            if (_done)
            {
                return;
            }
            if (_run.Done)
            {
                Finish();
                return;
            }
            if (_run.Changes != _shownChanges)
            {
                _shownChanges = _run.Changes;
                BuildBeat();
            }
            bool image = HasPicture(_run.Current);
            _black.Color = new Color(0f, 0f, 0f, image ? 1f : (float)_run.Fade);
            float light = 1f - (float)_run.Fade;
            _picture.Modulate = new Color(light, light, light);
            if (_zooming != null && _run.Current.Zoom is { Length: 2 } zoom)
            {
                float z = Mathf.Lerp((float)zoom[0], (float)zoom[1], (float)_run.Progress);
                _zooming.PivotOffset = _zooming.Size / 2f;
                _zooming.Scale = new Vector2(z, z);
            }
            if (_speech != null)
            {
                _speech.Visible = !_run.Typing;
            }
            float bars = Mathf.Clamp(_age / BarSeconds, 0f, 1f) * BarHeight;
            _barTop.OffsetBottom = bars;
            _barBottom.OffsetTop = -bars;
            if (_run.Current.Camera is { } camera)
            {
                float t = (float)_run.Progress;
                Game.SetIntroCamera(_cameraFrom.Lerp(_cameraTo, t), Mathf.Lerp((float)camera.ZoomFrom, (float)camera.ZoomTo, t));
            }
            if (_typed != null && _run.Letters != _shownLetters)
            {
                _shownLetters = _run.Letters;
                _typed.VisibleCharacters = _shownLetters;
                if (_run.Typing)
                {
                    Game.Sound.Play("typewriter", Game.CameraCenter, -14f, 100000f, 0.12f);
                }
            }
        }

        private void BuildBeat()
        {
            foreach (var child in _sheet.GetChildren())
            {
                child.QueueFree();
            }
            _typed = null;
            _speech = null;
            _shownLetters = -1;
            var beat = _run.Current;
            if (!HasPicture(beat))
            {
                ClearPicture();
            }
            if (beat.Camera is { } camera)
            {
                var offset = camera.Offset is { Length: 2 } o ? new Vector2((float)o[0], (float)o[1]) : Vector2.Zero;
                _cameraFrom = Game.SequenceAnchor(camera.From) + offset;
                _cameraTo = Game.SequenceAnchor(camera.To) + offset;
            }
            switch (beat.Show)
            {
                case "title": BuildTitle(); break;
                case "card": BuildCard(); break;
                case "paper": BuildSheet(PaperUi.PaperColor, 0.72f, 600f, 17); break;
                case "telegram": BuildSheet(TelegramColor, 0.5f, 640f, 16); break;
                case "newspaper": BuildNewspaper(); break;
                case "image": BuildImage(beat); break;
                case "ledger": BuildLedger(beat); break;
            }
        }

        /// <summary>Liegt in diesem Abschnitt ein Bild über der Schwarzblende?</summary>
        private static bool HasPicture(SequenceBeat beat) => beat.Show == "image" || (beat.Show == "ledger" && beat.Image.Length > 0);

        /// <summary>
        /// Seite des Totenbuchs für die letzte Nacht: Namen beider Seiten auf einem Blatt, links Kolbs Erzählkasten.
        /// Mit Bild liegt das Blatt auf diesem Bild, sonst auf Schwarz.
        /// </summary>
        private void BuildLedger(SequenceBeat beat)
        {
            if (beat.Image.Length > 0)
            {
                BuildImage(beat);
            }
            else
            {
                Narration();
            }
            var body = Sheet(PaperUi.PaperColor, 0.68f, 560f);
            body.AddChild(PaperUi.Text(_run.Head.Length > 0 ? _run.Head : "TOTENBUCH", 14, PaperUi.StampRed, PaperUi.Serif));
            if (_run.Title.Length > 0)
            {
                var title = PaperUi.Text(_run.Title, 22, PaperUi.Ink, PaperUi.Serif);
                title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                title.CustomMinimumSize = new Vector2(520f, 0f);
                body.AddChild(title);
            }
            body.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = PaperUi.Ink, MouseFilter = Control.MouseFilterEnum.Ignore });
            // Zwei Spalten: links die eigenen Toten, rechts die der Wache. Eine leere Spalte bleibt leer stehen.
            var columns = new HBoxContainer();
            columns.AddThemeConstantOverride("separation", 24);
            body.AddChild(columns);
            foreach (var (heading, key) in new[] { ("UNSERE", "totenbuch_eigene"), ("IHRE", "totenbuch_wache") })
            {
                var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(240f, 0f) };
                column.AddChild(PaperUi.Text(heading, 12, PaperUi.StampRed, PaperUi.Serif));
                string names = Texts.TryGetValue(key, out string? list) ? list : "";
                var lines = PaperUi.Text(names.Length > 0 ? names : "kein Eintrag", 15, PaperUi.Ink, PaperUi.Typewriter);
                lines.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                lines.CustomMinimumSize = new Vector2(240f, 0f);
                column.AddChild(lines);
                columns.AddChild(column);
            }
            var sheet = (Control)body.GetParent();
            sheet.Rotation = 0.012f;
        }

        /// <summary>Kolbs Erzählkasten oben links, getippt.</summary>
        private void Narration()
        {
            if (_run.Body.Length == 0)
            {
                return;
            }
            var box = new PanelContainer
            {
                OffsetLeft = 36f, OffsetTop = BarHeight + 18f, OffsetRight = 36f + 560f,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            box.AddThemeStyleboxOverride("panel", PaperUi.PaperBox(CaptionColor));
            _sheet.AddChild(box);
            _typed = PaperUi.Text(_run.Body, 17, PaperUi.Ink, PaperUi.Typewriter);
            _typed.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _typed.CustomMinimumSize = new Vector2(530f, 0f);
            _typed.VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
            _typed.VisibleCharacters = 0;
            box.AddChild(_typed);
        }

        private void ClearPicture()
        {
            foreach (var child in _picture.GetChildren())
            {
                child.QueueFree();
            }
            _zooming = null;
        }

        private Texture2D Texture(string path)
        {
            if (!_textures.TryGetValue(path, out var texture))
            {
                texture = ImageTexture.CreateFromImage(Godot.Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/" + path)));
                _textures[path] = texture;
            }
            return texture;
        }

        /// <summary>Bild-Panel: bildschirmfüllend oder als Einschub, dazu Kolbs Erzählkasten, Sprechblasen und Beschriftungen.</summary>
        private void BuildImage(SequenceBeat beat)
        {
            var rect = new TextureRect
            {
                Texture = Texture(beat.Image), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            if (beat.Inset)
            {
                // Einschub unten rechts mit schwarzem Rand, das vorige Bild bleibt dahinter stehen
                var frame = new ColorRect
                {
                    Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore,
                    AnchorLeft = 0.5f, AnchorRight = 0.96f, AnchorTop = 0.36f, AnchorBottom = 0.84f,
                };
                _picture.AddChild(frame);
                frame.AddChild(rect);
                rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 6);
                frame.Modulate = new Color(1f, 1f, 1f, 0f);
                frame.CreateTween().TweenProperty(frame, "modulate:a", 1f, 0.25f);
            }
            else
            {
                ClearPicture();
                _picture.AddChild(rect);
                rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _zooming = rect;
            }
            Narration();
            if (beat.Speech.Count > 0)
            {
                var column = new VBoxContainer
                {
                    AnchorLeft = 1f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
                    OffsetLeft = -470f, OffsetRight = -36f, OffsetTop = -BarHeight - 24f, OffsetBottom = -BarHeight - 24f,
                    GrowVertical = Control.GrowDirection.Begin, MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                column.AddThemeConstantOverride("separation", 10);
                column.Alignment = BoxContainer.AlignmentMode.End;
                _sheet.AddChild(column);
                foreach (string line in beat.Speech)
                {
                    var bubble = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                    var style = PaperUi.PaperBox(new Color(0.97f, 0.96f, 0.93f));
                    style.SetCornerRadiusAll(18);
                    bubble.AddThemeStyleboxOverride("panel", style);
                    var text = PaperUi.Text(SequenceTexts.Fill(line, Texts), 16, PaperUi.Ink, PaperUi.Typewriter);
                    text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    text.CustomMinimumSize = new Vector2(400f, 0f);
                    bubble.AddChild(text);
                    column.AddChild(bubble);
                }
                _speech = column;
                _speech.Visible = false;
            }
            if (beat.Captions.Count > 0)
            {
                var row = new HBoxContainer
                {
                    AnchorLeft = 0.235f, AnchorRight = 0.745f, AnchorTop = 1f, AnchorBottom = 1f,
                    OffsetTop = -BarHeight - 62f, OffsetBottom = -BarHeight - 8f, MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                _sheet.AddChild(row);
                foreach (string caption in beat.Captions)
                {
                    var label = PaperUi.Text(caption, 15, CaptionColor, PaperUi.Typewriter);
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    row.AddChild(label);
                }
            }
        }

        private void BuildTitle()
        {
            var title = Centered(_run.Title, 64, new Color(0.82f, 0.68f, 0.38f), PaperUi.Serif, 0f);
            title.Modulate = new Color(1f, 1f, 1f, 0f);
            title.CreateTween().TweenProperty(title, "modulate:a", 1f, 1.2f);
            var subtitle = Centered(_run.Body, 20, new Color(0.65f, 0.62f, 0.55f), PaperUi.Typewriter, 88f);
            subtitle.Modulate = new Color(1f, 1f, 1f, 0f);
            subtitle.CreateTween().TweenProperty(subtitle, "modulate:a", 1f, 1.2f).SetDelay(0.8f);
        }

        private void BuildCard()
        {
            _typed = Centered(_run.Body, 22, new Color(0.8f, 0.76f, 0.66f), PaperUi.Typewriter, 0f);
            _typed.VisibleCharacters = 0;
        }

        private Label Centered(string text, int size, Color color, Font font, float offset)
        {
            var label = PaperUi.Text(text, size, color, font);
            label.AnchorRight = 1f;
            label.AnchorTop = 0.42f;
            label.AnchorBottom = 0.42f;
            label.OffsetTop = offset;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            _sheet.AddChild(label);
            return label;
        }

        /// <summary>Getipptes Blatt: Auftrag rechts neben dem Trupp, Telegramm in der Mitte.</summary>
        private void BuildSheet(Color color, float centerX, float width, int size)
        {
            var body = Sheet(color, centerX, width);
            if (_run.Head.Length > 0)
            {
                body.AddChild(PaperUi.Text(_run.Head, 14, PaperUi.StampRed, PaperUi.Serif));
            }
            if (_run.Title.Length > 0)
            {
                body.AddChild(PaperUi.Text(_run.Title, 24, PaperUi.Ink, PaperUi.Serif));
            }
            // Der ganze Text steht von Anfang an im Feld, sichtbar wird er Buchstabe für Buchstabe. So wächst das Blatt nicht.
            _typed = PaperUi.Text(_run.Body, size, PaperUi.Ink, PaperUi.Typewriter);
            _typed.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _typed.CustomMinimumSize = new Vector2(width - 40f, 0f);
            _typed.VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
            _typed.VisibleCharacters = 0;
            body.AddChild(_typed);
        }

        private void BuildNewspaper()
        {
            var body = Sheet(PaperUi.PaperColor, 0.5f, 680f);
            body.AddChild(PaperUi.Text(_run.Head, 14, PaperUi.StampRed, PaperUi.Serif));
            body.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = PaperUi.Ink, MouseFilter = Control.MouseFilterEnum.Ignore });
            var head = PaperUi.Text(_run.Title, 30, PaperUi.Ink, PaperUi.Serif);
            head.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            head.CustomMinimumSize = new Vector2(640, 0);
            body.AddChild(head);
            var line = PaperUi.Text(_run.Body, 15, PaperUi.Ink, PaperUi.Typewriter);
            line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            line.CustomMinimumSize = new Vector2(640, 0);
            body.AddChild(line);
            // Die Zeitung fällt leicht schräg auf den Tisch und richtet sich auf.
            var sheet = (Control)body.GetParent();
            sheet.Scale = new Vector2(1.15f, 1.15f);
            sheet.Rotation = -0.06f;
            var tween = sheet.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sheet, "scale", Vector2.One, 0.35f);
            tween.TweenProperty(sheet, "rotation", -0.015f, 0.35f);
        }

        private VBoxContainer Sheet(Color color, float centerX, float width)
        {
            var panel = new PanelContainer
            {
                AnchorLeft = centerX, AnchorRight = centerX, AnchorTop = 0.5f, AnchorBottom = 0.5f,
                OffsetLeft = -width / 2f, OffsetRight = width / 2f,
                GrowVertical = Control.GrowDirection.Both, MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            panel.AddThemeStyleboxOverride("panel", PaperUi.PaperBox(color));
            _sheet.AddChild(panel);
            panel.Resized += () => panel.PivotOffset = panel.Size / 2f;
            var body = new VBoxContainer();
            body.AddThemeConstantOverride("separation", 10);
            panel.AddChild(body);
            return body;
        }

        private void Finish()
        {
            _done = true;
            var done = OnDone;
            OnDone = null;
            QueueFree();
            done?.Invoke();
        }
    }
}
