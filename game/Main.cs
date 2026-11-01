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

        // Häuser und Boden tragen vorberechnetes Licht aus dem Pathtracer (tools/art/bake_light.py).
        // Die Nacht von CanvasModulate wird im Shader herausgerechnet, sie gilt nur noch für Rauch, Regen und Marken.
        private const string LitShaderCode = @"
shader_type canvas_item;
uniform sampler2D light_a : filter_nearest;
uniform sampler2D light_b : filter_nearest;
uniform sampler2D pane_ids : filter_nearest;
uniform sampler2D pane_states : filter_nearest;
uniform vec3 night;
uniform vec3 lamp_color;
uniform vec3 fire_color;
uniform float stutter = 1.0;
uniform float unrest = 1.0;
uniform float fire = 1.0;
uniform float lightning = 0.0;
void fragment() {
    vec3 a = texture(light_a, UV).rgb;
    vec4 b = texture(light_b, UV);
    vec3 l = 4.0 * a * a + 4.0 * b.r * b.r * (stutter - 1.0) * lamp_color
        + 4.0 * b.b * b.b * (unrest - 1.0) * lamp_color + 4.0 * b.g * b.g * (fire - 1.0) * fire_color
        + 4.0 * b.a * b.a * lightning * vec3(0.72, 0.82, 1.0);
    float shade = dot(COLOR.rgb, vec3(0.34));
    COLOR.rgb *= max(l, vec3(0.0)) / night;
    float pane = texture(pane_ids, UV).r;
    if (pane > 0.0) {
        vec4 state = texture(pane_states, vec2((pane * 255.0 + 0.5) / 256.0, 0.5));
        COLOR.rgb = mix(COLOR.rgb, state.rgb * (0.35 + shade * 1.3) / night, state.a);
    }
}";
        private const float LightMax = 4f;
        private static readonly Color LampColor = new(1f, 0.74f, 0.42f);
        private static readonly Color FireColor = new(1f, 0.45f, 0.15f);

        // Bewegte Figuren nehmen ihr Licht aus dem Lichtgitter. Damit sie lesbar bleiben, gibt es eine Mindesthelligkeit,
        // und das Licht färbt den braunen Mantel nur gedämpft ein.
        private const float AgentMinLight = 0.6f;
        private const float SpottedGuardMinLight = 0.5f;
        private const float HiddenGuardMinLight = 0.26f;
        private const float FallenMinLight = 0.42f;
        private const float BookMinLight = 0.7f;
        private const float FigureMaxLight = 1.2f;
        private const float FigureLightColor = 0.65f;
        private const float GlowFadePerSecond = 5f;

        // Im Lageplan: So lange bleibt eine Figur am Ziel stehen, das muss noch unbemerkt gehen.
        private const int ArrivalHoldTicks = 40;
        private const int MaxPlanWaitTicks = 60 * World.TickRate;
        private const float AbandonWarningSeconds = 4f;

        // Lebendige Kamera
        private const float CameraFollow = 7f;
        private const float DriftPixels = 1.2f;
        private const float ShakePixels = 7f;
        private const float TraumaFadePerSecond = 1.4f;
        private const float FocusSeconds = 2.2f;
        private const float FocusPull = 0.3f;
        private const float FocusZoom = 0.2f;
        private const int AimTicksStanding = 12;
        private const int AimTicksWalking = 5;

        // Spalten im Figurenblatt, siehe tools/art/figures3d.py
        private const int WalkColumn = 0;
        private const int RunColumn = 8;
        private const int IdleColumn = 16;
        private const int AimColumn = 20;
        private const int RecoilColumn = 21;
        private const int AimWalkColumn = 22;
        private const int FallColumn = 30;
        private const int FallFrames = 8;
        private const int FallVariants = 2;
        private const int CycleFrames = 8;
        private const int IdleFrames = 4;
        private const float IdleFramesPerSecond = 1.6f;
        private const float FallSeconds = 1f;

        // Weg für einen Doppelschritt in Einheiten, damit die Füße zum Tempo passen und nicht rutschen.
        private const float StrideUnits = 1150f;
        private const float RunStrideUnits = 1700f;

        /// <summary>Farbe für Ring und Nummer. Alle Agenten tragen dasselbe, nur die Nummer unterscheidet sie.</summary>
        public static readonly Color AgentColor = new(1f, 0.8f, 0.45f);

        private sealed record ThingInfo(float X0, float Y0, float X1, float Y1, float Z1);

        private readonly List<ThingInfo> _things = new();
        private readonly List<Sprite2D> _thingSprites = new();
        private readonly List<Image> _thingImages = new();
        private readonly List<ShaderMaterial> _litMaterials = new();
        private readonly WindowLife _windowLife = new();
        private readonly SoundScape _sound = new();
        private float _lightning;
        private Texture2D _noPanes = null!;
        private Texture2D _critterGlow = null!;

        // Der Arbeiter raucht vor der Seitentür des Hotels.
        private static readonly Vector2 WorkerTile = new(6.35f, 15.35f);
        private static readonly Vector2 StutterLamp = new(5.6f, 10.5f);
        private static readonly Vector2 WorkerDoorTile = new(6.04f, 15.8f);
        private Shader _litShader = null!;
        private Image _lightGrid = null!;
        private readonly List<Color> _unitGlow = new();
        private readonly Ambience _ambience = new();
        private readonly List<Sprite2D> _unitSprites = new();
        private readonly Dictionary<string, int> _figureRows = new();
        private readonly Dictionary<int, float> _fallStarted = new();
        private readonly List<Vector2> _tickFrom = new();
        private int _figureViews, _footX, _footY;
        private float _figureRes = 1f;
        private Sprite2D _book = null!;
        private Camera2D _camera = null!;
        private PaperUi _paper = null!;
        private Chatter _chatter = null!;
        private Label _banner = null!;
        private bool _introRunning;
        private bool _showIntro = true;
        private LightData _lightData = null!;
        private float _trauma;
        private float _focusLeft;
        private Vector2 _focusAt;
        private float _baseZoom = 2f;
        private int _testPanelFrame = -1;
        private PanelContainer _panel = null!;
        private VBoxContainer _panelBody = null!;
        private Campaign _campaign = new();
        private bool _finished;
        private bool _abandoned;
        private float _abandonWarning;
        private Label _planLegend = null!;
        private ColorRect _planBand = null!;
        private int _queuedX, _queuedY, _queuedTick;
        private int _lastReward;
        private MissionData _mission = null!;
        private MapData _map = null!;
        private Texture2D _figures = null!;
        private int _ox, _oy, _cellW, _cellH;
        private float _accumulator;
        private bool _paused;

        // Automatischer Prüflauf: --shot=<datei> --frames=<n> --auto
        // Mit --shot-on-fire=<n> entsteht das Bild im Moment des ersten Schusses ab Takt n.
        // --plan zeigt den Lageplan, --look=<x>,<y> hält die Kamera auf einem Feld.
        // --flash=<stärke> hält einen Blitz an, --ship=<x>,<y> stellt das Luftschiff über dieses Feld.
        private float _testFlash = -1f;
        private Vector2? _testShip;
        private bool _forcePlan;
        private bool _forceIntro;
        private Vector2? _look;
        private int _shotOnFireTick = -1;
        private string? _shotPath;
        private int _shotFrame = -1;
        private bool _auto;
        private int _frame;

        public World World { get; private set; } = null!;
        public HashSet<int> Selected { get; } = new();
        public Vector2 MarkerPos { get; private set; }
        public float MarkerTime { get; private set; }
        public bool Finished => _finished;
        public SoundScape Sound => _sound;
        public List<double[]> LampSpots => _mission.Lamps;
        public Color NightColor => Night;

        /// <summary>Wann die Wachen welches Feld im Blick haben. Grundlage für Lageplan und Wegvorschau.</summary>
        public WatchMap Watch { get; private set; } = null!;

        /// <summary>Q gehalten: Lageplan und Wegvorschau anzeigen.</summary>
        public bool PlanHeld => _forcePlan || Input.IsPhysicalKeyPressed(Key.Q);
        public List<(int X, int Y)> PreviewPath { get; } = new();
        public int PreviewRisk { get; private set; } = -1;
        public string PreviewText { get; private set; } = "";

        /// <summary>Agenten, die auf den freien Weg warten, und die Sekunden bis dahin.</summary>
        public HashSet<int> WaitingIds { get; } = new();
        public int WaitSeconds => Mathf.CeilToInt((_queuedTick - World.Tick) / (float)World.TickRate);

        public override void _Ready()
        {
            ParseArgs();
            // Gleiche Farbe wie der Himmel in ground.png, damit außerhalb der Karte kein Streifen sichtbar ist.
            RenderingServer.SetDefaultClearColor(new Color(52 / 255f, 56 / 255f, 78 / 255f) * Night);
            AddChild(new CanvasModulate { Color = Night });
            string root = ProjectSettings.GlobalizePath("res://");
            string data = Path.GetFullPath(Path.Combine(root, "..", "data"));
            _map = Loader.ParseMap(File.ReadAllText(Path.Combine(data, "map_01.json")));
            _lightData = Loader.ParseLight(File.ReadAllText(Path.Combine(data, "light_01.json")));
            _mission = LoadMission();
            Watch = WatchMap.Measure(_map, _mission);

            LoadCampaign();
            LoadStatics();
            _camera = new Camera2D { Zoom = new Vector2(2, 2) };
            AddChild(_camera);
            _camera.MakeCurrent();
            AddEnvironment();
            // Eigene Ebene, die der Kamera folgt: Ringe und Balken bleiben auch in der Nacht gut lesbar.
            var overlayLayer = new CanvasLayer { Layer = 1, FollowViewportEnabled = true };
            AddChild(overlayLayer);
            overlayLayer.AddChild(new Overlay { Game = this });
            _chatter = new Chatter { Game = this };
            overlayLayer.AddChild(_chatter);
            BuildUi();
            StartMission();
            _sound.Game = this;
            AddChild(_sound);
            AddAmbientSounds();
            AddWeatherAndLife();
            AddChild(new Traces { Game = this, ZIndex = 0 });
            AddChild(new Critters
            {
                Game = this, Sound = _sound, Sheet = LoadTexture("res://assets/critters.png"), Glow = _critterGlow,
                Gullies = _ambience.Steam.ConvertAll(ScreenToMap),
                WorkerSpot = WorkerTile, WorkerDoor = WorkerDoorTile,
            });
            MaybeStartIntro();
        }

        private MissionData LoadMission()
        {
            string path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data", "mission_01.json"));
            var mission = Loader.ParseMission(File.ReadAllText(path));
            mission.Light = _lightData;
            return mission;
        }

        /// <summary>Kurzes Wort für die Akte: Steht der Agent im Dunkeln oder im Licht?</summary>
        public string LightWord(Unit u)
        {
            int level = World.LightLevelAt(u.X, u.Y);
            return level <= World.DarkLevel ? "im Dunkeln" : level >= World.BrightLevel ? "im Licht" : "";
        }

        /// <summary>Der Einstieg läuft einmal beim Programmstart. Prüfläufe überspringen ihn, außer mit --intro.</summary>
        private void MaybeStartIntro()
        {
            bool testing = _shotPath != null || _auto || _look != null;
            if (!_showIntro || (testing && !_forceIntro))
            {
                _camera.PositionSmoothingEnabled = !testing;
                _camera.PositionSmoothingSpeed = CameraFollow;
                return;
            }
            _showIntro = false;
            _introRunning = true;
            _paper.Visible = false;
            var target = _camera.Position;
            AddChild(new Intro
            {
                Game = this, Briefing = _mission.Briefing, MissionTitle = _mission.Title,
                Short = File.Exists(IntroSeenPath) && !_forceIntro,
                CameraFrom = new Vector2(_ox, _oy + _map.Height * 12f), CameraTo = target,
            });
        }

        /// <summary>Kamerafahrt des Einstiegs.</summary>
        public void SetIntroCamera(Vector2 position, float zoom)
        {
            _camera.Position = position;
            _camera.Zoom = new Vector2(zoom, zoom);
        }

        private static string IntroSeenPath => ProjectSettings.GlobalizePath("user://einstieg_gesehen");

        public void EndIntro()
        {
            _introRunning = false;
            if (_shotPath == null)
            {
                File.WriteAllText(IntroSeenPath, "");
            }
            _paper.Visible = true;
            _camera.Zoom = new Vector2(_baseZoom, _baseZoom);
            _camera.PositionSmoothingEnabled = true;
            _camera.PositionSmoothingSpeed = CameraFollow;
        }

        /// <summary>Lässt das Bild kurz zittern, etwa bei Donner oder nahen Schüssen. Stärke von 0 bis 1.</summary>
        public void ShakeCamera(float amount) => _trauma = Mathf.Min(1f, Mathf.Max(_trauma, amount));

        /// <summary>Lebendige Kamera: leichtes Atmen, Zittern und ein kurzes Heranrücken, wenn ein Agent fällt.</summary>
        private void UpdateCameraLife(float dt)
        {
            if (_introRunning || _look != null || _shotPath != null)
            {
                return;
            }
            float now = Time.GetTicksMsec() / 1000f;
            _trauma = Mathf.Max(0f, _trauma - TraumaFadePerSecond * dt);
            float shake = _trauma * _trauma * ShakePixels;
            var offset = new Vector2(Mathf.Sin(now * 0.31f), Mathf.Sin(now * 0.23f + 1f)) * DriftPixels
                + new Vector2(Mathf.Sin(now * 47f), Mathf.Sin(now * 53f + 2f)) * shake;
            float zoom = _baseZoom;
            if (_focusLeft > 0f)
            {
                _focusLeft -= dt;
                float strength = Mathf.Sin(Mathf.Clamp(_focusLeft / FocusSeconds, 0f, 1f) * Mathf.Pi);
                offset += (_focusAt - _camera.Position) * FocusPull * strength;
                zoom += FocusZoom * strength;
            }
            _camera.Offset = offset;
            _camera.Zoom = new Vector2(zoom, zoom);
        }

        /// <summary>Helligkeit am Standort des führenden Agenten, für die Musik.</summary>
        public float LeadLight()
        {
            var lead = Lead();
            if (lead == null)
            {
                return 1f;
            }
            var light = LightAt(lead.X, lead.Y);
            return (light.R + light.G + light.B) / 3f;
        }

        public void AddWorkerVoice(Func<Vector2?> where) => _chatter.Workers.Add(where);

        /// <summary>Wetter, Luftschiff und Streikposten. Sie brauchen die fertige Tiefe der Objekte.</summary>
        private void AddWeatherAndLife()
        {
            var weather = new Weather
            {
                Game = this, Night = Night, LampColor = LampColor, HeldFlash = _testFlash,
                GroundLight = LoadTexture("res://assets/light/ground_a.png"), FogNoise = LoadTexture("res://assets/light/fog.png"),
                GroundSize = LoadTexture("res://assets/ground.png").GetSize(), GroundOrigin = new Vector2(_ox, _oy),
            };
            AddChild(weather);
            weather.AddLampCones(_mission.Lamps, StutterLamp);
            AddChild(new Airship { Game = this, Glow = _critterGlow, Night = Night, ShipTexture = LoadTexture("res://assets/airship.png"), HeldAt = _testShip });
            AddChild(new Strikers
            {
                Game = this, Glow = _critterGlow, BarrelTexture = LoadTexture("res://assets/barrel.png"), BannerTexture = LoadTexture("res://assets/banner.png"),
            });
            foreach (var p in _ambience.Fires)
            {
                _ambience.Valves.Add(p - new Vector2(8, 46));
            }
        }

        /// <summary>Dauergeräusche an festen Orten: Sie sind nur zu hören, wenn die Kamera in der Nähe ist.</summary>
        private void AddAmbientSounds()
        {
            foreach (var lamp in _mission.Lamps)
            {
                _sound.AddLoop("lamp", ToScreen((int)(lamp[0] * World.TileUnits), (int)(lamp[1] * World.TileUnits)), -38f, 120f);
            }
            foreach (var p in _ambience.Steam)
            {
                _sound.AddLoop("steam", p, -27f);
            }
            foreach (var p in _ambience.Fires)
            {
                _sound.AddLoop("fire", p, -18f);
                _sound.AddLoop("machine", p, -26f, 500f);
            }
            var exit = World.Exit;
            _sound.AddLoop("diesel", ToScreen((exit.X0 + exit.X1) / 2, (exit.Y0 + exit.Y1) / 2), -20f, 380f);
            foreach (var (kind, center) in _windowLife.PaneSpots)
            {
                if (kind == "tv")
                {
                    _sound.AddLoop("television", center, -32f, 180f);
                }
            }
            if (_windowLife.SignCenter is { } sign)
            {
                _sound.AddLoop("neon", sign, -34f, 260f);
            }
            // Ein Radio spielt hinter einem Fenster des Hotels, ganz leise.
            foreach (var (kind, center) in _windowLife.PaneSpots)
            {
                if (kind == "window")
                {
                    _sound.AddLoop("radio", center, -24f, 230f);
                    break;
                }
            }
        }

        /// <summary>Lichtschein für Mündungsfeuer, die Marken am Boden und die bewegte Umgebung.</summary>
        private void AddEnvironment()
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
            // Unter den Gebäuden gezeichnet: Was hinter einem Haus liegt, bleibt auch im Bild verdeckt.
            AddChild(new GroundMarks { Game = this, ZIndex = 0 });
            _ambience.Game = this;
            _ambience.Glow = glow;
            float halfW = _map.Width * 32f;
            _ambience.Area = new Rect2(_ox - halfW, _oy, 2 * halfW, _map.Height * 32f);
            AddChild(_ambience);
            _windowLife.Glow = glow;
            _windowLife.LightParent = this;
            _windowLife.Sound = _sound;
            AddChild(_windowLife);
            _critterGlow = glow;
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
                else if (a.StartsWith("--flash="))
                {
                    _testFlash = float.Parse(a["--flash=".Length..], System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (a.StartsWith("--ship="))
                {
                    var ship = a["--ship=".Length..].Split(',');
                    _testShip = new Vector2(float.Parse(ship[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ship[1], System.Globalization.CultureInfo.InvariantCulture));
                }
                else if (a == "--intro")
                {
                    _forceIntro = true;
                }
                else if (a.StartsWith("--panel="))
                {
                    _testPanelFrame = int.Parse(a["--panel=".Length..]);
                }
                else if (a == "--plan")
                {
                    _forcePlan = true;
                }
                else if (a.StartsWith("--look="))
                {
                    var look = a["--look=".Length..].Split(',');
                    _look = new Vector2(float.Parse(look[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(look[1], System.Globalization.CultureInfo.InvariantCulture));
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

        private ShaderMaterial LitMaterial(string name)
        {
            var material = new ShaderMaterial { Shader = _litShader };
            material.SetShaderParameter("light_a", LoadTexture($"res://assets/light/{name}_a.png"));
            material.SetShaderParameter("light_b", LoadTexture($"res://assets/light/{name}_b.png"));
            string panes = ProjectSettings.GlobalizePath($"res://assets/light/{name}_w.png");
            material.SetShaderParameter("pane_ids", File.Exists(panes) ? LoadTexture($"res://assets/light/{name}_w.png") : _noPanes);
            material.SetShaderParameter("pane_states", _windowLife.States);
            material.SetShaderParameter("night", Night);
            material.SetShaderParameter("lamp_color", LampColor);
            material.SetShaderParameter("fire_color", FireColor);
            _litMaterials.Add(material);
            return material;
        }

        /// <summary>Helligkeit der Laternen gerade jetzt, für die Lichtkegel.</summary>
        public float UnrestLevel { get; private set; } = 1f;
        public float StutterLevel { get; private set; } = 1f;

        /// <summary>Blitzlicht: blendet den vorberechneten Blitzanteil ein und hellt kurz den Himmel auf.</summary>
        public void SetLightning(float strength)
        {
            if (strength == _lightning)
            {
                return;
            }
            _lightning = strength;
            foreach (var material in _litMaterials)
            {
                material.SetShaderParameter("lightning", strength);
            }
            var sky = new Color(52 / 255f, 56 / 255f, 78 / 255f) * Night;
            RenderingServer.SetDefaultClearColor(sky.Lerp(new Color(0.55f, 0.6f, 0.72f), Mathf.Min(1f, strength * 0.7f)));
        }

        /// <summary>Flackern von Laternen und Feuer. Die Anteile dafür stehen in den Lichtkarten.</summary>
        public void SetFlicker(float stutter, float unrest, float fire)
        {
            UnrestLevel = unrest;
            StutterLevel = stutter;
            foreach (var material in _litMaterials)
            {
                material.SetShaderParameter("stutter", stutter);
                material.SetShaderParameter("unrest", unrest);
                material.SetShaderParameter("fire", fire);
            }
        }

        private void LoadStatics()
        {
            _litShader = new Shader { Code = LitShaderCode };
            var blank = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            blank.Fill(Colors.Black);
            _noPanes = ImageTexture.CreateFromImage(blank);
            _lightGrid = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/light/grid.png"));
            AddChild(new Sprite2D { Texture = LoadTexture("res://assets/ground.png"), Centered = false, ZIndex = -1, LightMask = 3, Material = LitMaterial("ground") });
            using var manifest = JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/manifest.json")));
            _ox = manifest.RootElement.GetProperty("ox").GetInt32();
            _oy = manifest.RootElement.GetProperty("oy").GetInt32();
            int index = 0;
            foreach (var t in manifest.RootElement.GetProperty("things").EnumerateArray())
            {
                _things.Add(new ThingInfo(t.GetProperty("x0").GetSingle(), t.GetProperty("y0").GetSingle(),
                    t.GetProperty("x1").GetSingle(), t.GetProperty("y1").GetSingle(), t.GetProperty("z1").GetSingle()));
                var image = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/things/" + t.GetProperty("file").GetString()));
                _thingImages.Add(image);
                var sprite = new Sprite2D
                {
                    Texture = ImageTexture.CreateFromImage(image),
                    Centered = false,
                    Position = new Vector2(t.GetProperty("px").GetInt32(), t.GetProperty("py").GetInt32()),
                    ZIndex = ThingZ(index),
                    LightMask = 3,
                    Material = LitMaterial(Path.GetFileNameWithoutExtension(t.GetProperty("file").GetString()!)),
                };
                AddChild(sprite);
                _thingSprites.Add(sprite);
                index++;
            }
            _ambience.Smoke = ReadPoints(manifest.RootElement, "smoke");
            _ambience.Steam = ReadPoints(manifest.RootElement, "steam");
            using (var panes = JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/panes.json"))))
            {
                foreach (var p in panes.RootElement.EnumerateArray())
                {
                    _windowLife.AddPane(p.GetProperty("id").GetInt32(), p.GetProperty("kind").GetString()!,
                        new Vector2(p.GetProperty("x").GetSingle(), p.GetProperty("y").GetSingle()));
                }
            }
            _ambience.Fires = ReadPoints(manifest.RootElement, "fires");
            foreach (var p in manifest.RootElement.GetProperty("puddles").EnumerateArray())
            {
                _ambience.Puddles.Add((new Vector2(p[0].GetInt32(), p[1].GetInt32()), p[2].GetInt32()));
            }
            using var figures = JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/figures.json")));
            _cellW = figures.RootElement.GetProperty("w").GetInt32();
            _cellH = figures.RootElement.GetProperty("h").GetInt32();
            _figureViews = figures.RootElement.GetProperty("views").GetInt32();
            _footX = figures.RootElement.GetProperty("footX").GetInt32();
            _footY = figures.RootElement.GetProperty("footY").GetInt32();
            _figureRes = figures.RootElement.GetProperty("res").GetSingle();
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
            AddChild(new FilmLook { Layer = 2 });
            var layer = new CanvasLayer { Layer = 3 };
            AddChild(layer);
            _paper = new PaperUi { Game = this };
            layer.AddChild(_paper);
            _planBand = new ColorRect
            {
                AnchorRight = 1f, OffsetLeft = 10f, OffsetTop = 62f, OffsetRight = -18f, OffsetBottom = 108f, Visible = false,
                Color = PaperUi.PaperDark, MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            layer.AddChild(_planBand);
            _planLegend = PaperUi.Text(
                "LAGEPLAN  Grün: nie bewacht. Gelb bis Rot: immer öfter im Blick. Punkte: Wege der Wachen. Klick: losgehen, sobald frei.\n"
                + "Zum Buch führt der Weg über die Straße in die Gasse am Mietshaus, zum Wagen hinten um das Lager.",
                13, PaperUi.Ink, PaperUi.Typewriter);
            _planLegend.Position = new Vector2(20, 65);
            _planLegend.Visible = false;
            layer.AddChild(_planLegend);
            var help = PaperUi.Text(
                "Links: wählen/gehen   Doppelklick: rennen   Rechts: angreifen, Emil: bestechen, Otto: Tür   Q halten: Lageplan   1-4 Leertaste   WASD F P M   F11: Vollbild   R: abbrechen",
                12, new Color(0.75f, 0.72f, 0.65f, 0.75f), PaperUi.Typewriter);
            help.AnchorTop = 1f;
            help.AnchorBottom = 1f;
            help.OffsetLeft = 12f;
            help.OffsetTop = -23f;
            layer.AddChild(help);
            _banner = PaperUi.Text("", 34, new Color(1f, 0.85f, 0.4f), PaperUi.Serif);
            _banner.AnchorRight = 1f;
            _banner.AnchorTop = 0.45f;
            _banner.AnchorBottom = 0.45f;
            _banner.HorizontalAlignment = HorizontalAlignment.Center;
            _banner.AddThemeColorOverride("font_shadow_color", Colors.Black);
            layer.AddChild(_banner);
            _panel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(680, 0), Visible = false,
                AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
                OffsetLeft = -340f, OffsetRight = 340f,
                GrowHorizontal = Control.GrowDirection.Both, GrowVertical = Control.GrowDirection.Both,
            };
            _panel.AddThemeStyleboxOverride("panel", PaperUi.PaperBox(PaperUi.PaperColor));
            _panelBody = new VBoxContainer();
            _panelBody.AddThemeConstantOverride("separation", 8);
            _panel.AddChild(_panelBody);
            layer.AddChild(_panel);
        }

        private static string SavePath => ProjectSettings.GlobalizePath("user://kampagne.json");

        private void LoadCampaign()
        {
            _campaign = File.Exists(SavePath) ? Campaign.FromJson(File.ReadAllText(SavePath)) : new Campaign();
        }

        private void SaveCampaign()
        {
            // Prüfläufe dürfen den echten Spielstand nicht verändern.
            if (_shotPath == null)
            {
                File.WriteAllText(SavePath, _campaign.ToJson());
            }
        }

        public override void _Notification(int what)
        {
            // Fenster schließen ist derselbe Abbruch wie R, sonst wäre es der Ausweg vor dem Tod eines Agenten.
            if (what == NotificationWMCloseRequest && World != null && !_finished && Campaign.AbandonCostsSquad(World))
            {
                _campaign.Abandon(World);
                SaveCampaign();
            }
        }

        /// <summary>R: Vor dem ersten Schuss ist der Rückzug frei, danach kostet er nach einer Warnung den Trupp.</summary>
        private void PressRestart()
        {
            if (_finished)
            {
                StartMission();
            }
            else if (!Campaign.AbandonCostsSquad(World))
            {
                _campaign.Abandon(World);
                SaveCampaign();
                StartMission();
            }
            else if (_abandonWarning <= 0f)
            {
                _abandonWarning = AbandonWarningSeconds;
            }
            else
            {
                _campaign.Abandon(World);
                SaveCampaign();
                _finished = true;
                _abandoned = true;
                _abandonWarning = 0f;
                _lastReward = 0;
                RefreshPanel();
                _panel.Visible = true;
            }
        }

        private void FinishMission()
        {
            _finished = true;
            _lastReward = World.State == MissionState.Won ? Campaign.Reward(World.Rating) : 0;
            _campaign.Finish(World);
            SaveCampaign();
            RefreshPanel();
            _panel.Visible = true;
        }

        private static Label PanelLabel(string text, int size = 15) => PaperUi.Text(text, size, PaperUi.Ink, PaperUi.Typewriter);

        /// <summary>Baut den Bildschirm zwischen den Einsätzen neu auf: Ergebnis, Kasse und Ausbau.</summary>
        private void RefreshPanel()
        {
            foreach (var child in _panelBody.GetChildren())
            {
                child.QueueFree();
            }
            bool won = World.State == MissionState.Won;
            var (headline, line) = Headlines.For(World, _abandoned);
            _panelBody.AddChild(PaperUi.Text(Headlines.Paper.ToUpperInvariant(), 14, PaperUi.StampRed, PaperUi.Serif));
            _panelBody.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = PaperUi.Ink, MouseFilter = Control.MouseFilterEnum.Ignore });
            var head = PaperUi.Text(headline, 28, PaperUi.Ink, PaperUi.Serif);
            head.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            head.CustomMinimumSize = new Vector2(640, 0);
            _panelBody.AddChild(head);
            var article = PaperUi.Text(line, 14, PaperUi.Ink, PaperUi.Typewriter);
            article.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            article.CustomMinimumSize = new Vector2(640, 0);
            _panelBody.AddChild(article);
            _panelBody.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = PaperUi.PaperDark, MouseFilter = Control.MouseFilterEnum.Ignore });
            string result = won ? $"Einsatz geglückt: {RatingText()}" : _abandoned ? "Einsatz aufgegeben" : "Trupp verloren";
            _panelBody.AddChild(PaperUi.Text(result, 16, PaperUi.StampRed, PaperUi.Serif));
            if (won)
            {
                string loot = World.LootTaken ? $", dazu {World.LootName} {World.LootValue}" : "";
                _panelBody.AddChild(PanelLabel($"Lohn: {_lastReward}{loot}"));
            }
            if (World.Units.Any(u => u.Faction == Faction.Agent && (!u.Alive || _abandoned)))
            {
                _panelBody.AddChild(PanelLabel("Gefallene kommen zurück, aber Beine und Herz fallen auf Stufe 1."));
            }
            _panelBody.AddChild(PanelLabel($"Kasse: {_campaign.Money}    Einsätze: {_campaign.Missions}", 16));
            foreach (var u in World.Units.Where(u => u.Faction == Faction.Agent))
            {
                var record = _campaign.Agent(u.Name);
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                var name = PanelLabel($"{u.Name}: Beine {record.Legs}, Herz {record.Heart}{(u.Alive && !_abandoned ? "" : "  (gefallen)")}");
                name.CustomMinimumSize = new Vector2(280, 0);
                row.AddChild(name);
                row.AddChild(UpgradeButton("Beine", record.Name, record.Legs, true));
                row.AddChild(UpgradeButton("Herz", record.Name, record.Heart, false));
                _panelBody.AddChild(row);
            }
            var next = new Button { Text = won ? "Nächster Einsatz" : "Nochmal" };
            PaperUi.InkButton(next);
            next.Pressed += StartMission;
            _panelBody.AddChild(next);
            var reset = new Button { Text = "Neue Kampagne" };
            PaperUi.InkButton(reset);
            reset.Pressed += () =>
            {
                _campaign = new Campaign();
                SaveCampaign();
                _mission = LoadMission();
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
            PaperUi.InkButton(button);
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
            _unitGlow.Clear();
            _fallStarted.Clear();
            _tickFrom.Clear();
            _campaign.Prepare(_mission);
            SaveCampaign();
            _finished = false;
            _abandoned = false;
            _abandonWarning = 0f;
            WaitingIds.Clear();
            _panel.Visible = false;
            World = new World(_map, _mission);
            foreach (var u in World.Units)
            {
                var sprite = new Sprite2D { Texture = _figures, RegionEnabled = true, Centered = false, Scale = FigureScale };
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

        public Vector2 ScreenToMap(Vector2 p)
        {
            float a = (p.X - _ox) / 32f, b = (p.Y - _oy) / 16f;
            return new Vector2((a + b) / 2f, (b - a) / 2f);
        }

        public override void _Process(double delta)
        {
            if (!_introRunning)
            {
                HandleCamera((float)delta);
            }
            UpdateCameraLife((float)delta);
            if (_testPanelFrame >= 0 && _frame == _testPanelFrame)
            {
                _abandoned = true;
                _finished = true;
                RefreshPanel();
                _panel.Visible = true;
            }
            MarkerTime = Mathf.Max(0f, MarkerTime - (float)delta);
            _abandonWarning = Mathf.Max(0f, _abandonWarning - (float)delta);
            Selected.RemoveWhere(id => !World.Units[id].Alive);
            if (!_paused && !_finished && !_introRunning)
            {
                _accumulator += (float)delta;
                while (_accumulator >= TickSeconds)
                {
                    _accumulator -= TickSeconds;
                    RememberPositions();
                    World.Step();
                    StartQueuedMove();
                }
            }
            UpdatePlan();
            if (_look is { } tile)
            {
                _camera.Position = ToScreen((int)(tile.X * World.TileUnits), (int)(tile.Y * World.TileUnits));
            }
            else if (_auto)
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
                GD.Print($"Geräusche abgespielt: {_sound.PlayCount}  " + string.Join(", ", _sound.PlayedByGroup.Select(g => $"{g.Key} {g.Value}")));
                GetViewport().GetTexture().GetImage().SavePng(_shotPath);
                GetTree().Quit();
            }
        }

        /// <summary>Hält im Lageplan den Weg zum Mauszeiger aktuell.</summary>
        private void UpdatePlan()
        {
            bool held = PlanHeld && !_finished;
            _planLegend.Visible = held;
            _planBand.Visible = held;
            PreviewPath.Clear();
            PreviewRisk = -1;
            PreviewText = "";
            var lead = Lead();
            if (!held || lead == null)
            {
                return;
            }
            if (World.Rating != MissionRating.Unseen)
            {
                PreviewText = "Wachen aufgescheucht: keine Vorhersage mehr";
                return;
            }
            var m = ScreenToMap(GetGlobalMousePosition());
            int tx = Mathf.FloorToInt(m.X), ty = Mathf.FloorToInt(m.Y);
            var path = World.Map.IsBlocked(tx, ty) ? null : World.Map.FindPath(lead.TileX, lead.TileY, tx, ty);
            if (path == null || path.Count == 0)
            {
                return;
            }
            PreviewPath.AddRange(path);
            int wait = PlanWait(lead, path, tx, ty);
            PreviewRisk = Watch.FirstRisk(path, lead.X, lead.Y, World.WalkSpeed(lead), World.Tick, PlanHold(lead, tx, ty));
            PreviewText = wait == 0 ? "Weg frei"
                : wait > 0 ? $"frei in {Mathf.CeilToInt(wait / (float)World.TickRate)} s"
                : "kein freies Fenster";
        }

        private Unit? Lead() => World.Units.Where(u => Selected.Contains(u.Id)).OrderByDescending(u => u.CarriesItem).ThenBy(u => u.Id).FirstOrDefault();

        private int PlanWait(Unit lead, List<(int X, int Y)> path, int tx, int ty) =>
            Watch.TicksUntilClear(path, lead.X, lead.Y, World.WalkSpeed(lead), World.Tick, PlanHold(lead, tx, ty), MaxPlanWaitTicks);

        /// <summary>Wie lange die Figur am Ziel steht: am Buch und im Fluchtwagen länger als sonst.</summary>
        private int PlanHold(Unit lead, int tx, int ty)
        {
            if (!World.ItemTaken && (tx, ty) == (World.ItemX / World.TileUnits, World.ItemY / World.TileUnits))
            {
                return World.PickupTicks;
            }
            int cx = tx * World.TileUnits, cy = ty * World.TileUnits;
            bool boarding = lead.CarriesItem && cx >= World.Exit.X0 && cx < World.Exit.X1 && cy >= World.Exit.Y0 && cy < World.Exit.Y1;
            return boarding ? World.EscapeTicks : ArrivalHoldTicks;
        }

        /// <summary>Klick im Lageplan: Die Auswahl geht los, sobald der Weg frei ist. Liefert false ohne freies Fenster.</summary>
        private bool QueueMove(int tx, int ty)
        {
            var lead = Lead();
            var path = lead == null || World.Map.IsBlocked(tx, ty) ? null : World.Map.FindPath(lead.TileX, lead.TileY, tx, ty);
            if (lead == null || path == null || World.Rating != MissionRating.Unseen)
            {
                return MoveSelected(tx, ty);
            }
            int wait = PlanWait(lead, path, tx, ty);
            if (wait < 0)
            {
                return false;
            }
            if (wait == 0)
            {
                return MoveSelected(tx, ty);
            }
            WaitingIds.Clear();
            WaitingIds.UnionWith(Selected);
            _queuedX = tx;
            _queuedY = ty;
            _queuedTick = World.Tick + wait;
            return true;
        }

        private void StartQueuedMove()
        {
            if (WaitingIds.Count == 0)
            {
                return;
            }
            // Bemerkt eine Wache etwas, stimmt der Plan nicht mehr. Dann entscheidet wieder der Spieler.
            if (World.Rating != MissionRating.Unseen)
            {
                WaitingIds.Clear();
            }
            else if (World.Tick >= _queuedTick)
            {
                World.OrderGroupMove(WaitingIds, _queuedX, _queuedY);
                WaitingIds.Clear();
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
                var added = new Sprite2D { Texture = _figures, RegionEnabled = true, Centered = false, Scale = FigureScale };
                AddChild(added);
                _unitSprites.Add(added);
            }
            var order = World.Units.OrderBy(u => u.X + u.Y).ToList();
            for (int rank = 0; rank < order.Count; rank++)
            {
                var u = order[rank];
                var s = _unitSprites[u.Id];
                var foot = UnitPos(u);
                // Nach einem Schuss hält die Figur die Waffe kurz aufs Ziel, auch wenn sie dabei weitergeht.
                int sinceShot = World.Tick - u.LastFiredTick;
                bool aiming = u.Alive && sinceShot <= (u.Moving ? AimTicksWalking : AimTicksStanding);
                int lookX = aiming ? u.AimX : u.FacingX, lookY = aiming ? u.AimY : u.FacingY;
                // Am Boden gedacht: x - y läuft im Bild quer, x + y zur Kamera. Der Westen entsteht durch Spiegeln.
                float across = lookX - lookY, toward = lookX + lookY;
                float angle = Mathf.RadToDeg(Mathf.Atan2(toward, Mathf.Abs(across)));
                int view = Mathf.Clamp(Mathf.RoundToInt((90f - angle) / 45f), 0, _figureViews - 1);
                s.RegionRect = FigureRegion(u.Sprite, view, FigureColumn(u, aiming, sinceShot));
                s.FlipH = across < 0;
                s.Modulate = FigureGlow(u);
                s.Position = foot - FigureFoot;
                int depth = ThingZ(InsertIndex(u.X / (float)World.TileUnits, u.Y / (float)World.TileUnits) - 1);
                s.ZIndex = u.Alive ? depth + rank + 2 : depth + 1;
            }
            UpdateOcclusion();
            _book.Visible = !World.ItemTaken;
            _book.Position = ToScreen(World.ItemX, World.ItemY);
            _book.ZIndex = 1;
            _book.Modulate = LightFor(LightAt(World.ItemX, World.ItemY), BookMinLight);
        }

        private Color GridLight(int x, int y)
        {
            var c = _lightGrid.GetPixel(Mathf.Clamp(x, 0, _lightGrid.GetWidth() - 1), Mathf.Clamp(y, 0, _lightGrid.GetHeight() - 1));
            // Im vierten Kanal steht der Blitzanteil: Wer im Schatten eines Hauses steht, bleibt auch beim Blitz dunkel.
            float flash = c.A * c.A * LightMax * _lightning;
            return new Color(c.R * c.R * LightMax + flash * 0.72f, c.G * c.G * LightMax + flash * 0.82f, c.B * c.B * LightMax + flash);
        }

        /// <summary>Licht aus dem vorberechneten Gitter an einer Stelle der Karte.</summary>
        public Color LightAt(int unitsX, int unitsY)
        {
            float cells = _lightGrid.GetWidth() / (float)_map.Width;
            float gx = unitsX / (float)World.TileUnits * cells - 0.5f, gy = unitsY / (float)World.TileUnits * cells - 0.5f;
            int x = Mathf.FloorToInt(gx), y = Mathf.FloorToInt(gy);
            float fx = gx - x, fy = gy - y;
            return GridLight(x, y).Lerp(GridLight(x + 1, y), fx).Lerp(GridLight(x, y + 1).Lerp(GridLight(x + 1, y + 1), fx), fy);
        }

        /// <summary>Einfärbung für Kleintiere und Passanten am gleichen Licht wie die Figuren.</summary>
        public Color FigureLight(int unitsX, int unitsY, float minLight) => LightFor(LightAt(unitsX, unitsY), minLight);

        /// <summary>Tiefe im Bild für eine kleine Figur an dieser Stelle, zwischen den Objekten davor und dahinter.</summary>
        public int DepthZ(float x, float y) => ThingZ(InsertIndex(x, y) - 1) + 20;

        /// <summary>Tiefe für etwas, das auf einem Objekt in dieser Höhe sitzt: direkt über diesem Objekt.</summary>
        public int PerchZ(float x, float y, float z)
        {
            for (int i = _things.Count - 1; i >= 0; i--)
            {
                var t = _things[i];
                if (x >= t.X0 && x <= t.X1 && y >= t.Y0 && y <= t.Y1 && Mathf.Abs(t.Z1 - z) < 1f)
                {
                    return ThingZ(i) + 20;
                }
            }
            return DepthZ(x, y);
        }

        /// <summary>Figurenblatt und Ausschnitt für eine Figur, Blickrichtung und Spalte.</summary>
        public (Texture2D Sheet, Rect2 Region) FigureFrame(string name, int view, int column) =>
            (_figures, FigureRegion(name, view, column));

        /// <summary>Lage der Füße im Figurenbild, in Weltpixeln.</summary>
        public Vector2 FigureFoot => new Vector2(_footX, _footY) / _figureRes;

        /// <summary>Das Blatt ist feiner aufgelöst als die Welt. So groß werden Figuren gezeichnet.</summary>
        public Vector2 FigureScale => Vector2.One / _figureRes;

        /// <summary>Gegner unter dem Mauszeiger, den ein Agent gerade sieht. Für die Anzeige der Art.</summary>
        public Unit? HoveredGuard()
        {
            var guard = UnitUnderCursor(GetGlobalMousePosition(), Faction.Guard);
            return guard != null && World.IsSpottedByAgents(guard) ? guard : null;
        }
        public const int IdleFirstColumn = IdleColumn;
        public const int WalkFirstColumn = WalkColumn;

        /// <summary>Einfärbung einer Figur: das Licht am Ort, nicht dunkler als minLight und nur gedämpft farbig.</summary>
        private static Color LightFor(Color light, float minLight)
        {
            float level = (light.R + light.G + light.B) / 3f;
            float target = Mathf.Clamp(level, minLight, FigureMaxLight);
            float scale = target / Mathf.Max(level, 0.01f);
            var grey = new Color(target, target, target);
            var tinted = grey.Lerp(new Color(light.R * scale, light.G * scale, light.B * scale), FigureLightColor);
            return new Color(tinted.R / Night.R, tinted.G / Night.G, tinted.B / Night.B);
        }

        /// <summary>Zählt die Schritte einer gehenden Figur, damit das Geräusch zum Aufsetzen der Füße passt.</summary>
        public int StepsTaken(Unit u)
        {
            float stride = u.Sprinting ? RunStrideUnits : StrideUnits;
            float speed = World.WalkSpeed(u) * (u.Sprinting ? 1.4f : 1f);
            return (int)(u.WalkTick * speed / stride * 2f);
        }

        public Vector2 CameraCenter => _camera.Position;

        /// <summary>Welches Bild der Bewegung gerade dran ist. Schrittlänge und Tempo passen zusammen, damit die Füße nicht rutschen.</summary>
        private int FigureColumn(Unit u, bool aiming, int sinceShot)
        {
            float now = Time.GetTicksMsec() / 1000f;
            if (!u.Alive)
            {
                if (!_fallStarted.TryGetValue(u.Id, out float started))
                {
                    started = now;
                    _fallStarted[u.Id] = now;
                    if (u.Faction == Faction.Agent)
                    {
                        _focusAt = ToScreen(u.X, u.Y);
                        _focusLeft = FocusSeconds;
                    }
                }
                // Nicht alle sterben gleich: Die Id bestimmt, ob die Figur nach vorn oder zur Seite sackt.
                return FallColumn + u.Id % FallVariants * FallFrames + Mathf.Min(FallFrames - 1, (int)((now - started) / FallSeconds * FallFrames));
            }
            if (u.Moving)
            {
                // Ein alarmierter Hund rennt, er kennt kein Gehen mehr.
                bool running = u.Sprinting || (u.Type.Bites && u.Alerted);
                float stride = running ? RunStrideUnits : StrideUnits;
                float speed = World.WalkSpeed(u) * (u.Sprinting ? 1.4f : 1f);
                int frame = (int)(u.WalkTick * speed / stride * CycleFrames) % CycleFrames;
                return (aiming ? AimWalkColumn : running ? RunColumn : WalkColumn) + frame;
            }
            if (aiming)
            {
                return sinceShot <= 1 ? RecoilColumn : AimColumn;
            }
            return IdleColumn + (int)(now * IdleFramesPerSecond + u.Id * 1.7f) % IdleFrames;
        }

        private Rect2 FigureRegion(string name, int view, int column) =>
            new(column * _cellW, (_figureRows.GetValueOrDefault(name, 0) * _figureViews + view) * _cellH, _cellW, _cellH);

        private void RememberPositions()
        {
            for (int i = 0; i < World.Units.Count; i++)
            {
                var pos = ToScreen(World.Units[i].X, World.Units[i].Y);
                if (i < _tickFrom.Count)
                {
                    _tickFrom[i] = pos;
                }
                else
                {
                    _tickFrom.Add(pos);
                }
            }
        }

        /// <summary>Stelle der Figur im Bild, geglättet zwischen zwei Spieltakten. Die Spiellogik bleibt davon unberührt.</summary>
        public Vector2 UnitPos(Unit u)
        {
            var now = ToScreen(u.X, u.Y);
            if (_paused || _finished || u.Id >= _tickFrom.Count)
            {
                return now;
            }
            return _tickFrom[u.Id].Lerp(now, Mathf.Clamp(_accumulator / TickSeconds, 0f, 1f));
        }

        /// <summary>Licht am Standort einer Figur. Wachen ohne Sichtkontakt bleiben im Dunkeln.</summary>
        private Color FigureGlow(Unit u)
        {
            while (_unitGlow.Count <= u.Id)
            {
                _unitGlow.Add(Colors.White);
            }
            float minLight = !u.Alive ? FallenMinLight : u.Faction == Faction.Agent ? AgentMinLight
                : World.IsSpottedByAgents(u) ? SpottedGuardMinLight : HiddenGuardMinLight;
            var target = LightFor(LightAt(u.X, u.Y), minLight);
            // Beim Blitz sofort, sonst käme das Licht auf der Figur zu spät.
            float weight = _lightning > 0.02f ? 1f : Mathf.Min(1f, GlowFadePerSecond * (float)GetProcessDeltaTime());
            var g = _unitGlow[u.Id].Lerp(target, weight);
            _unitGlow[u.Id] = g;
            return g;
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
        private int InsertIndex(float x, float y)
        {
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
            string name = World.ItemName.ToUpperInvariant();
            string telegram = World.ItemTaken
                ? (World.EscapeProgress > 0 ? "EINSTEIGEN STOP STEHEN BLEIBEN STOP" : $"{name} GESICHERT STOP ZUM FLUCHTWAGEN IM OSTEN STOP")
                : World.ItemProgress > 0 ? $"{name} WIRD GESICHERT STOP STEHEN BLEIBEN STOP"
                : $"{name} AM MIETSHAUS HOLEN STOP GASSE NUTZEN STOP DANN ZUM FLUCHTWAGEN IM OSTEN STOP";
            int guards = World.Units.Count(u => u.Faction == Faction.Guard && u.Alive);
            string info = $"WACHEN {guards}   ZEIT {World.Tick / World.TickRate} S   KASSE {World.Money}";
            if (World.TicksUntilReinforcement >= 0 && !_finished)
            {
                info += $"   ALARM: VERSTÄRKUNG IN {World.TicksUntilReinforcement / World.TickRate + 1} S";
            }
            _paper.Refresh(World, Selected, telegram, info);
            _banner.Text = _abandonWarning > 0f ? "R nochmal: aufgeben, Trupp verloren" : _paused ? "PAUSE" : "";
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
                    case Key.M: _sound.ToggleMute(); break;
                    case Key.F11: ToggleFullscreen(); break;
                    case Key.R: PressRestart(); break;
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
                        int tx = (int)Mathf.Floor(m.X), ty = (int)Mathf.Floor(m.Y);
                        WaitingIds.Clear();
                        if (PlanHeld ? QueueMove(tx, ty) : MoveSelected(tx, ty, mouse.DoubleClick))
                        {
                            MarkerPos = worldPos;
                            MarkerTime = MarkerSeconds;
                        }
                    }
                }
                else if (mouse.ButtonIndex == MouseButton.Right)
                {
                    WaitingIds.Clear();
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

        private static void ToggleFullscreen()
        {
            bool full = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
            DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
        }

        public void SelectAgent(int index, bool add)
        {
            if (!World.Units[index].Alive) return;
            if (!add) Selected.Clear();
            Selected.Add(index);
        }

        private int DoorUnderCursor(Vector2 p)
        {
            for (int d = 0; d < World.Doors.Count; d++)
            {
                if (!World.Doors[d].Open && DoorMark(World.Doors[d]).DistanceTo(p) < 22f)
                {
                    return d;
                }
            }
            return -1;
        }

        /// <summary>Wo das Schloss einer Tür im Bild sitzt: auf halber Höhe der gemalten Tür.</summary>
        public Vector2 DoorMark(Door door) => ToScreen(door.MarkX, door.MarkY) + new Vector2(0, -20);

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
