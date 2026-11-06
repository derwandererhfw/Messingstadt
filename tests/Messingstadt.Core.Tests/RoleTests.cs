using Messingstadt.Core;
using Xunit;

namespace Messingstadt.Core.Tests
{
    public class RoleTests
    {
        [Fact]
        public void Role_FromMissionDataWins()
        {
            Assert.Equal("Truppführerin", new UnitData { Role = "Truppführerin" }.RoleName());
            Assert.Equal("Kurierin", new UnitData { Role = "Kurierin", Skill = World.SkillBookkeeper }.RoleName());
        }

        [Theory]
        [InlineData(World.SkillLocksmith, "Schlosser")]
        [InlineData(World.SkillBookkeeper, "Buchhalter")]
        [InlineData("", "Agent")]
        public void WithoutRole_TheSkillNamesTheJob(string skill, string expected)
        {
            Assert.Equal(expected, new UnitData { Skill = skill }.RoleName());
        }

        [Fact]
        public void Role_IsReadFromMissionJson()
        {
            var mission = Loader.ParseMission("{\"agents\":[{\"name\":\"Greta\",\"role\":\"Truppführerin\"}]}");
            Assert.Equal("Truppführerin", mission.Agents[0].RoleName());
        }
    }
}
