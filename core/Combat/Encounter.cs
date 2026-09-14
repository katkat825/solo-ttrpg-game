using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Resolution;

namespace Core.Combat
{
    // THE FIGHT WITH A PLAYER IN IT (COMBAT_LOOP.md, "the one thing that doesn't exist").
    //
    // `CombatEngine.Run` plays the hero automatically. That is right for the sim and wrong for the
    // game, where the player chooses. This is the other path: the round structure and the action
    // economy, driven from outside one action at a time. It YIELDS - it never asks anybody what to
    // do. `Acting` says whose turn it is and `ActionsLeft` says how much of it is left; the caller
    // supplies the choice, whether that caller is a mouse on a board or an auto-player in a sim
    // report.
    //
    // WHY IT IS IN core/ AND NOT IN A GODOT SCRIPT. Round structure and the action economy are
    // rules (CORE_RULES.md section 8) and the action economy in particular is the load-bearing
    // one - SIMULATION.md section 2 measures one action a round at a 4% win rate. A `game/` script
    // re-implementing turns would be the rules stated twice, with the second copy unsimulatable
    // and unreachable from a test.
    //
    // `Run` IS NOT TOUCHED, and must not be. If a change here forces a change there, the sim's
    // numbers move and SIMULATION.md is wrong until re-verified. What holds the two together is
    // `sim/Reports/PlayerPathReport.cs`, which plays this path with an auto-player that makes
    // exactly `Run`'s choices and prints both win rates side by side. They agree, and if they ever
    // stop, one of them has a bug.
    //
    // WHAT IT DOES NOT KNOW: geometry. It has no map, no squares and no reach - which foe is
    // adjacent, whether there is a way there, how far one move carries a piece are all questions
    // for whoever is holding the board. The caller decides who can be struck and calls `Strike`;
    // this decides whether there was an action left to do it with.
    public sealed class Encounter
    {
        readonly CombatEngine _engine;

        readonly List<Actor> _foes;

        // turn order, hero and foes together. one list, because "the hero, then everybody else"
        // stops being the answer the moment initiative is rolled (C3) and the shape of this file
        // should not have to change when it does
        readonly List<Actor> _order = new List<Actor>();

        // per-foe behaviour, falling back to the engine's. THIS is what the targeting seam is for:
        // C6 swaps a Dread's selector at its phase change and nothing else in the fight changes
        readonly Dictionary<Actor, ITargetSelector> _behaviour =
            new Dictionary<Actor, ITargetSelector>(ReferenceEqualityComparer.Instance);

        // what everybody rolled, so the table can write the order down the side of the map
        readonly Dictionary<Actor, int> _initiative =
            new Dictionary<Actor, int>(ReferenceEqualityComparer.Instance);

        // reactions spent this round, and who is watching for something to come within reach.
        // both are cleared at the top of a round, because a reaction is per round and a readied
        // action is until your next turn (CORE_RULES.md section 8)
        readonly Dictionary<Actor, int> _reacted =
            new Dictionary<Actor, int>(ReferenceEqualityComparer.Instance);

        readonly HashSet<Actor> _readied = new HashSet<Actor>(ReferenceEqualityComparer.Instance);

        // what turns into something else, and when (CORE_RULES.md section 8). Empty for every
        // fight that has no boss in it, which is every fight `CombatEngine.Run` can express -
        // so the two paths still agree about every encounter the sim measures
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

        // the order everybody acts in, once the fight has begun
        public IReadOnlyList<Actor> Order => _order;

        public int Round { get; private set; }

        public EncounterResult Result { get; private set; }

        public bool IsOver => Result != null;

        // whose turn it is, or null before Begin and after the last blow
        public Actor Acting => IsOver || _order.Count == 0 ? null : _order[_turn];

        public int ActionsLeft => IsOver ? 0 : _actionsLeft;

        public bool AwaitingHero => !IsOver && ReferenceEquals(Acting, Hero);

        // ---- what turns into something else (COMBAT_LOOP.md C6) ----

        // A BOSS AND WHAT IT BECOMES, attached from outside like its behaviour is - because what
        // a Dread turns into is a statblock and statblocks are content (Phase P)
        public void Phases(PhaseChange change)
        {
            if (change != null) _phases.Add(change);
        }

