using System;
using System.Collections.Generic;
using System.Linq;

namespace Messingstadt.Core
{
    public enum LedgerSide
    {
        Squad,
        Guard,

        /// <summary>
        /// Ein Gefangener, der bei seiner Befreiung starb. Steht auf der eigenen Seite des Buchs, ist aber kein Truppmitglied:
        /// kein Einfluss auf Blutschuld, Dauertod oder Nachrücken. Entscheidung der Hauptsession vom 14.11.
        /// </summary>
        Prisoner,
    }

    /// <summary>Wie die Stadt zum Trupp steht, aus der Blutschuld. Die Kulisse, die Türen und die Wachen hängen an dieser Stufe.</summary>
    public enum CityStance
    {
        Helps,
        Silent,
        Against,
    }

    public sealed partial class Unit
    {
        /// <summary>Anrede in Zeitung und Totenbuch, wenn sie von der Art abweicht, etwa "Oberwachtmeister". Sonst leer.</summary>
        public string Rank = "";

        /// <summary>Alter, nur bei Jungen gesetzt. 0 heißt: wird nicht genannt.</summary>
        public int Age;
    }

    /// <summary>Eine Wache am Ende eines Einsatzes, so wie die Spiellogik sie meldet.</summary>
    public sealed class GuardReport
    {
        public string Name { get; set; } = "";

        /// <summary>Art der Wache, Kennung aus GuardType.</summary>
        public string Kind { get; set; } = "";
        public GuardFate Fate { get; set; }

        /// <summary>Eigene Anrede aus der Mission, sonst leer.</summary>
        public string Rank { get; set; } = "";

        /// <summary>Alter, nur bei Jungen gesetzt, sonst 0.</summary>
        public int Age { get; set; }

        /// <summary>Anrede für Zeitung und Totenbuch: die eigene oder die der Art.</summary>
        public string Title => LedgerEntry.TitleFor(LedgerSide.Guard, Kind, Rank);

        /// <summary>
        /// Liest die Wachen eines beendeten Einsatzes, ohne Hunde. Was aus ihnen wurde, entscheidet World.FateOf.
        /// Der Name ist der des Menschen, wenn die Mission einen nennt, sonst der Posten. Gleiche Namen werden durchgezählt,
        /// damit zwei namenlose Mann Verstärkung zwei Einträge sind.
        /// </summary>
        public static List<GuardReport> From(World world)
        {
            var list = new List<GuardReport>();
            foreach (var u in world.Units.Where(u => u.Faction == Faction.Guard && u.Type != GuardType.Hund))
            {
                string name = u.Person.Length > 0 ? u.Person : u.Name;
                int same = list.Count(r => r.Name == name || r.Name.StartsWith(name + " ("));
                list.Add(new GuardReport
                {
                    Name = same == 0 ? name : $"{name} ({same + 1})",
                    Kind = u.Type.Id,
                    Fate = World.FateOf(u),
                    Rank = u.Rank,
                    Age = u.Age,
                });
            }
            return list;
        }
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

        /// <summary>Eigene Anrede einer Wache aus der Mission, etwa "Oberwachtmeister". Leer, wenn die Art genügt.</summary>
        public string Rank { get; set; } = "";

        /// <summary>Alter, nur bei Jungen gesetzt, sonst 0.</summary>
        public int Age { get; set; }

        /// <summary>Anrede für Zeitung und Totenbuch: bei Wachen die eigene oder die der Art, beim Trupp der Beruf.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string Title => TitleFor(Side, Kind, Rank);

        /// <summary>Steht der Eintrag auf der eigenen Seite des Buchs: Trupp oder Gefangener?</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool Ours => Side != LedgerSide.Guard;

        public static string TitleFor(LedgerSide side, string kind, string rank) =>
            rank.Length > 0 ? rank
            : side != LedgerSide.Guard ? kind
            : kind switch
            {
                "offizier" => "Wachtmeister",
                "werkschutz" => "Werkschutzmann",
                "laufbursche" => "Laufbursche",
                _ => "Wachmann",
            };
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
        public const int MaxBloodDebt = 6;

        /// <summary>Um mehr als so viel steigt die Schuld in einer Nacht nicht, auch wenn mehr Wachen sterben.</summary>
        public const int BloodPerNight = 3;

        /// <summary>So viel tilgt eine Nacht ohne tote Wache.</summary>
        public const int BloodForgiven = 2;

        /// <summary>Ab dieser Schuld schweigt die Stadt, ab der zweiten stellt sie sich gegen den Trupp.</summary>
        public const int SilentFrom = 2;
        public const int AgainstFrom = 4;

        /// <summary>
        /// Die Blutschuld des Trupps bei der ganzen Stadt, 0 bis 6. Der erste Tote einer Nacht ist frei, jeder weitere erhöht sie um 1,
        /// höchstens um 3 je Nacht. Eine Nacht ohne tote Wache tilgt 2. Gelegentliche Tote verzeiht die Stadt, eine Gewohnheit nicht,
        /// und keine einzelne Nacht kippt alles. Das Totenbuch selbst vergisst nichts.
        /// </summary>
        public int BloodDebt { get; set; }

        /// <summary>Wie die Stadt im nächsten Einsatz zum Trupp steht.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public CityStance Stance => StanceFor(BloodDebt);

