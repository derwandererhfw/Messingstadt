using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Passanten als Kulisse: Sie gehen auf den Gehwegen der Längsstraße, warten an der Haltestelle, stehen unter einem
    /// Dachvorsprung, steigen in die Bahn und aus ihr aus. Fällt ein Schuss, geht Alarm los oder liegt ein Toter in der
    /// Nähe, erschrecken sie: Die meisten rennen aus dem Bild, manche kauern sich hin. Die Straße bleibt leer, bis es
    /// eine Weile ruhig war. Reine Darstellung: Sie stehen nicht in der Welt der Regeln, versperren nichts, melden
    /// nichts, und der Mauszeiger kennt sie nicht. Aus Messingstadt.Core wird nur gelesen.
    /// Das Kesselviertel hat seine festen Gehwege hier im Code. Jede andere Karte nennt ihre Wege in den Daten
    /// (MapSite.Walks): wer geht, von wo nach wo, wo er stehen bleibt. Ohne Wege gibt es dort keine Passanten.
    /// </summary>
    public partial class Passersby : Node2D
    {
        // Die Besetzung, wie die Animation sie im Figurenblatt anlegt: Zeile und Farbvarianten über die Palette.
        // Eine leere Variante ist die Grundfarbe.
        private static readonly (string Name, string[] Variants)[] Cast =
        {
            ("Zivil_Arbeiter", new[] { "", "blau", "braun" }), ("Zivil_Frau", new[] { "", "gruen", "grau" }), ("Zivil_Herr", new[] { "", "grau" }),
            ("Zivil_Junge", new[] { "", "braun" }), ("Zivil_Trinker", new[] { "", "grau" }),
        };
        // Beim Kauern sind die ersten beiden Bilder der Übergang in die Hocke, der Rest die Schleife (docs/figuren.md).
        private const int CowerLoopFrom = 2;
        private const float CowerFramesPerSecond = 3f;
        private const float SmokeFramesPerSecond = 3f;
        private const float SmokeEverySeconds = 7f;
        private const float ReadFramesPerSecond = 1f;
        private const float StartleFramesPerSecond = 10f;

        private const float WalkTilesPerSecond = 1.25f;
        private const float HurryTilesPerSecond = 1.9f;
        private const float RunTilesPerSecond = 3.3f;
        private const float IdleFramesPerSecond = 1.6f;
        private const float MinLight = 0.3f;
        private const float LaneJitter = 0.28f;
        private const float CalmSeconds = 28f;
        private const float ShotHeardTiles = 16f;
        private const float AlarmHeardTiles = 12f;
        private const float BodySeenTiles = 4.5f;
        private const float BodyCheckSeconds = 0.4f;
        private const float MinReactSeconds = 0.12f;
        private const float MaxReactSeconds = 0.7f;
        private const float CowerShare = 0.3f;
        private const float GetUpSeconds = 4f;
        // Klänge der Ton-Session. Die Pegel sind deren Vorschläge, gehört hat sie noch niemand.
        private const float StepDb = -24f;
        private const float StepReach = 220f;
        private const float StepPause = 0.34f;
        private const float StepHeardPixels = 190f;
        private const float GaspDb = -8f;
        private const float GaspReach = 400f;
        private const float GaspPause = 6f;
        // DepthZ liegt 20 über dem Objekt dahinter, Einheiten beginnen 2 darüber. So bleiben Passanten unter ihnen.
        private const int UnitsAbove = 19;

        // Gehwege der Längsstraße: westlich und östlich der Gleise. Die Gassen zwischen den Häusern bleiben frei.
        private const float WestLane = 6.95f;
        private const float EastLane = 11.1f;
        private const float NorthEdge = -0.8f;
        private const float SouthEdge = 20.8f;
        // So weit vor dem Kartenrand ist eine Figur ganz ausgeblendet.
        private const float BeyondEdge = 0.8f;
        private const float CrossingNorth = 8.6f;
        private const float CrossingSouth = 12.4f;
        private const float StopLane = 7.05f;
        private const float StopNorth = 3f;
        private const float StopSouth = 6.2f;
        private const float DoorX = 7.98f;
        // Unter dem Dachvorsprung an der Hauswand. Der Platz ums Kontobuch (6.5, 3.5) bleibt frei.
        private static readonly Vector2[] Shelters = { new(6.32f, 5.4f), new(6.3f, 1.5f), new(6.32f, 8.3f), new(11.65f, 4.6f) };

        private enum Mode
        {
            Walk,
            Wait,
            Startled,
            Flee,
            Cower,
            GetUp,
        }

        private sealed class Step
        {
            public Vector2 To;
            public float Wait;
            public bool Look;
            public string Pastime = "";
            // Nur auf Wegen aus den Daten: der wievielte Punkt des Wegs das ist und wohin die Figur dort schaut.
            public int Index = -1;
            public Vector2? Facing;
        }

        private sealed class Person
        {
            public Sprite2D Sprite = null!;
            public string Name = "";
            public string Variant = "";
            public float ModeTime;
            public Vector2 Tile;
            public Vector2 Facing = new(0f, 1f);
            public readonly List<Step> Plan = new();
            public Mode Mode;
            public float Timer;
            public float Walked;
            public float Phase;
            public float Lane;
            public bool Cowers;
            public Vector2 Noise;
            public Mode Shown;
            // Der Weg aus den Daten, in Gehrichtung, und der letzte Punkt, den die Figur erreicht hat. Null im Kesselviertel.
            public MapSite.WalkPoint[]? Route;
            public MapSite.Walk? Walk;
            public int Reached;
        }

        private readonly Random _rng = new();
        private readonly List<Person> _people = new();
        private readonly List<Vector2> _positions = new();
        private MapSite _site = null!;
        private float _mapWidth = 20f;
        private float _mapHeight = 20f;
        private int _views = 5;
        private World? _seenWorld;
        private int _lastShotTick;
        private bool _alarmRunning;
        private float _time;
        private float _calmAt;
        private float _nextSpawn;
        private float _nextBodyCheck;
        private float _nextStep;
        private float _nextGasp;
        private int _testScareFrame = -1;
        private int _frames;
        private static readonly Vector2 TestScareTile = new(9f, 9f);

        public Main Game = null!;
        public string FiguresPath = "";

        /// <summary>Wie viele Passanten gerade zu sehen sind. Für Prüfläufe.</summary>
        public int Count => _people.Count;

        /// <summary>
        /// Wo die Passanten gerade stehen, in Feldern der Karte (0 bis 20, mit Nachkommastellen), etwa für ein Peilgerät.
        /// Nur wer auf der Karte ist: Wer am Rand hinein- oder hinausgeht, fehlt. Die Liste wird bei jedem Aufruf neu
        /// gefüllt und gehört dem Aufrufer nicht, also nicht aufbewahren und nicht ändern.
        /// </summary>
        public IReadOnlyList<Vector2> Positions
        {
            get
            {
                _positions.Clear();
                foreach (var p in _people)
                {
                    if (p.Tile.X >= 0f && p.Tile.Y >= 0f && p.Tile.X < _mapWidth && p.Tile.Y < _mapHeight)
                    {
                        _positions.Add(p.Tile);
                    }
                }
                return _positions;
            }
        }

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        // ---------------------------------------------------------------------------------------------------------
        // Die einzige Stelle, an der Passanten das Figurenblatt anfassen. Schnittstelle: docs/figuren.md.
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Wer kommt: Zeile und Farbvariante eines neuen Passanten.</summary>
        private (string Name, string Variant) PickFigure(string[]? only = null)
        {
            var (name, variants) = Cast[_rng.Next(Cast.Length)];
            if (only != null && only.Length > 0)
            {
                // Der Weg legt fest, wer ihn geht. Ein Name, den das Blatt nicht kennt, fällt auf irgendwen zurück.
                string wanted = only[_rng.Next(only.Length)];
                int row = Array.FindIndex(Cast, c => c.Name == wanted);
                if (row >= 0)
                {
                    (name, variants) = Cast[row];
                }
            }
            return (name, variants[_rng.Next(variants.Length)]);
        }

        private void Dress(Sprite2D sprite, string name, string variant) => Game.SetFigureVariant(sprite, name, variant);

        private Rect2 Frame(string name, int view, string motion, int frame) => Game.CivilFrame(name, view, motion, frame % Frames(motion));

        private int Frames(string motion) => Mathf.Max(1, Game.CivilFrames(motion));

        /// <summary>Schrittlänge in Spieleinheiten je Zyklus für walk und run.</summary>
        private float Stride(string name, string motion) => Mathf.Max(1f, Game.CivilStride(name, motion));

        public override void _Ready()
        {
            // Feste Gehwege hat nur das Kesselviertel. Andere Karten brauchen Wege in ihren Daten, sonst bleibt die Straße leer.
            _site = MapSite.Current;
            if (!_site.Passersby && _site.Walks.Count == 0)
            {
                SetProcess(false);
                return;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(FiguresPath));
            var root = doc.RootElement;
            _views = root.GetProperty("views").GetInt32();
            foreach (string a in OS.GetCmdlineUserArgs())
            {
                // Prüflauf: --schreck=<bild> erschreckt die Passanten einmal in diesem Bild, als fiele an der Kreuzung ein Schuss.
                // Gezählt wird in Bildern wie bei --frames, damit Bildfolgen den Moment sicher treffen.
                if (a.StartsWith("--schreck="))
                {
                    _testScareFrame = int.Parse(a["--schreck=".Length..]);
                }
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;
            _time += dt;
            var world = Game.World;
            if (world != _seenWorld)
            {
                Reset(world);
            }
            WatchForTrouble(world);
            _frames++;
            if (_testScareFrame >= 0 && _frames >= _testScareFrame)
            {
                _testScareFrame = -1;
                Scare(TestScareTile, ShotHeardTiles);
            }
            if (_time >= _calmAt && _time >= _nextSpawn && _people.Count < _site.WalkersMax)
            {
                _nextSpawn = _time + Rand(_site.WalkerPause.X, _site.WalkerPause.Y);
                if (_site.Passersby)
                {
                    Arrive();
                }
                else
                {
                    ArriveOnWalk(alreadyThere: false);
                }
            }
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                if (!Update(_people[i], dt))
                {
                    _people[i].Sprite.QueueFree();
                    _people.RemoveAt(i);
                }
            }
        }

        private void Reset(World world)
        {
            _seenWorld = world;
            _lastShotTick = world.Shots.Count > 0 ? world.Shots[^1].Tick : 0;
            _alarmRunning = false;
            _calmAt = 0f;
            _nextSpawn = _time + Rand(_site.WalkerPause.X, _site.WalkerPause.Y);
            _mapWidth = world.Map.Width;
            _mapHeight = world.Map.Height;
            foreach (var p in _people)
            {
                p.Sprite.QueueFree();
            }
            _people.Clear();
            if (!_site.Passersby)
            {
                for (int i = 0; i < _site.WalkersAtStart; i++)
                {
                    ArriveOnWalk(alreadyThere: true);
                }
                return;
            }
            // Die Straße ist schon belebt, wenn der Einsatz beginnt.
            var waiting = Add(new Vector2(StopLane, Rand(StopNorth, StopSouth)));
            waiting.Plan.Add(new Step { To = waiting.Tile, Wait = Rand(6f, 16f), Look = true, Pastime = Pastime(atStop: true) });
            BoardTram(waiting);
            var sheltered = Add(Shelters[0]);
            sheltered.Plan.Add(new Step { To = sheltered.Tile, Wait = Rand(10f, 24f), Look = true, Pastime = Pastime(atStop: false) });
            LeaveTo(sheltered, south: true);
            var south = Add(new Vector2(WestLane, Rand(13.5f, 16f)));
            LeaveTo(south, south: false);
            var north = Add(new Vector2(EastLane, Rand(0.5f, 2f)));
            north.Plan.Add(new Step { To = Shelters[3], Wait = Rand(8f, 18f), Look = true, Pastime = Pastime(atStop: false) });
            north.Plan.Add(new Step { To = new Vector2(north.Lane, NorthEdge) });
            while (_people.Count > _site.WalkersAtStart)
            {
                _people[^1].Sprite.QueueFree();
                _people.RemoveAt(_people.Count - 1);
            }
        }

        /// <summary>
        /// Ein Passant auf einem Weg aus den Daten. Jeder Weg hat höchstens einen Gänger, sonst stünden zwei Betrunkene
        /// an derselben Wand. alreadyThere setzt ihn gleich an den ersten Halt des Wegs: Die Straße lebt schon, wenn
        /// der Einsatz beginnt.
        /// </summary>
        private void ArriveOnWalk(bool alreadyThere)
        {
            float total = 0f;
            foreach (var w in _site.Walks)
            {
                total += _people.Exists(p => p.Walk == w) ? 0f : w.Weight;
            }
            if (total <= 0f)
            {
                return;
            }
            float pick = Rand(0f, total);
            MapSite.Walk? walk = null;
            foreach (var w in _site.Walks)
            {
                if (_people.Exists(p => p.Walk == w))
                {
                    continue;
                }
                walk = w;
                pick -= w.Weight;
                if (pick <= 0f)
                {
                    break;
                }
            }
            var route = (MapSite.WalkPoint[])walk!.Points.Clone();
            if (walk.BothWays && _rng.Next(2) == 0)
            {
                Array.Reverse(route);
            }
            int from = 0;
            if (alreadyThere)
            {
                from = Array.FindIndex(route, point => point.WaitMax > 0f);
                if (from < 0)
                {
                    // Ein Weg ohne Halt: irgendwo unterwegs, nicht am Rand.
                    from = Math.Min(1, route.Length - 2);
                }
            }
            var person = Add(route[from].Tile, walk.Cast);
            person.Walk = walk;
            person.Route = route;
            person.Reached = from;
            for (int i = alreadyThere ? from : 1; i < route.Length; i++)
            {
                person.Plan.Add(new Step
                {
                    To = route[i].Tile, Wait = route[i].WaitMax > 0f ? Rand(route[i].WaitMin, route[i].WaitMax) : 0f,
                    Pastime = route[i].Pastime, Index = i, Facing = route[i].Look,
                });
            }
        }

        private Person Add(Vector2 tile, string[]? cast = null)
        {
            var person = new Person
            {
                Sprite = Game.NewFigureSprite(), Tile = tile, Phase = Rand(0f, 10f),
                Lane = (tile.X > 9f ? EastLane : WestLane) + Rand(-LaneJitter, LaneJitter),
            };
            (person.Name, person.Variant) = PickFigure(cast);
            Dress(person.Sprite, person.Name, person.Variant);
            AddChild(person.Sprite);
            _people.Add(person);
            Place(person, 0f);
            return person;
        }

        /// <summary>Ein neuer Passant kommt: vom Nordrand, vom Südrand oder aus der Bahn.</summary>
        private void Arrive()
        {
            switch (_rng.Next(6))
            {
                case 0:
                {
                    // Aus der Bahn, dann den Gehweg hinunter oder hinauf.
                    var p = Add(new Vector2(DoorX, Rand(StopNorth + 0.4f, StopSouth - 0.4f)));
                    p.Plan.Add(new Step { To = new Vector2(p.Lane, p.Tile.Y) });
                    LeaveTo(p, south: _rng.Next(2) == 0);
                    break;
                }
                case 1:
                {
                    // Zur Haltestelle, warten, einsteigen.
                    var p = Add(new Vector2(WestLane, NorthEdge));
                    p.Plan.Add(new Step { To = new Vector2(StopLane + Rand(-0.15f, 0.15f), Rand(StopNorth, StopSouth)), Wait = Rand(7f, 18f), Look = true, Pastime = Pastime(atStop: true) });
                    BoardTram(p);
                    break;
                }
                case 2:
                {
                    // Von Süden herauf, unterwegs ein Halt unterm Dachvorsprung.
                    var p = Add(new Vector2(WestLane, SouthEdge));
                    p.Plan.Add(new Step { To = new Vector2(p.Lane, CrossingSouth) });
                    p.Plan.Add(new Step { To = new Vector2(p.Lane, CrossingNorth) });
                    var shelter = Shelters[_rng.Next(3)];
                    p.Plan.Add(new Step { To = shelter, Wait = Rand(6f, 16f), Look = true, Pastime = Pastime(atStop: false) });
                    p.Plan.Add(new Step { To = new Vector2(p.Lane, shelter.Y) });
                    p.Plan.Add(new Step { To = new Vector2(p.Lane, NorthEdge) });
                    break;
                }
                case 3:
                {
                    // Auf dem östlichen Gehweg ein Stück herein, an der Hallenwand rauchen, wieder hinaus.
                    var p = Add(new Vector2(EastLane, NorthEdge));
                    p.Plan.Add(new Step { To = Shelters[3], Wait = Rand(8f, 20f), Look = true, Pastime = Pastime(atStop: false) });
                    p.Plan.Add(new Step { To = new Vector2(p.Lane, NorthEdge) });
                    break;
                }
                default:
                {
                    // Einfach durch: von Norden nach Süden oder umgekehrt.
                    bool south = _rng.Next(2) == 0;
                    var p = Add(new Vector2(WestLane, south ? NorthEdge : SouthEdge));
                    LeaveTo(p, south);
                    break;
                }
            }
        }

        /// <summary>Was jemand beim Warten tut: an der Haltestelle eher lesen, an der Hauswand eher rauchen, sonst nur stehen.</summary>
        private string Pastime(bool atStop) => _rng.Next(3) switch { 0 => atStop ? "read" : "smoke", 1 => atStop ? "" : "smoke", _ => "" };

        private void BoardTram(Person p)
        {
            var from = p.Plan.Count > 0 ? p.Plan[^1].To : p.Tile;
            p.Plan.Add(new Step { To = new Vector2(DoorX, Mathf.Clamp(from.Y, StopNorth + 0.4f, StopSouth - 0.4f)) });
        }

        /// <summary>Den westlichen Gehweg entlang aus dem Bild, über die Kreuzung hinweg.</summary>
        private void LeaveTo(Person p, bool south)
        {
            var from = p.Plan.Count > 0 ? p.Plan[^1].To : p.Tile;
            p.Plan.Add(new Step { To = new Vector2(p.Lane, from.Y) });
            if (south && from.Y < CrossingNorth)
            {
                p.Plan.Add(new Step { To = new Vector2(p.Lane, CrossingNorth) });
            }
            if (!south && from.Y > CrossingSouth)
            {
                p.Plan.Add(new Step { To = new Vector2(p.Lane, CrossingSouth) });
            }
            p.Plan.Add(new Step { To = new Vector2(p.Lane, south ? SouthEdge : NorthEdge) });
        }

        /// <summary>Schüsse, Alarm und Tote: Was die Passanten erschreckt, steht alles in der Welt und wird nur gelesen.</summary>
        private void WatchForTrouble(World world)
        {
            if (world.Shots.Count > 0 && world.Shots[^1].Tick > _lastShotTick)
            {
                var shot = world.Shots[^1];
                _lastShotTick = shot.Tick;
                Scare(new Vector2(shot.FromX, shot.FromY) / World.TileUnits, ShotHeardTiles);
            }
            bool running = false;
            foreach (var u in world.Units)
            {
                if (u.Alive && u.AlarmTick > 0)
                {
                    running = true;
                    if (!_alarmRunning)
                    {
                        Scare(new Vector2(u.AlarmX, u.AlarmY) / World.TileUnits, AlarmHeardTiles);
                    }
                    break;
                }
            }
            _alarmRunning = running;
            if (_time < _nextBodyCheck)
            {
                return;
            }
            _nextBodyCheck = _time + BodyCheckSeconds;
            foreach (var u in world.Units)
            {
                if (u.Alive)
                {
                    continue;
                }
                var body = new Vector2(u.X, u.Y) / World.TileUnits;
                foreach (var p in _people)
                {
                    if (p.Mode is Mode.Walk or Mode.Wait && p.Tile.DistanceTo(body) < BodySeenTiles)
                    {
                        Startle(p, body);
                        _calmAt = Mathf.Max(_calmAt, _time + CalmSeconds);
                    }
                }
            }
        }

        private void Scare(Vector2 noise, float heardTiles)
        {
            // Solange es knallt, kommt niemand Neues, auch wenn gerade keiner in Hörweite ist.
            _calmAt = _time + CalmSeconds;
            foreach (var p in _people)
            {
                if (p.Tile.DistanceTo(noise) > heardTiles)
                {
                    continue;
                }
                if (p.Mode is Mode.Walk or Mode.Wait)
                {
                    Startle(p, noise);
                }
                else if (p.Mode is Mode.Cower or Mode.GetUp)
                {
                    // Wer schon kauert, bleibt unten, solange es weiter knallt.
                    p.Mode = Mode.Cower;
                    p.Timer = Rand(CalmSeconds * 0.5f, CalmSeconds);
                }
            }
        }

        /// <summary>
        /// Schritte nur für Passanten nahe der Bildmitte und nie dichter als StepPause, sonst klappert die ganze Straße.
        /// Ein Schritt fällt, wenn der halbe Doppelschritt voll ist.
        /// </summary>
        private void Footstep(Person p, float walkedBefore)
        {
            float half = Stride(p.Name, p.Mode == Mode.Flee ? "run" : "walk") / 2f;
            if ((int)(walkedBefore / half) == (int)(p.Walked / half) || _time < _nextStep)
            {
                return;
            }
            var at = Game.ToScreen((int)(p.Tile.X * World.TileUnits), (int)(p.Tile.Y * World.TileUnits));
            var camera = GetViewport().GetCamera2D();
            if (camera == null || at.DistanceTo(camera.GetScreenCenterPosition()) > StepHeardPixels)
            {
                return;
            }
            _nextStep = _time + StepPause;
            Game.Sound.Play("step_soft", at, StepDb, StepReach);
        }

        private void Startle(Person p, Vector2 noise)
        {
            // Ein scharfes Einatmen, aber nur von einem: Erschrecken alle zugleich, hört man es sonst als Chor.
            if (_time >= _nextGasp)
            {
                _nextGasp = _time + GaspPause;
                Game.Sound.Play("gasp", Game.ToScreen((int)(p.Tile.X * World.TileUnits), (int)(p.Tile.Y * World.TileUnits)), GaspDb, GaspReach);
            }
            p.Mode = Mode.Startled;
            p.Timer = Rand(MinReactSeconds, MaxReactSeconds);
            p.Noise = noise;
            p.Cowers = _rng.NextDouble() < CowerShare;
            // Der erste Blick geht dorthin, wo es geknallt hat.
            if (noise.DistanceTo(p.Tile) > 0.05f)
            {
                p.Facing = (noise - p.Tile).Normalized();
            }
        }

        /// <summary>Weg vom Lärm aus dem Bild: den eigenen Gehweg entlang zu dem Rand, der vom Lärm wegführt.</summary>
        private void Flee(Person p, bool hurry)
        {
            p.Mode = hurry ? Mode.Walk : Mode.Flee;
            p.Plan.Clear();
            if (p.Route != null)
            {
                // Auf dem eigenen Weg zu dem Ende, das weiter vom Lärm liegt: zurück über die Punkte, die er kam, oder voran.
                bool onward = p.Route[^1].Tile.DistanceTo(p.Noise) > p.Route[0].Tile.DistanceTo(p.Noise);
                for (int i = onward ? p.Reached + 1 : p.Reached; i >= 0 && i < p.Route.Length; i += onward ? 1 : -1)
                {
                    p.Plan.Add(new Step { To = p.Route[i].Tile });
                }
                return;
            }
            bool south = p.Noise.Y < p.Tile.Y;
            // Der östliche Gehweg endet im Süden beim Lager, von dort geht es nur nach Norden hinaus.
            if (p.Lane > 9f)
            {
                south = false;
            }
            p.Plan.Add(new Step { To = new Vector2(p.Lane, p.Tile.Y) });
            p.Plan.Add(new Step { To = new Vector2(p.Lane, south ? SouthEdge : NorthEdge) });
        }

        /// <summary>Bewegt eine Figur. Gibt false zurück, wenn sie das Bild verlassen hat.</summary>
        private bool Update(Person p, float dt)
        {
            float moved = 0f;
            switch (p.Mode)
            {
                case Mode.Startled:
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        if (p.Cowers)
                        {
                            p.Mode = Mode.Cower;
                            p.Timer = Rand(CalmSeconds * 0.5f, CalmSeconds);
                        }
                        else
                        {
                            Flee(p, hurry: false);
                        }
                    }
                    break;
                case Mode.Cower:
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        p.Mode = Mode.GetUp;
                        p.Timer = GetUpSeconds;
                    }
                    break;
                case Mode.GetUp:
                    // Erst umsehen, dann mit eiligen Schritten fort.
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        Flee(p, hurry: true);
                    }
                    break;
                case Mode.Wait:
                    p.Timer -= dt;
                    if (p.Timer <= 0f)
                    {
                        p.Plan.RemoveAt(0);
                        p.Mode = Mode.Walk;
                    }
                    break;
                default:
                    if (p.Plan.Count == 0)
                    {
                        return false;
                    }
                    var step = p.Plan[0];
                    var to = step.To - p.Tile;
                    float speed = p.Mode == Mode.Flee ? RunTilesPerSecond : _time < _calmAt ? HurryTilesPerSecond : WalkTilesPerSecond;
                    if (to.Length() <= speed * dt)
                    {
                        moved = to.Length();
                        p.Tile = step.To;
                        if (step.Index >= 0)
                        {
                            p.Reached = step.Index;
                        }
                        if (step.Wait > 0f && p.Mode != Mode.Flee)
                        {
                            p.Mode = Mode.Wait;
                            p.Timer = step.Wait;
                            // Wer wartet, schaut zur Straße. Auf Wegen aus den Daten dorthin, wohin der Punkt weist.
                            if (p.Route == null)
                            {
                                p.Facing = new Vector2(p.Tile.X < 9f ? 1f : -1f, 0.25f).Normalized();
                            }
                            else if (step.Facing is Vector2 look && look.LengthSquared() > 0f)
                            {
                                p.Facing = look.Normalized();
                            }
                        }
                        else
                        {
                            p.Plan.RemoveAt(0);
                        }
                    }
                    else
                    {
                        p.Facing = to.Normalized();
                        moved = speed * dt;
                        p.Tile += p.Facing * moved;
                    }
                    break;
            }
            Place(p, moved);
            return true;
        }

        private void Place(Person p, float movedTiles)
        {
            float before = p.Walked;
            p.Walked += movedTiles * World.TileUnits;
            if (movedTiles > 0f)
            {
                Footstep(p, before);
            }
            p.ModeTime = p.Mode == p.Shown ? p.ModeTime + (float)GetProcessDeltaTime() : 0f;
            p.Shown = p.Mode;
            string motion;
            int frame;
            float beat = _time + p.Phase;
            switch (p.Mode)
            {
                case Mode.Flee:
                    motion = "run";
                    frame = (int)(p.Walked / Stride(p.Name, motion) * Frames(motion));
                    break;
                case Mode.Walk:
                    motion = "walk";
                    frame = (int)(p.Walked / Stride(p.Name, motion) * Frames(motion));
                    break;
                case Mode.Startled:
                    // Der Schreck läuft einmal durch und hält auf dem letzten Bild.
                    motion = "startle";
                    frame = Mathf.Min(Frames(motion) - 1, (int)(p.ModeTime * StartleFramesPerSecond));
                    break;
                case Mode.Cower:
                    // Erst hinunter, dann in der Schleife zittern.
                    motion = "cower";
                    int down = (int)(p.ModeTime * CowerFramesPerSecond * 2f);
                    frame = down < CowerLoopFrom ? down : CowerLoopFrom + (int)(beat * CowerFramesPerSecond) % (Frames(motion) - CowerLoopFrom);
                    break;
                case Mode.Wait when p.Plan.Count > 0 && p.Plan[0].Pastime == "smoke":
                    // Ein Zug an der Zigarette, dann steht die Figur wieder eine Weile.
                    float puff = beat % SmokeEverySeconds * SmokeFramesPerSecond;
                    motion = puff < Game.CivilFrames("smoke") ? "smoke" : "idle";
                    frame = motion == "smoke" ? (int)puff : (int)(beat * IdleFramesPerSecond);
                    break;
                case Mode.Wait when p.Plan.Count > 0 && p.Plan[0].Pastime == "read":
                    motion = "read";
                    frame = (int)(beat * ReadFramesPerSecond);
                    break;
                default:
                    motion = "idle";
                    frame = (int)(beat * IdleFramesPerSecond);
                    break;
            }
            // Blick wie bei den Einheiten: 0 bis 4 für Süd, Südost, Ost, Nordost, Nord. Der Westen entsteht durch Spiegeln.
            // Am Boden gedacht läuft x - y im Bild quer und x + y zur Kamera.
            float across = p.Facing.X - p.Facing.Y, toward = p.Facing.X + p.Facing.Y;
            float angle = Mathf.RadToDeg(Mathf.Atan2(toward, Mathf.Abs(across)));
            int view = Mathf.Clamp(Mathf.RoundToInt((90f - angle) / 45f), 0, _views - 1);
            int ux = (int)(p.Tile.X * World.TileUnits), uy = (int)(p.Tile.Y * World.TileUnits);
            var sprite = p.Sprite;
            sprite.RegionRect = Frame(p.Name, view, motion, frame);
            sprite.FlipH = across < 0f;
            sprite.Position = Game.ToScreen(ux, uy) - Game.FigureFoot;
            // Unter den Einheiten derselben Tiefe: Agenten und Wachen bleiben im Zweifel obenauf.
            sprite.ZIndex = Game.DepthZ(Mathf.Clamp(p.Tile.X, 0.05f, _mapWidth - 0.05f), Mathf.Clamp(p.Tile.Y, 0.05f, _mapHeight - 0.05f)) - UnitsAbove;
            var light = Game.FigureLight(Mathf.Clamp(ux, 0, (int)_mapWidth * World.TileUnits - 1), Mathf.Clamp(uy, 0, (int)_mapHeight * World.TileUnits - 1), MinLight);
            // Das Blatt malt Passanten schon grauer und dunkler als Agenten und Wachen.
            sprite.Modulate = new Color(light.R, light.G, light.B, EdgeFade(p.Tile));
        }

        /// <summary>Am Kartenrand blendet die Figur aus, statt hart zu verschwinden.</summary>
        private float EdgeFade(Vector2 tile)
        {
            float inside = Mathf.Min(Mathf.Min(tile.X, _mapWidth - tile.X), Mathf.Min(tile.Y, _mapHeight - tile.Y)) + BeyondEdge;
            return Mathf.Clamp(inside / 0.7f, 0f, 1f);
        }
    }
}
