namespace Core.Characters
{
    // what a tier IS, mechanically - stated here once and read everywhere else.
    //
    // THIS FILE EXISTS BECAUSE ONE RULE WAS WRITTEN DOWN THREE TIMES AND THE THREE DISAGREED
    // (SEAMS.md section 5). "Rabble die to any hit" lived in CombatEngine.Attack, in Actor.Damage
    // and in the Rabble statblock's maxVigor: 1, with no shared code - and the disagreement was
    // real, not theoretical: Actor.Damage ignored the number it was handed and zeroed vigor, so
    // Actor.Damage(0) killed a Rabble. A miss that reported no damage would have removed one.
    //
    // The rule is a property of the TIER, so it is a predicate on the tier, and the two callers
    // ask it rather than restating it. The statblock's vigor stops being an expression of the rule
    // and becomes what it always should have been - a number nothing reads for a Rabble.
    //
    // The action economy is here for the same reason. Actor's constructor said
    // `tier == Tier.Dread ? 2 : 1` and nothing ever read the field (SEAMS.md section 3), so a
    // Dread looked like it acted twice and did not. Encounter reads Actor.ActionsPerRound now and
    // Actor gets its default from here, so there is one sentence about how often a Dread swings.
    public static class TierRules
    {
        // CORE_RULES.md section 8: Rabble have no health track. Any successful hit removes one -
        // they exist for pressure and positioning, never grind
        public static bool HasHealthTrack(this Tier tier) => tier != Tier.Rabble;

        // the hero's two actions are not here: they are CombatOptions.HeroActionsPerRound, because
        // the hero acting more than everyone else is the balance lever SIMULATION.md section 2
        // measured, and a lever belongs on the dial panel rather than in a per-tier constant
        public static int ActionsPerRound(this Tier tier) => tier == Tier.Dread ? 2 : 1;

        // a Dread has a reaction and ordinary enemies do not (CORE_RULES.md section 8)
        public static int ReactionsPerRound(this Tier tier) => tier == Tier.Dread ? 1 : 0;

        // CORE_RULES.md section 7 is about the hero; a Rabble is not a person with heroic effort
        // in it. Rivals and Dreads carry the same 3 so a campaign can spend it for them later
        public static int StartingNerve(this Tier tier) =>
            tier == Tier.Rabble ? 0 : Core.Combat.Nerve.StartOfDay;

        public static int NerveCap(this Tier tier) =>
            tier == Tier.Rabble ? 0 : Core.Combat.Nerve.Cap;

        // WHAT AN ORDINARY FOE SWINGS WITH, and a placeholder for exactly as long as
        // BuiltInArchetypes is - a monster says this for itself once statblocks are campaign data
        // (Phase P). It is here rather than written out at each caller because it was written out
        // at three of them by C2: CombatEngine.Run, the sim's auto-player and the fight on the
        // table, each naming Rabble and Blades for itself. One rule, one sentence
        //
        // Might because every foe in the built-in roster is a brawler, and Rabble are UNTRAINED
        // rather than penalised - a smaller pool with no leftover die, which is the whole of
        // CORE_RULES.md section 1's "why untrained isn't punished, exactly"
        public static Attr AttackAttr(this Tier tier) => Attr.Might;

        public static Skill AttackSkill(this Tier tier) => tier == Tier.Rabble ? Skill.None : Skill.Blades;
    }
}
