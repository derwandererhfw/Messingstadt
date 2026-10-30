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
        private static readonly Color Night = new(0.34f, 0.37f, 0.55f);
        private const float LampEnergy = 1.15f;
        private const int AimTicksStanding = 12;
        private const int AimTicksWalking = 5;
        private const int AimColumn = 12;

        /// <summary>Farbe für Ring und Nummer. Alle Agenten tragen dasselbe, nur die Nummer unterscheidet sie.</summary>
        public static readonly Color AgentColor = new(1f, 0.8f, 0.45f);

        private sealed record ThingInfo(float X0, float Y0, float X1, float Y1);

        private readonly List<ThingInfo> _things = new();
        private readonly List<Sprite2D> _thingSprites = new();
        private readonly List<Image> _thingImages = new();
        private readonly List<PointLight2D> _lampLights = new();
        private readonly Ambience _ambience = new();
        private readonly List<Sprite2D> _unitSprites = new();
        private readonly Dictionary<string, int> _figureRows = new();
        private Sprite2D _book = null!;
        private Camera2D _camera = null!;
        private Label _status = null!;
        private RichTextLabel _roster = null!;
        private Label _banner = null!;
        private PanelContainer _panel = null!;
        private VBoxContainer _panelBody = null!;
        private Campaign _campaign = new();
        private bool _finished;
        private int _lastReward;
        private MissionData _mission = null!;
        private MapData _map = null!;
        private Texture2D _figures = null!;
        private int _ox, _oy, _cellW, _cellH;
        private float _accumulator;
        private bool _paused;

        // Automatischer Prüflauf: --shot=<datei> --frames=<n> --auto
        // Mit --shot-on-fire=<n> entsteht das Bild im Moment des ersten Schusses ab Takt n.
        private int _shotOnFireTick = -1;
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
            RenderingServer.SetDefaultClearColor(new Color(52 / 255f, 56 / 255f, 78 / 255f) * Night);
            AddChild(new CanvasModulate { Color = Night });
            string root = ProjectSettings.GlobalizePath("res://");
            string data = Path.GetFullPath(Path.Combine(root, "..", "data"));
            _map = Loader.ParseMap(File.ReadAllText(Path.Combine(data, "map_01.json")));
            _mission = Loader.ParseMission(File.ReadAllText(Path.Combine(data, "mission_01.json")));

            LoadCampaign();
            LoadStatics();
            _camera = new Camera2D { Zoom = new Vector2(2, 2) };
            AddChild(_camera);
            _camera.MakeCurrent();
            AddLamps();
            // Eigene Ebene, die der Kamera folgt: Ringe und Balken bleiben auch in der Nacht gut lesbar.
            var overlayLayer = new CanvasLayer { Layer = 1, FollowViewportEnabled = true };
            AddChild(overlayLayer);
            overlayLayer.AddChild(new Overlay { Game = this });
            BuildUi();
            StartMission();
        }

        /// <summary>Setzt an jede Straßenlaterne ein warmes Licht, das die Nacht aufhellt.</summary>
        private void AddLamps()
        {
            var gradient = new Gradient
            {
                Offsets = new[] { 0f, 0.35f, 1f },
                Colors = new[] { Colors.White, new Color(0.55f, 0.55f, 0.55f), Colors.Black },
            };
            var glow = new GradientTexture2D
            {
                Gradient = gradient, Width = 256, Height = 256, Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
            };
            foreach (var lamp in _mission.Lamps)
            {
                var light = new PointLight2D
                {
                    Texture = glow,
                    TextureScale = 1.1f,
                    Color = new Color(1f, 0.74f, 0.42f),
                    Energy = LampEnergy,
                    RangeZMin = -1,
                    RangeZMax = 4096,
                    Position = new Vector2(_ox + (float)(lamp[0] - lamp[1]) * 32f, _oy + (float)(lamp[0] + lamp[1]) * 16f),
                };
                AddChild(light);
                _lampLights.Add(light);
            }
            _ambience.Game = this;
            _ambience.Glow = glow;
            _ambience.Lamps = _lampLights;
            _ambience.LampEnergy = LampEnergy;
            float halfW = _map.Width * 32f;
            _ambience.Area = new Rect2(_ox - halfW, _oy, 2 * halfW, _map.Height * 32f);
            AddChild(_ambience);
            AddChild(new RainLayer { ZIndex = 3900 });
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
                else if (a.StartsWith("--shot-on-fire="))
                {
                    _shotOnFireTick = int.Parse(a["--shot-on-fire=".Length..]);
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
                var image = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/things/" + t.GetProperty("file").GetString()));
                _thingImages.Add(image);
                var sprite = new Sprite2D
                {
                    Texture = ImageTexture.CreateFromImage(image),
                    Centered = false,
                    Position = new Vector2(t.GetProperty("px").GetInt32(), t.GetProperty("py").GetInt32()),
                    ZIndex = ThingZ(index),
                };
                AddChild(sprite);
                _thingSprites.Add(sprite);
                index++;
            }
            _ambience.Smoke = ReadPoints(manifest.RootElement, "smoke");
            _ambience.Steam = ReadPoints(manifest.RootElement, "steam");
            _ambience.Fires = ReadPoints(manifest.RootElement, "fires");
            foreach (var p in manifest.RootElement.GetProperty("puddles").EnumerateArray())
            {
                _ambience.Puddles.Add((new Vector2(p[0].GetInt32(), p[1].GetInt32()), p[2].GetInt32()));
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

        private static List<Vector2> ReadPoints(JsonElement root, string name)
        {
            var points = new List<Vector2>();
            foreach (var p in root.GetProperty(name).EnumerateArray())
            {
                points.Add(new Vector2(p[0].GetInt32(), p[1].GetInt32()));
            }
            return points;
        }

        private static int ThingZ(int index) => (index + 1) * 32;

        private void BuildUi()
        {
            var layer = new CanvasLayer { Layer = 2 };
            AddChild(layer);
            var font = 16;
            _status = new Label { Position = new Vector2(12, 8), Size = new Vector2(1250, 60), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _status.AddThemeFontSizeOverride("font_size", font);
            _status.AddThemeColorOverride("font_shadow_color", Colors.Black);
            layer.AddChild(_status);
            _roster = new RichTextLabel
            {
                Position = new Vector2(12, 660), Size = new Vector2(760, 110), BbcodeEnabled = true,
                ScrollActive = false, MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _roster.AddThemeFontSizeOverride("normal_font_size", font);
            _roster.AddThemeColorOverride("font_shadow_color", Colors.Black);
            layer.AddChild(_roster);
            var help = new Label
            {
                Text = "Links: wählen/gehen   Doppelklick: rennen   Rechts: Wache angreifen, Emil allein: bestechen, Otto: Tür öffnen   1–4, Leertaste   WASD   F: Kamera   P   R",
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
            _panel = new PanelContainer { Position = new Vector2(340, 130), CustomMinimumSize = new Vector2(600, 0), Visible = false };
            _panelBody = new VBoxContainer();
            _panelBody.AddThemeConstantOverride("separation", 10);
            _panel.AddChild(_panelBody);
            layer.AddChild(_panel);
        }

        private static string SavePath => ProjectSettings.GlobalizePath("user://kampagne.json");

        private void LoadCampaign()
        {
            _campaign = File.Exists(SavePath) ? Campaign.FromJson(File.ReadAllText(SavePath)) : new Campaign();
        }

        private void SaveCampaign() => File.WriteAllText(SavePath, _campaign.ToJson());

        private void FinishMission()
        {
            _finished = true;
            _lastReward = World.State == MissionState.Won ? Campaign.Reward(World.Rating) : 0;
            _campaign.Finish(World);
            SaveCampaign();
            RefreshPanel();
            _panel.Visible = true;
        }

        private static Label PanelLabel(string text, int size = 16)
        {
            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", size);
            return label;
        }

        /// <summary>Baut den Bildschirm zwischen den Einsätzen neu auf: Ergebnis, Kasse und Ausbau.</summary>
        private void RefreshPanel()
        {
            foreach (var child in _panelBody.GetChildren())
            {
                child.QueueFree();
            }
            bool won = World.State == MissionState.Won;
            _panelBody.AddChild(PanelLabel(won ? $"EINSATZ GEGLÜCKT: {RatingText()}" : "TRUPP VERLOREN", 26));
            if (won)
            {
                string loot = World.LootTaken ? $", dazu {World.LootName} {World.LootValue}" : "";
                _panelBody.AddChild(PanelLabel($"Lohn: {_lastReward}{loot}"));
            }
            _panelBody.AddChild(PanelLabel($"Kasse: {_campaign.Money}    Einsätze: {_campaign.Missions}", 18));
            foreach (var u in World.Units.Where(u => u.Faction == Faction.Agent))
            {
                var record = _campaign.Agent(u.Name);
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                var name = PanelLabel($"{u.Name}: Beine {record.Legs}, Herz {record.Heart}{(u.Alive ? "" : "  (gefallen)")}");
                name.CustomMinimumSize = new Vector2(280, 0);
                row.AddChild(name);
                row.AddChild(UpgradeButton("Beine", record.Name, record.Legs, true));
                row.AddChild(UpgradeButton("Herz", record.Name, record.Heart, false));
                _panelBody.AddChild(row);
            }
            var next = new Button { Text = won ? "Nächster Einsatz" : "Nochmal" };
            next.Pressed += StartMission;
            _panelBody.AddChild(next);
            var reset = new Button { Text = "Neue Kampagne" };
            reset.Pressed += () =>
            {
                _campaign = new Campaign();
                SaveCampaign();
                _mission = Loader.ParseMission(File.ReadAllText(Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data", "mission_01.json"))));
                StartMission();
            };
            _panelBody.AddChild(reset);
        }

        private Button UpgradeButton(string label, string agent, int level, bool legs)
        {
            bool full = level >= Campaign.MaxLevel;
            int cost = Campaign.UpgradeCost(level);
            var button = new Button
            {
                Text = full ? $"{label} voll" : $"{label} + ({cost})",
                Disabled = full || _campaign.Money < cost,
                CustomMinimumSize = new Vector2(130, 0),
            };
            button.Pressed += () =>
            {
                if (_campaign.TryUpgrade(agent, legs))
                {
                    SaveCampaign();
                    RefreshPanel();
                }
            };
            return button;
        }

        private void StartMission()
        {
            foreach (var s in _unitSprites)
            {
                s.QueueFree();
            }
            _unitSprites.Clear();
            _campaign.Prepare(_mission);
            SaveCampaign();
            _finished = false;
            _panel.Visible = false;
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
            if (!_finished && World.State != MissionState.Running)
            {
                FinishMission();
            }
            UpdateUnits();
            UpdateUi();
            _frame++;
            if (_shotOnFireTick >= 0 && World.Tick >= _shotOnFireTick && World.Shots.Count > 0 && World.Shots[^1].Tick == World.Tick)
            {
                // Erst im nächsten Bild ist der Schuss gezeichnet.
                _shotFrame = _frame + 1;
                _shotOnFireTick = -1;
            }
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
                // Nach einem Schuss hält die Figur die Waffe kurz aufs Ziel, auch wenn sie dabei weitergeht.
                int sinceShot = World.Tick - u.LastFiredTick;
                bool aiming = u.Alive && sinceShot <= (u.Moving ? AimTicksWalking : AimTicksStanding);
                int lookX = aiming ? u.AimX : u.FacingX, lookY = aiming ? u.AimY : u.FacingY;
                float sx = lookX - lookY, sy = (lookX + lookY) / 2f;
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
                int column = aiming ? AimColumn + dir : dir * 4 + step;
                s.RegionRect = new Rect2(column * _cellW, row * _cellH, _cellW, _cellH);
                s.FlipH = flip;
                if (u.Alive)
                {
                    s.RotationDegrees = 0;
                    s.Modulate = Colors.White;
                    s.Position = foot - new Vector2(12, 41);
                    if (aiming && sinceShot <= 1)
                    {
                        // Rückstoß: ein kurzer Ruck gegen die Schussrichtung
                        s.Position -= new Vector2(sx, sy).Normalized() * 1.5f;
                    }
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
                    if (u.Faction != Faction.Agent || !u.Alive)
                    {
                        continue;
                    }
                    if (IsInside(_things[i], u))
                    {
                        covers = true;
                        break;
                    }
                    if (!IsInFront(_things[i], u))
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

        /// <summary>True, wenn ein undurchsichtiges Objekt Kopf und Körper der Figur im Bild verdeckt.</summary>
        public bool IsHiddenByThing(Unit u)
        {
            var foot = ToScreen(u.X, u.Y);
            for (int i = 0; i < _things.Count; i++)
            {
                if (_thingSprites[i].Modulate.A >= 1f && IsInFront(_things[i], u)
                    && IsOpaqueAt(i, foot + new Vector2(0, -40)) && IsOpaqueAt(i, foot + new Vector2(0, -20)))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsOpaqueAt(int thing, Vector2 screen)
        {
            var image = _thingImages[thing];
            var p = screen - _thingSprites[thing].Position;
            return p.X >= 0 && p.Y >= 0 && p.X < image.GetWidth() && p.Y < image.GetHeight()
                && image.GetPixel((int)p.X, (int)p.Y).A > 0.5f;
        }

        private static bool IsInside(ThingInfo t, Unit u)
        {
            float x = u.X / (float)World.TileUnits, y = u.Y / (float)World.TileUnits;
            return x >= t.X0 && x <= t.X1 && y >= t.Y0 && y <= t.Y1;
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
                string skill = u.Skill == World.SkillLocksmith ? "  Schlosser" : u.Skill == World.SkillBookkeeper ? "  Buchhalter" : "";
                string body = u.Alive ? $"  Beine {u.Legs} Herz {u.Heart}{skill}{(u.Sprinting ? "  rennt" : "")}" : "";
                r.Append(mark).Append(n++).Append(' ').Append(u.Name).Append("  ").Append(state).Append(body).Append(item).Append('\n');
            }
            _roster.Text = r.ToString();
            int guards = World.Units.Count(u => u.Faction == Faction.Guard && u.Alive);
            _banner.Text = World.State switch
            {
                _ => _paused ? "PAUSE" : "",
            };
            _status.Text += $"   Wachen: {guards}   Zeit: {World.Tick / World.TickRate} s   Kasse: {World.Money}";
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
                        if (MoveSelected((int)Mathf.Floor(m.X), (int)Mathf.Floor(m.Y), mouse.DoubleClick))
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
                        // Wer nur den Buchhalter gewählt hat, will verhandeln statt schießen.
                        bool bribed = Selected.Count == 1 && World.OrderBribe(Selected.First(), guard.Id);
                        if (!bribed)
                        {
                            foreach (int id in Selected) World.OrderAttack(id, guard.Id);
                        }
                    }
                    else
                    {
                        int door = DoorUnderCursor(worldPos);
                        if (door >= 0)
                        {
                            foreach (int id in Selected) World.OrderOpenDoor(id, door);
                        }
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

        private int DoorUnderCursor(Vector2 p)
        {
            for (int d = 0; d < World.Doors.Count; d++)
            {
                var tiles = World.Doors[d].Tiles;
                if (World.Doors[d].Open || tiles.Count == 0)
                {
                    continue;
                }
                foreach (var (x, y) in new[] { tiles[0], tiles[^1] })
                {
                    if (TileCenter(x, y).DistanceTo(p) < 22f)
                    {
                        return d;
                    }
                }
            }
            return -1;
        }

        public Vector2 TileCenter(int tileX, int tileY) =>
            ToScreen(tileX * World.TileUnits + World.TileUnits / 2, tileY * World.TileUnits + World.TileUnits / 2);

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
        private bool MoveSelected(int tx, int ty, bool sprint = false)
        {
            return World.OrderGroupMove(Selected, tx, ty, sprint);
        }
    }
}
