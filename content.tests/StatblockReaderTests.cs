using System.Linq;
using Content.Monsters;
using Content.Schema;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Xunit;

namespace Content.Tests
{
    public class StatblockReaderTests
    {
        const string File = "ghoul.json";

        static Read<Statblock> Parse(string json, string campaign = "ashfall") =>
            StatblockReader.Parse(json, File, campaign);

        const string Good = @"{
            ""id"": ""ghoul"",
            ""tier"": ""rival"",
            ""vigor"": 8,
            ""defense"": 11,
            ""attributes"": { ""might"": ""d8"", ""grace"": ""d6"" },
            ""skills"": { ""blades"": ""d6"" },
            ""gear"": { ""id"": ""claw"", ""die"": ""d6"" },
            ""behaviour"": ""strongest_first""
        }";


        [Fact]
        public void AWholeStatblockComesBack()
        {
            Read<Statblock> read = Parse(Good);

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            Statblock block = read.Value;

            Assert.Equal(Tier.Rival, block.Tier);
            Assert.Equal(8, block.Vigor);
            Assert.Equal(11, block.Defense);
            Assert.Equal(Die.D8, block.Attributes[Attr.Might]);
            Assert.Equal(Die.D6, block.Attributes[Attr.Grace]);
            Assert.Equal(Die.D6, block.Skills[Skill.Blades]);
            Assert.Equal("claw", block.GearId);
            Assert.Equal(Die.D6, block.GearDie);
            Assert.Equal(Behaviours.StrongestFirst, block.Behaviour);
        }

        [Fact]
        public void TheCampaignScopesTheId()
        {
            Assert.Equal("ashfall.ghoul", Parse(Good).Value.Id);
            Assert.Equal("other.ghoul", Parse(Good, "other").Value.Id);
        }

        [Fact]
        public void AndNothingScopesItWhenThereIsNoCampaign()
        {
            Assert.Equal("ghoul", Parse(Good, campaign: null).Value.Id);
        }

        [Fact]
        public void AndTheKeyItNamesItselfWithFallsOutOfThat()
        {
            Actor ghoul = Parse(Good).Value.Create();

            Assert.Equal("actor.ashfall.ghoul.name", ghoul.NameKey);
            Assert.Equal(Core.Localization.KeyConventions.WellFormed,
                         Core.Localization.KeyConventions.Explain(ghoul.NameKey));
        }

        [Fact]
        public void AndItStampsOutAFreshActorEveryTime()
        {
            Statblock block = Parse(Good).Value;

            Actor first = block.Create();
            Actor second = block.Create();

            Assert.NotSame(first, second);

            first.Damage(4);

            Assert.Equal(8, second.Vigor);
        }


