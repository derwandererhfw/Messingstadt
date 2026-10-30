using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>Verbindet Spiellogik und Darstellung. Enthält selbst keine Spielregeln.</summary>
    public partial class Main : Node2D
    {
        private const float TickSeconds = 1f / World.TickRate;
        private const float ScrollSpeed = 300f;
        private const float MarkerSeconds = 0.8f;

        private sealed record ThingInfo(float X0, float Y0, float X1, float Y1);

        private readonly List<ThingInfo> _things = new();
        private readonly List<Sprite2D> _thingSprites = new();
        private readonly List<Sprite2D> _unitSprites = new();
        private readonly Dictionary<string, int> _figureRows = new();
        private Sprite2D _book = null!;
        private Camera2D _camera = null!;
        private Label _status = null!;
        private Label _roster = null!;
        private Label _banner = null!;
        private MissionData _mission = null!;
        private MapData _map = null!;
        private Texture2D _figures = null!;
        private int _ox, _oy, _cellW, _cellH;
        private float _accumulator;
        private bool _paused;

        // Automatischer Prüflauf: --shot=<datei> --frames=<n> --auto
        private string? _shotPath;
        private int _shotFrame = -1;
        private bool _auto;
        private int _frame;

        public World World { get; private set; } = null!;
        public HashSet<int> Selected { get; } = new();
        public Vector2 MarkerPos { get; private set; }
        public float MarkerTime { get; private set; }

        public override void _Ready()
        {
            ParseArgs();
            // Gleiche Farbe wie der Himmel in ground.png, damit außerhalb der Karte kein Streifen sichtbar ist.
            RenderingServer.SetDefaultClearColor(new Color(52 / 255f, 56 / 255f, 78 / 255f));
            string root = ProjectSettings.GlobalizePath("res://");
            string data = Path.GetFullPath(Path.Combine(root, "..", "data"));
            _map = Loader.ParseMap(File.ReadAllText(Path.Combine(data, "map_01.json")));
            _mission = Loader.ParseMission(File.ReadAllText(Path.Combine(data, "mission_01.json")));

            LoadStatics();
            _camera = new Camera2D { Zoom = new Vector2(2, 2) };
            AddChild(_camera);
            _camera.MakeCurrent();
            AddChild(new Overlay { Game = this, ZIndex = 4000 });
            BuildUi();
            StartMission();
        }

        private void ParseArgs()
        {
            foreach (string a in OS.GetCmdlineUserArgs())
            {
                if (a.StartsWith("--shot="))
                {
                    _shotPath = a["--shot=".Length..];
                }
                else if (a.StartsWith("--frames="))
                {
                    _shotFrame = int.Parse(a["--frames=".Length..]);
                }
                else if (a == "--auto")
                {
                    _auto = true;
                }
            }
        }

        private static Texture2D LoadTexture(string resPath)
        {
            var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(resPath));
            return ImageTexture.CreateFromImage(image);
        }

        private void LoadStatics()
        {
            AddChild(new Sprite2D { Texture = LoadTexture("res://assets/ground.png"), Centered = false, ZIndex = -1 });
            using var manifest = JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/manifest.json")));
            _ox = manifest.RootElement.GetProperty("ox").GetInt32();
            _oy = manifest.RootElement.GetProperty("oy").GetInt32();
            int index = 0;
            foreach (var t in manifest.RootElement.GetProperty("things").EnumerateArray())
            {
                _things.Add(new ThingInfo(t.GetProperty("x0").GetSingle(), t.GetProperty("y0").GetSingle(),
                    t.GetProperty("x1").GetSingle(), t.GetProperty("y1").GetSingle()));
                var sprite = new Sprite2D
                {
                    Texture = LoadTexture("res://assets/things/" + t.GetProperty("file").GetString()),
                    Centered = false,
                    Position = new Vector2(t.GetProperty("px").GetInt32(), t.GetProperty("py").GetInt32()),
                    ZIndex = ThingZ(index),
                };
                AddChild(sprite);
                _thingSprites.Add(sprite);
                index++;
            }
            using var figures = JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/figures.json")));
            _cellW = figures.RootElement.GetProperty("w").GetInt32();
            _cellH = figures.RootElement.GetProperty("h").GetInt32();
            int row = 0;
            foreach (var n in figures.RootElement.GetProperty("names").EnumerateArray())
            {
                _figureRows[n.GetString()!] = row++;
            }
            _figures = LoadTexture("res://assets/figures.png");
            _book = new Sprite2D { Texture = LoadTexture("res://assets/book.png") };
            AddChild(_book);
        }

        private static int ThingZ(int index) => (index + 1) * 32;

        private void BuildUi()
        {
            var layer = new CanvasLayer();
            AddChild(layer);
            var font = 16;
            _status = new Label { Position = new Vector2(12, 8), Size = new Vector2(1250, 60), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _status.AddThemeFontSizeOverride("font_size", font);
            _status.AddThemeColorOverride("font_shadow_color", Colors.Black);
            layer.AddChild(_status);
            _roster = new Label { Position = new Vector2(12, 660) };
            _roster.AddThemeFontSizeOverride("font_size", font);
            _roster.AddThemeColorOverride("font_shadow_color", Colors.Black);
            layer.AddChild(_roster);
            var help = new Label
            {
                Text = "Links: wählen / gehen   Rechts auf Wache: angreifen   1–4 / Leertaste: Auswahl   Pfeile/WASD: Kamera   F: zur Auswahl   P: Pause   R: Neustart",
                Position = new Vector2(12, 772),
            };
            help.AddThemeFontSizeOverride("font_size", 13);
            help.AddThemeColorOverride("font_shadow_color", Colors.Black);
            layer.AddChild(help);
            _banner = new Label { Position = new Vector2(0, 340), Size = new Vector2(1280, 80), HorizontalAlignment = HorizontalAlignment.Center };
            _banner.AddThemeFontSizeOverride("font_size", 40);
            _banner.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
            _banner.AddThemeColorOverride("font_shadow_color", Colors.Black);
            layer.AddChild(_banner);
        }

        private void StartMission()
        {
            foreach (var s in _unitSprites)
            {
                s.QueueFree();
            }
            _unitSprites.Clear();
            World = new World(_map, _mission);
            foreach (var u in World.Units)
            {
                var sprite = new Sprite2D { Texture = _figures, RegionEnabled = true, Centered = false };
                AddChild(sprite);
                _unitSprites.Add(sprite);
            }
            Selected.Clear();
            for (int i = 0; i < _mission.Agents.Count; i++)
            {
                Selected.Add(i);
            }
            _paused = false;
            _accumulator = 0;
            var first = World.Units[0];
            _camera.Position = ToScreen(first.X, first.Y) + new Vector2(0, -60);
            if (_auto)
            {
                MoveSelected((int)_mission.Item[0], (int)_mission.Item[1]);
            }
        }

        public Vector2 ToScreen(int x, int y) => new(
            _ox + (x - y) / (float)World.TileUnits * 32f,
            _oy + (x + y) / (float)World.TileUnits * 16f);

        private Vector2 ScreenToMap(Vector2 p)
        {
            float a = (p.X - _ox) / 32f, b = (p.Y - _oy) / 16f;
            return new Vector2((a + b) / 2f, (b - a) / 2f);
        }

        public override void _Process(double delta)
        {
            HandleCamera((float)delta);
            MarkerTime = Mathf.Max(0f, MarkerTime - (float)delta);
            Selected.RemoveWhere(id => !World.Units[id].Alive);
            if (!_paused)
            {
                _accumulator += (float)delta;
                while (_accumulator >= TickSeconds)
                {
                    _accumulator -= TickSeconds;
                    World.Step();
                }
            }
            if (_auto)
            {
                CenterOnSelection();
            }
            UpdateUnits();
            UpdateUi();
            _frame++;
            if (_shotPath != null && _frame == _shotFrame)
            {
                GetViewport().GetTexture().GetImage().SavePng(_shotPath);
                GetTree().Quit();
            }
        }

        private void HandleCamera(float dt)
        {
            var dir = Vector2.Zero;
            if (Input.IsPhysicalKeyPressed(Key.Left) || Input.IsPhysicalKeyPressed(Key.A)) dir.X -= 1;
            if (Input.IsPhysicalKeyPressed(Key.Right) || Input.IsPhysicalKeyPressed(Key.D)) dir.X += 1;
            if (Input.IsPhysicalKeyPressed(Key.Up) || Input.IsPhysicalKeyPressed(Key.W)) dir.Y -= 1;
            if (Input.IsPhysicalKeyPressed(Key.Down) || Input.IsPhysicalKeyPressed(Key.S)) dir.Y += 1;
            SetCamera(_camera.Position + dir * ScrollSpeed * dt);
        }

        /// <summary>Hält die Bildmitte innerhalb der Karte, damit man nicht in den leeren Himmel scrollt.</summary>
        private void SetCamera(Vector2 p)
        {
            float halfW = _map.Width * 32f, height = _map.Height * 32f;
            _camera.Position = new Vector2(
                Mathf.Clamp(p.X, _ox - halfW, _ox + halfW),
                Mathf.Clamp(p.Y, _oy, _oy + height));
        }

        private void CenterOnSelection()
        {
            var units = World.Units.Where(u => Selected.Contains(u.Id)).ToList();
            if (units.Count > 0)
            {
                SetCamera(ToScreen((int)units.Average(u => u.X), (int)units.Average(u => u.Y)) + new Vector2(0, -20));
            }
        }

        private void UpdateUnits()
        {
            // Verstärkung kommt erst im Einsatz dazu und braucht dann ein eigenes Bild.
            while (_unitSprites.Count < World.Units.Count)
            {
                var added = new Sprite2D { Texture = _figures, RegionEnabled = true, Centered = false };
                AddChild(added);
                _unitSprites.Add(added);
            }
            var order = World.Units.OrderBy(u => u.X + u.Y).ToList();
            for (int rank = 0; rank < order.Count; rank++)
            {
                var u = order[rank];
                var s = _unitSprites[u.Id];
                var foot = ToScreen(u.X, u.Y);
                int row = _figureRows.GetValueOrDefault(u.Sprite, 0);
                float sx = u.FacingX - u.FacingY, sy = (u.FacingX + u.FacingY) / 2f;
                int dir;
                bool flip = false;
                if (Math.Abs(sx) > Math.Abs(sy) * 1.2f)
                {
                    dir = 1;
                    flip = sx < 0;
                }
                else
                {
                    dir = sy >= 0 ? 0 : 2;
                }
                int step = 0;
                if (u.Moving)
                {
                    int[] seq = { 1, 2, 3, 2 };
                    step = seq[u.WalkTick / 3 % 4];
                }
                s.RegionRect = new Rect2((dir * 4 + step) * _cellW, row * _cellH, _cellW, _cellH);
                s.FlipH = flip;
                if (u.Alive)
                {
                    s.RotationDegrees = 0;
                    s.Modulate = Colors.White;
                    s.Position = foot - new Vector2(12, 41);
                    s.ZIndex = ThingZ(InsertIndex(u) - 1) + rank + 1;
                }
                else
                {
                    s.RotationDegrees = 90;
                    s.Modulate = new Color(0.6f, 0.55f, 0.55f);
                    s.Position = foot + new Vector2(20, -8);
                    s.ZIndex = 0;
                }
            }
            UpdateOcclusion();
            _book.Visible = !World.ItemTaken;
            _book.Position = ToScreen(World.ItemX, World.ItemY);
            _book.ZIndex = 1;
        }

        /// <summary>Macht Objekte durchscheinend, die einen lebenden Agenten verdecken.</summary>
        private void UpdateOcclusion()
        {
            for (int i = 0; i < _things.Count; i++)
            {
                var sprite = _thingSprites[i];
                var rect = new Rect2(sprite.Position, sprite.Texture.GetSize());
                bool covers = false;
                foreach (var u in World.Units)
                {
                    if (u.Faction != Faction.Agent || !u.Alive || !IsInFront(_things[i], u))
                    {
                        continue;
                    }
                    if (rect.HasPoint(ToScreen(u.X, u.Y) + new Vector2(0, -20)))
                    {
                        covers = true;
                        break;
                    }
                }
                sprite.Modulate = new Color(1, 1, 1, covers ? 0.45f : 1f);
            }
        }

        private static bool IsInFront(ThingInfo t, Unit u)
        {
            float x = u.X / (float)World.TileUnits, y = u.Y / (float)World.TileUnits;
            float x0 = x - 0.1f, x1 = x + 0.1f, y0 = y - 0.1f, y1 = y + 0.1f;
            bool beforeX = x1 <= t.X0 && !(t.X1 <= x0) && !(t.Y1 <= y0);
            bool beforeY = y1 <= t.Y0 && !(t.Y1 <= y0) && !(t.X1 <= x0);
            return beforeX || beforeY;
        }

        /// <summary>Index des ersten Objekts, das vor der Figur steht.</summary>
        private int InsertIndex(Unit u)
        {
            float x = u.X / (float)World.TileUnits, y = u.Y / (float)World.TileUnits;
            float x0 = x - 0.1f, x1 = x + 0.1f, y0 = y - 0.1f, y1 = y + 0.1f;
            for (int i = 0; i < _things.Count; i++)
            {
                var t = _things[i];
                bool unitBeforeX = x1 <= t.X0 && !(t.X1 <= x0) && !(t.Y1 <= y0);
                bool unitBeforeY = y1 <= t.Y0 && !(t.Y1 <= y0) && !(t.X1 <= x0);
                if (unitBeforeX || unitBeforeY)
                {
                    return i;
                }
            }
            return _things.Count;
        }

        private void UpdateUi()
        {
            var sb = new StringBuilder();
            sb.Append(_mission.Title).Append(": ");
            sb.Append(World.ItemTaken
                ? (World.EscapeProgress > 0 ? "Einsteigen. Stehen bleiben!" : $"{World.ItemName} gesichert. Zum Fluchtwagen im Osten!")
                : World.ItemProgress > 0 ? $"{World.ItemName} wird gesichert. Stehen bleiben!" : _mission.Briefing);
            _status.Text = sb.ToString();
            var r = new StringBuilder();
            int n = 1;
            foreach (var u in World.Units.Where(u => u.Faction == Faction.Agent))
            {
                string mark = Selected.Contains(u.Id) ? "> " : "  ";
                string state = u.Alive ? $"{u.Hp}/{u.MaxHp}" : "ausgeschaltet";
                string item = u.CarriesItem ? " [Kontobuch]" : "";
                r.Append(mark).Append(n++).Append(' ').Append(u.Name).Append("  ").Append(state).Append(item).Append('\n');
            }
            _roster.Text = r.ToString();
            int guards = World.Units.Count(u => u.Faction == Faction.Guard && u.Alive);
            _banner.Text = World.State switch
            {
                MissionState.Won => $"EINSATZ GEGLÜCKT: {RatingText()}  (R: nochmal)",
                MissionState.Lost => "TRUPP VERLOREN  (R: nochmal)",
                _ => _paused ? "PAUSE" : "",
            };
            _status.Text += $"   Wachen: {guards}   Zeit: {World.Tick / World.TickRate} s";
            if (World.TicksUntilReinforcement >= 0)
            {
                _status.Text += $"   ALARM: Verstärkung in {World.TicksUntilReinforcement / World.TickRate + 1} s";
            }
        }

        private string RatingText() => World.Rating switch
        {
            MissionRating.Unseen => "UNENTDECKT",
            MissionRating.Quiet => "LEISE",
            _ => "LAUT",
        };

        public override void _UnhandledInput(InputEvent e)
        {
            if (e is InputEventKey { Pressed: true, Echo: false } key)
            {
                switch (key.PhysicalKeycode)
                {
                    case Key.Key1: SelectAgent(0, key.ShiftPressed); break;
                    case Key.Key2: SelectAgent(1, key.ShiftPressed); break;
                    case Key.Key3: SelectAgent(2, key.ShiftPressed); break;
                    case Key.Key4: SelectAgent(3, key.ShiftPressed); break;
                    case Key.Space:
                        for (int i = 0; i < _mission.Agents.Count; i++)
                        {
                            if (World.Units[i].Alive) Selected.Add(i);
                        }
                        break;
                    case Key.F: CenterOnSelection(); break;
                    case Key.P: _paused = !_paused; break;
                    case Key.R: StartMission(); break;
                }
            }
            else if (e is InputEventMouseButton { Pressed: true } mouse)
            {
                // Position aus dem Ereignis: GetGlobalMousePosition() kann bei schnellen Klicks veraltet sein.
                var worldPos = GetCanvasTransform().AffineInverse() * mouse.Position;
                if (mouse.ButtonIndex == MouseButton.Left)
                {
                    var agent = UnitUnderCursor(worldPos, Faction.Agent);
                    if (agent != null)
                    {
                        if (!mouse.ShiftPressed) Selected.Clear();
                        Selected.Add(agent.Id);
                    }
                    else
                    {
                        var m = ScreenToMap(worldPos);
                        if (MoveSelected((int)Mathf.Floor(m.X), (int)Mathf.Floor(m.Y)))
                        {
                            MarkerPos = worldPos;
                            MarkerTime = MarkerSeconds;
                        }
                    }
                }
                else if (mouse.ButtonIndex == MouseButton.Right)
                {
                    var guard = UnitUnderCursor(worldPos, Faction.Guard);
                    if (guard != null)
                    {
                        foreach (int id in Selected) World.OrderAttack(id, guard.Id);
                    }
                }
            }
        }

        private void SelectAgent(int index, bool add)
        {
            if (!World.Units[index].Alive) return;
            if (!add) Selected.Clear();
            Selected.Add(index);
        }

        private Unit? UnitUnderCursor(Vector2 p, Faction faction)
        {
            Unit? best = null;
            float bestD = 18f;
            foreach (var u in World.Units)
            {
                if (u.Faction != faction || !u.Alive) continue;
                var center = ToScreen(u.X, u.Y) + new Vector2(0, -20);
                float d = Math.Max(Math.Abs(center.X - p.X), Math.Abs(center.Y - p.Y) * 0.6f);
                if (d < bestD)
                {
                    bestD = d;
                    best = u;
                }
            }
            return best;
        }

        /// <summary>Schickt die Auswahl los. Die Verteilung auf freie Felder regelt die Spiellogik.</summary>
        private bool MoveSelected(int tx, int ty)
        {
            return World.OrderGroupMove(Selected, tx, ty);
        }
    }
}
