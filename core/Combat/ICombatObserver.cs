using Core.Characters;

namespace Core.Combat
{
    // how the rules layer talks to anything watching, without knowing what is watching
    // the seam between rules and presentation - Godot implements it and animates what it's told
    // also serves debug transcripts, replay capture and a combat log
    // the rules narrate, and never reference the engine drawing them
    public interface ICombatObserver
    {
        void RoundBegan(int round);

        // whose turn it is, and which round it is theirs in (COMBAT_LOOP.md C2/C3).
        //
        // Only the player-driven path fires this - CombatEngine.Run has no notion of a turn order,
        // it takes the hero's whole turn and then every foe's, which is right for a fight nobody
        // is watching. In front of a player, "it is the ogre's turn now" is the single most
        // important thing the table has to say, and it is what C3 draws on the board
        void TurnBegan(Actor actor, int round);

        void AttackResolved(AttackOutcome outcome);

        void ConditionApplied(Actor actor, Condition condition);

        void ActorDowned(Actor actor);

        // heroic effort, spent or banked (CORE_RULES.md section 7). The table draws the tokens
        // beside the piece from it, and the transcript says which way it went - which matters,
        // because the interesting half of Nerve is the GAINING: a player who took a complication
        // on purpose made a decision, and a log that only showed the spending would show the
        // consequence and hide the choice
        void NerveChanged(Actor actor, int was, int now);

        // a Dread at half Vigor turning into the second half of the fight (CORE_RULES.md section
        // 8). The table makes it look like something and the transcript records that it happened
        // EXACTLY at the threshold, which is the only part of a phase change that can be wrong
        // without anybody noticing
        void PhaseChanged(Actor actor);

        void EncounterEnded(EncounterResult result);
    }
}
