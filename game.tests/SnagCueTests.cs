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
    public class SnagCueTests
    {
        static readonly Die[] Starting = { Die.D8, Die.D6, Die.D6 };

        static TrayThrow Throw(params int[] values) =>
            new TrayResolution(Pool.Of(
                ("attr.might.name", Starting[0]),
                ("skill.blades.name", Starting[1]),
                ("gear.axe.name", Starting[2]))).Resolve(values);

        static SnagCue Cue(int seed = 4242) => new SnagCue(Starting, new SeededRng(seed));


        // a snag hands over a key, never words; anything unkeyable means dialogue leaked into game/
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

        [Fact]
        public void TroubleIsNotASnag()
        {
            var cue = Cue();

            Assert.Null(cue.Watch(Throw(1, 1, 3)));
            Assert.Equal(0, cue.Snags);
            Assert.Equal(1, cue.Throws);
        }


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


        // the tally must survive the tray's per-throw reset; it once read "0 of 1" after every swing
        [Fact]
        public void ResettingToTheSameShapes_KeepsTheCount()
        {
            SnagCue cue = Cue();

            cue.Watch(Throw(1, 4, 3));
            cue.Watch(Throw(2, 4, 3));
            cue.Reset(new[] { Die.D8, Die.D6, Die.D6 });

            Assert.Equal(2, cue.Throws);
            Assert.Equal(1, cue.Snags);
        }

        [Fact]
        public void ResettingToDifferentShapes_StartsTheCountAgain()
        {
            SnagCue cue = Cue();

            cue.Watch(Throw(1, 4, 3));
            cue.Reset(new[] { Die.D12, Die.D12, Die.D12 });

            Assert.Equal(0, cue.Throws);
            Assert.Equal(0, cue.Snags);
            Assert.Null(cue.LastKey);
        }

    }
}
