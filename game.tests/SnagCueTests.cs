using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Game.Tray;
using Xunit;

namespace Game.Tests
{
    // the companion's cue, before there is a companion
    //
    // nothing says these lines yet, so what can be checked is the hook and the rate: that a
    // Snag on the felt produces a key a translator could be handed, that the key obeys the
    // grammar, and that the tally counts what actually happened rather than what was expected
    public class SnagCueTests
    {
        static readonly Die[] Starting = { Die.D8, Die.D6, Die.D6 };

        static TrayThrow Throw(params int[] values) =>
            new TrayResolution(Starting).Resolve(values);

        static SnagCue Cue(int seed = 4242) => new SnagCue(Starting, new SeededRng(seed));

        // ---- the hook ----

        // the whole point of M9: a Snag hands over a KEY, never words. if this ever returns
        // something a locale file could not be keyed by, dialogue has leaked into game/
        [Fact]
        public void ASnagProducesAKeyThatObeysTheGrammar()
        {
            string bark = Cue().Watch(Throw(1, 4, 3));

            Assert.NotNull(bark);
            Assert.Equal(KeyConventions.WellFormed, KeyConventions.Explain(bark));
            Assert.StartsWith($"dialogue.{SnagCue.Speaker}.bark.{SnagCue.Situation}.", bark);
        }

        [Fact]
        public void AQuietThrowSaysNothing()
        {
            var cue = Cue();

            Assert.Null(cue.Watch(Throw(4, 3, 2)));
            Assert.Null(cue.LastKey);
        }

        // two 1s is Trouble, which has its own bark and is deliberately not this one
        [Fact]
        public void TroubleIsNotASnag()
        {
            var cue = Cue();

            Assert.Null(cue.Watch(Throw(1, 1, 3)));
            Assert.Equal(0, cue.Snags);
            Assert.Equal(1, cue.Throws);
        }

        // ---- the tally ----

        [Fact]
        public void EveryThrowIsCounted_NotOnlyTheOnesThatSnag()
        {
            var cue = Cue();

            cue.Watch(Throw(1, 4, 3));
            cue.Watch(Throw(4, 3, 2));
            cue.Watch(Throw(5, 1, 3));

            Assert.Equal(3, cue.Throws);
            Assert.Equal(2, cue.Snags);
            Assert.Equal(2 / 3.0, cue.Rate, 10);
        }

        // bigger dice snag less, so a count carried across a change of shapes is measuring a
        // table that no longer exists
        [Fact]
        public void ChangingThePoolStartsTheCountAgain()
        {
            var cue = Cue();
            cue.Watch(Throw(1, 4, 3));

            cue.Reset(new[] { Die.D12, Die.D10, Die.D8 });

            Assert.Equal(0, cue.Throws);
            Assert.Equal(0, cue.Snags);
            Assert.Null(cue.LastKey);
            Assert.Equal(PoolOdds.Snag(new[] { Die.D12, Die.D10, Die.D8 }), cue.Expected, 10);
        }

        [Fact]
        public void TheExpectedRateIsTheClosedForm_NotAMeasurement() =>
            Assert.Equal(PoolOdds.Snag(Starting), Cue().Expected, 10);

        [Fact]
        public void AnEmptyPoolIsRefused() =>
            Assert.Throws<ArgumentNullException>(() => new SnagCue(null));

        // ---- repeatability ----

        // the rng is a seam so a seeded session says the same things twice - which is what
        // makes "re-run with the same seed" a debugging technique rather than a hope
        [Fact]
        public void TheSameSeedPicksTheSameLines()
        {
            IEnumerable<string> Session()
            {
                var cue = Cue(seed: 99);
                foreach (int face in new[] { 1, 1, 1, 1 })
                    yield return cue.Watch(Throw(face, 4, 3));
            }

            Assert.Equal(Session().ToList(), Session().ToList());
        }
    }
}
