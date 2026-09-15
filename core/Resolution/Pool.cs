using System.Collections.Generic;
using Core.Dice;

namespace Core.Resolution
{
    // each die carries its trait's localization key, never text, so the tray can label it in any language
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

        // a None die is dropped silently - that's how an untrained pool shrinks
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
