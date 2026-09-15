using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    public class InitiativeTests
    {
        static CombatEngine Engine(IRng rng, CombatOptions opts = null) =>
            new CombatEngine(new StandardResolver(rng), opts);


        [Fact]
        public void ItIsGraceAndInsight_AndNoGearDie()
        {
            Pool pool = Initiative.PoolFor(Fixtures.Hero());

            Assert.Equal(1, pool.Count);
            Assert.Equal(Attr.Grace.Key(), pool.Dice[0].LabelKey);
            Assert.Equal(Die.D6, pool.Dice[0].Die);
            Assert.DoesNotContain(pool.Dice, d => d.LabelKey == Fixtures.Hero().WeaponKey);
        }

        [Fact]
        public void AnActorWithNothingToThrow_ScoresNothing_AndDoesNotThrow()
        {
            var mook = Fixtures.Mook();

            Assert.Equal(0, Initiative.PoolFor(mook).Count);

            Assert.Equal(0, Initiative.Roll(new StandardResolver(new ScriptedRng(6)), mook));
        }


        [Fact]
        public void TheOrderIsRolledOnce_AndHoldsForTheWholeFight()
        {
            var fight = new Encounter(Engine(new SeededRng(7)), Fixtures.Hero(), Fixtures.StandardEncounter(2));
            fight.Begin();

            Actor[] first = fight.Order.ToArray();

            for (int i = 0; i < first.Length * 3; i++) fight.EndTurn();

            Assert.Equal(first, fight.Order.ToArray());
            Assert.Equal(4, fight.Round);
        }

        [Fact]
        public void EverybodyIsInIt_Exactly_Once()
        {
            var fight = new Encounter(Engine(new SeededRng(3)), Fixtures.Hero(), Fixtures.StandardEncounter(4));
            fight.Begin();

            Assert.Equal(6, fight.Order.Count);
            Assert.Equal(fight.Order.Count, fight.Order.Distinct().Count());
            Assert.Contains(fight.Hero, fight.Order);
        }

        [Fact]
        public void TheOrderRunsHighestFirst()
        {
            var fight = new Encounter(Engine(new SeededRng(11)), Fixtures.Hero(), Fixtures.StandardEncounter(3));
            fight.Begin();

            int[] scores = fight.Order.Select(fight.InitiativeOf).ToArray();

            Assert.Equal(scores.OrderByDescending(n => n).ToArray(), scores);
        }

        [Fact]
        public void TheHerosScoreCanBeHandedIn_FromTheTable()
        {
            var fight = new Encounter(Engine(new SeededRng(5)), Fixtures.Hero(), Fixtures.StandardEncounter(2));
            fight.Begin(heroInitiative: 99);

            Assert.Equal(99, fight.InitiativeOf(fight.Hero));
            Assert.Same(fight.Hero, fight.Order[0]);
        }

        // rabble rolling nothing must come out in a stable order, or a seed replays a different fight
        [Fact]
        public void TiesBreakTheSameWayEveryTime()
        {
            Actor[] First()
            {
                var fight = new Encounter(Engine(new SeededRng(4242)), Fixtures.Hero(),
                                          Fixtures.StandardEncounter(4));
                fight.Begin(heroInitiative: 0);
                return fight.Order.ToArray();
            }

            Actor[] a = First();
            Actor[] b = First();

            Assert.Equal(a.Select(x => x.DebugName), b.Select(x => x.DebugName));
        }

        // ties go to the hero; losing the action economy on a coin flip is worth ruling out
        [Fact]
        public void ATieGoesToTheHero()
        {
            var fight = new Encounter(Engine(new SeededRng(1)), Fixtures.Hero(), Fixtures.StandardEncounter(3));
            fight.Begin(heroInitiative: 0);

            Assert.Same(fight.Hero, fight.Order[0]);
        }


        // off must mean "did not roll", not "rolled and ignored": an extra throw shifts every seeded number after
        [Fact]
        public void TurnedOff_TheOrderIsMusterOrder_AndNothingIsThrown()
        {
            var foes = Fixtures.StandardEncounter(2);

            var fight = new Encounter(
                Engine(new ScriptedRng(4), new CombatOptions { RollInitiative = false }),
                Fixtures.Hero(), foes);

            fight.Begin();

            Assert.Same(fight.Hero, fight.Order[0]);
            Assert.Equal(foes, fight.Order.Skip(1));
            Assert.Equal(0, fight.InitiativeOf(fight.Hero));
        }


        [Fact]
        public void TheHeroHasOneReactionAndAnOrdinaryFoeHasNone()
        {
            var fight = new Encounter(Engine(new SeededRng(1)), Fixtures.Hero(), Fixtures.StandardEncounter(1));
            fight.Begin();

            Assert.Equal(1, fight.ReactionsLeft(fight.Hero));
            Assert.Equal(0, fight.ReactionsLeft(fight.Foes[0]));
        }

        [Fact]
        public void ReadyingGivesUpTheRestOfTheTurn()
        {
            var fight = Ready(out Encounter _);

            Assert.True(fight.IsReadied(fight.Hero));
            Assert.False(fight.AwaitingHero);
        }

        [Fact]
        public void SomebodyWithNoReactionCannotReady()
        {
            var fight = new Encounter(Engine(new SeededRng(1)), Fixtures.Hero(), Fixtures.StandardEncounter(1));
            fight.Begin(heroInitiative: 99);

            fight.EndTurn();

            Actor foe = fight.Acting;

            Assert.NotSame(fight.Hero, foe);
            Assert.False(fight.Ready());
            Assert.False(fight.IsReadied(foe));
        }

        [Fact]
        public void TheHeroCanReadyAgainNextRound()
        {
            Encounter fight = Ready(out Encounter _);
            Actor foe = fight.Foes[0];

            fight.React(fight.Hero, foe, Beats(foe.Defense), impact: 1);

            while (!fight.AwaitingHero && !fight.IsOver) fight.EndTurn();

            Assert.Equal(2, fight.Round);
            Assert.True(fight.Ready());
        }

        [Fact]
        public void AReadiedStrikeCostsAReaction_AndNobodysTurn()
        {
            Encounter fight = Ready(out Encounter _);

            Actor acting = fight.Acting;
            int actionsLeft = fight.ActionsLeft;
            Actor foe = fight.Foes[0];

            AttackOutcome outcome = fight.React(fight.Hero, foe, Beats(foe.Defense), impact: 3);

            Assert.NotNull(outcome);
            Assert.True(outcome.Hit);
            Assert.Same(acting, fight.Acting);
            Assert.Equal(actionsLeft, fight.ActionsLeft);
            Assert.Equal(0, fight.ReactionsLeft(fight.Hero));
            Assert.False(fight.IsReadied(fight.Hero));
        }

        [Fact]
        public void AndThereIsOnlyOne()
        {
            Encounter fight = Ready(out Encounter _);
            Actor foe = fight.Foes[0];

            fight.React(fight.Hero, foe, Beats(foe.Defense), impact: 1);

            Assert.Null(fight.React(fight.Hero, foe, Beats(foe.Defense), impact: 1));
        }

        [Fact]
        public void ItComesBackAtTheTopOfTheRound()
        {
            Encounter fight = Ready(out Encounter _);
            Actor foe = fight.Foes[0];

            fight.React(fight.Hero, foe, Beats(foe.Defense), impact: 1);

            while (fight.Round == 1 && !fight.IsOver) fight.EndTurn();

            Assert.Equal(2, fight.Round);
            Assert.Equal(1, fight.ReactionsLeft(fight.Hero));
        }

        [Fact]
        public void BeingReadiedLastsUntilYourNextTurn()
        {
            Encounter fight = Ready(out Encounter _);

            while (!fight.AwaitingHero && !fight.IsOver) fight.EndTurn();

            Assert.False(fight.IsReadied(fight.Hero));
        }

        static Encounter Ready(out Encounter also)
        {
            var fight = new Encounter(Engine(new SeededRng(1)), Fixtures.Hero(), new[] { Fixtures.Rival() });

            fight.Begin(heroInitiative: 99);
            fight.Ready();

            also = fight;
            return fight;
        }

        static PoolResult Beats(int defense) => new PoolResult(
            new[] { new RolledDie(Attr.Might.Key(), Die.D8, defense, true) },
            defense, Die.D6, ones: 0);
    }
}
