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

            // ROLL HERE, READ THERE. The dice are thrown in pool order - which is what makes a
            // seeded run repeat - and what the throw MEANS is `PoolResult.From`, so the arithmetic
            // has one implementation and save/load reads a throw the same way the table does
            // (SEAMS.md section 9)
            var thrown = pool.Dice
                .Select(d => (d.LabelKey, d.Die, Value: _rng.Roll(d.Die.Sides())))
                .ToList();

            return PoolResult.From(thrown);
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
