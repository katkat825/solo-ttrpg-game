using Core.Characters;
using Core.Combat;
using Core.Resolution;

namespace Game.Fight
{
    // THE ONE EFFECT, AND THE ONLY HARDCODED CONTENT IN THIS MILESTONE (COMBAT_LOOP.md C5).
    //
    // "One placeholder effect is enough - the LIST of effects and their difficulties is content
    // (Phase P/R), the MECHANIC is rules and lives here." So the mechanic is
    // `Core.Combat.Channeling` and this is the placeholder: a bolt of something at a foe the
    // caster can see. It is the same shape as `Board/DoorCheck.cs` and it is here for the same
    // reason - in a file of its own so that swapping in a campaign's spell list is a deletion
    // rather than an excavation, and Godot-free so what it decides is testable without an engine.
    //
    // A BOLT, AND NOT A BUFF, because it has to be watchable. Phase C's whole argument is that
    // every milestone ends in something you can look at, and a foe across the room losing Vigor
    // from a die that never touched it is the shortest sentence that says "magic is the same
    // roll" out loud. It also exercises the geometry B3 built and nothing has used yet: you must
    // be able to SEE what you are throwing it at, which is a line between two squares and not a
    // distance.
    //
    // THE DIFFICULTY IS THE TARGET'S DEFENSE, so a bolt and a swing are measured against the same
    // number - which is what makes "magic is the same roll" true rather than merely claimed.
    // Bigger effects raise the difficulty rather than adding a spell level (CORE_RULES.md section
    // 10), and that is where a campaign's list of effects lives.
    public static class TheEffect
    {
        public const Attr Attribute = Channeling.Attribute;

        public const Skill Skill = Channeling.Skill;

        // WHAT IT COSTS TO THROW, whether it lands or not - one step off the Heart die until a
        // rest. Named here rather than called at the call site so the cost and the effect are one
        // thing a campaign replaces together
        public static Pool Cast(Actor caster)
        {
            Pool pool = Channeling.PoolFor(caster);

            Channeling.Strains(caster);

            return pool;
        }

        // whether this caster would be offered it at all. An untrained one can channel and will
        // get nothing out of it (Channeling.IsTrained says why), so a click at range walks him
        // over instead
        public static bool Offered(Actor caster) => Channeling.IsTrained(caster);
    }
}
