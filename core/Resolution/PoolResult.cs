using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Resolution
{
    // the outcome of one throw, and the dice that made it
    // every die keeps its key, size, value and whether it counted
    // so presentation can animate and label what happened
    // without recomputing anything or knowing a rule
    public readonly struct RolledDie
    {
        public readonly string LabelKey;

        public readonly Die Die;
        public readonly int Value;
        public readonly bool Counted;

        public RolledDie(string labelKey, Die die, int value, bool counted)
        {
            LabelKey = labelKey;
            Die = die;
            Value = value;
            Counted = counted;
        }

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            $"{LabelKey} {Die.Label()}->{Value}{(Counted ? "" : " (unused)")}";
    }

    public sealed class PoolResult
    {
        public IReadOnlyList<RolledDie> Rolls { get; }

        public int Total { get; }

        // largest die NOT counted toward the total - d4 if nothing is left over
        public Die Impact { get; }

        public int Ones { get; }

        public PoolResult(IReadOnlyList<RolledDie> rolls, int total, Die impact, int ones)
        {
            Rolls = rolls;
            Total = total;
            Impact = impact;
            Ones = ones;
        }

        // exactly one 1 - cosmetic, the companion's cue to speak
        public bool Snag => Ones == 1;

        // two or more - a real mechanical consequence, about 6% of rolls
        public bool Trouble => Ones >= 2;

        public bool Beats(int difficulty) => Total >= difficulty;

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            string.Join(", ", Rolls.Select(r => r.ToString())) +
            $" | total {Total} | impact {Impact.Label()}" +
            (Trouble ? " | TROUBLE" : Snag ? " | snag" : "");
    }
}
