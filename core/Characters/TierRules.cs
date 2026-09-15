namespace Core.Characters
{
    // tier rules stated once here - they were written three times and disagreed
    public static class TierRules
    {
        public static bool HasHealthTrack(this Tier tier) => tier != Tier.Rabble;

        // the hero's count is CombatOptions.HeroActionsPerRound, not this per-tier one
        public static int ActionsPerRound(this Tier tier) => tier == Tier.Dread ? 2 : 1;

        public static int ReactionsPerRound(this Tier tier) => tier == Tier.Dread ? 1 : 0;

        public static int StartingNerve(this Tier tier) =>
            tier == Tier.Rabble ? 0 : Core.Combat.Nerve.StartOfDay;

        public static int NerveCap(this Tier tier) =>
            tier == Tier.Rabble ? 0 : Core.Combat.Nerve.Cap;

        public static Attr AttackAttr(this Tier tier) => Attr.Might;

        public static Skill AttackSkill(this Tier tier) => tier == Tier.Rabble ? Skill.None : Skill.Blades;
    }
}
