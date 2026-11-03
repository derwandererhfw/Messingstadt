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
    /// expect Größe Wert, remember Größe, same Größe, changed Größe, waitfor Größe Wert, attack, bribe, close.
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
                case "close": Game.PropagateNotification((int)NotificationWMCloseRequest); break;
                case "remember": _remembered[arg] = Probe(arg); break;
                case "same": Check(line, _remembered[arg] == Probe(arg), Probe(arg)); break;
                case "changed": Check(line, _remembered[arg] != Probe(arg), Probe(arg)); break;
                default: Check(line, false, "unbekannter Befehl"); break;
            }
        }

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

        private static Button? FindButton(Node node, string text)
        {
            if (node is Button { Disabled: false } button && button.Text == text && button.IsVisibleInTree())
            {
                return button;
            }
            foreach (var child in node.GetChildren())
            {
                if (FindButton(child, text) is { } found)
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
