using System;
using System.Collections.Generic;
using System.Linq;
using Core.Dice;
using Core.Resolution;
using Game.Tray;
using Xunit;

namespace Game.Tests
{
    // the felt handing three faces to the rules, and the rules handing back what each die is
    //
    // TrayResolution reimplements no rule - it feeds the faces to StandardResolver through a
    // ScriptedRng and reads the answer. so what is under test here is not the dice system, it
    // is the ORDER: the pool is built in throw order, the resolver answers in value order, and
    // every bug this class can have is the two coming apart
    public class TrayResolutionTests
    {
        // sizes in throw order, matching TrayResolution's own PoolLabels: attribute, skill, gear
        static TrayThrow Throw(Die a, Die b, Die c, params int[] values) =>
            new TrayResolution(new[] { a, b, c }).Resolve(values);

        // ---- the pool it will accept ----

        [Fact]
        public void APoolThatIsNotThreeDice_IsRefused() =>
            Assert.Throws<ArgumentException>(() => new TrayResolution(new[] { Die.D6, Die.D6 }));

        [Fact]
        public void ADieThatIsNotThere_IsRefused() =>
            Assert.Throws<ArgumentException>(() => new TrayResolution(new[] { Die.D6, Die.None, Die.D6 }));

        [Fact]
        public void AFaceThatIsNotOnTheDie_IsRefused() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Throw(Die.D6, Die.D8, Die.D12, 7, 3, 3));

        // ---- throw order is kept ----

        [Fact]
        public void SlotsComeBackInThrowOrder_NotInValueOrder()
        {
            TrayThrow thrown = Throw(Die.D6, Die.D8, Die.D12, 2, 7, 5);

            Assert.Equal(new[] { 2, 7, 5 }, thrown.Slots.Select(s => s.Value));
            Assert.Equal(new[] { Die.D6, Die.D8, Die.D12 }, thrown.Slots.Select(s => s.Die));
        }

        // the load-bearing one. PoolResult.Rolls is sorted by value, so a view that indexed
        // the dice with it would ring the right NUMBER of dice and the wrong ones - and only
        // when the throw came out in a different order than it was made, which is most of the
        // time and never obviously
        [Fact]
        public void RolesAreInThrowOrder_EvenWhenTheResultOrderDiffers()
        {
            // 7 and 5 count, the 2 is left over - so the FIRST die thrown is the Impact die
            TrayThrow thrown = Throw(Die.D6, Die.D8, Die.D12, 2, 7, 5);

            Assert.Equal(new[] { DieRole.Impact, DieRole.Counted, DieRole.Counted }, thrown.Roles);

            // and the resolver really did answer in the other order
            Assert.Equal(new[] { 7, 5, 2 }, thrown.Result.Rolls.Select(r => r.Value));
        }

        [Fact]
        public void ExactlyTwoDiceCount_AndTheLeftoverIsImpact()
        {
            TrayThrow thrown = Throw(Die.D8, Die.D6, Die.D6, 5, 4, 3);

            Assert.Equal(2, thrown.Roles.Count(r => r == DieRole.Counted));
            Assert.Equal(1, thrown.Roles.Count(r => r == DieRole.Impact));

            // the leftover die's SIZE is what the rules call Impact, and it is the die that
            // did not count that carries the role
            Assert.Equal(Die.D6, thrown.Result.Impact);
            Assert.Equal(DieRole.Impact, thrown.Roles[2]);
            Assert.False(thrown.ImpactIsFallback);
        }

        // three dice of one size showing one face is the case where the roll-to-slot matcher
        // has nothing to tell them apart by - each slot must be claimed once, not one slot
        // three times, and no die may be left without a role
        [Fact]
        public void IdenticalDiceOnIdenticalFaces_AreHandedOutOneApiece()
        {
            TrayThrow thrown = Throw(Die.D6, Die.D6, Die.D6, 4, 4, 4);

            Assert.Equal(new[] { DieRole.Counted, DieRole.Counted, DieRole.Impact }, thrown.Roles);
        }

        // ties break toward the player: equal rolls count the SMALLER die, which leaves the
        // larger one free to be Impact. it is a rules decision, and this is the felt reading it
        [Fact]
        public void OnATie_TheLargerDieIsLeftForImpact()
        {
            TrayThrow thrown = Throw(Die.D12, Die.D6, Die.D8, 4, 4, 4);

            Assert.Equal(Die.D12, thrown.Result.Impact);
            Assert.Equal(DieRole.Impact, thrown.Roles[0]);
        }

        // ---- where the 1 landed ----

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

        // two 1s is Trouble, a different tier with its own cue - deliberately not a snag,
        // and deliberately not "the first 1 we found"
        [Fact]
        public void TwoOnes_IsTroubleAndNotASnag()
        {
            TrayThrow thrown = Throw(Die.D6, Die.D8, Die.D12, 1, 1, 5);

            Assert.True(thrown.Result.Trouble);
            Assert.False(thrown.Result.Snag);
            Assert.Equal(-1, thrown.SnaggedSlot);
        }

        // ---- the short pool the tray never throws ----

        // Result.Impact is d4 when nothing was left over, which is the resolver's fallback and
        // not a die anybody rolled. the tray cannot produce it with three dice; this exists so
        // that if a shorter pool ever reaches the felt, "impact d4" is never read as a result
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

        // the felt and the pool coming apart is the one thing this class must never shrug at
        [Fact]
        public void ARollNoDieThrew_Throws()
        {
            var slots = new List<TraySlot> { new("attr.might.name", Die.D6, 4) };

            var result = new PoolResult(
                new[] { new RolledDie("attr.might.name", Die.D6, 5, true) },
                total: 5, impact: Die.D4, ones: 0);

            Assert.Throws<InvalidOperationException>(() => new TrayThrow(result, slots));
        }
    }
}
