using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Messingstadt.Core;

namespace Messingstadt
{
    /// <summary>
    /// Alles Hörbare. Liest nur den Zustand der Spiellogik und spielt dazu Geräusche, die Logik merkt davon nichts.
    /// Die Geräusche liegen in game/assets/audio und stammen aus tools/art/generate_audio.py.
    /// </summary>
    public partial class SoundScape : Node2D
    {
        private const int PoolSize = 24;
        private const float StepReach = 620f;
        private const float AgentStepReach = 420f;
        private const float GuardStepReach = 220f;
        private const float EventReach = 1500f;
        private const float AmbientReach = 300f;
        private const float WhistleCooldown = 9f;
        private const float RepeatCooldown = 0.03f;
        private const float BarkSeconds = 1.8f;
        private const float ShakeReach = 340f;
        private const float ShotShake = 0.3f;
        private const float MusicDb = -19f;
        private const float DarkLayerDb = MusicDb - 2f;
        private const float SilentDb = -60f;
        private const float DarkLight = 0.42f;
        private const float RainDb = -27f;
        private const float RainCurveDb = 16f;
        private const float RainInAlleyDrop = 2f;
        private const float WindDb = -30f;
        private const float GustDb = -24f;
        private const float GustFromWind = 8f;
        private const float GustFullWind = 34f;
        private const float GutterDb = -20f;
        private const float GutterCurveDb = 14f;
        private const float GutterReach = 220f;
        private const float GutterSpacing = 60f;
        private const int MaxGutters = 12;
        private const float HushFadePerSecond = 0.6f;
        private const float TramHumDb = -30f;
        private const float TramHumReach = 260f;
        private const int CalmLayer = 0, DarkLayer = 1, SuspectLayer = 2, FightLayer = 3;
        private const float LayerFadePerSecond = 0.35f;
        private const float TitleDb = MusicDb;
        private const float TitleFadePerSecond = 0.5f;
        private const float SuspectRisePerSecond = 0.7f;
        private const float DarkUnderFight = 0.7f;
        private const int RoomProbeTiles = 6;
        private const float RoomProbeSeconds = 0.25f;
        private const float RoomFadePerSecond = 0.5f;
        private const float OpenWet = 0.05f, AlleyWet = 0.26f;
        private const float OpenRoomSize = 0.35f, AlleyRoomSize = 0.62f;
        private const float OpenRainCutoff = 14000f, AlleyRainCutoff = 5000f;
        private const float LimiterCeilingDb = -0.5f;
        private const float HeartDb = -10f;
        private const float HeartRisePerSecond = 4f;
        private const float HeartFallPerSecond = 0.5f;
        private const string MusicBus = "Musik";
        private const string EffectsBus = "Geräusche";
        private const string WeatherBus = "Wetter";
        private const string WorldBus = "Welt";
        private static readonly (int X, int Y)[] RoomRays = { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };

        private readonly Dictionary<string, AudioStreamWav> _sounds = new();
        private readonly Dictionary<string, float> _lastPlayed = new();
        private readonly List<AudioStreamPlayer2D> _pool = new();
        private readonly Random _rng = new();
        private readonly Dictionary<int, int> _stepCount = new();
        private readonly HashSet<int> _fallen = new();
        private readonly HashSet<int> _bribed = new();
        private readonly HashSet<int> _inCombat = new();
        private readonly HashSet<int> _reporting = new();
        private readonly Dictionary<int, int> _kicks = new();
        private readonly Dictionary<int, int> _dogBite = new();
        private float _nextBark;
        private AudioStreamPlayer2D _lockpick = null!;
        private AudioStreamSynchronized _layers = null!;
        private AudioStreamPlayer _rain = null!;
        private AudioStreamPlayer _wind = null!;
        private AudioStreamPlayer _gust = null!;
        private AudioStreamPlayer _heart = null!;
        private AudioStreamPlayer _title = null!;
        private bool _titleWanted;
        private float _titleLevel;
        private readonly List<AudioStreamPlayer2D> _gutters = new();
        private readonly List<(AudioStreamPlayer2D Player, float Db)> _hushable = new();
        private float _hush;
        private AudioEffectReverb _reverb = null!;
        private AudioEffectLowPassFilter _rainFilter = null!;
        private float _heartLevel;
        private float _calmLevel = 1f, _darkLevel, _suspectLevel, _fightLevel;
        private float _roomWanted;
        private float _nextRoomProbe;
        private string? _heldMusic;
        private World? _world;
        private int _lastShot;
        private bool _itemTaken, _lootTaken, _bellRang, _ended;
        private bool[] _doorsOpen = Array.Empty<bool>();
        private float _time;
        private float _nextWhistle;
        private float _nextPages;

