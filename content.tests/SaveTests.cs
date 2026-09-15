using System;
using System.IO;
using System.Linq;
using Content.Saves;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    public sealed class SaveTests : IDisposable
    {
        readonly string _folder;

        public SaveTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "saves-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_folder, recursive: true); } catch { }
        }

        const string File = "slot1.json";

        static Read<SaveGame> Parse(string json) => SaveReader.Parse(json, File);

        static SaveGame Somewhere()
        {
            var save = new SaveGame
            {
                Campaign = "ashfall",
                CampaignFormat = 1,
                Chapter = "the_yard",
                Encounter = "ash_yard",
                Round = 3,
                Turn = 0,
                ActionsLeft = 1,
                Hero = new SavedActor
                {
                    Id = "barbarian",
                    Vigor = 9,
                    Nerve = 2,
                    Seat = 0,
                    Initiative = 7,
                    Notches = 1,
                    Wielded = "cold_iron_axe",
                    Worn = "scale_coat",
                    X = 1,
                    Y = 1,
                },
            };

            save.Hero.Conditions.Add(Condition.Winded);
            save.Hero.Satchel.Add("claw_necklace");

            var ghoul = new SavedActor
            {
                Id = "ashfall.ghoul",
                Vigor = 4,
                Seat = 1,
                Slot = 2,
                Initiative = 5,
                X = 7,
                Y = 0,
            };

            ghoul.Conditions.Add(Condition.Reeling);
            save.Foes.Add(ghoul);

            save.Felt.Add(new SavedDie("attr.might.name", Die.D6, 4));
            save.Felt.Add(new SavedDie("skill.blades.name", Die.D8, 5));
            save.Felt.Add(new SavedDie("gear.axe.name", Die.D6, 2));

            return save;
        }

        static SaveGame RoundTrip(SaveGame save)
        {
            Read<SaveGame> read = Parse(SaveWriter.Write(save));

            Assert.True(read.Ok, string.Join(" | ", read.Problems.Select(p => p.ToString())));

            return read.Value;
        }


        [Fact]
        public void AMomentInAFightSurvivesTheRoundTrip()
        {
            SaveGame back = RoundTrip(Somewhere());

            Assert.Equal("ashfall", back.Campaign);
            Assert.Equal("the_yard", back.Chapter);
            Assert.Equal("ash_yard", back.Encounter);
            Assert.Equal(3, back.Round);
            Assert.Equal(0, back.Turn);
            Assert.Equal(1, back.ActionsLeft);
        }

        [Fact]
        public void TheHeroComesBackWithEverythingThatHadHappenedToHim()
        {
            SavedActor hero = RoundTrip(Somewhere()).Hero;

            Assert.Equal("barbarian", hero.Id);
            Assert.Equal(9, hero.Vigor);
            Assert.Equal(2, hero.Nerve);
            Assert.Equal(1, hero.Notches);
            Assert.Equal(7, hero.Initiative);
            Assert.Equal(0, hero.Seat);
            Assert.Equal("cold_iron_axe", hero.Wielded);
            Assert.Equal("scale_coat", hero.Worn);
            Assert.Equal(new[] { Condition.Winded }, hero.Conditions);
            Assert.Equal(new[] { "claw_necklace" }, hero.Satchel);
            Assert.Equal(1, hero.X);
            Assert.Equal(1, hero.Y);
        }

        [Fact]
        public void AFoeIsNamedByTheSpawnSlotItStartedOn()
        {
            SavedActor ghoul = Assert.Single(RoundTrip(Somewhere()).Foes);

            Assert.Equal("ashfall.ghoul", ghoul.Id);
            Assert.Equal(2, ghoul.Slot);
            Assert.Equal(1, ghoul.Seat);
            Assert.Equal(4, ghoul.Vigor);
            Assert.Equal(new[] { Condition.Reeling }, ghoul.Conditions);
        }

        [Fact]
        public void TheDiceOnTheFeltComeBackShowingWhatTheyShowed()
        {
            var felt = RoundTrip(Somewhere()).Felt;

            Assert.Equal(3, felt.Count);
            Assert.Equal("attr.might.name", felt[0].Trait);
            Assert.Equal(Die.D6, felt[0].Die);
            Assert.Equal(4, felt[0].Value);
        }

        [Fact]
        public void AndTheThrowTheyAddUpToIsTheOneThatWasSaved()
        {
            PoolResult was = Reading(Somewhere());
            PoolResult now = Reading(RoundTrip(Somewhere()));

            Assert.Equal(was.Total, now.Total);
            Assert.Equal(was.Impact, now.Impact);
            Assert.Equal(was.Ones, now.Ones);
            Assert.Equal(9, now.Total);
            Assert.Equal(Die.D6, now.Impact);
        }

        static PoolResult Reading(SaveGame save) =>
            PoolResult.From(save.Felt.Select(d => (d.Trait, d.Die, d.Value)).ToList());


        [Fact]
        public void TheFileIsIndentedAndSpellsItsWordsTheWayACampaignFileDoes()
        {
            string json = SaveWriter.Write(Somewhere());

            Assert.Contains("\n", json);
            Assert.Contains("\"winded\"", json);
            Assert.Contains("\"d8\"", json);
            Assert.Contains("\"at\"", json);
        }

        [Fact]
        public void ChangingTheHerosVigorByHandChangesTheHerosVigor()
        {
            string json = SaveWriter.Write(Somewhere()).Replace("\"vigor\": 9", "\"vigor\": 2");

            Read<SaveGame> read = Parse(json);

            Assert.True(read.Ok);
            Assert.Equal(2, read.Value.Hero.Vigor);
        }

        [Fact]
        public void ASaveGoesToADiskAndComesBackOffOne()
        {
            string path = Path.Combine(_folder, File);

            Assert.Null(SaveWriter.To(path, Somewhere()));

            Read<SaveGame> read = SaveReader.From(path);

            Assert.True(read.Ok);
            Assert.Equal("ash_yard", read.Value.Encounter);
        }

        [Fact]
        public void ASaveThatIsNotThereIsASentenceAndNotAnException()
        {
            Read<SaveGame> read = SaveReader.From(Path.Combine(_folder, "nothing.json"));

            Assert.False(read.Ok);
            Assert.False(read.Any);
            Assert.NotEmpty(read.Problems);
        }


        [Fact]
        public void AFileThatIsNotJsonIsTheOnlyThingThatStopsALoad()
        {
            Read<SaveGame> read = Parse("{ \"campaign\": \"ashfall\",");

            Assert.False(read.Ok);
            Assert.False(read.Any);
            Assert.Contains(read.Problems, p => p.Line > 0);
        }

        [Fact]
        public void ACorruptFieldIsNamedAndTheSaveStillLoads()
        {
            string json = SaveWriter.Write(Somewhere()).Replace("\"vigor\": 9", "\"vigor\": \"lots\"");

            Read<SaveGame> read = Parse(json);

            Assert.False(read.Ok);
            Assert.True(read.Any);
            Assert.Contains(read.Problems, p => p.Where == "hero.vigor" && p.What.Contains("read as"));
            Assert.Equal("barbarian", read.Value.Hero.Id);
        }

        [Fact]
        public void AFieldThisBuildHasNeverHeardOfIsNamedAndIgnored()
        {
            string json = SaveWriter.Write(Somewhere())
                                    .Replace("\"round\": 3", "\"weather\": \"rain\",\n\"round\": 3");

            Read<SaveGame> read = Parse(json);

            Assert.True(read.Any);
            Assert.Equal(3, read.Value.Round);
            Assert.Contains(read.Problems, p => p.Where == "weather");
        }

        [Fact]
        public void ASaveFromALaterBuildIsReadAsFarAsItCanBe()
        {
            string json = SaveWriter.Write(Somewhere())
                                    .Replace($"\"format\": {SaveFormat.Current}",
                                             $"\"format\": {SaveFormat.Current + 1}");

            Read<SaveGame> read = Parse(json);

            Assert.True(read.Any);
            Assert.Equal("ashfall", read.Value.Campaign);
            Assert.Contains(read.Problems, p => p.Where == "format");
        }

        [Fact]
        public void ADieShowingAFaceItDoesNotHaveIsClampedAndNamed()
        {
            string json = SaveWriter.Write(Somewhere()).Replace("\"value\": 4", "\"value\": 40");

            Read<SaveGame> read = Parse(json);

            Assert.True(read.Any);
            Assert.Equal(6, read.Value.Felt[0].Value);
            Assert.Contains(read.Problems, p => p.What.Contains("cannot show 40"));
        }

        [Fact]
        public void ADieThatIsNotADieIsLeftOffTheFeltRatherThanGuessedAt()
        {
            string json = SaveWriter.Write(Somewhere()).Replace("\"die\": \"d8\"", "\"die\": \"d7\"");

            Read<SaveGame> read = Parse(json);

            Assert.True(read.Any);
            Assert.Equal(2, read.Value.Felt.Count);
            Assert.Contains(read.Problems, p => p.What.Contains("not a die"));
        }

        [Fact]
        public void AConditionThisBuildDoesNotHaveIsLeftOffRatherThanStoppingTheLoad()
        {
            string json = SaveWriter.Write(Somewhere()).Replace("\"winded\"", "\"cursed\"");

            Read<SaveGame> read = Parse(json);

            Assert.True(read.Any);
            Assert.Empty(read.Value.Hero.Conditions);
            Assert.Contains(read.Problems, p => p.Where.StartsWith("hero.conditions["));
        }

        [Fact]
        public void AnActorWithNoIdIsLeftOutAndSaidSoRatherThanLoadedAsNobody()
        {
            string json = SaveWriter.Write(Somewhere())
                                    .Replace("\"id\": \"ashfall.ghoul\"", "\"id\": \"\"");

            Read<SaveGame> read = Parse(json);

            Assert.True(read.Any);
            Assert.Empty(read.Value.Foes);
            Assert.Contains(read.Problems, p => p.What.Contains("nobody is named here"));
        }

        [Fact]
        public void ASquareThatIsNotASquareLeavesThatOneOffTheBoard()
        {
            Read<SaveGame> read = Parse(@"{
                ""campaign"": ""ashfall"",
                ""round"": 2,
                ""foes"": [ { ""id"": ""ashfall.ghoul"", ""vigor"": 4, ""at"": [ 7 ] } ]
            }");

            Assert.True(read.Any);

            SavedActor ghoul = Assert.Single(read.Value.Foes);

            Assert.False(ghoul.OnTheBoard);
            Assert.Contains(read.Problems, p => p.Where.EndsWith(".at"));
        }


        [Fact]
        public void ASaveTakenBetweenFightsCarriesWhereYouAreAndNothingElse()
        {
            var save = new SaveGame
            {
                Campaign = "ashfall",
                Chapter = "the_yard",
                Hero = new SavedActor { Id = "barbarian", Vigor = 12 },
            };

            SaveGame back = RoundTrip(save);

            Assert.False(back.MidFight);
            Assert.Empty(back.Foes);
            Assert.Empty(back.Felt);
            Assert.Equal(12, back.Hero.Vigor);
        }
    }
}
