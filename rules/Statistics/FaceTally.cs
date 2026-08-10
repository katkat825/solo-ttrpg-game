using System;
using System.Collections.Generic;
using System.Linq;

namespace Rules.Statistics
{
    // counts how a die's faces land, and says whether that looks fair
    // a physics die is not fair by construction
    // mass, spawn orientation, tray shape and throw all bias it, invisibly
    // chi-squared against uniform asks whether a fair die could have done this
    // headless and Godot-free, so it lives in rules/ and has its own tests
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

        // sum of (observed - expected)^2 / expected
        // accounts for sample size, unlike eyeballing percentages
        // so it neither panics at a short run nor shrugs at a long one
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

        // percentage points off an even share, not percent
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

        // Suspicious is not a failure
        // a fair die crosses the 5% line one run in twenty - failing there cries wolf
        // only the 0.1% line counts as real bias, about one false alarm in a thousand
        // Suspicious means throw more at it, not rebuild the die
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
        // below this the test says Inconclusive rather than a comforting pass
        public int MinimumUsefulThrows => Sides * 5;

        // DEVELOPER ONLY - not localized, must never reach the screen
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

                // sign written by hand
                // .NET's "+0.0;-0.0" section format looks right and is not
                // a small negative that rounds to zero comes out as "-+0.0"
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

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            string.Join(" ", Enumerable.Range(1, Sides).Select(f => $"{f}:{_counts[f]}"));
    }

    // what the chi-squared test can conclude, worst answer last
    public enum Fairness
    {
        Inconclusive,

        Uniform,

        Suspicious,

        Biased,
    }
}
