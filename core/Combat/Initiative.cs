using Core.Characters;
using Core.Resolution;

namespace Core.Combat
{
    // no gear die - the only caller that passes useWeapon: false, since an axe doesn't help you notice things
    public static class Initiative
    {
        public const Attr Attribute = Attr.Grace;

        public const Skill Skill = Core.Characters.Skill.Insight;

        public static Pool PoolFor(Actor actor) =>
            actor?.BuildPool(Attribute, Skill, useWeapon: false) ?? new Pool();

        // zero for an empty pool, not an exception - no Grace is a statblock, not a mistake
        public static int Roll(IResolver resolver, Actor actor)
        {
            if (resolver == null || actor == null) return 0;

            Pool pool = PoolFor(actor);

            return pool.Count == 0 ? 0 : resolver.Resolve(pool).Total;
        }
    }
}
