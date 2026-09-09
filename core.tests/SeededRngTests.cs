using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Statistics;
using Xunit;

namespace Core.Tests
{
    // SeededRng is the only source of randomness in the game, and since F0 the project owns the
    // algorithm outright rather than borrowing System.Random's, whose sequence Microsoft
    // documents as an implementation detail and has already changed once.
    //
    // Two things need guarding, and they are different kinds of thing:
    //
    //   the sequence must never change again  - GoldenSequence_IsPinnedForever
    //   the sequence must be uniform          - the FaceTally cases below
    //
    // The first is the durable-replay promise. A save (Phase P) will store a seed to replay a
    // session; the golden test is what makes that promise mechanical instead of hopeful.
    public class SeededRngTests
    {
        static int[] Rolls(int seed, int sides, int count)
        {
            var rng = new SeededRng(seed);
            return Enumerable.Range(0, count).Select(_ => rng.Roll(sides)).ToArray();
        }

        // ---- the pin ----

        // THESE NUMBERS ARE A CONTRACT. If this test fails, the generator has moved and every
        // seed ever written down - in a save, in a bug report, in a test below - now means a
        // different game. That is a regression even when the new generator is better. Fix the
        // generator, not the test.
        //
        // Produced by SplitMix64 seed expansion into xoshiro256**, mapped to [1, sides] by
        // Lemire's multiply-shift. Cross-checked on 2026-09-08 against an independent
        // implementation of the same two published algorithms, written from the papers, which
        // reproduced all three rows exactly - so these pin the algorithms, not one C#
        // transcription of them.
        [Theory]
        [InlineData(4242, 20, new[] { 16, 2, 9, 18, 11, 5, 9, 20, 16, 13, 8, 8, 3, 7, 12, 9, 10, 5, 14, 2 })]
        [InlineData(0, 6, new[] { 4, 5, 1, 3, 5, 6, 3, 4, 6, 6, 1, 1, 1, 4, 3, 2 })]
        [InlineData(-1, 12, new[] { 7, 10, 7, 9, 7, 9, 5, 10, 8, 8, 4, 1, 6, 6, 3, 10 })]
        public void GoldenSequence_IsPinnedForever(int seed, int sides, int[] expected) =>
            Assert.Equal(expected, Rolls(seed, sides, expected.Length));

        // ---- reproducibility ----

        [Fact]
        public void SameSeed_ReplaysExactly()
        {
            // CONVENTIONS 6 leans on this: when a result looks wrong, re-run the seed with a
            // recorder attached rather than adding print statements
            Assert.Equal(Rolls(20260908, 12, 5000), Rolls(20260908, 12, 5000));
        }

        [Fact]
        public void AdjacentSeeds_DivergeImmediately()
        {
            // the reason the seed goes through SplitMix64 first. xoshiro seeded straight from a
            // small integer opens with visibly similar rolls for neighbouring seeds, which would
            // make seeds 1, 2, 3 a bad way to write three independent tests - and they are used
            // that way all over this suite
            int[] a = Rolls(1, 20, 12);
            int[] b = Rolls(2, 20, 12);
            int[] c = Rolls(3, 20, 12);

            Assert.NotEqual(a[0], b[0]);
            Assert.NotEqual(b[0], c[0]);
            Assert.True(a.Zip(b, (x, y) => x == y).Count(same => same) < 6);
        }

        // ---- the range contract ----

        [Theory]
        [InlineData(4)]
        [InlineData(6)]
        [InlineData(8)]
        [InlineData(10)]
        [InlineData(12)]
        [InlineData(20)]
        public void EveryRoll_LandsOnTheDie_AndEveryFaceIsReachable(int sides)
        {
            var seen = new HashSet<int>();
            var rng = new SeededRng(sides * 7919);

            for (int i = 0; i < sides * 200; i++)
            {
                int v = rng.Roll(sides);

                Assert.InRange(v, 1, sides);
                seen.Add(v);
            }

            // an off-by-one in the mapping loses a face at one end, and the chi-squared case
            // below would call that biased - but this says which end, in one line
            Assert.Equal(sides, seen.Count);
        }

