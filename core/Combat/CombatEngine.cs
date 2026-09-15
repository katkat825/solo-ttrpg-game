using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;

namespace Core.Combat
{
    // runs a fight round by round until one side is down
    // resolver, targeting and observer are injected; nothing here reaches for a global
    public sealed class CombatEngine
    {
        readonly IResolver _resolver;
        readonly CombatOptions _options;
        readonly ITargetSelector _heroTargeting;
        readonly ITargetSelector _foeTargeting;
        readonly ICombatObserver _observer;

        public CombatEngine(
            IResolver resolver,
            CombatOptions options = null,
            ITargetSelector heroTargeting = null,
            ITargetSelector foeTargeting = null,
            ICombatObserver observer = null)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _options = options ?? new CombatOptions();
            _heroTargeting = heroTargeting ?? RabbleFirstSelector.Instance;
            _foeTargeting = foeTargeting ?? RabbleFirstSelector.Instance;
            _observer = observer ?? NullCombatObserver.Instance;
        }

        public CombatOptions Options => _options;
        public ICombatObserver Observer => _observer;
        public ITargetSelector FoeTargeting => _foeTargeting;
        public ITargetSelector HeroTargeting => _heroTargeting;

        // throws the pool and rolls the impact itself; the felt-driven path uses Resolve instead
        public AttackOutcome Attack(Actor attacker, Actor target, Attr attr, Skill skill)
        {
            PoolResult roll = _resolver.Resolve(attacker.BuildPool(attr, skill));

            // roll the impact only when something uses it - a miss and a Rabble settle before it
            if (!roll.Beats(target.Defense) || !target.Tier.HasHealthTrack())
                return Resolve(attacker, target, roll, 0);

            return Resolve(attacker, target, roll, _resolver.RollImpact(roll.Impact, _options.ImpactExplodes));
        }

        // resolves a swing whose dice are already on the felt, so the felt and the sim agree
        public AttackOutcome Resolve(Actor attacker, Actor target, PoolResult roll, int impact)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (roll == null) throw new ArgumentNullException(nameof(roll));

            if (!roll.Beats(target.Defense))
                return new AttackOutcome(attacker, target, roll, false, 0);

            // no health track: a hit removes whatever is left
            if (!target.Tier.HasHealthTrack())
                return new AttackOutcome(attacker, target, roll, true, target.Vigor);

            return new AttackOutcome(attacker, target, roll, true, impact);
        }

        public int RollInitiative(Actor actor) => Initiative.Roll(_resolver, actor);

        public EncounterResult Run(Actor hero, IList<Actor> foes)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (foes == null) throw new ArgumentNullException(nameof(foes));

            var foeList = (IReadOnlyList<Actor>)foes.ToList();
            var heroOnly = new[] { hero };
            int round = 0;

            while (round < _options.MaxRounds && !hero.IsDown && foes.Any(f => !f.IsDown))
            {
                round++;
                _observer.RoundBegan(round);

                TakeHeroTurn(hero, foeList);
                if (foes.All(f => f.IsDown)) break;

                TakeFoeTurns(hero, foes, heroOnly);
            }

            bool won = !hero.IsDown && foes.All(f => f.IsDown);
            var result = new EncounterResult(won, round, hero.Vigor);
            _observer.EncounterEnded(result);
            return result;
        }

        void TakeHeroTurn(Actor hero, IReadOnlyList<Actor> foes)
        {
            for (int i = 0; i < _options.HeroActionsPerRound; i++)
            {
                var target = _heroTargeting.Choose(hero, foes);
                if (target == null) return;

                var outcome = Attack(hero, target, _options.HeroAttackAttr, _options.HeroAttackSkill);
                Apply(outcome);
            }
        }

        void TakeFoeTurns(Actor hero, IList<Actor> foes, IReadOnlyList<Actor> heroOnly)
        {
            foreach (var foe in foes.Where(f => !f.IsDown).ToList())
            {
                var target = _foeTargeting.Choose(foe, heroOnly);
                if (target == null) return;

                var outcome = Attack(foe, target, foe.Tier.AttackAttr(), foe.Tier.AttackSkill());
                Apply(outcome);

                if (hero.IsDown) return;
            }
        }

        // applies an outcome: damage, then the conditions it triggered, then a downed actor
        public void Apply(AttackOutcome outcome)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));

            Actor target = outcome.Target;

            if (!outcome.Hit)
            {
                _observer.AttackResolved(outcome);
                return;
            }

            IReadOnlyList<Condition> applied = target.Damage(outcome.Damage);
            _observer.AttackResolved(outcome);

            foreach (Condition c in applied) _observer.ConditionApplied(target, c);

            // conditions the weapon inflicts, after the vigor thresholds so the wound reads first
            foreach (Condition c in outcome.Attacker.Inflicts)
                if (target.ApplyCondition(c)) _observer.ConditionApplied(target, c);

            if (target.IsDown) _observer.ActorDowned(target);
        }
    }
}
