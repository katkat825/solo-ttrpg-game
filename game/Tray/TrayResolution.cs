using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Resolution;

namespace Game.Tray
{
    // reimplements no rule: the physics owns the raw numbers, core owns what they mean
    public sealed class TrayResolution
    {
        readonly Pool _pool;

        readonly Die[] _dice;

        // the pool being thrown, in throw order, each die tagged with the trait that contributed it
        public TrayResolution(Pool pool)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            // an empty pool is a caller bug: there is nothing to throw and nothing to read
            if (pool.Count == 0)
                throw new ArgumentException("A pool with no dice in it cannot be thrown.", nameof(pool));

            _pool = pool;
            _dice = pool.Dice.Select(d => d.Die).ToArray();
        }

        // how many dice this pool puts on the felt; DiceTray checks the scene has that many
        public int Size => _dice.Length;

        public IReadOnlyList<Die> Dice => _dice;

        // one face per die in throw order - not settle order, not sorted; index i is throw point i
        public TrayThrow Resolve(IReadOnlyList<int> tableValues)
        {
            if (tableValues == null) throw new ArgumentNullException(nameof(tableValues));

            if (tableValues.Count != Size)
                throw new ArgumentException(
                    $"The pool is {Size} dice; got {tableValues.Count} values.", nameof(tableValues));

            var slots = new List<TraySlot>();

            for (int i = 0; i < Size; i++)
            {
                PoolDie die = _pool.Dice[i];
                int value = tableValues[i];

                if (value < 1 || value > die.Die.Sides())
                    throw new ArgumentOutOfRangeException(
                        nameof(tableValues), value, $"Not a face on a {die.Die.Label()}.");

                slots.Add(new TraySlot(die.LabelKey, die.Die, value));
            }

            // load-bearing coupling: ScriptedRng returns values in call order and the resolver rolls in pool order; reorder one and the dice silently lie about which trait they were
            var rng = new ScriptedRng(slots.Select(s => s.Value).ToArray());
            PoolResult result = new StandardResolver(rng).Resolve(_pool);

            return new TrayThrow(result, slots);
        }
    }
}
