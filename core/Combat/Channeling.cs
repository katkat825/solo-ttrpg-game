using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Core.Combat
{
    public static class Channeling
    {
        public const Attr Attribute = Attr.Heart;

        public const Skill Skill = Core.Characters.Skill.Channeling;

        public static Pool PoolFor(Actor caster) =>
            caster?.BuildPool(Attribute, Skill, useWeapon: true) ?? new Pool();

        // a presentation gate, not a rule - a two-die pool's Impact is always the d4 fallback, an effect with no power
        public static bool IsTrained(Actor caster) =>
            caster != null
            && caster.Attribute(Attribute).IsReal()
            && caster.SkillDie(Skill).IsReal();

        public static void Strains(Actor caster) => caster?.AddStrain();
    }
}
