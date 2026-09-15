using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Resolution;
using Game.Tray;
using Xunit;

namespace Game.Tests
{
    public class TrayResolutionTests
    {
        static Pool Pool(Die a, Die b, Die c) => Core.Resolution.Pool.Of(
            ("attr.might.name", a), ("skill.blades.name", b), ("gear.axe.name", c));

        static TrayThrow Throw(Die a, Die b, Die c, params int[] values) =>
            new TrayResolution(Pool(a, b, c)).Resolve(values);


        [Fact]
        public void APoolWithNoDiceInIt_IsRefused() =>
            Assert.Throws<ArgumentException>(() => new TrayResolution(new Pool()));

        [Fact]
        public void NoPoolAtAll_IsRefused() =>
            Assert.Throws<ArgumentNullException>(() => new TrayResolution(null));

        // pool.add drops an absent die, so an untrained attempt arrives as two dice, not a pool with a hole
        [Fact]
        public void ADieThatIsNotThere_ShrinksThePool()
        {
            var pool = Core.Resolution.Pool.Of(
                ("attr.might.name", Die.D6), ("skill.none.name", Die.None), ("gear.axe.name", Die.D6));

            Assert.Equal(2, new TrayResolution(pool).Size);
        }

        [Fact]
        public void TheWrongNumberOfFaces_IsRefused() =>
            Assert.Throws<ArgumentException>(() => Throw(Die.D6, Die.D6, Die.D6, 1, 2));

        [Fact]
        public void AFaceThatIsNotOnTheDie_IsRefused() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Throw(Die.D6, Die.D8, Die.D12, 7, 3, 3));


        [Fact]
        public void ImpactValue_IsTheFaceOnTheDieTheRulesLeftOver()
        {
            TrayThrow thrown = Throw(Die.D8, Die.D6, Die.D6, 4, 4, 3);

            Assert.False(thrown.ImpactValue == 0);
            Assert.Equal(3, thrown.ImpactValue);

            int impact = -1;

            for (int i = 0; i < thrown.Roles.Count; i++)
                if (thrown.Roles[i] == DieRole.Impact) impact = thrown.Slots[i].Value;

            Assert.Equal(impact, thrown.ImpactValue);
        }

        [Fact]
        public void ImpactValue_IsNothingWhenNothingWasLeftOver()
        {
            var two = Core.Resolution.Pool.Of(("attr.might.name", Die.D6), ("gear.axe.name", Die.D6));

            TrayThrow thrown = new TrayResolution(two).Resolve(new[] { 3, 4 });

            Assert.True(thrown.ImpactIsFallback);
            Assert.Equal(0, thrown.ImpactValue);
        }


        [Fact]
        public void SlotsComeBackInThrowOrder_NotInValueOrder()
        {
            TrayThrow thrown = Throw(Die.D6, Die.D8, Die.D12, 2, 7, 5);

            Assert.Equal(new[] { 2, 7, 5 }, thrown.Slots.Select(s => s.Value));
            Assert.Equal(new[] { Die.D6, Die.D8, Die.D12 }, thrown.Slots.Select(s => s.Die));
        }

        // poolresult.rolls is sorted by value, so indexing dice by it rings the right count of wrong dice
        [Fact]
        public void RolesAreInThrowOrder_EvenWhenTheResultOrderDiffers()
        {
            TrayThrow thrown = Throw(Die.D6, Die.D8, Die.D12, 2, 7, 5);

            Assert.Equal(new[] { DieRole.Impact, DieRole.Counted, DieRole.Counted }, thrown.Roles);

            Assert.Equal(new[] { 7, 5, 2 }, thrown.Result.Rolls.Select(r => r.Value));
        }

        [Fact]
        public void ExactlyTwoDiceCount_AndTheLeftoverIsImpact()
        {
            TrayThrow thrown = Throw(Die.D8, Die.D6, Die.D6, 5, 4, 3);

            Assert.Equal(2, thrown.Roles.Count(r => r == DieRole.Counted));
            Assert.Equal(1, thrown.Roles.Count(r => r == DieRole.Impact));

            Assert.Equal(Die.D6, thrown.Result.Impact);
            Assert.Equal(DieRole.Impact, thrown.Roles[2]);
            Assert.False(thrown.ImpactIsFallback);
        }

        // three identical dice: the slot matcher must claim each slot once, not one slot three times
        [Fact]
        public void IdenticalDiceOnIdenticalFaces_AreHandedOutOneApiece()
        {
            TrayThrow thrown = Throw(Die.D6, Die.D6, Die.D6, 4, 4, 4);

            Assert.Equal(new[] { DieRole.Counted, DieRole.Counted, DieRole.Impact }, thrown.Roles);
        }

