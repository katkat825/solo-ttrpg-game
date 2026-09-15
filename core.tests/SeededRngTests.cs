using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Statistics;
using Xunit;

namespace Core.Tests
{
    public class SeededRngTests
    {
        static int[] Rolls(int seed, int sides, int count)
        {
            var rng = new SeededRng(seed);
            return Enumerable.Range(0, count).Select(_ => rng.Roll(sides)).ToArray();
        }


        // these numbers are a contract: if this fails the generator moved; fix the generator, not the test
        // splitmix64 into xoshiro256**, mapped by lemire's multiply-shift; cross-checked against an independent implementation
        [Theory]
        [InlineData(4242, 20, new[] { 16, 2, 9, 18, 11, 5, 9, 20, 16, 13, 8, 8, 3, 7, 12, 9, 10, 5, 14, 2 })]
        [InlineData(0, 6, new[] { 4, 5, 1, 3, 5, 6, 3, 4, 6, 6, 1, 1, 1, 4, 3, 2 })]
        [InlineData(-1, 12, new[] { 7, 10, 7, 9, 7, 9, 5, 10, 8, 8, 4, 1, 6, 6, 3, 10 })]
        public void GoldenSequence_IsPinnedForever(int seed, int sides, int[] expected) =>
            Assert.Equal(expected, Rolls(seed, sides, expected.Length));


        [Fact]
        public void SameSeed_ReplaysExactly()
        {
            Assert.Equal(Rolls(20260908, 12, 5000), Rolls(20260908, 12, 5000));
        }

        [Fact]
        public void AdjacentSeeds_DivergeImmediately()
        {
            // seed goes through splitmix64 first: raw xoshiro makes neighbouring seeds 1,2,3 look similar
            int[] a = Rolls(1, 20, 12);
            int[] b = Rolls(2, 20, 12);
            int[] c = Rolls(3, 20, 12);

            Assert.NotEqual(a[0], b[0]);
            Assert.NotEqual(b[0], c[0]);
            Assert.True(a.Zip(b, (x, y) => x == y).Count(same => same) < 6);
        }


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

            // a range test names which end an off-by-one drops; chi-squared would only call it biased
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
            var rng = new SeededRng(31337);

            for (int i = 0; i < 2000; i++)
            {
                Assert.InRange(rng.Roll(100), 1, 100);
                Assert.InRange(rng.Roll(1000), 1, 1000);
            }
        }

        // sampling can't see 64-bit modulo bias (removed in bounded by construction); it catches gross mapping errors

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

            Assert.Equal(Fairness.Uniform, tally.Verdict);

            Assert.Equal(Fairness.Uniform, tally.DriftVerdict);
            Assert.True(Math.Abs(tally.MeanDriftZ) < 2.58,
                $"d{sides} mean pip {tally.MeanPip:0.000} drifted {tally.MeanDriftZ:0.00} standard errors");
        }

        [Fact]
        public void ManySweeps_AndTheVerdictHolds()
        {
            var rng = new SeededRng(777);
            int suspicious = 0;

            for (int run = 0; run < 100; run++)
            {
                var tally = new FaceTally(6);
                for (int i = 0; i < 600; i++) tally.Add(rng.Roll(6));

                Assert.NotEqual(Fairness.Biased, tally.Verdict);
                if (tally.Verdict == Fairness.Suspicious) suspicious++;
            }

            // a fair generator crosses the 5% line about 5 times in 100; far off either way is a defect
            Assert.InRange(suspicious, 0, 12);
        }
    }
}
