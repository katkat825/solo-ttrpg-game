using Content.Kits;
using Core.Characters;
using Core.Resolution;

namespace Game.Fight
{
    public static class TheEffect
    {
        public static Ability Ability => SharedKit.Bolt;

        public static Attr Attribute => Ability.Attribute;

        public static Skill Skill => Ability.Skill;

        // paid on cast, not on hit
        public static Pool Cast(Actor caster)
        {
            Pool pool = Ability.PoolFor(caster);

            Ability.Pay(caster);

            return pool;
        }

        public static bool Offered(Actor caster) => Ability.Offered(caster);
    }
}
