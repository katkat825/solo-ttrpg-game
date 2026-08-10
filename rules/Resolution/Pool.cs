using System.Collections.Generic;
using Rules.Dice;

namespace Rules.Resolution
{
    // a pool is up to three dice - attribute, optional skill, optional gear
    // untrained means a smaller pool, not a penalty
    // each die carries the localization key of the trait that gave it
    // never display text, so the tray can label the same die in any language
    public readonly struct PoolDie
    {
        public readonly string LabelKey;
        public readonly Die Die;

        public PoolDie(string labelKey, Die die)
        {
            LabelKey = labelKey;
            Die = die;
        }
    }

    public sealed class Pool
    {
        readonly List<PoolDie> _dice = new List<PoolDie>();

        public IReadOnlyList<PoolDie> Dice => _dice;
        public int Count => _dice.Count;

        // a None die is dropped without complaint - that is how the pool shrinks
        public Pool Add(string labelKey, Die die)
        {
            if (die.IsReal()) _dice.Add(new PoolDie(labelKey, die));
            return this;
        }

        public static Pool Of(params (string labelKey, Die die)[] dice)
        {
            var p = new Pool();
            foreach (var d in dice) p.Add(d.labelKey, d.die);
            return p;
        }
    }
}
