using System;
using System.Collections.Generic;
using System.Linq;

namespace Messingstadt.Core
{
    public enum LedgerSide
    {
        Squad,
        Guard,
    }

    /// <summary>Eine Wache am Ende eines Einsatzes, so wie die Spiellogik sie meldet.</summary>
    public sealed class GuardReport
    {
        public string Name { get; set; } = "";

        /// <summary>Art der Wache, Kennung aus GuardType.</summary>
        public string Kind { get; set; } = "";
        public GuardFate Fate { get; set; }

        /// <summary>
        /// Liest die Wachen eines beendeten Einsatzes, ohne Hunde. Die einzige Stelle, die weiß, woran die Spiellogik tot, niedergeschlagen,
        /// gekauft und ahnungslos erkennt.
        /// </summary>
        public static List<GuardReport> From(World world) => world.GuardOutcomes()
            .Where(o => o.Type != GuardType.Hund.Id)
            .Select(o => new GuardReport
            {
                Name = o.Person.Length > 0 ? o.Person : o.Role,
                Kind = o.Type,
                Fate = o.Fate,
            })
            .ToList();
    }

    /// <summary>Ein Toter im Totenbuch, von beiden Seiten.</summary>
    public sealed class LedgerEntry
    {
        public string Name { get; set; } = "";
        public LedgerSide Side { get; set; }

        /// <summary>Art der Wache oder Beruf des Agenten, für eine Zeile im Buch.</summary>
        public string Kind { get; set; } = "";

        /// <summary>Kennung der Mission. Leer bei Toten aus Ständen vor dem Totenbuch.</summary>
        public string Mission { get; set; } = "";
        public string District { get; set; } = "";

        /// <summary>Der wievielte zählende Einsatz der Kampagne es war, ab 1. 0 bei Toten aus Ständen vor dem Totenbuch.</summary>
        public int Night { get; set; }
    }

    /// <summary>Was die Kampagne über eine Wache weiß, die sie getroffen hat. Je Mission und Name ein Eintrag, der letzte Stand zählt, tot bleibt tot.</summary>
    public sealed class GuardMemory
    {
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Mission { get; set; } = "";
        public string District { get; set; } = "";
        public GuardFate Fate { get; set; }
        public int Night { get; set; }
    }

    /// <summary>
    /// Das Gedächtnis der Stadt: das Totenbuch beider Seiten, was aus den Wachen wurde und wie viel Blut in welchem Viertel floss.
    /// Alle Abfragen für die anderen Bereiche stehen in dieser Datei.
    /// </summary>
    public sealed partial class Campaign
    {
        public const string HelperWidow = "witwe";
        public const string HelperGravedigger = "totengraeber";
        public const string HelperLandlady = "wirtin";
        private const string FirstMission = "mission_01";

        /// <summary>Das Totenbuch in der Reihenfolge der Tode. Niedergeschlagene und Verwundete stehen nicht darin.</summary>
        public List<LedgerEntry> Ledger { get; set; } = new();

        /// <summary>Alle Wachen, denen der Trupp begegnet ist, mit ihrem letzten Stand.</summary>
        public List<GuardMemory> Guards { get; set; } = new();

        /// <summary>Wie viele Einsätze bisher gezählt haben. Nummeriert die Einträge im Totenbuch.</summary>
        public int Nights { get; set; }

        /// <summary>Je Viertel: tote Wachen im letzten zählenden Einsatz dort. Ein Viertel ohne Eintrag hat noch keinen Einsatz gesehen.</summary>
        public Dictionary<string, int> LastBlood { get; set; } = new();

        // ---- Wer ist tot, wer wurde gekauft, wer hat überlebt ----

        /// <summary>Die Toten einer Seite, in der Reihenfolge der Tode.</summary>
        public List<LedgerEntry> Dead(LedgerSide side) => Ledger.Where(e => e.Side == side).ToList();

        /// <summary>Die Toten des letzten zählenden Einsatzes, erst der Trupp, dann die Wache.</summary>
        public List<LedgerEntry> DeadLastNight() => Ledger.Where(e => e.Night == Nights && e.Night > 0).OrderBy(e => e.Side).ToList();

        /// <summary>Steht dieser Name im Totenbuch, als Wache oder als Agent? Mit Mission nur die Wache dieses Einsatzes.</summary>
        public bool IsDead(string name, string? mission = null) =>
            Ledger.Exists(e => e.Name == name && (mission == null || e.Mission == mission));

        /// <summary>Was zuletzt aus dieser Wache wurde. Null, wenn der Trupp ihr nie begegnet ist.</summary>
        public GuardFate? FateOf(string name, string? mission = null) =>
            Guards.LastOrDefault(g => g.Name == name && (mission == null || g.Mission == mission))?.Fate;

        /// <summary>Hat diese Wache schon einmal Geld genommen und lebt noch?</summary>
        public bool WasBribed(string name, string? mission = null) => FateOf(name, mission) == GuardFate.Bribed;

