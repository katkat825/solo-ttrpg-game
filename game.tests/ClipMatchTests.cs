using Game.Board;
using Xunit;

namespace Game.Tests
{
    // FINDING THE CLIP A MANIFEST NAMED, IN THE LIST THE ENGINE ACTUALLY IMPORTED
    // (MINIS_AND_ART.md A1).
    //
    // A pack author reads a clip name off the screen in their modelling tool and types it into a
    // manifest. What arrives in an `AnimationPlayer` is what the exporter wrote and the importer
    // then made of it, and the three do not always agree about the spelling. Getting this wrong in
    // either direction is bad in a way that is miserable to debug by looking at a miniature: too
    // strict and every supplied model animates on nothing while its author is certain they typed
    // the name correctly; too loose and a piece plays the WRONG clip, which looks like a rig
    // problem rather than a lookup one.
    //
    // IT IS TESTED HERE BECAUSE IT NEEDS NO ENGINE. `ClipMatch` is string matching, and
    // `game.tests` reaches anything in `game/` that runs without Godot behind it - the same
    // boundary `NudgeThenRethrowTests` and `MiniStepTests` sit on.
    public sealed class ClipMatchTests
    {
        static readonly string[] Blender =
        {
            "Armature|Idle", "Armature|Walk", "Armature|Death_A",
        };

        static readonly string[] Plain = { "Idle", "Walk_A", "Death_A" };

        // ---- the case that needs no justification ----

        [Fact]
        public void TheNameExactlyIsTheAnswerWheneverItIsThere()
        {
            Assert.Equal("Walk_A", ClipMatch.In(Plain, "Walk_A"));
            Assert.Equal("Death_A", ClipMatch.In(Plain, "Death_A"));
        }

        // AN EXACT MATCH BEATS A LOOSER ONE, always - which matters when a file carries both
        // spellings, because otherwise the answer would depend on enumeration order
        [Fact]
        public void AnExactMatchWinsOverAQualifiedOne()
        {
            Assert.Equal("Walk", ClipMatch.In(new[] { "Armature|Walk", "Walk" }, "Walk"));
        }

        // ---- and the three widenings, each narrower than a guess ----

        [Fact]
        public void TheSameNameInADifferentCaseIsTheSameClip()
        {
            Assert.Equal("Walk_A", ClipMatch.In(Plain, "walk_a"));
        }

        // `Armature|Walk` is the exporter saying which rig the action was on, which is a fact
        // about the file rather than about the motion
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

        // and from the other side, for an author who copied the qualified name out of Blender
        // while the importer dropped it
        [Fact]
        public void AnAuthorWhoCopiedTheQualifiedNameStillFindsTheClip()
        {
            Assert.Equal("Walk_A", ClipMatch.In(Plain, "Armature|Walk_A"));
        }

        // ---- and the step that is deliberately not taken ----

        // A SUBSTRING MATCH WOULD MAKE `Walk` FIND `Walk_Backwards`, which is a piece animating on
        // the wrong clip - worse than one animating on none, because nothing about it looks like a
        // mistake
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

        // an empty answer is the procedural motion, which is missing polish and never a broken
        // fight - so none of these may be an exception
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

        // ---- the helper the widening is built on ----

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

        // a name that ENDS in a qualifier has nothing after it, so there is nothing to strip
        [Fact]
        public void ANameEndingInAQualifierIsLeftAlone()
        {
            Assert.Equal("Walk|", ClipMatch.Unqualified("Walk|"));
        }
    }
}
