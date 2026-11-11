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

        // Figuren nehmen ihr Licht über Modulate. Gezeichnet wird scharf-bilinear wie die Stadt, siehe sharp_uv unten.
        private const string FigureShaderCode = @"
shader_type canvas_item;
// Das Figurenblatt speichert je Bildpunkt nur eine Nummer. Die Farbe steht in der Palette, eine Zeile je Figur.
// Überblendet wird wie bei sharp_uv, nur von Hand über die vier Nachbarn, denn Nummern lassen sich nicht mitteln.
uniform sampler2D palette : filter_nearest;
uniform float block = 540.0;
// Farbvariante: eine andere Zeile der Palette als die der Figur. Unter null gilt die Zeile der Figur.
instance uniform float palette_row = -1.0;
varying vec4 tint;
void vertex() {
    tint = COLOR;
}
vec4 tone(sampler2D sheet, ivec2 at, ivec2 size, float row) {
    at = clamp(at, ivec2(0), size - ivec2(1));
    int slot = int(texelFetch(sheet, at, 0).r * 255.0 + 0.5);
    vec4 c = texelFetch(palette, ivec2(slot, row < 0.0 ? int(float(at.y) / block) : int(row + 0.5)), 0);
    return vec4(c.rgb * c.a, c.a);
}
void fragment() {
    ivec2 size = textureSize(TEXTURE, 0);
    vec2 p = UV * vec2(size);
    vec2 edge = floor(p + 0.5);
    vec2 reach = max(fwidth(p), vec2(1e-4));
    vec2 f = clamp((p - edge) / reach, -0.5, 0.5) + 0.5;
    ivec2 b = ivec2(edge);
    vec4 c = mix(mix(tone(TEXTURE, b + ivec2(-1, -1), size, palette_row), tone(TEXTURE, b + ivec2(0, -1), size, palette_row), f.x),
                 mix(tone(TEXTURE, b + ivec2(-1, 0), size, palette_row), tone(TEXTURE, b, size, palette_row), f.x), f.y);
    COLOR = vec4(c.rgb / max(c.a, 1e-4), c.a) * tint;
}
";

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
uniform vec2 cuts[6];
uniform int cut_count = 0;
uniform float cut_open = 0.0;
varying vec2 world;
varying vec4 tint;
void vertex() {
    world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
    tint = COLOR;
}
// Scharf-bilinear: innerhalb eines Bildpunkts der Grafik exakt wie nearest, nur an seinem Rand wird über einen
// Bildschirmpunkt weich überblendet. So bleibt Pixelgrafik bei jedem Vergrößerungsfaktor gleichmäßig und ohne Flimmern.
vec2 sharp_uv(vec2 uv, vec2 texel) {
    vec2 p = uv / texel;
    vec2 edge = floor(p + 0.5);
    vec2 reach = max(fwidth(p), vec2(1e-4));
    return (edge + clamp((p - edge) / reach, -0.5, 0.5)) * texel;
}
void fragment() {
    COLOR = texture(TEXTURE, sharp_uv(UV, TEXTURE_PIXEL_SIZE)) * tint;
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
    // Durchblick auf verdeckte Agenten: innen so weit frei wie früher, zum Rand hin läuft das Objekt weich aus.
    // Gerechnet in Weltkoordinaten ohne Raster, damit der Rand bei jeder Auflösung so fein ist wie die Stadt.
    if (cut_open > 0.001) {
        float keep = 1.0;
        for (int i = 0; i < cut_count; i++) {
            float reach = length((world - cuts[i]) / vec2(21.0, 33.0));
            keep = min(keep, smoothstep(cut_open * 0.72, cut_open, reach));
        }
        COLOR.a *= keep;
    }
}";
        private const float LightMax = 4f;
        private const int MaxCuts = 6;
        private const float CutOpenPerSecond = 6f;
        private const float CutRise = 21f;
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
        private static readonly Vector2I MinWindow = new(1024, 640);

        // Lebendige Kamera
        private const float CameraFollow = 7f;
        private const float ResultFocusDelay = 0.8f;
        private const float DriftPixels = 1.2f;
        private const float ShakePixels = 7f;
        private const float TraumaFadePerSecond = 1.4f;
        private const float FocusSeconds = 2.2f;
        private const float FocusPull = 0.3f;
        private const float FocusZoom = 0.2f;

        /// <summary>Farbe für Ring und Nummer. Alle Agenten tragen dasselbe, nur die Nummer unterscheidet sie.</summary>
        public static readonly Color AgentColor = new(1f, 0.8f, 0.45f);

        private sealed record ThingInfo(float X0, float Y0, float X1, float Y1, float Z1);

        private readonly List<ThingInfo> _things = new();
        private readonly List<Sprite2D> _thingSprites = new();
        private readonly List<float> _thingCuts = new();
        private readonly Vector2[] _cutPoints = new Vector2[MaxCuts];
        private readonly List<Image> _thingImages = new();
        private readonly List<float> _thingImageScale = new();
        private readonly Dictionary<string, Image> _hdImages = new();
        private Vector2 _groundSize;
        private float _hdRes = 1f;
        private ulong _loadStaticsMs;
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
        private ShaderMaterial _figureMaterial = null!;
        private Image _lightGrid = null!;
        private readonly List<Color> _unitGlow = new();
        private readonly Ambience _ambience = new();
        private PuddleMirror _puddleMirror = null!;
        private readonly List<Sprite2D> _unitSprites = new();
        private readonly Dictionary<string, int> _figureRows = new();
        private readonly FigureAnimator _animator = new();
        private readonly List<Vector2> _tickFrom = new();
        private int _figureViews, _footX, _footY;
        private float _figureRes = 1f;
        private Sprite2D _book = null!;
        private Camera2D _camera = null!;
        private Hud _paper = null!;
        private Chatter _chatter = null!;
        private Label _banner = null!;
        // Eine Zwischensequenz läuft, das Spiel steht still.
        private bool _introRunning;
        private bool _showIntro = true;
        private SequencePlayer? _sequence;
        private Vector2 _cameraStart;
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
        private Label _planLegend = null!;
        private Label _help = null!;
        private Menus _menus = null!;
        private CanvasLayer _overlayLayer = null!;
        private Settings _settings = new();
        private bool _hadSave;
        private readonly SaveFolder _saves = new(ProjectSettings.GlobalizePath("user://"));
        private int _slot = 1;
        private Button? _panelFocus;
        private Armory _armory = null!;
        private CityMap _cityMap = null!;
        // Vorbereitung vor dem Einsatz: Stadtplan, dann Waffenkammer. Danach geht es mit _afterBriefing weiter.
        private Action? _afterBriefing;
        private bool _briefingFromResult;
        // Ein neues Spiel aus dem Menü: Nach dem Prolog kommen erst Stadtplan und Waffenkammer.
        private bool _briefFirst;
        private float _hudShift;
        private float _peilSeconds = Hud.PeilSeconds;
        private const float HudShiftPerSecond = 240f;
        // Agent, dessen Rauchtopf auf den nächsten Linksklick in die Welt wartet. -1: keiner.
        private int _smokeArmed = -1;
        private string _upgradeFocus = "";
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
        // --shots=<anzahl>,<abstand> macht daraus eine Bildfolge: datei_00.png, datei_01.png und so fort.
        // Mit --shot-on-fire=<n> entsteht das Bild im Moment des ersten Schusses ab Takt n.
        // --plan zeigt den Lageplan, --look=<x>,<y> hält die Kamera auf einem Feld.
        // --flash=<stärke> hält einen Blitz an, --ship=<x>,<y> stellt das Luftschiff über dieses Feld.
        // --rain=<stärke> hält den Regen, von 0 bis 1. --smoke lässt alle ruhigen Wachen gleich rauchen.
        // --menu öffnet auch im Prüflauf den Startbildschirm, --fresh tut so, als gäbe es keinen Spielstand.
        // --script=<datei> spielt Tasten und Klicks ab, siehe TestScript. Mit --script-writes schreibt er auch Dateien,
        // das gehört nur in einen eigenen APPDATA-Ordner.
        private bool _forceMenu;
        private bool _fresh;
        private string? _scriptPath;
        private bool _scriptWrites;
        private float _testFlash = -1f;
        private float _testRain = -1f;
        private bool _testSmoke;
        private Vector2? _testPot;

        /// <summary>Die Passanten der Kulisse, null vor dem Aufbau der Stadt. Ihre Orte liefert Passersby.Positions.</summary>
        public Passersby? Passersby { get; private set; }
        private Vector2? _testShip;
        private bool _forcePlan;
        // --intro zeigt im Prüflauf alle Sequenzen, --seq=<id> beginnt mit dieser, --seq-time=<s> springt in ihr vor.
        private bool _forceIntro;
        private string? _forceSequence;
        private bool _sequencesForced;
        // Die Akte beginnt gerade von vorn: Beim Betreten läuft der Prolog. Eine alte Datei einstieg_gesehen zählt nicht mehr.
        private bool _newCampaign;
        // Ein Spiel ist begonnen. Solange nur der Startbildschirm offen ist, wird nichts gespeichert und kein Abbruch verbucht.
        private bool _playing;
        private float _sequenceTime;
        private Vector2? _look;
        private int _shotOnFireTick = -1;
        private string? _shotPath;
        private int _shotFrame = -1;
        private int _shotCount = 1, _shotEvery = 1;
        private bool _testCivilians;
        private bool _auto;
        private int _frame;

        public World World { get; private set; } = null!;

        /// <summary>Waffen und Ausrüstung aus data/waffen.json, auch für die Waffenkammer.</summary>
        public ItemCatalog Catalog { get; private set; } = null!;
        public HashSet<int> Selected { get; } = new();
        public Vector2 MarkerPos { get; private set; }
        public float MarkerTime { get; private set; }
        public bool Finished => _finished;
        public SoundScape Sound => _sound;
        public List<double[]> LampSpots => _mission.Lamps;
        public Color NightColor => Night;
        public float Lightning => _lightning;
        public WindowLife Windows => _windowLife;
        public IReadOnlyList<Sprite2D> UnitSprites => _unitSprites;

        /// <summary>Die Karte, schon bevor der Einsatz beginnt.</summary>
        public MapData Map => _map;


        /// <summary>Regen, Wind und woher das Blitzlicht kommt.</summary>
        public Weather Climate { get; private set; } = null!;

        /// <summary>Wann die Wachen welches Feld im Blick haben. Grundlage für Lageplan und Wegvorschau.</summary>
        public WatchMap Watch { get; private set; } = null!;

        /// <summary>Q gehalten: Lageplan und Wegvorschau anzeigen.</summary>
        public bool PlanHeld => _forcePlan || (!ControlsLocked && Input.IsPhysicalKeyPressed(Key.Q));
        public List<(int X, int Y)> PreviewPath { get; } = new();
        public int PreviewRisk { get; private set; } = -1;
        public string PreviewText { get; private set; } = "";

        /// <summary>Agenten, die auf den freien Weg warten, und die Sekunden bis dahin.</summary>
        public HashSet<int> WaitingIds { get; } = new();
        public int WaitSeconds => Mathf.CeilToInt((_queuedTick - World.Tick) / (float)World.TickRate);

        public override void _Ready()
        {
            ParseArgs();
            LoadSettings();
            // Gleiche Farbe wie der Himmel in ground.png, damit außerhalb der Karte kein Streifen sichtbar ist.
            RenderingServer.SetDefaultClearColor(new Color(52 / 255f, 56 / 255f, 78 / 255f) * Night);
            AddChild(new CanvasModulate { Color = Night });
            string root = ProjectSettings.GlobalizePath("res://");
            string data = Path.GetFullPath(Path.Combine(root, "..", "data"));
            _map = Loader.ParseMap(File.ReadAllText(Path.Combine(data, "map_01.json")));
            _lightData = Loader.ParseLight(File.ReadAllText(Path.Combine(data, "light_01.json")));
            Catalog = Loader.ParseCatalog(File.ReadAllText(Path.Combine(data, "waffen.json")));
            _mission = LoadMission();
            Watch = WatchMap.Measure(_map, _mission);

            LoadCampaign();
            ulong loadStart = Time.GetTicksMsec();
            LoadStatics();
            _loadStaticsMs = Time.GetTicksMsec() - loadStart;
            _camera = new Camera2D { Zoom = new Vector2(2, 2) };
            AddChild(_camera);
            _camera.MakeCurrent();
            AddEnvironment();
            // Eigene Ebene, die der Kamera folgt: Ringe und Balken bleiben auch in der Nacht gut lesbar.
            _overlayLayer = new CanvasLayer { Layer = 1, FollowViewportEnabled = true };
            AddChild(_overlayLayer);
            _overlayLayer.AddChild(new Overlay { Game = this });
            _chatter = new Chatter { Game = this };
            _overlayLayer.AddChild(_chatter);
            BuildUi();
            _playing = Testing && !_forceMenu;
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
            _menus = new Menus { Game = this };
            AddChild(_menus);
            ApplySound();
            ApplyWindow();
            if (!Testing || _forceMenu)
            {
                OpenMainMenu();
            }
            else
            {
                MaybeStartIntro();
            }
            if (_scriptPath != null)
            {
                AddChild(new TestScript { Game = this, FilePath = _scriptPath });
            }
        }

        /// <summary>Prüfläufe schreiben nichts in die Dateien des Spielers.</summary>
        private bool Testing => _shotPath != null || _auto || _look != null || _scriptPath != null;
        private bool WritesFiles => !Testing || _scriptWrites;

        /// <summary>Menü offen, Einstieg läuft oder Einsatz vorbei: Das Spiel nimmt keine Befehle an.</summary>
        private bool ControlsLocked => _introRunning || _finished || Briefing || (_menus != null && _menus.IsOpen);

        public Settings Settings => _settings;
        public bool HasSave => _hadSave;
        public bool AbandonCostsSquad => Campaign.AbandonCostsSquad(World);
        public string CampaignLine => $"Akte {_slot}: {SaveFolder.Describe(_campaign)}";
        public int CurrentSlot => _slot;

        /// <summary>Ein Aktenordner im Menü. Mit --fresh gelten alle als leer.</summary>
        public bool SlotExists(int slot) => !_fresh && _saves.Exists(slot);
        public string SlotLine(int slot) => SlotExists(slot) ? _saves.Describe(slot) : "leer";

        /// <summary>Wer in der Akte gerade aussetzt, für den Aktenschrank.</summary>
        public List<string> SlotResting(int slot) => SlotExists(slot) ? SaveFolder.Resting(_saves.Load(slot)) : new List<string>();

        /// <summary>Wer im laufenden Einsatz fehlt, weil er aussetzt. Nach dem Einsatz leer, dann zählt schon der nächste.</summary>
        public string RestingLine => _finished ? ""
            : string.Join("\n", SaveFolder.Resting(_campaign, World.Units.Where(u => u.Faction == Faction.Agent).Select(u => u.Name).ToList()));

        /// <summary>Liegt in der Akte eine Kampagne, die schon etwas erlebt hat? Nur dann fragt "Neues Spiel" vor dem Überschreiben.</summary>
        public bool SlotBegun(int slot) => !_fresh && _saves.Begun(slot);

        /// <summary>Stempel auf der Akte eines gefallenen Agenten: wer schon einmal geflickt wurde, ist tot.</summary>
        public string FallStamp(Unit unit) => _campaign.FallIsFatal(unit.Name) ? "GEFALLEN" : "KRANKENSTUBE";

        /// <summary>Beruf für die Akte im Einsatz, aus den Missionsdaten.</summary>
        public string AgentRole(int index) => index < _mission.Agents.Count ? _mission.Agents[index].RoleName() : "Agent";

        private static string SettingsPath => ProjectSettings.GlobalizePath("user://einstellungen.json");

        private void LoadSettings()
        {
            if (WritesFiles && File.Exists(SettingsPath))
            {
                _settings = Settings.FromJson(File.ReadAllText(SettingsPath));
            }
        }

        public void SaveSettings()
        {
            if (WritesFiles)
            {
                File.WriteAllText(SettingsPath, _settings.ToJson());
            }
        }

        public void ApplySound() => _sound.SetVolumes(_settings.MasterVolume, _settings.MusicVolume, _settings.EffectsVolume, _settings.Muted);

        private void ApplyWindow()
        {
            // Prüfläufe dürfen kleinere Fenster und --resolution behalten.
            if (!WritesFiles)
            {
                return;
            }
            DisplayServer.WindowSetMinSize(MinWindow);
            if (_settings.Fullscreen)
            {
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            }
        }

        public void ToggleMute()
        {
            _settings.Muted = !_settings.Muted;
            ApplySound();
            SaveSettings();
            _menus.Refresh();
        }

        public void ToggleFullscreen()
        {
            bool full = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
            DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
            _settings.Fullscreen = !full;
            SaveSettings();
            _menus.Refresh();
        }

        private void OpenMainMenu()
        {
            _playing = false;
            _paper.Visible = false;
            _overlayLayer.Visible = false;
            _menus.ShowTitle();
        }

        /// <summary>Vom Startbildschirm in den Einsatz, beim Betreten läuft der Einstieg.</summary>
        public void ContinueMission()
        {
            _hadSave = true;
            _paper.Visible = true;
            _overlayLayer.Visible = true;
            _menus.CloseAll();
            MaybeStartIntro();
        }

        public void NewCampaign() => OpenSlot(_slot, true);

        /// <summary>Öffnet einen Aktenordner und tritt den Einsatz an. Neu heißt: die Akte beginnt von vorn.</summary>
        public void OpenSlot(int slot, bool fresh)
        {
            _playing = true;
            _slot = SaveFolder.ClampSlot(slot);
            _settings.LastSlot = _slot;
            SaveSettings();
            _newCampaign = fresh || !SlotExists(_slot);
            _campaign = _newCampaign ? new Campaign() : LoadSlot(_slot);
            _briefFirst = _newCampaign;
            SaveCampaign();
            _mission = LoadMission();
            StartMission();
            Plan.Reset();
            ContinueMission();
        }

        /// <summary>Verlässt einen laufenden Einsatz, das zählt wie ein Abbruch. Danach steht der Startbildschirm wieder am Anfang.</summary>
        public void ToMainMenu()
        {
            BookLeaving();
            _menus.CloseAll();
            StartMission();
            _showIntro = true;
            OpenMainMenu();
        }

        public void QuitGame()
        {
            BookLeaving();
            SaveSettings();
            GetTree().Quit();
        }

        private void BookLeaving()
        {
            if (!_finished && _playing)
            {
                _campaign.Abandon(World);
                SaveCampaign();
            }
        }

        /// <summary>Nach dem Schließen eines Blatts bekommt der Ergebnis-Bildschirm die Tasten zurück.</summary>
        public void MenusClosed()
        {
            if (_panel.Visible)
            {
                _panelFocus?.GrabFocus();
            }
            else if (_armory.Visible)
            {
                _armory.Refocus();
            }
            else if (_cityMap.Visible)
            {
                _cityMap.Refocus();
            }
        }

        /// <summary>Stadtplan und Waffenkammer liegen offen: Der Einsatz dahinter ruht.</summary>
        private bool Briefing => _cityMap != null && (_cityMap.Visible || _armory.Visible);

        /// <summary>Woher der Stadtplan seine Viertel hat, siehe CityPlanSource.</summary>
        public CityPlanSource Plan { get; private set; } = null!;

        /// <summary>Der Einsatz, der gerade ansteht.</summary>
        public MissionData Mission => _mission;
        public string MissionKey => MissionId;

        /// <summary>Die Kampagne für die Waffenkammer. Nach jeder Änderung CampaignChanged rufen, das speichert.</summary>
        public Campaign Campaign => _campaign;

        public void CampaignChanged() => SaveCampaign();

        /// <summary>Wie der Weg aus Ergebnis und Waffenkammer heißt: nach einem Sieg weiter, sonst derselbe Einsatz noch einmal.</summary>
        public string NextMissionLabel => World.State == MissionState.Won ? "Nächster Einsatz" : "Nochmal";

        /// <summary>
        /// Vor jedem Einsatz: erst der Stadtplan, dann die Waffenkammer, danach then. Mit Bericht führt Esc vom Plan dorthin zurück.
        /// </summary>
        private void OpenBriefing(bool fromResult, Action then)
        {
            // Telegramm und Akten des vergangenen Einsatzes zeigen alte Zahlen, die Kammer hat ihre eigene Kasse.
            _paper.Visible = false;
            _panel.Visible = false;
            _briefingFromResult = fromResult;
            _afterBriefing = then;
            _cityMap.Open(fromResult);
        }

        public void CityMapBack()
        {
            _paper.Visible = true;
            ShowPanel();
        }

        public void CityMapChosen() => _armory.Open();

        public void ArmoryBack() => _cityMap.Open(_briefingFromResult);

        public void LeaveArmory()
        {
            _armory.Close();
            _paper.Visible = true;
            var then = _afterBriefing;
            _afterBriefing = null;
            then?.Invoke();
        }

        /// <summary>Wo im Prüflauf nach Knöpfen gesucht wird: im offenen Blatt, sonst überall.</summary>
        public Node ActiveUi() => _menus.IsOpen ? _menus : this;

        /// <summary>Zustand für den Prüflauf.</summary>
        public Dictionary<string, string> Probe() => new()
        {
            ["screen"] = _menus.ScreenName,
            ["title"] = _menus.TitleParts,
            ["focus"] = GetViewport().GuiGetFocusOwner() is Button button ? button.Text : GetViewport().GuiGetFocusOwner()?.GetClass() ?? "",
            ["tick"] = World.Tick.ToString(),
            ["paused"] = _paused.ToString(),
            ["finished"] = _finished.ToString(),
            ["panel"] = _panel.Visible.ToString(),
            ["hud"] = _paper.Visible.ToString(),
            ["help"] = _help.Visible.ToString(),
            ["helpline"] = _helpPlain,
            ["helprows"] = string.Join(" / ", _help.Text.Split('\n').Select(row => row.Replace(' ', ' ').Trim())),
            ["resting"] = RestingLine.Replace("\n", "; "),
            ["stamps"] = string.Join(",", World.Units.Where(u => u.Faction == Faction.Agent && !u.Alive).Select(u => $"{u.Name}:{FallStamp(u)}")),
            ["begun"] = SlotBegun(_slot).ToString(),
            ["saved"] = _saves.Exists(_slot).ToString(),
            ["camera"] = _camera.Position.ToString(),
            ["sequence"] = _sequence?.Id ?? "",
            ["beat"] = _sequence?.Beat.ToString() ?? "",
            ["selected"] = string.Join(",", Selected.OrderBy(id => id)),
            ["moving"] = World.Units.Count(u => u.Faction == Faction.Agent && u.Moving).ToString(),
            ["plan"] = _planBand.Visible.ToString(),
            ["master"] = _settings.MasterVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["music"] = _settings.MusicVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["musicdb"] = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Musik")).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
            ["muted"] = AudioServer.IsBusMute(0).ToString(),
            ["shake"] = _settings.ScreenShake.ToString(),
            ["fullscreen"] = (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen).ToString(),
            ["window"] = DisplayServer.WindowGetSize().ToString(),
            ["money"] = _campaign.Money.ToString(),
            ["slot"] = _slot.ToString(),
            ["down"] = World.Units.Count(u => u.Faction == Faction.Agent && !u.Alive).ToString(),
            ["rating"] = World.Rating.ToString(),
            ["cash"] = World.Money.ToString(),
            ["weapons"] = string.Join(", ", World.Units.Where(u => u.Faction == Faction.Agent).Select(u => $"{u.Name}:{WeaponWord(u)}")),
            ["items"] = string.Join(", ", World.Units.Where(u => u.Faction == Faction.Agent).Select(u => $"{u.Name}:{string.Join("+", World.ItemIds(u))}")),
            ["hp"] = string.Join(",", World.Units.Where(u => u.Faction == Faction.Agent).Select(u => u.Hp)),
            ["smokes"] = World.Smokes.Count.ToString(),
            ["armory"] = _armory.State,
            ["citymap"] = _cityMap.State,
            ["state"] = World.State.ToString(),
            ["item"] = World.ItemTaken.ToString(),
            ["fields"] = _paper.FieldTexts,
            ["slots"] = _paper.SlotTexts,
            ["peil"] = _paper.PeilState,
            ["peilsec"] = _paper.PeilInterval.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            ["armed"] = _smokeArmed.ToString(),
            ["stash"] = string.Join("+", _campaign.Stash),
            ["gear"] = string.Join(", ", _campaign.Roster().Where(a => !a.Dead).Select(a => $"{a.Name}:{string.Join("+", a.Items)}")),
        };

        /// <summary>Waffe in der Hand für die Akte: Kurzname und Schüsse im Magazin, beim Nachladen ein Hinweis.</summary>
        public string WeaponWord(Unit u)
        {
            var item = u.ActiveItem;
            if (item == null)
            {
                return "ohne Waffe";
            }
            if (item.Def.Magazine <= 0)
            {
                return item.Def.ShortName;
            }
            return u.ReloadTicks > 0 ? $"{item.Def.ShortName} lädt" : $"{item.Def.ShortName} {item.Ammo}";
        }

        private MissionData LoadMission()
        {
            string path = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data", "mission_01.json"));
            var mission = Loader.ParseMission(File.ReadAllText(path));
            mission.Light = _lightData;
            mission.Catalog = Catalog;
            return mission;
        }

        /// <summary>
        /// Brustbild eines Agenten für sein Feld in der Spalte, aus game/assets/hud (tools/art/portraits.py). Die einzige Stelle,
        /// an der es gewählt wird. Ein Bild je Agent, wer anders heißt, ist ein Rekrut. Zustände zeigt die Spalte durch Abdunkeln und Stempel.
        /// </summary>
        public Texture2D AgentPortrait(Unit u)
        {
            string who = u.Name.ToLowerInvariant();
            string key = PortraitNames.Contains(who) ? who : "rekrut";
            if (!_portraits.TryGetValue(key, out var picture))
            {
                var image = Image.LoadFromFile(ProjectSettings.GlobalizePath($"res://assets/hud/portrait_{key}.png"));
                // Das Bild ist viel größer als sein Feld, ohne Mipmaps flimmert es beim Verkleinern.
                image.GenerateMipmaps();
                picture = ImageTexture.CreateFromImage(image);
                _portraits[key] = picture;
            }
            return picture;
        }

        private static readonly string[] PortraitNames = { "greta", "otto", "emil", "hanna" };
        private readonly Dictionary<string, Texture2D> _portraits = new();
        // Die Hilfezeile ohne Umbruch, wie HelpLine sie liefert.
        private string _helpPlain = "";

        /// <summary>Agent, dessen Rauchtopf auf ein Ziel wartet, sonst -1.</summary>
        public int SmokeArmedFor => _smokeArmed;

        /// <summary>
        /// Klick auf einen Platz im Inventar: Waffe in die Hand wie E, Verband wie H. Der Rauchtopf wartet danach auf einen Linksklick
        /// in die Welt, das entspricht G mit der Maus über dem Ziel. Ein zweiter Klick auf den Platz nimmt das zurück.
        /// </summary>
        public void UseSlot(int unitId, int slot)
        {
            if (ControlsLocked || unitId >= World.Units.Count || slot >= World.Units[unitId].Items.Count)
            {
                return;
            }
            var item = World.Units[unitId].Items[slot];
            if (item.Def.IsWeapon)
            {
                World.SelectWeapon(unitId, slot);
            }
            else if (item.Def.Kind == ItemKind.Medkit)
            {
                World.OrderUseMedkit(unitId);
            }
            else if (item.Def.Kind == ItemKind.Smoke)
            {
                _smokeArmed = _smokeArmed == unitId ? -1 : unitId;
            }
        }

        /// <summary>Kurzes Wort für die Akte: Steht der Agent im Dunkeln oder im Licht?</summary>
        public string LightWord(Unit u)
        {
            int level = World.LightLevelAt(u.X, u.Y);
            return level <= World.DarkLevel ? "im Dunkeln" : level >= World.BrightLevel ? "im Licht" : "";
        }

        /// <summary>Prüfläufe zeigen keine Sequenzen, außer mit --intro oder --seq.</summary>
        private bool SequencesOn => !Testing || _forceIntro || _sequencesForced;

        /// <summary>Beim Betreten des Einsatzes: nach einem neuen Spiel der ganze Einstieg, sonst nur der Übergang.</summary>
        private void MaybeStartIntro()
        {
            // Ein neues Spiel aus dem Menü geht nach dem Prolog über Stadtplan und Waffenkammer in den Einsatz.
            bool brief = _briefFirst;
            _briefFirst = false;
            if (!_showIntro || !SequencesOn)
            {
                _camera.PositionSmoothingEnabled = !Testing;
                _camera.PositionSmoothingSpeed = CameraFollow;
                if (brief)
                {
                    // Der Einsatz beginnt danach neu, damit die Einkäufe aus der Kammer mitkommen.
                    OpenBriefing(false, StartMission);
                }
                return;
            }
            _showIntro = false;
            // Bei jedem neuen Spiel erzählt Kolb den Prolog, danach folgen Kamerafahrt und Auftrag ohne Titel und Ortskarte.
            string[] ids = SequenceEntry.For(_newCampaign, _forceSequence);
            _newCampaign = false;
            _forceSequence = null;
            void Play(int i)
            {
                if (i == 1 && brief)
                {
                    brief = false;
                    OpenBriefing(false, () =>
                    {
                        StartMission();
                        Play(1);
                    });
                }
                else if (i < ids.Length)
                {
                    PlaySequence(ids[i], () => Play(i + 1));
                }
            }
            Play(0);
        }

        /// <summary>
        /// Spielt eine Sequenz aus data/sequences und ruft danach genau einmal done. Der Spielstand ist vorher verbucht,
        /// die Sequenz schreibt nichts. Fehlt die Datei oder ist sie kaputt, geht es ohne sie weiter.
        /// </summary>
        public void PlaySequence(string id, Action? done = null)
        {
            SequenceData data;
            try
            {
                data = SequencePlayer.Load(id);
            }
            catch (Exception e) when (e is IOException or System.Text.Json.JsonException)
            {
                GD.PushError($"Sequenz {id}: {e.Message}");
                done?.Invoke();
                return;
            }
            if (!SequencesOn || data.Problems().Any())
            {
                foreach (string problem in data.Problems())
                {
                    GD.PushError($"Sequenz {id}: {problem}");
                }
                done?.Invoke();
                return;
            }
            _introRunning = true;
            _paper.Visible = false;
            _overlayLayer.Visible = false;
            _camera.PositionSmoothingEnabled = false;
            _camera.Offset = Vector2.Zero;
            _sequence = new SequencePlayer
            {
                Game = this, Data = data, Texts = SequenceTexts.For(_mission, World, _abandoned, _campaign),
                OnDone = () =>
                {
                    EndSequence();
                    done?.Invoke();
                },
            };
            AddChild(_sequence);
            if (_sequenceTime > 0f)
            {
                _sequence.FastForward(_sequenceTime);
                _sequenceTime = 0f;
            }
        }

        /// <summary>Kameraziel einer Sequenz im Bild. @start ist der Blick, mit dem der Einsatz beginnt.</summary>
        public Vector2 SequenceAnchor(string target) =>
            SequenceData.Locate(target, World) is { } p ? ToScreen(p.X, p.Y) : _cameraStart;

        /// <summary>Kamerafahrt einer Sequenz.</summary>
        public void SetIntroCamera(Vector2 position, float zoom)
        {
            _camera.Position = position;
            _camera.Zoom = new Vector2(zoom, zoom);
        }

        private void EndSequence()
        {
            _introRunning = false;
            _sequence = null;
            _paper.Visible = true;
            _overlayLayer.Visible = true;
            _camera.Zoom = new Vector2(_baseZoom, _baseZoom);
            _camera.PositionSmoothingEnabled = !Testing;
            _camera.PositionSmoothingSpeed = CameraFollow;
        }

        /// <summary>Lässt das Bild kurz zittern, etwa bei Donner oder nahen Schüssen. Stärke von 0 bis 1.</summary>
        public void ShakeCamera(float amount)
        {
            if (_settings.ScreenShake)
            {
                _trauma = Mathf.Min(1f, Mathf.Max(_trauma, amount));
            }
        }

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
            // Die Spielfläche beginnt rechts der Spalte: Die Bildmitte der Welt rückt um die halbe Spaltenbreite nach rechts.
            float shift = _paper.Visible ? Hud.ColumnWidth / 2f / zoom : 0f;
            _hudShift = Mathf.MoveToward(_hudShift, shift, HudShiftPerSecond * dt);
            offset.X -= _hudShift;
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
                Game = this, Night = Night, LampColor = LampColor, HeldFlash = _testFlash, HeldRain = _testRain, Glow = _critterGlow,
                ManifestPath = ProjectSettings.GlobalizePath("res://assets/manifest.json"),
                GroundLight = LoadTexture("res://assets/light/ground_a.png"), FogNoise = LoadTexture("res://assets/light/fog.png"),
                GroundSize = _groundSize, GroundOrigin = new Vector2(_ox, _oy),
            };
            Climate = weather;
            AddChild(weather);
            weather.AddLampCones(_mission.Lamps, StutterLamp);
            AddChild(new Water { Game = this, ManifestPath = ProjectSettings.GlobalizePath("res://assets/manifest.json") });
            AddChild(new GunSmoke { Game = this });
            var tram = new Tram { Game = this, ManifestPath = ProjectSettings.GlobalizePath("res://assets/manifest.json") };
            AddChild(tram);
            _puddleMirror.Tram = tram;
            AddChild(new Breath { Game = this, Glow = _critterGlow, HeldSmoke = _testSmoke });
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
                    _sound.AddLoop("television", center, -32f, 180f, hushable: true);
                }
            }
            if (_windowLife.SignCenter is { } sign)
            {
                _sound.AddLoop("neon", sign, -34f, 260f);
            }
            // Ein Radio spielt hinter einem Fenster des Hotels, ganz leise. Hinter dem letzten Fenster klimpert ein Klavier.
            var windows = _windowLife.PaneSpots.Where(s => s.Item1 == "window").Select(s => s.Item2).ToList();
            if (windows.Count > 0)
            {
                _sound.AddLoop("radio", windows[0], -24f, 230f, hushable: true);
            }
            if (windows.Count > 1)
            {
                _sound.AddLoop("piano", windows[^1], -26f, 230f, hushable: true);
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
            var fogNoise = LoadTexture("res://assets/light/fog.png");
            // Smog, Laternen und Hochbahn stehen in den Straßen der Häuser hinter der Karte, ohne diese hingen sie in der Luft.
            if (File.Exists(ProjectSettings.GlobalizePath("res://assets/backdrop.png")))
            {
                AddChild(new Skyline
                {
                    Game = this, ZIndex = -1, Backdrop = LoadTexture("res://assets/skyline.png"), Effects = LoadTexture("res://assets/skyline_fx.png"), Noise = fogNoise,
                    DataPath = ProjectSettings.GlobalizePath("res://assets/skyline.json"),
                });
            }
            var wet = new WetGround
            {
                Game = this, ZIndex = 0, Windows = _windowLife, Noise = fogNoise, GroundSize = _groundSize,
                ManifestPath = ProjectSettings.GlobalizePath("res://assets/manifest.json"),
                LampColor = LampColor, FireColor = FireColor, StutterLamp = StutterLamp, Lamps = _mission.Lamps, Fires = _ambience.Fires,
                ExtraFires = new[] { ToScreen((int)(Strikers.BarrelTile.X * World.TileUnits), (int)(Strikers.BarrelTile.Y * World.TileUnits)) },
                Puddles = _ambience.Puddles,
            };
            AddChild(wet);
            _puddleMirror = new PuddleMirror
            {
                Game = this, ZIndex = 0, Wet = wet, Windows = _windowLife, Noise = fogNoise,
                GroundPath = ProjectSettings.GlobalizePath("res://assets/ground.png"), GroundLight = LoadTexture("res://assets/light/ground_a.png"), GroundLightB = LoadTexture("res://assets/light/ground_b.png"),
                LampColor = LampColor, FireColor = FireColor, StutterLamp = StutterLamp, Lamps = _mission.Lamps,
                ManifestPath = ProjectSettings.GlobalizePath("res://assets/manifest.json"), Puddles = _ambience.Puddles,
            };
            AddChild(_puddleMirror);
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
            _windowLife.Game = this;
            AddChild(_windowLife);
            _critterGlow = glow;
            AddChild(new Haze { Game = this });
            AddChild(new Drains { Game = this });
            Passersby = new Passersby { Game = this, FiguresPath = ProjectSettings.GlobalizePath("res://assets/figures.json") };
            AddChild(Passersby);
            AddChild(new SmokePot { Game = this, TestAt = _testPot });
            AddChild(new RainLayer { Game = this, ZIndex = 3900 });
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
                else if (a.StartsWith("--shots="))
                {
                    var series = a["--shots=".Length..].Split(',');
                    _shotCount = Mathf.Max(1, int.Parse(series[0]));
                    _shotEvery = series.Length > 1 ? Mathf.Max(1, int.Parse(series[1])) : 1;
                }
                else if (a.StartsWith("--shot-on-fire="))
                {
                    _shotOnFireTick = int.Parse(a["--shot-on-fire=".Length..]);
                }
                else if (a.StartsWith("--flash="))
                {
                    _testFlash = float.Parse(a["--flash=".Length..], System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (a.StartsWith("--pot="))
                {
                    // Prüflauf: stellt eine Rauchwolke wie vom Rauchtopf auf dieses Feld.
                    var pot = a["--pot=".Length..].Split(',');
                    _testPot = new Vector2(float.Parse(pot[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(pot[1], System.Globalization.CultureInfo.InvariantCulture));
                }
                else if (a == "--smoke")
                {
                    _testSmoke = true;
                }
                else if (a.StartsWith("--rain="))
                {
                    _testRain = float.Parse(a["--rain=".Length..], System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (a.StartsWith("--ship="))
                {
                    var ship = a["--ship=".Length..].Split(',');
                    _testShip = new Vector2(float.Parse(ship[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ship[1], System.Globalization.CultureInfo.InvariantCulture));
                }
                else if (a == "--passanten")
                {
                    _testCivilians = true;
                }
                else if (a == "--intro")
                {
                    _forceIntro = true;
                }
                else if (a.StartsWith("--seq="))
                {
                    _forceSequence = a["--seq=".Length..];
                    _sequencesForced = true;
                }
                else if (a.StartsWith("--peil="))
                {
                    _peilSeconds = float.Parse(a["--peil=".Length..], System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (a.StartsWith("--seq-time="))
                {
                    _sequenceTime = float.Parse(a["--seq-time=".Length..], System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (a == "--menu")
                {
                    _forceMenu = true;
                }
                else if (a == "--fresh")
                {
                    _fresh = true;
                }
                else if (a == "--script-writes")
                {
                    _scriptWrites = true;
                }
                else if (a.StartsWith("--script="))
                {
                    _scriptPath = a["--script=".Length..];
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

        /// <summary>Bild der Stadt zum Anzeigen: Ränder durchsichtiger Flächen bekommen die Farbe ihrer Nachbarn,
        /// sonst zeichnet die weiche Überblendung dort einen dunklen Saum.</summary>
        private static ImageTexture DisplayTexture(Image image)
        {
            image.FixAlphaEdges();
            return ImageTexture.CreateFromImage(image);
        }

        /// <summary>Feinere Fassung eines Bilds aus assets/hd, falls vorhanden, sonst das einfache Bild.
        /// Die feinen Bilder haben ihre Ränder schon beim Export aufgefüllt bekommen.</summary>
        private (ImageTexture Texture, Vector2 Scale) Display(string file, Func<Image> fallback)
        {
            if (_hdImages.TryGetValue(file, out var fine))
            {
                return (ImageTexture.CreateFromImage(fine), Vector2.One / _hdRes);
            }
            return (DisplayTexture(fallback()), Vector2.One);
        }

        /// <summary>Lädt alle feinen Bilder gleichzeitig, das Entpacken der PNG-Dateien ist der größte Teil der Ladezeit.</summary>
        private void LoadHdImages(IEnumerable<string> files)
        {
            var list = files.ToList();
            var images = new Image[list.Count];
            System.Threading.Tasks.Parallel.For(0, list.Count, i =>
                images[i] = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/hd/" + list[i])));
            for (int i = 0; i < list.Count; i++)
            {
                _hdImages[list[i]] = images[i];
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
        public float FireLevel { get; private set; } = 1f;

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
            FireLevel = fire;
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
            _figureMaterial = new ShaderMaterial { Shader = new Shader { Code = FigureShaderCode } };
            var blank = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            blank.Fill(Colors.Black);
            _noPanes = ImageTexture.CreateFromImage(blank);
            _lightGrid = Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/light/grid.png"));
            using var manifest = JsonDocument.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/manifest.json")));
            _ox = manifest.RootElement.GetProperty("ox").GetInt32();
            _oy = manifest.RootElement.GetProperty("oy").GetInt32();
            if (manifest.RootElement.TryGetProperty("hd", out var hd))
            {
                _hdRes = hd.GetProperty("res").GetSingle();
                var files = new List<string> { Path.GetFileName(hd.GetProperty("ground").GetString()!) };
                files.AddRange(hd.GetProperty("things").EnumerateArray().Select(f => "things/" + f.GetString()));
                LoadHdImages(files);
            }
            var (groundTexture, groundScale) = Display("ground.png", () => Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/ground.png")));
            _groundSize = groundTexture.GetSize() * groundScale;
            AddChild(new Sprite2D
            {
                Texture = groundTexture, Scale = groundScale, Centered = false, ZIndex = -1, LightMask = 3,
                TextureFilter = TextureFilterEnum.Linear, Material = LitMaterial("ground"),
            });
            var backdrop = manifest.RootElement.GetProperty("backdrop");
            AddChild(new Sprite2D
            {
                // Die Kulisse ist deckend, sie braucht keinen Randausgleich.
                Texture = LoadTexture("res://assets/" + backdrop.GetProperty("file").GetString()),
                Centered = false,
                TextureFilter = TextureFilterEnum.Linear,
                Position = new Vector2(backdrop.GetProperty("px").GetInt32(), backdrop.GetProperty("py").GetInt32()),
                ZIndex = -2,
                LightMask = 3,
                Material = LitMaterial("backdrop"),
            });
            int index = 0;
            foreach (var t in manifest.RootElement.GetProperty("things").EnumerateArray())
            {
                _things.Add(new ThingInfo(t.GetProperty("x0").GetSingle(), t.GetProperty("y0").GetSingle(),
                    t.GetProperty("x1").GetSingle(), t.GetProperty("y1").GetSingle(), t.GetProperty("z1").GetSingle()));
                string file = "things/" + t.GetProperty("file").GetString();
                // Für die Verdeckung reicht das Bild, das ohnehin angezeigt wird: fein, wenn es eins gibt.
                var image = _hdImages.TryGetValue(file, out var fineImage) ? fineImage : Image.LoadFromFile(ProjectSettings.GlobalizePath("res://assets/" + file));
                var (texture, scale) = Display(file, () => (Image)image.Duplicate());
                _thingImages.Add(image);
                _thingImageScale.Add(scale.X);
                var sprite = new Sprite2D
                {
                    Texture = texture,
                    Scale = scale,
                    TextureFilter = TextureFilterEnum.Linear,
                    Centered = false,
                    Position = new Vector2(t.GetProperty("px").GetInt32(), t.GetProperty("py").GetInt32()),
                    ZIndex = ThingZ(index),
                    LightMask = 3,
                    Material = LitMaterial(Path.GetFileNameWithoutExtension(t.GetProperty("file").GetString()!)),
                };
                AddChild(sprite);
                _thingSprites.Add(sprite);
                _thingCuts.Add(0f);
                index++;
            }
            _hdImages.Clear();
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
            _animator.Load(figures.RootElement);
            _figures = LoadTexture("res://assets/figures.png");
            _figureMaterial.SetShaderParameter("palette", LoadTexture("res://assets/figures_palette.png"));
            _figureMaterial.SetShaderParameter("block", (float)(_cellH * _figureViews));
            FigureMirror = LoadTexture("res://assets/figures_mirror.png");
            FigureMirrorScale = figures.RootElement.GetProperty("mirror").GetSingle();
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
            _paper = new Hud { Game = this };
            layer.AddChild(_paper);
            _paper.PeilInterval = _peilSeconds;
            _planBand = new ColorRect
            {
                AnchorRight = 1f, OffsetLeft = Hud.ColumnWidth + 8f, OffsetTop = 62f, OffsetRight = -10f, OffsetBottom = 108f, Visible = false,
                Color = PaperUi.PaperDark, MouseFilter = Control.MouseFilterEnum.Stop,
            };
            layer.AddChild(_planBand);
            _planLegend = PaperUi.Text(
                "LAGEPLAN  Grün: nie bewacht. Gelb bis Rot: immer öfter im Blick. Punkte: Wege der Wachen. Klick: losgehen, sobald frei.\n"
                + "Zum Buch führt der Weg über die Straße in die Gasse am Mietshaus, zum Wagen hinten um das Lager.",
                13, PaperUi.Ink, PaperUi.Typewriter);
            _planLegend.Position = new Vector2(Hud.ColumnWidth + 18f, 65);
            _planLegend.AnchorRight = 1f;
            _planLegend.OffsetRight = -16f;
            _planLegend.ClipText = true;
            _planLegend.Visible = false;
            layer.AddChild(_planLegend);
            _help = PaperUi.Text(
                "Links: wählen/gehen   Doppelklick: rennen   Rechts: angreifen, Emil: bestechen, Otto: Tür   Q halten: Lageplan   1-4 Leertaste   WASD F P M   Esc: Menü",
                12, new Color(0.75f, 0.72f, 0.65f, 0.75f), PaperUi.Typewriter);
            // Rechts neben der Spalte, bei schmalem Fenster bricht die Zeile nach oben um.
            _help.AnchorTop = 1f;
            _help.AnchorBottom = 1f;
            _help.AnchorRight = 1f;
            _help.OffsetLeft = Hud.ColumnWidth + 12f;
            _help.OffsetRight = -10f;
            _help.OffsetTop = -23f;
            _help.OffsetBottom = -6f;
            _help.GrowVertical = Control.GrowDirection.Begin;
            _help.VerticalAlignment = VerticalAlignment.Bottom;
            _help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            layer.AddChild(_help);
            _banner = PaperUi.Text("", 34, new Color(1f, 0.85f, 0.4f), PaperUi.Serif);
            _banner.AnchorRight = 1f;
            _banner.OffsetLeft = Hud.ColumnWidth;
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
            _armory = new Armory { Game = this };
            layer.AddChild(_armory);
            Plan = new CityPlanSource(this);
            _cityMap = new CityMap { Game = this };
            layer.AddChild(_cityMap);
        }

        private const string MissionId = "mission_01";

        private void LoadCampaign()
        {
            _slot = _settings.LastSlot;
            _hadSave = SlotExists(_slot);
            _campaign = _hadSave ? LoadSlot(_slot) : new Campaign();
            _newCampaign = !_hadSave;
        }

        /// <summary>Lädt eine Akte. Ein unlesbarer oder zu neuer Stand wird nicht stillschweigend überschrieben, sondern beiseitegelegt.</summary>
        private Campaign LoadSlot(int slot)
        {
            var campaign = _saves.Load(slot, out var status);
            if (status == SaveStatus.Discarded && WritesFiles)
            {
                _saves.SetAside(slot);
                GD.PushWarning($"Akte {slot} unlesbar oder zu neu, neue Kampagne. Der alte Stand liegt in {_saves.AsidePathOf(slot)}.");
            }
            return campaign;
        }

        private void SaveCampaign()
        {
            // Prüfläufe dürfen den echten Spielstand nicht verändern, und hinter dem Startbildschirm wird nichts angelegt.
            if (WritesFiles && _playing)
            {
                _saves.Save(_slot, _campaign);
            }
        }

        public override void _Notification(int what)
        {
            // Fenster schließen ist derselbe Abbruch wie R, sonst wäre es der Ausweg vor dem Tod eines Agenten.
            // Auch ohne Schuss, sonst käme bezahltes Bestechungsgeld zurück.
            if (what == NotificationWMCloseRequest && World != null)
            {
                BookLeaving();
                SaveSettings();
            }
        }

        /// <summary>R: fragt nach, was der Abbruch kostet. Vor dem ersten Schuss ist der Rückzug frei, danach kostet er den Trupp.</summary>
        private void PressRestart() => _menus.ConfirmAbandon();

        /// <summary>Verbucht den Abbruch: ohne Schuss telegrafiert K. und es geht neu los, sonst zeigt die Zeitung den verlorenen Trupp.</summary>
        public void AbandonMission()
        {
            bool costs = Campaign.AbandonCostsSquad(World);
            _campaign.Abandon(World);
            SaveCampaign();
            _menus.CloseAll();
            // Verbucht ist alles. Fenster schließen während der Sequenz bucht nichts mehr.
            _finished = true;
            _abandoned = true;
            _lastReward = 0;
            if (!costs)
            {
                PlaySequence("rueckzug", () => OpenBriefing(false, NextMission));
                return;
            }
            PlaySequence("niederlage", ShowResult);
        }

        private void ShowPanel()
        {
            RefreshPanel();
            _panel.Visible = true;
            _panelFocus?.GrabFocus();
        }

        /// <summary>Ergebnis nach einer Sequenz. Die Tasten kommen erst kurz danach an, sonst löst eine wiederholte Leertaste gleich "Nochmal" aus.</summary>
        private void ShowResult()
        {
            if (!SequencesOn)
            {
                ShowPanel();
                return;
            }
            RefreshPanel();
            _panel.Visible = true;
            GetTree().CreateTimer(ResultFocusDelay).Timeout += () =>
            {
                if (_panel.Visible && !_menus.IsOpen)
                {
                    _panelFocus?.GrabFocus();
                }
            };
        }

        private void FinishMission()
        {
            _finished = true;
            _lastReward = World.State == MissionState.Won ? Campaign.Reward(World.Rating) : 0;
            _campaign.Finish(World);
            SaveCampaign();
            PlaySequence(World.State == MissionState.Won ? "sieg" : "niederlage", ShowResult);
        }

        private static Label PanelLabel(string text, int size = 15) => PaperUi.Text(text, size, PaperUi.Ink, PaperUi.Typewriter);

        /// <summary>Baut den Bildschirm zwischen den Einsätzen neu auf: Ergebnis, Kasse und Ausbau.</summary>
        private void RefreshPanel()
        {
            foreach (var child in _panelBody.GetChildren())
            {
                _panelBody.RemoveChild(child);
                child.QueueFree();
            }
            _panelFocus = null;
            bool won = World.State == MissionState.Won;
            var (headline, line) = Headlines.For(World, _abandoned);
            var masthead = new HBoxContainer();
            var paper = PaperUi.Text(Headlines.Paper.ToUpperInvariant(), 14, PaperUi.StampRed, PaperUi.Serif);
            paper.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            masthead.AddChild(paper);
            masthead.AddChild(PaperUi.Text($"in Akte {_slot} abgelegt", 13, new Color(PaperUi.Ink, 0.7f), PaperUi.Typewriter));
            _panelBody.AddChild(masthead);
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
            // Ergebnis und Abrechnung stehen in einer Zeile, darunter die Akten der Agenten.
            var result = new HBoxContainer();
            result.AddThemeConstantOverride("separation", 18);
            string verdict = won ? $"Einsatz geglückt: {RatingText()}" : _abandoned ? "Einsatz aufgegeben" : "Trupp verloren";
            result.AddChild(PaperUi.Text(verdict, 17, PaperUi.StampRed, PaperUi.Serif));
            string loot = won && World.LootTaken ? $" + {World.LootName} {World.LootValue}" : "";
            string pay = won ? $"Lohn {_lastReward}{loot}   " : "";
            result.AddChild(PanelLabel($"{pay}Kasse {_campaign.Money}   Einsätze {_campaign.Missions}"));
            _panelBody.AddChild(result);
            if (World.Units.Any(u => u.Faction == Faction.Agent && (!u.Alive || _abandoned)))
            {
                _panelBody.AddChild(PanelLabel(CampaignTexts.RuleHint, 13));
            }
            _panelBody.AddChild(PanelLabel($"Fahndung {_campaign.Heat}/{Campaign.MaxHeat}   Rückhalt {_campaign.Support}/{Campaign.MaxSupport}, Beitrag {_campaign.Dues} je Einsatz"));
            foreach (var record in _campaign.Roster())
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);
                var name = PanelLabel($"{record.Name}: Beine {record.Legs}, Herz {record.Heart}{AgentStatus(record)}");
                name.CustomMinimumSize = new Vector2(380, 0);
                row.AddChild(name);
                if (record.Dead)
                {
                    row.AddChild(CampaignButton($"Anwerben ({Campaign.RecruitCost})", _campaign.Money >= Campaign.RecruitCost, () => _campaign.TryRecruit(record.Slot)));
                }
                else
                {
                    row.AddChild(UpgradeButton("Beine", record.Name, record.Legs, true));
                    row.AddChild(UpgradeButton("Herz", record.Name, record.Heart, false));
                    if (record.Rest > 0)
                    {
                        row.AddChild(CampaignButton($"Arzt ({Campaign.DoctorCost})", _campaign.Money >= Campaign.DoctorCost, () => _campaign.TryDoctor(record.Name)));
                    }
                }
                _panelBody.AddChild(row);
            }
            _panelBody.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = PaperUi.PaperDark, MouseFilter = Control.MouseFilterEnum.Ignore });
            // Der Weg nach vorn steht rechts und ist vorgewählt, der Weg hinaus links.
            var buttons = new HBoxContainer();
            buttons.AddThemeConstantOverride("separation", 12);
            var menu = new Button { Text = "Zum Hauptmenü", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            PaperUi.InkButton(menu);
            menu.Pressed += ToMainMenu;
            buttons.AddChild(menu);
            var next = new Button { Text = NextMissionLabel, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            PaperUi.InkButton(next);
            next.Pressed += () => OpenBriefing(true, NextMission);
            buttons.AddChild(next);
            _panelBody.AddChild(buttons);
            _panelFocus ??= next;
        }

        private static string AgentStatus(AgentRecord record) =>
            record.Dead ? "  (tot)"
            : record.Infirmary ? "  (Krankenstube, setzt aus)"
            : record.Rest > 0 ? "  (angeschlagen, setzt aus)"
            : record.Falls > 0 ? "  (einmal geflickt)"
            : "";

        private Button CampaignButton(string text, bool enabled, System.Func<bool> act)
        {
            var button = new Button { Text = text, Disabled = !enabled, CustomMinimumSize = new Vector2(130, 0) };
            PaperUi.InkButton(button);
            button.Pressed += () =>
            {
                if (act())
                {
                    SaveCampaign();
                    RefreshPanel();
                }
            };
            return button;
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
            button.FocusMode = button.Disabled ? Control.FocusModeEnum.None : Control.FocusModeEnum.All;
            PaperUi.InkButton(button);
            // Nach dem Kauf wird die Liste neu gebaut. Die Tastenwahl bleibt auf diesem Knopf, solange er noch geht.
            string key = agent + label;
            if (key == _upgradeFocus && !button.Disabled)
            {
                _panelFocus = button;
            }
            button.Pressed += () =>
            {
                if (_campaign.TryUpgrade(agent, legs))
                {
                    SaveCampaign();
                    _upgradeFocus = key;
                    ShowPanel();
                }
            };
            return button;
        }

        /// <summary>Neuer Anlauf nach dem Ergebnis: Ort und Uhrzeit, dann geht es los.</summary>
        private void NextMission()
        {
            StartMission();
            PlaySequence("uebergang");
        }

        private void StartMission()
        {
            foreach (var s in _unitSprites)
            {
                s.QueueFree();
            }
            _unitSprites.Clear();
            _unitGlow.Clear();
            _animator.Clear();
            _tickFrom.Clear();
            // Prepare verändert die Missionsdaten, deshalb jedes Mal frisch laden.
            _mission = LoadMission();
            _campaign.Prepare(_mission, MissionId);
            SaveCampaign();
            _finished = false;
            _abandoned = false;
            _upgradeFocus = "";
            WaitingIds.Clear();
            _panel.Visible = false;
            _armory.Visible = false;
            _cityMap.Visible = false;
            _smokeArmed = -1;
            World = new World(_map, _mission);
            foreach (var u in World.Units)
            {
                var sprite = NewFigureSprite();
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
            _cameraStart = _camera.Position;
            if (_auto)
            {
                MoveSelected((int)_mission.Item[0], (int)_mission.Item[1]);
            }
            if (_testCivilians)
            {
                AddChild(new CivilPreview { Game = this });
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
            if (!ControlsLocked)
            {
                HandleCamera((float)delta);
            }
            UpdateCameraLife((float)delta);
            if (_testPanelFrame >= 0 && _frame == _testPanelFrame)
            {
                _abandoned = true;
                _finished = true;
                ShowPanel();
            }
            MarkerTime = Mathf.Max(0f, MarkerTime - (float)delta);
            Selected.RemoveWhere(id => !World.Units[id].Alive);
            if (!_paused && !ControlsLocked)
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
            int shot = _shotFrame >= 0 ? _frame - _shotFrame : -1;
            if (_shotPath != null && shot >= 0 && shot % _shotEvery == 0)
            {
                int index = shot / _shotEvery;
                string path = _shotCount > 1 ? _shotPath.Replace(".png", $"_{index:00}.png") : _shotPath;
                GetViewport().GetTexture().GetImage().SavePng(path);
                if (index >= _shotCount - 1)
                {
                    GD.Print($"Raum {_sound.Enclosure:0.00}, Stille {_windowLife.Hush:0.00}, Geräusche abgespielt: {_sound.PlayCount}  " + string.Join(", ", _sound.PlayedByGroup.Select(g => $"{g.Key} {g.Value}")));
                    GD.Print($"Leistung: {Engine.GetFramesPerSecond():0} fps, Texturen {Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / 1048576:0} MB, "
                        + $"Speicher {OS.GetStaticMemoryUsage() / 1048576} MB, Stadt geladen in {_loadStaticsMs} ms");
                    GetTree().Quit();
                }
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
                var added = NewFigureSprite();
                AddChild(added);
                _unitSprites.Add(added);
            }
            var order = World.Units.OrderBy(u => u.X + u.Y).ToList();
            float delta = (float)GetProcessDeltaTime(), now = Time.GetTicksMsec() / 1000f;
            for (int rank = 0; rank < order.Count; rank++)
            {
                var u = order[rank];
                var s = _unitSprites[u.Id];
                var foot = UnitPos(u);
                _animator.Track(u, foot);
                int column = _animator.Column(World, u, delta, now, out bool fell);
                if (fell && u.Faction == Faction.Agent)
                {
                    _focusAt = ToScreen(u.X, u.Y);
                    _focusLeft = FocusSeconds;
                }
                // Wer die Waffe hebt, hält oder senkt, blickt aufs Ziel, auch wenn er dabei weitergeht.
                var (lookX, lookY) = _animator.Look(World, u, now);
                // Am Boden gedacht: x - y läuft im Bild quer, x + y zur Kamera. Der Westen entsteht durch Spiegeln.
                float across = lookX - lookY, toward = lookX + lookY;
                float angle = Mathf.RadToDeg(Mathf.Atan2(toward, Mathf.Abs(across)));
                int view = Mathf.Clamp(Mathf.RoundToInt((90f - angle) / 45f), 0, _figureViews - 1);
                string row = _animator.SpriteName(u);
                s.RegionRect = FigureRegion(row, view, column);
                SetFigureVariant(s, row, _animator.IsRecruit(u) ? "rekrut" : "");
                s.FlipH = across < 0;
                s.Modulate = FigureGlow(u);
                s.Position = foot - FigureFoot + _animator.BiteOffset(World, u, this);
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

        /// <summary>Tiefe für einen Dunstschleier längs x + y = depth: direkt hinter dem ersten Objekt, das über diese Linie nach vorn reicht.</summary>
        public int BehindLineZ(float depth)
        {
            int i = 0;
            while (i < _things.Count && _things[i].X1 + _things[i].Y1 <= depth)
            {
                i++;
            }
            return ThingZ(i) - 1;
        }

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

        /// <summary>Figur zum Zeichnen: scharf-bilinear wie die Stadt, damit die Pixel bei jeder Bildschirmgröße gleich breit sind.</summary>
        public Sprite2D NewFigureSprite() => new()
        {
            Texture = _figures, RegionEnabled = true, Centered = false, Scale = FigureScale,
            TextureFilter = TextureFilterEnum.Nearest, Material = _figureMaterial,
        };

        /// <summary>Zeilen der Passanten im Figurenblatt. Sie sind Kulisse und haben eigene Bewegungen, siehe docs/figuren.md.</summary>
        public IReadOnlyList<string> Civilians => _animator.Civilians;

        /// <summary>Ausschnitt für einen Passanten: Bewegung walk, run, idle, smoke, startle, cower oder read, frame läuft im Kreis.</summary>
        public Rect2 CivilFrame(string name, int view, string motion, int frame) =>
            FigureRegion(name, view, _animator.CivilColumn(motion, frame));

        public int CivilFrames(string motion) => _animator.CivilFrames(motion);

        /// <summary>Weg je Zyklus in Spieleinheiten für walk oder run eines Passanten.</summary>
        public float CivilStride(string name, string motion) => _animator.StrideOf(name, motion);

        /// <summary>Farbvarianten einer Figur, ohne die Grundfarbe.</summary>
        public IEnumerable<string> FigureVariants(string name) => _animator.Variants(name);

        /// <summary>Färbt einen Figuren-Sprite über eine andere Palettenzeile um. Ein leerer Name stellt die Grundfarbe wieder her.</summary>
        public void SetFigureVariant(Sprite2D sprite, string name, string variant) =>
            sprite.SetInstanceShaderParameter("palette_row", (float)_animator.PaletteRow(name, variant));

        /// <summary>Das Figurenblatt in Farbe und verkleinert, für Spiegelungen. Das große Blatt enthält nur Palettennummern.</summary>
        public Texture2D FigureMirror { get; private set; } = null!;

        /// <summary>Um diesen Faktor ist FigureMirror kleiner als das Figurenblatt.</summary>
        public float FigureMirrorScale { get; private set; } = 1f;

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
        public static int IdleFirstColumn => FigureAnimator.IdleFirst;
        public static int IdleFrameCount => FigureAnimator.IdleCount;
        public static int WalkFirstColumn => FigureAnimator.WalkFirst;
        public static int WalkFrameCount => FigureAnimator.WalkCount;

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
        public int StepsTaken(Unit u) => _animator.StepsTaken(u);

        public Vector2 CameraCenter => _camera.Position;

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

        /// <summary>Öffnet in Objekten, die einen lebenden Agenten verdecken, einen Durchblick um die Figur.</summary>
        private void UpdateOcclusion()
        {
            float step = CutOpenPerSecond * (float)GetProcessDeltaTime();
            for (int i = 0; i < _things.Count; i++)
            {
                var sprite = _thingSprites[i];
                var rect = new Rect2(sprite.Position, (Vector2)_thingImages[i].GetSize() * _thingImageScale[i]);
                int count = 0;
                foreach (var u in World.Units)
                {
                    if (u.Faction != Faction.Agent || !u.Alive || count == MaxCuts)
                    {
                        continue;
                    }
                    var body = new Rect2(ToScreen(u.X, u.Y) + new Vector2(-12, -46), new Vector2(24, 48));
                    if (IsInside(_things[i], u) || (IsInFront(_things[i], u) && rect.Intersects(body)))
                    {
                        _cutPoints[count++] = UnitPos(u) + new Vector2(0, -CutRise);
                    }
                }
                float open = Mathf.MoveToward(_thingCuts[i], count > 0 ? 1f : 0f, step);
                if (count == 0 && _thingCuts[i] == 0f)
                {
                    continue;
                }
                _thingCuts[i] = open;
                var material = (ShaderMaterial)sprite.Material;
                material.SetShaderParameter("cut_open", open);
                // Beim Schließen bleiben die letzten Stellen stehen, bis der Durchblick zu ist.
                if (count > 0)
                {
                    material.SetShaderParameter("cuts", _cutPoints);
                    material.SetShaderParameter("cut_count", count);
                }
                else if (open == 0f)
                {
                    material.SetShaderParameter("cut_count", 0);
                }
            }
        }

        /// <summary>Ob die Füße einer Figur hinter einem Objekt stehen, etwa hinter der Straßenbahn. Für den Auswahlring.</summary>
        public bool IsFootHiddenByThing(Unit u)
        {
            var foot = ToScreen(u.X, u.Y);
            for (int i = 0; i < _things.Count; i++)
            {
                if (IsInFront(_things[i], u) && IsOpaqueAt(i, foot))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True, wenn ein undurchsichtiges Objekt Kopf und Körper der Figur im Bild verdeckt.</summary>
        public bool IsHiddenByThing(Unit u)
        {
            var foot = ToScreen(u.X, u.Y);
            for (int i = 0; i < _things.Count; i++)
            {
                if (IsInFront(_things[i], u)
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
            var p = (screen - _thingSprites[thing].Position) / _thingImageScale[thing];
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
            _banner.Text = _paused && !ControlsLocked ? "PAUSE" : "";
            _help.Visible = _paper.Visible && !_finished;
            _helpPlain = HelpLine();
            string help = Hud.FitHelp(_helpPlain, _help.Size.X, 12);
            if (_help.Text != help)
            {
                _help.Text = help;
            }
        }

        /// <summary>Hilfezeile unten: Bestechen und Tür nennt sie nur, solange ein Buchhalter oder Schlosser im Einsatz lebt.</summary>
        private string HelpLine()
        {
            var agents = World.Units.Where(u => u.Faction == Faction.Agent && u.Alive).ToList();
            string right = string.Concat(
                agents.Where(u => u.Skill == World.SkillBookkeeper).Select(u => $", {u.Name}: bestechen")
                .Concat(agents.Where(u => u.Skill == World.SkillLocksmith).Select(u => $", {u.Name}: Tür")));
            // Die Zifferntasten wählen die Agenten im Einsatz, auch Gefallene behalten ihre Ziffer.
            int squad = World.Units.Count(u => u.Faction == Faction.Agent);
            string keys = squad <= 1 ? "1" : $"1-{squad}";
            // Ausrüstungstasten nur, wenn jemand sie brauchen kann. Mit der Grundausstattung bleibt die Zeile wie bisher.
            string gear = (agents.Any(u => u.Items.Count(i => i.Def.IsWeapon) > 1) ? "   E: Waffe" : "")
                + (agents.Any(u => World.HasItem(u, ItemKind.Smoke)) ? "   G: Rauch" : "")
                + (agents.Any(u => World.HasItem(u, ItemKind.Medkit)) ? "   H: Verband" : "");
            return $"Links: wählen/gehen   Doppelklick: rennen   Rechts: angreifen{right}{gear}   Q halten: Lageplan   {keys} Leertaste   WASD F P M   Esc: Menü";
        }

        public override void _Input(InputEvent e)
        {
            if (e is InputEventMouseMotion motion)
            {
                _mouseAt = motion.Position;
            }
            // Die Leertaste schaltet Sequenzen weiter. Auf dem Ergebnis-Bildschirm löst sie keinen Knopf aus,
            // sonst rutscht ein Druck zu viel in "Nochmal". Enter und Maus wählen dort weiter.
            if ((_panel.Visible || Briefing) && !_menus.IsOpen && e is InputEventKey key && (key.Keycode == Key.Space || key.PhysicalKeycode == Key.Space))
            {
                GetViewport().SetInputAsHandled();
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
            if (e is InputEventKey { Pressed: true, Echo: false } global)
            {
                // Diese Tasten gelten auch in den Menüs.
                if (global.PhysicalKeycode == Key.F11)
                {
                    ToggleFullscreen();
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (global.PhysicalKeycode == Key.M)
                {
                    ToggleMute();
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (global.PhysicalKeycode == Key.Escape && !_menus.IsOpen
                    && ((_armory.Visible && _armory.Back()) || (_cityMap.Visible && _cityMap.Back())))
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (global.PhysicalKeycode == Key.Escape && !_menus.IsOpen && !_introRunning)
                {
                    _menus.ShowPause();
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            if (ControlsLocked)
            {
                return;
            }
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
                    case Key.E:
                        foreach (int id in Selected) World.CycleWeapon(id);
                        break;
                    case Key.G: ThrowSmoke(); break;
                    case Key.H:
                        foreach (int id in Selected) World.OrderUseMedkit(id);
                        break;
                    case Key.P: _paused = !_paused; break;
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
                    if (_smokeArmed >= 0)
                    {
                        var at = ScreenToMap(worldPos);
                        World.OrderThrowSmoke(_smokeArmed, (int)Mathf.Floor(at.X), (int)Mathf.Floor(at.Y));
                        _smokeArmed = -1;
                    }
                    else if (agent != null)
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
                else if (mouse.ButtonIndex == MouseButton.Right && _smokeArmed >= 0)
                {
                    _smokeArmed = -1;
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

        /// <summary>Letzte Mausposition aus dem Ereignis, wie bei den Klicks.</summary>
        private Vector2 _mouseAt;

        /// <summary>Der erste gewählte Agent mit Rauchtopf wirft ihn auf das Feld unter dem Mauszeiger.</summary>
        private void ThrowSmoke()
        {
            var m = ScreenToMap(GetCanvasTransform().AffineInverse() * _mouseAt);
            int tx = (int)Mathf.Floor(m.X), ty = (int)Mathf.Floor(m.Y);
            foreach (int id in Selected.OrderBy(id => id))
            {
                if (World.OrderThrowSmoke(id, tx, ty)) return;
            }
        }

        public void SelectAgent(int index, bool add)
        {
            if (index >= _mission.Agents.Count || !World.Units[index].Alive) return;
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
