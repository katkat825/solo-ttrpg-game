using System;
using Core.Dice;
using Core.Statistics;
using Xunit;

namespace Core.Tests
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

            // the drift half divides by Total too, so it belongs in this test and not a second one
            Assert.Equal(0, t.MeanPip, 10);
            Assert.Equal(0, t.MeanDriftZ, 10);
            Assert.Equal(Fairness.Uniform, t.DriftVerdict);
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

        // ---- mean drift ----
        //
        // this maths lived in game/Diagnostics/DiceFairness until 2026-08-20, where game/ is not
        // in the solution and nothing could reach it. it is the second fairness test, aimed at the
        // ordered alternative chi-squared is blind to, and it had never been checked against a
        // hand-computed number

        [Fact]
        public void PerfectlyUniform_HasNoDrift()
        {
            var t = Repeat(6, 1000);

            Assert.Equal(3.5, t.MeanPip, 10);
            Assert.Equal(3.5, t.ExpectedMeanPip, 10);
            Assert.Equal(0, t.MeanDriftZ, 10);
            Assert.Equal(Fairness.Uniform, t.DriftVerdict);
        }

        [Theory]
        [InlineData(4, 2.5)]
        [InlineData(6, 3.5)]
        [InlineData(8, 4.5)]
        [InlineData(10, 5.5)]
        [InlineData(12, 6.5)]
        public void ExpectedMeanPip_IsTheFairAverage(int sides, double expected) =>
            Assert.Equal(expected, new FaceTally(sides).ExpectedMeanPip, 10);

        [Fact]
        public void MeanPip_MatchesHandCalculation()
        {
            // 1, 2, 6, 6 -> 15 / 4
            var t = Tally(1, 2, 6, 6);
            Assert.Equal(3.75, t.MeanPip, 10);
        }

        [Fact]
        public void MeanDriftZ_MatchesHandCalculation()
        {
            // every throw a 6 on a d6, 105 times.
            // mean 6, expected 3.5, variance (36-1)/12 = 2.916667,
            // standard error sqrt(2.916667 / 105) = 0.1666667, z = 2.5 / 0.1666667 = 15
            var t = new FaceTally(6);
            for (int i = 0; i < 105; i++) t.Add(6);

            Assert.Equal(6.0, t.MeanPip, 10);
            Assert.Equal(15.0, t.MeanDriftZ, 6);
            Assert.True(t.DriftsHigh);
            Assert.Equal(Fairness.Biased, t.DriftVerdict);
        }

        [Fact]
        public void DriftRunsBothWays()
        {
            var low = new FaceTally(6);
            for (int i = 0; i < 105; i++) low.Add(1);

            Assert.Equal(-15.0, low.MeanDriftZ, 6);
            Assert.False(low.DriftsHigh);
            Assert.Equal(Fairness.Biased, low.DriftVerdict);
        }

        // the case that justifies drift existing at all, and the whole reason it is not enough
        // to run chi-squared and stop. faces climbing monotonically 1 -> 6 is the signature of
        // "this tray rolls high", and chi-squared is structurally blind to it: it discards the
        // ORDER of the faces and spreads its power across five degrees of freedom.
        //
        // 6,000 throws on a gentle ladder, 940 up to 1060 in even steps of 24:
        //   chi-squared 10.08 - under the 5% line of 11.070, so it reports UNIFORM
        //   mean pip     3.57 - 3.17 standard errors high, so drift reports BIASED
        //
        // one degree of freedom aimed at the right alternative beats five aimed at all of them.
        // if this test ever fails it means the two tests have stopped disagreeing here, and the
        // sweep has lost the only thing that catches a loaded tray
        [Fact]
        public void OrderedBias_MovesDrift_WhileChiSquaredStaysCalm()
        {
            var t = new FaceTally(6);

            int[] counts = { 940, 964, 988, 1012, 1036, 1060 };
            for (int f = 1; f <= 6; f++)
                for (int i = 0; i < counts[f - 1]; i++) t.Add(f);

            Assert.Equal(6000, t.Total);
            Assert.Equal(3.57, t.MeanPip, 10);

            // chi-squared sees nothing wrong
            Assert.Equal(10.08, t.ChiSquare, 6);
            Assert.Equal(Fairness.Uniform, t.Verdict);

            // drift does
            Assert.Equal(3.1749, t.MeanDriftZ, 4);
            Assert.True(t.DriftsHigh);
            Assert.Equal(Fairness.Biased, t.DriftVerdict);
        }
    }
}
