using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Game.Tray
{
    // one throw: what the table produced, and what the rules made of it
    //
    // IT DEGRADES RATHER THAN THROWING, and that is P6's doing (SEAMS.md section 9, which named
    // this class as the specific obstacle to save/load: "a saved throw whose rolls and slots
    // disagree would crash rather than degrade"). Two things used to be exceptions - a roll that
    // no die on the felt threw, and a Snag with no 1 under it - and both are now recorded in
    // `Disagreement` with the marks left off rather than the run ended.
    //
    // DEGRADING IS NOT SHRUGGING. The original comments were right that a cue pointing at the
    // wrong die is a silent lie, so nothing here is silent: a disagreement is a sentence, the
    // live table prints it (DiceTray), and the marks that would have been wrong are simply not
    // drawn. What changed is only WHO decides it is fatal - a save being read back is a thing to
    // report and carry on from, and the table coming apart mid-fight is a bug to shout about, and
    // an exception in here could not tell them apart.
    public sealed class TrayThrow
    {
        public PoolResult Result { get; }

        // the pool as fed in, in throw order
        public IReadOnlyList<TraySlot> Slots { get; }

        // what each die on the felt is, in THROW order
        // so the view can mark Dice[i] from Roles[i] and reason about nothing
        //
        // this exists because PoolResult.Rolls is in RESULT order, sorted by value
        // Rolls[0] is the highest die, not the first die thrown
        // indexing the dice with it highlights the wrong ones, plausibly and silently -
        // the right NUMBER of rings on the wrong dice, and only wrong when the throw
        // came out in a different order than it was made
        public IReadOnlyList<DieRole> Roles { get; }

        // which die on the felt showed the 1, in THROW order, or -1 when nothing snagged
        //
        // orthogonal to Roles and it has to be: the die that snagged is also either counted
        // or the Impact die, so a snag is a fourth DieRole value only if you are willing to
        // lose which of the other three it was
        //
        // the rules decide THAT it snagged - PoolResult.Snag, exactly one 1 - and this only
        // finds where it landed. two or more 1s is Trouble, a different cue with its own bark,
        // and deliberately not this
        public int SnaggedSlot { get; }

        // WHAT DOES NOT ADD UP, or empty when everything does. Never null.
        //
        // A pool and a felt that have come apart produce a throw with no marks on it rather than
        // no throw at all - which is the difference between a save that loads with a warning and a
        // save that cannot be loaded (CONTENT_PIPELINE.md P6)
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

        // A THROW READ OFF THE FELT RATHER THAN ROLLED (P6). The faces are the whole of what a
        // saved throw is, and what they MEAN is `PoolResult.From` - the same arithmetic the
        // resolver runs, so a restored throw cannot disagree with the one that was saved.
        //
        // This is the shape SEAMS.md section 9 was asking for: nothing is computed in a
        // constructor from two fields that a file could have set inconsistently, because the file
        // only sets one of them
        public static TrayThrow Read(IReadOnlyList<TraySlot> slots)
        {
            var faces = new List<(string, Die, int)>();

            foreach (TraySlot slot in slots ?? System.Array.Empty<TraySlot>())
                faces.Add((slot.LabelKey, slot.Die, slot.Value));

            return new TrayThrow(PoolResult.From(faces), slots);
        }

        // puts each roll back on the die it came off
        // matched on trait, size and face together, each slot claimed at most once,
        // so two identical dice are handed out one apiece
        // the lists are a permutation of each other, so a roll with no die under it is recorded
        // as a disagreement and left unmarked, rather than quietly given the wrong die
        //
        // NOTHING HERE RE-DECIDES ANYTHING - Counted comes from RolledDie.Counted,
        // Impact from PoolResult.Impact
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

                // UNMARKED RATHER THAN MISMARKED. The die keeps DieRole.None, which draws no
                // ring at all - the one outcome that cannot be read as a claim about the rules
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

            // Impact is a SIZE in PoolResult, not an index, so the die carrying it is the first
            // leftover of that size - never ambiguous with three dice, exactly one leftover
            // a shorter pool has none, and the resolver's d4 fallback is not a die anything
            // rolled, so nothing gets the mark
            for (int i = 0; i < result.Rolls.Count; i++)
            {
                if (result.Rolls[i].Counted || result.Rolls[i].Die != result.Impact) continue;

                if (slotOf[i] < 0) continue;

                roles[slotOf[i]] = DieRole.Impact;
                break;
            }

            return roles;
        }

        // the slot showing the 1, once the rules have called it a Snag
        // scanning for the 1 rather than being handed it, because PoolResult counts 1s and does
        // not say which die carried one - the tier is the rules' answer and the address is ours
        //
        // both failures are recorded and neither is guessed at: a Snag with no 1 on the felt, or
        // a second 1 the rules did not count, each mean the pool and the table have come apart,
        // and a cue pointing at the wrong die is exactly the silent lie this exists to prevent. So
        // an ambiguous snag points at NOTHING - no cue is better than the wrong one
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

        // no die was left over, so Result.Impact is the resolver's d4 fallback, not a real die
        // cannot happen with three dice, kept so a short pool never reads "impact d4" as a result
        public bool ImpactIsFallback => Result.Rolls.All(r => r.Counted);

        // WHAT THE IMPACT DIE IS SHOWING, on the felt, right now - or 0 when nothing was left over.
        //
        // Read off the table rather than rolled again. CombatEngine asks its resolver to roll the
        // Impact die a second time (and explode it), which is right for a fight nobody is watching;
        // in front of a player it would be a hidden roll deciding how hard the hero hit something,
        // and CORE_RULES 0 pillar 1 says every roll is a visible handful hitting the table. The
        // magnitude is already lying there with a ring round it - B4 uses THIS one.
        //
        // Reads Roles and Slots and decides nothing: the rules said which die was left over.
        public int ImpactValue
        {
            get
            {
                for (int i = 0; i < Roles.Count; i++)
                    if (Roles[i] == DieRole.Impact) return Slots[i].Value;

                return 0;
            }
        }

        // AND WHICH TRAIT IT IS, so the die can be picked up and thrown again wearing the same
        // name. An exploding Impact die (COMBAT_LOOP.md C2) is thrown alone as a pool of one, and
        // a pool needs a label key for every die in it - taking the trait off the felt rather than
        // inventing one is what keeps the mark on the second throw saying what the first said
        //
        // Reads Roles and Slots and decides nothing, exactly as ImpactValue does. Empty when
        // nothing was left over
        public string ImpactLabelKey
        {
            get
            {
                for (int i = 0; i < Roles.Count; i++)
                    if (Roles[i] == DieRole.Impact) return Slots[i].LabelKey;

                return "";
            }
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        // the best-two line is read out of PoolResult rather than recomputed,
        // so a mismatch is always a view bug and never a rules bug
        //
        // it lays translated fragments out in a fixed order, which a player-facing
        // sentence must never do - that is why this stays developer output
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

            // in throw order, so it lines up with the "tray" line above and with the dice
            // left to right on the felt - this is the line you check the rings against
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
