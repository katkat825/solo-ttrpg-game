using System;
using System.Collections.Generic;
using Rules.Characters;
using Rules.Dice;

namespace Rules.Combat
{
    // the stock ICombatObserver implementations
    // one that swallows everything, one that transcribes, one that fans out
    // between them a fight can be watched, recorded and animated at once
    // the null one is the default, so nobody has to pass an observer
    public sealed class NullCombatObserver : ICombatObserver
    {
        public static readonly NullCombatObserver Instance = new NullCombatObserver();

        NullCombatObserver() { }

        public void RoundBegan(int round) { }
        public void AttackResolved(AttackOutcome outcome) { }
        public void ConditionApplied(Actor actor, Condition condition) { }
        public void ActorDowned(Actor actor) { }
        public void EncounterEnded(EncounterResult result) { }
    }

    // captures a readable transcript, blow by blow
    // re-run a suspect fight on the same seed with one of these attached
    // the lines are DebugName-based and not localized, so they stay off screen
    public sealed class RecordingCombatObserver : ICombatObserver
    {
        readonly List<string> _lines = new List<string>();
        readonly Action<string> _sink;

        // sink is optional live output, e.g. GD.Print or Console.WriteLine
        public RecordingCombatObserver(Action<string> sink = null) => _sink = sink;

        public IReadOnlyList<string> Lines => _lines;

        public void Clear() => _lines.Clear();

        public void RoundBegan(int round) => Write($"-- round {round} --");

        public void AttackResolved(AttackOutcome outcome) => Write(outcome.ToString());

        public void ConditionApplied(Actor actor, Condition condition) =>
            Write($"   {actor.DebugName} is {condition} -> {condition.Affects()} now {actor.Attribute(condition.Affects()).Label()}");

        public void ActorDowned(Actor actor) => Write($"   {actor.DebugName} goes down");

        public void EncounterEnded(EncounterResult result) => Write(result.ToString());

        void Write(string line)
        {
            _lines.Add(line);
            _sink?.Invoke(line);
        }
    }

    // sends the same events to several observers - debug while you animate
    public sealed class CompositeCombatObserver : ICombatObserver
    {
        readonly ICombatObserver[] _observers;

        public CompositeCombatObserver(params ICombatObserver[] observers) =>
            _observers = observers ?? Array.Empty<ICombatObserver>();

        public void RoundBegan(int round)
        {
            foreach (var o in _observers) o.RoundBegan(round);
        }

        public void AttackResolved(AttackOutcome outcome)
        {
            foreach (var o in _observers) o.AttackResolved(outcome);
        }

        public void ConditionApplied(Actor actor, Condition condition)
        {
            foreach (var o in _observers) o.ConditionApplied(actor, condition);
        }

        public void ActorDowned(Actor actor)
        {
            foreach (var o in _observers) o.ActorDowned(actor);
        }

        public void EncounterEnded(EncounterResult result)
        {
            foreach (var o in _observers) o.EncounterEnded(result);
        }
    }
}
