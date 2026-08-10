using System;
using Rules.Dice;
using Rules.Statistics;
using Xunit;

namespace Rules.Tests
{
    // FaceTally decides whether the physics dice can be trusted
    // so it had better be trustworthy itself
    // these pin it against known distributions
    // a fair generator must not trip it, a loaded one must
    // and a short run must refuse to answer at all
    public class FaceTallyTests
    {
        static FaceTally Tally(params int[] faces)
        {
            var t = new FaceTally(6);
            foreach (var f in faces) t.Add(f);
            return t;
        }

        static FaceTally Repeat(int sides, int perFace)
        {
            var t = new FaceTally(sides);
            for (int f = 1; f <= sides; f++)
                for (int i = 0; i < perFace; i++) t.Add(f);
            return t;
        }

        // ---- basics ----

        [Fact]
        public void PerfectlyUniform_HasZeroChiSquare()
        {
            var t = Repeat(6, 100);

            Assert.Equal(600, t.Total);
            Assert.Equal(100, t.Expected, 6);
            Assert.Equal(0, t.ChiSquare, 6);
            Assert.Equal(Fairness.Uniform, t.Verdict);
        }

        [Fact]
        public void ChiSquare_MatchesHandCalculation()
        {
            // the first 82 real throws off the tray: 12, 12, 16, 19, 11, 12
            // expected 13.667 each, sum of (o-e)^2 is 49.333, over 13.667 gives 3.610
            var t = new FaceTally(6);
            int[] counts = { 12, 12, 16, 19, 11, 12 };

            for (int f = 1; f <= 6; f++)
                for (int i = 0; i < counts[f - 1]; i++) t.Add(f);

            Assert.Equal(82, t.Total);
            Assert.Equal(3.610, t.ChiSquare, 3);
            Assert.Equal(Fairness.Uniform, t.Verdict);

            // face 4 at 19/82 is 23.2% against a 16.7% share - 6.5 points out, and fine
            Assert.Equal(23.17, t.Percent(4), 2);
            Assert.Equal(6.50, t.WorstDeviationPoints, 2);
        }

        [Fact]
        public void ShortRun_IsInconclusive_NotAComfortingPass()
        {
            var t = Tally(1, 2, 3, 4, 5, 6, 1, 2, 3, 4, 5, 6, 1, 2, 3, 4, 5, 6, 1, 2);

            Assert.Equal(20, t.Total);
            Assert.True(t.Total < t.MinimumUsefulThrows);
            Assert.Equal(Fairness.Inconclusive, t.Verdict);
        }

        [Fact]
        public void MinimumUsefulThrows_IsFivePerFace()
        {
            Assert.Equal(30, new FaceTally(6).MinimumUsefulThrows);
            Assert.Equal(60, new FaceTally(12).MinimumUsefulThrows);
        }

        // ---- guards ----

        [Fact]
        public void UntabulatedDieSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FaceTally(7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FaceTally(100));
        }

        [Fact]
        public void EveryTabulatedSize_Works_IncludingD20()
        {
            // a d20 is not part of the pool, but the tally must not be what blocks one
            // critical points verified against exact chi-squared for 19 degrees of freedom
            foreach (int sides in new[] { 4, 6, 8, 10, 12, 20 })
            {
                var t = Repeat(sides, 50);

                Assert.Equal(sides * 50, t.Total);
                Assert.Equal(0, t.ChiSquare, 6);
                Assert.Equal(Fairness.Uniform, t.Verdict);
            }
        }

        [Fact]
        public void D20_CatchesALoadedFace()
        {
            var rng = new SeededRng(31337);
            var t = new FaceTally(20);

            // face 20 pushed to 10% against a 5% share
            for (int i = 0; i < 8000; i++)
                t.Add(rng.Roll(100) <= 10 ? 20 : rng.Roll(19));

            Assert.Equal(Fairness.Biased, t.Verdict);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        [InlineData(-1)]
        public void FaceOffTheDie_Throws(int face)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FaceTally(6).Add(face));
        }

        [Fact]
        public void EmptyTally_DoesNotDivideByZero()
        {
            var t = new FaceTally(6);

            Assert.Equal(0, t.ChiSquare);
            Assert.Equal(0, t.Percent(1));
            Assert.Equal(0, t.WorstDeviationPoints);
            Assert.Equal(Fairness.Inconclusive, t.Verdict);
        }

        // ---- behaviour against known distributions ----

        [Fact]
        public void FairGenerator_IsNeverCalledBiased()
        {
            // 200 independent sweeps of 600 fair throws
            // at the 0.1% line the expected number of false alarms is 0.2
            // so more than a couple means the verdict thresholds are wrong
            // seeded, so this is deterministic rather than flaky
            var rng = new SeededRng(20260803);
            int biased = 0;

            for (int run = 0; run < 200; run++)
            {
                var t = new FaceTally(6);
                for (int i = 0; i < 600; i++) t.Add(rng.Roll(6));
                if (t.Verdict == Fairness.Biased) biased++;
            }

            Assert.True(biased <= 2, $"fair generator called BIASED {biased} times in 200 runs");
        }

        [Fact]
        public void LoadedDie_IsAlwaysCaught()
        {
            // face 6 pushed to 25% instead of 16.7% - every sweep should be called out
            var rng = new SeededRng(4242);

            for (int run = 0; run < 20; run++)
            {
                var t = new FaceTally(6);

                for (int i = 0; i < 2000; i++)
                    t.Add(rng.Roll(100) <= 25 ? 6 : rng.Roll(5));

                Assert.Equal(Fairness.Biased, t.Verdict);
            }
        }

        [Fact]
        public void SubtleBias_NeedsMoreThrows_AndTheTestSaysSoHonestly()
        {
            // a face at 19% is only 2.3 points off
            // at 2,000 throws it usually slips past - that is the detection floor, not a defect
            // pinned here so nobody later mistakes a pass at 2,000 for a clean bill
            var rng = new SeededRng(777);
            int caught = 0;

            for (int run = 0; run < 20; run++)
            {
                var t = new FaceTally(6);

                for (int i = 0; i < 2000; i++)
                    t.Add(rng.Roll(1000) <= 190 ? 6 : rng.Roll(5));

                if (t.Verdict == Fairness.Biased) caught++;
            }

            Assert.True(caught < 20, "a 19% face was caught every time at 2,000 throws - detection floor has moved, update the documented sweep size");
        }

        [Fact]
        public void DebugLines_RenderWithoutBlowingUp_AndAreNotLocalized()
        {
            var t = Repeat(6, 20);
            var lines = string.Join("\n", t.DebugLines());

            Assert.Contains("120 throws of a d6", lines);
            Assert.Contains("UNIFORM", lines);

            // guards the .NET "+0.0;-0.0" format trap
            // a deviation of exactly zero must render as "+0.0", never "-+0.0"
            Assert.DoesNotContain("-+", lines);
        }
    }
}
