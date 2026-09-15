using System.Linq;
using Content.Encounters;
using Content.Schema;
using Core.Localization;
using Xunit;

namespace Content.Tests
{
    public sealed class CueTests
    {
        static Read<EncounterPlan> Parse(string cues) =>
            EncounterReader.Parse(@"{
                ""id"": ""yard"",
                ""map"": ""yard"",
                ""placements"": [ { ""slot"": 1, ""monster"": ""ghoul"" } ],
                ""cues"": " + cues + @"
            }", "yard.json");

        static EncounterPlan Good(string cues)
        {
            Read<EncounterPlan> read = Parse(cues);

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            return read.Value;
        }

        static string Why(string cues)
        {
            Read<EncounterPlan> read = Parse(cues);

            Assert.False(read.Ok, "this was supposed to be refused");

            return string.Join("; ", read.Problems.Select(p => p.What));
        }


        [Fact]
        public void AnOldCueStillReadsAndGainsAGesture()
        {
            Cue cue = Good(@"[ { ""when"": ""entered"", ""cue"": ""the_yard_opens"" } ]").Cues.Single();

            Assert.Equal(When.Entered, cue.WhenIt);
            Assert.Equal("the_yard_opens", cue.Id);
            Assert.Equal(Gesture.Push, cue.Does);
            Assert.False(cue.Hesitant);
        }


        [Theory]
        [InlineData("place", Gesture.Place)]
        [InlineData("slide", Gesture.Slide)]
        [InlineData("tap", Gesture.Tap)]
        [InlineData("reachbehind", Gesture.ReachBehind)]
        [InlineData("withdraw", Gesture.Withdraw)]
        [InlineData("turnpage", Gesture.TurnPage)]
        public void ACampaignCanNameAnyGestureTheEngineHas(string word, Gesture gesture)
        {
            Assert.Equal(gesture,
                         Good(@"[ { ""when"": ""entered"", ""cue"": ""x"", ""gesture"": """ + word +
                              @""" } ]").Cues.Single().Does);
        }

        [Fact]
        public void AGestureTheEngineDoesNotHaveIsRefusedAsAnimationWork()
        {
            string why = Why(@"[ { ""when"": ""entered"", ""cue"": ""x"", ""gesture"": ""juggle"" } ]");

            Assert.Contains("is not something the DM's hands can do", why);
            Assert.Contains("animation work, not campaign data", why);
            Assert.Contains("place", why);
        }


        [Fact]
        public void OnlyAPlacementCanHesitate()
        {
            Assert.True(Good(@"[ { ""when"": ""entered"", ""cue"": ""x"", ""gesture"": ""place"",
                                   ""hesitant"": true } ]").Cues.Single().Hesitant);

            Assert.Contains("cannot hesitate",
                            Why(@"[ { ""when"": ""entered"", ""cue"": ""x"",
                                      ""gesture"": ""turnpage"", ""hesitant"": true } ]"));
        }


        [Fact]
        public void AGestureThatHappensSomewhereTakesASpawnSlot()
        {
            Assert.Equal(2, Good(@"[ { ""when"": ""entered"", ""cue"": ""x"", ""gesture"": ""place"",
                                       ""at"": 2 } ]").Cues.Single().Slot);
        }

        [Fact]
        public void AGestureThatHappensNowhereInParticularIsRefusedASquare()
        {
            Assert.Contains("does not happen at a square",
                            Why(@"[ { ""when"": ""entered"", ""cue"": ""x"", ""gesture"": ""push"",
                                      ""at"": 2 } ]"));
        }


        [Theory]
        [InlineData("push", true)]
        [InlineData("tack", true)]
        [InlineData("turnpage", true)]
        [InlineData("write", true)]
        [InlineData("place", false)]
        [InlineData("slide", false)]
        [InlineData("tap", false)]
        [InlineData("reachbehind", false)]
        public void OnlyTheDeliveryGesturesCarryALine(string word, bool tells)
        {
            Cue cue = Good(@"[ { ""when"": ""entered"", ""cue"": ""x"", ""gesture"": """ + word +
                           @""" } ]").Cues.Single();

            Assert.Equal(tells, cue.Tells);
            Assert.Equal(tells, cue.LineKey("ashfall") != null);
        }

        [Fact]
        public void TheLineIsScopedByTheCampaignAndDerivedFromTheId()
        {
            Cue cue = Good(@"[ { ""when"": ""entered"", ""cue"": ""the_yard_opens"" } ]").Cues.Single();

            Assert.Equal("dialogue.dm.narration.ashfall.the_yard_opens", cue.LineKey("ashfall"));
            Assert.Equal("dialogue.dm.narration.other.the_yard_opens", cue.LineKey("other"));

            Assert.Equal(KeyConventions.WellFormed,
                         KeyConventions.Explain(cue.LineKey("ashfall")));
        }


        [Fact]
        public void TheCuesForAMomentComeBackInTheOrderTheAuthorWroteThem()
        {
            EncounterPlan plan = Good(@"[
                { ""when"": ""entered"", ""cue"": ""first"", ""gesture"": ""place"", ""at"": 1 },
                { ""when"": ""cleared"", ""cue"": ""last"" },
                { ""when"": ""entered"", ""cue"": ""second"", ""gesture"": ""tap"", ""at"": 1 }
            ]");

            Assert.Equal(new[] { "first", "second" },
                         plan.CuesFor(When.Entered).Select(c => c.Id).ToArray());

            Assert.Equal(new[] { "last" }, plan.CuesFor(When.Cleared).Select(c => c.Id).ToArray());
        }


        [Fact]
        public void EveryGestureThatTellsIsOneOfTheFourDeliveryGestures()
        {
            Assert.Equal(new[] { Gesture.Push, Gesture.Tack, Gesture.TurnPage, Gesture.Write },
                         System.Enum.GetValues<Gesture>().Where(g => g.Tells()).ToArray());
        }
    }
}
