using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Waffenkammer vor jedem Einsatz: ein Tisch mit den Akten der Agenten links, der Kiste der Truppe in der Mitte und dem Katalog rechts.
    /// Bedienung: einen Agenten wählen, dann einen Gegenstand anklicken. Rechts unten stehen seine Werte und was man mit ihm tun kann:
    /// kaufen, ausrüsten, ablegen, verkaufen. Die Regeln liegen in Campaign (TryBuy, TrySell, Equip, Unequip), siehe docs/ausruestung.md.
    /// </summary>
    public partial class Armory : PanelContainer
    {
        private const int Small = 13;
        private const int Normal = 15;
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        private enum Source { None, Catalog, Stash, Slot }

        public Main Game = null!;

        private VBoxContainer _body = null!;
        private string? _agent;
        private Source _source;
        private string _item = "";
        private int _slot = -1;
        private string _message = "";
        // Hinweis zum gewählten Gegenstand, etwa fehlendes Geld. Eine Meldung nach einem Handgriff hat Vorrang.
        private string _hint = "";
        private string _focusKey = "";
        private readonly Dictionary<string, Button> _buttons = new();

        /// <summary>Für den Prüflauf: gewählter Agent und Gegenstand.</summary>
        public string State => Visible ? $"{_agent}:{_source}:{_item}" : "";

        public override void _Ready()
        {
            Visible = false;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            OffsetLeft = 24;
            OffsetTop = 24;
            OffsetRight = -24;
            OffsetBottom = -24;
            AddThemeStyleboxOverride("panel", PaperUi.PaperBox(PaperUi.PaperColor));
            _body = new VBoxContainer();
            _body.AddThemeConstantOverride("separation", 8);
            AddChild(_body);
        }

        private Campaign Campaign => Game.Campaign;

        /// <summary>Öffnet die Kammer. Esc und "Zurück" führen auf den Stadtplan.</summary>
        public void Open()
        {
            _message = "";
            _source = Source.None;
            _item = "";
            var roster = Campaign.Roster();
            if (_agent == null || roster.All(a => a.Name != _agent || a.Dead))
            {
                _agent = (roster.FirstOrDefault(a => a.Ready) ?? roster.FirstOrDefault(a => !a.Dead))?.Name;
            }
            Visible = true;
            _focusKey = _agent != null ? "agent:" + _agent : "go";
            Rebuild();
        }

        public void Close() => Visible = false;

        /// <summary>Nach der Pausenmappe bekommt die Kammer die Tasten zurück.</summary>
        public void Refocus() => Rebuild();

        /// <summary>Esc: zurück zum Stadtplan.</summary>
        public bool Back()
        {
            Close();
            Game.ArmoryBack();
            return true;
        }

        private void Rebuild()
        {
            foreach (var child in _body.GetChildren())
            {
                _body.RemoveChild(child);
                child.QueueFree();
            }
            _buttons.Clear();
            _hint = "";

            var head = new HBoxContainer();
            var title = PaperUi.Text("ROTER HAMMER   WAFFENKAMMER", 14, PaperUi.StampRed, PaperUi.Serif);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(title);
            head.AddChild(PaperUi.Text($"Kasse {Campaign.Money}", 22, PaperUi.Ink, PaperUi.Serif));
            _body.AddChild(head);
            _body.AddChild(Rule(2, PaperUi.Ink));

            var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            columns.AddThemeConstantOverride("separation", 22);
            _body.AddChild(columns);
            columns.AddChild(Scrolled(BuildAgents(), 340));
            columns.AddChild(new ColorRect { CustomMinimumSize = new Vector2(1, 0), Color = PaperUi.PaperDark, MouseFilter = MouseFilterEnum.Ignore });
            columns.AddChild(Scrolled(BuildStash(), 220));
            columns.AddChild(new ColorRect { CustomMinimumSize = new Vector2(1, 0), Color = PaperUi.PaperDark, MouseFilter = MouseFilterEnum.Ignore });
            var right = BuildCatalog();
            right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            columns.AddChild(right);

            _body.AddChild(Rule(1, PaperUi.PaperDark));
            var foot = new HBoxContainer();
            foot.AddThemeConstantOverride("separation", 12);
            foot.AddChild(MakeButton("back", "Zurück zum Stadtplan", () => Back(), 220));
            var message = PaperUi.Text(_message.Length > 0 ? _message : _hint, Normal, PaperUi.StampRed, PaperUi.Typewriter);
            message.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            message.HorizontalAlignment = HorizontalAlignment.Center;
            foot.AddChild(message);
            foot.AddChild(MakeButton("go", "Zum Einsatz", Game.LeaveArmory, 220));
            _body.AddChild(foot);

            // Die Wahl bleibt nach jedem Kauf oder Tausch auf derselben Stelle, sonst springt sie bei jedem Enter weg.
            var target = _buttons.GetValueOrDefault(_focusKey);
            if (target == null || target.Disabled)
            {
                target = _buttons.GetValueOrDefault(_agent != null ? "agent:" + _agent : "go") ?? _buttons["go"];
            }
            target.CallDeferred(Control.MethodName.GrabFocus);
        }

        private Control BuildAgents()
        {
            var list = new VBoxContainer();
            list.AddThemeConstantOverride("separation", 4);
            list.AddChild(PaperUi.Text("Akten", 20, PaperUi.Ink, PaperUi.Serif));
            foreach (var record in Campaign.Roster())
            {
                string name = record.Name;
                bool chosen = name == _agent;
                if (record.Dead)
                {
                    var dead = MakeButton("agent:" + name, $"{name.ToUpperInvariant()}   tot", () => { }, 0);
                    dead.Disabled = true;
                    list.AddChild(dead);
                    continue;
                }
                string state = Status(record);
                var agent = MakeButton("agent:" + name, $"{(chosen ? "» " : "")}{name.ToUpperInvariant()}   {state}", () => ChooseAgent(name), 0);
                agent.Alignment = HorizontalAlignment.Left;
                if (!record.Ready)
                {
                    agent.AddThemeColorOverride("font_color", PaperUi.StampRed);
                }
                list.AddChild(agent);
                var slots = new GridContainer { Columns = 2 };
                slots.AddThemeConstantOverride("h_separation", 4);
                slots.AddThemeConstantOverride("v_separation", 4);
                for (int i = 0; i < Campaign.ItemSlots; i++)
                {
                    int slot = i;
                    string? id = slot < record.Items.Count ? record.Items[slot] : null;
                    string text = id != null ? Game.Catalog.Get(id)?.ShortName ?? id : "frei";
                    var button = MakeButton($"slot:{name}:{slot}", text, () => ChooseSlot(name, slot), 0);
                    button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    button.CustomMinimumSize = new Vector2(150, 0);
                    button.AddThemeFontSizeOverride("font_size", Small);
                    if (id == null)
                    {
                        button.AddThemeColorOverride("font_color", PaperUi.PaperDark);
                    }
                    if (chosen && _source == Source.Slot && _slot == slot)
                    {
                        Mark(button);
                    }
                    slots.AddChild(button);
                }
                var indent = new MarginContainer();
                indent.AddThemeConstantOverride("margin_left", 14);
                indent.AddThemeConstantOverride("margin_bottom", 6);
                indent.AddChild(slots);
                list.AddChild(indent);
            }
            return list;
        }

        private Control BuildStash()
        {
            var list = new VBoxContainer();
            list.AddThemeConstantOverride("separation", 4);
            list.AddChild(PaperUi.Text("Kiste der Truppe", 20, PaperUi.Ink, PaperUi.Serif));
            if (Campaign.Stash.Count == 0)
            {
                list.AddChild(PaperUi.Text("leer", Normal, new Color(PaperUi.Ink, 0.55f), PaperUi.Typewriter));
            }
            // Gleiches liegt zusammen, in der Reihenfolge des Katalogs.
            foreach (var group in Campaign.Stash.GroupBy(id => id).OrderBy(g => Game.Catalog.Items.FindIndex(d => d.Id == g.Key)))
            {
                string id = group.Key;
                string name = Game.Catalog.Get(id)?.Name ?? id;
                var button = MakeButton("stash:" + id, group.Count() > 1 ? $"{name} x{group.Count()}" : name, () => Choose(Source.Stash, id), 0);
                button.Alignment = HorizontalAlignment.Left;
                if (_source == Source.Stash && _item == id)
                {
                    Mark(button);
                }
                list.AddChild(button);
            }
            list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
            list.AddChild(Wrapped("Was niemand trägt, liegt hier und bleibt auch, wenn ein Agent fällt.", Small, 210));
            return list;
        }

        private Control BuildCatalog()
        {
            var right = new VBoxContainer();
            right.AddThemeConstantOverride("separation", 4);
            right.AddChild(PaperUi.Text("Katalog", 20, PaperUi.Ink, PaperUi.Serif));
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 6);
            grid.AddThemeConstantOverride("v_separation", 4);
            foreach (var def in Game.Catalog.Items)
            {
                string id = def.Id;
                var button = MakeButton("catalog:" + id, $"{def.Name}   {def.Price}", () => Choose(Source.Catalog, id), 0);
                button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                button.Alignment = HorizontalAlignment.Left;
                if (_source == Source.Catalog && _item == id)
                {
                    Mark(button);
                }
                grid.AddChild(button);
            }
            right.AddChild(grid);
            right.AddChild(Rule(1, PaperUi.PaperDark));
            BuildDetail(right);
            return right;
        }

        /// <summary>Werte, Aktentext und die möglichen Handgriffe für den gewählten Gegenstand.</summary>
        private void BuildDetail(VBoxContainer box)
        {
            var def = _source == Source.None ? null : Game.Catalog.Get(_item);
            if (def == null)
            {
                box.AddChild(Wrapped(_agent != null
                    ? $"{_agent} ist gewählt. Einen Gegenstand im Katalog, in der Kiste oder in einem Platz anklicken."
                    : "Niemand kann etwas tragen.", Normal, 0));
                return;
            }
            box.AddChild(PaperUi.Text(def.Name, 22, PaperUi.Ink, PaperUi.Serif));
            box.AddChild(Wrapped(Values(def), Small, 0));
            box.AddChild(PaperUi.Text($"Preis {def.Price}   Verkauf {Game.Catalog.SellPrice(def.Id)}", Small, new Color(PaperUi.Ink, 0.75f), PaperUi.Typewriter));
            box.AddChild(Wrapped(def.Text, Normal, 0));
            var actions = new HFlowContainer();
            actions.AddThemeConstantOverride("h_separation", 8);
            actions.AddThemeConstantOverride("v_separation", 6);
            box.AddChild(actions);
            var agent = _agent != null ? Campaign.Agent(_agent) : null;
            bool canCarry = agent != null && !agent.Dead && agent.Items.Count < Campaign.ItemSlots;
            int sell = Game.Catalog.SellPrice(def.Id);
            switch (_source)
            {
                case Source.Catalog:
                    Action("buy", agent != null ? $"Kaufen für {agent.Name}" : "Kaufen", canCarry && Campaign.Money >= def.Price, actions,
                        () => Do(Campaign.TryBuy(Game.Catalog, def.Id, _agent), $"{def.Name} gekauft, {_agent} trägt sie.", "buy", BuySound));
                    Action("buystash", "In die Kiste kaufen", Campaign.Money >= def.Price, actions,
                        () => Do(Campaign.TryBuy(Game.Catalog, def.Id), $"{def.Name} gekauft, liegt in der Kiste.", "buystash", BuySound));
                    if (Campaign.Money < def.Price)
                    {
                        _hint = $"Für {def.Name} fehlen {def.Price - Campaign.Money}.";
                    }
                    else if (agent != null && !canCarry)
                    {
                        _hint = $"{agent.Name} hat keinen Platz frei.";
                    }
                    break;
                case Source.Stash:
                    Action("equip", agent != null ? $"Ausrüsten: {agent.Name}" : "Ausrüsten", canCarry, actions,
                        () => Do(Campaign.Equip(_agent!, def.Id), $"{_agent} trägt jetzt {def.Name}.", "stash:" + def.Id, EquipSound));
                    Action("sellstash", $"Verkaufen (+{sell})", true, actions,
                        () => Do(Campaign.TrySell(Game.Catalog, def.Id), $"{def.Name} verkauft für {sell}.", "stash:" + def.Id, SellSound));
                    break;
                case Source.Slot:
                    Action("unequip", "In die Kiste legen", true, actions,
                        () => Do(Campaign.Unequip(_agent!, _slot), $"{def.Name} liegt jetzt in der Kiste.", $"slot:{_agent}:{_slot}", EquipSound));
                    Action("sellslot", $"Verkaufen (+{sell})", true, actions,
                        () => Do(SellSlot(), $"{def.Name} verkauft für {sell}.", $"slot:{_agent}:{_slot}", SellSound));
                    break;
            }
        }

        /// <summary>Verkauft genau den Gegenstand in diesem Platz, auch wenn der Agent dasselbe zweimal trägt.</summary>
        private bool SellSlot() => Campaign.Unequip(_agent!, _slot) && Campaign.TrySell(Game.Catalog, _item);

        private static string Values(ItemDef def)
        {
            string N(double v) => v.ToString("0.#", German);
            return def.Kind switch
            {
                ItemKind.Firearm => $"Schaden {def.Damage}   Reichweite {N(def.Range)}   {N(1 / def.Cooldown)} Schuss/s   Magazin {def.Magazine}   "
                    + $"Treffer {(def.Accuracy >= 0 ? "+" : "")}{def.Accuracy}   Hörweite {N(def.Noise)}",
                ItemKind.Melee => $"Schaden {def.Damage}   Reichweite {N(def.Range)}   lautlos",
                ItemKind.Smoke => $"Wurfweite {N(def.Range)}   Wolke {N(def.Radius)} Felder   {N(def.Duration)} s   einmal",
                ItemKind.Medkit => $"heilt {def.Heal}   {N(def.Duration)} s anlegen   einmal",
                _ => "öffnet Schlösser",
            };
        }

        private static string Status(AgentRecord record) =>
            record.Ready ? "geht mit" : record.Infirmary ? "Krankenstube, setzt aus" : "angeschlagen, setzt aus";

        private void ChooseAgent(string name)
        {
            _agent = name;
            _message = "";
            if (_source == Source.Slot)
            {
                _source = Source.None;
            }
            _focusKey = "agent:" + name;
            Rebuild();
        }

        private void ChooseSlot(string name, int slot)
        {
            _agent = name;
            _message = "";
            var record = Campaign.Agent(name);
            if (slot < record.Items.Count)
            {
                (_source, _item, _slot) = (Source.Slot, record.Items[slot], slot);
                _focusKey = "unequip";
            }
            else
            {
                (_source, _item, _slot) = (Source.None, "", -1);
                _message = $"Platz frei. Etwas aus dem Katalog oder der Kiste für {name} wählen.";
                _focusKey = $"slot:{name}:{slot}";
            }
            Rebuild();
        }

        private void Choose(Source source, string id)
        {
            (_source, _item, _slot) = (source, id, -1);
            _message = "";
            _focusKey = source == Source.Catalog ? "buy" : "equip";
            Rebuild();
            // Gibt es den Handgriff nicht, bleibt die Wahl auf dem Gegenstand.
            if (_buttons.GetValueOrDefault(_focusKey) is not { Disabled: false })
            {
                _focusKey = (source == Source.Catalog ? "catalog:" : "stash:") + id;
                Rebuild();
            }
        }

        // Klänge der Ton-Session. Ein Handgriff, der scheitert, bleibt stumm.
        private const string BuySound = "shop_buy", EquipSound = "shop_equip", SellSound = "shop_sell";
        private const float ShopDb = -10f;

        private void Do(bool done, string success, string focusAfter, string sound)
        {
            if (!done)
            {
                _message = "Das geht nicht.";
                Rebuild();
                return;
            }
            Game.Sound.PlayEverywhere(sound, ShopDb);
            Game.CampaignChanged();
            _message = success;
            // Was weg ist, kann nicht mehr gewählt sein.
            bool stillThere = _source switch
            {
                Source.Stash => Campaign.Stash.Contains(_item),
                Source.Slot => false,
                _ => true,
            };
            if (!stillThere)
            {
                (_source, _item, _slot) = (Source.None, "", -1);
            }
            _focusKey = focusAfter;
            Rebuild();
        }

        private void Action(string key, string text, bool enabled, Container parent, System.Action act)
        {
            var button = MakeButton(key, text, act, 0);
            button.Disabled = !enabled;
            parent.AddChild(button);
        }

        private Button MakeButton(string key, string text, System.Action act, float width)
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 0) };
            PaperUi.InkButton(button);
            button.AddThemeFontSizeOverride("font_size", Normal);
            button.Pressed += () =>
            {
                if (key != "go" && key != "back")
                {
                    Game.Sound.PlayEverywhere("menu_move", -20f);
                }
                act();
            };
            _buttons[key] = button;
            return button;
        }

        private static void Mark(Button button)
        {
            button.AddThemeColorOverride("font_color", PaperUi.StampRed);
            button.Text = "» " + button.Text;
        }

        private static ScrollContainer Scrolled(Control content, float width)
        {
            var scroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(width, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(content);
            return scroll;
        }

        private static ColorRect Rule(int height, Color color) =>
            new() { CustomMinimumSize = new Vector2(0, height), Color = color, MouseFilter = MouseFilterEnum.Ignore };

        private static Label Wrapped(string text, int size, float width)
        {
            var label = PaperUi.Text(text, size, PaperUi.Ink, PaperUi.Typewriter);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(width, 0);
            return label;
        }
    }
}
