using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    public class CombatTests
    {
        static CombatEngine Engine(IRng rng, CombatOptions opts = null) =>
            new CombatEngine(new StandardResolver(rng), opts);

        [Fact]
        public void Rabble_DieToAnySuccessfulHit()
        {
            var hero = Fixtures.Hero();
            var mook = Fixtures.Mook();

            var o = Engine(new ScriptedRng(6, 6, 6)).Attack(hero, mook, Attr.Might, Skill.Blades);
            mook.Damage(o.Damage);

            Assert.True(o.Hit);
            Assert.True(mook.IsDown);
        }

        [Fact]
        public void MissedAttack_DealsNoDamage()
        {
            var hero = Fixtures.Hero();
            var rival = Fixtures.Rival();

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


        [Fact]
        public void ZeroDamage_DoesNotRemoveARabble()
        {
            var mook = Fixtures.Mook();

            // a miss reports damage 0; without the guard, applying it would clear the board with a whiff
            mook.Damage(0);

            Assert.False(mook.IsDown);
        }

        [Fact]
        public void AMissOnARabble_LeavesItStanding()
        {
            var hero = Fixtures.Hero();
            var mook = Fixtures.Mook();
            var engine = Engine(new ScriptedRng(1, 1, 1));

            var o = engine.Attack(hero, mook, Attr.Might, Skill.Blades);
            engine.Apply(o);

            Assert.False(o.Hit);
            Assert.False(mook.IsDown);
        }

        [Fact]
        public void AHitOnARabble_ReportsEnoughToRemoveIt_AndDoes()
        {
            var hero = Fixtures.Hero();
            var mook = Fixtures.Mook();
            var engine = Engine(new ScriptedRng(6, 6, 6));

            var o = engine.Attack(hero, mook, Attr.Might, Skill.Blades);
            engine.Apply(o);

            Assert.True(o.Hit);
            Assert.Equal(mook.MaxVigor, o.Damage);
            Assert.True(mook.IsDown);
        }


        [Fact]
        public void Resolve_TakesTheDamageOffTheFelt_RatherThanRollingAgain()
        {
            var hero = Fixtures.Hero();
            var rival = Fixtures.Rival();

            var roll = Thrown(rival.Defense + 1, Die.D6);
            var engine = Engine(new ScriptedRng(1));

            var o = engine.Resolve(hero, rival, roll, impact: 5);
            engine.Apply(o);

            Assert.True(o.Hit);
            Assert.Equal(5, o.Damage);
            Assert.Equal(rival.MaxVigor - 5, rival.Vigor);
        }

        [Fact]
        public void Resolve_ShortOfDefense_IsAMissAndCostsNothing()
        {
            var hero = Fixtures.Hero();
            var rival = Fixtures.Rival();

            var o = Engine(new ScriptedRng(1)).Resolve(hero, rival, Thrown(rival.Defense - 1, Die.D6), impact: 6);

            Assert.False(o.Hit);
            Assert.Equal(0, o.Damage);
        }

        [Fact]
        public void Resolve_OnARabble_IgnoresTheImpactDie()
        {
            var hero = Fixtures.Hero();
            var mook = Fixtures.Mook();

            var o = Engine(new ScriptedRng(1)).Resolve(hero, mook, Thrown(mook.Defense, Die.D12), impact: 11);

            Assert.True(o.Hit);
            Assert.Equal(mook.MaxVigor, o.Damage);
        }

        [Fact]
        public void Resolve_AndAttack_AgreeOnTheSameThrow()
        {
            var hero = Fixtures.Hero();

            var thrown = Engine(new ScriptedRng(4, 4, 4, 3)).Attack(hero, Fixtures.Rival(), Attr.Might, Skill.Blades);
            var offTheFelt = Engine(new ScriptedRng(1)).Resolve(hero, Fixtures.Rival(), thrown.Roll, impact: 3);

            Assert.Equal(thrown.Hit, offTheFelt.Hit);
            Assert.Equal(thrown.Damage, offTheFelt.Damage);
        }

        static PoolResult Thrown(int total, Die impact) => new PoolResult(
            new[]
            {
                new RolledDie(Attr.Might.Key(), Die.D8, total - 1, true),
                new RolledDie(Skill.Blades.Key(), Die.D6, 1, true),
            },
            total, impact, ones: 0);

    }
}
