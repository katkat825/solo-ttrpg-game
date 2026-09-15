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
    }
}
