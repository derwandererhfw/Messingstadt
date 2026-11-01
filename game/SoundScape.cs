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
        private const float EventReach = 1500f;
        private const float AmbientReach = 300f;
        private const float WhistleCooldown = 9f;
        private const float RepeatCooldown = 0.07f;
        private const float MusicDb = -19f;
        private const float SilentDb = -60f;
        private const float TensionFadePerSecond = 0.35f;

        private readonly Dictionary<string, AudioStreamWav> _sounds = new();
        private readonly Dictionary<string, float> _lastPlayed = new();
        private readonly List<AudioStreamPlayer2D> _pool = new();
        private readonly Random _rng = new();
        private readonly Dictionary<int, int> _stepCount = new();
        private readonly HashSet<int> _fallen = new();
        private readonly HashSet<int> _bribed = new();
        private readonly HashSet<int> _inCombat = new();
        private AudioStreamPlayer2D _lockpick = null!;
        private AudioStreamPlayer _music = null!;
        private AudioStreamPlayer _tension = null!;
        private float _tensionLevel;
        private World? _world;
        private int _lastShot;
        private bool _itemTaken, _lootTaken, _bellRang, _ended;
        private bool[] _doorsOpen = Array.Empty<bool>();
        private float _time;
        private float _nextWhistle;
        private float _nextPages;
        private bool _muted;

        public Main Game = null!;

        /// <summary>Wie oft insgesamt etwas abgespielt wurde. Für den Prüflauf.</summary>
        public int PlayCount { get; private set; }

        public override void _Ready()
        {
            string dir = ProjectSettings.GlobalizePath("res://assets/audio");
            foreach (string path in Directory.GetFiles(dir, "*.wav"))
            {
                _sounds[Path.GetFileNameWithoutExtension(path)] = AudioStreamWav.LoadFromFile(path);
            }
            for (int i = 0; i < PoolSize; i++)
            {
                var player = new AudioStreamPlayer2D();
                AddChild(player);
                _pool.Add(player);
            }
            _lockpick = new AudioStreamPlayer2D { Stream = Looping("lockpick"), MaxDistance = StepReach, VolumeDb = -8f };
            AddChild(_lockpick);
            AddBackground("rain", -27f);
            AddBackground("wind", -30f);
            _music = AddBackground("music_calm", MusicDb);
            _tension = AddBackground("music_tension", SilentDb);
        }

        private AudioStreamWav Looping(string name)
        {
            var sound = _sounds[name];
            sound.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            sound.LoopBegin = 0;
            sound.LoopEnd = sound.Data.Length / 2;
            return sound;
        }

        private AudioStreamPlayer AddBackground(string name, float volume)
        {
            var player = new AudioStreamPlayer { Stream = Looping(name), VolumeDb = volume, Autoplay = true };
            AddChild(player);
            return player;
        }

        /// <summary>Die treibende Bassfigur blendet sich ein, solange Wachen alarmiert sind. Beide Spuren laufen gleich lang und bleiben im Takt.</summary>
        private void UpdateMusic(World world, float dt)
        {
            bool alarmed = world.State == MissionState.Running && (world.TicksUntilReinforcement >= 0
                || world.Units.Exists(u => u.Alive && u.Faction == Faction.Guard && u.Alerted));
            _tensionLevel = Mathf.MoveToward(_tensionLevel, alarmed ? 1f : 0f, TensionFadePerSecond * dt);
            _tension.VolumeDb = Mathf.Max(SilentDb, MusicDb + Mathf.LinearToDb(Mathf.Max(_tensionLevel, 0.001f)));
        }

        /// <summary>Ein Dauergeräusch an einem festen Ort, etwa an einer Laterne. Es ist nur aus der Nähe zu hören.</summary>
        public void AddLoop(string name, Vector2 position, float volume, float reach = AmbientReach)
        {
            var player = new AudioStreamPlayer2D
            {
                Stream = Looping(name), Position = position, VolumeDb = volume, MaxDistance = reach,
                Attenuation = 1.6f, Autoplay = true,
            };
            AddChild(player);
        }

        /// <summary>Ein Dauergeräusch, das mit etwas mitfliegt. Der Aufrufer startet es und setzt die Lage selbst.</summary>
        public AudioStreamPlayer2D AddMovingLoop(string name, float volume, float reach)
        {
            var player = new AudioStreamPlayer2D { Stream = Looping(name), VolumeDb = volume, MaxDistance = reach, Attenuation = 1.2f };
            AddChild(player);
            return player;
        }

        /// <summary>Überall gleich laut, etwa Donner.</summary>
        public void PlayEverywhere(string name, float volume)
        {
            var player = new AudioStreamPlayer { Stream = _sounds[name], VolumeDb = volume, Autoplay = true };
            player.Finished += player.QueueFree;
            AddChild(player);
            PlayCount++;
        }

        /// <summary>Spielt ein kurzes Geräusch an einer Stelle der Welt.</summary>
        public void Play(string name, Vector2 position, float volume = 0f, float reach = EventReach, float pitchSpread = 0.06f)
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
            player.PitchScale = 1f + (float)(_rng.NextDouble() * 2 - 1) * pitchSpread;
            player.Play();
            PlayCount++;
        }

        public void ToggleMute()
        {
            _muted = !_muted;
            AudioServer.SetBusMute(0, _muted);
        }

        private void Reset(World world)
        {
            _world = world;
            _lastShot = 0;
            _itemTaken = _lootTaken = _bellRang = _ended = false;
            _stepCount.Clear();
            _fallen.Clear();
            _bribed.Clear();
            _inCombat.Clear();
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
                    string kind = u.Sprinting ? "step_run" : u.Faction == Faction.Agent ? "step_soft" : "step_boot";
                    float volume = u.Sprinting ? -5f : u.Faction == Faction.Agent ? -13f : -9f;
                    PlayStep($"{kind}_{_rng.Next(4)}", at, volume);
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
        }

        // Schritte teilen sich keine Sperre, sonst verschluckt ein Trupp seine eigenen Schritte.
        private void PlayStep(string name, Vector2 at, float volume)
        {
            _lastPlayed.Remove(name);
            Play(name, at, volume, StepReach, 0.1f);
        }

        private void ListenToShots(World world)
        {
            foreach (var shot in world.Shots)
            {
                if (shot.Tick <= _lastShot)
                {
                    continue;
                }
                Play($"shot_{(shot.FromX + shot.Tick) % 3}", Game.ToScreen(shot.FromX, shot.FromY), 0f);
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
