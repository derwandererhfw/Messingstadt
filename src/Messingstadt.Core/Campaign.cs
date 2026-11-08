using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Messingstadt.Core
{
    /// <summary>Ein Agent über mehrere Einsätze. Der Platz ist der Name des Agenten in der Mission, ein Rekrut übernimmt ihn samt Fähigkeit.</summary>
    public sealed class AgentRecord
    {
        public string Name { get; set; } = "";
        public string Slot { get; set; } = "";
        public int Legs { get; set; } = 1;
        public int Heart { get; set; } = 1;

        /// <summary>Wie oft der Agent gefallen ist. Beim zweiten Mal stirbt er.</summary>
        public int Falls { get; set; }
        public bool Dead { get; set; }

        /// <summary>Wie viele Einsätze der Agent noch aussetzt.</summary>
        public int Rest { get; set; }

        /// <summary>Sitzt die Pause in der Krankenstube ab, nicht nur angeschlagen.</summary>
        public bool Infirmary { get; set; }

        /// <summary>Fähigkeit des Platzes, siehe World.SkillLocksmith und World.SkillBookkeeper.</summary>
        public string Skill { get; set; } = "";

        /// <summary>Beruf eines Neuen für die Akte. Leer bei den Agenten der Mission.</summary>
        public string Job { get; set; } = "";

        public bool Ready => !Dead && Rest == 0;
    }

    /// <summary>Ein Neuer aus dem Vorrat der Gewerkschaft. Texte von Story, docs/story/texte/dauertod.md.</summary>
    public sealed record Newcomer(string Name, string Job, string Skill, string Origin);

    /// <summary>Freigegebene Texte zum Dauertod. Kolb nennt in Telegrammen keine Namen.</summary>
    public static class CampaignTexts
    {
        public const string Doctor = "Ilse Marquardt";
        public static readonly string[] DoctorLines =
        {
            "Einmal flick ich jeden zusammen. Ein zweites Mal hält das keiner aus.",
            "Die Narbe bleibt. Und beim nächsten Mal bleibst du.",
            "Geh langsam die ersten Wochen. Ich mein das ernst.",
        };
        public const string RuleHint = "Wer fällt, kommt einmal aus der Krankenstube zurück. Wer zweimal fällt, bleibt liegen.";
        public const string TelegramOneDead = "AN ABTEILUNG K STOP EINER VON EUCH IST NICHT MEHR AUFGEWACHT STOP BEGRABEN OHNE NAMEN STOP MACHT WEITER STOP K";
        public const string TelegramManyDead = "AN ABTEILUNG K STOP ZU VIELE GRAEBER FUER EINE WOCHE STOP MACHT WEITER STOP K";
        public const string TelegramNewcomers = "AN ABTEILUNG K STOP NEUE LEUTE UNTERWEGS STOP SIE WISSEN WOFUER STOP BRINGT SIE ZURUECK STOP K";

        public static string FirstFall(string name) => $"{name} liegt in der Krankenstube. Beine und Herz fallen auf Stufe 1. Ein zweites Mal geht das nicht gut.";

        public static string Died(string name) => $"{name} ist tot.";
    }

    /// <summary>Was die Story später über einen Einsatz wissen will.</summary>
    public sealed class MissionRecord
    {
        public int Attempts { get; set; }
        public bool Won { get; set; }
        public MissionRating Rating { get; set; }
        public int DeadGuards { get; set; }
        public int Bribed { get; set; }
        public int DeadAgents { get; set; }
        public bool Loot { get; set; }
    }

    public enum SaveStatus
    {
        New,
        Loaded,
        Migrated,
        Discarded,
    }

    /// <summary>Was zwischen den Einsätzen bleibt: Geld, Fahndung, Rückhalt, die Agenten und was in jedem Einsatz geschah.</summary>
    public sealed class Campaign
    {
        public const int CurrentVersion = 2;
        public const int MaxLevel = 3;
        public const int MaxHeat = 4;
        public const int MaxSupport = 5;
        public const int DuesPerSupport = 50;
        public const int DoctorCost = 100;
        public const int RecruitCost = 250;
        public const int MinSquad = 2;
        public const int BloodyNight = 3;
        private const int BaseReward = 300;
        private const int QuietBonus = 150;
        private const int UnseenBonus = 300;
        private const int UpgradeCostPerLevel = 150;

        /// <summary>Neue in fester Reihenfolge. Ein leerer Platz bekommt den ersten freien Neuen mit seiner Fähigkeit.</summary>
        public static readonly Newcomer[] Newcomers =
        {
            new("Karl Hoffmann", "Schlosser", World.SkillLocksmith, "Hat zwanzig Jahre Kessel repariert, bis Brandt ihn aussperrte"),
            new("Frieda Kranz", "Buchhalterin", World.SkillBookkeeper, "Führte die Kasse einer Konsumgenossenschaft"),
            new("Willi Saß", "Schlosser", World.SkillLocksmith, "Lehrling"),
            new("Erwin Pohl", "Buchhalter", World.SkillBookkeeper, "Lohnschreiber im Kontor der Brandt-Werke, entlassen wegen Streikgeld"),
            new("Lotte Becker", "Kurierin", "", "Zeitungsjunge gewesen, nur dass sie ein Mädchen ist"),
            new("Max Kühn", "Truppführer", "", "Früher Gewerkschaftsordner bei Versammlungen"),
        };

        public int Version { get; set; } = CurrentVersion;
        public int Money { get; set; } = 300;
        public int Missions { get; set; }

        /// <summary>0 bis 4. Steigt mit Schüssen und Toten. Je angefangene zwei Stufen rückt eine Wache mehr zur Verstärkung an.</summary>
        public int Heat { get; set; }

        /// <summary>0 bis 5. Steigt mit Siegen, fällt mit Niederlagen. Jede Stufe bringt nach jedem Einsatz Beitrag.</summary>
        public int Support { get; set; }

        /// <summary>Welche Mission gerade läuft, als Schlüssel für Records.</summary>
        public string Current { get; set; } = "";
        public List<AgentRecord> Agents { get; set; } = new();
        public Dictionary<string, MissionRecord> Records { get; set; } = new();

        /// <summary>Der allgemeine Hinweis zum Dauertod erscheint einmal je Kampagne. Die Anzeige setzt das, wenn sie ihn gezeigt hat.</summary>
        public bool DeathRuleExplained { get; set; }

        /// <summary>Was der letzte zählende Einsatz mit dem Trupp gemacht hat, für Ergebnisfenster und Telegramme. Prepare leert die Listen.</summary>
        public List<string> LastFirstFalls { get; set; } = new();
        public List<string> LastDeaths { get; set; } = new();
        public List<string> LastNewcomers { get; set; } = new();

        public static int Reward(MissionRating rating) => BaseReward + rating switch
        {
            MissionRating.Unseen => UnseenBonus,
            MissionRating.Quiet => QuietBonus,
            _ => 0,
        };

        /// <summary>Kosten, um von dieser Stufe auf die nächste zu kommen.</summary>
        public static int UpgradeCost(int level) => level * UpgradeCostPerLevel;

        public int Dues => Support * DuesPerSupport;

        /// <summary>Gemessen mit --kampagne im Balance-Werkzeug: Eine Wache mehr je Stufe trieb laute Spielweisen in eine Abwärtsspirale.</summary>
        public static int ExtraReinforcements(int heat) => (heat + 1) / 2;

        public AgentRecord Agent(string name) => Agents.Find(a => a.Name == name)
            ?? throw new ArgumentException($"Unbekannter Agent {name}");

        /// <summary>Die Plätze in fester Reihenfolge, je Platz der lebende Agent oder der zuletzt gestorbene.</summary>
        public List<AgentRecord> Roster()
        {
            var roster = new List<AgentRecord>();
            foreach (string slot in Slots())
            {
                roster.Add(Agents.LastOrDefault(a => a.Slot == slot && !a.Dead) ?? Agents.Last(a => a.Slot == slot));
            }
            return roster;
        }

        private List<string> Slots() => Agents.Select(a => a.Slot).Distinct().ToList();

        private bool SlotEmpty(string slot) => Agents.All(a => a.Slot != slot || a.Dead);

        /// <summary>Überträgt den Stand auf frisch geladene Missionsdaten: Wer nicht einsatzbereit ist, bleibt zu Hause, die Fahndung verstärkt die Wachen.</summary>
        public void Prepare(MissionData mission, string missionId = "")
        {
            Current = missionId;
            mission.Money = Money;
            foreach (var a in mission.Agents)
            {
                if (!Agents.Exists(r => r.Slot == a.Name))
                {
                    Agents.Add(new AgentRecord { Name = a.Name, Slot = a.Name, Legs = a.Legs, Heart = a.Heart });
                }
                foreach (var record in Agents.Where(x => x.Slot == a.Name && x.Skill == ""))
                {
                    record.Skill = a.Skill;
                }
            }
            LastFirstFalls.Clear();
            LastDeaths.Clear();
            LastNewcomers.Clear();
            FillRanks(mission.Agents.Select(a => a.Name).ToList());
            var squad = new List<UnitData>();
            foreach (var a in mission.Agents)
            {
                var record = Agents.LastOrDefault(r => r.Slot == a.Name && r.Ready);
                if (record != null)
                {
                    a.Name = record.Name;
                    a.Legs = record.Legs;
                    a.Heart = record.Heart;
                    squad.Add(a);
                }
            }
            mission.Agents = squad;
            if (mission.Reinforcements is { } r && r.Count > 0)
            {
                r.Count += ExtraReinforcements(Heat);
            }
        }

        /// <summary>Sorgt dafür, dass mindestens zwei Agenten einsatzbereit sind: erst kostenlose Rekruten auf leeren Plätzen, dann kommen Ruhende trotzdem mit.</summary>
        private void FillRanks(List<string> slots)
        {
            int need = Math.Min(MinSquad, slots.Count);
            int ready() => slots.Count(s => Agents.Exists(a => a.Slot == s && a.Ready));
            foreach (string slot in slots.Where(SlotEmpty).ToList())
            {
                if (ready() >= need)
                {
                    break;
                }
                Recruit(slot);
            }
            foreach (var resting in Agents.Where(a => !a.Dead && a.Rest > 0 && slots.Contains(a.Slot)).OrderBy(a => a.Rest).ToList())
            {
                if (ready() >= need)
                {
                    break;
                }
                resting.Rest = 0;
                resting.Infirmary = false;
            }
        }

        private AgentRecord Recruit(string slot)
        {
            string skill = Agents.First(a => a.Slot == slot).Skill;
            var next = Newcomers.FirstOrDefault(n => n.Skill == skill && !Agents.Exists(a => a.Name == n.Name));
            var record = next != null
                ? new AgentRecord { Name = next.Name, Slot = slot, Skill = skill, Job = next.Job }
                : new AgentRecord { Name = $"Rekrut {Agents.Count + 1}", Slot = slot, Skill = skill, Job = "Rekrut" };
            Agents.Add(record);
            LastNewcomers.Add(record.Name);
            return record;
        }

        /// <summary>Wirbt für einen leeren Platz einen Rekruten auf Stufe 1 an.</summary>
        public bool TryRecruit(string slot)
        {
            if (!Agents.Exists(a => a.Slot == slot) || !SlotEmpty(slot) || Money < RecruitCost)
            {
                return false;
            }
            Money -= RecruitCost;
            Recruit(slot);
            return true;
        }

        /// <summary>Der Arzt macht einen Agenten sofort wieder einsatzbereit.</summary>
        public bool TryDoctor(string name)
        {
            var record = Agent(name);
            if (record.Dead || record.Rest == 0 || Money < DoctorCost)
            {
                return false;
            }
            Money -= DoctorCost;
            record.Rest = 0;
            record.Infirmary = false;
            return true;
        }

        /// <summary>Verbucht einen beendeten Einsatz. Beute zählt nur, wenn der Trupp entkommt.</summary>
        public void Finish(World world)
        {
            bool won = world.State == MissionState.Won;
            Money = won ? world.Money + Reward(world.Rating) : world.Money - (world.LootTaken ? world.LootValue : 0);
            if (won)
            {
                Missions++;
            }
            Settle(world, won, false);
        }

        /// <summary>Kostet der Abbruch den Trupp? Vor dem ersten Schuss kann niemand gefallen sein, der Rückzug ist frei.</summary>
        public static bool AbandonCostsSquad(World world) => world.Rating == MissionRating.Loud;

        /// <summary>Verbucht einen abgebrochenen Einsatz. Nach dem ersten Schuss gilt der ganze Trupp als gefallen, sonst ließe sich jeder Sturz durch rechtzeitiges Abbrechen vermeiden.</summary>
        public void Abandon(World world)
        {
            Money = world.Money - (world.LootTaken ? world.LootValue : 0);
            if (AbandonCostsSquad(world))
            {
                Settle(world, false, true);
            }
        }

        /// <summary>Folgen eines Einsatzes, der zählt: Agenten, Fahndung, Rückhalt, Beitrag und der Eintrag für die Story.</summary>
        private void Settle(World world, bool won, bool abandoned)
        {
            var squad = world.Units.Where(u => u.Faction == Faction.Agent).ToList();
            LastFirstFalls.Clear();
            LastDeaths.Clear();
            LastNewcomers.Clear();
            foreach (var resting in Agents.Where(a => !a.Dead && a.Rest > 0 && !squad.Exists(u => u.Name == a.Name)))
            {
                resting.Rest--;
                resting.Infirmary &= resting.Rest > 0;
            }
            foreach (var u in squad)
            {
                var record = Agents.Find(a => a.Name == u.Name);
                if (record == null)
                {
                    continue;
                }
                record.Rest = 0;
                record.Infirmary = false;
                if (!u.Alive || abandoned)
                {
                    record.Falls++;
                    record.Legs = 1;
                    record.Heart = 1;
                    record.Dead = record.Falls >= 2;
                    record.Rest = record.Dead ? 0 : 1;
                    record.Infirmary = !record.Dead;
                    (record.Dead ? LastDeaths : LastFirstFalls).Add(record.Name);
                }
                else if (u.Hp * 2 < u.MaxHp)
                {
                    record.Rest = 1;
                }
            }

            int deadGuards = world.Units.Count(u => u.Faction == Faction.Guard && !u.Alive);
            int bribed = world.Units.Count(u => u.Faction == Faction.Guard && u.Bribed);
            var rating = world.Rating;
            int heat = rating switch
            {
                MissionRating.Unseen => -1,
                MissionRating.Quiet => bribed > 0 ? -1 : 0,
                _ => 1,
            };
            if (deadGuards >= BloodyNight)
            {
                heat++;
            }
            Heat = Math.Clamp(Heat + heat, 0, MaxHeat);
            Support = Math.Clamp(Support + (won ? 1 : -1), 0, MaxSupport);
            Money += Dues;

            if (!Records.TryGetValue(Current, out var entry))
            {
                entry = new MissionRecord();
                Records[Current] = entry;
            }
            entry.Attempts++;
            if (won)
            {
                entry.Won = true;
                entry.Rating = rating;
                entry.DeadGuards = deadGuards;
                entry.Bribed = bribed;
                entry.DeadAgents = squad.Count(u => !u.Alive);
                entry.Loot = world.LootTaken;
            }
            FillRanks(Slots());
        }

        /// <summary>
        /// Stempel für einen Agenten am Boden: true heißt GEFALLEN, false KRANKENSTUBE. Gilt im Einsatz, wo der Sturz noch nicht verbucht ist,
        /// und danach, wo ein erster Sturz Falls schon auf 1 gesetzt hat.
        /// </summary>
        public bool FallIsFatal(string name)
        {
            var record = Agents.Find(a => a.Name == name);
            return record != null && (record.Dead || (record.Falls >= 1 && !LastFirstFalls.Contains(name)));
        }

        /// <summary>Herkunft eines Neuen in einem Satz, leer bei den Agenten der Mission.</summary>
        public string Origin(AgentRecord record)
        {
            var newcomer = Newcomers.FirstOrDefault(n => n.Name == record.Name);
            if (newcomer == null)
            {
                return "";
            }
            bool ottoAlive = Agents.Exists(a => a.Name == "Otto" && !a.Dead);
            return newcomer.Name == "Willi Saß" && ottoAlive ? "Lehrling bei Otto" : newcomer.Origin;
        }

        /// <summary>Telegramme von Kolb nach dem letzten Einsatz: erst zu den Toten, dann zu den Neuen.</summary>
        public List<string> Telegrams()
        {
            var telegrams = new List<string>();
            if (LastDeaths.Count == 1)
            {
                telegrams.Add(CampaignTexts.TelegramOneDead);
            }
            else if (LastDeaths.Count > 1)
            {
                telegrams.Add(CampaignTexts.TelegramManyDead);
            }
            if (LastNewcomers.Count > 0)
            {
                telegrams.Add(CampaignTexts.TelegramNewcomers);
            }
            return telegrams;
        }

        public bool TryUpgrade(string name, bool legs)
        {
            var record = Agent(name);
            int level = legs ? record.Legs : record.Heart;
            if (record.Dead || level >= MaxLevel || Money < UpgradeCost(level))
            {
                return false;
            }
            Money -= UpgradeCost(level);
            if (legs)
            {
                record.Legs++;
            }
            else
            {
                record.Heart++;
            }
            return true;
        }

        public string ToJson() => JsonSerializer.Serialize(this);

        /// <summary>Lädt einen Spielstand. Ein beschädigter Stand startet eine neue Kampagne.</summary>
        public static Campaign FromJson(string json) => Load(json, out _);

        /// <summary>Lädt einen Spielstand. Stände ohne Versionsnummer werden übernommen, beschädigte oder neuere verworfen.</summary>
        public static Campaign Load(string? json, out SaveStatus status)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                status = SaveStatus.New;
                return new Campaign();
            }
            Campaign? loaded;
            int version;
            try
            {
                using var doc = JsonDocument.Parse(json);
                version = doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(nameof(Version), out var v) && v.TryGetInt32(out int n) ? n : 1;
                loaded = JsonSerializer.Deserialize<Campaign>(json);
            }
            catch (JsonException)
            {
                loaded = null;
                version = 0;
            }
            catch (InvalidOperationException)
            {
                loaded = null;
                version = 0;
            }
            if (loaded == null || version < 1 || version > CurrentVersion)
            {
                status = SaveStatus.Discarded;
                return new Campaign();
            }
            status = version < CurrentVersion ? SaveStatus.Migrated : SaveStatus.Loaded;
            loaded.Repair();
            return loaded;
        }

        /// <summary>Bringt einen geladenen Stand in gültige Grenzen. Alte Stände kennen keinen Platz, dort ist er der Name.</summary>
        private void Repair()
        {
            Version = CurrentVersion;
            Money = Math.Max(0, Money);
            Missions = Math.Max(0, Missions);
            Heat = Math.Clamp(Heat, 0, MaxHeat);
            Support = Math.Clamp(Support, 0, MaxSupport);
            Current ??= "";
            Records ??= new();
            LastFirstFalls ??= new();
            LastDeaths ??= new();
            LastNewcomers ??= new();
            Agents = (Agents ?? new()).Where(a => a != null && !string.IsNullOrEmpty(a.Name)).GroupBy(a => a.Name).Select(g => g.First()).ToList();
            foreach (var a in Agents)
            {
                a.Slot = string.IsNullOrEmpty(a.Slot) ? a.Name : a.Slot;
                a.Legs = Math.Clamp(a.Legs, 1, MaxLevel);
                a.Heart = Math.Clamp(a.Heart, 1, MaxLevel);
                a.Falls = Math.Clamp(a.Falls, 0, 2);
                a.Dead = a.Dead || a.Falls >= 2;
                a.Rest = a.Dead ? 0 : Math.Clamp(a.Rest, 0, 9);
                a.Infirmary &= a.Rest > 0;
            }
        }
    }
}