        public Main Game = null!;

        /// <summary>Wie oft insgesamt etwas abgespielt wurde. Für den Prüflauf.</summary>
        public int PlayCount { get; private set; }

        /// <summary>Wie oft welche Art Geräusch lief. Für den Prüflauf, um Dauergeräusche zu finden.</summary>
        public Dictionary<string, int> PlayedByGroup { get; } = new();

        /// <summary>Wie eng es um die Kamera ist, von 0 im Freien bis 1 in der Gasse. Danach richten sich Hall und Regen.</summary>
        public float Enclosure { get; private set; }

        public override void _Ready()
        {
            AddBus(MusicBus, "Master");
            AddBus(EffectsBus, "Master");
            AddBus(WeatherBus, EffectsBus);
            AddBus(WorldBus, EffectsBus);
            AudioServer.AddBusEffect(0, new AudioEffectHardLimiter { CeilingDb = LimiterCeilingDb });
            _reverb = new AudioEffectReverb { Dry = 1f, Wet = OpenWet, RoomSize = OpenRoomSize, Damping = 0.7f, Spread = 1f, Hipass = 0.1f, PredelayMsec = 40f, PredelayFeedback = 0.3f };
            AudioServer.AddBusEffect(AudioServer.GetBusIndex(WorldBus), _reverb);
            _rainFilter = new AudioEffectLowPassFilter { CutoffHz = OpenRainCutoff };
            AudioServer.AddBusEffect(AudioServer.GetBusIndex(WeatherBus), _rainFilter);
            foreach (string a in OS.GetCmdlineUserArgs())
            {
                if (a.StartsWith("--music="))
                {
                    _heldMusic = a["--music=".Length..];
                    _titleWanted = _heldMusic == "titel";
                }
            }
            string dir = ProjectSettings.GlobalizePath("res://assets/audio");
            foreach (string path in Directory.GetFiles(dir, "*.wav"))
            {
                _sounds[Path.GetFileNameWithoutExtension(path)] = AudioStreamWav.LoadFromFile(path);
            }
            for (int i = 0; i < PoolSize; i++)
            {
                var player = new AudioStreamPlayer2D { Bus = WorldBus };
                AddChild(player);
                _pool.Add(player);
            }
            _lockpick = new AudioStreamPlayer2D { Stream = Looping("lockpick"), MaxDistance = StepReach, VolumeDb = -8f, Bus = WorldBus };
            AddChild(_lockpick);
            _rain = AddBackground("rain", RainDb, WeatherBus);
            _wind = AddBackground("wind", WindDb, WeatherBus);
            _gust = AddBackground("wind_gust", SilentDb, WeatherBus);
            AddMusic();
            _heart = AddBackground("heartbeat", SilentDb, MusicBus);
            _title = new AudioStreamPlayer { Stream = Looping("music_title"), VolumeDb = SilentDb, Bus = MusicBus };
            AddChild(_title);
        }

        /// <summary>Titelthema für den Startbildschirm. Es blendet in etwa zwei Sekunden über und drückt dabei die Einsatzmusik weg.</summary>
        public void SetTitleMusic(bool on) => _titleWanted = on;

