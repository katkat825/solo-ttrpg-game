using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Game.Tray;
using Xunit;

namespace Game.Tests
{
    // every case forces a maximum on purpose: the explosion branch rarely fires in a real run
    public class ImpactChainTests
    {
        static TrayThrow Throw(params int[] values) =>
            new TrayResolution(Pool.Of(
                ("attr.might.name", Die.D8),
                ("skill.blades.name", Die.D6),
                ("gear.axe.name", Die.D6))).Resolve(values);

        // arranging a max leftover is fiddly: ties count the smaller die, so a pool 6 usually gets counted
        static TrayThrow Maxed() => Throw(8, 6, 6);

        static TrayThrow Bare() =>
            new TrayResolution(Pool.Of(
                ("attr.might.name", Die.D8),
                ("gear.axe.name", Die.D6))).Resolve(new[] { 8, 6 });

        static TrayThrow Alone(string key, Die die, int value) =>
            new TrayResolution(Pool.Of((key, die))).Resolve(new[] { value });


        [Fact]
        public void ItTakesTheDieTheFeltLeftOver()
        {
            var chain = new ImpactChain(Throw(5, 4, 2));

            Assert.True(chain.IsReal);
            Assert.Equal(Die.D6, chain.Die);
            Assert.Equal("gear.axe.name", chain.LabelKey);
            Assert.Equal(2, chain.Total);
            Assert.Equal(1, chain.Throws);
            Assert.False(chain.GoesAgain);
        }

        [Fact]
        public void TheSecondThrowIsTheSameDieUnderTheSameName()
        {
            var chain = new ImpactChain(Maxed());

            Pool again = chain.Again();

            Assert.Equal(1, again.Count);
            Assert.Equal(Die.D6, again.Dice[0].Die);
            Assert.Equal(chain.LabelKey, again.Dice[0].LabelKey);
        }


        [Fact]
        public void AMaximumGoesAgain()
        {
            var chain = new ImpactChain(Maxed());

            Assert.True(chain.GoesAgain);
            Assert.Equal(6, chain.Total);
        }

        [Fact]
        public void AndAdds()
        {
            var chain = new ImpactChain(Maxed());

            chain.Landed(3);

            Assert.Equal(9, chain.Total);
            Assert.Equal(2, chain.Throws);
            Assert.False(chain.GoesAgain);
        }

        [Fact]
        public void AndAgain()
        {
            var chain = new ImpactChain(Maxed());

            chain.Landed(6);
            Assert.True(chain.GoesAgain);

            chain.Landed(6);
            Assert.True(chain.GoesAgain);

            chain.Landed(1);

            Assert.Equal(19, chain.Total);
            Assert.Equal(4, chain.Throws);
            Assert.False(chain.GoesAgain);
        }

        // a die stuck on its max is a physics bug; the loop caps like standardresolver's
        [Fact]
        public void ADieStuckOnItsMaximum_StopsBeingThrown()
        {
            var chain = new ImpactChain(Maxed());

            for (int i = 0; i < ImpactChain.MaxThrows * 2; i++) chain.Landed(6);

            Assert.False(chain.GoesAgain);
            Assert.True(chain.Throws >= ImpactChain.MaxThrows);
        }


        // the case that was wrong: a two-die pool's impact is the default d4, once misread as no die and zero damage
        [Fact]
        public void ATwoDiePool_TakesTheDefaultD4OutOfTheBox()
        {
            var chain = new ImpactChain(Bare());

            Assert.True(chain.IsReal);
            Assert.True(chain.FromTheBox);
            Assert.Equal(Die.D4, chain.Die);

            Assert.True(chain.GoesAgain);
            Assert.False(chain.IsExploding);
            Assert.Equal(0, chain.Throws);
            Assert.Equal(0, chain.Total);
        }

        [Fact]
        public void AndItIsNamedForWhatItIs()
        {
            var chain = new ImpactChain(Bare());

            Assert.Equal(KeyConventions.DefaultImpactName, chain.LabelKey);
            Assert.Equal(KeyConventions.WellFormed, KeyConventions.Explain(chain.LabelKey));
            Assert.Equal(chain.LabelKey, chain.Again().Dice[0].LabelKey);
        }

        [Fact]
        public void AndOnceThrownItIsTheDamage()
        {
            var chain = new ImpactChain(Bare());

            chain.Landed(3);

            Assert.Equal(3, chain.Total);
            Assert.Equal(1, chain.Throws);
            Assert.False(chain.GoesAgain);
        }

        [Fact]
        public void AndItExplodesLikeAnyOther()
        {
            var chain = new ImpactChain(Bare());

            chain.Landed(4);

            Assert.True(chain.GoesAgain);
            Assert.True(chain.IsExploding);

            chain.Landed(2);

            Assert.Equal(6, chain.Total);
            Assert.False(chain.GoesAgain);
        }

        [Fact]
        public void ADieOffTheFeltHasAlreadyBeenThrown()
        {
            var chain = new ImpactChain(Throw(5, 4, 2));

            Assert.False(chain.FromTheBox);
            Assert.Equal(1, chain.Throws);
            Assert.True(chain.IsExploding);
        }


        [Fact]
        public void TheReThrowResolvesAsAnOrdinaryPoolOfOne()
        {
            var chain = new ImpactChain(Maxed());

            TrayThrow again = Alone(chain.LabelKey, chain.Die, 4);

            Assert.Single(again.Slots);
            Assert.Equal(4, again.Slots[0].Value);

            chain.Landed(again.Slots[0].Value);

            Assert.Equal(10, chain.Total);
        }

        [Fact]
        public void AFaceTheDieDoesNotHave_IsRefused()
        {
            var chain = new ImpactChain(Maxed());

            Assert.Throws<System.ArgumentOutOfRangeException>(() => chain.Landed(7));
        }
    }
}
