using System;
using System.Collections.Generic;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Was man nebenbei aufschnappt: Wachen und Arbeiter reden kurze Sätze über ihren Köpfen, im Alarm rufen die Wachen.
    /// Reine Darstellung, die Sätze ändern nichts am Spiel.
    /// </summary>
    public partial class Chatter : Node2D
    {
        private const float MinPause = 6f;
        private const float MaxPause = 14f;
        private const float SpeechSeconds = 4f;
        private const float ShoutSeconds = 2.2f;
        private const float FadeSeconds = 0.6f;
        private const float HeadHeight = 60f;
        private const float ViewMargin = 40f;
        private const int FontSize = 9;
        private const int MaxShown = 4;

        // Sätze mit Spielhinweis sind gegen Karte und Regeln geprüft. Beim Ändern der Mission mitprüfen.
        private static readonly string[] GuardTalk =
        {
            "Noch drei Stunden Schicht.", "Sauwetter. Und das für den Lohn.", "Hast du was gehört? Nein? Gut.",
            "Der Alte will das Buch heute Nacht zurück.", "Meine Stiefel sind durch.", "Wenn die Streikenden kommen, bin ich weg.",
            "Sag dem Brandt, er soll selbst Wache schieben.", "Mir ist kalt bis in die Knochen.", "Drei Kinder, und der Lohn kommt zu spät.",
            "Das Luftschiff sieht auch nicht mehr als wir.", "Hörst du den Donner? Das wird noch schlimmer.", "Wer hat schon wieder die Zigaretten?",
            "Die Ratten werden auch immer dreister.", "Bei dem Regen kommt doch keiner.", "Ich hätte zur Bahn gehen sollen.",
            "Mein Schwager streikt auch. Reden wir nicht drüber.", "Der Kaffee war wieder nur heißes Wasser.", "Noch ein Winter hier, und ich hau ab.",
            // Stimmt: Posten am Wagen und am Buch drehen sich alle 3 Sekunden.
            "Der Posten am Wagen dreht sich alle paar Sekunden.", "Der am Buch schaut reihum, wie ein Uhrzeiger.",
            // Stimmt: Die Tür des Lagers ist verschlossen, dahinter liegt die Lohnkasse.
            "Die Hintertür vom Lager bleibt zu. Da liegt die Lohnkasse.",
            // Stimmt: Hinter dem Lager gibt es keinen Rundgang, der Lageplan zeigt das Feld grün.
            "Hinter dem Lager läuft sowieso keiner Streife.",
            // Stimmt: So wirkt die Lichtregel beim Bemerken.
            "Unter den Laternen erkenn ich dich sofort.", "Im Dunkeln siehst du die Hand vor Augen nicht.", "Halt dich ans Licht, im Schatten sieht man nichts.",
        };

        private static readonly string[] WorkerTalk =
        {
            "Drei Wochen ohne Lohn.", "Die sollen ruhig frieren da oben.", "Halt das Feuer klein, die Wache schaut.",
            "Mein Junge fragt schon nach Brot.", "Solidarität, Kollegen.", "Das Luftschiff kostet mehr als unsere ganze Straße.",
            "Brandt zahlt die Streikbrecher doppelt, sagt man.", "Wenn wir nur das Buch hätten.", "Gib mal das Feuer rüber.",
            "Meine Frau näht jetzt für die Bank.", "Heute Nacht bleibt keiner allein.", "Sechzehn Stunden am Kessel, und wofür?",
            "Im Hotel brennt Licht, für die reicht es immer.", "Hörst du? Das Schichthorn. Ohne uns.", "Noch hält die Kasse vom Hammer.",
            "Mach die Augen auf, die Wache ist nervös.", "Einer hat den Roten Hammer in der Gasse gesehen.", "Gestern haben sie wieder einen abgeholt.",
        };

        private static readonly string[] Noticed = { "Moment. War da was?", "He! Wer da?", "Hab ich was gesehen?" };
        private static readonly string[] Fighting = { "Halt! Stehen bleiben!", "Alarm! Hierher!", "Da drüben! Schießt!" };
        private static readonly string[] Searching = { "Wo ist er hin?", "Ausschwärmen!", "Der kann nicht weit sein." };
        private static readonly string[] Bribed = { "Ich hab nichts gesehen.", "Kein Wort zu niemandem." };

        private sealed class Speech
        {
            public Func<Vector2?> Where = null!;
            public string Text = "";
            public float Until;
            public Color Color;
        }

        private readonly Random _rng = new();
        private readonly List<Speech> _speeches = new();
        private readonly Dictionary<int, GuardState> _lastState = new();
        private readonly HashSet<int> _bribedSpoken = new();
        private World? _world;
        private float _time;
        private float _nextTalk = 5f;

        public Main Game = null!;

        /// <summary>Wer sonst noch redet, etwa Streikposten. Liefert null, wenn die Person gerade nicht da ist.</summary>
        public List<Func<Vector2?>> Workers { get; } = new();

        private static string Pick(Random rng, string[] lines) => lines[rng.Next(lines.Length)];

        private bool OnScreen(Vector2 p)
        {
            var view = GetViewportRect().Size / 2f / 2f + new Vector2(ViewMargin, ViewMargin);
            var d = (p - Game.CameraCenter).Abs();
            return d.X < view.X && d.Y < view.Y;
        }

        private void Say(Func<Vector2?> where, string text, float seconds, Color color)
        {
            if (_speeches.Count < MaxShown)
            {
                _speeches.Add(new Speech { Where = where, Text = text, Until = _time + seconds, Color = color });
            }
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            var world = Game.World;
            if (world != _world)
            {
                _world = world;
                _lastState.Clear();
                _bribedSpoken.Clear();
                _speeches.Clear();
            }
            foreach (var u in world.Units)
            {
                if (u.Faction == Faction.Guard && u.Alive)
                {
                    ListenToGuard(world, u);
                }
            }
            if (_time > _nextTalk)
            {
                _nextTalk = _time + MinPause + (float)_rng.NextDouble() * (MaxPause - MinPause);
                Smalltalk(world);
            }
            _speeches.RemoveAll(s => _time > s.Until || s.Where() == null);
            QueueRedraw();
        }

        private void ListenToGuard(World world, Unit u)
        {
            Func<Vector2?> where = () => u.Alive ? Game.UnitPos(u) : null;
            if (u.Bribed && _bribedSpoken.Add(u.Id))
            {
                Say(where, Pick(_rng, Bribed), SpeechSeconds, new Color(0.7f, 0.95f, 0.7f));
            }
            if (_lastState.TryGetValue(u.Id, out var before) && before != u.GuardState && OnScreen(Game.UnitPos(u)) && !Game.IsHiddenByThing(u))
            {
                string[]? lines = u.GuardState switch
                {
                    GuardState.Noticing => Noticed,
                    GuardState.Combat => Fighting,
                    GuardState.Investigate => Searching,
                    _ => null,
                };
                if (lines != null)
                {
                    Say(where, Pick(_rng, lines), ShoutSeconds, new Color(1f, 0.82f, 0.6f));
                }
            }
            _lastState[u.Id] = u.GuardState;
        }

        private void Smalltalk(World world)
        {
            var guards = new List<Unit>();
            foreach (var u in world.Units)
            {
                if (u.Faction == Faction.Guard && u.Alive && u.GuardState == GuardState.Patrol && !u.Bribed
                    && OnScreen(Game.UnitPos(u)) && !Game.IsHiddenByThing(u))
                {
                    guards.Add(u);
                }
            }
            var workers = Workers.FindAll(w => w() is { } p && OnScreen(p));
            bool workerTurn = workers.Count > 0 && (guards.Count == 0 || _rng.NextDouble() < 0.4);
            if (workerTurn)
            {
                Say(workers[_rng.Next(workers.Count)], Pick(_rng, WorkerTalk), SpeechSeconds, new Color(0.9f, 0.85f, 0.72f));
            }
            else if (guards.Count > 0)
            {
                var g = guards[_rng.Next(guards.Count)];
                Say(() => g.Alive ? Game.UnitPos(g) : null, Pick(_rng, GuardTalk), SpeechSeconds, new Color(0.82f, 0.86f, 0.9f));
            }
        }

        public override void _Draw()
        {
            var font = PaperUi.Typewriter;
            foreach (var s in _speeches)
            {
                if (s.Where() is not { } p)
                {
                    continue;
                }
                float alpha = Mathf.Clamp((s.Until - _time) / FadeSeconds, 0f, 1f);
                float width = font.GetStringSize(s.Text, HorizontalAlignment.Left, -1, FontSize).X;
                var at = p + new Vector2(-width / 2f, -HeadHeight);
                DrawStringOutline(font, at, s.Text, HorizontalAlignment.Left, -1, FontSize, 3, new Color(0f, 0f, 0f, 0.8f * alpha));
                DrawString(font, at, s.Text, HorizontalAlignment.Left, -1, FontSize, new Color(s.Color, alpha));
            }
        }
    }
}
