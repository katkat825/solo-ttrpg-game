using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Resolution
{
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

            // thrown in pool order so a seed repeats; PoolResult.From reads the result, the same as save/load
            var thrown = pool.Dice
                .Select(d => (d.LabelKey, d.Die, Value: _rng.Roll(d.Die.Sides())))
                .ToList();

            return PoolResult.From(thrown);
        }

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
