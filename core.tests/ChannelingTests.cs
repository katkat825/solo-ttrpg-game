using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    // magic is the same roll, and the cost is a die going down (CORE_RULES.md sections 10 and 11)
    //
    // There is deliberately very little here, and that is the finding rather than a gap: if
    // channelling needed a lot of tests it would be a second subsystem, which is exactly what
    // CORE_RULES.md section 10 exists to avoid. What is worth pinning is the cost - Strain stacks,
    // it comes off in one piece, and it leaves you unable to absorb a Shaken.
    public class ChannelingTests
    {
        static Actor Caster() => new BuiltInArchetypes().Create(EngineIds.Mage);

        // ---- the same roll ----

        [Fact]
        public void ThePoolIsHeartAndChannelingAndTheFocus()
        {
            Pool pool = Channeling.PoolFor(Caster());

            Assert.Equal(3, pool.Count);
            Assert.Equal(Attr.Heart.Key(), pool.Dice[0].LabelKey);
            Assert.Equal(Skill.Channeling.Key(), pool.Dice[1].LabelKey);
            Assert.Equal("gear.focus.name", pool.Dice[2].LabelKey);
        }

        // three dice means a die left over, which means the effect HAS a power. That is the whole
        // reason the roster grew a caster for C5
        [Fact]
        public void AndThereIsADieLeftOverToBeThePower()
        {
            PoolResult thrown = new StandardResolver(new ScriptedRng(8, 5, 4))
                .Resolve(Channeling.PoolFor(Caster()));

            Assert.Equal(13, thrown.Total);
            Assert.Equal(Die.D6, thrown.Impact);
            Assert.Single(thrown.Rolls.Where(r => !r.Counted));
        }

        // an untrained caster gets a smaller pool and no leftover - "you can succeed, you just
        // cannot succeed hard". Not a penalty, and not a refusal
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

        // ---- and the cost ----

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

            // d8 has two steps in it, so three of those five were refused
            Assert.Equal(-3, caster.Saturation(Attr.Heart));
        }

        // strain alone does not drop you. A cursed ring can pin a die at d4 all day and you keep
        // fighting; it is a CONDITION landing on a die with no room that finishes you
        [Fact]
        public void StrainAloneNeverDropsACaster()
        {
            Actor caster = Caster();

            for (int i = 0; i < 9; i++) Channeling.Strains(caster);

            Assert.False(caster.IsOverwhelmed);
            Assert.False(caster.IsDown);
        }

        // THE SENTENCE CORE_RULES.md SECTION 10 ENDS ON: casting makes you fragile to Shaken.
        // A rested caster shrugs one off; a strained one has nothing left to give
        [Fact]
        public void ButAStrainedHeartHasNothingLeftToAbsorbAShakenWith()
        {
            Actor rested = Caster();
            rested.ApplyCondition(Condition.Shaken);

            Assert.False(rested.IsDown);
            Assert.Equal(Die.D6, rested.Attribute(Attr.Heart));

            Actor spent = Caster();
            for (int i = 0; i < 2; i++) Channeling.Strains(spent);   // d8 -> d4

            spent.ApplyCondition(Condition.Shaken);

            Assert.True(spent.IsOverwhelmed);
            Assert.True(spent.IsDown);
        }

        // ---- getting it back ----

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

        // and takes off exactly what channelling put on. A Condition sitting on the same attribute
        // is somebody else's modifier and stays where it is - the whole point of provenance (F3)
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

        // ---- a Nerve buys nothing a caster already has ----

        // the Heart die is already in the pool, so the fourth-die spend is refused rather than
        // charged for nothing (CORE_RULES.md section 7, Nerve.CanAddHeart)
        [Fact]
        public void ANerveCannotBuyACasterAFourthDie()
        {
            Actor caster = Caster();

            Assert.False(Nerve.CanAddHeart(caster, Channeling.PoolFor(caster)));
        }
    }
}
