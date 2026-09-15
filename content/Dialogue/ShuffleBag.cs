using System;
using System.Collections.Generic;
using Core.Dice;

namespace Content.Dialogue
{
    // A bank of forty barks picked at random will say the same line twice in the first ten throws
    // more often than not, and that is the moment a living creature turns back into a random number
    // generator. So the bank is dealt, not drawn: every line is said once before any is said twice.
    //
    // CORE_RULES.md section 12 calls for the shuffle; DICE_TRAY.md M9 deferred it to "the campaign
    // that ships the lines", which is now.
    public sealed class ShuffleBag
    {
        readonly IRng _rng;

        readonly List<int> _dealt = new List<int>();

        int _at;

        // the last one out of the previous deal, so a reshuffle cannot hand it straight back
        int _last;

        public ShuffleBag(int count, IRng rng)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "A bank cannot be shorter than nothing.");

            Count = count;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public int Count { get; }

        // how many are left before the bank comes round again; the bag is empty at zero
        public int Left => _dealt.Count - _at;

        public int Deals { get; private set; }

        // 1-based, matching the index in the key; 0 means the bank is empty and nothing is said
        public int Next()
        {
            if (Count == 0) return 0;

            if (_at >= _dealt.Count) Deal();

            return _last = _dealt[_at++];
        }

        void Deal()
        {
            _dealt.Clear();

            for (int index = 1; index <= Count; index++) _dealt.Add(index);

            // Fisher-Yates through IRng, so a seeded session says the same things twice
            for (int i = _dealt.Count - 1; i > 0; i--)
            {
                int j = _rng.Roll(i + 1) - 1;

                (_dealt[i], _dealt[j]) = (_dealt[j], _dealt[i]);
            }

            // one bank of one has no second line to offer, so it repeats and that is honest
            if (Count > 1 && _dealt[0] == _last)
            {
                (_dealt[0], _dealt[_dealt.Count - 1]) = (_dealt[_dealt.Count - 1], _dealt[0]);
            }

            _at = 0;
            Deals++;
        }

        public override string ToString() => $"{Count} lines, {Left} unheard, {Deals} deals";
    }
}
