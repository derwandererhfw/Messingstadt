using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Anzeige im Einsatz: eine feste Spalte am linken Bildrand mit vier Agentenfeldern, dem Inventar des gewählten Agenten
    /// und dem Peilgerät, dazu oben rechts neben der Spalte das Telegramm. Die Spielfläche beginnt rechts der Spalte,
    /// siehe ColumnWidth. Die Spalte fängt jeden Klick ab, in die Welt geht von dort kein Befehl.
    /// </summary>
    public partial class Hud : Control
    {
        /// <summary>Breite der Spalte in Bildpunkten der Oberfläche. Kamera, Telegramm und Hilfezeile richten sich danach.</summary>
        public const float ColumnWidth = 244f;

        /// <summary>Sekunden zwischen zwei Ausschlägen des Peilgeräts. Startschalter --peil=2.5 überschreibt den Wert.</summary>
        public const float PeilSeconds = 3f;

        private const int FieldWidth = 114, FieldHeight = 100, FieldGap = 4, Margin = 6;
        // Hier beginnt rechts neben dem Lichtbild der Zustand.
        private const int StateLeft = 44;
        private const int Fields = 4;
        private const int InventoryTop = Margin + 2 * FieldHeight + FieldGap + 8;
        private const int InventoryHeight = 172;
        private const int LevelDots = 3;
        private static readonly Color SelectedEdge = new(0.95f, 0.78f, 0.35f);
        private static readonly Color Board = new(0.17f, 0.15f, 0.13f);
        private static readonly Color HurtTint = new(0.85f, 0.6f, 0.55f);
        private static readonly Color DownTint = new(0.32f, 0.3f, 0.3f);
        private const string HelpGap = "   ";

        /// <summary>
        /// Hilfezeile für die Breite neben der Spalte. Passt sie nicht in eine Zeile, wird sie an der Lücke zwischen zwei Gruppen
        /// geteilt, die der Mitte am nächsten liegt. So steht nie ein einzelnes Wort allein in der zweiten Zeile. Innerhalb einer
        /// Gruppe wie "WASD F P M" bricht nichts um.
        /// </summary>
        public static string FitHelp(string text, float width, int fontSize)
        {
            var groups = text.Split(HelpGap).Select(g => g.Replace(' ', ' ')).ToArray();
            string line = string.Join(HelpGap, groups);
            if (width <= 0f || groups.Length < 2 || PaperUi.Typewriter.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X <= width)
            {
                return line;
            }
            int best = 1;
            float bestGap = float.MaxValue;
            for (int i = 1; i < groups.Length; i++)
            {
                float gap = Math.Abs(string.Join(HelpGap, groups.Take(i)).Length - string.Join(HelpGap, groups.Skip(i)).Length);
                if (gap < bestGap)
                {
                    (best, bestGap) = (i, gap);
                }
            }
            return string.Join(HelpGap, groups.Take(best)) + "\n" + string.Join(HelpGap, groups.Skip(best));
        }

        private sealed class Field
        {
            public Control Root = null!;
            public ColorRect Edge = null!;
            public ColorRect Paper = null!;
            public Label Name = null!;
            public TextureRect Portrait = null!;
            public ColorRect Frame = null!;
            public Label Doing = null!;
            public Label State = null!;
            public Control Alive = null!;
            public ColorRect Health = null!;
            public ColorRect Breath = null!;
            public Label Stamp = null!;
            public Line2D Strike = null!;
        }

        private readonly List<Field> _fields = new();
        private readonly List<Button> _slots = new();
        private readonly List<ColorRect> _legs = new(), _heart = new();
        private Label _telegram = null!;
        private Label _info = null!;
        private Label _owner = null!;
        private Label _role = null!;
        private Control _levels = null!;
        private Peil _peil = null!;
        private int _owned = -1;

        public Main Game = null!;

        /// <summary>Sekunden zwischen zwei Ausschlägen, zum Justieren.</summary>
        public float PeilInterval
        {
            get => _peil.Interval;
            set => _peil.Interval = Mathf.Max(0.2f, value);
        }

        /// <summary>Für den Prüflauf: Zahl der Ausschläge und was der letzte gezeigt hat.</summary>
        public string PeilState => _peil.State;

        /// <summary>Für den Prüflauf: die Plätze des gewählten Agenten, wie sie dastehen.</summary>
        public string SlotTexts => string.Join(" | ", _slots.Select(s => s.Text));

        /// <summary>Für den Prüflauf: was in den vier Feldern steht.</summary>
        public string FieldTexts => string.Join(" | ", _fields.Select(f =>
            (f.Stamp.Visible ? $"{f.Name.Text} {f.Stamp.Text}" : $"{f.Name.Text} {f.State.Text}").Replace("\n", " ").Trim()));

        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            BuildTelegram();
            // Das Brett der Spalte fängt jeden Klick ab, auch zwischen den Feldern.
            var board = new ColorRect { AnchorBottom = 1f, OffsetRight = ColumnWidth, Color = Board, MouseFilter = MouseFilterEnum.Stop };
            AddChild(board);
            board.AddChild(new ColorRect { AnchorLeft = 1f, AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = -2, Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = MouseFilterEnum.Ignore });
            for (int i = 0; i < Fields; i++)
            {
                AddField(board, i);
            }
            BuildInventory(board);
            _peil = new Peil { Game = Game };
            board.AddChild(_peil);
        }

        private void BuildTelegram()
        {
            AddChild(Strip(ColumnWidth + 12, 12, new Color(0, 0, 0, 0.5f)));
            // Das Papier fängt Klicks ab, sonst liefe die Auswahl auf ein Feld darunter.
            var strip = Strip(ColumnWidth + 8, 8, PaperUi.PaperColor);
            strip.MouseFilter = MouseFilterEnum.Stop;
            strip.ClipContents = true;
            AddChild(strip);
            var head = PaperUi.Text("TELEGRAMM", 13, PaperUi.StampRed, PaperUi.Serif);
            head.Position = new Vector2(10, 4);
            strip.AddChild(head);
            _telegram = PaperUi.Text("", 15, PaperUi.Ink, PaperUi.Typewriter);
            _telegram.Position = new Vector2(112, 3);
            strip.AddChild(_telegram);
            _info = PaperUi.Text("", 13, PaperUi.Ink, PaperUi.Typewriter);
            _info.Position = new Vector2(112, 27);
            strip.AddChild(_info);
        }

        /// <summary>Papierstreifen für das Telegramm: von der Spalte bis zum rechten Bildrand.</summary>
        private static ColorRect Strip(float left, int top, Color color) => new()
        {
            AnchorRight = 1f, OffsetLeft = left, OffsetTop = top, OffsetRight = -10, OffsetBottom = top + 52,
            Color = color, MouseFilter = MouseFilterEnum.Ignore,
        };

        private void AddField(Control board, int index)
        {
            int x = Margin + index % 2 * (FieldWidth + FieldGap), y = Margin + index / 2 * (FieldHeight + FieldGap);
            var root = new Control { Position = new Vector2(x, y), Size = new Vector2(FieldWidth, FieldHeight), MouseFilter = MouseFilterEnum.Stop };
            root.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
                {
                    Game.SelectAgent(index, click.ShiftPressed);
                }
            };
            board.AddChild(root);
            var f = new Field { Root = root };
            f.Edge = new ColorRect { Position = new Vector2(-3, -3), Size = new Vector2(FieldWidth + 6, FieldHeight + 6), MouseFilter = MouseFilterEnum.Ignore };
            root.AddChild(f.Edge);
            f.Paper = new ColorRect { Size = new Vector2(FieldWidth, FieldHeight), Color = PaperUi.PaperColor, MouseFilter = MouseFilterEnum.Ignore };
            root.AddChild(f.Paper);
            f.Name = PaperUi.Text("", 13, PaperUi.Ink, PaperUi.Typewriter);
            f.Name.Position = new Vector2(5, 1);
            root.AddChild(f.Name);
            // Lichtbild wie in einer Personalakte, dunkel hinterlegt. Es bleibt auch bei Niedergegangenen stehen, nur abgedunkelt.
            f.Frame = new ColorRect { Position = new Vector2(5, 31), Size = new Vector2(36, 46), Color = new Color(0.25f, 0.23f, 0.2f), MouseFilter = MouseFilterEnum.Ignore };
            root.AddChild(f.Frame);
            f.Portrait = new TextureRect
            {
                Position = new Vector2(1, 1), Size = new Vector2(34, 44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, TextureFilter = TextureFilterEnum.LinearWithMipmaps, MouseFilter = MouseFilterEnum.Ignore,
            };
            f.Frame.AddChild(f.Portrait);
            f.Alive = new Control { Size = new Vector2(FieldWidth, FieldHeight), MouseFilter = MouseFilterEnum.Ignore };
            root.AddChild(f.Alive);
            f.Doing = Small("", new Color(PaperUi.Ink, 0.8f));
            f.Doing.Position = new Vector2(5, 16);
            f.Doing.Size = new Vector2(FieldWidth - 8, 14);
            f.Doing.ClipText = true;
            f.Alive.AddChild(f.Doing);
            f.State = PaperUi.Text("", 9, PaperUi.StampRed, PaperUi.Typewriter);
            f.State.Position = new Vector2(StateLeft, 33);
            f.State.Size = new Vector2(FieldWidth - StateLeft - 2, 44);
            f.State.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            root.AddChild(f.State);
            f.Alive.AddChild(new ColorRect { Position = new Vector2(5, 79), Size = new Vector2(FieldWidth - 10, 9), Color = PaperUi.PaperDark, MouseFilter = MouseFilterEnum.Ignore });
            f.Health = new ColorRect { Position = new Vector2(6, 80), Size = new Vector2(FieldWidth - 12, 7), Color = new Color(0.4f, 0.12f, 0.08f), MouseFilter = MouseFilterEnum.Ignore };
            f.Alive.AddChild(f.Health);
            f.Alive.AddChild(new ColorRect { Position = new Vector2(5, 90), Size = new Vector2(FieldWidth - 10, 6), Color = PaperUi.PaperDark, MouseFilter = MouseFilterEnum.Ignore });
            f.Breath = new ColorRect { Position = new Vector2(6, 91), Size = new Vector2(FieldWidth - 12, 4), Color = new Color(0.15f, 0.22f, 0.32f), MouseFilter = MouseFilterEnum.Ignore };
            f.Alive.AddChild(f.Breath);
            // Wer fehlt, ist durchgestrichen.
            f.Strike = new Line2D { Points = new[] { new Vector2(6, FieldHeight - 6), new Vector2(FieldWidth - 6, 20) }, Width = 2f, DefaultColor = new Color(PaperUi.StampRed, 0.7f), Visible = false };
            root.AddChild(f.Strike);
            // Der erste Sturz heißt schwer verwundet, der zweite tot, siehe docs/story.md.
            f.Stamp = PaperUi.Text("", 12, PaperUi.StampRed, PaperUi.Serif);
            f.Stamp.Position = new Vector2(4, 60);
            f.Stamp.RotationDegrees = -11f;
            f.Stamp.Visible = false;
            root.AddChild(f.Stamp);
            _fields.Add(f);
        }

        private static Label Small(string text, Color color) => PaperUi.Text(text, 10, color, PaperUi.Typewriter);

        private void BuildInventory(Control board)
        {
            var sheet = new ColorRect
            {
                Position = new Vector2(Margin, InventoryTop), Size = new Vector2(ColumnWidth - 2 * Margin - 2, InventoryHeight),
                Color = PaperUi.PaperColor, MouseFilter = MouseFilterEnum.Stop,
            };
            board.AddChild(sheet);
            _owner = PaperUi.Text("", 13, PaperUi.Ink, PaperUi.Typewriter);
            _owner.Position = new Vector2(6, 2);
            sheet.AddChild(_owner);
            _role = Small("", new Color(PaperUi.Ink, 0.8f));
            _role.Position = new Vector2(6, 19);
            sheet.AddChild(_role);
            _levels = new Control { Position = new Vector2(0, 32), MouseFilter = MouseFilterEnum.Ignore };
            sheet.AddChild(_levels);
            AddLevelRow(_levels, _legs, "Beine", 6);
            AddLevelRow(_levels, _heart, "Herz", 116);
            for (int i = 0; i < Campaign.ItemSlots; i++)
            {
                int slot = i;
                var button = new Button
                {
                    Position = new Vector2(5, 52 + i * 29), Size = new Vector2(sheet.Size.X - 10, 26), Alignment = HorizontalAlignment.Left,
                    // Nur Maus: Die Tasten gehören im Einsatz dem Spiel, die Plätze dürfen sie nicht einfangen.
                    FocusMode = FocusModeEnum.None, ClipText = true,
                };
                PaperUi.InkButton(button);
                button.AddThemeFontSizeOverride("font_size", 13);
                button.Pressed += () =>
                {
                    if (_owned >= 0)
                    {
                        Game.UseSlot(_owned, slot);
                    }
                };
                sheet.AddChild(button);
                _slots.Add(button);
            }
        }

        private static void AddLevelRow(Control parent, List<ColorRect> pips, string label, int x)
        {
            var text = Small(label, PaperUi.Ink);
            text.Position = new Vector2(x, 0);
            parent.AddChild(text);
            for (int i = 0; i < LevelDots; i++)
            {
                var pip = new ColorRect { Position = new Vector2(x + 40 + i * 12, 3), Size = new Vector2(9, 8), MouseFilter = MouseFilterEnum.Ignore };
                parent.AddChild(pip);
                pips.Add(pip);
            }
        }

        private static void ShowLevel(List<ColorRect> pips, int level)
        {
            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].Color = i < level ? PaperUi.Ink : PaperUi.PaperDark;
            }
        }

        public void Refresh(World world, HashSet<int> selected, string telegram, string info)
        {
            _telegram.Text = telegram;
            _info.Text = info;
            var squad = world.Units.Where(u => u.Faction == Faction.Agent).Take(Fields).ToList();
            // Die übrigen Felder gehören denen, die fehlen: Tote und Aussetzende aus der Akte.
            var missing = Game.Campaign.Roster().Where(r => squad.All(u => u.Name != r.Name) && (r.Dead || !r.Ready)).ToList();
            for (int i = 0; i < _fields.Count; i++)
            {
                var f = _fields[i];
                if (i < squad.Count)
                {
                    ShowAgent(f, i, squad[i], selected.Contains(squad[i].Id));
                }
                else if (i - squad.Count < missing.Count)
                {
                    ShowMissing(f, missing[i - squad.Count]);
                }
                else
                {
                    ShowEmpty(f);
                }
            }
            RefreshInventory(world, selected);
        }

        private void ShowAgent(Field f, int index, Unit u, bool chosen)
        {
            f.Root.MouseFilter = MouseFilterEnum.Stop;
            f.Name.Text = $"{index + 1} {u.Name.ToUpperInvariant()}";
            f.Paper.Color = chosen ? PaperUi.PaperColor.Lightened(0.12f) : PaperUi.PaperColor;
            f.Edge.Color = chosen ? SelectedEdge : new Color(0f, 0f, 0f, 0.45f);
            f.Alive.Visible = u.Alive;
            f.Strike.Visible = false;
            f.Stamp.Visible = !u.Alive;
            f.Frame.Visible = true;
            f.Portrait.Texture = Game.AgentPortrait(u);
            // Ein Bild je Agent: angeschlagen ist es rötlich gedämpft, niedergegangen dunkel.
            f.Portrait.Modulate = !u.Alive ? DownTint : u.Hp * 2 < u.MaxHp ? HurtTint : Colors.White;
            f.State.Position = new Vector2(StateLeft, 33);
            f.State.Size = new Vector2(FieldWidth - StateLeft - 2, 44);
            if (!u.Alive)
            {
                f.Stamp.Text = Game.FallStamp(u);
                f.State.Text = "";
                return;
            }
            f.Doing.Text = u.CarriesItem ? "trägt das Buch" : u.BandageTicks > 0 ? "verbindet sich" : u.Sprinting ? "rennt" : Game.AgentRole(index);
            string light = Game.LightWord(u);
            f.State.Text = u.Hp * 2 < u.MaxHp ? (light.Length > 0 ? $"angeschlagen\n{light}" : "angeschlagen") : light;
            f.State.AddThemeColorOverride("font_color", u.Hp * 2 < u.MaxHp || light == "im Licht" ? PaperUi.StampRed : new Color(PaperUi.Ink, 0.8f));
            float width = FieldWidth - 12;
            f.Health.Size = new Vector2(width * u.Hp / u.MaxHp, 7);
            f.Breath.Size = new Vector2(width * u.Stamina / World.MaxStamina(u), 4);
        }

        private static void ShowMissing(Field f, AgentRecord record)
        {
            f.Root.MouseFilter = MouseFilterEnum.Ignore;
            f.Name.Text = record.Name.ToUpperInvariant();
            f.Paper.Color = PaperUi.PaperDark;
            f.Edge.Color = new Color(0f, 0f, 0f, 0.45f);
            f.Alive.Visible = false;
            f.Frame.Visible = false;
            f.Stamp.Visible = false;
            f.Strike.Visible = true;
            // Ohne Bild gehört die ganze Breite dem Grund.
            f.State.Position = new Vector2(5, 34);
            f.State.Size = new Vector2(FieldWidth - 8, 44);
            f.State.Text = record.Dead ? "tot" : record.Infirmary ? "Krankenstube,\nsetzt aus" : "angeschlagen,\nsetzt aus";
            f.State.AddThemeColorOverride("font_color", PaperUi.StampRed);
        }

        private static void ShowEmpty(Field f)
        {
            f.Root.MouseFilter = MouseFilterEnum.Ignore;
            f.Name.Text = "";
            f.Paper.Color = new Color(PaperUi.PaperDark, 0.45f);
            f.Edge.Color = new Color(0f, 0f, 0f, 0.3f);
            f.Alive.Visible = false;
            f.Frame.Visible = false;
            f.Stamp.Visible = false;
            f.Strike.Visible = false;
            f.State.Text = "";
        }

        /// <summary>Inventar des ersten gewählten Agenten: vier Plätze, die Waffe in der Hand ist markiert.</summary>
        private void RefreshInventory(World world, HashSet<int> selected)
        {
            var u = world.Units.Where(x => x.Faction == Faction.Agent && x.Alive && selected.Contains(x.Id)).OrderBy(x => x.Id).FirstOrDefault();
            _owned = u?.Id ?? -1;
            _levels.Visible = u != null;
            if (u == null)
            {
                _owner.Text = "NIEMAND GEWÄHLT";
                _role.Text = "";
                foreach (var button in _slots)
                {
                    SetSlot(button, "", false, false);
                }
                return;
            }
            int more = selected.Count(id => id != u.Id && id < world.Units.Count && world.Units[id].Alive);
            _owner.Text = $"{u.Id + 1} {u.Name.ToUpperInvariant()}{(more > 0 ? $"  +{more}" : "")}";
            _role.Text = Game.AgentRole(u.Id);
            ShowLevel(_legs, u.Legs);
            ShowLevel(_heart, u.Heart);
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i >= u.Items.Count)
                {
                    SetSlot(_slots[i], "frei", false, false);
                    continue;
                }
                var item = u.Items[i];
                bool inHand = u.Weapon == i;
                string count = item.Def.Magazine > 0 ? (inHand && u.ReloadTicks > 0 ? "lädt" : $"{item.Ammo}/{item.Def.Magazine}")
                    : item.Def.Charges > 0 ? $"x{item.Charges}" : "";
                string text = Game.SmokeArmedFor == u.Id && item.Def.Kind == ItemKind.Smoke ? "Rauch: Ziel anklicken" : $"{item.Def.ShortName}  {count}".TrimEnd();
                // Der Dietrich wirkt von selbst an der Tür, ihn anzuklicken bringt nichts.
                SetSlot(_slots[i], (inHand ? "» " : "  ") + text, item.Def.Kind != ItemKind.Lockpick, inHand);
            }
        }

        private static void SetSlot(Button button, string text, bool enabled, bool inHand)
        {
            if (button.Text != text)
            {
                button.Text = text;
            }
            button.Disabled = !enabled;
            button.AddThemeColorOverride("font_color", inHand ? SelectedEdge : PaperUi.PaperColor);
        }

        /// <summary>
        /// Was das Peilgerät zeigt. Die Regel steht nur hier: Es zeigt alle lebenden Wachen in Reichweite, auch die, die noch kein Agent
        /// gesehen hat. Wer das einschränken will, ändert ShowsGuard.
        /// </summary>
        public static class PeilRule
        {
            /// <summary>Reichweite des Geräts in Feldern.</summary>
            public const float Range = 14f;

            public static bool ShowsGuard(World world, Unit guard) => guard.Alive;
        }

        /// <summary>
        /// Peilgerät unten in der Spalte. Alle Interval Sekunden schlägt es aus und hält fest, wer wo steht. Die Punkte leuchten
        /// beim Ausschlag auf und verblassen bis zum nächsten. Die Mitte ist der gewählte Agent, sonst die Bildmitte.
        /// </summary>
        private sealed partial class Peil : Control
        {
            private const float Side = 232f;
            private const float Radius = 102f;
            private const float RingSeconds = 0.7f;
            private const float FadedTo = 0.14f;
            private static readonly Color AgentDot = new(1f, 0.86f, 0.45f);
            private static readonly Color GuardDot = new(0.95f, 0.22f, 0.16f);
            private static readonly Color CivilianDot = new(0.55f, 0.75f, 0.95f);
            private static readonly Color Mark = new(0.9f, 0.9f, 0.82f);
            private static readonly Color Glass = new(0.05f, 0.09f, 0.08f);
            private static readonly Color Grid = new(0.35f, 0.5f, 0.42f, 0.35f);

            private enum Kind { Agent, Chosen, Guard, Civilian, Goal, Car }

            private readonly List<(Vector2 At, Kind Kind)> _dots = new();
            private float _since;
            private int _pings;
            private bool _soundMissing;

            public Main Game = null!;
            public float Interval = PeilSeconds;

            public string State => $"{_pings}: {_dots.Count(d => d.Kind is Kind.Agent or Kind.Chosen)} Agenten, {_dots.Count(d => d.Kind == Kind.Guard)} Wachen, "
                + $"{_dots.Count(d => d.Kind == Kind.Civilian)} Zivilisten";

            public override void _Ready()
            {
                AnchorTop = 1f;
                AnchorBottom = 1f;
                OffsetLeft = Margin;
                OffsetRight = Margin + Side;
                OffsetTop = -Side - Margin - 24;
                OffsetBottom = -Margin - 24;
                MouseFilter = MouseFilterEnum.Ignore;
                var title = PaperUi.Text("PEILGERÄT", 11, new Color(PaperUi.PaperColor, 0.8f), PaperUi.Serif);
                title.Position = new Vector2(2, -16);
                AddChild(title);
                _since = Interval;
            }

            public override void _Process(double delta)
            {
                if (!IsVisibleInTree() || Game.World == null)
                {
                    return;
                }
                _since += (float)delta;
                if (_since >= Interval)
                {
                    _since = 0f;
                    Ping();
                }
                QueueRedraw();
            }

            private void Ping()
            {
                _pings++;
                var world = Game.World;
                var center = Center(world);
                _dots.Clear();
                foreach (var u in world.Units)
                {
                    if (u.Faction == Faction.Agent && u.Alive)
                    {
                        Add(Tile(u.X, u.Y) - center, Game.Selected.Contains(u.Id) ? Kind.Chosen : Kind.Agent, false);
                    }
                    else if (u.Faction == Faction.Guard && PeilRule.ShowsGuard(world, u))
                    {
                        Add(Tile(u.X, u.Y) - center, Kind.Guard, false);
                    }
                }
                // Die Passanten der Kulisse, in Kartenfeldern. Die Liste gehört Passersby und wird jedes Mal neu gelesen.
                if (Game.Passersby != null)
                {
                    foreach (var tile in Game.Passersby.Positions)
                    {
                        Add(tile - center, Kind.Civilian, false);
                    }
                }
                // Ziel und Fluchtwagen bleiben als Marke am Rand, wenn sie zu weit weg sind: so zeigen sie die Richtung.
                if (!world.ItemTaken)
                {
                    Add(Tile(world.ItemX, world.ItemY) - center, Kind.Goal, true);
                }
                var exit = world.Exit;
                Add(Tile((exit.X0 + exit.X1) / 2, (exit.Y0 + exit.Y1) / 2) - center, Kind.Car, true);
                if (!Game.Finished && !_soundMissing)
                {
                    try
                    {
                        Game.Sound.PlayEverywhere("peil_ping", -22f);
                    }
                    catch (KeyNotFoundException)
                    {
                        // Der Klang kommt von der Ton-Session. Solange er fehlt, schlägt das Gerät stumm aus.
                        _soundMissing = true;
                    }
                }
            }

            private Vector2 Center(World world)
            {
                var chosen = world.Units.Where(u => u.Faction == Faction.Agent && u.Alive && Game.Selected.Contains(u.Id)).OrderBy(u => u.Id).FirstOrDefault();
                return chosen != null ? Tile(chosen.X, chosen.Y) : Game.ScreenToMap(Game.CameraCenter);
            }

            private static Vector2 Tile(int x, int y) => new Vector2(x, y) / World.TileUnits;

            private void Add(Vector2 offset, Kind kind, bool keepAtRim)
            {
                // Um 45 Grad gedreht wie die Stadt im Bild: Was auf dem Schirm rechts liegt, liegt auch im Gerät rechts.
                var at = new Vector2(offset.X - offset.Y, offset.X + offset.Y) * (Radius / PeilRule.Range / Mathf.Sqrt2);
                if (at.Length() > Radius - 4f)
                {
                    if (!keepAtRim)
                    {
                        return;
                    }
                    at = at.Normalized() * (Radius - 4f);
                }
                _dots.Add((at, kind));
            }

            public override void _Draw()
            {
                var mid = new Vector2(Side / 2f, Side / 2f);
                DrawRect(new Rect2(0, 0, Side, Side), PaperUi.PaperDark);
                DrawRect(new Rect2(0, 0, Side, Side), new Color(0f, 0f, 0f, 0.5f), false, 2f);
                DrawCircle(mid, Radius + 6f, new Color(0.42f, 0.34f, 0.18f));
                DrawCircle(mid, Radius + 2f, Glass);
                DrawArc(mid, Radius * 0.5f, 0f, Mathf.Tau, 48, Grid, 1f);
                DrawLine(mid - new Vector2(Radius, 0), mid + new Vector2(Radius, 0), Grid, 1f);
                DrawLine(mid - new Vector2(0, Radius), mid + new Vector2(0, Radius), Grid, 1f);
                float t = Mathf.Clamp(_since / Interval, 0f, 1f);
                // Schnell aufleuchten, langsam verblassen.
                float glow = Mathf.Lerp(1f, FadedTo, Mathf.Pow(t, 0.6f));
                if (_since < RingSeconds)
                {
                    float r = _since / RingSeconds;
                    DrawArc(mid, Radius * r, 0f, Mathf.Tau, 64, new Color(0.6f, 0.95f, 0.75f, 0.7f * (1f - r)), 2f);
                }
                foreach (var (at, kind) in _dots)
                {
                    var p = mid + at;
                    switch (kind)
                    {
                        case Kind.Goal:
                            // Kreuz: das Ziel
                            DrawLine(p - new Vector2(5, 0), p + new Vector2(5, 0), new Color(Mark, glow), 2f);
                            DrawLine(p - new Vector2(0, 5), p + new Vector2(0, 5), new Color(Mark, glow), 2f);
                            break;
                        case Kind.Car:
                            // Offenes Viereck: der Fluchtwagen
                            DrawRect(new Rect2(p - new Vector2(4, 4), new Vector2(8, 8)), new Color(Mark, glow), false, 2f);
                            break;
                        case Kind.Chosen:
                            DrawCircle(p, 4f, new Color(AgentDot, glow));
                            DrawArc(p, 6.5f, 0f, Mathf.Tau, 16, new Color(AgentDot, glow * 0.8f), 1f);
                            break;
                        case Kind.Agent:
                            DrawCircle(p, 3.5f, new Color(AgentDot, glow));
                            break;
                        case Kind.Guard:
                            DrawCircle(p, 3.5f, new Color(GuardDot, glow));
                            break;
                        default:
                            DrawCircle(p, 3f, new Color(CivilianDot, glow));
                            break;
                    }
                }
            }
        }
    }
}
