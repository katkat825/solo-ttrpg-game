using Core.Characters;

namespace Core.Combat
{
    // GETTING IT BACK (CORE_RULES.md section 11).
    //
    // Two rests, and only the first of them is mechanical enough to build yet. A **Breather** is a
    // few minutes anywhere safe: it clears Strain and gives back one Nerve, and it is what makes
    // channelling a pacing mechanic rather than a one-way trip - cast freely, and then stop and
    // breathe before the next room.
    //
    // A **Camp** is the other one, and most of what it is is not mechanics: "camp is the delivery
    // vehicle for your entire companion relationship", one conversation per night, reactive to
    // what happened that day. That is a first-class scene in Phase W and building the numbers
    // without the scene would be building the smaller half. What is here is the half C5 needs.
    //
    // WHAT A BREATHER DOES NOT DO: heal, or clear a Condition. Vigor comes back some other way
    // (CORE_RULES.md section 11 leaves it open) and a Condition takes a Camp. A rest that put
    // everything back would make the day one long fight with pauses in it.
    public static class Rest
    {
        public const int NerveFromABreather = 1;

        // returns how much Strain was shed, so a caller can say whether it was worth stopping
        public static int Breather(Actor actor)
        {
            if (actor == null) return 0;

            int shed = actor.MendStrain();
            actor.GainNerve(NerveFromABreather);

            return shed;
        }
    }
}
