using System.Linq;
using Rules.Characters;
using Rules.Dice;
using Rules.Localization;
using Rules.Resolution;
using Xunit;

namespace Rules.Tests
{
    // covers the core roll - best two summed, the leftover die becoming Impact
    // ties broken in the player's favour, so the biggest die is the one left over
    // Snag and Trouble counting
    // and the ceiling a starting pool cannot roll past
    public class ResolverTests
    {
        // pool dice carry localization keys, never display text
        static Pool ThreeDie() => Pool.Of(
            (Attr.Might.Key(), Die.D8),
            (Skill.Blades.Key(), Die.D6),
            (KeyConventions.GearName("axe"), Die.D6));

        static IResolver With(params int[] script) =>
            new StandardResolver(new ScriptedRng(script));

        [Fact]
        public void SumsBestTwo_AndLeftoverBecomesImpact()
        {
            // Might=7, Blades=5, Axe=2  ->  best two 7+5=12, leftover is the d6
            var r = With(7, 5, 2).Resolve(ThreeDie());

            Assert.Equal(12, r.Total);
            Assert.Equal(Die.D6, r.Impact);
        }

        [Fact]
        public void TwoDiePool_GetsImpactD4()
        {
            // untrained and ungeared - you can succeed, but you cannot hit hard
            var r = With(4, 3).Resolve(Pool.Of(
                (Attr.Might.Key(), Die.D8),
                (KeyConventions.GearName("axe"), Die.D6)));

            Assert.Equal(7, r.Total);
            Assert.Equal(Die.D4, r.Impact);
        }

        [Fact]
        public void TiesBreakInPlayersFavour_LargerDieLeftForImpact()
        {
            // all three roll 5 - count the two SMALL dice so the d8 is left for Impact
            var r = With(5, 5, 5).Resolve(ThreeDie());

            Assert.Equal(10, r.Total);
            Assert.Equal(Die.D8, r.Impact);
        }

        [Fact]
        public void BigDieRollingBadly_IsStillWorthBringing()
        {
            // Might d8 rolls 1 - useless for the total, but it is the Impact die
            var r = With(1, 6, 6).Resolve(ThreeDie());

            Assert.Equal(12, r.Total);
            Assert.Equal(Die.D8, r.Impact);
        }

        [Fact]
        public void OneOne_IsASnag_NotTrouble()
        {
            var r = With(1, 5, 5).Resolve(ThreeDie());

            Assert.Equal(1, r.Ones);
            Assert.True(r.Snag);
            Assert.False(r.Trouble);
        }

        [Fact]
        public void TwoOnes_IsTrouble()
        {
            var r = With(1, 1, 5).Resolve(ThreeDie());

            Assert.Equal(2, r.Ones);
            Assert.True(r.Trouble);
        }

        [Fact]
        public void StartingHero_CannotBeatFormidable_EvenOnMaximumRolls()
        {
            // d8+d6+d6 caps at 8+6=14
            // difficulty 15 is a structural gate, not bad luck
            var r = With(8, 6, 6).Resolve(ThreeDie());

            Assert.Equal(14, r.Total);
            Assert.True(r.Beats(Difficulty.Hard));
            Assert.False(r.Beats(Difficulty.Formidable));
        }

        [Fact]
        public void CountedFlags_MarkExactlyTwoDice()
        {
            var r = With(7, 5, 2).Resolve(ThreeDie());

            Assert.Equal(2, r.Rolls.Count(x => x.Counted));
        }

        [Fact]
        public void EmptyPool_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => With(1).Resolve(new Pool()));
        }
    }
}