        /// <summary>Alle Lagen laufen in einem Strom. So bleiben sie auf die Abtastung genau im Takt.</summary>
        private void AddMusic()
        {
            _layers = new AudioStreamSynchronized { StreamCount = 4 };
            _layers.SetSyncStream(CalmLayer, Looping("music_calm"));
            _layers.SetSyncStream(DarkLayer, Looping("music_dark"));
            _layers.SetSyncStream(SuspectLayer, Looping("music_suspect"));
            _layers.SetSyncStream(FightLayer, Looping("music_tension"));
            SetLayer(CalmLayer, MusicDb, 1f);
            SetLayer(DarkLayer, DarkLayerDb, 0f);
            SetLayer(SuspectLayer, MusicDb, 0f);
            SetLayer(FightLayer, MusicDb, 0f);
            AddChild(new AudioStreamPlayer { Stream = _layers, Autoplay = true, Bus = MusicBus });
        }

        // Gleiche Leistung beim Überblenden: Der Sinus hält die Summe zweier Lagen ungefähr gleich laut.
        private void SetLayer(int layer, float db, float level)
        {
            _layers.SetSyncStreamVolume(layer, Faded(db, Mathf.Sin(level * Mathf.Pi / 2f) * (1f - _titleLevel)));
        }

        private static float Faded(float db, float gain) => Mathf.Max(SilentDb, db + Mathf.LinearToDb(Mathf.Max(gain, 0.001f)));

        private static void AddBus(string name, string sendTo)
        {
            int index = AudioServer.BusCount;
            AudioServer.AddBus(index);
            AudioServer.SetBusName(index, name);
            AudioServer.SetBusSend(index, sendTo);
        }

        /// <summary>Stellt die Regler aus dem Menü ein: Lautstärken von 0 bis 1, stumm schaltet alles ab.</summary>
        public void SetVolumes(double master, double music, double effects, bool muted)
        {
            SetBusVolume(0, master);
            SetBusVolume(AudioServer.GetBusIndex(MusicBus), music);
            SetBusVolume(AudioServer.GetBusIndex(EffectsBus), effects);
            AudioServer.SetBusMute(0, muted);
        }

        private static void SetBusVolume(int bus, double volume)
        {
            AudioServer.SetBusVolumeDb(bus, volume <= 0.0 ? SilentDb * 2f : Mathf.LinearToDb((float)volume));
        }

        private AudioStreamWav Looping(string name)
        {
            var sound = _sounds[name];
            sound.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            sound.LoopBegin = 0;
            sound.LoopEnd = sound.Data.Length / (sound.Stereo ? 4 : 2);
            return sound;
        }

        private AudioStreamPlayer AddBackground(string name, float volume, string bus)
        {
            var player = new AudioStreamPlayer { Stream = Looping(name), VolumeDb = volume, Autoplay = true, Bus = bus };
            AddChild(player);
            return player;
        }

        /// <summary>Ruhe, Dunkel, Verdacht und Gefecht überblenden je nach Lage. Alle laufen gleich lang und bleiben im Takt.</summary>
        private void UpdateMusic(World world, float dt)
        {
            bool alarmed = world.State == MissionState.Running && (world.TicksUntilReinforcement >= 0
                || world.Units.Exists(u => u.Alive && u.Faction == Faction.Guard && u.Alerted));
            // Schleicht der Agent im Dunkeln, wird die Musik dünner und der Regen rückt nach vorn.
            bool dark = !alarmed && Game.LeadLight() < DarkLight;
            bool suspicious = !alarmed && world.Units.Exists(u => u.Alive && u.Faction == Faction.Guard
                && u.GuardState is GuardState.Noticing or GuardState.Investigate or GuardState.Search or GuardState.Report);
            if (_heldMusic != null)
            {
                alarmed = _heldMusic == "gefecht";
                dark = _heldMusic == "dunkel";
                suspicious = _heldMusic == "verdacht";
            }
            UpdateTitle(dt);
            float fade = LayerFadePerSecond * dt;
            _calmLevel = Mathf.MoveToward(_calmLevel, alarmed || dark ? 0f : 1f, fade);
            _darkLevel = Mathf.MoveToward(_darkLevel, alarmed ? DarkUnderFight : dark ? 1f : 0f, fade);
            _fightLevel = Mathf.MoveToward(_fightLevel, alarmed ? 1f : 0f, fade);
            _suspectLevel = Mathf.MoveToward(_suspectLevel, suspicious ? 1f : 0f, suspicious ? SuspectRisePerSecond * dt : fade);
            SetLayer(CalmLayer, MusicDb, _calmLevel);
            SetLayer(DarkLayer, DarkLayerDb, _darkLevel);
            SetLayer(SuspectLayer, MusicDb, _suspectLevel);
            SetLayer(FightLayer, MusicDb, _fightLevel);
            // Herzschlag, solange eine Wache gerade etwas bemerkt und noch nicht schießt.
            bool noticed = world.Units.Exists(u => u.Alive && u.Faction == Faction.Guard && u.GuardState == GuardState.Noticing);
            _heartLevel = Mathf.MoveToward(_heartLevel, noticed ? 1f : 0f, (noticed ? HeartRisePerSecond : HeartFallPerSecond) * dt);
            _heart.VolumeDb = Faded(HeartDb, _heartLevel);
        }

