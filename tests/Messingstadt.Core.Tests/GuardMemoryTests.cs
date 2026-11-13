using System.Collections.Generic;
using System.Linq;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class GuardMemoryTests
    {
        private static UnitData Agent(double x, double y = 5.5, string skill = "", params string[] items) =>
            new() { Name = "A", Sprite = "Agent", X = x, Y = y, Skill = skill, Items = items.Length > 0 ? items.ToList() : null };

        private static UnitData Guard(string role, string person, double x, double y = 5.5, string type = "", double fx = 1, double fy = 0) =>
            new() { Name = role, Person = person, Sprite = "Wachmann", Type = type, X = x, Y = y, Facing = new[] { fx, fy } };

        private static MissionData Mission(UnitData[] agents, params UnitData[] guards) => new()
        {
            Seed = 3, Item = new[] { 39.5, 39.5 }, ItemName = "Buch", Money = 1000,
            Exit = new ZoneData { X0 = 0, Y0 = 38, X1 = 1, Y1 = 39 },
            Agents = new(agents), Guards = new(guards), Catalog = Catalog,
        };

        private static World NewWorld(MissionData m, params SolidData[] solids) =>
            new(new MapData { Width = 40, Height = 40, Solids = new(solids) }, m);

        private static readonly ItemCatalog Catalog = Loader.ParseCatalog(System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot(), "data", "waffen.json")));

        private static string RepoRoot()
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "README.md")))
            {
                dir = dir.Parent;
            }
            return dir!.FullName;
        }

        private static World Run(World w, int ticks)
        {
            for (int i = 0; i < ticks && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            return w;
        }

        // ---------- Namen ----------

        [Fact]
        public void PersonComesFromTheMissionAndRoleStaysTheName()
        {
            var w = NewWorld(Mission(new[] { Agent(35.5, 35.5) }, Guard("Schließer", "Krüger", 5.5)));
            Assert.Equal("Schließer", w.Units[1].Name);
            Assert.Equal("Krüger", w.Units[1].Person);
        }

        [Fact]
        public void ReinforcementsCarryTheirNamesInOrder()
        {
            var m = Mission(new[] { Agent(8.5) }, Guard("Posten", "Krüger", 2.5));
            m.Reinforcements = new ReinforcementData { X = 30.5, Y = 30.5, Sprite = "W", FirstDelay = 1, Interval = 1, Count = 3, Persons = new() { "Behrens", "Tietz" } };
            var w = NewWorld(m);
            w.OrderAttack(0, 1);
            Run(w, 400);
            var came = w.Units.Where(u => u.Reinforcement).ToList();
            Assert.Equal(3, came.Count);
            Assert.Equal(new[] { "Behrens", "Tietz", "" }, came.Select(u => u.Person));
        }

        // ---------- Rufe ----------

        [Fact]
        public void AColleagueWhoSeesAGuardFallCallsHisNameOnce()
        {
            var w = NewWorld(Mission(new[] { Agent(9.5) }, Guard("Posten", "Krüger", 5.5), Guard("Streife", "Behrens", 3.5)));
            w.Units[1].Hp = 1;
            w.OrderAttack(0, 1);
            Run(w, 200);
            Assert.False(w.Units[1].Alive);
            var call = Assert.Single(w.Calls, c => c.Kind == CallKind.Fallen);
            Assert.Equal(2, call.SpeakerId);
            Assert.Equal(1, call.SubjectId);
        }

        [Fact]
        public void NobodyCallsWhenNobodySeesItOrTheFallenHasNoName()
        {
            var wall = new SolidData { X0 = 4, Y0 = 0, X1 = 5, Y1 = 20 };
            var hidden = NewWorld(Mission(new[] { Agent(9.5) }, Guard("Posten", "Krüger", 6.5), Guard("Streife", "Behrens", 2.5)), wall);
            hidden.Units[1].Hp = 1;
            hidden.OrderAttack(0, 1);
            Run(hidden, 200);
            Assert.False(hidden.Units[1].Alive);
            Assert.DoesNotContain(hidden.Calls, c => c.Kind == CallKind.Fallen);

            var nameless = NewWorld(Mission(new[] { Agent(9.5) }, Guard("Posten", "", 5.5), Guard("Streife", "Behrens", 3.5)));
            nameless.Units[1].Hp = 1;
            nameless.OrderAttack(0, 1);
            Run(nameless, 200);
            Assert.Empty(nameless.Calls);
        }

        [Fact]
        public void GuardsWhoMeetOnPatrolGreetByNameButNotAllTheTime()
        {
            var m = Mission(new[] { Agent(35.5, 35.5) }, Guard("Streife 1", "Krüger", 5.5), Guard("Posten", "Behrens", 10.5, 6.5));
            m.Guards[0].Patrol = new List<double[]> { new[] { 12.0, 5.0 }, new[] { 5.0, 5.0 } };
            var w = NewWorld(m);
            Run(w, 500);
            var greetings = w.Calls.Where(c => c.Kind == CallKind.Greeting).ToList();
            Assert.InRange(greetings.Count, 1, 2);
            Assert.Equal(1, greetings[0].SpeakerId);
            Assert.Equal(2, greetings[0].SubjectId);
            Assert.Equal(MissionRating.Unseen, w.Rating);
        }

        [Fact]
        public void CallsDoNotChangeTheCourseOfTheMission()
        {
            static ulong Play(string person)
            {
                var m = Mission(new[] { Agent(9.5) }, Guard("Posten", person, 5.5), Guard("Streife", person, 3.5));
                m.Guards[1].Patrol = new List<double[]> { new[] { 6.0, 5.0 }, new[] { 2.0, 5.0 } };
                var w = NewWorld(m);
                w.OrderAttack(0, 1);
                Run(w, 300);
                return w.StateHash();
            }
            Assert.Equal(Play(""), Play("Krüger"));
        }

        // ---------- Schicksal ----------

        [Fact]
        public void OutcomesTellDeadFromKnockedOutBribedAlertedAndUnaware()
        {
            var m = Mission(
                new[] { Agent(9.5, 5.5, World.SkillBookkeeper, "pistole", "totschlaeger") },
                Guard("Tot", "Krüger", 12.5, 5.5, fx: 1),
                Guard("Nieder", "Behrens", 9.5, 8.5, fx: 0, fy: 1),
                Guard("Gekauft", "Tietz", 9.5, 2.5, fx: 0, fy: -1),
                Guard("Ahnungslos", "Wolff", 30.5, 30.5),
                Guard("Hund", "", 31.5, 30.5, "hund"));
            var w = NewWorld(m);
            Assert.True(w.OrderBribe(0, 3));
            Run(w, 60);
            Assert.True(w.SelectWeapon(0, 1));
            w.OrderAttack(0, 2);
            Run(w, 120);
            Assert.True(w.SelectWeapon(0, 0));
            w.Units[1].Hp = 1;
            w.OrderAttack(0, 1);
            Run(w, 200);
            var fates = w.GuardOutcomes().ToDictionary(o => o.Role);
            Assert.Equal(GuardFate.Dead, fates["Tot"].Fate);
            Assert.Equal("Krüger", fates["Tot"].Person);
            Assert.Equal("wachmann", fates["Tot"].Type);
            Assert.Equal(GuardFate.KnockedOut, fates["Nieder"].Fate);
            Assert.Equal(GuardFate.Bribed, fates["Gekauft"].Fate);
            Assert.Equal(GuardFate.Unaware, fates["Ahnungslos"].Fate);
            Assert.Equal("hund", fates["Hund"].Type);
            Assert.All(fates.Values, o => Assert.False(o.Reinforcement));
        }

        [Fact]
        public void AGuardWhoLookedAndCalmedDownCountsAsAlerted()
        {
            var w = NewWorld(Mission(new[] { Agent(35.5, 35.5) }, Guard("Posten", "Krüger", 5.5)));
            w.RaiseAlarm(6500, 5500);
            Run(w, 400);
            Assert.Equal(GuardState.Patrol, w.Units[1].GuardState);
            Assert.Equal(GuardFate.Alerted, w.GuardOutcomes()[0].Fate);
        }

        [Fact]
        public void ReinforcementsAreMarkedInTheOutcome()
        {
            var m = Mission(new[] { Agent(8.5) }, Guard("Posten", "Krüger", 2.5));
            m.Reinforcements = new ReinforcementData { X = 30.5, Y = 30.5, Sprite = "W", FirstDelay = 1, Interval = 1, Count = 1, Persons = new() { "Behrens" } };
            var w = NewWorld(m);
            w.OrderAttack(0, 1);
            Run(w, 300);
            var last = w.GuardOutcomes()[^1];
            Assert.True(last.Reinforcement);
            Assert.Equal("Behrens", last.Person);
        }
    }
}
