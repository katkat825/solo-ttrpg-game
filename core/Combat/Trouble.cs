using Core.Characters;

namespace Core.Combat
{
    // falls back to a Condition when no gear die is left to notch, so a Trouble never does nothing
    public static class Trouble
    {
        public enum Cost
        {
            Nothing,

            Notched,

            Condition,
        }

        public static Cost Lands(Actor actor, Attr @using)
        {
            if (actor == null) return Cost.Nothing;

            if (actor.NotchGear()) return Cost.Notched;

            return actor.ApplyCondition(@using.Pressing()) ? Cost.Condition : Cost.Nothing;
        }
    }
}
