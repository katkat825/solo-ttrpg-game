using System;
using System.Collections.Generic;
using System.Linq;
using Rules.Dice;
using Rules.Localization;
using Rules.Resolution;

// the companion's cue to speak, before there is a companion
//
// one 1 is a Snag: no mechanical effect, and the moment the creature sitting on the table
// has an opinion. it fires on about a third of throws with the starting pool, which is why
// CORE_RULES 12 spends its writing budget there - it is the highest-frequency piece of
// characterisation in the game
//
// nothing says these lines yet, so this picks the key one WOULD have said and keeps count of
// how often the felt actually asks for one. that is the whole of M9: prove the hook exists,
// and prove the rate the sim assumes survives contact with physics
//
// it lives in game/ and not in rules/ on purpose. the engine's namespaces stop at gear -
// dialogue is campaign content, and a bark bank belongs with the campaign that ships the
// lines, not in the engine. this stands in for that the way TrayResolution.PoolLabels
// stands in for a hero, and it should be deleted when the real one arrives
//
// Godot-free, so nothing here needs a running engine to be true

public sealed class SnagCue
{
    // the barbarian's companion. speakers are keyed by creature and never by class -
    // the class picks the companion, the companion owns the voice
    public const string Speaker = "wolf";

    public const string Situation = "snag";

    // CORE_RULES 12 asks for about forty per companion, tagged by situation, and shuffled
    // none of them are written, so this is the size of the hole rather than a count of
    // anything that exists - and shuffling proves nothing until there is something to shuffle
    public const int PlaceholderLines = 40;

    // the randomness seam, so a seeded session says the same things twice
    readonly IRng _rng;

    Die[] _pool;

    public SnagCue(IReadOnlyList<Die> pool, IRng rng = null)
    {
        // no ambient default - a caller that wants a repeatable session passes a seeded one
        _rng = rng ?? new SeededRng(Environment.TickCount);

        Reset(pool);
    }

    public int Throws { get; private set; }

    public int Snags { get; private set; }

    // the last key a companion would have spoken, or null if the last throw was quiet
    public string LastKey { get; private set; }

    // what this pool should snag at, exactly - not the 39.2% in CORE_RULES 6, which is the
    // at-least-one-1 column and includes the Trouble tier on top
    public double Expected => PoolOdds.Snag(_pool);

    // starts the count again, and it has to: bigger dice snag less, so a tally carried across
    // a change of shapes measures a pool that no longer exists and reads as a finding
    public void Reset(IReadOnlyList<Die> pool)
    {
        _pool = (pool ?? throw new ArgumentNullException(nameof(pool))).ToArray();

        Throws = 0;
        Snags = 0;
        LastKey = null;
    }

    // one throw. returns the bark key the companion would have used, or null for a quiet throw
    // the decision is entirely the rules' - this asks TrayThrow where the 1 landed and never
    // looks at a value itself
    public string Watch(TrayThrow thrown)
    {
        if (thrown == null) throw new ArgumentNullException(nameof(thrown));

        Throws++;

        if (thrown.SnaggedSlot < 0) return LastKey = null;

        Snags++;

        // built rather than written out, so it is correct by construction and stays correct
        // if the grammar ever moves
        return LastKey = KeyConventions.Bark(Speaker, Situation, _rng.Roll(PlaceholderLines));
    }

    public double Rate => Throws == 0 ? 0.0 : (double)Snags / Throws;

    // how far the felt is from the closed form, in standard errors
    // the M9 verify is "throw fifty and count" - this is what turns that count into an answer,
    // because fifty throws is a wide enough net that eyeballing the percentage proves nothing
    public double Drift => PoolOdds.Drift(Snags, Throws, _pool);

    // DEVELOPER ONLY - not localized, never reaches the screen
    public string Tally =>
        $"{Snags} of {Throws} throws, {Rate:0.0%} - " +
        $"{string.Join("+", _pool.Select(d => d.Label()))} predicts {Expected:0.0%}, " +
        $"{Drift:+0.00;-0.00} standard errors";
}
