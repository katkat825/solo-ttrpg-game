using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Game.Tray
{
    // degrades rather than throwing: a pool and felt that disagree record it in Disagreement and leave marks off, so a save reads back instead of crashing
    public sealed class TrayThrow
    {
        public PoolResult Result { get; }

        // the pool as fed in, in throw order
        public IReadOnlyList<TraySlot> Slots { get; }

        // each die's role in throw order, so the view marks Dice[i] from Roles[i]
        // needed because PoolResult.Rolls is in result order (sorted by value); indexing dice with it would silently ring the wrong ones
        public IReadOnlyList<DieRole> Roles { get; }

        // which felt die showed the 1, in throw order, -1 when none; kept orthogonal to Roles because the snagged die is also counted or Impact
        // the rules decide that it snagged (one 1); this only finds where it landed - two or more 1s is Trouble, not this
        public int SnaggedSlot { get; }

        // what doesn't add up, empty when everything does, never null
        // a pool and felt that came apart produce a throw with no marks, not no throw, so a save loads with a warning rather than not at all
        public string Disagreement { get; }

        public bool Agrees => Disagreement.Length == 0;

        public TrayThrow(PoolResult result, IReadOnlyList<TraySlot> slots)
        {
            Result = result;
            Slots = slots ?? System.Array.Empty<TraySlot>();

            var apart = new List<string>();

            Roles = Assign(result, Slots, apart);
            SnaggedSlot = FindSnag(result, Slots, apart);

            Disagreement = string.Join(" ", apart);
        }

        // rebuild a throw from saved faces via PoolResult.From, the same arithmetic the resolver ran, so a restored throw can't disagree with the saved one
        // the file sets only the faces, never a second field that could contradict them
        public static TrayThrow Read(IReadOnlyList<TraySlot> slots)
        {
            var faces = new List<(string, Die, int)>();

            foreach (TraySlot slot in slots ?? System.Array.Empty<TraySlot>())
                faces.Add((slot.LabelKey, slot.Die, slot.Value));

            return new TrayThrow(PoolResult.From(faces), slots);
        }

        // put each roll back on the die it came off, matched on trait, size and face, each slot claimed once so identical dice get one apiece
        // a roll with no die under it is recorded as a disagreement and left unmarked, not quietly given the wrong die
        static DieRole[] Assign(PoolResult result, IReadOnlyList<TraySlot> slots,
                                List<string> apart)
        {
            var roles = new DieRole[slots.Count];
            var claimed = new bool[slots.Count];
            var slotOf = new int[result.Rolls.Count];

            for (int i = 0; i < result.Rolls.Count; i++)
            {
                RolledDie roll = result.Rolls[i];
                int found = -1;

                for (int s = 0; s < slots.Count; s++)
                {
                    if (claimed[s]) continue;
                    if (slots[s].LabelKey != roll.LabelKey) continue;
                    if (slots[s].Die != roll.Die || slots[s].Value != roll.Value) continue;

                    found = s;
                    break;
                }

                // unmarked rather than mismarked: None draws no ring, the one outcome that makes no claim about the rules
                if (found < 0)
                {
                    apart.Add($"The resolver returned {roll}, which no die on the felt threw.");
                    slotOf[i] = -1;
                    continue;
                }

                claimed[found] = true;
                slotOf[i] = found;
                roles[found] = roll.Counted ? DieRole.Counted : DieRole.None;
            }

            // Impact is a size, not an index: mark the first leftover of that size; a short pool's d4 fallback is no die anything rolled, so nothing gets marked
            for (int i = 0; i < result.Rolls.Count; i++)
            {
                if (result.Rolls[i].Counted || result.Rolls[i].Die != result.Impact) continue;

                if (slotOf[i] < 0) continue;

                roles[slotOf[i]] = DieRole.Impact;
                break;
            }

            return roles;
        }

        // find the felt die showing the 1: PoolResult counts 1s but doesn't say which die carried one
        // a Snag with no 1, or a second uncounted 1, each mean the felt and pool disagree, so the snag points at nothing rather than the wrong die
        static int FindSnag(PoolResult result, IReadOnlyList<TraySlot> slots, List<string> apart)
        {
            if (!result.Snag) return -1;

            int found = -1;

            for (int s = 0; s < slots.Count; s++)
            {
                if (slots[s].Value != 1) continue;

                if (found >= 0)
                {
                    apart.Add("The rules called one 1 and the felt is showing more than one.");
                    return -1;
                }

                found = s;
            }

            if (found < 0)
                apart.Add("The rules called a Snag and no die on the felt is showing a 1.");

            return found;
        }

        // true when nothing was left over, so Result.Impact is the resolver's d4 fallback, not a felt die
        public bool ImpactIsFallback => Result.Rolls.All(r => r.Counted);

        // what the Impact die shows on the felt now, or 0 when nothing was left over
        // read off the table, not rolled again: rolling the Impact die a second time would be a hidden roll, and every roll is meant to be visible on the felt
        public int ImpactValue
        {
            get
            {
                for (int i = 0; i < Roles.Count; i++)
                    if (Roles[i] == DieRole.Impact) return Slots[i].Value;

                return 0;
            }
        }

        // the Impact die's trait, so an exploding throw is a one-die pool that wears the same name on the second throw
        public string ImpactLabelKey
        {
            get
            {
                for (int i = 0; i < Roles.Count; i++)
                    if (Roles[i] == DieRole.Impact) return Slots[i].LabelKey;

                return "";
            }
        }

        // developer only, not localized
        // the numbers are read out of PoolResult, not recomputed, so any mismatch is a view bug
        public IEnumerable<string> DebugLines(int difficulty, ILocalizer text = null)
        {
            yield return "pool   " + string.Join(" | ", Slots.Select(
                s => $"{s.LabelKey} {s.Die.Label()}->{s.Value}"));

            if (text != null)
                yield return "words  " + string.Join(" | ", Slots.Select(
                    s => $"{text.Get(s.LabelKey)} {s.Die.Label()}->{s.Value}"));

            yield return "rules  " + Result;

            var counted = Result.Rolls.Where(r => r.Counted).Select(r => r.Value).ToList();
            var spare = Result.Rolls.FirstOrDefault(r => !r.Counted);

            // in throw order, so it lines up with the dice left to right on the felt - the line you check the rings against
            yield return "roles  " + string.Join(" | ", Slots.Select(
                (s, i) => $"{s.LabelKey} {s.Die.Label()}->{s.Value} {Roles[i].ToString().ToLowerInvariant()}"));

            yield return $"best   {string.Join(" + ", counted)} = {Result.Total}" +
                         (ImpactIsFallback
                             ? $", impact {Result.Impact.Label()} is the resolver's fallback, not a die"
                             : $", impact is the leftover {Result.Impact.Label()} showing {spare.Value} ({spare.LabelKey})");

            if (Result.Trouble) yield return "TROUBLE  two or more 1s - a real consequence";
            else if (Result.Snag)
                yield return $"snag   {Slots[SnaggedSlot].LabelKey} {Slots[SnaggedSlot].Die.Label()} " +
                             "showing 1 - cosmetic, the companion's cue to speak";

            int margin = Result.Total - difficulty;
            yield return $"vs {difficulty,-2}  {(Result.Beats(difficulty) ? "beats it" : "falls short")} by {Math.Abs(margin)}";
        }
    }
}
