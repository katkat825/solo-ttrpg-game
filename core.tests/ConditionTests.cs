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

        // ---- the pipeline underneath, seen through an Actor (F3) ----

        [Fact]
        public void AConditionAndAnItem_ComposeOnTheSameAttribute_WhicheverArrivesFirst()
        {
            var ring = ModifierSource.Gear("cursed_ring");

            var first = Fixtures.Hero();
            first.ApplyCondition(Condition.Winded);
            first.AddModifier(new TraitModifier(ring, Attr.Might, -1));

            var second = Fixtures.Hero();
            second.AddModifier(new TraitModifier(ring, Attr.Might, -1));
            second.ApplyCondition(Condition.Winded);

            Assert.Equal(Die.D4, first.Attribute(Attr.Might));
            Assert.Equal(first.Attribute(Attr.Might), second.Attribute(Attr.Might));
            Assert.Equal(0, first.Saturation(Attr.Might));
        }

        [Fact]
        public void TakingTheRingOff_LeavesTheConditionWhereItWas()
        {
            var ring = ModifierSource.Gear("cursed_ring");
            var hero = Fixtures.Hero();

            hero.ApplyCondition(Condition.Winded);
            hero.AddModifier(new TraitModifier(ring, Attr.Might, -1));
            hero.RemoveModifiers(ring);

            Assert.Equal(Die.D6, hero.Attribute(Attr.Might));
            Assert.Contains(Condition.Winded, hero.Conditions);
        }

        [Fact]
        public void ClearingTheCondition_LeavesTheRingWhereItWas()
        {
            var ring = ModifierSource.Gear("cursed_ring");
            var hero = Fixtures.Hero();

            hero.ApplyCondition(Condition.Winded);
            hero.AddModifier(new TraitModifier(ring, Attr.Might, -1));
            hero.ClearCondition(Condition.Winded);

            Assert.Equal(Die.D6, hero.Attribute(Attr.Might));
            Assert.Single(hero.Modifiers);
        }

        // the saturation rule, CORE_RULES.md section 9
        [Fact]
        public void AnItemPastTheFloor_IsRefused_AndSaysHowFar()
        {
            var hero = Fixtures.Hero();
            hero.AddModifier(new TraitModifier(ModifierSource.Gear("cursed_ring"), Attr.Might, -4));

            Assert.Equal(Die.D4, hero.Attribute(Attr.Might));
            Assert.Equal(-2, hero.Saturation(Attr.Might));
        }

        [Fact]
        public void AnItemAlone_CannotDropYou_HoweverFarPastTheFloorItAsks()
        {
            var hero = Fixtures.Hero();
            hero.AddModifier(new TraitModifier(ModifierSource.Gear("cursed_ring"), Attr.Might, -9));

            Assert.False(hero.IsOverwhelmed);
            Assert.False(hero.IsDown);
        }

        [Fact]
        public void AConditionOnADieWithNothingLeftToGive_DropsYou()
        {
            var hero = Fixtures.Hero();
            hero.AddModifier(new TraitModifier(ModifierSource.Gear("cursed_ring"), Attr.Might, -4));

            Assert.False(hero.IsDown);

            hero.ApplyCondition(Condition.Winded);

            Assert.True(hero.IsOverwhelmed);
            Assert.True(hero.IsDown);
            Assert.True(hero.Vigor > 0);
        }

        [Fact]
        public void TakingTheRingOff_BringsYouBack()
        {
            var ring = ModifierSource.Gear("cursed_ring");
            var hero = Fixtures.Hero();

            hero.AddModifier(new TraitModifier(ring, Attr.Might, -4));
            hero.ApplyCondition(Condition.Winded);
            hero.RemoveModifiers(ring);

            Assert.False(hero.IsDown);
        }

        // a foe with no Grace statblock takes Reeling and is simply unaffected
        // the alternative drops every mook that catches a Condition it has no die for
        [Fact]
        public void AConditionOnAnAttributeYouHaveNoDieFor_DoesNothing()
        {
            var rival = Fixtures.Rival();
            Assert.Equal(Die.None, rival.Attribute(Attr.Grace));

            rival.ApplyCondition(Condition.Reeling);

            Assert.Equal(Die.None, rival.Attribute(Attr.Grace));
            Assert.Equal(0, rival.Saturation(Attr.Grace));
            Assert.False(rival.IsOverwhelmed);
        }

        // ---- vigor has one answer (F3) ----

        [Fact]
        public void Vigor_ClampsAtZero_RatherThanRunningNegative()
        {
            var hero = Fixtures.Hero();

            hero.Damage(hero.MaxVigor + 50);

            Assert.Equal(0, hero.Vigor);
            Assert.True(hero.IsDown);
        }

        // ---- the point of the whole mechanic: the NEXT pool is smaller (COMBAT_LOOP.md C1) ----

        // "getting hurt shrinks your dice and you SEE it" is only true if the die that reaches the
        // table is the shrunken one. This is that sentence as a test: the pool is built from the
        // pipeline's current die, not from the base, so a Winded hero throws a smaller solid
        [Fact]
        public void AWindedHero_ThrowsASmallerMightDie()
        {
            var hero = Fixtures.Hero();

            Assert.Equal(Die.D8, hero.BuildPool(Attr.Might, Skill.Blades).Dice[0].Die);

            hero.ApplyCondition(Condition.Winded);

            Assert.Equal(Die.D6, hero.BuildPool(Attr.Might, Skill.Blades).Dice[0].Die);
            Assert.Equal(Attr.Might.Key(), hero.BuildPool(Attr.Might, Skill.Blades).Dice[0].LabelKey);
        }

        // and the pool does not lose a die - the hero is worse off, not untrained. a condition
        // that dropped a die out of the pool would change the Impact rule underneath it
        [Fact]
        public void AConditionShrinksTheDie_AndNeverRemovesIt()
        {
            var hero = Fixtures.Hero();
            int before = hero.BuildPool(Attr.Might, Skill.Blades).Count;

            hero.ApplyCondition(Condition.Winded);

            Assert.Equal(before, hero.BuildPool(Attr.Might, Skill.Blades).Count);
        }

        // crossing both thresholds in one blow reports both, in the order they were crossed -
        // the view writes one mark per Condition and gets them from this list
        [Fact]
        public void OneBigHit_CanReportTwoConditionsAtOnce()
        {
            var hero = Fixtures.Hero();   // 20 vigor: Winded at 13, Reeling at 6

            var applied = hero.Damage(15);

            Assert.Equal(new[] { Condition.Winded, Condition.Reeling }, applied);
        }

    }
}
