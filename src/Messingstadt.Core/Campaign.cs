using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Messingstadt.Core
{
    /// <summary>Ausbaustufen eines Agenten über mehrere Einsätze.</summary>
    public sealed class AgentRecord
    {
        public string Name { get; set; } = "";
        public int Legs { get; set; } = 1;
        public int Heart { get; set; } = 1;
    }

    /// <summary>Was zwischen den Einsätzen bleibt: Geld und der Ausbau der Agenten.</summary>
    public sealed class Campaign
    {
        public const int MaxLevel = 3;
        private const int BaseReward = 300;
        private const int QuietBonus = 150;
        private const int UnseenBonus = 300;
        private const int UpgradeCostPerLevel = 150;

        public int Money { get; set; } = 300;
        public int Missions { get; set; }
        public List<AgentRecord> Agents { get; set; } = new();

        public static int Reward(MissionRating rating) => BaseReward + rating switch
        {
            MissionRating.Unseen => UnseenBonus,
            MissionRating.Quiet => QuietBonus,
            _ => 0,
        };

        /// <summary>Kosten, um von dieser Stufe auf die nächste zu kommen.</summary>
        public static int UpgradeCost(int level) => level * UpgradeCostPerLevel;

        public AgentRecord Agent(string name) => Agents.Find(a => a.Name == name)
            ?? throw new ArgumentException($"Unbekannter Agent {name}");

        /// <summary>Überträgt den Stand auf die Missionsdaten. Neue Agenten übernehmen die Werte der Mission.</summary>
        public void Prepare(MissionData mission)
        {
            mission.Money = Money;
            foreach (var a in mission.Agents)
            {
                var record = Agents.Find(r => r.Name == a.Name);
                if (record == null)
                {
                    record = new AgentRecord { Name = a.Name, Legs = a.Legs, Heart = a.Heart };
                    Agents.Add(record);
                }
                a.Legs = record.Legs;
                a.Heart = record.Heart;
            }
        }

        /// <summary>Verbucht einen beendeten Einsatz. Beute zählt nur, wenn der Trupp entkommt.</summary>
        public void Finish(World world)
        {
            if (world.State == MissionState.Won)
            {
                Money = world.Money + Reward(world.Rating);
                Missions++;
                foreach (var u in world.Units)
                {
                    if (u.Faction == Faction.Agent && !u.Alive)
                    {
                        var record = Agent(u.Name);
                        record.Legs = 1;
                        record.Heart = 1;
                    }
                }
            }
            else
            {
                Money = world.Money - (world.LootTaken ? world.LootValue : 0);
            }
        }

        public bool TryUpgrade(string name, bool legs)
        {
            var record = Agent(name);
            int level = legs ? record.Legs : record.Heart;
            if (level >= MaxLevel || Money < UpgradeCost(level))
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
        public static Campaign FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<Campaign>(json) ?? new Campaign();
            }
            catch (JsonException)
            {
                return new Campaign();
            }
        }
    }
}
