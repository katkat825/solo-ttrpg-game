using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Resolution
{
    // how often a given pool snags or lands in Trouble, exactly
    //
    // CORE_RULES 6 quotes these for three pools and SIMULATION.md was checked against
    // enumeration, but neither is reachable from code, so nothing holds the tray to them
    // this is the same figure in closed form: a measured rate is only a finding if there
    // is a number to hold it against, and a Monte Carlo answer would come with its own
    // error bar on top of the one being measured
    //
    // Snag is EXACTLY one 1, because that is what PoolResult.Snag is
    // the "39.2%" in CORE_RULES 6 is the AT LEAST ONE column - the Snag tier is the 33.0%
    // underneath it, and the two are easy to read as the same number
    public static class PoolOdds
    {
        // P(exactly one die shows a 1) - the companion's cue
        public static double Snag(IEnumerable<Die> dice) => Ones(dice)[1];

        // P(two or more 1s) - a real consequence
        public static double Trouble(IEnumerable<Die> dice) => Ones(dice)[2];

        // P(any 1 at all) - the union, and the column CORE_RULES 6 tabulates
        public static double AnyOne(IEnumerable<Die> dice) => 1.0 - Ones(dice)[0];

        public static double Snag(Pool pool) => Snag(Sizes(pool));

        public static double Trouble(Pool pool) => Trouble(Sizes(pool));

        public static double AnyOne(Pool pool) => AnyOne(Sizes(pool));

        // how many standard errors a measured count sits from what the pool predicts
        // one degree of freedom aimed at the only question being asked, the same statistic
        // DiceFairness settled on for pip drift - a rate with no sigma on it is a number
        // nobody can act on
        // zero throws is zero drift rather than a divide by nothing
        public static double Drift(int snags, int throws, IEnumerable<Die> dice)
        {
            double p = Snag(dice);
            double variance = throws * p * (1.0 - p);

            return variance <= 0.0 ? 0.0 : (snags - throws * p) / System.Math.Sqrt(variance);
        }

        static IEnumerable<Die> Sizes(Pool pool) => pool.Dice.Select(d => d.Die);

        // the distribution of how many 1s the pool shows, bucketed 0, 1, and two-or-more
        // three buckets rather than one per die: those are the three tiers the rules have,
        // and collapsing the tail keeps this a fixed-width step whatever the pool grows to
        //
        // a None die is not on the felt and cannot show anything, so it is skipped -
        // the same silent drop Pool.Add makes, and for the same reason
        static double[] Ones(IEnumerable<Die> dice)
        {
            double[] p = { 1.0, 0.0, 0.0 };

            foreach (Die die in dice)
            {
                if (!die.IsReal()) continue;

                double one = 1.0 / die.Sides();
                double not = 1.0 - one;

                p = new[]
                {
                    p[0] * not,
                    p[0] * one + p[1] * not,
                    p[1] * one + p[2],
                };
            }

            return p;
        }
    }
}
