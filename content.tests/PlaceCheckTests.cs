using System.Linq;
using Content.Places;
using Content.Schema;
using Content.Sheet;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    // WHAT A PLACE LETS YOU TRY, AND HOW HARD IT MAKES IT (Phase T).
    //
    // The verbs on the people standing in a place are the author's offer. These are the other
    // half: the things a character reaches for off their own sheet, which nobody offered them, and
    // what a place says about them is a difficulty and nothing else.
    //
    // That is the whole reason they are authorable at all. Each is the check primitive the kit
    // already runs, so "the guard can be leaned on, and he is stubborn" is a number in a campaign
    // file - and a fourth thing to try is engine work, exactly as a sixth verb is.
    public sealed class PlaceCheckTests
    {
        static Read<Place> Parse(string checks) =>
            PlaceReader.Parse(@"{
                ""id"": ""quay"",
                ""map"": ""quay"",
                ""standing"": [ { ""slot"": 1, ""entity"": ""norrel"" } ],
                ""checks"": " + checks + @"
            }", "quay.json");

        static Place Good(string checks)
        {
            Read<Place> read = Parse(checks);

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            return read.Value;
        }

        static string Why(string checks)
        {
            Read<Place> read = Parse(checks);

            Assert.False(read.Ok, "this was supposed to be refused");

            return string.Join("; ", read.Problems.Select(p => p.What));
        }


        [Fact]
        public void APlaceThatSaysNothingOffersNothingToTry()
        {
            Read<Place> read = PlaceReader.Parse(@"{
                ""id"": ""quay"", ""map"": ""quay"",
                ""standing"": [ { ""slot"": 1, ""entity"": ""norrel"" } ]
            }", "quay.json");

            Assert.True(read.Ok);
            Assert.Empty(read.Value.Checks);

            foreach (Check check in Checks.All) Assert.False(read.Value.Allows(check));
        }

        [Fact]
        public void APlaceSaysWhatMayBeTriedAndAgainstWhat()
        {
            Place quay = Good(@"{ ""persuade"": 11, ""perception"": 9 }");

            Assert.True(quay.Allows(Check.Persuade));
            Assert.True(quay.Allows(Check.Perception));
            Assert.False(quay.Allows(Check.Intimidate));

            Assert.Equal(Difficulty.Tricky, quay.Against(Check.Persuade));
            Assert.Equal(Difficulty.Standard, quay.Against(Check.Perception));
        }

        // the same thing is trivial in a tavern and formidable at a gate, and the scene is what
        // decides which - so the number is the place's and never the check's
        [Fact]
        public void TheSameCheckIsADifferentNumberInADifferentPlace()
        {
            Assert.Equal(Difficulty.Easy, Good(@"{ ""intimidate"": 7 }").Against(Check.Intimidate));
            Assert.Equal(Difficulty.Formidable,
                         Good(@"{ ""intimidate"": 15 }").Against(Check.Intimidate));
        }

        [Fact]
        public void APlaceThatDoesNotAllowItStillAnswersWithStandard()
        {
            Place quay = Good(@"{ ""persuade"": 11 }");

            Assert.False(quay.Allows(Check.Intimidate));
            Assert.Equal(Difficulty.Standard, quay.Against(Check.Intimidate));
        }

        // A FOURTH THING TO TRY IS ENGINE WORK, the same as a sixth verb is
        [Fact]
        public void SomethingOutsideTheThreeIsRefusedAndTheThreeAreOffered()
        {
            string why = Why(@"{ ""pickpocket"": 11 }");

            Assert.Contains("intimidate", why);
            Assert.Contains("persuade", why);
            Assert.Contains("perception", why);
        }

        [Fact]
        public void ADifficultyNothingCouldBeIsRefused()
        {
            foreach (string absurd in new[] { "0", "-4", "99", @"""hard""" })
                Assert.Contains("difficulty", Why($@"{{ ""persuade"": {absurd} }}"));
        }

        [Fact]
        public void ChecksThatAreNotAnObjectAreRefused()
        {
            Assert.Contains("persuade", Why(@"[ ""persuade"" ]"));
        }

        // a place with a check on it and nothing else in it is a place: there is something to do
        [Fact]
        public void APlaceWithNothingButSomethingToTryIsStillAPlace()
        {
            Read<Place> read = PlaceReader.Parse(@"{
                ""id"": ""crossing"", ""map"": ""crossing"",
                ""checks"": { ""perception"": 9 }
            }", "crossing.json");

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));
            Assert.True(read.Value.Allows(Check.Perception));
        }

        [Fact]
        public void APlaceWithNothingInItAtAllIsStillRefused()
        {
            Read<Place> read = PlaceReader.Parse(@"{
                ""id"": ""empty"", ""map"": ""empty""
            }", "empty.json");

            Assert.False(read.Ok);
        }
    }
}
