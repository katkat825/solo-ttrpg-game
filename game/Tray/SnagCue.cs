using System;
using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Game.Tray
{
    public sealed class SnagCue
    {
        // the barbarian's companion; speakers are keyed by creature, not class
        public const string Speaker = "wolf";

        public const string Situation = "snag";

        // what M9 used before there were any lines: about forty barks per companion was the size
        // of the hole, and the key was built against a bank that did not exist. W2 filled it, and
        // this stays only as the fallback for a table with no companion sitting at it
        public const int PlaceholderLines = 40;

        // the randomness seam, so a seeded session says the same things twice
        readonly IRng _rng;

        // the real bank, once a companion is at the table; null falls back to the placeholder and
        // the keys it builds are still well formed, they just have nothing behind them
        readonly Speaking _speaking;

        Die[] _pool;

        public SnagCue(IReadOnlyList<Die> pool, IRng rng = null, BarkBank bank = null)
        {
            // no ambient default - a caller that wants a repeatable session passes a seeded one
            _rng = rng ?? new SeededRng(Environment.TickCount);

            // dealt rather than drawn, so forty lines do not repeat inside ten throws (W2)
            _speaking = bank?.Open(_rng);

            Reset(pool);
        }

        // which creature's bank this is drawing on
        public string Speaking => _speaking?.Speaker ?? Speaker;

        // false while the fallback is in use: the key is shaped right and nobody has written it
        public bool HasLines => _speaking != null;

        public int Throws { get; private set; }

        public int Snags { get; private set; }

        // the last key a companion would have spoken, or null if the last throw was quiet
        public string LastKey { get; private set; }

        // what this pool snags at exactly (one 1), not the at-least-one-1 figure that includes Trouble
        public double Expected => PoolOdds.Snag(_pool);

        // reset the count only on a real change of shapes: bigger dice snag less, so a tally across a change measures a pool that no longer exists
        // same shapes are not a change: a fight rebuilds the tray every swing, and resetting on each left the tally stuck at '0 of 1'
        public void Reset(IReadOnlyList<Die> pool)
        {
            Die[] shapes = (pool ?? throw new ArgumentNullException(nameof(pool))).ToArray();

            if (_pool != null && _pool.SequenceEqual(shapes)) return;

            _pool = shapes;

            Throws = 0;
            Snags = 0;
            LastKey = null;
        }

        // one throw: returns the bark key or null for a quiet throw; asks TrayThrow where the 1 landed and never reads a value itself
        public string Watch(TrayThrow thrown)
        {
            if (thrown == null) throw new ArgumentNullException(nameof(thrown));

            Throws++;

            if (thrown.SnaggedSlot < 0) return LastKey = null;

            Snags++;

            // the companion's own bank where there is one; built, never written out, so the key
            // stays correct if the grammar ever moves
            return LastKey = _speaking?.Next(Bark.Snag)
                          ?? KeyConventions.Bark(Speaker, Situation, _rng.Roll(PlaceholderLines));
        }

        public double Rate => Throws == 0 ? 0.0 : (double)Snags / Throws;

        // how far the felt is from the closed form, in standard errors, so fifty throws become an answer instead of eyeballing
        public double Drift => PoolOdds.Drift(Snags, Throws, _pool);

        // developer only, not localized, never reaches the screen
        public string Tally =>
            $"{Snags} of {Throws} throws, {Rate:0.0%} - " +
            $"{string.Join("+", _pool.Select(d => d.Label()))} predicts {Expected:0.0%}, " +
            $"{Drift:+0.00;-0.00} standard errors";
    }
}
