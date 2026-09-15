using Core.Characters;
using Core.Combat;
using Core.Dice;

namespace Core.Tests
{
    // CORE_RULES.md section 11, built for W3. The two rests are deliberately different sizes:
    // a Breather is the one you take in a corridor, and a Camp is the one that costs a night.
    public class RestTests
    {
        static Actor Hero() =>
            new Actor("hero", 12, 9)
                .With(Attr.Might, Die.D8)
                .With(Attr.Grace, Die.D6)
                .With(Attr.Wits, Die.D6)
                .With(Attr.Heart, Die.D6);

        [Fact]
        public void A_camp_clears_strain_lifts_one_condition_and_resets_nerve()
        {
            Actor hero = Hero();

            hero.AddStrain(2);
            hero.ApplyCondition(Condition.Winded);
            hero.ApplyCondition(Condition.Reeling);
            hero.SpendNerve(3);

            Camped night = Rest.Camp(hero);

            Assert.Equal(2, night.StrainShed);
            Assert.Equal(Condition.Winded, night.ConditionLifted);
            Assert.Equal(Nerve.StartOfDay, hero.Nerve);
            Assert.Equal(0, hero.Strain);
        }

        // a day's damage follows you into the next one, which is what makes a chapter a chapter
        [Fact]
        public void A_camp_lifts_exactly_one_condition_and_no_vigor()
        {
            Actor hero = Hero();

            hero.ApplyCondition(Condition.Winded);
            hero.ApplyCondition(Condition.Reeling);
            hero.Damage(5);

            int hurt = hero.Vigor;

            Rest.Camp(hero);

            Assert.Single(hero.Conditions);
            Assert.Equal(hurt, hero.Vigor);
        }

        [Fact]
        public void A_camp_can_be_told_which_condition_to_sleep_off()
        {
            Actor hero = Hero();

            hero.ApplyCondition(Condition.Winded);
            hero.ApplyCondition(Condition.Reeling);

            Camped night = Rest.Camp(hero, Condition.Reeling);

            Assert.Equal(Condition.Reeling, night.ConditionLifted);
            Assert.Equal(new[] { Condition.Winded }, hero.Conditions);
        }

        [Fact]
        public void A_camp_with_nothing_to_shake_off_is_a_good_night_and_not_a_failure()
        {
            Actor hero = Hero();

            Camped night = Rest.Camp(hero);

            Assert.Null(night.ConditionLifted);
            Assert.Equal(Nerve.StartOfDay, night.Nerve);
        }

        // asking for a Condition the hero does not have must not silently claim to have lifted one
        [Fact]
        public void A_camp_told_to_lift_a_condition_that_is_not_there_lifts_nothing()
        {
            Actor hero = Hero();

            hero.ApplyCondition(Condition.Winded);

            Camped night = Rest.Camp(hero, Condition.Reeling);

            Assert.Null(night.ConditionLifted);
            Assert.Equal(new[] { Condition.Winded }, hero.Conditions);
        }

        [Fact]
        public void A_camp_resets_nerve_rather_than_adding_to_it_so_a_full_hero_comes_down_to_three()
        {
            Actor hero = Hero();

            hero.NerveCap = Nerve.Cap;
            hero.RestoreNerve(Nerve.Cap);

            Rest.Camp(hero);

            Assert.Equal(Nerve.StartOfDay, hero.Nerve);
        }

        [Fact]
        public void A_breather_is_still_the_smaller_rest()
        {
            Actor hero = Hero();

            hero.AddStrain(2);
            hero.ApplyCondition(Condition.Winded);
            hero.SpendNerve(3);

            int shed = Rest.Breather(hero);

            Assert.Equal(2, shed);
            Assert.Single(hero.Conditions);
            Assert.Equal(Rest.NerveFromABreather, hero.Nerve);
        }

        [Fact]
        public void Resting_nobody_does_nothing_rather_than_throwing()
        {
            Assert.Equal(0, Rest.Breather(null));

            Camped night = Rest.Camp(null);

            Assert.Equal(0, night.StrainShed);
            Assert.Null(night.ConditionLifted);
        }
    }
}