        /// <summary>Wachen dieser Mission, die den letzten Einsatz dort überlebt haben, für das Wiedersehen.</summary>
        public List<GuardMemory> Survivors(string mission) => Guards.Where(g => g.Mission == mission && g.Fate != GuardFate.Dead).ToList();

        // ---- Blut ----

        /// <summary>Tote Wachen im letzten zählenden Einsatz in diesem Viertel. 0 auch, wenn dort noch kein Einsatz lief.</summary>
        public int BloodLast(string districtId) => LastBlood.GetValueOrDefault(districtId);

        /// <summary>Tote Wachen in der ganzen Kampagne, mit Viertel nur dort.</summary>
        public int BloodTotal(string? districtId = null) =>
            Ledger.Count(e => e.Side == LedgerSide.Guard && (districtId == null || e.District == districtId));

        /// <summary>Tote Wachen in einer Mission, über alle Versuche.</summary>
        public int BloodIn(string mission) => Ledger.Count(e => e.Side == LedgerSide.Guard && e.Mission == mission);

        // ---- Türen der Stadt ----

        /// <summary>
        /// Hilft dieses Haus dem Trupp, etwa mit einer Kerze im Fenster oder einer offenen Hoftür?
        /// Witwe und Totengräber helfen, solange im ersten Einsatz keine Wache starb. Die Wirtin hilft, wenn Greta lebt
        /// oder der erste Einsatz ganz ohne Tote blieb. Jedes andere Haus hilft, wenn im Viertel zuletzt kein Blut floss.
        /// </summary>
        public bool HouseHelps(string helper, string districtId = "") => helper switch
        {
            HelperWidow or HelperGravedigger => BloodIn(FirstMission) == 0,
            HelperLandlady => !IsDead("Greta") || !Ledger.Exists(e => e.Mission == FirstMission),
            _ => BloodLast(districtId) == 0,
        };

        // ---- Schreiben ----

        /// <summary>Trägt eine Nacht nur aus Meldungen ein, ohne Spielwelt. Für Werkzeuge und Tests, das Spiel geht über Finish und Abandon.</summary>
        public void RememberNight(IReadOnlyList<GuardReport> guards, string districtId) => Remember(guards, Array.Empty<AgentRecord>(), districtId);

        /// <summary>Trägt das Ende eines zählenden Einsatzes ein: die Wachen, die Toten beider Seiten und das Blut im Viertel.</summary>
        private void Remember(IReadOnlyList<GuardReport> guards, IEnumerable<AgentRecord> fallen, string districtId)
        {
            Nights++;
            foreach (var g in guards.Where(g => !string.IsNullOrEmpty(g.Name)))
            {
                var memory = Guards.Find(m => m.Name == g.Name && m.Mission == Current);
                if (memory == null)
                {
                    memory = new GuardMemory { Name = g.Name, Kind = g.Kind, Mission = Current, District = districtId };
                    Guards.Add(memory);
                }
                if (memory.Fate == GuardFate.Dead)
                {
                    continue;
                }
                memory.Fate = g.Fate;
                memory.Night = Nights;
                if (g.Fate == GuardFate.Dead)
                {
                    Ledger.Add(new LedgerEntry { Name = g.Name, Side = LedgerSide.Guard, Kind = g.Kind, Mission = Current, District = districtId, Night = Nights });
                }
            }
            foreach (var a in fallen.Where(a => !Ledger.Exists(e => e.Side == LedgerSide.Squad && e.Name == a.Name)))
            {
                Ledger.Add(new LedgerEntry { Name = a.Name, Side = LedgerSide.Squad, Kind = a.Job, Mission = Current, District = districtId, Night = Nights });
            }
            LastBlood[districtId] = guards.Count(g => g.Fate == GuardFate.Dead);
        }

        /// <summary>Alte Stände haben ein leeres Buch. Nur die eigenen Toten sind bekannt und kommen ohne Einsatz hinein.</summary>
        private void RepairLedger(int version)
        {
            Nights = Math.Max(0, Nights);
            Ledger = (Ledger ?? new()).Where(e => e != null && !string.IsNullOrEmpty(e.Name)).ToList();
            Guards = (Guards ?? new()).Where(g => g != null && !string.IsNullOrEmpty(g.Name)).ToList();
            LastBlood = (LastBlood ?? new()).ToDictionary(b => b.Key, b => Math.Max(0, b.Value));
            foreach (var e in Ledger)
            {
                e.Kind ??= "";
                e.Mission ??= "";
                e.District ??= "";
            }
            foreach (var g in Guards)
            {
                g.Kind ??= "";
                g.Mission ??= "";
                g.District ??= "";
            }
            if (version < 5)
            {
                foreach (var a in Agents.Where(a => a.Dead && !Ledger.Exists(e => e.Side == LedgerSide.Squad && e.Name == a.Name)))
                {
                    Ledger.Add(new LedgerEntry { Name = a.Name, Side = LedgerSide.Squad, Kind = a.Job });
                }
            }
        }
    }
}
