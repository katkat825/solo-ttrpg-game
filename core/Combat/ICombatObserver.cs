using Core.Characters;

namespace Core.Combat
{
    public interface ICombatObserver
    {
        void RoundBegan(int round);

        // only the player-driven path fires this - Run has no turn order
        void TurnBegan(Actor actor, int round);

        void AttackResolved(AttackOutcome outcome);

        void ConditionApplied(Actor actor, Condition condition);

        void ActorDowned(Actor actor);

        void NerveChanged(Actor actor, int was, int now);

        void PhaseChanged(Actor actor);

        void EncounterEnded(EncounterResult result);
    }
}