        [Fact]
        public void ADieWithOneFace_AlwaysRollsIt()
        {
            var rng = new SeededRng(11);
            for (int i = 0; i < 100; i++) Assert.Equal(1, rng.Roll(1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ADieWithNoFaces_Throws(int sides) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRng(1).Roll(sides));

        [Fact]
        public void LargeRanges_Work()
        {
            // FaceTallyTests builds its loaded dice out of Roll(100) and Roll(1000)
            var rng = new SeededRng(31337);

            for (int i = 0; i < 2000; i++)
            {
                Assert.InRange(rng.Roll(100), 1, 100);
                Assert.InRange(rng.Roll(1000), 1, 1000);
            }
        }

        // ---- uniformity ----
        //
        // What this can and cannot see is worth being straight about.
        //
        // It CANNOT see modulo bias at 64 bits. Naive "% sides" crowds the low faces by about
        // sides / 2^64, which is roughly one part in 10^18 - no sample anyone will ever run
        // detects it, and a test claiming otherwise would be lying. Lemire's multiply-shift
        // removes that bias by construction, and construction is the only place it can be
        // removed; the argument lives in the comment on Bounded, not here.
        //
        // What it CAN see is every mapping error that is actually plausible: a face that never
        // comes up, a range truncated at one end, a shift that halves the entropy, a state
        // update transcribed wrong. Those are gross, and chi-squared plus mean drift catch them
        // at these sample sizes without breaking a sweat. It is also the instrument the physics
        // dice are judged with (check-fairness.ps1), so pointing it at the generator keeps the
        // two notions of "fair" in agreement - if this ever went red, every fairness verdict the
        // project has ever printed would be suspect.

        [Theory]
        [InlineData(4)]
        [InlineData(6)]
        [InlineData(8)]
        [InlineData(10)]
        [InlineData(12)]
        [InlineData(20)]
        public void FaceTally_CallsTheGeneratorUniform(int sides)
        {
            var rng = new SeededRng(20260908 + sides);
            var tally = new FaceTally(sides);

            for (int i = 0; i < 12000; i++) tally.Add(rng.Roll(sides));

            // Uniform, not merely "not Biased" - a fair generator clears the 5% line 19 runs in
            // 20, and these seeds are fixed, so there is nothing flaky to tolerate here
            Assert.Equal(Fairness.Uniform, tally.Verdict);

            // and the second test, aimed at the ordered alternative chi-squared is blind to.
            // this is the one that would catch a mapping that quietly leans low
            Assert.Equal(Fairness.Uniform, tally.DriftVerdict);
            Assert.True(Math.Abs(tally.MeanDriftZ) < 2.58,
                $"d{sides} mean pip {tally.MeanPip:0.000} drifted {tally.MeanDriftZ:0.00} standard errors");
        }

        [Fact]
        public void ManySweeps_AndTheVerdictHolds()
        {
            // 100 independent sweeps of a d6, each long enough to judge. one seed landing well
            // proves little; a hundred consecutive stretches of the same stream proves the
            // generator is uniform along its length and not just at its start
            var rng = new SeededRng(777);
            int suspicious = 0;

            for (int run = 0; run < 100; run++)
            {
                var tally = new FaceTally(6);
                for (int i = 0; i < 600; i++) tally.Add(rng.Roll(6));

                Assert.NotEqual(Fairness.Biased, tally.Verdict);
                if (tally.Verdict == Fairness.Suspicious) suspicious++;
            }

            // the 5% line is crossed by a fair generator about 5 times in 100
            // far fewer would mean the sweeps are not independent, far more means real bias
            Assert.InRange(suspicious, 0, 12);
        }
    }
}
