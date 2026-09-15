using System.Linq;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    public class ResolverTests
    {
        static Pool ThreeDie() => Pool.Of(
            (Attr.Might.Key(), Die.D8),
            (Skill.Blades.Key(), Die.D6),
            (KeyConventions.GearName("axe"), Die.D6));

        static IResolver With(params int[] script) =>
            new StandardResolver(new ScriptedRng(script));

        [Fact]
        public void SumsBestTwo_AndLeftoverBecomesImpact()
        {
            var r = With(7, 5, 2).Resolve(ThreeDie());

            Assert.Equal(12, r.Total);
            Assert.Equal(Die.D6, r.Impact);
        }

        [Fact]
        public void TwoDiePool_GetsImpactD4()
        {
            var r = With(4, 3).Resolve(Pool.Of(
                (Attr.Might.Key(), Die.D8),
                (KeyConventions.GearName("axe"), Die.D6)));

            Assert.Equal(7, r.Total);
            Assert.Equal(Die.D4, r.Impact);
        }

        [Fact]
        public void TiesBreakInPlayersFavour_LargerDieLeftForImpact()
        {
            // on a tie count the two small dice, leaving the d8 for impact
            var r = With(5, 5, 5).Resolve(ThreeDie());

            Assert.Equal(10, r.Total);
            Assert.Equal(Die.D8, r.Impact);
        }

        [Fact]
        public void BigDieRollingBadly_IsStillWorthBringing()
        {
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
            // d8+d6+d6 caps at 8+6=14, so difficulty 15 is a structural gate
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