        private void UpdateTitle(float dt)
        {
            if (_titleWanted && !_title.Playing)
            {
                _title.Play();
            }
            _titleLevel = Mathf.MoveToward(_titleLevel, _titleWanted ? 1f : 0f, TitleFadePerSecond * dt);
            _title.VolumeDb = Faded(TitleDb, Mathf.Sin(_titleLevel * Mathf.Pi / 2f));
            if (!_titleWanted && _titleLevel <= 0f && _title.Playing)
            {
                _title.Stop();
            }
        }

        /// <summary>Regen und Wind folgen dem Wetter. Der Schauer steht auf dem alten festen Pegel, lauter wird der Regen nie.</summary>
        private void UpdateWeather()
        {
            if (Game.Climate is not { } climate)
            {
                return;
            }
            _rain.VolumeDb = Mathf.Max(SilentDb, RainDb + RainCurveDb * Mathf.Log(Mathf.Max(climate.RainStrength, 0.01f)) / Mathf.Log(10f) - RainInAlleyDrop * Enclosure);
            float gust = Mathf.Clamp((climate.Wind - GustFromWind) / (GustFullWind - GustFromWind), 0f, 1f);
            _gust.VolumeDb = Faded(GustDb, gust);
            float flowDb = GutterDb + GutterCurveDb * Mathf.Log(Mathf.Max(climate.RoofFlow, 0.01f)) / Mathf.Log(10f);
            foreach (var gutter in _gutters)
            {
                gutter.VolumeDb = flowDb;
            }
        }

        /// <summary>Radio, Bildfunk und Klavier werden still, so weit die Fenster ringsum dunkel sind.</summary>
        private void UpdateHush(float dt)
        {
            float wanted = Game.Windows?.Hush ?? 0f;
            _hush = Mathf.MoveToward(_hush, wanted, HushFadePerSecond * dt);
            foreach (var (player, db) in _hushable)
            {
                player.VolumeDb = Faded(db, 1f - _hush);
            }
        }

        /// <summary>Der Raum wird aus den Hindernissen ringsum geschätzt: Kurze Wege bis zur nächsten Wand ergeben eine enge Gasse.</summary>
        private void UpdateRoom(World world, float dt)
        {
            if (_time >= _nextRoomProbe)
            {
                _nextRoomProbe = _time + RoomProbeSeconds;
                var at = Game.ScreenToMap(Game.CameraCenter);
                _roomWanted = MeasureEnclosure(world.Map, Mathf.FloorToInt(at.X), Mathf.FloorToInt(at.Y));
            }
            Enclosure = Mathf.MoveToward(Enclosure, _roomWanted, RoomFadePerSecond * dt);
            _reverb.Wet = Mathf.Lerp(OpenWet, AlleyWet, Enclosure);
            _reverb.RoomSize = Mathf.Lerp(OpenRoomSize, AlleyRoomSize, Enclosure);
            // Das Filter klingt gleichmäßig, wenn die Grenzfrequenz in Verhältnissen wandert.
            _rainFilter.CutoffHz = OpenRainCutoff * Mathf.Pow(AlleyRainCutoff / OpenRainCutoff, Enclosure);
        }

