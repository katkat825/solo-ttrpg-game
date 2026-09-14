using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Resolution;

namespace Core.Combat
{
    // THE HEROIC-EFFORT RESOURCE, AND THE LOOP WORTH PROTECTING (CORE_RULES.md section 7).
    //
    // Three at the start of a day, five at most, and four things to spend one on. The four are not
    // a list of powers - they are four different ways of saying "this one matters", and between
    // them they cover every stage of a throw: before it (a fourth die), during it (a re-throw),
    // after it (shrug the complication), and instead of it (one more action).
    //
    // THE LOOP IS THE REGAIN, NOT THE SPEND. "Regain Nerve by accepting Trouble" is the sentence
    // the whole resource exists for: the game proposes something going wrong, and going wrong is
    // how you afford going right later. It gives a player a reason to WANT the bad thing
    // occasionally, which is very hard to get any other way - and it gives the companion an
    // opinion to have about the choice (Phase W).
    //
    // WHAT LIVES HERE AND WHAT LIVES ELSEWHERE. This file is the resource and the two rules that
    // are only about the resource: how much of it there is, and what adding the Heart die means.
    // Spending it on an action is `Encounter.Push`, because that is the action economy; spending
    // it on a re-throw is the tray, because that is a die on a table; spending it on a Trouble is
    // `Trouble`, because that is what a Trouble costs.
    public static class Nerve
    {
        // CORE_RULES.md section 7. A day, not a fight - a Breather gives one back and a Camp
        // resets to this (section 11), neither of which exists yet
        public const int StartOfDay = 3;

        public const int Cap = 5;

        // ---- the fourth die ----

        // A NERVE ADDS YOUR HEART DIE TO A POOL THAT DOES NOT ALREADY USE IT. Not a bonus: a
        // fourth die, which means a better chance at the best two AND a bigger die left over, so
        // it makes the blow harder as well as more likely. That second half is the interesting one
        // and it falls out of the Impact rule for free.
        //
        // REFUSED WHEN THE POOL ALREADY USES HEART, because then it buys nothing and a Nerve spent
        // on nothing is a Nerve the player will never spend again. A caster channelling with
        // Heart + Channeling + focus is exactly that case (CORE_RULES.md section 10).
        //
        // Also refused when the actor has no Heart die at all - there is nothing to add.
        public static bool CanAddHeart(Actor actor, Pool pool) =>
            actor != null
            && pool != null
            && actor.Attribute(Attr.Heart).IsReal()
            && !UsesHeart(pool);

        // hands back a NEW pool rather than growing the one it was given: a pool is what is about
        // to be thrown, and something else may still be holding the one without the Heart in it
        public static Pool WithHeart(Actor actor, Pool pool)
        {
            if (!CanAddHeart(actor, pool)) return pool;

            var bigger = new Pool();

            foreach (PoolDie die in pool.Dice) bigger.Add(die.LabelKey, die.Die);

            bigger.Add(Attr.Heart.Key(), actor.Attribute(Attr.Heart));

            return bigger;
        }

        // by the trait's own key, which is what a pool carries - never by die size, because two
        // d6s in a pool are two different traits
        public static bool UsesHeart(Pool pool) =>
            pool != null && pool.Dice.Any(d => d.LabelKey == Attr.Heart.Key());
    }
}
