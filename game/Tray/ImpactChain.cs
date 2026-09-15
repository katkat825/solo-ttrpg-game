using System;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Game.Tray
{
    public sealed class ImpactChain
    {
        // cap, or a die stuck on its maximum would throw for the rest of the evening; StandardResolver caps the same
        public const int MaxThrows = 20;

        public ImpactChain(TrayThrow opening)
        {
            if (opening == null) throw new ArgumentNullException(nameof(opening));

            // no die was left over, so Impact is the resolver's default d4 - real and counted, just not yet thrown
            FromTheBox = opening.ImpactIsFallback;

            Die = opening.Result.Impact;
            LabelKey = FromTheBox ? KeyConventions.DefaultImpactName : opening.ImpactLabelKey;
            Total = FromTheBox ? 0 : opening.ImpactValue;
            Last = Total;

            // zero throws means it is still in the box, which is what makes GoesAgain true
            Throws = FromTheBox ? 0 : 1;
        }

        public bool FromTheBox { get; }

        // the die that was left over, or None when nothing was
        public Die Die { get; }

        public string LabelKey { get; }

        // what the die has come to across the whole chain
        public int Total { get; private set; }

        // and what it showed on the throw just read
        public int Last { get; private set; }

        public int Throws { get; private set; }

        // there is always an Impact die (a leftover, or the default d4); false only for an impossible empty throw
        public bool IsReal => Die.IsReal();

        // needs throwing: never thrown (the d4 from the box), or its last face was its maximum and it explodes, up to the cap
        public bool GoesAgain =>
            IsReal && Throws < MaxThrows && (Throws == 0 || Last == Die.Sides());

        // which of the two it is: a caller may explode only when the rules allow, but always throws a die that has never been thrown
        public bool IsExploding => Throws > 0;

        // the one-die pool to hand the tray; a pool of one is ordinary and sits the others out
        public Pool Again() => Pool.Of((LabelKey, Die));

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

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            !IsReal ? "no impact die"
            : Throws == 0 ? $"{Die.Label()} still in the box"
            : Throws == 1 ? $"{Die.Label()} showing {Total}"
            : $"{Die.Label()} exploded {Throws - 1}x to {Total}";
    }
}