        private static float MeasureEnclosure(GameMap map, int x, int y)
        {
            float closed = 0f;
            foreach (var (dx, dy) in RoomRays)
            {
                for (int i = 1; i <= RoomProbeTiles; i++)
                {
                    int nx = x + dx * i, ny = y + dy * i;
                    if (!map.InBounds(nx, ny))
                    {
                        break;
                    }
                    if (map.IsBlocked(nx, ny))
                    {
                        closed += 1f - (i - 1f) / RoomProbeTiles;
                        break;
                    }
                }
            }
            return Mathf.Clamp(closed / RoomRays.Length, 0f, 1f);
        }

        /// <summary>Eine Regenrinne, deren Wasser dem Dachablauf folgt. Nahe beieinander reicht eine, und alle beginnen an anderer Stelle.</summary>
        public void AddGutter(Vector2 position)
        {
            if (_gutters.Count >= MaxGutters || _gutters.Exists(g => g.Position.DistanceTo(position) < GutterSpacing))
            {
                return;
            }
            var player = new AudioStreamPlayer2D { Stream = Looping("gutter"), Position = position, VolumeDb = SilentDb, MaxDistance = GutterReach, Attenuation = 1.6f, Bus = EffectsBus };
            AddChild(player);
            player.Play((float)_rng.NextDouble() * (float)player.Stream.GetLength());
            _gutters.Add(player);
        }

        /// <summary>Ein Dauergeräusch an einem festen Ort, etwa an einer Laterne. Es ist nur aus der Nähe zu hören.</summary>
        /// <summary>Mit hushable verstummt es, wenn die Nachbarschaft nach Schüssen die Fenster dunkel macht.</summary>
        public void AddLoop(string name, Vector2 position, float volume, float reach = AmbientReach, bool hushable = false)
        {
            var player = new AudioStreamPlayer2D
            {
                Stream = Looping(name), Position = position, VolumeDb = volume, MaxDistance = reach,
                Attenuation = 1.6f, Autoplay = true, Bus = EffectsBus,
            };
            AddChild(player);
            if (hushable)
            {
                _hushable.Add((player, volume));
            }
        }

        /// <summary>Das Brummen der Lampe in der Straßenbahn. Der Aufrufer stellt die Helligkeit, mit ihr wird es lauter oder leiser.</summary>
        public AudioStreamPlayer2D AddTramHum(Vector2 position)
        {
            var player = new AudioStreamPlayer2D
            {
                Stream = Looping("tram_hum"), Position = position, VolumeDb = TramHumDb, MaxDistance = TramHumReach,
                Attenuation = 1.6f, Autoplay = true, Bus = EffectsBus,
            };
            AddChild(player);
            return player;
        }

        public static float TramHumAt(float light) => Faded(TramHumDb, light);

        /// <summary>Ein Dauergeräusch, das mit etwas mitfliegt. Der Aufrufer startet es und setzt die Lage selbst.</summary>
        public AudioStreamPlayer2D AddMovingLoop(string name, float volume, float reach)
        {
            var player = new AudioStreamPlayer2D { Stream = Looping(name), VolumeDb = volume, MaxDistance = reach, Attenuation = 1.2f, Bus = EffectsBus };
            AddChild(player);
            return player;
        }

        /// <summary>Überall gleich laut, etwa Donner.</summary>
        public void PlayEverywhere(string name, float volume)
        {
            var player = new AudioStreamPlayer { Stream = _sounds[name], VolumeDb = volume, Autoplay = true, Bus = EffectsBus };
            player.Finished += player.QueueFree;
            AddChild(player);
            PlayCount++;
        }