        public IReadOnlyList<PhaseChange> PhaseChanges => _phases;

        // ASKED AFTER EVERY BLOW, which is the only moment anybody's vigor can have moved. That is
        // what makes "exactly at the threshold" true rather than "some time after it"
        void CheckPhases()
        {
            foreach (PhaseChange phase in _phases)
            {
                if (!phase.Due || !phase.Turn()) continue;

                // the behaviour half. Attached through the same seam a campaign would use, so
                // nothing here is a special case for bosses
                if (phase.Then != null) Behaviour(phase.Actor, phase.Then);

                _engine.Observer.PhaseChanged(phase.Actor);
            }
        }

        // ---- behaviour, per foe ----

        // "targeting is policy, not rules - it changes per enemy, per campaign, per boss"
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

        // who this foe would go for. the hero is the only candidate today - CORE_RULES.md section
        // 13 keeps party members dead - but the selector is asked anyway, because the day a
        // campaign puts something else worth attacking on the board, nothing here should change
        public Actor TargetFor(Actor foe) => BehaviourOf(foe).Choose(foe, HeroOnly);

        Actor[] HeroOnly => new[] { Hero };

        // ---- the round ----

        // <paramref name="heroInitiative"/> is the hero's own roll, thrown on the real tray in
        // front of the player - because his dice are visible and the DM's are not (CORE_RULES.md
        // pillar 1, THE_TABLE.md). Left out, this rolls his through the resolver too, which is
        // what the sim and the tests want
        public void Begin(int? heroInitiative = null)
        {
            if (Round > 0) return;

            BuildOrder(heroInitiative);

            if (Decided()) { Finish(); return; }

            Round = 1;
            _turn = 0;
            _engine.Observer.RoundBegan(Round);

            // an order whose first actor is already down is possible the moment a campaign can
            // start a fight with somebody bleeding out in it
            if (_order[_turn].IsDown) Advance();
            else BeginTurn();
        }