        [Fact]
        public void GearIsOptional_AndEmptyHandedIsARealStatblock()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""wisp"", ""vigor"": 4, ""defense"": 9,
                                            ""attributes"": { ""wits"": ""d8"" } }");

            Assert.True(read.Ok);
            Assert.Null(read.Value.GearId);
            Assert.Equal(Die.None, read.Value.GearDie);

            // pool.add drops absent dice, so this throw is one die, not a pool with a hole
            Assert.Equal(1, read.Value.Create().BuildPool(Attr.Wits).Count);
        }

        [Fact]
        public void TierDefaultsToRival_AndBehaviourToTheEnginesDefault()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""wisp"", ""vigor"": 4, ""defense"": 9 }");

            Assert.True(read.Ok);
            Assert.Equal(Tier.Rival, read.Value.Tier);
            Assert.Null(read.Value.Behaviour);
        }


        [Fact]
        public void SomethingThatIsNotJsonIsRefusedWithALine()
        {
            Read<Statblock> read = Parse("{ \"id\": \"ghoul\",,, }");

            Assert.False(read.Ok);
            Assert.Single(read.Problems);
            Assert.True(read.Problems[0].Line > 0, "a syntax error knows which line it is on");
            Assert.Contains(File, read.Problems[0].ToString());
        }

        [Fact]
        public void AListIsNotAStatblock()
        {
            Read<Statblock> read = Parse("[ 1, 2, 3 ]");

            Assert.False(read.Ok);
            Assert.Contains("array", read.Problems[0].What);
        }

        [Fact]
        public void ADieNobodyOwnsIsRefused_AndTheOnesThatExistAreOffered()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""ghoul"", ""vigor"": 8, ""defense"": 11,
                                            ""attributes"": { ""might"": ""d7"" } }");

            Assert.False(read.Ok);

            ContentProblem problem = read.Problems.Single();

            Assert.Equal("attributes.might", problem.Where);
            Assert.Contains("d7", problem.What);
            Assert.Contains("d8", problem.What);
            Assert.Contains("d12", problem.What);
        }

        [Fact]
        public void AnAttributeNobodyHasIsRefused()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""ghoul"", ""vigor"": 8, ""defense"": 11,
                                            ""attributes"": { ""charisma"": ""d8"" } }");

            Assert.False(read.Ok);
            Assert.Equal("attributes.charisma", read.Problems.Single().Where);
            Assert.Contains("might", read.Problems.Single().What);
        }

        // tier is by name not ordinal, or reordering the enum would silently re-tier every monster
        [Fact]
        public void ATierByItsNumberIsRefused()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""ghoul"", ""tier"": ""1"", ""vigor"": 8,
                                            ""defense"": 11 }");

            Assert.False(read.Ok);
            Assert.Equal("tier", read.Problems.Single().Where);
        }

        [Fact]
        public void ABehaviourThatIsNotInTheVocabularyIsRefused()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""ghoul"", ""vigor"": 8, ""defense"": 11,
                                            ""behaviour"": ""run_a_script"" }");

            Assert.False(read.Ok);
            Assert.Equal("behaviour", read.Problems.Single().Where);
            Assert.Contains(Behaviours.RabbleFirst, read.Problems.Single().What);
        }

        [Fact]
        public void AnIdWithADotInItIsRefused_BecauseTheCampaignAddsThat()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""ashfall.ghoul"", ""vigor"": 8, ""defense"": 11 }");

            Assert.False(read.Ok);
            Assert.Equal("id", read.Problems.Single().Where);
        }

        [Fact]
        public void AnIdThatWouldMakeABadKeyIsRefusedHere_NotThreeLayersLater()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""Ghoul"", ""vigor"": 8, ""defense"": 11 }");

            Assert.False(read.Ok);
            Assert.Equal("id", read.Problems.Single().Where);
        }

        [Fact]
        public void AFieldNobodyKnowsIsRefused()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""ghoul"", ""vigor"": 8, ""defense"": 11,
                                            ""defence"": 13 }");

            Assert.False(read.Ok);
            Assert.Equal("defence", read.Problems.Single().Where);
            Assert.Contains("defense", read.Problems.Single().What);
        }

        [Fact]
        public void MissingVigorAndDefenseAreBothReported()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""ghoul"" }");

            Assert.False(read.Ok);
            Assert.Equal(2, read.Problems.Count);
            Assert.Contains(read.Problems, p => p.Where == "vigor");
            Assert.Contains(read.Problems, p => p.Where == "defense");
        }

        [Fact]
        public void EveryProblemInOneFileComesBackTogether()
        {
            Read<Statblock> read = Parse(@"{
                ""id"": ""Ghoul"",
                ""tier"": ""legend"",
                ""vigor"": 0,
                ""defense"": 11,
                ""attributes"": { ""might"": ""d7"", ""charisma"": ""d6"" },
                ""behaviour"": ""sneaky""
            }");

            Assert.False(read.Ok);
            Assert.True(read.Problems.Count >= 6,
                        $"only {read.Problems.Count} came back: " +
                        string.Join(" | ", read.Problems.Select(p => p.ToString())));
        }

        [Fact]
        public void AndEveryOneNamesTheFile()
        {
            Read<Statblock> read = Parse(@"{ ""id"": ""Ghoul"", ""attributes"": { ""might"": ""d7"" } }");

            Assert.All(read.Problems, p => Assert.Equal(File, p.File));
            Assert.All(read.Problems, p => Assert.StartsWith(File + ":", p.ToString()));
        }
    }
}