        /// <summary>Spielt ein kurzes Geräusch an einer Stelle der Welt.</summary>
        public void Play(string name, Vector2 position, float volume = 0f, float reach = EventReach, float pitchSpread = 0.06f, float pitch = 1f)
        {
            if (_lastPlayed.TryGetValue(name, out float last) && _time - last < RepeatCooldown)
            {
                return;
            }
            _lastPlayed[name] = _time;
            var player = _pool.Find(p => !p.Playing) ?? _pool[0];
            player.Stream = _sounds[name];
            player.Position = position;
            player.VolumeDb = volume;
            player.MaxDistance = reach;
            player.Attenuation = 1.4f;
            player.PitchScale = pitch * (1f + (float)(_rng.NextDouble() * 2 - 1) * pitchSpread);
            player.Play();
            PlayCount++;
            string group = name.Split('_')[0] + (name.StartsWith("step_") ? "_" + name.Split('_')[1] : "");
            PlayedByGroup[group] = PlayedByGroup.GetValueOrDefault(group) + 1;
        }

        private void Reset(World world)
        {
            _world = world;
            _lastShot = 0;
            _itemTaken = _lootTaken = _bellRang = _ended = false;
            _stepCount.Clear();
            _fallen.Clear();
            _bribed.Clear();
            _dogBite.Clear();
            _inCombat.Clear();
            _reporting.Clear();
            _kicks.Clear();
            _doorsOpen = new bool[world.Doors.Count];
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;
            var world = Game.World;
            if (world != _world)
            {
                Reset(world);
            }
            foreach (var u in world.Units)
            {
                ListenToUnit(world, u);
            }
            ListenToShots(world);
            ListenToMission(world);
            UpdateMusic(world, (float)delta);
            UpdateRoom(world, (float)delta);
            UpdateWeather();
            UpdateHush((float)delta);
        }

        private void ListenToUnit(World world, Unit u)
        {
            var at = Game.ToScreen(u.X, u.Y);
            if (!u.Alive)
            {
                if (_fallen.Add(u.Id))
                {
                    Play("fall", at, -1f);
                }
                return;
            }
            if (u.Moving)
            {
                int count = Game.StepsTaken(u);
                if (_stepCount.TryGetValue(u.Id, out int before) && count != before)
                {
                    if (u.Type.Bites)
                    {
                        _stepCount[u.Id] = count;
                        // Die Kette an der Leine verrät das Paar, bevor man es sieht.
                        if (u.LeashedTo >= 0 && count % 4 == 0)
                        {
                            PlayStep("chain", at, -17f, GuardStepReach);
                        }
                        return;
                    }
                    string kind = u.Sprinting ? "step_run" : u.Faction == Faction.Agent ? "step_soft" : "step_boot";
                    float volume = u.Sprinting ? -13f : u.Faction == Faction.Agent ? -19f : -22f;
                    // Wachen hört man erst aus der Nähe, sonst klappern alle Rundgänge der Karte zugleich.
                    float reach = u.Faction == Faction.Agent ? AgentStepReach : GuardStepReach;
                    PlayStep($"{kind}_{_rng.Next(4)}", at, volume, reach);
                }
                _stepCount[u.Id] = count;
            }
            if (u.Bribed && _bribed.Add(u.Id))
            {
                Play("coins", at, -4f, StepReach);
            }
            if (u.Faction == Faction.Guard && u.GuardState == GuardState.Combat && _inCombat.Add(u.Id) && _time > _nextWhistle)
            {
                Play("whistle", at, -3f);
                _nextWhistle = _time + WhistleCooldown;
            }
            if (u.Type.Bites)
            {
                ListenToDog(u, at);
            }
            if (u.GuardState == GuardState.Report)
            {
                if (_reporting.Add(u.Id))
                {
                    Play("boy_whistle", at, -5f);
                }
            }
            else if (_reporting.Remove(u.Id) && !u.Bribed)
            {
                // Die Meldung ist angekommen: Jetzt pfeift die Wache.
                Play("whistle", at, -3f);
            }
            _kicks.TryGetValue(u.Id, out int lastKick);
            if (u.LastKickTick > lastKick)
            {
                _kicks[u.Id] = u.LastKickTick;
                Play("kick", at, -5f, StepReach);
            }
        }

