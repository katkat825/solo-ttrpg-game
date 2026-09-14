using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;

namespace Core.Combat
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

        // who is watching, for Encounter - the player-driven path narrates through the same
        // observer this engine was built with, so a fight has one audience however it is driven
        public ICombatObserver Observer => _observer;

        // the default behaviour a foe falls back to. Encounter lets one be attached per foe,
        // which is what a Dread's phase change swaps (C6)
        public ITargetSelector FoeTargeting => _foeTargeting;

        public ITargetSelector HeroTargeting => _heroTargeting;

        // public so a single swing can be resolved outside a full encounter
        //
        // THE RESOLVER THROWS AND THE RESOLVER ROLLS THE IMPACT. right for the sim and for a fight
        // nobody is watching. In front of a player the handful is already lying on the felt, so
        // the game's path is Resolve below - one rule, two ways of learning what the dice said
        public AttackOutcome Attack(Actor attacker, Actor target, Attr attr, Skill skill)
        {
            PoolResult roll = _resolver.Resolve(attacker.BuildPool(attr, skill));

            // the Impact die is rolled ONLY when something is going to be done with it. a miss and
            // a Rabble both settle before it, exactly as they always have - moving that call moves
            // every seeded number after it and SIMULATION.md's figures with them
            if (!roll.Beats(target.Defense) || !target.Tier.HasHealthTrack())
                return Resolve(attacker, target, roll, 0);

            return Resolve(attacker, target, roll, _resolver.RollImpact(roll.Impact, _options.ImpactExplodes));
        }

        // THE SAME SWING, WHEN THE DICE HAVE ALREADY BEEN THROWN (COMBAT_LOOP.md C0).
        //
        // The board throws the hero's pool on the real tray and reads the answer off the felt, so
        // by the time the rules are asked, both numbers exist: the PoolResult the resolver made of
        // the faces, and the Impact die's value as it lies there with a ring round it. Rolling the
        // Impact again here would be a hidden roll deciding how hard the hero hit something, and
        // CORE_RULES.md pillar 1 says every roll is a visible handful hitting the table.
        //
        // Everything that makes a swing a swing is in this method and nowhere else - whether it
        // beat Defense, and what happens to a Rabble - so the felt and the sim cannot come to
        // different conclusions about the same numbers.
        public AttackOutcome Resolve(Actor attacker, Actor target, PoolResult roll, int impact)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (roll == null) throw new ArgumentNullException(nameof(roll));

            if (!roll.Beats(target.Defense))
                return new AttackOutcome(attacker, target, roll, false, 0);

            // no health track, so the number is not a measurement of anything - it is however much
            // is left, because a hit removes one (CORE_RULES.md section 8, Tier.HasHealthTrack)
            if (!target.Tier.HasHealthTrack())
                return new AttackOutcome(attacker, target, roll, true, target.Vigor);

            return new AttackOutcome(attacker, target, roll, true, impact);
        }

        // WHO GOES FIRST. The rules' own throw, for anybody whose dice are not on the felt -
        // which is everybody but the hero (Initiative.cs). Here rather than on Encounter because
        // the resolver is this object's and stays this object's
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
            // Actor clamps Vigor at 0 since F3, so there is nothing left to paper over here
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

                // stated once, on the tier - the sim's auto-player and the fight on the table
                // ask the same question and got the same answer written out separately until C2
                var outcome = Attack(foe, target, foe.Tier.AttackAttr(), foe.Tier.AttackSkill());
                Apply(outcome);

                if (hero.IsDown) return;
            }
        }

        // what an outcome DOES, and the only place it is done. public since C0 because the
        // player-driven path resolves a swing off the felt and then needs exactly this - the
        // damage applied, the conditions it triggered narrated, and a downed actor announced,
        // in that order. a second copy of this in game/ would be the fight and the sim applying
        // the same outcome two different ways
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
            if (target.IsDown) _observer.ActorDowned(target);
        }
    }
}
