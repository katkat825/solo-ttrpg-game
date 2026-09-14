using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    // who goes first, and the one reaction that comes with going at all (CORE_RULES.md section 8)
    //
    // Both are new in C3 and both are the sort of rule that is easy to write and easy to get
    // subtly wrong: an order that reshuffles itself, a tie that breaks differently on alternate
    // runs, a reaction that recharges when it should not or that steals somebody's turn.
    public class InitiativeTests
    {
        static CombatEngine Engine(IRng rng, CombatOptions opts = null) =>
            new CombatEngine(new StandardResolver(rng), opts);

        // ---- the pool ----

        [Fact]
        public void ItIsGraceAndInsight_AndNoGearDie()
        {
            Pool pool = Initiative.PoolFor(Fixtures.Hero());

            // the Barbarian has Grace d6, no Insight and an axe. one die, and the axe is not in it
            Assert.Equal(1, pool.Count);
            Assert.Equal(Attr.Grace.Key(), pool.Dice[0].LabelKey);
            Assert.Equal(Die.D6, pool.Dice[0].Die);
            Assert.DoesNotContain(pool.Dice, d => d.LabelKey == Fixtures.Hero().WeaponKey);
        }

        // a Rabble's statblock is Might and a club. It has nothing to throw for the order, and
        // that is a statblock rather than a mistake - untrained means a smaller pool, taken to its
        // end (CORE_RULES.md section 1)
        [Fact]
        public void AnActorWithNothingToThrow_ScoresNothing_AndDoesNotThrow()
        {
            var mook = Fixtures.Mook();

            Assert.Equal(0, Initiative.PoolFor(mook).Count);

            // a ScriptedRng with one value throws if asked twice, so this also proves nothing
            // reached the resolver
            Assert.Equal(0, Initiative.Roll(new StandardResolver(new ScriptedRng(6)), mook));
        }

        // ---- the order ----

        [Fact]
        public void TheOrderIsRolledOnce_AndHoldsForTheWholeFight()
        {
            var fight = new Encounter(Engine(new SeededRng(7)), Fixtures.Hero(), Fixtures.StandardEncounter(2));
            fight.Begin();

            Actor[] first = fight.Order.ToArray();

            // three whole rounds of everybody passing
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

        // the hero's own throw comes from the felt in the game, so it can be handed in
        [Fact]
        public void TheHerosScoreCanBeHandedIn_FromTheTable()
        {
            var fight = new Encounter(Engine(new SeededRng(5)), Fixtures.Hero(), Fixtures.StandardEncounter(2));
            fight.Begin(heroInitiative: 99);

            Assert.Equal(99, fight.InitiativeOf(fight.Hero));
            Assert.Same(fight.Hero, fight.Order[0]);
        }

        // four Rabble all rolling nothing have to come out in the same order every time, or the
        // same seed replays a different fight
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

        // and a tie goes to the hero. losing the action economy on a coin flip is the one outcome
        // worth ruling out by hand
        [Fact]
        public void ATieGoesToTheHero()
        {
            var fight = new Encounter(Engine(new SeededRng(1)), Fixtures.Hero(), Fixtures.StandardEncounter(3));
            fight.Begin(heroInitiative: 0);

            // every Rabble rolls nothing too, so the hero is tied with all of them
            Assert.Same(fight.Hero, fight.Order[0]);
        }

        // ---- and the switch that keeps the sim honest ----

        // Run has no initiative in it, so a path that rolled for one would be a different game
        // before the first blow - and one extra throw at the top moves every seeded number after
        // it. Off has to mean "did not roll", not "rolled and ignored it"
        [Fact]
        public void TurnedOff_TheOrderIsMusterOrder_AndNothingIsThrown()
        {
            var foes = Fixtures.StandardEncounter(2);

            // one value: a second call to the rng would throw
            var fight = new Encounter(
                Engine(new ScriptedRng(4), new CombatOptions { RollInitiative = false }),
                Fixtures.Hero(), foes);

            fight.Begin();

            Assert.Same(fight.Hero, fight.Order[0]);
            Assert.Equal(foes, fight.Order.Skip(1));
            Assert.Equal(0, fight.InitiativeOf(fight.Hero));
        }

        // ---- the reaction ----

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

        // an ordinary foe has no reaction, so it has nothing to give the rest of its turn up FOR
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

        // and the hero can ready again next round, because a reaction is per round. Spending it
        // and then readying in the SAME round is not reachable - readying ends the turn, so his
        // next chance to ready is his next turn, which is the round after
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

        // A STRIKE OUT OF TURN. It costs the reaction, not an action, and it does not move the
        // turn on - somebody else is having one
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

        // until the next round, when it comes back
        [Fact]
        public void ItComesBackAtTheTopOfTheRound()
        {
            Encounter fight = Ready(out Encounter _);
            Actor foe = fight.Foes[0];

            fight.React(fight.Hero, foe, Beats(foe.Defense), impact: 1);

            // round out: everybody left passes
            while (fight.Round == 1 && !fight.IsOver) fight.EndTurn();

            Assert.Equal(2, fight.Round);
            Assert.Equal(1, fight.ReactionsLeft(fight.Hero));
        }

        // a readied action lasts until your next turn and no longer - watching all night is not a
        // thing anybody does
        [Fact]
        public void BeingReadiedLastsUntilYourNextTurn()
        {
            Encounter fight = Ready(out Encounter _);

            while (!fight.AwaitingHero && !fight.IsOver) fight.EndTurn();

            Assert.False(fight.IsReadied(fight.Hero));
        }

        // a hero readied, with a Rival still standing and the hero first in the order
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
