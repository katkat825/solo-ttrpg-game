using Core.Characters;

namespace Core.Combat
{
    // a Breather clears Strain and gives a Nerve, but not Vigor or Conditions - those take a Camp
    public static class Rest
    {
        public const int NerveFromABreather = 1;

        public static int Breather(Actor actor)
        {
            if (actor == null) return 0;

            int shed = actor.MendStrain();
            actor.GainNerve(NerveFromABreather);

            return shed;
        }

        // Camp, once per in-game night (CORE_RULES.md section 11): clear Strain, remove ONE
        // Condition, reset Nerve to 3. Not "all Conditions" and not full Vigor - a day's damage is
        // meant to follow you into the next one, which is what makes a long chapter a chapter.
        //
        // The companion scene camp also triggers is not here: the rules have no idea there is a
        // companion, and W3 hangs the conversation off the Camped this returns.
        public static Camped Camp(Actor actor, Condition? shed = null)
        {
            if (actor == null) return new Camped(0, null, 0);

            int strain = actor.MendStrain();

            Condition? lifted = shed ?? First(actor);

            if (lifted is { } condition && !actor.ClearCondition(condition)) lifted = null;

            actor.RestoreNerve(Nerve.StartOfDay);

            return new Camped(strain, lifted, actor.Nerve);
        }

        // the one taken first: the oldest complaint is the one a night's rest is most likely to settle
        static Condition? First(Actor actor) =>
            actor.Conditions.Count > 0 ? actor.Conditions[0] : (Condition?)null;
    }

    // what a night did, so the table can show it and the companion can talk about it
    public readonly struct Camped
    {
        public Camped(int strainShed, Condition? conditionLifted, int nerve)
        {
            StrainShed = strainShed;
            ConditionLifted = conditionLifted;
            Nerve = nerve;
        }

        public int StrainShed { get; }

        // null when there was nothing to shake off, which is a good night
        public Condition? ConditionLifted { get; }

        public int Nerve { get; }

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{StrainShed} strain shed, " +
            (ConditionLifted is { } c ? $"{c} slept off" : "nothing to sleep off") +
            $", nerve back to {Nerve}";
    }
}
