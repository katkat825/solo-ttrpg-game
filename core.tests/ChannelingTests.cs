using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    public class ChannelingTests
    {
        static Actor Caster() => new BuiltInArchetypes().Create(EngineIds.Mage);


        [Fact]
        public void ThePoolIsHeartAndChannelingAndTheFocus()
        {
            Pool pool = Channeling.PoolFor(Caster());

            Assert.Equal(3, pool.Count);
            Assert.Equal(Attr.Heart.Key(), pool.Dice[0].LabelKey);
            Assert.Equal(Skill.Channeling.Key(), pool.Dice[1].LabelKey);
            Assert.Equal("gear.focus.name", pool.Dice[2].LabelKey);
        }

        [Fact]
        public void AndThereIsADieLeftOverToBeThePower()
        {
            PoolResult thrown = new StandardResolver(new ScriptedRng(8, 5, 4))
                .Resolve(Channeling.PoolFor(Caster()));

            Assert.Equal(13, thrown.Total);
            Assert.Equal(Die.D6, thrown.Impact);
            Assert.Single(thrown.Rolls, r => !r.Counted);
        }

        [Fact]
        public void AnUntrainedCasterThrowsTwoDice_AndHasNoPower()
        {
            var barbarian = Fixtures.Hero();

            Assert.False(Channeling.IsTrained(barbarian));
            Assert.Equal(2, Channeling.PoolFor(barbarian).Count);

            PoolResult thrown = new StandardResolver(new ScriptedRng(6, 6))
                .Resolve(Channeling.PoolFor(barbarian));

            Assert.All(thrown.Rolls, r => Assert.True(r.Counted));
        }

        [Fact]
        public void TrainedMeansAHeartDieAndTheSkill()
        {
            Assert.True(Channeling.IsTrained(Caster()));
            Assert.False(Channeling.IsTrained(Fixtures.Mook()));
        }


        [Fact]
        public void ChannellingStepsTheHeartDieDown()
        {
            Actor caster = Caster();

            Assert.Equal(Die.D8, caster.Attribute(Attr.Heart));

            Channeling.Strains(caster);

            Assert.Equal(Die.D6, caster.Attribute(Attr.Heart));
            Assert.Equal(1, caster.Strain);
            Assert.Equal(Die.D8, caster.BaseAttribute(Attr.Heart));
        }

        [Fact]
        public void AndTheNextPoolIsBuiltFromTheSmallerDie()
        {
            Actor caster = Caster();

            Channeling.Strains(caster);

            Assert.Equal(Die.D6, Channeling.PoolFor(caster).Dice[0].Die);
        }

        [Fact]
        public void ItStacks_AndClampsAtTheFloor_AndRemembersTheOverflow()
        {
            Actor caster = Caster();

            for (int i = 0; i < 5; i++) Channeling.Strains(caster);

            Assert.Equal(Die.D4, caster.Attribute(Attr.Heart));
            Assert.Equal(5, caster.Strain);

            // d8 has two steps to d4, so three of the five were refused
            Assert.Equal(-3, caster.Saturation(Attr.Heart));
        }

        // strain alone never drops you; a condition landing on a die with no room does
        [Fact]
        public void StrainAloneNeverDropsACaster()
        {
            Actor caster = Caster();

            for (int i = 0; i < 9; i++) Channeling.Strains(caster);

            Assert.False(caster.IsOverwhelmed);
            Assert.False(caster.IsDown);
        }

        [Fact]
        public void ButAStrainedHeartHasNothingLeftToAbsorbAShakenWith()
        {
            Actor rested = Caster();
            rested.ApplyCondition(Condition.Shaken);

            Assert.False(rested.IsDown);
            Assert.Equal(Die.D6, rested.Attribute(Attr.Heart));

            Actor spent = Caster();
            for (int i = 0; i < 2; i++) Channeling.Strains(spent);

            spent.ApplyCondition(Condition.Shaken);

            Assert.True(spent.IsOverwhelmed);
            Assert.True(spent.IsDown);
        }


        [Fact]
        public void ABreatherClearsStrain_AndGivesBackOneNerve()
        {
            Actor caster = Caster();

            for (int i = 0; i < 3; i++) Channeling.Strains(caster);
            caster.SpendNerve();
            caster.SpendNerve();

            int nerve = caster.Nerve;
            int shed = Rest.Breather(caster);

            Assert.Equal(3, shed);
            Assert.Equal(0, caster.Strain);
            Assert.Equal(Die.D8, caster.Attribute(Attr.Heart));
            Assert.Equal(nerve + Rest.NerveFromABreather, caster.Nerve);
        }

        [Fact]
        public void AndLeavesAConditionOnTheSameDieWhereItWas()
        {
            Actor caster = Caster();

            caster.ApplyCondition(Condition.Shaken);
            Channeling.Strains(caster);

            Assert.Equal(Die.D4, caster.Attribute(Attr.Heart));

            Rest.Breather(caster);

            Assert.Equal(Die.D6, caster.Attribute(Attr.Heart));
            Assert.Contains(Condition.Shaken, caster.Conditions);
        }


        [Fact]
        public void ANerveCannotBuyACasterAFourthDie()
        {
            Actor caster = Caster();

            Assert.False(Nerve.CanAddHeart(caster, Channeling.PoolFor(caster)));
        }
    }
}
