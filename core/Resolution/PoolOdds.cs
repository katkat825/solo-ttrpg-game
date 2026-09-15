using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Resolution
{
    // Snag is exactly one 1 (like PoolResult.Snag) - the at-least-one figure is different and larger
    public static class PoolOdds
    {
        public static double Snag(IEnumerable<Die> dice) => Ones(dice)[1];

        public static double Trouble(IEnumerable<Die> dice) => Ones(dice)[2];

        public static double AnyOne(IEnumerable<Die> dice) => 1.0 - Ones(dice)[0];

        public static double Snag(Pool pool) => Snag(Sizes(pool));

        public static double Trouble(Pool pool) => Trouble(Sizes(pool));

        public static double AnyOne(Pool pool) => AnyOne(Sizes(pool));

        // zero throws is zero drift, not a divide by zero
        public static double Drift(int snags, int throws, IEnumerable<Die> dice)
        {
            double p = Snag(dice);
            double variance = throws * p * (1.0 - p);

            return variance <= 0.0 ? 0.0 : (snags - throws * p) / System.Math.Sqrt(variance);
        }

        static IEnumerable<Die> Sizes(Pool pool) => pool.Dice.Select(d => d.Die);

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
