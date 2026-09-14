using System.Linq;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Game.Tray;
using Xunit;

namespace Game.Tests
{
    // the Impact die going back in the hand (COMBAT_LOOP.md C2)
    //
    // THIS FILE EXISTS BECAUSE THE BRANCH DOES NOT FIRE OFTEN. A d6 Impact explodes on one hit in
    // six, and only against something with a health track worth the bigger number - so a run of
    // the real table can play four whole fights, land twenty-nine blows and never once take the
    // die back off the felt. That is not evidence the code works; it is evidence nothing tried it.
    // Every case below is a maximum on purpose.
    public class ImpactChainTests
    {
        // the starting hero's pool, in throw order: Might d8, Blades d6, an axe d6
        static TrayThrow Throw(params int[] values) =>
            new TrayResolution(Pool.Of(
                ("attr.might.name", Die.D8),
                ("skill.blades.name", Die.D6),
                ("gear.axe.name", Die.D6))).Resolve(values);

        // AN OPENING THROW WHOSE LEFTOVER DIE IS SHOWING ITS MAXIMUM, which is not as easy to
        // write as it looks. The resolver counts the best two and breaks ties toward the player -
        // equal values count the SMALLER die, leaving the larger free to be Impact - so a 6 in the
        // pool is usually a 6 that got counted. Might 8, Blades 6, axe 6: the d8 and the first d6
        // make 14, and the axe's d6 is left over showing its maximum
        static TrayThrow Maxed() => Throw(8, 6, 6);

        // a pool that counts every die it has, so nothing is left over: the hero's Might and his
        // axe, both counted, which is what an untrained attempt looks like (CORE_RULES.md 1)
        static TrayThrow Bare() =>
            new TrayResolution(Pool.Of(
                ("attr.might.name", Die.D8),
                ("gear.axe.name", Die.D6))).Resolve(new[] { 8, 6 });

        // one die on its own, which is what an explosion re-throws
        static TrayThrow Alone(string key, Die die, int value) =>
            new TrayResolution(Pool.Of((key, die))).Resolve(new[] { value });

        // ---- reading the opening throw ----

        [Fact]
        public void ItTakesTheDieTheFeltLeftOver()
        {
            // 5 and 4 counted, the d6 axe showing 2 left over
            var chain = new ImpactChain(Throw(5, 4, 2));

            Assert.True(chain.IsReal);
            Assert.Equal(Die.D6, chain.Die);
            Assert.Equal("gear.axe.name", chain.LabelKey);
            Assert.Equal(2, chain.Total);
            Assert.Equal(1, chain.Throws);
            Assert.False(chain.GoesAgain);
        }

        // the leftover die is thrown again wearing the name it already had, so the mark on the
        // felt says the same thing twice rather than something new the second time
        [Fact]
        public void TheSecondThrowIsTheSameDieUnderTheSameName()
        {
            var chain = new ImpactChain(Maxed());

            Pool again = chain.Again();

            Assert.Equal(1, again.Count);
            Assert.Equal(Die.D6, again.Dice[0].Die);
            Assert.Equal(chain.LabelKey, again.Dice[0].LabelKey);
        }

        // ---- exploding ----

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

        // the moment the rule exists for: a die that keeps coming up its maximum keeps going
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

        // a die stuck on its maximum is a physics bug, not a lucky player. StandardResolver caps
        // its own loop at the same number
        [Fact]
        public void ADieStuckOnItsMaximum_StopsBeingThrown()
        {
            var chain = new ImpactChain(Maxed());

            for (int i = 0; i < ImpactChain.MaxThrows * 2; i++) chain.Landed(6);

            Assert.False(chain.GoesAgain);
            Assert.True(chain.Throws >= ImpactChain.MaxThrows);
        }

        // ---- the die nobody brought ----

        // A TWO-DIE POOL COUNTS BOTH, so nothing is left over and the Impact die is the d4 the
        // rules hand you by default (CORE_RULES.md section 2). It is a REAL die and it counts -
        // it is just still in the box, so it has to be thrown before it can be read.
        //
        // This is the case that was wrong: the chain called it "no impact die", the felt path read
        // it as zero, and an untrained caster's hit dealt no damage at all while
        // CombatEngine.Attack rolled the same fallback d4 and dealt one to four. The fight check
        // caught it as "hit for nothing"
        [Fact]
        public void ATwoDiePool_TakesTheDefaultD4OutOfTheBox()
        {
            var chain = new ImpactChain(Bare());

            Assert.True(chain.IsReal);
            Assert.True(chain.FromTheBox);
            Assert.Equal(Die.D4, chain.Die);

            // never thrown, so it goes - and that is not an explosion
            Assert.True(chain.GoesAgain);
            Assert.False(chain.IsExploding);
            Assert.Equal(0, chain.Throws);
            Assert.Equal(0, chain.Total);
        }

        // and it wears a name that belongs to no trait, because no trait brought it
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

        // it explodes like any other Impact die - a d4 showing 4 goes again
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

        // a die that WAS left over has been thrown already, and is not from the box
        [Fact]
        public void ADieOffTheFeltHasAlreadyBeenThrown()
        {
            var chain = new ImpactChain(Throw(5, 4, 2));

            Assert.False(chain.FromTheBox);
            Assert.Equal(1, chain.Throws);
            Assert.True(chain.IsExploding);
        }

        // ---- the throw it is handed back is a real one ----

        // the chain and the tray have to agree about what came down: the one-die pool resolves to
        // a TrayThrow whose only slot is the face, and that is what Landed is given
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

        // a face the die does not have means the felt and the pool have come apart, which is the
        // same refusal TrayResolution makes and for the same reason
        [Fact]
        public void AFaceTheDieDoesNotHave_IsRefused()
        {
            var chain = new ImpactChain(Maxed());

            Assert.Throws<System.ArgumentOutOfRangeException>(() => chain.Landed(7));
        }
    }
}
