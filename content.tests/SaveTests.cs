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
    // SAVE AND LOAD (CONTENT_PIPELINE.md P6).
    //
    // The two things worth pinning are the two the milestone asks for by name. One: a save
    // round-trips - what goes in comes out, including the faces lying on the felt. Two: it
    // DEGRADES. "Corrupt one field and confirm it degrades with a legible message rather than
    // crashing" is a test and not a hope, and so is "open the save in a text editor, change the
    // hero's Vigor by hand, reload, and see the change".
    //
    // Everything here is text in and text out, because that is what a save is. Nothing needs a
    // table, which is the point of `Content.Saves` being in this assembly at all.
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
            try { Directory.Delete(_folder, recursive: true); } catch { /* a temp folder */ }
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

        // ---- what goes in comes out ----

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

        // THE FELT, AND WHAT IT MEANS. The save carries faces and nothing else; the verdict is
        // read back with the same arithmetic the table runs, which is the whole of SEAMS.md
        // section 9's "state shaped for save/load degrades; it doesn't throw" - there is no second
        // field to disagree with the first
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

        // ---- hand-editable, which is a promise about the FILE ----

        [Fact]
        public void TheFileIsIndentedAndSpellsItsWordsTheWayACampaignFileDoes()
        {
            string json = SaveWriter.Write(Somewhere());

            Assert.Contains("\n", json);
            Assert.Contains("\"winded\"", json);
            Assert.Contains("\"d8\"", json);
            Assert.Contains("\"at\"", json);
        }

        // "Open the save in a text editor, change the hero's Vigor by hand, reload, and see the
        // change" - P6's verify, with the text editor played by a string replace
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

        // ---- and it degrades ----

        // THE ONE FATAL CASE, and the reason it is fatal: there is nothing to degrade TO
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

            // NOT OK AND STILL THERE, which is the third state a save needs: something was not
            // understood, and the player's afternoon opens anyway
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

        // A DIE SHOWING A FACE IT DOES NOT HAVE is the likeliest hand-edit mistake there is, and
        // the marks drawn from it would be a claim the rules never made
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
            // written by hand rather than by editing the writer's output, because a
            // half-written square is a shape the writer cannot produce - which is the point
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

        // ---- and a save with no fight in it is still a save ----

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
