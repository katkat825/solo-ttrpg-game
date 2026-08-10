using Rules.Characters;
using Rules.Dice;
using Rules.Resolution;

namespace Sim
{
    // the three benchmark pools every report runs against
    // starting, skilled and master
    // built from real trait keys, so the sim walks the same path the game does
    static class SamplePools
    {
        public readonly struct Sample
        {
            // DEVELOPER ONLY - a row label for the console table, never localized
            public readonly string DebugName;

            public readonly Die AttrDie;
            public readonly Die SkillDie;
            public readonly Die GearDie;

            public Sample(string debugName, Die attrDie, Die skillDie, Die gearDie)
            {
                DebugName = debugName;
                AttrDie = attrDie;
                SkillDie = skillDie;
                GearDie = gearDie;
            }

            public Pool Build() => Pool.Of(
                (Attr.Might.Key(), AttrDie),
                (Skill.Blades.Key(), SkillDie),
                (Rules.Localization.KeyConventions.GearName("axe"), GearDie));
        }

        public static readonly Sample[] All =
        {
            new Sample("starting  d8+d6+d6",   Die.D8,  Die.D6,  Die.D6),
            new Sample("skilled   d10+d8+d6",  Die.D10, Die.D8,  Die.D6),
            new Sample("master    d12+d10+d8", Die.D12, Die.D10, Die.D8),
        };
    }
}
