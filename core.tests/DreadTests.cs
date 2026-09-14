using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    // the boss tier, and the two things that make it one (CORE_RULES.md section 8)
    //
    // Two actions and a reaction, which were fields that were set and never read anywhere in the
    // repo until Phase C - so `Tier.Dread` LOOKED like it acted twice and did not (SEAMS.md
    // section 3) - and a phase change at half Vigor, which has to fire exactly once and exactly
    // at the threshold or it is a rule nobody can predict from the table.
    public class DreadTests
    {
        static readonly IArchetypeSource Archetypes = new BuiltInArchetypes();

        static Actor Boss() => Archetypes.Create(EngineIds.Dread);

        static CombatEngine Engine(IRng rng = null, ICombatObserver observer = null) =>
            new CombatEngine(new StandardResolver(rng ?? new SeededRng(1)), observer: observer);

        // ---- the action economy, finally read ----

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

        // and it uses both before the turn moves on - which is the whole of the fix
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

        // ---- the phase change ----

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

        // a blow that takes it from full to below half still phases - a threshold is crossed, not
        // landed on
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

        // THE DICE RE-RATE, which is the first caller Die.StepUp has ever had.
        //
        // AGAINST THE BASE AND NOT AGAINST A NUMBER. The statblock is content in code and was
        // re-tuned once already when the sim measured the first attempt at a 0.3% win rate - a
        // test that named d10 and d12 failed for that, which is a test reporting a balance change
        // as a bug. What the rule says is "one step up on everything it has"
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

            // and at least one of them actually moved, or the boss has no dice and this proves
            // nothing at all
            Assert.Contains(System.Enum.GetValues<Attr>(), a => boss.Attribute(a) != before[a]);
        }

        // and never an attribute it has no die for - a modifier on one does nothing anyway, but a
        // boss that grew a Heart die at half health would be a boss with a stat nobody gave it
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

        // ORDER IS IRRELEVANT, which is what F3's pipeline was built for and what nothing could
        // exercise until a positive step existed: a boss that is Winded and then phases lands on
        // the same die as one that phases and is then Winded
        [Fact]
        public void AConditionAndAPhaseCompose_WhicheverArrivesFirst()
        {
            Actor first = Boss();
            first.ApplyCondition(Condition.Winded);
            PhaseChange.Standard(first).Turn();

            Actor second = Boss();
            PhaseChange.Standard(second).Turn();
            second.ApplyCondition(Condition.Winded);

            // one step up and one step down, which is the base die whichever order they arrived in
            Assert.Equal(Boss().BaseAttribute(Attr.Might), first.Attribute(Attr.Might));
            Assert.Equal(first.Attribute(Attr.Might), second.Attribute(Attr.Might));
        }

        // ---- and the encounter fires it, exactly at the threshold ----

        [Fact]
        public void TheEncounterTurnsItWhenABlowTakesItToHalf()
        {
            var recorder = new RecordingCombatObserver();
            Actor boss = Boss();

            var fight = new Encounter(Engine(observer: recorder), Fixtures.Hero(), new[] { boss });
            fight.Phases(PhaseChange.Standard(boss));
            fight.Begin(heroInitiative: 99);

            // one blow short of half
            fight.Strike(boss, Beats(boss.Defense), boss.MaxVigor / 2 - 1);

            Assert.DoesNotContain(recorder.Lines, l => l.Contains("CHANGES"));

            // read AFTER that blow rather than before it: the damage crossed a vigor threshold on
            // the way and Winded the boss, which steps the same die DOWN. That is two rules on one
            // attribute and the phase change is the one being measured here
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

            // the hero's turn does not end itself while he has a Nerve to push with
            // (Encounter.Done), so this says he is done rather than waiting to be asked
            for (int i = 0; i < 12 && !fight.IsOver; i++)
            {
                if (!fight.AwaitingHero || fight.ActionsLeft <= 0) { fight.EndTurn(); continue; }

                fight.Strike(boss, Beats(boss.Defense), 5);
            }

            Assert.Single(recorder.Lines.Where(l => l.Contains("CHANGES")));
        }

        // THE BEHAVIOUR HALF, through the seam a campaign would use. It changes nothing anybody
        // can see while the hero is the only thing on the board worth attacking - that is what
        // CORE_RULES.md section 13 means by no party - so this is what exercises it
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

        // ---- the boss fight is a fight ----

        [Fact]
        public void TheBossFightIsOneDreadAndSomeRabble()
        {
            System.Collections.Generic.IList<Actor> foes = Archetypes.WithRabble(3);

            Assert.Equal(3, foes.Count(f => f.Tier == Tier.Rabble));
            Assert.Single(foes.Where(f => f.Tier == Tier.Dread));
        }

        static PoolResult Beats(int defense) => new PoolResult(
            new[] { new RolledDie(Attr.Might.Key(), Die.D8, defense, true) },
            defense, Die.D6, ones: 0);
    }
}