        // ONE THROW OF Grace + Insight, AT THE TOP, AND NEVER AGAIN (CORE_RULES.md section 8, and
        // see Initiative.cs for why re-rolling each round is a pause with no decision in it).
        //
        // Ties go to the hero and then to the order they were mustered in. Both halves matter: a
        // tie the hero loses is the action economy quietly working against him on a coin flip,
        // and four Rabble all rolling nothing have to come out in the same order every time or the
        // same fight replays differently on the same seed
        void BuildOrder(int? heroInitiative)
        {
            _order.Clear();
            _initiative.Clear();

            var mustered = new List<Actor> { Hero };
            mustered.AddRange(_foes);

            // OFF MEANS OFF, INCLUDING THE THROW. Turning the ordering off and rolling anyway
            // would still move every seeded number after it, which is the whole thing this switch
            // exists to avoid - see CombatOptions.RollInitiative
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

        // what this actor threw for the order, or 0 before the fight began
        public int InitiativeOf(Actor actor) =>
            actor != null && _initiative.TryGetValue(actor, out int score) ? score : 0;

        void BeginTurn()
        {
            _actionsLeft = ActionsFor(Acting);

            // a readied action lasts until your next turn, and this is it
            _readied.Remove(Acting);

            _engine.Observer.TurnBegan(Acting, Round);
        }

        // ---- the reaction (CORE_RULES.md section 8) ----
        //
        // ONE FOR THE HERO AND NONE FOR AN ORDINARY ENEMY, which is half of what makes the hero a
        // hero rather than a person with better numbers. It is deliberately SMALL here: the only
        // trigger the game has is a readied strike - give up an action, watch, and hit the first
        // thing that comes within reach - because a reaction that fired on its own every round
        // would move every number in SIMULATION.md and there is nothing in the sim with a position
        // in it to re-measure them with (COMBAT_LOOP.md C3: don't build a full reaction system).
        //
        // WHAT IT ACTUALLY COSTS, measured rather than asserted: nothing, most of the time. The
        // hero HAS a reaction each round (CORE_RULES.md section 8) and readying is only how it is
        // armed, so ending a turn with no actions left arms it for free. What keeps that in
        // proportion is the trigger and not the price - something has to STEP INTO reach, which
        // happens once as a fight is joined and then never again, because after that everything
        // is already there. `check-fight.ps1 -Plain -Fights 10` measures ten fights and ten
        // strikes out of turn: one each, the Rival closing on the first round.

        public int ReactionsLeft(Actor actor)
        {
            if (actor == null) return 0;

            return ReactionsFor(actor) - (_reacted.TryGetValue(actor, out int used) ? used : 0);
        }

        // the hero's from the dial, everybody else's from the Actor - exactly as ActionsFor splits
        // it, and for exactly the same reason
        int ReactionsFor(Actor actor) =>
            ReferenceEquals(actor, Hero) ? _engine.Options.HeroReactionsPerRound : actor.ReactionsPerRound;

        // give up what is left of this turn and watch instead. Only the actor whose turn it is can
        // ready, and only one with a reaction to spend on it
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

        // A STRIKE OUT OF TURN. It costs a reaction rather than an action, does not advance the
        // turn, and can be taken by somebody whose turn it is not - which is the whole of what
        // makes it a reaction rather than a strike
        public AttackOutcome React(Actor reactor, Actor target, PoolResult roll, int impact)
        {
            if (IsOver || reactor == null || target == null || target.IsDown) return null;
            if (ReactionsLeft(reactor) <= 0 || reactor.IsDown) return null;

            _reacted[reactor] = (_reacted.TryGetValue(reactor, out int used) ? used : 0) + 1;
            _readied.Remove(reactor);

            AttackOutcome outcome = _engine.Resolve(reactor, target, roll, impact);
            _engine.Apply(outcome);

            CheckPhases();

            // it can end the fight, and then nothing else happens - but it never takes the turn
            // away from whoever was having one
            if (Decided()) Finish();

            return outcome;
        }

        // THE HERO'S COUNT IS THE OPTION AND EVERYBODY ELSE'S IS THEIR OWN. The hero acting more
        // than anyone else is the balance lever SIMULATION.md section 2 measured, so it lives on
        // the dial panel; a Dread's two actions are a property of being a Dread and live on the
        // Actor. Reading Actor.ActionsPerRound here is what stopped it being a field that was set
        // and never read (SEAMS.md section 3) - before this, Tier.Dread LOOKED like it acted
        // twice and did not
        int ActionsFor(Actor actor) =>
            ReferenceEquals(actor, Hero) ? _engine.Options.HeroActionsPerRound : actor.ActionsPerRound;

        // ---- what an actor does with its turn ----

        // one action, spent on something the rules have no opinion about - a move, a step back, a
        // door. The caller did the thing; this only keeps the books
        public bool Spend(int actions = 1)
        {
            if (IsOver || actions <= 0 || actions > _actionsLeft) return false;

            _actionsLeft -= actions;

            if (_actionsLeft == 0) Done();

            return true;
        }

        // A TURN ENDS ITSELF WHEN THERE IS NOTHING LEFT TO DECIDE, and not before.
        //
        // A foe out of actions has nothing else it could do, so its turn is over and the next one
        // begins - which is what keeps a room of eight from needing eight clicks to say "and now
        // nothing happens". The HERO out of actions may still have a Nerve, and a Nerve buys a
        // third action (CORE_RULES.md section 7). Ending his turn for him would spend that
        // decision on his behalf by taking the chance to make it away, so his turn waits for him
        // to say he is done - `EndTurn`, or `Ready` if he would rather watch.
        //
        // With no Nerve left there is nothing to wait for, and it ends itself like anybody's
        void Done()
        {
            if (_actionsLeft > 0) return;

            if (ReferenceEquals(Acting, Hero) && Hero.Nerve > 0) return;

            Advance();
        }

        // ---- heroic effort (CORE_RULES.md section 7) ----

        // ACT ONE MORE TIME. The fourth thing a Nerve buys, and the one that touches the action
        // economy - which is why it is here and not in `Nerve`, and why it is worth being careful
        // with: SIMULATION.md section 2 measures a third action a round at a 96% win rate against
        // 89% for two. That is not a small lever, and the only thing keeping it in proportion is
        // that a day has three Nerve in it
        public bool Push()
        {
            if (IsOver || !AwaitingHero) return false;

            int was = Hero.Nerve;

            if (!Hero.SpendNerve()) return false;

            _actionsLeft++;
            _engine.Observer.NerveChanged(Hero, was, Hero.Nerve);

            return true;
        }

        // ONE NERVE, ON SOMETHING THE ROUND STRUCTURE HAS NO OPINION ABOUT. Two of the four spends
        // are like that: re-throwing one die in a pool, and shrugging a Trouble off before it
        // resolves (CORE_RULES.md section 7). Neither touches the action economy and neither needs
        // the rules to know what happened - the tray re-throws a die, the caller drops a
        // consequence on the floor - so both are this one method rather than two that differ only
        // in their name.
        //
        // The two that DO touch something have their own: `Push` buys an action, and `Accept`
        // banks one in exchange for a complication landing
        public bool SpendNerve(Actor actor)
        {
            if (IsOver || actor == null) return false;

            int was = actor.Nerve;

            if (!actor.SpendNerve()) return false;

            _engine.Observer.NerveChanged(actor, was, actor.Nerve);
            return true;
        }

        // AND TAKE ONE INSTEAD, WHICH IS THE LOOP. The consequence lands and a Nerve is banked -
        // the game proposed something going wrong, and going wrong is how you afford going right
        // later (CORE_RULES.md section 7).
        //
        // <paramref name="using"/> is the attribute the throw was made with, because that is what
        // decides where the complication lands when there is no gear left to take it
        public Trouble.Cost Accept(Actor actor, Attr @using)
        {
            if (IsOver || actor == null) return Trouble.Cost.Nothing;

            Trouble.Cost cost = Trouble.Lands(actor, @using);

            if (cost == Trouble.Cost.Condition)
                _engine.Observer.ConditionApplied(actor, @using.Pressing());

            int was = actor.Nerve;

            if (actor.GainNerve() > 0) _engine.Observer.NerveChanged(actor, was, actor.Nerve);

            // a Condition can be one the ladder has no room for, and that finishes you
            // (CORE_RULES.md section 9, Actor.IsOverwhelmed)
            if (actor.IsDown) _engine.Observer.ActorDowned(actor);

            if (Decided()) Finish();

            return cost;
        }

        // a swing, with the dice thrown by the resolver. what a foe does, and what an auto-player
        // does in the sim
        public AttackOutcome Strike(Actor target, Attr attr, Skill skill)
        {
            Actor acting = Ready(target);

            if (acting == null) return null;

            AttackOutcome outcome = _engine.Attack(acting, target, attr, skill);

            return Land(outcome);
        }

        // and the same swing off dice that are already lying on the table. what the hero does,
        // because in front of a player every roll is a visible handful (CORE_RULES.md pillar 1)
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

            // the books, then the consequence: an actor whose blow ended the fight does not get
            // to spend the rest of its turn, and Advance is where "the fight is decided" is asked
            _actionsLeft--;

            if (Decided()) { Advance(); return outcome; }

            Done();

            return outcome;
        }

