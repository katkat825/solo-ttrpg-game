using System;
using System.Collections.Generic;
using System.Linq;
using Rules.Characters;
using Rules.Resolution;

namespace Rules.Combat
{
    // runs a fight, round by round, until one side is down
    // an instance rather than a static class so every collaborator can be swapped
    // resolver, target selectors and observer are all injected
    // nothing here reaches for a global
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

        // public so a single swing can be resolved outside a full encounter
        public AttackOutcome Attack(Actor attacker, Actor target, Attr attr, Skill skill)
        {
            var roll = _resolver.Resolve(attacker.BuildPool(attr, skill));

            if (!roll.Beats(target.Defense))
                return new AttackOutcome(attacker, target, roll, false, 0);

            // Rabble have no health track - any hit removes them
            if (target.Tier == Tier.Rabble)
                return new AttackOutcome(attacker, target, roll, true, target.MaxVigor);

            int damage = _resolver.RollImpact(roll.Impact, _options.ImpactExplodes);
            return new AttackOutcome(attacker, target, roll, true, damage);
        }

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
            var result = new EncounterResult(won, round, Math.Max(0, hero.Vigor));
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
                ApplyAndReport(outcome, target);
            }
        }

        void TakeFoeTurns(Actor hero, IList<Actor> foes, IReadOnlyList<Actor> heroOnly)
        {
            foreach (var foe in foes.Where(f => !f.IsDown).ToList())
            {
                var target = _foeTargeting.Choose(foe, heroOnly);
                if (target == null) return;

                var skill = foe.Tier == Tier.Rabble ? Skill.None : Skill.Blades;
                var outcome = Attack(foe, target, Attr.Might, skill);
                ApplyAndReport(outcome, target);

                if (hero.IsDown) return;
            }
        }

        void ApplyAndReport(AttackOutcome outcome, Actor target)
        {
            if (outcome.Hit)
            {
                var applied = target.Damage(outcome.Damage);
                _observer.AttackResolved(outcome);

                foreach (var c in applied) _observer.ConditionApplied(target, c);
                if (target.IsDown) _observer.ActorDowned(target);
            }
            else
            {
                _observer.AttackResolved(outcome);
            }
        }
    }
}