        private void ListenToDog(Unit dog, Vector2 at)
        {
            bool alarmed = dog.GuardState == GuardState.Noticing || dog.GuardState == GuardState.Combat;
            if (alarmed && _time > _nextBark)
            {
                Play("dog", at, -6f, 800f, 0.1f);
                _nextBark = _time + BarkSeconds;
            }
            _dogBite.TryGetValue(dog.Id, out int last);
            if (dog.LastFiredTick > last)
            {
                _dogBite[dog.Id] = dog.LastFiredTick;
                Play(dog.LastBiteHit ? "dog_bite" : "dog_snap", at, -4f, StepReach);
            }
        }

        // Schritte teilen sich keine Sperre, sonst verschluckt ein Trupp seine eigenen Schritte.
        private void PlayStep(string name, Vector2 at, float volume, float reach)
        {
            _lastPlayed.Remove(name);
            Play(name, at, volume, reach, 0.1f);
        }

        private void ListenToShots(World world)
        {
            foreach (var shot in world.Shots)
            {
                if (shot.Tick <= _lastShot)
                {
                    continue;
                }
                var from = Game.ToScreen(shot.FromX, shot.FromY);
                // Gewehr und Schrotflinte klingen tiefer als die Pistole, das Gewehr rollt länger.
                var type = shot.ShooterId >= 0 && shot.ShooterId < world.Units.Count ? world.Units[shot.ShooterId].Type : GuardType.Wachmann;
                float pitch = type == GuardType.Schuetze ? 0.72f : type == GuardType.Werkschutz ? 0.62f : 1f;
                Play($"shot_{(shot.FromX + shot.Tick) % 3}", from, type == GuardType.Wachmann ? 0f : 2f, EventReach, 0.05f, pitch);
                if (from.DistanceTo(Game.CameraCenter) < ShakeReach)
                {
                    Game.ShakeCamera(ShotShake);
                }
                var impact = Game.ToScreen(shot.ToX, shot.ToY);
                if (shot.Hit)
                {
                    Play("hit", impact, -3f, StepReach);
                }
                else if (_rng.NextDouble() < 0.35)
                {
                    Play("ricochet", impact, -6f);
                }
            }
            if (world.Shots.Count > 0)
            {
                _lastShot = Math.Max(_lastShot, world.Shots[^1].Tick);
            }
        }

        private void ListenToMission(World world)
        {
            if (world.TicksUntilReinforcement >= 0 && !_bellRang)
            {
                _bellRang = true;
                Play("alarm_bell", Game.ToScreen(world.Exit.X0, world.Exit.Y0), 0f, 3000f);
            }
            var book = Game.ToScreen(world.ItemX, world.ItemY);
            if (world.ItemProgress > 0 && _time > _nextPages)
            {
                Play("pages", book, -3f, StepReach);
                _nextPages = _time + 0.8f;
            }
            if (world.ItemTaken != _itemTaken)
            {
                _itemTaken = world.ItemTaken;
                if (_itemTaken)
                {
                    Play("click", book, -3f, StepReach);
                }
            }
            if (world.LootTaken && !_lootTaken)
            {
                _lootTaken = true;
                Play("coins", Game.ToScreen(world.LootX, world.LootY), -2f);
            }
            bool picking = false;
            for (int i = 0; i < world.Doors.Count; i++)
            {
                var door = world.Doors[i];
                var at = Game.DoorMark(door);
                picking |= door.Progress > 0 && !door.Open;
                if (door.Progress > 0 && !door.Open)
                {
                    _lockpick.Position = at;
                }
                if (door.Open && !_doorsOpen[i])
                {
                    _doorsOpen[i] = true;
                    Play("door_open", at, -2f);
                }
            }
            if (picking != _lockpick.Playing)
            {
                if (picking)
                {
                    _lockpick.Play();
                }
                else
                {
                    _lockpick.Stop();
                }
            }
            if (world.State != MissionState.Running && !_ended)
            {
                _ended = true;
                _lockpick.Stop();
                Play(world.State == MissionState.Won ? "win" : "lose", Game.CameraCenter, -2f, 100000f, 0f);
            }
        }
    }
}
