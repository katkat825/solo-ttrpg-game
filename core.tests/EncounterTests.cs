using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    // the player-driven path: rounds, whose turn it is, and how many actions they get
    //
    // the property that matters most is the last one in this file - that driving the encounter
    // the way `Run` plays it produces the same fight. `Run` is what SIMULATION.md's numbers were
    // measured with, and a player path that quietly disagreed with it would mean the balance
    // work describes a game nobody is playing
    public class EncounterTests
    {
        static CombatEngine Engine(IRng rng, CombatOptions opts = null, ICombatObserver observer = null) =>
            new CombatEngine(new StandardResolver(rng), opts, observer: observer);

        static Encounter Fight(IRng rng, int rabble = 2, CombatOptions opts = null,
                               ICombatObserver observer = null) =>
            new Encounter(Engine(rng, opts, observer), Fixtures.Hero(), Fixtures.StandardEncounter(rabble));

        // ---- the shape of a round ----

        [Fact]
        public void BeforeItBegins_NobodyIsActing()
        {
            var fight = Fight(new SeededRng(1));

            Assert.Equal(0, fight.Round);
            Assert.Null(fight.Acting);
            Assert.False(fight.AwaitingHero);
        }

        [Fact]
        public void TheHeroActsFirst_AndGetsTheOptionsCount()
        {
            var fight = Fight(new SeededRng(1));
            fight.Begin();

            Assert.Equal(1, fight.Round);
            Assert.True(fight.AwaitingHero);
            Assert.Equal(new CombatOptions().HeroActionsPerRound, fight.ActionsLeft);
        }

        // A TURN ENDS ITSELF WHEN THERE IS NOTHING LEFT TO DECIDE, and the hero out of actions
        // still has a Nerve, which buys a third one (CORE_RULES.md section 7). Ending his turn for
        // him would spend that decision by taking away the chance to make it
        [Fact]
        public void TheHeroOutOfActions_KeepsTheTurn_WhileHeHasANerve()
        {
            var fight = Fight(new SeededRng(1));
            fight.Begin();

            for (int i = new CombatOptions().HeroActionsPerRound; i > 0; i--) fight.Spend();

            Assert.True(fight.Hero.Nerve > 0);
            Assert.True(fight.AwaitingHero);
            Assert.Equal(0, fight.ActionsLeft);
        }

        [Fact]
        public void AndSayingHeIsDone_PassesItOn()
        {
            var fight = Fight(new SeededRng(1));
            fight.Begin();

            for (int i = new CombatOptions().HeroActionsPerRound; i > 0; i--) fight.Spend();
            fight.EndTurn();

            Assert.False(fight.AwaitingHero);
            Assert.Contains(fight.Acting, fight.Foes);
        }

        // with no Nerve there is nothing to wait for, and the turn ends itself like anybody's
        [Fact]
        public void WithNoNerveLeft_TheTurnEndsItself()
        {
            var fight = Fight(new SeededRng(1));
            fight.Begin();

            while (fight.Hero.SpendNerve()) { }

            for (int i = new CombatOptions().HeroActionsPerRound; i > 0; i--) fight.Spend();

            Assert.False(fight.AwaitingHero);
        }

        // and a foe never waits: it has no Nerve and nothing else it could be about to do
        [Fact]
        public void AFoeOutOfActions_PassesItOnAtOnce()
        {
            var fight = Fight(new SeededRng(1));
            fight.Begin();
            fight.EndTurn();

            Actor foe = fight.Acting;
            fight.Spend();

            Assert.NotSame(foe, fight.Acting);
        }

        [Fact]
        public void AnActionCannotBeSpentTwice_NorMoreThanAreLeft()
        {
            var fight = Fight(new SeededRng(1), opts: new CombatOptions { HeroActionsPerRound = 2 });
            fight.Begin();

            Assert.False(fight.Spend(3));
            Assert.True(fight.Spend(2));
            Assert.Equal(0, fight.ActionsLeft);
            Assert.False(fight.Spend());
        }

        [Fact]
        public void EndingTheTurnEarly_GivesUpWhatIsLeft()
        {
            var fight = Fight(new SeededRng(1));
            fight.Begin();
            fight.EndTurn();

            Assert.False(fight.AwaitingHero);
        }

        // ordinary enemies get one action - the whole of the action-economy fix seen from the
        // other side (CORE_RULES.md section 8)
        [Fact]
        public void OrdinaryFoesGetOneAction()
        {
            var fight = Fight(new SeededRng(1));
            fight.Begin();
            fight.EndTurn();

            Assert.Equal(1, fight.ActionsLeft);
            Assert.Equal(1, fight.Acting.ActionsPerRound);
        }

        // and a Dread gets two, because Encounter reads Actor.ActionsPerRound rather than
        // assuming. that field was set and never read anywhere in the repo before Phase C
        [Fact]
        public void ADreadGetsTwo_BecauseTheActorSaysSo()
        {
            var dread = new Actor("boss", maxVigor: 30, defense: 13, Tier.Dread)
                .With(Attr.Might, Die.D10);

            var fight = new Encounter(Engine(new SeededRng(1)), Fixtures.Hero(), new[] { dread });
            fight.Begin();
            fight.EndTurn();

            Assert.Same(dread, fight.Acting);
            Assert.Equal(2, fight.ActionsLeft);
        }

        [Fact]
        public void EverybodyHasActed_AndItIsRoundTwo()
        {
            var fight = Fight(new SeededRng(1), rabble: 2);
            fight.Begin();

            // hero, then three foes (two Rabble and a Rival), then round two
            fight.EndTurn();
            for (int i = 0; i < fight.Foes.Count; i++) fight.EndTurn();

            Assert.Equal(2, fight.Round);
            Assert.True(fight.AwaitingHero);
        }

        // ---- the downed are skipped, and the fight ends ----

        [Fact]
        public void ADownedFoe_DoesNotGetATurn()
        {
            var fight = Fight(new SeededRng(1), rabble: 2);
            fight.Begin();

            Actor first = fight.Foes[0];
            first.Damage(first.MaxVigor);

            fight.EndTurn();

            Assert.NotSame(first, fight.Acting);
        }

        [Fact]
        public void TheLastFoeFalling_EndsIt_AndTheHeroWon()
        {
            var fight = Fight(new SeededRng(1), rabble: 1);
            fight.Begin();

            foreach (Actor foe in fight.Foes) foe.Damage(foe.MaxVigor);

            fight.EndTurn();

            Assert.True(fight.IsOver);
            Assert.True(fight.Result.HeroWon);
            Assert.Null(fight.Acting);
        }

        [Fact]
        public void TheHeroFalling_EndsIt_AndHeDidNot()
        {
            var fight = Fight(new SeededRng(1));
            fight.Begin();

            fight.Hero.Damage(fight.Hero.MaxVigor);
            fight.EndTurn();

            Assert.True(fight.IsOver);
            Assert.False(fight.Result.HeroWon);
        }

        [Fact]
        public void NothingHappensAfterItIsOver()
        {
            var fight = Fight(new SeededRng(1), rabble: 1);
            fight.Begin();

            foreach (Actor foe in fight.Foes) foe.Damage(foe.MaxVigor);
            fight.EndTurn();

            EncounterResult was = fight.Result;

            fight.EndTurn();
            fight.Spend();

            Assert.Same(was, fight.Result);
        }

        // ---- striking ----

        [Fact]
        public void AStrikeCostsAnAction()
        {
            var fight = Fight(new ScriptedRng(6, 6, 6, 4), rabble: 1);
            fight.Begin();

            int had = fight.ActionsLeft;
            fight.Strike(fight.Foes[0], Attr.Might, Skill.Blades);

            Assert.Equal(had - 1, fight.ActionsLeft);
        }

        [Fact]
        public void AStrikeOffTheFelt_UsesTheDieItWasHanded()
        {
            var rival = Fixtures.Rival();   // defense 11, 8 vigor
            var fight = new Encounter(Engine(new ScriptedRng(1)), Fixtures.Hero(), new[] { rival });
            fight.Begin();

            var roll = new PoolResult(
                new[] { new RolledDie(Attr.Might.Key(), Die.D8, 8, true),
                        new RolledDie(Skill.Blades.Key(), Die.D6, 5, true) },
                13, Die.D6, 0);

            AttackOutcome outcome = fight.Strike(rival, roll, impact: 4);

            Assert.True(outcome.Hit);
            Assert.Equal(4, outcome.Damage);
            Assert.Equal(4, rival.Vigor);
        }

        [Fact]
        public void ThereIsNoStrikingOnceItIsOver()
        {
            var fight = Fight(new SeededRng(1), rabble: 1);
            fight.Begin();

            foreach (Actor foe in fight.Foes) foe.Damage(foe.MaxVigor);
            fight.EndTurn();

            Assert.True(fight.IsOver);
            Assert.Null(fight.Strike(fight.Foes[0], Attr.Might, Skill.Blades));
        }

        // and never at something already lying down. it would spend a real action on a corpse,
        // which is the kind of thing a mis-click does at speed
        [Fact]
        public void ThereIsNoStrikingSomethingAlreadyDown()
        {
            var fight = Fight(new SeededRng(1), rabble: 2);
            fight.Begin();

            Actor first = fight.Foes[0];
            first.Damage(first.MaxVigor);

            int had = fight.ActionsLeft;

            Assert.Null(fight.Strike(first, Attr.Might, Skill.Blades));
            Assert.Equal(had, fight.ActionsLeft);
        }

        // ---- foe behaviour is injectable, per foe (the seam C6 needs) ----

        sealed class NeverSelector : ITargetSelector
        {
            public Actor Choose(Actor attacker, IReadOnlyList<Actor> candidates) => null;
        }

        [Fact]
        public void AFoeCanBeGivenItsOwnBehaviour_WithoutDisturbingTheOthers()
        {
            var fight = Fight(new SeededRng(1), rabble: 2);
            Actor pacifist = fight.Foes[0];

            fight.Behaviour(pacifist, new NeverSelector());

            Assert.Null(fight.TargetFor(pacifist));
            Assert.Same(fight.Hero, fight.TargetFor(fight.Foes[1]));
        }

        [Fact]
        public void TakingTheBehaviourBackOff_RestoresTheEnginesDefault()
        {
            var fight = Fight(new SeededRng(1), rabble: 1);
            Actor foe = fight.Foes[0];

            fight.Behaviour(foe, new NeverSelector());
            fight.Behaviour(foe, null);

            Assert.Same(fight.Hero, fight.TargetFor(foe));
        }

        // ---- and the property the whole path rests on ----

        // DRIVEN THE WAY `Run` PLAYS IT, IT IS THE SAME FIGHT. Same seed, same statblocks, same
        // choices - the hero takes his actions against the engine's own targeting, then every foe
        // takes one - so the two paths make the same calls on the same RNG in the same order and
        // must land on the same result. If this ever fails, SIMULATION.md is describing one game
        // and the table is playing another.
        //
        // INITIATIVE IS OFF FOR THIS ONE, and that is the honest comparison rather than a dodge.
        // `Run` has no initiative in it - hero, then every foe, every round - so a path that rolled
        // for the order would be a different game before the first blow, and this test would be
        // measuring the difference between two rules rather than between two implementations of
        // one. What initiative COSTS is a separate question with a separate answer, and the sim
        // prints it (PlayerPathReport)
        [Theory]
        [InlineData(1)]
        [InlineData(4242)]
        [InlineData(99)]
        public void DrivenLikeRun_ItIsRun(int seed)
        {
            EncounterResult scripted = new CombatEngine(new StandardResolver(new SeededRng(seed)))
                .Run(Fixtures.Hero(), Fixtures.StandardEncounter());

            EncounterResult played = AutoPlay(seed);

            Assert.Equal(scripted.HeroWon, played.HeroWon);
            Assert.Equal(scripted.Rounds, played.Rounds);
            Assert.Equal(scripted.HeroVigorRemaining, played.HeroVigorRemaining);
        }

        // the auto-player: exactly the choices CombatEngine.Run makes, made from outside
        static EncounterResult AutoPlay(int seed)
        {
            var engine = new CombatEngine(
                new StandardResolver(new SeededRng(seed)),
                new CombatOptions { RollInitiative = false });
            var fight = new Encounter(engine, Fixtures.Hero(), Fixtures.StandardEncounter());

            fight.Begin();

            while (!fight.IsOver)
            {
                Actor acting = fight.Acting;

                if (ReferenceEquals(acting, fight.Hero))
                {
                    Actor target = engine.HeroTargeting.Choose(acting, fight.Foes);

                    // out of actions and holding the turn open for a Nerve he is never going to
                    // spend. `Run` takes exactly its actions and moves on, so this says so out
                    // loud - see Encounter.Done for why the hero's turn waits to be told
                    if (target == null || fight.ActionsLeft <= 0) { fight.EndTurn(); continue; }

                    fight.Strike(target, engine.Options.HeroAttackAttr, engine.Options.HeroAttackSkill);
                    continue;
                }

                Actor prey = fight.TargetFor(acting);

                if (prey == null) { fight.EndTurn(); continue; }

                fight.Strike(prey, acting.Tier.AttackAttr(), acting.Tier.AttackSkill());
            }

            return fight.Result;
        }
    }
}
