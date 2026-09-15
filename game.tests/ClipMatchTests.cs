using Game.Board;
using Xunit;

namespace Game.Tests
{
    public sealed class ClipMatchTests
    {
        static readonly string[] Blender =
        {
            "Armature|Idle", "Armature|Walk", "Armature|Death_A",
        };

        static readonly string[] Plain = { "Idle", "Walk_A", "Death_A" };


        [Fact]
        public void TheNameExactlyIsTheAnswerWheneverItIsThere()
        {
            Assert.Equal("Walk_A", ClipMatch.In(Plain, "Walk_A"));
            Assert.Equal("Death_A", ClipMatch.In(Plain, "Death_A"));
        }

        // an exact match always beats a looser one, or the answer would depend on enumeration order
        [Fact]
        public void AnExactMatchWinsOverAQualifiedOne()
        {
            Assert.Equal("Walk", ClipMatch.In(new[] { "Armature|Walk", "Walk" }, "Walk"));
        }


        [Fact]
        public void TheSameNameInADifferentCaseIsTheSameClip()
        {
            Assert.Equal("Walk_A", ClipMatch.In(Plain, "walk_a"));
        }

        [Fact]
        public void AQualifierTheExporterAddedIsNotPartOfTheName()
        {
            Assert.Equal("Armature|Walk", ClipMatch.In(Blender, "Walk"));
            Assert.Equal("Armature|Death_A", ClipMatch.In(Blender, "death_a"));
        }

        [Fact]
        public void ALibraryPrefixTheImporterAddedIsNotEither()
        {
            Assert.Equal("library/Walk", ClipMatch.In(new[] { "library/Walk" }, "Walk"));
        }

        [Fact]
        public void AnAuthorWhoCopiedTheQualifiedNameStillFindsTheClip()
        {
            Assert.Equal("Walk_A", ClipMatch.In(Plain, "Armature|Walk_A"));
        }


        // no substring match: "walk" must not find "walk_backwards", worse than matching nothing
        [Fact]
        public void AClipThatMerelyContainsTheNameIsNotTheClip()
        {
            Assert.Equal("", ClipMatch.In(new[] { "Walk_Backwards", "Idle_Dead" }, "Walk"));
            Assert.Equal("", ClipMatch.In(new[] { "Idle_Dead" }, "Idle"));
        }

        [Fact]
        public void ANameNothingResemblesComesBackEmptyRatherThanAsAGuess()
        {
            Assert.Equal("", ClipMatch.In(Plain, "Boogie"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NothingNamedMatchesNothing(string wanted)
        {
            Assert.Equal("", ClipMatch.In(Plain, wanted));
        }

        [Fact]
        public void AModelWithNoClipsAtAllIsNotAnException()
        {
            Assert.Equal("", ClipMatch.In(new string[0], "Walk"));
            Assert.Equal("", ClipMatch.In(null, "Walk"));
        }

        [Fact]
        public void AnEmptyNameInTheModelsOwnListIsSkippedRatherThanMatched()
        {
            Assert.Equal("Walk", ClipMatch.In(new[] { "", null, "Walk" }, "Walk"));
        }


        [Theory]
        [InlineData("Armature|Walk", "Walk")]
        [InlineData("library/Walk", "Walk")]
        [InlineData("CharacterArmature|Death_A", "Death_A")]
        [InlineData("Walk", "Walk")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void TheQualifierIsWhateverCameBeforeTheLastBarOrSlash(string clip, string bare)
        {
            Assert.Equal(bare, ClipMatch.Unqualified(clip));
        }

        [Fact]
        public void ANameEndingInAQualifierIsLeftAlone()
        {
            Assert.Equal("Walk|", ClipMatch.Unqualified("Walk|"));
        }
    }
}
