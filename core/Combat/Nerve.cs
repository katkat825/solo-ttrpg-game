using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Core.Combat
{
    public static class Nerve
    {
        public const int StartOfDay = 3;

        public const int Cap = 5;


        // a fourth die, not a bonus - a better best-two and a bigger leftover, so harder as well as likelier
        public static bool CanAddHeart(Actor actor, Pool pool) =>
            actor != null
            && pool != null
            && actor.Attribute(Attr.Heart).IsReal()
            && !UsesHeart(pool);

        // returns a new pool, doesn't grow the given one - something may still hold the one without Heart
        public static Pool WithHeart(Actor actor, Pool pool)
        {
            if (!CanAddHeart(actor, pool)) return pool;

            var bigger = new Pool();

            foreach (PoolDie die in pool.Dice) bigger.Add(die.LabelKey, die.Die);

            bigger.Add(Attr.Heart.Key(), actor.Attribute(Attr.Heart));

            return bigger;
        }

        // matched by the trait's key, never die size - two d6s are two different traits
        public static bool UsesHeart(Pool pool) =>
            pool != null && pool.Dice.Any(d => d.LabelKey == Attr.Heart.Key());
    }
}
