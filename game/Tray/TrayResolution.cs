using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Resolution;

namespace Game.Tray
{
    // turns the faces the dice settled on into a real PoolResult
    // the physics owns every raw number, core/ owns what they mean
    // nothing here reimplements a rule - the moment it starts adding dice up itself, it is wrong
    // Godot-free, so it is testable headless
    //
    // IT IS HANDED A POOL, and that is the whole of B4's connection between the board and the
    // tray. Until then this held three label keys of its own - a comment saying they "stand in
    // for Actor.BuildPool(Attr.Might, Skill.Blades) until there is a hero", and a promise that
    // the swap would be one line. This is that line. The pool now comes from whoever asked for
    // the throw: the tray's own hero when you press space, the board's hero when a door is being
    // broken down, and a campaign's when there is one. Nothing here knows which.
    public sealed class TrayResolution
    {
        readonly Pool _pool;

        readonly Die[] _dice;

        // pool: what is being thrown, in THROW order - each die with the key of the trait that
        // contributed it. a description of the handful, not a preference about it
        public TrayResolution(Pool pool)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            // Pool.Add drops a die that isn't there, so an untrained attempt arrives as a smaller
            // pool rather than as a pool with a hole in it. an EMPTY one is a caller with a bug:
            // there is nothing to throw and nothing to read
            if (pool.Count == 0)
                throw new ArgumentException("A pool with no dice in it cannot be thrown.", nameof(pool));

            _pool = pool;
            _dice = pool.Dice.Select(d => d.Die).ToArray();
        }

        // how many dice this pool puts on the felt. the scene has to have at least that many,
        // and DiceTray is what checks it
        public int Size => _dice.Length;

        public IReadOnlyList<Die> Dice => _dice;

        // tableValues: one face per die in THROW order - not settle order, and not sorted
        // the pool is built in the same order, so index i came from throw point i
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

            // load-bearing coupling
            // ScriptedRng returns its values in call order, StandardResolver rolls in pool order
            // reorder one without the other and the dice start lying about which trait they were,
            // silently
            var rng = new ScriptedRng(slots.Select(s => s.Value).ToArray());
            PoolResult result = new StandardResolver(rng).Resolve(_pool);

            return new TrayThrow(result, slots);
        }
    }
}
