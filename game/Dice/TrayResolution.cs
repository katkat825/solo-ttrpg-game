using System;
using System.Collections.Generic;
using System.Linq;
using Rules.Characters;
using Rules.Dice;
using Rules.Localization;
using Rules.Resolution;

// turns the faces three dice settled on into a real PoolResult
// the physics owns every raw number, rules/ owns what they mean
// nothing here reimplements a rule - the moment it starts adding dice up itself, it is wrong
// Godot-free, so it is testable headless

public sealed class TrayResolution
{
    // stands in for Actor.BuildPool(Attr.Might, Skill.Blades) until there is a hero
    // same shape deliberately, so the swap is a one-line change
    // keys, never display text
    static readonly string[] PoolLabels =
    {
        Attr.Might.Key(),
        Skill.Blades.Key(),
        KeyConventions.GearName("axe"),
    };

    public static int PoolSize => PoolLabels.Length;

    readonly Die[] _dice;

    // dice: the sizes on the felt, in throw order - a description, not a preference
    public TrayResolution(IReadOnlyList<Die> dice)
    {
        if (dice == null) throw new ArgumentNullException(nameof(dice));

        if (dice.Count != PoolSize)
            throw new ArgumentException(
                $"The pool is {PoolSize} dice; got {dice.Count}.", nameof(dice));

        foreach (Die die in dice)
            if (!die.IsReal())
                throw new ArgumentException("A pool cannot contain a die that isn't there.", nameof(dice));

        _dice = dice.ToArray();
    }

    public IReadOnlyList<Die> Dice => _dice;

    // tableValues: one face per die in THROW order - not settle order, and not sorted
    // the pool is built in the same order, so index i came from throw point i
    public TrayThrow Resolve(IReadOnlyList<int> tableValues)
    {
        if (tableValues == null) throw new ArgumentNullException(nameof(tableValues));

        if (tableValues.Count != PoolSize)
            throw new ArgumentException(
                $"The pool is {PoolSize} dice; got {tableValues.Count} values.", nameof(tableValues));

        var pool = new Pool();
        var slots = new List<TraySlot>();

        for (int i = 0; i < PoolSize; i++)
        {
            string labelKey = PoolLabels[i];
            Die die = _dice[i];
            int value = tableValues[i];

            if (value < 1 || value > die.Sides())
                throw new ArgumentOutOfRangeException(
                    nameof(tableValues), value, $"Not a face on a {die.Label()}.");

            pool.Add(labelKey, die);
            slots.Add(new TraySlot(labelKey, die, value));
        }

        // load-bearing coupling
        // ScriptedRng returns its values in call order, StandardResolver rolls in pool order
        // reorder one without the other and the dice start lying about which trait they were,
        // silently
        var rng = new ScriptedRng(slots.Select(s => s.Value).ToArray());
        PoolResult result = new StandardResolver(rng).Resolve(pool);

        return new TrayThrow(result, slots);
    }
}

// one entry in the pool as handed to the resolver, kept in throw order
public readonly struct TraySlot
{
    public readonly string LabelKey;
    public readonly Die Die;
    public readonly int Value;

    public TraySlot(string labelKey, Die die, int value)
    {
        LabelKey = labelKey;
        Die = die;
        Value = value;
    }
}

// what a die on the felt is, once the rules have looked at the throw
// the only thing the view is allowed to decide highlighting from
public enum TrayMark
{
    // rolled, didn't count, isn't Impact - impossible in a three-die pool
    None,

    Counted,

    // the largest die left over - the one thrown again for damage
    Impact,
}

// one throw: what the table produced, and what the rules made of it
public sealed class TrayThrow
{
    public PoolResult Result { get; }

    // the pool as fed in, in throw order
    public IReadOnlyList<TraySlot> Slots { get; }

