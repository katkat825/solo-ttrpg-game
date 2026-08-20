using System.Linq;
using Core.Characters;
using Core.Dice;
using Xunit;

namespace Core.Tests
{
    // covers conditions stepping their attribute die down one size
    // clearing restores it, the same condition twice does not stack
    // vigor thresholds apply them and say which ones landed
    // Rabble have no vigor track and drop to any hit
    public class ConditionTests
    {
        [Fact]
        public void Condition_StepsItsAttributeDown()
        {
            var hero = Fixtures.Hero();
            Assert.Equal(Die.D8, hero.Attribute(Attr.Might));

            hero.ApplyCondition(Condition.Winded);

            Assert.Equal(Die.D6, hero.Attribute(Attr.Might));
            Assert.Equal(Die.D8, hero.BaseAttribute(Attr.Might));
        }

        [Fact]
        public void ClearingCondition_RestoresTheDie()
        {
            var hero = Fixtures.Hero();
            hero.ApplyCondition(Condition.Winded);
            hero.ClearCondition(Condition.Winded);

            Assert.Equal(Die.D8, hero.Attribute(Attr.Might));
        }

        [Fact]
        public void SameCondition_DoesNotStack()
        {
            var hero = Fixtures.Hero();
            hero.ApplyCondition(Condition.Winded);
            hero.ApplyCondition(Condition.Winded);

            Assert.Equal(Die.D6, hero.Attribute(Attr.Might));
            Assert.Single(hero.Conditions);
        }

        [Fact]
        public void DifferentConditions_AffectDifferentAttributes()
        {
            var hero = Fixtures.Hero();
            hero.ApplyCondition(Condition.Winded);   // Might
            hero.ApplyCondition(Condition.Reeling);  // Grace

            Assert.Equal(Die.D6, hero.Attribute(Attr.Might));
            Assert.Equal(Die.D4, hero.Attribute(Attr.Grace));
        }

        [Fact]
        public void DamageAcrossThreshold_AppliesWinded_AndReportsIt()
        {
            var hero = Fixtures.Hero(); // 20 vigor, Winded at <= 13.33

            var applied = hero.Damage(7);

            Assert.Equal(13, hero.Vigor);
            Assert.Contains(Condition.Winded, applied);
            Assert.Equal(Die.D6, hero.Attribute(Attr.Might));
        }

        [Fact]
        public void Damage_ReportsNothing_WhenNoThresholdCrossed()
        {
            var hero = Fixtures.Hero();

            Assert.Empty(hero.Damage(1));
        }

        [Fact]
        public void Rabble_IgnoreTheVigorTrack()
        {
            var mook = Fixtures.Mook();

            mook.Damage(1);

            Assert.True(mook.IsDown);
        }
    }
}
