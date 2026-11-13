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
        private const float EdgeMargin = 8f;
        private const float TopMargin = 36f;
        private const float BottomMargin = 70f;

        // Sätze mit Spielhinweis sind gegen Karte und Regeln geprüft. Beim Ändern der Mission mitprüfen.
        private static readonly string[] GuardTalk =
        {
            "Noch drei Stunden Schicht.", "Sauwetter. Und das für den Lohn.", "Hast du was gehört? Nein? Gut.",
            "Der Alte will das Buch morgen früh zurück.", "Meine Stiefel sind durch.", "Wenn die Streikenden kommen, bin ich weg.",
            "Sag dem Brandt, er soll selbst Wache schieben.", "Mir ist kalt bis in die Knochen.", "Drei Kinder, und der Lohn kommt zu spät.",
            "Das Luftschiff sieht auch nicht mehr als wir.", "Hörst du den Donner? Das wird noch schlimmer.", "Wer hat schon wieder die Zigaretten?",
            "Die Ratten werden auch immer dreister.", "Bei dem Regen kommt doch keiner.", "Ich hätte zur Bahn gehen sollen.",
            "Mein Schwager streikt auch. Reden wir nicht drüber.", "Der Kaffee war wieder nur heißes Wasser.", "Noch ein Winter hier, und ich hau ab.",
            "Pieper sagt, bald kommt der Notstand.", "Kriegt der Werkschutz die Straße, sind wir die Nächsten, die gehen.", "Die Bank kauft schon die Häuser in der Altstadt.",
            // Stimmt: Posten am Wagen und am Buch drehen sich alle 3 Sekunden.
            "Der Posten am Wagen dreht sich alle paar Sekunden.", "Der am Buch schaut reihum, wie ein Uhrzeiger.",
            // Stimmt: Die Tür des Lagers ist verschlossen, dahinter liegt die Lohnkasse.
            "Die Hintertür vom Lager bleibt zu. Da liegt die Lohnkasse.",
            // Stimmt: Hinter dem Lager gibt es keinen Rundgang, der Lageplan zeigt das Feld grün.
            "Hinter dem Lager läuft sowieso keiner Streife.",
            // Stimmt: Am Wagen steht der Werkschutz, oben auf der Straße der Schütze, der Hund läuft im Hof an der Fabrik.
            "Der Werkschutz am Wagen nimmt kein Geld.", "Oben an der Straße hat einer ein Gewehr.", "Der Hund im Fabrikhof riecht dich auch im Dunkeln.",
            // Stimmt: Der Hundeführer ist für 300 käuflich, sein Hund gibt dann mit Ruhe. Der Laufbursche am Hotel rennt zur nächsten Wache.
            "Der mit dem Hund verkauft seine Mutter. Den Hund gleich mit.", "Der Junge am Hotel rennt sofort zu uns, wenn er was sieht.",
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
            "Den jungen Roth haben sie auch geholt.", "Der kleine Kurt läuft für die Wache. Für zwei Groschen.", "Rehm sagt, wir halten durch. Der hat leicht reden.", "Woher hat die Streikkasse eigentlich das Geld?",
        };

        private static readonly string[] Noticed = { "Moment. War da was?", "He! Wer da?", "Hab ich was gesehen?" };
        private static readonly string[] Fighting = { "Halt! Stehen bleiben!", "Alarm! Hierher!", "Da drüben! Schießt!" };
        // Rufe beim Namen (docs/story/texte/wachen.md). {name} ist der Rufname des Angesprochenen oder Gefallenen.
        private static readonly string[] Greetings = { "{name}. Alles ruhig?", "Ruhig, {name}.", "{name}, hast du Feuer?", "Noch zwei Stunden, {name}.", "Geh dich wärmen, {name}." };
        private static readonly string[] FallenCalls = { "{name}!", "{name} ist getroffen!", "Sie haben {name}!", "{name}, steh auf!" };
        private const string BoyFallen = "Der Junge! Sie haben den Jungen!";
        private const string DogFallenHandler = "{name}!";
        private const string DogFallenOther = "{name}, such!";
        private const float CallSeconds = 2.5f;
        private static readonly Color GreetingColor = new(0.82f, 0.86f, 0.9f);
        private static readonly Color ShoutColor = new(1f, 0.82f, 0.6f);
        private readonly HashSet<(int Tick, int Speaker, int Subject)> _callsHeard = new();
        private readonly HashSet<string> _housesSpoken = new();
        private CityMood.Stance _stance;
        private const float HouseGreetTiles = 2.2f;
        private const float HouseDoorTiles = 1.3f;
        private static readonly Color HelperColor = new(0.86f, 0.9f, 0.74f);
        private static readonly Color ShutColor = new(0.72f, 0.72f, 0.76f);
        private readonly Dictionary<int, int> _fallenCallAt = new();
        private static readonly string[] Searching = { "Wo ist er hin?", "Ausschwärmen!", "Der kann nicht weit sein." };
        private static readonly string[] Bribed = { "Ich hab nichts gesehen.", "Kein Wort zu niemandem." };
        private static readonly string[] Reporting = { "Wache! Wache!", "Da ist wer! Ich hol die Wache!" };
        private static readonly string[] Bitten = { "Der Hund! Ein Schuss, und alle hören es.", "Weg, du Mistvieh! Soll ich schießen?" };

        // Zurufe der Befreiten aus mission.prisoners[].lines. Jeder fällt einmal, im passenden Moment.
        private const float FollowAfterSeconds = 5f;
        private const float AnswerAfterSeconds = 1.6f;
        private const float GapSeconds = 2.5f;
        private const string AnswerKey = "freedAnswer";
        private static readonly Color PrisonerColor = new(0.84f, 0.93f, 0.82f);
        private static readonly Color AgentColor = new(0.95f, 0.9f, 0.75f);

        /// <summary>Was ein Befreiter schon gesagt hat und seit wann er frei ist.</summary>
        private sealed class Rescued
        {
            public bool WasCaptive = true;
            public float FreedAt = -1f;
            public float QuietUntil;
            public readonly HashSet<string> Said = new();
        }

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
        private readonly HashSet<int> _bittenSpoken = new();
        private readonly Dictionary<int, Rescued> _rescued = new();
        private readonly List<(float At, Func<Vector2?> Where, string Text)> _answers = new();
        private World? _world;
        private float _time;
        private float _nextTalk = 5f;

        public Main Game = null!;

        /// <summary>Wer sonst noch redet, etwa Streikposten. Liefert null, wenn die Person gerade nicht da ist.</summary>
        public List<Func<Vector2?>> Workers { get; } = new();

        private static string Pick(Random rng, string[] lines) => lines[rng.Next(lines.Length)];

        // Sätze mit Spielhinweis gelten nur für ihre Karte. Nennt die Mission eigene Listen, ersetzen sie die des ersten Einsatzes.
        private string[] GuardLines => Game.MissionChatter is { Guards.Count: > 0 } c ? c.Guards.ToArray() : GuardTalk;

        private string[] WorkerLines => Game.MissionChatter is { Others.Count: > 0 } c ? c.Others.ToArray() : WorkerTalk;

        private string BribedLine(Unit u) =>
            Game.MissionChatter is { } c && c.Bribed.TryGetValue(u.Name, out string? line) ? line : Pick(_rng, Bribed);

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
                _bittenSpoken.Clear();
                _speeches.Clear();
                _rescued.Clear();
                _answers.Clear();
                _callsHeard.Clear();
                _fallenCallAt.Clear();
                _housesSpoken.Clear();
                _stance = CityMood.Of(Game);
            }
            ListenToCalls(world);
            ListenToTown(world);
            ListenToPrisoners(world);
            foreach (var u in world.Units)
            {
                if (u.Faction == Faction.Guard && u.Alive && !u.Type.Bites)
                {
                    ListenToGuard(world, u);
                }
                else if (u.Alive && u.Type.Bites && u.Faction == Faction.Guard && u.LastBiteHit && _bittenSpoken.Add(u.Id))
                {
                    AgentCriesOut(world, u);
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

        /// <summary>
        /// Die Befreiten reden an fünf Stellen: wenn die Zelle aufgeht, wenn sie eine Weile mitgelaufen sind, wenn eine
        /// Wache etwas merkt, wenn geschossen wird und am Wagen. Die Zeilen stehen in der Mission. Fehlt eine, bleibt der
        /// Moment stumm.
        /// </summary>
        private void ListenToPrisoners(World world)
        {
            for (int i = _answers.Count - 1; i >= 0; i--)
            {
                if (_time >= _answers[i].At)
                {
                    Say(_answers[i].Where, _answers[i].Text, SpeechSeconds, AgentColor);
                    _answers.RemoveAt(i);
                }
            }
            if (!world.IsRescue)
            {
                return;
            }
            bool noticed = false, fighting = false;
            foreach (var g in world.Units)
            {
                if (g.Faction == Faction.Guard && g.Alive)
                {
                    noticed |= g.GuardState is GuardState.Noticing or GuardState.Investigate or GuardState.Search;
                    fighting |= g.GuardState == GuardState.Combat;
                }
            }
            bool shots = world.Shots.Count > 0 && world.Tick - world.Shots[^1].Tick < World.TickRate;
            foreach (var u in world.Prisoners)
            {
                if (!_rescued.TryGetValue(u.Id, out var state))
                {
                    _rescued[u.Id] = state = new Rescued { WasCaptive = u.Captive };
                }
                if (!u.Alive)
                {
                    continue;
                }
                var lines = Game.Mission.Prisoners.Find(p => p.Name == u.Name)?.Lines;
                if (lines == null)
                {
                    continue;
                }
                if (state.WasCaptive && !u.Captive)
                {
                    state.WasCaptive = false;
                    state.FreedAt = _time;
                    if (Speak(u, state, lines, "freed"))
                    {
                        Answer(world, lines);
                    }
                }
                if (u.Captive || _time < state.QuietUntil)
                {
                    continue;
                }
                // Das Dringlichste zuerst: Schüsse vor Entdeckung, der Wagen vor dem Murren unterwegs.
                if ((shots || fighting) && Speak(u, state, lines, "shots"))
                {
                    continue;
                }
                if (noticed && Speak(u, state, lines, "noticed"))
                {
                    continue;
                }
                if (world.EscapeProgress > 0 && Speak(u, state, lines, "wagon"))
                {
                    continue;
                }
                if (u.FollowId >= 0 && !u.Waiting && _time - state.FreedAt > FollowAfterSeconds)
                {
                    Speak(u, state, lines, "follow");
                }
            }
        }

        private bool Speak(Unit u, Rescued state, Dictionary<string, string> lines, string moment)
        {
            if (!lines.TryGetValue(moment, out string? text) || text.Length == 0 || !state.Said.Add(moment))
            {
                return false;
            }
            // Zwischen zwei Sätzen desselben Befreiten liegt eine Pause, damit sie sich nicht überdecken.
            state.QuietUntil = _time + SpeechSeconds + GapSeconds;
            Say(() => u.Alive ? Game.UnitPos(u) : null, text, SpeechSeconds, PrisonerColor);
            return true;
        }

        /// <summary>Antwort eines Agenten auf den ersten Satz, etwa freedAnswerHanna. Nur wenn dieser Agent dabei ist und lebt.</summary>
        private void Answer(World world, Dictionary<string, string> lines)
        {
            foreach (var (key, text) in lines)
            {
                if (!key.StartsWith(AnswerKey) || text.Length == 0)
                {
                    continue;
                }
                string name = key[AnswerKey.Length..];
                var agent = world.Units.Find(a => a.Faction == Faction.Agent && a.Alive && a.Name == name);
                if (agent != null)
                {
                    _answers.Add((_time + AnswerAfterSeconds, () => agent.Alive ? Game.UnitPos(agent) : null, text));
                }
            }
        }

        private void AgentCriesOut(World world, Unit dog)
        {
            Unit? bitten = null;
            foreach (var a in world.Units)
            {
                if (a.Alive && a.Faction == Faction.Agent && (bitten == null || World.Distance(a, dog) < World.Distance(bitten, dog)))
                {
                    bitten = a;
                }
            }
            if (bitten != null)
            {
                Say(() => bitten.Alive ? Game.UnitPos(bitten) : null, Pick(_rng, Bitten), ShoutSeconds, new Color(0.95f, 0.9f, 0.75f));
            }
        }

        /// <summary>
        /// Wachen rufen einander beim Namen: bei einer Begegnung leise, wenn einer fällt laut. Die Welt meldet, wer wen
        /// ruft, hier steht nur der Satz. Zu sehen ist er wie jeder Zuruf nur, wenn der Trupp den Rufer sieht.
        /// </summary>
        private void ListenToCalls(World world)
        {
            foreach (var call in world.Calls)
            {
                if (world.Tick - call.Tick > World.TickRate || !_callsHeard.Add((call.Tick, call.SpeakerId, call.SubjectId)))
                {
                    continue;
                }
                var speaker = world.Units[call.SpeakerId];
                var subject = world.Units[call.SubjectId];
                if (call.Kind == CallKind.Fallen)
                {
                    // Wer einen Namen ruft, ruft nicht im selben Atemzug noch "Alarm!": siehe ListenToGuard.
                    _fallenCallAt[speaker.Id] = call.Tick;
                }
                if (!speaker.Alive || !world.IsSpottedByAgents(speaker) || Game.IsHiddenByThing(speaker))
                {
                    continue;
                }
                string name = subject.CallName;
                string line;
                if (call.Kind == CallKind.Greeting)
                {
                    if (name.Length == 0)
                    {
                        continue;
                    }
                    line = Pick(_rng, Greetings);
                }
                else if (subject.Type.Reports)
                {
                    line = BoyFallen;
                }
                else if (subject.Type.Id == GuardType.Hund.Id)
                {
                    line = subject.LeashedTo == speaker.Id || speaker.Type.Id == GuardType.Hundefuehrer.Id ? DogFallenHandler : DogFallenOther;
                }
                else
                {
                    line = Pick(_rng, FallenCalls);
                }
                if (name.Length == 0 && line.Contains("{name}"))
                {
                    continue;
                }
                bool fallen = call.Kind == CallKind.Fallen;
                Say(() => speaker.Alive ? Game.UnitPos(speaker) : null, line.Replace("{name}", name), fallen ? ShoutSeconds : CallSeconds, fallen ? ShoutColor : GreetingColor);
            }
            _callsHeard.RemoveWhere(c => world.Tick - c.Tick > World.TickRate * 2);
        }

        /// <summary>
        /// Türen der Stadt: Wer hilft, sagt einen Satz, wenn der Trupp durchgeht. Schweigt die Stadt und ein Agent steht
        /// vor der verschlossenen Tür, kommt höchstens ein Satz von drinnen. Rufe nach der Wache zeigt diese Stelle
        /// nicht: Ihnen muss eine Regel folgen, und die meldet die Welt.
        /// </summary>
        private void ListenToTown(World world)
        {
            foreach (var house in MapSite.Current.Houses)
            {
                if (house.Lines.Count == 0 || _housesSpoken.Contains(house.Id))
                {
                    continue;
                }
                var spot = house.Door ?? house.Window;
                float nearest = float.MaxValue;
                bool greta = false;
                foreach (var a in world.Units)
                {
                    if (a.Alive && a.Faction == Faction.Agent)
                    {
                        nearest = Mathf.Min(nearest, new Vector2(a.X, a.Y).DistanceTo(spot * World.TileUnits) / World.TileUnits);
                        greta |= a.Name == "Greta";
                    }
                }
                bool helps = house.Helper && CityMood.HouseHelps(Game, house.Id);
                string key = helps ? (greta && house.Lines.ContainsKey("passGreta") ? "passGreta" : "pass")
                    : _stance == CityMood.Stance.Silent ? "shut"
                    : _stance == CityMood.Stance.Against && greta ? "spare" : "";
                float reach = helps ? HouseGreetTiles : HouseDoorTiles;
                if (key.Length == 0 || nearest > reach || !house.Lines.TryGetValue(key, out string? text) || text.Length == 0)
                {
                    continue;
                }
                _housesSpoken.Add(house.Id);
                var at = Game.ToScreen((int)(house.Window.X * World.TileUnits), (int)(house.Window.Y * World.TileUnits)) - new Vector2(0, house.Height - 40f);
                Say(() => at, text, SpeechSeconds, helps ? HelperColor : ShutColor);
            }
        }

        private void ListenToGuard(World world, Unit u)
        {
            Func<Vector2?> where = () => u.Alive ? Game.UnitPos(u) : null;
            if (u.Bribed && _bribedSpoken.Add(u.Id))
            {
                Say(where, BribedLine(u), SpeechSeconds, new Color(0.7f, 0.95f, 0.7f));
            }
            if (_lastState.TryGetValue(u.Id, out var before) && before != u.GuardState && OnScreen(Game.UnitPos(u)) && !Game.IsHiddenByThing(u))
            {
                string[]? lines = u.GuardState switch
                {
                    GuardState.Noticing when !u.Type.Reports => Noticed,
                    // Ruft die Wache gerade einen Gefallenen beim Namen, entfällt ihr Kampfruf.
                    GuardState.Combat when _fallenCallAt.TryGetValue(u.Id, out int at) && world.Tick - at <= 2 => null,
                    GuardState.Combat => Fighting,
                    GuardState.Investigate => Searching,
                    GuardState.Report => Reporting,
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
                if (u.Faction == Faction.Guard && u.Alive && !u.Type.Bites && u.GuardState == GuardState.Patrol && !u.Bribed
                    && OnScreen(Game.UnitPos(u)) && !Game.IsHiddenByThing(u))
                {
                    guards.Add(u);
                }
            }
            var workers = Workers.FindAll(w => w() is { } p && OnScreen(p));
            bool workerTurn = workers.Count > 0 && (guards.Count == 0 || _rng.NextDouble() < 0.4);
            if (workerTurn)
            {
                Say(workers[_rng.Next(workers.Count)], Pick(_rng, WorkerLines), SpeechSeconds, new Color(0.9f, 0.85f, 0.72f));
            }
            else if (guards.Count > 0)
            {
                var g = guards[_rng.Next(guards.Count)];
                Say(() => g.Alive ? Game.UnitPos(g) : null, Pick(_rng, GuardLines), SpeechSeconds, new Color(0.82f, 0.86f, 0.9f));
            }
        }

        public override void _Draw()
        {
            var font = PaperUi.Typewriter;
            // Sichtbarer Ausschnitt der Welt: Sätze rücken vom Rand ein und weichen einander nach oben aus.
            var view = GetCanvasTransform().AffineInverse() * GetViewportRect();
            float line = font.GetHeight(FontSize);
            var placed = new List<Rect2>();
            foreach (var s in _speeches)
            {
                if (s.Where() is not { } p)
                {
                    continue;
                }
                float alpha = Mathf.Clamp((s.Until - _time) / FadeSeconds, 0f, 1f);
                float width = font.GetStringSize(s.Text, HorizontalAlignment.Left, -1, FontSize).X;
                var at = p + new Vector2(-width / 2f, -HeadHeight);
                float left = view.Position.X + EdgeMargin;
                at.X = Mathf.Clamp(at.X, left, Mathf.Max(left, view.End.X - EdgeMargin - width));
                at.Y = Mathf.Clamp(at.Y, view.Position.Y + TopMargin, Mathf.Max(view.Position.Y + TopMargin, view.End.Y - BottomMargin));
                var box = new Rect2(at.X - 3f, at.Y - line, width + 6f, line + 3f);
                for (int tries = 0; tries < 6 && placed.Exists(r => r.Intersects(box)); tries++)
                {
                    // Nahe am oberen Rand geht es nach unten weiter, sonst nach oben.
                    float step = at.Y - line - 3f < view.Position.Y + TopMargin ? line + 3f : -(line + 3f);
                    at.Y += step;
                    box.Position += new Vector2(0f, step);
                }
                placed.Add(box);
                DrawStringOutline(font, at, s.Text, HorizontalAlignment.Left, -1, FontSize, 4, new Color(0f, 0f, 0f, 0.95f * alpha));
                DrawString(font, at, s.Text, HorizontalAlignment.Left, -1, FontSize, new Color(s.Color, alpha));
            }
        }
    }
}
