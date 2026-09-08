using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Game.Tray
{
    // turns the faces three dice settled on into a real PoolResult
    // the physics owns every raw number, core/ owns what they mean
    // nothing here reimplements a rule - the moment it starts adding dice up itself, it is wrong
    // Godot-free, so it is testable headless
    public sealed class TrayResolution
    {
        // stands in for Actor.BuildPool(Attr.Might, Skill.Blades) until there is a hero
        // same shape deliberately, so the swap is a one-line change
        // keys, never display text
        static readonly string[] PoolLabels =
        {
            Attr.Might.Key(),
            Skill.Blades.Key(),
            KeyConventions.GearName("axe"),
        };

        public static int PoolSize => PoolLabels.Length;

        readonly Die[] _dice;

        // dice: the sizes on the felt, in throw order - a description, not a preference
        public TrayResolution(IReadOnlyList<Die> dice)
        {
            if (dice == null) throw new ArgumentNullException(nameof(dice));

            if (dice.Count != PoolSize)
                throw new ArgumentException(
                    $"The pool is {PoolSize} dice; got {dice.Count}.", nameof(dice));

            foreach (Die die in dice)
                if (!die.IsReal())
                    throw new ArgumentException("A pool cannot contain a die that isn't there.", nameof(dice));

            _dice = dice.ToArray();
        }

        public IReadOnlyList<Die> Dice => _dice;

        // tableValues: one face per die in THROW order - not settle order, and not sorted
        // the pool is built in the same order, so index i came from throw point i
        public TrayThrow Resolve(IReadOnlyList<int> tableValues)
        {
            if (tableValues == null) throw new ArgumentNullException(nameof(tableValues));

            if (tableValues.Count != PoolSize)
                throw new ArgumentException(
                    $"The pool is {PoolSize} dice; got {tableValues.Count} values.", nameof(tableValues));

            var pool = new Pool();
            var slots = new List<TraySlot>();

            for (int i = 0; i < PoolSize; i++)
            {
                string labelKey = PoolLabels[i];
                Die die = _dice[i];
                int value = tableValues[i];

                if (value < 1 || value > die.Sides())
                    throw new ArgumentOutOfRangeException(
                        nameof(tableValues), value, $"Not a face on a {die.Label()}.");

                pool.Add(labelKey, die);
                slots.Add(new TraySlot(labelKey, die, value));
            }

            // load-bearing coupling
            // ScriptedRng returns its values in call order, StandardResolver rolls in pool order
            // reorder one without the other and the dice start lying about which trait they were,
            // silently
            var rng = new ScriptedRng(slots.Select(s => s.Value).ToArray());
            PoolResult result = new StandardResolver(rng).Resolve(pool);

            return new TrayThrow(result, slots);
        }
    }
}
