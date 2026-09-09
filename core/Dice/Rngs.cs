using System;
using System.Collections.Generic;

namespace Core.Dice
{
    // the three implementations of IRng
    // SeededRng is what the game runs on - a seed replays a session exactly
    // ScriptedRng feeds tests a known sequence
    // RecordingRng wraps either and keeps the raw rolls

    // ---------------------------------------------------------------------------------------
    // SeededRng owns its generator outright, and owning it is the whole point.
    //
    // It used to wrap System.Random. The sequence System.Random gives a seed is documented as
    // an implementation detail, not a contract, and it has already changed once - between .NET
    // Framework and .NET Core. The project's horizon is a decade of hand-editable saves and
    // seeded replay (ARCHITECTURE.md 8), so that is a quiet, years-out failure: a save stores a
    // seed, a future .NET upgrade changes the algorithm, every replay diverges, and no test
    // fails on the day it breaks because nothing in this repo changed.
    //
    // So the stream is defined here instead. SplitMix64 expands the seed into 256 bits of
    // state; xoshiro256** produces the stream. Both are small, well studied, and public
    // domain. Every operation is ulong arithmetic, which C# defines exactly - no floats, no
    // platform width, no culture - so the sequence is identical on every machine and every
    // runtime version, forever.
    //
    // THIS IS NOW A FIXED ARTIFACT. SeededRngTests.GoldenSequence_IsPinnedForever holds the
    // first rolls off three seeds. Changing anything below breaks it loudly, which is the
    // durable-replay promise made mechanical - the same move PoolOdds made for the Snag rate.
    // If that test fails, the generator moved and every stored seed just changed meaning.
    // ---------------------------------------------------------------------------------------
    public sealed class SeededRng : IRng
    {
        ulong _s0, _s1, _s2, _s3;

        public SeededRng(int seed)
        {
            // seeding xoshiro from a small integer directly leaves nearby seeds correlated for
            // their first outputs - seeds 1 and 2 would open with visibly similar rolls.
            // SplitMix64 is the author's prescribed fix: one counter in, four scrambled words out
            ulong x = unchecked((ulong)seed);

            _s0 = SplitMix64(ref x);
            _s1 = SplitMix64(ref x);
            _s2 = SplitMix64(ref x);
            _s3 = SplitMix64(ref x);

            // all-zero is xoshiro's one fixed point and would emit zeros forever
            // four consecutive SplitMix64 draws cannot actually produce it, but the generator
            // should not rest on that being true
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s3 = 1;
        }

        // inclusive 1..sides, uniform, no modulo bias
        public int Roll(int sides)
        {
            if (sides < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(sides), sides, "A die needs at least one face.");

            return (int)(Bounded((ulong)sides) + 1);
        }

        // Lemire's multiply-shift - a uniform value in [0, n) taking one draw almost always.
        //
        // The obvious `% n` is wrong here and wrong in a way this project is unusually exposed
        // to. 2^64 is not divisible by 6, 10, 12 or 20, so the leftover values crowd the low
        // faces - a die that rolls slightly low, forever. That is precisely the skew FaceTally
        // and check-fairness.ps1 exist to hunt, so a biased mapping would poison the instrument
        // as well as the dice, and every table in SIMULATION.md is driven by the mean of a die.
        //
        // Multiply the draw by n and take the high word: that partitions 2^64 into n buckets.
        // The buckets differ in size by at most one, and the low word says which draws fall in
        // the ragged first 2^64 mod n products. Reject those and redraw, and the buckets are
        // exactly equal. The rejection rate is under n / 2^64 - it has never once fired for a
        // d20 and never will, but it is what makes the mapping exact rather than merely close
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

        // xoshiro256** - period 2^256 - 1, passes BigCrush, five lines of state update
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

        // SplitMix64 - a counter run through a fixed-point-free mixer, used only for seeding
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
