using System.Collections.Generic;
using System.Linq;
using Core.Dice;

namespace Core.Resolution
{
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

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{LabelKey} {Die.Label()}->{Value}{(Counted ? "" : " (unused)")}";
    }

    public sealed class PoolResult
    {
        public IReadOnlyList<RolledDie> Rolls { get; }

        public int Total { get; }

        // largest die not counted toward the total - d4 if nothing is left over
        public Die Impact { get; }

        public int Ones { get; }

        public PoolResult(IReadOnlyList<RolledDie> rolls, int total, Die impact, int ones)
        {
            Rolls = rolls;
            Total = total;
            Impact = impact;
            Ones = ones;
        }

        public bool Snag => Ones == 1;

        public bool Trouble => Ones >= 2;

        public bool Beats(int difficulty) => Total >= difficulty;


        // sum the best two; largest leftover is Impact; ties count the smaller so the larger stays free for Impact
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

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            string.Join(", ", Rolls.Select(r => r.ToString())) +
            $" | total {Total} | impact {Impact.Label()}" +
            (Trouble ? " | TROUBLE" : Snag ? " | snag" : "");
    }
}
