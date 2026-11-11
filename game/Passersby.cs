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
    /// </summary>
    public partial class Passersby : Node2D
    {
        // Zeilen im Figurenblatt. Sobald die Animation eigene Zeilen für Passanten liefert, nur hier eintragen.
        private static readonly string[] Rows = { "Arbeiter", "Laufbursche" };
        // Spalten fürs Kauern, falls eine Zeile sie hat. Sonst duckt sich die Figur behelfsweise durch Stauchen.
        private static readonly string[] CowerColumns = { "cower", "kauern", "duck" };

        private const int MaxPeople = 5;
        private const int PeopleAtStart = 4;
        private const float MinSpawnPause = 5f;
        private const float MaxSpawnPause = 12f;
        private const float WalkTilesPerSecond = 1.25f;
        private const float HurryTilesPerSecond = 1.9f;
        private const float RunTilesPerSecond = 3.3f;
        private const float DefaultWalkStride = 928f;
        private const float DefaultRunStride = 1635f;
        private const float IdleFramesPerSecond = 1.6f;
        private const float LookFramesPerSecond = 2.2f;
        private const float MinLight = 0.3f;
        private const float Dim = 0.86f;
        private const float LaneJitter = 0.28f;
        private const float CalmSeconds = 28f;
        private const float ShotHeardTiles = 16f;
        private const float AlarmHeardTiles = 12f;
        private const float BodySeenTiles = 4.5f;
        private const float BodyCheckSeconds = 0.4f;
        private const float MinReactSeconds = 0.12f;
        private const float MaxReactSeconds = 0.7f;
        private const float CowerShare = 0.3f;
        private const float CowerSquash = 0.7f;
        private const float GetUpSeconds = 4f;
        // DepthZ liegt 20 über dem Objekt dahinter, Einheiten beginnen 2 darüber. So bleiben Passanten unter ihnen.
        private const int UnitsAbove = 19;

        // Gehwege der Längsstraße: westlich und östlich der Gleise. Die Gassen zwischen den Häusern bleiben frei.
        private const float WestLane = 6.95f;
        private const float EastLane = 11.1f;
        private const float NorthEdge = -0.8f;
        private const float SouthEdge = 20.8f;
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
        }

        private sealed class Person
        {
            public Sprite2D Sprite = null!;
            public string Row = "";
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
        }

        private readonly Random _rng = new();
        private readonly List<Person> _people = new();
        private readonly Dictionary<string, (int Start, int Count)> _columns = new();
        private readonly Dictionary<string, HashSet<string>> _has = new();
        private readonly Dictionary<string, (float Walk, float Run)> _strides = new();
        private int _views = 5;
        private World? _seenWorld;
        private int _lastShotTick;
        private bool _alarmRunning;
        private float _time;
        private float _calmAt;
        private float _nextSpawn;
        private float _nextBodyCheck;

        public Main Game = null!;
        public string FiguresPath = "";

        /// <summary>Wie viele Passanten gerade zu sehen sind. Für Prüfläufe.</summary>
        public int Count => _people.Count;

        private float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

        /// <summary>Welche Zeile des Figurenblatts ein neuer Passant bekommt. Die einzige Stelle, an der sie gewählt wird.</summary>
        private string PickRow() => Rows[_rng.Next(Rows.Length)];

        public override void _Ready()
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(FiguresPath));
            var root = doc.RootElement;
            _views = root.GetProperty("views").GetInt32();
            foreach (var c in root.GetProperty("columns").EnumerateObject())
            {
                _columns[c.Name] = (c.Value[0].GetInt32(), c.Value[1].GetInt32());
            }
            if (root.TryGetProperty("has", out var has))
            {
                foreach (var row in has.EnumerateObject())
                {
                    var set = new HashSet<string>();
                    foreach (var name in row.Value.EnumerateArray())
                    {
                        set.Add(name.GetString() ?? "");
                    }
                    _has[row.Name] = set;
                }
            }
            if (root.TryGetProperty("stride", out var strides))
            {
                foreach (var row in strides.EnumerateObject())
                {
                    float walk = row.Value.TryGetProperty("walk", out var w) ? w.GetSingle() : DefaultWalkStride;
                    float run = row.Value.TryGetProperty("run", out var r) ? r.GetSingle() : DefaultRunStride;
                    _strides[row.Name] = (walk, run);
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
            if (_time >= _calmAt && _time >= _nextSpawn && _people.Count < MaxPeople)
            {
                _nextSpawn = _time + Rand(MinSpawnPause, MaxSpawnPause);
                Arrive();
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
            _nextSpawn = _time + Rand(MinSpawnPause, MaxSpawnPause);
            foreach (var p in _people)
            {
                p.Sprite.QueueFree();
            }
            _people.Clear();
            // Die Straße ist schon belebt, wenn der Einsatz beginnt.
            var waiting = Add(new Vector2(StopLane, Rand(StopNorth, StopSouth)));
            waiting.Plan.Add(new Step { To = waiting.Tile, Wait = Rand(6f, 16f), Look = true });
            BoardTram(waiting);
            var sheltered = Add(Shelters[0]);
            sheltered.Plan.Add(new Step { To = sheltered.Tile, Wait = Rand(10f, 24f), Look = true });
            LeaveTo(sheltered, south: true);
            var south = Add(new Vector2(WestLane, Rand(13.5f, 16f)));
            LeaveTo(south, south: false);
            var north = Add(new Vector2(EastLane, Rand(0.5f, 2f)));
            north.Plan.Add(new Step { To = Shelters[3], Wait = Rand(8f, 18f), Look = true });
            north.Plan.Add(new Step { To = new Vector2(north.Lane, NorthEdge) });
            while (_people.Count > PeopleAtStart)
            {
                _people[^1].Sprite.QueueFree();
                _people.RemoveAt(_people.Count - 1);
            }
        }

        private Person Add(Vector2 tile)
        {
            var person = new Person
            {
                Sprite = Game.NewFigureSprite(), Row = PickRow(), Tile = tile, Phase = Rand(0f, 10f),
                Lane = (tile.X > 9f ? EastLane : WestLane) + Rand(-LaneJitter, LaneJitter),
            };
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
                    p.Plan.Add(new Step { To = new Vector2(StopLane + Rand(-0.15f, 0.15f), Rand(StopNorth, StopSouth)), Wait = Rand(7f, 18f), Look = true });
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
                    p.Plan.Add(new Step { To = shelter, Wait = Rand(6f, 16f), Look = true });
                    p.Plan.Add(new Step { To = new Vector2(p.Lane, shelter.Y) });
                    p.Plan.Add(new Step { To = new Vector2(p.Lane, NorthEdge) });
                    break;
                }
                case 3:
                {
                    // Auf dem östlichen Gehweg ein Stück herein, an der Hallenwand rauchen, wieder hinaus.
                    var p = Add(new Vector2(EastLane, NorthEdge));
                    p.Plan.Add(new Step { To = Shelters[3], Wait = Rand(8f, 20f), Look = true });
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

        private void Startle(Person p, Vector2 noise)
        {
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
                        if (step.Wait > 0f && p.Mode != Mode.Flee)
                        {
                            p.Mode = Mode.Wait;
                            p.Timer = step.Wait;
                            // Wer wartet, schaut zur Straße.
                            p.Facing = new Vector2(p.Tile.X < 9f ? 1f : -1f, 0.25f).Normalized();
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
            p.Walked += movedTiles * World.TileUnits;
            var (walkStride, runStride) = _strides.TryGetValue(p.Row, out var s) ? s : (DefaultWalkStride, DefaultRunStride);
            int column;
            float squash = 1f;
            switch (p.Mode)
            {
                case Mode.Flee:
                    column = Frame("run", p.Walked / runStride);
                    break;
                case Mode.Walk:
                    column = Frame("walk", p.Walked / walkStride);
                    break;
                case Mode.Cower:
                    column = CowerFrame(p, out squash);
                    break;
                case Mode.Wait when p.Plan.Count > 0 && p.Plan[0].Look:
                case Mode.GetUp:
                    // Zwischendurch schaut sich die Figur um.
                    column = (_time + p.Phase) % 9f < 2.7f
                        ? Loop("look", (_time + p.Phase) % 9f * LookFramesPerSecond)
                        : Loop("idle", (_time + p.Phase) * IdleFramesPerSecond);
                    break;
                default:
                    column = Loop("idle", (_time + p.Phase) * IdleFramesPerSecond);
                    break;
            }
            // Am Boden gedacht wie bei den Einheiten: x - y läuft im Bild quer, x + y zur Kamera. Westen entsteht durch Spiegeln.
            float across = p.Facing.X - p.Facing.Y, toward = p.Facing.X + p.Facing.Y;
            float angle = Mathf.RadToDeg(Mathf.Atan2(toward, Mathf.Abs(across)));
            int view = Mathf.Clamp(Mathf.RoundToInt((90f - angle) / 45f), 0, _views - 1);
            int ux = (int)(p.Tile.X * World.TileUnits), uy = (int)(p.Tile.Y * World.TileUnits);
            var sprite = p.Sprite;
            sprite.RegionRect = Game.FigureFrame(p.Row, view, column).Region;
            sprite.FlipH = across < 0f;
            sprite.Scale = Game.FigureScale * new Vector2(1f, squash);
            sprite.Position = Game.ToScreen(ux, uy) - Game.FigureFoot * new Vector2(1f, squash);
            // Unter den Einheiten derselben Tiefe: Agenten und Wachen bleiben im Zweifel obenauf.
            sprite.ZIndex = Game.DepthZ(Mathf.Clamp(p.Tile.X, 0.05f, 19.95f), Mathf.Clamp(p.Tile.Y, 0.05f, 19.95f)) - UnitsAbove;
            var light = Game.FigureLight(Mathf.Clamp(ux, 0, 20 * World.TileUnits - 1), Mathf.Clamp(uy, 0, 20 * World.TileUnits - 1), MinLight);
            sprite.Modulate = new Color(light.R * Dim, light.G * Dim, light.B * Dim, EdgeFade(p.Tile.Y));
        }

        /// <summary>Am Kartenrand blendet die Figur aus, statt hart zu verschwinden.</summary>
        private static float EdgeFade(float y) => Mathf.Clamp(Mathf.Min(y - NorthEdge, SouthEdge - y) / 0.7f, 0f, 1f);

        private int Frame(string name, float strides)
        {
            var (start, count) = _columns.TryGetValue(name, out var c) ? c : (0, 1);
            return start + (int)(strides * count) % count;
        }

        private int Loop(string name, float frames)
        {
            var (start, count) = _columns.TryGetValue(name, out var c) ? c : (0, 1);
            return start + (int)frames % count;
        }

        private int CowerFrame(Person p, out float squash)
        {
            foreach (string name in CowerColumns)
            {
                if (_columns.ContainsKey(name) && _has.TryGetValue(p.Row, out var has) && has.Contains(name))
                {
                    squash = 1f;
                    return Loop(name, (_time + p.Phase) * IdleFramesPerSecond);
                }
            }
            squash = CowerSquash;
            return Loop("idle", 0f);
        }
    }
}
