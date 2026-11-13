using System;
using System.Collections.Generic;

namespace Messingstadt.Core
{
    /// <summary>Eine Wache, die der Trupp schon aus einem früheren Einsatz kennt, und was dort aus ihr wurde.</summary>
    public sealed class KnownGuardData
    {
        public string Person { get; set; } = "";
        public GuardFate Fate { get; set; }
    }

    /// <summary>Was die Wachen dieses Einsatzes vom letzten wissen. Kommt von der Kampagne, ohne Angabe weiß niemand etwas.</summary>
    public sealed class GuardMemoryData
    {
        /// <summary>Wie die Stadt nach der Blutschuld zum Trupp steht. Die eine Größe, an der die Erinnerung der Wachen hängt.</summary>
        public CityStance Stance { get; set; } = CityStance.Silent;

        /// <summary>Wachen aus früheren Einsätzen, die hier wieder Dienst haben können, mit ihrem Schicksal dort.</summary>
        public List<KnownGuardData> Known { get; set; } = new();

        /// <summary>
        /// Was die Wachen nach dem bisherigen Verlauf der Kampagne wissen: die Haltung der Stadt aus der Blutschuld und jede Wache, der der Trupp begegnet ist.
        /// </summary>
        public static GuardMemoryData From(Campaign campaign)
        {
            var memory = new GuardMemoryData { Stance = campaign.Stance };
            foreach (var g in campaign.Guards)
            {
                memory.Known.RemoveAll(k => k.Person == g.Name);
                memory.Known.Add(new KnownGuardData { Person = g.Name, Fate = g.Fate });
            }
            return memory;
        }
    }

    /// <summary>Wie eine Wache dem Trupp wegen des letzten Einsatzes begegnet.</summary>
    public enum GuardRecall
    {
        None,

        /// <summary>Wurde schon einmal gekauft: nimmt weniger und schaut weg.</summary>
        OldDeal,

        /// <summary>Hat überlebt, und die Stadt steht gegen den Trupp: nimmt kein Geld und bemerkt schneller.</summary>
        Grudge,

        /// <summary>Die Stadt hilft dem Trupp: steht allein und schaut weg.</summary>
        AtEase,
    }

    public sealed partial class Unit
    {
        public GuardRecall Recall;

        /// <summary>Faktor in Prozent auf die Zeit, bis die Wache jemanden bemerkt. Über 100 schaut sie weg, unter 100 ist sie wachsam.</summary>
        public int NoticeScale = 100;

        /// <summary>Nachlass in Prozent, weil die Wache den Trupp schon als Zahler kennt.</summary>
        public int MemoryDiscount;

        /// <summary>Nimmt in diesem Einsatz kein Geld, auch wenn ihre Art käuflich ist.</summary>
        public bool Unbribable;
    }

    public sealed partial class World
    {
        // Die Decke: Mehr als das kostet die Blutschuld im nächsten Einsatz nie, egal wie viele gefallen sind.
        public const int MaxBloodPartners = 2;
        public const int MaxGrudges = 2;
        public const int MaxAtEase = 2;
        public const int GrudgeNoticeScale = 75;
        public const int AtEaseNoticeScale = 150;
        public const int OldDealDiscount = 25;

        /// <summary>
        /// Wachen mit AfterBlood gehen nur mit, wenn die Stadt gegen den Trupp steht, und davon höchstens MaxBloodPartners.
        /// Liefert die Mission unverändert, wenn sie keine solchen Wachen kennt.
        /// </summary>
        public static MissionData Recalled(MissionData mission)
        {
            if (!mission.Guards.Exists(g => g.AfterBlood))
            {
                return mission;
            }
            bool blood = mission.Memory is { Stance: CityStance.Against };
            int partners = 0;
            var guards = new List<UnitData>();
            foreach (var g in mission.Guards)
            {
                if (!g.AfterBlood || (blood && partners++ < MaxBloodPartners))
                {
                    guards.Add(g);
                }
            }
            return mission.WithGuards(guards);
        }

        private GuardMemoryData? _memory;

        private void ApplyMemory(MissionData mission)
        {
            for (int i = 0; i < mission.Guards.Count; i++)
            {
                var g = Units[mission.Agents.Count + i];
                g.Unbribable = mission.Guards[i].NoBribe;
                g.NoticeScale = Math.Max(1, mission.Guards[i].NoticeScale);
            }
            if (mission.Memory is not { } memory)
            {
                return;
            }
            _memory = memory;
            int grudges = 0, atEase = 0;
            for (int i = 0; i < mission.Guards.Count; i++)
            {
                var g = Units[mission.Agents.Count + i];
                string returning = mission.Guards[i].Returning;
                if (returning.Length > 0 && memory.Known.Exists(k => k.Person == returning && k.Fate != GuardFate.Dead))
                {
                    g.Person = returning;
                }
                var known = g.Person.Length > 0 ? memory.Known.Find(k => k.Person == g.Person) : null;
                bool human = !g.Type.Bites;
                if (known is { Fate: GuardFate.Bribed } && g.Type.BribeCost > 0)
                {
                    g.Recall = GuardRecall.OldDeal;
                    g.MemoryDiscount = OldDealDiscount;
                    g.NoticeScale = Math.Max(g.NoticeScale, AtEaseNoticeScale);
                }
                else if (known != null && known.Fate != GuardFate.Dead && human && memory.Stance == CityStance.Against && grudges < MaxGrudges)
                {
                    grudges++;
                    g.Recall = GuardRecall.Grudge;
                    g.Unbribable = true;
                    g.NoticeScale = GrudgeNoticeScale;
                }
                else if (mission.Guards[i].AtEase && memory.Stance == CityStance.Helps && human && atEase < MaxAtEase)
                {
                    atEase++;
                    g.Recall = GuardRecall.AtEase;
                    g.NoticeScale = Math.Max(g.NoticeScale, AtEaseNoticeScale);
                }
            }
        }

        /// <summary>Nimmt diese Wache in diesem Einsatz überhaupt Geld?</summary>
        public bool IsBribable(Unit guard) => guard.Type.BribeCost > 0 && !guard.Unbribable;

        /// <summary>Der größere Nachlass gilt, sie addieren sich nicht: der der Truppführerin oder der aus dem alten Handel.</summary>
        private int BribePriceWith(Unit guard, int leaderPercent) =>
            guard.Type.BribeCost * (100 - Math.Max(leaderPercent, guard.MemoryDiscount)) / 100;

        /// <summary>Was die Wache nähme, wenn die Truppführerin dabei wäre. Für die Anzeige.</summary>
        public int BribePriceWithLeader(Unit guard) => BribePriceWith(guard, LeaderDiscountPercent);
    }
}
