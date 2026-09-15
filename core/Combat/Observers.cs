using System;
using System.Collections.Generic;
using Core.Characters;
using Core.Dice;

namespace Core.Combat
{
    public sealed class NullCombatObserver : ICombatObserver
    {
        public static readonly NullCombatObserver Instance = new NullCombatObserver();

        NullCombatObserver() { }

        public void RoundBegan(int round) { }
        public void TurnBegan(Actor actor, int round) { }
        public void AttackResolved(AttackOutcome outcome) { }
        public void ConditionApplied(Actor actor, Condition condition) { }
        public void ActorDowned(Actor actor) { }
        public void NerveChanged(Actor actor, int was, int now) { }
        public void PhaseChanged(Actor actor) { }
        public void EncounterEnded(EncounterResult result) { }
    }

    // DebugName-based and not localized - a debug transcript, keep it off screen
    public sealed class RecordingCombatObserver : ICombatObserver
    {
        readonly List<string> _lines = new List<string>();
        readonly Action<string> _sink;

        public RecordingCombatObserver(Action<string> sink = null) => _sink = sink;

        public IReadOnlyList<string> Lines => _lines;

        public void Clear() => _lines.Clear();

        public void RoundBegan(int round) => Write($"-- round {round} --");

        public void TurnBegan(Actor actor, int round) => Write($"  {actor.DebugName} to act");

        public void AttackResolved(AttackOutcome outcome) => Write(outcome.ToString());

        public void ConditionApplied(Actor actor, Condition condition) =>
            Write($"   {actor.DebugName} is {condition} -> {condition.Affects()} now {actor.Attribute(condition.Affects()).Label()}");

        public void ActorDowned(Actor actor) => Write($"   {actor.DebugName} goes down");

        public void NerveChanged(Actor actor, int was, int now) =>
            Write($"   {actor.DebugName} nerve {was} -> {now}");

        public void PhaseChanged(Actor actor) =>
            Write($"   {actor.DebugName} CHANGES at {actor.Vigor}/{actor.MaxVigor} - " +
                  string.Join(", ", System.Linq.Enumerable.Select(
                      System.Enum.GetValues<Attr>(),
                      a => $"{a} {actor.Attribute(a).Label()}")));

        public void EncounterEnded(EncounterResult result) => Write(result.ToString());

        void Write(string line)
        {
            _lines.Add(line);
            _sink?.Invoke(line);
        }
    }

    public sealed class CompositeCombatObserver : ICombatObserver
    {
        readonly List<ICombatObserver> _observers;

        public CompositeCombatObserver(params ICombatObserver[] observers) =>
            _observers = new List<ICombatObserver>(observers ?? Array.Empty<ICombatObserver>());

        public IReadOnlyList<ICombatObserver> Observers => _observers;

        // dedup - the same observer twice would double the transcript and the animation
        public bool Add(ICombatObserver observer)
        {
            if (observer == null || _observers.Contains(observer)) return false;

            _observers.Add(observer);
            return true;
        }

        public bool Remove(ICombatObserver observer) => _observers.Remove(observer);

        // snapshot, so an observer that adds or removes itself mid-event can't break the loop
        ICombatObserver[] Watching => _observers.ToArray();

        public void RoundBegan(int round)
        {
            foreach (var o in Watching) o.RoundBegan(round);
        }

        public void TurnBegan(Actor actor, int round)
        {
            foreach (var o in Watching) o.TurnBegan(actor, round);
        }

        public void AttackResolved(AttackOutcome outcome)
        {
            foreach (var o in Watching) o.AttackResolved(outcome);
        }

        public void ConditionApplied(Actor actor, Condition condition)
        {
            foreach (var o in Watching) o.ConditionApplied(actor, condition);
        }

        public void ActorDowned(Actor actor)
        {
            foreach (var o in Watching) o.ActorDowned(actor);
        }

        public void NerveChanged(Actor actor, int was, int now)
        {
            foreach (var o in Watching) o.NerveChanged(actor, was, now);
        }

        public void PhaseChanged(Actor actor)
        {
            foreach (var o in Watching) o.PhaseChanged(actor);
        }

        public void EncounterEnded(EncounterResult result)
        {
            foreach (var o in Watching) o.EncounterEnded(result);
        }
    }
}
