using System;
using System.Collections.Generic;

namespace Core.Dice
{

    // owns its own generator so a seed replays identically across .NET versions - System.Random's stream is not a contract
    // changing anything below breaks the pinned golden-sequence test, which is the point
    public sealed class SeededRng : IRng
    {
        ulong _s0, _s1, _s2, _s3;

        public SeededRng(int seed)
        {
            // SplitMix64 scrambles the seed first, so nearby seeds don't open with similar rolls
            ulong x = unchecked((ulong)seed);

            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            _s2 = SplitMix64(ref x);
            _s3 = SplitMix64(ref x);

            // all-zero is xoshiro's fixed point - four SplitMix64 draws can't hit it, but guard anyway
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s3 = 1;
        }

        public int Roll(int sides)
        {
            if (sides < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(sides), sides, "A die needs at least one face.");

            return (int)(Bounded((ulong)sides) + 1);
        }

        // plain % n would crowd the low faces - the exact skew FaceTally hunts
        ulong Bounded(ulong n)
        {
            ulong high = Math.BigMul(NextUlong(), n, out ulong low);

            if (low < n)
            {
                ulong floor = unchecked(0UL - n) % n; // 2^64 mod n

                while (low < floor)
                    high = Math.BigMul(NextUlong(), n, out low);
            }

            return high;
        }

        ulong NextUlong()
        {
            unchecked
            {
                ulong result = Rotl(_s1 * 5, 7) * 9;
                ulong t = _s1 << 17;

                _s2 ^= _s0;
                _s3 ^= _s1;
                _s1 ^= _s2;
                _s0 ^= _s3;
                _s2 ^= t;
                _s3 = Rotl(_s3, 45);

                return result;
            }
        }

        static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        static ulong SplitMix64(ref ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;

                ulong z = x;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

                return z ^ (z >> 31);
            }
        }
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
