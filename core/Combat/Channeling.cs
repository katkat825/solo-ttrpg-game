using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Core.Combat
{
    // MAGIC IS THE SAME ROLL (CORE_RULES.md section 10).
    //
    // `Heart + Channeling + focus`, best two against a difficulty, and the Impact die is the
    // effect's power. No spell slots, no prepared lists, no per-day counters, no second subsystem
    // to build and balance and debug. That is the whole of it, and it is why this file is short:
    // there is nothing here the resolver does not already do. What it adds is the cost.
    //
    // THE COST IS STRAIN, AND STRAIN IS THE SAME MECHANIC AS EVERYTHING ELSE. Channelling steps
    // your Heart die down until you rest, which makes further casting worse AND makes you fragile
    // to Shaken - a Heart already strained to the floor has nowhere to go, and a Condition landing
    // on a die with nothing left to give is what finishes you (CORE_RULES.md section 9,
    // `Actor.IsOverwhelmed`). The resource attrition that spell slots exist to provide, out of one
    // line of rules and no new UI.
    //
    // Strain is a `TraitModifier` from one source, which is what F3's pipeline was built for: it
    // stacks, the ladder clamps and remembers the overflow, and a rest takes off exactly what
    // channelling put on and nothing else (`Actor.MendStrain`).
    //
    // WHAT IS NOT HERE: the effects. A spell list is a list of effects and their difficulties,
    // which is content and belongs in a campaign (Phase P/R). Bigger effects raise the difficulty
    // rather than adding a spell level. The one placeholder the game has lives in `game/Fight` for
    // exactly as long as `Board/DoorCheck.cs` does, and for the same reason.
    public static class Channeling
    {
        public const Attr Attribute = Attr.Heart;

        public const Skill Skill = Core.Characters.Skill.Channeling;

        // WHAT A CASTER THROWS. The focus is the gear die - `Actor` has one gear slot and a focus
        // is what a caster keeps in it, the way a blade is what a fighter keeps in it. A second
        // slot is an inventory, and CORE_RULES.md section 13 keeps a second inventory dead
        public static Pool PoolFor(Actor caster) =>
            caster?.BuildPool(Attribute, Skill, useWeapon: true) ?? new Pool();

        // trained, and with a Heart die to throw. An UNTRAINED caster is a real thing the rules
        // handle - a smaller pool, no leftover die, so you can succeed and cannot succeed hard
        // (CORE_RULES.md section 1) - and this is not a rule refusing them. It is the question a
        // presentation layer asks before offering channelling as a gesture, because a two-die pool
        // whose Impact is always the d4 fallback is an effect with no power in it, and offering it
        // as the answer to a click would be offering nothing
        public static bool IsTrained(Actor caster) =>
            caster != null
            && caster.Attribute(Attribute).IsReal()
            && caster.SkillDie(Skill).IsReal();

        // AND THE COST, PAID WHETHER IT WORKED OR NOT. A spell that fizzled still took it out of
        // you, which is what makes a bad throw expensive rather than merely disappointing
        public static void Strains(Actor caster) => caster?.AddStrain();
    }
}