        public static CityStance StanceFor(int bloodDebt) =>
            bloodDebt >= AgainstFrom ? CityStance.Against : bloodDebt >= SilentFrom ? CityStance.Silent : CityStance.Helps;

        /// <summary>Die Schuld nach einer Nacht mit so vielen toten Wachen.</summary>
        public static int DebtAfter(int bloodDebt, int deadGuards) =>
            deadGuards == 0 ? Math.Max(0, bloodDebt - BloodForgiven)
            : Math.Min(MaxBloodDebt, bloodDebt + Math.Min(deadGuards - 1, BloodPerNight));

        /// <summary>Das Totenbuch in der Reihenfolge der Tode. Niedergeschlagene und Verwundete stehen nicht darin.</summary>
        public List<LedgerEntry> Ledger { get; set; } = new();

        /// <summary>Alle Wachen, denen der Trupp begegnet ist, mit ihrem letzten Stand.</summary>
        public List<GuardMemory> Guards { get; set; } = new();

        /// <summary>Wie viele Einsätze bisher gezählt haben. Nummeriert die Einträge im Totenbuch.</summary>
        public int Nights { get; set; }

        /// <summary>Je Viertel: tote Wachen im letzten zählenden Einsatz dort. Ein Viertel ohne Eintrag hat noch keinen Einsatz gesehen.</summary>
        public Dictionary<string, int> LastBlood { get; set; } = new();

        // ---- Wer ist tot, wer wurde gekauft, wer hat überlebt ----

        public const string PrisonerKind = "Gefangener";

        /// <summary>Die Toten einer Seite, in der Reihenfolge der Tode. Squad sind nur Truppmitglieder, tote Gefangene stehen unter Prisoner.</summary>
        public List<LedgerEntry> Dead(LedgerSide side) => Ledger.Where(e => e.Side == side).ToList();

        /// <summary>Die linke Seite des Buchs, „Unsere“: Truppmitglieder und tote Gefangene, in der Reihenfolge der Tode.</summary>
        public List<LedgerEntry> DeadOurs() => Ledger.Where(e => e.Ours).ToList();

        /// <summary>Die Toten des letzten zählenden Einsatzes: erst der Trupp, dann tote Gefangene, dann die Wache.</summary>
        public List<LedgerEntry> DeadLastNight() => Ledger.Where(e => e.Night == Nights && e.Night > 0)
            .OrderBy(e => e.Side == LedgerSide.Squad ? 0 : e.Side == LedgerSide.Prisoner ? 1 : 2).ToList();

        /// <summary>Trägt Gefangene ein, die bei ihrer Befreiung starben. Kein Einfluss auf Blut, Schuld oder Trupp.</summary>
        private void RememberPrisoners(IReadOnlyDictionary<string, bool> prisoners, string mission, string districtId, int night)
        {
            foreach (string name in prisoners.Where(p => !p.Value).Select(p => p.Key))
            {
                if (!Ledger.Exists(e => e.Side == LedgerSide.Prisoner && e.Name == name))
                {
                    Ledger.Add(new LedgerEntry { Name = name, Side = LedgerSide.Prisoner, Kind = PrisonerKind, Mission = mission, District = districtId, Night = night });
                }
            }
        }

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
        /// Jedes Haus hilft, solange die Stadt hilft. Die Wirtin kennt Greta: Lebt sie, hilft die Wirtin auch dann noch, wenn die Stadt schweigt.
        /// Das Viertel spielt dabei keine Rolle mehr, die Blutschuld gilt für die ganze Stadt.
        /// </summary>
        public bool HouseHelps(string helper, string districtId = "") =>
            Stance == CityStance.Helps || (helper == HelperLandlady && Stance == CityStance.Silent && !IsDead("Greta"));

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
                    Ledger.Add(new LedgerEntry { Name = g.Name, Side = LedgerSide.Guard, Kind = g.Kind, Mission = Current, District = districtId, Night = Nights, Rank = g.Rank, Age = g.Age });
                }
            }
            foreach (var a in fallen.Where(a => !Ledger.Exists(e => e.Side == LedgerSide.Squad && e.Name == a.Name)))
            {
                Ledger.Add(new LedgerEntry { Name = a.Name, Side = LedgerSide.Squad, Kind = a.Role, Mission = Current, District = districtId, Night = Nights });
            }
            int dead = guards.Count(g => g.Fate == GuardFate.Dead);
            LastBlood[districtId] = dead;
            BloodDebt = DebtAfter(BloodDebt, dead);
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
                e.Rank ??= "";
                e.Age = Math.Max(0, e.Age);
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
                    Ledger.Add(new LedgerEntry { Name = a.Name, Side = LedgerSide.Squad, Kind = a.Role });
                }
            }
            // Vor Version 8 standen tote Gefangene nur im Merker des Einsatzes. Nacht und Viertel sind dort nicht bekannt.
            if (version < 8)
            {
                foreach (var (mission, record) in Records)
                {
                    RememberPrisoners(record.Prisoners, mission, "", 0);
                }
            }
            // Vor der Blutschuld: aus den Nächten des Totenbuchs nachrechnen.
            if (version < 6)
            {
                BloodDebt = 0;
                for (int night = 1; night <= Nights; night++)
                {
                    BloodDebt = DebtAfter(BloodDebt, Ledger.Count(e => e.Side == LedgerSide.Guard && e.Night == night));
                }
            }
            BloodDebt = Math.Clamp(BloodDebt, 0, MaxBloodDebt);
        }
    }
}
