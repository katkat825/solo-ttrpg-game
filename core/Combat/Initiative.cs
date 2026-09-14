using Core.Characters;
using Core.Resolution;

namespace Core.Combat
{
    // WHO GOES FIRST, AND ONLY ONCE (CORE_RULES.md section 8).
    //
    // `Grace + Insight`, best two, rolled at the start of the fight and never again. The "never
    // again" is the decision worth writing down: re-rolling every round is what a lot of systems
    // do, and in a single-player game it is a pause with no decision in it - the player watches a
    // number appear and then does exactly what they were going to do anyway. Once, at the top, is
    // one throw and a turn order you can read off the table for the rest of the fight.
    //
    // NO GEAR DIE. `Actor.BuildPool` adds the weapon by default and initiative is the one check
    // where that is plainly wrong - an axe does not help you notice things first. This is the only
    // caller in the engine that passes `useWeapon: false`, which is exactly why the parameter
    // exists.
    //
    // AN ACTOR WITH NO DICE FOR IT ROLLS NOTHING AND GOES LAST. A Rabble's statblock is Might and
    // a club; it has no Grace and no Insight, so the pool is empty and the score is zero. That is
    // not a special case bolted on - it is the same rule as everywhere else in this system, where
    // untrained means a smaller pool rather than a penalty (CORE_RULES.md section 1), taken to its
    // end. A mob is slow off the mark, which is also what it should look like.
    public static class Initiative
    {
        public const Attr Attribute = Attr.Grace;

        public const Skill Skill = Core.Characters.Skill.Insight;

        // what this actor throws for it - one die, two, or none at all
        public static Pool PoolFor(Actor actor) =>
            actor?.BuildPool(Attribute, Skill, useWeapon: false) ?? new Pool();

        // and what it comes to. Zero for an actor with nothing to throw, rather than an exception
        // out of the resolver - "no Grace at all" is a statblock, not a mistake
        public static int Roll(IResolver resolver, Actor actor)
        {
            if (resolver == null || actor == null) return 0;

            Pool pool = PoolFor(actor);

            return pool.Count == 0 ? 0 : resolver.Resolve(pool).Total;
        }
    }
}
