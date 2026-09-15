using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Statistics
{
    public sealed class FaceTally
    {
        // chi-squared critical values by degrees of freedom (faces - 1)
        // columns are the 5%, 1% and 0.1% significance levels
        static readonly Dictionary<int, (double P05, double P01, double P001)> Critical = new()
        {
            [3] = (7.815, 11.345, 16.266),   // d4
            [5] = (11.070, 15.086, 20.515),  // d6
            [7] = (14.067, 18.475, 24.322),  // d8
            [9] = (16.919, 21.666, 27.877),  // d10
            [11] = (19.675, 24.725, 31.264), // d12
            [19] = (30.144, 36.191, 43.820), // d20 - not part of any pool yet
        };

        readonly int[] _counts;

        public int Sides { get; }
        public int Total { get; private set; }

        public FaceTally(int sides)
        {
            if (!Critical.ContainsKey(sides - 1))
                throw new ArgumentOutOfRangeException(
                    nameof(sides), sides, "No critical values tabulated for a die this size.");

            Sides = sides;
            _counts = new int[sides + 1]; // 1-based, slot 0 unused so a face indexes directly
        }

        public void Add(int face)
        {
            if (face < 1 || face > Sides)
                throw new ArgumentOutOfRangeException(nameof(face), face, "Not a face on this die.");

            _counts[face]++;
            Total++;
        }

        public int this[int face] => _counts[face];

        public double Expected => (double)Total / Sides;

        public int DegreesOfFreedom => Sides - 1;

        public double ChiSquare
        {
            get
            {
                if (Total == 0) return 0;

                double e = Expected;
                double sum = 0;

                for (int f = 1; f <= Sides; f++)
                {
                    double d = _counts[f] - e;
                    sum += d * d / e;
                }

                return sum;
            }
        }

        public double WorstDeviationPoints
        {
            get
            {
                if (Total == 0) return 0;

                double share = 100.0 / Sides;
                return Enumerable.Range(1, Sides).Max(f => Math.Abs(Percent(f) - share));
            }
        }

        public double Percent(int face) => Total == 0 ? 0 : 100.0 * _counts[face] / Total;

        public Fairness Verdict
        {
            get
            {
                if (Total < MinimumUsefulThrows) return Fairness.Inconclusive;

                (double p05, _, double p001) = Critical[DegreesOfFreedom];
                double x = ChiSquare;

                if (x >= p001) return Fairness.Biased;
                if (x >= p05) return Fairness.Suspicious;
                return Fairness.Uniform;
            }
        }

        // chi-squared wants five expected observations per face
        public int MinimumUsefulThrows => Sides * 5;

        // chi-squared ignores face order, so this second test watches the mean for a tray that rolls high

        public double MeanPip
        {
            get
            {
                if (Total == 0) return 0;

                double sum = 0;
                for (int f = 1; f <= Sides; f++) sum += (double)f * _counts[f];
                return sum / Total;
            }
        }

        public double ExpectedMeanPip => (Sides + 1) / 2.0;

        // the variance of one uniform roll over n faces is (n^2 - 1) / 12
        public double MeanDriftZ
        {
            get
            {
                if (Total == 0) return 0;

                double variance = ((double)Sides * Sides - 1) / 12.0;
                double standardError = Math.Sqrt(variance / Total);

                return standardError > 0 ? (MeanPip - ExpectedMeanPip) / standardError : 0;
            }
        }

        // two-sided: 2.58 is the 1% line, 3.0 is about one run in 370
        public Fairness DriftVerdict => Math.Abs(MeanDriftZ) switch
        {
            >= DriftBiased => Fairness.Biased,
            >= DriftSuspicious => Fairness.Suspicious,
            _ => Fairness.Uniform,
        };

        public bool DriftsHigh => MeanDriftZ > 0;

        const double DriftSuspicious = 2.58;

        const double DriftBiased = 3.0;

        // debug only, never localized - keep it off the screen
        public IEnumerable<string> DebugLines()
        {
            double share = 100.0 / Sides;

            yield return $"{Total} throws of a d{Sides}, expecting {Expected:0.0} per face ({share:0.0}%)";

            int widest = Enumerable.Range(1, Sides).Max(f => _counts[f]);

            for (int f = 1; f <= Sides; f++)
            {
                double pct = Percent(f);
                double deviation = pct - share;
                int bar = widest == 0 ? 0 : (int)Math.Round(40.0 * _counts[f] / widest);

                // sign by hand - .NET's "+0.0;-0.0" format turns a rounded-to-zero negative into "-+0.0"
                string signed = (deviation < 0 ? "-" : "+") + Math.Abs(deviation).ToString("0.0");

                yield return $"  {f}  {_counts[f],6}  {pct,5:0.0}%  {signed,6}pt  " +
                             new string('#', bar);
            }

            (double p05, double p01, double p001) = Critical[DegreesOfFreedom];

            yield return $"chi-squared {ChiSquare:0.00} on {DegreesOfFreedom} dof " +
                         $"(5% {p05:0.00}, 1% {p01:0.00}, 0.1% {p001:0.00})";

            yield return $"worst face is {WorstDeviationPoints:0.00} points off its share";
            yield return "verdict " + Verdict switch
            {
                Fairness.Uniform => "UNIFORM - consistent with a fair die",
                Fairness.Suspicious => "SUSPICIOUS - past the 5% line. A fair die does this 1 run in 20; throw it again before believing it",
                Fairness.Biased => "BIASED - past the 0.1% line. This die, throw or tray is skewed and needs work",
                _ => $"INCONCLUSIVE - needs at least {MinimumUsefulThrows} throws to say anything",
            };
        }

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            string.Join(" ", Enumerable.Range(1, Sides).Select(f => $"{f}:{_counts[f]}"));
    }

    public enum Fairness
    {
        Inconclusive,

        Uniform,

        Suspicious,

        Biased,
    }
}
