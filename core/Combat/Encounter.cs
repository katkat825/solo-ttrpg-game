using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;

namespace Core.Combat
{
    // the player-driven counterpart to Run; Run is untouched, and PlayerPathReport checks the two still agree
    public sealed class Encounter
    {
        readonly CombatEngine _engine;

        readonly List<Actor> _foes;

        readonly List<Actor> _order = new List<Actor>();

        readonly Dictionary<Actor, ITargetSelector> _behaviour =
            new Dictionary<Actor, ITargetSelector>(ReferenceEqualityComparer.Instance);

        readonly Dictionary<Actor, int> _initiative =
            new Dictionary<Actor, int>(ReferenceEqualityComparer.Instance);

        readonly Dictionary<Actor, int> _reacted =
            new Dictionary<Actor, int>(ReferenceEqualityComparer.Instance);

        readonly HashSet<Actor> _readied = new HashSet<Actor>(ReferenceEqualityComparer.Instance);

        readonly List<PhaseChange> _phases = new List<PhaseChange>();

        int _turn;

        int _actionsLeft;

        public Encounter(CombatEngine engine, Actor hero, IEnumerable<Actor> foes)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _foes = (foes ?? throw new ArgumentNullException(nameof(foes))).ToList();
        }

        public Actor Hero { get; }

        public IReadOnlyList<Actor> Foes => _foes;

        public IReadOnlyList<Actor> Order => _order;

        public int Round { get; private set; }

        public EncounterResult Result { get; private set; }

        public bool IsOver => Result != null;

        public Actor Acting => IsOver || _order.Count == 0 ? null : _order[_turn];

        public int ActionsLeft => IsOver ? 0 : _actionsLeft;

        public bool AwaitingHero => !IsOver && ReferenceEquals(Acting, Hero);


        public void Phases(PhaseChange change)
        {
            if (change != null) _phases.Add(change);
        }

        public IReadOnlyList<PhaseChange> PhaseChanges => _phases;

        // after every blow - the only moment vigor moves, so a phase turns exactly at the threshold
        void CheckPhases()
        {
            foreach (PhaseChange phase in _phases)
            {
                if (!phase.Due || !phase.Turn()) continue;

                if (phase.Then != null) Behaviour(phase.Actor, phase.Then);

                _engine.Observer.PhaseChanged(phase.Actor);
            }
        }


        public void Behaviour(Actor foe, ITargetSelector selector)
        {
            if (foe == null) return;

            if (selector == null) _behaviour.Remove(foe);
            else _behaviour[foe] = selector;
        }

        public ITargetSelector BehaviourOf(Actor foe) =>
            foe != null && _behaviour.TryGetValue(foe, out ITargetSelector selector)
                ? selector
                : _engine.FoeTargeting;

        public Actor TargetFor(Actor foe) => BehaviourOf(foe).Choose(foe, HeroOnly);

        Actor[] HeroOnly => new[] { Hero };


        public void Begin(int? heroInitiative = null)
        {
            if (Round > 0) return;

            BuildOrder(heroInitiative);

            if (Decided()) { Finish(); return; }

            Round = 1;
            _turn = 0;
            _engine.Observer.RoundBegan(Round);

            if (_order[_turn].IsDown) Advance();
            else BeginTurn();
        }

        void BuildOrder(int? heroInitiative)
        {
            _order.Clear();
            _initiative.Clear();

            var mustered = new List<Actor> { Hero };
            mustered.AddRange(_foes);

            // off means off, including the throw - a wasted roll shifts every seeded number after it
            if (!_engine.Options.RollInitiative)
            {
                _order.AddRange(mustered);
                return;
            }

            _initiative[Hero] = heroInitiative ?? _engine.RollInitiative(Hero);

            foreach (Actor foe in _foes) _initiative[foe] = _engine.RollInitiative(foe);

            _order.AddRange(mustered
                .Select((a, i) => (Actor: a, Seat: i))
                .OrderByDescending(e => _initiative[e.Actor])
                .ThenBy(e => e.Seat)
                .Select(e => e.Actor));
        }


        // a load must not re-roll the order, or a player could reload until it suited them
        public void Resume(IReadOnlyList<Actor> order, int round, int turn, int actionsLeft,
                           IReadOnlyDictionary<Actor, int> initiative = null)
        {
            if (Round > 0) return;

            _order.Clear();
            _initiative.Clear();

            var mustered = new List<Actor> { Hero };
            mustered.AddRange(_foes);

            foreach (Actor actor in order ?? Array.Empty<Actor>())
                if (actor != null && mustered.Contains(actor) && !_order.Contains(actor))
                    _order.Add(actor);

            foreach (Actor actor in mustered)
                if (!_order.Contains(actor)) _order.Add(actor);

            if (initiative != null)
                foreach (KeyValuePair<Actor, int> rolled in initiative)
                    if (rolled.Key != null && _order.Contains(rolled.Key))
                        _initiative[rolled.Key] = rolled.Value;

            Round = Math.Max(1, round);
            _turn = _order.Count == 0 ? 0 : ((turn % _order.Count) + _order.Count) % _order.Count;

            _engine.Observer.RoundBegan(Round);

            if (Decided()) { Finish(); return; }

            // resume, don't BeginTurn - that would refill actions and clear a readied strike
            _actionsLeft = Math.Clamp(actionsLeft, 0, ActionsFor(Acting));

            if (Acting != null && Acting.IsDown) Advance();
            else _engine.Observer.TurnBegan(Acting, Round);
        }

        public int InitiativeOf(Actor actor) =>
            actor != null && _initiative.TryGetValue(actor, out int score) ? score : 0;

