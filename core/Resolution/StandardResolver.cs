using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Resolution
{
    // the dice system as written, and the default IResolver
    // throw the pool, sum the best two, the largest leftover die is Impact
    // ties break toward the player - equal rolls count the SMALLER die
    // which leaves the larger one free to be Impact
    public sealed class StandardResolver : IResolver
    {
        readonly IRng _rng;

        public StandardResolver(IRng rng) =>
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        public PoolResult Resolve(Pool pool)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (pool.Count == 0)
                throw new InvalidOperationException("Cannot resolve an empty pool.");

            var thrown = pool.Dice
                .Select(d => (d.LabelKey, d.Die, Value: _rng.Roll(d.Die.Sides())))
                .ToList();

            var ordered = thrown
                .OrderByDescending(t => t.Value)
                .ThenBy(t => (int)t.Die)
                .ToList();

            int counted = Math.Min(2, ordered.Count);
            int total = ordered.Take(counted).Sum(t => t.Value);

            var unused = ordered.Skip(counted).ToList();
            Die impact = unused.Count > 0 ? unused.Max(t => t.Die) : Die.D4;

            var rolls = new List<RolledDie>();
            for (int i = 0; i < ordered.Count; i++)
            {
                var t = ordered[i];
                rolls.Add(new RolledDie(t.LabelKey, t.Die, t.Value, i < counted));
            }

            int ones = thrown.Count(t => t.Value == 1);
            return new PoolResult(rolls, total, impact, ones);
        }

        // a maximum roll rolls again and adds
        // simulated as a pacing lever, not a power one
        // enemies explode too, so win rates barely move but fights shorten ~15%
        public int RollImpact(Die impact, bool explodes = true)
        {
            int sides = impact.Sides();
            int roll = _rng.Roll(sides);
            int total = roll;
            int guard = 0;

            // guard caps the chain - an rng stuck on max would loop forever
            while (explodes && roll == sides && guard++ < 20)
            {
                roll = _rng.Roll(sides);
                total += roll;
            }

            return total;
        }
    }
}
