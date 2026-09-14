using System;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Game.Tray
{
    // THE IMPACT DIE GOING BACK IN THE HAND (CORE_RULES.md section 2, SIMULATION.md section 4).
    //
    // "A maximum roll rolls again and adds." `StandardResolver.RollImpact` does that with a loop,
    // which is right for a fight nobody is watching. In front of a player it is the moment the
    // rule exists for - a d12 rolling 12 into another 12 is something people talk about - so the
    // die is picked up and thrown again, alone, on the same felt. That is a sequence of throws
    // rather than a loop, and this is the sequence: what die, what it is called, what it has come
    // to so far, and whether it goes again.
    //
    // AND IT COVERS THE DIE NOBODY BROUGHT. A two-die pool counts both dice, so nothing is left
    // over and the Impact die is the d4 the rules hand you by default (CORE_RULES.md section 2).
    // That d4 is not lying on the felt - it is still in the box - so it has to be thrown before it
    // can be read, which is the same act as an explosion and is handled as one: a chain that has
    // not been thrown yet goes again.
    //
    // Getting this wrong is what the fight check caught: the felt path read the missing die as
    // zero and an untrained caster's hit dealt no damage at all, while `CombatEngine.Attack` rolled
    // the fallback d4 and dealt one to four. Two paths, one rule, and they disagreed.
    //
    // IT DECIDES NOTHING ABOUT THE FIGHT. Whether the blow landed at all, and whether the number
    // is worth rolling for - a Rabble has no health track, so a bigger number changes nothing -
    // are the caller's questions. This only knows about one die and its own arithmetic.
    //
    // Godot-free, so it is testable headless. That is the whole reason it is a class rather than
    // five fields on `Fight`: the branch that decides a die goes back in the hand is exactly the
    // sort of thing that can sit there for months without firing, and a run of the real table
    // that happens not to roll a maximum proves nothing about it.
    public sealed class ImpactChain
    {
        // a die stuck on its maximum would be a tray throwing dice for the rest of the evening.
        // StandardResolver caps its own chain at the same number and for the same reason
        public const int MaxThrows = 20;

        public ImpactChain(TrayThrow opening)
        {
            if (opening == null) throw new ArgumentNullException(nameof(opening));

            // no die was left over, so PoolResult.Impact is the resolver's d4 default rather than
            // anything on the felt. It is a real die and it counts - it has just not been thrown
            FromTheBox = opening.ImpactIsFallback;

            Die = opening.Result.Impact;
            LabelKey = FromTheBox ? KeyConventions.DefaultImpactName : opening.ImpactLabelKey;
            Total = FromTheBox ? 0 : opening.ImpactValue;
            Last = Total;

            // zero throws means it is still in the box, which is what makes GoesAgain true
            Throws = FromTheBox ? 0 : 1;
        }

        // the pool left nothing over, so this die came out of the box rather than off the felt
        public bool FromTheBox { get; }

        // the die that was left over, or None when nothing was
        public Die Die { get; }

        // the trait it came from, so the second throw wears the same name on the felt
        public string LabelKey { get; }

        // what the die has come to across the whole chain
        public int Total { get; private set; }

        // and what it showed on the throw just read
        public int Last { get; private set; }

        public int Throws { get; private set; }

        // there is always an Impact die: a pool with something left over hands you that, and one
        // with nothing left over hands you a d4 (StandardResolver). False only for a chain built
        // from a throw with no dice in it at all, which cannot happen
        public bool IsReal => Die.IsReal();

        // IT NEEDS THROWING. Either because it has never been thrown - the d4 out of the box - or
        // because the last face was its maximum and it explodes. Unless it has gone twenty times,
        // at which point something is wrong with the dice rather than lucky
        public bool GoesAgain =>
            IsReal && Throws < MaxThrows && (Throws == 0 || Last == Die.Sides());

        // and which of those two it is, because a caller may explode only when the rules say to
        // (CombatOptions.ImpactExplodes) while always throwing a die that has never been thrown
        public bool IsExploding => Throws > 0;

        // the one-die pool to hand the tray. A pool of one is an ordinary pool as far as the tray
        // is concerned since SEAMS.md section 8 was lifted in C2 - it sits the others out
        public Pool Again() => Pool.Of((LabelKey, Die));

        // it came back showing this
        public void Landed(int face)
        {
            if (!IsReal) return;

            if (face < 1 || face > Die.Sides())
                throw new ArgumentOutOfRangeException(
                    nameof(face), face, $"Not a face on a {Die.Label()}.");

            Last = face;
            Total += face;
            Throws++;
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() =>
            !IsReal ? "no impact die"
            : Throws == 0 ? $"{Die.Label()} still in the box"
            : Throws == 1 ? $"{Die.Label()} showing {Total}"
            : $"{Die.Label()} exploded {Throws - 1}x to {Total}";
    }
}
