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

        // ---- reading a throw that has already happened ----

        // THE RULES OF READING A HANDFUL OF DICE, separated from the rolling of them: sum the best
        // two, the largest leftover die is Impact, and ties break toward the player by counting the
        // SMALLER die so the larger one is free to be Impact (CORE_RULES.md section 2).
        //
        // `StandardResolver.Resolve` rolls and then calls this, so there is exactly ONE
        // implementation of the arithmetic - which is the whole reason it is here rather than
        // copied. The second caller is save/load (CONTENT_PIPELINE.md P6, SEAMS.md section 9): a
        // saved throw is the FACES that were on the felt and nothing else, and it is read back
        // with this. A save that stored the faces AND the verdict would be two fields that can
        // disagree, and `SEAMS.md` names that disagreement as the specific obstacle to round
        // tripping. Store one, derive the other, and there is nothing left to disagree.
        public static PoolResult From(IReadOnlyList<(string LabelKey, Die Die, int Value)> thrown)
        {
            if (thrown == null || thrown.Count == 0)
                return new PoolResult(System.Array.Empty<RolledDie>(), 0, Die.D4, 0);

            var ordered = thrown
                .OrderByDescending(t => t.Value)
                .ThenBy(t => (int)t.Die)
                .ToList();

            int counted = System.Math.Min(2, ordered.Count);
            int total = ordered.Take(counted).Sum(t => t.Value);

            var unused = ordered.Skip(counted).ToList();
            Die impact = unused.Count > 0 ? unused.Max(t => t.Die) : Die.D4;

            var rolls = new List<RolledDie>();

            for (int i = 0; i < ordered.Count; i++)
                rolls.Add(new RolledDie(ordered[i].LabelKey, ordered[i].Die, ordered[i].Value,
                                        i < counted));

            int ones = thrown.Count(t => t.Value == 1);

            return new PoolResult(rolls, total, impact, ones);
        }

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            string.Join(", ", Rolls.Select(r => r.ToString())) +
            $" | total {Total} | impact {Impact.Label()}" +
            (Trouble ? " | TROUBLE" : Snag ? " | snag" : "");
    }
}
