using Rules.Characters;

namespace Rules.Combat
{
    // how the rules layer talks to anything watching, without knowing what is watching
    // the seam between rules and presentation - Godot implements it and animates what it's told
    // also serves debug transcripts, replay capture and a combat log
    // the rules narrate, and never reference the engine drawing them
    public interface ICombatObserver
    {
        void RoundBegan(int round);

        void AttackResolved(AttackOutcome outcome);

        void ConditionApplied(Actor actor, Condition condition);

        void ActorDowned(Actor actor);

        void EncounterEnded(EncounterResult result);
    }
}
