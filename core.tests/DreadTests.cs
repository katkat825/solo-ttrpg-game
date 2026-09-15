using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    public class DreadTests
    {
        static readonly IArchetypeSource Archetypes = new BuiltInArchetypes();

        static Actor Boss() => Archetypes.Create(EngineIds.Dread);

        static CombatEngine Engine(IRng rng = null, ICombatObserver observer = null) =>
            new CombatEngine(new StandardResolver(rng ?? new SeededRng(1)), observer: observer);


        [Fact]
        public void ADreadActsTwiceAndHasAReaction()
        {
            Actor boss = Boss();

            Assert.Equal(Tier.Dread, boss.Tier);
            Assert.Equal(2, boss.ActionsPerRound);
            Assert.Equal(1, boss.ReactionsPerRound);
        }

        [Fact]
        public void AndTheEncounterGivesItBoth()
        {
            var fight = new Encounter(Engine(), Fixtures.Hero(), new[] { Boss() });
            fight.Begin(heroInitiative: 99);
            fight.EndTurn();

            Assert.Equal(Tier.Dread, fight.Acting.Tier);
            Assert.Equal(2, fight.ActionsLeft);
            Assert.Equal(1, fight.ReactionsLeft(fight.Acting));
        }

        [Fact]
        public void AndItTakesBothBeforeTheTurnPassesOn()
        {
            var fight = new Encounter(Engine(), Fixtures.Hero(), new[] { Boss() });
            fight.Begin(heroInitiative: 99);
            fight.EndTurn();

            Actor boss = fight.Acting;

            fight.Spend();
            Assert.Same(boss, fight.Acting);

            fight.Spend();
            Assert.NotSame(boss, fight.Acting);
        }


        [Fact]
        public void ItIsNotDueAtFullVigor()
        {
            Actor boss = Boss();
            var phase = PhaseChange.Standard(boss);

            Assert.False(phase.Due);
            Assert.False(phase.Turned);
        }

        [Fact]
        public void ItIsDueAtExactlyHalf_AndNotOneAbove()
        {
            Actor boss = Boss();
            var phase = PhaseChange.Standard(boss);

            boss.Damage(boss.MaxVigor / 2 - 1);
            Assert.False(phase.Due);

            boss.Damage(1);
            Assert.True(phase.Due);
        }

        // a threshold is crossed, not landed on, so full-to-below-half still phases
        [Fact]
        public void OneBigBlowStillTurnsIt()
        {
            Actor boss = Boss();
            var phase = PhaseChange.Standard(boss);

            boss.Damage(boss.MaxVigor - 1);

            Assert.True(phase.Due);
        }

        [Fact]
        public void AndAKillingBlowDoesNot()
        {
            Actor boss = Boss();
            var phase = PhaseChange.Standard(boss);

            boss.Damage(boss.MaxVigor);

            Assert.True(boss.IsDown);
            Assert.False(phase.Due);
        }

        [Fact]
        public void TurningStepsEveryDieItHasUp()
        {
            Actor boss = Boss();
            var phase = PhaseChange.Standard(boss);

            var before = System.Enum.GetValues<Attr>().ToDictionary(a => a, boss.Attribute);

            phase.Turn();

            Assert.True(phase.Turned);

            foreach (Attr a in System.Enum.GetValues<Attr>())
                Assert.Equal(before[a].IsReal() ? before[a].StepUp() : Die.None, boss.Attribute(a));

            Assert.Contains(System.Enum.GetValues<Attr>(), a => boss.Attribute(a) != before[a]);
        }

        [Fact]
        public void AndNeverOneItHasNoDieFor()
        {
            Actor boss = Boss();

            Assert.Equal(Die.None, boss.Attribute(Attr.Heart));

            PhaseChange.Standard(boss).Turn();

            Assert.Equal(Die.None, boss.Attribute(Attr.Heart));
        }

        [Fact]
        public void AndOnlyOnce()
        {
            Actor boss = Boss();
            Die was = boss.Attribute(Attr.Might);
            var phase = PhaseChange.Standard(boss);

            Assert.True(phase.Turn());
            Assert.False(phase.Turn());
            Assert.Equal(was.StepUp(), boss.Attribute(Attr.Might));
        }

        [Fact]
        public void AConditionAndAPhaseCompose_WhicheverArrivesFirst()
        {
            Actor first = Boss();
            first.ApplyCondition(Condition.Winded);
            PhaseChange.Standard(first).Turn();

            Actor second = Boss();
            PhaseChange.Standard(second).Turn();
            second.ApplyCondition(Condition.Winded);

            // one step up and one down is the base die, in either order
            Assert.Equal(Boss().BaseAttribute(Attr.Might), first.Attribute(Attr.Might));
            Assert.Equal(first.Attribute(Attr.Might), second.Attribute(Attr.Might));
        }


        [Fact]
        public void TheEncounterTurnsItWhenABlowTakesItToHalf()
        {
            var recorder = new RecordingCombatObserver();
            Actor boss = Boss();

            var fight = new Encounter(Engine(observer: recorder), Fixtures.Hero(), new[] { boss });
            fight.Phases(PhaseChange.Standard(boss));
            fight.Begin(heroInitiative: 99);

            fight.Strike(boss, Beats(boss.Defense), boss.MaxVigor / 2 - 1);

            Assert.DoesNotContain(recorder.Lines, l => l.Contains("CHANGES"));

            Die pressed = boss.Attribute(Attr.Might);

            fight.Strike(boss, Beats(boss.Defense), 1);

            Assert.Contains(recorder.Lines, l => l.Contains("CHANGES"));
            Assert.Equal(pressed.StepUp(), boss.Attribute(Attr.Might));
        }

        [Fact]
        public void AndSaysSoExactlyOnce()
        {
            var recorder = new RecordingCombatObserver();
            Actor boss = Boss();

            var fight = new Encounter(Engine(observer: recorder), Fixtures.Hero(), new[] { boss });
            fight.Phases(PhaseChange.Standard(boss));
            fight.Begin(heroInitiative: 99);

            for (int i = 0; i < 12 && !fight.IsOver; i++)
            {
                if (!fight.AwaitingHero || fight.ActionsLeft <= 0) { fight.EndTurn(); continue; }

                fight.Strike(boss, Beats(boss.Defense), 5);
            }

            Assert.Single(recorder.Lines, l => l.Contains("CHANGES"));
        }

        [Fact]
        public void AndSwapsTheBehaviourThroughTheSameSeamACampaignWouldUse()
        {
            Actor boss = Boss();

            var fight = new Encounter(Engine(), Fixtures.Hero(), new[] { boss });
            var phase = PhaseChange.Standard(boss);

            fight.Phases(phase);
            fight.Begin(heroInitiative: 99);

            Assert.NotSame(phase.Then, fight.BehaviourOf(boss));

            fight.Strike(boss, Beats(boss.Defense), boss.MaxVigor / 2);

            Assert.Same(phase.Then, fight.BehaviourOf(boss));
        }


        [Fact]
        public void TheBossFightIsOneDreadAndSomeRabble()
        {
            System.Collections.Generic.IList<Actor> foes = Archetypes.WithRabble(3);

            Assert.Equal(3, foes.Count(f => f.Tier == Tier.Rabble));
            Assert.Single(foes, f => f.Tier == Tier.Dread);
        }

        static PoolResult Beats(int defense) => new PoolResult(
            new[] { new RolledDie(Attr.Might.Key(), Die.D8, defense, true) },
            defense, Die.D6, ones: 0);
    }
}
