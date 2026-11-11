using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Prüflauf für die Bedienung: --script=&lt;datei&gt; schickt Tasten und Klicks durch den echten Eingabeweg und vergleicht den Zustand.
    /// Zeilen: wait n, key Name, hold Name, release Name, click Text, clickat x y, shot Datei, print,
    /// expect Größe Wert, remember Größe, same Größe, changed Größe, waitfor Größe Wert, attack, bribe, close,
    /// pointat Name dx dy (Maus über das Feld neben einem Agenten, ohne Klick), hurt Name Punkte.
    /// Am Ende steht die Zahl der Fehler.
    /// </summary>
    public partial class TestScript : Node
    {
        private const int FramesAfterAction = 4;
        private const int MaxWaitFrames = 6000;

        private readonly Dictionary<string, string> _remembered = new();
        private string[] _lines = Array.Empty<string>();
        private int _next;
        private int _wait = 30;
        private int _errors;
        private string _waitName = "", _waitValue = "";
        private int _waitLeft;

        public Main Game = null!;
        public string FilePath = "";

        public override void _Ready()
        {
            // Ein fehlendes Skript darf nicht als fehlerfreier Lauf durchgehen.
            if (!File.Exists(FilePath))
            {
                Check("script " + FilePath, false, "Datei fehlt");
                return;
            }
            _lines = File.ReadAllLines(FilePath);
        }

        public override void _Process(double delta)
        {
            if (_wait-- > 0)
            {
                return;
            }
            if (_waitLeft > 0)
            {
                _waitLeft--;
                if (Probe(_waitName) != _waitValue)
                {
                    return;
                }
                _waitLeft = 0;
            }
            if (_next >= _lines.Length)
            {
                GD.Print($"PRUEFLAUF ENDE: {_errors} Fehler");
                GetTree().Quit(_errors == 0 ? 0 : 1);
                return;
            }
            string line = _lines[_next++].Trim();
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                _wait = FramesAfterAction;
                Run(line);
            }
            else
            {
                _wait = 0;
            }
        }

        private void Run(string line)
        {
            var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string arg = parts.Length > 1 ? parts[1] : "";
            switch (parts[0])
            {
                case "wait": _wait = int.Parse(arg); break;
                case "key": SendKey(arg, true); SendKey(arg, false); break;
                case "hold": SendKey(arg, true); break;
                case "release": SendKey(arg, false); break;
                case "click": Click(arg); break;
                case "clickany":
                    // Auch ausgegraute Knöpfe: Der Klick darf dann nichts bewirken.
                    if (FindButton(Game.ActiveUi(), arg, true) is { } any)
                    {
                        ClickAt(any.GetGlobalRect().GetCenter());
                    }
                    else
                    {
                        Check(line, false, "kein solcher Knopf");
                    }
                    break;
                case "clickat":
                    var at = arg.Split(' ');
                    ClickAt(new Vector2(float.Parse(at[0]), float.Parse(at[1])));
                    break;
                case "shot": GetViewport().GetTexture().GetImage().SavePng(arg); break;
                case "print": GD.Print(string.Join("  ", Game.Probe().Select(p => $"{p.Key}={p.Value}"))); break;
                case "expect":
                    var want = arg.Split(' ', 2);
                    Check(line, Probe(want[0]) == (want.Length > 1 ? want[1] : ""), Probe(want[0]));
                    break;
                case "waitfor":
                    var until = arg.Split(' ', 2);
                    (_waitName, _waitValue, _waitLeft) = (until[0], until[1], MaxWaitFrames);
                    break;
                case "attack":
                    var guard = Game.World.Units.First(u => u.Faction == Faction.Guard && u.Alive);
                    foreach (int id in Game.Selected)
                    {
                        Game.World.OrderAttack(id, guard.Id);
                    }
                    break;
                case "bribe":
                    // Emil zur nächsten käuflichen Wache, ohne dass der Prüflauf sie im Bild treffen muss.
                    var emil = Game.World.Units.First(u => u.Faction == Faction.Agent && u.Skill == World.SkillBookkeeper);
                    var target = Game.World.Units.Where(u => u.Faction == Faction.Guard && u.Alive && u.Type.BribeCost > 0)
                        .OrderBy(u => Math.Abs(u.X - emil.X) + Math.Abs(u.Y - emil.Y)).First();
                    Check(line, Game.World.OrderBribe(emil.Id, target.Id), target.Type.Title);
                    break;
                case "fell":
                    // Ein Agent fällt sofort, damit Stempel und Hilfezeile ohne langes Gefecht prüfbar sind.
                    var fallen = Game.World.Units.FirstOrDefault(u => u.Faction == Faction.Agent && u.Alive && u.Name == arg);
                    Check(line, fallen != null, arg);
                    if (fallen != null)
                    {
                        fallen.Hp = 0;
                    }
                    break;
                case "pointat":
                    var spot = arg.Split(' ');
                    var who = Game.World.Units.First(u => u.Faction == Faction.Agent && u.Name == spot[0]);
                    var tile = Game.TileCenter(who.TileX + int.Parse(spot[1]), who.TileY + int.Parse(spot[2]));
                    var screen = GetViewport().GetFinalTransform() * (Game.GetCanvasTransform() * tile);
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = screen, GlobalPosition = screen });
                    break;
                case "clicktile":
                    // Linksklick auf ein Feld der Welt, gezählt vom Agenten aus.
                    var rel = arg.Split(' ');
                    var from = Game.World.Units.First(u => u.Faction == Faction.Agent && u.Name == rel[0]);
                    ClickAt(Game.GetCanvasTransform() * Game.TileCenter(from.TileX + int.Parse(rel[1]), from.TileY + int.Parse(rel[2])));
                    break;
                case "place":
                    // Stellt den Trupp ans Ziel (item) oder in den Fluchtwagen (exit), damit ein Sieg ohne langen Weg prüfbar ist.
                    var zone = Game.World.Exit;
                    var (px, py) = arg == "exit" ? ((zone.X0 + zone.X1) / 2, (zone.Y0 + zone.Y1) / 2) : (Game.World.ItemX, Game.World.ItemY);
                    foreach (var member in Game.World.Units.Where(u => u.Faction == Faction.Agent && u.Alive))
                    {
                        member.Path.Clear();
                        (member.X, member.Y) = (px, py);
                    }
                    break;
                case "hurt":
                    var hurt = arg.Split(' ');
                    var victim = Game.World.Units.FirstOrDefault(u => u.Faction == Faction.Agent && u.Alive && u.Name == hurt[0]);
                    Check(line, victim != null, hurt[0]);
                    if (victim != null)
                    {
                        victim.Hp = Math.Max(1, victim.Hp - int.Parse(hurt[1]));
                    }
                    break;
                case "seetext": Check(line, HasLabel(Game.ActiveUi(), arg.Replace("\\n", "\n")), arg); break;
                case "close": Game.PropagateNotification((int)NotificationWMCloseRequest); break;
                case "remember": _remembered[arg] = Probe(arg); break;
                case "same": Check(line, _remembered[arg] == Probe(arg), Probe(arg)); break;
                case "changed": Check(line, _remembered[arg] != Probe(arg), Probe(arg)); break;
                default: Check(line, false, "unbekannter Befehl"); break;
            }
        }

        /// <summary>Steht dieser Text sichtbar auf dem Bildschirm?</summary>
        private static bool HasLabel(Node node, string text) =>
            (node is Label label && label.IsVisibleInTree() && label.Text == text) || node.GetChildren().Any(child => HasLabel(child, text));

        private string Probe(string name) => Game.Probe().GetValueOrDefault(name, "?");

        private void Check(string line, bool ok, string found)
        {
            GD.Print($"{(ok ? "OK    " : "FEHLER")} {line}  [{found}]");
            _errors += ok ? 0 : 1;
        }

        private static void SendKey(string name, bool pressed)
        {
            var key = Enum.Parse<Key>(name);
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
        }

        private void Click(string text)
        {
            var button = FindButton(Game.ActiveUi(), text);
            if (button == null)
            {
                Check("click " + text, false, "kein solcher Knopf");
                return;
            }
            ClickAt(button.GetGlobalRect().GetCenter());
        }

        private static Button? FindButton(Node node, string text, bool disabledToo = false)
        {
            if (node is Button button && (disabledToo || !button.Disabled) && button.Text == text && button.IsVisibleInTree())
            {
                return button;
            }
            foreach (var child in node.GetChildren())
            {
                if (FindButton(child, text, disabledToo) is { } found)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>Punkt im Bild der Oberfläche. Die Streckung des Fensters wird wie bei einer echten Maus eingerechnet.</summary>
        private void ClickAt(Vector2 point)
        {
            var window = GetViewport().GetFinalTransform() * point;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = window, GlobalPosition = window });
            foreach (bool pressed in new[] { true, false })
            {
                Input.ParseInputEvent(new InputEventMouseButton
                {
                    Position = window, GlobalPosition = window, ButtonIndex = MouseButton.Left, Pressed = pressed,
                    ButtonMask = pressed ? MouseButtonMask.Left : 0,
                });
            }
        }
    }
}
