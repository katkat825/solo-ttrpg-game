using System;
using System.Collections.Generic;

namespace Rules.Dice
{
    // the three implementations of IRng
    // SeededRng is what the game runs on - a seed replays a session exactly
    // ScriptedRng feeds tests a known sequence
    // RecordingRng wraps either and keeps the raw rolls
    public sealed class SeededRng : IRng
    {
        readonly Random _r;

        public SeededRng(int seed) => _r = new Random(seed);

        public int Roll(int sides) => _r.Next(1, sides + 1);
    }

    public sealed class ScriptedRng : IRng
    {
        readonly int[] _values;
        int _i;

        public ScriptedRng(params int[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("ScriptedRng needs at least one value.");
            _values = values;
        }

        public int Roll(int sides)
        {
            // clamped silently - a script written for a d12 still runs against a d6
            int v = _values[_i % _values.Length];
            _i++;
            return Math.Min(v, sides);
        }
    }

    public sealed class RecordingRng : IRng
    {
        readonly IRng _inner;
        readonly List<(int Sides, int Value)> _rolls = new List<(int Sides, int Value)>();

        public RecordingRng(IRng inner) => _inner = inner;

        public IReadOnlyList<(int Sides, int Value)> Rolls => _rolls;

        public int Roll(int sides)
        {
            int v = _inner.Roll(sides);
            _rolls.Add((sides, v));
            return v;
        }
    }
}