        [Fact]
        public void OnATie_TheLargerDieIsLeftForImpact()
        {
            TrayThrow thrown = Throw(Die.D12, Die.D6, Die.D8, 4, 4, 4);

            Assert.Equal(Die.D12, thrown.Result.Impact);
            Assert.Equal(DieRole.Impact, thrown.Roles[0]);
        }


        [Theory]
        [InlineData(1, 7, 5, 0)]
        [InlineData(2, 1, 5, 1)]
        [InlineData(2, 7, 1, 2)]
        public void OneSingleOne_IsASnagAtThatSlot(int a, int b, int c, int slot)
        {
            TrayThrow thrown = Throw(Die.D6, Die.D8, Die.D12, a, b, c);

            Assert.True(thrown.Result.Snag);
            Assert.Equal(slot, thrown.SnaggedSlot);
        }

        [Fact]
        public void NoOnes_IsNoSnag() =>
            Assert.Equal(-1, Throw(Die.D6, Die.D8, Die.D12, 2, 7, 5).SnaggedSlot);

        [Fact]
        public void TwoOnes_IsTroubleAndNotASnag()
        {
            TrayThrow thrown = Throw(Die.D6, Die.D8, Die.D12, 1, 1, 5);

            Assert.True(thrown.Result.Trouble);
            Assert.False(thrown.Result.Snag);
            Assert.Equal(-1, thrown.SnaggedSlot);
        }


        // impact d4 is the resolver's fallback for a short pool, not a rolled die; guard it from reaching the felt
        [Fact]
        public void WhenEveryDieCounted_ImpactIsTheResolversFallback()
        {
            var slots = new List<TraySlot>
            {
                new("attr.might.name", Die.D6, 4),
                new("skill.blades.name", Die.D8, 5),
            };

            var result = new PoolResult(
                new[]
                {
                    new RolledDie("skill.blades.name", Die.D8, 5, true),
                    new RolledDie("attr.might.name", Die.D6, 4, true),
                },
                total: 9, impact: Die.D4, ones: 0);

            var thrown = new TrayThrow(result, slots);

            Assert.True(thrown.ImpactIsFallback);
            Assert.All(thrown.Roles, r => Assert.Equal(DieRole.Counted, r));
        }

        // felt and pool disagreeing must not throw; it leaves the dice unmarked rather than claim a wrong one
        [Fact]
        public void ARollNoDieThrew_IsNamedAndLeavesTheDiceUnmarked()
        {
            var slots = new List<TraySlot> { new("attr.might.name", Die.D6, 4) };

            var result = new PoolResult(
                new[] { new RolledDie("attr.might.name", Die.D6, 5, true) },
                total: 5, impact: Die.D4, ones: 0);

            var thrown = new TrayThrow(result, slots);

            Assert.False(thrown.Agrees);
            Assert.Contains("no die on the felt threw", thrown.Disagreement);
            Assert.All(thrown.Roles, r => Assert.Equal(DieRole.None, r));
        }

        [Fact]
        public void ASnagWithNoOneUnderItIsNamedAndPointsAtNothing()
        {
            var slots = new List<TraySlot> { new("attr.might.name", Die.D6, 4) };

            var result = new PoolResult(
                new[] { new RolledDie("attr.might.name", Die.D6, 4, true) },
                total: 4, impact: Die.D4, ones: 1);

            var thrown = new TrayThrow(result, slots);

            Assert.False(thrown.Agrees);
            Assert.Equal(-1, thrown.SnaggedSlot);
        }

        [Fact]
        public void AThrowReadOffTheFeltAgreesWithItselfAndScoresTheSame()
        {
            var slots = new List<TraySlot>
            {
                new("attr.might.name", Die.D6, 4),
                new("skill.blades.name", Die.D8, 5),
                new("gear.axe.name", Die.D6, 2),
            };

            TrayThrow read = TrayThrow.Read(slots);

            Assert.True(read.Agrees);
            Assert.Equal(9, read.Result.Total);
            Assert.Equal(Die.D6, read.Result.Impact);
            Assert.Equal(2, read.ImpactValue);
            Assert.Equal("gear.axe.name", read.ImpactLabelKey);
        }

        [Fact]
        public void AThrowReadOffTheFeltMatchesTheOneTheTableMade()
        {
            TrayThrow made = Throw(Die.D6, Die.D8, Die.D6, 4, 5, 2);

            TrayThrow read = TrayThrow.Read(made.Slots);

            Assert.Equal(made.Result.Total, read.Result.Total);
            Assert.Equal(made.Result.Impact, read.Result.Impact);
            Assert.Equal(made.Result.Ones, read.Result.Ones);
            Assert.Equal(made.Roles, read.Roles);
            Assert.Equal(made.SnaggedSlot, read.SnaggedSlot);
        }
    }
}