        // give up what is left of this turn. a pass, or a caller that has run out of anything
        // worth doing
        public void EndTurn()
        {
            if (IsOver || Round == 0) return;

            _actionsLeft = 0;
            Advance();
        }

        void Advance()
        {
            if (Decided()) { Finish(); return; }

            // one full cycle at most: if it comes all the way round without finding anybody
            // standing, the fight is over whatever Decided thinks
            for (int stepped = 0; stepped <= _order.Count; stepped++)
            {
                _turn++;

                if (_turn >= _order.Count)
                {
                    _turn = 0;
                    Round++;

                    // the safety net Run has, for the same reason: a rule change that made a fight
                    // unwinnable should end in a report rather than in a table nobody can leave
                    if (Round > _engine.Options.MaxRounds) { Finish(); return; }

                    // a reaction is one per ROUND, so the slate is wiped here and nowhere else
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

            // Round is the round the last blow landed in, which is what Run reports too. A fight
            // that ran out of rounds reports the cap rather than the round after it
            Result = new EncounterResult(
                !Hero.IsDown && _foes.All(f => f.IsDown),
                Math.Min(Round, _engine.Options.MaxRounds),
                Hero.Vigor);

            _engine.Observer.EncounterEnded(Result);
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() =>
            IsOver
                ? $"over: {Result}"
                : Round == 0
                    ? $"not begun: {Hero.DebugName} vs {_foes.Count}"
                    : $"round {Round}, {Acting?.DebugName} to act, {_actionsLeft} left";
    }
}
