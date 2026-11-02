using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Hauptmenü, Pausenmappe, Einstellungen, Bedienung und Rückfragen: ein Blatt Papier mitten im Bild.
    /// Pfeiltasten wählen, Enter bestätigt, Esc geht eine Ebene zurück. Die Maus wählt dasselbe wie die Tasten.
    /// Solange ein Blatt offen ist, steht das Spiel still und nimmt keine Befehle an.
    /// </summary>
    public partial class Menus : CanvasLayer
    {
        private const int EntryFontSize = 17;
        private const float SheetWidth = 460f;
        private const float WideSheetWidth = 720f;
        private const float SliderWidth = 240f;
        private const double SliderStep = 0.05;
        private const float SampleSeconds = 0.15f;
        private static readonly Color Dim = new(0f, 0f, 0f, 0.5f);

        private sealed class Page
        {
            public string Name = "";
            public Action Build = null!;
            public int Focus;
            public Action? OnBack;
        }

        private readonly Stack<Page> _pages = new();
        private readonly List<Control> _focusable = new();
        private ColorRect _dim = null!;
        private PanelContainer _sheet = null!;
        private VBoxContainer _body = null!;
        private float _lastSample;

        public Main Game = null!;

        public bool IsOpen => _pages.Count > 0;
        public string ScreenName => IsOpen ? _pages.Peek().Name : "";

        public override void _Ready()
        {
            Layer = 5;
            _dim = new ColorRect { Color = Dim, MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
            AddChild(_dim);
            _dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            _dim.AddChild(center);
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _sheet = new PanelContainer();
            _sheet.AddThemeStyleboxOverride("panel", PaperUi.PaperBox(PaperUi.PaperColor));
            center.AddChild(_sheet);
            _body = new VBoxContainer();
            _body.AddThemeConstantOverride("separation", 8);
            _sheet.AddChild(_body);
        }

        public void ShowMain() => Open("Hauptmenü", BuildMain);

        public void ShowPause() => Open("Pausenmappe", BuildPause);

        public void CloseAll()
        {
            _pages.Clear();
            Clear();
            _dim.Visible = false;
            Game.MenusClosed();
        }

        /// <summary>R im Einsatz und "Einsatz abbrechen" führen zur selben Rückfrage.</summary>
        public void ConfirmAbandon() => AskAbandon("Einsatz abbrechen", "Abbrechen", Game.AbandonMission);

        /// <summary>Baut das offene Blatt neu auf, etwa wenn F11 oder M die Anzeige dort ändern.</summary>
        public void Refresh()
        {
            if (IsOpen)
            {
                Remember();
                Show(_pages.Peek());
            }
        }

        public override void _UnhandledInput(InputEvent e)
        {
            if (IsOpen && e.IsActionPressed("ui_cancel"))
            {
                GetViewport().SetInputAsHandled();
                Back();
            }
        }

        private void Open(string name, Action build, Action? onBack = null)
        {
            Remember();
            var page = new Page { Name = name, Build = build, OnBack = onBack };
            _pages.Push(page);
            Show(page);
        }

        /// <summary>Eine Ebene zurück. Das Hauptmenü ist die unterste Ebene und bleibt offen.</summary>
        private void Back()
        {
            if (_pages.Peek().Name == "Hauptmenü")
            {
                return;
            }
            var left = _pages.Pop();
            left.OnBack?.Invoke();
            if (_pages.Count == 0)
            {
                CloseAll();
                return;
            }
            Show(_pages.Peek());
        }

        private void Remember()
        {
            if (IsOpen)
            {
                _pages.Peek().Focus = Math.Max(0, _focusable.FindIndex(c => c.HasFocus()));
            }
        }

        private void Show(Page page)
        {
            Clear();
            _dim.Visible = true;
            page.Build();
            if (_focusable.Count > 0)
            {
                _focusable[Math.Min(page.Focus, _focusable.Count - 1)].GrabFocus();
            }
        }

        private void Clear()
        {
            _focusable.Clear();
            foreach (var child in _body.GetChildren())
            {
                _body.RemoveChild(child);
                child.QueueFree();
            }
        }

        private void BuildMain()
        {
            Heading("ROTER HAMMER   AKTE 1927", "VERTRAULICH", SheetWidth);
            _body.AddChild(PaperUi.Text("MESSINGSTADT", 42, PaperUi.Ink, PaperUi.Serif));
            _body.AddChild(Wrapped("Die Stadt gehört den Brandt-Werken. Noch.", 15, SheetWidth));
            _body.AddChild(Rule());
            if (Game.HasSave)
            {
                _body.AddChild(PaperUi.Text(Game.CampaignLine, 15, PaperUi.StampRed, PaperUi.Typewriter));
                Entry("Weiter", Game.ContinueMission);
                Entry("Neue Kampagne", () => Ask(
                    "Neue Kampagne", "Die bisherige Akte wird überschrieben: " + Game.CampaignLine + ".",
                    "Neu beginnen", Game.NewCampaign));
            }
            else
            {
                Entry("Kampagne beginnen", Game.NewCampaign);
            }
            Entry("Einstellungen", ShowSettings);
            Entry("Bedienung", ShowControls);
            Entry("Beenden", Game.QuitGame);
        }

        private void BuildPause()
        {
            Heading("ROTER HAMMER   LAUFENDER EINSATZ", "UNTERBROCHEN", SheetWidth);
            _body.AddChild(Rule());
            Entry("Weiter", CloseAll);
            if (!Game.Finished)
            {
                Entry("Einsatz abbrechen", ConfirmAbandon);
            }
            Entry("Einstellungen", ShowSettings);
            Entry("Bedienung", ShowControls);
            Entry("Zum Hauptmenü", () => AskAbandon("Zum Hauptmenü", "Zum Hauptmenü", Game.ToMainMenu));
            Entry("Beenden", () => AskAbandon("Beenden", "Beenden", Game.QuitGame));
        }

        /// <summary>Wer den laufenden Einsatz verlässt, bekommt vorher gesagt, was das kostet. Ein beendeter Einsatz kostet nichts mehr.</summary>
        private void AskAbandon(string title, string yes, Action action)
        {
            if (Game.Finished)
            {
                action();
                return;
            }
            string text = Game.AbandonCostsSquad
                ? "Es ist geschossen worden. Wer jetzt geht, verliert den Trupp: Beine und Herz aller Agenten fallen auf Stufe 1."
                : "Noch ist kein Schuss gefallen. Der Trupp zieht sich unbeschadet zurück, ausgegebenes Geld bleibt weg.";
            Ask(title, text, yes, action);
        }

        /// <summary>Rückfrage. "Nein" steht vorn und ist vorgewählt, damit Enter nichts zerstört.</summary>
        private void Ask(string title, string text, string yes, Action action)
        {
            Open("Rückfrage", () =>
            {
                Heading("ROTER HAMMER   RÜCKFRAGE", "ACHTUNG", SheetWidth);
                _body.AddChild(PaperUi.Text(title, 26, PaperUi.Ink, PaperUi.Serif));
                _body.AddChild(Wrapped(text, 16, SheetWidth));
                _body.AddChild(Rule());
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                _body.AddChild(row);
                row.AddChild(MakeEntry("Nein, zurück", Back, true));
                row.AddChild(MakeEntry(yes, action, true));
            });
        }

        private void ShowSettings() => Open("Einstellungen", BuildSettings, Game.SaveSettings);

        private void BuildSettings()
        {
            var s = Game.Settings;
            Heading("ROTER HAMMER   DIENSTVORSCHRIFT", "", SheetWidth);
            _body.AddChild(PaperUi.Text("Einstellungen", 26, PaperUi.Ink, PaperUi.Serif));
            _body.AddChild(Rule());
            AddSlider("Gesamt", s.MasterVolume, v => { s.MasterVolume = v; Game.ApplySound(); }, false);
            AddSlider("Musik", s.MusicVolume, v => { s.MusicVolume = v; Game.ApplySound(); }, false);
            AddSlider("Geräusche", s.EffectsVolume, v => { s.EffectsVolume = v; Game.ApplySound(); }, true);
            Entry($"Ton: {(s.Muted ? "aus" : "an")}", Game.ToggleMute);
            Entry($"Vollbild: {(s.Fullscreen ? "an" : "aus")}", Game.ToggleFullscreen);
            Entry($"Bildwackeln: {(s.ScreenShake ? "an" : "aus")}", () =>
            {
                s.ScreenShake = !s.ScreenShake;
                Refresh();
            });
            Entry("Zurück", Back);
        }

        private void ShowControls() => Open("Bedienung", BuildControls);

        private void BuildControls()
        {
            Heading("ROTER HAMMER   DIENSTVORSCHRIFT", "", WideSheetWidth);
            _body.AddChild(PaperUi.Text("Bedienung", 26, PaperUi.Ink, PaperUi.Serif));
            _body.AddChild(Rule());
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 18);
            grid.AddThemeConstantOverride("v_separation", 3);
            _body.AddChild(grid);
            foreach (var (keys, what) in Controls)
            {
                grid.AddChild(PaperUi.Text(keys, 14, PaperUi.StampRed, PaperUi.Typewriter));
                grid.AddChild(PaperUi.Text(what, 14, PaperUi.Ink, PaperUi.Typewriter));
            }
            _body.AddChild(Rule());
            Entry("Zurück", Back);
        }

        private static readonly (string Keys, string What)[] Controls =
        {
            ("Linksklick", "Agent wählen oder Auswahl losschicken"),
            ("Umschalt + Klick", "Agent zur Auswahl hinzufügen"),
            ("Doppelklick", "rennen, kostet Puste, dabei kein Feuer"),
            ("Rechtsklick Wache", "angreifen. Nur Emil gewählt: bestechen"),
            ("Rechtsklick Tür", "Otto öffnet die Tür"),
            ("1 bis 4, Leertaste", "Agent wählen, alle wählen"),
            ("Pfeile, WASD", "Kamera bewegen"),
            ("F", "Kamera auf die Auswahl"),
            ("Q halten", "Lageplan. Ein Klick wartet, bis der Weg frei ist"),
            ("P", "Pause, Befehle bleiben möglich"),
            ("Esc", "Pausenmappe, im Menü eine Ebene zurück"),
            ("R", "Einsatz abbrechen"),
            ("M", "Ton an und aus"),
            ("F11", "Vollbild an und aus"),
        };

        private void Heading(string text, string stamp, float width)
        {
            var head = new Control { CustomMinimumSize = new Vector2(width, 20) };
            head.MouseFilter = Control.MouseFilterEnum.Ignore;
            head.AddChild(PaperUi.Text(text, 14, PaperUi.StampRed, PaperUi.Serif));
            if (stamp.Length > 0)
            {
                var mark = PaperUi.Text(stamp, 20, new Color(PaperUi.StampRed, 0.7f), PaperUi.Serif);
                mark.Position = new Vector2(width - 8f - stamp.Length * 14f, 22f);
                mark.RotationDegrees = -11f;
                head.AddChild(mark);
            }
            _body.AddChild(head);
        }

        private static Label Wrapped(string text, int size, float width)
        {
            var label = PaperUi.Text(text, size, PaperUi.Ink, PaperUi.Typewriter);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(width, 0);
            return label;
        }

        private static ColorRect Rule() =>
            new() { CustomMinimumSize = new Vector2(0, 2), Color = PaperUi.Ink, MouseFilter = Control.MouseFilterEnum.Ignore };

        private void Entry(string text, Action action) => _body.AddChild(MakeEntry(text, action, false));

        private Button MakeEntry(string text, Action action, bool expand)
        {
            var button = new Button { Text = text };
            PaperUi.InkButton(button);
            button.AddThemeFontSizeOverride("font_size", EntryFontSize);
            if (expand)
            {
                button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            }
            button.Pressed += () =>
            {
                Game.Sound.PlayEverywhere("click", -18f);
                action();
            };
            _focusable.Add(button);
            return button;
        }

        private void AddSlider(string name, double value, Action<double> changed, bool sample)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var label = PaperUi.Text(name, 16, PaperUi.Ink, PaperUi.Typewriter);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            var slider = new HSlider
            {
                MinValue = 0, MaxValue = 1, Step = SliderStep, Value = value,
                CustomMinimumSize = new Vector2(SliderWidth, 22), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            PaperUi.InkSlider(slider);
            row.AddChild(slider);
            var percent = PaperUi.Text(Percent(value), 16, PaperUi.Ink, PaperUi.Typewriter);
            percent.CustomMinimumSize = new Vector2(58, 0);
            percent.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(percent);
            slider.ValueChanged += v =>
            {
                percent.Text = Percent(v);
                changed(v);
                if (sample)
                {
                    PlaySample();
                }
            };
            // Der Regler selbst zeigt keine Markierung, darum wird beim gewählten Regler die Beschriftung rot.
            slider.FocusEntered += () => label.AddThemeColorOverride("font_color", PaperUi.StampRed);
            slider.FocusExited += () => label.AddThemeColorOverride("font_color", PaperUi.Ink);
            _focusable.Add(slider);
            _body.AddChild(row);
        }

        private static string Percent(double value) => $"{Mathf.RoundToInt(value * 100)} %";

        /// <summary>Beim Schieben ist nichts zu hören, wenn gerade Stille herrscht. Darum eine kleine Probe.</summary>
        private void PlaySample()
        {
            float now = Time.GetTicksMsec() / 1000f;
            if (now - _lastSample >= SampleSeconds)
            {
                _lastSample = now;
                Game.Sound.PlayEverywhere("click", -10f);
            }
        }
    }
}
