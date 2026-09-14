using Core.Characters;

namespace Core.Combat
{
    // WHAT TWO OR MORE 1s ACTUALLY COSTS (CORE_RULES.md section 6, COMBAT_LOOP.md C4).
    //
    // M9 built the cheap half: exactly one 1 is a Snag, purely cosmetic, the companion's cue to
    // speak, about a third of throws. Two or more is the other tier - "real consequence: gear takes
    // a Condition, you're spotted, the spell backfires" - at about 6%, which is rare enough to
    // sting and be memorable. `PoolResult.Trouble` has reported it since the resolver was written
    // and nothing has ever acted on it. This is the acting on it.
    //
    // ONE ENGINE-OWNED CONSEQUENCE, AND IT IS A DIE GOING DOWN. Being spotted and a spell
    // backfiring are campaign content and arrive with the content pipeline; what the engine can
    // own is the one CORE_RULES.md lists first and section 9 already has a rule for: gear takes
    // Conditions too, and a Notched blade drops its gear die. That is the same mechanic as every
    // other consequence in this game - the dice ARE the stats, so getting hurt is a smaller die -
    // and the player watches it happen on the next throw.
    //
    // AND A FALLBACK, BECAUSE SOMETHING HAS TO HAPPEN. A hero fighting bare-handed, or one whose
    // axe is already notched down to a d4, has no gear die left to lose - and a Trouble that
    // quietly did nothing would be worse than no Trouble at all, because the player would learn
    // that the tier is decorative. So it lands on the hero instead, as the Condition that presses
    // on the attribute he was using: over-committing with Might leaves you Winded.
    //
    // TWO CLAUSES AND NO MORE. A third tier would be a rule nobody could predict from the table.
    public static class Trouble
    {
        // what happened, so a caller can narrate it without asking again
        public enum Cost
        {
            // there was nothing to take it - a Trouble on an actor with no gear and every relevant
            // Condition already carried. Possible, rare, and honest to report as such
            Nothing,

            // the gear die stepped down
            Notched,

            // the actor took the Condition pressing on the attribute they were using
            Condition,
        }

        // <paramref name="using"/> is the attribute the throw was made with, which is what decides
        // the fallback Condition - and is derived from ConditionRules rather than stated again
        public static Cost Lands(Actor actor, Attr @using)
        {
            if (actor == null) return Cost.Nothing;

            if (actor.NotchGear()) return Cost.Notched;

            return actor.ApplyCondition(@using.Pressing()) ? Cost.Condition : Cost.Nothing;
        }
    }
}
