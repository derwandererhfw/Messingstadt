using System;
using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class GuardTypeTests
    {
        private const int Size = 40;

        private static MissionData Mission(double agentX, string type, double guardX = 2.5) => new()
        {
            Seed = 3, Item = new[] { 39.5, 39.5 }, ItemName = "Buch", Money = 1000,
            Exit = new ZoneData { X0 = 0, Y0 = 38, X1 = 1, Y1 = 39 },
            Agents = new() { new UnitData { Name = "A", Sprite = "Agent", X = agentX, Y = 5.5, Skill = World.SkillBookkeeper } },
            Guards = new() { new UnitData { Name = "W", Sprite = "W", Type = type, X = guardX, Y = 5.5, Facing = new[] { 1.0, 0.0 } } },
        };

        private static World NewWorld(MissionData m) => new(new MapData { Width = Size, Height = Size }, m);

        private static World Run(World w, int ticks)
        {
            for (int i = 0; i < ticks && w.State == MissionState.Running; i++)
            {
                w.Step();
            }
            return w;
        }

        [Fact]
        public void MissingTypeMeansWachmannAndUnknownTypeFails()
        {
            Assert.Equal(GuardType.Wachmann, GuardType.ForId(""));
            Assert.Throws<ArgumentException>(() => GuardType.ForId("drache"));
        }

        [Fact]
        public void HitPointsComeFromTheType()
        {
            Assert.Equal(GuardType.Werkschutz.Hp, NewWorld(Mission(30.5, "werkschutz")).Units[1].MaxHp);
            Assert.Equal(GuardType.Hund.Hp, NewWorld(Mission(30.5, "hund")).Units[1].Hp);
        }

        [Fact]
        public void RifleHitsFartherThanAPistol()
        {
            var rifle = NewWorld(Mission(11.5, "schuetze"));
            rifle.Units[1].GuardState = GuardState.Combat;
            rifle.Units[1].Alerted = true;
            Assert.True(Run(rifle, 40).Shots.Count > 0);
            var pistol = NewWorld(Mission(11.5, "wachmann"));
            pistol.Units[1].GuardState = GuardState.Combat;
            pistol.Units[1].Alerted = true;
            pistol.Units[1].Patrol.Clear();
            Run(pistol, 3);
            Assert.Empty(pistol.Shots);
        }

        [Fact]
        public void ShotgunDoesMoreDamageThanAPistol()
        {
            Assert.True(GuardType.Werkschutz.Damage > GuardType.Wachmann.Damage);
            Assert.True(GuardType.Werkschutz.WeaponRange < GuardType.Wachmann.WeaponRange);
        }

        [Fact]
        public void WerkschutzAndDogCannotBeBribed()
        {
            var w = NewWorld(Mission(4.5, "werkschutz"));
            Assert.False(w.OrderBribe(0, 1));
            var dog = NewWorld(Mission(4.5, "hund"));
            Assert.False(dog.OrderBribe(0, 1));
        }

        [Fact]
        public void BribingAnOfficerCostsMore()
        {
            var w = NewWorld(Mission(3.5, "offizier"));
            w.Units[1].Patrol.Clear();
            Assert.True(w.OrderBribe(0, 1));
            w.Step();
            Assert.True(w.Units[1].Bribed);
            Assert.Equal(1000 - GuardType.Offizier.BribeCost, w.Money);
        }

        [Fact]
        public void OfficerNoticesFaster()
        {
            static int Ticks(string type)
            {
                var w = NewWorld(Mission(5.5, type));
                Run(w, 1);
                return w.Units[1].NoticeTicks;
            }
            Assert.True(Ticks("offizier") < Ticks("wachmann"));
        }

        [Fact]
        public void OfficerCallsOthersWhenFightingStarts()
        {
            var m = Mission(5.5, "offizier");
            m.Guards.Add(new UnitData { Name = "Fern", Sprite = "W", X = 2.5, Y = 11.5, Facing = new[] { 0.0, 1.0 } });
            var w = NewWorld(m);
            Run(w, 40);
            Assert.NotEqual(GuardState.Patrol, w.Units[2].GuardState);
            Assert.Equal(MissionRating.Loud, w.Rating == MissionRating.Unseen ? MissionRating.Unseen : MissionRating.Loud);
        }

        [Fact]
        public void DogSmellsBehindItselfButOnlyClose()
        {
            // Agent hinter dem Hund: Ein Wachmann bemerkt das auf 2,4 Feldern nicht, der Hund schon.
            var dog = NewWorld(Mission(5.1, "hund", 7.5));
            var man = NewWorld(Mission(5.1, "wachmann", 7.5));
            Run(dog, 3);
            Run(man, 3);
            Assert.NotEqual(MissionRating.Unseen, dog.Rating);
            Assert.Equal(MissionRating.Unseen, man.Rating);
            Assert.Equal(MissionRating.Unseen, Run(NewWorld(Mission(1.5, "hund", 7.5)), 3).Rating);
        }

        [Fact]
        public void DogRunsUpAndBitesWithoutAShot()
        {
            var w = NewWorld(Mission(5.5, "hund"));
            Run(w, 120);
            Assert.True(w.Units[0].Hp < w.Units[0].MaxHp);
            Assert.DoesNotContain(w.Shots, s => s.ShooterId == 1);
        }

        [Fact]
        public void ShotsNameTheirShooter()
        {
            var w = NewWorld(Mission(5.5, "wachmann"));
            Run(w, 80);
            Assert.Contains(w.Shots, s => s.ShooterId == 1);
        }
    }
}
