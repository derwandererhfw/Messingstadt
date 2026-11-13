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
        /// <summary>Tote Wachen im letzten Einsatz. Niedergeschlagene zählen nicht.</summary>
        public int Dead { get; set; }

        /// <summary>Wachen aus früheren Einsätzen, die hier wieder Dienst haben können, mit ihrem Schicksal dort.</summary>
        public List<KnownGuardData> Known { get; set; } = new();
    }

    /// <summary>Wie eine Wache dem Trupp wegen des letzten Einsatzes begegnet.</summary>
    public enum GuardRecall
    {
        None,

        /// <summary>Wurde schon einmal gekauft: nimmt weniger und schaut weg.</summary>
        OldDeal,

        /// <summary>Hat ein Blutbad überlebt: nimmt kein Geld und bemerkt schneller.</summary>
        Grudge,

        /// <summary>Im letzten Einsatz starb niemand: steht allein und schaut weg.</summary>
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
        /// <summary>Ab so vielen Toten im letzten Einsatz gilt er den Wachen als Blutbad.</summary>
        public const int BloodbathDead = 3;

        // Die Decke: Mehr als das kostet Blut im nächsten Einsatz nie, egal wie viele gefallen sind.
        public const int MaxBloodPartners = 2;
        public const int MaxGrudges = 2;
        public const int MaxAtEase = 2;
        public const int GrudgeNoticeScale = 75;
        public const int AtEaseNoticeScale = 150;
        public const int OldDealDiscount = 25;

        /// <summary>
        /// Wachen mit AfterBlood gehen nur mit, wenn im letzten Einsatz jemand starb, und davon höchstens MaxBloodPartners.
        /// Liefert die Mission unverändert, wenn sie keine solchen Wachen kennt.
        /// </summary>
        public static MissionData Recalled(MissionData mission)
        {
            if (!mission.Guards.Exists(g => g.AfterBlood))
            {
                return mission;
            }
            bool blood = mission.Memory is { Dead: > 0 };
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

        private void ApplyMemory(MissionData mission)
        {
            if (mission.Memory is not { } memory)
            {
                return;
            }
            int grudges = 0, atEase = 0;
            for (int i = 0; i < mission.Guards.Count; i++)
            {
                var g = Units[mission.Agents.Count + i];
                var known = g.Person.Length > 0 ? memory.Known.Find(k => k.Person == g.Person) : null;
                bool human = !g.Type.Bites;
                if (known is { Fate: GuardFate.Bribed } && g.Type.BribeCost > 0)
                {
                    g.Recall = GuardRecall.OldDeal;
                    g.MemoryDiscount = OldDealDiscount;
                    g.NoticeScale = AtEaseNoticeScale;
                }
                else if (known != null && known.Fate != GuardFate.Dead && human && memory.Dead >= BloodbathDead && grudges < MaxGrudges)
                {
                    grudges++;
                    g.Recall = GuardRecall.Grudge;
                    g.Unbribable = true;
                    g.NoticeScale = GrudgeNoticeScale;
                }
                else if (mission.Guards[i].AtEase && memory.Dead == 0 && human && atEase < MaxAtEase)
                {
                    atEase++;
                    g.Recall = GuardRecall.AtEase;
                    g.NoticeScale = AtEaseNoticeScale;
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
