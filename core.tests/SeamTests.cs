using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    // proves the seams are real
    // each test substitutes a piece of the engine from outside
    // without editing anything in core/
    // if one of these stops compiling, a seam has been welded shut
    // that is a regression in the engine, not in the test
    public class SeamTests
    {
        // ---- a resolver that ignores the dice entirely ----
        sealed class AlwaysResolver : IResolver
        {
            readonly int _total;
            public AlwaysResolver(int total) => _total = total;

            public PoolResult Resolve(Pool pool) =>
                new PoolResult(new List<RolledDie>(), _total, Die.D6, 0);

            public int RollImpact(Die impact, bool explodes = true) => 1;
        }

        [Fact]
        public void Resolver_CanBeReplacedEntirely()
        {
            var hero = Fixtures.Hero();
            var rival = Fixtures.Rival(); // defense 11

            var never = new CombatEngine(new AlwaysResolver(0));
            var always = new CombatEngine(new AlwaysResolver(99));

            Assert.False(never.Attack(hero, rival, Attr.Might, Skill.Blades).Hit);
            Assert.True(always.Attack(hero, rival, Attr.Might, Skill.Blades).Hit);
        }

        [Fact]
        public void Resolver_CanBeDecorated_WithoutTouchingTheStandardOne()
        {
            var logging = new LoggingResolver(new StandardResolver(new SeededRng(1)));
            var engine = new CombatEngine(logging);

            engine.Run(Fixtures.Hero(), Fixtures.StandardEncounter());

            Assert.NotEmpty(logging.Lines);
        }

        [Fact]
        public void Observer_ReceivesTheWholeFight()
        {
            var recorder = new RecordingCombatObserver();
            var engine = new CombatEngine(
                new StandardResolver(new SeededRng(7)), observer: recorder);

            var result = engine.Run(Fixtures.Hero(), Fixtures.StandardEncounter());

            Assert.NotEmpty(recorder.Lines);
            Assert.Contains(recorder.Lines, l => l.Contains("round 1"));
            Assert.Contains(recorder.Lines, l => l.Contains(result.HeroWon ? "victory" : "defeat"));
        }

        [Fact]
        public void Observers_Compose()
        {
            var a = new RecordingCombatObserver();
            var b = new RecordingCombatObserver();

            var engine = new CombatEngine(
                new StandardResolver(new SeededRng(3)),
                observer: new CompositeCombatObserver(a, b));

            engine.Run(Fixtures.Hero(), Fixtures.StandardEncounter());

            Assert.Equal(a.Lines.Count, b.Lines.Count);
            Assert.NotEmpty(a.Lines);
        }

        // ---- targeting policy, swapped from outside ----
        sealed class LastStandingSelector : ITargetSelector
        {
            public Actor Choose(Actor attacker, IReadOnlyList<Actor> candidates) =>
                candidates.LastOrDefault(c => !c.IsDown);
        }

        [Fact]
        public void TargetSelector_ChangesWhoGetsHit()
        {
            var foes = Fixtures.StandardEncounter(3);
            var engine = new CombatEngine(
                new AlwaysResolver(99),
                heroTargeting: new LastStandingSelector());

            // last in the list is the Rival, so it should be taking the damage
            engine.Run(Fixtures.Hero(), foes);

            Assert.True(foes.Last().IsDown);
        }

        [Fact]
        public void ArchetypeSource_IsAnInterface_ReadyForCampaignData()
        {
            IArchetypeSource source = new BuiltInArchetypes();

            Assert.True(source.Has(BuiltInArchetypes.BarbarianId));
            Assert.Contains(BuiltInArchetypes.RivalId, source.Ids);

            var a = source.Create(BuiltInArchetypes.BarbarianId);
            var b = source.Create(BuiltInArchetypes.BarbarianId);

            Assert.NotSame(a, b); // actors are mutable; never hand out a shared instance
        }

        [Fact]
        public void SeededRun_IsReproducible()
        {
            EncounterResult RunOnce() =>
                new CombatEngine(new StandardResolver(new SeededRng(4242)))
                    .Run(Fixtures.Hero(), Fixtures.StandardEncounter());

            var first = RunOnce();
            var second = RunOnce();

            Assert.Equal(first.HeroWon, second.HeroWon);
            Assert.Equal(first.Rounds, second.Rounds);
            Assert.Equal(first.HeroVigorRemaining, second.HeroVigorRemaining);
        }
    }
}
