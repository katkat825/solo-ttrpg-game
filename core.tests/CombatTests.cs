using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    // covers the combat engine end to end
    // hits and misses against defense, Rabble folding to any successful hit
    // encounters terminating and naming a winner
    // and the action economy - two hero actions a round against one
    public class CombatTests
    {
        static CombatEngine Engine(IRng rng, CombatOptions opts = null) =>
            new CombatEngine(new StandardResolver(rng), opts);

        [Fact]
        public void Rabble_DieToAnySuccessfulHit()
        {
            var hero = Fixtures.Hero();
            var mook = Fixtures.Mook();

            // beats defense 7 comfortably
            var o = Engine(new ScriptedRng(6, 6, 6)).Attack(hero, mook, Attr.Might, Skill.Blades);
            mook.Damage(o.Damage);

            Assert.True(o.Hit);
            Assert.True(mook.IsDown);
        }

        [Fact]
        public void MissedAttack_DealsNoDamage()
        {
            var hero = Fixtures.Hero();
            var rival = Fixtures.Rival(); // defense 11

            var o = Engine(new ScriptedRng(2, 2, 2)).Attack(hero, rival, Attr.Might, Skill.Blades);

            Assert.False(o.Hit);
            Assert.Equal(0, o.Damage);
        }

        [Fact]
        public void Encounter_Terminates_AndReportsAWinner()
        {
            var hero = Fixtures.Hero();
            var foes = Fixtures.StandardEncounter();

            var result = Engine(new SeededRng(12345)).Run(hero, foes);

            Assert.True(result.Rounds > 0);
            Assert.True(result.Rounds <= 40);
            Assert.Equal(result.HeroWon, !hero.IsDown && foes.All(f => f.IsDown));
        }

        [Fact]
        public void MoreHeroActions_ProducesMoreWins()
        {
            // one action a round is unsurvivable
            // this is the whole reason the hero gets two
            Assert.True(WinsWith(2) > WinsWith(1));
        }

        static int WinsWith(int actions)
        {
            var engine = Engine(new SeededRng(99), new CombatOptions { HeroActionsPerRound = actions });
            int wins = 0;

            for (int i = 0; i < 400; i++)
                if (engine.Run(Fixtures.Hero(), Fixtures.StandardEncounter()).HeroWon)
                    wins++;

            return wins;
        }

        [Fact]
        public void StandardEncounter_HasRequestedRabblePlusOneRival()
        {
            var foes = Fixtures.StandardEncounter(6);

            Assert.Equal(6, foes.Count(f => f.Tier == Tier.Rabble));
            Assert.Equal(1, foes.Count(f => f.Tier == Tier.Rival));
        }
    }
}
