using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Dice;

namespace Core.Combat
{
    // AT HALF VIGOR, A DREAD BECOMES A DIFFERENT FIGHT (CORE_RULES.md section 8).
    //
    // "Boss. 2 actions, a reaction, and a phase change - at half health its dice re-rate and its
    // behaviour shifts. One per chapter." The two halves are deliberately different kinds of
    // thing:
    //
    //   THE DICE RE-RATE. A `TraitModifier` with a POSITIVE step, which is the first caller
    //   `Die.StepUp` has ever had - it was written as the buff half of the ladder and sat there
    //   with only its own test for company (SEAMS.md section 3). F3's pipeline was built so that
    //   order would not matter when something could finally move a die upward, and this is that
    //   something: a Dread that is Winded and then phases lands on the same die as one that
    //   phases and is then Winded.
    //
    //   THE BEHAVIOUR SHIFTS. A different `ITargetSelector`, attached through
    //   `Encounter.Behaviour` - "this is where per-enemy behaviour injection earns its seam".
    //   Honesty about that: with one hero on the board and no party (CORE_RULES.md section 13),
    //   every selector returns the hero and the swap changes nothing anybody can see. It is
    //   exercised by a test rather than at the table, and it is here because the alternative is
    //   discovering at Phase P that the seam was never wired to anything.
    //
    // EXACTLY AT THE THRESHOLD, AND ONCE. `Turned` is what makes it once; the threshold is
    // half of MaxVigor and it is crossed rather than sat on, so a Dread that is healed back above
    // it does not get a second phase to spend.
    //
    // WHAT IT DOES NOT DO: decide when it is checked. `Encounter` asks after every blow, which is
    // the only moment vigor can have moved.
    public sealed class PhaseChange
    {
        readonly Actor _actor;

        readonly TraitModifier[] _rerated;

        // <paramref name="then"/> may be null, which means the behaviour does not change and only
        // the dice do - a perfectly good boss, and one fewer moving part
        public PhaseChange(Actor actor, ITargetSelector then, params TraitModifier[] rerated)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _rerated = rerated ?? Array.Empty<TraitModifier>();

            Then = then;
        }

        public Actor Actor => _actor;

        public ITargetSelector Then { get; }

        public IReadOnlyList<TraitModifier> Rerated => _rerated;

        public bool Turned { get; private set; }

        // half of what it started with, or less. A boss taken from full to below half in one blow
        // still phases - it is a threshold and not a step it has to land on
        public bool Due => !Turned && !_actor.IsDown && _actor.Vigor * 2 <= _actor.MaxVigor;

        public bool Turn()
        {
            if (Turned) return false;

            Turned = true;

            foreach (TraitModifier m in _rerated) _actor.AddModifier(m);

            return true;
        }

        // ---- and the one the built-in roster uses ----

        // WHERE THIS SOURCE ID COMES BACK: a phase change is removable by source like anything
        // else in the pipeline, so a campaign that wants a boss to phase back has the handle
        public static readonly ModifierSource Source = ModifierSource.Effect("phase");

        // TEMPORARY, exactly as long as BuiltInArchetypes is - what a boss turns INTO is content
        // and belongs in a campaign's statblock (Phase P). It is here rather than in `game/`
        // because the sim needs it too, and the sim cannot see Godot.
        //
        // One step up on everything it has, and it stops clearing the room and comes for you. The
        // step is +1 and not +2 because the ladder is short: a d10 that becomes a d12 has run out
        // of ladder, and a boss whose second phase is mostly saturation is a boss whose second
        // phase does nothing (CORE_RULES.md section 9)
        public static PhaseChange Standard(Actor dread)
        {
            var rerated = new List<TraitModifier>();

            foreach (Attr a in Enum.GetValues<Attr>())
                if (dread.BaseAttribute(a).IsReal())
                    rerated.Add(new TraitModifier(Source, a, +1));

            return new PhaseChange(dread, new StrongestFirstSelector(), rerated.ToArray());
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() =>
            $"{_actor.DebugName} phase {(Turned ? "turned" : Due ? "due" : "waiting")} " +
            $"at {_actor.Vigor}/{_actor.MaxVigor}";
    }
}
