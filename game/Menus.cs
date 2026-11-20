using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Startbildschirm, Pausenmappe, Einstellungen, Bedienung und Rückfragen: ein Blatt Papier mitten im Bild,
    /// der Startbildschirm als eigene Seite über die ganze Fläche (TitleScreen).
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
            public bool Title;
            public Action Build = null!;
            public int Focus;
            public Action? OnBack;
        }

        private readonly Stack<Page> _pages = new();
        private readonly List<Control> _focusable = new();
        private ColorRect _dim = null!;
        private PanelContainer _sheet = null!;
        private VBoxContainer _body = null!;
        private VBoxContainer _sheetBody = null!;
        private TitleScreen _title = null!;
        private float _lastSample;
        private bool _building;

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
            _sheetBody = _body;
            // Der Startbildschirm liegt unter dem Blatt, damit Rückfragen über ihm erscheinen.
            _title = new TitleScreen { Game = Game };
            _dim.AddChild(_title);
            _dim.MoveChild(_title, 0);
        }

        public void ShowMain() => Open("Hauptmenü", BuildMain);

        /// <summary>Startbildschirm: "Neues Spiel", "Spiel laden" und "Beenden" wirken, der Rest ist sichtbar und ausgegraut.</summary>
        public void ShowTitle()
        {
            Remember();
            var page = new Page { Name = "Startbildschirm", Build = BuildTitle, Title = true };
            _pages.Push(page);
            Show(page);
        }

        /// <summary>Für den Prüflauf: welche Zutaten der Startbildschirm gefunden hat.</summary>
        public string TitleParts => _title.Parts;

        public void ShowPause() => Open("Pausenmappe", BuildPause);

        public void CloseAll()
        {
            _pages.Clear();
            Clear();
            _dim.Visible = false;
            _title.Visible = false;
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

        private void Open(string name, Action build, Action? onBack = null, int focus = 0)
        {
            Remember();
            var page = new Page { Name = name, Build = build, OnBack = onBack, Focus = focus };
            _pages.Push(page);
            Show(page);
        }

        /// <summary>Eine Ebene zurück. Das Hauptmenü ist die unterste Ebene und bleibt offen.</summary>
        private void Back()
        {
            if (_pages.Peek().Name is "Hauptmenü" or "Startbildschirm")
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
            bool overTitle = _pages.Any(p => p.Title);
            _title.Visible = overTitle;
            _title.ShowColumn = page.Title;
            _sheet.Visible = !page.Title;
            _dim.Color = overTitle ? Colors.Transparent : Dim;
            _body = page.Title ? _title.Entries : _sheetBody;
            // Beim Aufbau wandert die Wahl von selbst, das soll nicht klicken.
            _building = true;
            page.Build();
            if (_focusable.Count > 0)
            {
                _focusable[Math.Min(page.Focus, _focusable.Count - 1)].GrabFocus();
            }
            _building = false;
        }

        /// <summary>Jede wählbare Stelle meldet sich mit einem leisen Geräusch, wenn die Wahl auf sie wandert.</summary>
        private void Choosable(Control control)
        {
            control.FocusEntered += () =>
            {
                if (!_building)
                {
                    Game.Sound.PlayEverywhere("menu_move", -20f);
                }
            };
            _focusable.Add(control);
        }

        private void Clear()
        {
            _focusable.Clear();
            foreach (var box in new[] { _sheetBody, _title.Entries })
            {
                foreach (var child in box.GetChildren())
                {
                    box.RemoveChild(child);
                    child.QueueFree();
                }
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
            }
            else
            {
                Entry("Kampagne beginnen", Game.NewCampaign);
            }
            Entry("Akten", ShowFiles);
            Entry("Einstellungen", ShowSettings);
            Entry("Bedienung", ShowControls);
            Entry("Beenden", Game.QuitGame);
        }

        /// <summary>
        /// Einträge des Startbildschirms. Einstellungen und Mitwirkende kommen später dazu,
        /// die Einstellungen sind schon gebaut (ShowSettings) und werden dann nur freigeschaltet.
        /// </summary>
        private void BuildTitle()
        {
            TitleEntry("Neues Spiel", NewGame);
            TitleEntry("Spiel laden", ShowLoad);
            TitleEntry("Einstellungen", null);
            TitleEntry("Mitwirkende", null);
            TitleEntry("Beenden", Game.QuitGame);
        }

        /// <summary>Ein neues Spiel beginnt in der zuletzt offenen Akte. Liegt dort schon eine begonnene Kampagne, wird vorher gefragt.</summary>
        private void NewGame()
        {
            int slot = Game.CurrentSlot;
            if (!Game.SlotBegun(slot))
            {
                Game.NewCampaign();
                return;
            }
            Ask("Neues Spiel", $"In Akte {slot} liegt schon eine Kampagne: {Game.SlotLine(slot)}. Sie wird überschrieben.", "Neu beginnen", Game.NewCampaign);
        }

        private void TitleEntry(string text, Action? action)
        {
            var button = TitleScreen.Entry(text, action != null);
            if (action != null)
            {
                button.Pressed += () =>
                {
                    Game.Sound.PlayEverywhere("menu_select", -14f);
                    action();
                };
                Choosable(button);
            }
            _body.AddChild(button);
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
            Entry("Totenbuch", () => ShowLedger(Game.Campaign));
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
                ? "Es ist geschossen worden. Wer jetzt geht, lässt den Trupp liegen: Jeder gilt als gefallen, wer schon einmal geflickt wurde, stirbt."
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

        /// <summary>Spiel laden: Die zuletzt geöffnete Akte ist vorgewählt, wenn sie belegt ist.</summary>
        private void ShowLoad()
        {
            var filled = Enumerable.Range(1, SaveFolder.Slots).Where(Game.SlotExists).ToList();
            // Je belegter Akte zwei Knöpfe: die Akte selbst und ihr Totenbuch.
            Open("Spiel laden", BuildLoad, focus: 2 * Math.Max(0, filled.IndexOf(Game.CurrentSlot)));
        }

        /// <summary>Belegte Akten setzen ihre Kampagne fort, leere stehen ausgegraut da. Neu beginnen geht nur über "Neues Spiel".</summary>
        private void BuildLoad()
        {
            Heading("ROTER HAMMER   AKTENSCHRANK", "", SheetWidth);
            _body.AddChild(PaperUi.Text("Spiel laden", 26, PaperUi.Ink, PaperUi.Serif));
            _body.AddChild(Rule());
            bool any = false;
            for (int slot = 1; slot <= SaveFolder.Slots; slot++)
            {
                int s = slot;
                if (!Game.SlotExists(s))
                {
                    var empty = new Button { Text = $"Akte {s}: leer", Disabled = true, FocusMode = Control.FocusModeEnum.None };
                    PaperUi.InkButton(empty);
                    empty.AddThemeFontSizeOverride("font_size", EntryFontSize);
                    _body.AddChild(empty);
                    continue;
                }
                any = true;
                string mark = s == Game.CurrentSlot ? "  *" : "";
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                _body.AddChild(row);
                row.AddChild(MakeEntry($"Akte {s}: {Game.SlotLine(s)}{mark}", () => Game.OpenSlot(s, false), true));
                row.AddChild(MakeEntry("Totenbuch", () => ShowLedger(Game.SlotCampaign(s)), false));
                // Wer aussetzt, steht klein unter der Akte, wie auf dem Ergebnis-Bildschirm.
                foreach (string resting in Game.SlotResting(s))
                {
                    var note = new MarginContainer();
                    note.AddThemeConstantOverride("margin_left", 18);
                    note.AddChild(PaperUi.Text(resting, 14, PaperUi.StampRed, PaperUi.Typewriter));
                    _body.AddChild(note);
                }
            }
            _body.AddChild(any
                ? PaperUi.Text("* zuletzt geöffnet", 13, new Color(PaperUi.Ink, 0.7f), PaperUi.Typewriter)
                : Wrapped("Noch liegt keine Akte im Schrank. \"Neues Spiel\" legt die erste an.", 15, SheetWidth));
            Entry("Zurück", Back);
        }

        // ---- Totenbuch ----

        /// <summary>So viele Namen je Seite und Spalte. Mehr wird geblättert, damit das Blatt nie über den Bildschirm wächst.</summary>
        public const int LedgerRows = 6;
        public const string LedgerStamp = "GEFALLEN";
        public const string LedgerEmpty = "Noch steht kein Name im Buch.";
        private const float LedgerColumnWidth = 344f;
        private const float LedgerRowHeight = 40f;
        private const float LedgerNameWidth = 236f;

        private Campaign? _ledgerOf;
        private int _ledgerPage;

        /// <summary>Das Blatt "Totenbuch" einer Kampagne: links die Toten des Trupps, rechts die der Wache. Nur Namen, Viertel und Stempel.</summary>
        public void ShowLedger(Campaign campaign)
        {
            _ledgerOf = campaign;
            _ledgerPage = 0;
            Open("Totenbuch", BuildLedger);
        }

        private int LedgerPages => _ledgerOf == null ? 1
            : Math.Max(1, (Math.Max(_ledgerOf.DeadOurs().Count, _ledgerOf.Dead(LedgerSide.Guard).Count) + LedgerRows - 1) / LedgerRows);

        /// <summary>Für den Prüflauf: wer auf dem offenen Blatt steht und welche Seite aufgeschlagen ist.</summary>
        public string LedgerProbe => ScreenName != "Totenbuch" || _ledgerOf == null ? ""
            : $"unsere {string.Join(", ", _ledgerOf.DeadOurs().Select(e => e.Name))} | wache {string.Join(", ", _ledgerOf.Dead(LedgerSide.Guard).Select(e => e.Name))} | seite {_ledgerPage + 1}/{LedgerPages}";

        private void BuildLedger()
        {
            var book = _ledgerOf!;
            // Links stehen unsere Toten: der Trupp und die Gefangenen, die nicht herauskamen, in der Reihenfolge der Tode.
            var squad = book.DeadOurs();
            var guards = book.Dead(LedgerSide.Guard);
            int pages = LedgerPages;
            _ledgerPage = Math.Clamp(_ledgerPage, 0, pages - 1);
            Heading("ROTER HAMMER   AKTENSCHRANK", "", WideSheetWidth);
            _body.AddChild(PaperUi.Text("Totenbuch", 26, PaperUi.Ink, PaperUi.Serif));
            _body.AddChild(Rule());
            if (squad.Count + guards.Count == 0)
            {
                _body.AddChild(Wrapped(LedgerEmpty, 15, WideSheetWidth));
            }
            else
            {
                var columns = new HBoxContainer();
                columns.AddThemeConstantOverride("separation", 16);
                columns.AddChild(LedgerColumn("UNSERE", squad));
                columns.AddChild(new ColorRect { CustomMinimumSize = new Vector2(1, 0), Color = PaperUi.PaperDark, MouseFilter = Control.MouseFilterEnum.Ignore });
                columns.AddChild(LedgerColumn("DIE WACHE", guards));
                _body.AddChild(columns);
            }
            _body.AddChild(Rule());
            if (pages > 1)
            {
                // Ein Knopf, der im Kreis blättert: Die Wahl bleibt auf ihm stehen.
                Entry($"Blättern   Seite {_ledgerPage + 1} von {pages}", () =>
                {
                    _ledgerPage = (_ledgerPage + 1) % pages;
                    Refresh();
                });
            }
            Entry("Zurück", Back);
        }

        private Control LedgerColumn(string title, List<LedgerEntry> dead)
        {
            var column = new VBoxContainer { CustomMinimumSize = new Vector2(LedgerColumnWidth, LedgerRows * LedgerRowHeight + 24f) };
            column.AddThemeConstantOverride("separation", 0);
            column.AddChild(PaperUi.Text($"{title}   {dead.Count}", 14, PaperUi.StampRed, PaperUi.Serif));
            if (dead.Count == 0)
            {
                column.AddChild(PaperUi.Text("Niemand.", 14, new Color(PaperUi.Ink, 0.7f), PaperUi.Typewriter));
            }
            foreach (var entry in dead.Skip(_ledgerPage * LedgerRows).Take(LedgerRows))
            {
                var row = new Control { CustomMinimumSize = new Vector2(LedgerColumnWidth, LedgerRowHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
                var name = PaperUi.Text(entry.Name, 15, PaperUi.Ink, PaperUi.Typewriter);
                name.Position = new Vector2(0, 2);
                name.Size = new Vector2(LedgerNameWidth, 18);
                name.ClipText = true;
                name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                row.AddChild(name);
                // Ein Gefangener ist kein Agent: Er trägt keinen Beruf, nur den Vermerk vor dem Viertel.
                string place = Game.DistrictName(entry.District);
                var where = PaperUi.Text(entry.Side == LedgerSide.Prisoner ? $"{Campaign.PrisonerKind}, {place}" : place, 12, new Color(PaperUi.Ink, 0.7f), PaperUi.Typewriter);
                where.Position = new Vector2(0, 20);
                row.AddChild(where);
                var stamp = PaperUi.Text(LedgerStamp, 13, new Color(PaperUi.StampRed, 0.75f), PaperUi.Serif);
                stamp.Position = new Vector2(LedgerNameWidth + 12f, 14f);
                stamp.RotationDegrees = -7f;
                row.AddChild(stamp);
                column.AddChild(row);
            }
            return column;
        }

        private void ShowFiles() => Open("Akten", BuildFiles);

        /// <summary>Drei Aktenordner: eine belegte Akte laden oder neu beginnen, eine leere anlegen.</summary>
        private void BuildFiles()
        {
            Heading("ROTER HAMMER   AKTENSCHRANK", "", SheetWidth);
            _body.AddChild(PaperUi.Text("Akten", 26, PaperUi.Ink, PaperUi.Serif));
            _body.AddChild(Rule());
            for (int slot = 1; slot <= SaveFolder.Slots; slot++)
            {
                int s = slot;
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                _body.AddChild(row);
                if (!Game.SlotExists(s))
                {
                    row.AddChild(MakeEntry($"Akte {s}: leer, neu beginnen", () => Game.OpenSlot(s, true), true));
                    continue;
                }
                string line = Game.SlotLine(s);
                string mark = s == Game.CurrentSlot ? "  *" : "";
                row.AddChild(MakeEntry($"Akte {s}: {line}{mark}", () => Game.OpenSlot(s, false), true));
                row.AddChild(MakeEntry("Totenbuch", () => ShowLedger(Game.SlotCampaign(s)), false));
                var fresh = MakeEntry("Neu", () => Ask(
                    $"Akte {s} neu beginnen", $"Die Akte wird überschrieben: {line}.", "Neu beginnen", () => Game.OpenSlot(s, true)), false);
                fresh.CustomMinimumSize = new Vector2(70, 0);
                row.AddChild(fresh);
            }
            _body.AddChild(PaperUi.Text("* zuletzt geöffnet", 13, new Color(PaperUi.Ink, 0.7f), PaperUi.Typewriter));
            Entry("Zurück", Back);
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
            ("Greta in Rufweite", "Bestechen kostet die Hälfte"),
            ("Rechtsklick Tür", "Otto öffnet die Tür, mit Dietrich auch andere"),
            ("E", "nächste Waffe in die Hand"),
            ("G", "Rauchtopf auf das Feld unter der Maus"),
            ("H", "Verband anlegen, 3 s still stehen"),
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
                Game.Sound.PlayEverywhere("menu_select", -14f);
                action();
            };
            Choosable(button);
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
            Choosable(slider);
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
