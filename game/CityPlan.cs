using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>Ein Viertel auf dem Stadtplan, so wie der Bildschirm es braucht.</summary>
    public sealed class District
    {
        public string Id = "";
        public string Name = "";
        /// <summary>Auftragsart, etwa "Holen".</summary>
        public string Kind = "";
        public string Title = "";
        public string Time = "";
        public string Text = "";
        public string Stakes = "";
        public string Reward = "";
        public DistrictState State;
        /// <summary>Erobert: Das Viertel trägt die Farben des Roten Hammers.</summary>
        public bool Ours;
        public bool Selectable;
        /// <summary>Warum das Viertel nicht wählbar ist, oder ein Hinweis zum wählbaren.</summary>
        public string Note = "";
        /// <summary>Gerade erobert und noch nicht gezeigt: Der Plan spielt einmal den Übergang.</summary>
        public bool Fresh;
    }

    /// <summary>
    /// Zwischenschicht zwischen Stadtplan und Kampagne, die einzige Stelle, die weiß, woher die Viertel kommen:
    /// aus Main.City (data/stadtplan.json) und den Abfragen State, Owner, CanSelect und TrySelect der Kampagne.
    /// Den Stadtplan lädt Main, hier wird er nur gelesen.
    /// </summary>
    public sealed class CityPlanSource
    {
        // Texte der Story, docs/story/stadtplan-vorlage.json unter texte.
        public const string NoMissionText = "Für dieses Viertel liegt noch kein Auftrag vor. K. meldet sich.";
        public const string TakenText = "Hammer hält. Bis neue Aufträge kommen, lässt sich dieser Einsatz noch einmal spielen.";

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

        /// <summary>Kennung des Viertels, das die Kampagne gewählt hat, leer ohne spielbares Viertel.</summary>
        public string SelectedId => _game.Campaign.Selected(_game.City)?.Id ?? "";

        /// <summary>Wählt das Viertel für den nächsten Einsatz und speichert. Falsch, wenn es gesperrt ist.</summary>
        public bool Choose(string id)
        {
            if (!_game.Campaign.TrySelect(_game.City, id))
            {
                return false;
            }
            _game.CampaignChanged();
            return true;
        }

        public List<District> Read()
        {
            var campaign = _game.Campaign;
            var city = _game.City;
            var list = new List<District>();
            foreach (var data in city.Districts)
            {
                var state = campaign.State(city, data.Id);
                bool ours = campaign.Owner(data.Id) == DistrictOwner.RoterHammer;
                var d = new District
                {
                    Id = data.Id, Name = data.Name, Kind = data.Kind, Title = data.Title, State = state, Ours = ours,
                    Selectable = campaign.CanSelect(city, data.Id), Fresh = ours && !_shown.Contains(data.Id),
                };
                if (d.Selectable)
                {
                    d.Time = data.Time;
                    d.Text = data.Briefing;
                    d.Stakes = data.Stakes;
                    d.Reward = $"{Campaign.Reward(MissionRating.Loud, data.Reward)} bis {Campaign.Reward(MissionRating.Unseen, data.Reward)}, je leiser, desto mehr";
                }
                d.Note = state switch
                {
                    DistrictState.Taken => TakenText,
                    DistrictState.NoMission => NoMissionText,
                    DistrictState.Locked => data.Locked.Length > 0 ? data.Locked : LockedFallback(city, data),
                    _ => data.Character,
                };
                list.Add(d);
            }
            return list;
        }

        /// <summary>Falls der fertige Satz in den Daten fehlt: ohne Artikel, damit die Grammatik stimmt.</summary>
        private static string LockedFallback(CityMapData city, DistrictData data) =>
            $"Kein Weg hinein. Erst halten: {string.Join(", ", data.Requires.Select(id => city.Get(id)?.Name ?? id))}.";
    }

    /// <summary>
    /// Stadtplan zur Einsatzwahl vor jedem Einsatz: links der vergilbte Plan mit den fünf Vierteln, rechts die Akte zum gewählten Viertel.
    /// Viertel der Brandt-Werke sind grün schraffiert, eroberte tragen das Rot des Hammers als volle Fläche mit Stempel: So erkennt man
    /// den Besitzer auch ohne Farbe. Das gewählte Viertel pulsiert ruhig, ein frisch erobertes färbt sich beim ersten Anzeigen um.
    /// Grundkarte, Masken und Maße kommen aus game/assets/stadtplan (plan.png, viertel_1.png …, plan.json).
    /// Solange sie fehlen, zeichnet der Plan Platzhalterflächen.
    /// </summary>
    public partial class CityPlanScreen : PanelContainer
    {
        // Farben der Story, docs/story/stadtplan-vorlage.json unter farben. Nur hier ändern.
        /// <summary>Fläche der Viertel in der Hand der Brandt-Werke: Werksgrün.</summary>
        public static readonly Color BrandtColor = new("4C7064");
        public static readonly Color BrandtEdge = new("36544E");
        public static readonly Color BrandtInk = new("DED0B0");

        /// <summary>Fläche und Stempel der Viertel, die der Rote Hammer hält.</summary>
        public static readonly Color HammerColor = new("8C1A14");
        public static readonly Color HammerEdge = new("5E120E");
        public static readonly Color HammerInk = new("A03832");

        private const string Heading = "ROTER HAMMER   STADTPLAN";
        private const string Subline = "Stand der Viertel, November 1927";
        public const string GoText = "Einsatz annehmen";
        private const string BackText = "Zurück zum Bericht";
        private const string StampText = "HAMMER HÄLT";

        public Main Game = null!;

        private List<District> _districts = new();
        private readonly List<Button> _buttons = new();
        private PlanView _plan = null!;
        private VBoxContainer _list = null!;
        private Label _name = null!, _kind = null!, _time = null!, _text = null!, _stakes = null!, _reward = null!, _note = null!;
        private Button _go = null!, _back = null!;
        private int _selected;
        private bool _fromResult;

        /// <summary>Für den Prüflauf: gewähltes Viertel und wem jedes gehört.</summary>
        public string State => !Visible || _districts.Count == 0 ? ""
            : $"{_districts[_selected].Id}: " + string.Join(" ", _districts.Select(d => d.Ours ? "hammer" : "brandt"));

        /// <summary>Für den Prüflauf: woher die Karte kommt.</summary>
        public string MapSource => _plan.FromFiles ? "dateien" : "platzhalter";

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
            var title = PaperUi.Text(Heading, 14, PaperUi.StampRed, PaperUi.Serif);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(title);
            head.AddChild(PaperUi.Text(Subline, 18, PaperUi.Ink, PaperUi.Serif));
            body.AddChild(head);
            body.AddChild(Rule(2, PaperUi.Ink));

            var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            columns.AddThemeConstantOverride("separation", 22);
            body.AddChild(columns);
            _plan = new PlanView { Screen = this, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            columns.AddChild(_plan);
            columns.AddChild(new ColorRect { CustomMinimumSize = new Vector2(1, 0), Color = PaperUi.PaperDark, MouseFilter = MouseFilterEnum.Ignore });

            var file = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
            file.AddThemeConstantOverride("separation", 5);
            columns.AddChild(file);
            file.AddChild(PaperUi.Text("Viertel", 20, PaperUi.Ink, PaperUi.Serif));
            _list = new VBoxContainer();
            _list.AddThemeConstantOverride("separation", 5);
            file.AddChild(_list);
            file.AddChild(Rule(1, PaperUi.PaperDark));
            _name = PaperUi.Text("", 24, PaperUi.Ink, PaperUi.Serif);
            file.AddChild(_name);
            _kind = PaperUi.Text("", 15, PaperUi.StampRed, PaperUi.Typewriter);
            file.AddChild(_kind);
            _time = PaperUi.Text("", 13, new Color(PaperUi.Ink, 0.75f), PaperUi.Typewriter);
            file.AddChild(_time);
            _text = Wrapped(15);
            file.AddChild(_text);
            _stakes = Wrapped(14);
            file.AddChild(_stakes);
            _reward = Wrapped(15);
            file.AddChild(_reward);
            _note = Wrapped(13);
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
            legend.AddChild(PaperUi.Text("Brandt-Werke, schraffiert   ", 14, PaperUi.Ink, PaperUi.Typewriter));
            legend.AddChild(new ColorRect { CustomMinimumSize = new Vector2(18, 0), Color = HammerColor });
            legend.AddChild(PaperUi.Text("Roter Hammer, volle Fläche", 14, PaperUi.Ink, PaperUi.Typewriter));
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
            BuildList();
            Visible = true;
            _plan.Show(_districts);
            if (_districts.Count == 0)
            {
                _go.Disabled = true;
                return;
            }
            // Vorgewählt ist das Viertel der Kampagne, sonst das erste.
            string chosen = Game.Plan.SelectedId;
            Select(Math.Max(0, _districts.FindIndex(d => d.Id == chosen)), true);
        }

        private void BuildList()
        {
            foreach (var child in _list.GetChildren())
            {
                _list.RemoveChild(child);
                child.QueueFree();
            }
            _buttons.Clear();
            for (int i = 0; i < _districts.Count; i++)
            {
                int index = i;
                var d = _districts[i];
                string mark = d.State switch
                {
                    DistrictState.Taken => "   Hammer hält",
                    DistrictState.Open => "",
                    DistrictState.NoMission => "   kein Auftrag",
                    _ => "   gesperrt",
                };
                var button = new Button { Text = $"{i + 1}  {d.Name}{mark}", Alignment = HorizontalAlignment.Left };
                PaperUi.InkButton(button);
                button.AddThemeFontSizeOverride("font_size", 15);
                button.AddThemeColorOverride("font_color", d.Selectable ? PaperUi.PaperColor : PaperUi.PaperDark);
                // Die Wahl wandert mit den Pfeiltasten durch die Viertel, Enter nimmt das Viertel.
                button.FocusEntered += () => Select(index, false);
                button.Pressed += () =>
                {
                    Select(index, false);
                    Confirm();
                };
                _list.AddChild(button);
                _buttons.Add(button);
            }
        }

        public void Close() => Visible = false;

        /// <summary>Nach der Pausenmappe bekommt der Plan die Tasten zurück.</summary>
        public void Refocus()
        {
            if (_selected < _buttons.Count)
            {
                _buttons[_selected].GrabFocus();
            }
        }

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
            if (index < 0 || index >= _districts.Count)
            {
                return;
            }
            _selected = index;
            var d = _districts[index];
            _name.Text = d.Name;
            _kind.Text = d.Selectable ? $"{d.Kind}: {d.Title}" : d.State == DistrictState.NoMission ? "Kein Auftrag" : "Gesperrt";
            Show(_time, d.Time);
            Show(_text, d.Text);
            Show(_stakes, d.Stakes);
            Show(_reward, d.Reward.Length > 0 ? $"Belohnung: {d.Reward}" : "");
            _note.Text = d.Note;
            _go.Disabled = !d.Selectable;
            _plan.Selected = index;
            if (focus || !_buttons[index].HasFocus())
            {
                _buttons[index].GrabFocus();
            }
        }

        private static void Show(Label label, string text)
        {
            label.Text = text;
            label.Visible = text.Length > 0;
        }

        private void Confirm()
        {
            if (_districts.Count == 0 || !_districts[_selected].Selectable || !Game.Plan.Choose(_districts[_selected].Id))
            {
                return;
            }
            Game.Sound.PlayEverywhere("menu_select", -14f);
            Close();
            Game.CityMapChosen();
        }

        private static Button FootButton(string text, Action act)
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

        /// <summary>Der Plan selbst: Grundkarte, darüber je Viertel eine eingefärbte Maske, obenauf Namen, Umriss und Stempel.</summary>
        private sealed partial class PlanView : Control
        {
            private const string Folder = "res://assets/stadtplan/";
            private const float PulseSeconds = 1.8f;
            private const float TintAlpha = 0.6f;
            private const float PulseDepth = 0.18f;
            private const float WipeSeconds = 1.2f;
            private const float StampSeconds = 0.35f;

            // Brandt-Viertel schraffiert, Hammer-Viertel als volle Fläche. Beim Übergang läuft die volle Fläche von links über das Viertel.
            private const string MaskShader = @"
shader_type canvas_item;
uniform vec4 brandt : source_color;
uniform vec4 hammer : source_color;
uniform float wipe = 0.0;
uniform float strength = 0.6;
uniform float stripe_px = 9.0;
void fragment() {
    float mask = texture(TEXTURE, UV).a;
    float stripe = step(0.5, fract((FRAGCOORD.x + FRAGCOORD.y) / stripe_px));
    vec4 ours = vec4(hammer.rgb, strength);
    vec4 theirs = vec4(brandt.rgb, strength * mix(0.3, 1.0, stripe));
    vec4 c = UV.x < wipe ? ours : theirs;
    COLOR = vec4(c.rgb, c.a * mask);
}";

            /// <summary>Was der Plan je Viertel aus plan.json braucht, in Bildpunkten der Grundkarte.</summary>
            private sealed class Shape
            {
                public Vector2 Label, Stamp;
                public Vector2[] Polygon = Array.Empty<Vector2>();
            }

            public CityPlanScreen Screen = null!;
            public int Selected;
            public bool FromFiles { get; private set; }

            private Texture2D _base = null!;
            private Vector2 _size;
            private readonly List<Shape> _shapes = new();
            private readonly List<TextureRect> _masks = new();
            private readonly List<ShaderMaterial> _looks = new();
            private Control _top = null!;
            private List<District> _districts = new();
            private float _time;
            // Sekunden seit Beginn des Übergangs je Viertel, -1 ohne Übergang.
            private readonly List<float> _wipe = new();

            public override void _Ready()
            {
                MouseFilter = MouseFilterEnum.Stop;
                TextureFilter = TextureFilterEnum.Linear;
                var masks = LoadFiles();
                FromFiles = masks != null;
                masks ??= BuildPlaceholder();
                var shader = new Shader { Code = MaskShader };
                foreach (var mask in masks)
                {
                    var look = new ShaderMaterial { Shader = shader };
                    look.SetShaderParameter("brandt", BrandtColor);
                    look.SetShaderParameter("hammer", HammerColor);
                    var rect = new TextureRect
                    {
                        Texture = mask, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                        Material = look, MouseFilter = MouseFilterEnum.Ignore,
                    };
                    AddChild(rect);
                    _masks.Add(rect);
                    _looks.Add(look);
                }
                // Namen, Umriss und Stempel liegen über den Masken.
                _top = new Control { MouseFilter = MouseFilterEnum.Ignore };
                _top.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                _top.Draw += DrawTop;
                AddChild(_top);
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
                        Screen.Game.Plan.MarkShown(d.Id);
                    }
                }
            }

            /// <summary>
            /// Lädt Grundkarte, Masken und plan.json der Grafik. Die Kennungen dort sind Zahlen 1 bis 5, die Viertel der Kampagne heißen
            /// viertel_1 bis viertel_5 und stehen in dieser Reihenfolge in der Liste: Nummer n ist das Viertel an Stelle n - 1.
            /// </summary>
            private List<Texture2D>? LoadFiles()
            {
                string plan = ProjectSettings.GlobalizePath(Folder + "plan.png");
                string json = ProjectSettings.GlobalizePath(Folder + "plan.json");
                if (!File.Exists(plan) || !File.Exists(json))
                {
                    return null;
                }
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(json));
                    var entries = doc.RootElement.GetProperty("districts").EnumerateArray().OrderBy(e => e.GetProperty("id").GetInt32()).ToList();
                    var shapes = new List<Shape>();
                    var masks = new List<Texture2D>();
                    foreach (var entry in entries)
                    {
                        string file = ProjectSettings.GlobalizePath(Folder + entry.GetProperty("mask").GetString());
                        if (!File.Exists(file))
                        {
                            return null;
                        }
                        var image = Image.LoadFromFile(file);
                        // Weiß mit Alpha: als Graustufe mit Alpha kostet die Maske halb so viel Speicher.
                        image.Convert(Image.Format.La8);
                        masks.Add(ImageTexture.CreateFromImage(image));
                        shapes.Add(new Shape
                        {
                            Label = Point(entry.GetProperty("label")), Stamp = Point(entry.GetProperty("stamp")),
                            Polygon = entry.GetProperty("polygon").EnumerateArray().Select(Point).ToArray(),
                        });
                    }
                    _base = ImageTexture.CreateFromImage(Image.LoadFromFile(plan));
                    _size = _base.GetSize();
                    _shapes.AddRange(shapes);
                    return masks;
                }
                catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
                {
                    GD.PushError($"Stadtplan: plan.json unlesbar, Platzhalter stattdessen. {e.Message}");
                    _shapes.Clear();
                    return null;
                }
            }

            private static Vector2 Point(JsonElement pair) => new(pair[0].GetSingle(), pair[1].GetSingle());

            /// <summary>Fünf einfache Flächen, bis die Karte der Grafik da ist.</summary>
            private List<Texture2D> BuildPlaceholder()
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
                var images = shapes.Select(_ => Image.CreateEmpty(w, h, false, Image.Format.La8)).ToList();
                var paper = PaperUi.PaperColor.Lightened(0.12f);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int o = owner[x, y];
                        // Straßenraster, damit die Fläche nach Plan aussieht.
                        bool street = o >= 0 && ((x + y / 3) % 34 == 0 || (y + x / 5) % 29 == 0);
                        plan.SetPixel(x, y, o < 0 ? PaperUi.PaperDark : street ? paper.Darkened(0.18f) : paper);
                        if (o >= 0)
                        {
                            images[o].SetPixel(x, y, Colors.White);
                        }
                    }
                }
                _base = ImageTexture.CreateFromImage(plan);
                _size = new Vector2(w, h);
                foreach (var shape in shapes)
                {
                    var middle = shape.Aggregate(Vector2.Zero, (sum, p) => sum + p) / shape.Length;
                    _shapes.Add(new Shape { Polygon = shape, Label = middle + new Vector2(0, -16), Stamp = middle + new Vector2(0, 18) });
                }
                return images.Select(i => (Texture2D)ImageTexture.CreateFromImage(i)).ToList();
            }

            /// <summary>Wo die Karte in der Fläche liegt: so groß wie möglich, Seitenverhältnis bleibt, die echte Karte nie größer als 1:1.</summary>
            private Rect2 MapRect()
            {
                float scale = Mathf.Min(Size.X / _size.X, Size.Y / _size.Y);
                if (FromFiles)
                {
                    scale = Mathf.Min(scale, 1f);
                }
                return new Rect2((Size - _size * scale) / 2f, _size * scale);
            }

            public override void _Process(double delta)
            {
                if (!IsVisibleInTree())
                {
                    return;
                }
                _time += (float)delta;
                var rect = MapRect();
                // Ruhiges Pulsieren, kein Blinken: eine langsame Sinuswelle auf der Deckkraft.
                float pulse = 0.5f + 0.5f * Mathf.Sin(_time * Mathf.Tau / PulseSeconds);
                for (int i = 0; i < _masks.Count; i++)
                {
                    _masks[i].Position = rect.Position;
                    _masks[i].Size = rect.Size;
                    _masks[i].Visible = i < _districts.Count;
                    if (i >= _districts.Count)
                    {
                        continue;
                    }
                    if (_wipe[i] >= 0f)
                    {
                        _wipe[i] += (float)delta;
                    }
                    bool ours = _districts[i].Ours;
                    float wipe = !ours ? 0f : _wipe[i] < 0f ? 1f : Mathf.Clamp(_wipe[i] / WipeSeconds, 0f, 1f);
                    _looks[i].SetShaderParameter("wipe", wipe);
                    _looks[i].SetShaderParameter("strength", TintAlpha + (i == Selected ? PulseDepth * pulse : 0f));
                }
                QueueRedraw();
                _top.QueueRedraw();
            }

            public override void _GuiInput(InputEvent e)
            {
                if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
                {
                    return;
                }
                var rect = MapRect();
                var at = (click.Position - rect.Position) / rect.Size * _size;
                for (int i = 0; i < _shapes.Count && i < _districts.Count; i++)
                {
                    if (Geometry2D.IsPointInPolygon(at, _shapes[i].Polygon))
                    {
                        Screen.Select(i, true);
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
            }

            private void DrawTop()
            {
                var rect = MapRect();
                float scale = rect.Size.X / _size.X;
                float pulse = 0.5f + 0.5f * Mathf.Sin(_time * Mathf.Tau / PulseSeconds);
                for (int i = 0; i < _shapes.Count && i < _districts.Count; i++)
                {
                    var d = _districts[i];
                    var shape = _shapes[i];
                    var outline = shape.Polygon.Select(p => rect.Position + p * scale).Append(rect.Position + shape.Polygon[0] * scale).ToArray();
                    bool wiped = d.Ours && (_wipe[i] < 0f || _wipe[i] >= WipeSeconds);
                    _top.DrawPolyline(outline, wiped ? HammerEdge : BrandtEdge, 2f, true);
                    var ink = wiped ? BrandtInk : d.Selectable ? PaperUi.Ink : new Color(PaperUi.Ink, 0.6f);
                    const int size = 17;
                    float width = PaperUi.Serif.GetStringSize(d.Name, HorizontalAlignment.Left, -1, size).X;
                    var label = rect.Position + shape.Label * scale;
                    // Papierstreifen unter dem Namen, damit er auf Schraffur und Karte lesbar bleibt.
                    if (!wiped)
                    {
                        _top.DrawRect(new Rect2(label + new Vector2(-width / 2f - 5, -size / 2f - 3), new Vector2(width + 10, size + 7)), new Color(PaperUi.PaperColor, 0.85f));
                    }
                    _top.DrawString(PaperUi.Serif, label + new Vector2(-width / 2f, size / 2f), d.Name, HorizontalAlignment.Left, -1, size, ink);
                    if (wiped)
                    {
                        // Der Stempel fällt nach dem Umfärben aufs Papier: erst groß, dann auf Maß.
                        float t = _wipe[i] < 0f ? 1f : Mathf.Clamp((_wipe[i] - WipeSeconds) / StampSeconds, 0f, 1f);
                        DrawStamp(rect.Position + shape.Stamp * scale, Mathf.Lerp(1.7f, 1f, t), t);
                    }
                }
                _top.DrawRect(rect, PaperUi.Ink, false, 2f);
                if (Selected < _shapes.Count && Selected < _districts.Count)
                {
                    // Der Umriss des gewählten Viertels pulsiert mit, in heller Tinte. Zuletzt gezeichnet, damit ihn kein Nachbar überdeckt.
                    var chosen = _shapes[Selected].Polygon;
                    var ring = chosen.Select(p => rect.Position + p * scale).Append(rect.Position + chosen[0] * scale).ToArray();
                    _top.DrawPolyline(ring, new Color(BrandtInk, 0.55f + 0.4f * pulse), 3f, true);
                }
            }

            private void DrawStamp(Vector2 center, float scale, float alpha)
            {
                const int size = 18;
                float width = PaperUi.Serif.GetStringSize(StampText, HorizontalAlignment.Left, -1, size).X;
                _top.DrawSetTransform(center, Mathf.DegToRad(-9f), new Vector2(scale, scale));
                var ink = new Color(BrandtInk, alpha);
                _top.DrawRect(new Rect2(-width / 2f - 6, -size / 2f - 5, width + 12, size + 8), ink, false, 2f);
                _top.DrawString(PaperUi.Serif, new Vector2(-width / 2f, size / 2f - 3), StampText, HorizontalAlignment.Left, -1, size, ink);
                _top.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
        }
    }
}