    // what each die on the felt is, in THROW order
    // so the view can mark Dice[i] from Marks[i] and reason about nothing
    //
    // this exists because PoolResult.Rolls is in RESULT order, sorted by value
    // Rolls[0] is the highest die, not the first die thrown
    // indexing the dice with it highlights the wrong ones, plausibly and silently -
    // the right NUMBER of rings on the wrong dice, and only wrong when the throw
    // came out in a different order than it was made
    public IReadOnlyList<TrayMark> Marks { get; }

    // which die on the felt showed the 1, in THROW order, or -1 when nothing snagged
    //
    // orthogonal to Marks and it has to be: the die that snagged is also either counted
    // or the Impact die, so a snag is a fourth TrayMark value only if you are willing to
    // lose which of the other three it was
    //
    // the rules decide THAT it snagged - PoolResult.Snag, exactly one 1 - and this only
    // finds where it landed. two or more 1s is Trouble, a different cue with its own bark,
    // and deliberately not this
    public int SnaggedSlot { get; }

    public TrayThrow(PoolResult result, IReadOnlyList<TraySlot> slots)
    {
        Result = result;
        Slots = slots;
        Marks = Assign(result, slots);
        SnaggedSlot = FindSnag(result, slots);
    }

    // puts each roll back on the die it came off
    // matched on trait, size and face together, each slot claimed at most once,
    // so two identical dice are handed out one apiece
    // the lists are a permutation of each other, so no match throws rather than
    // quietly leaving a die unmarked
    //
    // NOTHING HERE RE-DECIDES ANYTHING - Counted comes from RolledDie.Counted,
    // Impact from PoolResult.Impact
    static TrayMark[] Assign(PoolResult result, IReadOnlyList<TraySlot> slots)
    {
        var marks = new TrayMark[slots.Count];
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

            if (found < 0)
                throw new InvalidOperationException(
                    $"The resolver returned {roll}, which no die on the felt threw. " +
                    "The pool and the table have come apart.");

            claimed[found] = true;
            slotOf[i] = found;
            marks[found] = roll.Counted ? TrayMark.Counted : TrayMark.None;
        }

        // Impact is a SIZE in PoolResult, not an index, so the die carrying it is the first
        // leftover of that size - never ambiguous with three dice, exactly one leftover
        // a shorter pool has none, and the resolver's d4 fallback is not a die anything
        // rolled, so nothing gets the mark
        for (int i = 0; i < result.Rolls.Count; i++)
        {
            if (result.Rolls[i].Counted || result.Rolls[i].Die != result.Impact) continue;

            marks[slotOf[i]] = TrayMark.Impact;
            break;
        }

        return marks;
    }

    // the slot showing the 1, once the rules have called it a Snag
    // scanning for the 1 rather than being handed it, because PoolResult counts 1s and does
    // not say which die carried one - the tier is the rules' answer and the address is ours
    //
    // both failures throw rather than shrug: a Snag with no 1 on the felt, or a second 1 the
    // rules did not count, each mean the pool and the table have come apart, and a cue
    // pointing at the wrong die is exactly the silent lie TrayThrow.Assign exists to prevent
    static int FindSnag(PoolResult result, IReadOnlyList<TraySlot> slots)
    {
        if (!result.Snag) return -1;

        int found = -1;

        for (int s = 0; s < slots.Count; s++)
        {
            if (slots[s].Value != 1) continue;

            if (found >= 0)
                throw new InvalidOperationException(
                    "The rules called one 1 and the felt is showing more than one.");

            found = s;
        }

        if (found < 0)
            throw new InvalidOperationException(
                "The rules called a Snag and no die on the felt is showing a 1.");

        return found;
    }

    // no die was left over, so Result.Impact is the resolver's d4 fallback, not a real die
    // cannot happen with three dice, kept so a short pool never reads "impact d4" as a result
    public bool ImpactIsFallback => Result.Rolls.All(r => r.Counted);

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
        yield return "marks  " + string.Join(" | ", Slots.Select(
            (s, i) => $"{s.LabelKey} {s.Die.Label()}->{s.Value} {Marks[i].ToString().ToLowerInvariant()}"));

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
