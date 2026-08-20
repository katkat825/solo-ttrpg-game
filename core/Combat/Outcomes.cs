using Core.Characters;
using Core.Resolution;

namespace Core.Combat
{
    // what the combat engine hands to an observer
    // immutable records of one swing and one whole fight
    // they carry actors and the raw roll, so a view can replay the detail
    public sealed class AttackOutcome
    {
        public Actor Attacker { get; }
        public Actor Target { get; }
        public PoolResult Roll { get; }
        public bool Hit { get; }
        public int Damage { get; }

        public AttackOutcome(Actor attacker, Actor target, PoolResult roll, bool hit, int damage)
        {
            Attacker = attacker;
            Target = target;
            Roll = roll;
            Hit = hit;
            Damage = damage;
        }

        // DEVELOPER ONLY - not localized, never shown to a player
        public override string ToString() =>
            $"{Attacker.DebugName} -> {Target.DebugName}: {Roll} => " +
            (Hit ? $"HIT for {Damage}" : "miss");
    }

    public sealed class EncounterResult
    {
        public bool HeroWon { get; }
        public int Rounds { get; }
        public int HeroVigorRemaining { get; }

        public EncounterResult(bool won, int rounds, int vigor)
        {
            HeroWon = won;
            Rounds = rounds;
            HeroVigorRemaining = vigor;
        }

        // DEVELOPER ONLY - not localized, never shown to a player
        public override string ToString() =>
            $"{(HeroWon ? "victory" : "defeat")} in {Rounds} rounds, {HeroVigorRemaining} vigor left";
    }
}
