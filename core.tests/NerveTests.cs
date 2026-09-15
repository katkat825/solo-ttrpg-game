using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    public class NerveTests
    {
        static CombatEngine Engine(IRng rng = null, CombatOptions opts = null) =>
            new CombatEngine(new StandardResolver(rng ?? new SeededRng(1)), opts);

        static Encounter Fight(int rabble = 1) =>
            new Encounter(Engine(), Fixtures.Hero(), Fixtures.StandardEncounter(rabble));


        [Fact]
        public void AHeroStartsTheDayWithThree_AndCapsAtFive()
        {
            var hero = Fixtures.Hero();

            Assert.Equal(Nerve.StartOfDay, hero.Nerve);
            Assert.Equal(Nerve.Cap, hero.NerveCap);
        }

        [Fact]
        public void ARabbleHasNone_AndCannotBank()
        {
            var mook = Fixtures.Mook();

            Assert.Equal(0, mook.Nerve);
            Assert.False(mook.SpendNerve());
            Assert.Equal(0, mook.GainNerve());
        }

        [Fact]
        public void SpendingTakesOne_AndRefusesWhenThereIsNone()
        {
            var hero = Fixtures.Hero();

            for (int i = Nerve.StartOfDay; i > 0; i--) Assert.True(hero.SpendNerve());

            Assert.Equal(0, hero.Nerve);
            Assert.False(hero.SpendNerve());
        }

        [Fact]
        public void BankingStopsAtTheCap_AndSaysHowMuchLanded()
        {
            var hero = Fixtures.Hero();
            hero.RestoreNerve(Nerve.Cap);

            Assert.Equal(0, hero.GainNerve());
            Assert.Equal(Nerve.Cap, hero.Nerve);

            hero.SpendNerve();

            Assert.Equal(1, hero.GainNerve(3));
            Assert.Equal(Nerve.Cap, hero.Nerve);
        }


        [Fact]
        public void TheHeartDieIsAFourthDie_AndKeepsItsOwnName()
        {
            var hero = Fixtures.Hero();
            Pool swing = hero.BuildPool(Attr.Might, Skill.Blades);

            Pool bigger = Nerve.WithHeart(hero, swing);

            Assert.Equal(3, swing.Count);
            Assert.Equal(4, bigger.Count);
            Assert.Equal(Attr.Heart.Key(), bigger.Dice[3].LabelKey);
            Assert.Equal(hero.Attribute(Attr.Heart), bigger.Dice[3].Die);
        }

        // a fourth die makes the blow harder as well as more likely (bigger leftover)
        [Fact]
        public void AndTheLeftoverIsBigger()
        {
            var hero = Fixtures.Hero();

            PoolResult without = new StandardResolver(new ScriptedRng(6, 6, 1))
                .Resolve(hero.BuildPool(Attr.Might, Skill.Blades));

            PoolResult with = new StandardResolver(new ScriptedRng(6, 6, 1, 1))
                .Resolve(Nerve.WithHeart(hero, hero.BuildPool(Attr.Might, Skill.Blades)));

            Assert.Equal(without.Total, with.Total);
            Assert.Equal(4, with.Rolls.Count);
            Assert.Equal(2, with.Rolls.Count(r => !r.Counted));
        }

        [Fact]
        public void ItIsRefusedWhenThePoolAlreadyUsesHeart()
        {
            var hero = Fixtures.Hero();
            Pool channel = hero.BuildPool(Attr.Heart, Skill.Channeling);

            Assert.False(Nerve.CanAddHeart(hero, channel));
            Assert.Same(channel, Nerve.WithHeart(hero, channel));
        }

        [Fact]
        public void AndWhenThereIsNoHeartDieToAdd()
        {
            var mook = Fixtures.Mook();

            Assert.False(Nerve.CanAddHeart(mook, mook.BuildPool(Attr.Might)));
        }


        [Fact]
        public void PushingBuysAThirdAction_ForANerve()
        {
            Encounter fight = Fight();
            fight.Begin();

            int nerve = fight.Hero.Nerve;

            fight.Spend();
            fight.Spend();

            Assert.Equal(0, fight.ActionsLeft);
            Assert.True(fight.Push());
            Assert.Equal(1, fight.ActionsLeft);
            Assert.Equal(nerve - 1, fight.Hero.Nerve);
            Assert.True(fight.AwaitingHero);
        }

        [Fact]
        public void AndNotWithoutOne()
        {
            Encounter fight = Fight();
            fight.Begin();

            while (fight.Hero.SpendNerve()) { }

            Assert.False(fight.Push());
        }

        [Fact]
        public void NorOnSomebodyElsesTurn()
        {
            Encounter fight = Fight();
            fight.Begin();
            fight.EndTurn();

            int nerve = fight.Hero.Nerve;

            Assert.False(fight.Push());
            Assert.Equal(nerve, fight.Hero.Nerve);
        }


        [Fact]
        public void ATroubleNotchesTheGear()
        {
            var hero = Fixtures.Hero();

            Assert.Equal(Trouble.Cost.Notched, Trouble.Lands(hero, Attr.Might));
            Assert.Equal(Die.D4, hero.Weapon);
            Assert.Equal(1, hero.Notches);
            Assert.Empty(hero.Conditions);
        }

        [Fact]
        public void AndTheNextPoolIsBuiltFromTheSmallerGearDie()
        {
            var hero = Fixtures.Hero();

            Trouble.Lands(hero, Attr.Might);

            Assert.Equal(Die.D4, hero.BuildPool(Attr.Might, Skill.Blades).Dice[2].Die);
        }

        [Fact]
        public void WithTheGearAtTheFloor_ItLandsOnTheHeroInstead()
        {
            var hero = Fixtures.Hero();

            Trouble.Lands(hero, Attr.Might);

            Assert.Equal(Trouble.Cost.Condition, Trouble.Lands(hero, Attr.Might));
            Assert.Contains(Condition.Winded, hero.Conditions);
            Assert.Equal(Die.D4, hero.Weapon);
        }

        [Fact]
        public void AndTheConditionIsTheOnePressingOnWhatWasUsed()
        {
            var hero = Fixtures.Hero();
            hero.WithWeapon("bare", Die.None);

            Assert.Equal(Trouble.Cost.Condition, Trouble.Lands(hero, Attr.Grace));
            Assert.Contains(Condition.Reeling, hero.Conditions);
        }

        [Fact]
        public void WithNowhereLeftToPutIt_NothingHappens_AndItSaysSo()
        {
            var hero = Fixtures.Hero();
            hero.WithWeapon("bare", Die.None);
            hero.ApplyCondition(Condition.Winded);

            Assert.Equal(Trouble.Cost.Nothing, Trouble.Lands(hero, Attr.Might));
        }

        [Fact]
        public void EveryConditionIsThePressingOneForItsAttribute()
        {
            foreach (Condition c in System.Enum.GetValues<Condition>())
                Assert.Equal(c, c.Affects().Pressing());
        }


        [Fact]
        public void ShruggingCostsANerve_AndNothingElseHappens()
        {
            Encounter fight = Fight();
            fight.Begin();

            int nerve = fight.Hero.Nerve;
            Die axe = fight.Hero.Weapon;

            Assert.True(fight.SpendNerve(fight.Hero));
            Assert.Equal(nerve - 1, fight.Hero.Nerve);
            Assert.Equal(axe, fight.Hero.Weapon);
            Assert.Empty(fight.Hero.Conditions);
        }

        [Fact]
        public void AndCannotBeAffordedWithNoNerve()
        {
            Encounter fight = Fight();
            fight.Begin();

            while (fight.Hero.SpendNerve()) { }

            Assert.False(fight.SpendNerve(fight.Hero));
        }

        [Fact]
        public void AcceptingBanksANerve_AndTheConsequenceLands()
        {
            Encounter fight = Fight();
            fight.Begin();

            fight.Hero.SpendNerve();
            int nerve = fight.Hero.Nerve;

            Assert.Equal(Trouble.Cost.Notched, fight.Accept(fight.Hero, Attr.Might));
            Assert.Equal(nerve + 1, fight.Hero.Nerve);
            Assert.Equal(Die.D4, fight.Hero.Weapon);
        }

        // at the cap the consequence still lands and nothing banks, which is why gainnerve reports what landed
        [Fact]
        public void AtTheCap_TheConsequenceStillLands_AndNothingIsBanked()
        {
            Encounter fight = Fight();
            fight.Begin();
            fight.Hero.RestoreNerve(Nerve.Cap);

            Assert.Equal(Trouble.Cost.Notched, fight.Accept(fight.Hero, Attr.Might));
            Assert.Equal(Nerve.Cap, fight.Hero.Nerve);
        }
    }
}