        void BeginTurn()
        {
            _actionsLeft = ActionsFor(Acting);

            _readied.Remove(Acting);

            _engine.Observer.TurnBegan(Acting, Round);
        }


        public int ReactionsLeft(Actor actor)
        {
            if (actor == null) return 0;

            return ReactionsFor(actor) - (_reacted.TryGetValue(actor, out int used) ? used : 0);
        }

        int ReactionsFor(Actor actor) =>
            ReferenceEquals(actor, Hero) ? _engine.Options.HeroReactionsPerRound : actor.ReactionsPerRound;

        public bool Ready()
        {
            Actor acting = Acting;

            if (IsOver || Round == 0 || acting == null) return false;
            if (ReactionsLeft(acting) <= 0) return false;

            _readied.Add(acting);
            EndTurn();

            return true;
        }

        public bool IsReadied(Actor actor) => actor != null && _readied.Contains(actor);

        public AttackOutcome React(Actor reactor, Actor target, PoolResult roll, int impact)
        {
            if (IsOver || reactor == null || target == null || target.IsDown) return null;
            if (ReactionsLeft(reactor) <= 0 || reactor.IsDown) return null;

            _reacted[reactor] = (_reacted.TryGetValue(reactor, out int used) ? used : 0) + 1;
            _readied.Remove(reactor);

            AttackOutcome outcome = _engine.Resolve(reactor, target, roll, impact);
            _engine.Apply(outcome);

            CheckPhases();

            if (Decided()) Finish();

            return outcome;
        }

        int ActionsFor(Actor actor) =>
            ReferenceEquals(actor, Hero) ? _engine.Options.HeroActionsPerRound : actor.ActionsPerRound;


        public bool Spend(int actions = 1)
        {
            if (IsOver || actions <= 0 || actions > _actionsLeft) return false;

            _actionsLeft -= actions;

            if (_actionsLeft == 0) Done();

            return true;
        }

        // the hero out of actions may still buy a third with a Nerve, so his turn waits rather than auto-ending
        void Done()
        {
            if (_actionsLeft > 0) return;

            if (ReferenceEquals(Acting, Hero) && Hero.Nerve > 0) return;

            Advance();
        }


        public bool Push()
        {
            if (IsOver || !AwaitingHero) return false;

            int was = Hero.Nerve;

            if (!Hero.SpendNerve()) return false;

            _actionsLeft++;
            _engine.Observer.NerveChanged(Hero, was, Hero.Nerve);

            return true;
        }

        public bool SpendNerve(Actor actor)
        {
            if (IsOver || actor == null) return false;

            int was = actor.Nerve;

            if (!actor.SpendNerve()) return false;

            _engine.Observer.NerveChanged(actor, was, actor.Nerve);
            return true;
        }

        public Trouble.Cost Accept(Actor actor, Attr @using)
        {
            if (IsOver || actor == null) return Trouble.Cost.Nothing;

            Trouble.Cost cost = Trouble.Lands(actor, @using);

            if (cost == Trouble.Cost.Condition)
                _engine.Observer.ConditionApplied(actor, @using.Pressing());

            int was = actor.Nerve;

            if (actor.GainNerve() > 0) _engine.Observer.NerveChanged(actor, was, actor.Nerve);

            if (actor.IsDown) _engine.Observer.ActorDowned(actor);

            if (Decided()) Finish();

            return cost;
        }

        public AttackOutcome Strike(Actor target, Attr attr, Skill skill)
        {
            Actor acting = Ready(target);

            if (acting == null) return null;

            AttackOutcome outcome = _engine.Attack(acting, target, attr, skill);

            return Land(outcome);
        }

        public AttackOutcome Strike(Actor target, PoolResult roll, int impact)
        {
            Actor acting = Ready(target);

            if (acting == null) return null;

            return Land(_engine.Resolve(acting, target, roll, impact));
        }

        Actor Ready(Actor target)
        {
            if (IsOver || _actionsLeft <= 0 || target == null || target.IsDown) return null;

            return Acting;
        }

        AttackOutcome Land(AttackOutcome outcome)
        {
            _engine.Apply(outcome);

            CheckPhases();

            _actionsLeft--;

            if (Decided()) { Advance(); return outcome; }

            Done();

            return outcome;
        }

        public void EndTurn()
        {
            if (IsOver || Round == 0) return;

            _actionsLeft = 0;
            Advance();
        }

        void Advance()
        {
            if (Decided()) { Finish(); return; }

            // one full cycle at most - if nobody's standing after a lap, the fight is over whatever Decided says
            for (int stepped = 0; stepped <= _order.Count; stepped++)
            {
                _turn++;

                if (_turn >= _order.Count)
                {
                    _turn = 0;
                    Round++;

                    if (Round > _engine.Options.MaxRounds) { Finish(); return; }

                    _reacted.Clear();

                    _engine.Observer.RoundBegan(Round);
                }

                if (!_order[_turn].IsDown) { BeginTurn(); return; }
            }

            Finish();
        }

        bool Decided() => Hero.IsDown || _foes.All(f => f.IsDown);

        void Finish()
        {
            if (IsOver) return;

            _actionsLeft = 0;

            Result = new EncounterResult(
                !Hero.IsDown && _foes.All(f => f.IsDown),
                Math.Min(Round, _engine.Options.MaxRounds),
                Hero.Vigor);

            _engine.Observer.EncounterEnded(Result);
        }

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            IsOver
                ? $"over: {Result}"
                : Round == 0
                    ? $"not begun: {Hero.DebugName} vs {_foes.Count}"
                    : $"round {Round}, {Acting?.DebugName} to act, {_actionsLeft} left";
    }
}
