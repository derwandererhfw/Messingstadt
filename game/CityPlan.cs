using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>Ein Viertel auf dem Stadtplan, so wie der Bildschirm es braucht.</summary>
    public sealed class District
    {
        public string Id = "";
        public string Name = "";
        /// <summary>Auftragsart, etwa "Diebstahl". Leer ohne Auftrag.</summary>
        public string Kind = "";
        public string Title = "";
        public string Text = "";
        public string Reward = "";
        /// <summary>Erobert: Das Viertel trägt die Farben des Roten Hammers.</summary>
        public bool Ours;
        public bool Selectable;
        /// <summary>Warum das Viertel nicht wählbar ist, oder ein Hinweis zum wählbaren.</summary>
        public string Note = "";
        /// <summary>Gerade erobert und noch nicht gezeigt: Der Plan spielt einmal den Übergang.</summary>
        public bool Fresh;
    }

    /// <summary>
    /// Zwischenschicht zwischen Stadtplan und Kampagne, die einzige Stelle, die weiß, woher die Viertel kommen.
    /// Bis data/stadtplan.json und die Abfragen in Campaign da sind, gibt es fünf Viertel, von denen nur das erste einen Einsatz hat:
    /// den aus den Missionsdaten. Erobert ist es, wenn dieser Einsatz gewonnen wurde. Namen der übrigen Viertel sind Platzhalter.
    /// </summary>
    public sealed class CityPlanSource
    {
        private static readonly string[] PlaceholderNames = { "Mietskasernen", "Hafen", "Werksgelände", "Kontorviertel", "Rathausviertel" };
        private const string FirstKind = "Diebstahl";

        private readonly Main _game;
        // Eroberte Viertel, die der Plan schon in Rot gezeigt hat. Gilt für die laufende Sitzung.
        private readonly HashSet<string> _shown = new();

        public CityPlanSource(Main game) => _game = game;

        /// <summary>Beim Öffnen einer Akte: Was schon erobert ist, bekommt keinen Übergang mehr.</summary>
        public void Reset()
        {
            _shown.Clear();
            foreach (var d in Read().Where(d => d.Ours))
            {
                _shown.Add(d.Id);
            }
        }

        public void MarkShown(string id) => _shown.Add(id);

        public List<District> Read()
        {
            var campaign = _game.Campaign;
            var mission = _game.Mission;
            bool won = campaign.Records.TryGetValue(_game.MissionKey, out var record) && record.Won;
            var list = new List<District>();
            for (int i = 0; i < PlaceholderNames.Length; i++)
            {
                var d = new District { Id = $"viertel_{i + 1}", Name = PlaceholderNames[i] };
                if (i == 0)
                {
                    d.Kind = FirstKind;
                    d.Title = mission.Title;
                    d.Text = mission.Briefing;
                    d.Reward = $"{Campaign.Reward(MissionRating.Loud)} bis {Campaign.Reward(MissionRating.Unseen)}, je leiser, desto mehr";
                    d.Ours = won;
                    d.Selectable = true;
                    d.Fresh = won && !_shown.Contains(d.Id);
                    d.Note = won ? "Erobert. Bis neue Aufträge kommen, lässt sich dieser Einsatz noch einmal spielen." : mission.Place;
                }
                else
                {
                    d.Note = "Gesperrt: Für dieses Viertel hat Abteilung K noch keinen Auftrag.";
                }
                list.Add(d);
            }
            return list;
        }
    }

    /// <summary>
    /// Stadtplan zur Einsatzwahl vor jedem Einsatz: links der vergilbte Plan mit den fünf Vierteln, rechts die Akte zum gewählten Viertel.
    /// Viertel der Brandt-Werke und des Roten Hammers tragen ihre Farbe, das gewählte pulsiert ruhig. Ein frisch erobertes Viertel
    /// färbt sich beim ersten Anzeigen um und bekommt seinen Stempel. Die Grundkarte und je Viertel eine Maske kommen aus
    /// game/assets/stadtplan (plan.png, viertel_1.png …), solange sie fehlen, zeichnet der Plan Platzhalterflächen.
    /// </summary>
    public partial class CityMap : PanelContainer
    {
        /// <summary>Farbe der Viertel in der Hand der Brandt-Werke. Die endgültigen Farben liefert Story, nur hier ändern.</summary>
        public static readonly Color BrandtColor = new(0.14f, 0.27f, 0.47f);

        /// <summary>Farbe der Viertel, die der Rote Hammer erobert hat.</summary>
        public static readonly Color HammerColor = new(0.62f, 0.13f, 0.1f);

        private const string GoText = "Zur Waffenkammer";
        private const string BackText = "Zurück zum Bericht";

        public Main Game = null!;

        private List<District> _districts = new();
        private readonly List<Button> _buttons = new();
        private PlanView _plan = null!;
        private Label _name = null!, _kind = null!, _text = null!, _reward = null!, _note = null!;
        private Button _go = null!, _back = null!;
        private int _selected;
        private bool _fromResult;

        /// <summary>Für den Prüflauf: gewähltes Viertel und wem jedes gehört.</summary>
        public string State => !Visible ? "" : $"{_districts[_selected].Id}: " + string.Join(" ", _districts.Select(d => d.Ours ? "hammer" : "brandt"));

        public override void _Ready()
        {
            Visible = false;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            OffsetLeft = 24;
            OffsetTop = 24;
            OffsetRight = -24;
            OffsetBottom = -24;
            AddThemeStyleboxOverride("panel", PaperUi.PaperBox(PaperUi.PaperColor));
            var body = new VBoxContainer();
            body.AddThemeConstantOverride("separation", 8);
            AddChild(body);

            var head = new HBoxContainer();
            var title = PaperUi.Text("ROTER HAMMER   STADTPLAN", 14, PaperUi.StampRed, PaperUi.Serif);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(title);
            head.AddChild(PaperUi.Text("Messingstadt, November 1927", 18, PaperUi.Ink, PaperUi.Serif));
            body.AddChild(head);
            body.AddChild(Rule(2, PaperUi.Ink));

            var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            columns.AddThemeConstantOverride("separation", 22);
            body.AddChild(columns);
            _plan = new PlanView { Map = this, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            columns.AddChild(_plan);
            columns.AddChild(new ColorRect { CustomMinimumSize = new Vector2(1, 0), Color = PaperUi.PaperDark, MouseFilter = MouseFilterEnum.Ignore });

            var file = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
            file.AddThemeConstantOverride("separation", 5);
            columns.AddChild(file);
            file.AddChild(PaperUi.Text("Viertel", 20, PaperUi.Ink, PaperUi.Serif));
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                var button = new Button { Alignment = HorizontalAlignment.Left };
                PaperUi.InkButton(button);
                button.AddThemeFontSizeOverride("font_size", 15);
                // Die Wahl wandert mit den Pfeiltasten durch die Viertel, Enter nimmt das Viertel.
                button.FocusEntered += () => Select(index, false);
                button.Pressed += () =>
                {
                    Select(index, false);
                    Confirm();
                };
                file.AddChild(button);
                _buttons.Add(button);
            }
            file.AddChild(Rule(1, PaperUi.PaperDark));
            _name = PaperUi.Text("", 24, PaperUi.Ink, PaperUi.Serif);
            file.AddChild(_name);
            _kind = PaperUi.Text("", 15, PaperUi.StampRed, PaperUi.Typewriter);
            file.AddChild(_kind);
            _text = Wrapped(15);
            file.AddChild(_text);
            _reward = Wrapped(15);
            file.AddChild(_reward);
            _note = Wrapped(14);
            _note.AddThemeColorOverride("font_color", new Color(PaperUi.Ink, 0.75f));
            file.AddChild(_note);

            body.AddChild(Rule(1, PaperUi.PaperDark));
            var foot = new HBoxContainer();
            foot.AddThemeConstantOverride("separation", 12);
            _back = FootButton(BackText, () => Back());
            foot.AddChild(_back);
            var legend = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
            legend.AddThemeConstantOverride("separation", 8);
            legend.AddChild(new ColorRect { CustomMinimumSize = new Vector2(18, 0), Color = BrandtColor });
            legend.AddChild(PaperUi.Text("Brandt-Werke   ", 14, PaperUi.Ink, PaperUi.Typewriter));
            legend.AddChild(new ColorRect { CustomMinimumSize = new Vector2(18, 0), Color = HammerColor });
            legend.AddChild(PaperUi.Text("Roter Hammer", 14, PaperUi.Ink, PaperUi.Typewriter));
            foot.AddChild(legend);
            _go = FootButton(GoText, Confirm);
            foot.AddChild(_go);
            body.AddChild(foot);
        }

        /// <summary>Öffnet den Plan. Mit Bericht führen Esc und "Zurück" auf den Ergebnis-Bildschirm.</summary>
        public void Open(bool fromResult)
        {
            _fromResult = fromResult;
            _districts = Game.Plan.Read();
            _back.Visible = fromResult;
            for (int i = 0; i < _buttons.Count; i++)
            {
                var d = _districts[i];
                _buttons[i].Text = $"{i + 1}  {d.Name}{(d.Ours ? "   erobert" : d.Selectable ? "" : "   gesperrt")}";
                _buttons[i].AddThemeColorOverride("font_color", d.Selectable ? PaperUi.PaperColor : PaperUi.PaperDark);
            }
            Visible = true;
            _plan.Show(_districts);
            int first = Math.Max(0, _districts.FindIndex(d => d.Selectable));
            Select(first, true);
        }

        public void Close() => Visible = false;

        /// <summary>Nach der Pausenmappe bekommt der Plan die Tasten zurück.</summary>
        public void Refocus() => _buttons[_selected].GrabFocus();

        /// <summary>Esc: zurück zum Bericht, wenn es einen gibt. Sonst bleibt der Plan offen und Esc öffnet die Pausenmappe.</summary>
        public bool Back()
        {
            if (!_fromResult)
            {
                return false;
            }
            Close();
            Game.CityMapBack();
            return true;
        }

        /// <summary>Klick auf den Plan oder Wahl in der Liste.</summary>
        public void Select(int index, bool focus)
        {
            _selected = index;
            var d = _districts[index];
            _name.Text = d.Name;
            _kind.Text = d.Kind.Length > 0 ? $"{d.Kind}: {d.Title}" : "Kein Auftrag";
            _text.Text = d.Text;
            _text.Visible = d.Text.Length > 0;
            _reward.Text = d.Reward.Length > 0 ? $"Belohnung: {d.Reward}" : "";
            _reward.Visible = d.Reward.Length > 0;
            _note.Text = d.Note;
            _go.Disabled = !d.Selectable;
            _plan.Selected = index;
            if (focus || !_buttons[index].HasFocus())
            {
                _buttons[index].GrabFocus();
            }
        }

        private void Confirm()
        {
            var d = _districts[_selected];
            if (!d.Selectable)
            {
                return;
            }
            Game.Sound.PlayEverywhere("menu_select", -14f);
            Close();
            Game.CityMapChosen();
        }

        private Button FootButton(string text, Action act)
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(220, 0) };
            PaperUi.InkButton(button);
            button.AddThemeFontSizeOverride("font_size", 15);
            button.Pressed += act;
            return button;
        }

        private static ColorRect Rule(int height, Color color) =>
            new() { CustomMinimumSize = new Vector2(0, height), Color = color, MouseFilter = MouseFilterEnum.Ignore };

        private static Label Wrapped(int size)
        {
            var label = PaperUi.Text("", size, PaperUi.Ink, PaperUi.Typewriter);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(380, 0);
            return label;
        }

        /// <summary>Der Plan selbst: Grundkarte, darüber je Viertel eine eingefärbte Maske.</summary>
        private sealed partial class PlanView : Control
        {
            private const string Folder = "res://assets/stadtplan/";
            private const float PulseSeconds = 1.8f;
            private const float TintAlpha = 0.55f;
            private const float PulseDepth = 0.2f;
            private const float WipeSeconds = 1.2f;
            private const float StampSeconds = 0.35f;
            private const string StampText = "EROBERT";

            public CityMap Map = null!;
            public int Selected;

            private Texture2D _base = null!;
            private readonly List<Texture2D> _masks = new();
            private readonly List<Image> _maskImages = new();
            private readonly List<Vector2> _centers = new();
            private List<District> _districts = new();
            private float _time;
            // Sekunden seit Beginn des Übergangs je Viertel, -1 ohne Übergang.
            private readonly List<float> _wipe = new();

            public override void _Ready()
            {
                MouseFilter = MouseFilterEnum.Stop;
                TextureFilter = TextureFilterEnum.Linear;
                if (!LoadFiles())
                {
                    BuildPlaceholder();
                }
                foreach (var image in _maskImages)
                {
                    _centers.Add(CenterOf(image));
                }
            }

            public void Show(List<District> districts)
            {
                _districts = districts;
                _wipe.Clear();
                foreach (var d in districts)
                {
                    _wipe.Add(d.Fresh ? 0f : -1f);
                    if (d.Fresh)
                    {
                        Map.Game.Plan.MarkShown(d.Id);
                    }
                }
            }

            private bool LoadFiles()
            {
                string plan = ProjectSettings.GlobalizePath(Folder + "plan.png");
                if (!File.Exists(plan))
                {
                    return false;
                }
                var images = new List<Image>();
                for (int i = 1; i <= 5; i++)
                {
                    string mask = ProjectSettings.GlobalizePath($"{Folder}viertel_{i}.png");
                    if (!File.Exists(mask))
                    {
                        return false;
                    }
                    images.Add(Image.LoadFromFile(mask));
                }
                _base = ImageTexture.CreateFromImage(Image.LoadFromFile(plan));
                foreach (var image in images)
                {
                    _maskImages.Add(image);
                    _masks.Add(ImageTexture.CreateFromImage(image));
                }
                return true;
            }

            /// <summary>Fünf einfache Flächen, bis die Karte der Grafik da ist.</summary>
            private void BuildPlaceholder()
            {
                const int w = 480, h = 340;
                var shapes = new[]
                {
                    new[] { new Vector2(20, 180), new Vector2(170, 150), new Vector2(200, 320), new Vector2(20, 320) },
                    new[] { new Vector2(20, 20), new Vector2(190, 20), new Vector2(170, 150), new Vector2(20, 180) },
                    new[] { new Vector2(190, 20), new Vector2(330, 20), new Vector2(310, 170), new Vector2(170, 150) },
                    new[] { new Vector2(330, 20), new Vector2(460, 20), new Vector2(460, 200), new Vector2(310, 170) },
                    new[] { new Vector2(200, 320), new Vector2(170, 150), new Vector2(310, 170), new Vector2(460, 200), new Vector2(460, 320) },
                };
                var owner = new int[w, h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        owner[x, y] = Array.FindIndex(shapes, s => Geometry2D.IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), s));
                    }
                }
                var plan = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
                var masks = shapes.Select(_ => Image.CreateEmpty(w, h, false, Image.Format.Rgba8)).ToList();
                var paper = PaperUi.PaperColor.Lightened(0.12f);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int o = owner[x, y];
                        bool edge = o >= 0 && (x + 2 >= w || y + 2 >= h || owner[x + 2, y] != o || owner[x, y + 2] != o || x < 2 || y < 2 || owner[x - 2, y] != o || owner[x, y - 2] != o);
                        // Straßenraster, damit die Fläche nach Plan aussieht.
                        bool street = o >= 0 && ((x + y / 3) % 34 == 0 || (y + x / 5) % 29 == 0);
                        plan.SetPixel(x, y, o < 0 ? PaperUi.PaperDark : edge ? PaperUi.Ink : street ? paper.Darkened(0.18f) : paper);
                        if (o >= 0 && !edge)
                        {
                            masks[o].SetPixel(x, y, Colors.White);
                        }
                    }
                }
                _base = ImageTexture.CreateFromImage(plan);
                foreach (var image in masks)
                {
                    _maskImages.Add(image);
                    _masks.Add(ImageTexture.CreateFromImage(image));
                }
            }

            /// <summary>Schwerpunkt der Maske als Anteil der Karte, für den Stempel.</summary>
            private static Vector2 CenterOf(Image mask)
            {
                var sum = Vector2.Zero;
                int count = 0;
                for (int y = 0; y < mask.GetHeight(); y += 4)
                {
                    for (int x = 0; x < mask.GetWidth(); x += 4)
                    {
                        if (mask.GetPixel(x, y).A > 0.5f)
                        {
                            sum += new Vector2(x, y);
                            count++;
                        }
                    }
                }
                return count == 0 ? new Vector2(0.5f, 0.5f) : sum / count / new Vector2(mask.GetWidth(), mask.GetHeight());
            }

            /// <summary>Wo die Karte in der Fläche liegt: so groß wie möglich, Seitenverhältnis bleibt.</summary>
            private Rect2 MapRect()
            {
                var size = _base.GetSize();
                float scale = Mathf.Min(Size.X / size.X, Size.Y / size.Y);
                return new Rect2((Size - size * scale) / 2f, size * scale);
            }

            public override void _Process(double delta)
            {
                if (!IsVisibleInTree())
                {
                    return;
                }
                _time += (float)delta;
                for (int i = 0; i < _wipe.Count; i++)
                {
                    if (_wipe[i] >= 0f)
                    {
                        _wipe[i] += (float)delta;
                    }
                }
                QueueRedraw();
            }

            public override void _GuiInput(InputEvent e)
            {
                if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
                {
                    return;
                }
                var rect = MapRect();
                var at = (click.Position - rect.Position) / rect.Size;
                if (at.X < 0f || at.Y < 0f || at.X >= 1f || at.Y >= 1f)
                {
                    return;
                }
                for (int i = 0; i < _maskImages.Count; i++)
                {
                    var image = _maskImages[i];
                    if (image.GetPixel((int)(at.X * image.GetWidth()), (int)(at.Y * image.GetHeight())).A > 0.5f)
                    {
                        Map.Select(i, true);
                        AcceptEvent();
                        return;
                    }
                }
            }

            public override void _Draw()
            {
                var rect = MapRect();
                DrawRect(new Rect2(rect.Position + new Vector2(5, 6), rect.Size), new Color(0f, 0f, 0f, 0.35f));
                DrawTextureRect(_base, rect, false);
                // Ruhiges Pulsieren, kein Blinken: eine langsame Sinuswelle auf der Deckkraft.
                float pulse = 0.5f + 0.5f * Mathf.Sin(_time * Mathf.Tau / PulseSeconds);
                for (int i = 0; i < _masks.Count && i < _districts.Count; i++)
                {
                    var d = _districts[i];
                    float alpha = TintAlpha + (i == Selected ? PulseDepth * pulse : 0f);
                    float wipe = _wipe[i] < 0f ? 1f : Mathf.Clamp(_wipe[i] / WipeSeconds, 0f, 1f);
                    if (!d.Ours || wipe >= 1f)
                    {
                        DrawTextureRect(_masks[i], rect, false, new Color(d.Ours ? HammerColor : BrandtColor, alpha));
                    }
                    else
                    {
                        // Die neue Farbe läuft von links über das Viertel.
                        var size = _masks[i].GetSize();
                        DrawTextureRectRegion(_masks[i], new Rect2(rect.Position, new Vector2(rect.Size.X * wipe, rect.Size.Y)),
                            new Rect2(0, 0, size.X * wipe, size.Y), new Color(HammerColor, alpha));
                        DrawTextureRectRegion(_masks[i], new Rect2(rect.Position + new Vector2(rect.Size.X * wipe, 0), new Vector2(rect.Size.X * (1f - wipe), rect.Size.Y)),
                            new Rect2(size.X * wipe, 0, size.X * (1f - wipe), size.Y), new Color(BrandtColor, alpha));
                    }
                    var center = rect.Position + _centers[i] * rect.Size;
                    if (d.Ours && (_wipe[i] < 0f || _wipe[i] >= WipeSeconds))
                    {
                        // Der Stempel fällt nach dem Umfärben aufs Papier: erst groß, dann auf Maß.
                        float t = _wipe[i] < 0f ? 1f : Mathf.Clamp((_wipe[i] - WipeSeconds) / StampSeconds, 0f, 1f);
                        DrawStamp(center, Mathf.Lerp(1.7f, 1f, t), t);
                    }
                    else if (!d.Ours)
                    {
                        string number = (i + 1).ToString();
                        DrawString(PaperUi.Serif, center + new Vector2(-6, 8), number, HorizontalAlignment.Left, -1, 24, new Color(PaperUi.Ink, d.Selectable ? 0.9f : 0.45f));
                    }
                }
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                DrawRect(rect, PaperUi.Ink, false, 2f);
            }

            private void DrawStamp(Vector2 center, float scale, float alpha)
            {
                const int size = 20;
                float width = PaperUi.Serif.GetStringSize(StampText, HorizontalAlignment.Left, -1, size).X;
                DrawSetTransform(center, Mathf.DegToRad(-11f), new Vector2(scale, scale));
                var ink = new Color(PaperUi.PaperColor.Lightened(0.3f), alpha);
                DrawRect(new Rect2(-width / 2f - 6, -size / 2f - 5, width + 12, size + 8), ink, false, 2f);
                DrawString(PaperUi.Serif, new Vector2(-width / 2f, size / 2f - 3), StampText, HorizontalAlignment.Left, -1, size, ink);
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
        }
    }
}
