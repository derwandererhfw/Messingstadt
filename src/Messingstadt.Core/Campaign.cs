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

        /// <summary>Beruf für das Totenbuch, bei allen gesetzt: der aus der Mission oder der des Neuen.</summary>
        public string Role { get; set; } = "";

        /// <summary>Was der Agent trägt, nach Kennung aus data/waffen.json, höchstens Campaign.ItemSlots Stück.</summary>
        public List<string> Items { get; set; } = new(ItemCatalog.StarterKit);

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

        /// <summary>Zeile für das Ergebnis: die Toten des Einsatzes mit Namen, erst der Trupp, dann die Wache. Leer ohne Tote.</summary>
        public static string DeadLine(IReadOnlyList<LedgerEntry> dead, int most = 4)
        {
            string Names(LedgerSide side)
            {
                var names = dead.Where(e => e.Side == side).Select(e => e.Name).ToList();
                return names.Count <= most ? string.Join(", ", names) : $"{string.Join(", ", names.Take(most))} und {names.Count - most} weitere";
            }
            string squad = Names(LedgerSide.Squad), guards = Names(LedgerSide.Guard);
            return string.Join("   ", new[] { squad.Length > 0 ? $"Tote im Trupp: {squad}." : "", guards.Length > 0 ? $"Tote der Wache: {guards}." : "" }.Where(t => t.Length > 0));
        }

        /// <summary>Zeile für das Ergebnis eines Einsatzes der Art Befreien: wer herauskam und wer nicht. Leer ohne Gefangene.</summary>
        public static string Rescue(IReadOnlyDictionary<string, bool> prisoners)
        {
            var freed = prisoners.Where(p => p.Value).Select(p => p.Key).ToList();
            var dead = prisoners.Where(p => !p.Value).Select(p => p.Key).ToList();
            string line = freed.Count > 0 ? $"Befreit: {string.Join(", ", freed)}." : "";
            return dead.Count == 0 ? line : $"{line} Nicht überlebt: {string.Join(", ", dead)}.".Trim();
        }
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

        /// <summary>Befreien: je Gefangenem mit vollem Namen, ob er lebend herauskam. Leer bei anderen Auftragsarten und vor dem ersten Sieg.</summary>
        public Dictionary<string, bool> Prisoners { get; set; } = new();
    }

    public enum SaveStatus
    {
        New,
        Loaded,
        Migrated,
        Discarded,
    }

    /// <summary>Was zwischen den Einsätzen bleibt: Geld, Fahndung, Rückhalt, die Agenten und was in jedem Einsatz geschah.</summary>
    public sealed partial class Campaign
    {
        public const int CurrentVersion = 6;
        public const int StartMoney = 600;

        /// <summary>Das Viertel des ersten Einsatzes. Alte Stände mit einem Sieg bekommen es als erobert.</summary>
        public const string FirstDistrict = "viertel_1";

        /// <summary>Plätze für Ausrüstung je Agent.</summary>
        public const int ItemSlots = 4;

        /// <summary>Anteil des Preises, den ein Verkauf zurückbringt.</summary>
        public const int SellPercent = 50;
        public const int MaxLevel = 3;
        public const int MaxHeat = 4;
        public const int MaxSupport = 5;
        public const int DuesPerSupport = 50;
        public const int DoctorCost = 100;
        public const int RecruitCost = 250;
        public const int MinSquad = 2;
        public const int BloodyNight = 3;
        public const int BaseReward = 300;
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
        public int Money { get; set; } = StartMoney;
        public int Missions { get; set; }

        /// <summary>0 bis 4. Steigt mit Schüssen und Toten. Je angefangene zwei Stufen rückt eine Wache mehr zur Verstärkung an.</summary>
        public int Heat { get; set; }

        /// <summary>0 bis 5. Steigt mit Siegen, fällt mit Niederlagen. Jede Stufe bringt nach jedem Einsatz Beitrag.</summary>
        public int Support { get; set; }

        /// <summary>Welche Mission gerade läuft, als Schlüssel für Records.</summary>
        public string Current { get; set; } = "";
        public List<AgentRecord> Agents { get; set; } = new();
        public Dictionary<string, MissionRecord> Records { get; set; } = new();

        /// <summary>Kennungen der Viertel, die dem Roten Hammer gehören. Alle anderen gehören den Brandt-Werken.</summary>
        public List<string> Conquered { get; set; } = new();

        /// <summary>Kennung des gewählten Viertels, dort läuft der nächste Einsatz.</summary>
        public string District { get; set; } = "";

        /// <summary>Die Kiste der Truppe: Gekauftes, das gerade niemand trägt.</summary>
        public List<string> Stash { get; set; } = new();

        /// <summary>Der allgemeine Hinweis zum Dauertod erscheint einmal je Kampagne. Die Anzeige setzt das, wenn sie ihn gezeigt hat.</summary>
        public bool DeathRuleExplained { get; set; }

        /// <summary>Was der letzte zählende Einsatz mit dem Trupp gemacht hat, für Ergebnisfenster und Telegramme. Prepare leert die Listen.</summary>
        public List<string> LastFirstFalls { get; set; } = new();
        public List<string> LastDeaths { get; set; } = new();
        public List<string> LastNewcomers { get; set; } = new();

        public static int Reward(MissionRating rating) => Reward(rating, BaseReward);

        /// <summary>Lohn für einen Sieg: der Grundlohn des Viertels plus Zuschlag für die Wertung.</summary>
        public static int Reward(MissionRating rating, int baseReward) => baseReward + rating switch
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

        public DistrictOwner Owner(string districtId) => Conquered.Contains(districtId) ? DistrictOwner.RoterHammer : DistrictOwner.Brandt;

        public DistrictState State(CityMapData map, string districtId)
        {
            var district = map.Get(districtId);
            return Conquered.Contains(districtId) ? DistrictState.Taken
                : district == null || district.Mission.Length == 0 ? DistrictState.NoMission
                : district.Requires.All(Conquered.Contains) ? DistrictState.Open
                : DistrictState.Locked;
        }

        /// <summary>Lässt sich dort ein Einsatz starten? Auch ein erobertes Viertel bleibt spielbar, ein Viertel ohne Mission nie.</summary>
        public bool CanSelect(CityMapData map, string districtId) =>
            map.Get(districtId) is { } district && district.Mission.Length > 0 && district.Requires.All(Conquered.Contains);

        public bool TrySelect(CityMapData map, string districtId)
        {
            if (!CanSelect(map, districtId))
            {
                return false;
            }
            District = districtId;
            return true;
        }

        /// <summary>Das gewählte Viertel samt Auftrag. Ohne gültige Wahl das erste offene, sonst das erste spielbare. Null, wenn nichts spielbar ist.</summary>
        public DistrictData? Selected(CityMapData map)
        {
            if (!CanSelect(map, District))
            {
                District = (map.Districts.Find(d => State(map, d.Id) == DistrictState.Open)
                    ?? map.Districts.Find(d => CanSelect(map, d.Id)))?.Id ?? "";
            }
            return map.Get(District);
        }

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
            // Vor der ersten zählenden Nacht weiß keine Wache etwas vom Trupp.
            mission.Memory ??= Nights > 0 ? GuardMemoryData.From(this) : null;
            mission.Support = Support;
            // Türen der Stadt: Wer in diesem Einsatz hilft, entscheidet das Gedächtnis der Stadt.
            mission.Helpers = mission.Doors.Select(d => d.Helper).Where(h => h.Length > 0).Distinct().Where(h => HouseHelps(h, missionId)).ToList();
            foreach (var a in mission.Agents)
            {
                if (!Agents.Exists(r => r.Slot == a.Name))
                {
                    Agents.Add(new AgentRecord { Name = a.Name, Slot = a.Name, Legs = a.Legs, Heart = a.Heart });
                }
                // Was die Truppführerin kann, hängt an der Person, nicht am Platz: Wer sie ersetzt, kennt die Wache nicht.
                foreach (var record in Agents.Where(x => x.Slot == a.Name && x.Skill == "" && (a.Skill != World.SkillLeader || x.Name == a.Name)))
                {
                    record.Skill = a.Skill;
                }
                foreach (var record in Agents.Where(x => x.Slot == a.Name && x.Role == ""))
                {
                    record.Role = record.Job.Length > 0 ? record.Job : a.RoleName();
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
                    if (a.Skill == World.SkillLeader && record.Name != a.Name)
                    {
                        a.Skill = "";
                    }
                    a.Name = record.Name;
                    a.Legs = record.Legs;
                    a.Heart = record.Heart;
                    a.Items = new List<string>(record.Items);
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
            skill = skill == World.SkillLeader ? "" : skill;
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

        /// <summary>
        /// Verbucht einen beendeten Einsatz. Beute zählt nur, wenn der Trupp entkommt.
        /// Mit Viertel gilt dessen Grundlohn, und bei einem Sieg gehört es danach dem Roten Hammer.
        /// </summary>
        public void Finish(World world, DistrictData? district = null)
        {
            bool won = world.State == MissionState.Won;
            int reward = Reward(world.Rating, district?.Reward ?? BaseReward);
            Money = won ? world.Money + reward : world.Money - (world.LootTaken ? world.LootValue : 0);
            if (won)
            {
                Missions++;
                if (district != null && !Conquered.Contains(district.Id))
                {
                    Conquered.Add(district.Id);
                }
            }
            Settle(world, won, false, district?.Id ?? District);
        }

        /// <summary>Kostet der Abbruch den Trupp? Vor dem ersten Schuss kann niemand gefallen sein, der Rückzug ist frei.</summary>
        public static bool AbandonCostsSquad(World world) => world.Rating == MissionRating.Loud;

        /// <summary>Verbucht einen abgebrochenen Einsatz. Nach dem ersten Schuss gilt der ganze Trupp als gefallen, sonst ließe sich jeder Sturz durch rechtzeitiges Abbrechen vermeiden.</summary>
        public void Abandon(World world)
        {
            Money = world.Money - (world.LootTaken ? world.LootValue : 0);
            if (AbandonCostsSquad(world))
            {
                Settle(world, false, true, District);
            }
        }

        /// <summary>Folgen eines Einsatzes, der zählt: Agenten, Fahndung, Rückhalt, Beitrag und der Eintrag für die Story.</summary>
        private void Settle(World world, bool won, bool abandoned, string districtId)
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
                // Verbrauchtes ist weg. Wer stirbt, verliert alles, die Krankenstube behält die Sachen.
                record.Items = record.Dead ? new List<string>() : World.ItemIds(u);
            }

            Remember(GuardReport.From(world), Agents.Where(a => LastDeaths.Contains(a.Name)), districtId);
            // Niedergeschlagene liegen am Boden, sind aber nicht tot. Hunde zählen für Fahndung und Zeitung wie bisher mit.
            int deadGuards = world.GuardOutcomes().Count(o => o.Fate == GuardFate.Dead);
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
                // Die erste Befreiung zählt. Wer ein erobertes Viertel noch einmal spielt, macht keinen Toten lebendig und keinen Lebenden tot.
                if (entry.Prisoners.Count == 0)
                {
                    entry.Prisoners = world.Prisoners.ToDictionary(p => p.Name, p => p.Alive);
                }
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

        /// <summary>Kauft einen Gegenstand. Mit Agent landet er in dessen freiem Platz, sonst in der Kiste.</summary>
        public bool TryBuy(ItemCatalog catalog, string itemId, string? agent = null)
        {
            var def = catalog.Get(itemId);
            var record = agent == null ? null : Agent(agent);
            if (def == null || Money < def.Price || (record != null && !CanCarryMore(record)))
            {
                return false;
            }
            Money -= def.Price;
            (record?.Items ?? Stash).Add(itemId);
            return true;
        }

        /// <summary>Verkauft einen Gegenstand aus den Plätzen des Agenten oder, ohne Agent, aus der Kiste. Bringt SellPercent des Preises.</summary>
        public bool TrySell(ItemCatalog catalog, string itemId, string? agent = null)
        {
            var from = agent == null ? Stash : Agent(agent).Items;
            if (catalog.Get(itemId) == null || !from.Remove(itemId))
            {
                return false;
            }
            Money += catalog.SellPrice(itemId);
            return true;
        }

        /// <summary>Nimmt einen Gegenstand aus der Kiste und gibt ihn dem Agenten.</summary>
        public bool Equip(string agent, string itemId)
        {
            var record = Agent(agent);
            if (!CanCarryMore(record) || !Stash.Remove(itemId))
            {
                return false;
            }
            record.Items.Add(itemId);
            return true;
        }

        /// <summary>Legt den Gegenstand auf diesem Platz des Agenten in die Kiste.</summary>
        public bool Unequip(string agent, int slot)
        {
            var record = Agent(agent);
            if (record.Dead || slot < 0 || slot >= record.Items.Count)
            {
                return false;
            }
            Stash.Add(record.Items[slot]);
            record.Items.RemoveAt(slot);
            return true;
        }

        private static bool CanCarryMore(AgentRecord record) => !record.Dead && record.Items.Count < ItemSlots;

        /// <summary>Lebt ein Befreiter aus einem gewonnenen Einsatz? Null, solange er noch nicht befreit wurde.</summary>
        /// <summary>Wer im laufenden oder zuletzt gespielten Einsatz befreit wurde, nach der ersten Befreiung. Leer ohne Sieg oder ohne Gefangene.</summary>
        public IReadOnlyDictionary<string, bool> LastPrisoners =>
            Records.TryGetValue(Current, out var record) ? record.Prisoners : new Dictionary<string, bool>();

        public bool? PrisonerAlive(string name)
        {
            foreach (var record in Records.Values)
            {
                if (record.Prisoners != null && record.Prisoners.TryGetValue(name, out bool alive))
                {
                    return alive;
                }
            }
            return null;
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
            loaded.Repair(version);
            return loaded;
        }

        /// <summary>Bringt einen geladenen Stand in gültige Grenzen. Alte Stände kennen keinen Platz, dort ist er der Name.</summary>
        private void Repair(int version)
        {
            Version = CurrentVersion;
            Money = Math.Max(0, Money);
            Missions = Math.Max(0, Missions);
            Heat = Math.Clamp(Heat, 0, MaxHeat);
            Support = Math.Clamp(Support, 0, MaxSupport);
            Current ??= "";
            Records = (Records ?? new()).Where(r => r.Value != null).ToDictionary(r => r.Key, r => r.Value);
            foreach (var record in Records.Values)
            {
                record.Prisoners ??= new();
            }
            LastFirstFalls ??= new();
            LastDeaths ??= new();
            LastNewcomers ??= new();
            District ??= "";
            Conquered = (Conquered ?? new()).Where(d => !string.IsNullOrEmpty(d)).Distinct().ToList();
            // Vor dem Stadtplan gab es nur den ersten Einsatz. Wer ihn gewonnen hat, hält sein Viertel.
            if (version < 4 && (Missions > 0 || Records.Values.Any(r => r != null && r.Won)) && !Conquered.Contains(FirstDistrict))
            {
                Conquered.Add(FirstDistrict);
            }
            Stash = (Stash ?? new()).Where(i => !string.IsNullOrEmpty(i)).ToList();
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
                a.Role ??= "";
                a.Role = a.Role.Length > 0 ? a.Role : a.Job ?? "";
                // Vor der Waffenkammer trug jeder Lebende eine Pistole.
                a.Items = (a.Items ?? new()).Where(i => !string.IsNullOrEmpty(i)).Take(ItemSlots).ToList();
                if (version < 3 && a.Items.Count == 0 && !a.Dead)
                {
                    a.Items.AddRange(ItemCatalog.StarterKit);
                }
                if (a.Dead)
                {
                    a.Items.Clear();
                }
            }
            RepairLedger(version);
        }
    }
}
